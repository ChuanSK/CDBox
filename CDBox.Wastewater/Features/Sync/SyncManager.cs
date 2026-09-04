using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.Core.Sync
{
    public sealed class SyncManager
        : CDBox.Shared.Wastewater.Automation.ISyncManager
    {
        private const int MaximumHistoryCount = 200;
        private readonly object _gate = new object();
        private readonly Dictionary<string, DocumentSyncState> _documents =
            new Dictionary<string, DocumentSyncState>(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<SyncChangedEventArgs> Changed;

        public SyncTask MarkDirty(SyncTask task)
        {
            if (task == null) throw new ArgumentNullException("task");
            string documentId = Normalize(task.DocumentId);
            SyncTask stored;
            lock (_gate)
            {
                DocumentSyncState state = Get(documentId, true);
                string mergeKey = NormalizeMergeKey(task);
                stored = state.Tasks.Values.FirstOrDefault(x =>
                    string.Equals(x.MergeKey, mergeKey,
                        StringComparison.OrdinalIgnoreCase));
                if (stored == null)
                {
                    stored = task.Clone();
                    stored.Id = string.IsNullOrWhiteSpace(stored.Id)
                        ? Guid.NewGuid().ToString("N") : stored.Id.Trim();
                    stored.DocumentId = documentId;
                    stored.MergeKey = mergeKey;
                    stored.State = SyncTaskState.Dirty;
                    stored.CreatedAt = stored.CreatedAt == default(DateTime)
                        ? DateTime.UtcNow : stored.CreatedAt;
                    NormalizeLists(stored);
                    state.Tasks[stored.Id] = stored;
                }
                else
                {
                    Merge(stored, task);
                    stored.State = SyncTaskState.Dirty;
                    stored.CompletedAt = null;
                    stored.ResultMessage = string.Empty;
                }
                stored.UpdatedAt = DateTime.UtcNow;
                state.UpdatedAt = stored.UpdatedAt;
                stored = stored.Clone();
            }
            RaiseChanged(documentId);
            return stored;
        }

        public SyncTask Begin(string documentId, string taskId)
        {
            return ChangeState(documentId, taskId, SyncTaskState.Running,
                string.Empty, false);
        }

        public SyncTask Complete(string documentId, string taskId,
            string resultMessage)
        {
            return ChangeState(documentId, taskId, SyncTaskState.Completed,
                resultMessage, true);
        }

        public SyncTask Fail(string documentId, string taskId,
            string resultMessage, bool conflict)
        {
            return ChangeState(documentId, taskId, conflict
                ? SyncTaskState.Conflict : SyncTaskState.Error,
                resultMessage, false);
        }

        public SyncTask RecordCompleted(SyncTask task, string resultMessage)
        {
            if (task == null) throw new ArgumentNullException("task");
            string documentId = Normalize(task.DocumentId);
            SyncTask stored = task.Clone();
            lock (_gate)
            {
                DocumentSyncState state = Get(documentId, true);
                stored.DocumentId = documentId;
                stored.MergeKey = NormalizeMergeKey(stored);
                stored.State = SyncTaskState.Completed;
                stored.ResultMessage = resultMessage ?? string.Empty;
                stored.UpdatedAt = DateTime.UtcNow;
                stored.CompletedAt = stored.UpdatedAt;
                NormalizeLists(stored);
                AddHistory(state, stored);
                state.UpdatedAt = stored.UpdatedAt;
                stored = stored.Clone();
            }
            RaiseChanged(documentId);
            return stored;
        }

        public SyncTask GetTask(string documentId, string taskId)
        {
            string id = Normalize(documentId);
            lock (_gate)
            {
                DocumentSyncState state = Get(id, false);
                SyncTask task;
                return state != null && state.Tasks.TryGetValue(
                    taskId ?? string.Empty, out task) ? task.Clone() : null;
            }
        }

        public SyncSnapshot GetSnapshot(string documentId)
        {
            string id = Normalize(documentId);
            lock (_gate)
            {
                DocumentSyncState state = Get(id, false);
                if (state == null) return new SyncSnapshot
                {
                    DocumentId = id,
                    UpdatedAt = DateTime.UtcNow
                };
                return new SyncSnapshot
                {
                    DocumentId = id,
                    Tasks = state.Tasks.Values
                        .OrderByDescending(x => x.Risk)
                        .ThenBy(x => x.CreatedAt)
                        .Select(x => x.Clone()).ToList(),
                    History = state.History.Select(x => x.Clone()).ToList(),
                    UpdatedAt = state.UpdatedAt
                };
            }
        }

        public void ClearDocument(string documentId)
        {
            string id = Normalize(documentId);
            lock (_gate) _documents.Remove(id);
            RaiseChanged(id);
        }

        public void ClearAll()
        {
            lock (_gate) _documents.Clear();
        }

        private SyncTask ChangeState(string documentId, string taskId,
            SyncTaskState stateValue, string resultMessage, bool moveToHistory)
        {
            string id = Normalize(documentId);
            SyncTask result = null;
            lock (_gate)
            {
                DocumentSyncState state = Get(id, false);
                SyncTask task;
                if (state == null || !state.Tasks.TryGetValue(
                    taskId ?? string.Empty, out task)) return null;
                task.State = stateValue;
                task.ResultMessage = resultMessage ?? string.Empty;
                task.UpdatedAt = DateTime.UtcNow;
                if (moveToHistory)
                {
                    task.CompletedAt = task.UpdatedAt;
                    state.Tasks.Remove(task.Id);
                    AddHistory(state, task);
                }
                state.UpdatedAt = task.UpdatedAt;
                result = task.Clone();
            }
            RaiseChanged(id);
            return result;
        }

        private static void Merge(SyncTask target, SyncTask source)
        {
            if (!string.IsNullOrWhiteSpace(source.Source)) target.Source = source.Source;
            if (!string.IsNullOrWhiteSpace(source.Title)) target.Title = source.Title;
            if (!string.IsNullOrWhiteSpace(source.Summary)) target.Summary = source.Summary;
            if (!string.IsNullOrWhiteSpace(source.Reason)) target.Reason = source.Reason;
            target.Type = source.Type;
            if (source.Risk > target.Risk) target.Risk = source.Risk;
            target.ObjectHandles = Union(target.ObjectHandles, source.ObjectHandles);
            target.ChangedProperties = Union(target.ChangedProperties,
                source.ChangedProperties);
        }

        private static List<string> Union(IEnumerable<string> first,
            IEnumerable<string> second)
        {
            return (first ?? Enumerable.Empty<string>())
                .Concat(second ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void NormalizeLists(SyncTask task)
        {
            task.ObjectHandles = Union(task.ObjectHandles, null);
            task.ChangedProperties = Union(task.ChangedProperties, null);
        }

        private static string NormalizeMergeKey(SyncTask task)
        {
            string value = (task.MergeKey ?? string.Empty).Trim();
            return value.Length > 0 ? value : task.Type.ToString().ToLowerInvariant();
        }

        private static void AddHistory(DocumentSyncState state, SyncTask task)
        {
            state.History.Insert(0, task.Clone());
            while (state.History.Count > MaximumHistoryCount)
                state.History.RemoveAt(state.History.Count - 1);
        }

        private DocumentSyncState Get(string documentId, bool create)
        {
            DocumentSyncState state;
            if (_documents.TryGetValue(documentId, out state)) return state;
            if (!create) return null;
            state = new DocumentSyncState();
            _documents[documentId] = state;
            return state;
        }

        private void RaiseChanged(string documentId)
        {
            EventHandler<SyncChangedEventArgs> handler = Changed;
            if (handler == null) return;
            var args = new SyncChangedEventArgs(documentId);
            foreach (Delegate callback in handler.GetInvocationList())
                try { ((EventHandler<SyncChangedEventArgs>)callback)(this, args); }
                catch { }
        }

        private static string Normalize(string value)
        {
            string id = (value ?? string.Empty).Trim();
            return id.Length == 0 ? "__global__" : id;
        }

        private sealed class DocumentSyncState
        {
            public readonly Dictionary<string, SyncTask> Tasks =
                new Dictionary<string, SyncTask>(StringComparer.OrdinalIgnoreCase);
            public readonly List<SyncTask> History = new List<SyncTask>();
            public DateTime UpdatedAt = DateTime.UtcNow;
        }
    }
}
