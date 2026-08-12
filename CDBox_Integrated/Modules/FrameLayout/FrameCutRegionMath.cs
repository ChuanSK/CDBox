using System;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    internal static class FrameCutRegionMath
    {
        public static bool Fits(double width, double height,
            double maximumWidth, double maximumHeight,
            double tolerance = 1e-6)
        {
            return width <= maximumWidth + tolerance
                && height <= maximumHeight + tolerance;
        }
    }
}
