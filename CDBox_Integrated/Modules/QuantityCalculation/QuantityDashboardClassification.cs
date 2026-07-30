using System;
using System.Linq;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    internal static class QuantityDashboardClassification
    {
        public static string BuildPipeType(string diameterValue, string materialValue)
        {
            string diameter = string.IsNullOrWhiteSpace(diameterValue) ? "未设置管径" : diameterValue.Trim();
            string material = string.IsNullOrWhiteSpace(materialValue) ? "未设置管材" : materialValue.Trim();

            if (diameter != "未设置管径" && !diameter.StartsWith("DN", StringComparison.CurrentCultureIgnoreCase) && diameter.All(char.IsDigit))
                diameter = "DN" + diameter;
            if (material != "未设置管材" && !material.EndsWith("管", StringComparison.CurrentCultureIgnoreCase))
                material += "管";

            return diameter + material;
        }

        public static bool ShouldIncludeInQualityCheck(QuantityPipeAttributes attributes)
        {
            return attributes == null || !attributes.IsSpecialObject;
        }
    }
}
