using System;
using CDBox.RealEstate.Services;
using CDBox.Shared.Modules;
using CDBox.Shared.Services;
using CDBox.Shared.UI;

namespace CDBox.RealEstate.Module
{
    public sealed class RealEstateModule : ICDBoxWorkspaceModule
    {
        public const string ModuleId = "realestate";
        public const string ModuleName = "CDBox 不动产";
        public const string ModuleVersion = "0.1.0";

        private ICDBoxLogger _logger;
        private RealEstateWorkspaceService _workspace;

        public string Id { get { return ModuleId; } }
        public string Name { get { return ModuleName; } }
        public string Version { get { return ModuleVersion; } }

        public void Initialize(ICDBoxServices services)
        {
            if (services == null) throw new ArgumentNullException("services");

            ICDBoxLogger logger = services.GetRequired<ICDBoxLogger>();
            ICDBoxPageService pageService =
                services.GetRequired<ICDBoxPageService>();

            _logger = logger;
            _workspace = new RealEstateWorkspaceService(
                pageService, logger);
            _logger.Info("RealEstate 模块初始化完成，版本 " + Version + "。");
        }

        public void OpenWorkspace()
        {
            if (_workspace == null)
                throw new InvalidOperationException(
                    "RealEstate 模块尚未初始化。");
            _workspace.Open(Version);
        }

        public void Shutdown()
        {
            if (_logger != null)
                _logger.Info("RealEstate 模块已关闭。");
            _workspace = null;
            _logger = null;
        }
    }
}
