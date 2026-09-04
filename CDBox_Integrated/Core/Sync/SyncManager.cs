using System;
using CDBox.Shared.Wastewater.Automation;

namespace TCPipeAutoDraw.Core.Sync
{
    /// <summary>
    /// 污水同步状态机兼容门面。实际任务合并、冲突与历史状态由
    /// CDBox.Wastewater.dll 维护。
    /// </summary>
    public sealed class SyncManager
    {
        private readonly object _gate = new object();
        private ISyncManager _inner;

        public event EventHandler<SyncChangedEventArgs> Changed
        {
            add
            {
                ISyncManager manager = Resolve();
                if (manager != null) manager.Changed += value;
            }
            remove
            {
                ISyncManager manager = Existing;
                if (manager != null) manager.Changed -= value;
            }
        }

        public SyncTask MarkDirty(SyncTask task)
        {
            ISyncManager manager = Resolve();
            return manager == null ? (task == null ? null : task.Clone())
                : manager.MarkDirty(task);
        }

        public SyncTask Begin(string documentId, string taskId)
        {
            ISyncManager manager = Resolve();
            return manager == null ? null : manager.Begin(documentId, taskId);
        }

        public SyncTask Complete(string documentId, string taskId,
            string resultMessage)
        {
            ISyncManager manager = Resolve();
            return manager == null ? null
                : manager.Complete(documentId, taskId, resultMessage);
        }

        public SyncTask Fail(string documentId, string taskId,
            string resultMessage, bool conflict)
        {
            ISyncManager manager = Resolve();
            return manager == null ? null
                : manager.Fail(documentId, taskId, resultMessage, conflict);
        }

        public SyncTask RecordCompleted(SyncTask task, string resultMessage)
        {
            ISyncManager manager = Resolve();
            return manager == null ? (task == null ? null : task.Clone())
                : manager.RecordCompleted(task, resultMessage);
        }

        public SyncTask GetTask(string documentId, string taskId)
        {
            ISyncManager manager = Resolve();
            return manager == null ? null
                : manager.GetTask(documentId, taskId);
        }

        public SyncSnapshot GetSnapshot(string documentId)
        {
            ISyncManager manager = Resolve();
            return manager == null ? new SyncSnapshot
            {
                DocumentId = documentId ?? string.Empty
            } : manager.GetSnapshot(documentId);
        }

        public void ClearDocument(string documentId)
        {
            ISyncManager manager = Existing;
            if (manager != null) manager.ClearDocument(documentId);
        }

        public void ClearAll()
        {
            ISyncManager manager = Existing;
            if (manager != null) manager.ClearAll();
        }

        private ISyncManager Existing
        {
            get { lock (_gate) return _inner; }
        }

        private ISyncManager Resolve()
        {
            lock (_gate)
            {
                if (_inner != null) return _inner;
                IWastewaterSyncService service =
                    WastewaterSyncRegistry.Current;
                if (service != null) _inner = service.CreateManager();
                return _inner;
            }
        }
    }
}
