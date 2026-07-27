using System;
using System.Collections.Generic;
using System.Drawing;
using Autodesk.AutoCAD.Colors;
using AcadColor = Autodesk.AutoCAD.Colors.Color;

namespace TCPipeAutoDraw.Core.Colors
{
    public static class CDBoxColorService
    {
        private static CDBoxColorOutputMode _outputMode = CDBoxColorOutputMode.PreserveOriginalType;

        public static CDBoxColorOutputMode OutputMode
        {
            get { return _outputMode; }
            set { _outputMode = Enum.IsDefined(typeof(CDBoxColorOutputMode), value) ? value : CDBoxColorOutputMode.PreserveOriginalType; }
        }

        public static IReadOnlyList<CDBoxColor> GetAciPalette()
        {
            var result = new List<CDBoxColor>(255);
            for (short index = 1; index <= 255; index++)
                result.Add(FromCadColor(AcadColor.FromColorIndex(ColorMethod.ByAci, index)));
            return result;
        }

        public static CDBoxColor FromCadColor(AcadColor color)
        {
            if (color == null) return CDBoxColor.FromIndex(7);
            if (color.IsByLayer) return CDBoxColor.ByLayer();
            if (color.IsByBlock) return CDBoxColor.ByBlock();
            if (color.IsByAci) return WithCadRgb(CDBoxColor.FromIndex(color.ColorIndex), color);

            System.Drawing.Color value = SafeColorValue(color);
            if (color.HasBookName || color.HasColorName)
            {
                return new CDBoxColor
                {
                    Type = CDBoxColorType.ColorBook,
                    R = value.R,
                    G = value.G,
                    B = value.B,
                    BookName = color.BookName ?? string.Empty,
                    ColorName = color.ColorName ?? string.Empty,
                    DisplayName = color.ColorNameForDisplay ?? string.Empty
                };
            }
            CDBoxColor standard = CDBoxStandardColorService.FindNearest(value.R, value.G, value.B);
            if (standard.R == value.R && standard.G == value.G && standard.B == value.B) return standard;
            return new CDBoxColor
            {
                Type = CDBoxColorType.TrueColor,
                R = value.R,
                G = value.G,
                B = value.B,
                DisplayName = string.Format("RGB {0}, {1}, {2}", value.R, value.G, value.B)
            };
        }

        public static AcadColor ToCadColor(CDBoxColor value)
        {
            value = value ?? CDBoxColor.FromIndex(7);
            switch (value.Type)
            {
                case CDBoxColorType.ByLayer:
                    return AcadColor.FromColorIndex(ColorMethod.ByLayer, 256);
                case CDBoxColorType.ByBlock:
                    return AcadColor.FromColorIndex(ColorMethod.ByBlock, 0);
                case CDBoxColorType.IndexColor:
                    return AcadColor.FromColorIndex(ColorMethod.ByAci, (short)Math.Max(1, Math.Min(255, value.Index)));
                case CDBoxColorType.ColorBook:
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(value.BookName) && !string.IsNullOrWhiteSpace(value.ColorName))
                            return AcadColor.FromNames(value.ColorName.Trim(), value.BookName.Trim());
                    }
                    catch { }
                    return AcadColor.FromRgb(value.R, value.G, value.B);
                default:
                    return AcadColor.FromRgb(value.R, value.G, value.B);
            }
        }

        public static CDBoxColor PrepareForWrite(CDBoxColor selected, CDBoxColor original)
        {
            return CDBoxColorOutputResolver.Resolve(selected, original, OutputMode);
        }

        public static short ToCompatibleColorIndex(CDBoxColor value, short fallback)
        {
            if (value == null) return fallback;
            if (value.Type == CDBoxColorType.ByLayer) return 256;
            if (value.Type == CDBoxColorType.ByBlock) return 0;
            if (value.Type == CDBoxColorType.IndexColor) return (short)Math.Max(1, Math.Min(255, value.Index));
            return (short)CDBoxColorConverter.RgbToNearestAci(value.R, value.G, value.B);
        }

        private static CDBoxColor WithCadRgb(CDBoxColor value, AcadColor color)
        {
            System.Drawing.Color rgb = SafeColorValue(color);
            value.R = rgb.R;
            value.G = rgb.G;
            value.B = rgb.B;
            return value;
        }

        private static System.Drawing.Color SafeColorValue(AcadColor color)
        {
            try { return color.ColorValue; }
            catch
            {
                byte red;
                byte green;
                byte blue;
                CDBoxColorConverter.AciToRgb(color.ColorIndex, out red, out green, out blue);
                return System.Drawing.Color.FromArgb(red, green, blue);
            }
        }
    }
}
