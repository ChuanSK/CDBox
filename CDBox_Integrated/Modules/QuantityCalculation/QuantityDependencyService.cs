using System.Collections.Generic;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 旧调用点兼容门面。实际联动计算由动态加载的污水模块完成。
    /// </summary>
    public static class QuantityDependencyService
    {
        public static QuantityDependencyResult NormalizeDraft(
            QuantityPipeAttributes draft,
            IEnumerable<QuantityStructureLayer> submittedLayers,
            QuantityPipeAttributes previous,
            QuantityPipeAttributes startWell,
            QuantityPipeAttributes endWell,
            QuantityPipeAttributes branchDefaults,
            string changedField)
        {
            return QuantityAttributeEngineRegistry.GetRequired()
                .NormalizeDraft(draft, submittedLayers, previous, startWell,
                    endWell, branchDefaults, changedField);
        }

        public static double CalculateAverageDepthForEditor(
            double startDepth, double endDepth)
        {
            return QuantityAttributeEngineRegistry.GetRequired()
                .CalculateAverageDepthForEditor(startDepth, endDepth);
        }
    }
}
