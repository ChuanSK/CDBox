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
}
