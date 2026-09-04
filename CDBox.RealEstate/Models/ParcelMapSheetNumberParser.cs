using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CDBox.RealEstate.Models
{
    /// <summary>
    /// 读取 CASS“建立格网”生成的矩形分幅号。大比例尺图幅号采用
    /// “西南角 X 公里数-西南角 Y 公里数”的形式。
    /// </summary>
    public static class ParcelMapSheetNumberParser
    {
        private static readonly Regex Pattern = new Regex(
            @"(?<!\d)(\d{1,7}(?:\.\d{1,3})?\s*[-－—]\s*\d{1,8}(?:\.\d{1,3})?)(?!\d)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static IList<string> Extract(IEnumerable<string> texts)
        {
            var values = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string source in texts ?? Enumerable.Empty<string>())
            {
                string text = source ?? string.Empty;
                foreach (Match match in Pattern.Matches(text))
                {
                    string value = Regex.Replace(match.Groups[1].Value,
                            @"\s+", string.Empty)
                        .Replace('－', '-').Replace('—', '-');
                    if (value.Length > 0) values.Add(value);
                }
            }
            return values.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
