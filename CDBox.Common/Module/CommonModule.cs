using System;
using CDBox.Shared.Modules;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using CDBox.Common.Commands;
using CDBox.Common.Services;

namespace CDBox.Common.Module
{
    /// <summary>
    /// 公共业务的独立模块边界。基础组件仅保留 AutoCAD 命令名
    /// 和统一 UI，公共业务实现由本程序集承载。
    /// </summary>
    public sealed class CommonModule : ICDBoxWorkspaceModule,
        ICDBoxCommandModule
    {
        public const string ModuleId = "common";
        public const string ModuleName = "CDBox 公共业务";
        public const string ModuleVersion = "0.5.0";

        private ICDBoxLogger _logger;
        private ICDBoxPageService _pages;
        private ShortCodeRecognitionCommandService _shortCode;
        private FrameLayoutCommandService _frames;
        private ExcelToCadCommandService _excelToCad;

        public string Id { get { return ModuleId; } }
        public string Name { get { return ModuleName; } }
        public string Version { get { return ModuleVersion; } }

        public void Initialize(ICDBoxServices services)
        {
            if (services == null) throw new ArgumentNullException("services");
            _logger = services.GetRequired<ICDBoxLogger>();
            _pages = services.GetRequired<ICDBoxPageService>();
            _shortCode = new ShortCodeRecognitionCommandService(
                _pages, _logger);
            _frames = new FrameLayoutCommandService(_pages, _logger);
            _excelToCad = new ExcelToCadCommandService(_pages, _logger);
            _logger.Info("Common 模块边界初始化完成，版本 " + Version
                + "。简码识别、图框与 Excel 转 CAD 由公共业务程序集承载；图层管理器已归入基础组件。");
        }

        public void OpenWorkspace()
        {
            if (_pages == null)
                throw new InvalidOperationException("Common 模块尚未初始化。");
            _pages.Show(CDBoxUiGateway.Call<CDBoxPageDefinition>("common.home", "CreateWithTools", Version,
                new Action(_shortCode.OpenSettings), new Action(_frames.OpenSettings), new Action(_excelToCad.Open)));
        }

        public void ExecuteCommand(string commandId)
        {
            if (_shortCode == null)
                throw new InvalidOperationException("Common 模块尚未初始化。");
            if (string.Equals(commandId,
                    CommonCommandCatalog.RecognizeShortCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                _shortCode.Recognize();
                return;
            }
            if (string.Equals(commandId,
                    CommonCommandCatalog.OpenShortCodeSettings,
                    StringComparison.OrdinalIgnoreCase))
            {
                _shortCode.OpenSettings();
                return;
            }
            if (string.Equals(commandId,
                    CommonCommandCatalog.AddFrameTemplate,
                    StringComparison.OrdinalIgnoreCase))
            {
                _frames.AddTemplate();
                return;
            }
            if (string.Equals(commandId,
                    CommonCommandCatalog.PlaceFrameCutRegions,
                    StringComparison.OrdinalIgnoreCase))
            {
                _frames.PlaceCutRegions();
                return;
            }
            if (string.Equals(commandId,
                    CommonCommandCatalog.LayoutFrames,
                    StringComparison.OrdinalIgnoreCase))
            {
                _frames.LayoutFrames();
                return;
            }
            if (string.Equals(commandId,
                    CommonCommandCatalog.PlaceFramesDirectly,
                    StringComparison.OrdinalIgnoreCase))
            {
                _frames.PlaceFramesDirectly();
                return;
            }
            if (string.Equals(commandId,
                    CommonCommandCatalog.OpenFrameSettings,
                    StringComparison.OrdinalIgnoreCase))
            {
                _frames.OpenSettings();
                return;
            }
            if (string.Equals(commandId,
                    CommonCommandCatalog.ExcelToCad,
                    StringComparison.OrdinalIgnoreCase))
            {
                _excelToCad.Open();
                return;
            }
            throw new ArgumentException("未知的公共业务命令：" + commandId,
                "commandId");
        }

        public void Shutdown()
        {
            if (_logger != null) _logger.Info("Common 模块边界已关闭。");
            _shortCode = null;
            if (_excelToCad != null) _excelToCad.Dispose();
            _excelToCad = null;
            if (_frames != null) _frames.Dispose();
            _frames = null;
            _pages = null;
            _logger = null;
        }
    }
}
