using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.Core.FloatingCenter
{
    public enum FloatingMessageKind
    {
        Information,
        Success,
        Prompt,
        Progress,
        Warning,
        Error,
        Decision,
        Sync,
        Check
    }

    public enum FloatingMessagePriority
    {
        Unspecified = 0,
        Low = 1,
        Normal = 2,
        High = 3,
        Critical = 4
    }

    public enum FloatingHealthState
    {
        Normal,
        Pending,
        Warning,
        Critical
    }

    public enum FloatingActivityState
    {
        Idle,
        Working,
        WaitingForCadInput,
        WaitingForUserDecision,
        Paused
    }

    public sealed class FloatingAction
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsDestructive { get; set; }

        public FloatingAction Clone()
        {
            return new FloatingAction
            {
                Id = Id,
                Text = Text,
                IsPrimary = IsPrimary,
                IsDestructive = IsDestructive
            };
        }
    }

    public sealed class FloatingMessage
    {
        public FloatingMessage()
        {
            Actions = new List<FloatingAction>();
            CreatedAt = DateTime.UtcNow;
            RepeatCount = 1;
            RecordInHistory = true;
        }

        public string Id { get; set; }
        public string DocumentId { get; set; }
        public string Source { get; set; }
        public FloatingMessageKind Kind { get; set; }
        public FloatingMessagePriority Priority { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Detail { get; set; }
        public double? Progress { get; set; }
        public bool IsIndeterminate { get; set; }
        public bool IsPersistent { get; set; }
        public bool RequiresDecision { get; set; }
        public bool RecordInHistory { get; set; }
        public bool PresentAsCard { get; set; }
        public IList<FloatingAction> Actions { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string MergeKey { get; set; }
        public string CorrelationId { get; set; }
        public int RepeatCount { get; set; }

        public FloatingMessage Clone()
        {
            var clone = new FloatingMessage
            {
                Id = Id,
                DocumentId = DocumentId,
                Source = Source,
                Kind = Kind,
                Priority = Priority,
                Title = Title,
                Summary = Summary,
                Detail = Detail,
                Progress = Progress,
                IsIndeterminate = IsIndeterminate,
                IsPersistent = IsPersistent,
                RequiresDecision = RequiresDecision,
                RecordInHistory = RecordInHistory,
                PresentAsCard = PresentAsCard,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt,
                MergeKey = MergeKey,
                CorrelationId = CorrelationId,
                RepeatCount = RepeatCount
            };
            clone.Actions.Clear();
            if (Actions != null)
            {
                foreach (FloatingAction action in Actions)
                    if (action != null) clone.Actions.Add(action.Clone());
            }
            return clone;
        }
    }

    public sealed class FloatingPrompt
    {
        public string DocumentId { get; set; }
        public string Source { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string CorrelationId { get; set; }
    }

    public sealed class FloatingProgressSpec
    {
        public string DocumentId { get; set; }
        public string Source { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string MergeKey { get; set; }
        public string CorrelationId { get; set; }
        public bool IsIndeterminate { get; set; }
        public bool CanCancel { get; set; }
    }

    public sealed class FloatingStatusSnapshot
    {
        public string DocumentId { get; set; }
        public FloatingHealthState Health { get; set; }
        public FloatingActivityState Activity { get; set; }
        public int TaskGroupCount { get; set; }
        public int WarningCount { get; set; }
        public int ErrorCount { get; set; }
        public int ActiveProgressCount { get; set; }
        public int ActivePromptCount { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class FloatingCenterChangedEventArgs : EventArgs
    {
        public FloatingCenterChangedEventArgs(string documentId,
            FloatingStatusSnapshot status)
        {
            DocumentId = documentId ?? string.Empty;
            Status = status;
        }

        public string DocumentId { get; private set; }
        public FloatingStatusSnapshot Status { get; private set; }
    }

    public interface IPromptSession : IDisposable
    {
        string Id { get; }
        void Update(string message);
    }

    public interface IProgressHandle : IDisposable
    {
        string Id { get; }
        bool IsCompleted { get; }
        void Report(double? progress, string message);
        void Complete(string message);
        void Fail(string message);
        void Cancel(string message);
    }

    public interface IFloatingCenter
    {
        event EventHandler<FloatingCenterChangedEventArgs> Changed;

        FloatingMessage Publish(FloatingMessage message);
        IPromptSession BeginPrompt(FloatingPrompt prompt);
        IProgressHandle BeginProgress(FloatingProgressSpec spec);
        FloatingStatusSnapshot GetStatus(string documentId);
        IList<FloatingMessage> GetHistory(string documentId);
        IList<FloatingMessage> GetActiveMessages(string documentId);
        void Dismiss(string documentId, string messageId);
        void ClearDocument(string documentId);
        void ClearAll();
    }
}
