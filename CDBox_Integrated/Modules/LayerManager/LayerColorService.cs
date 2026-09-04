using System;
using Autodesk.AutoCAD.Colors;
using TCPipeAutoDraw.Core.Colors;
using AcadColor = Autodesk.AutoCAD.Colors.Color;

namespace TCPipeAutoDraw.Modules.LayerManager
{
    /// <summary>
    /// 公共业务程序集内的 CAD 颜色适配器。颜色值类型来自基础组件，
    /// AutoCAD Color 只在此边界出现。
    /// </summary>
    internal static class LayerColorService
    {
        public static CDBoxColor FromCadColor(AcadColor color)
        {
            if (color == null) return CDBoxColor.FromIndex(7);
            if (color.IsByLayer) return CDBoxColor.ByLayer();
            if (color.IsByBlock) return CDBoxColor.ByBlock();
            if (color.IsByAci)
            {
                CDBoxColor indexed = CDBoxColor.FromIndex(color.ColorIndex);
                ApplyRgb(indexed, color);
                return indexed;
            }

            System.Drawing.Color rgb = SafeColorValue(color);
            if (color.HasBookName || color.HasColorName)
            {
                return new CDBoxColor
                {
                    Type = CDBoxColorType.ColorBook,
                    R = rgb.R,
                    G = rgb.G,
                    B = rgb.B,
                    BookName = color.BookName ?? string.Empty,
                    ColorName = color.ColorName ?? string.Empty,
                    DisplayName = color.ColorNameForDisplay ?? string.Empty
                };
            }
            return CDBoxColor.FromRgb(rgb.R, rgb.G, rgb.B);
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
                    return AcadColor.FromColorIndex(ColorMethod.ByAci,
                        (short)Math.Max(1, Math.Min(255, value.Index)));
                case CDBoxColorType.ColorBook:
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(value.BookName)
                            && !string.IsNullOrWhiteSpace(value.ColorName))
                            return AcadColor.FromNames(value.ColorName.Trim(),
                                value.BookName.Trim());
                    }
                    catch
                    {
                    }
                    return AcadColor.FromRgb(value.R, value.G, value.B);
                default:
                    return AcadColor.FromRgb(value.R, value.G, value.B);
            }
        }

        public static CDBoxColor PrepareForWrite(CDBoxColor selected,
            CDBoxColor original)
        {
            return CDBoxColorOutputResolver.Resolve(selected, original,
                CDBoxColorOutputMode.PreserveOriginalType);
        }

        private static void ApplyRgb(CDBoxColor value, AcadColor color)
        {
            System.Drawing.Color rgb = SafeColorValue(color);
            value.R = rgb.R;
            value.G = rgb.G;
            value.B = rgb.B;
        }

        private static System.Drawing.Color SafeColorValue(AcadColor color)
        {
            try
            {
                return color.ColorValue;
            }
            catch
            {
                byte red;
                byte green;
                byte blue;
                CDBoxColorConverter.AciToRgb(color.ColorIndex,
                    out red, out green, out blue);
                return System.Drawing.Color.FromArgb(red, green, blue);
            }
        }
    }
}
