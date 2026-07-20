using System;
using System.Drawing;
using System.Windows.Forms;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioSectionDrawingWindow
    {
        private static CDBoxStudioWebPageForm _current;

        public static void ShowWindow(IWin32Window owner)
        {
            if (_current != null && !_current.IsDisposed)
            {
                _current.WindowState = FormWindowState.Normal;
                _current.Activate();
                _current.RefreshPage();
                return;
            }

            _current = new CDBoxStudioWebPageForm(
                "断面图生成 · Preview 10",
                delegate { return CDBoxStudioSectionDrawingPage.BuildStandaloneDocument(CDBoxStudioSettingsStore.Load(), CDBoxStudioLogger.LogFilePath); },
                Route,
                "section-drawing");
            _current.Width = 1420;
            _current.Height = 880;
            _current.MinimumSize = new Size(1040, 700);
            _current.FormClosed += delegate { _current = null; };
            _current.Show(owner ?? new AcadMainWindow());
            CDBoxStudioLogger.Info("已打开断面图生成 Preview 10 独立窗口。");
        }

        private static CDBoxStudioRouteResult Route(CDBoxStudioRouteRequest request)
        {
            if (request != null && string.Equals(request.Name, "ready", StringComparison.OrdinalIgnoreCase))
            {
                return new CDBoxStudioRouteResult { Handled = true, ToastKind = "success", ToastMessage = "断面图生成已就绪" };
            }

            CDBoxStudioRouteResult result;
            Action<string> scriptSink = delegate(string script)
            {
                if (_current != null && !_current.IsDisposed) _current.TryExecutePageScript(script);
            };
            if (!CDBoxStudioSectionDrawingRoutes.TryRoute(request, scriptSink, out result))
            {
                return new CDBoxStudioRouteResult { Handled = false };
            }

            if (result.ActionToRun != null)
            {
                CDBoxStudioAction inner = result.ActionToRun;
                bool restoreAfterRun = inner.RestoreStudioAfterRun;
                result.ActionToRun = new CDBoxStudioAction(
                    inner.Id,
                    inner.Title,
                    inner.Category,
                    inner.Description,
                    inner.CommandName,
                    inner.BadgeText,
                    inner.Kind,
                    inner.Enabled,
                    restoreAfterRun,
                    delegate
                    {
                        CDBoxStudioWebPageForm window = _current;
                        if (window != null && !window.IsDisposed) window.Hide();
                        try
                        {
                            inner.Run();
                            if (!restoreAfterRun && window != null && !window.IsDisposed) window.Close();
                        }
                        catch
                        {
                            if (!restoreAfterRun && window != null && !window.IsDisposed)
                            {
                                window.Show();
                                window.WindowState = FormWindowState.Normal;
                                window.Activate();
                            }
                            throw;
                        }
                        finally
                        {
                            if (restoreAfterRun && window != null && !window.IsDisposed)
                            {
                                window.Show();
                                window.WindowState = FormWindowState.Normal;
                                window.Activate();
                            }
                        }
                    });
            }
            return result;
        }
    }
}
