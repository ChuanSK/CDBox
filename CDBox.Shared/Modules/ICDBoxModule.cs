using CDBox.Shared.Services;

namespace CDBox.Shared.Modules
{
    /// <summary>
    /// CDBox 业务模块的最小生命周期契约。
    /// </summary>
    public interface ICDBoxModule
    {
        string Id { get; }
        string Name { get; }
        string Version { get; }

        void Initialize(ICDBoxServices services);
        void Shutdown();
    }

    /// <summary>
    /// 具有统一 CDBox 工作区入口的业务模块。
    /// </summary>
    public interface ICDBoxWorkspaceModule : ICDBoxModule
    {
        void OpenWorkspace();
    }

    /// <summary>
    /// 由主程序集中的 AutoCAD 命令桥转发到业务模块的命令契约。
    /// </summary>
    public interface ICDBoxCommandModule : ICDBoxModule
    {
        void ExecuteCommand(string commandId);
    }
}
