using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioLayerManagerWindow
    {
        private static CDBoxStudioWebPageForm _current;
        private static bool _pageReady;
        private static bool _allowClose;

        public static void ShowWindow(IWin32Window owner)
        {
            if (_current != null && !_current.IsDisposed)
            {
                _current.WindowState = FormWindowState.Normal;
                _current.Activate();
                return;
            }

            _pageReady = false;
            _allowClose = false;
            _current = new CDBoxStudioWebPageForm(
                "图层管理器",
                delegate { return CDBoxStudioLayerManagerWindowHtml.Build(CDBoxStudioSettingsStore.Load(), CDBoxStudioLogger.LogFilePath); },
                Route);
            _current.Width = 1480;
            _current.Height = 900;
            _current.MinimumSize = new Size(1120, 700);
            _current.FormClosing += OnFormClosing;
            _current.FormClosed += delegate
            {
                _current = null;
                _pageReady = false;
                _allowClose = false;
            };
            _current.Show(owner ?? new AcadMainWindow());
            CDBoxStudioLogger.Info("已打开图层管理器独立 WebView2 窗口。");
        }

        private static void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_allowClose || !_pageReady || _current == null || _current.IsDisposed) return;
            bool requested = _current.TryExecutePageScript("window.CDBoxLayerManagerRequestClose && window.CDBoxLayerManagerRequestClose();");
            if (requested) e.Cancel = true;
        }

        private static CDBoxStudioRouteResult Route(CDBoxStudioRouteRequest request)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "info" };
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                result.Handled = false;
                return result;
            }

            string name = request.Name.Trim().ToLowerInvariant();
            if (name == "ready")
            {
                _pageReady = true;
                result.ToastKind = "success";
                result.ToastMessage = "图层管理器独立窗口已就绪";
                return result;
            }

            if (name == "closelayermanagerconfirmed")
            {
                _allowClose = true;
                if (_current != null && !_current.IsDisposed) _current.BeginInvoke(new Action(delegate { _current.Close(); }));
                return result;
            }

            CDBoxStudioRouteResult shared;
            if (CDBoxStudioLayerManagerRoutes.TryRoute(request, true, out shared)) return shared;

            result.Handled = false;
            CDBoxStudioLogger.Warn("图层管理器独立窗口收到未知路由消息：" + request.Name);
            return result;
        }
    }
}
