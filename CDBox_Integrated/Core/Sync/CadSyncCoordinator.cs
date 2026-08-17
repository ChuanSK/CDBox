using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using TCPipeAutoDraw.Core.Check;
using TCPipeAutoDraw.Core.Business;
using TCPipeAutoDraw.Core.FloatingCenter;
using TCPipeAutoDraw.Modules.AnnotationHud;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using FloatingHub = TCPipeAutoDraw.Core.FloatingCenter.FloatingCenter;

namespace TCPipeAutoDraw.Core.Sync
{
    internal static class CadSyncCoordinator
    {
        private const string SourceName = "SyncCenter";
        private const int SafeObjectLimit = 5;
        private static readonly object Gate = new object();
        private static readonly SyncManager ManagerValue = new SyncManager();
        private static bool _initialized;
        private static bool _suppressQuantityDirty;

        public static SyncManager Manager { get { return ManagerValue; } }

        public static void Initialize()
        {
            lock (Gate)
            {
                if (_initialized) return;
                QuantityPipeAttributeService.AttributesChanged +=
                    AttributesChanged;
                QuantityDashboardLiveMonitor.DirtyMarked += DashboardDirtyMarked;
                try
                {
                    AcadApp.DocumentManager.DocumentToBeDestroyed +=
                        DocumentToBeDestroyed;
                }
                catch { }
                CDBoxBusinessModeService.Changed += BusinessModeChanged;
                _initialized = true;
            }
        }

        public static void Terminate()
        {
            lock (Gate)
            {
                if (!_initialized) return;
                QuantityPipeAttributeService.AttributesChanged -=
                    AttributesChanged;
                QuantityDashboardLiveMonitor.DirtyMarked -= DashboardDirtyMarked;
                try
                {
                    AcadApp.DocumentManager.DocumentToBeDestroyed -=
                        DocumentToBeDestroyed;
                }
                catch { }
                CDBoxBusinessModeService.Changed -= BusinessModeChanged;
                ManagerValue.ClearAll();
                _suppressQuantityDirty = false;
                _initialized = false;
            }
        }

        public static void MarkAnnotationDirty(Document document,
            IEnumerable<string> handles, string reason)
        {
            if (!CDBoxBusinessModeService.IsWastewater || document == null)
                return;
            List<string> original = NormalizeHandles(handles);
            List<string> affected = FilterToDashboardScope(document, original);
            if (original.Count > 0 && affected.Count == 0) return;
            SyncTask task = ManagerValue.MarkDirty(new SyncTask
            {
                DocumentId = CadFloatingDocumentIdentity.GetDocumentId(document),
                MergeKey = "annotation",
                Source = SourceName,
                Type = SyncTaskType.Annotation,
                Title = "标注需要同步",
                Summary = BuildAffectedSummary(affected,
                    "绑定标注需要刷新"),
                Reason = reason ?? string.Empty,
                ObjectHandles = affected,
                ChangedProperties = new List<string> { "绑定标注" },
                Risk = ResolveRisk(affected)
            });
            PublishTask(task);
            TryAutoSync(document, task);
        }

        public static void ViewImpact(Document document, string taskId)
        {
            if (!CDBoxBusinessModeService.IsWastewater || document == null)
                return;
            string documentId = CadFloatingDocumentIdentity.GetDocumentId(document);
            SyncTask task = ManagerValue.GetTask(documentId, taskId);
            if (task == null) return;
            if (task.ObjectHandles.Count > 0)
            {
                CadDrawingCheckCoordinator.LocateHandles(document,
                    task.ObjectHandles, "同步影响对象");
                return;
            }
            FloatingHub.Current.Publish(new FloatingMessage
            {
                DocumentId = documentId,
                Source = SourceName,
                Kind = FloatingMessageKind.Information,
                Title = "同步影响范围",
                Summary = "该任务影响当前图纸的整体统计结果，没有单独对象可定位。",
                Detail = task.Reason,
                PresentAsCard = true,
                RecordInHistory = true,
                MergeKey = "sync-impact:" + task.Id
            });
        }

        public static void RunTask(Document document, string taskId)
        {
            if (!CDBoxBusinessModeService.IsWastewater || document == null
                || string.IsNullOrWhiteSpace(taskId)) return;
            string documentId = CadFloatingDocumentIdentity.GetDocumentId(document);
            SyncTask task = ManagerValue.GetTask(documentId, taskId);
            if (task == null) return;
            ExecuteTask(document, task);
        }

        public static void RunAll(Document document, bool confirmed)
        {
            if (!CDBoxBusinessModeService.IsWastewater || document == null)
                return;
            string documentId = CadFloatingDocumentIdentity.GetDocumentId(document);
            List<SyncTask> tasks = ManagerValue.GetSnapshot(documentId).Tasks
                .Where(x => x != null && (x.State == SyncTaskState.Dirty ||
                    x.State == SyncTaskState.Error ||
                    x.State == SyncTaskState.Conflict)).ToList();
            if (tasks.Count == 0) return;
            bool requiresConfirmation = tasks.Count > 1 || tasks.Any(x =>
                x.Risk == SyncRiskLevel.ConfirmationRequired);
            if (!confirmed && requiresConfirmation)
            {
                PublishBatchDecision(documentId, tasks);
                return;
            }

            DismissBatchDecision(documentId);
            bool undoMarkStarted = tasks.Any(x =>
                x.Type == SyncTaskType.Annotation) && TrySetUndoMark(true);
            try
            {
                foreach (SyncTask task in tasks)
                    ExecuteTask(document, task, !undoMarkStarted);
            }
            finally
            {
                if (undoMarkStarted) TrySetUndoMark(false);
            }
        }

        public static void CancelBatch(Document document)
        {
            if (document == null) return;
            DismissBatchDecision(CadFloatingDocumentIdentity.GetDocumentId(
                document));
        }

        private static void AttributesChanged(object sender,
            QuantityAttributesChangedEventArgs e)
        {
            if (!CDBoxBusinessModeService.IsWastewater || e == null
                || e.Document == null) return;
            string documentId = CadFloatingDocumentIdentity.GetDocumentId(
                e.Document);
            List<string> original = NormalizeHandles(e.ObjectHandles);
            List<string> handles = FilterToDashboardScope(e.Document, original);
            if (original.Count > 0 && handles.Count == 0) return;
            if (e.AnnotationsRefreshed)
            {
                SyncTask completed = ManagerValue.RecordCompleted(new SyncTask
                {
                    DocumentId = documentId,
                    MergeKey = "annotation",
                    Source = SourceName,
                    Type = SyncTaskType.Annotation,
                    Title = "绑定标注已同步",
                    Summary = BuildAffectedSummary(handles,
                        "属性与绑定标注已同步"),
                    Reason = e.Reason,
                    ObjectHandles = handles,
                    ChangedProperties = new List<string> { "对象属性", "绑定标注" }
                }, "已随属性保存完成刷新");
                PublishCompletedHistory(completed);
            }
            else MarkAnnotationDirty(e.Document, handles, e.Reason);

            MarkCalculationDirty(e.Document, handles, e.Reason);
        }

        private static void DashboardDirtyMarked(object sender,
            QuantityDashboardDirtyEventArgs e)
        {
            if (!CDBoxBusinessModeService.IsWastewater || e == null
                || e.Document == null || _suppressQuantityDirty)
                return;
            if (string.Equals(e.Reason, "quantity-attribute-saved",
                StringComparison.OrdinalIgnoreCase)) return;
            MarkCalculationDirty(e.Document, e.ObjectHandles, e.Reason);
        }

        private static void MarkCalculationDirty(Document document,
            IEnumerable<string> handles, string reason)
        {
            if (!CDBoxBusinessModeService.IsWastewater) return;
            List<string> original = NormalizeHandles(handles);
            List<string> affected = FilterToDashboardScope(document, original);
            if (original.Count > 0 && affected.Count == 0) return;
            SyncTask task = ManagerValue.MarkDirty(new SyncTask
            {
                DocumentId = CadFloatingDocumentIdentity.GetDocumentId(document),
                MergeKey = "calculation",
                Source = SourceName,
                Type = SyncTaskType.Calculation,
                Title = "工程量结果需要同步",
                Summary = BuildAffectedSummary(affected,
                    "当前图纸的工程量结果需要刷新"),
                Reason = reason ?? string.Empty,
                ObjectHandles = affected,
                ChangedProperties = new List<string> { "工程量统计结果" },
                Risk = ResolveRisk(affected)
            });
            PublishTask(task);
            TryAutoSync(document, task);
        }

        private static void TryAutoSync(Document document, SyncTask task)
        {
            if (!CDBoxBusinessModeService.IsWastewater || document == null
                || task == null ||
                task.Risk != SyncRiskLevel.Safe) return;
            CDBoxStudioSettings settings = CDBoxStudioSettingsStore.Load();
            if (settings == null || !settings.FloatingCenterSafeAutoSyncEnabled)
                return;
            ExecuteTask(document, task);
        }

        private static void ExecuteTask(Document document, SyncTask task)
        {
            ExecuteTask(document, task, true);
        }

        private static void ExecuteTask(Document document, SyncTask task,
            bool manageUndoMark)
        {
            if (!CDBoxBusinessModeService.IsWastewater) return;
            string documentId = CadFloatingDocumentIdentity.GetDocumentId(document);
            SyncTask running = ManagerValue.Begin(documentId, task.Id);
            if (running == null) return;
            PublishTask(running);
            bool undoMarkStarted = false;
            try
            {
                if (task.Type == SyncTaskType.Annotation)
                {
                    List<string> scopedHandles = FilterToDashboardScope(
                        document, task.ObjectHandles);
                    undoMarkStarted = manageUndoMark && TrySetUndoMark(true);
                    SimpleAnnotationObjectService.RefreshNodeAnnotationsForSourceHandles(
                        document, scopedHandles);
                    PipeLengthAnnotationObjectService.RefreshBindingsForSourceHandles(
                        document, scopedHandles, string.Empty);
                }
                else if (task.Type == SyncTaskType.Calculation)
                {
                    _suppressQuantityDirty = true;
                    try { QuantityDashboardLiveMonitor.MarkDirty("sync-center"); }
                    finally { _suppressQuantityDirty = false; }
                }

                string result = task.Type == SyncTaskType.Calculation
                    ? "工程量结果刷新请求已完成。"
                    : "受影响的绑定标注已完成同步。";
                SyncTask completed = ManagerValue.Complete(documentId,
                    task.Id, result);
                FloatingHub.Current.Dismiss(documentId,
                    MessageId(task.Id));
                PublishCompletedHistory(completed);
            }
            catch (Exception ex)
            {
                SyncTask failed = ManagerValue.Fail(documentId, task.Id,
                    ex.Message, false);
                PublishTask(failed);
            }
            finally
            {
                if (undoMarkStarted) TrySetUndoMark(false);
            }
        }

        private static bool TrySetUndoMark(bool begin)
        {
            try { return Autodesk.AutoCAD.Internal.Utils.SetUndoMark(begin); }
            catch { return false; }
        }

        private static void PublishTask(SyncTask task)
        {
            if (task == null) return;
            bool running = task.State == SyncTaskState.Running;
            bool failed = task.State == SyncTaskState.Error ||
                task.State == SyncTaskState.Conflict;
            var actions = new List<FloatingAction>();
            if (task.ObjectHandles.Count > 0)
                actions.Add(new FloatingAction
                {
                    Id = "sync.view|" + task.Id,
                    Text = "查看影响"
                });
            if (!running)
                actions.Add(new FloatingAction
                {
                    Id = "sync.run|" + task.Id,
                    Text = failed ? "重试" : "立即同步",
                    IsPrimary = true
                });
            FloatingHub.Current.Publish(new FloatingMessage
            {
                Id = MessageId(task.Id),
                DocumentId = task.DocumentId,
                Source = SourceName,
                Kind = failed ? FloatingMessageKind.Warning :
                    FloatingMessageKind.Sync,
                Priority = failed ? FloatingMessagePriority.High :
                    FloatingMessagePriority.Normal,
                Title = task.Title,
                Summary = running ? "正在同步…" : task.Summary,
                Detail = BuildDetail(task),
                IsPersistent = true,
                RecordInHistory = false,
                PresentAsCard = !running,
                MergeKey = "sync-task:" + task.MergeKey,
                Actions = actions
            });
        }

        private static void PublishCompletedHistory(SyncTask task)
        {
            if (task == null) return;
            FloatingHub.Current.Publish(new FloatingMessage
            {
                DocumentId = task.DocumentId,
                Source = SourceName,
                Kind = FloatingMessageKind.Success,
                Priority = FloatingMessagePriority.Low,
                Title = task.Title,
                Summary = string.IsNullOrWhiteSpace(task.ResultMessage)
                    ? task.Summary : task.ResultMessage,
                Detail = BuildDetail(task),
                RecordInHistory = true,
                PresentAsCard = false,
                MergeKey = "sync-history:" + task.MergeKey
            });
        }

        private static void PublishBatchDecision(string documentId,
            IList<SyncTask> tasks)
        {
            int objects = tasks.SelectMany(x => x.ObjectHandles ??
                new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            FloatingHub.Current.Publish(new FloatingMessage
            {
                Id = BatchDecisionId(documentId),
                DocumentId = documentId,
                Source = SourceName,
                Kind = FloatingMessageKind.Decision,
                Priority = FloatingMessagePriority.High,
                Title = "确认批量同步",
                Summary = "将处理 " + tasks.Count + " 个同步任务" +
                    (objects > 0 ? "，影响 " + objects + " 个对象。" : "。"),
                Detail = "同步将作为本次集中操作执行；如涉及图形写入，可使用 CAD 撤销恢复。",
                IsPersistent = true,
                RequiresDecision = true,
                RecordInHistory = false,
                PresentAsCard = true,
                MergeKey = "sync-batch-decision",
                Actions = new List<FloatingAction>
                {
                    new FloatingAction { Id = "sync.confirm-all", Text = "确认同步", IsPrimary = true },
                    new FloatingAction { Id = "sync.cancel-all", Text = "取消" }
                }
            });
        }

        private static void DismissBatchDecision(string documentId)
        {
            FloatingHub.Current.Dismiss(documentId,
                BatchDecisionId(documentId));
        }

        private static string BuildDetail(SyncTask task)
        {
            string risk = task.Risk == SyncRiskLevel.Safe
                ? "安全范围" : "需要确认";
            string properties = task.ChangedProperties == null ||
                task.ChangedProperties.Count == 0 ? string.Empty :
                "\n同步内容：" + string.Join("、", task.ChangedProperties.ToArray());
            string reason = string.IsNullOrWhiteSpace(task.Reason)
                ? string.Empty : "\n触发原因：" + task.Reason;
            return "影响对象：" + task.AffectedObjectCount + " · " + risk +
                properties + reason;
        }

        private static string BuildAffectedSummary(IList<string> handles,
            string fallback)
        {
            return handles != null && handles.Count > 0
                ? "检测到 " + handles.Count + " 个受影响对象。"
                : fallback;
        }

        private static SyncRiskLevel ResolveRisk(IList<string> handles)
        {
            return handles == null || handles.Count == 0 ||
                handles.Count > SafeObjectLimit
                ? SyncRiskLevel.ConfirmationRequired : SyncRiskLevel.Safe;
        }

        private static List<string> NormalizeHandles(
            IEnumerable<string> handles)
        {
            return (handles ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<string> FilterToDashboardScope(Document document,
            IEnumerable<string> handles)
        {
            List<string> values = NormalizeHandles(handles);
            if (document == null || values.Count == 0) return values;
            try
            {
                return QuantityDashboardRegionService
                    .FilterHandlesToSavedScope(document, values);
            }
            catch { return values; }
        }

        private static string MessageId(string taskId)
        {
            return "sync-task:" + (taskId ?? string.Empty);
        }

        private static string BatchDecisionId(string documentId)
        {
            return "sync-batch-decision:" + (documentId ?? string.Empty);
        }

        private static void DocumentToBeDestroyed(object sender,
            DocumentCollectionEventArgs e)
        {
            Document document = e == null ? null : e.Document;
            if (document == null) return;
            ManagerValue.ClearDocument(
                CadFloatingDocumentIdentity.GetDocumentId(document));
        }

        private static void BusinessModeChanged(object sender,
            CDBoxBusinessModeChangedEventArgs e)
        {
            if (e != null && e.Mode == CDBoxBusinessMode.Wastewater) return;
            ManagerValue.ClearAll();
            try
            {
                foreach (Document document in AcadApp.DocumentManager)
                {
                    if (document == null) continue;
                    string documentId = CadFloatingDocumentIdentity
                        .GetDocumentId(document);
                    foreach (FloatingMessage message in FloatingHub.Current
                        .GetActiveMessages(documentId))
                    {
                        if (message == null || !string.Equals(message.Source,
                            SourceName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        try { FloatingHub.Current.Dismiss(documentId, message.Id); }
                        catch { }
                    }
                }
            }
            catch { }
        }
    }
}
