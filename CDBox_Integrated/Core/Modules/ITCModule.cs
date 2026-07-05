using System;

namespace TCPipeAutoDraw.Core.Modules
{
    /// <summary>
    /// CDBOX 主界面的统一模块接口。
    /// 新功能只需要实现或注册为 ITCModule，就可以自动出现在工具箱中。
    /// </summary>
    public interface ITCModule
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        string CommandName { get; }
        bool Enabled { get; }
        void Run();
    }

    /// <summary>
    /// 基于委托的模块描述，适合把已有命令快速接入 CDBOX。
    /// 后续复杂模块也可以改成独立类实现 ITCModule。
    /// </summary>
    public sealed class TCModuleDescriptor : ITCModule
    {
        private readonly Action _runAction;

        public TCModuleDescriptor(string id, string name, string description, string commandName, bool enabled, Action runAction)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("模块 Id 不能为空。", "id");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("模块名称不能为空。", "name");

            Id = id;
            Name = name;
            Description = description ?? string.Empty;
            CommandName = commandName ?? string.Empty;
            Enabled = enabled;
            _runAction = runAction;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string CommandName { get; private set; }
        public bool Enabled { get; private set; }

        public void Run()
        {
            if (!Enabled) return;
            if (_runAction == null) return;
            _runAction();
        }
    }
}
