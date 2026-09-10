using System;
using CDBox.RealEstate.Cad;
using CDBox.RealEstate.Settings;
using CDBox.Shared.Services;
using CDBox.Shared.UI;

namespace CDBox.RealEstate.Services
{
    internal sealed class ParcelSurveyEditorService
    {
        private readonly ICDBoxPageService _pages;
        private readonly ParcelSurveyStore _store;
        private readonly ParcelBoundaryCadService _cad;
        private readonly ParcelMapSheetCadService _mapSheets;
        private readonly ParcelSurveyCadScopeService _scopes;
        private readonly ParcelSurveyCadWorkflowService _workflow;
        public Action AddBuilding { get; set; }

        public ParcelSurveyEditorService(ICDBoxPageService pages,
            ICDBoxPromptService prompts,
            ICDBoxNotificationService notifications,
            ICDBoxLogger logger)
        {
            _pages = pages ?? throw new ArgumentNullException("pages");
            _store = new ParcelSurveyStore();
            _cad = new ParcelBoundaryCadService(prompts, notifications,
                logger);
            _mapSheets = new ParcelMapSheetCadService(notifications, logger);
            _scopes = new ParcelSurveyCadScopeService(notifications);
            _workflow = new ParcelSurveyCadWorkflowService(_store, _cad,
                _mapSheets, _scopes, notifications);
            _workflow.EditorRefreshRequested += Open;
        }

        public void Open()
        {
            _pages.Show(CDBoxUiGateway.Call<CDBoxPageDefinition>("realestate.parcel", "Create",
                _store, _cad, _scopes, _workflow, AddBuilding));
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

        public void FinishMapSheetRecognition()
        {
            _workflow.FinishMapSheetRecognition();
        }
    }
}
