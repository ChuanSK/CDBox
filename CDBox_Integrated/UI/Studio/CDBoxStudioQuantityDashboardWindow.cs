using System.Windows.Forms;
using TCPipeAutoDraw.Core.Modules;

namespace TCPipeAutoDraw.UI.Studio
{
    /// <summary>
    /// 旧 Studio 调用点兼容门面。页面实现位于污水模块。
    /// </summary>
    internal static class CDBoxStudioQuantityDashboardWindow
    {
        public static void ShowWindow(IWin32Window owner)
        {
            WastewaterModuleHost.OpenQuantityDashboard();
        }
    }
}
