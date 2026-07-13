using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TCPipeAutoDraw.Modules.SurfaceAreaAnnotation;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    /// <summary>
    /// 管线长度标注的本机设置缓存。
    /// 用普通文本保存到 %APPDATA%\CDBox，避免引入 Settings/配置文件依赖，方便在 AutoCAD 插件中直接使用。
    /// </summary>
    internal static class PipeLengthAnnotationSettingsStore
    {
        private const string FolderName = "CDBox";
        private const string FileName = "PipeLengthAnnotation.settings";

        public static PipeLengthAnnotationOptions Load()
        {
            PipeLengthAnnotationOptions options = PipeLengthAnnotationOptions.Default;

            try
            {
                string path = GetSettingsPath();
                if (!File.Exists(path)) return options;

                Dictionary<string, string> values = ReadKeyValues(path);

                double textHeight;
                if (TryGetDouble(values, "TextHeight", out textHeight) && textHeight > 0)
                {
                    options.TextHeight = textHeight;
                }

                int decimalPlaces;
                if (TryGetInt(values, "DecimalPlaces", out decimalPlaces))
                {
                    if (decimalPlaces < 0) decimalPlaces = 0;
                    if (decimalPlaces > 6) decimalPlaces = 6;
                    options.DecimalPlaces = decimalPlaces;
                }

                string text;
                if (TryGetString(values, "AnnotationTemplate", out text) && !string.IsNullOrWhiteSpace(text))
                {
                    options.AnnotationTemplate = text;
                }

                if (TryGetString(values, "AnnotationFontName", out text) && !string.IsNullOrWhiteSpace(text))
                {
                    options.AnnotationFontName = text;
                }

                if (TryGetString(values, "SelectedLayerName", out text) && !string.IsNullOrWhiteSpace(text))
                {
                    options.SelectedLayerName = text;
                }

                if (TryGetString(values, "AnnotationLayerName", out text) && !string.IsNullOrWhiteSpace(text))
                {
                    options.AnnotationLayerName = text;
                }

                AnnotationLayerMode mode;
                if (TryGetLayerMode(values, "LayerMode", out mode))
                {
                    options.LayerMode = mode;
                }

                AnnotationLayerLinkMode linkMode;
                if (TryGetLinkMode(values, "LayerLinkMode", out linkMode))
                {
                    options.LayerLinkMode = linkMode;
                }

                bool boolValue;
                if (TryGetBool(values, "EnableSourceMetadataLayerLink", out boolValue))
                {
                    options.EnableSourceMetadataLayerLink = boolValue;
                }

                if (TryGetBool(values, "WriteAutoAnnotationLayerMetadata", out boolValue))
                {
                    options.WriteAutoAnnotationLayerMetadata = boolValue;
                }

                if (TryGetString(values, "AutoAnnotationLayerSuffix", out text) && !string.IsNullOrWhiteSpace(text))
                {
                    options.AutoAnnotationLayerSuffix = text;
                }

                if (TryGetString(values, "FallbackAnnotationLayerName", out text) && !string.IsNullOrWhiteSpace(text))
                {
                    options.FallbackAnnotationLayerName = text;
                }

                if (TryGetString(values, "AnnotationSplitTagText", out text))
                {
                    options.AnnotationSplitTagText = text;
                }

                if (TryGetBool(values, "DrawBottomAnnotation", out boolValue))
                {
                    options.DrawBottomAnnotation = boolValue;
                }

                if (TryGetString(values, "BottomAnnotationTemplate", out text) && !string.IsNullOrWhiteSpace(text))
                {
                    options.BottomAnnotationTemplate = text;
                }

                double numberValue;
                if (TryGetDouble(values, "ExcavationWidth", out numberValue) && numberValue >= 0)
                {
                    options.ExcavationWidth = numberValue;
                }

                if (TryGetDouble(values, "ExcavationHeight", out numberValue) && numberValue >= 0)
                {
                    options.ExcavationHeight = numberValue;
                }

                if (TryGetDouble(values, "ExcavationDepth", out numberValue) && numberValue >= 0)
                {
                    options.ExcavationDepth = numberValue;
                }
            }
            catch
            {
                // 设置读取失败时不影响插件使用，直接采用默认值。
            }

            return options;
        }

        public static void Save(PipeLengthAnnotationOptions options)
        {
            if (options == null) return;

            try
            {
                SaveStrict(options);
            }
            catch
            {
                // 设置保存失败不能影响正式标注流程。
            }
        }

        internal static void SaveStrict(PipeLengthAnnotationOptions options)
        {
            if (options == null) throw new ArgumentNullException("options");

            string path = GetSettingsPath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var lines = new List<string>();
            lines.Add("TextHeight=" + Escape(options.TextHeight.ToString(CultureInfo.InvariantCulture)));
            lines.Add("DecimalPlaces=" + Escape(options.DecimalPlaces.ToString(CultureInfo.InvariantCulture)));
            lines.Add("AnnotationTemplate=" + Escape(options.AnnotationTemplate ?? string.Empty));
            lines.Add("AnnotationFontName=" + Escape(options.AnnotationFontName ?? string.Empty));
            lines.Add("LayerMode=" + Escape(options.LayerMode.ToString()));
            lines.Add("SelectedLayerName=" + Escape(options.SelectedLayerName ?? string.Empty));
            lines.Add("AnnotationLayerName=" + Escape(options.AnnotationLayerName ?? string.Empty));
            lines.Add("EnableSourceMetadataLayerLink=" + Escape(options.EnableSourceMetadataLayerLink ? "true" : "false"));
            lines.Add("LayerLinkMode=" + Escape(options.LayerLinkMode.ToString()));
            lines.Add("AutoAnnotationLayerSuffix=" + Escape(options.AutoAnnotationLayerSuffix ?? string.Empty));
            lines.Add("FallbackAnnotationLayerName=" + Escape(options.FallbackAnnotationLayerName ?? string.Empty));
            lines.Add("WriteAutoAnnotationLayerMetadata=" + Escape(options.WriteAutoAnnotationLayerMetadata ? "true" : "false"));
            lines.Add("AnnotationSplitTagText=" + Escape(options.AnnotationSplitTagText ?? string.Empty));
            lines.Add("DrawBottomAnnotation=" + Escape(options.DrawBottomAnnotation ? "true" : "false"));
            lines.Add("BottomAnnotationTemplate=" + Escape(options.BottomAnnotationTemplate ?? string.Empty));
            lines.Add("ExcavationWidth=" + Escape(options.ExcavationWidth.ToString(CultureInfo.InvariantCulture)));
            lines.Add("ExcavationHeight=" + Escape(options.ExcavationHeight.ToString(CultureInfo.InvariantCulture)));
            lines.Add("ExcavationDepth=" + Escape(options.ExcavationDepth.ToString(CultureInfo.InvariantCulture)));

            File.WriteAllLines(path, lines.ToArray());
        }

        private static string GetSettingsPath()
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(baseDir)) baseDir = Path.GetTempPath();
            return Path.Combine(Path.Combine(baseDir, FolderName), FileName);
        }

        private static Dictionary<string, string> ReadKeyValues(string path)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = File.ReadAllLines(path);
            foreach (string raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                int index = raw.IndexOf('=');
                if (index <= 0) continue;

                string key = raw.Substring(0, index).Trim();
                string value = raw.Substring(index + 1);
                if (string.IsNullOrWhiteSpace(key)) continue;
                dict[key] = Unescape(value);
            }
            return dict;
        }

        private static bool TryGetString(Dictionary<string, string> values, string key, out string value)
        {
            value = string.Empty;
            if (values == null || string.IsNullOrWhiteSpace(key)) return false;
            return values.TryGetValue(key, out value);
        }

        private static bool TryGetDouble(Dictionary<string, string> values, string key, out double value)
        {
            value = 0;
            string text;
            return TryGetString(values, key, out text)
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetInt(Dictionary<string, string> values, string key, out int value)
        {
            value = 0;
            string text;
            return TryGetString(values, key, out text)
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetLayerMode(Dictionary<string, string> values, string key, out AnnotationLayerMode mode)
        {
            mode = AnnotationLayerMode.DefaultZJ;
            string text;
            return TryGetString(values, key, out text)
                && Enum.TryParse<AnnotationLayerMode>(text, true, out mode);
        }

        private static bool TryGetLinkMode(Dictionary<string, string> values, string key, out AnnotationLayerLinkMode mode)
        {
            mode = AnnotationLayerLinkMode.ParentGroup;
            string text;
            return TryGetString(values, key, out text)
                && Enum.TryParse<AnnotationLayerLinkMode>(text, true, out mode);
        }

        private static bool TryGetBool(Dictionary<string, string> values, string key, out bool value)
        {
            value = false;
            string text;
            return TryGetString(values, key, out text)
                && bool.TryParse(text, out value);
        }

        private static string Escape(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty);
        }

        private static string Unescape(string value)
        {
            try { return Uri.UnescapeDataString(value ?? string.Empty); }
            catch { return value ?? string.Empty; }
        }
    }
}
