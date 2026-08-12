using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.Core.FloatingCenter
{
    internal sealed class FloatingMessageStore
    {
        private const int MaximumHistoryCount = 200;
        private static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds(3);
        private readonly object _gate = new object();
        private readonly Dictionary<string, DocumentBucket> _documents =
            new Dictionary<string, DocumentBucket>(
                StringComparer.OrdinalIgnoreCase);

        public FloatingMessage Publish(FloatingMessage source,
            string fallbackDocumentId)
        {
            FloatingMessage message = Normalize(source, fallbackDocumentId);
            lock (_gate)
            {
                DocumentBucket bucket = GetBucket(message.DocumentId, true);
                FloatingMessage stored = message;
                if (message.RecordInHistory)
                    stored = AddOrMergeHistory(bucket, message);
                if (message.IsPersistent)
                    bucket.Active[stored.Id] = stored.Clone();
                bucket.UpdatedAt = DateTime.UtcNow;
                return stored.Clone();
            }
        }

        public FloatingMessage UpsertActive(FloatingMessage source,
            string fallbackDocumentId)
        {
            FloatingMessage message = Normalize(source, fallbackDocumentId);
            lock (_gate)
            {
                DocumentBucket bucket = GetBucket(message.DocumentId, true);
                bucket.Active[message.Id] = message.Clone();
                bucket.UpdatedAt = DateTime.UtcNow;
                return message.Clone();
            }
        }

        public bool RemoveActive(string documentId, string messageId)
        {
            string key = NormalizeDocumentId(documentId);
            if (string.IsNullOrWhiteSpace(messageId)) return false;
            lock (_gate)
            {
                DocumentBucket bucket = GetBucket(key, false);
                if (bucket == null || !bucket.Active.Remove(messageId))
                    return false;
                bucket.UpdatedAt = DateTime.UtcNow;
                return true;
            }
        }

        public IList<FloatingMessage> GetHistory(string documentId)
        {
            lock (_gate)
            {
                DocumentBucket bucket = GetBucket(
                    NormalizeDocumentId(documentId), false);
                return bucket == null
                    ? new List<FloatingMessage>()
                    : bucket.History.Select(x => x.Clone()).ToList();
            }
        }

        public IList<FloatingMessage> GetActive(string documentId)
        {
            lock (_gate)
            {
                DocumentBucket bucket = GetBucket(
                    NormalizeDocumentId(documentId), false);
                return bucket == null
                    ? new List<FloatingMessage>()
                    : bucket.Active.Values
                        .OrderByDescending(x => x.Priority)
                        .ThenByDescending(x => x.UpdatedAt)
                        .Select(x => x.Clone()).ToList();
            }
        }

        public DateTime GetUpdatedAt(string documentId)
        {
            lock (_gate)
            {
                DocumentBucket bucket = GetBucket(
                    NormalizeDocumentId(documentId), false);
                return bucket == null ? DateTime.UtcNow : bucket.UpdatedAt;
            }
        }

        public void ClearDocument(string documentId)
        {
            lock (_gate)
                _documents.Remove(NormalizeDocumentId(documentId));
        }

        public void ClearAll()
        {
            lock (_gate) _documents.Clear();
        }

        private static FloatingMessage AddOrMergeHistory(
            DocumentBucket bucket, FloatingMessage message)
        {
            FloatingMessage previous = bucket.History.FirstOrDefault(x =>
                CanMerge(x, message));
            if (previous != null)
            {
                previous.Title = message.Title;
                previous.Summary = message.Summary;
                previous.Detail = message.Detail;
                previous.Priority = message.Priority;
                previous.Progress = message.Progress;
                previous.IsIndeterminate = message.IsIndeterminate;
                previous.IsPersistent = message.IsPersistent;
                previous.PresentAsCard = message.PresentAsCard;
                previous.RequiresDecision = message.RequiresDecision;
                previous.UpdatedAt = message.UpdatedAt;
                previous.RepeatCount = Math.Max(1, previous.RepeatCount) + 1;
                previous.Actions = message.Actions == null
                    ? new List<FloatingAction>()
                    : message.Actions.Where(x => x != null)
                        .Select(x => x.Clone()).ToList();
                return previous;
            }

            FloatingMessage stored = message.Clone();
            bucket.History.Insert(0, stored);
            while (bucket.History.Count > MaximumHistoryCount)
                bucket.History.RemoveAt(bucket.History.Count - 1);
            return stored;
        }

        private static bool CanMerge(FloatingMessage existing,
            FloatingMessage incoming)
        {
            if (existing == null || incoming == null
                || string.IsNullOrWhiteSpace(incoming.MergeKey)
                || !string.Equals(existing.MergeKey, incoming.MergeKey,
                    StringComparison.OrdinalIgnoreCase)) return false;
            DateTime existingTime = existing.UpdatedAt == default(DateTime)
                ? existing.CreatedAt : existing.UpdatedAt;
            return incoming.UpdatedAt - existingTime <= MergeWindow;
        }

        private DocumentBucket GetBucket(string documentId, bool create)
        {
            DocumentBucket bucket;
            if (_documents.TryGetValue(documentId, out bucket)) return bucket;
            if (!create) return null;
            bucket = new DocumentBucket();
            _documents[documentId] = bucket;
            return bucket;
        }

        private static FloatingMessage Normalize(FloatingMessage source,
            string fallbackDocumentId)
        {
            if (source == null) throw new ArgumentNullException("source");
            FloatingMessage message = source.Clone();
            message.Id = string.IsNullOrWhiteSpace(message.Id)
                ? Guid.NewGuid().ToString("N") : message.Id.Trim();
            message.DocumentId = NormalizeDocumentId(
                string.IsNullOrWhiteSpace(message.DocumentId)
                    ? fallbackDocumentId : message.DocumentId);
            message.Source = string.IsNullOrWhiteSpace(message.Source)
                ? "CDBox" : message.Source.Trim();
            message.Title = string.IsNullOrWhiteSpace(message.Title)
                ? "CDBox" : message.Title.Trim();
            message.Summary = (message.Summary ?? string.Empty).Trim();
            message.Detail = (message.Detail ?? string.Empty).Trim();
            message.MergeKey = (message.MergeKey ?? string.Empty).Trim();
            message.CorrelationId = (message.CorrelationId ?? string.Empty).Trim();
            if (message.CreatedAt == default(DateTime))
                message.CreatedAt = DateTime.UtcNow;
            message.UpdatedAt = DateTime.UtcNow;
            message.RepeatCount = Math.Max(1, message.RepeatCount);
            if (message.Progress.HasValue)
                message.Progress = Math.Max(0.0,
                    Math.Min(100.0, message.Progress.Value));
            if (message.Priority == FloatingMessagePriority.Unspecified)
                message.Priority = DefaultPriority(message.Kind);
            if (message.Actions == null)
                message.Actions = new List<FloatingAction>();
            return message;
        }

        internal static string NormalizeDocumentId(string value)
        {
            string id = (value ?? string.Empty).Trim();
            return id.Length == 0 ? "__global__" : id;
        }

        internal static FloatingMessagePriority DefaultPriority(
            FloatingMessageKind kind)
        {
            if (kind == FloatingMessageKind.Error)
                return FloatingMessagePriority.Critical;
            if (kind == FloatingMessageKind.Warning
                || kind == FloatingMessageKind.Decision)
                return FloatingMessagePriority.High;
            if (kind == FloatingMessageKind.Success)
                return FloatingMessagePriority.Low;
            return FloatingMessagePriority.Normal;
        }

        private sealed class DocumentBucket
        {
            public readonly List<FloatingMessage> History =
                new List<FloatingMessage>();
            public readonly Dictionary<string, FloatingMessage> Active =
                new Dictionary<string, FloatingMessage>(
                    StringComparer.OrdinalIgnoreCase);
            public DateTime UpdatedAt = DateTime.UtcNow;
        }
    }
}
