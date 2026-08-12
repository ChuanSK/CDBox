using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.Core.Check
{
    public sealed class DrawingCheckChangedEventArgs : EventArgs
    {
        public DrawingCheckChangedEventArgs(string documentId)
        {
            DocumentId = documentId ?? string.Empty;
        }

        public string DocumentId { get; private set; }
    }

    public sealed class DrawingCheckManager
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, DocumentCheckState> _documents =
            new Dictionary<string, DocumentCheckState>(
                StringComparer.OrdinalIgnoreCase);

        public event EventHandler<DrawingCheckChangedEventArgs> Changed;

        public void Begin(string documentId, string stage)
        {
            string id = Normalize(documentId);
            lock (_gate)
            {
                DocumentCheckState state = Get(id, true);
                state.IsRunning = true;
                state.Progress = null;
                state.Stage = stage ?? string.Empty;
                state.UpdatedAt = DateTime.UtcNow;
            }
            RaiseChanged(id);
        }

        public void Report(string documentId, double? progress, string stage)
        {
            string id = Normalize(documentId);
            lock (_gate)
            {
                DocumentCheckState state = Get(id, true);
                state.IsRunning = true;
                state.Progress = progress.HasValue
                    ? Math.Max(0.0, Math.Min(100.0, progress.Value))
                    : (double?)null;
                state.Stage = stage ?? state.Stage;
                state.UpdatedAt = DateTime.UtcNow;
            }
            RaiseChanged(id);
        }

        public void Complete(string documentId,
            IEnumerable<DrawingCheckIssue> issues)
        {
            string id = Normalize(documentId);
            List<DrawingCheckIssue> values = (issues ??
                Enumerable.Empty<DrawingCheckIssue>()).Where(x => x != null)
                .Select(x => x.Clone()).ToList();
            lock (_gate)
            {
                DocumentCheckState state = Get(id, true);
                state.Issues = values;
                state.Groups = DrawingCheckRuleEvaluator.Group(id, values);
                state.IgnoredGroups = DrawingCheckRuleEvaluator.Group(id,
                    values, DrawingCheckIssueStatus.Ignored);
                state.IsRunning = false;
                state.Progress = 100.0;
                state.Stage = "检查完成";
                state.UpdatedAt = DateTime.UtcNow;
            }
            RaiseChanged(id);
        }

        public void Cancel(string documentId)
        {
            string id = Normalize(documentId);
            lock (_gate)
            {
                DocumentCheckState state = Get(id, false);
                if (state == null) return;
                state.IsRunning = false;
                state.Progress = null;
                state.Stage = "已取消";
                state.UpdatedAt = DateTime.UtcNow;
            }
            RaiseChanged(id);
        }

        public DrawingCheckSnapshot GetSnapshot(string documentId)
        {
            string id = Normalize(documentId);
            lock (_gate)
            {
                DocumentCheckState state = Get(id, false);
                return state == null ? new DrawingCheckSnapshot
                {
                    DocumentId = id
                } : state.Snapshot(id);
            }
        }

        public DrawingCheckGroup GetGroup(string documentId, string groupId)
        {
            DrawingCheckSnapshot snapshot = GetSnapshot(documentId);
            DrawingCheckGroup group = snapshot.Groups.FirstOrDefault(x =>
                string.Equals(x.Id, groupId,
                    StringComparison.OrdinalIgnoreCase));
            if (group == null) group = snapshot.IgnoredGroups.FirstOrDefault(x =>
                string.Equals(x.Id, groupId,
                    StringComparison.OrdinalIgnoreCase));
            return group == null ? null : group.Clone();
        }

        public List<DrawingCheckIssue> IgnoreGroup(string documentId,
            string groupId)
        {
            return SetGroupStatus(documentId, groupId,
                DrawingCheckIssueStatus.Active,
                DrawingCheckIssueStatus.Ignored);
        }

        public List<DrawingCheckIssue> RestoreGroup(string documentId,
            string groupId)
        {
            return SetGroupStatus(documentId, groupId,
                DrawingCheckIssueStatus.Ignored,
                DrawingCheckIssueStatus.Active);
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

        private DocumentCheckState Get(string id, bool create)
        {
            DocumentCheckState value;
            if (_documents.TryGetValue(id, out value)) return value;
            if (!create) return null;
            value = new DocumentCheckState();
            _documents[id] = value;
            return value;
        }

        private List<DrawingCheckIssue> SetGroupStatus(string documentId,
            string groupId, DrawingCheckIssueStatus from,
            DrawingCheckIssueStatus to)
        {
            string id = Normalize(documentId);
            var changed = new List<DrawingCheckIssue>();
            lock (_gate)
            {
                DocumentCheckState state = Get(id, false);
                if (state == null) return changed;
                List<DrawingCheckGroup> source = from ==
                    DrawingCheckIssueStatus.Ignored
                    ? state.IgnoredGroups : state.Groups;
                DrawingCheckGroup group = source.FirstOrDefault(x =>
                    string.Equals(x.Id, groupId,
                        StringComparison.OrdinalIgnoreCase));
                if (group == null) return changed;
                var issueIds = new HashSet<string>(group.IssueIds ??
                    new List<string>(), StringComparer.OrdinalIgnoreCase);
                foreach (DrawingCheckIssue issue in state.Issues)
                {
                    if (issue == null || issue.Status != from ||
                        !issueIds.Contains(issue.Id)) continue;
                    issue.Status = to;
                    issue.UpdatedAt = DateTime.UtcNow;
                    changed.Add(issue.Clone());
                }
                state.Groups = DrawingCheckRuleEvaluator.Group(id,
                    state.Issues);
                state.IgnoredGroups = DrawingCheckRuleEvaluator.Group(id,
                    state.Issues, DrawingCheckIssueStatus.Ignored);
                state.UpdatedAt = DateTime.UtcNow;
            }
            if (changed.Count > 0) RaiseChanged(id);
            return changed;
        }

        private void RaiseChanged(string documentId)
        {
            EventHandler<DrawingCheckChangedEventArgs> handler = Changed;
            if (handler == null) return;
            var args = new DrawingCheckChangedEventArgs(documentId);
            foreach (Delegate callback in handler.GetInvocationList())
                try
                {
                    ((EventHandler<DrawingCheckChangedEventArgs>)callback)(
                        this, args);
                }
                catch { }
        }

        private static string Normalize(string value)
        {
            string id = (value ?? string.Empty).Trim();
            return id.Length == 0 ? "__global__" : id;
        }

        private sealed class DocumentCheckState
        {
            public bool IsRunning;
            public double? Progress;
            public string Stage = string.Empty;
            public DateTime UpdatedAt = DateTime.UtcNow;
            public List<DrawingCheckIssue> Issues =
                new List<DrawingCheckIssue>();
            public List<DrawingCheckGroup> Groups =
                new List<DrawingCheckGroup>();
            public List<DrawingCheckGroup> IgnoredGroups =
                new List<DrawingCheckGroup>();

            public DrawingCheckSnapshot Snapshot(string documentId)
            {
                return new DrawingCheckSnapshot
                {
                    DocumentId = documentId,
                    IsRunning = IsRunning,
                    Progress = Progress,
                    Stage = Stage,
                    UpdatedAt = UpdatedAt,
                    Groups = Groups.Select(x => x.Clone()).ToList(),
                    IgnoredGroups = IgnoredGroups.Select(x => x.Clone()).ToList()
                };
            }
        }
    }
}
