using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace TCPipeAutoDraw.Modules.ExcelToCad
{
    internal static class ExcelToCadSettingsStore
    {
        public static string SettingsFilePath
        {
            get
            {
                string root = Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
                return Path.Combine(root, "CDBox", "Studio",
                    "excel-to-cad-settings.xml");
            }
        }

        public static ExcelToCadDialogSettings Load(double defaultTextHeight)
        {
            var settings = new ExcelToCadDialogSettings
            {
                TextHeight = defaultTextHeight
            };
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    settings.Normalize(defaultTextHeight);
                    return settings;
                }
                XDocument document = XDocument.Load(SettingsFilePath);
                XElement root = document.Root;
                if (root == null)
                {
                    settings.Normalize(defaultTextHeight);
                    return settings;
                }
                ExcelTableRangeMode rangeMode;
                if (Enum.TryParse((string)root.Element("RangeMode"), true,
                    out rangeMode)) settings.RangeMode = rangeMode;
                CadExcelTableOutputType outputType;
                if (Enum.TryParse((string)root.Element("OutputType"), true,
                    out outputType)) settings.OutputType = outputType;
                settings.TextHeight = ReadDouble(root, "TextHeight",
                    settings.TextHeight);
                settings.PreserveBackgroundColors = ReadBool(root,
                    "PreserveBackgroundColors", settings.PreserveBackgroundColors);
                settings.PreserveTextColors = ReadBool(root,
                    "PreserveTextColors", settings.PreserveTextColors);
                settings.PreserveMergedCells = ReadBool(root,
                    "PreserveMergedCells", settings.PreserveMergedCells);
                settings.DrawGridLines = ReadBool(root, "DrawGridLines",
                    settings.DrawGridLines);
                settings.GridLayerName = ((string)root.Element("GridLayerName")
                    ?? settings.GridLayerName).Trim();
                settings.ContentLayerName = ((string)root.Element("ContentLayerName")
                    ?? settings.ContentLayerName).Trim();
                ExcelCadEntityColorMode colorMode;
                if (Enum.TryParse((string)root.Element("EntityColorMode"), true,
                    out colorMode)) settings.EntityColorMode = colorMode;
            }
            catch
            {
                settings = new ExcelToCadDialogSettings
                {
                    TextHeight = defaultTextHeight
                };
            }
            settings.Normalize(defaultTextHeight);
            return settings;
        }

        public static void Save(ExcelToCadDialogSettings settings)
        {
            if (settings == null) return;
            try
            {
                settings.Normalize(2.5);
                string directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                var root = new XElement("ExcelToCadSettings",
                    new XAttribute("Version", "1"),
                    new XElement("RangeMode", settings.RangeMode),
                    new XElement("OutputType", settings.OutputType),
                    new XElement("TextHeight", settings.TextHeight.ToString(
                        "0.###", CultureInfo.InvariantCulture)),
                    new XElement("PreserveBackgroundColors",
                        settings.PreserveBackgroundColors),
                    new XElement("PreserveTextColors",
                        settings.PreserveTextColors),
                    new XElement("PreserveMergedCells",
                        settings.PreserveMergedCells),
                    new XElement("DrawGridLines", settings.DrawGridLines),
                    new XElement("GridLayerName", settings.GridLayerName),
                    new XElement("ContentLayerName", settings.ContentLayerName),
                    new XElement("EntityColorMode", settings.EntityColorMode));
                new XDocument(root).Save(SettingsFilePath);
            }
            catch
            {
                // Preference persistence must never interrupt CAD work.
            }
        }

        private static double ReadDouble(XElement root, string name,
            double fallback)
        {
            double value;
            return double.TryParse((string)root.Element(name), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static bool ReadBool(XElement root, string name, bool fallback)
        {
            bool value;
            return bool.TryParse((string)root.Element(name), out value)
                ? value : fallback;
        }
    }
}
