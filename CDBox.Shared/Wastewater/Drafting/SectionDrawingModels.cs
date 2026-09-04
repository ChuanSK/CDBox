using System;
using System.Collections.Generic;

using System.Globalization;
using System.Text.RegularExpressions;

namespace TCPipeAutoDraw.Modules.SectionDrawing
{
    public enum SectionPipeVerticalMode
    {
        /// <summary>管底贴在所选层底线，圆心 = 层底 + 管径/2。</summary>
        LayerBottom = 0,
        /// <summary>管道圆心位于所选层中部。</summary>
        LayerCenter = 1
    }

    public sealed class SectionDrawingOptions
    {
        public string SectionTitle { get; set; }
        public double Width { get; set; }
        public double TotalHeight { get; set; }
        public bool LockTotalHeight { get; set; }
        public double DrawingScale { get; set; }
        public double TextHeight { get; set; }
        public string TextStyleName { get; set; }
        public string BorderLayerName { get; set; }
        public string TextLayerName { get; set; }
        public string HatchLayerName { get; set; }
        public string DimensionLayerName { get; set; }
        public string DimensionStyleName { get; set; }
        public double LeftLabelWidth { get; set; }
        public double TopDimensionOffset { get; set; }
        public double BottomDimensionOffset { get; set; }
        public double RightDimensionOffset { get; set; }
        public double TitleOffset { get; set; }
        public bool DrawTopDimension { get; set; }
        public bool DrawBottomDimension { get; set; }
        public bool DrawRightDimensions { get; set; }
        public bool DrawTotalHeightDimension { get; set; }
        public bool DrawTitle { get; set; }

        // 保留旧字段用于兼容旧设置；新版以各层 Pipes 为准。
        public bool DrawPipeCircle { get; set; }
        public SectionPipeOptions Pipe { get; set; }

        public List<SectionLayerOptions> Layers { get; set; }

        public static SectionDrawingOptions Default
        {
            get
            {
                return new SectionDrawingOptions
                {
                    SectionTitle = "W9至W10",
                    Width = 1.00,
                    TotalHeight = 1.68,
                    LockTotalHeight = false,
                    DrawingScale = 1.0,
                    TextHeight = 0.08,
                    TextStyleName = "宋体",
                    BorderLayerName = "0",
                    TextLayerName = "0",
                    HatchLayerName = "0",
                    DimensionLayerName = "0",
                    DimensionStyleName = string.Empty,
                    LeftLabelWidth = 0.45,
                    TopDimensionOffset = 0.08,
                    BottomDimensionOffset = 0.08,
                    RightDimensionOffset = 0.08,
                    TitleOffset = 0.12,
                    DrawTopDimension = true,
                    DrawBottomDimension = true,
                    DrawRightDimensions = true,
                    DrawTotalHeightDimension = false,
                    DrawTitle = true,
                    DrawPipeCircle = true,
                    Pipe = SectionPipeOptions.Default,
                    Layers = new List<SectionLayerOptions>
                    {
                        new SectionLayerOptions { LeftLabel = "原土回填", Height = 0.70, HatchPatternName = "ANSI37", HatchScale = 0.02, HatchAngle = 0.0 },
                        new SectionLayerOptions { LeftLabel = "中粗砂包管", Height = 0.83, HatchPatternName = "ANSI31", HatchScale = 0.02, HatchAngle = 0.0,
                            Pipes = new List<SectionPipeOptions> { SectionPipeOptions.Default.Clone() } },
                        new SectionLayerOptions { LeftLabel = "中粗砂垫层", Height = 0.15, HatchPatternName = "ANSI31", HatchScale = 0.02, HatchAngle = 0.0 }
                    }
                };
            }
        }

        public SectionDrawingOptions Clone()
        {
            var copy = (SectionDrawingOptions)MemberwiseClone();
            copy.Pipe = Pipe == null ? SectionPipeOptions.Default : Pipe.Clone();
            copy.Layers = new List<SectionLayerOptions>();
            if (Layers != null)
            {
                foreach (SectionLayerOptions layer in Layers)
                {
                    copy.Layers.Add(layer == null ? new SectionLayerOptions() : layer.Clone());
                }
            }
            return copy;
        }
    }

    public sealed class SectionLayerOptions
    {
        public bool DrawLayer { get; set; }
        public string LeftLabel { get; set; }
        public double Height { get; set; }
        public bool HeightLocked { get; set; }
        public string HatchPatternName { get; set; }
        public double HatchScale { get; set; }
        public double HatchAngle { get; set; }
        public List<SectionPipeOptions> Pipes { get; set; }

        public SectionLayerOptions()
        {
            DrawLayer = true;
            LeftLabel = string.Empty;
            Height = 0.10;
            HeightLocked = false;
            HatchPatternName = string.Empty;
            HatchScale = 1.0;
            HatchAngle = 0.0;
            Pipes = new List<SectionPipeOptions>();
        }

        public SectionLayerOptions Clone()
        {
            var copy = new SectionLayerOptions
            {
                DrawLayer = DrawLayer,
                LeftLabel = LeftLabel,
                Height = Height,
                HeightLocked = HeightLocked,
                HatchPatternName = HatchPatternName,
                HatchScale = HatchScale,
                HatchAngle = HatchAngle,
                Pipes = new List<SectionPipeOptions>()
            };
            if (Pipes != null)
            {
                foreach (SectionPipeOptions pipe in Pipes)
                {
                    if (pipe != null) copy.Pipes.Add(pipe.Clone());
                }
            }
            return copy;
        }
    }

    public sealed class SectionPipeOptions
    {
        public double Diameter { get; set; }
        public string PipeText { get; set; }
        public int HostLayerIndex { get; set; }
        public SectionPipeVerticalMode VerticalMode { get; set; }

        public static SectionPipeOptions Default
        {
            get
            {
                return new SectionPipeOptions
                {
                    Diameter = 0.30,
                    PipeText = "DN300",
                    HostLayerIndex = 1,
                    VerticalMode = SectionPipeVerticalMode.LayerCenter
                };
            }
        }

        public SectionPipeOptions Clone()
        {
            return new SectionPipeOptions
            {
                Diameter = Diameter,
                PipeText = PipeText,
                HostLayerIndex = HostLayerIndex,
                VerticalMode = VerticalMode
            };
        }

        public static string BuildPipeText(double diameter)
        {
            if (diameter <= 0) return string.Empty;
            double mm = Math.Round(diameter * 1000.0, 0, MidpointRounding.AwayFromZero);
            return "DN" + mm.ToString("0");
        }

        public static bool TryParsePipeDiameter(string text, out double diameter)
        {
            diameter = 0.0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            Match match = Regex.Match(text, @"DN\s*(?<mm>\d+(?:[\.,]\d+)?)", RegexOptions.IgnoreCase);
            if (!match.Success) return false;

            double millimeters;
            string number = match.Groups["mm"].Value.Replace(',', '.');
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out millimeters)) return false;
            if (millimeters <= 0.0) return false;
            diameter = millimeters / 1000.0;
            return true;
        }
    }

    public sealed class SectionDrawingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int EntityCount { get; set; }
        public int HatchFailureCount { get; set; }

        public SectionDrawingResult()
        {
            Message = string.Empty;
        }

        public string ToEditorMessage()
        {
            if (!Success) return "\n[断面图生成] " + Message;
            string text = "\n[断面图生成] 完成。生成对象：" + EntityCount + " 个。";
            if (HatchFailureCount > 0) text += "有 " + HatchFailureCount + " 个填充图案未能生成，请检查填充名称是否存在。";
            return text;
        }
    }
}
