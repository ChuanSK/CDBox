using System.Windows.Forms;

namespace TCPipeAutoDraw.UI
{
    internal static class CDBoxPromptDialog
    {
        public static DialogResult ShowYesNo(IWin32Window owner,
            string title, string message, string yesText, string noText,
            out bool doNotAskAgain)
        {
            return CDBoxNotificationService.ShowYesNoWithOption(owner,
                title, message, yesText, noText, out doNotAskAgain);
        }
    }
}
