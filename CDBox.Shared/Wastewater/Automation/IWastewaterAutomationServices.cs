using System;
using System.Collections.Generic;
using TCPipeAutoDraw.Core.Check;
using TCPipeAutoDraw.Core.Sync;

namespace CDBox.Shared.Wastewater.Automation
{
    /// <summary>
    /// 一次图纸检查的可变状态边界。规则与状态实现属于 Wastewater，
    /// 基础程序集只持有此接口并负责 CAD 扫描及悬浮球呈现。
    /// </summary>
    public interface IDrawingCheckManager
    {
        event EventHandler<DrawingCheckChangedEventArgs> Changed;
        void Begin(string documentId, string stage);
        void Report(string documentId, double? progress, string stage);
        void Complete(string documentId,
            IEnumerable<DrawingCheckIssue> issues);
        void Cancel(string documentId);
        DrawingCheckSnapshot GetSnapshot(string documentId);
        DrawingCheckGroup GetGroup(string documentId, string groupId);
        List<DrawingCheckIssue> IgnoreGroup(string documentId,
            string groupId);
        List<DrawingCheckIssue> RestoreGroup(string documentId,
            string groupId);
        void ClearDocument(string documentId);
        void ClearAll();
    }

    public interface IWastewaterInspectionService
    {
        IDrawingCheckManager CreateManager();
        List<DrawingCheckIssue> Evaluate(string documentId,
            IEnumerable<DrawingCheckLayerSnapshot> layers,
            IEnumerable<DrawingCheckObjectSnapshot> objects,
            IEnumerable<DrawingCheckAnnotationSnapshot> annotations);
        List<DrawingCheckGroup> Group(string documentId,
            IEnumerable<DrawingCheckIssue> issues,
            DrawingCheckIssueStatus status);
        string NormalizeKind(string value);
        HashSet<string> LoadIgnoredKeys(string documentKey);
        List<DrawingCheckIssue> LoadIgnoredIssues(string documentKey,
            string documentId);
        void AddIgnoredIssues(string documentKey,
            IEnumerable<DrawingCheckIssue> issues);
        void RemoveIgnoredGroup(string documentKey,
            IEnumerable<DrawingCheckIssue> issues);
        void RemoveIgnoredKeys(string documentKey,
            IEnumerable<string> ignoreKeys);
    }

    public interface ISyncManager
    {
        event EventHandler<SyncChangedEventArgs> Changed;
        SyncTask MarkDirty(SyncTask task);
        SyncTask Begin(string documentId, string taskId);
        SyncTask Complete(string documentId, string taskId,
            string resultMessage);
        SyncTask Fail(string documentId, string taskId,
            string resultMessage, bool conflict);
        SyncTask RecordCompleted(SyncTask task, string resultMessage);
        SyncTask GetTask(string documentId, string taskId);
        SyncSnapshot GetSnapshot(string documentId);
        void ClearDocument(string documentId);
        void ClearAll();
    }

    public interface IWastewaterSyncService
    {
        ISyncManager CreateManager();
    }

    public static class WastewaterInspectionRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IWastewaterInspectionService _current;

        public static void Register(IWastewaterInspectionService service)
        {
            if (service == null) throw new ArgumentNullException("service");
            lock (SyncRoot) _current = service;
        }

        public static void Unregister(IWastewaterInspectionService service)
        {
            lock (SyncRoot)
                if (ReferenceEquals(_current, service)) _current = null;
        }

        public static IWastewaterInspectionService Current
        {
            get { lock (SyncRoot) return _current; }
        }

        public static bool IsAvailable
        {
            get { lock (SyncRoot) return _current != null; }
        }
    }

    public static class WastewaterSyncRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IWastewaterSyncService _current;

        public static void Register(IWastewaterSyncService service)
        {
            if (service == null) throw new ArgumentNullException("service");
            lock (SyncRoot) _current = service;
        }

        public static void Unregister(IWastewaterSyncService service)
        {
            lock (SyncRoot)
                if (ReferenceEquals(_current, service)) _current = null;
        }

        public static IWastewaterSyncService Current
        {
            get { lock (SyncRoot) return _current; }
        }

        public static bool IsAvailable
        {
            get { lock (SyncRoot) return _current != null; }
        }
    }
}
