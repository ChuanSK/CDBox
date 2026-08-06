using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TCPipeAutoDraw.Modules.SurfaceAreaAnnotation
{
    /// <summary>
    /// ?????????????
    /// ????????????????? CDBOX ?????????????
    /// </summary>
    internal static class SurfaceAreaAnnotationSettingsStore
    {
        private const string FolderName = "CDBox";
        private const string FileName = "SurfaceAreaAnnotation.settings";

        public static SurfaceAreaAnnotationOptions Load()
        {
            SurfaceAreaAnnotationOptions options = SurfaceAreaAnnotationOptions.Default;

            try
            {
                string path = GetSettingsPath();
                if (!File.Exists(path)) return options;

                Dictionary<string, string> values = ReadKeyValues(path);

                double numberValue;
                if (TryGetDouble(values, "BoundaryInterval", out numberValue) && numberValue > 0)
                {
                    options.BoundaryInterval = numberValue;
                }

                if (TryGetDouble(values, "TextHeight", out numberValue) && numberValue > 0)
                {
                    options.TextHeight = numberValue;
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

                SurfaceAreaCalculationMode calculationMode;
                if (TryGetCalculationMode(values, "CalculationMode", out calculationMode))
                {
                    options.CalculationMode = calculationMode;
                }

                if (TryGetString(values, "CassSurfaceLogPath", out text))
                {
                    options.CassSurfaceLogPath = text ?? string.Empty;
                }

                bool boolValue;
                if (TryGetBool(values, "DeleteCassGeneratedObjects", out boolValue))
                {
                    options.DeleteCassGeneratedObjects = boolValue;
                }

                if (TryGetString(values, "AnnotationFontName", out text) && !string.IsNullOrWhiteSpace(text))
                {
                    options.AnnotationFontName = text;
                }

                AnnotationLayerMode mode;
                if (TryGetLayerMode(values, "LayerMode", out mode))
                {
                    options.LayerMode = mode;
                }

                if (TryGetString(values, "SelectedLayerName", out text) && !string.IsNullOrWhiteSpace(text))
                {
                    options.SelectedLayerName = text;
                }

                if (TryGetString(values, "AnnotationLayerName", out text) && !string.IsNullOrWhiteSpace(text))
                {
                    options.AnnotationLayerName = text;
                }

                options.UseBoundaryLayerForAnnotation = false;
                options.DrawLeader = true;
            }
            catch
            {
                // ????????????????????????
            }

            return options;
        }

        public static void Save(SurfaceAreaAnnotationOptions options)
        {
            if (options == null) return;

            try
            {
                SaveStrict(options);
            }
            catch
            {
                // ?????????????????
            }
        }

        internal static void SaveStrict(SurfaceAreaAnnotationOptions options)
        {
            if (options == null) throw new ArgumentNullException("options");

            string path = GetSettingsPath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var lines = new List<string>();
            lines.Add("BoundaryInterval=" + Escape(options.BoundaryInterval.ToString(CultureInfo.InvariantCulture)));
            lines.Add("TextHeight=" + Escape(options.TextHeight.ToString(CultureInfo.InvariantCulture)));
            lines.Add("DecimalPlaces=" + Escape(options.DecimalPlaces.ToString(CultureInfo.InvariantCulture)));
            lines.Add("AnnotationTemplate=" + Escape(options.AnnotationTemplate ?? string.Empty));
            lines.Add("CalculationMode=" + Escape(options.CalculationMode.ToString()));
            lines.Add("CassSurfaceLogPath=" + Escape(options.CassSurfaceLogPath ?? string.Empty));
            lines.Add("DeleteCassGeneratedObjects=" + Escape(options.DeleteCassGeneratedObjects ? "true" : "false"));
            lines.Add("AnnotationFontName=" + Escape(options.AnnotationFontName ?? string.Empty));
            lines.Add("LayerMode=" + Escape(options.LayerMode.ToString()));
            lines.Add("SelectedLayerName=" + Escape(options.SelectedLayerName ?? string.Empty));
            lines.Add("AnnotationLayerName=" + Escape(options.AnnotationLayerName ?? string.Empty));

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

        private static bool TryGetBool(Dictionary<string, string> values, string key, out bool value)
        {
            value = false;
            string text;
            return TryGetString(values, key, out text)
                && bool.TryParse(text, out value);
        }

        private static bool TryGetLayerMode(Dictionary<string, string> values, string key, out AnnotationLayerMode mode)
        {
            mode = AnnotationLayerMode.DefaultZJ;
            string text;
            return TryGetString(values, key, out text)
                && Enum.TryParse<AnnotationLayerMode>(text, true, out mode);
        }

        private static bool TryGetCalculationMode(Dictionary<string, string> values, string key, out SurfaceAreaCalculationMode mode)
        {
            mode = SurfaceAreaCalculationMode.CassCommand;
            string text;
            if (!TryGetString(values, key, out text)
                || !Enum.TryParse<SurfaceAreaCalculationMode>(text, true, out mode)) return false;
            return mode == SurfaceAreaCalculationMode.CassCommand
                || mode == SurfaceAreaCalculationMode.PlanArea;
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
