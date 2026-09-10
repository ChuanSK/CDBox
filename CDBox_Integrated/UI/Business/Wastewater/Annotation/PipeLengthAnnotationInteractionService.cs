extern alias WastewaterBusiness;
using PipeLengthAnnotationCloneSafetyPolicy = WastewaterBusiness::TCPipeAutoDraw.Modules.PipeLengthAnnotation.PipeLengthAnnotationCloneSafetyPolicy;
using PipeLengthAnnotationEditModel = WastewaterBusiness::TCPipeAutoDraw.Modules.PipeLengthAnnotation.PipeLengthAnnotationEditModel;
using PipeLengthAnnotationGripOverrule = WastewaterBusiness::TCPipeAutoDraw.Modules.PipeLengthAnnotation.PipeLengthAnnotationGripOverrule;
using PipeLengthAnnotationObjectService = WastewaterBusiness::TCPipeAutoDraw.Modules.PipeLengthAnnotation.PipeLengthAnnotationObjectService;
using QuantityAttributeDoubleClickService = WastewaterBusiness::TCPipeAutoDraw.Modules.QuantityCalculation.QuantityAttributeDoubleClickService;
using QuantityPipeAttributeService = WastewaterBusiness::TCPipeAutoDraw.Modules.QuantityCalculation.QuantityPipeAttributeService;
using SimpleAnnotationObjectService = WastewaterBusiness::TCPipeAutoDraw.Modules.AnnotationHud.SimpleAnnotationObjectService;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using TCPipeAutoDraw.Modules.AnnotationHud;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    /// <summary>
    /// Owns the contextual annotation HUD. A single click remains entirely native
    /// AutoCAD interaction; only a left-button double click opens the HUD.
    /// </summary>
    public static class PipeLengthAnnotationInteractionService
    {
        private const int WmLeftButtonDoubleClick = 0x0203;
        private static readonly HashSet<string> DirtySourceHandles =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<CloneBatch> PendingCloneBatches =
            new List<CloneBatch>();
        private static bool _initialized;
        private static bool _spatialEditActive;
        private static bool _restoreAfterSpatialEdit;
        private static bool _bindingRefreshActive;
        private static bool _synchronizingAnnotationSelection;
        private static bool _selectionSyncPending;
        private static bool _documentSaveActive;
        private static bool _doubleClickOpenEnabled = true;
        private static int _spatialEditCompletionVersion;
        private static PipeLengthAnnotationCardWindow _window;
        private static Document _document;
        private static PipeLengthAnnotationEditModel _model;

        public static void Initialize()
        {
            if (_initialized) return;
            RefreshSettingsCache();
            AcadApp.PreTranslateMessage += PreTranslateMessage;
            AcadApp.DocumentManager.DocumentActivated += DocumentActivated;
            PipeLengthAnnotationGripOverrule.Initialize();
            AttachDocument(AcadApp.DocumentManager.MdiActiveDocument);
            _initialized = true;
        }

        public static void Terminate()
        {
            if (!_initialized) return;
            AcadApp.PreTranslateMessage -= PreTranslateMessage;
            AcadApp.DocumentManager.DocumentActivated -= DocumentActivated;
            PipeLengthAnnotationGripOverrule.Terminate();
            _initialized = false;
            CloseWindow();
            SimpleAnnotationHudInteractionService.CloseWindow();
            DetachDocument();
        }

        internal static void NotifySpatialEditStarted(Document doc, string annotationId)
        {
            PipeLengthAnnotationGripOverrule.SuspendSelectedMarker(doc, annotationId);
            if (doc == null || doc != _document) return;
            _spatialEditCompletionVersion++;
            _spatialEditActive = true;
            if (_window == null || _model == null
                || !string.Equals(_model.AnnotationId, annotationId,
                    StringComparison.OrdinalIgnoreCase)) return;
            _restoreAfterSpatialEdit = _restoreAfterSpatialEdit
                || _window.IsVisible || _window.IsClosingAnimation;
            if (_window.IsVisible) _window.HideAnimated(ResolveAnimationOrigin(doc, _model));
        }

        internal static void NotifySpatialEditFinished(Document doc, string annotationId)
        {
            PipeLengthAnnotationGripOverrule.ResumeSelectedMarker(doc, annotationId);
            if (doc == null || doc != _document) return;
            int completionVersion = ++_spatialEditCompletionVersion;
            try
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(delegate
                {
                    if (_document != doc || completionVersion != _spatialEditCompletionVersion) return;
                    try
                    {
                        RestoreAnnotationSelection(doc, annotationId);
                        if (!_restoreAfterSpatialEdit || _window == null || _model == null
                            || !string.Equals(_model.AnnotationId, annotationId,
                                StringComparison.OrdinalIgnoreCase)) return;

                        PipeLengthAnnotationEditModel refreshed =
                            PipeLengthAnnotationObjectService.LoadEditModel(doc, _model.SelectedObjectId);
                        if (refreshed == null) return;
                        _model = refreshed;
                        _window.SetModel(refreshed);
                        _window.ShowAnimated(ResolveAnimationOrigin(doc, refreshed), null, null);
                    }
                    catch { }
                    finally
                    {
                        _restoreAfterSpatialEdit = false;
                        _spatialEditActive = false;
                    }
                }));
            }
            catch
            {
                _spatialEditActive = false;
                _restoreAfterSpatialEdit = false;
            }
        }

        private static void RestoreAnnotationSelection(Document doc, string annotationId)
        {
            if (doc == null || string.IsNullOrWhiteSpace(annotationId)) return;
            try
            {
                ObjectId[] ids = PipeLengthAnnotationObjectService.GetAnnotationObjectIds(doc, annotationId);
                if (ids.Length == 0) return;
                _synchronizingAnnotationSelection = true;
                try { doc.Editor.SetImpliedSelection(ids); }
                finally { _synchronizingAnnotationSelection = false; }
                PipeLengthAnnotationGripOverrule.SetSelectedAnnotation(doc, annotationId);
            }
            catch { _synchronizingAnnotationSelection = false; }
        }

        internal static void RefreshCard(Document doc, ObjectId annotationObjectId)
        {
            if (_window == null || doc == null || doc != _document) return;
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
            {
                try
                {
                    PipeLengthAnnotationEditModel model =
                        PipeLengthAnnotationObjectService.LoadEditModel(doc, annotationObjectId);
                    if (model == null) return;
                    _model = model;
                    _window.SetModel(model);
                    _window.SetAnimationOrigin(ResolveAnimationOrigin(doc, model));
                }
                catch { }
            }));
        }

        private static void PreTranslateMessage(object sender, PreTranslateMessageEventArgs e)
        {
            if (e == null || e.Message.message != WmLeftButtonDoubleClick) return;
            if (!_doubleClickOpenEnabled) return;
            if (_window != null && _window.IsVisible)
            {
                try { if (new WindowInteropHelper(_window).Handle == e.Message.hwnd) return; }
                catch { }
            }
            if (SimpleAnnotationHudInteractionService.IsOwnWindowHandle(e.Message.hwnd)) return;

            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            ObjectId[] selectedIds = ReadImpliedSelection(doc);
            if (selectedIds.Length == 0) return;

            ObjectId annotationObjectId;
            if (!PipeLengthAnnotationObjectService.TryResolveAnnotationObject(
                doc, selectedIds, out annotationObjectId))
            {
                ObjectId simpleObjectId;
                if (SimpleAnnotationObjectService.TryResolveAnnotationObject(doc, selectedIds,
                    out simpleObjectId))
                {
                    e.Handled = true;
                    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(delegate
                        {
                            CloseWindow();
                            SimpleAnnotationHudInteractionService.TryOpen(doc, selectedIds);
                        }));
                    return;
                }

                if (QuantityAttributeDoubleClickService.CanOpen(doc, selectedIds))
                {
                    e.Handled = true;
                    Point screenPoint = GetDoubleClickScreenPoint(e.Message);
                    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(delegate
                        {
                            if (doc.IsDisposed || AcadApp.DocumentManager.MdiActiveDocument != doc) return;
                            CloseWindow();
                            SimpleAnnotationHudInteractionService.CloseWindow();
                            QuantityAttributeDoubleClickService.TryOpenWithOverlapSelection(
                                doc, selectedIds, screenPoint);
                        }));
                }
                return;
            }
            e.Handled = true;
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(delegate
                {
                    SimpleAnnotationHudInteractionService.CloseWindow();
                    ShowCard(doc, annotationObjectId);
                }));
        }

        private static ObjectId[] ReadImpliedSelection(Document doc)
        {
            if (doc == null) return new ObjectId[0];
            try
            {
                PromptSelectionResult implied = doc.Editor.SelectImplied();
                if (implied.Status == PromptStatus.OK && implied.Value != null)
                    return implied.Value.GetObjectIds();
            }
            catch { }
            return new ObjectId[0];
        }

        private static Point GetDoubleClickScreenPoint(MSG message)
        {
            try
            {
                long value = message.lParam.ToInt64();
                var point = new NativePoint
                {
                    X = unchecked((short)(value & 0xFFFF)),
                    Y = unchecked((short)((value >> 16) & 0xFFFF))
                };
                if (message.hwnd != IntPtr.Zero && ClientToScreen(message.hwnd, ref point))
                    return new Point(point.X, point.Y);
            }
            catch { }

            System.Drawing.Point cursor = System.Windows.Forms.Cursor.Position;
            return new Point(cursor.X, cursor.Y);
        }

        private static void ShowCard(Document doc, ObjectId selectedObjectId)
        {
            try
            {
                PipeLengthAnnotationEditModel model =
                    PipeLengthAnnotationObjectService.LoadEditModel(doc, selectedObjectId);
                if (model == null) return;
                EnsureWindow();
                AttachDocument(doc);
                _spatialEditActive = false;
                _restoreAfterSpatialEdit = false;
                _model = model;
                _window.SetModel(model);
                ObjectId[] annotationIds = PipeLengthAnnotationObjectService.GetAnnotationObjectIds(doc, model.AnnotationId);
                if (annotationIds.Length > 0) doc.Editor.SetImpliedSelection(annotationIds);
                PipeLengthAnnotationGripOverrule.SetSelectedAnnotation(doc, model.AnnotationId);
                Point origin = ResolveAnimationOrigin(doc, model);
                if (!_window.IsVisible || _window.IsClosingAnimation)
                {
                    IntPtr owner = AcadApp.MainWindow == null ? IntPtr.Zero : AcadApp.MainWindow.Handle;
                    if (owner != IntPtr.Zero) new WindowInteropHelper(_window).Owner = owner;
                    double left = 0.0;
                    double top = 0.0;
                    bool remembered = !_window.HasInitializedPosition
                        && PipeLengthAnnotationHudPositionStore.TryLoad(out left, out top);
                    _window.ShowAnimated(origin, remembered ? (double?)left : null, remembered ? (double?)top : null);
                }
                else _window.SetAnimationOrigin(origin);
                _window.Activate();
            }
            catch (Exception ex)
            {
                doc.Editor.WriteHudMessage("\n[CDBox 标注浮窗] 打开失败：" + ex.Message);
            }
        }

        private static void EnsureWindow()
        {
            if (_window != null) return;
            _window = new PipeLengthAnnotationCardWindow
            {
                CommitHandler = ApplyModel,
                BindingHandler = ToggleBinding
            };
            ApplyWindowAppearance(_window, CDBoxStudioSettingsStore.Load());
            _window.UserMoved += delegate { SaveHudPosition(); };
            _window.Closed += delegate
            {
                SaveHudPosition();
                _window = null;
                _model = null;
                _spatialEditActive = false;
                _restoreAfterSpatialEdit = false;
            };
        }

        internal static void RefreshAppearance()
        {
            CDBoxStudioSettings settings = null;
            try
            {
                settings = CDBoxStudioSettingsStore.Load();
                _doubleClickOpenEnabled = settings.DoubleClickOpenEnabled;
            }
            catch { }
            SimpleAnnotationHudInteractionService.RefreshAppearance();
            if (_window == null || settings == null) return;
            try { ApplyWindowAppearance(_window, settings); }
            catch { }
        }

        private static void RefreshSettingsCache()
        {
            try
            {
                _doubleClickOpenEnabled =
                    CDBoxStudioSettingsStore.Load().DoubleClickOpenEnabled;
            }
            catch { _doubleClickOpenEnabled = true; }
        }

        private static void ApplyWindowAppearance(PipeLengthAnnotationCardWindow window,
            CDBoxStudioSettings settings)
        {
            if (window == null || settings == null) return;
            window.ApplyAppearance(settings.AnnotationHudNormalOpacity,
                settings.AnnotationHudHoverOpacity, settings.AnnotationHudGlowEnabled,
                settings.AnnotationHudGlowIntensity);
        }

        private static PipeLengthAnnotationEditModel ApplyModel(PipeLengthAnnotationEditModel submitted)
        {
            if (_document == null) throw new InvalidOperationException("当前图纸已关闭。 ");
            PipeLengthAnnotationEditModel saved =
                PipeLengthAnnotationObjectService.SaveEditModel(_document, submitted);
            _model = saved;
            _document.Editor.Regen();
            return saved;
        }

        private static PipeLengthAnnotationEditModel ToggleBinding(PipeLengthAnnotationEditModel submitted)
        {
            if (_document == null) throw new InvalidOperationException("当前图纸已关闭。 ");
            PipeLengthAnnotationEditModel result;
            if (submitted.IsBound)
            {
                result = PipeLengthAnnotationObjectService.DetachAnnotation(
                    _document, submitted.AnnotationId, submitted.SelectedObjectId);
            }
            else
            {
                string message;
                if (!PipeLengthAnnotationObjectService.TryRebindAtLeaderPoint(_document,
                    submitted.AnnotationId, submitted.SelectedObjectId, out result, out message))
                {
                    throw new InvalidOperationException(message);
                }
            }
            _model = result;
            return result;
        }

        private static void AttachDocument(Document doc)
        {
            if (_document == doc) return;
            DetachDocument();
            _document = doc;
            if (_document == null) return;
            _document.CloseWillStart += DocumentCloseWillStart;
            _document.ImpliedSelectionChanged += DocumentImpliedSelectionChanged;
            _document.CommandWillStart += DocumentCommandWillStart;
            _document.CommandEnded += DocumentCommandEnded;
            _document.CommandCancelled += DocumentCommandAborted;
            _document.CommandFailed += DocumentCommandAborted;
            _document.Database.ObjectModified += DatabaseObjectModified;
            _document.Database.ObjectErased += DatabaseObjectErased;
            _document.Database.BeginDeepCloneTranslation += DatabaseBeginDeepCloneTranslation;
            _document.Database.BeginSave += DatabaseBeginSave;
            _document.Database.SaveComplete += DatabaseSaveComplete;
            _document.Database.AbortSave += DatabaseAbortSave;
        }

        private static void DetachDocument()
        {
            if (_document != null)
            {
                PipeLengthAnnotationGripOverrule.ClearSelectedAnnotation(_document);
                try { _document.CloseWillStart -= DocumentCloseWillStart; } catch { }
                try { _document.ImpliedSelectionChanged -= DocumentImpliedSelectionChanged; } catch { }
                try { _document.CommandWillStart -= DocumentCommandWillStart; } catch { }
                try { _document.CommandEnded -= DocumentCommandEnded; } catch { }
                try { _document.CommandCancelled -= DocumentCommandAborted; } catch { }
                try { _document.CommandFailed -= DocumentCommandAborted; } catch { }
                try { _document.Database.ObjectModified -= DatabaseObjectModified; } catch { }
                try { _document.Database.ObjectErased -= DatabaseObjectErased; } catch { }
                try { _document.Database.BeginDeepCloneTranslation -= DatabaseBeginDeepCloneTranslation; } catch { }
                try { _document.Database.BeginSave -= DatabaseBeginSave; } catch { }
                try { _document.Database.SaveComplete -= DatabaseSaveComplete; } catch { }
                try { _document.Database.AbortSave -= DatabaseAbortSave; } catch { }
            }
            DirtySourceHandles.Clear();
            PendingCloneBatches.Clear();
            _spatialEditCompletionVersion++;
            _selectionSyncPending = false;
            _synchronizingAnnotationSelection = false;
            _documentSaveActive = false;
            _document = null;
        }

        private static void DocumentImpliedSelectionChanged(object sender, EventArgs e)
        {
            if (_document == null || _spatialEditActive || _synchronizingAnnotationSelection) return;
            try
            {
                PromptSelectionResult selection = _document.Editor.SelectImplied();
                ObjectId[] selectedIds = selection.Status == PromptStatus.OK && selection.Value != null
                    ? selection.Value.GetObjectIds() : new ObjectId[0];
                SimpleAnnotationHudInteractionService.OnSelectionChanged(_document, selectedIds);
                ObjectId resolved;
                if (selection.Status != PromptStatus.OK || selection.Value == null
                    || !PipeLengthAnnotationObjectService.TryResolveAnnotationObject(_document,
                        selection.Value.GetObjectIds(), out resolved))
                {
                    PipeLengthAnnotationGripOverrule.ClearSelectedAnnotation(_document);
                    if (_window != null && _window.IsVisible) _window.Close();
                    return;
                }

                string selectedAnnotationId;
                if (!PipeLengthAnnotationObjectService.TryGetAnnotationId(resolved, out selectedAnnotationId)) return;
                PipeLengthAnnotationGripOverrule.SetSelectedAnnotation(_document, selectedAnnotationId);
                QueueAnnotationSelectionSync(_document, selectedAnnotationId);

                if (_window != null && _window.IsVisible && _model != null
                    && !string.Equals(selectedAnnotationId, _model.AnnotationId,
                        StringComparison.OrdinalIgnoreCase)) _window.Close();
            }
            catch { }
        }

        private static void QueueAnnotationSelectionSync(Document doc, string annotationId)
        {
            if (_selectionSyncPending || doc == null || string.IsNullOrWhiteSpace(annotationId)) return;
            _selectionSyncPending = true;
            try
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                {
                    _selectionSyncPending = false;
                    if (_document != doc || _spatialEditActive) return;
                    try
                    {
                        PromptSelectionResult selection = doc.Editor.SelectImplied();
                        ObjectId resolved;
                        if (selection.Status != PromptStatus.OK || selection.Value == null
                            || !PipeLengthAnnotationObjectService.TryResolveAnnotationObject(doc,
                                selection.Value.GetObjectIds(), out resolved)) return;

                        string currentAnnotationId;
                        if (!PipeLengthAnnotationObjectService.TryGetAnnotationId(resolved,
                                out currentAnnotationId)
                            || !string.Equals(currentAnnotationId, annotationId,
                                StringComparison.OrdinalIgnoreCase)) return;

                        var combined = new HashSet<ObjectId>(selection.Value.GetObjectIds());
                        ObjectId[] annotationIds = PipeLengthAnnotationObjectService.GetAnnotationObjectIds(
                            doc, annotationId);
                        bool selectionChanged = false;
                        for (int i = 0; i < annotationIds.Length; i++)
                        {
                            if (combined.Add(annotationIds[i])) selectionChanged = true;
                        }
                        if (!selectionChanged) return;

                        var all = new ObjectId[combined.Count];
                        combined.CopyTo(all);
                        _synchronizingAnnotationSelection = true;
                        try { doc.Editor.SetImpliedSelection(all); }
                        finally { _synchronizingAnnotationSelection = false; }
                    }
                    catch { _synchronizingAnnotationSelection = false; }
                }));
            }
            catch { _selectionSyncPending = false; }
        }

        private static void DatabaseObjectModified(object sender, ObjectEventArgs e)
        {
            QueueDirtySource(e == null ? null : e.DBObject);
        }

        private static void DatabaseObjectErased(object sender, ObjectErasedEventArgs e)
        {
            QueueDirtySource(e == null ? null : e.DBObject);
        }

        private static void DatabaseBeginDeepCloneTranslation(object sender,
            IdMappingEventArgs e)
        {
            if (_document == null || e == null || e.IdMapping == null) return;
            try
            {
                IdMapping mapping = e.IdMapping;
                if (mapping.DestinationDatabase != _document.Database) return;
                var batch = new CloneBatch();
                batch.IsCrossDatabase = mapping.OriginalDatabase != mapping.DestinationDatabase;
                foreach (IdPair pair in mapping)
                {
                    if (!pair.IsCloned || !pair.IsPrimary
                        || pair.Key.IsNull || pair.Value.IsNull) continue;
                    ObjectId stableKey =
                        PipeLengthAnnotationCloneSafetyPolicy.StableMapKey(
                            pair.Key, pair.Value, batch.IsCrossDatabase);
                    batch.ObjectMap[stableKey] = pair.Value;
                    if (!PipeLengthAnnotationCloneSafetyPolicy
                        .MayRetainSourceIdentity(batch.IsCrossDatabase))
                    {
                        // COPYCLIP/PASTECLIP 的源对象属于临时或外部数据库，
                        // 其生命周期不受当前文档保证，因此不得把源
                        // ObjectId/Handle 留到命令结束后访问。
                        continue;
                    }
                    try
                    {
                        string originalHandle = pair.Key.Handle.ToString();
                        if (!string.IsNullOrWhiteSpace(originalHandle))
                            batch.ClonesByOriginalHandle[originalHandle] =
                                pair.Value;
                    }
                    catch { }
                }
                if (batch.ObjectMap.Count > 0) PendingCloneBatches.Add(batch);
            }
            catch { }
        }

        private static void QueueDirtySource(DBObject value)
        {
            if (_bindingRefreshActive || _documentSaveActive) return;
            Curve curve = value as Curve;
            if (curve == null || curve.ObjectId.IsNull) return;
            string annotationId;
            string annotationPart;
            try
            {
                if (PipeLengthAnnotationObjectService.TryGetAnnotationPart(
                    curve, out annotationId, out annotationPart)) return;
            }
            catch { }
            try { DirtySourceHandles.Add(curve.Handle.ToString()); }
            catch { }
        }

        private static void DocumentCommandEnded(object sender, CommandEventArgs e)
        {
            if (_document == null) return;
            bool saveCommand = _documentSaveActive || IsDrawingSaveCommand(
                e == null ? string.Empty : e.GlobalCommandName);
            _documentSaveActive = false;
            if (saveCommand)
            {
                DirtySourceHandles.Clear();
                PendingCloneBatches.Clear();
            }
            else
            {
                RepairPendingClones();
                PipeLengthAnnotationGripOverrule.CompletePendingBindings(_document);
                RefreshDirtyBindings();
            }
            SimpleAnnotationHudInteractionService.OnCommandFinished(_document);
        }

        private static void DocumentCommandAborted(object sender, CommandEventArgs e)
        {
            if (_document == null) return;
            bool saveCommand = _documentSaveActive || IsDrawingSaveCommand(
                e == null ? string.Empty : e.GlobalCommandName);
            _documentSaveActive = false;
            if (saveCommand)
            {
                DirtySourceHandles.Clear();
                PendingCloneBatches.Clear();
            }
            else RepairPendingClones();
            PipeLengthAnnotationGripOverrule.CancelPendingBindings(_document);
            if (!saveCommand) RefreshDirtyBindings();
            SimpleAnnotationHudInteractionService.OnCommandFinished(_document);
        }

        private static void DocumentCommandWillStart(object sender, CommandEventArgs e)
        {
            if (_document == null) return;
            string commandName = e == null ? string.Empty : e.GlobalCommandName;
            _documentSaveActive = IsDrawingSaveCommand(commandName);
            if (_documentSaveActive) DirtySourceHandles.Clear();
            SimpleAnnotationHudInteractionService.OnCommandWillStart(_document,
                commandName);
        }

        private static void DatabaseBeginSave(object sender, DatabaseIOEventArgs e)
        {
            _documentSaveActive = true;
            DirtySourceHandles.Clear();
        }

        private static void DatabaseSaveComplete(object sender, DatabaseIOEventArgs e)
        {
            _documentSaveActive = false;
            DirtySourceHandles.Clear();
        }

        private static void DatabaseAbortSave(object sender, EventArgs e)
        {
            _documentSaveActive = false;
            DirtySourceHandles.Clear();
        }

        private static bool IsDrawingSaveCommand(string commandName)
        {
            string value = (commandName ?? string.Empty).Trim().TrimStart('_', '.').ToUpperInvariant();
            return value == "SAVE" || value == "QSAVE" || value == "SAVEAS" || value == "SAVEALL";
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr hWnd, ref NativePoint point);

        private static void RefreshDirtyBindings()
        {
            if (_document == null) return;
            if (DirtySourceHandles.Count == 0 || _bindingRefreshActive) return;
            string[] handles = new string[DirtySourceHandles.Count];
            DirtySourceHandles.CopyTo(handles);
            DirtySourceHandles.Clear();
            try
            {
                _bindingRefreshActive = true;
                ObjectId refreshId = PipeLengthAnnotationObjectService.RefreshBindingsForSourceHandles(
                    _document, handles, _model == null ? string.Empty : _model.AnnotationId);
                if (!refreshId.IsNull) RefreshCard(_document, refreshId);
            }
            catch { }
            finally { _bindingRefreshActive = false; }
        }

        private static void RepairPendingClones()
        {
            if (_document == null || PendingCloneBatches.Count == 0) return;
            CloneBatch[] batches = PendingCloneBatches.ToArray();
            PendingCloneBatches.Clear();
            var pipeHandles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nodeHandles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool previousRefreshState = _bindingRefreshActive;
            try
            {
                _bindingRefreshActive = true;
                using (DocumentLock docLock = _document.LockDocument())
                using (Transaction tr = _document.Database.TransactionManager.StartTransaction())
                {
                    for (int i = 0; i < batches.Length; i++)
                    {
                        List<string> nodes = QuantityPipeAttributeService.RepairClonedAttributeObjects(
                            _document.Database, tr, batches[i].ObjectMap);
                        for (int j = 0; j < nodes.Count; j++) nodeHandles.Add(nodes[j]);
                        List<string> pipes = PipeLengthAnnotationObjectService.RepairClonedObjects(
                            _document.Database, tr, batches[i].ObjectMap,
                            batches[i].ClonesByOriginalHandle, batches[i].IsCrossDatabase);
                        for (int j = 0; j < pipes.Count; j++) pipeHandles.Add(pipes[j]);
                        SimpleAnnotationObjectService.RepairClonedAnnotations(
                            _document.Database, tr, batches[i].ObjectMap,
                            batches[i].ClonesByOriginalHandle, batches[i].IsCrossDatabase);
                    }
                    tr.Commit();
                }

                if (nodeHandles.Count > 0)
                    SimpleAnnotationObjectService.RefreshNodeAnnotationsForSourceHandles(
                        _document, nodeHandles);
                if (pipeHandles.Count > 0)
                {
                    ObjectId refreshId = PipeLengthAnnotationObjectService.RefreshBindingsForSourceHandles(
                        _document, pipeHandles, _model == null ? string.Empty : _model.AnnotationId);
                    if (!refreshId.IsNull) RefreshCard(_document, refreshId);
                }
            }
            catch (System.Exception ex)
            {
                CDBoxStudioLogger.Error("复制对象关联修复失败。", ex);
            }
            finally { _bindingRefreshActive = previousRefreshState; }
        }

        private sealed class CloneBatch
        {
            public Dictionary<ObjectId, ObjectId> ObjectMap { get; private set; }
            public Dictionary<string, ObjectId> ClonesByOriginalHandle { get; private set; }
            public bool IsCrossDatabase { get; set; }

            public CloneBatch()
            {
                ObjectMap = new Dictionary<ObjectId, ObjectId>();
                ClonesByOriginalHandle = new Dictionary<string, ObjectId>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void DocumentCloseWillStart(object sender, EventArgs e)
        {
            CloseWindow();
            SimpleAnnotationHudInteractionService.CloseWindow();
            DetachDocument();
        }

        private static void DocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            Document activated = e == null ? AcadApp.DocumentManager.MdiActiveDocument : e.Document;
            if (activated == _document) return;
            CloseWindow();
            SimpleAnnotationHudInteractionService.CloseWindow();
            AttachDocument(activated);
        }

        private static void SaveHudPosition()
        {
            if (_window == null || !_window.HasInitializedPosition) return;
            double left;
            double top;
            if (_window.TryGetRestingPosition(out left, out top))
            {
                PipeLengthAnnotationHudPositionStore.Save(left, top);
            }
        }

        private static Point ResolveAnimationOrigin(Document doc, PipeLengthAnnotationEditModel model)
        {
            if (doc != null && model != null && model.HasBindingPoint)
            {
                try { return doc.Editor.PointToScreen(model.BindingPoint, 0); }
                catch { }
            }
            if (_window != null)
            {
                try { return _window.GetCursorScreenPosition(); }
                catch { }
            }
            System.Drawing.Point cursor = System.Windows.Forms.Cursor.Position;
            return new Point(cursor.X, cursor.Y);
        }

        private static void CloseWindow()
        {
            if (_window != null)
            {
                try { _window.CloseImmediately(); } catch { }
            }
            _window = null;
            _model = null;
            _spatialEditCompletionVersion++;
            _spatialEditActive = false;
            _restoreAfterSpatialEdit = false;
        }
    }
}
