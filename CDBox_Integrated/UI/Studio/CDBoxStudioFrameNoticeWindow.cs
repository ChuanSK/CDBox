using System.Collections.Generic;
using System.Linq;
using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioFrameNoticeWindow
    {
        public static void ShowNotice(string title, IList<string> messages,
            System.Windows.Forms.IWin32Window owner = null)
        {
            string message = string.Join("\n",
                (messages ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => "? " + x.Trim()));
            if (string.IsNullOrWhiteSpace(message)) return;
            CDBoxMessageBox.Show(owner, message,
                string.IsNullOrWhiteSpace(title) ? "????" : title,
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Warning);
        }
    }
}
