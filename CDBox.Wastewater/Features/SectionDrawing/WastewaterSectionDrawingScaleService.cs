using System;
using CDBox.Wastewater.Features.SectionDrawing;

namespace TCPipeAutoDraw.Modules.SectionDrawing
{
    internal static class WastewaterSectionDrawingScaleService
    {
        public static SectionDrawingOptions CreateScaledOptions(SectionDrawingOptions source)
        {
            SectionDrawingOptions options = source == null ? SectionDrawingOptions.Default.Clone() : source.Clone();
            WastewaterSectionDrawingEngine.NormalizeCore(options);
            double factor = options.DrawingScale <= 0 ? 1.0 : options.DrawingScale;
            if (Math.Abs(factor - 1.0) <= 0.000000001) return options;

            options.Width *= factor;
            options.TotalHeight *= factor;
            options.TextHeight *= factor;
            options.LeftLabelWidth *= factor;
            options.TopDimensionOffset *= factor;
            options.BottomDimensionOffset *= factor;
            options.RightDimensionOffset *= factor;
            options.TitleOffset *= factor;

            if (options.Pipe != null) options.Pipe.Diameter *= factor;
            if (options.Layers != null)
            {
                foreach (SectionLayerOptions layer in options.Layers)
                {
                    if (layer == null) continue;
                    layer.Height *= factor;
                    if (layer.HatchScale > 0) layer.HatchScale *= factor;
                    if (layer.Pipes == null) continue;
                    foreach (SectionPipeOptions pipe in layer.Pipes)
                    {
                        if (pipe != null) pipe.Diameter *= factor;
                    }
                }
            }

            return options;
        }
    }
}
