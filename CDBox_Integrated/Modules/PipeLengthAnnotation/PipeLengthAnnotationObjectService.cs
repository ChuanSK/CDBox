using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using TCPipeAutoDraw.Core.Colors;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    /// <summary>
    /// 阶段 B 管线长度标注对象化服务。
    /// 图形仍由标准 DBText/Polyline 构成；本服务仅附加 DWG 内元数据并建立原生 Group。
    /// </summary>
    public static class PipeLengthAnnotationObjectService
    {
        internal const string ObjectIdentityXrecordName = "CDBoxObjectIdentity";
        internal const string AnnotationSourceXrecordName = "CDBoxAnnotationSource";
        private const string MetadataVersion = "2";
        private const string AnnotationType = "PipeLength";
        private const string GroupNamePrefix = "CDBOX_PIPE_LENGTH_";

        public static void BindNewAnnotation(Database db, Transaction tr, ObjectId pipeId, PipeLengthAnnotationResult result)
        {
            if (db == null) throw new ArgumentNullException("db");
            if (tr == null) throw new ArgumentNullException("tr");
            if (pipeId.IsNull) throw new ArgumentException("源管线对象无效。", "pipeId");
            if (result == null) throw new ArgumentNullException("result");
            if (result.AnnotationObjectId.IsNull) throw new InvalidOperationException("管线长度标注缺少主文字对象。 ");

            string sourceObjectId = EnsureStablePipeObjectId(db, tr, pipeId);
            string annotationId = Guid.NewGuid().ToString("D");
            string groupName = BuildUniqueGroupName(db, tr, annotationId);

            var memberIds = new List<ObjectId>();
            memberIds.Add(result.AnnotationObjectId);
            if (!result.BottomAnnotationObjectId.IsNull) memberIds.Add(result.BottomAnnotationObjectId);
            if (!result.LeaderObjectId.IsNull) memberIds.Add(result.LeaderObjectId);

            ObjectId groupId = CreateNativeGroup(db, tr, groupName, annotationId, memberIds);

            result.AnnotationId = annotationId;
            result.SourceCDBoxObjectId = sourceObjectId;
            result.AnnotationGroupName = groupName;
            result.AnnotationGroupObjectId = groupId;

            WriteAnnotationMetadata(tr, result.AnnotationObjectId, result, "MainText");
            if (!result.BottomAnnotationObjectId.IsNull)
            {
                WriteAnnotationMetadata(tr, result.BottomAnnotationObjectId, result, "SecondaryText");
            }

            if (!result.LeaderObjectId.IsNull)
            {
                WriteAnnotationMetadata(tr, result.LeaderObjectId, result, "LeaderLine");
            }
        }

        public static void MoveAnnotation(Document doc)
        {
            if (doc == null) return;
            Editor ed = doc.Editor;

            var options = new PromptEntityOptions("\n选择需要整体移动的 CDBox 管线长度标注：");
            PromptEntityResult selected = ed.GetEntity(options);
            if (selected.Status != PromptStatus.OK) return;

            AnnotationMetadata selectedMetadata;
            using (Transaction read = doc.Database.TransactionManager.StartTransaction())
            {
                Entity entity = read.GetObject(selected.ObjectId, OpenMode.ForRead, false) as Entity;
                if (entity == null || !TryReadAnnotationMetadata(read, entity, out selectedMetadata))
                {
                    ed.WriteMessage("\n[CDBox 标注整体移动] 所选对象不是已对象化的管线长度标注。 ");
                    return;
                }
                read.Commit();
            }

            var destinationOptions = new PromptPointOptions("\n指定标注新位置：");
            destinationOptions.UseBasePoint = true;
            destinationOptions.BasePoint = selected.PickedPoint;
            PromptPointResult destination = ed.GetPoint(destinationOptions);
            if (destination.Status != PromptStatus.OK) return;

            Vector3d displacement = destination.Value - selected.PickedPoint;
            displacement = new Vector3d(displacement.X, displacement.Y, 0.0);
            if (displacement.Length < 0.0000001)
            {
                ed.WriteMessage("\n[CDBox 标注整体移动] 位置未变化。 ");
                return;
            }

            int movedCount = 0;
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, selectedMetadata.AnnotationId);
                if (members.Count == 0)
                {
                    ed.WriteMessage("\n[CDBox 标注整体移动] 未找到同一标注的组成对象。 ");
                    return;
                }

                foreach (AnnotationMember member in members)
                {
                    Entity entity;
                    try { entity = tr.GetObject(member.ObjectId, OpenMode.ForWrite, false) as Entity; }
                    catch { continue; }
                    if (entity == null || entity.IsErased) continue;

                    if (string.Equals(member.Metadata.AnnotationPart, "LeaderLine", StringComparison.OrdinalIgnoreCase))
                    {
                        MoveLeaderKeepingSourceAnchor(entity, displacement);
                    }
                    else
                    {
                        entity.TransformBy(Matrix3d.Displacement(displacement));
                    }
                    movedCount++;
                }

                tr.Commit();
            }

            ed.Regen();
            ed.WriteMessage("\n[CDBox 标注整体移动] 已移动 " + movedCount.ToString(CultureInfo.InvariantCulture)
                + " 个组成对象；源管线引线锚点保持不变。 ");
        }

        public static void DiagnoseAssociation(Document doc)
        {
            if (doc == null) return;
            Editor ed = doc.Editor;
            var options = new PromptEntityOptions("\n选择需要诊断的 CDBox 管线或管线长度标注：");
            PromptEntityResult selected = ed.GetEntity(options);
            if (selected.Status != PromptStatus.OK) return;

            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                Entity entity = tr.GetObject(selected.ObjectId, OpenMode.ForRead, false) as Entity;
                if (entity == null)
                {
                    ed.WriteMessage("\n[CDBox 关联诊断] 无法读取所选对象。 ");
                    return;
                }

                AnnotationMetadata annotation;
                if (TryReadAnnotationMetadata(tr, entity, out annotation))
                {
                    ed.WriteMessage(BuildAnnotationDiagnostic(doc.Database, tr, annotation));
                    tr.Commit();
                    return;
                }

                ObjectIdentity identity;
                if (TryReadObjectIdentity(tr, entity, out identity))
                {
                    ed.WriteMessage(BuildSourceDiagnostic(doc.Database, tr, selected.ObjectId, identity));
                    tr.Commit();
                    return;
                }

                ed.WriteMessage("\n[CDBox 关联诊断] 所选对象没有阶段 B 对象身份或标注元数据。 ");
                tr.Commit();
            }
        }

        public static bool TryResolveAnnotationObject(Document doc, ObjectId[] candidateIds, out ObjectId annotationObjectId)
        {
            annotationObjectId = ObjectId.Null;
            if (doc == null || candidateIds == null || candidateIds.Length == 0) return false;
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                for (int i = 0; i < candidateIds.Length; i++)
                {
                    Entity entity;
                    try { entity = tr.GetObject(candidateIds[i], OpenMode.ForRead, false) as Entity; }
                    catch { continue; }
                    AnnotationMetadata metadata;
                    if (entity != null && TryReadAnnotationMetadata(tr, entity, out metadata))
                    {
                        annotationObjectId = candidateIds[i];
                        tr.Commit();
                        return true;
                    }
                }
                tr.Commit();
            }
            return false;
        }

        internal static ObjectId[] GetAnnotationObjectIds(Document doc, string annotationId)
        {
            if (doc == null || !IsGuid(annotationId)) return new ObjectId[0];
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, annotationId);
                var ids = new List<ObjectId>();
                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i] != null && !members[i].ObjectId.IsNull) ids.Add(members[i].ObjectId);
                }
                tr.Commit();
                return ids.ToArray();
            }
        }

        internal static bool TryGetAnnotationPart(Entity entity, out string annotationId, out string annotationPart)
        {
            annotationId = string.Empty;
            annotationPart = string.Empty;
            if (entity == null || entity.Database == null || entity.ExtensionDictionary.IsNull) return false;
            Transaction top = entity.Database.TransactionManager.TopTransaction;
            if (top != null)
            {
                AnnotationMetadata metadata;
                if (!TryReadAnnotationMetadata(top, entity, out metadata)) return false;
                annotationId = metadata.AnnotationId;
                annotationPart = metadata.AnnotationPart;
                return true;
            }

            using (Transaction tr = entity.Database.TransactionManager.StartOpenCloseTransaction())
            {
                AnnotationMetadata metadata;
                bool result = TryReadAnnotationMetadata(tr, entity, out metadata);
                if (result)
                {
                    annotationId = metadata.AnnotationId;
                    annotationPart = metadata.AnnotationPart;
                }
                tr.Commit();
                return result;
            }
        }

        internal static bool TryGetAnnotationId(ObjectId objectId, out string annotationId)
        {
            annotationId = string.Empty;
            if (objectId.IsNull || objectId.Database == null) return false;
            using (Transaction tr = objectId.Database.TransactionManager.StartOpenCloseTransaction())
            {
                Entity entity;
                try { entity = tr.GetObject(objectId, OpenMode.ForRead, false) as Entity; }
                catch { return false; }
                AnnotationMetadata metadata;
                if (entity == null || !TryReadAnnotationMetadata(tr, entity, out metadata)) return false;
                annotationId = metadata.AnnotationId;
                tr.Commit();
                return true;
            }
        }

        internal static ObjectId GetAnnotationLeaderId(Document doc, string annotationId)
        {
            if (doc == null || !IsGuid(annotationId)) return ObjectId.Null;
            using (Transaction tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, annotationId);
                AnnotationMember leader = FindMember(members, "LeaderLine");
                ObjectId result = leader == null ? ObjectId.Null : leader.ObjectId;
                tr.Commit();
                return result;
            }
        }

        internal static void SynchronizeTextGrip(Document doc, ObjectId movedTextId)
        {
            if (doc == null || movedTextId.IsNull) return;
            ObjectId refreshId = movedTextId;
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                DBText movedText = tr.GetObject(movedTextId, OpenMode.ForRead, false) as DBText;
                AnnotationMetadata movedMetadata;
                if (movedText == null || !TryReadAnnotationMetadata(tr, movedText, out movedMetadata)) return;
                List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, movedMetadata.AnnotationId);
                AnnotationMember leaderMember = FindMember(members, "LeaderLine");
                if (leaderMember == null) return;
                Polyline leader = tr.GetObject(leaderMember.ObjectId, OpenMode.ForWrite, false) as Polyline;
                if (leader == null || leader.NumberOfVertices < 3) return;

                Point3d join = leader.GetPoint3dAt(1);
                Point3d far = leader.GetPoint3dAt(leader.NumberOfVertices - 1);
                double centerX = (join.X + far.X) / 2.0;
                double lineY = (join.Y + far.Y) / 2.0;
                double gap = Math.Max(movedText.Height * 0.22, 0.05);
                Point3d expected = string.Equals(movedMetadata.AnnotationPart, "SecondaryText", StringComparison.OrdinalIgnoreCase)
                    ? new Point3d(centerX, lineY - gap - movedText.Height, movedText.Position.Z)
                    : new Point3d(centerX, lineY + gap, movedText.Position.Z);
                Point3d actual = GetTextAnchor(movedText);
                Vector3d offset = actual - expected;
                offset = new Vector3d(offset.X, offset.Y, 0.0);
                if (offset.Length < 0.0000001) return;

                for (int i = 1; i < leader.NumberOfVertices; i++)
                {
                    Point2d point = leader.GetPoint2dAt(i);
                    leader.SetPointAt(i, point + new Vector2d(offset.X, offset.Y));
                }

                foreach (AnnotationMember member in members)
                {
                    if (member.ObjectId == movedTextId) continue;
                    if (!string.Equals(member.Metadata.AnnotationPart, "MainText", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(member.Metadata.AnnotationPart, "SecondaryText", StringComparison.OrdinalIgnoreCase)) continue;
                    DBText text = tr.GetObject(member.ObjectId, OpenMode.ForWrite, false) as DBText;
                    if (text != null) text.TransformBy(Matrix3d.Displacement(offset));
                }
                tr.Commit();
            }
            doc.Editor.Regen();
            PipeLengthAnnotationInteractionService.RefreshCard(doc, refreshId);
        }

        internal static void AutoBindLeaderGrip(Document doc, ObjectId leaderId, Point3d originalBindingPoint)
        {
            if (doc == null || leaderId.IsNull) return;
            bool completed = false;
            string statusMessage = string.Empty;
            string reboundAnnotationId = string.Empty;
            ObjectId reboundSourceId = ObjectId.Null;
            bool refreshReboundContent = false;
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                Polyline leader = tr.GetObject(leaderId, OpenMode.ForWrite, false) as Polyline;
                AnnotationMetadata metadata;
                if (leader == null || leader.NumberOfVertices < 3 || !TryReadAnnotationMetadata(tr, leader, out metadata)) return;
                Point3d candidate = leader.GetPoint3dAt(0);
                bool wasDetached = string.Equals(metadata.BindingState, "Detached",
                    StringComparison.OrdinalIgnoreCase);
                Curve target;
                Point3d targetPoint;
                if (TryFindDirectBindingCandidate(doc.Database, tr, doc.Editor, candidate,
                    out target, out targetPoint, out statusMessage))
                {
                    string sourceObjectId = EnsureStablePipeObjectId(doc.Database, tr, target.ObjectId);
                    List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, metadata.AnnotationId);
                    AnnotationMember topMember = FindMember(members, "MainText");
                    DBText top = topMember == null ? null : tr.GetObject(topMember.ObjectId, OpenMode.ForWrite, false) as DBText;
                    string userText = metadata.UserText;
                    string oldLength = metadata.SystemLengthText;
                    if (top != null && string.IsNullOrWhiteSpace(oldLength))
                    {
                        Curve previous = FindSourceObject(doc.Database, tr, metadata.SourceObjectId,
                            metadata.SourceHandle) as Curve;
                        if (previous != null)
                        {
                            PipeLengthAnnotationTextComposer.SplitLegacyText(top.TextString,
                                GetCurveLength(previous), out userText, out oldLength);
                        }
                    }
                    string systemLength = FormatUsingCurrentSettings(GetCurveLength(target), oldLength);
                    if (top != null)
                    {
                        top.TextString = PipeLengthAnnotationTextComposer.Compose(userText, systemLength);
                        try { top.AdjustAlignment(doc.Database); } catch { }
                    }

                    foreach (AnnotationMember member in members)
                    {
                        Entity entity = tr.GetObject(member.ObjectId, OpenMode.ForWrite, false) as Entity;
                        if (entity == null) continue;
                        Dictionary<string, string> values;
                        if (!TryReadRecord(tr, entity, AnnotationSourceXrecordName, out values)) continue;
                        values["MetadataVersion"] = MetadataVersion;
                        values["BindingState"] = "Bound";
                        values["SourceObjectId"] = sourceObjectId;
                        values["SourceHandle"] = target.Handle.ToString();
                        values["SourceLayer"] = target.Layer ?? string.Empty;
                        values["UserText"] = userText ?? string.Empty;
                        values["SystemLengthText"] = systemLength;
                        values["DetachedLengthToken"] = string.Empty;
                        SetAnchorValues(values, target, targetPoint);
                        WriteRecord(tr, entity, AnnotationSourceXrecordName, BuildRecordValues(values));
                    }
                    leader.SetPointAt(0, new Point2d(targetPoint.X, targetPoint.Y));
                    reboundAnnotationId = metadata.AnnotationId;
                    reboundSourceId = target.ObjectId;
                    refreshReboundContent = wasDetached || !string.Equals(metadata.SourceHandle,
                        target.Handle.ToString(), StringComparison.OrdinalIgnoreCase);
                    completed = true;
                }
                else if (!wasDetached)
                {
                    leader.SetPointAt(0, new Point2d(originalBindingPoint.X, originalBindingPoint.Y));
                }
                tr.Commit();
            }
            if (completed && refreshReboundContent && !reboundSourceId.IsNull)
            {
                try
                {
                    RefreshBoundAnnotationContent(doc, reboundAnnotationId, reboundSourceId, leaderId);
                }
                catch (Exception ex)
                {
                    doc.Editor.WriteMessage("\n[CDBox 标注绑定] 内容刷新失败：" + ex.Message);
                }
            }
            doc.Editor.Regen();
            PipeLengthAnnotationInteractionService.RefreshCard(doc, leaderId);
            if (!completed)
            {
                doc.Editor.WriteMessage("\n[CDBox 标注绑定] "
                    + (string.IsNullOrWhiteSpace(statusMessage) ? "绑定点未命中管线，已恢复原位置。" : statusMessage));
            }
        }

        internal static void ApplyExistingPlacement(Document doc, string annotationId, Point3d topTextPoint,
            Point3d bottomTextPoint, Point3d leaderJoin, Point3d farEnd, ObjectId refreshObjectId)
        {
            if (doc == null || !IsGuid(annotationId)) return;
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, annotationId);
                AnnotationMember topMember = FindMember(members, "MainText");
                AnnotationMember bottomMember = FindMember(members, "SecondaryText");
                AnnotationMember leaderMember = FindMember(members, "LeaderLine");
                if (topMember == null || leaderMember == null) return;

                DBText top = tr.GetObject(topMember.ObjectId, OpenMode.ForWrite, false) as DBText;
                DBText bottom = bottomMember == null ? null : tr.GetObject(bottomMember.ObjectId, OpenMode.ForWrite, false) as DBText;
                Polyline leader = tr.GetObject(leaderMember.ObjectId, OpenMode.ForWrite, false) as Polyline;
                if (top == null || leader == null || leader.NumberOfVertices < 2) return;

                MoveTextToAnchor(top, topTextPoint);
                if (bottom != null) MoveTextToAnchor(bottom, bottomTextPoint);
                while (leader.NumberOfVertices > 3) leader.RemoveVertexAt(leader.NumberOfVertices - 1);
                leader.SetPointAt(1, new Point2d(leaderJoin.X, leaderJoin.Y));
                if (leader.NumberOfVertices == 2)
                {
                    leader.AddVertexAt(2, new Point2d(farEnd.X, farEnd.Y), 0.0, 0.0, 0.0);
                }
                else
                {
                    leader.SetPointAt(2, new Point2d(farEnd.X, farEnd.Y));
                }
                tr.Commit();
            }
            try { doc.Database.TransactionManager.QueueForGraphicsFlush(); } catch { }
            try { Autodesk.AutoCAD.ApplicationServices.Core.Application.UpdateScreen(); } catch { }
            PipeLengthAnnotationInteractionService.RefreshCard(doc, refreshObjectId);
        }

        internal static bool SetAnnotationVisibility(Document doc, string annotationId, bool visible)
        {
            if (doc == null || !IsGuid(annotationId)) return false;
            int changed = 0;
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, annotationId);
                foreach (AnnotationMember member in members)
                {
                    Entity entity;
                    try { entity = tr.GetObject(member.ObjectId, OpenMode.ForWrite, false) as Entity; }
                    catch { continue; }
                    if (entity == null || entity.IsErased || entity.Visible == visible) continue;
                    entity.Visible = visible;
                    entity.RecordGraphicsModified(true);
                    changed++;
                }
                tr.Commit();
            }
            if (changed > 0)
            {
                try { doc.Database.TransactionManager.QueueForGraphicsFlush(); } catch { }
                try { Autodesk.AutoCAD.ApplicationServices.Core.Application.UpdateScreen(); } catch { }
            }
            return changed > 0;
        }

        public static PipeLengthAnnotationEditModel LoadEditModel(Document doc, ObjectId selectedObjectId)
        {
            if (doc == null || selectedObjectId.IsNull) return null;
            EnsureUnifiedLeader(doc, selectedObjectId);

            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                Entity selected = tr.GetObject(selectedObjectId, OpenMode.ForRead, false) as Entity;
                AnnotationMetadata selectedMetadata;
                if (selected == null || !TryReadAnnotationMetadata(tr, selected, out selectedMetadata)) return null;

                List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, selectedMetadata.AnnotationId);
                DBText topText = null;
                DBText bottomText = null;
                Entity leader = null;
                foreach (AnnotationMember member in members)
                {
                    Entity entity = tr.GetObject(member.ObjectId, OpenMode.ForRead, false) as Entity;
                    if (entity == null) continue;
                    if (string.Equals(member.Metadata.AnnotationPart, "MainText", StringComparison.OrdinalIgnoreCase)) topText = entity as DBText;
                    else if (string.Equals(member.Metadata.AnnotationPart, "SecondaryText", StringComparison.OrdinalIgnoreCase)) bottomText = entity as DBText;
                    else if (string.Equals(member.Metadata.AnnotationPart, "LeaderLine", StringComparison.OrdinalIgnoreCase)) leader = entity;
                }
                if (topText == null) return null;

                var model = new PipeLengthAnnotationEditModel
                {
                    SelectedObjectId = selectedObjectId,
                    AnnotationId = selectedMetadata.AnnotationId,
                    TopText = topText.TextString ?? string.Empty,
                    BottomText = bottomText == null ? string.Empty : bottomText.TextString ?? string.Empty,
                    HasBottomAnnotation = bottomText != null,
                    TextStyleName = ReadTextStyleName(tr, topText.TextStyleId),
                    TextHeight = topText.Height,
                    LayerName = topText.Layer,
                    TextColorIndex = NormalizeColorIndex(topText.ColorIndex),
                    LeaderColorIndex = NormalizeColorIndex(leader == null ? (short)7 : leader.ColorIndex),
                    TextColor = CDBoxColorService.FromCadColor(topText.Color),
                    LeaderColor = CDBoxColorService.FromCadColor(leader == null ? null : leader.Color),
                    LinetypeName = leader == null ? "ByLayer" : leader.Linetype,
                    LineWeight = leader == null ? LineWeight.ByLayer : leader.LineWeight,
                    SourceObjectId = selectedMetadata.SourceObjectId,
                    SourceHandle = selectedMetadata.SourceHandle,
                    SourceObjectType = "Pipe",
                    DetachedLengthToken = selectedMetadata.DetachedLengthToken ?? string.Empty
                };

                Polyline leaderPolyline = leader as Polyline;
                if (leaderPolyline != null && leaderPolyline.NumberOfVertices > 0)
                {
                    model.BindingPoint = leaderPolyline.GetPoint3dAt(0);
                    model.HasBindingPoint = true;
                }

                bool detached = string.Equals(selectedMetadata.BindingState, "Detached", StringComparison.OrdinalIgnoreCase);
                model.IsBound = !detached;
                Entity source = detached ? null : FindSourceObject(doc.Database, tr,
                    selectedMetadata.SourceObjectId, selectedMetadata.SourceHandle);
                if (source != null)
                {
                    model.BindingIsValid = true;
                    model.SourceHandle = source.Handle.ToString();
                    model.SourceLayerName = source.Layer;
                    ObjectIdentity identity;
                    if (TryReadObjectIdentity(tr, source, out identity) && !string.IsNullOrWhiteSpace(identity.CDBoxObjectType))
                    {
                        model.SourceObjectType = identity.CDBoxObjectType;
                    }

                    Curve sourceCurve = source as Curve;
                    string userText = selectedMetadata.UserText;
                    string systemLengthText = selectedMetadata.SystemLengthText;
                    if (sourceCurve != null && string.IsNullOrWhiteSpace(systemLengthText))
                    {
                        PipeLengthAnnotationTextComposer.SplitLegacyText(model.TopText, GetCurveLength(sourceCurve),
                            out userText, out systemLengthText);
                    }
                    if (string.IsNullOrWhiteSpace(userText) && string.IsNullOrWhiteSpace(systemLengthText))
                    {
                        userText = model.TopText;
                    }
                    if (sourceCurve != null && !string.IsNullOrWhiteSpace(systemLengthText))
                    {
                        systemLengthText = FormatUsingCurrentSettings(
                            GetCurveLength(sourceCurve), systemLengthText);
                    }
                    model.UserText = userText ?? string.Empty;
                    model.SystemLengthText = systemLengthText ?? string.Empty;
                }
                else if (detached)
                {
                    model.BindingIsValid = false;
                    model.SourceObjectId = string.Empty;
                    model.SourceHandle = string.Empty;
                    model.SourceLayerName = string.Empty;
                    model.UserText = string.IsNullOrEmpty(selectedMetadata.UserText)
                        ? model.TopText
                        : selectedMetadata.UserText;
                    model.SystemLengthText = string.Empty;
                }
                else
                {
                    model.BindingIsValid = false;
                    model.SourceLayerName = "绑定对象不可用";
                    model.UserText = selectedMetadata.UserText ?? string.Empty;
                    model.SystemLengthText = selectedMetadata.SystemLengthText ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(model.UserText) && string.IsNullOrWhiteSpace(model.SystemLengthText))
                    {
                        model.UserText = model.TopText;
                    }
                }

                model.TextStyleNames = ReadTextStyleNames(doc.Database, tr);
                model.LayerNames = ReadLayerNames(doc.Database, tr);
                model.LinetypeNames = ReadLinetypeNames(doc.Database, tr);
                tr.Commit();
                return model;
            }
        }

        public static PipeLengthAnnotationEditModel SaveEditModel(Document doc, PipeLengthAnnotationEditModel model)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (model == null || !IsGuid(model.AnnotationId)) throw new InvalidOperationException("标注对象无效。 ");
            if (model.TextHeight <= 0.0) throw new InvalidOperationException("文字高度必须大于 0。 ");

            ObjectId selectedId = model.SelectedObjectId;
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, model.AnnotationId);
                AnnotationMember topMember = FindMember(members, "MainText");
                AnnotationMember bottomMember = FindMember(members, "SecondaryText");
                AnnotationMember leaderMember = FindMember(members, "LeaderLine");
                if (topMember == null) throw new InvalidOperationException("标注主文字已丢失。 ");
                selectedId = topMember.ObjectId;

                DBText topText = tr.GetObject(topMember.ObjectId, OpenMode.ForWrite, false) as DBText;
                if (topText == null) throw new InvalidOperationException("标注主文字类型无效。 ");
                ObjectId textStyleId = ResolveTextStyleId(doc.Database, tr, model.TextStyleName, topText.TextStyleId);

                string topValue;
                if (model.IsBound)
                {
                    Entity source = FindSourceObject(doc.Database, tr, topMember.Metadata.SourceObjectId,
                        topMember.Metadata.SourceHandle);
                    Curve sourceCurve = source as Curve;
                    if (sourceCurve != null)
                    {
                        model.SystemLengthText = FormatUsingCurrentSettings(
                            GetCurveLength(sourceCurve), model.SystemLengthText);
                    }
                    topValue = PipeLengthAnnotationTextComposer.Compose(model.UserText, model.SystemLengthText);
                }
                else
                {
                    topValue = model.UserText ?? model.TopText;
                }
                if (string.IsNullOrWhiteSpace(topValue)) throw new InvalidOperationException("上侧文字不能为空。 ");

                topText.TextString = topValue.Trim();
                topText.Height = model.TextHeight;
                if (!textStyleId.IsNull) topText.TextStyleId = textStyleId;
                ApplyEntityLayerAndColor(topText, model.LayerName, model.TextColor, model.TextColorIndex);
                try { topText.AdjustAlignment(doc.Database); } catch { }

                DBText bottomText = bottomMember == null ? null : tr.GetObject(bottomMember.ObjectId, OpenMode.ForWrite, false) as DBText;
                if (string.IsNullOrWhiteSpace(model.BottomText))
                {
                    if (bottomText != null)
                    {
                        RemoveFromNativeGroup(doc.Database, tr, bottomMember.Metadata.GroupName, bottomMember.ObjectId);
                        bottomText.Erase();
                    }
                    bottomText = null;
                }
                else
                {
                    if (bottomText == null)
                    {
                        bottomText = CreateBottomText(doc.Database, tr, topText, leaderMember, members, model);
                    }
                    bottomText.TextString = model.BottomText.Trim();
                    bottomText.Height = model.TextHeight;
                    if (!textStyleId.IsNull) bottomText.TextStyleId = textStyleId;
                    ApplyEntityLayerAndColor(bottomText, model.LayerName, model.TextColor, model.TextColorIndex);
                    try { bottomText.AdjustAlignment(doc.Database); } catch { }
                }

                Entity leader = leaderMember == null ? null : tr.GetObject(leaderMember.ObjectId, OpenMode.ForWrite, false) as Entity;
                ApplyLineAppearance(doc.Database, tr, leader, model);
                if (leader != null)
                {
                    AlignTextsToUnifiedLeader(leader as Polyline, topText, bottomText, model.TextHeight);
                    ResizeUnifiedLeaderLanding(leader as Polyline, topText, bottomText, model.TextHeight);
                }

                foreach (AnnotationMember member in members)
                {
                    Entity entity;
                    try { entity = tr.GetObject(member.ObjectId, OpenMode.ForWrite, false) as Entity; }
                    catch { continue; }
                    if (entity == null || entity.IsErased) continue;
                    Dictionary<string, string> values;
                    if (!TryReadRecord(tr, entity, AnnotationSourceXrecordName, out values)) continue;
                    values["ContentMode"] = "ManualOverride";
                    values["MetadataVersion"] = MetadataVersion;
                    values["BindingState"] = model.IsBound ? "Bound" : "Detached";
                    values["UserText"] = model.UserText ?? string.Empty;
                    values["SystemLengthText"] = model.IsBound ? model.SystemLengthText ?? string.Empty : string.Empty;
                    values["DetachedLengthToken"] = model.DetachedLengthToken ?? string.Empty;
                    WriteRecord(tr, entity, AnnotationSourceXrecordName, BuildRecordValues(values));
                }

                tr.Commit();
            }

            return LoadEditModel(doc, selectedId);
        }

        public static PipeLengthAnnotationEditModel DetachAnnotation(Document doc, string annotationId, ObjectId selectedObjectId)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (!IsGuid(annotationId)) throw new InvalidOperationException("标注对象无效。 ");
            ObjectId reloadId = selectedObjectId;
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, annotationId);
                AnnotationMember topMember = FindMember(members, "MainText");
                if (topMember == null) throw new InvalidOperationException("标注主文字已丢失。 ");
                reloadId = topMember.ObjectId;
                DBText top = tr.GetObject(topMember.ObjectId, OpenMode.ForRead, false) as DBText;
                if (top == null) throw new InvalidOperationException("标注主文字类型无效。 ");

                string frozenText = top.TextString ?? string.Empty;
                string lengthToken = topMember.Metadata.SystemLengthText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(lengthToken))
                {
                    Curve source = FindSourceObject(doc.Database, tr, topMember.Metadata.SourceObjectId,
                        topMember.Metadata.SourceHandle) as Curve;
                    string ignored;
                    if (source != null)
                    {
                        PipeLengthAnnotationTextComposer.SplitLegacyText(frozenText, GetCurveLength(source),
                            out ignored, out lengthToken);
                    }
                }

                foreach (AnnotationMember member in members)
                {
                    Entity entity = tr.GetObject(member.ObjectId, OpenMode.ForWrite, false) as Entity;
                    Dictionary<string, string> values;
                    if (entity == null || !TryReadRecord(tr, entity, AnnotationSourceXrecordName, out values)) continue;
                    values["MetadataVersion"] = MetadataVersion;
                    values["BindingState"] = "Detached";
                    values["SourceObjectId"] = string.Empty;
                    values["SourceHandle"] = string.Empty;
                    values["SourceLayer"] = string.Empty;
                    values["UserText"] = frozenText;
                    values["SystemLengthText"] = string.Empty;
                    values["DetachedLengthToken"] = lengthToken;
                    WriteRecord(tr, entity, AnnotationSourceXrecordName, BuildRecordValues(values));
                }
                tr.Commit();
            }
            doc.Editor.Regen();
            return LoadEditModel(doc, reloadId);
        }

        public static bool TryRebindAtLeaderPoint(Document doc, string annotationId, ObjectId selectedObjectId,
            out PipeLengthAnnotationEditModel model, out string message)
        {
            model = null;
            message = string.Empty;
            if (doc == null || !IsGuid(annotationId))
            {
                message = "标注对象无效。";
                return false;
            }

            Point3d leaderPoint;
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, annotationId);
                AnnotationMember leaderMember = FindMember(members, "LeaderLine");
                Polyline leader = leaderMember == null ? null
                    : tr.GetObject(leaderMember.ObjectId, OpenMode.ForRead, false) as Polyline;
                if (leader == null || leader.NumberOfVertices == 0)
                {
                    message = "标注引线已丢失。";
                    return false;
                }
                leaderPoint = leader.GetPoint3dAt(0);
                tr.Commit();
            }

            ObjectId pipeId;
            Point3d bindingPoint;
            string detectionMessage;
            if (!PipeLengthAnnotationService.TryFindPolylineAtPoint(doc, leaderPoint,
                out pipeId, out bindingPoint, out detectionMessage))
            {
                message = "[管线长度标注] " + detectionMessage;
                return false;
            }
            model = RebindAnnotation(doc, annotationId, pipeId, bindingPoint, selectedObjectId);
            message = "已按当前引线端点重新绑定。";
            return model != null;
        }

        public static PipeLengthAnnotationEditModel RebindAnnotation(Document doc, string annotationId, ObjectId newPipeId, Point3d pickedPoint, ObjectId selectedObjectId)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                Curve pipe = tr.GetObject(newPipeId, OpenMode.ForWrite, false) as Curve;
                if (!IsSupportedBindingCurve(pipe))
                {
                    throw new InvalidOperationException("绑定对象必须具有可用的长度属性。 ");
                }

                Point3d bindingPoint = pipe.GetClosestPointTo(pickedPoint, false);
                string sourceObjectId = EnsureStablePipeObjectId(doc.Database, tr, newPipeId);
                List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, annotationId);
                if (members.Count == 0) throw new InvalidOperationException("标注组成对象已丢失。 ");

                AnnotationMember topMember = FindMember(members, "MainText");
                DBText top = topMember == null ? null : tr.GetObject(topMember.ObjectId, OpenMode.ForWrite, false) as DBText;
                string userText = topMember == null ? string.Empty : topMember.Metadata.UserText;
                string oldLength = topMember == null ? string.Empty : topMember.Metadata.SystemLengthText;
                bool wasDetached = topMember != null && string.Equals(topMember.Metadata.BindingState,
                    "Detached", StringComparison.OrdinalIgnoreCase);
                if (top != null && wasDetached)
                {
                    oldLength = topMember.Metadata.DetachedLengthToken;
                    userText = PipeLengthAnnotationTextComposer.RemoveDetachedLengthToken(
                        top.TextString, oldLength);
                }
                else if (top != null && string.IsNullOrWhiteSpace(oldLength))
                {
                    PipeLengthAnnotationTextComposer.SplitLastLengthToken(top.TextString, out userText, out oldLength);
                }
                string systemLength = FormatUsingCurrentSettings(GetCurveLength(pipe), oldLength);
                if (top != null)
                {
                    top.TextString = PipeLengthAnnotationTextComposer.Compose(userText, systemLength);
                    try { top.AdjustAlignment(doc.Database); } catch { }
                }

                foreach (AnnotationMember member in members)
                {
                    Entity entity = tr.GetObject(member.ObjectId, OpenMode.ForWrite, false) as Entity;
                    if (entity == null) continue;
                    Dictionary<string, string> values;
                    if (!TryReadRecord(tr, entity, AnnotationSourceXrecordName, out values)) continue;
                    values["SourceObjectId"] = sourceObjectId;
                    values["SourceHandle"] = pipe.Handle.ToString();
                    values["SourceLayer"] = pipe.Layer ?? string.Empty;
                    values["MetadataVersion"] = MetadataVersion;
                    values["BindingState"] = "Bound";
                    values["UserText"] = userText ?? string.Empty;
                    values["SystemLengthText"] = systemLength;
                    values["DetachedLengthToken"] = string.Empty;
                    SetAnchorValues(values, pipe, bindingPoint);
                    WriteRecord(tr, entity, AnnotationSourceXrecordName, BuildRecordValues(values));

                    if (string.Equals(member.Metadata.AnnotationPart, "LeaderLine", StringComparison.OrdinalIgnoreCase))
                    {
                        Polyline leader = entity as Polyline;
                        if (leader != null && leader.NumberOfVertices > 0)
                        {
                            leader.SetPointAt(0, new Point2d(bindingPoint.X, bindingPoint.Y));
                        }
                    }
                }
                tr.Commit();
            }
            return RefreshBoundAnnotationContent(doc, annotationId, newPipeId, selectedObjectId);
        }

        private static PipeLengthAnnotationEditModel RefreshBoundAnnotationContent(Document doc,
            string annotationId, ObjectId sourceId, ObjectId selectedObjectId)
        {
            PipeLengthAnnotationBindingContent content;
            string ignoredError;
            if (!PipeLengthAnnotationService.TryBuildBindingContent(doc, sourceId,
                out content, out ignoredError) || content == null)
            {
                return LoadEditModel(doc, selectedObjectId);
            }

            PipeLengthAnnotationEditModel model = LoadEditModel(doc, selectedObjectId);
            if (model == null) return null;
            model.UserText = content.UserText;
            model.SystemLengthText = content.SystemLengthText;
            model.TopText = content.TopText;
            model.BottomText = content.BottomText;
            model.HasBottomAnnotation = !string.IsNullOrWhiteSpace(content.BottomText);
            model.DetachedLengthToken = string.Empty;
            model.IsBound = true;
            model.BindingIsValid = true;
            return SaveEditModel(doc, model);
        }

        internal static ObjectId RefreshBindingsForSourceHandles(Document doc, IEnumerable<string> sourceHandles,
            string activeAnnotationId)
        {
            if (doc == null || sourceHandles == null) return ObjectId.Null;
            var dirty = new HashSet<string>(sourceHandles, StringComparer.OrdinalIgnoreCase);
            if (dirty.Count == 0) return ObjectId.Null;
            ObjectId refreshId = ObjectId.Null;
            bool changed = false;

            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                var annotationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ObjectId id in EnumerateCurrentSpace(doc.Database, tr))
                {
                    Entity entity;
                    try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                    catch { continue; }
                    AnnotationMetadata metadata;
                    if (entity != null && TryReadAnnotationMetadata(tr, entity, out metadata)
                        && dirty.Contains(metadata.SourceHandle)
                        && !string.Equals(metadata.BindingState, "Detached", StringComparison.OrdinalIgnoreCase))
                    {
                        annotationIds.Add(metadata.AnnotationId);
                    }
                }

                foreach (string annotationId in annotationIds)
                {
                    List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, annotationId);
                    AnnotationMember topMember = FindMember(members, "MainText");
                    AnnotationMember leaderMember = FindMember(members, "LeaderLine");
                    if (topMember == null) continue;
                    Entity source = FindSourceObject(doc.Database, tr, topMember.Metadata.SourceObjectId,
                        topMember.Metadata.SourceHandle);
                    Curve sourceCurve = source as Curve;
                    if (!IsSupportedBindingCurve(sourceCurve))
                    {
                        foreach (AnnotationMember member in members)
                        {
                            Entity entity = tr.GetObject(member.ObjectId, OpenMode.ForWrite, false) as Entity;
                            Dictionary<string, string> values;
                            if (entity == null || !TryReadRecord(tr, entity, AnnotationSourceXrecordName, out values)) continue;
                            values["BindingState"] = "Invalid";
                            values["MetadataVersion"] = MetadataVersion;
                            WriteRecord(tr, entity, AnnotationSourceXrecordName, BuildRecordValues(values));
                        }
                        changed = true;
                    }
                    else
                    {
                        DBText top = tr.GetObject(topMember.ObjectId, OpenMode.ForWrite, false) as DBText;
                        Polyline leader = leaderMember == null ? null
                            : tr.GetObject(leaderMember.ObjectId, OpenMode.ForWrite, false) as Polyline;
                        Point3d fallback = leader != null && leader.NumberOfVertices > 0
                            ? leader.GetPoint3dAt(0) : Point3d.Origin;
                        Point3d anchor = ResolveAnchorPoint(sourceCurve, topMember.Metadata, fallback);
                        string userText = topMember.Metadata.UserText;
                        string systemLength = topMember.Metadata.SystemLengthText;
                        if (top != null && string.IsNullOrWhiteSpace(systemLength))
                        {
                            PipeLengthAnnotationTextComposer.SplitLastLengthToken(top.TextString,
                                out userText, out systemLength);
                        }
                        systemLength = FormatUsingCurrentSettings(
                            GetCurveLength(sourceCurve), systemLength);
                        if (top != null)
                        {
                            top.TextString = PipeLengthAnnotationTextComposer.Compose(userText, systemLength);
                            try { top.AdjustAlignment(doc.Database); } catch { }
                        }
                        if (leader != null && leader.NumberOfVertices > 0)
                        {
                            leader.SetPointAt(0, new Point2d(anchor.X, anchor.Y));
                        }
                        foreach (AnnotationMember member in members)
                        {
                            Entity entity = tr.GetObject(member.ObjectId, OpenMode.ForWrite, false) as Entity;
                            Dictionary<string, string> values;
                            if (entity == null || !TryReadRecord(tr, entity, AnnotationSourceXrecordName, out values)) continue;
                            values["BindingState"] = "Bound";
                            values["MetadataVersion"] = MetadataVersion;
                            values["SourceLayer"] = source.Layer ?? string.Empty;
                            values["UserText"] = userText ?? string.Empty;
                            values["SystemLengthText"] = systemLength;
                            SetAnchorValues(values, sourceCurve, anchor);
                            WriteRecord(tr, entity, AnnotationSourceXrecordName, BuildRecordValues(values));
                        }
                        changed = true;
                    }
                    if (string.Equals(annotationId, activeAnnotationId, StringComparison.OrdinalIgnoreCase))
                    {
                        refreshId = topMember.ObjectId;
                    }
                }
                tr.Commit();
            }
            if (changed) doc.Editor.Regen();
            return refreshId;
        }

        private static void EnsureUnifiedLeader(Document doc, ObjectId selectedObjectId)
        {
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                Entity selected = tr.GetObject(selectedObjectId, OpenMode.ForRead, false) as Entity;
                AnnotationMetadata metadata;
                if (selected == null || !TryReadAnnotationMetadata(tr, selected, out metadata)) return;
                List<AnnotationMember> members = FindAnnotationMembers(doc.Database, tr, metadata.AnnotationId);
                AnnotationMember leaderMember = FindMember(members, "LeaderLine");
                AnnotationMember landingMember = FindMember(members, "LandingLine");
                SetNativeGroupSelectable(doc.Database, tr, metadata.GroupName, false);
                if (leaderMember == null || landingMember == null)
                {
                    tr.Commit();
                    return;
                }
                Polyline leader = tr.GetObject(leaderMember.ObjectId, OpenMode.ForWrite, false) as Polyline;
                Polyline landing = tr.GetObject(landingMember.ObjectId, OpenMode.ForWrite, false) as Polyline;
                if (leader == null || landing == null || leader.NumberOfVertices < 2 || landing.NumberOfVertices < 2) return;

                Point3d join = leader.GetPoint3dAt(leader.NumberOfVertices - 1);
                Point3d first = landing.GetPoint3dAt(0);
                Point3d last = landing.GetPoint3dAt(landing.NumberOfVertices - 1);
                Point3d farEnd = join.DistanceTo(first) <= join.DistanceTo(last) ? last : first;
                while (leader.NumberOfVertices > 2) leader.RemoveVertexAt(leader.NumberOfVertices - 1);
                leader.AddVertexAt(leader.NumberOfVertices, new Point2d(farEnd.X, farEnd.Y), 0.0, 0.0, 0.0);
                RemoveFromNativeGroup(doc.Database, tr, landingMember.Metadata.GroupName, landingMember.ObjectId);
                AppendToNativeGroup(doc.Database, tr, leaderMember.Metadata.GroupName, leaderMember.ObjectId);
                landing.Erase();
                tr.Commit();
            }
        }

        private static AnnotationMember FindMember(IList<AnnotationMember> members, string part)
        {
            if (members == null) return null;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null && members[i].Metadata != null
                    && string.Equals(members[i].Metadata.AnnotationPart, part, StringComparison.OrdinalIgnoreCase))
                {
                    return members[i];
                }
            }
            return null;
        }

        private static Entity FindSourceObject(Database db, Transaction tr, string sourceObjectId, string sourceHandle)
        {
            Entity handleMatch = null;
            foreach (ObjectId id in EnumerateCurrentSpace(db, tr))
            {
                Entity entity;
                try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                catch { continue; }
                if (entity == null) continue;
                ObjectIdentity identity;
                if (TryReadObjectIdentity(tr, entity, out identity)
                    && string.Equals(identity.CDBoxObjectId, sourceObjectId, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(entity.Handle.ToString(), sourceHandle, StringComparison.OrdinalIgnoreCase)) return entity;
                    if (handleMatch == null) handleMatch = entity;
                }
            }
            return handleMatch;
        }

        private static List<string> ReadTextStyleNames(Database db, Transaction tr)
        {
            var names = new List<string>();
            TextStyleTable table = tr.GetObject(db.TextStyleTableId, OpenMode.ForRead, false) as TextStyleTable;
            if (table != null)
            {
                foreach (ObjectId id in table)
                {
                    TextStyleTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as TextStyleTableRecord;
                    if (record != null && !record.IsErased && !string.IsNullOrWhiteSpace(record.Name)) names.Add(record.Name);
                }
            }
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }

        private static List<string> ReadLayerNames(Database db, Transaction tr)
        {
            var names = new List<string>();
            LayerTable table = tr.GetObject(db.LayerTableId, OpenMode.ForRead, false) as LayerTable;
            if (table != null)
            {
                foreach (ObjectId id in table)
                {
                    LayerTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as LayerTableRecord;
                    if (record != null && !record.IsErased && !string.IsNullOrWhiteSpace(record.Name)) names.Add(record.Name);
                }
            }
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }

        private static List<string> ReadLinetypeNames(Database db, Transaction tr)
        {
            var names = new List<string>();
            LinetypeTable table = tr.GetObject(db.LinetypeTableId, OpenMode.ForRead, false) as LinetypeTable;
            if (table != null)
            {
                foreach (ObjectId id in table)
                {
                    LinetypeTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as LinetypeTableRecord;
                    if (record != null && !record.IsErased && !string.IsNullOrWhiteSpace(record.Name)) names.Add(record.Name);
                }
            }
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }

        private static string ReadTextStyleName(Transaction tr, ObjectId styleId)
        {
            if (styleId.IsNull) return string.Empty;
            TextStyleTableRecord record = tr.GetObject(styleId, OpenMode.ForRead, false) as TextStyleTableRecord;
            return record == null ? string.Empty : record.Name ?? string.Empty;
        }

        private static ObjectId ResolveTextStyleId(Database db, Transaction tr, string name, ObjectId fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return fallback;
            TextStyleTable table = tr.GetObject(db.TextStyleTableId, OpenMode.ForRead, false) as TextStyleTable;
            return table != null && table.Has(name) ? table[name] : fallback;
        }

        private static short NormalizeColorIndex(int colorIndex)
        {
            return colorIndex < 0 ? (short)256 : (short)colorIndex;
        }

        private static void ApplyEntityLayerAndColor(Entity entity, string layerName, CDBoxColor color, short fallbackColorIndex)
        {
            if (entity == null) return;
            if (!string.IsNullOrWhiteSpace(layerName)) entity.Layer = layerName;
            CDBoxColor original = CDBoxColorService.FromCadColor(entity.Color);
            CDBoxColor output = CDBoxColorService.PrepareForWrite(
                color ?? CDBoxColor.FromIndex(fallbackColorIndex), original);
            entity.Color = CDBoxColorService.ToCadColor(output);
        }

        private static void ApplyLineAppearance(Database db, Transaction tr, Entity entity, PipeLengthAnnotationEditModel model)
        {
            if (entity == null || model == null) return;
            ApplyEntityLayerAndColor(entity, model.LayerName, model.LeaderColor, model.LeaderColorIndex);
            if (!string.IsNullOrWhiteSpace(model.LinetypeName))
            {
                LinetypeTable table = tr.GetObject(db.LinetypeTableId, OpenMode.ForRead, false) as LinetypeTable;
                if (table != null && table.Has(model.LinetypeName)) entity.LinetypeId = table[model.LinetypeName];
            }
            entity.LineWeight = model.LineWeight;
        }

        private static DBText CreateBottomText(Database db, Transaction tr, DBText topText, AnnotationMember leaderMember,
            IList<AnnotationMember> members, PipeLengthAnnotationEditModel model)
        {
            BlockTableRecord owner = tr.GetObject(topText.OwnerId, OpenMode.ForWrite, false) as BlockTableRecord;
            if (owner == null) throw new InvalidOperationException("无法创建下侧文字。 ");

            Point3d point = topText.AlignmentPoint;
            if (point == Point3d.Origin) point = topText.Position;
            Polyline leader = leaderMember == null ? null : tr.GetObject(leaderMember.ObjectId, OpenMode.ForRead, false) as Polyline;
            if (leader != null && leader.NumberOfVertices >= 3)
            {
                Point3d first = leader.GetPoint3dAt(1);
                Point3d last = leader.GetPoint3dAt(leader.NumberOfVertices - 1);
                double gap = Math.Max(model.TextHeight * 0.22, 0.05);
                point = new Point3d((first.X + last.X) / 2.0, (first.Y + last.Y) / 2.0 - gap - model.TextHeight, first.Z);
            }
            else
            {
                point = new Point3d(point.X, point.Y - model.TextHeight * 1.4, point.Z);
            }

            var bottom = new DBText();
            bottom.SetDatabaseDefaults(db);
            bottom.HorizontalMode = TextHorizontalMode.TextCenter;
            bottom.Position = point;
            bottom.AlignmentPoint = point;
            bottom.Height = model.TextHeight;
            bottom.TextString = model.BottomText.Trim();
            bottom.TextStyleId = topText.TextStyleId;
            bottom.LayerId = topText.LayerId;
            CDBoxColor bottomColor = CDBoxColorService.PrepareForWrite(
                model.TextColor ?? CDBoxColor.FromIndex(model.TextColorIndex),
                CDBoxColorService.FromCadColor(topText.Color));
            bottom.Color = CDBoxColorService.ToCadColor(bottomColor);
            ObjectId id = owner.AppendEntity(bottom);
            tr.AddNewlyCreatedDBObject(bottom, true);
            try { bottom.AdjustAlignment(db); } catch { }

            AnnotationMember topMember = FindMember(members, "MainText");
            Dictionary<string, string> values;
            if (topMember != null && TryReadRecord(tr, topText, AnnotationSourceXrecordName, out values))
            {
                values["AnnotationPart"] = "SecondaryText";
                values["ContentMode"] = "ManualOverride";
                WriteRecord(tr, bottom, AnnotationSourceXrecordName, BuildRecordValues(values));
                AppendToNativeGroup(db, tr, topMember.Metadata.GroupName, id);
            }
            return bottom;
        }

        private static void ResizeUnifiedLeaderLanding(Polyline leader, DBText topText, DBText bottomText, double textHeight)
        {
            if (leader == null || leader.NumberOfVertices < 3 || topText == null) return;
            Point3d first = leader.GetPoint3dAt(1);
            Point3d last = leader.GetPoint3dAt(leader.NumberOfVertices - 1);
            double centerX = (first.X + last.X) / 2.0;
            double width = EstimateEntityWidth(topText, textHeight);
            if (bottomText != null) width = Math.Max(width, EstimateEntityWidth(bottomText, textHeight));
            width += Math.Max(textHeight * 0.24, 0.06);
            bool joinOnRight = first.X >= last.X;
            Point2d left = new Point2d(centerX - width / 2.0, first.Y);
            Point2d right = new Point2d(centerX + width / 2.0, last.Y);
            leader.SetPointAt(1, joinOnRight ? right : left);
            leader.SetPointAt(leader.NumberOfVertices - 1, joinOnRight ? left : right);
        }

        private static void AlignTextsToUnifiedLeader(Polyline leader, DBText topText, DBText bottomText, double textHeight)
        {
            if (leader == null || leader.NumberOfVertices < 3 || topText == null) return;
            Point3d join = leader.GetPoint3dAt(1);
            Point3d far = leader.GetPoint3dAt(leader.NumberOfVertices - 1);
            double centerX = (join.X + far.X) / 2.0;
            double lineY = (join.Y + far.Y) / 2.0;
            double gap = Math.Max(textHeight * 0.22, 0.05);
            MoveTextToAnchor(topText, new Point3d(centerX, lineY + gap, GetTextAnchor(topText).Z));
            if (bottomText != null)
            {
                MoveTextToAnchor(bottomText, new Point3d(centerX, lineY - gap - textHeight, GetTextAnchor(bottomText).Z));
            }
        }

        private static void MoveTextToAnchor(DBText text, Point3d target)
        {
            if (text == null) return;
            Point3d current = GetTextAnchor(text);
            Vector3d offset = target - current;
            if (offset.Length > 0.0000001) text.TransformBy(Matrix3d.Displacement(offset));
        }

        private static double EstimateEntityWidth(DBText text, double textHeight)
        {
            if (text == null) return Math.Max(textHeight * 4.0, 1.0);
            try
            {
                Extents3d extents = text.GeometricExtents;
                double width = Math.Abs(extents.MaxPoint.X - extents.MinPoint.X);
                if (width > 0.0001) return width;
            }
            catch { }
            return Math.Max((text.TextString ?? string.Empty).Length * textHeight, textHeight * 2.0);
        }

        private static Point3d GetTextAnchor(DBText text)
        {
            if (text == null) return Point3d.Origin;
            try
            {
                if (text.HorizontalMode != TextHorizontalMode.TextLeft || text.VerticalMode != TextVerticalMode.TextBase)
                {
                    return text.AlignmentPoint;
                }
            }
            catch { }
            return text.Position;
        }

        private static double GetBindingTolerance(Editor editor)
        {
            try
            {
                using (ViewTableRecord view = editor.GetCurrentView())
                {
                    object screen = Autodesk.AutoCAD.ApplicationServices.Core.Application.GetSystemVariable("SCREENSIZE");
                    if (screen is Point2d size && size.Y > 1.0)
                    {
                        double directPickTolerance = view.Height / size.Y * 2.0;
                        return Math.Max(0.001, Math.Min(directPickTolerance, 0.50));
                    }
                }
            }
            catch { }
            return 0.05;
        }

        private static bool TryFindDirectBindingCandidate(Database db, Transaction tr, Editor editor,
            Point3d candidatePoint, out Curve candidate, out Point3d anchor,
            out string message)
        {
            candidate = null;
            anchor = Point3d.Origin;
            message = string.Empty;
            List<PipeSelectionCandidate> candidates = PipeLengthAnnotationService.FindCandidatesAtPoint(
                db, tr, editor, candidatePoint);
            if (candidates.Count == 0)
            {
                message = "绑定点未直接落在具有长度的对象上。";
                return false;
            }
            Document doc = null;
            try { doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.GetDocument(db); }
            catch { }
            if (doc != null) OverlappingPipeSelectionService.EnrichDisplay(doc, candidates);
            PipeSelectionCandidate selected = candidates.Count == 1
                ? candidates[0]
                : OverlappingPipeSelectionService.Select(doc, candidates, tr);
            if (selected == null)
            {
                message = "已取消重叠对象选择，原绑定保持不变。";
                return false;
            }
            candidate = tr.GetObject(selected.ObjectId, OpenMode.ForRead, false) as Curve;
            anchor = selected.AnchorPoint;
            if (candidate == null)
            {
                message = "所选对象已不可用，原绑定保持不变。";
                return false;
            }
            return true;
        }

        private static double GetCurveLength(Curve curve)
        {
            if (curve == null) return 0.0;
            try
            {
                return Math.Abs(curve.GetDistanceAtParameter(curve.EndParam)
                    - curve.GetDistanceAtParameter(curve.StartParam));
            }
            catch
            {
                try { return Math.Abs(curve.GetDistanceAtParameter(curve.EndParam)); }
                catch { return 0.0; }
            }
        }

        private static string FormatUsingCurrentSettings(double length, string previousToken)
        {
            int decimals = PipeLengthAnnotationOptions.Default.DecimalPlaces;
            try
            {
                PipeLengthAnnotationOptions options = PipeLengthAnnotationSettingsStore.Load();
                if (options != null) decimals = options.DecimalPlaces;
            }
            catch { }
            decimals = Math.Max(0, Math.Min(decimals, 6));
            return PipeLengthAnnotationTextComposer.FormatWithDecimals(length, decimals, previousToken);
        }

        private static bool IsSupportedBindingCurve(Curve curve)
        {
            if (curve == null) return false;
            double length = GetCurveLength(curve);
            return !double.IsNaN(length) && !double.IsInfinity(length) && length > 0.0000001;
        }

        private static void SetAnchorValues(IDictionary<string, string> values, Curve curve, Point3d pickedPoint)
        {
            if (values == null || curve == null) return;
            Point3d anchor = pickedPoint;
            double normalized = 0.0;
            try
            {
                anchor = curve.GetClosestPointTo(pickedPoint, false);
                double parameter = curve.GetParameterAtPoint(anchor);
                double range = curve.EndParam - curve.StartParam;
                normalized = Math.Abs(range) < 0.0000001 ? 0.0 : (parameter - curve.StartParam) / range;
            }
            catch { }
            normalized = Math.Max(0.0, Math.Min(1.0, normalized));
            values["AnchorKind"] = "NormalizedCurveParameter";
            values["AnchorParameter"] = normalized.ToString("R", CultureInfo.InvariantCulture);
            values["AnchorPointX"] = anchor.X.ToString("R", CultureInfo.InvariantCulture);
            values["AnchorPointY"] = anchor.Y.ToString("R", CultureInfo.InvariantCulture);
            values["AnchorPointZ"] = anchor.Z.ToString("R", CultureInfo.InvariantCulture);
        }

        private static Point3d ResolveAnchorPoint(Curve curve, AnnotationMetadata metadata, Point3d fallback)
        {
            if (curve == null) return fallback;
            double normalized;
            if (metadata != null
                && double.TryParse(metadata.AnchorParameter, NumberStyles.Float, CultureInfo.InvariantCulture, out normalized))
            {
                try
                {
                    normalized = Math.Max(0.0, Math.Min(1.0, normalized));
                    double parameter = curve.StartParam + (curve.EndParam - curve.StartParam) * normalized;
                    return curve.GetPointAtParameter(parameter);
                }
                catch { }
            }
            try { return curve.GetClosestPointTo(fallback, false); }
            catch { return fallback; }
        }

        private static void AppendToNativeGroup(Database db, Transaction tr, string groupName, ObjectId objectId)
        {
            if (string.IsNullOrWhiteSpace(groupName) || objectId.IsNull) return;
            DBDictionary dictionary = tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead, false) as DBDictionary;
            if (dictionary == null || !dictionary.Contains(groupName)) return;
            Group group = tr.GetObject(dictionary.GetAt(groupName), OpenMode.ForWrite, false) as Group;
            if (group != null && !group.IsErased) group.Append(objectId);
        }

        private static void RemoveFromNativeGroup(Database db, Transaction tr, string groupName, ObjectId objectId)
        {
            if (string.IsNullOrWhiteSpace(groupName) || objectId.IsNull) return;
            DBDictionary dictionary = tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead, false) as DBDictionary;
            if (dictionary == null || !dictionary.Contains(groupName)) return;
            Group group = tr.GetObject(dictionary.GetAt(groupName), OpenMode.ForWrite, false) as Group;
            if (group == null || group.IsErased) return;
            try { group.Remove(objectId); } catch { }
        }

        private static void SetNativeGroupSelectable(Database db, Transaction tr, string groupName, bool selectable)
        {
            if (string.IsNullOrWhiteSpace(groupName)) return;
            DBDictionary dictionary = tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead, false) as DBDictionary;
            if (dictionary == null || !dictionary.Contains(groupName)) return;
            Group group = tr.GetObject(dictionary.GetAt(groupName), OpenMode.ForWrite, false) as Group;
            if (group != null && !group.IsErased) group.Selectable = selectable;
        }

        private static string[] BuildRecordValues(IDictionary<string, string> values)
        {
            string[] keys =
            {
                "MetadataVersion", "AnnotationId", "AnnotationType", "AnnotationPart", "SourceObjectId", "SourceHandle",
                "GroupName", "ContentMode", "SourceLayer", "SourceParent", "SourceClass", "SourceTags",
                "BindingState", "UserText", "SystemLengthText", "DetachedLengthToken",
                "AnchorKind", "AnchorParameter", "AnchorPointX", "AnchorPointY", "AnchorPointZ"
            };
            var result = new List<string>();
            for (int i = 0; i < keys.Length; i++)
            {
                string value;
                if (values != null && values.TryGetValue(keys[i], out value)) result.Add(keys[i] + "=" + (value ?? string.Empty));
            }
            return result.ToArray();
        }

        private static string EnsureStablePipeObjectId(Database db, Transaction tr, ObjectId pipeId)
        {
            Entity pipe = tr.GetObject(pipeId, OpenMode.ForWrite, false) as Entity;
            if (pipe == null) throw new InvalidOperationException("无法读取源管线对象。 ");

            ObjectIdentity current;
            string objectId = TryReadObjectIdentity(tr, pipe, out current) && IsGuid(current.CDBoxObjectId)
                ? current.CDBoxObjectId
                : Guid.NewGuid().ToString("D");

            string currentHandle = GetHandleText(pipeId);
            if (CountObjectsWithIdentity(db, tr, objectId, pipeId) > 0
                && !HasAnnotationReferenceForHandle(db, tr, objectId, currentHandle))
            {
                objectId = Guid.NewGuid().ToString("D");
            }

            WriteRecord(tr, pipe, ObjectIdentityXrecordName,
                "MetadataVersion=" + MetadataVersion,
                "CDBoxObjectId=" + objectId,
                "CDBoxObjectType=Pipe");
            return objectId;
        }

        private static int CountObjectsWithIdentity(Database db, Transaction tr, string objectId, ObjectId excludedId)
        {
            if (string.IsNullOrWhiteSpace(objectId)) return 0;
            int count = 0;
            foreach (ObjectId id in EnumerateCurrentSpace(db, tr))
            {
                if (id == excludedId) continue;
                Entity entity;
                try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                catch { continue; }
                ObjectIdentity identity;
                if (entity != null && TryReadObjectIdentity(tr, entity, out identity)
                    && string.Equals(identity.CDBoxObjectId, objectId, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }
            return count;
        }

        private static bool HasAnnotationReferenceForHandle(Database db, Transaction tr, string sourceObjectId, string sourceHandle)
        {
            if (string.IsNullOrWhiteSpace(sourceObjectId) || string.IsNullOrWhiteSpace(sourceHandle)) return false;
            foreach (ObjectId id in EnumerateCurrentSpace(db, tr))
            {
                Entity entity;
                try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                catch { continue; }
                AnnotationMetadata metadata;
                if (entity != null && TryReadAnnotationMetadata(tr, entity, out metadata)
                    && string.Equals(metadata.SourceObjectId, sourceObjectId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(metadata.SourceHandle, sourceHandle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static ObjectId CreateNativeGroup(Database db, Transaction tr, string groupName, string annotationId, IList<ObjectId> memberIds)
        {
            DBDictionary groupDictionary = tr.GetObject(db.GroupDictionaryId, OpenMode.ForWrite, false) as DBDictionary;
            if (groupDictionary == null) throw new InvalidOperationException("无法读取 AutoCAD 原生组字典。 ");

            var group = new Group("CDBox 管线长度标注 " + annotationId, false);
            ObjectId groupId = groupDictionary.SetAt(groupName, group);
            tr.AddNewlyCreatedDBObject(group, true);
            for (int i = 0; i < memberIds.Count; i++)
            {
                if (!memberIds[i].IsNull) group.Append(memberIds[i]);
            }
            return groupId;
        }

        private static string BuildUniqueGroupName(Database db, Transaction tr, string annotationId)
        {
            string suffix = (annotationId ?? Guid.NewGuid().ToString("N")).Replace("-", string.Empty).ToUpperInvariant();
            string baseName = GroupNamePrefix + suffix;
            DBDictionary dictionary = tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead, false) as DBDictionary;
            if (dictionary == null || !dictionary.Contains(baseName)) return baseName;

            int index = 2;
            while (dictionary.Contains(baseName + "_" + index.ToString(CultureInfo.InvariantCulture))) index++;
            return baseName + "_" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static void WriteAnnotationMetadata(Transaction tr, ObjectId objectId, PipeLengthAnnotationResult result, string part)
        {
            Entity entity = tr.GetObject(objectId, OpenMode.ForWrite, false) as Entity;
            if (entity == null) throw new InvalidOperationException("无法写入标注组成对象元数据。 ");

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MetadataVersion"] = MetadataVersion,
                ["AnnotationId"] = result.AnnotationId,
                ["AnnotationType"] = AnnotationType,
                ["AnnotationPart"] = part,
                ["SourceObjectId"] = result.SourceCDBoxObjectId,
                ["SourceHandle"] = GetHandleText(result.PipeObjectId),
                ["GroupName"] = result.AnnotationGroupName,
                ["ContentMode"] = "Auto",
                ["SourceLayer"] = result.PipeLayerName ?? string.Empty,
                ["SourceParent"] = result.PipeParentGroup ?? string.Empty,
                ["SourceClass"] = result.PipeParentClass ?? string.Empty,
                ["SourceTags"] = result.PipeTagText ?? string.Empty,
                ["BindingState"] = "Bound",
                ["UserText"] = result.UserText ?? string.Empty,
                ["SystemLengthText"] = result.SystemLengthText ?? string.Empty,
                ["DetachedLengthToken"] = string.Empty
            };
            Curve source = tr.GetObject(result.PipeObjectId, OpenMode.ForRead, false) as Curve;
            SetAnchorValues(values, source, result.BindingPoint);
            WriteRecord(tr, entity, AnnotationSourceXrecordName, BuildRecordValues(values));
        }

        private static void WriteRecord(Transaction tr, DBObject owner, string recordName, params string[] values)
        {
            if (owner.ExtensionDictionary.IsNull) owner.CreateExtensionDictionary();
            DBDictionary dictionary = tr.GetObject(owner.ExtensionDictionary, OpenMode.ForWrite, false) as DBDictionary;
            if (dictionary == null) throw new InvalidOperationException("无法读取对象扩展字典。 ");

            Xrecord record;
            if (dictionary.Contains(recordName))
            {
                record = tr.GetObject(dictionary.GetAt(recordName), OpenMode.ForWrite, false) as Xrecord;
            }
            else
            {
                record = new Xrecord();
                dictionary.SetAt(recordName, record);
                tr.AddNewlyCreatedDBObject(record, true);
            }

            if (record == null) throw new InvalidOperationException("无法创建对象元数据记录。 ");
            var typedValues = new TypedValue[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                typedValues[i] = new TypedValue((int)DxfCode.Text, values[i] ?? string.Empty);
            }
            record.Data = new ResultBuffer(typedValues);
        }

        private static bool TryReadAnnotationMetadata(Transaction tr, DBObject owner, out AnnotationMetadata metadata)
        {
            metadata = null;
            Dictionary<string, string> values;
            if (!TryReadRecord(tr, owner, AnnotationSourceXrecordName, out values)) return false;

            string annotationId = GetValue(values, "AnnotationId");
            string annotationType = GetValue(values, "AnnotationType");
            if (!IsGuid(annotationId) || !string.Equals(annotationType, AnnotationType, StringComparison.OrdinalIgnoreCase)) return false;

            metadata = new AnnotationMetadata
            {
                AnnotationId = annotationId,
                AnnotationType = annotationType,
                AnnotationPart = GetValue(values, "AnnotationPart"),
                SourceObjectId = GetValue(values, "SourceObjectId"),
                SourceHandle = GetValue(values, "SourceHandle"),
                GroupName = GetValue(values, "GroupName"),
                MetadataVersion = GetValue(values, "MetadataVersion"),
                BindingState = GetValue(values, "BindingState"),
                UserText = GetValue(values, "UserText"),
                SystemLengthText = GetValue(values, "SystemLengthText"),
                DetachedLengthToken = GetValue(values, "DetachedLengthToken"),
                AnchorKind = GetValue(values, "AnchorKind"),
                AnchorParameter = GetValue(values, "AnchorParameter"),
                AnchorPointX = GetValue(values, "AnchorPointX"),
                AnchorPointY = GetValue(values, "AnchorPointY"),
                AnchorPointZ = GetValue(values, "AnchorPointZ")
            };
            return true;
        }

        private static bool TryReadObjectIdentity(Transaction tr, DBObject owner, out ObjectIdentity identity)
        {
            identity = null;
            Dictionary<string, string> values;
            if (!TryReadRecord(tr, owner, ObjectIdentityXrecordName, out values)) return false;
            string id = GetValue(values, "CDBoxObjectId");
            if (!IsGuid(id)) return false;
            identity = new ObjectIdentity
            {
                CDBoxObjectId = id,
                CDBoxObjectType = GetValue(values, "CDBoxObjectType"),
                MetadataVersion = GetValue(values, "MetadataVersion")
            };
            return true;
        }

        private static bool TryReadRecord(Transaction tr, DBObject owner, string recordName, out Dictionary<string, string> values)
        {
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (tr == null || owner == null || owner.ExtensionDictionary.IsNull) return false;
            DBDictionary dictionary;
            try { dictionary = tr.GetObject(owner.ExtensionDictionary, OpenMode.ForRead, false) as DBDictionary; }
            catch { return false; }
            if (dictionary == null || !dictionary.Contains(recordName)) return false;

            Xrecord record;
            try { record = tr.GetObject(dictionary.GetAt(recordName), OpenMode.ForRead, false) as Xrecord; }
            catch { return false; }
            if (record == null || record.Data == null) return false;

            foreach (TypedValue typedValue in record.Data)
            {
                if (typedValue.TypeCode != (int)DxfCode.Text || typedValue.Value == null) continue;
                string text = Convert.ToString(typedValue.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                int equals = text.IndexOf('=');
                if (equals <= 0) continue;
                values[text.Substring(0, equals)] = text.Substring(equals + 1);
            }
            return values.Count > 0;
        }

        private static List<AnnotationMember> FindAnnotationMembers(Database db, Transaction tr, string annotationId)
        {
            var members = new List<AnnotationMember>();
            foreach (ObjectId id in EnumerateCurrentSpace(db, tr))
            {
                Entity entity;
                try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                catch { continue; }
                AnnotationMetadata metadata;
                if (entity != null && !entity.IsErased && TryReadAnnotationMetadata(tr, entity, out metadata)
                    && string.Equals(metadata.AnnotationId, annotationId, StringComparison.OrdinalIgnoreCase))
                {
                    members.Add(new AnnotationMember { ObjectId = id, Metadata = metadata });
                }
            }
            return members;
        }

        private static IEnumerable<ObjectId> EnumerateCurrentSpace(Database db, Transaction tr)
        {
            BlockTableRecord space = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead, false) as BlockTableRecord;
            if (space == null) yield break;
            foreach (ObjectId id in space) yield return id;
        }

        private static void MoveLeaderKeepingSourceAnchor(Entity entity, Vector3d displacement)
        {
            Polyline polyline = entity as Polyline;
            if (polyline == null || polyline.NumberOfVertices < 2)
            {
                entity.TransformBy(Matrix3d.Displacement(displacement));
                return;
            }

            var delta = new Vector2d(displacement.X, displacement.Y);
            for (int i = 1; i < polyline.NumberOfVertices; i++)
            {
                polyline.SetPointAt(i, polyline.GetPoint2dAt(i) + delta);
            }
        }

        private static string BuildAnnotationDiagnostic(Database db, Transaction tr, AnnotationMetadata annotation)
        {
            List<AnnotationMember> members = FindAnnotationMembers(db, tr, annotation.AnnotationId);
            int mainTextCount = 0;
            int secondaryTextCount = 0;
            int leaderCount = 0;
            var groupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AnnotationMember member in members)
            {
                if (string.Equals(member.Metadata.AnnotationPart, "MainText", StringComparison.OrdinalIgnoreCase)) mainTextCount++;
                else if (string.Equals(member.Metadata.AnnotationPart, "SecondaryText", StringComparison.OrdinalIgnoreCase)) secondaryTextCount++;
                else if (string.Equals(member.Metadata.AnnotationPart, "LeaderLine", StringComparison.OrdinalIgnoreCase)) leaderCount++;
                if (!string.IsNullOrWhiteSpace(member.Metadata.GroupName)) groupNames.Add(member.Metadata.GroupName);
            }

            int sourceCount = CountObjectsWithIdentity(db, tr, annotation.SourceObjectId, ObjectId.Null);
            bool groupExists = false;
            int nativeGroupMemberCount = 0;
            bool groupMembersMatch = false;
            if (!string.IsNullOrWhiteSpace(annotation.GroupName))
            {
                DBDictionary groupDictionary = tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead, false) as DBDictionary;
                if (groupDictionary != null && groupDictionary.Contains(annotation.GroupName))
                {
                    Group group = tr.GetObject(groupDictionary.GetAt(annotation.GroupName), OpenMode.ForRead, false) as Group;
                    if (group != null && !group.IsErased)
                    {
                        groupExists = true;
                        ObjectId[] nativeIds = group.GetAllEntityIds();
                        nativeGroupMemberCount = nativeIds == null ? 0 : nativeIds.Length;
                        var nativeSet = new HashSet<ObjectId>(nativeIds ?? new ObjectId[0]);
                        groupMembersMatch = nativeSet.Count == members.Count;
                        if (groupMembersMatch)
                        {
                            foreach (AnnotationMember member in members)
                            {
                                if (!nativeSet.Contains(member.ObjectId))
                                {
                                    groupMembersMatch = false;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            var issues = new List<string>();
            if (mainTextCount != 1) issues.Add("主文字数量应为 1，当前为 " + mainTextCount.ToString(CultureInfo.InvariantCulture));
            if (secondaryTextCount > 1) issues.Add("次文字重复");
            if (leaderCount > 1) issues.Add("引线重复");
            if (sourceCount == 0) issues.Add("关联源管线不存在或身份丢失");
            if (sourceCount > 1) issues.Add("源管线 CDBoxObjectId 重复");
            if (!groupExists) issues.Add("AutoCAD 原生 Group 丢失");
            else if (nativeGroupMemberCount != members.Count || !groupMembersMatch) issues.Add("Group 成员与元数据组成对象不一致");
            if (groupNames.Count > 1) issues.Add("同一 AnnotationId 指向多个 Group");

            var text = new StringBuilder();
            text.Append("\n[CDBox 管线长度标注关联诊断]");
            text.Append("\n  AnnotationId：").Append(annotation.AnnotationId);
            text.Append("\n  源 CDBoxObjectId：").Append(annotation.SourceObjectId);
            text.Append("\n  原生 Group：").Append(string.IsNullOrWhiteSpace(annotation.GroupName) ? "未记录" : annotation.GroupName);
            text.Append("\n  组成：主文字 ").Append(mainTextCount).Append("，次文字 ").Append(secondaryTextCount).Append("，引线/横线 ").Append(leaderCount);
            text.Append("\n  状态：").Append(issues.Count == 0 ? "正常" : string.Join("；", issues.ToArray()));
            return text.ToString();
        }

        private static string BuildSourceDiagnostic(Database db, Transaction tr, ObjectId selectedId, ObjectIdentity identity)
        {
            var annotationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId id in EnumerateCurrentSpace(db, tr))
            {
                Entity entity;
                try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                catch { continue; }
                AnnotationMetadata metadata;
                if (entity != null && TryReadAnnotationMetadata(tr, entity, out metadata)
                    && string.Equals(metadata.SourceObjectId, identity.CDBoxObjectId, StringComparison.OrdinalIgnoreCase))
                {
                    annotationIds.Add(metadata.AnnotationId);
                }
            }

            int duplicates = CountObjectsWithIdentity(db, tr, identity.CDBoxObjectId, selectedId);
            var text = new StringBuilder();
            text.Append("\n[CDBox 管线关联诊断]");
            text.Append("\n  CDBoxObjectId：").Append(identity.CDBoxObjectId);
            text.Append("\n  对象类型：").Append(string.IsNullOrWhiteSpace(identity.CDBoxObjectType) ? "未记录" : identity.CDBoxObjectType);
            text.Append("\n  关联管线长度标注：").Append(annotationIds.Count.ToString(CultureInfo.InvariantCulture)).Append(" 个");
            text.Append("\n  状态：").Append(duplicates == 0 ? "正常" : "发现重复 CDBoxObjectId，请重新标注复制得到的管线");
            return text.ToString();
        }

        private static string GetValue(IDictionary<string, string> values, string key)
        {
            string value;
            return values != null && values.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static bool IsGuid(string value)
        {
            Guid parsed;
            return Guid.TryParse(value, out parsed);
        }

        private static string GetHandleText(ObjectId objectId)
        {
            if (objectId.IsNull) return string.Empty;
            try { return objectId.Handle.ToString(); }
            catch { return string.Empty; }
        }

        private sealed class AnnotationMetadata
        {
            public string AnnotationId { get; set; }
            public string AnnotationType { get; set; }
            public string AnnotationPart { get; set; }
            public string SourceObjectId { get; set; }
            public string SourceHandle { get; set; }
            public string GroupName { get; set; }
            public string MetadataVersion { get; set; }
            public string BindingState { get; set; }
            public string UserText { get; set; }
            public string SystemLengthText { get; set; }
            public string DetachedLengthToken { get; set; }
            public string AnchorKind { get; set; }
            public string AnchorParameter { get; set; }
            public string AnchorPointX { get; set; }
            public string AnchorPointY { get; set; }
            public string AnchorPointZ { get; set; }
        }

        private sealed class ObjectIdentity
        {
            public string CDBoxObjectId { get; set; }
            public string CDBoxObjectType { get; set; }
            public string MetadataVersion { get; set; }
        }

        private sealed class AnnotationMember
        {
            public ObjectId ObjectId { get; set; }
            public AnnotationMetadata Metadata { get; set; }
        }
    }
}
