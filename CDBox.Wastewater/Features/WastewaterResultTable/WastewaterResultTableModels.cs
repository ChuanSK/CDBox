using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TCPipeAutoDraw.Modules.WastewaterResultTable
{
    public static class WastewaterResultTableDefaults
    {
        public const string EntityLayerName = "污水管成果表";
    }

    public sealed class WastewaterResultTableRow
    {
        public string NodeNo { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double GroundElevation { get; set; }
        public double WellDepth { get; set; }
        public string WellSpec { get; set; }

        public double BottomElevation
        {
            get { return GroundElevation - WellDepth; }
        }

        public WastewaterResultTableRow()
        {
            NodeNo = string.Empty;
            WellSpec = string.Empty;
        }
    }

    public static class WastewaterResultTableFormatter
    {
        private static readonly Regex DiameterNumber = new Regex(
            @"(?<!\d)(\d+(?:\.\d+)?)", RegexOptions.Compiled);

        public static IList<WastewaterResultTableRow> SortRows(
            IEnumerable<WastewaterResultTableRow> rows)
        {
            return (rows ?? Enumerable.Empty<WastewaterResultTableRow>())
                .Where(x => x != null)
                .OrderBy(x => x.NodeNo, NaturalNodeNumberComparer.Instance)
                .ToList();
        }

        public static string Coordinate(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        public static string Elevation(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        public static string Depth(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public static string Diameter(string value)
        {
            string text = (value ?? string.Empty).Trim();
            Match match = DiameterNumber.Match(text);
            return match.Success ? "%%c" + match.Groups[1].Value : text;
        }
    }

    internal sealed class NaturalNodeNumberComparer : IComparer<string>
    {
        public static readonly NaturalNodeNumberComparer Instance =
            new NaturalNodeNumberComparer();

        public int Compare(string left, string right)
        {
            string a = (left ?? string.Empty).Trim();
            string b = (right ?? string.Empty).Trim();
            if (a.Length == 0 || b.Length == 0)
            {
                if (a.Length == b.Length) return 0;
                return a.Length == 0 ? 1 : -1;
            }

            int ai = 0;
            int bi = 0;
            while (ai < a.Length && bi < b.Length)
            {
                bool aDigit = char.IsDigit(a[ai]);
                bool bDigit = char.IsDigit(b[bi]);
                int aEnd = FindRunEnd(a, ai, aDigit);
                int bEnd = FindRunEnd(b, bi, bDigit);
                string aRun = a.Substring(ai, aEnd - ai);
                string bRun = b.Substring(bi, bEnd - bi);

                int compared;
                if (aDigit && bDigit)
                    compared = CompareNumericRuns(aRun, bRun);
                else
                    compared = StringComparer.OrdinalIgnoreCase.Compare(
                        aRun, bRun);
                if (compared != 0) return compared;

                ai = aEnd;
                bi = bEnd;
            }

            if (ai != a.Length || bi != b.Length)
                return ai == a.Length ? -1 : 1;
            return StringComparer.Ordinal.Compare(a, b);
        }

        private static int FindRunEnd(string value, int start,
            bool digit)
        {
            int index = start + 1;
            while (index < value.Length
                   && char.IsDigit(value[index]) == digit)
                index++;
            return index;
        }

        private static int CompareNumericRuns(string left, string right)
        {
            string a = left.TrimStart('0');
            string b = right.TrimStart('0');
            if (a.Length == 0) a = "0";
            if (b.Length == 0) b = "0";
            int compared = a.Length.CompareTo(b.Length);
            if (compared != 0) return compared;
            compared = string.CompareOrdinal(a, b);
            if (compared != 0) return compared;
            return left.Length.CompareTo(right.Length);
        }
    }
}
