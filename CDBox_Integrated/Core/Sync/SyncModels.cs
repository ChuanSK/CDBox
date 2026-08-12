using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.Core.Sync
{
    public enum SyncTaskType
    {
        Property,
        Calculation,
        Annotation,
        Relation,
        Display
    }

    public enum SyncTaskState
    {
        Clean,
        Dirty,
        Running,
        Completed,
        Conflict,
        Error,
        Ignored
    }

    public enum SyncRiskLevel
    {
        Safe,
        ConfirmationRequired
    }

    public sealed class SyncTask
    {
        public SyncTask()
        {
            Id = Guid.NewGuid().ToString("N");
            DocumentId = string.Empty;
            MergeKey = string.Empty;
            Source = string.Empty;
            Title = string.Empty;
            Summary = string.Empty;
            Reason = string.Empty;
            ObjectHandles = new List<string>();
            ChangedProperties = new List<string>();
            State = SyncTaskState.Dirty;
            Risk = SyncRiskLevel.Safe;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
        }

        public string Id { get; set; }
        public string DocumentId { get; set; }
        public string MergeKey { get; set; }
        public string Source { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Reason { get; set; }
        public SyncTaskType Type { get; set; }
        public SyncTaskState State { get; set; }
        public SyncRiskLevel Risk { get; set; }
        public List<string> ObjectHandles { get; set; }
        public List<string> ChangedProperties { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string ResultMessage { get; set; }

        public int AffectedObjectCount
        {
            get { return ObjectHandles == null ? 0 : ObjectHandles.Count; }
        }

        public SyncTask Clone()
        {
            return new SyncTask
            {
                Id = Id,
                DocumentId = DocumentId,
                MergeKey = MergeKey,
                Source = Source,
                Title = Title,
                Summary = Summary,
                Reason = Reason,
                Type = Type,
                State = State,
                Risk = Risk,
                ObjectHandles = ObjectHandles == null
                    ? new List<string>() : ObjectHandles.ToList(),
                ChangedProperties = ChangedProperties == null
                    ? new List<string>() : ChangedProperties.ToList(),
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt,
                CompletedAt = CompletedAt,
                ResultMessage = ResultMessage
            };
        }
    }

    public sealed class SyncSnapshot
    {
        public SyncSnapshot()
        {
            Tasks = new List<SyncTask>();
            History = new List<SyncTask>();
        }

        public string DocumentId { get; set; }
        public List<SyncTask> Tasks { get; set; }
        public List<SyncTask> History { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class SyncChangedEventArgs : EventArgs
    {
        public SyncChangedEventArgs(string documentId)
        {
            DocumentId = documentId ?? string.Empty;
        }

        public string DocumentId { get; private set; }
    }
}
