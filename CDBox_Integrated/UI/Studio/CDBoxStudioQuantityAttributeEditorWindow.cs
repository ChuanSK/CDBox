using System;
using System.Drawing;
using System.Windows.Forms;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI;

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
            if (_current != null && !_current.IsDisposed)
            {
                _current.WindowState = FormWindowState.Normal; _current.Activate();
                CDBoxStudioQuantityAttributeEditorContext context = info == null ? CDBoxStudioQuantityAttributeEditorApi.GetContext(CDBoxStudioQuantityAttributeEditorApi.Serialize(new { documentId = _documentId, handle = _handle })) : CDBoxStudioQuantityAttributeEditorApi.FromInfo(QuantityDashboardService.ResolveDocument(_documentId), info);
                _current.TryExecutePageScript("window.CDBoxQuantityAttributeEditorLoad && window.CDBoxQuantityAttributeEditorLoad(" + CDBoxStudioQuantityAttributeEditorApi.Serialize(context) + ");");
                return;
            }
            _current = new CDBoxStudioWebPageForm("属性编辑器 · Preview 9", delegate { return CDBoxStudioQuantityAttributeEditorPage.BuildStandaloneDocument(CDBoxStudioSettingsStore.Load(), CDBoxStudioLogger.LogFilePath, _documentId, _handle); }, Route);
            _current.Width = 1180; _current.Height = 820; _current.MinimumSize = new Size(900, 650);
            _current.FormClosed += delegate { _current = null; _documentId = string.Empty; _handle = string.Empty; };
            _current.Show(owner ?? new AcadMainWindow());
            CDBoxStudioLogger.Info("已打开属性编辑器 Preview 9 独立窗口。");
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
