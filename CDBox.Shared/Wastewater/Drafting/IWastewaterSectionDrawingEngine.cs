using System;
using System.Collections.Generic;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.Modules.SectionDrawing;

namespace CDBox.Shared.Wastewater.Drafting
{
    public interface IWastewaterSectionDrawingEngine
    {
        SectionDrawingOptions LoadOptions();
        void SaveOptions(SectionDrawingOptions options);
        void Normalize(SectionDrawingOptions options);
        SectionDrawingOptions CreateScaledOptions(
            SectionDrawingOptions source);
        List<SectionBatchPlanGroup> PlanBatch(
            IEnumerable<SectionBatchSourceData> sources,
            SectionDrawingOptions baseOptions);
    }

    public sealed class SectionBatchSourceData
    {
        public string SourceId { get; set; }
        public int Order { get; set; }
        public QuantityPipeAttributes Attributes { get; set; }

        public SectionBatchSourceData()
        {
            SourceId = string.Empty;
            Attributes = QuantityPipeAttributes.Default;
        }
    }

    public sealed class SectionBatchPlanGroup
    {
        public bool IsBranch { get; set; }
        public SectionDrawingOptions Options { get; set; }
        public List<string> SourceIds { get; set; }

        public SectionBatchPlanGroup()
        {
            Options = SectionDrawingOptions.Default.Clone();
            SourceIds = new List<string>();
        }
    }

    public static class WastewaterSectionDrawingRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IWastewaterSectionDrawingEngine _current;

        public static void Register(IWastewaterSectionDrawingEngine engine)
        {
            if (engine == null) throw new ArgumentNullException("engine");
            lock (SyncRoot) _current = engine;
        }

        public static void Unregister(IWastewaterSectionDrawingEngine engine)
        {
            lock (SyncRoot)
                if (ReferenceEquals(_current, engine)) _current = null;
        }

        public static IWastewaterSectionDrawingEngine Current
        {
            get { lock (SyncRoot) return _current; }
        }

        public static IWastewaterSectionDrawingEngine GetRequired()
        {
            IWastewaterSectionDrawingEngine current = Current;
            if (current != null) return current;
            throw new InvalidOperationException(
                "污水断面模块尚未安装或初始化。");
        }

        public static bool IsAvailable
        {
            get { lock (SyncRoot) return _current != null; }
        }
    }
}
