using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using TCPipeAutoDraw.Core.Cad;
using TCPipeAutoDraw.Modules.LayerManager;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace TCPipeAutoDraw.Modules.NodeAnnotation
{
    /// <summary>
    /// 节点/检查井标注。
    /// 交互方式：移动光标指定文字位置，预览自动吸附最近井对象并显示红色预览引线；落图时只生成文字，不生成引线。
    /// </summary>
    public static class NodeAnnotationService
    {
        private const double DuplicateTolerance = 0.001;
        private const string QuantityXrecordName = QuantityPipeAttributeService.PipeAttributeXrecordName;

        public static NodeAnnotationResult SelectAndAnnotate(Document doc, NodeAnnotationOptions options)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            options = NormalizeOptions(options);

            List<NodeAnnotationCandidate> candidates = CollectNodeCandidates(doc);
            if (candidates.Count == 0)
            {
                return new NodeAnnotationResult { Success = false, Message = "未找到可识别的节点/检查井对象。请使用图层管理器进行图层识别。" };
            }

            ObjectId textStyleId = ResolveTextStyleId(doc, options.AnnotationFontName);
            var jig = new NodeAnnotationPreviewJig(candidates, options, textStyleId);
            PromptResult dragResult = doc.Editor.Drag(jig);
            if (dragResult.Status != PromptStatus.OK)
            {
                return new NodeAnnotationResult { Success = false, Message = "已取消节点标注。" };
            }

            NodeAnnotationCandidate selected = jig.SelectedCandidate ?? FindNearestCandidate(candidates, jig.AnnotationPoint);
            if (selected == null)
            {
                return new NodeAnnotationResult { Success = false, Message = "未吸附到有效节点/检查井对象。" };
            }

            return DrawAnnotation(doc, selected, jig.AnnotationPoint, options);
        }

        private static NodeAnnotationResult DrawAnnotation(Document doc, NodeAnnotationCandidate candidate, Point3d annotationPoint, NodeAnnotationOptions options)
        {
            options = NormalizeOptions(options);
            NodeAnnotationResult result = new NodeAnnotationResult();
            result.NodeObjectId = candidate == null ? ObjectId.Null : candidate.ObjectId;
            result.NodeNo = candidate == null ? string.Empty : candidate.NodeNo;
            result.WellType = candidate == null ? string.Empty : candidate.WellType;
            result.AnnotationPoint = annotationPoint;
            result.NodeCenter = candidate == null ? Point3d.Origin : candidate.Center;
            result.AnnotationLayerName = options.AnnotationLayerName;

            if (candidate == null)
            {
                result.Success = false;
                result.Message = "节点对象无效。";
                return result;
            }

            List<NodeAnnotationLine> lines = BuildAnnotationLines(candidate, options);
            if (lines.Count == 0)
            {
                result.Success = false;
                result.Message = "没有可标注的节点内容。";
                return result;
            }

            Database db = doc.Database;
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                CadLayerService.EnsureLayer(db, tr, options.AnnotationLayerName, options.TextColorIndex);
                ObjectId textStyleId = GetExistingTextStyleId(db, tr, options.AnnotationFontName);
                double spacing = Math.Max(options.TextHeight * options.LineSpacingFactor, options.TextHeight);

                for (int i = 0; i < lines.Count; i++)
                {
                    NodeAnnotationLine line = lines[i];
                    Point3d pos = new Point3d(annotationPoint.X, annotationPoint.Y - spacing * i, annotationPoint.Z);
                    ObjectId id = DrawCenteredDbText(db, tr, pos, line.Text, options.TextHeight, options.AnnotationLayerName, line.ColorIndex, textStyleId);
                    if (!id.IsNull) result.TextObjectIds.Add(id);
                }

                tr.Commit();
            }

            result.Success = result.TextObjectIds.Count > 0;
            result.Message = result.Success ? "标注已生成。" : "文字生成失败。";
            return result;
        }

        private static List<NodeAnnotationCandidate> CollectNodeCandidates(Document doc)
        {
            var candidates = new List<NodeAnnotationCandidate>();
            var textRefs = new List<TextReference>();
            if (doc == null || doc.Database == null) return candidates;

            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord space = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead, false) as BlockTableRecord;
                if (space == null)
                {
                    tr.Commit();
                    return candidates;
                }

                foreach (ObjectId id in space)
                {
                    Entity entity = null;
                    try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                    catch { continue; }
                    if (entity == null) continue;

                    TextReference textRef;
                    if (TryCreateTextReference(entity, out textRef)) textRefs.Add(textRef);
                }

                foreach (ObjectId id in space)
                {
                    Entity entity = null;
                    try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                    catch { continue; }
                    if (entity == null) continue;

                    QuantityPipeAttributes attrs;
                    bool hasSavedAttributes = TryReadQuantityAttributes(entity, tr, out attrs);
                    LayerMetadata layerMetadata = GetEntityLayerMetadata(db, tr, entity);
                    string sourceText = BuildSourceText(tr, entity, attrs, layerMetadata);

                    if (!IsPotentialNodeEntity(entity, attrs, hasSavedAttributes, sourceText, layerMetadata)) continue;

                    Point3d center;
                    double diagonal;
                    if (!TryGetEntityCenter(entity, out center, out diagonal)) continue;

                    if (attrs == null) attrs = QuantityPipeAttributes.DefaultNodeWell.Clone();
                    attrs.ObjectKind = QuantityPipeAttributes.KindNodeWell;

                    string resolvedWellType = ResolveCandidateWellType(tr, entity, attrs, hasSavedAttributes, layerMetadata);
                    if (string.IsNullOrWhiteSpace(attrs.WellType)) attrs.WellType = resolvedWellType;
                    if (string.IsNullOrWhiteSpace(attrs.WellSpec)) attrs.WellSpec = InferWellSpec(sourceText);
                    if (string.IsNullOrWhiteSpace(attrs.NodeNo)) attrs.NodeNo = ExtractNodeNoFromEntityText(entity);

                    var candidate = new NodeAnnotationCandidate
                    {
                        ObjectId = id,
                        Center = center,
                        NodeNo = CleanNodeNoForAnnotation(attrs.NodeNo),
                        WellType = resolvedWellType,
                        WellDepth = attrs.WellDepth,
                        ShaftLength = attrs.ShaftLength,
                        LayerName = entity.Layer ?? string.Empty,
                        SourceText = sourceText,
                        ExtentsDiagonal = diagonal,
                        HasSavedAttributes = hasSavedAttributes
                    };
                    candidates.Add(candidate);
                }

                tr.Commit();
            }

            FillMissingNodeNumbers(candidates, textRefs);
            return MergeDuplicateCandidates(candidates);
        }

        private static bool IsPotentialNodeEntity(Entity entity, QuantityPipeAttributes attrs, bool hasSavedAttributes, string sourceText, LayerMetadata layerMetadata)
        {
            if (entity == null) return false;
            if (!IsSupportedNodeGeometry(entity)) return false;
            if (IsExcluded315Well(attrs, sourceText, layerMetadata)) return false;

            // 节点标注吸附范围固定为：图层管理父属性=“井”，分类=“检查、沉泥井”。
            // “检查、沉泥井”只是可吸附分类，不参与判断井类型；井类型必须读取对象属性中的 WellType。
            return IsApprovedWellLayer(layerMetadata, attrs);
        }

        private static bool IsSupportedNodeGeometry(Entity entity)
        {
            if (entity == null) return false;
            if (entity is Circle || entity is BlockReference || entity is DBPoint || entity is Ellipse) return true;
            if (entity is Autodesk.AutoCAD.DatabaseServices.Polyline) return true;
            if (entity is Polyline2d || entity is Polyline3d) return true;
            if (entity is Arc) return true;
            return false;
        }

        private static bool IsApprovedWellLayer(LayerMetadata layerMetadata, QuantityPipeAttributes attrs)
        {
            string parentGroup = layerMetadata == null ? string.Empty : (layerMetadata.ParentGroup ?? string.Empty);
            string parentClass = layerMetadata == null ? string.Empty : (layerMetadata.ParentClass ?? string.Empty);

            // 若当前图层元数据为空，允许使用对象已保存属性中的图层父属性/分类作为兜底，
            // 但仍必须满足同一套固定规则，避免 315井 或其他无关块因名称相似被吸附。
            if (string.IsNullOrWhiteSpace(parentGroup) && attrs != null) parentGroup = attrs.LayerParentGroup ?? string.Empty;
            if (string.IsNullOrWhiteSpace(parentClass) && attrs != null) parentClass = attrs.LayerParentClass ?? string.Empty;

            return TextEquals(parentGroup, "井") && TextEquals(parentClass, "检查沉泥井");
        }

        private static bool IsExcluded315Well(QuantityPipeAttributes attrs, string sourceText, LayerMetadata layerMetadata)
        {
            string wellSpec = attrs == null ? string.Empty : (attrs.WellSpec ?? string.Empty);
            if (Regex.IsMatch(wellSpec, @"(?<!\d)315(?!\d)", RegexOptions.IgnoreCase)) return true;

            string layerMetaText = string.Empty;
            if (layerMetadata != null)
            {
                layerMetaText = (layerMetadata.ParentGroup ?? string.Empty) + " " + (layerMetadata.ParentClass ?? string.Empty) + " " + (layerMetadata.TagText ?? string.Empty);
            }

            string text = (sourceText ?? string.Empty) + " " + layerMetaText;
            return Regex.IsMatch(text, @"(?<![A-Za-z0-9])(?:φ|Φ)?\s*315\s*(?:井|检查井|沉泥井|圆井)", RegexOptions.IgnoreCase);
        }

        private static LayerMetadata GetEntityLayerMetadata(Database db, Transaction tr, Entity entity)
        {
            if (db == null || tr == null || entity == null || string.IsNullOrWhiteSpace(entity.Layer)) return new LayerMetadata();
            try
            {
                return LayerManagerService.GetLayerMetadata(db, tr, entity.Layer) ?? new LayerMetadata();
            }
            catch
            {
                return new LayerMetadata();
            }
        }

        private static string BuildSourceText(Transaction tr, Entity entity, QuantityPipeAttributes attrs, LayerMetadata layerMetadata)
        {
            string text = entity == null ? string.Empty : (entity.Layer ?? string.Empty);
            if (layerMetadata != null)
            {
                text += " " + (layerMetadata.ParentGroup ?? string.Empty) + " " + (layerMetadata.ParentClass ?? string.Empty) + " " + (layerMetadata.TagText ?? string.Empty);
            }

            if (attrs != null)
            {
                text += " " + (attrs.ObjectKind ?? string.Empty)
                    + " " + (attrs.NodeNo ?? string.Empty)
                    + " " + (attrs.WellSpec ?? string.Empty)
                    + " " + (attrs.WellType ?? string.Empty)
                    + " " + (attrs.WellMaterialType ?? string.Empty)
                    + " " + (attrs.WellCoverMaterial ?? string.Empty);
            }

            string blockText = ExtractBlockReferenceText(tr, entity);
            if (!string.IsNullOrWhiteSpace(blockText)) text += " " + blockText;

            string entityText = ExtractRawEntityText(entity);
            if (!string.IsNullOrWhiteSpace(entityText)) text += " " + entityText;
            return text;
        }

        private static string ExtractBlockReferenceText(Transaction tr, Entity entity)
        {
            BlockReference block = entity as BlockReference;
            if (block == null || tr == null) return string.Empty;

            var parts = new List<string>();
            try
            {
                ObjectId btrId = !block.DynamicBlockTableRecord.IsNull ? block.DynamicBlockTableRecord : block.BlockTableRecord;
                if (!btrId.IsNull)
                {
                    BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead, false) as BlockTableRecord;
                    if (btr != null && !string.IsNullOrWhiteSpace(btr.Name)) parts.Add(btr.Name);
                }
            }
            catch
            {
            }

            try
            {
                foreach (ObjectId attId in block.AttributeCollection)
                {
                    AttributeReference attr = tr.GetObject(attId, OpenMode.ForRead, false) as AttributeReference;
                    if (attr != null && !string.IsNullOrWhiteSpace(attr.TextString)) parts.Add(attr.TextString);
                }
            }
            catch
            {
            }

            return parts.Count == 0 ? string.Empty : string.Join(" ", parts.ToArray());
        }

        private static bool TryReadQuantityAttributes(Entity entity, Transaction tr, out QuantityPipeAttributes attrs)
        {
            attrs = null;
            if (entity == null || tr == null || entity.ExtensionDictionary.IsNull) return false;

            try
            {
                DBDictionary dict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForRead, false) as DBDictionary;
                if (dict == null || !dict.Contains(QuantityXrecordName)) return false;

                Xrecord record = tr.GetObject(dict.GetAt(QuantityXrecordName), OpenMode.ForRead, false) as Xrecord;
                if (record == null || record.Data == null) return false;

                QuantityPipeAttributes value = QuantityPipeAttributes.DefaultNodeWell.Clone();
                foreach (TypedValue typedValue in record.Data)
                {
                    if (typedValue.Value == null) continue;
                    string text = typedValue.Value.ToString();
                    int idx = text.IndexOf('=');
                    if (idx <= 0) continue;
                    string key = text.Substring(0, idx).Trim();
                    string val = text.Substring(idx + 1).Trim();
                    ApplyQuantityKeyValue(value, key, val);
                }

                QuantityPipeAttributes.ApplyStructureLayerText(value);
                attrs = value;
                return true;
            }
            catch
            {
                attrs = null;
                return false;
            }
        }

        private static void ApplyQuantityKeyValue(QuantityPipeAttributes attrs, string key, string val)
        {
            if (attrs == null || string.IsNullOrWhiteSpace(key)) return;
            if (string.Equals(key, "ObjectKind", StringComparison.OrdinalIgnoreCase)) attrs.ObjectKind = val;
            else if (string.Equals(key, "LayerParentGroup", StringComparison.OrdinalIgnoreCase)) attrs.LayerParentGroup = val;
            else if (string.Equals(key, "LayerParentClass", StringComparison.OrdinalIgnoreCase)) attrs.LayerParentClass = val;
            else if (string.Equals(key, "LayerTags", StringComparison.OrdinalIgnoreCase)) attrs.LayerTags = val;
            else if (string.Equals(key, "NodeNo", StringComparison.OrdinalIgnoreCase)) attrs.NodeNo = val;
            else if (string.Equals(key, "WellSpec", StringComparison.OrdinalIgnoreCase)) attrs.WellSpec = val;
            else if (string.Equals(key, "WellType", StringComparison.OrdinalIgnoreCase)) attrs.WellType = val;
            else if (string.Equals(key, "WellMaterialType", StringComparison.OrdinalIgnoreCase)) attrs.WellMaterialType = val;
            else if (string.Equals(key, "WellCoverMaterial", StringComparison.OrdinalIgnoreCase)) attrs.WellCoverMaterial = val;
            else if (string.Equals(key, "WellDepth", StringComparison.OrdinalIgnoreCase)) attrs.WellDepth = QuantityPipeAttributes.ParseDouble(val, attrs.WellDepth);
            else if (string.Equals(key, "ShaftLength", StringComparison.OrdinalIgnoreCase)) attrs.ShaftLength = QuantityPipeAttributes.ParseDouble(val, attrs.ShaftLength);
            else if (string.Equals(key, "BackfillStructure", StringComparison.OrdinalIgnoreCase)) attrs.BackfillStructure = val;
            else if (string.Equals(key, "SandCushionThickness", StringComparison.OrdinalIgnoreCase)) attrs.SandCushionThickness = QuantityPipeAttributes.ParseDouble(val, attrs.SandCushionThickness);
            else if (string.Equals(key, "SiltWellDeductDepth500", StringComparison.OrdinalIgnoreCase)) attrs.SiltWellDeductDepth500 = QuantityPipeAttributes.ParseDouble(val, attrs.SiltWellDeductDepth500);
            else if (string.Equals(key, "SiltWellDeductDepth700", StringComparison.OrdinalIgnoreCase)) attrs.SiltWellDeductDepth700 = QuantityPipeAttributes.ParseDouble(val, attrs.SiltWellDeductDepth700);
        }

        private static void FillMissingNodeNumbers(List<NodeAnnotationCandidate> candidates, List<TextReference> textRefs)
        {
            if (candidates == null || textRefs == null || textRefs.Count == 0) return;
            foreach (NodeAnnotationCandidate candidate in candidates)
            {
                if (candidate == null || !string.IsNullOrWhiteSpace(candidate.NodeNo)) continue;

                double searchRadius = Math.Max(2.0, Math.Min(10.0, candidate.ExtentsDiagonal > 0 ? candidate.ExtentsDiagonal * 4.0 : 5.0));
                TextReference best = null;
                double bestDist = double.MaxValue;
                foreach (TextReference text in textRefs)
                {
                    if (text == null) continue;
                    string cleaned = CleanNodeNoForAnnotation(text.Text);
                    if (!IsLikelyNodeNoText(cleaned)) continue;
                    double dist = Distance2d(candidate.Center, text.Position);
                    if (dist > searchRadius) continue;
                    if (dist < bestDist)
                    {
                        best = text;
                        bestDist = dist;
                    }
                }

                if (best != null) candidate.NodeNo = CleanNodeNoForAnnotation(best.Text);
            }
        }

        private static List<NodeAnnotationCandidate> MergeDuplicateCandidates(List<NodeAnnotationCandidate> input)
        {
            var output = new List<NodeAnnotationCandidate>();
            if (input == null) return output;

            foreach (NodeAnnotationCandidate candidate in input)
            {
                if (candidate == null) continue;
                NodeAnnotationCandidate existing = null;
                foreach (NodeAnnotationCandidate item in output)
                {
                    if (Distance2d(candidate.Center, item.Center) <= Math.Max(0.05, Math.Min(0.50, Math.Max(candidate.ExtentsDiagonal, item.ExtentsDiagonal) * 0.05)))
                    {
                        existing = item;
                        break;
                    }
                }

                if (existing == null)
                {
                    output.Add(candidate);
                    continue;
                }

                if (IsBetterCandidate(candidate, existing))
                {
                    output.Remove(existing);
                    output.Add(candidate);
                }
                else
                {
                    MergeCandidateInfo(existing, candidate);
                }
            }

            return output;
        }

        private static bool IsBetterCandidate(NodeAnnotationCandidate candidate, NodeAnnotationCandidate existing)
        {
            if (candidate == null) return false;
            if (existing == null) return true;
            if (candidate.HasSavedAttributes != existing.HasSavedAttributes) return candidate.HasSavedAttributes;
            bool candidateHasNo = !string.IsNullOrWhiteSpace(candidate.NodeNo);
            bool existingHasNo = !string.IsNullOrWhiteSpace(existing.NodeNo);
            if (candidateHasNo != existingHasNo) return candidateHasNo;
            bool candidateHasDepth = candidate.WellDepth > 0 || candidate.ShaftLength > 0;
            bool existingHasDepth = existing.WellDepth > 0 || existing.ShaftLength > 0;
            if (candidateHasDepth != existingHasDepth) return candidateHasDepth;
            return candidate.ExtentsDiagonal > existing.ExtentsDiagonal;
        }

        private static void MergeCandidateInfo(NodeAnnotationCandidate target, NodeAnnotationCandidate source)
        {
            if (target == null || source == null) return;
            if (string.IsNullOrWhiteSpace(target.NodeNo)) target.NodeNo = source.NodeNo;
            if (string.IsNullOrWhiteSpace(target.WellType)) target.WellType = source.WellType;
            if (target.WellDepth <= 0) target.WellDepth = source.WellDepth;
            if (target.ShaftLength <= 0) target.ShaftLength = source.ShaftLength;
            if (string.IsNullOrWhiteSpace(target.SourceText)) target.SourceText = source.SourceText;
        }

        private static List<NodeAnnotationLine> BuildAnnotationLines(NodeAnnotationCandidate candidate, NodeAnnotationOptions options)
        {
            options = NormalizeOptions(options);
            var lines = new List<NodeAnnotationLine>();
            if (candidate == null) return lines;

            string nodeNo = CleanNodeNoForAnnotation(candidate.NodeNo);
            if (string.IsNullOrWhiteSpace(nodeNo)) nodeNo = "未编号";

            // 按示例图二排列：节点编号、井深、井筒；仅沉泥井追加“沉泥井”。
            // 深度固定保留两位小数，英文冒号后不加空格。
            lines.Add(new NodeAnnotationLine(nodeNo, options.NodeNoColorIndex));
            lines.Add(new NodeAnnotationLine("井深:" + FormatMeter(candidate.WellDepth, 2), options.TextColorIndex));
            lines.Add(new NodeAnnotationLine("井筒:" + FormatMeter(candidate.ShaftLength, 2), options.TextColorIndex));
            if (IsSiltWell(candidate)) lines.Add(new NodeAnnotationLine("沉泥井", options.TextColorIndex));
            return lines;
        }

        private static bool IsSiltWell(NodeAnnotationCandidate candidate)
        {
            if (candidate == null) return false;
            return IsSiltWellType(candidate.WellType);
        }

        /// <summary>
        /// 解析节点井类型。
        /// 注意：图层分类“检查、沉泥井 / 检查沉泥井”只表示可参与节点吸附，不能直接判定为沉泥井；
        /// 否则所有检查井都会被误标成“沉泥井”。
        /// </summary>
        private static string ResolveCandidateWellType(Transaction tr, Entity entity, QuantityPipeAttributes attrs, bool hasSavedAttributes, LayerMetadata layerMetadata)
        {
            // 井对象所属分类固定为“检查、沉泥井”，该分类只说明“这是可标注井对象”。
            // 真正的“检查井/沉泥井”必须读取对象属性表里的“井类型”(WellType)。
            if (hasSavedAttributes && attrs != null)
            {
                string savedType = NormalizeWellTypeValue(attrs.WellType);
                if (!string.IsNullOrWhiteSpace(savedType)) return savedType;
            }

            string metadataType = InferWellTypeFromLayerClass(layerMetadata == null ? string.Empty : layerMetadata.ParentClass);
            if (!string.IsNullOrWhiteSpace(metadataType)) return metadataType;

            string entityTypeSource = BuildEntityTypeSourceText(tr, entity);
            string entityType = InferWellTypeFromExplicitText(entityTypeSource);
            if (!string.IsNullOrWhiteSpace(entityType)) return entityType;

            return "检查井";
        }

        private static string BuildEntityTypeSourceText(Transaction tr, Entity entity)
        {
            if (entity == null) return string.Empty;
            string text = entity.Layer ?? string.Empty;

            string blockText = ExtractBlockReferenceText(tr, entity);
            if (!string.IsNullOrWhiteSpace(blockText)) text += " " + blockText;

            string entityText = ExtractRawEntityText(entity);
            if (!string.IsNullOrWhiteSpace(entityText)) text += " " + entityText;

            return text;
        }

        private static string InferWellTypeFromLayerClass(string parentClass)
        {
            string value = NormalizeWellClassText(parentClass);
            if (value.Length == 0) return string.Empty;

            if (value == "沉泥" || value == "沉泥井") return "沉泥井";
            if (value == "检查" || value == "检查井") return "检查井";
            if (value == "跌水" || value == "跌水井") return "跌水井";

            // “检查沉泥井”是合并分类，只用于筛选可吸附对象，不用于判定井类型。
            return string.Empty;
        }

        private static string InferWellTypeFromExplicitText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            string cleaned = RemoveAmbiguousWellTypePhrases(NormalizeDbTextString(text));
            if (ContainsAny(cleaned, "沉泥井", "沉泥")) return "沉泥井";
            if (ContainsAny(cleaned, "跌水井", "跌水")) return "跌水井";
            if (ContainsAny(cleaned, "检查井", "检查", "雨水", "污水")) return "检查井";
            return string.Empty;
        }

        private static bool IsSiltWellType(string wellType)
        {
            string value = NormalizeWellTypeValue(wellType);
            return string.Equals(value, "沉泥井", StringComparison.CurrentCultureIgnoreCase);
        }

        private static string NormalizeWellTypeValue(string wellType)
        {
            if (string.IsNullOrWhiteSpace(wellType)) return string.Empty;
            string value = NormalizeWellClassText(NormalizeDbTextString(wellType));
            if (value.Length == 0) return string.Empty;

            // “检查、沉泥井”是图层分类，不是具体井类型；若误写入井类型，不能按沉泥井处理。
            if (value.IndexOf("检查", StringComparison.CurrentCultureIgnoreCase) >= 0
                && value.IndexOf("沉泥", StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                return string.Empty;
            }

            if (value == "沉泥" || value == "沉泥井") return "沉泥井";
            if (value == "检查" || value == "检查井") return "检查井";
            if (value == "跌水" || value == "跌水井") return "跌水井";
            return NormalizeDbTextString(wellType).Trim();
        }

        private static bool IsAmbiguousWellClass(string parentClass)
        {
            string value = NormalizeWellClassText(parentClass);
            if (value.Length == 0) return false;
            return value == "检查沉泥井" || (value.IndexOf("检查", StringComparison.CurrentCultureIgnoreCase) >= 0 && value.IndexOf("沉泥", StringComparison.CurrentCultureIgnoreCase) >= 0);
        }

        private static string NormalizeWellClassText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text.Replace(" ", string.Empty)
                .Replace("　", string.Empty)
                .Replace("、", string.Empty)
                .Replace(",", string.Empty)
                .Replace("，", string.Empty)
                .Replace("/", string.Empty)
                .Replace("\\", string.Empty)
                .Replace("|", string.Empty)
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .Trim();
        }

        private static bool TextEquals(string left, string right)
        {
            return string.Equals(NormalizeWellClassText(left), NormalizeWellClassText(right), StringComparison.CurrentCultureIgnoreCase);
        }

        private static string RemoveAmbiguousWellTypePhrases(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string cleaned = text;
            cleaned = Regex.Replace(cleaned, @"检查\s*井?\s*[、,，/\\;；|_\-]*\s*沉泥井", "检查井", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"检查\s*沉泥井", "检查井", RegexOptions.IgnoreCase);
            return cleaned;
        }

        private static ObjectId DrawCenteredDbText(Database db, Transaction tr, Point3d position, string text, double height, string layerName, short colorIndex, ObjectId textStyleId)
        {
            BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite, false) as BlockTableRecord;
            if (btr == null) return ObjectId.Null;

            var dbText = new DBText();
            dbText.SetDatabaseDefaults(db);
            dbText.Height = height <= 0 ? 1.0 : height;
            dbText.TextString = NormalizeDbTextString(text);
            dbText.Layer = layerName;
            dbText.ColorIndex = colorIndex <= 0 ? (short)7 : colorIndex;
            if (!textStyleId.IsNull) dbText.TextStyleId = textStyleId;
            dbText.HorizontalMode = TextHorizontalMode.TextCenter;
            dbText.VerticalMode = TextVerticalMode.TextVerticalMid;
            dbText.Position = position;
            dbText.AlignmentPoint = position;

            ObjectId id = btr.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);
            try { dbText.AdjustAlignment(db); } catch { }
            return id;
        }

        private static ObjectId ResolveTextStyleId(Document doc, string textStyleName)
        {
            if (doc == null || doc.Database == null) return ObjectId.Null;
            try
            {
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    ObjectId id = GetExistingTextStyleId(doc.Database, tr, textStyleName);
                    tr.Commit();
                    return id;
                }
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        private static ObjectId GetExistingTextStyleId(Database db, Transaction tr, string textStyleName)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(textStyleName)) return ObjectId.Null;
            try
            {
                TextStyleTable table = tr.GetObject(db.TextStyleTableId, OpenMode.ForRead, false) as TextStyleTable;
                if (table == null) return ObjectId.Null;
                string target = textStyleName.Trim();
                if (table.Has(target)) return table[target];
                foreach (ObjectId id in table)
                {
                    TextStyleTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as TextStyleTableRecord;
                    if (record == null || record.IsErased) continue;
                    if (string.Equals(record.Name, target, StringComparison.CurrentCultureIgnoreCase)) return id;
                }
            }
            catch
            {
            }
            return ObjectId.Null;
        }

        private static NodeAnnotationOptions NormalizeOptions(NodeAnnotationOptions options)
        {
            options = options ?? NodeAnnotationOptions.Default;
            if (options.TextHeight <= 0) options.TextHeight = 1.0;
            // 节点标注深度固定保留两位小数。
            options.DecimalPlaces = 2;
            if (string.IsNullOrWhiteSpace(options.AnnotationFontName)) options.AnnotationFontName = "宋体";
            if (string.IsNullOrWhiteSpace(options.AnnotationLayerName)) options.AnnotationLayerName = "ZJ";
            if (options.LineSpacingFactor <= 0.5) options.LineSpacingFactor = 1.45;
            if (options.NodeNoColorIndex <= 0) options.NodeNoColorIndex = 1;
            if (options.TextColorIndex <= 0) options.TextColorIndex = 7;
            if (options.PreviewLeaderColorIndex <= 0) options.PreviewLeaderColorIndex = 1;
            return options;
        }

        private static bool TryCreateTextReference(Entity entity, out TextReference reference)
        {
            reference = null;
            if (entity == null) return false;
            string text = ExtractRawEntityText(entity);
            if (string.IsNullOrWhiteSpace(text)) return false;

            Point3d pos;
            double diag;
            if (!TryGetEntityCenter(entity, out pos, out diag)) return false;
            reference = new TextReference { Text = text, Position = pos };
            return true;
        }

        private static string ExtractNodeNoFromEntityText(Entity entity)
        {
            string text = ExtractRawEntityText(entity);
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            text = CleanNodeNoForAnnotation(text);
            return IsLikelyNodeNoText(text) ? text : string.Empty;
        }

        private static string ExtractRawEntityText(Entity entity)
        {
            if (entity == null) return string.Empty;
            try
            {
                DBText dbText = entity as DBText;
                if (dbText != null) return dbText.TextString ?? string.Empty;
                MText mText = entity as MText;
                if (mText != null) return mText.Contents ?? string.Empty;
                AttributeReference attrRef = entity as AttributeReference;
                if (attrRef != null) return attrRef.TextString ?? string.Empty;
            }
            catch
            {
            }
            return string.Empty;
        }

        private static string CleanNodeNoForAnnotation(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string value = NormalizeDbTextString(text);
            int lineBreak = value.IndexOf(' ');
            if (lineBreak > 0 && value.IndexOf("井", StringComparison.CurrentCultureIgnoreCase) >= 0) value = value.Substring(0, lineBreak);
            value = Regex.Replace(value, @"[（(]\s*[-+]?\d+(?:\.\d+)?\s*m?\s*[）)]\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();
            value = value.Trim('：', ':', '，', ',', ';', '；');
            return value;
        }

        private static bool IsLikelyNodeNoText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string value = text.Trim();
            if (value.Length > 32) return false;
            if (ContainsAny(value, "井深", "井筒", "沉泥", "面积", "长度", "开挖", "表面积", "m", "M", "㎡")) return false;
            return Regex.IsMatch(value, @"^[A-Za-z]*\d+(?:[-—–]\d+)?(?:[A-Za-z0-9_-]*)$", RegexOptions.IgnoreCase);
        }

        private static string InferWellType(string sourceText)
        {
            string type = InferWellTypeFromExplicitText(sourceText);
            return string.IsNullOrWhiteSpace(type) ? "检查井" : type;
        }

        private static string InferWellSpec(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText)) return string.Empty;
            Match phi = Regex.Match(sourceText, @"[φΦ]\s*(?<n>\d{3,4})");
            if (phi.Success) return "φ" + phi.Groups["n"].Value;
            Match well = Regex.Match(sourceText, @"(?<n>\d{3,4})(?:\s*)?(?:检查井|沉泥井|跌水井|井)");
            if (well.Success) return "φ" + well.Groups["n"].Value;
            return string.Empty;
        }

        private static bool TryGetEntityCenter(Entity entity, out Point3d center, out double diagonal)
        {
            center = Point3d.Origin;
            diagonal = 0.0;
            if (entity == null) return false;

            try
            {
                Circle circle = entity as Circle;
                if (circle != null)
                {
                    center = circle.Center;
                    diagonal = circle.Radius * 2.0;
                    return true;
                }

                DBPoint dbPoint = entity as DBPoint;
                if (dbPoint != null)
                {
                    center = dbPoint.Position;
                    diagonal = 0.0;
                    return true;
                }

                BlockReference block = entity as BlockReference;
                if (block != null)
                {
                    center = block.Position;
                    try { diagonal = GetExtentsDiagonal2d(block.GeometricExtents); } catch { diagonal = 0.0; }
                    return true;
                }

                DBText dbText = entity as DBText;
                if (dbText != null)
                {
                    center = dbText.Position;
                    try { diagonal = GetExtentsDiagonal2d(dbText.GeometricExtents); } catch { diagonal = dbText.Height; }
                    return true;
                }

                MText mText = entity as MText;
                if (mText != null)
                {
                    center = mText.Location;
                    try { diagonal = GetExtentsDiagonal2d(mText.GeometricExtents); } catch { diagonal = mText.TextHeight; }
                    return true;
                }

                Extents3d ext = entity.GeometricExtents;
                center = new Point3d((ext.MinPoint.X + ext.MaxPoint.X) / 2.0, (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0, (ext.MinPoint.Z + ext.MaxPoint.Z) / 2.0);
                diagonal = GetExtentsDiagonal2d(ext);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static double GetExtentsDiagonal2d(Extents3d ext)
        {
            double dx = ext.MaxPoint.X - ext.MinPoint.X;
            double dy = ext.MaxPoint.Y - ext.MinPoint.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static string FormatMeter(double value, int decimalPlaces)
        {
            if (decimalPlaces < 0) decimalPlaces = 0;
            if (decimalPlaces > 6) decimalPlaces = 6;
            string format = decimalPlaces == 0 ? "0" : "0." + new string('0', decimalPlaces);
            return value.ToString(format, CultureInfo.InvariantCulture) + "m";
        }

        private static string NormalizeDbTextString(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string normalized = text.Replace("\\P", " ").Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
            normalized = Regex.Replace(normalized, @"\{[^;]*;", string.Empty);
            normalized = normalized.Replace("}", string.Empty);
            return Regex.Replace(normalized, @"\s+", " ").Trim();
        }

        private static NodeAnnotationCandidate FindNearestCandidate(List<NodeAnnotationCandidate> candidates, Point3d point)
        {
            if (candidates == null || candidates.Count == 0) return null;
            NodeAnnotationCandidate best = null;
            double bestDistance = double.MaxValue;
            foreach (NodeAnnotationCandidate candidate in candidates)
            {
                if (candidate == null) continue;
                double distance = Distance2d(candidate.Center, point);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static double Distance2d(Point3d a, Point3d b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(text) || values == null) return false;
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private sealed class TextReference
        {
            public string Text { get; set; }
            public Point3d Position { get; set; }
        }

        private sealed class NodeAnnotationPreviewJig : DrawJig
        {
            private readonly List<NodeAnnotationCandidate> _candidates;
            private readonly NodeAnnotationOptions _options;
            private readonly ObjectId _textStyleId;
            private Point3d _annotationPoint;
            private NodeAnnotationCandidate _selectedCandidate;

            public NodeAnnotationPreviewJig(List<NodeAnnotationCandidate> candidates, NodeAnnotationOptions options, ObjectId textStyleId)
            {
                _candidates = candidates ?? new List<NodeAnnotationCandidate>();
                _options = NormalizeOptions(options);
                _textStyleId = textStyleId;
                _annotationPoint = _candidates.Count > 0
                    ? new Point3d(_candidates[0].Center.X + _options.TextHeight * 8.0, _candidates[0].Center.Y + _options.TextHeight * 5.0, _candidates[0].Center.Z)
                    : Point3d.Origin;
                _selectedCandidate = FindNearestCandidate(_candidates, _annotationPoint);
            }

            public Point3d AnnotationPoint
            {
                get { return _annotationPoint; }
            }

            public NodeAnnotationCandidate SelectedCandidate
            {
                get { return _selectedCandidate; }
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var options = new JigPromptPointOptions("\n指定节点标注文字位置（预览自动吸附最近井对象）");
                options.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NoZeroResponseAccepted;
                PromptPointResult result = prompts.AcquirePoint(options);
                if (result.Status != PromptStatus.OK) return SamplerStatus.Cancel;

                if (result.Value.DistanceTo(_annotationPoint) < DuplicateTolerance)
                {
                    return SamplerStatus.NoChange;
                }

                _annotationPoint = result.Value;
                _selectedCandidate = FindNearestCandidate(_candidates, _annotationPoint);
                return SamplerStatus.OK;
            }

            protected override bool WorldDraw(WorldDraw draw)
            {
                if (draw == null || draw.Geometry == null) return true;
                _selectedCandidate = FindNearestCandidate(_candidates, _annotationPoint);
                if (_selectedCandidate == null) return true;

                List<NodeAnnotationLine> lines = BuildAnnotationLines(_selectedCandidate, _options);
                double spacing = Math.Max(_options.TextHeight * _options.LineSpacingFactor, _options.TextHeight);
                for (int i = 0; i < lines.Count; i++)
                {
                    Point3d pos = new Point3d(_annotationPoint.X, _annotationPoint.Y - spacing * i, _annotationPoint.Z);
                    DrawPreviewText(draw, pos, lines[i].Text, _options.TextHeight, lines[i].ColorIndex, _textStyleId);
                }

                using (var leader = new Autodesk.AutoCAD.DatabaseServices.Polyline())
                {
                    leader.AddVertexAt(0, new Point2d(_selectedCandidate.Center.X, _selectedCandidate.Center.Y), 0, 0, 0);
                    leader.AddVertexAt(1, new Point2d(_annotationPoint.X, _annotationPoint.Y), 0, 0, 0);
                    leader.ColorIndex = _options.PreviewLeaderColorIndex;
                    draw.Geometry.Draw(leader);
                }

                return true;
            }

            private static void DrawPreviewText(WorldDraw draw, Point3d position, string text, double textHeight, short colorIndex, ObjectId textStyleId)
            {
                if (draw == null || draw.Geometry == null || string.IsNullOrWhiteSpace(text)) return;
                try
                {
                    using (var dbText = new DBText())
                    {
                        dbText.Height = textHeight <= 0 ? 1.0 : textHeight;
                        dbText.TextString = NormalizeDbTextString(text);
                        dbText.ColorIndex = colorIndex <= 0 ? (short)7 : colorIndex;
                        dbText.HorizontalMode = TextHorizontalMode.TextCenter;
                        dbText.VerticalMode = TextVerticalMode.TextVerticalMid;
                        dbText.Position = position;
                        dbText.AlignmentPoint = position;
                        if (!textStyleId.IsNull) dbText.TextStyleId = textStyleId;
                        draw.Geometry.Draw(dbText);
                    }
                }
                catch
                {
                    try
                    {
                        draw.Geometry.Text(position, Vector3d.ZAxis, Vector3d.XAxis, textHeight <= 0 ? 1.0 : textHeight, 1.0, 0.0, NormalizeDbTextString(text));
                    }
                    catch { }
                }
            }
        }
    }
}
