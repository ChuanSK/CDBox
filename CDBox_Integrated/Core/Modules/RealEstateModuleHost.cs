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
    /// RealEstate 的固定入口与故障隔离边界。
    /// </summary>
    internal static class RealEstateModuleHost
    {
        private const string ModuleFileName = "CDBox.RealEstate.dll";
        private const string ModuleTypeName =
            "CDBox.RealEstate.Module.RealEstateModule";

        private static readonly object SyncRoot = new object();
        private static readonly ICDBoxLogger Logger =
            new CDBoxModuleLoggerAdapter("RealEstate");
        private static readonly ICDBoxNotificationService Notifications =
            new CDBoxModuleNotificationAdapter();

        private static bool _loadAttempted;
        private static ICDBoxWorkspaceModule _module;
        private static Exception _loadError;
        private static CDBoxModulePageService _pageService;

        public static bool IsAvailable
        {
            get
            {
                lock (SyncRoot) return _module != null;
            }
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
                    "未安装 RealEstate 模块。", modulePath);
                lock (SyncRoot) _loadError = missing;
                Logger.Warn(missing.Message + " 路径：" + modulePath);
                return;
            }

            var pageService = new CDBoxModulePageService();
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
            {
                Logger.Info("RealEstate 模块已安全加载。");
                return;
            }

            Logger.Error("RealEstate 模块加载失败。", result.Error);
            Notifications.Show(
                "CDBox 不动产",
                "不动产模块加载失败，现有 CDBox 功能仍可继续使用。",
                CDBoxNotificationLevel.Warning);
        }

        public static void OpenWorkspace()
        {
            Initialize();

            ICDBoxWorkspaceModule module;
            Exception loadError;
            lock (SyncRoot)
            {
                module = _module;
                loadError = _loadError;
            }

            if (module == null)
            {
                Logger.Warn("无法打开 RealEstate 工作区："
                    + (loadError == null ? "模块不可用。" : loadError.Message));
                Notifications.Show(
                    "CDBox 不动产",
                    "不动产模块未安装或加载失败，现有 CDBox 功能不受影响。",
                    CDBoxNotificationLevel.Warning);
                return;
            }

            try
            {
                module.OpenWorkspace();
            }
            catch (Exception ex)
            {
                Logger.Error("打开 RealEstate 工作区失败。", ex);
                Notifications.Show(
                    "CDBox 不动产",
                    "不动产工作区打开失败，现有 CDBox 功能仍可继续使用。",
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
            {
                try { module.Shutdown(); }
                catch (Exception ex)
                {
                    Logger.Error("RealEstate 模块关闭失败。", ex);
                }
            }

            if (pageService != null)
            {
                try { pageService.CloseAll(); }
                catch (Exception ex)
                {
                    Logger.Error("关闭 RealEstate 页面失败。", ex);
                }
            }
        }

        private static string ResolveModulePath()
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string directory = Path.GetDirectoryName(assemblyPath)
                ?? AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(directory, ModuleFileName);
        }
    }
}
