using System;
using System.Collections.Generic;
using System.Linq;

namespace CDBox.RealEstate.Models
{
    public static class ParcelBoundaryPointNumberFormatter
    {
        public static string FormatSignatureMiddle(string value)
        {
            List<string> numbers = (value ?? string.Empty)
                .Split(new[] { '、', ',', '，', ';', '；', ' ' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != "/")
                .ToList();
            if (numbers.Count == 0) return "/";
            if (numbers.Count >= 3)
                return numbers[0] + "-" + numbers[numbers.Count - 1];
            return string.Join("、", numbers);
        }
    }
}
