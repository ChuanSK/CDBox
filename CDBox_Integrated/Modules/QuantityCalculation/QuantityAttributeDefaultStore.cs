using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    internal static class QuantityAttributeDefaultStore
    {
        private const string FolderName = "CDBox";
        private const string FileName = "QuantityAttributeDefaults.settings";

        public static QuantityAttributeDefaults Load()
        {
            QuantityAttributeDefaults defaults = new QuantityAttributeDefaults();
            try
            {
                string path = GetSettingsPath();
                if (!File.Exists(path)) return defaults;
                Dictionary<string, string> values = ReadKeyValues(path);
                ReadAttributes(values, "Main", defaults.MainPipe);
                ReadAttributes(values, "Branch", defaults.BranchPipe);
                ReadAttributes(values, "Node", defaults.NodeWell);
            }
            catch
            {
                return new QuantityAttributeDefaults();
            }
            return defaults;
        }

        public static void Save(QuantityAttributeDefaults defaults)
        {
            if (defaults == null) return;
            try
            {
                string path = GetSettingsPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var lines = new List<string>();
                WriteAttributes(lines, "Main", defaults.MainPipe ?? QuantityPipeAttributes.DefaultMainPipe);
                WriteAttributes(lines, "Branch", defaults.BranchPipe ?? QuantityPipeAttributes.DefaultBranchPipe);
                WriteAttributes(lines, "Node", defaults.NodeWell ?? QuantityPipeAttributes.DefaultNodeWell);
                File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(true));
            }
            catch
            {
            }
        }

        public static QuantityPipeAttributes LoadForKind(string kind)
        {
            return Load().GetForKind(kind);
        }

        public static void SaveForKind(string kind, QuantityPipeAttributes attrs)
        {
            QuantityAttributeDefaults defaults = Load();
            defaults.SetForKind(kind, attrs);
            Save(defaults);
        }

        private static string GetSettingsPath()
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(baseDir)) baseDir = Path.GetTempPath();
            return Path.Combine(Path.Combine(baseDir, FolderName), FileName);
        }

        private static void WriteAttributes(List<string> lines, string prefix, QuantityPipeAttributes attrs)
        {
            attrs = attrs == null ? QuantityPipeAttributes.DefaultForKind(prefix) : attrs.CloneForDefaultProfile();
            Add(lines, prefix + ".Enabled", attrs.Enabled);
            Add(lines, prefix + ".ObjectKind", attrs.ObjectKind);
            Add(lines, prefix + ".Material", attrs.Material);
            Add(lines, prefix + ".Diameter", attrs.Diameter);
            Add(lines, prefix + ".DrawLengthWidthHeightAnnotation", attrs.DrawLengthWidthHeightAnnotation);
            Add(lines, prefix + ".TrenchWidth", attrs.TrenchWidth);
            Add(lines, prefix + ".RoadThickness", attrs.RoadThickness);
            Add(lines, prefix + ".ExcavationType", attrs.ExcavationType);
            Add(lines, prefix + ".BackfillType", attrs.BackfillType);
            Add(lines, prefix + ".BackfillStructure", attrs.BackfillStructure);
            Add(lines, prefix + ".BranchType", attrs.BranchType);
            Add(lines, prefix + ".BranchIncludeInCalculation", attrs.BranchIncludeInCalculation);
            Add(lines, prefix + ".BranchDepth", attrs.BranchDepth);
            Add(lines, prefix + ".WellSpec", attrs.WellSpec);
            Add(lines, prefix + ".WellCoverMaterial", attrs.WellCoverMaterial);
            Add(lines, prefix + ".WellMaterialType", attrs.WellMaterialType);
            Add(lines, prefix + ".WellType", attrs.WellType);
            Add(lines, prefix + ".SiltWellDeductDepth500", attrs.SiltWellDeductDepth500);
            Add(lines, prefix + ".SiltWellDeductDepth700", attrs.SiltWellDeductDepth700);
            Add(lines, prefix + ".GroundElevation", attrs.GroundElevation);
            Add(lines, prefix + ".WellDepth", attrs.WellDepth);
            Add(lines, prefix + ".ShaftLength", attrs.ShaftLength);
            Add(lines, prefix + ".ExcavationLength", attrs.ExcavationLength);
            Add(lines, prefix + ".ExcavationWidth", attrs.ExcavationWidth);
            Add(lines, prefix + ".CoverPlate", attrs.CoverPlate);
            Add(lines, prefix + ".SandCushionThickness", attrs.SandCushionThickness);
            Add(lines, prefix + ".GravelCushionThickness", attrs.GravelCushionThickness);
            Add(lines, prefix + ".C25RestoreThickness", attrs.C25RestoreThickness);
            Add(lines, prefix + ".PipeOuterDiameter", attrs.PipeOuterDiameter);
            Add(lines, prefix + ".DeductPipeVolume", attrs.DeductPipeVolume);
        }

        private static void ReadAttributes(Dictionary<string, string> values, string prefix, QuantityPipeAttributes attrs)
        {
            if (values == null || attrs == null) return;
            string text;
            double number;
            bool boolValue;
            if (TryGetBool(values, prefix + ".Enabled", out boolValue)) attrs.Enabled = boolValue;
            if (TryGetString(values, prefix + ".ObjectKind", out text)) attrs.ObjectKind = text;
            if (TryGetString(values, prefix + ".Material", out text)) attrs.Material = text;
            if (TryGetString(values, prefix + ".Diameter", out text)) attrs.Diameter = text;
            if (TryGetBool(values, prefix + ".DrawLengthWidthHeightAnnotation", out boolValue)) attrs.DrawLengthWidthHeightAnnotation = boolValue;
            if (TryGetDouble(values, prefix + ".TrenchWidth", out number)) attrs.TrenchWidth = number;
            if (TryGetDouble(values, prefix + ".RoadThickness", out number)) attrs.RoadThickness = number;
            if (TryGetString(values, prefix + ".ExcavationType", out text)) attrs.ExcavationType = text;
            if (TryGetString(values, prefix + ".BackfillType", out text)) attrs.BackfillType = text;
            if (TryGetString(values, prefix + ".BackfillStructure", out text)) attrs.BackfillStructure = text;
            if (TryGetString(values, prefix + ".BranchType", out text)) attrs.BranchType = text;
            if (TryGetBool(values, prefix + ".BranchIncludeInCalculation", out boolValue)) attrs.BranchIncludeInCalculation = boolValue;
            if (TryGetDouble(values, prefix + ".BranchDepth", out number)) attrs.BranchDepth = number;
            if (TryGetString(values, prefix + ".WellSpec", out text)) attrs.WellSpec = text;
            if (TryGetString(values, prefix + ".WellCoverMaterial", out text)) attrs.WellCoverMaterial = text;
            if (TryGetString(values, prefix + ".WellMaterialType", out text)) attrs.WellMaterialType = text;
            if (TryGetString(values, prefix + ".WellType", out text)) attrs.WellType = text;
            if (TryGetDouble(values, prefix + ".SiltWellDeductDepth500", out number)) attrs.SiltWellDeductDepth500 = number;
            if (TryGetDouble(values, prefix + ".SiltWellDeductDepth700", out number)) attrs.SiltWellDeductDepth700 = number;
            if (TryGetDouble(values, prefix + ".GroundElevation", out number)) attrs.GroundElevation = number;
            if (TryGetDouble(values, prefix + ".WellDepth", out number)) attrs.WellDepth = number;
            if (TryGetDouble(values, prefix + ".ShaftLength", out number)) attrs.ShaftLength = number;
            if (TryGetDouble(values, prefix + ".ExcavationLength", out number)) attrs.ExcavationLength = number;
            if (TryGetDouble(values, prefix + ".ExcavationWidth", out number)) attrs.ExcavationWidth = number;
            if (TryGetString(values, prefix + ".CoverPlate", out text)) attrs.CoverPlate = text;
            if (TryGetDouble(values, prefix + ".SandCushionThickness", out number)) attrs.SandCushionThickness = number;
            if (TryGetDouble(values, prefix + ".GravelCushionThickness", out number)) attrs.GravelCushionThickness = number;
            if (TryGetDouble(values, prefix + ".C25RestoreThickness", out number)) attrs.C25RestoreThickness = number;
            if (TryGetDouble(values, prefix + ".PipeOuterDiameter", out number)) attrs.PipeOuterDiameter = number;
            if (TryGetBool(values, prefix + ".DeductPipeVolume", out boolValue)) attrs.DeductPipeVolume = boolValue;
        }

        private static void Add(List<string> lines, string key, string value)
        {
            lines.Add(key + "=" + Escape(value ?? string.Empty));
        }

        private static void Add(List<string> lines, string key, double value)
        {
            Add(lines, key, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Add(List<string> lines, string key, bool value)
        {
            Add(lines, key, value ? "true" : "false");
        }

        private static bool TryGetString(Dictionary<string, string> values, string key, out string value)
        {
            value = string.Empty;
            if (values == null || !values.ContainsKey(key)) return false;
            value = values[key] ?? string.Empty;
            return true;
        }

        private static bool TryGetDouble(Dictionary<string, string> values, string key, out double value)
        {
            value = 0.0;
            string text;
            if (!TryGetString(values, key, out text)) return false;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static bool TryGetBool(Dictionary<string, string> values, string key, out bool value)
        {
            value = false;
            string text;
            if (!TryGetString(values, key, out text)) return false;
            value = QuantityPipeAttributes.ParseBool(text, false);
            return true;
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
                string value = Unescape(raw.Substring(index + 1));
                if (key.Length == 0) continue;
                dict[key] = value;
            }
            return dict;
        }

        private static string Escape(string text)
        {
            if (text == null) return string.Empty;
            return text.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string Unescape(string text)
        {
            if (text == null) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' && i + 1 < text.Length)
                {
                    char n = text[++i];
                    if (n == 'r') sb.Append('\r');
                    else if (n == 'n') sb.Append('\n');
                    else sb.Append(n);
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
