// Canonical implementation owned by CDBox.Common.
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

using System;
using DbPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    /// <summary>
    /// 直接布框时，以用户指定点作为图框外框左下角的跟随鼠标预览。
    /// </summary>
    internal sealed class FrameDirectPlacementJig : DrawJig
    {
        private readonly FrameTemplateCatalogItem _template;
        private readonly FrameLayoutSettings _settings;
        private readonly ObjectId _templateBlockId;
        private readonly ObjectId _northArrowBlockId;
        private readonly ObjectId _scaleTextStyleId;
        private readonly string _scaleText;
        private Point3d _lowerLeft;
        private bool _hasSample;

        public FrameDirectPlacementJig(FrameTemplateCatalogItem template,
            FrameLayoutSettings settings, ObjectId templateBlockId,
            ObjectId northArrowBlockId, ObjectId scaleTextStyleId,
            string scaleText)
        {
            _template = template ?? throw new ArgumentNullException("template");
            _settings = settings ?? new FrameLayoutSettings();
            _templateBlockId = templateBlockId;
            _northArrowBlockId = northArrowBlockId;
            _scaleTextStyleId = scaleTextStyleId;
            _scaleText = scaleText ?? string.Empty;
            _lowerLeft = Point3d.Origin;
        }

        public Point3d LowerLeft
        {
            get { return _lowerLeft; }
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var options = new JigPromptPointOptions("\n ")
            {
                UserInputControls = UserInputControls.Accept3dCoordinates
                    | UserInputControls.NoZeroResponseAccepted
            };
            PromptPointResult result = prompts.AcquirePoint(options);
            if (result.Status != PromptStatus.OK)
                return SamplerStatus.Cancel;
            if (_hasSample && result.Value.DistanceTo(_lowerLeft) <= 1e-8)
                return SamplerStatus.NoChange;
            _lowerLeft = result.Value;
            _hasSample = true;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            if (draw == null || draw.Geometry == null || !_hasSample)
                return true;

            Point3d insertPoint = GeometryHelper.ComputeInsertPointForLocalPoint(
                new Point2d(_template.FrameMinX, _template.FrameMinY),
                _lowerLeft, 0.0, _template.ScaleX, _template.ScaleY);

            DrawTemplate(draw, insertPoint);
            DrawNorthArrow(draw, insertPoint);
            DrawScaleText(draw, insertPoint);
            return true;
        }

        private void DrawTemplate(WorldDraw draw, Point3d insertPoint)
        {
            if (!_templateBlockId.IsNull)
            {
                try
                {
                    using (var reference = new BlockReference(insertPoint,
                        _templateBlockId))
                    {
                        reference.ScaleFactors = new Scale3d(_template.ScaleX,
                            _template.ScaleY, _template.ScaleZ);
                        draw.Geometry.Draw(reference);
                        return;
                    }
                }
                catch { }
            }

            bool drewSegment = false;
            if (_template.PreviewSegments != null)
            {
                foreach (FrameTemplatePreviewSegment segment in
                    _template.PreviewSegments)
                {
                    if (segment == null) continue;
                    Point3d first = GeometryHelper.LocalToWorld(
                        new Point2d(segment.X1, segment.Y1), insertPoint,
                        0.0, _template.ScaleX, _template.ScaleY);
                    Point3d second = GeometryHelper.LocalToWorld(
                        new Point2d(segment.X2, segment.Y2), insertPoint,
                        0.0, _template.ScaleX, _template.ScaleY);
                    if (first.DistanceTo(second) <= 1e-8) continue;
                    draw.Geometry.WorldLine(first, second);
                    drewSegment = true;
                }
            }
            if (!drewSegment) DrawFallbackBoundary(draw);
        }

        private void DrawNorthArrow(WorldDraw draw, Point3d insertPoint)
        {
            if (!_settings.DrawNorthArrow || _northArrowBlockId.IsNull) return;
            try
            {
                Point3d position = FrameAnnotationService.ResolvePosition(
                    _template, insertPoint, 0.0,
                    _settings.NorthReferencePosition,
                    _settings.NorthOffsetX, _settings.NorthOffsetY);
                using (var reference = new BlockReference(position,
                    _northArrowBlockId))
                {
                    double scale = Math.Max(0.01,
                        _settings.NorthSize / 20.0);
                    reference.ScaleFactors = new Scale3d(scale);
                    draw.Geometry.Draw(reference);
                }
            }
            catch { }
        }

        private void DrawScaleText(WorldDraw draw, Point3d insertPoint)
        {
            if (!_settings.DrawScaleLabel
                || string.IsNullOrWhiteSpace(_scaleText)) return;
            try
            {
                Point3d position = FrameAnnotationService.ResolvePosition(
                    _template, insertPoint, 0.0,
                    _settings.ScaleReferencePosition,
                    _settings.ScaleOffsetX, _settings.ScaleOffsetY);
                using (var text = new DBText())
                {
                    text.TextString = _scaleText;
                    text.Height = _settings.ScaleTextHeight;
                    text.HorizontalMode = TextHorizontalMode.TextCenter;
                    text.VerticalMode = TextVerticalMode.TextVerticalMid;
                    text.Position = position;
                    text.AlignmentPoint = position;
                    text.ColorIndex = _settings.ScaleColorIndex;
                    if (!_scaleTextStyleId.IsNull)
                        text.TextStyleId = _scaleTextStyleId;
                    draw.Geometry.Draw(text);
                }
            }
            catch { }
        }

        private void DrawFallbackBoundary(WorldDraw draw)
        {
            using (var boundary = new DbPolyline())
            {
                boundary.AddVertexAt(0,
                    new Point2d(_lowerLeft.X, _lowerLeft.Y), 0, 0, 0);
                boundary.AddVertexAt(1,
                    new Point2d(_lowerLeft.X + _template.FrameWidth,
                        _lowerLeft.Y), 0, 0, 0);
                boundary.AddVertexAt(2,
                    new Point2d(_lowerLeft.X + _template.FrameWidth,
                        _lowerLeft.Y + _template.FrameHeight), 0, 0, 0);
                boundary.AddVertexAt(3,
                    new Point2d(_lowerLeft.X,
                        _lowerLeft.Y + _template.FrameHeight), 0, 0, 0);
                boundary.Closed = true;
                draw.Geometry.Draw(boundary);
            }
        }
    }
}
