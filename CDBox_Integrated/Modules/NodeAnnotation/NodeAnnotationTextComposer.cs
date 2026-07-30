using System;
using System.Collections.Generic;
using System.Globalization;

namespace TCPipeAutoDraw.Modules.NodeAnnotation
{
    internal static class NodeAnnotationTextComposer
    {
        public static Dictionary<string, string> Compose(string nodeNo,
            double wellDepth, double shaftLength, bool isSiltWell)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["NodeNo"] = string.IsNullOrWhiteSpace(nodeNo) ? "未编号" : nodeNo.Trim(),
                ["WellDepth"] = "井深:" + FormatMeter(wellDepth),
                ["ShaftLength"] = "井筒:" + FormatMeter(shaftLength)
            };
            if (isSiltWell) result["WellType"] = "沉泥井";
            return result;
        }

        private static string FormatMeter(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture) + "m";
        }
    }
}
