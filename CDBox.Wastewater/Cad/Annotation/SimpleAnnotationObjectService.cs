using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using TCPipeAutoDraw.Core.Colors;
using TCPipeAutoDraw.Modules.NodeAnnotation;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace TCPipeAutoDraw.Modules.AnnotationHud
{
    internal static class SimpleAnnotationObjectService
    {
        private const string MetadataRecordName = "CDBoxSimpleAnnotation";
        private const string KindNode = "Node";
        private const string KindSurface = "SurfaceArea";

        public static void AttachNodeMetadata(Database db, Transaction tr, ObjectId sourceObjectId,
            IList<ObjectId> textObjectIds)
        {
            if (db == null || tr == null || textObjectIds == null || textObjectIds.Count == 0) return;
            string annotationId = Guid.NewGuid().ToString("D");
            string sourceHandle = ReadHandle(sourceObjectId);
            string[] roles = { "NodeNo", "WellDepth", "ShaftLength", "WellType" };
            for (int i = 0; i < textObjectIds.Count; i++)
            {
                Entity entity = tr.GetObject(textObjectIds[i], OpenMode.ForWrite, false) as Entity;
                if (entity == null) continue;
                WriteMetadata(tr, entity, new SimpleMetadata
                {
                    Kind = KindNode,
                    AnnotationId = annotationId,
                    Role = i < roles.Length ? roles[i] : "Text" + i.ToString(CultureInfo.InvariantCulture),
                    SourceHandle = sourceHandle
                });
            }
        }

        public static void AttachSurfaceMetadata(Database db, Transaction tr, ObjectId boundaryObjectId,
            ObjectId textObjectId, ObjectId leaderObjectId)
        {
            if (db == null || tr == null || textObjectId.IsNull) return;
            string annotationId = Guid.NewGuid().ToString("D");
            string sourceHandle = ReadHandle(boundaryObjectId);
            Entity text = tr.GetObject(textObjectId, OpenMode.ForWrite, false) as Entity;
            if (text != null)
            {
                WriteMetadata(tr, text, new SimpleMetadata
                {
                    Kind = KindSurface,
                    AnnotationId = annotationId,
                    Role = "MainText",
                    SourceHandle = sourceHandle
                });
            }
            if (!leaderObjectId.IsNull)
            {
                Entity leader = tr.GetObject(leaderObjectId, OpenMode.ForWrite, false) as Entity;
                if (leader != null)
                {
                    WriteMetadata(tr, leader, new SimpleMetadata
                    {
                        Kind = KindSurface,
                        AnnotationId = annotationId,
                        Role = "Leader",
                        SourceHandle = sourceHandle
                    });
                }
            }
        }

        internal static bool RefreshNodeAnnotationsForSourceHandles(Document doc,
            IEnumerable<string> sourceHandles)
        {
            if (doc == null || sourceHandles == null) return false;
            var dirty = new HashSet<string>(sourceHandles.Where(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);
            if (dirty.Count == 0) return false;
            bool changed = false;

            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                var groups = new Dictionary<string, List<Member>>(StringComparer.OrdinalIgnoreCase);
                foreach (ObjectId id in EnumerateCurrentSpace(doc.Database, tr))
                {
                    Entity entity;
                    try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                    catch { continue; }
                    SimpleMetadata metadata;
                    if (entity == null || !TryReadMetadata(tr, entity, out metadata)
                        || !string.Equals(metadata.Kind, KindNode, StringComparison.OrdinalIgnoreCase)
                        || !dirty.Contains(metadata.SourceHandle)) continue;
                    List<Member> members;
                    if (!groups.TryGetValue(metadata.AnnotationId, out members))
                    {
                        members = new List<Member>();
                        groups[metadata.AnnotationId] = members;
                    }
                    members.Add(new Member { ObjectId = id, Entity = entity, Metadata = metadata });
                }

                foreach (KeyValuePair<string, List<Member>> pair in groups)
                {
                    List<Member> members = pair.Value;
                    if (members == null || members.Count == 0) continue;
                    string sourceHandle = members[0].Metadata.SourceHandle;
                    ObjectId sourceId = ResolveHandle(doc.Database, sourceHandle);
                    QuantityPipeAttributes attributes;
                    if (sourceId.IsNull || !QuantityPipeAttributeService.TryReadSavedAttributes(
                        doc.Database, tr, sourceId, out attributes)
                        || attributes == null
                        || !QuantityPipeAttributes.IsNodeKind(attributes.ObjectKind)) continue;

                    Dictionary<string, string> desired =
                        NodeAnnotationService.BuildBoundAnnotationTexts(attributes);
                    if (desired.Count == 0) continue;
                    var existingRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < members.Count; i++)
                    {
                        Member member = members[i];
                        string role = member.Metadata.Role ?? string.Empty;
                        existingRoles.Add(role);
                        DBText text = tr.GetObject(member.ObjectId, OpenMode.ForWrite, false) as DBText;
                        if (text == null) continue;
                        string value;
                        if (desired.TryGetValue(role, out value))
                        {
                            if (!string.Equals(text.TextString, value, StringComparison.Ordinal))
                            {
                                text.TextString = value ?? string.Empty;
                                try { text.AdjustAlignment(doc.Database); } catch { }
                                changed = true;
                            }
                        }
                        else if (string.Equals(role, "WellType", StringComparison.OrdinalIgnoreCase))
                        {
                            text.Erase();
                            changed = true;
                        }
                    }

                    string wellTypeText;
                    if (desired.TryGetValue("WellType", out wellTypeText)
                        && !existingRoles.Contains("WellType"))
                    {
                        changed = CreateMissingNodeText(doc.Database, tr, members,
                            pair.Key, sourceHandle, "WellType", wellTypeText) || changed;
                    }
                }
                tr.Commit();
            }

            if (changed)
            {
                try { doc.Editor.Regen(); } catch { }
            }
            return changed;
        }

        internal static void RepairClonedAnnotations(Database db, Transaction tr,
            IDictionary<ObjectId, ObjectId> cloneMap,
            IDictionary<string, ObjectId> clonesByOriginalHandle,
            bool isCrossDatabase)
        {
            if (db == null || tr == null || cloneMap == null || cloneMap.Count == 0) return;
            var clonedHandlesByOriginal = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            if (clonesByOriginalHandle != null)
            {
                foreach (KeyValuePair<string, ObjectId> pair in clonesByOriginalHandle)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value.IsNull) continue;
                    try
                    {
                        Entity clone = tr.GetObject(pair.Value, OpenMode.ForRead, false) as Entity;
                        if (clone != null)
                            clonedHandlesByOriginal[pair.Key] = clone.Handle.ToString();
                    }
                    catch { }
                }
            }

            var annotationIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<ObjectId, ObjectId> pair in cloneMap)
            {
                if (pair.Value.IsNull) continue;
                Entity clone;
                try { clone = tr.GetObject(pair.Value, OpenMode.ForRead, false) as Entity; }
                catch { continue; }
                SimpleMetadata metadata;
                if (clone == null || !TryReadMetadata(tr, clone, out metadata)) continue;
                string newAnnotationId;
                if (!annotationIds.TryGetValue(metadata.AnnotationId, out newAnnotationId))
                {
                    newAnnotationId = Guid.NewGuid().ToString("D");
                    annotationIds[metadata.AnnotationId] = newAnnotationId;
                }
                string clonedSourceHandle;
                if (clonedHandlesByOriginal.TryGetValue(metadata.SourceHandle,
                    out clonedSourceHandle)) metadata.SourceHandle = clonedSourceHandle;
                else if (isCrossDatabase) metadata.SourceHandle = string.Empty;
                metadata.AnnotationId = newAnnotationId;
                try
                {
                    if (!clone.IsWriteEnabled) clone.UpgradeOpen();
                    WriteMetadata(tr, clone, metadata);
                }
                catch { }
            }
        }

        private static bool CreateMissingNodeText(Database db, Transaction tr,
            IList<Member> members, string annotationId, string sourceHandle,
            string role, string value)
        {
            if (db == null || tr == null || members == null || members.Count == 0) return false;
            List<Member> textMembers = members.Where(x => x != null && x.Entity is DBText)
                .OrderBy(x => NodeRoleOrder(x.Metadata.Role)).ToList();
            if (textMembers.Count == 0) return false;
            DBText first = textMembers[0].Entity as DBText;
            if (first == null) return false;
            DBText colorSource = textMembers.Count > 1 ? textMembers[1].Entity as DBText : first;
            Point3d basePoint = GetTextAnchor(first);
            double spacing = Math.Max(first.Height * NodeAnnotationOptions.Default.LineSpacingFactor,
                first.Height);
            if (textMembers.Count > 1)
            {
                int firstOrder = NodeRoleOrder(textMembers[0].Metadata.Role);
                int secondOrder = NodeRoleOrder(textMembers[1].Metadata.Role);
                int orderDistance = Math.Max(1, Math.Abs(secondOrder - firstOrder));
                double measured = Math.Abs(GetTextAnchor((DBText)textMembers[1].Entity).Y
                    - basePoint.Y) / orderDistance;
                if (measured > 0.000001) spacing = measured;
            }
            Point3d position = new Point3d(basePoint.X,
                basePoint.Y - spacing * NodeRoleOrder(role), basePoint.Z);
            BlockTableRecord owner = tr.GetObject(first.OwnerId, OpenMode.ForWrite, false)
                as BlockTableRecord;
            if (owner == null) return false;

            var text = new DBText();
            text.SetDatabaseDefaults(db);
            text.TextString = value ?? string.Empty;
            text.Height = first.Height;
            text.TextStyleId = first.TextStyleId;
            text.LayerId = first.LayerId;
            text.Color = colorSource == null ? first.Color : colorSource.Color;
            text.HorizontalMode = TextHorizontalMode.TextCenter;
            text.VerticalMode = TextVerticalMode.TextVerticalMid;
            text.Position = position;
            text.AlignmentPoint = position;
            owner.AppendEntity(text);
            tr.AddNewlyCreatedDBObject(text, true);
            try { text.AdjustAlignment(db); } catch { }
            WriteMetadata(tr, text, new SimpleMetadata
            {
                Kind = KindNode,
                AnnotationId = annotationId,
                Role = role,
                SourceHandle = sourceHandle
            });
            return true;
        }

        public static bool TryResolveAnnotationObject(Document doc, IEnumerable<ObjectId> selectedIds,
            out ObjectId annotationObjectId)
        {
            annotationObjectId = ObjectId.Null;
            if (doc == null || selectedIds == null) return false;
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in selectedIds)
                {
                    if (id.IsNull) continue;
                    Entity entity;
                    try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                    catch { continue; }
                    if (entity == null) continue;
                    SimpleMetadata metadata;
                    if (TryReadMetadata(tr, entity, out metadata)
                        && (metadata.Kind == KindNode || metadata.Kind == KindSurface))
                    {
                        annotationObjectId = id;
                        return true;
                    }
                    DBText text = entity as DBText;
                    if (text != null && (LooksLikeNodeText(text.TextString)
                        || TryFindLegacyNodeCluster(doc.Database, tr, text).Count > 1))
                    {
                        annotationObjectId = id;
                        return true;
                    }
                }
                tr.Commit();
            }
            return false;
        }

        public static SimpleAnnotationHudModel LoadEditModel(Document doc, ObjectId selectedObjectId)
        {
            if (doc == null || selectedObjectId.IsNull) return null;
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                Entity selected = tr.GetObject(selectedObjectId, OpenMode.ForRead, false) as Entity;
                if (selected == null) return null;
                SimpleMetadata metadata;
                SimpleAnnotationHudModel model;
                if (TryReadMetadata(tr, selected, out metadata))
                    model = LoadMetadataModel(doc.Database, tr, metadata);
                else
                    model = LoadLegacyModel(doc.Database, tr, selected as DBText);
                tr.Commit();
                return model;
            }
        }

        public static SimpleAnnotationHudModel SaveEditModel(Document doc, SimpleAnnotationHudModel model)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (model == null || model.Lines.Count == 0) throw new InvalidOperationException("标注对象无效。");
            if (model.TextHeight <= 0.0) throw new InvalidOperationException("文字高度必须大于 0。");
            if (string.IsNullOrWhiteSpace(model.AnnotationId)) model.AnnotationId = Guid.NewGuid().ToString("D");

            ObjectId reloadId = model.Lines[0].ObjectId;
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                ObjectId textStyleId = FindTextStyleId(doc.Database, tr, model.TextStyleName);
                if (textStyleId.IsNull) throw new InvalidOperationException("文字样式不存在：" + model.TextStyleName);
                EnsureLayerExists(doc.Database, tr, model.LayerName);
                ObjectId linetypeId = FindLinetypeId(doc.Database, tr, model.LinetypeName);
                string kind = model.Kind == SimpleAnnotationKind.Node ? KindNode : KindSurface;
                var originalAnchors = new List<Point3d>();
                double originalHeight = model.TextHeight;
                if (model.Kind == SimpleAnnotationKind.Node)
                {
                    for (int i = 0; i < model.Lines.Count; i++)
                    {
                        DBText current = tr.GetObject(model.Lines[i].ObjectId, OpenMode.ForRead, false) as DBText;
                        originalAnchors.Add(GetTextAnchor(current));
                        if (i == 0 && current != null) originalHeight = current.Height;
                    }
                }
                bool resizeNodeLayout = model.Kind == SimpleAnnotationKind.Node
                    && originalAnchors.Count == model.Lines.Count && originalHeight > 0.000001
                    && Math.Abs(originalHeight - model.TextHeight) > 0.000001;
                double nodeScale = resizeNodeLayout ? model.TextHeight / originalHeight : 1.0;
                Point3d nodeBase = originalAnchors.Count == 0 ? Point3d.Origin : originalAnchors[0];

                for (int i = 0; i < model.Lines.Count; i++)
                {
                    SimpleAnnotationTextLine line = model.Lines[i];
                    DBText text = tr.GetObject(line.ObjectId, OpenMode.ForWrite, false) as DBText;
                    if (text == null) continue;
                    text.TextString = line.Text ?? string.Empty;
                    text.Height = model.TextHeight;
                    text.TextStyleId = textStyleId;
                    text.Layer = model.LayerName;
                    if (resizeNodeLayout)
                    {
                        Vector3d offset = originalAnchors[i] - nodeBase;
                        Point3d target = nodeBase + offset.MultiplyBy(nodeScale);
                        text.Position = target;
                        try { text.AlignmentPoint = target; } catch { }
                    }
                    CDBoxColor selectedColor = model.Kind == SimpleAnnotationKind.Node && i > 0
                        ? model.SecondaryColor : model.PrimaryColor;
                    CDBoxColor original = CDBox.Wastewater.Infrastructure.WastewaterColorPort.FromCadColor(text.Color);
                    text.Color = CDBox.Wastewater.Infrastructure.WastewaterColorPort.ToCadColor(CDBox.Wastewater.Infrastructure.WastewaterColorPort.PrepareForWrite(selectedColor, original));
                    try { text.AdjustAlignment(doc.Database); } catch { }
                    WriteMetadata(tr, text, new SimpleMetadata
                    {
                        Kind = kind,
                        AnnotationId = model.AnnotationId,
                        Role = string.IsNullOrWhiteSpace(line.Role) ? "Text" + i : line.Role,
                        SourceHandle = model.SourceHandle
                    });
                }

                if (!model.LeaderObjectId.IsNull)
                {
                    Entity leader = tr.GetObject(model.LeaderObjectId, OpenMode.ForWrite, false) as Entity;
                    if (leader != null)
                    {
                        leader.Layer = model.LayerName;
                        CDBoxColor original = CDBox.Wastewater.Infrastructure.WastewaterColorPort.FromCadColor(leader.Color);
                        leader.Color = CDBox.Wastewater.Infrastructure.WastewaterColorPort.ToCadColor(CDBox.Wastewater.Infrastructure.WastewaterColorPort.PrepareForWrite(
                            model.SecondaryColor, original));
                        if (!linetypeId.IsNull) leader.LinetypeId = linetypeId;
                        leader.LineWeight = model.LineWeight;
                        WriteMetadata(tr, leader, new SimpleMetadata
                        {
                            Kind = kind,
                            AnnotationId = model.AnnotationId,
                            Role = "Leader",
                            SourceHandle = model.SourceHandle
                        });
                        if (model.Kind == SimpleAnnotationKind.SurfaceArea)
                            UpdateSurfaceLeaderUnderline(model.Lines[0].ObjectId, leader as Polyline, tr);
                    }
                }
                tr.Commit();
            }
            return LoadEditModel(doc, reloadId);
        }

        public static bool SelectionMatches(Document doc, SimpleAnnotationHudModel model,
            IEnumerable<ObjectId> selectedIds)
        {
            if (doc == null || model == null || selectedIds == null) return false;
            HashSet<ObjectId> modelIds = new HashSet<ObjectId>(model.GetObjectIds());
            foreach (ObjectId id in selectedIds)
            {
                if (modelIds.Contains(id)) return true;
            }
            return false;
        }

        private static SimpleAnnotationHudModel LoadMetadataModel(Database db, Transaction tr,
            SimpleMetadata selectedMetadata)
        {
            var members = new List<Member>();
            foreach (ObjectId id in EnumerateCurrentSpace(db, tr))
            {
                Entity entity;
                try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                catch { continue; }
                if (entity == null) continue;
                SimpleMetadata metadata;
                if (!TryReadMetadata(tr, entity, out metadata)
                    || !string.Equals(metadata.AnnotationId, selectedMetadata.AnnotationId,
                        StringComparison.OrdinalIgnoreCase)) continue;
                members.Add(new Member { ObjectId = id, Entity = entity, Metadata = metadata });
            }
            if (members.Count == 0) return null;

            bool isNode = selectedMetadata.Kind == KindNode;
            List<Member> textMembers = members.Where(x => x.Entity is DBText).ToList();
            if (isNode) textMembers.Sort((a, b) => NodeRoleOrder(a.Metadata.Role).CompareTo(NodeRoleOrder(b.Metadata.Role)));
            else textMembers.Sort((a, b) => string.Compare(a.Metadata.Role, b.Metadata.Role, StringComparison.OrdinalIgnoreCase));
            if (textMembers.Count == 0) return null;
            DBText first = (DBText)textMembers[0].Entity;
            Member leaderMember = members.FirstOrDefault(x => string.Equals(x.Metadata.Role, "Leader", StringComparison.OrdinalIgnoreCase));
            var model = CreateBaseModel(db, tr, first, isNode ? SimpleAnnotationKind.Node : SimpleAnnotationKind.SurfaceArea);
            model.AnnotationId = selectedMetadata.AnnotationId;
            model.SourceHandle = selectedMetadata.SourceHandle ?? string.Empty;
            model.SourceObjectId = ResolveHandle(db, model.SourceHandle);
            model.Header = isNode ? "节点标注" : "表面积标注";
            model.Summary = BuildSummary(db, tr, model.SourceObjectId, isNode ? "无引线" : "面积注记");
            for (int i = 0; i < textMembers.Count; i++)
            {
                DBText text = (DBText)textMembers[i].Entity;
                model.Lines.Add(new SimpleAnnotationTextLine
                {
                    ObjectId = text.ObjectId,
                    Role = textMembers[i].Metadata.Role,
                    Label = GetLineLabel(textMembers[i].Metadata.Role, text.TextString, isNode),
                    Text = text.TextString ?? string.Empty,
                    Color = CDBox.Wastewater.Infrastructure.WastewaterColorPort.FromCadColor(text.Color)
                });
            }
            if (leaderMember != null)
            {
                model.LeaderObjectId = leaderMember.ObjectId;
                model.SecondaryColor = CDBox.Wastewater.Infrastructure.WastewaterColorPort.FromCadColor(leaderMember.Entity.Color);
                model.LinetypeName = ReadLinetypeName(tr, leaderMember.Entity.LinetypeId);
                model.LineWeight = leaderMember.Entity.LineWeight;
                Curve leader = leaderMember.Entity as Curve;
                if (leader != null) model.AnchorPoint = SafeCurveStart(leader, GetTextAnchor(first));
            }
            else if (isNode && textMembers.Count > 1)
                model.SecondaryColor = CDBox.Wastewater.Infrastructure.WastewaterColorPort.FromCadColor(textMembers[1].Entity.Color);
            return model;
        }

        private static SimpleAnnotationHudModel LoadLegacyModel(Database db, Transaction tr, DBText selected)
        {
            if (selected == null) return null;
            List<DBText> cluster = TryFindLegacyNodeCluster(db, tr, selected);
            if (cluster.Count < 2) return null;
            var node = CreateBaseModel(db, tr, cluster[0], SimpleAnnotationKind.Node);
            node.AnnotationId = Guid.NewGuid().ToString("D");
            node.Header = "节点标注";
            node.Summary = "无引线 · 旧注记保存后纳入统一浮窗管理";
            for (int i = 0; i < cluster.Count; i++)
            {
                string role = InferNodeRole(cluster[i].TextString, i);
                node.Lines.Add(new SimpleAnnotationTextLine
                {
                    ObjectId = cluster[i].ObjectId,
                    Role = role,
                    Label = GetLineLabel(role, cluster[i].TextString, true),
                    Text = cluster[i].TextString ?? string.Empty,
                    Color = CDBox.Wastewater.Infrastructure.WastewaterColorPort.FromCadColor(cluster[i].Color)
                });
            }
            if (cluster.Count > 1) node.SecondaryColor = CDBox.Wastewater.Infrastructure.WastewaterColorPort.FromCadColor(cluster[1].Color);
            return node;
        }

        private static SimpleAnnotationHudModel CreateBaseModel(Database db, Transaction tr, DBText text,
            SimpleAnnotationKind kind)
        {
            return new SimpleAnnotationHudModel
            {
                Kind = kind,
                AnchorPoint = GetTextAnchor(text),
                TextHeight = text.Height,
                TextStyleName = ReadTextStyleName(tr, text.TextStyleId),
                LayerName = text.Layer,
                PrimaryColor = CDBox.Wastewater.Infrastructure.WastewaterColorPort.FromCadColor(text.Color),
                SecondaryColor = CDBoxColor.FromIndex(7),
                TextStyleNames = ReadTextStyles(db, tr),
                LayerNames = ReadLayers(db, tr),
                LinetypeNames = ReadLinetypes(db, tr)
            };
        }

        private static List<DBText> TryFindLegacyNodeCluster(Database db, Transaction tr, DBText selected)
        {
            var nearby = new List<DBText>();
            if (selected == null) return nearby;
            Point3d selectedPoint = GetTextAnchor(selected);
            double height = Math.Max(selected.Height, 0.01);
            foreach (ObjectId id in EnumerateCurrentSpace(db, tr))
            {
                DBText text;
                try { text = tr.GetObject(id, OpenMode.ForRead, false) as DBText; }
                catch { continue; }
                if (text == null || !string.Equals(text.Layer, selected.Layer, StringComparison.OrdinalIgnoreCase)
                    || text.TextStyleId != selected.TextStyleId || Math.Abs(text.Height - selected.Height) > height * 0.08) continue;
                Point3d point = GetTextAnchor(text);
                if (Math.Abs(point.X - selectedPoint.X) > Math.Max(height * 0.35, 0.05)
                    || Math.Abs(point.Y - selectedPoint.Y) > height * 6.5) continue;
                nearby.Add(text);
            }
            bool hasNodeValue = nearby.Any(x => LooksLikeNodeText(x.TextString));
            if (!hasNodeValue) return new List<DBText>();
            nearby.Sort((a, b) => GetTextAnchor(b).Y.CompareTo(GetTextAnchor(a).Y));
            if (nearby.Count > 4)
            {
                nearby = nearby.OrderBy(x => Math.Abs(GetTextAnchor(x).Y - selectedPoint.Y)).Take(4).ToList();
                nearby.Sort((a, b) => GetTextAnchor(b).Y.CompareTo(GetTextAnchor(a).Y));
            }
            return nearby;
        }

        private static void UpdateSurfaceLeaderUnderline(ObjectId textId, Polyline leader,
            Transaction tr)
        {
            if (leader == null || leader.NumberOfVertices < 3 || textId.IsNull) return;
            DBText text = tr.GetObject(textId, OpenMode.ForRead, false) as DBText;
            if (text == null) return;
            try
            {
                Extents3d extents = text.GeometricExtents;
                double minX = Math.Min(extents.MinPoint.X, extents.MaxPoint.X);
                double maxX = Math.Max(extents.MinPoint.X, extents.MaxPoint.X);
                Point2d start = leader.GetPoint2dAt(0);
                Point2d join = leader.GetPoint2dAt(leader.NumberOfVertices - 2);
                Point2d far = leader.GetPoint2dAt(leader.NumberOfVertices - 1);
                bool fromRight = start.X > (minX + maxX) / 2.0;
                leader.SetPointAt(leader.NumberOfVertices - 2,
                    new Point2d(fromRight ? maxX : minX, join.Y));
                leader.SetPointAt(leader.NumberOfVertices - 1,
                    new Point2d(fromRight ? minX : maxX, far.Y));
            }
            catch { }
        }

        private static bool TryReadMetadata(Transaction tr, Entity entity, out SimpleMetadata metadata)
        {
            metadata = null;
            if (tr == null || entity == null || entity.ExtensionDictionary.IsNull) return false;
            try
            {
                DBDictionary dictionary = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForRead, false) as DBDictionary;
                if (dictionary == null || !dictionary.Contains(MetadataRecordName)) return false;
                Xrecord record = tr.GetObject(dictionary.GetAt(MetadataRecordName), OpenMode.ForRead, false) as Xrecord;
                if (record == null || record.Data == null) return false;
                var parsed = new SimpleMetadata();
                foreach (TypedValue value in record.Data)
                {
                    string text = value.Value as string;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    int split = text.IndexOf('=');
                    if (split <= 0) continue;
                    string key = text.Substring(0, split);
                    string val = text.Substring(split + 1);
                    if (key == "Kind") parsed.Kind = val;
                    else if (key == "AnnotationId") parsed.AnnotationId = val;
                    else if (key == "Role") parsed.Role = val;
                    else if (key == "SourceHandle") parsed.SourceHandle = val;
                }
                if (string.IsNullOrWhiteSpace(parsed.Kind) || string.IsNullOrWhiteSpace(parsed.AnnotationId)) return false;
                metadata = parsed;
                return true;
            }
            catch { return false; }
        }

        private static void WriteMetadata(Transaction tr, Entity entity, SimpleMetadata metadata)
        {
            if (tr == null || entity == null || metadata == null) return;
            if (entity.ExtensionDictionary.IsNull) entity.CreateExtensionDictionary();
            DBDictionary dictionary = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite, false) as DBDictionary;
            if (dictionary == null) return;
            Xrecord record;
            if (dictionary.Contains(MetadataRecordName))
                record = tr.GetObject(dictionary.GetAt(MetadataRecordName), OpenMode.ForWrite, false) as Xrecord;
            else
            {
                record = new Xrecord();
                dictionary.SetAt(MetadataRecordName, record);
                tr.AddNewlyCreatedDBObject(record, true);
            }
            if (record == null) return;
            record.Data = new ResultBuffer(
                new TypedValue((int)DxfCode.Text, "Version=1"),
                new TypedValue((int)DxfCode.Text, "Kind=" + (metadata.Kind ?? string.Empty)),
                new TypedValue((int)DxfCode.Text, "AnnotationId=" + (metadata.AnnotationId ?? string.Empty)),
                new TypedValue((int)DxfCode.Text, "Role=" + (metadata.Role ?? string.Empty)),
                new TypedValue((int)DxfCode.Text, "SourceHandle=" + (metadata.SourceHandle ?? string.Empty)));
        }

        private static IEnumerable<ObjectId> EnumerateCurrentSpace(Database db, Transaction tr)
        {
            BlockTableRecord space = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead, false) as BlockTableRecord;
            if (space == null) yield break;
            foreach (ObjectId id in space) yield return id;
        }

        private static string BuildSummary(Database db, Transaction tr, ObjectId sourceId, string fallback)
        {
            if (sourceId.IsNull) return fallback;
            try
            {
                Entity source = tr.GetObject(sourceId, OpenMode.ForRead, false) as Entity;
                if (source != null) return fallback + " · 来源图层：" + source.Layer;
            }
            catch { }
            return fallback;
        }

        private static string ReadHandle(ObjectId id)
        {
            if (id.IsNull) return string.Empty;
            try { return id.Handle.ToString(); }
            catch { return string.Empty; }
        }

        private static ObjectId ResolveHandle(Database db, string handleText)
        {
            if (db == null || string.IsNullOrWhiteSpace(handleText)) return ObjectId.Null;
            try
            {
                long value;
                if (!long.TryParse(handleText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) return ObjectId.Null;
                return db.GetObjectId(false, new Handle(value), 0);
            }
            catch { return ObjectId.Null; }
        }

        private static Point3d GetTextAnchor(DBText text)
        {
            if (text == null) return Point3d.Origin;
            try
            {
                if (text.HorizontalMode != TextHorizontalMode.TextLeft || text.VerticalMode != TextVerticalMode.TextBase)
                    return text.AlignmentPoint;
            }
            catch { }
            return text.Position;
        }

        private static Point3d SafeCurveStart(Curve curve, Point3d fallback)
        {
            try { return curve.StartPoint; }
            catch { return fallback; }
        }

        private static bool LooksLikeNodeText(string text)
        {
            string value = (text ?? string.Empty).Trim();
            return value.StartsWith("井深", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("井筒", StringComparison.OrdinalIgnoreCase)
                || value.IndexOf("沉泥井", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("检查井", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string InferNodeRole(string text, int index)
        {
            string value = (text ?? string.Empty).Trim();
            if (value.StartsWith("井深", StringComparison.OrdinalIgnoreCase)) return "WellDepth";
            if (value.StartsWith("井筒", StringComparison.OrdinalIgnoreCase)) return "ShaftLength";
            if (value.IndexOf("井", StringComparison.OrdinalIgnoreCase) >= 0 && index > 0) return "WellType";
            return index == 0 ? "NodeNo" : "Text" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static int NodeRoleOrder(string role)
        {
            if (string.Equals(role, "NodeNo", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(role, "WellDepth", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(role, "ShaftLength", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(role, "WellType", StringComparison.OrdinalIgnoreCase)) return 3;
            return 10;
        }

        private static string GetLineLabel(string role, string text, bool isNode)
        {
            if (!isNode) return "注记文字";
            if (string.Equals(role, "NodeNo", StringComparison.OrdinalIgnoreCase)) return "节点编号";
            if (string.Equals(role, "WellDepth", StringComparison.OrdinalIgnoreCase)) return "井深文字";
            if (string.Equals(role, "ShaftLength", StringComparison.OrdinalIgnoreCase)) return "井筒文字";
            if (string.Equals(role, "WellType", StringComparison.OrdinalIgnoreCase)) return "井类型";
            return "补充文字";
        }

        private static string ReadTextStyleName(Transaction tr, ObjectId id)
        {
            try
            {
                TextStyleTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as TextStyleTableRecord;
                return record == null ? string.Empty : record.Name;
            }
            catch { return string.Empty; }
        }

        private static string ReadLinetypeName(Transaction tr, ObjectId id)
        {
            try
            {
                LinetypeTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as LinetypeTableRecord;
                return record == null ? "ByLayer" : record.Name;
            }
            catch { return "ByLayer"; }
        }

        private static List<string> ReadTextStyles(Database db, Transaction tr)
        {
            var result = new List<string>();
            TextStyleTable table = tr.GetObject(db.TextStyleTableId, OpenMode.ForRead, false) as TextStyleTable;
            if (table != null)
            {
                foreach (ObjectId id in table)
                {
                    TextStyleTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as TextStyleTableRecord;
                    if (record != null && !string.IsNullOrWhiteSpace(record.Name)) result.Add(record.Name);
                }
            }
            result.Sort(StringComparer.CurrentCultureIgnoreCase);
            return result;
        }

        private static List<string> ReadLayers(Database db, Transaction tr)
        {
            var result = new List<string>();
            LayerTable table = tr.GetObject(db.LayerTableId, OpenMode.ForRead, false) as LayerTable;
            if (table != null)
            {
                foreach (ObjectId id in table)
                {
                    LayerTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as LayerTableRecord;
                    if (record != null && !string.IsNullOrWhiteSpace(record.Name)) result.Add(record.Name);
                }
            }
            result.Sort(StringComparer.CurrentCultureIgnoreCase);
            return result;
        }

        private static List<string> ReadLinetypes(Database db, Transaction tr)
        {
            var result = new List<string>();
            LinetypeTable table = tr.GetObject(db.LinetypeTableId, OpenMode.ForRead, false) as LinetypeTable;
            if (table != null)
            {
                foreach (ObjectId id in table)
                {
                    LinetypeTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as LinetypeTableRecord;
                    if (record != null && !string.IsNullOrWhiteSpace(record.Name)) result.Add(record.Name);
                }
            }
            result.Sort(StringComparer.CurrentCultureIgnoreCase);
            return result;
        }

        private static ObjectId FindTextStyleId(Database db, Transaction tr, string name)
        {
            TextStyleTable table = tr.GetObject(db.TextStyleTableId, OpenMode.ForRead, false) as TextStyleTable;
            return table != null && !string.IsNullOrWhiteSpace(name) && table.Has(name) ? table[name] : ObjectId.Null;
        }

        private static ObjectId FindLinetypeId(Database db, Transaction tr, string name)
        {
            LinetypeTable table = tr.GetObject(db.LinetypeTableId, OpenMode.ForRead, false) as LinetypeTable;
            if (table == null) return ObjectId.Null;
            if (!string.IsNullOrWhiteSpace(name) && table.Has(name)) return table[name];
            return table.Has("ByLayer") ? table["ByLayer"] : ObjectId.Null;
        }

        private static void EnsureLayerExists(Database db, Transaction tr, string name)
        {
            LayerTable table = tr.GetObject(db.LayerTableId, OpenMode.ForRead, false) as LayerTable;
            if (table == null || string.IsNullOrWhiteSpace(name) || !table.Has(name))
                throw new InvalidOperationException("标注图层不存在：" + name);
        }

        private sealed class SimpleMetadata
        {
            public string Kind { get; set; }
            public string AnnotationId { get; set; }
            public string Role { get; set; }
            public string SourceHandle { get; set; }
        }

        private sealed class Member
        {
            public ObjectId ObjectId { get; set; }
            public Entity Entity { get; set; }
            public SimpleMetadata Metadata { get; set; }
        }
    }
}
