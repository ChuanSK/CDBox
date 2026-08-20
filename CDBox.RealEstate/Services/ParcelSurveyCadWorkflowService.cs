using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using CDBox.RealEstate.Cad;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.Settings;
using CDBox.RealEstate.UI;
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
            return SelectParcel(null);
        }

        public ParcelSurveyRecord SelectParcel(ParcelSurveyRecord boundRegion)
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
            bool attachRegion = boundRegion != null
                && string.Equals(boundRegion.ScopeType, "region",
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(boundRegion.DocumentId, documentId,
                    StringComparison.OrdinalIgnoreCase);
            ParcelSurveyRecord record = attachRegion ? boundRegion
                : _store.CreateOrSelectParcel(documentId, documentName,
                    selection.OwnerName, selection.SourceObjectHandle);
            ParcelBoundaryRecognitionApplicator.Apply(record, selection);
            record.ParcelName = selection.OwnerName.Trim();
            if (attachRegion)
            {
                record.RegionName = record.ParcelName;
                _scopes.RenameRegion(record.RegionId, record.ParcelName);
            }
            ParcelBoundaryDescriptionGenerator.Apply(record, false);
            _store.Save(record);
            Notify("已保存独立宗地“" + record.ParcelName + "”（图元 "
                + record.Boundary.SourceObjectHandle + "）。",
                CDBoxNotificationLevel.Success);
            return record;
        }

        public ParcelSurveyRecord FillBoundarySegments()
        {
            IList<ParcelSurveyRecord> records = RecordsForCurrentDocument();
            if (records.Count == 0) return null;
            ParcelSurveyRecord last = null;
            int applied = 0;
            while (true)
            {
                ParcelBoundRangeSelection selected =
                    _cad.SelectBoundaryRangeAcrossParcels(records, false);
                if (selected == null) break;
                ParcelSurveyRecord record = records.FirstOrDefault(x =>
                    string.Equals(x.Id, selected.RecordId,
                        StringComparison.OrdinalIgnoreCase));
                if (record == null) continue;
                ParcelBoundarySegmentRecord existing = record.Boundary.Segments
                    .FirstOrDefault(x => SameRange(x.StartPointNumber,
                        x.EndPointNumber, selected.Range.StartPointNumber,
                        selected.Range.EndPointNumber));
                ParcelBoundarySegmentRecord segment =
                    ParcelBoundaryCadDialogs.EditSegment(selected.Range,
                        existing);
                if (segment == null) break;
                ParcelBoundaryCadService.ApplySegment(record, segment);
                ParcelBoundaryDescriptionGenerator.Apply(record, false);
                last = _store.Save(record);
                applied++;
            }
            if (applied > 0)
                Notify("已向对应宗地填入 " + applied
                    + " 个界址段。", CDBoxNotificationLevel.Success);
            return last;
        }

        public ParcelSurveyRecord FillNeighborInformation()
        {
            IList<ParcelSurveyRecord> records = RecordsForCurrentDocument();
            if (records.Count == 0) return null;
            ParcelBoundRangeSelection selected =
                _cad.SelectBoundaryRangeAcrossParcels(records, true);
            if (selected == null) return null;
            ParcelSurveyRecord record = records.FirstOrDefault(x =>
                string.Equals(x.Id, selected.RecordId,
                    StringComparison.OrdinalIgnoreCase));
            if (record == null) return null;
            ParcelBoundarySignatureGroupRecord existing = record.Boundary
                .SignatureGroups.FirstOrDefault(x => SameRange(
                    x.StartPointNumber, x.EndPointNumber,
                    selected.Range.StartPointNumber,
                    selected.Range.EndPointNumber));
            ParcelBoundarySegmentRecord segment = record.Boundary.Segments
                .FirstOrDefault(x => SameRange(x.StartPointNumber,
                    x.EndPointNumber, selected.Range.StartPointNumber,
                    selected.Range.EndPointNumber));
            ParcelBoundarySignatureGroupRecord group =
                ParcelBoundaryCadDialogs.EditSignature(selected.Range,
                    existing, segment,
                    record.Field("rights.ownerName").TextValue);
            if (group == null) return null;
            record.Boundary.SignatureGroups.RemoveAll(x => SameRange(x,
                group));
            record.Boundary.SignatureGroups.Add(group);
            ParcelBoundaryDescriptionGenerator.Apply(record, false);
            return _store.Save(record);
        }

        private IList<ParcelSurveyRecord> RecordsForCurrentDocument()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                Notify("当前没有可用的 CAD 图纸。",
                    CDBoxNotificationLevel.Warning);
                return new List<ParcelSurveyRecord>();
            }
            string documentId = ParcelSurveyCadScopeService.GetDocumentId(
                document);
            ParcelSurveyScopeContext context = _scopes.CurrentContext(_store);
            var validIds = new HashSet<string>(context.Parcels
                .Where(x => x.BoundaryValid).Select(x => x.RecordId),
                StringComparer.OrdinalIgnoreCase);
            IList<ParcelSurveyRecord> records = _store.GetBoundRecords(
                documentId).Where(x => validIds.Contains(x.Id)
                    && x.Boundary.Points.Count >= 2).ToList();
            if (records.Count == 0)
                Notify("当前图纸没有已绑定权属线和界址点的宗地。",
                    CDBoxNotificationLevel.Warning);
            return records;
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

        private static bool SameRange(string leftStart, string leftEnd,
            string rightStart, string rightEnd)
        {
            return string.Equals(leftStart ?? string.Empty,
                    rightStart ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(leftEnd ?? string.Empty,
                    rightEnd ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
