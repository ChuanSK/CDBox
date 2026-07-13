using System;
using System.Drawing;
using System.Windows.Forms;
using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioQuantityDashboardWindow
    {
        private static CDBoxStudioWebPageForm _current;
        private static Action<string> _scriptSink;

        public static void ShowWindow(IWin32Window owner)
        {
            if (_current != null && !_current.IsDisposed)
            {
                _current.WindowState = FormWindowState.Normal;
                _current.Activate();
                _current.TryExecutePageScript("window.CDBoxQuantityDashboardEvent && window.CDBoxQuantityDashboardEvent({type:'refreshRequested'});");
                return;
            }

            _current = new CDBoxStudioWebPageForm(
                "工程量动态看板",
                delegate { return CDBoxStudioQuantityDashboardWindowHtml.Build(CDBoxStudioSettingsStore.Load(), CDBoxStudioLogger.LogFilePath); },
                Route);
            _current.Width = 1480;
            _current.Height = 900;
            _current.MinimumSize = new Size(1120, 700);
            _scriptSink = PushScript;
            CDBoxStudioQuantityDashboardRoutes.Configure(_scriptSink);
            _current.FormClosed += delegate
            {
                CDBoxStudioQuantityDashboardRoutes.Unconfigure(_scriptSink);
                _scriptSink = null;
                _current = null;
            };
            _current.Show(owner ?? new AcadMainWindow());
            CDBoxStudioLogger.Info("已打开工程量动态看板独立 WebView2 窗口。");
        }

        private static CDBoxStudioRouteResult Route(CDBoxStudioRouteRequest request)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "info" };
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                result.Handled = false;
                return result;
            }

            if (string.Equals(request.Name.Trim(), "ready", StringComparison.OrdinalIgnoreCase))
            {
                result.ToastKind = "success";
                result.ToastMessage = "工程量动态看板独立窗口已就绪";
                return result;
            }

            CDBoxStudioRouteResult shared;
            if (CDBoxStudioQuantityDashboardRoutes.TryRoute(request, out shared)) return shared;

            result.Handled = false;
            CDBoxStudioLogger.Warn("工程量动态看板独立窗口收到未知路由消息：" + request.Name);
            return result;
        }

        private static void PushScript(string script)
        {
            CDBoxStudioWebPageForm form = _current;
            if (form == null || form.IsDisposed || string.IsNullOrWhiteSpace(script)) return;
            try
            {
                if (form.InvokeRequired) form.BeginInvoke(new Action(delegate { form.TryExecutePageScript(script); }));
                else form.TryExecutePageScript(script);
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("向工程量动态看板独立窗口推送脚本失败。", ex);
            }
        }
    }
}
