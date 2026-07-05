using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace TCPipeAutoDraw.UI
{
    internal sealed class AcadMainWindow : IWin32Window
    {
        public IntPtr Handle
        {
            get { return Process.GetCurrentProcess().MainWindowHandle; }
        }
    }
}
