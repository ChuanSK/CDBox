using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioSettingsWindow
    {
        private static CDBoxStudioWebPageForm _current;
        private static CDBoxStudioCommandRouter _router;

        public static void ShowWindow(IWin32Window owner)
        {
            if (_current != null && !_current.IsDisposed)
            {
                _current.WindowState = FormWindowState.Normal;
                _current.Activate();
                return;
            }

            CDBoxStudioSettings settings = CDBoxStudioSettingsStore.Load();
            _router = new CDBoxStudioCommandRouter(new Dictionary<string, CDBoxStudioAction>(StringComparer.OrdinalIgnoreCase), settings, delegate(string script)
            {
                if (_current != null && !_current.IsDisposed) _current.TryExecutePageScript(script);
            });
            _current = new CDBoxStudioWebPageForm("CDBox设置", CDBoxStudioSettingsPage.BuildStandaloneDocument, _router.Route, "cdbox-settings");
            _current.Width = 1180;
            _current.Height = 800;
            _current.MinimumSize = new Size(900, 660);
            _current.FormClosed += delegate
            {
                if (_router != null) _router.Dispose();
                _router = null;
                _current = null;
            };
            _current.Show(owner ?? new AcadMainWindow());
        }
    }
}
