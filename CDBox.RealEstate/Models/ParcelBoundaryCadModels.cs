using System.Collections.Generic;

namespace CDBox.RealEstate.Models
{
    public sealed class ParcelBoundaryCadVertex
    {
        public int SourceIndex { get; set; }
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Z { get; set; }
        public decimal DistanceToNext { get; set; }
    }

    public sealed class ParcelBoundaryCadSelection
    {
        public string SourceObjectHandle { get; set; }
        public string SourceLayerName { get; set; }
        public bool NativeClockwise { get; set; }
        public decimal Area { get; set; }
        public int SelectedStartSourceIndex { get; set; }
        public string StartPointPrefix { get; set; }
        public int StartPointNumber { get; set; }
        public bool ConfiguredClockwise { get; set; }
        public string OwnerName { get; set; }
        public List<ParcelBoundaryCadVertex> Vertices { get; set; }

        public ParcelBoundaryCadSelection()
        {
            SourceObjectHandle = string.Empty;
            SourceLayerName = string.Empty;
            StartPointPrefix = "J";
            StartPointNumber = 1;
            OwnerName = string.Empty;
            Vertices = new List<ParcelBoundaryCadVertex>();
        }
    }

    public sealed class ParcelBoundaryRangeSelection
    {
        public string StartPointNumber { get; set; }
        public string MiddlePointNumbers { get; set; }
        public string EndPointNumber { get; set; }
        public decimal Distance { get; set; }
        public string Direction { get; set; }

        public ParcelBoundaryRangeSelection()
        {
            StartPointNumber = string.Empty;
            MiddlePointNumbers = string.Empty;
            EndPointNumber = string.Empty;
            Direction = string.Empty;
        }
    }

    public sealed class ParcelBoundRangeSelection
    {
        public string RecordId { get; set; }
        public string ParcelName { get; set; }
        public ParcelBoundaryRangeSelection Range { get; set; }

        public ParcelBoundRangeSelection()
        {
            RecordId = string.Empty;
            ParcelName = string.Empty;
            Range = new ParcelBoundaryRangeSelection();
        }
    }
}
