using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CDBox.Common.Features.ShortCodeRecognition
{
    public sealed class ShortCodeCoordinateRecord
    {
        public int SourceLineNumber { get; set; }
        public string PointNumber { get; set; }
        public string Code { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public sealed class ShortCodeParseIssue
    {
        public int LineNumber { get; set; }
        public string Message { get; set; }
    }

    public sealed class ShortCodeReadResult
    {
        public ShortCodeReadResult()
        {
            Records = new List<ShortCodeCoordinateRecord>();
            Issues = new List<ShortCodeParseIssue>();
        }

        public List<ShortCodeCoordinateRecord> Records { get; private set; }
        public List<ShortCodeParseIssue> Issues { get; private set; }
        public int InvalidLineCount { get; set; }
        public string EncodingName { get; set; }
    }

    public sealed class ShortCodePath
    {
        public ShortCodePath()
        {
            Points = new List<ShortCodeCoordinateRecord>();
        }

        public List<ShortCodeCoordinateRecord> Points { get; private set; }
        public bool Closed { get; set; }
        public bool IsJumpConnection { get; set; }
    }

    public static class ShortCodeRecognitionParser
    {
        private const int MaxRecordedIssues = 50;
        private const int MaximumFileBytes = 128 * 1024 * 1024;

        public static ShortCodeReadResult ReadFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("坐标文件路径不能为空。",
                    "filePath");
            FileInfo info = new FileInfo(filePath);
            if (!info.Exists)
                throw new FileNotFoundException("未找到坐标文件。", filePath);
            if (info.Length > MaximumFileBytes)
                throw new InvalidDataException(
                    "坐标文件超过 128 MB，请拆分后再识别。");

            byte[] bytes = File.ReadAllBytes(filePath);
            string encodingName;
            string text = DecodeText(bytes, out encodingName);
            ShortCodeReadResult result = ParseText(text);
            result.EncodingName = encodingName;
            return result;
        }

        public static ShortCodeReadResult ParseText(string text)
        {
            ShortCodeReadResult result = new ShortCodeReadResult();
            using (StringReader reader = new StringReader(text ?? string.Empty))
            {
                string line;
                int lineNumber = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    ParseLine(line, lineNumber, result);
                }
            }
            return result;
        }

        public static List<ShortCodePath> BuildPaths(
            IList<ShortCodeCoordinateRecord> records,
            ShortCodeRecognitionSettings settings)
        {
            List<ShortCodePath> result = new List<ShortCodePath>();
            if (records == null || records.Count == 0) return result;
            settings = settings ?? new ShortCodeRecognitionSettings();
            settings.Normalize();
            string symbol = settings.RecognitionSymbol;

            int index = 0;
            while (index < records.Count)
            {
                string code = NormalizeCode(records[index].Code);
                if (string.Equals(code, symbol,
                    StringComparison.Ordinal))
                {
                    int runStart = index;
                    int runEnd = index;
                    while (runEnd + 1 < records.Count
                        && string.Equals(
                            NormalizeCode(records[runEnd + 1].Code),
                            symbol, StringComparison.Ordinal))
                        runEnd++;

                    int first = settings.ConnectPreviousPoint
                        ? Math.Max(0, runStart - 1) : runStart;
                    int last = settings.ConnectNextPoint
                        ? Math.Min(records.Count - 1, runEnd + 1) : runEnd;
                    AddPath(result, records, first, last,
                        settings.AutoClose, false);
                    index = runEnd + 1;
                    continue;
                }

                int jumpCount;
                if (TryParseJumpCode(code, symbol, out jumpCount))
                {
                    int target = index - jumpCount - 1;
                    if (target >= 0 && target < records.Count)
                    {
                        ShortCodePath path = new ShortCodePath
                        {
                            Closed = false,
                            IsJumpConnection = true
                        };
                        AddPoint(path.Points, records[target]);
                        AddPoint(path.Points, records[index]);
                        if (path.Points.Count >= 2) result.Add(path);
                    }
                }
                index++;
            }
            return result;
        }

        private static void AddPath(ICollection<ShortCodePath> paths,
            IList<ShortCodeCoordinateRecord> records, int first, int last,
            bool close, bool jump)
        {
            ShortCodePath path = new ShortCodePath
            {
                IsJumpConnection = jump
            };
            for (int i = first; i <= last; i++)
                AddPoint(path.Points, records[i]);
            if (path.Points.Count < 2) return;
            path.Closed = close && path.Points.Count >= 3;
            paths.Add(path);
        }

        private static void AddPoint(
            IList<ShortCodeCoordinateRecord> points,
            ShortCodeCoordinateRecord point)
        {
            if (point == null) return;
            if (points.Count > 0)
            {
                ShortCodeCoordinateRecord previous =
                    points[points.Count - 1];
                if (Math.Abs(previous.X - point.X) <= 1e-10
                    && Math.Abs(previous.Y - point.Y) <= 1e-10)
                    return;
            }
            points.Add(point);
        }

        private static bool TryParseJumpCode(string code, string symbol,
            out int jumpCount)
        {
            jumpCount = 0;
            if (string.IsNullOrEmpty(code)
                || string.IsNullOrEmpty(symbol)
                || code.Length <= symbol.Length
                || !code.EndsWith(symbol, StringComparison.Ordinal))
                return false;
            string prefix = code.Substring(0,
                code.Length - symbol.Length).Trim();
            return int.TryParse(prefix, NumberStyles.None,
                    CultureInfo.InvariantCulture, out jumpCount)
                && jumpCount >= 0;
        }

        private static void ParseLine(string raw, int lineNumber,
            ShortCodeReadResult result)
        {
            string line = (raw ?? string.Empty).Trim();
            if (line.Length == 0 || line.StartsWith("#")
                || line.StartsWith("//")) return;

            List<string> fields;
            try
            {
                fields = ParseDelimitedFields(line);
            }
            catch (Exception ex)
            {
                AddIssue(result, lineNumber,
                    "字段解析失败：" + ex.Message);
                return;
            }
            if (fields.Count < 5)
            {
                AddIssue(result, lineNumber,
                    "字段不足 5 项，已跳过。");
                return;
            }

            double x;
            double y;
            double z;
            if (!TryParseFinite(fields[2], out x)
                || !TryParseFinite(fields[3], out y)
                || !TryParseFinite(fields[4], out z))
            {
                AddIssue(result, lineNumber,
                    "坐标或高程不是有效数值，已跳过。");
                return;
            }

            result.Records.Add(new ShortCodeCoordinateRecord
            {
                SourceLineNumber = lineNumber,
                PointNumber = Clean(fields[0]),
                Code = Clean(fields[1]),
                // CASS DAT 顺序为点号、编码、Y、X、H；
                // 文件中的第 3/4 列分别对应 CAD 的 X/Y。
                X = x,
                Y = y,
                Z = z
            });
        }

        private static List<string> ParseDelimitedFields(string line)
        {
            char delimiter = line.IndexOf(',') >= 0
                ? ',' : (line.IndexOf('\t') >= 0 ? '\t' : ',');
            List<string> fields = new List<string>();
            StringBuilder current = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length
                        && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                    continue;
                }
                if (c == delimiter && !quoted)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                    continue;
                }
                current.Append(c);
            }
            if (quoted)
                throw new FormatException("引号未闭合");
            fields.Add(current.ToString());
            return fields;
        }

        private static bool TryParseFinite(string text, out double value)
        {
            bool parsed = double.TryParse(Clean(text),
                    NumberStyles.Float, CultureInfo.InvariantCulture,
                    out value)
                || double.TryParse(Clean(text), NumberStyles.Float,
                    CultureInfo.CurrentCulture, out value);
            return parsed && !double.IsNaN(value)
                && !double.IsInfinity(value);
        }

        private static void AddIssue(ShortCodeReadResult result,
            int lineNumber, string message)
        {
            result.InvalidLineCount++;
            if (result.Issues.Count < MaxRecordedIssues)
            {
                result.Issues.Add(new ShortCodeParseIssue
                {
                    LineNumber = lineNumber,
                    Message = message ?? string.Empty
                });
            }
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty).Replace("\0", string.Empty).Trim();
        }

        private static string NormalizeCode(string value)
        {
            return Clean(value);
        }

        private static string DecodeText(byte[] bytes,
            out string encodingName)
        {
            bytes = bytes ?? new byte[0];
            if (bytes.Length >= 3 && bytes[0] == 0xEF
                && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                encodingName = "UTF-8";
                return new UTF8Encoding(false, true).GetString(bytes, 3,
                    bytes.Length - 3);
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFF
                && bytes[1] == 0xFE)
            {
                encodingName = "UTF-16 LE";
                return new UnicodeEncoding(false, true, true).GetString(
                    bytes, 2, bytes.Length - 2);
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFE
                && bytes[1] == 0xFF)
            {
                encodingName = "UTF-16 BE";
                return new UnicodeEncoding(true, true, true).GetString(
                    bytes, 2, bytes.Length - 2);
            }
            bool bigEndian;
            if (LooksLikeUtf16WithoutBom(bytes, out bigEndian))
            {
                encodingName = bigEndian
                    ? "UTF-16 BE（无 BOM）" : "UTF-16 LE（无 BOM）";
                return new UnicodeEncoding(bigEndian, false, true)
                    .GetString(bytes);
            }

            List<Encoding> candidates = new List<Encoding>
            {
                new UTF8Encoding(false, true)
            };
            try
            {
                candidates.Add(Encoding.GetEncoding(54936,
                    EncoderFallback.ExceptionFallback,
                    DecoderFallback.ExceptionFallback));
            }
            catch
            {
            }
            try
            {
                candidates.Add(Encoding.GetEncoding(
                    Encoding.Default.CodePage,
                    EncoderFallback.ExceptionFallback,
                    DecoderFallback.ExceptionFallback));
            }
            catch
            {
            }

            foreach (Encoding encoding in candidates)
            {
                try
                {
                    string decoded = encoding.GetString(bytes);
                    encodingName = encoding.WebName;
                    return decoded;
                }
                catch (DecoderFallbackException)
                {
                }
            }

            encodingName = Encoding.Default.WebName + "（替换无效字符）";
            return Encoding.Default.GetString(bytes);
        }

        private static bool LooksLikeUtf16WithoutBom(byte[] bytes,
            out bool bigEndian)
        {
            bigEndian = false;
            if (bytes == null || bytes.Length < 8
                || (bytes.Length & 1) != 0) return false;
            int limit = Math.Min(bytes.Length, 1024);
            int evenZeros = 0;
            int oddZeros = 0;
            int pairs = limit / 2;
            for (int i = 0; i + 1 < limit; i += 2)
            {
                if (bytes[i] == 0) evenZeros++;
                if (bytes[i + 1] == 0) oddZeros++;
            }
            if (oddZeros > pairs / 3 && evenZeros < pairs / 12)
            {
                bigEndian = false;
                return true;
            }
            if (evenZeros > pairs / 3 && oddZeros < pairs / 12)
            {
                bigEndian = true;
                return true;
            }
            return false;
        }
    }
}
