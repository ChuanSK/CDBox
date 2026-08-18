using System;
using CDBox.RealEstate.Cad;
using CDBox.RealEstate.Settings;
using CDBox.RealEstate.UI;
using CDBox.Shared.Services;
using CDBox.Shared.UI;

namespace CDBox.RealEstate.Services
{
    internal sealed class ParcelSurveyEditorService
    {
        private readonly ICDBoxPageService _pages;
        private readonly ParcelSurveyStore _store;
        private readonly ParcelBoundaryCadService _cad;

        public ParcelSurveyEditorService(ICDBoxPageService pages,
            ICDBoxPromptService prompts,
            ICDBoxNotificationService notifications,
            ICDBoxLogger logger)
        {
            _pages = pages ?? throw new ArgumentNullException("pages");
            _store = new ParcelSurveyStore();
            _cad = new ParcelBoundaryCadService(prompts, notifications,
                logger);
        }

        public void Open()
        {
            _pages.Show(ParcelSurveyEditorPage.Create(_store, _cad));
        }
    }
}
