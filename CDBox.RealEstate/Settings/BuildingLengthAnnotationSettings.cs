using System;
using CDBox.Shared.UI;

namespace CDBox.RealEstate.Settings
{
    public sealed class BuildingLengthAnnotationSettings
    {
        public BuildingLengthAnnotationSettings()
        {
            TextHeight = 0.5;
            AdaptiveTextHeight = true;
            TextStyleName = "Standard";
            TextColor = CDBoxModuleColor.FromIndex(7);
            AuxiliaryLinetypeName = "Continuous";
            AuxiliaryColor = CDBoxModuleColor.FromIndex(7);
            AuxiliaryLineweight = "ByLayer";
        }

        public double TextHeight { get; set; }
        public bool AdaptiveTextHeight { get; set; }
        public string TextStyleName { get; set; }
        public CDBoxModuleColor TextColor { get; set; }
        public string AuxiliaryLinetypeName { get; set; }
        public CDBoxModuleColor AuxiliaryColor { get; set; }
        public string AuxiliaryLineweight { get; set; }

        public void Normalize()
        {
            if (double.IsNaN(TextHeight) || double.IsInfinity(TextHeight)
                || TextHeight < 0.01 || TextHeight > 10000)
                TextHeight = 0.5;
            TextStyleName = Clean(TextStyleName, "Standard");
            TextColor = NormalizeColor(TextColor, 7);
            AuxiliaryLinetypeName = Clean(AuxiliaryLinetypeName,
                "Continuous");
            AuxiliaryColor = NormalizeColor(AuxiliaryColor, 7);
            AuxiliaryLineweight = NormalizeLineweight(AuxiliaryLineweight);
        }

        private static CDBoxModuleColor NormalizeColor(
            CDBoxModuleColor color, int fallbackIndex)
        {
            color = color == null
                ? CDBoxModuleColor.FromIndex(fallbackIndex) : color.Clone();
            if (!Enum.IsDefined(typeof(CDBoxModuleColorType), color.Type))
                return CDBoxModuleColor.FromIndex(fallbackIndex);
            if (color.Type == CDBoxModuleColorType.IndexColor
                && (color.Index < 1 || color.Index > 255))
                return CDBoxModuleColor.FromIndex(fallbackIndex);
            color.BookName = color.BookName ?? string.Empty;
            color.ColorName = color.ColorName ?? string.Empty;
            color.DisplayName = color.DisplayName ?? string.Empty;
            return color;
        }

        private static string NormalizeLineweight(string value)
        {
            string clean = Clean(value, "ByLayer");
            switch (clean.ToLowerInvariant())
            {
                case "bylayer": return "ByLayer";
                case "byblock": return "ByBlock";
                case "default": return "Default";
                case "0.05": case "0.09": case "0.13": case "0.15":
                case "0.18": case "0.20": case "0.25": case "0.30":
                case "0.35": case "0.40": case "0.50": case "0.60":
                case "0.70": case "0.80": case "0.90": case "1.00":
                    return clean;
                default: return "ByLayer";
            }
        }

        private static string Clean(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
