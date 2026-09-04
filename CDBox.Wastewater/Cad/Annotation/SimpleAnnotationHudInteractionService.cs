using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace TCPipeAutoDraw.Modules.AnnotationHud
{
    internal static class SimpleAnnotationHudInteractionService
    {
        private static SimpleAnnotationHudWindow _window;
        private static Document _document;
        private static SimpleAnnotationHudModel _model;
        private static bool _spatialEditActive;
        private static bool _restoreAfterSpatialEdit;
        private static int _spatialEditVersion;

        public static bool TryOpen(Document doc, ObjectId[] selectedIds)
        {
            if (doc == null || selectedIds == null || selectedIds.Length == 0) return false;
            ObjectId selected;
            if (!SimpleAnnotationObjectService.TryResolveAnnotationObject(doc, selectedIds, out selected)) return false;
            ShowCard(doc, selected);
            return true;
        }

        public static bool IsOwnWindowHandle(IntPtr handle)
        {
            if (_window == null || !_window.IsVisible || handle == IntPtr.Zero) return false;
            try { return new WindowInteropHelper(_window).Handle == handle; }
            catch { return false; }
        }

        public static void OnSelectionChanged(Document doc, ObjectId[] selectedIds)
        {
            if (_spatialEditActive || _window == null || !_window.IsVisible || _model == null || doc != _document) return;
            if (!SimpleAnnotationObjectService.SelectionMatches(doc, _model, selectedIds)) CloseWindow();
        }

        public static void OnCommandWillStart(Document doc, string globalCommandName)
        {
            if (doc == null || doc != _document || _window == null || _model == null
                || !_window.IsVisible || !IsSpatialCommand(globalCommandName)) return;
            _spatialEditActive = true;
            _restoreAfterSpatialEdit = true;
            _spatialEditVersion++;
            _window.HideAnimated(ResolveAnimationOrigin(doc, _model));
        }

        public static void OnCommandFinished(Document doc)
        {
            if (!_spatialEditActive || doc == null || doc != _document) return;
            int version = ++_spatialEditVersion;
            try
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(delegate
                {
                    if (version != _spatialEditVersion || doc != _document) return;
                    try
                    {
                        if (!_restoreAfterSpatialEdit || _model == null || _window == null) return;
                        ObjectId[] ids = _model.GetObjectIds();
                        ObjectId reloadId = ids.Length == 0 ? ObjectId.Null : ids[0];
                        SimpleAnnotationHudModel refreshed = reloadId.IsNull
                            ? null : SimpleAnnotationObjectService.LoadEditModel(doc, reloadId);
                        if (refreshed == null)
                        {
                            CloseWindow();
                            return;
                        }
                        _model = refreshed;
                        _window.SetModel(refreshed);
                        doc.Editor.SetImpliedSelection(refreshed.GetObjectIds());
                        _window.ShowAnimated(ResolveAnimationOrigin(doc, refreshed), null, null);
                    }
                    catch { }
                    finally
                    {
                        _spatialEditActive = false;
                        _restoreAfterSpatialEdit = false;
                    }
                }));
            }
            catch
            {
                _spatialEditActive = false;
                _restoreAfterSpatialEdit = false;
            }
        }

        public static void RefreshAppearance()
        {
            if (_window == null) return;
            ApplyAppearance(_window, CDBoxStudioSettingsStore.Load());
        }

        public static void CloseWindow()
        {
            if (_window != null)
            {
                try { _window.CloseImmediately(); } catch { }
            }
            _window = null;
            _document = null;
            _model = null;
            _spatialEditVersion++;
            _spatialEditActive = false;
            _restoreAfterSpatialEdit = false;
        }

        private static void ShowCard(Document doc, ObjectId selectedObjectId)
        {
            try
            {
                SimpleAnnotationHudModel model = SimpleAnnotationObjectService.LoadEditModel(doc, selectedObjectId);
                if (model == null) return;
                EnsureWindow();
                _document = doc;
                _model = model;
                _window.SetModel(model);
                ObjectId[] ids = model.GetObjectIds();
                if (ids.Length > 0) doc.Editor.SetImpliedSelection(ids);
                Point origin = ResolveAnimationOrigin(doc, model);
                if (!_window.IsVisible || _window.IsClosingAnimation)
                {
                    IntPtr owner = AcadApp.MainWindow == null ? IntPtr.Zero : AcadApp.MainWindow.Handle;
                    if (owner != IntPtr.Zero) new WindowInteropHelper(_window).Owner = owner;
                    double left = 0.0;
                    double top = 0.0;
                    bool remembered = !_window.HasInitializedPosition
                        && PipeLengthAnnotationHudPositionStore.TryLoad(out left, out top);
                    _window.ShowAnimated(origin, remembered ? (double?)left : null,
                        remembered ? (double?)top : null);
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
            _window = new SimpleAnnotationHudWindow { CommitHandler = SaveModel };
            ApplyAppearance(_window, CDBoxStudioSettingsStore.Load());
            _window.UserMoved += delegate { SavePosition(); };
            _window.Closed += delegate
            {
                SavePosition();
                _window = null;
                _model = null;
            };
        }

        private static SimpleAnnotationHudModel SaveModel(SimpleAnnotationHudModel submitted)
        {
            if (_document == null) throw new InvalidOperationException("当前图纸已关闭。");
            SimpleAnnotationHudModel saved = SimpleAnnotationObjectService.SaveEditModel(_document, submitted);
            _model = saved;
            _document.Editor.Regen();
            return saved;
        }

        private static void ApplyAppearance(SimpleAnnotationHudWindow window, CDBoxStudioSettings settings)
        {
            if (window == null || settings == null) return;
            window.ApplyAppearance(settings.AnnotationHudNormalOpacity,
                settings.AnnotationHudHoverOpacity, settings.AnnotationHudGlowEnabled,
                settings.AnnotationHudGlowIntensity);
        }

        private static void SavePosition()
        {
            if (_window == null || !_window.HasInitializedPosition) return;
            double left;
            double top;
            if (_window.TryGetRestingPosition(out left, out top))
                PipeLengthAnnotationHudPositionStore.Save(left, top);
        }

        private static Point ResolveAnimationOrigin(Document doc, SimpleAnnotationHudModel model)
        {
            if (doc != null && model != null)
            {
                try { return doc.Editor.PointToScreen(model.AnchorPoint, 0); }
                catch { }
            }
            System.Drawing.Point cursor = System.Windows.Forms.Cursor.Position;
            return new Point(cursor.X, cursor.Y);
        }

        private static bool IsSpatialCommand(string globalCommandName)
        {
            string value = (globalCommandName ?? string.Empty).Trim().TrimStart('_', '.').ToUpperInvariant();
            return value.IndexOf("GRIP", StringComparison.Ordinal) >= 0
                || value == "MOVE" || value == "STRETCH" || value == "ROTATE"
                || value == "SCALE" || value == "MIRROR";
        }
    }
}
