using System;
using System.Collections.Generic;
using System.Linq;

namespace CDBox.RealEstate.Models
{
    public static class ParcelBoundaryPointNumberFormatter
    {
        public static string FormatSegmentMiddle(string value)
        {
            return FormatMiddle(value, string.Empty);
        }

        public static string FormatSignatureMiddle(string value)
        {
            return FormatMiddle(value, "/");
        }

        public static IList<string> ExpandMiddle(string value,
            IEnumerable<string> orderedPointNumbers)
        {
            List<string> order = (orderedPointNumbers
                ?? Enumerable.Empty<string>()).Select(x =>
                    (x ?? string.Empty).Trim()).Where(x => x.Length > 0)
                .ToList();
            var result = new List<string>();
            foreach (string token in Split(value))
            {
                int separator = RangeSeparator(token);
                if (separator <= 0 || separator >= token.Length - 1)
                {
                    result.Add(token);
                    continue;
                }

                string start = token.Substring(0, separator).Trim();
                string end = token.Substring(separator + 1).Trim();
                int startIndex = IndexOf(order, start);
                int endIndex = IndexOf(order, end);
                if (startIndex < 0 || endIndex < 0)
                {
                    result.Add(token);
                    continue;
                }

                int current = startIndex;
                int guard = 0;
                while (guard++ < order.Count)
                {
                    result.Add(order[current]);
                    if (current == endIndex) break;
                    current = (current + 1) % order.Count;
                }
            }
            return result;
        }

        private static string FormatMiddle(string value, string emptyValue)
        {
            List<string> numbers = Split(value);
            if (numbers.Count == 0) return emptyValue;
            if (numbers.Count >= 3)
                return numbers[0] + "-" + numbers[numbers.Count - 1];
            return string.Join("、", numbers);
        }

        private static List<string> Split(string value)
        {
            List<string> numbers = (value ?? string.Empty)
                .Split(new[] { '、', ',', '，', ';', '；', ' ' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != "/")
                .ToList();
            return numbers;
        }

        private static int RangeSeparator(string value)
        {
            int index = value.IndexOf('-');
            if (index < 0) index = value.IndexOf('－');
            if (index < 0) index = value.IndexOf('—');
            return index;
        }

        private static int IndexOf(IList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value,
                    StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }
}
