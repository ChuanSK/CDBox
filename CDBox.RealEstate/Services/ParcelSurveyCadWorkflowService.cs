using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using CDBox.RealEstate.Cad;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.Settings;
using CDBox.Shared.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace CDBox.RealEstate.Services
{
    internal sealed class ParcelSurveyCadWorkflowService
    {
        private readonly ParcelSurveyStore _store;
        private readonly ParcelBoundaryCadService _cad;
        private readonly ParcelSurveyCadScopeService _scopes;
        private readonly ICDBoxNotificationService _notifications;

        public ParcelSurveyCadWorkflowService(ParcelSurveyStore store,
            ParcelBoundaryCadService cad, ParcelSurveyCadScopeService scopes,
            ICDBoxNotificationService notifications)
        {
            _store = store ?? throw new ArgumentNullException("store");
            _cad = cad ?? throw new ArgumentNullException("cad");
            _scopes = scopes ?? throw new ArgumentNullException("scopes");
            _notifications = notifications
                ?? throw new ArgumentNullException("notifications");
        }

        public ParcelSurveyRecord SelectParcel()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                Notify("当前没有可用的 CAD 图纸。",
                    CDBoxNotificationLevel.Warning);
                return null;
            }
            ParcelBoundaryCadSelection selection =
                _cad.SelectOwnershipBoundary();
            if (selection == null) return null;
            string documentId = ParcelSurveyCadScopeService.GetDocumentId(
                document);
            string documentName = ParcelSurveyCadScopeService.GetDocumentName(
                document);
            ParcelSurveyRecord record = _store.CreateOrSelectParcel(
                documentId, documentName, selection.OwnerName,
                selection.SourceObjectHandle);
            ParcelBoundaryRecognitionApplicator.Apply(record, selection);
            record.ParcelName = selection.OwnerName.Trim();
            ParcelBoundaryDescriptionGenerator.Apply(record, false);
            _store.Save(record);
            Notify("已保存独立宗地“" + record.ParcelName + "”（图元 "
                + record.Boundary.SourceObjectHandle + "）。",
                CDBoxNotificationLevel.Success);
            return record;
        }

        public ParcelSurveyRecord FillBoundarySegments()
        {
            ParcelSurveyRecord record = CurrentParcel();
            if (record == null) return null;
            _cad.SelectBoundarySegmentsContinuously(record);
            ParcelBoundaryDescriptionGenerator.Apply(record, false);
            return _store.Save(record);
        }

        public ParcelSurveyRecord FillNeighborInformation()
        {
            ParcelSurveyRecord record = CurrentParcel();
            if (record == null) return null;
            ParcelBoundarySignatureGroupRecord group =
                _cad.SelectSignatureGroup(record);
            if (group == null) return null;
            record.Boundary.SignatureGroups.RemoveAll(x => SameRange(x,
                group));
            record.Boundary.SignatureGroups.Add(group);
            ParcelBoundaryDescriptionGenerator.Apply(record, false);
            return _store.Save(record);
        }

        private ParcelSurveyRecord CurrentParcel()
        {
            ParcelSurveyScopeContext scope = _scopes.CurrentContext(_store);
            if (!string.Equals(scope.ScopeType, "parcel",
                StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(scope.ParcelId))
            {
                Notify("请先使用“选择宗地”选择一条权属线。",
                    CDBoxNotificationLevel.Warning);
                return null;
            }
            return _store.Current(scope.DocumentId, scope.DocumentName,
                scope.ScopeType, scope.RegionId, scope.RegionName,
                scope.ParcelId, scope.ParcelName);
        }

        private void Notify(string message, CDBoxNotificationLevel level)
        {
            _notifications.Show("宗地调查", message, level);
        }

        private static bool SameRange(
            ParcelBoundarySignatureGroupRecord left,
            ParcelBoundarySignatureGroupRecord right)
        {
            return left != null && right != null
                && string.Equals(left.StartPointNumber ?? string.Empty,
                    right.StartPointNumber ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.EndPointNumber ?? string.Empty,
                    right.EndPointNumber ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
