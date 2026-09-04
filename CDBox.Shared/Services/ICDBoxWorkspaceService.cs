namespace CDBox.Shared.Services
{
    /// <summary>
    /// 由主程序集提供的通用工作区启动能力。业务模块只声明需要打开
    /// 自己的工作区，不持有主程序集窗口类型或 WebView2 宿主。
    /// </summary>
    public interface ICDBoxWorkspaceService
    {
        void Open(string moduleId);
    }
}
