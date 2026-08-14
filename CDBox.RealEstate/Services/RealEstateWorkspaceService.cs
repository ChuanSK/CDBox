using System;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.UI;
using CDBox.Shared.Services;
using CDBox.Shared.UI;

namespace CDBox.RealEstate.Services
{
    internal sealed class RealEstateWorkspaceService
    {
        private readonly ICDBoxPageService _pageService;
        private readonly ICDBoxLogger _logger;

        public RealEstateWorkspaceService(
            ICDBoxPageService pageService,
            ICDBoxLogger logger)
        {
            _pageService = pageService
                ?? throw new ArgumentNullException("pageService");
            _logger = logger ?? throw new ArgumentNullException("logger");
        }

        public void Open(string moduleVersion)
        {
            var state = new RealEstateWorkspaceState
            {
                ModuleVersion = moduleVersion ?? string.Empty,
                StatusLabel = "模块边界已就绪",
                Message = "不动产生产功能将按后续业务开发文档逐项接入。"
            };

            _logger.Info("打开 CDBox 不动产工作区。");
            _pageService.Show(RealEstateHomePage.Create(state));
        }
    }
}
