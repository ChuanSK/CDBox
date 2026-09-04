using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    internal static class WastewaterQuantityEngineeringMath
    {
        public static double CalculatePipeRemainingBackfillHeight(double totalDepth, IEnumerable<QuantityStructureLayer> layers)
        {
            double occupiedHeight = 0.0;
            if (layers != null)
            {
                foreach (QuantityStructureLayer layer in layers)
                {
                    if (layer == null) continue;
                    if (QuantityStructureLayer.IsSandBackfill(layer) || QuantityStructureLayer.IsOriginalSoilBackfill(layer)) continue;
                    occupiedHeight += layer.Height;
                }
            }
            return Math.Max(totalDepth - occupiedHeight, 0.0);
        }

        public static double CalculateEarthworkOut(double roadWaste, double excavation, double reusableOriginalSoil)
        {
            return Math.Max(roadWaste + excavation - reusableOriginalSoil, 0.0);
        }
    }
}
