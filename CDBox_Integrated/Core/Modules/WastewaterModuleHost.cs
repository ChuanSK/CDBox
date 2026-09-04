using System;
using System.IO;
using System.Reflection;
using CDBox.Shared.Modules;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using CDBox.Shared.Wastewater.Cad;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI.Studio;

namespace TCPipeAutoDraw.Core.Modules
{
    /// <summary>
    /// Wastewater 独立程序集的固定加载边界。基础组件只负责发现、
    /// 注册宿主服务和转发命令；模块缺失时明确提示，不再回退旧实现。
    /// </summary>
    internal static class WastewaterModuleHost
    {
        private const string ModuleFileName = "CDBox.Wastewater.dll";
        private const string ModuleTypeName =
            "CDBox.Wastewater.Module.WastewaterModule";

        private static readonly object SyncRoot = new object();
        private static readonly ICDBoxLogger Logger =
            new CDBoxModuleLoggerAdapter("Wastewater");
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
                    "未安装 Wastewater 模块。",
                    modulePath);
                lock (SyncRoot) _loadError = missing;
                Logger.Warn(missing.Message + " 路径：" + modulePath);
                return;
            }

            var pageService = new CDBoxModulePageService("污水业务");
            var services = new CDBoxServiceRegistry()
                .Register<ICDBoxLogger>(Logger)
                .Register<ICDBoxPageService>(pageService)
                .Register<ICDBoxCadCommandService>(
                    new CDBoxCadCommandAdapter())
                .Register<ICDBoxPageAppearanceService>(
                    new CDBoxPageAppearanceAdapter())
                .Register<ICDBoxColorPickerService>(
                    new CDBoxModuleColorPickerAdapter())
                .Register<ICDBoxPromptService>(
                    new CDBoxModulePromptAdapter())
                .Register<IWastewaterCadDataService>(
                    new WastewaterCadDataAdapter())
                .Register<IQuantityAttributeEditorCadService>(
                    new QuantityAttributeEditorCadAdapter())
                .Register<IQuantityDashboardMonitorService>(
                    new QuantityDashboardMonitorAdapter())
                .Register<IWastewaterResultTableDataSource>(
                    new WastewaterResultTableDataSourceAdapter())
                .Register<ICDBoxLayerClassificationService>(
                    new BaseLayerClassificationAdapter());
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
                Logger.Info("Wastewater 模块边界已安全加载。");
                return;
            }
            Logger.Error("Wastewater 模块边界加载失败。", result.Error);
        }

        public static void OpenWorkspace()
        {
            ICDBoxWorkspaceModule module;
            Exception loadError;
            lock (SyncRoot)
            {
                module = _module;
                loadError = _loadError;
            }

            if (module == null)
            {
                string reason = loadError == null
                    ? "尚未初始化。" : loadError.Message;
                Logger.Warn("Wastewater 模块边界不可用：" + reason);
                Notifications.Show("CDBox 污水管线",
                    "污水业务未安装或加载失败：" + reason,
                    CDBoxNotificationLevel.Warning);
                return;
            }

            try
            {
                module.OpenWorkspace();
            }
            catch (Exception ex)
            {
                Logger.Error("通过 Wastewater 模块打开工作台失败。", ex);
                Notifications.Show("CDBox 污水管线",
                    "污水管线工作台打开失败：" + ex.Message,
                    CDBoxNotificationLevel.Error);
            }
        }

        public static void ExecuteCommand(string commandId)
        {
            ICDBoxCommandModule commands;
            Exception loadError;
            lock (SyncRoot)
            {
                commands = _module as ICDBoxCommandModule;
                loadError = _loadError;
            }

            if (commands == null)
            {
                string reason = loadError == null
                    ? "污水业务模块尚未加载。" : loadError.Message;
                Logger.Warn("无法执行污水业务命令 " + commandId
                    + "：" + reason);
                Notifications.Show("CDBox 污水管线",
                    "污水业务未安装或加载失败：" + reason,
                    CDBoxNotificationLevel.Warning);
                return;
            }

            try
            {
                commands.ExecuteCommand(commandId);
            }
            catch (Exception ex)
            {
                Logger.Error("污水业务命令执行失败：" + commandId, ex);
                Notifications.Show("CDBox 污水管线",
                    "命令执行失败：" + ex.Message,
                    CDBoxNotificationLevel.Error);
            }
        }

        public static void OpenAttributeEditor(string documentId,
            string objectHandle)
        {
            IQuantityAttributeEditorModule editor;
            Exception loadError;
            lock (SyncRoot)
            {
                editor = _module as IQuantityAttributeEditorModule;
                loadError = _loadError;
            }
            if (editor == null)
            {
                string reason = loadError == null
                    ? "污水业务模块尚未加载。" : loadError.Message;
                Notifications.Show("CDBox 污水管线",
                    "属性编辑器不可用：" + reason,
                    CDBoxNotificationLevel.Warning);
                return;
            }
            try
            {
                editor.OpenAttributeEditor(documentId, objectHandle);
            }
            catch (Exception ex)
            {
                Logger.Error("打开污水属性编辑器失败。", ex);
                Notifications.Show("CDBox 污水管线",
                    "属性编辑器打开失败：" + ex.Message,
                    CDBoxNotificationLevel.Error);
            }
        }

        public static void OpenQuantityDashboard()
        {
            IQuantityDashboardModule dashboard;
            Exception loadError;
            lock (SyncRoot)
            {
                dashboard = _module as IQuantityDashboardModule;
                loadError = _loadError;
            }
            if (dashboard == null)
            {
                string reason = loadError == null
                    ? "污水业务模块尚未加载。" : loadError.Message;
                Notifications.Show("CDBox 污水管线",
                    "工程量看板不可用：" + reason,
                    CDBoxNotificationLevel.Warning);
                return;
            }
            try { dashboard.OpenQuantityDashboard(); }
            catch (Exception ex)
            {
                Logger.Error("打开工程量看板失败。", ex);
                Notifications.Show("CDBox 污水管线",
                    "工程量看板打开失败：" + ex.Message,
                    CDBoxNotificationLevel.Error);
            }
        }

        public static void ExportFormalQuantityReport()
        {
            IQuantityDashboardModule dashboard;
            lock (SyncRoot) dashboard = _module as IQuantityDashboardModule;
            if (dashboard == null)
            {
                Notifications.Show("CDBox 污水管线",
                    "正式工程量表不可用：污水业务未安装或未加载。",
                    CDBoxNotificationLevel.Warning);
                return;
            }
            try { dashboard.ExportFormalQuantityReport(); }
            catch (Exception ex)
            {
                Logger.Error("生成正式工程量表失败。", ex);
                Notifications.Show("CDBox 污水管线",
                    "正式工程量表生成失败：" + ex.Message,
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
            if (module == null) return;
            try { module.Shutdown(); }
            catch (Exception ex)
            {
                Logger.Error("Wastewater 模块边界关闭失败。", ex);
            }
            if (pageService != null)
                try { pageService.CloseAll(); }
                catch (Exception ex)
                {
                    Logger.Error("关闭污水业务页面失败。", ex);
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
