using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.Core.Check
{
    public enum DrawingCheckSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public enum DrawingCheckIssueStatus
    {
        Active,
        Ignored,
        Resolved
    }

    public sealed class DrawingCheckIssue
    {
        public DrawingCheckIssue()
        {
            Id = Guid.NewGuid().ToString("N");
            DocumentId = string.Empty;
            RuleId = string.Empty;
            Category = string.Empty;
            Title = string.Empty;
            Message = string.Empty;
            Description = string.Empty;
            ObjectHandles = new List<string>();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
            Status = DrawingCheckIssueStatus.Active;
            IgnoreKey = string.Empty;
        }

        public string Id { get; set; }
        public string DocumentId { get; set; }
        public string RuleId { get; set; }
        public string Category { get; set; }
        public DrawingCheckSeverity Severity { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Description { get; set; }
        public List<string> ObjectHandles { get; set; }
        public bool CanFix { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DrawingCheckIssueStatus Status { get; set; }
        public string IgnoreKey { get; set; }

        public DrawingCheckIssue Clone()
        {
            return new DrawingCheckIssue
            {
                Id = Id,
                DocumentId = DocumentId,
                RuleId = RuleId,
                Category = Category,
                Severity = Severity,
                Title = Title,
                Message = Message,
                Description = Description,
                ObjectHandles = ObjectHandles == null
                    ? new List<string>() : new List<string>(ObjectHandles),
                CanFix = CanFix,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt,
                Status = Status,
                IgnoreKey = IgnoreKey
            };
        }
    }

    public sealed class DrawingCheckGroup
    {
        public DrawingCheckGroup()
        {
            Id = string.Empty;
            DocumentId = string.Empty;
            RuleId = string.Empty;
            Category = string.Empty;
            Title = string.Empty;
            Summary = string.Empty;
            ObjectHandles = new List<string>();
            IssueIds = new List<string>();
            Status = DrawingCheckIssueStatus.Active;
        }

        public string Id { get; set; }
        public string DocumentId { get; set; }
        public string RuleId { get; set; }
        public string Category { get; set; }
        public DrawingCheckSeverity Severity { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public int IssueCount { get; set; }
        public int ObjectCount { get; set; }
        public List<string> ObjectHandles { get; set; }
        public List<string> IssueIds { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DrawingCheckIssueStatus Status { get; set; }

        public DrawingCheckGroup Clone()
        {
            return new DrawingCheckGroup
            {
                Id = Id,
                DocumentId = DocumentId,
                RuleId = RuleId,
                Category = Category,
                Severity = Severity,
                Title = Title,
                Summary = Summary,
                IssueCount = IssueCount,
                ObjectCount = ObjectCount,
                ObjectHandles = ObjectHandles == null
                    ? new List<string>() : new List<string>(ObjectHandles),
                IssueIds = IssueIds == null
                    ? new List<string>() : new List<string>(IssueIds),
                UpdatedAt = UpdatedAt,
                Status = Status
            };
        }
    }

    public sealed class DrawingCheckSnapshot
    {
        public DrawingCheckSnapshot()
        {
            DocumentId = string.Empty;
            Groups = new List<DrawingCheckGroup>();
            IgnoredGroups = new List<DrawingCheckGroup>();
            UpdatedAt = DateTime.UtcNow;
        }

        public string DocumentId { get; set; }
        public bool IsRunning { get; set; }
        public double? Progress { get; set; }
        public string Stage { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<DrawingCheckGroup> Groups { get; set; }
        public List<DrawingCheckGroup> IgnoredGroups { get; set; }

        public int WarningCount
        {
            get { return Groups.Count(x => x.Severity == DrawingCheckSeverity.Warning); }
        }

        public int ErrorCount
        {
            get
            {
                return Groups.Count(x => x.Severity == DrawingCheckSeverity.Error
                    || x.Severity == DrawingCheckSeverity.Critical);
            }
        }

        public DrawingCheckSnapshot Clone()
        {
            return new DrawingCheckSnapshot
            {
                DocumentId = DocumentId,
                IsRunning = IsRunning,
                Progress = Progress,
                Stage = Stage,
                UpdatedAt = UpdatedAt,
                Groups = Groups == null ? new List<DrawingCheckGroup>()
                    : Groups.Where(x => x != null).Select(x => x.Clone()).ToList(),
                IgnoredGroups = IgnoredGroups == null
                    ? new List<DrawingCheckGroup>() : IgnoredGroups
                        .Where(x => x != null).Select(x => x.Clone()).ToList()
            };
        }
    }

    public sealed class DrawingCheckObjectSnapshot
    {
        public DrawingCheckObjectSnapshot()
        {
            Handle = string.Empty;
            LayerName = string.Empty;
            EntityType = string.Empty;
            LayerParentGroup = string.Empty;
            LayerParentClass = string.Empty;
            ObjectKind = string.Empty;
            Material = string.Empty;
            Diameter = string.Empty;
            StartNode = string.Empty;
            EndNode = string.Empty;
            BackfillStructure = string.Empty;
            BranchType = string.Empty;
            NodeNo = string.Empty;
            WellSpec = string.Empty;
            WellCoverMaterial = string.Empty;
            WellMaterialType = string.Empty;
            WellType = string.Empty;
        }

        public string Handle { get; set; }
        public string LayerName { get; set; }
        public string EntityType { get; set; }
        public string LayerParentGroup { get; set; }
        public string LayerParentClass { get; set; }
        public string ObjectKind { get; set; }
        public bool HasSavedAttributes { get; set; }
        public bool IsSpecialObject { get; set; }
        public string Material { get; set; }
        public string Diameter { get; set; }
        public string StartNode { get; set; }
        public string EndNode { get; set; }
        public double AverageDepth { get; set; }
        public string BackfillStructure { get; set; }
        public double PipeOuterDiameter { get; set; }
        public bool PipeLayerBelowDiameter { get; set; }
        public string BranchType { get; set; }
        public bool BranchIncludeInCalculation { get; set; }
        public double BranchDepth { get; set; }
        public string NodeNo { get; set; }
        public string WellSpec { get; set; }
        public string WellCoverMaterial { get; set; }
        public string WellMaterialType { get; set; }
        public string WellType { get; set; }
        public double WellDepth { get; set; }
        public bool HasStartPoint { get; set; }
        public double StartX { get; set; }
        public double StartY { get; set; }
        public bool HasEndPoint { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public bool HasNodePosition { get; set; }
        public double NodeX { get; set; }
        public double NodeY { get; set; }
        public double NodeConnectionTolerance { get; set; }
    }

    public sealed class DrawingCheckLayerSnapshot
    {
        public DrawingCheckLayerSnapshot()
        {
            LayerName = string.Empty;
            ParentGroup = string.Empty;
            ParentClass = string.Empty;
            ObjectHandles = new List<string>();
        }

        public string LayerName { get; set; }
        public string ParentGroup { get; set; }
        public string ParentClass { get; set; }
        public List<string> ObjectHandles { get; set; }
    }

    public sealed class DrawingCheckAnnotationSnapshot
    {
        public DrawingCheckAnnotationSnapshot()
        {
            AnnotationId = string.Empty;
            AnnotationHandle = string.Empty;
            SourceHandle = string.Empty;
            BindingState = string.Empty;
        }

        public string AnnotationId { get; set; }
        public string AnnotationHandle { get; set; }
        public string SourceHandle { get; set; }
        public string BindingState { get; set; }
        public bool SourceExists { get; set; }
    }
}
