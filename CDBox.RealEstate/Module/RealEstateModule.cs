using System;
using CDBox.RealEstate.Cad;
using CDBox.RealEstate.Commands;
using CDBox.RealEstate.Services;
using CDBox.Shared.Modules;
using CDBox.Shared.Services;
using CDBox.Shared.UI;

namespace CDBox.RealEstate.Module
{
    public sealed class RealEstateModule : ICDBoxWorkspaceModule,
        ICDBoxCommandModule
    {
        public const string ModuleId = "realestate";
        public const string ModuleName = "CDBox 不动产";
        public const string ModuleVersion = "0.2.0";

        private ICDBoxLogger _logger;
        private RealEstateWorkspaceService _workspace;
        private BuildingLengthAnnotationCadService _buildingAnnotations;
        private BuildingLengthAnnotationSettingsService _buildingSettings;
        private ParcelSurveyEditorService _parcelSurveyEditor;

        public string Id { get { return ModuleId; } }
        public string Name { get { return ModuleName; } }
        public string Version { get { return ModuleVersion; } }

        public void Initialize(ICDBoxServices services)
        {
            if (services == null) throw new ArgumentNullException("services");

            ICDBoxLogger logger = services.GetRequired<ICDBoxLogger>();
            ICDBoxPageService pageService =
                services.GetRequired<ICDBoxPageService>();
            ICDBoxNotificationService notifications =
                services.GetRequired<ICDBoxNotificationService>();
            ICDBoxPromptService prompts =
                services.GetRequired<ICDBoxPromptService>();
            ICDBoxColorPickerService colors =
                services.GetRequired<ICDBoxColorPickerService>();

            _logger = logger;
            _workspace = new RealEstateWorkspaceService(
                pageService, logger);
            _buildingAnnotations = new BuildingLengthAnnotationCadService(
                prompts, notifications, logger);
            _buildingSettings = new BuildingLengthAnnotationSettingsService(
                pageService, colors, _buildingAnnotations);
            _parcelSurveyEditor = new ParcelSurveyEditorService(pageService,
                prompts, notifications, logger);
            _logger.Info("RealEstate 模块初始化完成，版本 " + Version + "。");
        }

        public void OpenWorkspace()
        {
            if (_workspace == null)
                throw new InvalidOperationException(
                    "RealEstate 模块尚未初始化。");
            _workspace.Open(Version);
        }

        public void ExecuteCommand(string commandId)
        {
            if (_workspace == null || _buildingAnnotations == null
                || _buildingSettings == null || _parcelSurveyEditor == null)
                throw new InvalidOperationException(
                    "RealEstate 模块尚未初始化。");
            if (string.Equals(commandId,
                RealEstateCommandCatalog.AnnotateBuildingLength,
                StringComparison.OrdinalIgnoreCase))
            {
                _buildingAnnotations.Execute();
                return;
            }
            if (string.Equals(commandId,
                RealEstateCommandCatalog.OpenBuildingLengthSettings,
                StringComparison.OrdinalIgnoreCase))
            {
                _buildingSettings.Open();
                return;
            }
            if (string.Equals(commandId,
                RealEstateCommandCatalog.OpenParcelSurveyEditor,
                StringComparison.OrdinalIgnoreCase))
            {
                _parcelSurveyEditor.Open();
                return;
            }
            if (string.Equals(commandId,
                RealEstateCommandCatalog.SelectParcel,
                StringComparison.OrdinalIgnoreCase))
            {
                _parcelSurveyEditor.SelectParcel();
                return;
            }
            if (string.Equals(commandId,
                RealEstateCommandCatalog.FillBoundarySegments,
                StringComparison.OrdinalIgnoreCase))
            {
                _parcelSurveyEditor.FillBoundarySegments();
                return;
            }
            if (string.Equals(commandId,
                RealEstateCommandCatalog.FillNeighborInformation,
                StringComparison.OrdinalIgnoreCase))
            {
                _parcelSurveyEditor.FillNeighborInformation();
                return;
            }
            throw new ArgumentException("未知的不动产命令：" + commandId,
                "commandId");
        }

        public void Shutdown()
        {
            if (_logger != null)
                _logger.Info("RealEstate 模块已关闭。");
            _workspace = null;
            _buildingAnnotations = null;
            _buildingSettings = null;
            _parcelSurveyEditor = null;
            _logger = null;
        }
    }
}
