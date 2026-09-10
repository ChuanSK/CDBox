using System;
using CDBox.Shared.Services;
using CDBox.Shared.UI;

namespace CDBox.Common.Services
{
    internal sealed class ExcelToCadCommandService : IDisposable
    {
        private readonly ICDBoxPageService _pages;
        private readonly ICDBoxLogger _logger;
        private readonly CDBoxUiSession _controller;

        public ExcelToCadCommandService(ICDBoxPageService pages,
            ICDBoxLogger logger)
        {
            _pages = pages ?? throw new ArgumentNullException("pages");
            _logger = logger ?? throw new ArgumentNullException("logger");
            _controller = new CDBoxUiSession("common.excel", logger);
        }

        public void Open()
        {
            _controller.RefreshDocument();
            _pages.Show(_controller.CreatePage());
        }

        public void Dispose()
        {
            _controller.Dispose();
        }
    }
}
