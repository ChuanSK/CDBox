using System.Collections.Generic;

namespace TCPipeAutoDraw.Core.Colors
{
    public static class CDBoxColorService
    {
        public static IReadOnlyList<CDBoxColor> GetAciPalette()
        {
            var colors = new List<CDBoxColor>(255);
            for (int i = 1; i <= 255; i++) colors.Add(CDBoxColor.FromIndex(i));
            return colors;
        }
    }
}
