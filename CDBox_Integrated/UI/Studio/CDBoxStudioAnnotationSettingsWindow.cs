using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioAnnotationSettingsWindow
    {
        private static CDBoxStudioWebPageForm _current;
        private static bool _pageReady;
        private static bool _allowClose;
        private static string _initialSection = "surface";

        public static void ShowWindow(IWin32Window owner, string section)
        {
            _initialSection = NormalizeSection(section);
            if (_current != null && !_current.IsDisposed)
            {
                _current.WindowState = FormWindowState.Normal;
                _current.Activate();
                _current.TryExecutePageScript("window.CDBoxAnnotationSettingsOpen && window.CDBoxAnnotationSettingsOpen('" + _initialSection + "');");
                return;
            }

            _pageReady = false;
            _allowClose = false;
            _current = new CDBoxStudioWebPageForm(
                "标注设置",
                delegate { return CDBoxStudioAnnotationSettingsWindowHtml.Build(CDBoxStudioSettingsStore.Load(), CDBoxStudioLogger.LogFilePath, _initialSection); },
                Route);
            _current.Width = 1280;
            _current.Height = 820;
            _current.MinimumSize = new Size(980, 680);
            _current.FormClosing += OnFormClosing;
            _current.FormClosed += delegate
            {
                _current = null;
                _pageReady = false;
                _allowClose = false;
            };
            _current.Show(owner ?? new AcadMainWindow());
            CDBoxStudioLogger.Info("已打开标注设置独立 WebView2 窗口，初始模块：" + _initialSection);
        }

        private static void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_allowClose || !_pageReady || _current == null || _current.IsDisposed) return;

            bool requested = _current.TryExecutePageScript("window.CDBoxAnnotationSettingsRequestClose && window.CDBoxAnnotationSettingsRequestClose();");
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
                result.ToastMessage = "标注设置独立窗口已就绪";
                return result;
            }

            if (name == "closeannotationsettingsconfirmed")
            {
                _allowClose = true;
                if (_current != null && !_current.IsDisposed) _current.BeginInvoke(new Action(delegate { _current.Close(); }));
                return result;
            }

            CDBoxStudioRouteResult shared;
            if (CDBoxStudioAnnotationSettingsRoutes.TryRoute(request, true, out shared)) return shared;

            result.Handled = false;
            CDBoxStudioLogger.Warn("标注设置独立窗口收到未知路由消息：" + request.Name);
            return result;
        }

        private static string NormalizeSection(string section)
        {
            string value = (section ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "pipe" || value == "pipe-length" || value == "pipelength") return "pipeLength";
            if (value == "node") return "node";
            return "surface";
        }
    }
}
