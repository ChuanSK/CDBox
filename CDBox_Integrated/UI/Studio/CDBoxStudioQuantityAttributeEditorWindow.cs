using System;
using System.Drawing;
using System.Windows.Forms;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioQuantityAttributeEditorWindow
    {
        private static CDBoxStudioWebPageForm _current;
        private static string _documentId = string.Empty;
        private static string _handle = string.Empty;

        public static void ShowWindow(IWin32Window owner, QuantityPipeSelectionInfo info, string documentId)
        {
            _documentId = documentId ?? string.Empty;
            _handle = info == null ? string.Empty : info.HandleText;
            if (IsReusable(_current))
            {
                _current.WindowState = FormWindowState.Normal;
                _current.Show();
                _current.Activate();
                CDBoxStudioQuantityAttributeEditorContext context = info == null ? CDBoxStudioQuantityAttributeEditorApi.GetContext(CDBoxStudioQuantityAttributeEditorApi.Serialize(new { documentId = _documentId, handle = _handle })) : CDBoxStudioQuantityAttributeEditorApi.FromInfo(QuantityDashboardService.ResolveDocument(_documentId), info);
                _current.TryExecutePageScript("window.CDBoxQuantityAttributeEditorLoad && window.CDBoxQuantityAttributeEditorLoad(" + CDBoxStudioQuantityAttributeEditorApi.Serialize(context) + ");");
                return;
            }
            ReleaseStaleWindow();
            var window = new CDBoxStudioWebPageForm("属性编辑器 · 3.6.1", delegate { return CDBoxStudioQuantityAttributeEditorPage.BuildStandaloneDocument(CDBoxStudioSettingsStore.Load(), CDBoxStudioLogger.LogFilePath, _documentId, _handle); }, Route, "quantity-attribute-editor");
            _current = window;
            window.Width = 1180;
            window.Height = 820;
            window.MinimumSize = new Size(900, 650);
            window.FormClosed += delegate
            {
                if (ReferenceEquals(_current, window))
                {
                    _current = null;
                    _documentId = string.Empty;
                    _handle = string.Empty;
                }
                RestoreCadFocus();
            };
            window.Show(owner ?? new AcadMainWindow());
            CDBoxStudioLogger.Info("已打开属性编辑器 3.6.1 独立窗口。");
        }

        private static bool IsReusable(CDBoxStudioWebPageForm window)
        {
            return window != null && !window.IsDisposed && !window.Disposing
                && window.IsHandleCreated && window.Visible;
        }

        private static void ReleaseStaleWindow()
        {
            CDBoxStudioWebPageForm stale = _current;
            _current = null;
            if (stale == null || stale.IsDisposed) return;
            try { stale.Dispose(); }
            catch { }
        }

        private static void RestoreCadFocus()
        {
            try
            {
                if (AcadApp.MainWindow != null && !AcadApp.MainWindow.IsDisposed)
                    AcadApp.MainWindow.Focus();
            }
            catch { }
        }

        internal static FormWindowState? MinimizeForCadSelection()
        {
            CDBoxStudioWebPageForm window = _current;
            if (!IsReusable(window)) return null;
            if (window.InvokeRequired)
            {
                return (FormWindowState?)window.Invoke(
                    new Func<FormWindowState?>(MinimizeForCadSelection));
            }

            FormWindowState previousState = window.WindowState;
            window.WindowState = FormWindowState.Minimized;
            System.Windows.Forms.Application.DoEvents();
            RestoreCadFocus();
            return previousState;
        }

        internal static void RestoreAfterCadSelection(FormWindowState? previousState)
        {
            CDBoxStudioWebPageForm window = _current;
            if (!previousState.HasValue || !IsReusable(window)) return;
            if (window.InvokeRequired)
            {
                window.Invoke(new Action<FormWindowState?>(RestoreAfterCadSelection), previousState);
                return;
            }

            window.WindowState = previousState.Value == FormWindowState.Minimized
                ? FormWindowState.Normal
                : previousState.Value;
            window.Show();
            window.Activate();
        }

        private static CDBoxStudioRouteResult Route(CDBoxStudioRouteRequest request)
        {
            if (request != null && string.Equals(request.Name, "ready", StringComparison.OrdinalIgnoreCase)) return new CDBoxStudioRouteResult { Handled = true, ToastKind = "success", ToastMessage = "属性编辑器已就绪" };
            CDBoxStudioRouteResult result;
            if (CDBoxStudioQuantityAttributeEditorRoutes.TryRoute(request, out result)) return result;
            return new CDBoxStudioRouteResult { Handled = false };
        }
    }
}
