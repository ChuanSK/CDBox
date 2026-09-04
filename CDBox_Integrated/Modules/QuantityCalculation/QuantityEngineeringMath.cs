using System.Collections.Generic;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 旧报表计算调用点兼容门面。实际算法位于 Wastewater 模块。
    /// </summary>
    internal static class QuantityEngineeringMath
    {
        public static double CalculatePipeRemainingBackfillHeight(
            double totalDepth, IEnumerable<QuantityStructureLayer> layers)
        {
            return QuantityAttributeEngineRegistry.GetRequired()
                .CalculatePipeRemainingBackfillHeight(totalDepth, layers);
        }

        public static double CalculateEarthworkOut(double roadWaste,
            double excavation, double reusableOriginalSoil)
        {
            return QuantityAttributeEngineRegistry.GetRequired()
                .CalculateEarthworkOut(roadWaste, excavation,
                    reusableOriginalSoil);
        }
    }
}
