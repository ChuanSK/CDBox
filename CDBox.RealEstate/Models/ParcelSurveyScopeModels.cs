using System.Collections.Generic;

namespace CDBox.RealEstate.Models
{
    public sealed class ParcelSurveyRegionInfo
    {
        public string RegionId { get; set; }
        public string RegionName { get; set; }
        public string CreatedAt { get; set; }
        public string Handle { get; set; }
        public bool BoundaryValid { get; set; }
        public bool OwnedBoundary { get; set; }

        public ParcelSurveyRegionInfo()
        {
            RegionId = string.Empty;
            RegionName = string.Empty;
            CreatedAt = string.Empty;
            Handle = string.Empty;
        }
    }

    public sealed class ParcelSurveyDocumentInfo
    {
        public string DocumentId { get; set; }
        public string DocumentName { get; set; }
        public bool IsActive { get; set; }

        public ParcelSurveyDocumentInfo()
        {
            DocumentId = string.Empty;
            DocumentName = string.Empty;
        }
    }

    public sealed class ParcelSurveyParcelInfo
    {
        public string RecordId { get; set; }
        public string ScopeType { get; set; }
        public string BindingKey { get; set; }
        public string ParcelId { get; set; }
        public string ParcelName { get; set; }
        public string RegionId { get; set; }
        public string RegionHandle { get; set; }
        public bool RegionOwnedBoundary { get; set; }
        public string OwnerName { get; set; }
        public string SourceObjectHandle { get; set; }
        public bool BoundaryValid { get; set; }

        public ParcelSurveyParcelInfo()
        {
            RecordId = string.Empty;
            ScopeType = "parcel";
            BindingKey = string.Empty;
            ParcelId = string.Empty;
            ParcelName = string.Empty;
            RegionId = string.Empty;
            RegionHandle = string.Empty;
            OwnerName = string.Empty;
            SourceObjectHandle = string.Empty;
        }
    }

    public sealed class ParcelSurveyScopeContext
    {
        public string DocumentId { get; set; }
        public string DocumentName { get; set; }
        public string ScopeType { get; set; }
        public string RegionId { get; set; }
        public string RegionName { get; set; }
        public string ParcelId { get; set; }
        public string ParcelName { get; set; }
        public string RecordId { get; set; }
        public bool BindingValid { get; set; }
        public IList<ParcelSurveyDocumentInfo> Documents { get; set; }
        public IList<ParcelSurveyRegionInfo> Regions { get; set; }
        public IList<ParcelSurveyParcelInfo> Parcels { get; set; }

        public ParcelSurveyScopeContext()
        {
            DocumentId = string.Empty;
            DocumentName = string.Empty;
            ScopeType = "whole";
            RegionId = string.Empty;
            RegionName = "整张图纸";
            ParcelId = string.Empty;
            ParcelName = string.Empty;
            RecordId = string.Empty;
            Documents = new List<ParcelSurveyDocumentInfo>();
            Regions = new List<ParcelSurveyRegionInfo>();
            Parcels = new List<ParcelSurveyParcelInfo>();
        }
    }

    public sealed class ParcelSurveyScopeActionRequest
    {
        public ParcelSurveyRecord Record { get; set; }
        public string DocumentId { get; set; }
        public string ScopeType { get; set; }
        public string RegionId { get; set; }
        public string RegionName { get; set; }
        public string ParcelId { get; set; }
        public string ParcelName { get; set; }
        public string RecordId { get; set; }
        public string ScopeToken { get; set; }

        public ParcelSurveyScopeActionRequest()
        {
            DocumentId = string.Empty;
            ScopeType = "whole";
            RegionId = string.Empty;
            RegionName = string.Empty;
            ParcelId = string.Empty;
            ParcelName = string.Empty;
            RecordId = string.Empty;
            ScopeToken = string.Empty;
        }
    }
}
