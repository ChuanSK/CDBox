using System.Windows.Forms;

namespace TCPipeAutoDraw.UI
{
    internal static class CDBoxMessageBox
    {
        public static DialogResult Show(string text)
        {
            return Show(null, text, "CDBox", MessageBoxButtons.OK,
                MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(string text, string caption)
        {
            return Show(null, text, caption, MessageBoxButtons.OK,
                MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(string text, string caption,
            MessageBoxButtons buttons)
        {
            return Show(null, text, caption, buttons,
                MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(null, text, caption, buttons, icon,
                MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton)
        {
            return Show(null, text, caption, buttons, icon, defaultButton);
        }

        public static DialogResult Show(IWin32Window owner, string text,
            string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(owner, text, caption, buttons, icon,
                MessageBoxDefaultButton.Button1);
        }

        public static DialogResult Show(IWin32Window owner, string text,
            string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton)
        {
            return CDBoxNotificationService.ShowDialog(owner, text,
                caption, buttons, icon, defaultButton);
        }
    }
}
