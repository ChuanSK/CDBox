using System;

namespace TCPipeAutoDraw.Core.Check
{
    public static class DrawingCheckClassification
    {
        public static string NormalizeKind(string value)
        {
            string text = (value ?? string.Empty).Replace(" ", string.Empty)
                .Replace("　", string.Empty).Replace("/", string.Empty)
                .Replace("、", string.Empty).Trim();
            if (EqualsAny(text, "主管", "主线", "主管管线")) return "主管";
            if (EqualsAny(text, "支管", "支线", "支管管线")) return "支管";
            if (EqualsAny(text, "井", "节点", "检查井", "节点检查井"))
                return "井";
            return string.Empty;
        }

        private static bool EqualsAny(string value, params string[] options)
        {
            foreach (string option in options)
                if (string.Equals(value, option,
                    StringComparison.CurrentCultureIgnoreCase)) return true;
            return false;
        }
    }
}
