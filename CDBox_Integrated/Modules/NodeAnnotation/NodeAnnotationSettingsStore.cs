using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TCPipeAutoDraw.Modules.NodeAnnotation
{
    internal static class NodeAnnotationSettingsStore
    {
        private const string FolderName = "CDBox";
        private const string FileName = "NodeAnnotation.settings";

        public static NodeAnnotationOptions Load()
        {
            NodeAnnotationOptions options = NodeAnnotationOptions.Default;
            try
            {
                string path = GetSettingsPath();
                if (!File.Exists(path)) return options;

                Dictionary<string, string> values = ReadKeyValues(path);
                double doubleValue;
                int intValue;
                string text;

                if (TryGetDouble(values, "TextHeight", out doubleValue) && doubleValue > 0) options.TextHeight = doubleValue;
                if (TryGetInt(values, "DecimalPlaces", out intValue)) options.DecimalPlaces = Math.Max(0, Math.Min(6, intValue));
                if (TryGetString(values, "AnnotationFontName", out text) && !string.IsNullOrWhiteSpace(text)) options.AnnotationFontName = text;
                if (TryGetString(values, "AnnotationLayerName", out text) && !string.IsNullOrWhiteSpace(text)) options.AnnotationLayerName = text;
                if (TryGetDouble(values, "LineSpacingFactor", out doubleValue) && doubleValue > 0.5) options.LineSpacingFactor = doubleValue;
                if (TryGetInt(values, "NodeNoColorIndex", out intValue)) options.NodeNoColorIndex = NormalizeColorIndex(intValue, 1);
                if (TryGetInt(values, "TextColorIndex", out intValue)) options.TextColorIndex = NormalizeColorIndex(intValue, 7);
                if (TryGetInt(values, "PreviewLeaderColorIndex", out intValue)) options.PreviewLeaderColorIndex = NormalizeColorIndex(intValue, 1);
            }
            catch
            {
            }
            return NormalizeOptions(options);
        }

        public static void Save(NodeAnnotationOptions options)
        {
            if (options == null) return;
            options = NormalizeOptions(options);
            try
            {
                string path = GetSettingsPath();
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var lines = new List<string>();
                lines.Add("TextHeight=" + options.TextHeight.ToString("0.###", CultureInfo.InvariantCulture));
                lines.Add("DecimalPlaces=" + options.DecimalPlaces.ToString(CultureInfo.InvariantCulture));
                lines.Add("AnnotationFontName=" + (options.AnnotationFontName ?? string.Empty));
                lines.Add("AnnotationLayerName=" + (options.AnnotationLayerName ?? string.Empty));
                lines.Add("LineSpacingFactor=" + options.LineSpacingFactor.ToString("0.###", CultureInfo.InvariantCulture));
                lines.Add("NodeNoColorIndex=" + options.NodeNoColorIndex.ToString(CultureInfo.InvariantCulture));
                lines.Add("TextColorIndex=" + options.TextColorIndex.ToString(CultureInfo.InvariantCulture));
                lines.Add("PreviewLeaderColorIndex=" + options.PreviewLeaderColorIndex.ToString(CultureInfo.InvariantCulture));
                File.WriteAllLines(path, lines.ToArray());
            }
            catch
            {
            }
        }

        private static NodeAnnotationOptions NormalizeOptions(NodeAnnotationOptions options)
        {
            options = options ?? NodeAnnotationOptions.Default;
            if (options.TextHeight <= 0) options.TextHeight = 1.0;
            // 节点标注深度固定保留两位小数。
            options.DecimalPlaces = 2;
            if (string.IsNullOrWhiteSpace(options.AnnotationFontName)) options.AnnotationFontName = "宋体";
            if (string.IsNullOrWhiteSpace(options.AnnotationLayerName)) options.AnnotationLayerName = "ZJ";
            if (options.LineSpacingFactor <= 0.5) options.LineSpacingFactor = 1.45;
            options.NodeNoColorIndex = NormalizeColorIndex(options.NodeNoColorIndex, 1);
            options.TextColorIndex = NormalizeColorIndex(options.TextColorIndex, 7);
            options.PreviewLeaderColorIndex = NormalizeColorIndex(options.PreviewLeaderColorIndex, 1);
            return options;
        }

        private static short NormalizeColorIndex(int value, short fallback)
        {
            if (value < 1 || value > 255) return fallback;
            return (short)value;
        }

        private static string GetSettingsPath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);
            return Path.Combine(folder, FileName);
        }

        private static Dictionary<string, string> ReadKeyValues(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in File.ReadAllLines(path))
            {
                if (raw == null) continue;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int idx = line.IndexOf('=');
                if (idx <= 0) continue;
                values[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
            }
            return values;
        }

        private static bool TryGetString(Dictionary<string, string> values, string key, out string value)
        {
            value = string.Empty;
            return values != null && values.TryGetValue(key, out value);
        }

        private static bool TryGetDouble(Dictionary<string, string> values, string key, out double value)
        {
            value = 0;
            string text;
            if (!TryGetString(values, key, out text)) return false;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static bool TryGetInt(Dictionary<string, string> values, string key, out int value)
        {
            value = 0;
            string text;
            if (!TryGetString(values, key, out text)) return false;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                || int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
        }
    }
}
