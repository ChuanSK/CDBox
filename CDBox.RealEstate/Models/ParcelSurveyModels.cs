using System;
using System.Collections.Generic;
using System.Globalization;

namespace CDBox.RealEstate.Models
{
    public enum ParcelFieldStatus
    {
        Automatic = 0,
        Default = 1,
        Imported = 2,
        Manual = 3,
        NotApplicable = 4
    }

    public sealed class ParcelSurveyFieldValue
    {
        public string TextValue { get; set; }
        public decimal? NumericValue { get; set; }
        public bool BooleanValue { get; set; }
        public List<string> Selections { get; set; }
        public ParcelFieldStatus Status { get; set; }
        public bool Confirmed { get; set; }

        public ParcelSurveyFieldValue()
        {
            TextValue = string.Empty;
            Selections = new List<string>();
            Status = ParcelFieldStatus.Manual;
        }

        public void Normalize()
        {
            TextValue = TextValue ?? string.Empty;
            Selections = Selections ?? new List<string>();
            if (!Enum.IsDefined(typeof(ParcelFieldStatus), Status))
                Status = ParcelFieldStatus.Manual;
            if (Status == ParcelFieldStatus.NotApplicable)
            {
                TextValue = "/";
                NumericValue = null;
                BooleanValue = false;
                Selections.Clear();
                Confirmed = true;
            }
            else if (TextValue == "/")
            {
                TextValue = string.Empty;
            }
        }

        public bool HasValue(string kind)
        {
            Normalize();
            if (Status == ParcelFieldStatus.NotApplicable) return true;
            if (string.Equals(kind, "number", StringComparison.OrdinalIgnoreCase))
                return NumericValue.HasValue;
            if (string.Equals(kind, "checkbox", StringComparison.OrdinalIgnoreCase))
                return BooleanValue;
            if (string.Equals(kind, "multiselect", StringComparison.OrdinalIgnoreCase))
                return Selections.Count > 0;
            return !string.IsNullOrWhiteSpace(TextValue);
        }

        public string DisplayValue(string kind)
        {
            Normalize();
            if (Status == ParcelFieldStatus.NotApplicable) return "/";
            if (string.Equals(kind, "number", StringComparison.OrdinalIgnoreCase))
                return NumericValue.HasValue
                    ? NumericValue.Value.ToString("0.##", CultureInfo.InvariantCulture)
                    : string.Empty;
            if (string.Equals(kind, "checkbox", StringComparison.OrdinalIgnoreCase))
                return BooleanValue ? "是" : "否";
            if (string.Equals(kind, "multiselect", StringComparison.OrdinalIgnoreCase))
                return string.Join("、", Selections);
            return TextValue;
        }
    }

    public sealed class ParcelBoundaryPointRecord
    {
        public int Sequence { get; set; }
        public string PointNumber { get; set; }
        public decimal? X { get; set; }
        public decimal? Y { get; set; }
        public decimal? DistanceToNext { get; set; }
        public string MarkerType { get; set; }
        public string Description { get; set; }
        public bool Confirmed { get; set; }
        public ParcelFieldStatus Status { get; set; }

        public void Normalize(int sequence)
        {
            Sequence = sequence;
            PointNumber = PointNumber ?? string.Empty;
            MarkerType = string.IsNullOrWhiteSpace(MarkerType)
                ? "喷涂" : MarkerType;
            Description = Description ?? string.Empty;
            if (!Enum.IsDefined(typeof(ParcelFieldStatus), Status))
                Status = ParcelFieldStatus.Automatic;
        }
    }

    public sealed class ParcelBoundarySegmentRecord
    {
        public string StartPointNumber { get; set; }
        public string MiddlePointNumbers { get; set; }
        public string EndPointNumber { get; set; }
        public decimal? Distance { get; set; }
        public string LineCategory { get; set; }
        public string LinePosition { get; set; }
        public string NeighborParcelCode { get; set; }
        public string NeighborOwner { get; set; }
        public string Direction { get; set; }
        public string Description { get; set; }
        public bool Confirmed { get; set; }
        public bool NeighborHandled { get; set; }
        public ParcelFieldStatus Status { get; set; }

        public void Normalize()
        {
            StartPointNumber = StartPointNumber ?? string.Empty;
            MiddlePointNumbers = MiddlePointNumbers ?? string.Empty;
            EndPointNumber = EndPointNumber ?? string.Empty;
            LineCategory = LineCategory ?? string.Empty;
            LinePosition = LinePosition ?? string.Empty;
            NeighborParcelCode = NeighborParcelCode ?? string.Empty;
            NeighborOwner = NeighborOwner ?? string.Empty;
            Direction = Direction ?? string.Empty;
            Description = Description ?? string.Empty;
            if (!Enum.IsDefined(typeof(ParcelFieldStatus), Status))
                Status = ParcelFieldStatus.Automatic;
        }
    }

    public sealed class ParcelBoundarySignatureGroupRecord
    {
        public string StartPointNumber { get; set; }
        public string MiddlePointNumbers { get; set; }
        public string EndPointNumber { get; set; }
        public string NeighborOwner { get; set; }
        public string NeighborParcelCode { get; set; }
        public string NeighborRepresentative { get; set; }
        public string ParcelRepresentative { get; set; }
        public string ConfirmationDate { get; set; }
        public string SignatureStatus { get; set; }
        public bool PreservePaperSignatureBlank { get; set; }
        public bool Confirmed { get; set; }
        public ParcelFieldStatus Status { get; set; }

        public void Normalize()
        {
            StartPointNumber = StartPointNumber ?? string.Empty;
            MiddlePointNumbers = ParcelBoundaryPointNumberFormatter
                .FormatSignatureMiddle(MiddlePointNumbers);
            EndPointNumber = EndPointNumber ?? string.Empty;
            NeighborOwner = NeighborOwner ?? string.Empty;
            NeighborParcelCode = NeighborParcelCode ?? string.Empty;
            NeighborRepresentative = NeighborRepresentative ?? string.Empty;
            ParcelRepresentative = ParcelRepresentative ?? string.Empty;
            ConfirmationDate = ConfirmationDate ?? string.Empty;
            SignatureStatus = SignatureStatus ?? string.Empty;
            if (!Enum.IsDefined(typeof(ParcelFieldStatus), Status))
                Status = ParcelFieldStatus.Automatic;
        }
    }

    public sealed class ParcelBoundaryData
    {
        public bool ParcelBoundaryClosed { get; set; }
        public string SourceObjectHandle { get; set; }
        public string SourceLayerName { get; set; }
        public bool SourceClockwise { get; set; }
        public decimal? SourceArea { get; set; }
        public List<ParcelBoundaryPointRecord> Points { get; set; }
        public List<ParcelBoundarySegmentRecord> Segments { get; set; }
        public List<ParcelBoundarySignatureGroupRecord> SignatureGroups { get; set; }

        public ParcelBoundaryData()
        {
            Points = new List<ParcelBoundaryPointRecord>();
            Segments = new List<ParcelBoundarySegmentRecord>();
            SignatureGroups = new List<ParcelBoundarySignatureGroupRecord>();
        }

        public void Normalize()
        {
            SourceObjectHandle = SourceObjectHandle ?? string.Empty;
            SourceLayerName = SourceLayerName ?? string.Empty;
            Points = Points ?? new List<ParcelBoundaryPointRecord>();
            Segments = Segments ?? new List<ParcelBoundarySegmentRecord>();
            SignatureGroups = SignatureGroups ??
                new List<ParcelBoundarySignatureGroupRecord>();
            for (int i = 0; i < Points.Count; i++)
            {
                if (Points[i] == null) Points[i] = new ParcelBoundaryPointRecord();
                Points[i].Normalize(i + 1);
            }
            foreach (ParcelBoundarySegmentRecord segment in Segments)
                if (segment != null) segment.Normalize();
            foreach (ParcelBoundarySignatureGroupRecord group in SignatureGroups)
                if (group != null) group.Normalize();
            Segments.RemoveAll(x => x == null);
            SignatureGroups.RemoveAll(x => x == null);
        }
    }

    public sealed class ParcelBuildingRecord
    {
        public string Id { get; set; }
        public Dictionary<string, ParcelSurveyFieldValue> Fields { get; set; }

        public ParcelBuildingRecord()
        {
            Id = Guid.NewGuid().ToString("N");
            Fields = new Dictionary<string, ParcelSurveyFieldValue>(
                StringComparer.OrdinalIgnoreCase);
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(Id)) Id = Guid.NewGuid().ToString("N");
            Fields = Fields ?? new Dictionary<string, ParcelSurveyFieldValue>(
                StringComparer.OrdinalIgnoreCase);
            foreach (ParcelSurveyFieldDefinition definition in
                ParcelSurveyFieldCatalog.BuildingFields)
            {
                ParcelSurveyFieldValue value;
                if (!Fields.TryGetValue(definition.Key, out value) || value == null)
                {
                    value = new ParcelSurveyFieldValue
                    {
                        Status = definition.DefaultStatus
                    };
                    Fields[definition.Key] = value;
                }
                value.Normalize();
            }
        }
    }

    public sealed class ParcelLayoutDiagnostics
    {
        public int TextOverflowCount { get; set; }
        public int AbnormalPaginationCount { get; set; }
        public bool FooterOrphanRisk { get; set; }
    }

    public sealed class ParcelSurveyRecord
    {
        public string Id { get; set; }
        public string UpdatedAtUtc { get; set; }
        public string DocumentId { get; set; }
        public string DocumentName { get; set; }
        public string ScopeType { get; set; }
        public string RegionId { get; set; }
        public string RegionName { get; set; }
        public string ParcelId { get; set; }
        public string ParcelName { get; set; }
        public Dictionary<string, ParcelSurveyFieldValue> Fields { get; set; }
        public ParcelBoundaryData Boundary { get; set; }
        public List<ParcelBuildingRecord> Buildings { get; set; }
        public ParcelLayoutDiagnostics LayoutDiagnostics { get; set; }

        public ParcelSurveyRecord()
        {
            Id = Guid.NewGuid().ToString("N");
            ScopeType = "whole";
            Fields = new Dictionary<string, ParcelSurveyFieldValue>(
                StringComparer.OrdinalIgnoreCase);
            Boundary = new ParcelBoundaryData();
            Buildings = new List<ParcelBuildingRecord>();
            LayoutDiagnostics = new ParcelLayoutDiagnostics();
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(Id)) Id = Guid.NewGuid().ToString("N");
            UpdatedAtUtc = UpdatedAtUtc ?? string.Empty;
            DocumentId = DocumentId ?? string.Empty;
            DocumentName = DocumentName ?? string.Empty;
            ScopeType = string.Equals(ScopeType, "parcel",
                StringComparison.OrdinalIgnoreCase) ? "parcel"
                : string.Equals(ScopeType, "region",
                    StringComparison.OrdinalIgnoreCase) ? "region" : "whole";
            RegionId = ScopeType == "region" ? RegionId ?? string.Empty
                : string.Empty;
            RegionName = ScopeType == "region" ? RegionName ?? string.Empty
                : string.Empty;
            ParcelId = ScopeType == "parcel" ? ParcelId ?? string.Empty
                : string.Empty;
            ParcelName = ScopeType == "parcel" || ScopeType == "region"
                ? ParcelName ?? string.Empty : string.Empty;
            Fields = Fields ?? new Dictionary<string, ParcelSurveyFieldValue>(
                StringComparer.OrdinalIgnoreCase);
            foreach (ParcelSurveyFieldDefinition definition in
                ParcelSurveyFieldCatalog.Fields)
            {
                ParcelSurveyFieldValue value;
                if (!Fields.TryGetValue(definition.Key, out value) || value == null)
                {
                    value = new ParcelSurveyFieldValue
                    {
                        Status = definition.DefaultStatus
                    };
                    Fields[definition.Key] = value;
                }
                value.Normalize();
            }
            Boundary = Boundary ?? new ParcelBoundaryData();
            Boundary.Normalize();
            Buildings = Buildings ?? new List<ParcelBuildingRecord>();
            Buildings.RemoveAll(x => x == null);
            foreach (ParcelBuildingRecord building in Buildings) building.Normalize();
            LayoutDiagnostics = LayoutDiagnostics ?? new ParcelLayoutDiagnostics();
        }

        public ParcelSurveyFieldValue Field(string key)
        {
            Normalize();
            ParcelSurveyFieldValue value;
            return Fields.TryGetValue(key, out value) ? value : null;
        }
    }

    public sealed class ParcelSurveyRepositoryState
    {
        public string CurrentRecordId { get; set; }
        public Dictionary<string, ParcelSurveyFieldValue> ProjectDefaults { get; set; }
        public Dictionary<string, string> CurrentScopeTypeByDocument { get; set; }
        public Dictionary<string, string> CurrentRegionByDocument { get; set; }
        public Dictionary<string, string> CurrentParcelByDocument { get; set; }
        public List<ParcelSurveyRecord> Records { get; set; }

        public ParcelSurveyRepositoryState()
        {
            ProjectDefaults = new Dictionary<string, ParcelSurveyFieldValue>(
                StringComparer.OrdinalIgnoreCase);
            CurrentScopeTypeByDocument = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            CurrentRegionByDocument = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            CurrentParcelByDocument = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            Records = new List<ParcelSurveyRecord>();
        }
    }
}
