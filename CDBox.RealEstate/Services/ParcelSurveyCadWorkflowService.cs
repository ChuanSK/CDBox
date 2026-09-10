using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using CDBox.RealEstate.Cad;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.Settings;
using CDBox.RealEstate.Services;
using CDBox.Shared.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace CDBox.RealEstate.Services
{
    internal sealed class ParcelSurveyCadWorkflowService
    {
        private readonly ParcelSurveyStore _store;
        private readonly ParcelBoundaryCadService _cad;
        private readonly ParcelMapSheetCadService _mapSheets;
        private readonly ParcelSurveyCadScopeService _scopes;
        private readonly ICDBoxNotificationService _notifications;

        public ParcelSurveyCadWorkflowService(ParcelSurveyStore store,
            ParcelBoundaryCadService cad, ParcelMapSheetCadService mapSheets,
            ParcelSurveyCadScopeService scopes,
            ICDBoxNotificationService notifications)
        {
            _store = store ?? throw new ArgumentNullException("store");
            _cad = cad ?? throw new ArgumentNullException("cad");
            _mapSheets = mapSheets
                ?? throw new ArgumentNullException("mapSheets");
            _scopes = scopes ?? throw new ArgumentNullException("scopes");
            _notifications = notifications
                ?? throw new ArgumentNullException("notifications");
        }

        public event Action EditorRefreshRequested;

        public bool IsMapSheetRecognitionPending
        {
            get { return _mapSheets.IsPending; }
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
            ParcelSurveyRecord record;
            try
            {
                record = _store.CreateOrSelectParcel(documentId, documentName,
                    selection.OwnerName, selection.SourceObjectHandle);
            }
            catch (InvalidOperationException ex)
            {
                Notify(ex.Message, CDBoxNotificationLevel.Warning);
                return null;
            }
            ParcelBoundaryRecognitionApplicator.Apply(record, selection);
            record.ParcelName = selection.OwnerName.Trim();
            ParcelBoundaryDescriptionGenerator.Apply(record, false);
            _store.Save(record);
            Notify("已保存独立宗地“" + record.ParcelName + "”（图元 "
                + record.Boundary.SourceObjectHandle + "）。",
                CDBoxNotificationLevel.Success);
            RecognizeMapSheet(record, false);
            return record;
        }

        public bool RecognizeMapSheet(ParcelSurveyRecord record,
            bool refreshEditorAfterMapSheet)
        {
            if (record == null) return false;
            record.Normalize();
            _store.Save(record);
            return _mapSheets.Start(record, delegate(
                ParcelMapSheetRecognitionResult result)
            {
                if (result != null && result.Success)
                {
                    ParcelSurveyFieldValue field = record.Field(
                        "parcel.mapSheetNumber");
                    field.TextValue = result.MapSheetNumber;
                    field.NumericValue = null;
                    field.Status = ParcelFieldStatus.Automatic;
                    field.Confirmed = true;
                    _store.Save(record);
                    Notify(result.Message, CDBoxNotificationLevel.Success);
                }
                else
                {
                    Notify(result == null
                            ? "图幅号识别未返回结果。" : result.Message,
                        CDBoxNotificationLevel.Warning);
                }
                if (refreshEditorAfterMapSheet)
                {
                    Action handler = EditorRefreshRequested;
                    if (handler != null) handler();
                }
            });
        }

        public void FinishMapSheetRecognition()
        {
            _mapSheets.FinishPending();
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
                    ParcelBoundaryInput.EditSegment(selected.Range,
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
            ParcelSurveyRecord last = null;
            int applied = 0;
            while (true)
            {
                ParcelBoundRangeSelection selected =
                    _cad.SelectBoundaryRangeAcrossParcels(records, true);
                if (selected == null) break;
                ParcelSurveyRecord record = records.FirstOrDefault(x =>
                    string.Equals(x.Id, selected.RecordId,
                        StringComparison.OrdinalIgnoreCase));
                if (record == null) continue;
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
                    ParcelBoundaryInput.EditSignature(selected.Range,
                        existing, segment,
                        record.Field("rights.ownerName").TextValue);
                if (group == null) break;
                record.Boundary.SignatureGroups.RemoveAll(x => SameRange(x,
                    group));
                record.Boundary.SignatureGroups.Add(group);
                ParcelBoundaryDescriptionGenerator.Apply(record, false);
                last = _store.Save(record);
                applied++;
            }
            if (applied > 0)
                Notify("已向对应宗地填入 " + applied
                    + " 组邻宗信息；连续选择已结束。",
                    CDBoxNotificationLevel.Success);
            return last;
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
