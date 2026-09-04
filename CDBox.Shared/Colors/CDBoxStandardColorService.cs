using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace TCPipeAutoDraw.Core.Colors
{
    public static class CDBoxStandardColorService
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static IReadOnlyList<CDBoxColor> GetColors()
        {
            try
            {
                string path = ResolvePath();
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    List<StandardColorRecord> records = Serializer.Deserialize<List<StandardColorRecord>>(File.ReadAllText(path));
                    List<CDBoxColor> parsed = Parse(records);
                    if (parsed.Count > 0) return parsed;
                }
            }
            catch { }
            return BuildDefaults();
        }

        public static CDBoxColor FindNearest(byte red, byte green, byte blue)
        {
            CDBoxColor best = null;
            long bestDistance = long.MaxValue;
            foreach (CDBoxColor color in GetColors())
            {
                long dr = red - color.R;
                long dg = green - color.G;
                long db = blue - color.B;
                long distance = dr * dr + dg * dg + db * db;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = color;
                if (distance == 0) break;
            }
            return (best ?? CDBoxColor.FromRgb(red, green, blue)).Clone();
        }

        private static string ResolvePath()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string candidate = Path.Combine(basePath, "Config", "CDBoxColors.json");
            if (File.Exists(candidate)) return candidate;
            string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? basePath;
            candidate = Path.Combine(assemblyPath, "Config", "CDBoxColors.json");
            return candidate;
        }

        private static List<CDBoxColor> Parse(IEnumerable<StandardColorRecord> records)
        {
            var result = new List<CDBoxColor>();
            foreach (StandardColorRecord record in records ?? new List<StandardColorRecord>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.Name)) continue;
                string[] parts = (record.RGB ?? string.Empty).Split(',');
                int r;
                int g;
                int b;
                if (parts.Length != 3
                    || !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out r)
                    || !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out g)
                    || !int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out b)) continue;
                result.Add(Standard(record.Name.Trim(), record.Category, (byte)ClampByte(r), (byte)ClampByte(g), (byte)ClampByte(b)));
            }
            return result;
        }

        private static IReadOnlyList<CDBoxColor> BuildDefaults()
        {
            return new[]
            {
                Standard("给水管", "管线", 0, 122, 255),
                Standard("污水管", "管线", 139, 92, 46),
                Standard("雨水管", "管线", 0, 180, 216),
                Standard("电力管", "管线", 229, 57, 53),
                Standard("通信管", "管线", 139, 67, 193),
                Standard("错误对象", "检查", 239, 68, 68),
                Standard("警告对象", "检查", 245, 158, 11),
                Standard("通过对象", "检查", 16, 185, 129)
            };
        }

        private static CDBoxColor Standard(string name, string category, byte red, byte green, byte blue)
        {
            return new CDBoxColor
            {
                Type = CDBoxColorType.CDBoxStandard,
                R = red,
                G = green,
                B = blue,
                DisplayName = name,
                ColorName = name,
                Category = string.IsNullOrWhiteSpace(category) ? "未分类" : category.Trim(),
                IsCDBoxStandard = true
            };
        }

        private static int ClampByte(int value)
        {
            return Math.Max(0, Math.Min(255, value));
        }

        private sealed class StandardColorRecord
        {
            public string Name { get; set; }
            public string RGB { get; set; }
            public string Category { get; set; }
        }
    }
}
