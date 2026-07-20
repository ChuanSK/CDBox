using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
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
        private static bool _initialized;
        private static bool _spatialEditActive;
        private static bool _restoreAfterSpatialEdit;
        private static bool _bindingRefreshActive;
        private static bool _synchronizingAnnotationSelection;
        private static bool _selectionSyncPending;
        private static PipeLengthAnnotationCardWindow _window;
        private static Document _document;
        private static PipeLengthAnnotationEditModel _model;
        private static DispatcherTimer _restoreTimer;

        public static void Initialize()
        {
            if (_initialized) return;
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
            DetachDocument();
        }

        internal static void NotifySpatialEditStarted(Document doc, string annotationId)
        {
            PipeLengthAnnotationGripOverrule.SuspendSelectedMarker(doc, annotationId);
            if (doc == null || doc != _document) return;
            _spatialEditActive = true;
            if (_window == null || _model == null
                || !string.Equals(_model.AnnotationId, annotationId,
                    StringComparison.OrdinalIgnoreCase)) return;
            StopRestoreTimer();
            _restoreAfterSpatialEdit = _window.IsVisible;
            if (_window.IsVisible) _window.Hide();
        }

        internal static void NotifySpatialEditFinished(Document doc, string annotationId)
        {
            PipeLengthAnnotationGripOverrule.ResumeSelectedMarker(doc, annotationId);
            if (doc == null || doc != _document) return;
            _spatialEditActive = false;
            if (_window == null || _model == null
                || !string.Equals(_model.AnnotationId, annotationId,
                    StringComparison.OrdinalIgnoreCase)) return;
            if (!_restoreAfterSpatialEdit) return;
            StopRestoreTimer();
            _restoreTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _restoreTimer.Tick += delegate
            {
                StopRestoreTimer();
                if (_spatialEditActive || !_restoreAfterSpatialEdit || _window == null || _document != doc) return;
                try
                {
                    PipeLengthAnnotationEditModel refreshed =
                        PipeLengthAnnotationObjectService.LoadEditModel(doc, _model.SelectedObjectId);
                    if (refreshed == null) return;
                    _model = refreshed;
                    _window.SetModel(refreshed);
                    _window.Show();
                }
                catch { }
                finally { _restoreAfterSpatialEdit = false; }
            };
            _restoreTimer.Start();
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
                }
                catch { }
            }));
        }

        private static void PreTranslateMessage(object sender, PreTranslateMessageEventArgs e)
        {
            if (e == null || e.Message.message != WmLeftButtonDoubleClick) return;
            if (_window != null && _window.IsVisible)
            {
                try { if (new WindowInteropHelper(_window).Handle == e.Message.hwnd) return; }
                catch { }
            }

            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            PromptSelectionResult implied;
            try { implied = doc.Editor.SelectImplied(); }
            catch { return; }
            if (implied.Status != PromptStatus.OK || implied.Value == null) return;

            ObjectId annotationObjectId;
            if (!PipeLengthAnnotationObjectService.TryResolveAnnotationObject(
                doc, implied.Value.GetObjectIds(), out annotationObjectId)) return;
            e.Handled = true;
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(delegate { ShowCard(doc, annotationObjectId); }));
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
                StopRestoreTimer();
                _spatialEditActive = false;
                _restoreAfterSpatialEdit = false;
                _model = model;
                _window.SetModel(model);
                ObjectId[] annotationIds = PipeLengthAnnotationObjectService.GetAnnotationObjectIds(doc, model.AnnotationId);
                if (annotationIds.Length > 0) doc.Editor.SetImpliedSelection(annotationIds);
                PipeLengthAnnotationGripOverrule.SetSelectedAnnotation(doc, model.AnnotationId);
                if (!_window.IsVisible)
                {
                    IntPtr owner = AcadApp.MainWindow == null ? IntPtr.Zero : AcadApp.MainWindow.Handle;
                    if (owner != IntPtr.Zero) new WindowInteropHelper(_window).Owner = owner;
                    _window.Show();
                    double left;
                    double top;
                    bool remembered = PipeLengthAnnotationHudPositionStore.TryLoad(out left, out top);
                    _window.InitializePosition(remembered ? (double?)left : null, remembered ? (double?)top : null);
                }
                _window.Activate();
            }
            catch (Exception ex)
            {
                doc.Editor.WriteMessage("\n[CDBox 标注浮窗] 打开失败：" + ex.Message);
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
            _window.UserMoved += delegate { SaveHudPosition(); };
            _window.Closed += delegate
            {
                SaveHudPosition();
                StopRestoreTimer();
                _window = null;
                _model = null;
                _spatialEditActive = false;
                _restoreAfterSpatialEdit = false;
            };
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
            _document.CommandEnded += DocumentCommandEnded;
            _document.CommandCancelled += DocumentCommandAborted;
            _document.CommandFailed += DocumentCommandAborted;
            _document.Database.ObjectModified += DatabaseObjectModified;
            _document.Database.ObjectErased += DatabaseObjectErased;
        }

        private static void DetachDocument()
        {
            if (_document != null)
            {
                PipeLengthAnnotationGripOverrule.ClearSelectedAnnotation(_document);
                try { _document.CloseWillStart -= DocumentCloseWillStart; } catch { }
                try { _document.ImpliedSelectionChanged -= DocumentImpliedSelectionChanged; } catch { }
                try { _document.CommandEnded -= DocumentCommandEnded; } catch { }
                try { _document.CommandCancelled -= DocumentCommandAborted; } catch { }
                try { _document.CommandFailed -= DocumentCommandAborted; } catch { }
                try { _document.Database.ObjectModified -= DatabaseObjectModified; } catch { }
                try { _document.Database.ObjectErased -= DatabaseObjectErased; } catch { }
            }
            DirtySourceHandles.Clear();
            _selectionSyncPending = false;
            _synchronizingAnnotationSelection = false;
            _document = null;
        }

        private static void DocumentImpliedSelectionChanged(object sender, EventArgs e)
        {
            if (_document == null || _spatialEditActive || _synchronizingAnnotationSelection) return;
            try
            {
                PromptSelectionResult selection = _document.Editor.SelectImplied();
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

        private static void QueueDirtySource(DBObject value)
        {
            if (_bindingRefreshActive) return;
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
            PipeLengthAnnotationGripOverrule.CompletePendingBindings(_document);
            RefreshDirtyBindings();
        }

        private static void DocumentCommandAborted(object sender, CommandEventArgs e)
        {
            if (_document == null) return;
            PipeLengthAnnotationGripOverrule.CancelPendingBindings(_document);
            RefreshDirtyBindings();
        }

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

        private static void DocumentCloseWillStart(object sender, EventArgs e)
        {
            CloseWindow();
            DetachDocument();
        }

        private static void DocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            Document activated = e == null ? AcadApp.DocumentManager.MdiActiveDocument : e.Document;
            if (activated == _document) return;
            CloseWindow();
            AttachDocument(activated);
        }

        private static void StopRestoreTimer()
        {
            if (_restoreTimer == null) return;
            try { _restoreTimer.Stop(); } catch { }
            _restoreTimer = null;
        }

        private static void SaveHudPosition()
        {
            if (_window == null || !_window.HasInitializedPosition) return;
            PipeLengthAnnotationHudPositionStore.Save(_window.Left, _window.Top);
        }

        private static void CloseWindow()
        {
            StopRestoreTimer();
            if (_window != null)
            {
                try { _window.Close(); } catch { }
            }
            _window = null;
            _model = null;
            _spatialEditActive = false;
            _restoreAfterSpatialEdit = false;
        }
    }
}
