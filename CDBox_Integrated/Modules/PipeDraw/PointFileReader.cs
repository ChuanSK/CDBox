using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.Geometry;

namespace TCPipeAutoDraw.Modules.PipeDraw
{
    internal static class PointFileReader
    {
        public static List<PointRow> Read(string filePath, PipeDrawOptions options, IList<string> messages)
        {
            if (options == null) options = PipeDrawOptions.Default;
            var rows = new List<PointRow>();
            string[] lines = ReadAllLinesSmart(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i] == null ? string.Empty : lines[i].Trim();
                if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#")) continue;

                string[] parts = raw.Split(',').Select(delegate (string x) { return x.Trim(); }).ToArray();
                if (parts.Length < 5)
                {
                    AddMessage(messages, "第 " + (i + 1) + " 行字段不足，已跳过：" + raw);
                    continue;
                }

                double x;
                double y;
                double z;
                if (!TryParseDouble(parts[2], out x) || !TryParseDouble(parts[3], out y) || !TryParseDouble(parts[4], out z))
                {
                    AddMessage(messages, "第 " + (i + 1) + " 行坐标无法识别，已跳过：" + raw);
                    continue;
                }

                if (options.SwapXY)
                {
                    double t = x;
                    x = y;
                    y = t;
                }

                rows.Add(new PointRow(parts[0], parts[1], new Point3d(x, y, z)));
            }

            return rows;
        }

        private static string[] ReadAllLinesSmart(string filePath)
        {
            try
            {
                return File.ReadAllLines(filePath, new UTF8Encoding(false, true));
            }
            catch (DecoderFallbackException)
            {
                return File.ReadAllLines(filePath, Encoding.Default);
            }
        }

        private static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static void AddMessage(IList<string> messages, string message)
        {
            if (messages != null) messages.Add(message);
        }
    }
}
