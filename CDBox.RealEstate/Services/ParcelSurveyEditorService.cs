using System;
using CDBox.RealEstate.Settings;
using CDBox.RealEstate.UI;
using CDBox.Shared.UI;

namespace CDBox.RealEstate.Services
{
    internal sealed class ParcelSurveyEditorService
    {
        private readonly ICDBoxPageService _pages;
        private readonly ParcelSurveyStore _store;

        public ParcelSurveyEditorService(ICDBoxPageService pages)
        {
            _pages = pages ?? throw new ArgumentNullException("pages");
            _store = new ParcelSurveyStore();
        }

        public void Open()
        {
            _pages.Show(ParcelSurveyEditorPage.Create(_store));
        }
    }
}
