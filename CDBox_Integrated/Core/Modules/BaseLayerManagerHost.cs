using System;
using CDBox.Shared.Services;
using TCPipeAutoDraw.UI.Studio;

namespace TCPipeAutoDraw.Core.Modules
{
    /// <summary>
    /// 必装基础组件中的图层管理器宿主。图层分类是各业务
    /// 模块的公共前置，不再受 Common 可选组件安装状态影响。
    /// </summary>
    internal static class BaseLayerManagerHost
    {
        private static readonly object SyncRoot = new object();
        private static readonly ICDBoxLogger Logger =
            new CDBoxModuleLoggerAdapter("Base.LayerManager");

        private static CDBoxModulePageService _pages;
        private static LayerManagerCommandService _commands;

        public static void Initialize()
        {
            lock (SyncRoot)
            {
                if (_commands != null) return;
                _pages = new CDBoxModulePageService("基础组件");
                _commands = new LayerManagerCommandService(_pages, Logger);
            }
            Logger.Info("图层管理器基础边界已初始化。");
        }

        public static void OpenManager()
        {
            Initialize();
            LayerManagerCommandService commands;
            lock (SyncRoot) commands = _commands;
            commands.OpenManager();
        }

        public static void OpenRecognitionRules()
        {
            Initialize();
            LayerManagerCommandService commands;
            lock (SyncRoot) commands = _commands;
            commands.OpenRecognitionRules();
        }

        public static void Shutdown()
        {
            CDBoxModulePageService pages;
            lock (SyncRoot)
            {
                pages = _pages;
                _pages = null;
                _commands = null;
            }
            if (pages != null)
            {
                try { pages.CloseAll(); }
                catch (Exception ex)
                {
                    Logger.Error("关闭图层管理器页面失败。", ex);
                }
            }
        }
    }
}
