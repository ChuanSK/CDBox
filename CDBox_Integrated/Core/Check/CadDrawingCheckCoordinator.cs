using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using TCPipeAutoDraw.Core.FloatingCenter;
using TCPipeAutoDraw.Modules.LayerManager;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using FloatingHub = TCPipeAutoDraw.Core.FloatingCenter.FloatingCenter;

namespace TCPipeAutoDraw.Core.Check
{
    /// <summary>
    /// CAD 侧图纸检查协调器。扫描分批发生在 CAD UI 线程，规则计算只消费快照。
    /// </summary>
    internal static class CadDrawingCheckCoordinator
    {
        private const int BatchSize = 120;
        private const string SourceName = "DrawingCheck";
        private const string AnnotationRecordName = "CDBoxAnnotationSource";
        private const string SimpleAnnotationRecordName = "CDBoxSimpleAnnotation";
        private static readonly object Gate = new object();
        private static readonly DrawingCheckManager ManagerValue =
            new DrawingCheckManager();
        private static readonly Dictionary<string, ScanSession> Sessions =
            new Dictionary<string, ScanSession>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<string>> GroupMessageIds =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> AutomaticallyChecked =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<Document, string> DocumentIds =
            new Dictionary<Document, string>(new ReferenceComparer<Document>());
        private static DispatcherTimer _scanTimer;
        private static DispatcherTimer _highlightTimer;
        private static HighlightSession _highlight;
        private static bool _initialized;

        public static DrawingCheckManager Manager { get { return ManagerValue; } }

        public static IList<FloatingMessage> GetIgnoredMessages(
            Document document)
        {
            if (document == null) return new List<FloatingMessage>();
            string documentId = CadFloatingDocumentIdentity.GetDocumentId(document);
            List<DrawingCheckGroup> groups = GetPersistedIgnoredGroups(
                document, documentId);
            return groups
                .Select(group => BuildIgnoredMessage(documentId, group))
                .ToList();
        }

        public static void Ignore(Document document, string groupId)
        {
            if (document == null || string.IsNullOrWhiteSpace(groupId)) return;
            string documentId = CadFloatingDocumentIdentity.GetDocumentId(document);
            List<DrawingCheckIssue> changed = ManagerValue.IgnoreGroup(
                documentId, groupId);
            if (changed.Count == 0) return;
            DrawingCheckIgnoreStore.Add(GetDocumentKey(document), changed);
            DismissGroupMessage(documentId, groupId);
            PublishAudit(documentId, "已忽略检查问题",
                "该问题已转入“已忽略”清单，可随时恢复。");
        }

        public static void Restore(Document document, string groupId)
        {
            if (document == null || string.IsNullOrWhiteSpace(groupId)) return;
            string documentId = CadFloatingDocumentIdentity.GetDocumentId(document);
            List<DrawingCheckGroup> persisted = GetPersistedIgnoredGroups(
                document, documentId);
            DrawingCheckGroup persistedGroup = persisted.FirstOrDefault(x =>
                string.Equals(x.Id, groupId,
                    StringComparison.OrdinalIgnoreCase));
            List<DrawingCheckIssue> changed = ManagerValue.RestoreGroup(
                documentId, groupId);
            if (persistedGroup == null && changed.Count == 0) return;
            if (persistedGroup != null)
                DrawingCheckIgnoreStore.RemoveKeys(GetDocumentKey(document),
                    persistedGroup.IssueIds);
            else DrawingCheckIgnoreStore.Remove(GetDocumentKey(document),
                changed);
            if (changed.Count > 0)
            {
                DrawingCheckSnapshot snapshot = ManagerValue.GetSnapshot(
                    documentId);
                var changedIds = new HashSet<string>(changed.Select(x => x.Id),
                    StringComparer.OrdinalIgnoreCase);
                DrawingCheckGroup active = snapshot.Groups.FirstOrDefault(x =>
                    (x.IssueIds ?? new List<string>()).Any(changedIds.Contains));
                if (active != null)
                {
                    DismissGroupMessage(documentId, active.Id);
                    AppendPublishedGroups(documentId,
                        new List<DrawingCheckGroup> { active });
                }
            }
            else if (!IsRunning(document)) Start(document, true);
            PublishAudit(documentId, "已恢复检查问题",
                changed.Count > 0 ? "该问题已重新加入任务列表。" :
                    "已取消忽略，正在重新检查当前图纸。");
        }

        public static void Initialize()
        {
            lock (Gate)
            {
                if (_initialized) return;
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                _scanTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(28),
                    DispatcherPriority.Background, ScanTimerTick, dispatcher);
                _highlightTimer = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(300), DispatcherPriority.Background,
                    HighlightTimerTick, dispatcher);
                try
                {
                    AcadApp.DocumentManager.DocumentActivated += DocumentActivated;
                    AcadApp.DocumentManager.DocumentToBeDestroyed +=
                        DocumentToBeDestroyed;
                }
                catch { }
                _initialized = true;
            }
            QueueAutomaticCheck(AcadApp.DocumentManager.MdiActiveDocument);
        }

        public static void Terminate()
        {
            lock (Gate)
            {
                if (!_initialized) return;
                try
                {
                    AcadApp.DocumentManager.DocumentActivated -= DocumentActivated;
                    AcadApp.DocumentManager.DocumentToBeDestroyed -=
                        DocumentToBeDestroyed;
                }
                catch { }
                if (_scanTimer != null) _scanTimer.Stop();
                if (_highlightTimer != null) _highlightTimer.Stop();
                foreach (ScanSession session in Sessions.Values)
                    DisposeProgress(session);
                Sessions.Clear();
                ClearHighlightCore();
                GroupMessageIds.Clear();
                AutomaticallyChecked.Clear();
                DocumentIds.Clear();
                ManagerValue.ClearAll();
                _scanTimer = null;
                _highlightTimer = null;
                _initialized = false;
            }
        }

        public static void Start(Document document, bool userInitiated)
        {
            if (document == null) return;
            string documentId = CadFloatingDocumentIdentity.GetDocumentId(document);
            DocumentIds[document] = documentId;
            lock (Gate)
            {
                ScanSession running;
                if (Sessions.TryGetValue(documentId, out running))
                {
                    running.CancelRequested = true;
                    return;
                }
            }

            try
            {
                var session = new ScanSession
                {
                    Document = document,
                    DocumentId = documentId,
                    DocumentKey = GetDocumentKey(document),
                    UserInitiated = userInitiated,
                    StartedAt = DateTime.UtcNow
                };
                using (DocumentLock documentLock = document.LockDocument())
                using (Transaction tr = document.Database.TransactionManager
                    .StartOpenCloseTransaction())
                {
                    BlockTableRecord space = tr.GetObject(
                        document.Database.CurrentSpaceId, OpenMode.ForRead, false)
                        as BlockTableRecord;
                    if (space != null)
                        foreach (ObjectId id in space)
                            if (!id.IsNull && id.IsValid && !id.IsErased)
                                session.ObjectIds.Add(id);
                    tr.Commit();
                }

                ManagerValue.Begin(documentId, "正在读取图纸对象");
                session.Progress = FloatingHub.Current.BeginProgress(
                    new FloatingProgressSpec
                    {
                        DocumentId = documentId,
                        Source = SourceName,
                        Title = "图纸检查",
                        Message = "正在读取图纸对象",
                        MergeKey = "drawing-check-progress",
                        IsIndeterminate = session.ObjectIds.Count == 0,
                        CanCancel = true
                    });
                lock (Gate)
                {
                    Sessions[documentId] = session;
                    if (_scanTimer != null && !_scanTimer.IsEnabled)
                        _scanTimer.Start();
                }
                if (session.ObjectIds.Count == 0) ProcessNextBatch(session);
            }
            catch (System.Exception ex)
            {
                ManagerValue.Cancel(documentId);
                FloatingHub.Current.Publish(new FloatingMessage
                {
                    DocumentId = documentId,
                    Source = SourceName,
                    Kind = FloatingMessageKind.Error,
                    Title = "图纸检查失败",
                    Summary = ex.Message,
                    PresentAsCard = true,
                    RecordInHistory = true
                });
            }
        }

        public static void RequestCancel(Document document)
        {
            if (document == null) return;
            string id = CadFloatingDocumentIdentity.GetDocumentId(document);
            DocumentIds[document] = id;
            lock (Gate)
            {
                ScanSession session;
                if (Sessions.TryGetValue(id, out session))
                    session.CancelRequested = true;
            }
        }

        public static bool IsRunning(Document document)
        {
            if (document == null) return false;
            string id = CadFloatingDocumentIdentity.GetDocumentId(document);
            DocumentIds[document] = id;
            lock (Gate) return Sessions.ContainsKey(id);
        }

        public static void Locate(Document document, string groupId)
        {
            if (document == null || string.IsNullOrWhiteSpace(groupId)) return;
            string documentId = CadFloatingDocumentIdentity.GetDocumentId(document);
            DrawingCheckGroup group = ManagerValue.GetGroup(documentId, groupId);
            if (group == null) group = GetPersistedIgnoredGroups(document,
                documentId).FirstOrDefault(x => string.Equals(x.Id, groupId,
                    StringComparison.OrdinalIgnoreCase));
            if (group == null || group.ObjectHandles == null ||
                group.ObjectHandles.Count == 0)
            {
                FloatingHub.Current.Publish(new FloatingMessage
                {
                    DocumentId = documentId,
                    Source = SourceName,
                    Kind = FloatingMessageKind.Information,
                    Title = "无法定位",
                    Summary = "此问题没有可定位的图形对象。",
                    PresentAsCard = true,
                    RecordInHistory = true
                });
                return;
            }

            LocateHandles(document, group.ObjectHandles, "检查问题对象");
        }

        internal static void LocateHandles(Document document,
            IEnumerable<string> objectHandles, string title)
        {
            if (document == null || objectHandles == null) return;
            List<string> handles = objectHandles
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            string documentId = CadFloatingDocumentIdentity.GetDocumentId(
                document);
            if (handles.Count == 0) return;

            try
            {
                ClearHighlightCore();
                List<ObjectId> ids = ResolveHandles(document.Database,
                    handles);
                if (ids.Count == 0) throw new InvalidOperationException(
                    "相关对象已被删除或不在当前图纸中。");
                Extents3d? total = null;
                using (DocumentLock documentLock = document.LockDocument())
                using (Transaction tr = document.Database.TransactionManager
                    .StartOpenCloseTransaction())
                {
                    foreach (ObjectId id in ids)
                    {
                        Entity entity;
                        try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                        catch { continue; }
                        if (entity == null || entity.IsErased) continue;
                        try { entity.Highlight(); } catch { }
                        try
                        {
                            Extents3d extents = entity.GeometricExtents;
                            if (!total.HasValue) total = extents;
                            else
                            {
                                Extents3d merged = total.Value;
                                merged.AddExtents(extents);
                                total = merged;
                            }
                        }
                        catch { }
                    }
                    tr.Commit();
                }
                document.Editor.SetImpliedSelection(ids.ToArray());
                if (total.HasValue) ZoomTo(document.Editor, total.Value);
                _highlight = new HighlightSession
                {
                    Document = document,
                    Handles = handles,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(4)
                };
                if (_highlightTimer != null && !_highlightTimer.IsEnabled)
                    _highlightTimer.Start();
            }
            catch (System.Exception ex)
            {
                FloatingHub.Current.Publish(new FloatingMessage
                {
                    DocumentId = documentId,
                    Source = SourceName,
                    Kind = FloatingMessageKind.Warning,
                    Title = string.IsNullOrWhiteSpace(title)
                        ? "定位失败" : title + "定位失败",
                    Summary = ex.Message,
                    PresentAsCard = true,
                    RecordInHistory = true
                });
            }
        }

        private static void ScanTimerTick(object sender, EventArgs e)
        {
            ScanSession session;
            lock (Gate)
            {
                session = Sessions.Values.OrderBy(x => x.StartedAt)
                    .FirstOrDefault();
                if (session == null)
                {
                    if (_scanTimer != null) _scanTimer.Stop();
                    return;
                }
            }
            ProcessNextBatch(session);
        }

        private static void ProcessNextBatch(ScanSession session)
        {
            if (session == null) return;
            if (session.CancelRequested || session.Document == null)
            {
                FinishCancelled(session);
                return;
            }
            try
            {
                int end = Math.Min(session.ObjectIds.Count,
                    session.Index + BatchSize);
                if (session.Index < end)
                {
                    using (DocumentLock documentLock = session.Document.LockDocument())
                    using (Transaction tr = session.Document.Database
                        .TransactionManager.StartOpenCloseTransaction())
                    {
                        while (session.Index < end)
                        {
                            ObjectId id = session.ObjectIds[session.Index++];
                            ReadObject(session, tr, id);
                        }
                        tr.Commit();
                    }
                }

                double progress = session.ObjectIds.Count == 0 ? 100.0 :
                    session.Index * 100.0 / session.ObjectIds.Count;
                string stage = session.Index >= session.ObjectIds.Count
                    ? "正在汇总检查结果" : "正在检查对象 " + session.Index
                        + " / " + session.ObjectIds.Count;
                ManagerValue.Report(session.DocumentId, progress, stage);
                if (session.Progress != null)
                    session.Progress.Report(progress, stage);
                if (session.Index < session.ObjectIds.Count) return;

                List<DrawingCheckIssue> issues = DrawingCheckRuleEvaluator.Evaluate(
                    session.DocumentId, session.Layers.Values,
                    session.Objects, session.Annotations.Values);
                HashSet<string> ignored = DrawingCheckIgnoreStore.LoadKeys(
                    session.DocumentKey);
                foreach (DrawingCheckIssue issue in issues)
                    if (issue != null && ignored.Contains(
                        issue.IgnoreKey ?? string.Empty))
                        issue.Status = DrawingCheckIssueStatus.Ignored;
                ManagerValue.Complete(session.DocumentId, issues);
                DrawingCheckSnapshot result = ManagerValue.GetSnapshot(
                    session.DocumentId);
                DisposeProgress(session);
                PublishGroups(session, result.Groups);
                AutomaticallyChecked.Add(session.DocumentId);
                RemoveSession(session.DocumentId);
            }
            catch (System.Exception ex)
            {
                DisposeProgress(session);
                ManagerValue.Cancel(session.DocumentId);
                RemoveSession(session.DocumentId);
                FloatingHub.Current.Publish(new FloatingMessage
                {
                    DocumentId = session.DocumentId,
                    Source = SourceName,
                    Kind = FloatingMessageKind.Error,
                    Title = "图纸检查失败",
                    Summary = ex.Message,
                    PresentAsCard = true,
                    RecordInHistory = true
                });
            }
        }

        private static void ReadObject(ScanSession session, Transaction tr,
            ObjectId id)
        {
            Entity entity;
            try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
            catch { return; }
            if (entity == null || entity.IsErased) return;
            string handle = entity.Handle.ToString();
            string layerName = entity.Layer ?? string.Empty;
            LayerContext layer = GetLayerContext(session, tr, layerName);
            QuantityPipeAttributes saved;
            bool hasSaved = QuantityPipeAttributeService.TryReadSavedAttributes(
                session.Document.Database, tr, id, out saved);
            string expected = NormalizeKind(layer.Metadata.ParentGroup);
            string suggested = NormalizeKind(layer.SuggestedParent);
            string actual = hasSaved && saved != null
                ? NormalizeKind(saved.ObjectKind)
                : InferActualKind(expected, entity);
            bool relevant = hasSaved ||
                !string.IsNullOrWhiteSpace(layer.Metadata.ParentGroup) ||
                suggested.Length > 0;
            if (relevant)
            {
                layer.Snapshot.ObjectHandles.Add(handle);
                var item = new DrawingCheckObjectSnapshot
                {
                    Handle = handle,
                    LayerName = layerName,
                    EntityType = entity.GetType().Name,
                    LayerParentGroup = layer.Metadata.ParentGroup,
                    LayerParentClass = layer.Metadata.ParentClass,
                    ObjectKind = actual,
                    HasSavedAttributes = hasSaved,
                    IsSpecialObject = saved != null && saved.IsSpecialObject
                };
                if (saved != null) ApplyAttributes(item, saved);
                ApplyGeometry(item, entity);
                session.Objects.Add(item);
            }
            ReadAnnotation(session, tr, entity, handle);
        }

        private static LayerContext GetLayerContext(ScanSession session,
            Transaction tr, string layerName)
        {
            LayerContext value;
            if (session.LayerContexts.TryGetValue(layerName, out value))
                return value;
            LayerMetadata metadata = LayerManagerService.GetLayerMetadata(
                session.Document.Database, tr, layerName) ?? new LayerMetadata();
            string suggested = string.Empty;
            try
            {
                LayerRecognitionResult recognition =
                    LayerManagerService.RecognizeLayerName(layerName);
                if (recognition != null && recognition.Metadata != null)
                    suggested = recognition.Metadata.ParentGroup ?? string.Empty;
            }
            catch { }
            value = new LayerContext
            {
                Metadata = metadata,
                SuggestedParent = suggested,
                Snapshot = new DrawingCheckLayerSnapshot
                {
                    LayerName = layerName,
                    ParentGroup = metadata.ParentGroup,
                    ParentClass = metadata.ParentClass
                }
            };
            session.LayerContexts[layerName] = value;
            session.Layers[layerName] = value.Snapshot;
            return value;
        }

        private static void ApplyAttributes(DrawingCheckObjectSnapshot target,
            QuantityPipeAttributes source)
        {
            target.ObjectKind = source.ObjectKind;
            target.IsSpecialObject = source.IsSpecialObject;
            target.Material = source.Material;
            target.Diameter = source.Diameter;
            target.StartNode = source.StartNode;
            target.EndNode = source.EndNode;
            target.AverageDepth = source.AverageDepth;
            target.BackfillStructure = source.BackfillStructure;
            target.PipeOuterDiameter = source.PipeOuterDiameter;
            target.PipeLayerBelowDiameter = source.PipeOuterDiameter > 0 &&
                QuantityStructureLayer.Parse(source.BackfillStructure)
                    .Any(x => x != null && x.IsPipeLayer &&
                        x.Height + 0.0000001 < source.PipeOuterDiameter);
            target.BranchType = source.BranchType;
            target.BranchIncludeInCalculation = source.BranchIncludeInCalculation;
            target.BranchDepth = source.BranchDepth;
            target.NodeNo = source.NodeNo;
            target.WellSpec = source.WellSpec;
            target.WellCoverMaterial = source.WellCoverMaterial;
            target.WellMaterialType = source.WellMaterialType;
            target.WellType = source.WellType;
            target.WellDepth = source.WellDepth;
        }

        private static void ApplyGeometry(DrawingCheckObjectSnapshot target,
            Entity entity)
        {
            Curve curve = entity as Curve;
            if (curve != null)
            {
                try
                {
                    Point3d point = curve.StartPoint;
                    target.HasStartPoint = true;
                    target.StartX = point.X;
                    target.StartY = point.Y;
                }
                catch { }
                try
                {
                    Point3d point = curve.EndPoint;
                    target.HasEndPoint = true;
                    target.EndX = point.X;
                    target.EndY = point.Y;
                }
                catch { }
            }
            Point3d position;
            if (!TryGetNodePosition(entity, out position)) return;
            target.HasNodePosition = true;
            target.NodeX = position.X;
            target.NodeY = position.Y;
            target.NodeConnectionTolerance = 0.05;
            try
            {
                Extents3d extents = entity.GeometricExtents;
                double dx = extents.MaxPoint.X - extents.MinPoint.X;
                double dy = extents.MaxPoint.Y - extents.MinPoint.Y;
                target.NodeConnectionTolerance = Math.Max(0.05,
                    Math.Sqrt(dx * dx + dy * dy) * 0.55);
            }
            catch { }
        }

        private static bool TryGetNodePosition(Entity entity,
            out Point3d position)
        {
            Circle circle = entity as Circle;
            if (circle != null) { position = circle.Center; return true; }
            DBPoint point = entity as DBPoint;
            if (point != null) { position = point.Position; return true; }
            BlockReference block = entity as BlockReference;
            if (block != null) { position = block.Position; return true; }
            position = Point3d.Origin;
            return false;
        }

        private static void ReadAnnotation(ScanSession session, Transaction tr,
            Entity entity, string handle)
        {
            ReadAnnotationRecord(session, tr, entity, handle,
                AnnotationRecordName, string.Empty);
            ReadAnnotationRecord(session, tr, entity, handle,
                SimpleAnnotationRecordName, "Bound");
        }

        private static void ReadAnnotationRecord(ScanSession session,
            Transaction tr, Entity entity, string handle, string recordName,
            string defaultBindingState)
        {
            Dictionary<string, string> values;
            if (!TryReadRecord(tr, entity, recordName, out values))
                return;
            string annotationId = GetValue(values, "AnnotationId");
            if (string.IsNullOrWhiteSpace(annotationId) ||
                session.Annotations.ContainsKey(annotationId)) return;
            string sourceHandle = GetValue(values, "SourceHandle");
            string bindingState = GetValue(values, "BindingState");
            if (string.IsNullOrWhiteSpace(bindingState))
                bindingState = defaultBindingState;
            session.Annotations[annotationId] =
                new DrawingCheckAnnotationSnapshot
                {
                    AnnotationId = annotationId,
                    AnnotationHandle = handle,
                    SourceHandle = sourceHandle,
                    BindingState = bindingState,
                    SourceExists = HandleExists(session.Document.Database,
                        sourceHandle)
                };
        }

        private static bool TryReadRecord(Transaction tr, DBObject owner,
            string recordName, out Dictionary<string, string> values)
        {
            values = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            if (tr == null || owner == null || owner.ExtensionDictionary.IsNull)
                return false;
            try
            {
                DBDictionary dictionary = tr.GetObject(owner.ExtensionDictionary,
                    OpenMode.ForRead, false) as DBDictionary;
                if (dictionary == null || !dictionary.Contains(recordName))
                    return false;
                Xrecord record = tr.GetObject(dictionary.GetAt(recordName),
                    OpenMode.ForRead, false) as Xrecord;
                if (record == null || record.Data == null) return false;
                foreach (TypedValue typed in record.Data)
                {
                    if (typed.TypeCode != (int)DxfCode.Text ||
                        typed.Value == null) continue;
                    string text = Convert.ToString(typed.Value,
                        CultureInfo.InvariantCulture) ?? string.Empty;
                    int equals = text.IndexOf('=');
                    if (equals > 0)
                        values[text.Substring(0, equals)] =
                            text.Substring(equals + 1);
                }
                return values.Count > 0;
            }
            catch { return false; }
        }

        private static string GetValue(IDictionary<string, string> values,
            string key)
        {
            string value;
            return values != null && values.TryGetValue(key, out value)
                ? value ?? string.Empty : string.Empty;
        }

        private static bool HandleExists(Database database, string text)
        {
            long raw;
            if (database == null || !long.TryParse((text ?? string.Empty).Trim(),
                NumberStyles.HexNumber, CultureInfo.InvariantCulture, out raw))
                return false;
            try
            {
                ObjectId id = database.GetObjectId(false, new Handle(raw), 0);
                return !id.IsNull && id.IsValid && !id.IsErased;
            }
            catch { return false; }
        }

        private static List<ObjectId> ResolveHandles(Database database,
            IEnumerable<string> handles)
        {
            var result = new List<ObjectId>();
            foreach (string handle in handles ?? Enumerable.Empty<string>())
            {
                long raw;
                if (!long.TryParse((handle ?? string.Empty).Trim(),
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                    out raw)) continue;
                try
                {
                    ObjectId id = database.GetObjectId(false, new Handle(raw), 0);
                    if (!id.IsNull && id.IsValid && !id.IsErased)
                        result.Add(id);
                }
                catch { }
            }
            return result.Distinct().ToList();
        }

        private static string InferActualKind(string expected, Entity entity)
        {
            if ((expected == "主管" || expected == "支管") && entity is Curve)
                return expected;
            if (expected == "井" && (entity is Circle || entity is DBPoint ||
                entity is BlockReference)) return expected;
            return string.Empty;
        }

        private static string NormalizeKind(string value)
        {
            string text = (value ?? string.Empty).Replace(" ", string.Empty)
                .Replace("　", string.Empty).Replace("/", string.Empty)
                .Replace("、", string.Empty);
            if (text.IndexOf("支", StringComparison.CurrentCultureIgnoreCase) >= 0)
                return "支管";
            if (text.IndexOf("井", StringComparison.CurrentCultureIgnoreCase) >= 0)
                return "井";
            if (text.IndexOf("主管", StringComparison.CurrentCultureIgnoreCase) >= 0)
                return "主管";
            return string.Empty;
        }

        private static void PublishGroups(ScanSession session,
            IList<DrawingCheckGroup> groups)
        {
            DismissPreviousGroups(session.DocumentId);
            List<string> messageIds = PublishActiveGroupMessages(
                session.DocumentId, groups);
            GroupMessageIds[session.DocumentId] = messageIds;
            if ((groups == null || groups.Count == 0) && session.UserInitiated)
                FloatingHub.Current.Publish(new FloatingMessage
                {
                    DocumentId = session.DocumentId,
                    Source = SourceName,
                    Kind = FloatingMessageKind.Success,
                    Title = "图纸检查完成",
                    Summary = "未发现需要处理的问题。",
                    PresentAsCard = true,
                    RecordInHistory = true,
                    MergeKey = "drawing-check-clean"
                });
        }

        private static List<string> PublishActiveGroupMessages(
            string documentId, IList<DrawingCheckGroup> groups)
        {
            var messageIds = new List<string>();
            foreach (DrawingCheckGroup group in groups ??
                new List<DrawingCheckGroup>())
            {
                bool severe = group.Severity == DrawingCheckSeverity.Error ||
                    group.Severity == DrawingCheckSeverity.Critical;
                FloatingMessage stored = FloatingHub.Current.Publish(
                    new FloatingMessage
                    {
                        DocumentId = documentId,
                        Source = SourceName,
                        Kind = severe ? FloatingMessageKind.Error :
                            FloatingMessageKind.Warning,
                        Priority = severe ? FloatingMessagePriority.Critical :
                            FloatingMessagePriority.High,
                        Title = group.Title,
                        Summary = group.Summary,
                        Detail = group.Category,
                        IsPersistent = true,
                        PresentAsCard = severe,
                        RecordInHistory = false,
                        MergeKey = "drawing-check-group:" + group.Id,
                        Actions = new List<FloatingAction>
                        {
                            new FloatingAction
                            {
                                Id = "drawing-check.locate|" + group.Id,
                                Text = "定位对象",
                                IsPrimary = true
                            },
                            new FloatingAction
                            {
                                Id = "drawing-check.ignore|" + group.Id,
                                Text = "忽略"
                            },
                            new FloatingAction
                            {
                                Id = "drawing-check.refresh",
                                Text = "重新检查"
                            }
                        }
                    });
                if (stored != null && !string.IsNullOrWhiteSpace(stored.Id))
                    messageIds.Add(stored.Id);
            }
            return messageIds;
        }

        private static void AppendPublishedGroups(string documentId,
            IList<DrawingCheckGroup> groups)
        {
            List<string> existing;
            if (!GroupMessageIds.TryGetValue(documentId, out existing))
                existing = new List<string>();
            existing.AddRange(PublishActiveGroupMessages(documentId, groups));
            GroupMessageIds[documentId] = existing.Distinct(
                StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void DismissGroupMessage(string documentId,
            string groupId)
        {
            string mergeKey = "drawing-check-group:" + groupId;
            IList<FloatingMessage> active = FloatingHub.Current
                .GetActiveMessages(documentId);
            foreach (FloatingMessage message in active)
            {
                if (message == null || !string.Equals(message.MergeKey,
                    mergeKey, StringComparison.OrdinalIgnoreCase)) continue;
                FloatingHub.Current.Dismiss(documentId, message.Id);
                List<string> ids;
                if (GroupMessageIds.TryGetValue(documentId, out ids))
                    ids.RemoveAll(x => string.Equals(x, message.Id,
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        private static FloatingMessage BuildIgnoredMessage(string documentId,
            DrawingCheckGroup group)
        {
            bool severe = group != null && (group.Severity ==
                DrawingCheckSeverity.Error || group.Severity ==
                DrawingCheckSeverity.Critical);
            return new FloatingMessage
            {
                Id = "ignored:" + (group == null ? string.Empty : group.Id),
                DocumentId = documentId,
                Source = SourceName,
                Kind = severe ? FloatingMessageKind.Error :
                    FloatingMessageKind.Warning,
                Priority = FloatingMessagePriority.Low,
                Title = group == null ? "已忽略问题" : group.Title,
                Summary = group == null ? string.Empty : group.Summary,
                Detail = group == null ? string.Empty : group.Category,
                UpdatedAt = group == null ? DateTime.UtcNow : group.UpdatedAt,
                Actions = group == null ? new List<FloatingAction>() :
                    new List<FloatingAction>
                    {
                        new FloatingAction
                        {
                            Id = "drawing-check.locate|" + group.Id,
                            Text = "定位对象"
                        },
                        new FloatingAction
                        {
                            Id = "drawing-check.restore|" + group.Id,
                            Text = "恢复",
                            IsPrimary = true
                        }
                    }
            };
        }

        private static void PublishAudit(string documentId, string title,
            string summary)
        {
            FloatingHub.Current.Publish(new FloatingMessage
            {
                DocumentId = documentId,
                Source = SourceName,
                Kind = FloatingMessageKind.Information,
                Priority = FloatingMessagePriority.Low,
                Title = title,
                Summary = summary,
                PresentAsCard = false,
                RecordInHistory = true,
                MergeKey = "drawing-check-audit:" + title
            });
        }

        private static List<DrawingCheckGroup> GetPersistedIgnoredGroups(
            Document document, string documentId)
        {
            List<DrawingCheckIssue> issues = DrawingCheckIgnoreStore.LoadIssues(
                GetDocumentKey(document), documentId);
            return DrawingCheckRuleEvaluator.Group(documentId, issues,
                DrawingCheckIssueStatus.Ignored);
        }

        private static void DismissPreviousGroups(string documentId)
        {
            List<string> ids;
            if (!GroupMessageIds.TryGetValue(documentId, out ids)) return;
            foreach (string id in ids)
                try { FloatingHub.Current.Dismiss(documentId, id); }
                catch { }
            GroupMessageIds.Remove(documentId);
        }

        private static void FinishCancelled(ScanSession session)
        {
            DisposeProgress(session);
            ManagerValue.Cancel(session.DocumentId);
            RemoveSession(session.DocumentId);
        }

        private static void DisposeProgress(ScanSession session)
        {
            if (session == null || session.Progress == null) return;
            try { session.Progress.Dispose(); } catch { }
            session.Progress = null;
        }

        private static void RemoveSession(string documentId)
        {
            lock (Gate)
            {
                Sessions.Remove(documentId);
                if (Sessions.Count == 0 && _scanTimer != null)
                    _scanTimer.Stop();
            }
        }

        private static void QueueAutomaticCheck(Document document)
        {
            if (document == null || !CDBoxStudioSettingsStore.Load()
                .FloatingCenterAutoCheckEnabled) return;
            string id = CadFloatingDocumentIdentity.GetDocumentId(document);
            if (AutomaticallyChecked.Contains(id) || IsRunning(document)) return;
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate
            {
                if (document == AcadApp.DocumentManager.MdiActiveDocument)
                    Start(document, false);
            }), DispatcherPriority.ApplicationIdle);
        }

        private static void DocumentActivated(object sender,
            DocumentCollectionEventArgs e)
        {
            QueueAutomaticCheck(e == null ? null : e.Document);
        }

        private static void DocumentToBeDestroyed(object sender,
            DocumentCollectionEventArgs e)
        {
            Document document = e == null ? null : e.Document;
            if (document == null) return;
            string id;
            lock (Gate)
                if (!DocumentIds.TryGetValue(document, out id))
                    id = Sessions.Values.Where(x => ReferenceEquals(
                            x.Document, document)).Select(x => x.DocumentId)
                        .FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id)) return;
            lock (Gate)
            {
                ScanSession session;
                if (Sessions.TryGetValue(id, out session))
                {
                    session.CancelRequested = true;
                    DisposeProgress(session);
                    Sessions.Remove(id);
                }
                AutomaticallyChecked.Remove(id);
                GroupMessageIds.Remove(id);
                DocumentIds.Remove(document);
            }
            ManagerValue.ClearDocument(id);
            if (_highlight != null && ReferenceEquals(_highlight.Document,
                document)) ClearHighlightCore();
        }

        private static void HighlightTimerTick(object sender, EventArgs e)
        {
            if (_highlight == null || DateTime.UtcNow < _highlight.ExpiresAt)
                return;
            ClearHighlightCore();
        }

        private static void ClearHighlightCore()
        {
            HighlightSession previous = _highlight;
            _highlight = null;
            if (_highlightTimer != null) _highlightTimer.Stop();
            if (previous == null || previous.Document == null) return;
            try
            {
                List<ObjectId> ids = ResolveHandles(previous.Document.Database,
                    previous.Handles);
                using (DocumentLock documentLock = previous.Document.LockDocument())
                using (Transaction tr = previous.Document.Database
                    .TransactionManager.StartOpenCloseTransaction())
                {
                    foreach (ObjectId id in ids)
                    {
                        Entity entity;
                        try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                        catch { continue; }
                        if (entity != null) try { entity.Unhighlight(); } catch { }
                    }
                    tr.Commit();
                }
            }
            catch { }
        }

        private static void ZoomTo(Editor editor, Extents3d extents)
        {
            if (editor == null) return;
            double width = Math.Max(1.0,
                (extents.MaxPoint.X - extents.MinPoint.X) * 1.25);
            double height = Math.Max(1.0,
                (extents.MaxPoint.Y - extents.MinPoint.Y) * 1.25);
            using (ViewTableRecord view = editor.GetCurrentView())
            {
                view.CenterPoint = new Point2d(
                    (extents.MinPoint.X + extents.MaxPoint.X) / 2.0,
                    (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0);
                view.Width = width;
                view.Height = height;
                editor.SetCurrentView(view);
            }
        }

        private static string GetDocumentKey(Document document)
        {
            if (document == null) return string.Empty;
            try
            {
                string fingerprint = Convert.ToString(
                    document.Database.FingerprintGuid,
                    CultureInfo.InvariantCulture) ?? string.Empty;
                fingerprint = fingerprint.Replace("-", string.Empty).Trim();
                if (fingerprint.Length > 0)
                    return "dwg:" + fingerprint.ToLowerInvariant();
            }
            catch { }
            try
            {
                string name = (document.Name ?? string.Empty).Trim();
                if (name.Length > 0) return "name:" + name.ToLowerInvariant();
            }
            catch { }
            return CadFloatingDocumentIdentity.GetDocumentId(document);
        }

        private sealed class ScanSession
        {
            public Document Document;
            public string DocumentId;
            public string DocumentKey;
            public bool UserInitiated;
            public DateTime StartedAt;
            public int Index;
            public bool CancelRequested;
            public IProgressHandle Progress;
            public readonly List<ObjectId> ObjectIds = new List<ObjectId>();
            public readonly List<DrawingCheckObjectSnapshot> Objects =
                new List<DrawingCheckObjectSnapshot>();
            public readonly Dictionary<string, DrawingCheckLayerSnapshot> Layers =
                new Dictionary<string, DrawingCheckLayerSnapshot>(
                    StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, LayerContext> LayerContexts =
                new Dictionary<string, LayerContext>(
                    StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, DrawingCheckAnnotationSnapshot>
                Annotations = new Dictionary<string,
                    DrawingCheckAnnotationSnapshot>(
                        StringComparer.OrdinalIgnoreCase);
        }

        private sealed class LayerContext
        {
            public LayerMetadata Metadata;
            public string SuggestedParent;
            public DrawingCheckLayerSnapshot Snapshot;
        }

        private sealed class HighlightSession
        {
            public Document Document;
            public List<string> Handles;
            public DateTime ExpiresAt;
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public bool Equals(T x, T y) { return ReferenceEquals(x, y); }
            public int GetHashCode(T obj)
            {
                return obj == null ? 0 :
                    System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
