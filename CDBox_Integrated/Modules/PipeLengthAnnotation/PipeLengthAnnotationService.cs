using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using TCPipeAutoDraw.Core.Cad;
using TCPipeAutoDraw.Modules.SurfaceAreaAnnotation;
using TCPipeAutoDraw.Modules.LayerManager;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    /// <summary>
    /// 管线长度标注服务。
    /// 点取指定多段线，读取其实际曲线长度，然后按用户设置生成上方文字、横线、引线和可选的横线下方注记。
    /// </summary>
    public static class PipeLengthAnnotationService
    {
        private const double DuplicateTolerance = 0.001;
        private const string AnnotationSourceXrecordName = "CDBoxAnnotationSource";

        public static PipeLengthAnnotationResult SelectCalculateAndAnnotate(Document doc, PipeLengthAnnotationOptions options)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            options = NormalizeOptions(options);

            Editor ed = doc.Editor;
            PromptPointOptions ppo = new PromptPointOptions("\n点取引线拉出位置，按 ESC 退出");
            ppo.AllowNone = false;

            PromptPointResult ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK)
            {
                bool isCancelled = ppr.Status == PromptStatus.Cancel;
                string message = isCancelled
                    ? "已退出管线长度标注。"
                    : "未获取到点取位置。";
                return new PipeLengthAnnotationResult { Success = false, IsCancelled = isCancelled, Message = message };
            }

            ObjectId pipeId;
            Point3d leaderStartPoint;
            string pickError;
            if (!TryFindPolylineAtPoint(doc, ppr.Value, out pipeId, out leaderStartPoint, out pickError))
            {
                return new PipeLengthAnnotationResult { Success = false, Message = pickError };
            }

            PipeLengthAnnotationResult previewSource;
            string previewError;
            if (!TryBuildPreviewSource(doc, pipeId, options, out previewSource, out previewError))
            {
                return new PipeLengthAnnotationResult { Success = false, Message = previewError };
            }

            string previewText = BuildAnnotationText(options, previewSource);
            string bottomPreviewText = BuildBottomAnnotationText(options, previewSource);
            ObjectId previewTextStyleId = ResolveTextStyleId(doc, options.AnnotationFontName);
            var jig = new PipeLengthAnnotationPreviewJig(leaderStartPoint, previewText, bottomPreviewText, options.TextHeight, previewTextStyleId);
            PromptResult dragResult = ed.Drag(jig);
            if (dragResult.Status != PromptStatus.OK)
            {
                bool isCancelled = dragResult.Status == PromptStatus.Cancel;
                return new PipeLengthAnnotationResult
                {
                    Success = false,
                    IsCancelled = isCancelled,
                    Message = isCancelled ? "已退出管线长度标注。" : "未获取到注记位置。"
                };
            }

            return CalculateAndAnnotate(doc, pipeId, leaderStartPoint, jig.AnnotationPoint, options);
        }

        private static bool TryFindPolylineAtPoint(Document doc, Point3d pickedPoint, out ObjectId pipeId, out Point3d leaderStartPoint, out string errorMessage)
        {
            pipeId = ObjectId.Null;
            leaderStartPoint = pickedPoint;
            errorMessage = string.Empty;

            if (doc == null)
            {
                errorMessage = "当前文档无效。";
                return false;
            }

            double tolerance = GetPointPickTolerance(doc.Editor);
            double bestDistance = double.MaxValue;
            Point3d bestPoint = pickedPoint;
            ObjectId bestId = ObjectId.Null;

            try
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead, false) as BlockTableRecord;
                    if (btr == null)
                    {
                        errorMessage = "无法读取当前空间。";
                        return false;
                    }

                    foreach (ObjectId id in btr)
                    {
                        Curve curve = null;
                        try { curve = tr.GetObject(id, OpenMode.ForRead, false) as Curve; }
                        catch { continue; }

                        if (!IsSupportedPolylineCurve(curve)) continue;

                        Point3d closestPoint;
                        try { closestPoint = curve.GetClosestPointTo(pickedPoint, false); }
                        catch { continue; }

                        double distance = closestPoint.DistanceTo(pickedPoint);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestPoint = closestPoint;
                            bestId = id;
                        }
                    }

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                errorMessage = "查找管线多段线失败：" + ex.Message;
                return false;
            }

            if (bestId.IsNull || bestDistance > tolerance)
            {
                errorMessage = "未在点取位置附近找到管线多段线；需直接点取目标管线。";
                return false;
            }

            pipeId = bestId;
            leaderStartPoint = bestPoint;
            return true;
        }

        private static bool IsSupportedPolylineCurve(Curve curve)
        {
            return curve is Autodesk.AutoCAD.DatabaseServices.Polyline
                || curve is Polyline2d
                || curve is Polyline3d;
        }

        private static double GetPointPickTolerance(Editor ed)
        {
            const double minTolerance = 0.001;
            const double maxTolerance = 0.50;

            try
            {
                using (ViewTableRecord view = ed.GetCurrentView())
                {
                    object screenSizeObj = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("SCREENSIZE");
                    if (screenSizeObj is Point2d)
                    {
                        Point2d screenSize = (Point2d)screenSizeObj;
                        if (screenSize.Y > 1.0 && view.Height > 0.0)
                        {
                            double pixelSize = view.Height / screenSize.Y;
                            double tolerance = pixelSize * 2.0;
                            if (tolerance < minTolerance) return minTolerance;
                            if (tolerance > maxTolerance) return maxTolerance;
                            return tolerance;
                        }
                    }
                }
            }
            catch { }

            return 0.05;
        }

        private static bool TryBuildPreviewSource(Document doc, ObjectId pipeId, PipeLengthAnnotationOptions options, out PipeLengthAnnotationResult result, out string errorMessage)
        {
            result = new PipeLengthAnnotationResult();
            errorMessage = string.Empty;

            try
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Curve curve = tr.GetObject(pipeId, OpenMode.ForRead, false) as Curve;
                    if (curve == null)
                    {
                        errorMessage = "所选对象不是可计算长度的曲线。";
                        return false;
                    }

                    if (!IsSupportedPolylineCurve(curve))
                    {
                        errorMessage = "仅支持多段线、二维多段线、三维多段线。";
                        return false;
                    }

                    LayerMetadata sourceMetadata = GetEffectiveSourceMetadata(db, tr, curve.Layer);

                    result.Success = true;
                    result.PipeObjectId = pipeId;
                    result.PipeLayerName = curve.Layer;
                    result.Length = GetCurveLength(curve);
                    ApplySourceMetadataToResult(result, sourceMetadata);
                    result.AnnotationLayerName = ResolveAnnotationLayer(options, sourceMetadata);
                    result.AnnotationFontName = options.AnnotationFontName;

                    tr.Commit();
                }

                ApplyQuantityInfoToResult(result, TryReadQuantityInfo(doc, pipeId), options);
                return true;
            }
            catch (System.Exception ex)
            {
                errorMessage = "读取管线长度失败：" + ex.Message;
                return false;
            }
        }

        public static PipeLengthAnnotationResult CalculateAndAnnotate(Document doc, ObjectId pipeId, Point3d leaderStartPoint, Point3d annotationPoint, PipeLengthAnnotationOptions options)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            options = NormalizeOptions(options);

            var result = new PipeLengthAnnotationResult();
            result.PipeObjectId = pipeId;
            result.AnnotationPoint = annotationPoint;

            QuantityPipeSelectionInfo quantityInfo = TryReadQuantityInfo(doc, pipeId);

            Database db = doc.Database;
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Curve curve = tr.GetObject(pipeId, OpenMode.ForRead, false) as Curve;
                if (curve == null)
                {
                    result.Success = false;
                    result.Message = "所选对象不是可计算长度的曲线。";
                    return result;
                }

                if (!IsSupportedPolylineCurve(curve))
                {
                    result.Success = false;
                    result.Message = "仅支持多段线、二维多段线、三维多段线。";
                    return result;
                }

                result.PipeLayerName = curve.Layer;
                result.Length = GetCurveLength(curve);
                LayerMetadata sourceMetadata = GetEffectiveSourceMetadata(db, tr, curve.Layer);
                ApplySourceMetadataToResult(result, sourceMetadata);
                ApplyQuantityInfoToResult(result, quantityInfo, options);
                result.AnnotationLayerName = ResolveAnnotationLayer(options, sourceMetadata);
                result.AnnotationFontName = options.AnnotationFontName;

                EnsureAutoAnnotationLayerMetadata(db, tr, result.AnnotationLayerName, result, options);

                string text = BuildAnnotationText(options, result);
                string bottomText = BuildBottomAnnotationText(options, result);
                AttachmentPoint attachment = GetTextAttachment(annotationPoint, leaderStartPoint);
                PreviewLayout initialLayout = BuildPreviewLayout(text, bottomText, options.TextHeight, annotationPoint, attachment);

                ObjectId textId = DrawAnnotationDbText(db, tr, initialLayout.TopTextPoint, text, options.TextHeight, result.AnnotationLayerName, 7, options.AnnotationFontName);
                result.AnnotationObjectId = textId;
                WriteAnnotationSourceData(tr, textId, result);

                ObjectId bottomTextId = ObjectId.Null;
                if (!string.IsNullOrWhiteSpace(bottomText) && !textId.IsNull)
                {
                    bottomTextId = DrawAnnotationDbText(db, tr, initialLayout.BottomTextPoint, bottomText, options.TextHeight, result.AnnotationLayerName, 7, options.AnnotationFontName);
                    result.BottomAnnotationObjectId = bottomTextId;
                    WriteAnnotationSourceData(tr, bottomTextId, result);
                }

                Point3d underlineStart;
                Point3d underlineEnd;
                if (TryArrangeFinalAnnotation(tr, textId, bottomTextId, annotationPoint, options.TextHeight, attachment, out underlineStart, out underlineEnd))
                {
                    if (options.DrawLeader && !textId.IsNull)
                    {
                        result.LeaderObjectId = DrawLeaderByUnderline(db, tr, leaderStartPoint, underlineStart, underlineEnd, result.AnnotationLayerName, attachment);
                        WriteAnnotationSourceData(tr, result.LeaderObjectId, result);
                    }
                }

                tr.Commit();
            }

            result.Success = true;
            result.Message = "标注已生成。";
            return result;
        }

        private static double GetCurveLength(Curve curve)
        {
            try
            {
                double start = curve.StartParam;
                double end = curve.EndParam;
                return Math.Abs(curve.GetDistanceAtParameter(end) - curve.GetDistanceAtParameter(start));
            }
            catch
            {
                try { return curve.GetDistanceAtParameter(curve.EndParam); }
                catch { return 0.0; }
            }
        }


        private static QuantityPipeSelectionInfo TryReadQuantityInfo(Document doc, ObjectId pipeId)
        {
            if (doc == null || pipeId.IsNull) return null;
            try
            {
                return QuantityPipeAttributeService.ReadPipe(doc, pipeId);
            }
            catch
            {
                return null;
            }
        }

        private static void ApplyQuantityInfoToResult(PipeLengthAnnotationResult result, QuantityPipeSelectionInfo info, PipeLengthAnnotationOptions options)
        {
            if (result == null || info == null || info.Attributes == null) return;

            QuantityPipeAttributes attrs = info.Attributes;
            result.HasQuantityAttributes = true;
            result.QuantityObjectKind = string.IsNullOrWhiteSpace(attrs.ObjectKind) ? (info.InferredKind ?? string.Empty) : attrs.ObjectKind;
            result.DrawLengthWidthHeightAnnotation = attrs.DrawLengthWidthHeightAnnotation;

            double width = attrs.TrenchWidth;
            double height = attrs.RoadThickness;
            double depth = ResolvePipeDepthForLengthWidthHeight(attrs);

            result.ExcavationWidth = width > 0 ? width : (options == null ? 0.0 : options.ExcavationWidth);
            result.ExcavationHeight = height > 0 ? height : (options == null ? 0.0 : options.ExcavationHeight);
            result.ExcavationDepth = depth > 0 ? depth : (options == null ? 0.0 : options.ExcavationDepth);
        }

        private static double ResolvePipeDepthForLengthWidthHeight(QuantityPipeAttributes attrs)
        {
            if (attrs == null) return 0.0;

            if (QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind))
            {
                if (attrs.BranchDepth > 0) return attrs.BranchDepth;
            }

            if (attrs.AverageDepth > 0) return attrs.AverageDepth;
            if (attrs.StartDepth > 0 && attrs.EndDepth > 0) return (attrs.StartDepth + attrs.EndDepth) / 2.0;
            if (attrs.StartDepth > 0) return attrs.StartDepth;
            if (attrs.EndDepth > 0) return attrs.EndDepth;
            if (attrs.BranchDepth > 0) return attrs.BranchDepth;
            return 0.0;
        }

        private static bool ShouldDrawBottomAnnotation(PipeLengthAnnotationOptions options, PipeLengthAnnotationResult result)
        {
            if (result != null && result.HasQuantityAttributes)
            {
                return result.DrawLengthWidthHeightAnnotation;
            }

            return options != null && options.DrawBottomAnnotation;
        }

        private static PipeLengthAnnotationOptions NormalizeOptions(PipeLengthAnnotationOptions options)
        {
            options = options ?? PipeLengthAnnotationOptions.Default;

            if (options.TextHeight <= 0) options.TextHeight = PipeLengthAnnotationOptions.Default.TextHeight;
            if (options.DecimalPlaces < 0) options.DecimalPlaces = 0;
            if (options.DecimalPlaces > 6) options.DecimalPlaces = 6;
            if (string.IsNullOrWhiteSpace(options.AnnotationTemplate)) options.AnnotationTemplate = PipeLengthAnnotationOptions.Default.AnnotationTemplate;
            if (string.IsNullOrWhiteSpace(options.AnnotationFontName)) options.AnnotationFontName = PipeLengthAnnotationOptions.Default.AnnotationFontName;
            if (string.IsNullOrWhiteSpace(options.SelectedLayerName)) options.SelectedLayerName = "ZJ";
            if (string.IsNullOrWhiteSpace(options.AnnotationLayerName)) options.AnnotationLayerName = "ZJ";
            if (string.IsNullOrWhiteSpace(options.AutoAnnotationLayerSuffix)) options.AutoAnnotationLayerSuffix = PipeLengthAnnotationOptions.Default.AutoAnnotationLayerSuffix;
            if (string.IsNullOrWhiteSpace(options.FallbackAnnotationLayerName)) options.FallbackAnnotationLayerName = PipeLengthAnnotationOptions.Default.FallbackAnnotationLayerName;
            if (options.AnnotationSplitTagText == null) options.AnnotationSplitTagText = string.Empty;
            if (string.IsNullOrWhiteSpace(options.BottomAnnotationTemplate)) options.BottomAnnotationTemplate = PipeLengthAnnotationOptions.Default.BottomAnnotationTemplate;
            if (IsLegacyBottomAnnotationTemplate(options.BottomAnnotationTemplate))
            {
                options.BottomAnnotationTemplate = PipeLengthAnnotationOptions.Default.BottomAnnotationTemplate;
            }
            if (options.ExcavationWidth < 0) options.ExcavationWidth = 0;
            if (options.ExcavationHeight < 0) options.ExcavationHeight = 0;
            if (options.ExcavationDepth < 0) options.ExcavationDepth = 0;
            return options;
        }

        private static bool IsLegacyBottomAnnotationTemplate(string template)
        {
            if (string.IsNullOrWhiteSpace(template)) return false;

            string text = template.Trim();
            return string.Equals(text, "开挖：长{长度}m，宽{宽}m，高{高}m，深{深}m", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "开挖：长{长度}m、宽{宽}m、高{高}m、深{深}m", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "开挖：长{长度}m，宽{宽}m，高{深}m", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveAnnotationLayer(PipeLengthAnnotationOptions options, LayerMetadata sourceMetadata)
        {
            if (options != null && options.EnableSourceMetadataLayerLink)
            {
                string value = ResolveLayerLinkValue(options, sourceMetadata);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    string suffix = string.IsNullOrWhiteSpace(options.AutoAnnotationLayerSuffix)
                        ? "注记"
                        : options.AutoAnnotationLayerSuffix.Trim();

                    string layerName = SanitizeLayerName(value.Trim());
                    if (!string.IsNullOrWhiteSpace(suffix) && !layerName.EndsWith(suffix, StringComparison.CurrentCultureIgnoreCase))
                    {
                        layerName += suffix;
                    }
                    if (!string.IsNullOrWhiteSpace(layerName)) return layerName;
                }

                string fallback = SanitizeLayerName(string.IsNullOrWhiteSpace(options.FallbackAnnotationLayerName)
                    ? "未分类注记"
                    : options.FallbackAnnotationLayerName.Trim());
                return string.IsNullOrWhiteSpace(fallback) ? "未分类注记" : fallback;
            }

            if (options.LayerMode == AnnotationLayerMode.ExistingLayer && !string.IsNullOrWhiteSpace(options.SelectedLayerName))
            {
                return options.SelectedLayerName.Trim();
            }

            if (options.LayerMode == AnnotationLayerMode.CustomLayer && !string.IsNullOrWhiteSpace(options.AnnotationLayerName))
            {
                return options.AnnotationLayerName.Trim();
            }

            return "ZJ";
        }

        private static string ResolveLayerLinkValue(PipeLengthAnnotationOptions options, LayerMetadata sourceMetadata)
        {
            sourceMetadata = sourceMetadata ?? new LayerMetadata();

            if (options.LayerLinkMode == AnnotationLayerLinkMode.ParentClass)
            {
                return sourceMetadata.ParentClass;
            }

            if (options.LayerLinkMode == AnnotationLayerLinkMode.FirstMatchedTag)
            {
                return ResolveMatchedTag(options, sourceMetadata);
            }

            if (options.LayerLinkMode == AnnotationLayerLinkMode.ParentGroupAndTag)
            {
                string parent = sourceMetadata.ParentGroup;
                string tag = ResolveMatchedTag(options, sourceMetadata);
                if (!string.IsNullOrWhiteSpace(parent) && !string.IsNullOrWhiteSpace(tag)) return parent + "-" + tag;
                if (!string.IsNullOrWhiteSpace(parent)) return parent;
                return tag;
            }

            return sourceMetadata.ParentGroup;
        }

        private static string ResolveMatchedTag(PipeLengthAnnotationOptions options, LayerMetadata sourceMetadata)
        {
            if (sourceMetadata == null || sourceMetadata.Tags == null || sourceMetadata.Tags.Count == 0) return string.Empty;

            List<string> candidates = LayerMetadata.ParseTags(options == null ? string.Empty : options.AnnotationSplitTagText);
            if (candidates.Count == 0) return sourceMetadata.Tags[0];

            foreach (string candidate in candidates)
            {
                foreach (string tag in sourceMetadata.Tags)
                {
                    if (string.Equals(candidate, tag, StringComparison.CurrentCultureIgnoreCase)) return tag;
                }
            }
            return string.Empty;
        }

        private static string SanitizeLayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            char[] invalid = new[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', '=', ',' };
            string cleaned = name.Trim();
            foreach (char ch in invalid)
            {
                cleaned = cleaned.Replace(ch, '-');
            }
            cleaned = Regex.Replace(cleaned, @"\s+", "");
            return cleaned;
        }


        private static LayerMetadata GetEffectiveSourceMetadata(Database db, Transaction tr, string layerName)
        {
            LayerMetadata metadata = LayerManagerService.GetLayerMetadata(db, tr, layerName);
            if (metadata == null || metadata.IsEmpty)
            {
                metadata = LayerManagerService.InferLayerMetadataFromName(layerName);
            }
            return metadata ?? new LayerMetadata();
        }

        private static void ApplySourceMetadataToResult(PipeLengthAnnotationResult result, LayerMetadata metadata)
        {
            if (result == null) return;
            metadata = metadata ?? new LayerMetadata();
            result.PipeParentGroup = metadata.ParentGroup ?? string.Empty;
            result.PipeParentClass = metadata.ParentClass ?? string.Empty;
            result.PipeTagText = metadata.TagText ?? string.Empty;
        }

        private static void EnsureAutoAnnotationLayerMetadata(Database db, Transaction tr, string annotationLayerName, PipeLengthAnnotationResult result, PipeLengthAnnotationOptions options)
        {
            if (db == null || tr == null || result == null || options == null) return;
            if (!options.EnableSourceMetadataLayerLink || !options.WriteAutoAnnotationLayerMetadata) return;
            if (string.IsNullOrWhiteSpace(annotationLayerName)) return;

            try
            {
                CadLayerService.EnsureLayer(db, tr, annotationLayerName, 7);

                var tags = new List<string>();
                tags.Add("管线注记");
                tags.Add("长度注记");
                if (!string.IsNullOrWhiteSpace(result.PipeParentGroup)) tags.Add(result.PipeParentGroup);
                if (!string.IsNullOrWhiteSpace(result.PipeParentClass)) tags.Add(result.PipeParentClass);
                foreach (string tag in LayerMetadata.ParseTags(result.PipeTagText)) tags.Add(tag);

                var metadata = new LayerMetadata
                {
                    ParentGroup = "注记",
                    ParentClass = "管线长度注记",
                    Tags = LayerMetadata.ParseTags(string.Join("、", tags.ToArray()))
                };
                LayerManagerService.EnsureLayerMetadata(db, tr, annotationLayerName, metadata, false);
            }
            catch
            {
            }
        }

        private static void WriteAnnotationSourceData(Transaction tr, ObjectId objectId, PipeLengthAnnotationResult result)
        {
            if (tr == null || objectId.IsNull || result == null) return;

            try
            {
                Entity entity = tr.GetObject(objectId, OpenMode.ForWrite, false) as Entity;
                if (entity == null) return;

                if (entity.ExtensionDictionary.IsNull) entity.CreateExtensionDictionary();
                DBDictionary dict = (DBDictionary)tr.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite);

                Xrecord record = null;
                if (dict.Contains(AnnotationSourceXrecordName))
                {
                    record = tr.GetObject(dict.GetAt(AnnotationSourceXrecordName), OpenMode.ForWrite, false) as Xrecord;
                }
                else
                {
                    record = new Xrecord();
                    dict.SetAt(AnnotationSourceXrecordName, record);
                    tr.AddNewlyCreatedDBObject(record, true);
                }

                if (record != null)
                {
                    record.Data = new ResultBuffer(
                        new TypedValue((int)DxfCode.Text, "AnnotationType=管线长度注记"),
                        new TypedValue((int)DxfCode.Text, "SourceLayer=" + (result.PipeLayerName ?? string.Empty)),
                        new TypedValue((int)DxfCode.Text, "SourceParent=" + (result.PipeParentGroup ?? string.Empty)),
                        new TypedValue((int)DxfCode.Text, "SourceClass=" + (result.PipeParentClass ?? string.Empty)),
                        new TypedValue((int)DxfCode.Text, "SourceTags=" + (result.PipeTagText ?? string.Empty)));
                }
            }
            catch
            {
            }
        }

        private static string BuildAnnotationText(PipeLengthAnnotationOptions options, PipeLengthAnnotationResult result)
        {
            string lengthText = FormatNumber(result.Length, options.DecimalPlaces);
            string layerName = string.IsNullOrWhiteSpace(result.PipeLayerName) ? string.Empty : result.PipeLayerName;
            string parentGroup = string.IsNullOrWhiteSpace(result.PipeParentGroup) ? string.Empty : result.PipeParentGroup;
            string parentClass = string.IsNullOrWhiteSpace(result.PipeParentClass) ? string.Empty : result.PipeParentClass;
            string tagText = string.IsNullOrWhiteSpace(result.PipeTagText) ? string.Empty : result.PipeTagText;

            return options.AnnotationTemplate
                .Replace("{父属性}", parentGroup)
                .Replace("{ParentGroup}", parentGroup)
                .Replace("{分类}", parentClass)
                .Replace("{ParentClass}", parentClass)
                .Replace("{标签}", tagText)
                .Replace("{Tags}", tagText)
                .Replace("{图层名}", layerName)
                .Replace("{层名}", layerName)
                .Replace("{LayerName}", layerName)
                .Replace("{长度}", lengthText)
                .Replace("{长}", lengthText)
                .Replace("{Length}", lengthText)
                .Replace("{管线长度}", lengthText);
        }

        private static string BuildBottomAnnotationText(PipeLengthAnnotationOptions options, PipeLengthAnnotationResult result)
        {
            if (options == null || result == null || !ShouldDrawBottomAnnotation(options, result)) return string.Empty;

            string template = string.IsNullOrWhiteSpace(options.BottomAnnotationTemplate)
                ? PipeLengthAnnotationOptions.Default.BottomAnnotationTemplate
                : options.BottomAnnotationTemplate;

            string lengthText = FormatNumber(result.Length, options.DecimalPlaces);
            double width = result.HasQuantityAttributes ? result.ExcavationWidth : options.ExcavationWidth;
            double height = result.HasQuantityAttributes ? result.ExcavationHeight : options.ExcavationHeight;
            double depth = result.HasQuantityAttributes ? result.ExcavationDepth : options.ExcavationDepth;
            string widthText = FormatOptionalNumber(width);
            string heightText = FormatOptionalNumber(height);
            string depthText = FormatOptionalNumber(depth);
            string layerName = string.IsNullOrWhiteSpace(result.PipeLayerName) ? string.Empty : result.PipeLayerName;
            string parentGroup = string.IsNullOrWhiteSpace(result.PipeParentGroup) ? string.Empty : result.PipeParentGroup;
            string parentClass = string.IsNullOrWhiteSpace(result.PipeParentClass) ? string.Empty : result.PipeParentClass;
            string tagText = string.IsNullOrWhiteSpace(result.PipeTagText) ? string.Empty : result.PipeTagText;

            return template
                .Replace("{父属性}", parentGroup)
                .Replace("{ParentGroup}", parentGroup)
                .Replace("{分类}", parentClass)
                .Replace("{ParentClass}", parentClass)
                .Replace("{标签}", tagText)
                .Replace("{Tags}", tagText)
                .Replace("{图层名}", layerName)
                .Replace("{层名}", layerName)
                .Replace("{LayerName}", layerName)
                .Replace("{长度}", lengthText)
                .Replace("{长}", lengthText)
                .Replace("{Length}", lengthText)
                .Replace("{管线长度}", lengthText)
                .Replace("{宽}", widthText)
                .Replace("{Width}", widthText)
                .Replace("{高}", heightText)
                .Replace("{Height}", heightText)
                .Replace("{深}", depthText)
                .Replace("{Depth}", depthText);
        }

        private static string FormatNumber(double value, int decimalPlaces)
        {
            string format = "0";
            if (decimalPlaces > 0) format += "." + new string('0', decimalPlaces);
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string FormatOptionalNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static AttachmentPoint GetTextAttachment(Point3d annotationPoint, Point3d leaderStartPoint)
        {
            return leaderStartPoint.X <= annotationPoint.X ? AttachmentPoint.BottomLeft : AttachmentPoint.BottomRight;
        }

        private static ObjectId DrawAnnotationDbText(Database db, Transaction tr, Point3d position, string text, double height, string layerName, short colorIndex, string textStyleName)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(text)) return ObjectId.Null;

            CadLayerService.EnsureLayer(db, tr, layerName, colorIndex);
            ObjectId textStyleId = GetExistingTextStyleId(db, tr, textStyleName);
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            var dbText = new DBText();
            dbText.Position = position;
            dbText.Height = height <= 0 ? 1.0 : height;
            dbText.TextString = NormalizeDbTextString(text);
            dbText.Layer = layerName;
            if (!textStyleId.IsNull) dbText.TextStyleId = textStyleId;

            dbText.HorizontalMode = TextHorizontalMode.TextLeft;

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
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    ObjectId id = GetExistingTextStyleId(db, tr, textStyleName);
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

            string target = textStyleName.Trim();
            try
            {
                TextStyleTable tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                if (tst.Has(target)) return tst[target];

                foreach (ObjectId id in tst)
                {
                    TextStyleTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as TextStyleTableRecord;
                    if (record == null || record.IsErased) continue;
                    if (string.Equals(record.Name, target, StringComparison.CurrentCultureIgnoreCase)) return id;
                }
            }
            catch { }

            return ObjectId.Null;
        }

        private static string NormalizeDbTextString(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string normalized = text.Replace("\\P", " ").Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
            return Regex.Replace(normalized, @"\s+", " ").Trim();
        }

        private static ObjectId DrawLeaderByUnderline(Database db, Transaction tr, Point3d leaderStartPoint, Point3d underlineStart, Point3d underlineEnd, string layerName, AttachmentPoint attachment)
        {
            Point3d leaderJoin = IsRightAttachment(attachment) ? underlineEnd : underlineStart;
            Point3d farEnd = IsRightAttachment(attachment) ? underlineStart : underlineEnd;

            var points = new System.Collections.Generic.List<Point3d>();
            points.Add(leaderStartPoint);
            if (leaderStartPoint.DistanceTo(leaderJoin) > DuplicateTolerance) points.Add(leaderJoin);
            if (leaderJoin.DistanceTo(farEnd) > DuplicateTolerance) points.Add(farEnd);
            if (points.Count < 2) return ObjectId.Null;

            return CadDrawService.DrawPolyline(db, tr, points, layerName, 7);
        }

        private static bool TryArrangeFinalAnnotation(Transaction tr, ObjectId topTextObjectId, ObjectId bottomTextObjectId, Point3d annotationPoint, double textHeight, AttachmentPoint attachment, out Point3d underlineStart, out Point3d underlineEnd)
        {
            underlineStart = Point3d.Origin;
            underlineEnd = Point3d.Origin;
            if (textHeight <= 0) textHeight = 1.0;

            Extents3d topExtents;
            if (!TryGetEntityExtents(tr, topTextObjectId, out topExtents)) return false;

            bool hasBottom = false;
            Extents3d bottomExtents = new Extents3d();
            if (!bottomTextObjectId.IsNull && TryGetEntityExtents(tr, bottomTextObjectId, out bottomExtents))
            {
                hasBottom = true;
            }

            double topMinX = Math.Min(topExtents.MinPoint.X, topExtents.MaxPoint.X);
            double topMaxX = Math.Max(topExtents.MinPoint.X, topExtents.MaxPoint.X);
            double topMinY = Math.Min(topExtents.MinPoint.Y, topExtents.MaxPoint.Y);
            double topWidth = Math.Max(topMaxX - topMinX, 0.0);

            double bottomMinX = 0.0;
            double bottomMaxX = 0.0;
            double bottomMaxY = 0.0;
            double bottomWidth = 0.0;
            if (hasBottom)
            {
                bottomMinX = Math.Min(bottomExtents.MinPoint.X, bottomExtents.MaxPoint.X);
                bottomMaxX = Math.Max(bottomExtents.MinPoint.X, bottomExtents.MaxPoint.X);
                bottomMaxY = Math.Max(bottomExtents.MinPoint.Y, bottomExtents.MaxPoint.Y);
                bottomWidth = Math.Max(bottomMaxX - bottomMinX, 0.0);
            }

            double lineWidth = Math.Max(topWidth, bottomWidth);
            if (lineWidth < DuplicateTolerance)
            {
                lineWidth = Math.Max(EstimatePreviewTextWidth(string.Empty, textHeight), textHeight * 4.0);
            }

            double lineStartX;
            double lineEndX;
            if (IsRightAttachment(attachment))
            {
                lineStartX = annotationPoint.X - lineWidth;
                lineEndX = annotationPoint.X;
            }
            else
            {
                lineStartX = annotationPoint.X;
                lineEndX = annotationPoint.X + lineWidth;
            }

            double gap = Math.Max(textHeight * 0.22, 0.05);
            double lineY = topMinY - gap;
            double z = annotationPoint.Z;

            double desiredTopMinX = lineStartX + (lineWidth - topWidth) / 2.0;
            MoveEntityByDelta(tr, topTextObjectId, desiredTopMinX - topMinX, 0.0, 0.0);

            if (hasBottom)
            {
                double desiredBottomMinX = lineStartX + (lineWidth - bottomWidth) / 2.0;
                double desiredBottomMaxY = lineY - gap;
                MoveEntityByDelta(tr, bottomTextObjectId, desiredBottomMinX - bottomMinX, desiredBottomMaxY - bottomMaxY, 0.0);
            }

            underlineStart = new Point3d(lineStartX, lineY, z);
            underlineEnd = new Point3d(lineEndX, lineY, z);
            return true;
        }

        private static void MoveEntityByDelta(Transaction tr, ObjectId objectId, double dx, double dy, double dz)
        {
            if (tr == null || objectId.IsNull) return;
            if (Math.Abs(dx) < 0.0000001 && Math.Abs(dy) < 0.0000001 && Math.Abs(dz) < 0.0000001) return;

            Entity entity;
            try { entity = tr.GetObject(objectId, OpenMode.ForWrite, false) as Entity; }
            catch { return; }
            if (entity == null) return;

            try
            {
                entity.TransformBy(Matrix3d.Displacement(new Vector3d(dx, dy, dz)));
            }
            catch { }
        }

        private static bool TryGetAnnotationUnderlinePoints(Transaction tr, ObjectId textObjectId, ObjectId bottomTextObjectId, double textHeight, AttachmentPoint attachment, out Point3d underlineStart, out Point3d underlineEnd)
        {
            underlineStart = Point3d.Origin;
            underlineEnd = Point3d.Origin;
            if (textHeight <= 0) textHeight = 1.0;

            Extents3d topExtents;
            if (!TryGetEntityExtents(tr, textObjectId, out topExtents)) return false;

            double minX = Math.Min(topExtents.MinPoint.X, topExtents.MaxPoint.X);
            double maxX = Math.Max(topExtents.MinPoint.X, topExtents.MaxPoint.X);
            double minY = Math.Min(topExtents.MinPoint.Y, topExtents.MaxPoint.Y);
            double z = topExtents.MinPoint.Z;

            Extents3d bottomExtents;
            if (!bottomTextObjectId.IsNull && TryGetEntityExtents(tr, bottomTextObjectId, out bottomExtents))
            {
                minX = Math.Min(minX, Math.Min(bottomExtents.MinPoint.X, bottomExtents.MaxPoint.X));
                maxX = Math.Max(maxX, Math.Max(bottomExtents.MinPoint.X, bottomExtents.MaxPoint.X));
            }

            double underlineGap = Math.Max(textHeight * 0.22, 0.05);
            double y = minY - underlineGap;

            underlineStart = new Point3d(minX, y, z);
            underlineEnd = new Point3d(maxX, y, z);

            if (underlineStart.DistanceTo(underlineEnd) < DuplicateTolerance)
            {
                double fallbackWidth = Math.Max(textHeight * 6.0, 1.0);
                if (IsRightAttachment(attachment))
                {
                    underlineStart = new Point3d(maxX - fallbackWidth, y, z);
                    underlineEnd = new Point3d(maxX, y, z);
                }
                else
                {
                    underlineStart = new Point3d(minX, y, z);
                    underlineEnd = new Point3d(minX + fallbackWidth, y, z);
                }
            }

            return true;
        }

        private static bool TryGetEntityExtents(Transaction tr, ObjectId objectId, out Extents3d extents)
        {
            extents = new Extents3d();
            if (tr == null || objectId.IsNull) return false;

            Entity entity;
            try { entity = tr.GetObject(objectId, OpenMode.ForRead, false) as Entity; }
            catch { return false; }

            if (entity == null) return false;

            try
            {
                extents = entity.GeometricExtents;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private sealed class PipeLengthAnnotationPreviewJig : DrawJig
        {
            private readonly Point3d _leaderStartPoint;
            private readonly string _text;
            private readonly string _bottomText;
            private readonly double _textHeight;
            private readonly ObjectId _textStyleId;
            private Point3d _annotationPoint;

            public PipeLengthAnnotationPreviewJig(Point3d leaderStartPoint, string text, string bottomText, double textHeight, ObjectId textStyleId)
            {
                _leaderStartPoint = leaderStartPoint;
                _text = string.IsNullOrWhiteSpace(text) ? "长度标注" : text;
                _bottomText = string.IsNullOrWhiteSpace(bottomText) ? string.Empty : bottomText;
                _textHeight = textHeight <= 0 ? 1.0 : textHeight;
                _textStyleId = textStyleId;
                _annotationPoint = leaderStartPoint;
            }

            public Point3d AnnotationPoint
            {
                get { return _annotationPoint; }
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var options = new JigPromptPointOptions("\n指定注记位置，按 ESC 退出");
                options.UseBasePoint = true;
                options.BasePoint = _leaderStartPoint;
                options.UserInputControls = UserInputControls.Accept3dCoordinates
                    | UserInputControls.NoZeroResponseAccepted;

                PromptPointResult result = prompts.AcquirePoint(options);
                if (result.Status != PromptStatus.OK) return SamplerStatus.Cancel;

                if (result.Value.DistanceTo(_annotationPoint) < DuplicateTolerance)
                {
                    return SamplerStatus.NoChange;
                }

                _annotationPoint = result.Value;
                return SamplerStatus.OK;
            }

            protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
            {
                if (draw == null || draw.Geometry == null) return true;

                AttachmentPoint attachment = GetTextAttachment(_annotationPoint, _leaderStartPoint);
                PreviewLayout layout = BuildPreviewLayout(_text, _bottomText, _textHeight, _annotationPoint, attachment);

                DrawPreviewText(draw, layout.TopTextPoint, _text, _textHeight, _textStyleId);
                if (!string.IsNullOrWhiteSpace(_bottomText))
                {
                    DrawPreviewText(draw, layout.BottomTextPoint, _bottomText, _textHeight, _textStyleId);
                }

                using (var polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline())
                {
                    Point3d leaderJoin = IsRightAttachment(attachment) ? layout.UnderlineEnd : layout.UnderlineStart;
                    Point3d farEnd = IsRightAttachment(attachment) ? layout.UnderlineStart : layout.UnderlineEnd;

                    polyline.AddVertexAt(0, new Point2d(_leaderStartPoint.X, _leaderStartPoint.Y), 0, 0, 0);
                    polyline.AddVertexAt(1, new Point2d(leaderJoin.X, leaderJoin.Y), 0, 0, 0);
                    polyline.AddVertexAt(2, new Point2d(farEnd.X, farEnd.Y), 0, 0, 0);
                    polyline.ColorIndex = 7;
                    draw.Geometry.Draw(polyline);
                }

                return true;
            }
        }

        private sealed class PreviewLayout
        {
            public Point3d TopTextPoint { get; set; }
            public Point3d BottomTextPoint { get; set; }
            public Point3d UnderlineStart { get; set; }
            public Point3d UnderlineEnd { get; set; }
        }

        private static PreviewLayout BuildPreviewLayout(string text, string bottomText, double textHeight, Point3d annotationPoint, AttachmentPoint attachment)
        {
            if (textHeight <= 0) textHeight = 1.0;

            double topWidth = EstimatePreviewTextWidth(text, textHeight);
            double bottomWidth = string.IsNullOrWhiteSpace(bottomText) ? 0.0 : EstimatePreviewTextWidth(bottomText, textHeight);
            double lineWidth = Math.Max(topWidth, bottomWidth);
            double lineGap = Math.Max(textHeight * 0.22, 0.05);

            double lineY = annotationPoint.Y - lineGap;
            double z = annotationPoint.Z;
            double lineStartX;
            double lineEndX;

            if (IsRightAttachment(attachment))
            {
                lineStartX = annotationPoint.X - lineWidth;
                lineEndX = annotationPoint.X;
            }
            else
            {
                lineStartX = annotationPoint.X;
                lineEndX = annotationPoint.X + lineWidth;
            }

            var layout = new PreviewLayout();
            layout.UnderlineStart = new Point3d(lineStartX, lineY, z);
            layout.UnderlineEnd = new Point3d(lineEndX, lineY, z);
            layout.TopTextPoint = new Point3d(lineStartX + (lineWidth - topWidth) / 2.0, annotationPoint.Y, z);
            layout.BottomTextPoint = new Point3d(lineStartX + (lineWidth - bottomWidth) / 2.0, lineY - lineGap - textHeight, z);
            return layout;
        }

        private static void DrawPreviewText(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw, Point3d position, string text, double textHeight, ObjectId textStyleId)
        {
            if (draw == null || draw.Geometry == null || string.IsNullOrWhiteSpace(text)) return;
            if (textHeight <= 0) textHeight = 1.0;

            try
            {
                using (var dbText = new DBText())
                {
                    dbText.Position = position;
                    dbText.Height = textHeight;
                    dbText.TextString = NormalizeDbTextString(text);
                    dbText.ColorIndex = 7;
                    dbText.HorizontalMode = TextHorizontalMode.TextLeft;
                    if (!textStyleId.IsNull) dbText.TextStyleId = textStyleId;
                    draw.Geometry.Draw(dbText);
                }
            }
            catch
            {
                try
                {
                    draw.Geometry.Text(position, Vector3d.ZAxis, Vector3d.XAxis, textHeight, 1.0, 0.0, NormalizeDbTextString(text));
                }
                catch { }
            }
        }

        private static double EstimatePreviewTextWidth(string text, double textHeight)
        {
            if (textHeight <= 0) textHeight = 1.0;
            if (string.IsNullOrEmpty(text)) return Math.Max(textHeight * 4.0, 1.0);

            double widthFactor = 0.0;
            foreach (char ch in text)
            {
                widthFactor += ch <= 127 ? 0.62 : 1.0;
            }

            return Math.Max(widthFactor * textHeight, textHeight * 4.0);
        }

        private static bool IsRightAttachment(AttachmentPoint attachment)
        {
            return attachment == AttachmentPoint.BottomRight
                || attachment == AttachmentPoint.MiddleRight
                || attachment == AttachmentPoint.TopRight;
        }
    }
}
