using System;
using System.Drawing;
using System.Windows.Forms;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioFrameSettingsWindow
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
                "图框设置",
                delegate
                {
                    return CDBoxStudioFrameSettingsPage.BuildStandaloneDocument(
                        CDBoxStudioSettingsStore.Load());
                },
                Route,
                "frame-settings");
            _current.Width = 1280;
            _current.Height = 850;
            _current.MinimumSize = new Size(980, 680);
            _current.FormClosed += delegate { _current = null; };
            _current.Show(owner ?? new AcadMainWindow());
        }

        private static CDBoxStudioRouteResult Route(
            CDBoxStudioRouteRequest request)
        {
            if (request != null && string.Equals(request.Name, "ready",
                StringComparison.OrdinalIgnoreCase))
                return new CDBoxStudioRouteResult { Handled = true };

            CDBoxStudioRouteResult result;
            if (!CDBoxStudioFrameSettingsRoutes.TryRoute(request, out result))
                return new CDBoxStudioRouteResult { Handled = false };

            if (result.ActionToRun != null)
            {
                CDBoxStudioAction inner = result.ActionToRun;
                result.ActionToRun = new CDBoxStudioAction(inner.Id,
                    inner.Title, inner.Category, inner.Description,
                    inner.CommandName, inner.BadgeText, inner.Kind,
                    inner.Enabled, true, delegate
                    {
                        CDBoxStudioWebPageForm window = _current;
                        if (window != null && !window.IsDisposed) window.Hide();
                        try
                        {
                            inner.Run();
                        }
                        finally
                        {
                            if (window != null && !window.IsDisposed)
                            {
                                window.Show();
                                window.WindowState = FormWindowState.Normal;
                                window.Activate();
                                window.RefreshPage();
                            }
                        }
                    });
            }
            return result;
        }
    }
}
