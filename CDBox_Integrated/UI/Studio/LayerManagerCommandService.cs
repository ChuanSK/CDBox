using System;
using CDBox.Shared.Services;
using CDBox.Shared.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class LayerManagerCommandService
    {
        private readonly ICDBoxPageService _pages;
        private readonly ICDBoxLogger _logger;

        public LayerManagerCommandService(ICDBoxPageService pages,
            ICDBoxLogger logger)
        {
            _pages = pages ?? throw new ArgumentNullException("pages");
            _logger = logger ?? throw new ArgumentNullException("logger");
        }

        public void OpenManager()
        {
            _pages.Show(LayerManagerPage.Create(_logger, _pages));
        }

        public void OpenRecognitionRules()
        {
            _pages.Show(LayerRecognitionRulesPage.Create(_logger));
        }
    }
}
