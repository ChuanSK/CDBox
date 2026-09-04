using System;
using System.Text.RegularExpressions;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxReleaseVersionComparer
    {
        private static readonly Regex Pattern = new Regex(
            @"^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z][0-9A-Za-z.-]*))?$",
            RegexOptions.CultureInvariant);

        public static bool IsValid(string version)
        {
            return Pattern.IsMatch((version ?? string.Empty).Trim());
        }

        public static int Compare(string left, string right)
        {
            Match a = Pattern.Match((left ?? string.Empty).Trim());
            Match b = Pattern.Match((right ?? string.Empty).Trim());
            if (!a.Success || !b.Success)
                throw new ArgumentException("发布版本号格式无效。");

            for (int i = 1; i <= 3; i++)
            {
                int comparison = CompareNumbers(a.Groups[i].Value,
                    b.Groups[i].Value);
                if (comparison != 0) return comparison;
            }

            string aPreview = a.Groups[4].Value;
            string bPreview = b.Groups[4].Value;
            if (aPreview.Length == 0)
                return bPreview.Length == 0 ? 0 : 1;
            if (bPreview.Length == 0) return -1;

            string[] aParts = aPreview.Split('.');
            string[] bParts = bPreview.Split('.');
            int count = Math.Min(aParts.Length, bParts.Length);
            for (int i = 0; i < count; i++)
            {
                bool aNumeric = Regex.IsMatch(aParts[i], "^[0-9]+$");
                bool bNumeric = Regex.IsMatch(bParts[i], "^[0-9]+$");
                int comparison = aNumeric && bNumeric
                    ? CompareNumbers(aParts[i], bParts[i])
                    : aNumeric != bNumeric ? (aNumeric ? -1 : 1)
                    : string.Compare(aParts[i], bParts[i],
                        StringComparison.Ordinal);
                if (comparison != 0) return comparison;
            }
            return aParts.Length.CompareTo(bParts.Length);
        }

        private static int CompareNumbers(string left, string right)
        {
            string a = left.TrimStart('0');
            string b = right.TrimStart('0');
            int length = a.Length.CompareTo(b.Length);
            return length != 0 ? length
                : string.Compare(a, b, StringComparison.Ordinal);
        }
    }
}
