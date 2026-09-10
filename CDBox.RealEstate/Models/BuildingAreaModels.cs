using System;
using System.Collections.Generic;

namespace CDBox.RealEstate.Models
{
    public enum BuildingAreaRole { Full = 0, Hollow = 1, HalfEnclosed = 2 }

    public sealed class BuildingAreaPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public sealed class BuildingAreaTerm
    {
        public int Sequence { get; set; }
        public string Method { get; set; }
        public List<BuildingAreaPoint> Vertices { get; set; } = new List<BuildingAreaPoint>();
        public BuildingAreaPoint BaseStart { get; set; }
        public BuildingAreaPoint BaseEnd { get; set; }
        public BuildingAreaPoint Apex { get; set; }
        public BuildingAreaPoint HeightFoot { get; set; }
        public decimal BaseLength { get; set; }
        public decimal Height { get; set; }
        public decimal Area { get; set; }
        public double GeometryArea { get; set; }
        public string Formula { get; set; }
        public decimal Radius { get; set; }
        public decimal AngleDegrees { get; set; }
        public decimal Pi { get; set; } = 3.141592653589793m;
    }

    public sealed class BuildingAreaComponent
    {
        public string SourceHandle { get; set; }
        public string LayerName { get; set; }
        public BuildingAreaRole Role { get; set; }
        public decimal Factor { get; set; }
        public decimal Area { get; set; }
        public decimal Contribution { get; set; }
        public double GeometryArea { get; set; }
        public string Formula { get; set; }
        public List<BuildingAreaTerm> Terms { get; set; } = new List<BuildingAreaTerm>();
        public List<BuildingAreaBoundaryEdge> Boundary { get; set; } = new List<BuildingAreaBoundaryEdge>();
    }

    public sealed class BuildingAreaCalculation
    {
        public int SchemaVersion { get; set; } = 1;
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
        public string DocumentId { get; set; }
        public int LengthDecimalPlaces { get; set; } = 2;
        public int AreaDecimalPlaces { get; set; } = 2;
        public List<BuildingAreaComponent> Components { get; set; } = new List<BuildingAreaComponent>();
        public decimal FullArea { get; set; }
        public decimal HollowArea { get; set; }
        public decimal HalfEnclosedArea { get; set; }
        public decimal Area { get; set; }
        public string Formula { get; set; }
        public List<string> ExcludedHollowHandles { get; set; } = new List<string>();
    }

    public sealed class BuildingAreaBoundaryEdge
    {
        public BuildingAreaPoint Start { get; set; }
        public BuildingAreaPoint End { get; set; }
        public double Bulge { get; set; }
    }

    public sealed class BuildingFloorArea
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; }
        public int Count { get; set; } = 1;
        public bool? IsGroundFloor { get; set; }
        public int AddedRevision { get; set; }
        public BuildingAreaCalculation Calculation { get; set; }
    }

    public sealed class BuildingCaptureTarget
    {
        public string RecordId { get; set; }
        public string BuildingId { get; set; }
        public string BuildingNumber { get; set; }
    }

    public sealed class BuildingCaptureInput
    {
        public string RecordId { get; set; }
        public string TargetBuildingId { get; set; }
        public string BuildingNumber { get; set; }
        public string HouseholdNumber { get; set; }
        public string Floor { get; set; }
        public string TotalFloors { get; set; }
        public string Structure { get; set; }
        public int FloorCount { get; set; } = 1;
        public bool? IsGroundFloor { get; set; }
    }

    public sealed class BuildingCaptureContext
    {
        public string DocumentName { get; set; }
        public string SelectedRecordId { get; set; }
        public IList<ParcelSurveyParcelInfo> Parcels { get; set; }
        public BuildingAreaCalculation Calculation { get; set; }
        public IList<BuildingCaptureTarget> Buildings { get; set; } = new List<BuildingCaptureTarget>();
    }
}
