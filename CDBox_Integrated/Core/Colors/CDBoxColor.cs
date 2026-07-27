using System;
using System.Globalization;

namespace TCPipeAutoDraw.Core.Colors
{
    public enum CDBoxColorType
    {
        ByLayer,
        ByBlock,
        IndexColor,
        TrueColor,
        ColorBook,
        CDBoxStandard
    }

    public enum CDBoxColorOutputMode
    {
        PreserveOriginalType,
        PreferIndexColor,
        PreferTrueColor,
        CDBoxStandard
    }

    /// <summary>
    /// CDBox 内部统一颜色值。界面与业务模块使用此对象，CAD Color 仅在服务边界转换。
    /// </summary>
    public sealed class CDBoxColor
    {
        public CDBoxColorType Type { get; set; }
        public int Index { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public string BookName { get; set; }
        public string ColorName { get; set; }
        public string DisplayName { get; set; }
        public bool IsCDBoxStandard { get; set; }
        public string Category { get; set; }

        public CDBoxColor()
        {
            Type = CDBoxColorType.IndexColor;
            Index = 7;
            R = 255;
            G = 255;
            B = 255;
            BookName = string.Empty;
            ColorName = string.Empty;
            DisplayName = string.Empty;
            Category = string.Empty;
        }

        public string Hex
        {
            get { return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", R, G, B); }
        }

        public string RgbText
        {
            get { return string.Format(CultureInfo.InvariantCulture, "RGB {0}, {1}, {2}", R, G, B); }
        }

        public CDBoxColor Clone()
        {
            return new CDBoxColor
            {
                Type = Type,
                Index = Index,
                R = R,
                G = G,
                B = B,
                BookName = BookName ?? string.Empty,
                ColorName = ColorName ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                IsCDBoxStandard = IsCDBoxStandard,
                Category = Category ?? string.Empty
            };
        }

        public static CDBoxColor ByLayer()
        {
            return new CDBoxColor { Type = CDBoxColorType.ByLayer, Index = 256, DisplayName = "随层" };
        }

        public static CDBoxColor ByBlock()
        {
            return new CDBoxColor { Type = CDBoxColorType.ByBlock, Index = 0, DisplayName = "随块" };
        }

        public static CDBoxColor FromIndex(int index)
        {
            if (index == 256) return ByLayer();
            if (index == 0) return ByBlock();
            index = Math.Max(1, Math.Min(255, index));
            byte r;
            byte g;
            byte b;
            CDBoxColorConverter.AciToRgb(index, out r, out g, out b);
            return new CDBoxColor
            {
                Type = CDBoxColorType.IndexColor,
                Index = index,
                R = r,
                G = g,
                B = b,
                DisplayName = CDBoxColorConverter.GetAciDisplayName(index)
            };
        }

        public static CDBoxColor FromRgb(byte red, byte green, byte blue)
        {
            return new CDBoxColor
            {
                Type = CDBoxColorType.TrueColor,
                Index = 0,
                R = red,
                G = green,
                B = blue,
                DisplayName = string.Format(CultureInfo.InvariantCulture, "RGB {0}, {1}, {2}", red, green, blue)
            };
        }

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(DisplayName)) return DisplayName;
            switch (Type)
            {
                case CDBoxColorType.ByLayer: return "随层";
                case CDBoxColorType.ByBlock: return "随块";
                case CDBoxColorType.IndexColor: return CDBoxColorConverter.GetAciDisplayName(Index);
                case CDBoxColorType.ColorBook:
                    return string.IsNullOrWhiteSpace(BookName) ? ColorName : BookName + " · " + ColorName;
                default: return RgbText;
            }
        }
    }
}
