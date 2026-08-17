using System.Collections.Generic;

namespace CDBox.RealEstate.Geometry
{
    public sealed class BuildingPlannedSegment
    {
        public BuildingPoint2 Start { get; internal set; }
        public BuildingPoint2 End { get; internal set; }
        public BuildingPoint2 TextPosition { get; internal set; }
        public double Length { get; internal set; }
        public double TextRotation { get; internal set; }
        public string Text { get; internal set; }
        public bool IsAuxiliary { get; internal set; }
    }

    public sealed class BuildingAnnotationPlan
    {
        internal BuildingAnnotationPlan()
        {
            BoundarySegments = new List<BuildingPlannedSegment>();
            AuxiliarySegments = new List<BuildingPlannedSegment>();
            Warning = string.Empty;
        }

        public IList<BuildingPlannedSegment> BoundarySegments { get; private set; }
        public IList<BuildingPlannedSegment> AuxiliarySegments { get; private set; }
        public double TextHeight { get; internal set; }
        public bool IsOrthogonal { get; internal set; }
        public string Warning { get; internal set; }
    }
}
