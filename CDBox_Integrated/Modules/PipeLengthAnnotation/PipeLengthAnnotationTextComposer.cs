using System;
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
            string suffix = "m";
            if (number.Success)
            {
                int separator = Math.Max(number.Value.IndexOf('.'), number.Value.IndexOf(','));
                decimals = separator < 0 ? 0 : number.Value.Length - separator - 1;
                suffix = token.Substring(number.Index + number.Length).Trim();
            }
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
    }
}
