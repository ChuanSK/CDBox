using System;
using System.Globalization;

namespace TCPipeAutoDraw.Core.Colors
{
    public static class CDBoxColorConverter
    {
        // AutoCAD ACI 10-249 uses five value levels (100%, 80%, 60%, 50%, 30%).
        // Each level is paired with 100% and 50% saturation.
        private static readonly byte[] ShadeValues = { 255, 204, 153, 127, 76 };

        public static void AciToRgb(int index, out byte red, out byte green, out byte blue)
        {
            index = Math.Max(1, Math.Min(255, index));
            switch (index)
            {
                case 1: red = 255; green = 0; blue = 0; return;
                case 2: red = 255; green = 255; blue = 0; return;
                case 3: red = 0; green = 255; blue = 0; return;
                case 4: red = 0; green = 255; blue = 255; return;
                case 5: red = 0; green = 0; blue = 255; return;
                case 6: red = 255; green = 0; blue = 255; return;
                case 7: red = 255; green = 255; blue = 255; return;
                case 8: red = 128; green = 128; blue = 128; return;
                case 9: red = 192; green = 192; blue = 192; return;
            }

            if (index >= 250)
            {
                byte[] gray = { 51, 80, 105, 130, 190, 255 };
                red = green = blue = gray[index - 250];
                return;
            }

            int offset = index - 10;
            int hueBand = offset / 10;
            int shade = offset % 10;
            double hue = hueBand * 15.0;
            double saturation = shade % 2 == 0 ? 1.0 : 0.5;
            double value = ShadeValues[shade / 2] / 255.0;
            HsvToRgb(hue, saturation, value, out red, out green, out blue);
        }

        public static int RgbToNearestAci(byte red, byte green, byte blue)
        {
            int bestIndex = 7;
            long bestDistance = long.MaxValue;
            for (int index = 1; index <= 255; index++)
            {
                byte r;
                byte g;
                byte b;
                AciToRgb(index, out r, out g, out b);
                long dr = red - r;
                long dg = green - g;
                long db = blue - b;
                long distance = dr * dr + dg * dg + db * db;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestIndex = index;
                if (distance == 0) break;
            }
            return bestIndex;
        }

        public static string GetAciDisplayName(int index)
        {
            switch (index)
            {
                case 1: return "红色 · ACI 1";
                case 2: return "黄色 · ACI 2";
                case 3: return "绿色 · ACI 3";
                case 4: return "青色 · ACI 4";
                case 5: return "蓝色 · ACI 5";
                case 6: return "洋红 · ACI 6";
                case 7: return "白色/黑色 · ACI 7";
                case 8: return "灰色 · ACI 8";
                case 9: return "浅灰 · ACI 9";
                default: return "ACI " + index.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static bool TryParseHex(string value, out byte red, out byte green, out byte blue)
        {
            red = green = blue = 0;
            string text = (value ?? string.Empty).Trim().TrimStart('#');
            if (text.Length == 3)
                text = new string(new[] { text[0], text[0], text[1], text[1], text[2], text[2] });
            if (text.Length != 6) return false;
            int packed;
            if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out packed)) return false;
            red = (byte)((packed >> 16) & 0xff);
            green = (byte)((packed >> 8) & 0xff);
            blue = (byte)(packed & 0xff);
            return true;
        }

        public static void RgbToHsv(byte red, byte green, byte blue, out double hue, out double saturation, out double value)
        {
            double r = red / 255.0;
            double g = green / 255.0;
            double b = blue / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;
            value = max;
            saturation = max <= 0.0 ? 0.0 : delta / max;
            if (delta <= 0.0) hue = 0.0;
            else if (Math.Abs(max - r) < 0.000001) hue = 60.0 * (((g - b) / delta) % 6.0);
            else if (Math.Abs(max - g) < 0.000001) hue = 60.0 * (((b - r) / delta) + 2.0);
            else hue = 60.0 * (((r - g) / delta) + 4.0);
            if (hue < 0.0) hue += 360.0;
        }

        public static void HsvToRgb(double hue, double saturation, double value, out byte red, out byte green, out byte blue)
        {
            hue = ((hue % 360.0) + 360.0) % 360.0;
            saturation = Math.Max(0.0, Math.Min(1.0, saturation));
            value = Math.Max(0.0, Math.Min(1.0, value));
            double chroma = value * saturation;
            double x = chroma * (1.0 - Math.Abs((hue / 60.0) % 2.0 - 1.0));
            double m = value - chroma;
            double r1;
            double g1;
            double b1;
            if (hue < 60.0) { r1 = chroma; g1 = x; b1 = 0.0; }
            else if (hue < 120.0) { r1 = x; g1 = chroma; b1 = 0.0; }
            else if (hue < 180.0) { r1 = 0.0; g1 = chroma; b1 = x; }
            else if (hue < 240.0) { r1 = 0.0; g1 = x; b1 = chroma; }
            else if (hue < 300.0) { r1 = x; g1 = 0.0; b1 = chroma; }
            else { r1 = chroma; g1 = 0.0; b1 = x; }
            red = ToByte((r1 + m) * 255.0);
            green = ToByte((g1 + m) * 255.0);
            blue = ToByte((b1 + m) * 255.0);
        }

        private static byte ToByte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value, MidpointRounding.AwayFromZero)));
        }
    }
}
