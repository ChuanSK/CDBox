using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;

namespace TCPipeAutoDraw.Modules.SectionDrawing
{
    internal sealed class SectionLayout
    {
        public double TotalHeight { get; set; }
        public List<SectionLayerLayout> Layers { get; private set; }
        public List<SectionPipeLayout> Pipes { get; private set; }
        public double ExtentMinX { get; set; }
        public double ExtentMinY { get; set; }
        public double ExtentMaxX { get; set; }
        public double ExtentMaxY { get; set; }

        public SectionLayout()
        {
            Layers = new List<SectionLayerLayout>();
            Pipes = new List<SectionPipeLayout>();
        }
    }

    internal sealed class SectionLayerLayout
    {
        public int SourceIndex { get; set; }
        public SectionLayerOptions Layer { get; set; }
        public double Bottom { get; set; }
        public double Top { get; set; }
        public double Height { get; set; }
        public Rect2d BodyRect { get; set; }
        public Rect2d LabelRect { get; set; }
    }

    internal sealed class SectionPipeLayout
    {
        public int HostLayerIndex { get; set; }
        public int PipeIndexInLayer { get; set; }
        public SectionPipeOptions Pipe { get; set; }
        public Point3d Center { get; set; }
        public double Radius { get; set; }
    }

    internal struct Rect2d
    {
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }

        public double Left { get { return X; } }
        public double Right { get { return X + Width; } }
        public double Bottom { get { return Y; } }
        public double Top { get { return Y + Height; } }
        public double CenterX { get { return X + Width / 2.0; } }
        public double CenterY { get { return Y + Height / 2.0; } }

        public Rect2d(double x, double y, double width, double height)
            : this()
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Point3d BottomLeft(Point3d origin) { return new Point3d(origin.X + Left, origin.Y + Bottom, origin.Z); }
        public Point3d BottomRight(Point3d origin) { return new Point3d(origin.X + Right, origin.Y + Bottom, origin.Z); }
        public Point3d TopRight(Point3d origin) { return new Point3d(origin.X + Right, origin.Y + Top, origin.Z); }
        public Point3d TopLeft(Point3d origin) { return new Point3d(origin.X + Left, origin.Y + Top, origin.Z); }
        public Point3d CenterPoint(Point3d origin) { return new Point3d(origin.X + CenterX, origin.Y + CenterY, origin.Z); }
        public Point3d Center(Point3d origin) { return CenterPoint(origin); }
    }

    internal static class SectionLayoutCalculator
    {
        public static SectionLayout Calculate(SectionDrawingOptions options)
        {
            if (options == null) throw new ArgumentNullException("options");
            Normalize(options);

            var layout = new SectionLayout();
            double totalHeight = 0.0;
            for (int i = 0; i < options.Layers.Count; i++)
            {
                SectionLayerOptions layer = options.Layers[i];
                if (layer == null || !layer.DrawLayer) continue;
                totalHeight += Math.Max(0.0, layer.Height);
            }
            layout.TotalHeight = totalHeight;

            double bottom = 0.0;
            for (int i = options.Layers.Count - 1; i >= 0; i--)
            {
                SectionLayerOptions layer = options.Layers[i];
                if (layer == null || !layer.DrawLayer) continue;
                double height = Math.Max(0.0, layer.Height);
                var item = new SectionLayerLayout
                {
                    SourceIndex = i,
                    Layer = layer,
                    Bottom = bottom,
                    Top = bottom + height,
                    Height = height,
                    BodyRect = new Rect2d(0.0, bottom, options.Width, height),
                    LabelRect = new Rect2d(-options.LeftLabelWidth, bottom, options.LeftLabelWidth, height)
                };
                layout.Layers.Add(item);
                bottom += height;
            }

            foreach (SectionLayerLayout host in layout.Layers)
            {
                List<SectionPipeOptions> pipes = host.Layer == null ? null : host.Layer.Pipes;
                int count = pipes == null ? 0 : pipes.Count;
                int validCount = 0;
                for (int i = 0; i < count; i++)
                {
                    if (pipes[i] != null && pipes[i].Diameter > 0) validCount++;
                }
                int order = 0;
                for (int i = 0; i < count; i++)
                {
                    SectionPipeOptions pipe = pipes[i];
                    if (pipe == null || pipe.Diameter <= 0) continue;

                    double radius = pipe.Diameter / 2.0;
                    double centerX = validCount <= 1 ? options.Width / 2.0 : options.Width * (order + 1.0) / (validCount + 1.0);
                    double centerY = pipe.VerticalMode == SectionPipeVerticalMode.LayerBottom
                        ? host.Bottom + radius
                        : host.Bottom + host.Height / 2.0;

                    layout.Pipes.Add(new SectionPipeLayout
                    {
                        HostLayerIndex = host.SourceIndex,
                        PipeIndexInLayer = i,
                        Pipe = pipe,
                        Radius = radius,
                        Center = new Point3d(centerX, centerY, 0.0)
                    });
                    order++;
                }
            }

            double minX = -options.LeftLabelWidth;
            double maxX = options.Width;
            if (options.DrawRightDimensions) maxX = Math.Max(maxX, options.Width + options.RightDimensionOffset + Math.Max(options.TextHeight * 3.0, 0.20));
            if (options.DrawTotalHeightDimension) maxX = Math.Max(maxX, options.Width + GetTotalHeightDimensionOffset(options) + Math.Max(options.TextHeight * 3.0, 0.20));
            foreach (SectionPipeLayout pipe in layout.Pipes)
            {
                minX = Math.Min(minX, pipe.Center.X - pipe.Radius);
                maxX = Math.Max(maxX, pipe.Center.X + pipe.Radius);
            }

            double minY = 0.0;
            double maxY = totalHeight;
            if (options.DrawTopDimension) maxY = Math.Max(maxY, totalHeight + options.TopDimensionOffset + Math.Max(options.TextHeight * 2.0, 0.10));
            if (options.DrawBottomDimension) minY = Math.Min(minY, -options.BottomDimensionOffset - Math.Max(options.TextHeight * 2.0, 0.10));
            if (options.DrawTitle && !string.IsNullOrWhiteSpace(options.SectionTitle))
            {
                double baseOffset = options.DrawBottomDimension ? options.BottomDimensionOffset : 0.0;
                int titleLineCount = CountTitleLines(options.SectionTitle);
                double titleBlockHeight = Math.Max(options.TextHeight * 1.35, 0.04) * Math.Max(1, titleLineCount);
                minY = Math.Min(minY, -baseOffset - options.TitleOffset - titleBlockHeight - Math.Max(options.TextHeight, 0.04));
            }

            layout.ExtentMinX = minX;
            layout.ExtentMinY = minY;
            layout.ExtentMaxX = maxX;
            layout.ExtentMaxY = maxY;
            return layout;
        }

        public static double GetTotalHeightDimensionOffset(SectionDrawingOptions options)
        {
            if (options == null) return 0.22;
            double right = options.RightDimensionOffset <= 0 ? 0.08 : options.RightDimensionOffset;
            double textGap = Math.Max(options.TextHeight * 3.0, 0.12);
            return right + textGap;
        }

        private static double EstimateRequiredLeftLabelWidth(SectionDrawingOptions options)
        {
            if (options == null || options.Layers == null) return 0.45;

            double max = 0.45;
            double baseTextHeight = Math.Max(0.01, options.TextHeight <= 0 ? 0.08 : options.TextHeight);

            for (int i = 0; i < options.Layers.Count; i++)
            {
                SectionLayerOptions layer = options.Layers[i];
                if (layer == null || !layer.DrawLayer || string.IsNullOrWhiteSpace(layer.LeftLabel)) continue;

                double layerHeight = layer.Height <= 0 ? 0.10 : layer.Height;
                double desiredHeight = Math.Max(0.01, baseTextHeight * 1.15);
                desiredHeight = Math.Min(desiredHeight, Math.Max(0.01, layerHeight * 0.58));

                // 左侧注记栏需要给单行文字留足边距，尤其是 C25/C30 + 中文材料名。
                double width = desiredHeight * GetWeightedTextLength(layer.LeftLabel.Trim()) * 1.02 + Math.Max(baseTextHeight * 1.50, 0.09) + 0.08;
                if (width > max) max = width;
            }

            return Math.Min(Math.Max(max, 0.45), 2.5);
        }

        private static double GetWeightedTextLength(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1.0;
            double length = 0.0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) length += 0.35;
                else if (c < 128) length += 0.72;
                else length += 1.22;
            }
            return Math.Max(1.0, length);
        }

        private static int CountTitleLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.None);
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i])) count++;
            }
            return count <= 0 ? 0 : count;
        }

        public static void Normalize(SectionDrawingOptions options)
        {
            CDBox.Shared.Wastewater.Drafting
                .WastewaterSectionDrawingRegistry.GetRequired()
                .Normalize(options);
        }

        // 仅保留旧代码用于源级兼容参考；正式运行由 Wastewater 引擎执行。
        private static void NormalizeLegacy(SectionDrawingOptions options)
        {
            if (options == null) return;
            if (options.Width <= 0) options.Width = 1.0;
            if (options.TotalHeight <= 0) options.TotalHeight = 0.0;
            if (options.DrawingScale <= 0) options.DrawingScale = 1.0;
            options.DrawingScale = Math.Max(0.0001, Math.Min(10000.0, options.DrawingScale));
            if (options.TextHeight <= 0) options.TextHeight = 0.08;
            if (options.LeftLabelWidth <= 0) options.LeftLabelWidth = 0.45;
            if (options.TopDimensionOffset < 0) options.TopDimensionOffset = 0.12;
            if (options.BottomDimensionOffset < 0) options.BottomDimensionOffset = 0.12;
            if (options.RightDimensionOffset < 0) options.RightDimensionOffset = 0.08;
            if (options.TitleOffset < 0) options.TitleOffset = 0.12;
            if (string.IsNullOrWhiteSpace(options.BorderLayerName)) options.BorderLayerName = SectionDrawingOptions.Default.BorderLayerName;
            if (string.IsNullOrWhiteSpace(options.TextLayerName)) options.TextLayerName = options.BorderLayerName;
            if (string.IsNullOrWhiteSpace(options.HatchLayerName)) options.HatchLayerName = options.BorderLayerName;
            if (string.IsNullOrWhiteSpace(options.DimensionLayerName)) options.DimensionLayerName = options.BorderLayerName;
            if (options.Pipe == null) options.Pipe = SectionPipeOptions.Default;
            if (options.Layers == null) options.Layers = new List<SectionLayerOptions>();
            if (options.Layers.Count == 0) options.Layers.Add(new SectionLayerOptions { LeftLabel = "自定义层", Height = 0.10 });

            for (int i = 0; i < options.Layers.Count; i++)
            {
                if (options.Layers[i] == null) options.Layers[i] = new SectionLayerOptions();
                if (options.Layers[i].Height <= 0) options.Layers[i].Height = 0.10;
                if (options.Layers[i].HatchScale < 0) options.Layers[i].HatchScale = 1.0;
                if (options.Layers[i].Pipes == null) options.Layers[i].Pipes = new List<SectionPipeOptions>();
                for (int j = options.Layers[i].Pipes.Count - 1; j >= 0; j--)
                {
                    if (options.Layers[i].Pipes[j] == null) options.Layers[i].Pipes.RemoveAt(j);
                }
                for (int j = 0; j < options.Layers[i].Pipes.Count; j++)
                {
                    SectionPipeOptions pipe = options.Layers[i].Pipes[j];
                    if (pipe.Diameter <= 0) pipe.Diameter = 0.30;
                    if (string.IsNullOrWhiteSpace(pipe.PipeText)) pipe.PipeText = SectionPipeOptions.BuildPipeText(pipe.Diameter);
                    pipe.HostLayerIndex = i;
                }
            }


            options.LeftLabelWidth = Math.Max(options.LeftLabelWidth, EstimateRequiredLeftLabelWidth(options));

            if (!options.LockTotalHeight || options.TotalHeight <= 0)
            {
                double total = 0.0;
                for (int i = 0; i < options.Layers.Count; i++)
                {
                    if (options.Layers[i] != null && options.Layers[i].Height > 0) total += options.Layers[i].Height;
                }
                options.TotalHeight = total;
            }

            // 兼容旧版本：旧设置中只有一个全局管圆，若各层还没有管圆，则迁移到对应层。
            bool hasLayerPipe = false;
            for (int i = 0; i < options.Layers.Count; i++)
            {
                if (options.Layers[i].Pipes != null && options.Layers[i].Pipes.Count > 0)
                {
                    hasLayerPipe = true;
                    break;
                }
            }
            if (!hasLayerPipe && options.DrawPipeCircle && options.Pipe != null && options.Pipe.Diameter > 0)
            {
                int index = options.Pipe.HostLayerIndex;
                if (index < 0) index = 0;
                if (index >= options.Layers.Count) index = options.Layers.Count - 1;
                SectionPipeOptions migrated = options.Pipe.Clone();
                migrated.HostLayerIndex = index;
                options.Layers[index].Pipes.Add(migrated);
            }
        }
    }
}
