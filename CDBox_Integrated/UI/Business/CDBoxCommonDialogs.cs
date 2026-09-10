using System.Windows.Forms;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxCommonDialogs
    {
        public static void SubscribeIdle(System.EventHandler handler) { Application.Idle += handler; }
        public static void UnsubscribeIdle(System.EventHandler handler) { Application.Idle -= handler; }
        public static string SaveFile(string title, string filter, string fileName, string extension)
        {
            using (var dialog = new SaveFileDialog
            {
                Title = title, Filter = filter, FileName = fileName,
                AddExtension = true, DefaultExt = extension
            })
                return dialog.ShowDialog(new AcadMainWindow()) == DialogResult.OK
                    ? dialog.FileName : null;
        }

        public static void Notify(string message, string title)
        {
            CDBoxMessageBox.Show(new AcadMainWindow(), message, title,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
