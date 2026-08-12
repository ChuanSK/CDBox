using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TCPipeAutoDraw.Core.FloatingCenter
{
    public sealed class FloatingCenterService : IFloatingCenter
    {
        private readonly FloatingMessageStore _store;
        private readonly Func<string> _currentDocumentId;

        public FloatingCenterService(Func<string> currentDocumentId = null)
        {
            _store = new FloatingMessageStore();
            _currentDocumentId = currentDocumentId ?? (() => string.Empty);
        }

        public event EventHandler<FloatingCenterChangedEventArgs> Changed;

        public FloatingMessage Publish(FloatingMessage message)
        {
            FloatingMessage stored = _store.Publish(message,
                GetCurrentDocumentId());
            RaiseChanged(stored.DocumentId);
            return stored;
        }

        public IPromptSession BeginPrompt(FloatingPrompt prompt)
        {
            prompt = prompt ?? new FloatingPrompt();
            var message = new FloatingMessage
            {
                DocumentId = prompt.DocumentId,
                Source = prompt.Source,
                Kind = FloatingMessageKind.Prompt,
                Priority = FloatingMessagePriority.High,
                Title = prompt.Title,
                Summary = prompt.Message,
                CorrelationId = prompt.CorrelationId,
                IsPersistent = true,
                RecordInHistory = false,
                PresentAsCard = true,
                MergeKey = "prompt:" + (prompt.Source ?? string.Empty)
            };
            FloatingMessage stored = _store.UpsertActive(message,
                GetCurrentDocumentId());
            RaiseChanged(stored.DocumentId);
            return new PromptSession(this, stored);
        }

        public IProgressHandle BeginProgress(FloatingProgressSpec spec)
        {
            spec = spec ?? new FloatingProgressSpec();
            var message = new FloatingMessage
            {
                DocumentId = spec.DocumentId,
                Source = spec.Source,
                Kind = FloatingMessageKind.Progress,
                Priority = FloatingMessagePriority.Normal,
                Title = spec.Title,
                Summary = spec.Message,
                CorrelationId = spec.CorrelationId,
                IsIndeterminate = spec.IsIndeterminate,
                IsPersistent = true,
                RecordInHistory = false,
                PresentAsCard = true,
                MergeKey = spec.MergeKey
            };
            FloatingMessage stored = _store.UpsertActive(message,
                GetCurrentDocumentId());
            RaiseChanged(stored.DocumentId);
            return new ProgressHandle(this, stored);
        }

        public FloatingStatusSnapshot GetStatus(string documentId)
        {
            string id = NormalizeDocumentId(documentId);
            IList<FloatingMessage> active = _store.GetActive(id);
            int warnings = active.Count(x =>
                x.Kind == FloatingMessageKind.Warning);
            int errors = active.Count(x =>
                x.Kind == FloatingMessageKind.Error);
            int progress = active.Count(x =>
                x.Kind == FloatingMessageKind.Progress);
            int prompts = active.Count(x =>
                x.Kind == FloatingMessageKind.Prompt);
            int decisions = active.Count(x =>
                x.Kind == FloatingMessageKind.Decision
                || x.RequiresDecision);
            int taskGroups = active.Count(x =>
                x.Kind == FloatingMessageKind.Sync
                || x.Kind == FloatingMessageKind.Check
                || x.Kind == FloatingMessageKind.Warning
                || x.Kind == FloatingMessageKind.Error);

            FloatingHealthState health = FloatingHealthState.Normal;
            if (errors > 0) health = FloatingHealthState.Critical;
            else if (warnings > 0) health = FloatingHealthState.Warning;
            else if (taskGroups > 0) health = FloatingHealthState.Pending;

            FloatingActivityState activity = FloatingActivityState.Idle;
            if (decisions > 0)
                activity = FloatingActivityState.WaitingForUserDecision;
            else if (prompts > 0)
                activity = FloatingActivityState.WaitingForCadInput;
            else if (progress > 0)
                activity = FloatingActivityState.Working;

            return new FloatingStatusSnapshot
            {
                DocumentId = id,
                Health = health,
                Activity = activity,
                TaskGroupCount = taskGroups,
                WarningCount = warnings,
                ErrorCount = errors,
                ActiveProgressCount = progress,
                ActivePromptCount = prompts,
                UpdatedAt = _store.GetUpdatedAt(id)
            };
        }

        public IList<FloatingMessage> GetHistory(string documentId)
        {
            return _store.GetHistory(NormalizeDocumentId(documentId));
        }

        public IList<FloatingMessage> GetActiveMessages(string documentId)
        {
            return _store.GetActive(NormalizeDocumentId(documentId));
        }

        public void Dismiss(string documentId, string messageId)
        {
            string id = NormalizeDocumentId(documentId);
            if (_store.RemoveActive(id, messageId)) RaiseChanged(id);
        }

        public void ClearDocument(string documentId)
        {
            string id = NormalizeDocumentId(documentId);
            _store.ClearDocument(id);
            RaiseChanged(id);
        }

        public void ClearAll()
        {
            _store.ClearAll();
        }

        internal void UpdateActive(FloatingMessage message)
        {
            if (message == null) return;
            FloatingMessage stored = _store.UpsertActive(message,
                GetCurrentDocumentId());
            RaiseChanged(stored.DocumentId);
        }

        internal void EndActive(FloatingMessage message)
        {
            if (message == null) return;
            if (_store.RemoveActive(message.DocumentId, message.Id))
                RaiseChanged(message.DocumentId);
        }

        internal void FinishProgress(FloatingMessage active,
            FloatingMessageKind resultKind, string message)
        {
            if (active == null) return;
            _store.RemoveActive(active.DocumentId, active.Id);
            Publish(new FloatingMessage
            {
                DocumentId = active.DocumentId,
                Source = active.Source,
                Kind = resultKind,
                Priority = FloatingMessageStore.DefaultPriority(resultKind),
                Title = active.Title,
                Summary = string.IsNullOrWhiteSpace(message)
                    ? active.Summary : message,
                CorrelationId = active.CorrelationId,
                MergeKey = string.IsNullOrWhiteSpace(active.MergeKey)
                    ? "progress-result:" + active.Id : active.MergeKey,
                PresentAsCard = true,
                RecordInHistory = true
            });
        }

        private string GetCurrentDocumentId()
        {
            try { return NormalizeDocumentId(_currentDocumentId()); }
            catch { return FloatingMessageStore.NormalizeDocumentId(null); }
        }

        private string NormalizeDocumentId(string value)
        {
            string id = (value ?? string.Empty).Trim();
            return id.Length == 0 ? GetCurrentDocumentIdSafe() : id;
        }

        private string GetCurrentDocumentIdSafe()
        {
            try
            {
                string value = (_currentDocumentId() ?? string.Empty).Trim();
                return FloatingMessageStore.NormalizeDocumentId(value);
            }
            catch
            {
                return FloatingMessageStore.NormalizeDocumentId(null);
            }
        }

        private void RaiseChanged(string documentId)
        {
            EventHandler<FloatingCenterChangedEventArgs> handler = Changed;
            if (handler == null) return;
            string id = NormalizeDocumentId(documentId);
            var args = new FloatingCenterChangedEventArgs(id,
                GetStatus(id));
            foreach (Delegate callback in handler.GetInvocationList())
            {
                try
                {
                    ((EventHandler<FloatingCenterChangedEventArgs>)callback)(
                        this, args);
                }
                catch
                {
                    // 状态订阅者属于呈现层，不能反向中断业务消息发布。
                }
            }
        }

        private sealed class PromptSession : IPromptSession
        {
            private FloatingCenterService _owner;
            private FloatingMessage _message;

            public PromptSession(FloatingCenterService owner,
                FloatingMessage message)
            {
                _owner = owner;
                _message = message;
            }

            public string Id
            {
                get { return _message == null ? string.Empty : _message.Id; }
            }

            public void Update(string message)
            {
                FloatingCenterService owner = _owner;
                FloatingMessage current = _message;
                if (owner == null || current == null) return;
                current.Summary = (message ?? string.Empty).Trim();
                owner.UpdateActive(current);
            }

            public void Dispose()
            {
                FloatingCenterService owner = Interlocked.Exchange(
                    ref _owner, null);
                FloatingMessage message = Interlocked.Exchange(
                    ref _message, null);
                if (owner != null) owner.EndActive(message);
            }
        }

        private sealed class ProgressHandle : IProgressHandle
        {
            private FloatingCenterService _owner;
            private FloatingMessage _message;
            private int _completed;

            public ProgressHandle(FloatingCenterService owner,
                FloatingMessage message)
            {
                _owner = owner;
                _message = message;
            }

            public string Id
            {
                get { return _message == null ? string.Empty : _message.Id; }
            }

            public bool IsCompleted
            {
                get { return Volatile.Read(ref _completed) != 0; }
            }

            public void Report(double? progress, string message)
            {
                if (IsCompleted) return;
                FloatingCenterService owner = _owner;
                FloatingMessage current = _message;
                if (owner == null || current == null) return;
                current.Progress = progress;
                current.Summary = string.IsNullOrWhiteSpace(message)
                    ? current.Summary : message.Trim();
                current.IsIndeterminate = !progress.HasValue;
                owner.UpdateActive(current);
            }

            public void Complete(string message)
            {
                Finish(FloatingMessageKind.Success, message);
            }

            public void Fail(string message)
            {
                Finish(FloatingMessageKind.Error, message);
            }

            public void Cancel(string message)
            {
                Finish(FloatingMessageKind.Information,
                    string.IsNullOrWhiteSpace(message)
                        ? "操作已取消。" : message);
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _completed, 1) != 0) return;
                FloatingCenterService owner = Interlocked.Exchange(
                    ref _owner, null);
                FloatingMessage current = Interlocked.Exchange(
                    ref _message, null);
                if (owner != null) owner.EndActive(current);
            }

            private void Finish(FloatingMessageKind kind, string message)
            {
                if (Interlocked.Exchange(ref _completed, 1) != 0) return;
                FloatingCenterService owner = Interlocked.Exchange(
                    ref _owner, null);
                FloatingMessage current = Interlocked.Exchange(
                    ref _message, null);
                if (owner != null)
                    owner.FinishProgress(current, kind, message);
            }
        }
    }
}
