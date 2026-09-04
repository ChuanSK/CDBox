using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.Modules.LongitudinalProfile
{
    public sealed class LongitudinalProfilePipeData
    {
        public string SourceId { get; set; }
        public string StartNode { get; set; }
        public string EndNode { get; set; }
        public string Diameter { get; set; }
        public string Foundation { get; set; }
        public double OuterDiameter { get; set; }
        public double PlanLength { get; set; }
        public int SelectionOrder { get; set; }
        public double StartDepth { get; set; }
        public double EndDepth { get; set; }
        public double StartInvertElevation { get; set; }
        public double EndInvertElevation { get; set; }
        public bool HasGeometry { get; set; }
        public double GeometryStartX { get; set; }
        public double GeometryStartY { get; set; }
        public double GeometryEndX { get; set; }
        public double GeometryEndY { get; set; }
    }

    public sealed class LongitudinalProfileWellData
    {
        public string SourceId { get; set; }
        public string NodeNo { get; set; }
        public string WellSpec { get; set; }
        public string WellType { get; set; }
        public double GroundElevation { get; set; }
        public double WellDepth { get; set; }
        public double SiltWellDeductDepth500 { get; set; }
        public double SiltWellDeductDepth700 { get; set; }
        public bool HasPosition { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
    }

    public sealed class LongitudinalProfileNodeData
    {
        public string SourceId { get; set; }
        public string NodeNo { get; set; }
        public double GroundElevation { get; set; }
        public double DesignInvertElevation { get; set; }
        public double PipeBottomDepth { get; set; }
        public double WellDepth { get; set; }
        public double SiltWellAdjustment { get; set; }
        public double CumulativeDistance { get; set; }
        public bool HasPosition { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
    }

    public sealed class LongitudinalProfileSpanData
    {
        public string SourceId { get; set; }
        public string StartNode { get; set; }
        public string EndNode { get; set; }
        public string Diameter { get; set; }
        public string Foundation { get; set; }
        public double OuterDiameter { get; set; }
        public double PlanLength { get; set; }
        public double StartInvertElevation { get; set; }
        public double EndInvertElevation { get; set; }
        public double SlopePermille { get; set; }
        public double SlopePercent { get { return SlopePermille / 10.0; } }
    }

    public sealed class LongitudinalProfileConnectionData
    {
        public string SourceId { get; set; }
        public string NodeNo { get; set; }
        public string Diameter { get; set; }
        public double OuterDiameter { get; set; }
        public double InvertElevation { get; set; }
        public string Side { get; set; }
    }

    public sealed class LongitudinalProfileBoundaryExtensionData
    {
        public string SourceId { get; set; }
        public string NodeNo { get; set; }
        public string Diameter { get; set; }
        public double OuterDiameter { get; set; }
        public double BoundaryInvertElevation { get; set; }
        public double OutsideInvertElevation { get; set; }
        public double PlanLength { get; set; }
        public bool AtStart { get; set; }
    }

    public sealed class LongitudinalProfileData
    {
        public List<LongitudinalProfileNodeData> Nodes { get; set; }
        public List<LongitudinalProfileSpanData> Spans { get; set; }
        public List<LongitudinalProfileConnectionData> Connections { get; set; }
        public LongitudinalProfileBoundaryExtensionData StartExtension
        { get; set; }
        public LongitudinalProfileBoundaryExtensionData EndExtension
        { get; set; }

        public LongitudinalProfileData()
        {
            Nodes = new List<LongitudinalProfileNodeData>();
            Spans = new List<LongitudinalProfileSpanData>();
            Connections = new List<LongitudinalProfileConnectionData>();
        }
    }

    public sealed class LongitudinalProfileBuildResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public LongitudinalProfileData Profile { get; set; }

        public LongitudinalProfileBuildResult()
        {
            Message = string.Empty;
            Profile = new LongitudinalProfileData();
        }
    }

    /// <summary>
    /// 旧调用点兼容门面。纵断面拓扑、高程和坡度计算由动态加载的
    /// Wastewater 模块执行。
    /// </summary>
    public static class LongitudinalProfileCalculator
    {
        public static LongitudinalProfileBuildResult Build(
            IEnumerable<LongitudinalProfilePipeData> pipes,
            IEnumerable<LongitudinalProfileWellData> wells)
        {
            return CDBox.Shared.Wastewater.Drafting
                .WastewaterLongitudinalProfileRegistry.GetRequired()
                .Build(pipes, wells);
        }

        public static LongitudinalProfileBuildResult BuildBetweenNodes(
            IEnumerable<LongitudinalProfilePipeData> pipes,
            IEnumerable<LongitudinalProfileWellData> wells,
            string startNode, string endNode)
        {
            return CDBox.Shared.Wastewater.Drafting
                .WastewaterLongitudinalProfileRegistry.GetRequired()
                .BuildBetweenNodes(pipes, wells, startNode, endNode);
        }
    }
}
