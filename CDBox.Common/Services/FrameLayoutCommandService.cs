using System;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using TCPipeAutoDraw.Modules.FrameLayout;
using TCPipeAutoDraw.UI.Studio;

namespace CDBox.Common.Services
{
    internal sealed class FrameLayoutCommandService : IDisposable
    {
        private readonly ICDBoxPageService _pages;
        private readonly ICDBoxLogger _logger;

        public FrameLayoutCommandService(ICDBoxPageService pages,
            ICDBoxLogger logger)
        {
            _pages = pages ?? throw new ArgumentNullException("pages");
            _logger = logger ?? throw new ArgumentNullException("logger");
            CDBoxStudioFrameSettingsWindow.ShowAction = OpenSettings;
        }

        public void AddTemplate() { Run("添加图框模板",
            new FrameLayoutCommands().AddFrameTemplate); }

        public void PlaceCutRegions() { Run("布置裁图区域",
            new FrameLayoutCommands().PlaceCutRegions); }

        public void LayoutFrames() { Run("裁图区域布框",
            new FrameLayoutCommands().LayoutFrames); }

        public void PlaceFramesDirectly() { Run("直接布置图框",
            new FrameLayoutCommands().PlaceFramesDirectly); }

        public void OpenSettings()
        {
            _pages.Show(CDBoxUiGateway.Call<CDBoxPageDefinition>("common.frame", "Create", this, _logger));
        }

        public Action ResolveAction(string action)
        {
            switch ((action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "add": return AddTemplate;
                case "cut": return PlaceCutRegions;
                case "layout": return LayoutFrames;
                case "place": return PlaceFramesDirectly;
                default:
                    throw new InvalidOperationException("未知图框操作。");
            }
        }

        public void Dispose()
        {
            CDBoxStudioFrameSettingsWindow.ShowAction = null;
        }

        private void Run(string title, Action action)
        {
            try
            {
                action();
                _logger.Info(title + "命令已结束。");
            }
            catch (Exception ex)
            {
                _logger.Error(title + "失败。", ex);
                throw;
            }
        }
    }
}
