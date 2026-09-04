using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 污水工程量属性的业务计算和默认值持久化边界。
    /// </summary>
    public interface IQuantityAttributeEngine
    {
        QuantityDependencyResult NormalizeDraft(
            QuantityPipeAttributes draft,
            IEnumerable<QuantityStructureLayer> submittedLayers,
            QuantityPipeAttributes previous,
            QuantityPipeAttributes startWell,
            QuantityPipeAttributes endWell,
            QuantityPipeAttributes branchDefaults,
            string changedField);

        /// <summary>
        /// 根据基础组件采集的图层元数据文本补齐污水对象业务默认值。
        /// 此方法不接触 CAD 实体，识别与计算规则由 Wastewater 所有。
        /// </summary>
        void ApplySmartDefaults(QuantityPipeAttributes attributes,
            string sourceText, bool overwrite);

        double CalculateAverageDepthForEditor(double startDepth,
            double endDepth);
        double CalculatePipeRemainingBackfillHeight(double totalDepth,
            IEnumerable<QuantityStructureLayer> layers);
        double CalculateEarthworkOut(double roadWaste, double excavation,
            double reusableOriginalSoil);

        string DefaultSettingsFilePath { get; }
        QuantityAttributeDefaults LoadDefaults();
        void SaveDefaults(QuantityAttributeDefaults defaults);
        QuantityPipeAttributes LoadDefaultForKind(string kind);
        void SaveDefaultForKind(string kind, QuantityPipeAttributes attrs);
    }

    public sealed class QuantityDependencyResult
    {
        public QuantityPipeAttributes Attributes { get; set; }
        public List<QuantityStructureLayer> Layers { get; set; }
        public List<string> Warnings { get; set; }
        public double RealExcavationDepth { get; set; }

        public QuantityDependencyResult()
        {
            Attributes = QuantityPipeAttributes.Default;
            Layers = new List<QuantityStructureLayer>();
            Warnings = new List<string>();
        }
    }

    /// <summary>
    /// 基础兼容门面与动态加载的 Wastewater 模块之间的进程内注册点。
    /// </summary>
    public static class QuantityAttributeEngineRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IQuantityAttributeEngine _current;

        public static void Register(IQuantityAttributeEngine engine)
        {
            if (engine == null) throw new ArgumentNullException("engine");
            lock (SyncRoot) _current = engine;
        }

        public static void Unregister(IQuantityAttributeEngine engine)
        {
            lock (SyncRoot)
                if (ReferenceEquals(_current, engine)) _current = null;
        }

        public static IQuantityAttributeEngine GetRequired()
        {
            lock (SyncRoot)
            {
                if (_current != null) return _current;
            }
            throw new InvalidOperationException(
                "污水工程量属性模块尚未安装或初始化。");
        }

        public static bool IsAvailable
        {
            get { lock (SyncRoot) return _current != null; }
        }
    }
}
