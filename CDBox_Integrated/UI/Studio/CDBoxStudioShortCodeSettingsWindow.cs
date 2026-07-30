using System;
using System.Drawing;
using System.Windows.Forms;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioShortCodeSettingsWindow
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
                "简码识别设置",
                delegate
                {
                    return CDBoxStudioShortCodeSettingsPage
                        .BuildStandaloneDocument(
                            CDBoxStudioSettingsStore.Load());
                },
                Route,
                "short-code-settings");
            _current.Width = 760;
            _current.Height = 480;
            _current.MinimumSize = new Size(620, 420);
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
            return CDBoxStudioShortCodeSettingsRoutes.TryRoute(request,
                out result)
                ? result
                : new CDBoxStudioRouteResult { Handled = false };
        }
    }
}
