using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TCPipeAutoDraw.Modules.SectionDrawing
{
    internal static class SectionDrawingSettingsStore
    {
        private const string FolderName = "CDBox";
        private const string FileName = "SectionDrawing.settings";

        public static SectionDrawingOptions Load()
        {
            SectionDrawingOptions options = SectionDrawingOptions.Default.Clone();

            try
            {
                string path = GetSettingsPath();
                if (!File.Exists(path)) return options;

                Dictionary<string, string> values = ReadKeyValues(path);
                string text;
                double number;
                int intValue;
                bool boolValue;

                if (TryGetString(values, "SectionTitle", out text)) options.SectionTitle = text;
                if (TryGetDouble(values, "Width", out number) && number > 0) options.Width = number;
                if (TryGetDouble(values, "TotalHeight", out number) && number > 0) options.TotalHeight = number;
                if (TryGetBool(values, "LockTotalHeight", out boolValue)) options.LockTotalHeight = boolValue;
                if (TryGetDouble(values, "TextHeight", out number) && number > 0) options.TextHeight = number;
                if (TryGetString(values, "TextStyleName", out text)) options.TextStyleName = text;
                if (TryGetString(values, "BorderLayerName", out text) && !string.IsNullOrWhiteSpace(text)) options.BorderLayerName = text;
                if (TryGetString(values, "TextLayerName", out text) && !string.IsNullOrWhiteSpace(text)) options.TextLayerName = text;
                if (TryGetString(values, "HatchLayerName", out text) && !string.IsNullOrWhiteSpace(text)) options.HatchLayerName = text;
                if (TryGetString(values, "DimensionLayerName", out text) && !string.IsNullOrWhiteSpace(text)) options.DimensionLayerName = text;
                if (TryGetString(values, "DimensionStyleName", out text)) options.DimensionStyleName = text;
                if (TryGetDouble(values, "LeftLabelWidth", out number) && number > 0) options.LeftLabelWidth = number;
                if (TryGetDouble(values, "TopDimensionOffset", out number) && number >= 0) options.TopDimensionOffset = number;
                if (TryGetDouble(values, "BottomDimensionOffset", out number) && number >= 0) options.BottomDimensionOffset = number;
                if (TryGetDouble(values, "RightDimensionOffset", out number) && number >= 0) options.RightDimensionOffset = number;
                if (TryGetDouble(values, "TitleOffset", out number) && number >= 0) options.TitleOffset = number;
                if (TryGetBool(values, "DrawTopDimension", out boolValue)) options.DrawTopDimension = boolValue;
                if (TryGetBool(values, "DrawBottomDimension", out boolValue)) options.DrawBottomDimension = boolValue;
                if (TryGetBool(values, "DrawRightDimensions", out boolValue)) options.DrawRightDimensions = boolValue;
                if (TryGetBool(values, "DrawTotalHeightDimension", out boolValue)) options.DrawTotalHeightDimension = boolValue;
                if (TryGetBool(values, "DrawTitle", out boolValue)) options.DrawTitle = boolValue;
                if (TryGetBool(values, "DrawPipeCircle", out boolValue)) options.DrawPipeCircle = boolValue;

                if (options.Pipe == null) options.Pipe = SectionPipeOptions.Default;
                if (TryGetDouble(values, "PipeDiameter", out number) && number > 0) options.Pipe.Diameter = number;
                if (TryGetString(values, "PipeText", out text)) options.Pipe.PipeText = text;
                if (TryGetInt(values, "PipeHostLayerIndex", out intValue)) options.Pipe.HostLayerIndex = intValue;
                if (TryGetString(values, "PipeVerticalMode", out text))
                {
                    SectionPipeVerticalMode mode;
                    if (Enum.TryParse<SectionPipeVerticalMode>(text, true, out mode)) options.Pipe.VerticalMode = mode;
                }

                if (TryGetInt(values, "LayerCount", out intValue) && intValue > 0)
                {
                    var layers = new List<SectionLayerOptions>();
                    for (int i = 0; i < intValue; i++)
                    {
                        var layer = new SectionLayerOptions();
                        if (TryGetBool(values, "Layer" + i + ".DrawLayer", out boolValue)) layer.DrawLayer = boolValue;
                        if (TryGetString(values, "Layer" + i + ".LeftLabel", out text)) layer.LeftLabel = text;
                        if (TryGetDouble(values, "Layer" + i + ".Height", out number) && number > 0) layer.Height = number;
                        if (TryGetBool(values, "Layer" + i + ".HeightLocked", out boolValue)) layer.HeightLocked = boolValue;
                        if (TryGetString(values, "Layer" + i + ".HatchPatternName", out text)) layer.HatchPatternName = text;
                        if (TryGetDouble(values, "Layer" + i + ".HatchScale", out number) && number >= 0) layer.HatchScale = number;
                        if (TryGetDouble(values, "Layer" + i + ".HatchAngle", out number)) layer.HatchAngle = number;

                        int pipeCount;
                        if (TryGetInt(values, "Layer" + i + ".PipeCount", out pipeCount) && pipeCount > 0)
                        {
                            for (int p = 0; p < pipeCount; p++)
                            {
                                var pipe = new SectionPipeOptions();
                                pipe.HostLayerIndex = i;
                                if (TryGetDouble(values, "Layer" + i + ".Pipe" + p + ".Diameter", out number) && number > 0) pipe.Diameter = number;
                                else pipe.Diameter = SectionPipeOptions.Default.Diameter;
                                if (TryGetString(values, "Layer" + i + ".Pipe" + p + ".PipeText", out text)) pipe.PipeText = text;
                                else pipe.PipeText = SectionPipeOptions.BuildPipeText(pipe.Diameter);
                                if (TryGetString(values, "Layer" + i + ".Pipe" + p + ".VerticalMode", out text))
                                {
                                    SectionPipeVerticalMode mode;
                                    if (Enum.TryParse<SectionPipeVerticalMode>(text, true, out mode)) pipe.VerticalMode = mode;
                                    else pipe.VerticalMode = SectionPipeVerticalMode.LayerCenter;
                                }
                                else pipe.VerticalMode = SectionPipeVerticalMode.LayerCenter;
                                layer.Pipes.Add(pipe);
                            }
                        }

                        layers.Add(layer);
                    }
                    options.Layers = layers;
                }
            }
            catch
            {
                // 设置损坏时直接退回默认值，避免影响绘图。
            }

            SectionLayoutCalculator.Normalize(options);
            return options;
        }

        public static void Save(SectionDrawingOptions options)
        {
            if (options == null) return;
            SectionLayoutCalculator.Normalize(options);

            try
            {
                string path = GetSettingsPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var lines = new List<string>();
                Add(lines, "SectionTitle", options.SectionTitle);
                Add(lines, "Width", options.Width);
                Add(lines, "TotalHeight", options.TotalHeight);
                Add(lines, "LockTotalHeight", options.LockTotalHeight);
                Add(lines, "TextHeight", options.TextHeight);
                Add(lines, "TextStyleName", options.TextStyleName);
                Add(lines, "BorderLayerName", options.BorderLayerName);
                Add(lines, "TextLayerName", options.TextLayerName);
                Add(lines, "HatchLayerName", options.HatchLayerName);
                Add(lines, "DimensionLayerName", options.DimensionLayerName);
                Add(lines, "DimensionStyleName", options.DimensionStyleName);
                Add(lines, "LeftLabelWidth", options.LeftLabelWidth);
                Add(lines, "TopDimensionOffset", options.TopDimensionOffset);
                Add(lines, "BottomDimensionOffset", options.BottomDimensionOffset);
                Add(lines, "RightDimensionOffset", options.RightDimensionOffset);
                Add(lines, "TitleOffset", options.TitleOffset);
                Add(lines, "DrawTopDimension", options.DrawTopDimension);
                Add(lines, "DrawBottomDimension", options.DrawBottomDimension);
                Add(lines, "DrawRightDimensions", options.DrawRightDimensions);
                Add(lines, "DrawTotalHeightDimension", options.DrawTotalHeightDimension);
                Add(lines, "DrawTitle", options.DrawTitle);
                Add(lines, "DrawPipeCircle", options.DrawPipeCircle);

                SectionPipeOptions pipe = options.Pipe ?? SectionPipeOptions.Default;
                Add(lines, "PipeDiameter", pipe.Diameter);
                Add(lines, "PipeText", pipe.PipeText);
                Add(lines, "PipeHostLayerIndex", pipe.HostLayerIndex);
                Add(lines, "PipeVerticalMode", pipe.VerticalMode.ToString());

                int count = options.Layers == null ? 0 : options.Layers.Count;
                Add(lines, "LayerCount", count);
                for (int i = 0; i < count; i++)
                {
                    SectionLayerOptions layer = options.Layers[i] ?? new SectionLayerOptions();
                    Add(lines, "Layer" + i + ".DrawLayer", layer.DrawLayer);
                    Add(lines, "Layer" + i + ".LeftLabel", layer.LeftLabel);
                    Add(lines, "Layer" + i + ".Height", layer.Height);
                    Add(lines, "Layer" + i + ".HeightLocked", layer.HeightLocked);
                    Add(lines, "Layer" + i + ".HatchPatternName", layer.HatchPatternName);
                    Add(lines, "Layer" + i + ".HatchScale", layer.HatchScale);
                    Add(lines, "Layer" + i + ".HatchAngle", layer.HatchAngle);

                    int pipeCount = layer.Pipes == null ? 0 : layer.Pipes.Count;
                    Add(lines, "Layer" + i + ".PipeCount", pipeCount);
                    for (int p = 0; p < pipeCount; p++)
                    {
                        SectionPipeOptions layerPipe = layer.Pipes[p] ?? SectionPipeOptions.Default;
                        Add(lines, "Layer" + i + ".Pipe" + p + ".Diameter", layerPipe.Diameter);
                        Add(lines, "Layer" + i + ".Pipe" + p + ".PipeText", layerPipe.PipeText);
                        Add(lines, "Layer" + i + ".Pipe" + p + ".VerticalMode", layerPipe.VerticalMode.ToString());
                    }
                }

                File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(true));
            }
            catch
            {
                // 保存失败不影响本次绘图。
            }
        }

        private static void Add(List<string> lines, string key, string value)
        {
            lines.Add(key + "=" + Escape(value ?? string.Empty));
        }

        private static void Add(List<string> lines, string key, double value)
        {
            Add(lines, key, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Add(List<string> lines, string key, int value)
        {
            Add(lines, key, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Add(List<string> lines, string key, bool value)
        {
            Add(lines, key, value ? "true" : "false");
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
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
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
            value = 0.0;
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

        private static bool TryGetBool(Dictionary<string, string> values, string key, out bool value)
        {
            value = false;
            string text;
            if (!TryGetString(values, key, out text)) return false;
            return bool.TryParse(text, out value);
        }

        private static string Escape(string value)
        {
            if (value == null) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string Unescape(string value)
        {
            if (value == null) return string.Empty;
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' && i + 1 < value.Length)
                {
                    char n = value[++i];
                    if (n == 'r') result.Append('\r');
                    else if (n == 'n') result.Append('\n');
                    else result.Append(n);
                }
                else result.Append(c);
            }
            return result.ToString();
        }
    }
}
