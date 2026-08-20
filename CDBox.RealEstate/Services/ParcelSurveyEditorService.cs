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
        private readonly ParcelSurveyCadScopeService _scopes;
        private readonly ParcelSurveyCadWorkflowService _workflow;

        public ParcelSurveyEditorService(ICDBoxPageService pages,
            ICDBoxPromptService prompts,
            ICDBoxNotificationService notifications,
            ICDBoxLogger logger)
        {
            _pages = pages ?? throw new ArgumentNullException("pages");
            _store = new ParcelSurveyStore();
            _cad = new ParcelBoundaryCadService(prompts, notifications,
                logger);
            _scopes = new ParcelSurveyCadScopeService(prompts, notifications,
                logger);
            _workflow = new ParcelSurveyCadWorkflowService(_store, _cad,
                _scopes, notifications);
        }

        public void Open()
        {
            _pages.Show(ParcelSurveyEditorPage.Create(_store, _cad,
                _scopes, _workflow));
        }

        public void SelectParcel() { _workflow.SelectParcel(); }

        public void FillBoundarySegments()
        {
            _workflow.FillBoundarySegments();
        }

        public void FillNeighborInformation()
        {
            _workflow.FillNeighborInformation();
        }
    }
}
