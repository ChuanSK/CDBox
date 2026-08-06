using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace TCPipeAutoDraw.Modules.SectionDrawing
{
    internal sealed class SectionPlacementJig : DrawJig
    {
        private readonly SectionDrawingOptions _options;
        private readonly SectionLayout _layout;
        private readonly ObjectId _textStyleId;
        private Point3d _labelBottomLeft;
        private Point3d _bodyOrigin;

        private double DimensionPreviewTextHeight
        {
            get { return Math.Max(0.01, _options.TextHeight); }
        }

        /// <summary>
        /// ????????????????????????????
        /// </summary>
        public Point3d Position { get { return _bodyOrigin; } }

        public SectionPlacementJig(SectionDrawingOptions options, ObjectId textStyleId)
        {
            if (options == null) throw new ArgumentNullException("options");
            _options = options.Clone();
            _textStyleId = textStyleId;
            SectionLayoutCalculator.Normalize(_options);
            _layout = SectionLayoutCalculator.Calculate(_options);
            _labelBottomLeft = Point3d.Origin;
            _bodyOrigin = new Point3d(_options.LeftLabelWidth, 0.0, 0.0);
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var opts = new JigPromptPointOptions("\n ");
            opts.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NoZeroResponseAccepted;
            PromptPointResult res = prompts.AcquirePoint(opts);
            if (res.Status != PromptStatus.OK) return SamplerStatus.Cancel;
            if (res.Value.DistanceTo(_labelBottomLeft) < 1e-8) return SamplerStatus.NoChange;
            _labelBottomLeft = res.Value;
            _bodyOrigin = new Point3d(res.Value.X + _options.LeftLabelWidth, res.Value.Y, res.Value.Z);
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            if (draw == null || draw.Geometry == null) return true;

            foreach (SectionLayerLayout layerLayout in _layout.Layers)
            {
                DrawRectangle(draw, layerLayout.BodyRect, _bodyOrigin);
                DrawRectangle(draw, layerLayout.LabelRect, _bodyOrigin);
                if (!string.IsNullOrWhiteSpace(layerLayout.Layer.LeftLabel))
                {
                    DrawCenteredText(draw, layerLayout.LabelRect.Center(_bodyOrigin), layerLayout.Layer.LeftLabel, GetLeftLabelTextHeight(_options.TextHeight));
                }
            }

            foreach (SectionPipeLayout pipeLayout in _layout.Pipes)
            {
                Point3d center = new Point3d(_bodyOrigin.X + pipeLayout.Center.X, _bodyOrigin.Y + pipeLayout.Center.Y, _bodyOrigin.Z);
                DrawCircle(draw, center, pipeLayout.Radius);
                string pipeText = pipeLayout.Pipe == null ? string.Empty : pipeLayout.Pipe.PipeText;
                if (!string.IsNullOrWhiteSpace(pipeText)) DrawCenteredText(draw, center, pipeText.Trim(), GetAdaptivePipeTextHeight(pipeText, pipeLayout.Radius));
            }

            if (_options.DrawTopDimension)
            {
                DrawSimpleDimension(draw,
                    new Point3d(_bodyOrigin.X, _bodyOrigin.Y + _layout.TotalHeight, _bodyOrigin.Z),
                    new Point3d(_bodyOrigin.X + _options.Width, _bodyOrigin.Y + _layout.TotalHeight, _bodyOrigin.Z),
                    new Point3d(_bodyOrigin.X + _options.Width / 2.0, _bodyOrigin.Y + _layout.TotalHeight + _options.TopDimensionOffset, _bodyOrigin.Z),
                    _options.Width.ToString("0.##"));
            }

            if (_options.DrawBottomDimension)
            {
                DrawSimpleDimension(draw,
                    new Point3d(_bodyOrigin.X, _bodyOrigin.Y, _bodyOrigin.Z),
                    new Point3d(_bodyOrigin.X + _options.Width, _bodyOrigin.Y, _bodyOrigin.Z),
                    new Point3d(_bodyOrigin.X + _options.Width / 2.0, _bodyOrigin.Y - _options.BottomDimensionOffset, _bodyOrigin.Z),
                    _options.Width.ToString("0.##"));
            }

            if (_options.DrawRightDimensions)
            {
                foreach (SectionLayerLayout layerLayout in _layout.Layers)
                {
                    double x = _bodyOrigin.X + _options.Width + _options.RightDimensionOffset;
                    Point3d a = new Point3d(_bodyOrigin.X + _options.Width, _bodyOrigin.Y + layerLayout.Bottom, _bodyOrigin.Z);
                    Point3d b = new Point3d(_bodyOrigin.X + _options.Width, _bodyOrigin.Y + layerLayout.Top, _bodyOrigin.Z);
                    Point3d c = new Point3d(x, _bodyOrigin.Y + (layerLayout.Bottom + layerLayout.Top) / 2.0, _bodyOrigin.Z);
                    DrawSimpleDimension(draw, a, b, c, layerLayout.Height.ToString("0.##"));
                }
            }

            if (_options.DrawTotalHeightDimension && _layout.TotalHeight > 0)
            {
                double x = _bodyOrigin.X + _options.Width + SectionLayoutCalculator.GetTotalHeightDimensionOffset(_options);
                Point3d a = new Point3d(_bodyOrigin.X + _options.Width, _bodyOrigin.Y, _bodyOrigin.Z);
                Point3d b = new Point3d(_bodyOrigin.X + _options.Width, _bodyOrigin.Y + _layout.TotalHeight, _bodyOrigin.Z);
                Point3d c = new Point3d(x, _bodyOrigin.Y + _layout.TotalHeight / 2.0, _bodyOrigin.Z);
                DrawSimpleDimension(draw, a, b, c, _layout.TotalHeight.ToString("0.##"));
            }

            if (_options.DrawTitle && !string.IsNullOrWhiteSpace(_options.SectionTitle))
            {
                double baseOffset = _options.DrawBottomDimension ? _options.BottomDimensionOffset : 0.0;
                Point3d titlePoint = new Point3d(_bodyOrigin.X + _options.Width / 2.0, _bodyOrigin.Y - baseOffset - _options.TitleOffset, _bodyOrigin.Z);
                DrawTitleLines(draw, titlePoint);
            }

            return true;
        }

        private static void DrawRectangle(WorldDraw draw, Rect2d rect, Point3d origin)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using (var pl = new Autodesk.AutoCAD.DatabaseServices.Polyline())
            {
                pl.AddVertexAt(0, new Point2d(origin.X + rect.Left, origin.Y + rect.Bottom), 0, 0, 0);
                pl.AddVertexAt(1, new Point2d(origin.X + rect.Right, origin.Y + rect.Bottom), 0, 0, 0);
                pl.AddVertexAt(2, new Point2d(origin.X + rect.Right, origin.Y + rect.Top), 0, 0, 0);
                pl.AddVertexAt(3, new Point2d(origin.X + rect.Left, origin.Y + rect.Top), 0, 0, 0);
                pl.Closed = true;
                pl.Elevation = origin.Z;
                draw.Geometry.Draw(pl);
            }
        }

        private static void DrawLine(WorldDraw draw, Point3d a, Point3d b)
        {
            using (var line = new Line(a, b))
            {
                draw.Geometry.Draw(line);
            }
        }

        private static void DrawCircle(WorldDraw draw, Point3d center, double radius)
        {
            if (radius <= 0) return;
            using (var circle = new Circle(center, Vector3d.ZAxis, radius))
            {
                draw.Geometry.Draw(circle);
            }
        }

        private void DrawCenteredText(WorldDraw draw, Point3d position, string text, double height)
        {
            if (draw == null || draw.Geometry == null || string.IsNullOrWhiteSpace(text)) return;

            string cleanText = NormalizeSingleLine(text);
            double safeHeight = height <= 0 ? 0.08 : height;

            // DrawJig ? DBText ???????? AutoCAD/CASS ?????? AdjustAlignment?
            // ?????????????????????????????????????????????
            double width = EstimateTextWidth(cleanText, safeHeight);
            Point3d basePoint = new Point3d(position.X - width / 2.0, position.Y - safeHeight * 0.35, position.Z);

            using (var dbText = new DBText())
            {
                dbText.TextString = cleanText;
                dbText.Height = safeHeight;
                dbText.HorizontalMode = TextHorizontalMode.TextLeft;
                dbText.VerticalMode = TextVerticalMode.TextBase;
                dbText.Position = basePoint;
                if (!_textStyleId.IsNull) dbText.TextStyleId = _textStyleId;
                draw.Geometry.Draw(dbText);
            }
        }

        private static double GetLeftLabelTextHeight(double baseTextHeight)
        {
            return Math.Max(0.01, (baseTextHeight <= 0 ? 0.08 : baseTextHeight) * 1.4375);
        }

        private static string NormalizeSingleLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\\P", " ").Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static double EstimateTextWidth(string text, double height)
        {
            if (height <= 0) height = 0.08;
            return Math.Max(height, GetWeightedTextLength(text) * height * 0.62);
        }

        private static double GetAdaptivePipeTextHeight(string text, double radius)
        {
            if (string.IsNullOrWhiteSpace(text) || radius <= 0.0) return 0.01;

            double diameter = radius * 2.0;
            double maxTextWidth = diameter * 0.78;
            double maxTextHeight = diameter * 0.38;
            double weightedLength = GetWeightedTextLength(text.Trim());
            double heightByWidth = maxTextWidth / Math.Max(0.1, weightedLength * 0.62);
            double height = Math.Min(maxTextHeight, heightByWidth);
            return Math.Max(0.01, height);
        }

        private static double GetWeightedTextLength(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1.0;
            double length = 0.0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) length += 0.35;
                else if (c < 128) length += 1.0;
                else length += 1.8;
            }
            return Math.Max(1.0, length);
        }

        private void DrawTitleLines(WorldDraw draw, Point3d topCenterPoint)
        {
            if (string.IsNullOrWhiteSpace(_options.SectionTitle)) return;
            string[] lines = _options.SectionTitle.Replace("\r\n", "\n").Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.None);
            double height = _options.TextHeight * 1.25;
            double lineGap = Math.Max(height * 1.35, 0.04);
            int visibleIndex = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                Point3d p = new Point3d(topCenterPoint.X, topCenterPoint.Y - visibleIndex * lineGap, topCenterPoint.Z);
                DrawCenteredText(draw, p, lines[i].Trim(), height);
                visibleIndex++;
            }
        }

        private void DrawSimpleDimension(WorldDraw draw, Point3d p1, Point3d p2, Point3d dimLinePoint, string label)
        {
            bool horizontal = Math.Abs(p1.Y - p2.Y) < Math.Abs(p1.X - p2.X);
            if (horizontal)
            {
                DrawLine(draw, new Point3d(p1.X, p1.Y, p1.Z), new Point3d(p1.X, dimLinePoint.Y, p1.Z));
                DrawLine(draw, new Point3d(p2.X, p2.Y, p2.Z), new Point3d(p2.X, dimLinePoint.Y, p2.Z));
                DrawLine(draw, new Point3d(p1.X, dimLinePoint.Y, p1.Z), new Point3d(p2.X, dimLinePoint.Y, p2.Z));
                DrawCenteredText(draw, dimLinePoint, label, DimensionPreviewTextHeight);
            }
            else
            {
                DrawLine(draw, new Point3d(p1.X, p1.Y, p1.Z), new Point3d(dimLinePoint.X, p1.Y, p1.Z));
                DrawLine(draw, new Point3d(p2.X, p2.Y, p2.Z), new Point3d(dimLinePoint.X, p2.Y, p2.Z));
                DrawLine(draw, new Point3d(dimLinePoint.X, p1.Y, p1.Z), new Point3d(dimLinePoint.X, p2.Y, p2.Z));
                DrawCenteredText(draw, dimLinePoint, label, DimensionPreviewTextHeight);
            }
        }
    }
}
