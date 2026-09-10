using System;
using System.Runtime.CompilerServices;
using CDBox.Shared.Modules;
using CDBox.Shared.Services;
using CDBox.Wastewater.Commands;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using TCPipeAutoDraw.Modules.WastewaterResultTable;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using CDBox.Shared.UI;
using CDBox.Shared.Wastewater.Automation;
using CDBox.Shared.Wastewater.Drafting;
using CDBox.Shared.Wastewater.Cad;
using CDBox.Wastewater.Features.Annotation;
using CDBox.Wastewater.Features.LongitudinalProfile;
using CDBox.Wastewater.Features.SectionDrawing;
using CDBox.Wastewater.Features.Inspection;
using CDBox.Wastewater.Features.Sync;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;
using TCPipeAutoDraw.Modules.NodeAnnotation;
using TCPipeAutoDraw.Modules.SurfaceAreaAnnotation;
using TCPipeAutoDraw.Modules.SectionDrawing;
using TCPipeAutoDraw.Modules.LongitudinalProfile;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CDBox.Wastewater.Module
{
    /// <summary>
    /// 污水业务模块。独立承载工程量属性业务核心、CAD 属性存储
    /// 及污水管成果表；页面、浮窗和外观由基础 UI 服务统一提供。
    /// </summary>
    public sealed class WastewaterModule : ICDBoxWorkspaceModule,
        ICDBoxCommandModule, IQuantityAttributeEditorModule,
        IQuantityDashboardModule
    {
        public const string ModuleId = "wastewater";
        public const string ModuleName = "CDBox 污水管线";
        public const string ModuleVersion = "0.8.0";

        private ICDBoxLogger _logger;
        private IWastewaterResultTableDataSource _resultTableDataSource;
        private ICDBoxLayerClassificationService _layerClassification;
        private ICDBoxPageService _pages;
        private WastewaterQuantityAttributeEngine _quantityAttributeEngine;
        private WastewaterQuantityAttributeCadStore _quantityCadStore;
        private IQuantityAttributeEditorCadService _attributeEditorCad;
        private ICDBoxPageAppearanceService _pageAppearance;
        private CDBoxUiSession
            _attributeEditor;
        private CDBoxUiSession _quantityDashboard;
        private WastewaterQuantityDashboardScopeAdapter _scopeAdapter;
        private WastewaterInspectionService _inspection;
        private WastewaterSyncService _sync;
        private WastewaterAnnotationEngine _annotation;
        private WastewaterLongitudinalProfileEngine _longitudinalProfile;
        private WastewaterSectionDrawingEngine _sectionDrawing;
        private IWastewaterCadInteractionService _cadInteraction;
        private CDBoxUiSession _annotationSettingsPage;
        private CDBoxUiSession _sectionDrawingPage;
        private CDBoxUiSession
            _longitudinalProfileSettingsPage;

        public string Id { get { return ModuleId; } }
        public string Name { get { return ModuleName; } }
        public string Version { get { return ModuleVersion; } }

        public void Initialize(ICDBoxServices services)
        {
            if (services == null) throw new ArgumentNullException("services");
            _logger = services.GetRequired<ICDBoxLogger>();
            _resultTableDataSource = services
                .GetRequired<IWastewaterResultTableDataSource>();
            _layerClassification = services
                .GetRequired<ICDBoxLayerClassificationService>();
            _pages = services.GetRequired<ICDBoxPageService>();
            _attributeEditorCad = services
                .GetRequired<IQuantityAttributeEditorCadService>();
            WastewaterRuntimeServices.AttributeCad = _attributeEditorCad;
            _pageAppearance = services
                .GetRequired<ICDBoxPageAppearanceService>();
            ICDBoxColorPickerService colorPicker;
            if (!services.TryGet(out colorPicker))
                colorPicker = new UnavailableColorPickerService();
            IQuantityDashboardMonitorService monitor = services
                .GetRequired<IQuantityDashboardMonitorService>();
            WastewaterRuntimeServices.DashboardMonitor = monitor;
            WastewaterRuntimeServices.OpenAttributeEditor =
                OpenAttributeEditor;
            WastewaterRuntimeServices.LayerClassification =
                _layerClassification;
            WastewaterRuntimeServices.Prompts =
                services.GetRequired<ICDBoxPromptService>();
            WastewaterRuntimeServices.Logger = _logger;
            WastewaterRuntimeServices.ColorPicker =
                colorPicker;
            IWastewaterCadDataService cadData;
            if (services.TryGet(out cadData))
                WastewaterRuntimeServices.CadData = cadData;
            _quantityCadStore = new WastewaterQuantityAttributeCadStore();
            QuantityAttributeCadStoreRegistry.Register(_quantityCadStore);
            _quantityAttributeEngine =
                new WastewaterQuantityAttributeEngine();
            QuantityAttributeEngineRegistry.Register(
                _quantityAttributeEngine);
            _attributeEditor =
                new CDBoxUiSession("wastewater.attributes",
                    _attributeEditorCad, _pageAppearance, _logger);
            _quantityDashboard = new CDBoxUiSession("wastewater.dashboard",
                _pages, _pageAppearance, monitor, _logger);
            _scopeAdapter = new WastewaterQuantityDashboardScopeAdapter();
            QuantityDashboardScopeRegistry.Register(_scopeAdapter);
            _inspection = new WastewaterInspectionService();
            WastewaterInspectionRegistry.Register(_inspection);
            _sync = new WastewaterSyncService();
            WastewaterSyncRegistry.Register(_sync);
            _annotation = new WastewaterAnnotationEngine();
            WastewaterAnnotationRegistry.Register(_annotation);
            _longitudinalProfile =
                new WastewaterLongitudinalProfileEngine();
            WastewaterLongitudinalProfileRegistry.Register(
                _longitudinalProfile);
            _sectionDrawing = new WastewaterSectionDrawingEngine();
            WastewaterSectionDrawingRegistry.Register(_sectionDrawing);
            if (cadData != null)
            {
                InitializeCadInteraction();
                InitializeCadPages(colorPicker);
            }
            _annotationSettingsPage = new CDBoxUiSession("wastewater.annotations",
                _pageAppearance, colorPicker, new Action(RunSurfaceAreaAnnotation),
                new Action(RunPipeLengthAnnotation), new Action(RunNodeAnnotation));
            _logger.Info("Wastewater 模块边界初始化完成，版本 "
                + Version + "。工作台、工程量计算、检查同步、污水标注、"
                + "纵断面与批量断面核心已由独立程序集承载。");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InitializeCadInteraction()
        {
            _cadInteraction = new WastewaterCadInteractionService();
            WastewaterCadInteractionRegistry.Register(_cadInteraction);
            _cadInteraction.Initialize();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InitializeCadPages(ICDBoxColorPickerService colorPicker)
        {
            _sectionDrawingPage =
                new CDBoxUiSession("wastewater.sections", _pageAppearance,
                    _logger);
            _longitudinalProfileSettingsPage =
                new CDBoxUiSession("wastewater.longitudinal",
                    _pageAppearance, colorPicker, _logger);
            WastewaterRuntimeServices.OpenLongitudinalSettings =
                OpenLongitudinalProfileSettings;
        }

        public void OpenWorkspace()
        {
            // 旧版工作台命令作为兼容入口保留，但不再创建聚合工作台；
            // 直接进入仍受支持的独立工程量看板。
            OpenQuantityDashboard();
        }

        private sealed class UnavailableColorPickerService
            : ICDBoxColorPickerService
        {
            public bool TryPick(CDBoxModuleColor initial,
                out CDBoxModuleColor selected, bool allowByLayer,
                bool allowByBlock)
            {
                selected = initial == null
                    ? CDBoxModuleColor.FromIndex(7) : initial.Clone();
                return false;
            }
        }

        public void OpenAttributeEditor(string documentId,
            string objectHandle)
        {
            if (_pages == null || _attributeEditor == null)
                throw new InvalidOperationException(
                    "Wastewater 属性编辑器尚未初始化。");
            _attributeEditor.SetTarget(documentId, objectHandle);
            _pages.Show(_attributeEditor.CreatePage());
        }

        public void OpenQuantityDashboard()
        {
            if (_pages == null || _quantityDashboard == null)
                throw new InvalidOperationException(
                    "Wastewater 工程量看板尚未初始化。");
            _pages.Show(_quantityDashboard.CreatePage());
        }

        public void ExportFormalQuantityReport()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            WastewaterFormalQuantityReportCommand.Run(document, _logger,
                WastewaterRuntimeServices.Prompts);
        }

        public void ExecuteCommand(string commandId)
        {
            if (_logger == null)
                throw new InvalidOperationException(
                    "Wastewater 模块尚未初始化。");
            if (string.Equals(commandId,
                    WastewaterCommandCatalog.ExportFormalQuantityReport,
                    StringComparison.OrdinalIgnoreCase))
            {
                ExportFormalQuantityReport();
                return;
            }
            if (string.Equals(commandId,
                    WastewaterCommandCatalog.OpenAnnotationSettings,
                    StringComparison.OrdinalIgnoreCase))
            {
                OpenAnnotationSettings();
                return;
            }
            if (string.Equals(commandId,
                    WastewaterCommandCatalog.AnnotatePipeLength,
                    StringComparison.OrdinalIgnoreCase))
            {
                RunPipeLengthAnnotation();
                return;
            }
            if (string.Equals(commandId,
                    WastewaterCommandCatalog.AnnotateNode,
                    StringComparison.OrdinalIgnoreCase))
            {
                RunNodeAnnotation();
                return;
            }
            if (string.Equals(commandId,
                    WastewaterCommandCatalog.AnnotateSurfaceArea,
                    StringComparison.OrdinalIgnoreCase))
            {
                RunSurfaceAreaAnnotation();
                return;
            }
            if (string.Equals(commandId,
                    WastewaterCommandCatalog.FinishSurfaceAreaCass,
                    StringComparison.OrdinalIgnoreCase))
            {
                SurfaceAreaAnnotationService
                    .FinishPendingCassCommandAnnotation();
                return;
            }
            if (string.Equals(commandId,
                    WastewaterCommandCatalog.OpenSectionDrawing,
                    StringComparison.OrdinalIgnoreCase))
            {
                OpenSectionDrawing();
                return;
            }
            if (string.Equals(commandId,
                    WastewaterCommandCatalog.DrawSectionsBatch,
                    StringComparison.OrdinalIgnoreCase))
            {
                RunSectionDrawingBatch();
                return;
            }
            if (string.Equals(commandId,
                    WastewaterCommandCatalog.DrawLongitudinalProfile,
                    StringComparison.OrdinalIgnoreCase))
            {
                new LongitudinalProfileCommands().Generate();
                return;
            }
            if (string.Equals(commandId,
                    WastewaterCommandCatalog
                        .OpenLongitudinalProfileSettings,
                    StringComparison.OrdinalIgnoreCase))
            {
                OpenLongitudinalProfileSettings();
                return;
            }
            if (!string.Equals(commandId,
                    WastewaterCommandCatalog.DrawResultTable,
                    StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(
                    "未知的污水业务命令：" + commandId);

            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            try
            {
                WastewaterResultTableDrawingService.Run(document,
                    _resultTableDataSource, _layerClassification);
            }
            catch (Exception ex)
            {
                _logger.Error("污水管成果表生成失败。", ex);
                document.Editor.WriteMessage(
                    "\n[污水管成果表] 生成失败：" + ex.Message);
            }
        }

        private void OpenAnnotationSettings()
        {
            if (_pages == null || _annotationSettingsPage == null)
                throw new InvalidOperationException(
                    "污水标注设置页尚未初始化。");
            _pages.Show(_annotationSettingsPage.CreatePage());
        }

        private void RunPipeLengthAnnotation()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            PipeLengthAnnotationOptions options =
                PipeLengthAnnotationSettingsStore.Load();
            while (true)
            {
                PipeLengthAnnotationResult result =
                    PipeLengthAnnotationService.SelectCalculateAndAnnotate(
                        document, options);
                document.Editor.WriteHudMessage(result.ToEditorMessage());
                if (result.IsCancelled) break;
            }
        }

        private void RunSurfaceAreaAnnotation()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            SurfaceAreaAnnotationOptions options =
                SurfaceAreaAnnotationSettingsStore.Load();
            SurfaceAreaAnnotationResult result = SurfaceAreaAnnotationService
                .SelectCalculateAndAnnotate(document, options);
            document.Editor.WriteHudMessage(result.ToEditorMessage());
        }

        private void RunNodeAnnotation()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            NodeAnnotationOptions options = NodeAnnotationSettingsStore.Load();
            while (true)
            {
                NodeAnnotationResult result = NodeAnnotationService
                    .SelectAndAnnotate(document, options);
                document.Editor.WriteHudMessage(result.ToEditorMessage());
                if (!result.Success) break;
            }
        }

        private void OpenSectionDrawing()
        {
            if (_pages == null || _sectionDrawingPage == null)
                throw new InvalidOperationException(
                    "断面图页面尚未初始化。");
            _pages.Show(_sectionDrawingPage.CreatePage());
        }

        private void RunSectionDrawingBatch()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            SectionBatchDrawingResult result = SectionBatchDrawingService.Run(
                document, delegate(int current, int total, string message)
                {
                    if (!string.IsNullOrWhiteSpace(message)
                        && (current == 0 || current >= total))
                        document.Editor.WriteHudMessage("\n[批量断面] "
                            + message);
                });
            if (result != null && !string.IsNullOrWhiteSpace(result.Message))
                document.Editor.WriteHudMessage("\n[批量断面] "
                    + result.Message);
        }

        private void OpenLongitudinalProfileSettings()
        {
            if (_pages == null || _longitudinalProfileSettingsPage == null)
                throw new InvalidOperationException(
                    "纵断面设置页尚未初始化。");
            _pages.Show(_longitudinalProfileSettingsPage.CreatePage());
        }

        public void Shutdown()
        {
            if (_attributeEditor != null) _attributeEditor.Dispose();
            if (_quantityDashboard != null) _quantityDashboard.Dispose();
            if (_annotationSettingsPage != null) _annotationSettingsPage.Dispose();
            if (_sectionDrawingPage != null) _sectionDrawingPage.Dispose();
            if (_longitudinalProfileSettingsPage != null) _longitudinalProfileSettingsPage.Dispose();
            if (_cadInteraction != null)
                ShutdownCadRuntime();
            WastewaterSectionDrawingRegistry.Unregister(_sectionDrawing);
            WastewaterLongitudinalProfileRegistry.Unregister(
                _longitudinalProfile);
            WastewaterAnnotationRegistry.Unregister(_annotation);
            WastewaterSyncRegistry.Unregister(_sync);
            WastewaterInspectionRegistry.Unregister(_inspection);
            QuantityAttributeEngineRegistry.Unregister(
                _quantityAttributeEngine);
            QuantityAttributeCadStoreRegistry.Unregister(
                _quantityCadStore);
            QuantityDashboardScopeRegistry.Unregister(_scopeAdapter);
            if (_logger != null)
                _logger.Info("Wastewater 模块边界已关闭。");
            _resultTableDataSource = null;
            _layerClassification = null;
            _pages = null;
            _quantityAttributeEngine = null;
            _quantityCadStore = null;
            _attributeEditorCad = null;
            _pageAppearance = null;
            _attributeEditor = null;
            _quantityDashboard = null;
            _scopeAdapter = null;
            _inspection = null;
            _sync = null;
            _annotation = null;
            _longitudinalProfile = null;
            _sectionDrawing = null;
            _annotationSettingsPage = null;
            WastewaterRuntimeServices.LayerClassification = null;
            WastewaterRuntimeServices.Prompts = null;
            WastewaterRuntimeServices.AttributeCad = null;
            WastewaterRuntimeServices.OpenAttributeEditor = null;
            WastewaterRuntimeServices.DashboardMonitor = null;
            WastewaterRuntimeServices.ColorPicker = null;
            WastewaterRuntimeServices.Logger = null;
            _logger = null;
        }

        /// <summary>
        /// 隔离所有引用 AutoCAD 托管程序集的关闭逻辑。安装器、单元测试和
        /// 其他无 CAD 宿主只执行上面的纯业务清理，不会在 JIT 阶段解析
        /// Acdbmgd/AcMgd。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ShutdownCadRuntime()
        {
            try { _cadInteraction.Terminate(); }
            catch (Exception ex)
            {
                if (_logger != null)
                    _logger.Error("关闭污水 CAD 交互失败。", ex);
            }
            WastewaterCadInteractionRegistry.Unregister(_cadInteraction);
            _cadInteraction = null;
            _sectionDrawingPage = null;
            _longitudinalProfileSettingsPage = null;
            WastewaterRuntimeServices.CadData = null;
            WastewaterRuntimeServices.OpenLongitudinalSettings = null;
        }
    }
}
