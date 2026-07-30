using System;
using System.Drawing;
using System.Windows.Forms;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class
        CDBoxStudioLongitudinalProfileSettingsWindow
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
                "纵断面设置",
                delegate
                {
                    return CDBoxStudioLongitudinalProfileSettingsPage
                        .BuildStandaloneDocument(
                            CDBoxStudioSettingsStore.Load());
                },
                Route,
                "longitudinal-profile-settings");
            _current.Width = 1080;
            _current.Height = 760;
            _current.MinimumSize = new Size(760, 560);
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
            return CDBoxStudioLongitudinalProfileSettingsRoutes.TryRoute(
                request, out result)
                ? result
                : new CDBoxStudioRouteResult { Handled = false };
        }
    }
}
