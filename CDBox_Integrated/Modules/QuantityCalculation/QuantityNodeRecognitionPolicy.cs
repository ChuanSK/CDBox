using System;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 节点属性识别的统一图层规则。
    /// </summary>
    public static class QuantityNodeRecognitionPolicy
    {
        public const string RequiredParentGroup = "井";
        public const string RequiredParentClass = "检查、沉泥井";

        public static bool IsSupportedLayer(string parentGroup,
            string parentClass)
        {
            return TextEquals(parentGroup, RequiredParentGroup)
                && TextEquals(parentClass, RequiredParentClass);
        }

        private static bool TextEquals(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right),
                StringComparison.CurrentCultureIgnoreCase);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return value.Replace(" ", string.Empty)
                .Replace("　", string.Empty)
                .Replace("/", string.Empty)
                .Replace("、", string.Empty)
                .Replace(",", string.Empty)
                .Replace("，", string.Empty)
                .Replace("\\", string.Empty)
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .Trim();
        }
    }
}
