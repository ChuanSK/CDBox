using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TCPipeAutoDraw.Modules.LongitudinalProfile
{
    public sealed class LongitudinalProfileRowSettings
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public double Height { get; set; }
        public double TextHeight { get; set; }
        public string TextStyleName { get; set; }

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
            HorizontalScale = Positive(HorizontalScale, 1000.0);
            VerticalScale = Positive(VerticalScale, 100.0);
            HorizontalGridInterval =
                Positive(HorizontalGridInterval, 5.0);
            ElevationGridInterval =
                Positive(ElevationGridInterval, 1.0);
            ElevationPadding = NonNegative(ElevationPadding, 1.0);
            ElevationDecimals = Clamp(ElevationDecimals, 0, 6, 3);
            ValueDecimals = Clamp(ValueDecimals, 0, 6, 2);
            SlopeDecimals = Clamp(SlopeDecimals, 0, 6, 2);
            LayerName = Clean(LayerName, "CDBox-纵断面");

            List<LongitudinalProfileRowSettings> defaults = DefaultRows();
            var supplied = (Rows ?? new List<LongitudinalProfileRowSettings>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(),
                    StringComparer.OrdinalIgnoreCase);
            Rows = new List<LongitudinalProfileRowSettings>();
            foreach (LongitudinalProfileRowSettings fallback in defaults)
            {
                LongitudinalProfileRowSettings row;
                if (!supplied.TryGetValue(fallback.Key, out row))
                    row = fallback;
                row.Key = fallback.Key;
                row.Name = Clean(row.Name, fallback.Name);
                row.Height = Positive(row.Height, fallback.Height);
                row.TextHeight =
                    Positive(row.TextHeight, fallback.TextHeight);
                row.TextStyleName =
                    Clean(row.TextStyleName, fallback.TextStyleName);
                Rows.Add(row);
            }

            List<LongitudinalProfileEntityStyle> styleDefaults =
                DefaultStyles();
            var styleSupplied =
                (Styles ?? new List<LongitudinalProfileEntityStyle>())
                    .Where(x => x != null
                        && !string.IsNullOrWhiteSpace(x.Name))
                    .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First(),
                        StringComparer.OrdinalIgnoreCase);
            Styles = new List<LongitudinalProfileEntityStyle>();
            foreach (LongitudinalProfileEntityStyle fallback in styleDefaults)
            {
                LongitudinalProfileEntityStyle style;
                if (!styleSupplied.TryGetValue(fallback.Name, out style))
                    style = fallback;
                style.Name = fallback.Name;
                style.ColorIndex = Color(style.ColorIndex,
                    fallback.ColorIndex);
                style.LineTypeName =
                    Clean(style.LineTypeName, fallback.LineTypeName);
                style.LineTypeScale =
                    Positive(style.LineTypeScale, fallback.LineTypeScale);
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
                Row("GroundElevation", "自然地面标高", 15.0),
                Row("DesignInvertElevation", "设计管内底标高", 10.0),
                Row("PipeBottomDepth", "管内底埋深", 10.0),
                Row("WellDepth", "井深", 10.0),
                Row("DiameterSlope", "管径及坡度", 10.0),
                Row("PlanDistance", "平面距离", 10.0),
                Row("PipeFoundation", "管道基础", 10.0),
                Row("WellNumber", "井编号", 10.0)
            };
        }

        public static List<LongitudinalProfileEntityStyle> DefaultStyles()
        {
            return new List<LongitudinalProfileEntityStyle>
            {
                Style("坐标网格", 8),
                Style("表头", 7),
                Style("表头栏", 7)
            };
        }

        private static LongitudinalProfileRowSettings Row(
            string key, string name, double height)
        {
            return new LongitudinalProfileRowSettings
            {
                Key = key,
                Name = name,
                Height = height,
                TextHeight = 2.5,
                TextStyleName = "宋体"
            };
        }

        private static LongitudinalProfileEntityStyle Style(
            string name, short color)
        {
            return new LongitudinalProfileEntityStyle
            {
                Name = name,
                ColorIndex = color,
                LineTypeName = "ByBlock",
                LineTypeScale = 1.0,
                LineWeight = "ByBlock"
            };
        }

        private static string Clean(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback : value.Trim();
        }

        private static double Positive(double value, double fallback)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                || value <= 0 ? fallback : value;
        }

        private static double NonNegative(double value, double fallback)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                || value < 0 ? fallback : value;
        }

        private static short Color(short value, short fallback)
        {
            return value < 0 || value > 256 ? fallback : value;
        }

        private static int Clamp(
            int value, int min, int max, int fallback)
        {
            return value < min || value > max ? fallback : value;
        }
    }

    public static class LongitudinalProfileSettingsStore
    {
        private static readonly object Gate = new object();

        public static string SettingsPath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "CDBox");
                return Path.Combine(folder,
                    "LongitudinalProfileSettings.xml");
            }
        }

        public static LongitudinalProfileSettings Load()
        {
            lock (Gate)
            {
                var settings = new LongitudinalProfileSettings();
                try
                {
                    if (!File.Exists(SettingsPath)) return settings;
                    XElement root = XDocument.Load(SettingsPath).Root;
                    if (root == null) return settings;
                    int version = AttrInteger(root, "Version", 1);
                    settings.HeaderWidth = Number(root, "HeaderWidth",
                        settings.HeaderWidth);
                    settings.HeaderChartGap = Number(root, "HeaderChartGap",
                        settings.HeaderChartGap);
                    settings.HeaderTextHeight =
                        Number(root, "HeaderTextHeight",
                            settings.HeaderTextHeight);
                    settings.HeaderTextStyleName =
                        Text(root, "HeaderTextStyleName",
                            settings.HeaderTextStyleName);
                    settings.HeaderTextColorIndex =
                        (short)Integer(root, "HeaderTextColorIndex",
                            settings.HeaderTextColorIndex);
                    settings.HeaderTextAlignment =
                        Text(root, "HeaderTextAlignment",
                            settings.HeaderTextAlignment);
                    settings.HorizontalScale = Number(root,
                        "HorizontalScale", settings.HorizontalScale);
                    settings.VerticalScale = Number(root, "VerticalScale",
                        settings.VerticalScale);
                    settings.HorizontalGridInterval = Number(root,
                        "HorizontalGridInterval",
                        settings.HorizontalGridInterval);
                    settings.ElevationGridInterval = Number(root,
                        "ElevationGridInterval",
                        settings.ElevationGridInterval);
                    settings.ElevationPadding = Number(root,
                        "ElevationPadding", settings.ElevationPadding);
                    settings.ElevationDecimals = Integer(root,
                        "ElevationDecimals", settings.ElevationDecimals);
                    settings.ValueDecimals = Integer(root,
                        "ValueDecimals", settings.ValueDecimals);
                    settings.SlopeDecimals = Integer(root,
                        "SlopeDecimals", settings.SlopeDecimals);
                    settings.LayerName =
                        Text(root, "LayerName", settings.LayerName);

                    XElement rows = root.Element("Rows");
                    if (rows != null)
                    {
                        settings.Rows = rows.Elements("Row").Select(x =>
                            new LongitudinalProfileRowSettings
                            {
                                Key = Attr(x, "Key", string.Empty),
                                Name = Attr(x, "Name", string.Empty),
                                Height = AttrNumber(x, "Height", 10.0),
                                TextHeight =
                                    AttrNumber(x, "TextHeight", 2.5),
                                TextStyleName =
                                    Attr(x, "TextStyleName", "Standard")
                            }).ToList();
                    }
                    XElement styles = root.Element("Styles");
                    if (styles != null)
                    {
                        settings.Styles = styles.Elements("Style").Select(x =>
                            new LongitudinalProfileEntityStyle
                            {
                                Name = Attr(x, "Name", string.Empty),
                                ColorIndex =
                                    (short)AttrInteger(x, "ColorIndex", 7),
                                LineTypeName =
                                    Attr(x, "LineTypeName", "ByBlock"),
                                LineTypeScale =
                                    AttrNumber(x, "LineTypeScale", 1.0),
                                LineWeight =
                                    Attr(x, "LineWeight", "ByBlock")
                            }).ToList();
                    }

                    // 第一阶段近似样式保存过 Version 1/2 配置；这些旧值
                    // 会把网格间距、字体和表头尺寸带回近似版本。Version 3
                    // 起统一迁移到“纵断面参考.dwg”的精确基准。
                    if (version < 3)
                        settings = new LongitudinalProfileSettings();
                }
                catch
                {
                    settings = new LongitudinalProfileSettings();
                }
                settings.Normalize();
                return settings;
            }
        }

        public static void Save(LongitudinalProfileSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            lock (Gate)
            {
                settings = settings.Clone();
                settings.Normalize();
                string folder = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrWhiteSpace(folder))
                    Directory.CreateDirectory(folder);
                XElement root = new XElement("LongitudinalProfileSettings",
                    new XAttribute("Version", "3"),
                    Element("HeaderWidth", settings.HeaderWidth),
                    Element("HeaderChartGap", settings.HeaderChartGap),
                    Element("HeaderTextHeight", settings.HeaderTextHeight),
                    new XElement("HeaderTextStyleName",
                        settings.HeaderTextStyleName),
                    new XElement("HeaderTextColorIndex",
                        settings.HeaderTextColorIndex),
                    new XElement("HeaderTextAlignment",
                        settings.HeaderTextAlignment),
                    Element("HorizontalScale", settings.HorizontalScale),
                    Element("VerticalScale", settings.VerticalScale),
                    Element("HorizontalGridInterval",
                        settings.HorizontalGridInterval),
                    Element("ElevationGridInterval",
                        settings.ElevationGridInterval),
                    Element("ElevationPadding", settings.ElevationPadding),
                    new XElement("ElevationDecimals",
                        settings.ElevationDecimals),
                    new XElement("ValueDecimals", settings.ValueDecimals),
                    new XElement("SlopeDecimals", settings.SlopeDecimals),
                    new XElement("LayerName", settings.LayerName),
                    new XElement("Rows", settings.Rows.Select(x =>
                        new XElement("Row",
                            new XAttribute("Key", x.Key),
                            new XAttribute("Name", x.Name),
                            AttrElement("Height", x.Height),
                            AttrElement("TextHeight", x.TextHeight),
                            new XAttribute("TextStyleName",
                                x.TextStyleName)))),
                    new XElement("Styles", settings.Styles.Select(x =>
                        new XElement("Style",
                            new XAttribute("Name", x.Name),
                            new XAttribute("ColorIndex", x.ColorIndex),
                            new XAttribute("LineTypeName", x.LineTypeName),
                            AttrElement("LineTypeScale", x.LineTypeScale),
                            new XAttribute("LineWeight", x.LineWeight)))));
                new XDocument(root).Save(SettingsPath);
            }
        }

        private static XElement Element(string name, double value)
        {
            return new XElement(name,
                value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static XAttribute AttrElement(string name, double value)
        {
            return new XAttribute(name,
                value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static string Text(
            XElement root, string name, string fallback)
        {
            XElement item = root.Element(name);
            return item == null ? fallback : (item.Value ?? fallback);
        }

        private static double Number(
            XElement root, string name, double fallback)
        {
            XElement item = root.Element(name);
            return item == null ? fallback
                : ParseNumber(item.Value, fallback);
        }

        private static int Integer(
            XElement root, string name, int fallback)
        {
            XElement item = root.Element(name);
            int value;
            return item != null
                && int.TryParse(item.Value, out value) ? value : fallback;
        }

        private static string Attr(
            XElement item, string name, string fallback)
        {
            XAttribute value = item == null ? null : item.Attribute(name);
            return value == null ? fallback : (value.Value ?? fallback);
        }

        private static double AttrNumber(
            XElement item, string name, double fallback)
        {
            return ParseNumber(Attr(item, name, string.Empty), fallback);
        }

        private static int AttrInteger(
            XElement item, string name, int fallback)
        {
            int value;
            return int.TryParse(Attr(item, name, string.Empty), out value)
                ? value : fallback;
        }

        private static double ParseNumber(string text, double fallback)
        {
            double value;
            return double.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) ? value : fallback;
        }
    }
}
