using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.Core.FloatingCenter
{
    public sealed class FloatingMessagePresentationQueue
    {
        private readonly int _capacity;
        private readonly List<FloatingMessage> _messages =
            new List<FloatingMessage>();

        public FloatingMessagePresentationQueue(int capacity = 8)
        {
            _capacity = Math.Max(1, capacity);
        }

        public int Count { get { return _messages.Count; } }

        public bool Contains(string documentId, string messageId)
        {
            string id = FloatingMessageStore.NormalizeDocumentId(documentId);
            if (string.IsNullOrWhiteSpace(messageId)) return false;
            return _messages.Any(message => message != null &&
                string.Equals(message.DocumentId, id,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(message.Id, messageId,
                    StringComparison.OrdinalIgnoreCase));
        }

        public void Enqueue(FloatingMessage message)
        {
            if (message == null) return;
            FloatingMessage value = message.Clone();
            int mergedIndex = FindMergeIndex(value);
            if (mergedIndex >= 0)
            {
                _messages[mergedIndex] = value;
                return;
            }
            _messages.Add(value);
            while (_messages.Count > _capacity)
            {
                int removeIndex = FindRemovalIndex();
                if (removeIndex < 0) removeIndex = 0;
                _messages.RemoveAt(removeIndex);
            }
        }

        public FloatingMessage Dequeue(string documentId)
        {
            string id = FloatingMessageStore.NormalizeDocumentId(documentId);
            int index = _messages.Select((message, position) => new
                {
                    Message = message,
                    Position = position
                })
                .Where(x => string.Equals(x.Message.DocumentId, id,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => PresentationRank(x.Message))
                .ThenBy(x => x.Message.UpdatedAt == default(DateTime)
                    ? x.Message.CreatedAt : x.Message.UpdatedAt)
                .Select(x => x.Position)
                .DefaultIfEmpty(-1).First();
            if (index < 0) return null;
            FloatingMessage value = _messages[index];
            _messages.RemoveAt(index);
            return value.Clone();
        }

        public int Remove(string documentId, string messageId)
        {
            string id = FloatingMessageStore.NormalizeDocumentId(documentId);
            if (string.IsNullOrWhiteSpace(messageId)) return 0;
            return _messages.RemoveAll(message => message != null &&
                string.Equals(message.DocumentId, id,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(message.Id, messageId,
                    StringComparison.OrdinalIgnoreCase));
        }

        public int RemoveInactivePersistent(string documentId,
            IEnumerable<string> activeMessageIds)
        {
            string id = FloatingMessageStore.NormalizeDocumentId(documentId);
            var active = new HashSet<string>(activeMessageIds ??
                Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return _messages.RemoveAll(message => message != null &&
                message.IsPersistent &&
                string.Equals(message.DocumentId, id,
                    StringComparison.OrdinalIgnoreCase) &&
                !active.Contains(message.Id ?? string.Empty));
        }

        public int RemoveSupersededBy(FloatingMessage incoming)
        {
            if (incoming == null) return 0;
            int incomingRank = PresentationRank(incoming);
            DateTime incomingTime = VersionOf(incoming);
            return _messages.RemoveAll(message => message != null &&
                string.Equals(message.DocumentId, incoming.DocumentId,
                    StringComparison.OrdinalIgnoreCase) &&
                PresentationRank(message) < incomingRank &&
                (message.Kind == FloatingMessageKind.Progress ||
                 message.Kind == FloatingMessageKind.Information ||
                 message.Kind == FloatingMessageKind.Success) &&
                VersionOf(message) <= incomingTime);
        }

        public void ClearDocument(string documentId)
        {
            string id = FloatingMessageStore.NormalizeDocumentId(documentId);
            _messages.RemoveAll(x => x != null && string.Equals(
                x.DocumentId, id, StringComparison.OrdinalIgnoreCase));
        }

        public void Clear()
        {
            _messages.Clear();
        }

        public static int PresentationRank(FloatingMessage message)
        {
            if (message == null) return 0;
            if (message.Kind == FloatingMessageKind.Prompt ||
                message.Kind == FloatingMessageKind.Decision ||
                message.RequiresDecision) return 600;
            if (message.Kind == FloatingMessageKind.Error ||
                message.Priority == FloatingMessagePriority.Critical) return 500;
            if (message.Kind == FloatingMessageKind.Warning) return 400;
            if (message.Kind == FloatingMessageKind.Progress) return 300;
            if (message.Kind == FloatingMessageKind.Sync ||
                message.Kind == FloatingMessageKind.Check) return 250;
            if (message.Kind == FloatingMessageKind.Success) return 150;
            return 100;
        }

        private int FindMergeIndex(FloatingMessage value)
        {
            for (int index = 0; index < _messages.Count; index++)
            {
                FloatingMessage current = _messages[index];
                if (current == null) continue;
                if (!string.IsNullOrWhiteSpace(value.Id) && string.Equals(
                    current.Id, value.Id, StringComparison.OrdinalIgnoreCase))
                    return index;
                if (!string.IsNullOrWhiteSpace(value.MergeKey) && string.Equals(
                    current.DocumentId, value.DocumentId,
                    StringComparison.OrdinalIgnoreCase) && string.Equals(
                    current.MergeKey, value.MergeKey,
                    StringComparison.OrdinalIgnoreCase)) return index;
            }
            return -1;
        }

        private int FindRemovalIndex()
        {
            return _messages.Select((message, position) => new
                {
                    Message = message,
                    Position = position
                })
                .OrderBy(x => PresentationRank(x.Message))
                .ThenBy(x => x.Message == null ? DateTime.MinValue :
                    (x.Message.UpdatedAt == default(DateTime)
                        ? x.Message.CreatedAt : x.Message.UpdatedAt))
                .Select(x => x.Position)
                .DefaultIfEmpty(-1).First();
        }

        private static DateTime VersionOf(FloatingMessage message)
        {
            if (message == null) return DateTime.MinValue;
            return message.UpdatedAt == default(DateTime)
                ? message.CreatedAt : message.UpdatedAt;
        }
    }
}
