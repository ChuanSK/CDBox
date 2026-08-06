using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    /// <summary>
    /// Keeps editable annotation text separate from source-derived length text.
    /// The helper is intentionally independent of AutoCAD so the migration and
    /// rebind rules stay deterministic.
    /// </summary>
    internal static class PipeLengthAnnotationTextComposer
    {
        private static readonly Regex NumberToken = new Regex(
            @"(?<![0-9])[-+]?[0-9]+(?:[\.,][0-9]+)?(?:\s*(?:m|米))?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static string Compose(string userText, string systemLengthText)
        {
            string user = (userText ?? string.Empty).TrimEnd();
            string system = (systemLengthText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(system)) return user;
            if (string.IsNullOrEmpty(user)) return system;
            return user + system;
        }

        public static void SplitLegacyText(string fullText, double sourceLength,
            out string userText, out string systemLengthText)
        {
            string source = fullText ?? string.Empty;
            Match best = null;
            double bestDifference = double.MaxValue;
            foreach (Match match in NumberToken.Matches(source))
            {
                string numeric = Regex.Match(match.Value, @"[-+]?[0-9]+(?:[\.,][0-9]+)?").Value.Replace(',', '.');
                double value;
                if (!double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) continue;
                double difference = Math.Abs(value - sourceLength);
                // When two numeric fragments are equally close (for example a DN value
                // and the actual length), the trailing fragment is the safer length token.
                if (difference <= bestDifference)
                {
                    best = match;
                    bestDifference = difference;
                }
            }

            double tolerance = Math.Max(Math.Abs(sourceLength) * 0.005, 0.011);
            if (best == null || bestDifference > tolerance)
            {
                userText = source;
                systemLengthText = string.Empty;
                return;
            }

            systemLengthText = best.Value;
            userText = (source.Remove(best.Index, best.Length)).TrimEnd();
        }

        public static string FormatLike(double length, string previousToken)
        {
            string token = previousToken ?? string.Empty;
            Match number = Regex.Match(token, @"[-+]?[0-9]+(?:[\.,][0-9]+)?");
            int decimals = 2;
            if (number.Success)
            {
                int separator = Math.Max(number.Value.IndexOf('.'), number.Value.IndexOf(','));
                decimals = separator < 0 ? 0 : number.Value.Length - separator - 1;
            }
            return FormatWithDecimals(length, decimals, token);
        }

        public static string FormatWithDecimals(double length, int decimals, string previousToken)
        {
            string token = previousToken ?? string.Empty;
            Match number = Regex.Match(token, @"[-+]?[0-9]+(?:[\.,][0-9]+)?");
            string suffix = number.Success
                ? token.Substring(number.Index + number.Length).Trim()
                : "m";
            decimals = Math.Max(0, Math.Min(decimals, 8));
            string format = decimals == 0 ? "0" : "0." + new string('0', decimals);
            return length.ToString(format, CultureInfo.InvariantCulture) + suffix;
        }

        public static void SplitLastLengthToken(string fullText, out string userText, out string systemLengthText)
        {
            string source = fullText ?? string.Empty;
            MatchCollection matches = NumberToken.Matches(source);
            if (matches.Count == 0)
            {
                userText = source;
                systemLengthText = string.Empty;
                return;
            }
            Match match = matches[matches.Count - 1];
            userText = source.Remove(match.Index, match.Length).TrimEnd();
            systemLengthText = match.Value;
        }

        public static string RemoveDetachedLengthToken(string frozenText, string oldToken)
        {
            string source = frozenText ?? string.Empty;
            string token = oldToken ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token)) return source;
            int index = source.LastIndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return source;
            return source.Remove(index, token.Length).TrimEnd();
        }

        public static string ReplaceDerivedLengthToken(string text, string oldToken,
            string newToken)
        {
            string source = text ?? string.Empty;
            string oldValue = (oldToken ?? string.Empty).Trim();
            string newValue = (newToken ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(newValue)) return source;

            string oldNumber = ExtractNumber(oldValue);
            string newNumber = ExtractNumber(newValue);
            if (string.IsNullOrEmpty(newNumber)) return source;

            string numberPattern = string.IsNullOrEmpty(oldNumber)
                ? @"[-+]?[0-9]+(?:[\.,][0-9]+)?"
                : Regex.Escape(oldNumber).Replace("\\.", "[\\.,]");
            Match labelled = Regex.Match(source,
                @"((?:\u957F|Length)\s*[:\uFF1A]?\s*)" + numberPattern,
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (labelled.Success)
            {
                int numberIndex = labelled.Index + labelled.Groups[1].Length;
                int numberLength = labelled.Length - labelled.Groups[1].Length;
                return source.Remove(numberIndex, numberLength).Insert(numberIndex, newNumber);
            }

            if (!string.IsNullOrEmpty(oldValue))
            {
                int exactIndex = source.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
                if (exactIndex >= 0)
                    return source.Remove(exactIndex, oldValue.Length).Insert(exactIndex, newValue);
            }

            if (string.IsNullOrEmpty(oldNumber)) return source;
            Match fallback = Regex.Match(source, Regex.Escape(oldNumber).Replace("\\.", "[\\.,]"),
                RegexOptions.CultureInvariant);
            return fallback.Success
                ? source.Remove(fallback.Index, fallback.Length).Insert(fallback.Index, newNumber)
                : source;
        }

        public static List<string> SplitBottomLines(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            string normalized = text.Replace("\\P", "\n")
                .Replace("\r\n", "\n").Replace('\r', '\n');
            foreach (string rawLine in normalized.Split(
                new[] { '\n' }, StringSplitOptions.None))
            {
                string line = Regex.Replace(rawLine ?? string.Empty,
                    @"\s+", " ").Trim();
                if (line.Length > 0) result.Add(line);
            }
            return result;
        }

        private static string ExtractNumber(string token)
        {
            Match match = Regex.Match(token ?? string.Empty,
                @"[-+]?[0-9]+(?:[\.,][0-9]+)?", RegexOptions.CultureInvariant);
            return match.Success ? match.Value : string.Empty;
        }
    }
}
