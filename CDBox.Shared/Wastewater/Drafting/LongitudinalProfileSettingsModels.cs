using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.Modules.LongitudinalProfile
{
    public sealed class LongitudinalProfileRowSettings
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public double Height { get; set; }
        public double TextHeight { get; set; }
        public string TextStyleName { get; set; }
        public short TextColorIndex { get; set; }
        public LongitudinalProfileRowSettings Clone()
        {
            return (LongitudinalProfileRowSettings)MemberwiseClone();
        }
    }

    public sealed class LongitudinalProfileEntityStyle
    {
        public string Name { get; set; }
        public short ColorIndex { get; set; }
        public string LineTypeName { get; set; }
        public double LineTypeScale { get; set; }
        public string LineWeight { get; set; }
        public LongitudinalProfileEntityStyle Clone()
        {
            return (LongitudinalProfileEntityStyle)MemberwiseClone();
        }
    }

    public sealed class LongitudinalProfileSettings
    {
        public double HeaderWidth { get; set; }
        public double HeaderChartGap { get; set; }
        public double HeaderTextHeight { get; set; }
        public string HeaderTextStyleName { get; set; }
        public short HeaderTextColorIndex { get; set; }
        public string HeaderTextAlignment { get; set; }
        public double HorizontalScale { get; set; }
        public double VerticalScale { get; set; }
        public double HorizontalGridInterval { get; set; }
        public double ElevationGridInterval { get; set; }
        public double ElevationPadding { get; set; }
        public int ElevationDecimals { get; set; }
        public int ValueDecimals { get; set; }
        public int SlopeDecimals { get; set; }
        public string LayerName { get; set; }
        public List<LongitudinalProfileRowSettings> Rows { get; set; }
        public List<LongitudinalProfileEntityStyle> Styles { get; set; }

        public LongitudinalProfileSettings()
        {
            HeaderWidth = 45.0;
            HeaderChartGap = 5.0;
            HeaderTextHeight = 6.0;
            HeaderTextStyleName = "宋体";
            HeaderTextColorIndex = 7;
            HeaderTextAlignment = "中间对齐";
            HorizontalScale = 1000.0;
            VerticalScale = 100.0;
            HorizontalGridInterval = 5.0;
            ElevationGridInterval = 1.0;
            ElevationPadding = 1.0;
            ElevationDecimals = 3;
            ValueDecimals = 2;
            SlopeDecimals = 2;
            LayerName = "CDBox-纵断面";
            Rows = DefaultRows();
            Styles = DefaultStyles();
        }

        public LongitudinalProfileSettings Clone()
        {
            var copy = (LongitudinalProfileSettings)MemberwiseClone();
            copy.Rows = (Rows ?? new List<LongitudinalProfileRowSettings>())
                .Where(x => x != null).Select(x => x.Clone()).ToList();
            copy.Styles =
                (Styles ?? new List<LongitudinalProfileEntityStyle>())
                    .Where(x => x != null).Select(x => x.Clone()).ToList();
            return copy;
        }

        public void Normalize()
        {
            HeaderWidth = Positive(HeaderWidth, 45.0);
            HeaderChartGap = NonNegative(HeaderChartGap, 5.0);
            HeaderTextHeight = Positive(HeaderTextHeight, 6.0);
            HeaderTextStyleName = Clean(HeaderTextStyleName, "宋体");
            HeaderTextColorIndex = Color(HeaderTextColorIndex, 7);
            HeaderTextAlignment = Clean(HeaderTextAlignment, "中间对齐");
            HorizontalScale = 1000.0;
            VerticalScale = 100.0;
            HorizontalGridInterval = Positive(HorizontalGridInterval, 5.0);
            ElevationGridInterval = Positive(ElevationGridInterval, 1.0);
            ElevationPadding = NonNegative(ElevationPadding, 1.0);
            ElevationDecimals = Clamp(ElevationDecimals, 0, 6, 3);
            ValueDecimals = Clamp(ValueDecimals, 0, 6, 2);
            SlopeDecimals = Clamp(SlopeDecimals, 0, 6, 2);
            LayerName = Clean(LayerName, "CDBox-纵断面");

            List<LongitudinalProfileRowSettings> defaults = DefaultRows();
            var fallbackRows = defaults.ToDictionary(x => x.Key,
                StringComparer.OrdinalIgnoreCase);
            var normalizedRows = new List<LongitudinalProfileRowSettings>();
            foreach (LongitudinalProfileRowSettings supplied in Rows ??
                new List<LongitudinalProfileRowSettings>())
            {
                if (supplied == null ||
                    string.IsNullOrWhiteSpace(supplied.Key)) continue;
                LongitudinalProfileRowSettings fallback;
                if (!fallbackRows.TryGetValue(supplied.Key.Trim(),
                        out fallback)) continue;
                LongitudinalProfileRowSettings row = supplied.Clone();
                row.Key = fallback.Key;
                row.Name = fallback.Name;
                row.Height = Positive(row.Height, fallback.Height);
                row.TextHeight = Positive(row.TextHeight,
                    fallback.TextHeight);
                row.TextStyleName = Clean(row.TextStyleName,
                    fallback.TextStyleName);
                row.TextColorIndex = Color(row.TextColorIndex,
                    fallback.TextColorIndex);
                normalizedRows.Add(row);
            }
            Rows = normalizedRows.Count > 0 ? normalizedRows : defaults;

            List<LongitudinalProfileEntityStyle> styleDefaults =
                DefaultStyles();
            var suppliedStyles = (Styles ??
                new List<LongitudinalProfileEntityStyle>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(),
                    StringComparer.OrdinalIgnoreCase);
            Styles = new List<LongitudinalProfileEntityStyle>();
            foreach (LongitudinalProfileEntityStyle fallback in styleDefaults)
            {
                LongitudinalProfileEntityStyle style;
                if (!suppliedStyles.TryGetValue(fallback.Name, out style))
                    style = fallback;
                style.Name = fallback.Name;
                style.ColorIndex = Color(style.ColorIndex,
                    fallback.ColorIndex);
                style.LineTypeName = Clean(style.LineTypeName,
                    fallback.LineTypeName);
                style.LineTypeScale = Positive(style.LineTypeScale,
                    fallback.LineTypeScale);
                style.LineWeight = Clean(style.LineWeight,
                    fallback.LineWeight);
                Styles.Add(style);
            }
        }

        public LongitudinalProfileRowSettings Row(string key)
        {
            Normalize();
            return Rows.First(x => string.Equals(x.Key, key,
                StringComparison.OrdinalIgnoreCase));
        }

        public LongitudinalProfileEntityStyle Style(string name)
        {
            Normalize();
            return Styles.First(x => string.Equals(x.Name, name,
                StringComparison.OrdinalIgnoreCase));
        }

        public static List<LongitudinalProfileRowSettings> DefaultRows()
        {
            return new List<LongitudinalProfileRowSettings>
            {
                RowValue("GroundElevation", "自然地面标高", 15.0),
                RowValue("DesignInvertElevation", "设计管内底标高", 10.0),
                RowValue("PipeBottomDepth", "管内底埋深", 10.0),
                RowValue("WellDepth", "井深", 10.0),
                RowValue("DiameterSlope", "管径及坡度", 10.0),
                RowValue("PlanDistance", "平面距离", 10.0),
                RowValue("PipeFoundation", "管道基础", 10.0),
                RowValue("WellNumber", "井编号", 10.0)
            };
        }

        public static List<LongitudinalProfileEntityStyle> DefaultStyles()
        {
            return new List<LongitudinalProfileEntityStyle>
            {
                StyleValue("坐标网格", 8),
                StyleValue("表头", 7),
                StyleValue("表头栏", 7)
            };
        }

        private static LongitudinalProfileRowSettings RowValue(string key,
            string name, double height)
        {
            return new LongitudinalProfileRowSettings
            {
                Key = key, Name = name, Height = height, TextHeight = 2.5,
                TextStyleName = "宋体", TextColorIndex = 7
            };
        }

        private static LongitudinalProfileEntityStyle StyleValue(string name,
            short color)
        {
            return new LongitudinalProfileEntityStyle
            {
                Name = name, ColorIndex = color, LineTypeName = "ByBlock",
                LineTypeScale = 1.0, LineWeight = "ByBlock"
            };
        }

        private static string Clean(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static double Positive(double value, double fallback)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ||
                value <= 0 ? fallback : value;
        }

        private static double NonNegative(double value, double fallback)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ||
                value < 0 ? fallback : value;
        }

        private static short Color(short value, short fallback)
        {
            return value < 0 || value > 256 ? fallback : value;
        }

        private static int Clamp(int value, int min, int max, int fallback)
        {
            return value < min || value > max ? fallback : value;
        }
    }

    /// <summary>纵断面设置持久化的基础兼容门面。</summary>
    public static class LongitudinalProfileSettingsStore
    {
        public static string SettingsPath
        {
            get
            {
                CDBox.Shared.Wastewater.Drafting
                    .IWastewaterLongitudinalProfileEngine engine =
                    CDBox.Shared.Wastewater.Drafting
                        .WastewaterLongitudinalProfileRegistry.Current;
                return engine == null ? string.Empty : engine.SettingsPath;
            }
        }

        public static LongitudinalProfileSettings Load()
        {
            CDBox.Shared.Wastewater.Drafting
                .IWastewaterLongitudinalProfileEngine engine =
                CDBox.Shared.Wastewater.Drafting
                    .WastewaterLongitudinalProfileRegistry.Current;
            return engine == null ? new LongitudinalProfileSettings()
                : engine.LoadSettings();
        }

        public static void Save(LongitudinalProfileSettings settings)
        {
            CDBox.Shared.Wastewater.Drafting
                .WastewaterLongitudinalProfileRegistry.GetRequired()
                .SaveSettings(settings);
        }
    }
}
