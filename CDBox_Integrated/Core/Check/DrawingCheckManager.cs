using System;
using System.Collections.Generic;
using CDBox.Shared.Wastewater.Automation;

namespace TCPipeAutoDraw.Core.Check
{
    /// <summary>
    /// 污水检查状态机兼容门面。模块未安装时返回空状态，基础组件不再
    /// 自行执行污水检查。
    /// </summary>
    public sealed class DrawingCheckManager
    {
        private readonly object _gate = new object();
        private IDrawingCheckManager _inner;

        public event EventHandler<DrawingCheckChangedEventArgs> Changed
        {
            add
            {
                IDrawingCheckManager manager = Resolve();
                if (manager != null) manager.Changed += value;
            }
            remove
            {
                IDrawingCheckManager manager = Existing;
                if (manager != null) manager.Changed -= value;
            }
        }

        public void Begin(string documentId, string stage)
        {
            IDrawingCheckManager manager = Resolve();
            if (manager != null) manager.Begin(documentId, stage);
        }

        public void Report(string documentId, double? progress, string stage)
        {
            IDrawingCheckManager manager = Resolve();
            if (manager != null) manager.Report(documentId, progress, stage);
        }

        public void Complete(string documentId,
            IEnumerable<DrawingCheckIssue> issues)
        {
            IDrawingCheckManager manager = Resolve();
            if (manager != null) manager.Complete(documentId, issues);
        }

        public void Cancel(string documentId)
        {
            IDrawingCheckManager manager = Resolve();
            if (manager != null) manager.Cancel(documentId);
        }

        public DrawingCheckSnapshot GetSnapshot(string documentId)
        {
            IDrawingCheckManager manager = Resolve();
            return manager == null ? new DrawingCheckSnapshot
            {
                DocumentId = documentId ?? string.Empty
            } : manager.GetSnapshot(documentId);
        }

        public DrawingCheckGroup GetGroup(string documentId, string groupId)
        {
            IDrawingCheckManager manager = Resolve();
            return manager == null ? null
                : manager.GetGroup(documentId, groupId);
        }

        public List<DrawingCheckIssue> IgnoreGroup(string documentId,
            string groupId)
        {
            IDrawingCheckManager manager = Resolve();
            return manager == null ? new List<DrawingCheckIssue>()
                : manager.IgnoreGroup(documentId, groupId);
        }

        public List<DrawingCheckIssue> RestoreGroup(string documentId,
            string groupId)
        {
            IDrawingCheckManager manager = Resolve();
            return manager == null ? new List<DrawingCheckIssue>()
                : manager.RestoreGroup(documentId, groupId);
        }

        public void ClearDocument(string documentId)
        {
            IDrawingCheckManager manager = Existing;
            if (manager != null) manager.ClearDocument(documentId);
        }

        public void ClearAll()
        {
            IDrawingCheckManager manager = Existing;
            if (manager != null) manager.ClearAll();
        }

        private IDrawingCheckManager Existing
        {
            get { lock (_gate) return _inner; }
        }

        private IDrawingCheckManager Resolve()
        {
            lock (_gate)
            {
                if (_inner != null) return _inner;
                IWastewaterInspectionService service =
                    WastewaterInspectionRegistry.Current;
                if (service != null) _inner = service.CreateManager();
                return _inner;
            }
        }
    }
}
