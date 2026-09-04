using System.Windows.Forms;
using TCPipeAutoDraw.Core.Modules;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace TCPipeAutoDraw.UI.Studio
{
    /// <summary>
    /// 旧调用点兼容壳。属性编辑器窗口实现已迁入 CDBox.Wastewater。
    /// </summary>
    internal static class CDBoxStudioQuantityAttributeEditorWindow
    {
        public static void ShowWindow(IWin32Window owner,
            QuantityPipeSelectionInfo info, string documentId)
        {
            WastewaterModuleHost.OpenAttributeEditor(documentId,
                info == null ? string.Empty : info.HandleText);
        }
    }
}
