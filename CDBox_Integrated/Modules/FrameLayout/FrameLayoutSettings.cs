using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    public sealed class FrameLayoutSettings
    {
        public FrameLayoutSettings()
        {
            DrawNorthArrow = true;
            NorthDirectionMode = "Auto";
            NorthReferencePosition = "TopRight";
            NorthOffsetX = 30.0;
            NorthOffsetY = 10.0;
            NorthSize = 15.0;
            DrawScaleLabel = true;
            ScaleText = "1:500";
            ScaleTextHeight = 2.5;
            ScaleColorIndex = 1;
            ScaleTextStyle = "Standard";
            ScaleReferencePosition = "TopRight";
            ScaleOffsetX = 60.0;
            ScaleOffsetY = 10.0;
            FramesPerRow = 10;
            HorizontalGap = 10.0;
            VerticalGap = 10.0;
            TemplateViewMode = "Double";
        }

        public bool DrawNorthArrow { get; set; }
        public string NorthDirectionMode { get; set; }
        public string NorthReferencePosition { get; set; }
        public double NorthOffsetX { get; set; }
        public double NorthOffsetY { get; set; }
        public double NorthSize { get; set; }
        public bool DrawScaleLabel { get; set; }
        public string ScaleText { get; set; }
        public double ScaleTextHeight { get; set; }
        public short ScaleColorIndex { get; set; }
        public string ScaleTextStyle { get; set; }
        public string ScaleReferencePosition { get; set; }
        public double ScaleOffsetX { get; set; }
        public double ScaleOffsetY { get; set; }
        public int FramesPerRow { get; set; }
        public double HorizontalGap { get; set; }
        public double VerticalGap { get; set; }
        public string TemplateViewMode { get; set; }

        public void Normalize()
        {
            NorthDirectionMode = NormalizeChoice(NorthDirectionMode,
                new[] { "Auto", "WorldNorth", "FrameUp" }, "Auto");
            NorthReferencePosition = NormalizeChoice(NorthReferencePosition,
                FrameReferencePositions.All, "TopRight");
            ScaleReferencePosition = NormalizeChoice(ScaleReferencePosition,
                FrameReferencePositions.All, "TopRight");
            NorthOffsetX = Clamp(NorthOffsetX, -1000000, 1000000, 30);
            NorthOffsetY = Clamp(NorthOffsetY, -1000000, 1000000, 10);
            NorthSize = Clamp(NorthSize, 1, 100000, 15);
            ScaleText = string.IsNullOrWhiteSpace(ScaleText) ? "1:500" : ScaleText.Trim();
            ScaleTextHeight = Clamp(ScaleTextHeight, 0.1, 100000, 2.5);
            if (ScaleColorIndex < 1 || ScaleColorIndex > 255) ScaleColorIndex = 1;
            ScaleTextStyle = string.IsNullOrWhiteSpace(ScaleTextStyle)
                ? "Standard" : ScaleTextStyle.Trim();
            FramesPerRow = Math.Max(1, Math.Min(100, FramesPerRow));
            HorizontalGap = Clamp(HorizontalGap, 0, 1000000, 10);
            VerticalGap = Clamp(VerticalGap, 0, 1000000, 10);
            TemplateViewMode = NormalizeChoice(TemplateViewMode,
                new[] { "Single", "Double" }, "Double");
        }

        private static string NormalizeChoice(string value, string[] choices,
            string fallback)
        {
            foreach (string choice in choices)
            {
                if (string.Equals(choice, value, StringComparison.OrdinalIgnoreCase))
                    return choice;
            }
            return fallback;
        }

        private static double Clamp(double value, double min, double max,
            double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return fallback;
            return Math.Max(min, Math.Min(max, value));
        }
    }

    public static class FrameReferencePositions
    {
        public static readonly string[] All =
        {
            "TopLeft", "TopCenter", "TopRight",
            "MiddleLeft", "Center", "MiddleRight",
            "BottomLeft", "BottomCenter", "BottomRight"
        };
    }

    public static class FrameLayoutSettingsStore
    {
        private static readonly object Gate = new object();

        public static string SettingsPath
        {
            get
            {
                return Path.Combine(FrameTemplateCatalogStore.DataDirectory,
                    "FrameLayoutSettings.xml");
            }
        }

        public static FrameLayoutSettings Load()
        {
            lock (Gate)
            {
                FrameLayoutSettings settings = new FrameLayoutSettings();
                try
                {
                    if (!File.Exists(SettingsPath)) return settings;
                    XElement root = XDocument.Load(SettingsPath).Root;
                    if (root == null) return settings;
                    settings.DrawNorthArrow = Bool(root, "DrawNorthArrow",
                        settings.DrawNorthArrow);
                    settings.NorthDirectionMode = Text(root, "NorthDirectionMode",
                        settings.NorthDirectionMode);
                    settings.NorthReferencePosition = Text(root,
                        "NorthReferencePosition", settings.NorthReferencePosition);
                    settings.NorthOffsetX = Double(root, "NorthOffsetX",
                        settings.NorthOffsetX);
                    settings.NorthOffsetY = Double(root, "NorthOffsetY",
                        settings.NorthOffsetY);
                    settings.NorthSize = Double(root, "NorthSize",
                        settings.NorthSize);
                    settings.DrawScaleLabel = Bool(root, "DrawScaleLabel",
                        settings.DrawScaleLabel);
                    settings.ScaleText = Text(root, "ScaleText",
                        settings.ScaleText);
                    settings.ScaleTextHeight = Double(root, "ScaleTextHeight",
                        settings.ScaleTextHeight);
                    settings.ScaleColorIndex = (short)Int(root, "ScaleColorIndex",
                        settings.ScaleColorIndex);
                    settings.ScaleTextStyle = Text(root, "ScaleTextStyle",
                        settings.ScaleTextStyle);
                    settings.ScaleReferencePosition = Text(root,
                        "ScaleReferencePosition", settings.ScaleReferencePosition);
                    settings.ScaleOffsetX = Double(root, "ScaleOffsetX",
                        settings.ScaleOffsetX);
                    settings.ScaleOffsetY = Double(root, "ScaleOffsetY",
                        settings.ScaleOffsetY);
                    settings.FramesPerRow = Int(root, "FramesPerRow",
                        settings.FramesPerRow);
                    settings.HorizontalGap = Double(root, "HorizontalGap",
                        settings.HorizontalGap);
                    settings.VerticalGap = Double(root, "VerticalGap",
                        settings.VerticalGap);
                    settings.TemplateViewMode = Text(root, "TemplateViewMode",
                        settings.TemplateViewMode);
                }
                catch { }
                settings.Normalize();
                return settings;
            }
        }

        public static void Save(FrameLayoutSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            lock (Gate)
            {
                settings.Normalize();
                Directory.CreateDirectory(FrameTemplateCatalogStore.DataDirectory);
                XElement root = new XElement("FrameLayoutSettings",
                    new XAttribute("Version", "3"),
                    new XElement("DrawNorthArrow", settings.DrawNorthArrow),
                    new XElement("NorthDirectionMode", settings.NorthDirectionMode),
                    new XElement("NorthReferencePosition", settings.NorthReferencePosition),
                    new XElement("NorthOffsetX", Format(settings.NorthOffsetX)),
                    new XElement("NorthOffsetY", Format(settings.NorthOffsetY)),
                    new XElement("NorthSize", Format(settings.NorthSize)),
                    new XElement("DrawScaleLabel", settings.DrawScaleLabel),
                    new XElement("ScaleText", settings.ScaleText),
                    new XElement("ScaleTextHeight", Format(settings.ScaleTextHeight)),
                    new XElement("ScaleColorIndex", settings.ScaleColorIndex),
                    new XElement("ScaleTextStyle", settings.ScaleTextStyle),
                    new XElement("ScaleReferencePosition", settings.ScaleReferencePosition),
                    new XElement("ScaleOffsetX", Format(settings.ScaleOffsetX)),
                    new XElement("ScaleOffsetY", Format(settings.ScaleOffsetY)),
                    new XElement("FramesPerRow", settings.FramesPerRow),
                    new XElement("HorizontalGap", Format(settings.HorizontalGap)),
                    new XElement("VerticalGap", Format(settings.VerticalGap)),
                    new XElement("TemplateViewMode",
                        settings.TemplateViewMode));
                new XDocument(root).Save(SettingsPath);
            }
        }

        private static string Text(XElement root, string name, string fallback)
        {
            string value = (string)root.Element(name);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static bool Bool(XElement root, string name, bool fallback)
        {
            bool value;
            return bool.TryParse((string)root.Element(name), out value)
                ? value : fallback;
        }

        private static int Int(XElement root, string name, int fallback)
        {
            int value;
            return int.TryParse((string)root.Element(name),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value : fallback;
        }

        private static double Double(XElement root, string name, double fallback)
        {
            double value;
            return double.TryParse((string)root.Element(name),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value : fallback;
        }

        private static string Format(double value)
        {
            return value.ToString("0.########", CultureInfo.InvariantCulture);
        }
    }
}
