using System;
using System.IO;
using System.Reflection;
using CDBox.Shared.Modules;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using TCPipeAutoDraw.UI.Studio;

namespace TCPipeAutoDraw.Core.Modules
{
    /// <summary>
    /// 公共业务独立程序集的固定加载、命令转发和故障隔离边界。
    /// </summary>
    internal static class CommonModuleHost
    {
        private const string ModuleFileName = "CDBox.Common.dll";
        private const string ModuleTypeName =
            "CDBox.Common.Module.CommonModule";

        private static readonly object SyncRoot = new object();
        private static readonly ICDBoxLogger Logger =
            new CDBoxModuleLoggerAdapter("Common");
        private static readonly ICDBoxNotificationService Notifications =
            new CDBoxModuleNotificationAdapter();

        private static bool _loadAttempted;
        private static ICDBoxWorkspaceModule _module;
        private static Exception _loadError;
        private static CDBoxModulePageService _pageService;

        public static bool IsAvailable
        {
            get { lock (SyncRoot) return _module != null; }
        }

        public static void Initialize()
        {
            string modulePath;
            lock (SyncRoot)
            {
                if (_loadAttempted) return;
                _loadAttempted = true;
                modulePath = ResolveModulePath();
            }

            if (!File.Exists(modulePath))
            {
                var missing = new FileNotFoundException(
                    "未安装 Common 公共业务组件。", modulePath);
                lock (SyncRoot) _loadError = missing;
                Logger.Warn(missing.Message + " 路径：" + modulePath);
                return;
            }

            var pageService = new CDBoxModulePageService("公共业务");
            var services = new CDBoxServiceRegistry()
                .Register<ICDBoxLogger>(Logger)
                .Register<ICDBoxNotificationService>(Notifications)
                .Register<ICDBoxPageService>(pageService);
            CDBoxModuleLoadResult result =
                CDBoxKnownModuleLoader.LoadAndInitialize(
                    modulePath, ModuleTypeName, services);
            lock (SyncRoot)
            {
                _module = result.Module;
                _loadError = result.Error;
                _pageService = result.Success ? pageService : null;
            }
            if (result.Success)
                Logger.Info("Common 公共业务组件已安全加载。");
            else
                Logger.Error("Common 公共业务组件加载失败。", result.Error);
        }

        public static void OpenWorkspace()
        {
            Initialize();
            ICDBoxWorkspaceModule module;
            lock (SyncRoot) module = _module;
            if (module == null)
            {
                NotifyUnavailable();
                return;
            }
            try { module.OpenWorkspace(); }
            catch (Exception ex)
            {
                Logger.Error("打开公共业务工作区失败。", ex);
                Notifications.Show("CDBox 公共业务",
                    "公共业务工作区打开失败：" + ex.Message,
                    CDBoxNotificationLevel.Error);
            }
        }

        public static void ExecuteCommand(string commandId)
        {
            Initialize();
            ICDBoxCommandModule commandModule;
            lock (SyncRoot) commandModule = _module as ICDBoxCommandModule;
            if (commandModule == null)
            {
                NotifyUnavailable();
                return;
            }
            try { commandModule.ExecuteCommand(commandId); }
            catch (Exception ex)
            {
                Logger.Error("公共业务命令执行失败：" + commandId, ex);
                Notifications.Show("CDBox 公共业务",
                    "公共业务命令执行失败：" + ex.Message,
                    CDBoxNotificationLevel.Error);
            }
        }

        public static void Shutdown()
        {
            ICDBoxWorkspaceModule module;
            CDBoxModulePageService pageService;
            lock (SyncRoot)
            {
                module = _module;
                pageService = _pageService;
                _module = null;
                _pageService = null;
            }
            if (module != null)
                try { module.Shutdown(); }
                catch (Exception ex)
                {
                    Logger.Error("Common 公共业务组件关闭失败。", ex);
                }
            if (pageService != null)
                try { pageService.CloseAll(); }
                catch (Exception ex)
                {
                    Logger.Error("关闭公共业务页面失败。", ex);
                }
        }

        private static void NotifyUnavailable()
        {
            Exception error;
            lock (SyncRoot) error = _loadError;
            Logger.Warn("公共业务组件不可用："
                + (error == null ? "尚未初始化。" : error.Message));
            Notifications.Show("CDBox 公共业务",
                "公共业务组件未安装或加载失败，其他 CDBox 功能不受影响。",
                CDBoxNotificationLevel.Warning);
        }

        private static string ResolveModulePath()
        {
            string directory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location)
                ?? AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(directory, ModuleFileName);
        }
    }
}
