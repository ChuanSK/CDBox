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
            TextLayoutMetrics previewMetrics = BuildTextLayoutMetrics(doc, previewText, bottomPreviewText, options.TextHeight, previewTextStyleId);
            var jig = new PipeLengthAnnotationPreviewJig(doc.Database, leaderStartPoint, previewText, bottomPreviewText, options.TextHeight, previewTextStyleId, previewMetrics);
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

        internal static bool TryFindPolylineAtPoint(Document doc, Point3d pickedPoint, out ObjectId pipeId, out Point3d leaderStartPoint, out string errorMessage)
        {
            pipeId = ObjectId.Null;
            leaderStartPoint = pickedPoint;
            errorMessage = string.Empty;

            if (doc == null)
            {
                errorMessage = "当前文档无效。";
                return false;
            }

            List<PipeSelectionCandidate> candidates;

            try
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    candidates = FindCandidatesAtPoint(db, tr, doc.Editor, pickedPoint);
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                errorMessage = "查找可计算长度对象失败：" + ex.Message;
                return false;
            }

            if (candidates == null || candidates.Count == 0)
            {
                errorMessage = "点取位置未直接命中具有长度的对象，请重新点取目标对象。";
                return false;
            }

            OverlappingPipeSelectionService.EnrichDisplay(doc, candidates);
            PipeSelectionCandidate selected = OverlappingPipeSelectionService.Select(doc, candidates, null);
            if (selected == null)
            {
                errorMessage = "已取消重叠对象选择。";
                return false;
            }
            pipeId = selected.ObjectId;
            leaderStartPoint = selected.AnchorPoint;
            return true;
        }

        internal static List<PipeSelectionCandidate> FindCandidatesAtPoint(Database db,
            Transaction tr, Editor editor, Point3d pickedPoint)
        {
            var result = new List<PipeSelectionCandidate>();
            if (db == null || tr == null || editor == null) return result;
            double tolerance = GetPointPickTolerance(editor);
            Vector3d viewDirection = GetViewDirection(editor);
            BlockTableRecord space = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead, false) as BlockTableRecord;
            if (space == null) return result;
            foreach (ObjectId id in space)
            {
                Curve curve;
                try { curve = tr.GetObject(id, OpenMode.ForRead, false) as Curve; }
                catch { continue; }
                if (!IsSupportedLengthCurve(curve)) continue;
                string annotationId;
                string annotationPart;
                if (PipeLengthAnnotationObjectService.TryGetAnnotationPart(
                    curve, out annotationId, out annotationPart)) continue;
                Point3d closestPoint;
                double distance;
                if (!TryGetDisplayClosestPoint(curve, pickedPoint, viewDirection,
                    out closestPoint, out distance)) continue;
                if (distance > tolerance) continue;
                result.Add(new PipeSelectionCandidate
                {
                    ObjectId = id,
                    AnchorPoint = closestPoint,
                    Distance = distance,
                    Length = GetCurveLength(curve),
                    LayerName = curve.Layer ?? string.Empty,
                    Title = "长度对象",
                    Detail = (curve.Layer ?? string.Empty) + " · 长度 "
                        + GetCurveLength(curve).ToString("0.##", CultureInfo.InvariantCulture) + "m"
                });
            }
            result.Sort(delegate(PipeSelectionCandidate left, PipeSelectionCandidate right)
            {
                int compare = left.Distance.CompareTo(right.Distance);
                if (compare != 0) return compare;
                return string.Compare(left.LayerName, right.LayerName,
                    StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }

        internal static List<PipeSelectionCandidate> FindOverlappingCandidates(Database db,
            Transaction tr, Editor editor, ObjectId selectedObjectId, Point3d pickedPoint)
        {
            var result = new List<PipeSelectionCandidate>();
            if (db == null || tr == null || editor == null || selectedObjectId.IsNull) return result;

            Curve selectedCurve;
            try { selectedCurve = tr.GetObject(selectedObjectId, OpenMode.ForRead, false) as Curve; }
            catch { return result; }
            if (!IsSupportedLengthCurve(selectedCurve)) return result;

            Vector3d viewDirection = GetViewDirection(editor);
            double tolerance = GetCurveOverlapTolerance(selectedCurve);
            List<DisplayCurveSegment> selectedSegments = BuildDisplaySegments(
                selectedCurve, viewDirection);
            DisplayCurveBounds selectedBounds;
            if (selectedSegments.Count == 0
                || !TryGetDisplayBounds(selectedCurve, viewDirection, out selectedBounds)) return result;
            BlockTableRecord space = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead, false) as BlockTableRecord;
            if (space == null) return result;

            foreach (ObjectId id in space)
            {
                Curve curve;
                try { curve = tr.GetObject(id, OpenMode.ForRead, false) as Curve; }
                catch { continue; }
                if (!IsSupportedLengthCurve(curve)) continue;
                string annotationId;
                string annotationPart;
                if (PipeLengthAnnotationObjectService.TryGetAnnotationPart(
                    curve, out annotationId, out annotationPart)) continue;
                if (id != selectedObjectId)
                {
                    DisplayCurveBounds curveBounds;
                    if (!TryGetDisplayBounds(curve, viewDirection, out curveBounds)
                        || !selectedBounds.Intersects(curveBounds, tolerance)) continue;
                    List<DisplayCurveSegment> curveSegments = BuildDisplaySegments(curve, viewDirection);
                    if (!HaveCoincidentDisplaySegment(selectedSegments, curveSegments,
                        viewDirection, tolerance, Math.Min(GetCurveLength(selectedCurve),
                            GetCurveLength(curve)))) continue;
                }

                Point3d closestPoint;
                double distance;
                if (!TryGetDisplayClosestPoint(curve, pickedPoint, viewDirection,
                    out closestPoint, out distance))
                {
                    try { closestPoint = curve.StartPoint; }
                    catch { closestPoint = pickedPoint; }
                    distance = id == selectedObjectId ? 0.0 : double.MaxValue;
                }
                if (id == selectedObjectId) distance = 0.0;
                double length = GetCurveLength(curve);
                result.Add(new PipeSelectionCandidate
                {
                    ObjectId = id,
                    AnchorPoint = closestPoint,
                    Distance = distance,
                    Length = length,
                    LayerName = curve.Layer ?? string.Empty,
                    Title = "长度对象",
                    Detail = (curve.Layer ?? string.Empty) + " · 长度 "
                        + length.ToString("0.##", CultureInfo.InvariantCulture) + "m"
                });
            }

            result.Sort(delegate(PipeSelectionCandidate left, PipeSelectionCandidate right)
            {
                bool leftSelected = left.ObjectId == selectedObjectId;
                bool rightSelected = right.ObjectId == selectedObjectId;
                if (leftSelected != rightSelected) return leftSelected ? -1 : 1;
                int compare = left.Distance.CompareTo(right.Distance);
                if (compare != 0) return compare;
                return string.Compare(left.LayerName, right.LayerName,
                    StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }

        private static bool HaveCoincidentDisplaySegment(
            IList<DisplayCurveSegment> leftSegments,
            IList<DisplayCurveSegment> rightSegments,
            Vector3d viewDirection, double tolerance, double shorterLength)
        {
            if (leftSegments == null || rightSegments == null
                || leftSegments.Count == 0 || rightSegments.Count == 0) return false;

            double minimumOverlap = Math.Max(tolerance * 4.0, shorterLength * 0.000001);
            for (int i = 0; i < leftSegments.Count; i++)
            {
                for (int j = 0; j < rightSegments.Count; j++)
                {
                    if (HaveCoincidentDisplayPortion(leftSegments[i], rightSegments[j],
                        viewDirection, tolerance, minimumOverlap)) return true;
                }
            }
            return false;
        }

        private static bool HaveCoincidentDisplayPortion(DisplayCurveSegment left,
            DisplayCurveSegment right, Vector3d viewDirection, double tolerance,
            double minimumOverlap)
        {
            if (left.Length <= tolerance || right.Length <= tolerance) return false;
            double parallel = Math.Abs(left.Direction.CrossProduct(right.Direction)
                .DotProduct(viewDirection));
            if (parallel > left.Length * right.Length * 0.01) return false;

            Vector3d offset = ProjectToDisplayPlane(right.Start - left.Start, viewDirection);
            double lineDistance = Math.Abs(offset.CrossProduct(left.Direction)
                .DotProduct(viewDirection)) / left.Length;
            if (lineDistance > tolerance) return false;

            Vector3d unit = left.Direction / left.Length;
            double rightStart = offset.DotProduct(unit);
            double rightEnd = ProjectToDisplayPlane(right.End - left.Start, viewDirection)
                .DotProduct(unit);
            double overlapStart = Math.Max(0.0, Math.Min(rightStart, rightEnd));
            double overlapEnd = Math.Min(left.Length, Math.Max(rightStart, rightEnd));
            return overlapEnd - overlapStart >= minimumOverlap;
        }

        private static List<DisplayCurveSegment> BuildDisplaySegments(Curve curve,
            Vector3d viewDirection)
        {
            var result = new List<DisplayCurveSegment>();
            if (curve == null) return result;
            double start;
            double end;
            try
            {
                start = curve.StartParam;
                end = curve.EndParam;
            }
            catch { return result; }

            int divisions;
            Polyline polyline = curve as Polyline;
            if (curve is Line) divisions = 1;
            else if (polyline != null) divisions = Math.Max(1, Math.Min(256,
                (int)Math.Ceiling(Math.Max(Math.Abs(end - start), 1.0) * 8.0)));
            else divisions = 64;

            Point3d previous;
            try { previous = curve.GetPointAtParameter(start); }
            catch
            {
                try { previous = curve.StartPoint; }
                catch { return result; }
            }
            for (int i = 1; i <= divisions; i++)
            {
                double parameter = start + (end - start) * i / divisions;
                Point3d current;
                try { current = curve.GetPointAtParameter(parameter); }
                catch
                {
                    if (i != divisions) continue;
                    try { current = curve.EndPoint; }
                    catch { continue; }
                }
                Vector3d direction = ProjectToDisplayPlane(current - previous, viewDirection);
                double length = direction.Length;
                if (length > 0.0000001)
                {
                    result.Add(new DisplayCurveSegment
                    {
                        Start = previous,
                        End = current,
                        Direction = direction,
                        Length = length
                    });
                }
                previous = current;
            }
            return result;
        }

        private static bool TryGetDisplayBounds(Curve curve, Vector3d viewDirection,
            out DisplayCurveBounds bounds)
        {
            bounds = new DisplayCurveBounds();
            if (curve == null) return false;
            Extents3d extents;
            try { extents = curve.GeometricExtents; }
            catch { return false; }

            Vector3d normal = viewDirection.Length > 0.0000001
                ? viewDirection.GetNormal()
                : Vector3d.ZAxis;
            Vector3d xAxis;
            try { xAxis = normal.GetPerpendicularVector().GetNormal(); }
            catch { xAxis = Vector3d.XAxis; }
            Vector3d yAxis = normal.CrossProduct(xAxis);
            if (yAxis.Length <= 0.0000001) yAxis = Vector3d.YAxis;
            else yAxis = yAxis.GetNormal();

            Point3d min = extents.MinPoint;
            Point3d max = extents.MaxPoint;
            bool initialized = false;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Point3d point = new Point3d(x == 0 ? min.X : max.X,
                            y == 0 ? min.Y : max.Y, z == 0 ? min.Z : max.Z);
                        Vector3d offset = point - Point3d.Origin;
                        double displayX = offset.DotProduct(xAxis);
                        double displayY = offset.DotProduct(yAxis);
                        if (!initialized)
                        {
                            bounds.MinX = bounds.MaxX = displayX;
                            bounds.MinY = bounds.MaxY = displayY;
                            initialized = true;
                        }
                        else
                        {
                            bounds.MinX = Math.Min(bounds.MinX, displayX);
                            bounds.MaxX = Math.Max(bounds.MaxX, displayX);
                            bounds.MinY = Math.Min(bounds.MinY, displayY);
                            bounds.MaxY = Math.Max(bounds.MaxY, displayY);
                        }
                    }
                }
            }
            return initialized;
        }

        private static Vector3d ProjectToDisplayPlane(Vector3d vector, Vector3d viewDirection)
        {
            Vector3d direction = viewDirection.Length > 0.0000001
                ? viewDirection.GetNormal()
                : Vector3d.ZAxis;
            return vector - direction.MultiplyBy(vector.DotProduct(direction));
        }

        private static double GetCurveOverlapTolerance(Curve curve)
        {
            double length = Math.Max(GetCurveLength(curve), 1.0);
            return Math.Max(0.0000001, length * 0.000001);
        }

        private struct DisplayCurveSegment
        {
            public Point3d Start;
            public Point3d End;
            public Vector3d Direction;
            public double Length;
        }

        private struct DisplayCurveBounds
        {
            public double MinX;
            public double MaxX;
            public double MinY;
            public double MaxY;

            public bool Intersects(DisplayCurveBounds other, double tolerance)
            {
                return MaxX + tolerance >= other.MinX
                    && other.MaxX + tolerance >= MinX
                    && MaxY + tolerance >= other.MinY
                    && other.MaxY + tolerance >= MinY;
            }
        }

        private static bool IsSupportedLengthCurve(Curve curve)
        {
            if (curve == null) return false;
            double length = GetCurveLength(curve);
            return !double.IsNaN(length) && !double.IsInfinity(length) && length > 0.0000001;
        }

        private static double GetPointPickTolerance(Editor ed)
        {
            const double fallbackTolerance = 0.05;

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
                            int pickBoxPixels = 3;
                            try
                            {
                                object pickBox = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("PICKBOX");
                                if (pickBox != null) pickBoxPixels = Convert.ToInt32(pickBox, CultureInfo.InvariantCulture);
                            }
                            catch { }
                            // PICKBOX is measured in screen pixels. Do not clamp the converted
                            // value to a fixed drawing-unit range: that made overlap detection
                            // fail in millimetre drawings and at wide zoom levels.
                            double pickRadiusPixels = Math.Max(4, Math.Min(30, pickBoxPixels + 2));
                            double tolerance = pixelSize * pickRadiusPixels;
                            double minimum = Math.Max(view.Height * 0.000000001, 0.0000001);
                            double maximum = Math.Max(view.Height * 0.05, minimum);
                            return Math.Max(minimum, Math.Min(maximum, tolerance));
                        }
                    }
                }
            }
            catch { }

            return fallbackTolerance;
        }

        private static Vector3d GetViewDirection(Editor editor)
        {
            if (editor == null) return Vector3d.ZAxis;
            try
            {
                using (ViewTableRecord view = editor.GetCurrentView())
                {
                    Vector3d direction = view.ViewDirection;
                    return direction.Length > 0.0000001 ? direction.GetNormal() : Vector3d.ZAxis;
                }
            }
            catch
            {
                return Vector3d.ZAxis;
            }
        }

        private static bool TryGetDisplayClosestPoint(Curve curve, Point3d pickedPoint,
            Vector3d viewDirection, out Point3d closestPoint, out double displayDistance)
        {
            closestPoint = Point3d.Origin;
            displayDistance = double.MaxValue;
            if (curve == null) return false;

            Vector3d direction = viewDirection.Length > 0.0000001
                ? viewDirection.GetNormal()
                : Vector3d.ZAxis;
            try
            {
                closestPoint = curve.GetClosestPointTo(pickedPoint, direction, false);
            }
            catch
            {
                try { closestPoint = curve.GetClosestPointTo(pickedPoint, false); }
                catch { return false; }
            }

            Vector3d offset = closestPoint - pickedPoint;
            displayDistance = offset.CrossProduct(direction).Length;
            return !double.IsNaN(displayDistance) && !double.IsInfinity(displayDistance);
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

                    if (!IsSupportedLengthCurve(curve))
                    {
                        errorMessage = "所选对象没有可用的长度属性。";
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

        internal static bool TryBuildBindingContent(Document doc, ObjectId pipeId,
            out PipeLengthAnnotationBindingContent content, out string errorMessage)
        {
            content = null;
            errorMessage = string.Empty;
            PipeLengthAnnotationOptions options = NormalizeOptions(PipeLengthAnnotationSettingsStore.Load());
            PipeLengthAnnotationResult result;
            if (!TryBuildPreviewSource(doc, pipeId, options, out result, out errorMessage)) return false;

            string topText = BuildAnnotationText(options, result);
            string rawLengthText = FormatNumber(result.Length, options.DecimalPlaces);
            string systemLengthText = topText.IndexOf(rawLengthText + "m", StringComparison.OrdinalIgnoreCase) >= 0
                ? rawLengthText + "m"
                : rawLengthText;
            int systemIndex = topText.LastIndexOf(systemLengthText, StringComparison.OrdinalIgnoreCase);
            content = new PipeLengthAnnotationBindingContent
            {
                TopText = topText,
                UserText = systemIndex < 0
                    ? topText
                    : topText.Remove(systemIndex, systemLengthText.Length).TrimEnd(),
                SystemLengthText = systemLengthText,
                BottomText = BuildBottomAnnotationText(options, result),
                SourceLayer = result.PipeLayerName ?? string.Empty,
                SourceParent = result.PipeParentGroup ?? string.Empty,
                SourceClass = result.PipeParentClass ?? string.Empty,
                SourceTags = result.PipeTagText ?? string.Empty,
                IsQuantityPipe = IsQuantityPipeResult(result)
            };
            return true;
        }

        public static PipeLengthAnnotationResult CalculateAndAnnotate(Document doc, ObjectId pipeId, Point3d leaderStartPoint, Point3d annotationPoint, PipeLengthAnnotationOptions options)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            options = NormalizeOptions(options);

            var result = new PipeLengthAnnotationResult();
            result.PipeObjectId = pipeId;
            result.AnnotationPoint = annotationPoint;
            result.BindingPoint = leaderStartPoint;

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

                if (!IsSupportedLengthCurve(curve))
                {
                    result.Success = false;
                    result.Message = "所选对象没有可用的长度属性。";
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
                string rawLengthText = FormatNumber(result.Length, options.DecimalPlaces);
                string systemLengthText = text.IndexOf(rawLengthText + "m", StringComparison.OrdinalIgnoreCase) >= 0
                    ? rawLengthText + "m"
                    : rawLengthText;
                int systemIndex = text.LastIndexOf(systemLengthText, StringComparison.OrdinalIgnoreCase);
                result.SystemLengthText = systemLengthText;
                result.UserText = systemIndex < 0
                    ? text
                    : text.Remove(systemIndex, systemLengthText.Length).TrimEnd();
                string bottomText = BuildBottomAnnotationText(options, result);
                AttachmentPoint attachment = GetTextAttachment(annotationPoint, leaderStartPoint);
                ObjectId finalTextStyleId = GetExistingTextStyleId(db, tr, options.AnnotationFontName);
                TextLayoutMetrics finalMetrics = BuildTextLayoutMetrics(db, tr, text, bottomText, options.TextHeight, finalTextStyleId);
                PreviewLayout initialLayout = BuildPreviewLayout(text, bottomText, options.TextHeight, annotationPoint, attachment, finalMetrics);

                ObjectId textId = DrawAnnotationDbText(db, tr, initialLayout.TopTextPoint, text, options.TextHeight, result.AnnotationLayerName, 7, finalTextStyleId);
                result.AnnotationObjectId = textId;

                ObjectId bottomTextId = ObjectId.Null;
                if (!string.IsNullOrWhiteSpace(bottomText) && !textId.IsNull)
                {
                    bottomTextId = DrawAnnotationDbText(db, tr, initialLayout.BottomTextPoint, bottomText, options.TextHeight, result.AnnotationLayerName, 7, finalTextStyleId);
                    result.BottomAnnotationObjectId = bottomTextId;
                }

                // 为保证“预览即实际”，正式落图使用与 Jig 预览完全相同的布局计算结果。
                // 之前落图后再次按 GeometricExtents 重排，会导致部分文字样式下预览和实际成图位置明显不一致。
                if (options.DrawLeader && !textId.IsNull)
                {
                    result.LeaderObjectId = DrawLeaderByUnderline(db, tr, leaderStartPoint, initialLayout.UnderlineStart,
                        initialLayout.UnderlineEnd, result.AnnotationLayerName, attachment);
                }

                PipeLengthAnnotationObjectService.BindNewAnnotation(db, tr, pipeId, result);

                tr.Commit();
            }

            result.Success = true;
            result.Message = "标注已生成。";
            return result;
        }

        public static bool RepositionExistingAnnotation(Document doc, ObjectId annotationObjectId)
        {
            if (doc == null || annotationObjectId.IsNull) return false;
            PipeLengthAnnotationEditModel model = PipeLengthAnnotationObjectService.LoadEditModel(doc, annotationObjectId);
            if (model == null || !model.HasBindingPoint)
            {
                doc.Editor.WriteMessage("\n[CDBox 标注调整] 标注缺少有效绑定点。 ");
                return false;
            }

            ObjectId textStyleId = ResolveTextStyleId(doc, model.TextStyleName);
            TextLayoutMetrics metrics = BuildTextLayoutMetrics(doc, model.TopText, model.BottomText,
                model.TextHeight, textStyleId);
            var jig = new PipeLengthAnnotationPreviewJig(doc.Database, model.BindingPoint, model.TopText,
                model.BottomText, model.TextHeight, textStyleId, metrics);
            PromptResult drag;
            bool originalsHidden = PipeLengthAnnotationObjectService.SetAnnotationVisibility(
                doc, model.AnnotationId, false);
            try
            {
                drag = doc.Editor.Drag(jig);
            }
            finally
            {
                if (originalsHidden)
                {
                    PipeLengthAnnotationObjectService.SetAnnotationVisibility(doc, model.AnnotationId, true);
                }
            }
            if (drag.Status != PromptStatus.OK) return false;

            AttachmentPoint attachment = GetTextAttachment(jig.AnnotationPoint, model.BindingPoint);
            PreviewLayout layout = BuildPreviewLayout(model.TopText, model.BottomText, model.TextHeight,
                jig.AnnotationPoint, attachment, metrics);
            Point3d leaderJoin = IsRightAttachment(attachment) ? layout.UnderlineEnd : layout.UnderlineStart;
            Point3d farEnd = IsRightAttachment(attachment) ? layout.UnderlineStart : layout.UnderlineEnd;
            PipeLengthAnnotationObjectService.ApplyExistingPlacement(doc, model.AnnotationId,
                layout.TopTextPoint, layout.BottomTextPoint, leaderJoin, farEnd, annotationObjectId);
            return true;
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

        private static string BuildAnnotationText(PipeLengthAnnotationOptions options, PipeLengthAnnotationResult result)
        {
            string lengthText = FormatNumber(result.Length, options.DecimalPlaces);
            string layerName = string.IsNullOrWhiteSpace(result.PipeLayerName) ? string.Empty : result.PipeLayerName;
            string parentGroup = string.IsNullOrWhiteSpace(result.PipeParentGroup) ? string.Empty : result.PipeParentGroup;
            string parentClass = string.IsNullOrWhiteSpace(result.PipeParentClass) ? string.Empty : result.PipeParentClass;
            string tagText = string.IsNullOrWhiteSpace(result.PipeTagText) ? string.Empty : result.PipeTagText;

            if (!IsQuantityPipeResult(result))
            {
                return (string.IsNullOrWhiteSpace(layerName) ? "未指定图层" : layerName)
                    + "：" + lengthText + "m";
            }

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
            if (options == null || result == null || !IsQuantityPipeResult(result)
                || !ShouldDrawBottomAnnotation(options, result)) return string.Empty;

            string template = string.IsNullOrWhiteSpace(options.BottomAnnotationTemplate)
                ? PipeLengthAnnotationOptions.Default.BottomAnnotationTemplate
                : options.BottomAnnotationTemplate;

            string lengthText = FormatNumber(result.Length, options.DecimalPlaces);
            double width = result.HasQuantityAttributes ? result.ExcavationWidth : options.ExcavationWidth;
            double height = result.HasQuantityAttributes ? result.ExcavationHeight : options.ExcavationHeight;
            double depth = result.HasQuantityAttributes ? result.ExcavationDepth : options.ExcavationDepth;
            string widthText = FormatNumber(width, options.DecimalPlaces);
            string heightText = FormatNumber(height, options.DecimalPlaces);
            string depthText = FormatNumber(depth, options.DecimalPlaces);
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

        private static bool IsQuantityPipeResult(PipeLengthAnnotationResult result)
        {
            if (result == null || !result.HasQuantityAttributes) return false;
            return QuantityPipeAttributes.IsMainPipeKind(result.QuantityObjectKind)
                || QuantityPipeAttributes.IsBranchKind(result.QuantityObjectKind);
        }

        private static string FormatNumber(double value, int decimalPlaces)
        {
            string format = "0";
            if (decimalPlaces > 0) format += "." + new string('0', decimalPlaces);
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static AttachmentPoint GetTextAttachment(Point3d annotationPoint, Point3d leaderStartPoint)
        {
            return leaderStartPoint.X <= annotationPoint.X ? AttachmentPoint.BottomLeft : AttachmentPoint.BottomRight;
        }

        private static ObjectId DrawAnnotationDbText(Database db, Transaction tr, Point3d centerBaselinePoint, string text, double height, string layerName, short colorIndex, ObjectId textStyleId)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(text)) return ObjectId.Null;

            CadLayerService.EnsureLayer(db, tr, layerName, colorIndex);
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            var dbText = new DBText();
            try { dbText.SetDatabaseDefaults(db); } catch { }
            // 使用中心对齐：上下文字与横线中心一致，左侧/右侧标注时不会再因左插入点产生偏移。
            dbText.HorizontalMode = TextHorizontalMode.TextCenter;
            dbText.Position = centerBaselinePoint;
            dbText.AlignmentPoint = centerBaselinePoint;
            dbText.Height = height <= 0 ? 1.0 : height;
            dbText.TextString = NormalizeDbTextString(text);
            dbText.Layer = layerName;
            dbText.ColorIndex = colorIndex;
            if (!textStyleId.IsNull) dbText.TextStyleId = textStyleId;

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

        private static ObjectId DrawLeaderByUnderline(Database db, Transaction tr, Point3d leaderStartPoint,
            Point3d underlineStart, Point3d underlineEnd, string layerName, AttachmentPoint attachment)
        {
            Point3d leaderJoin = IsRightAttachment(attachment) ? underlineEnd : underlineStart;
            Point3d farEnd = IsRightAttachment(attachment) ? underlineStart : underlineEnd;
            var points = new List<Point3d> { leaderStartPoint };
            if (leaderStartPoint.DistanceTo(leaderJoin) > DuplicateTolerance) points.Add(leaderJoin);
            if (leaderJoin.DistanceTo(farEnd) > DuplicateTolerance) points.Add(farEnd);
            return points.Count < 2 ? ObjectId.Null : CadDrawService.DrawPolyline(db, tr, points, layerName, 7);
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
            private readonly Database _database;
            private readonly Point3d _leaderStartPoint;
            private readonly string _text;
            private readonly string _bottomText;
            private readonly double _textHeight;
            private readonly ObjectId _textStyleId;
            private readonly TextLayoutMetrics _metrics;
            private Point3d _annotationPoint;

            public PipeLengthAnnotationPreviewJig(Database database, Point3d leaderStartPoint, string text, string bottomText, double textHeight, ObjectId textStyleId, TextLayoutMetrics metrics)
            {
                _database = database;
                _leaderStartPoint = leaderStartPoint;
                _text = string.IsNullOrWhiteSpace(text) ? "长度标注" : text;
                _bottomText = string.IsNullOrWhiteSpace(bottomText) ? string.Empty : bottomText;
                _textHeight = textHeight <= 0 ? 1.0 : textHeight;
                _textStyleId = textStyleId;
                _metrics = metrics ?? CreateEstimatedTextLayoutMetrics(_text, _bottomText, _textHeight);
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
                PreviewLayout layout = BuildPreviewLayout(_text, _bottomText, _textHeight, _annotationPoint, attachment, _metrics);

                DrawPreviewText(draw, _database, layout.TopTextPoint, _text, _textHeight, _textStyleId);
                if (!string.IsNullOrWhiteSpace(_bottomText))
                {
                    DrawPreviewText(draw, _database, layout.BottomTextPoint, _bottomText, _textHeight, _textStyleId);
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
            /// <summary>
            /// DBText 的中心基线点。预览和正式落图都使用同一对齐方式，避免“预览一套、成图一套”。
            /// </summary>
            public Point3d TopTextPoint { get; set; }
            public Point3d BottomTextPoint { get; set; }

            public double TopTextWidth { get; set; }
            public double BottomTextWidth { get; set; }
            public double UnderlineWidth { get; set; }
            public Point3d UnderlineStart { get; set; }
            public Point3d UnderlineEnd { get; set; }
        }

        private sealed class TextLayoutMetrics
        {
            public double TopTextWidth { get; set; }
            public double BottomTextWidth { get; set; }
        }

        private static PreviewLayout BuildPreviewLayout(string text, string bottomText, double textHeight, Point3d annotationPoint, AttachmentPoint attachment, TextLayoutMetrics metrics)
        {
            if (textHeight <= 0) textHeight = 1.0;
            metrics = metrics ?? CreateEstimatedTextLayoutMetrics(text, bottomText, textHeight);

            double topWidth = Math.Max(metrics.TopTextWidth, EstimatePreviewTextWidth(text, textHeight));
            double bottomWidth = string.IsNullOrWhiteSpace(bottomText) ? 0.0 : Math.Max(metrics.BottomTextWidth, EstimatePreviewTextWidth(bottomText, textHeight));
            double sideMargin = Math.Max(textHeight * 0.12, 0.03);
            double lineWidth = Math.Max(topWidth, bottomWidth) + sideMargin * 2.0;
            if (lineWidth < DuplicateTolerance) lineWidth = Math.Max(textHeight * 4.0, 1.0);

            double lineGap = Math.Max(textHeight * 0.22, 0.05);
            double lineY = annotationPoint.Y - lineGap;
            double z = annotationPoint.Z;
            double lineStartX;
            double lineEndX;

            // annotationPoint 始终作为横线靠近引线一侧的端点：
            // 右侧标注时为横线左端点，左侧标注时为横线右端点。
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

            double centerX = (lineStartX + lineEndX) / 2.0;
            var layout = new PreviewLayout();
            layout.TopTextWidth = topWidth;
            layout.BottomTextWidth = bottomWidth;
            layout.UnderlineWidth = lineWidth;
            layout.UnderlineStart = new Point3d(lineStartX, lineY, z);
            layout.UnderlineEnd = new Point3d(lineEndX, lineY, z);
            layout.TopTextPoint = new Point3d(centerX, annotationPoint.Y, z);
            layout.BottomTextPoint = new Point3d(centerX, lineY - lineGap - textHeight, z);
            return layout;
        }

        private static void DrawPreviewText(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw, Database db, Point3d centerBaselinePoint, string text, double textHeight, ObjectId textStyleId)
        {
            if (draw == null || draw.Geometry == null || string.IsNullOrWhiteSpace(text)) return;
            if (textHeight <= 0) textHeight = 1.0;

            string normalized = NormalizeDbTextString(text);
            try
            {
                using (var dbText = new DBText())
                {
                    if (db != null)
                    {
                        try { dbText.SetDatabaseDefaults(db); } catch { }
                    }

                    dbText.HorizontalMode = TextHorizontalMode.TextCenter;
                    dbText.Position = centerBaselinePoint;
                    dbText.AlignmentPoint = centerBaselinePoint;
                    dbText.Height = textHeight;
                    dbText.TextString = normalized;
                    dbText.ColorIndex = 7;
                    if (!textStyleId.IsNull) dbText.TextStyleId = textStyleId;
                    try { if (db != null) dbText.AdjustAlignment(db); } catch { }
                    draw.Geometry.Draw(dbText);
                }
            }
            catch
            {
                try
                {
                    // 兜底预览只在 DBText 预览失败时使用。正常情况下会走上方 DBText 分支，从而继承用户选择的文字样式和高度。
                    draw.Geometry.Text(centerBaselinePoint, Vector3d.ZAxis, Vector3d.XAxis, textHeight, 1.0, 0.0, normalized);
                }
                catch { }
            }
        }

        private static TextLayoutMetrics BuildTextLayoutMetrics(Document doc, string topText, string bottomText, double textHeight, ObjectId textStyleId)
        {
            if (doc == null || doc.Database == null) return CreateEstimatedTextLayoutMetrics(topText, bottomText, textHeight);

            try
            {
                using (DocumentLock docLock = doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    TextLayoutMetrics metrics = BuildTextLayoutMetrics(doc.Database, tr, topText, bottomText, textHeight, textStyleId);
                    tr.Commit();
                    return metrics;
                }
            }
            catch
            {
                return CreateEstimatedTextLayoutMetrics(topText, bottomText, textHeight);
            }
        }

        private static TextLayoutMetrics BuildTextLayoutMetrics(Database db, Transaction tr, string topText, string bottomText, double textHeight, ObjectId textStyleId)
        {
            if (textHeight <= 0) textHeight = 1.0;

            TextLayoutMetrics estimated = CreateEstimatedTextLayoutMetrics(topText, bottomText, textHeight);
            var metrics = new TextLayoutMetrics();
            metrics.TopTextWidth = Math.Max(estimated.TopTextWidth, MeasureDbTextWidth(db, tr, topText, textHeight, textStyleId, estimated.TopTextWidth));
            metrics.BottomTextWidth = string.IsNullOrWhiteSpace(bottomText)
                ? 0.0
                : Math.Max(estimated.BottomTextWidth, MeasureDbTextWidth(db, tr, bottomText, textHeight, textStyleId, estimated.BottomTextWidth));
            return metrics;
        }

        private static TextLayoutMetrics CreateEstimatedTextLayoutMetrics(string topText, string bottomText, double textHeight)
        {
            var metrics = new TextLayoutMetrics();
            metrics.TopTextWidth = EstimatePreviewTextWidth(topText, textHeight);
            metrics.BottomTextWidth = string.IsNullOrWhiteSpace(bottomText) ? 0.0 : EstimatePreviewTextWidth(bottomText, textHeight);
            return metrics;
        }

        private static double MeasureDbTextWidth(Database db, Transaction tr, string text, double textHeight, ObjectId textStyleId, double fallbackWidth)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(text)) return fallbackWidth;
            if (textHeight <= 0) textHeight = 1.0;

            DBText tempText = null;
            try
            {
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                tempText = new DBText();
                try { tempText.SetDatabaseDefaults(db); } catch { }
                tempText.Position = Point3d.Origin;
                tempText.Height = textHeight;
                tempText.TextString = NormalizeDbTextString(text);
                if (!textStyleId.IsNull) tempText.TextStyleId = textStyleId;
                tempText.HorizontalMode = TextHorizontalMode.TextLeft;

                btr.AppendEntity(tempText);
                tr.AddNewlyCreatedDBObject(tempText, true);
                try { tempText.AdjustAlignment(db); } catch { }

                Extents3d extents = tempText.GeometricExtents;
                double width = Math.Abs(extents.MaxPoint.X - extents.MinPoint.X);

                try { tempText.Erase(); } catch { }
                return width > DuplicateTolerance ? Math.Max(width, fallbackWidth) : fallbackWidth;
            }
            catch
            {
                try
                {
                    if (tempText != null && !tempText.IsErased) tempText.Erase();
                }
                catch { }
                return fallbackWidth;
            }
        }

        private static double EstimatePreviewTextWidth(string text, double textHeight)
        {
            if (textHeight <= 0) textHeight = 1.0;
            text = NormalizeDbTextString(text);
            if (string.IsNullOrEmpty(text)) return Math.Max(textHeight * 4.0, 1.0);

            double widthFactor = 0.0;
            foreach (char ch in text)
            {
                if (ch <= 127)
                {
                    if (char.IsWhiteSpace(ch)) widthFactor += 0.35;
                    else if (char.IsDigit(ch)) widthFactor += 0.68;
                    else if (char.IsLetter(ch)) widthFactor += 0.72;
                    else if (ch == '.' || ch == ',' || ch == ':' || ch == ';') widthFactor += 0.42;
                    else widthFactor += 0.58;
                }
                else
                {
                    // 中文及中文标点按略大于一个字高估算；测量失败时宁可横线略长，也不能短于下方长宽高注记。
                    widthFactor += 1.08;
                }
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
