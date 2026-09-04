using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.Settings;
using CDBox.Shared.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace CDBox.RealEstate.Cad
{
    public sealed class ParcelSurveyCadScopeService
    {
        public const string RegionRegAppName = "CDBoxParcelSurveyRegion";

        private readonly ICDBoxNotificationService _notifications;

        public ParcelSurveyCadScopeService(
            ICDBoxNotificationService notifications)
        {
            _notifications = notifications
                ?? throw new ArgumentNullException("notifications");
        }

        public ParcelSurveyScopeContext CurrentContext(ParcelSurveyStore store)
        {
            if (store == null) throw new ArgumentNullException("store");
            Document current = AcadApp.DocumentManager.MdiActiveDocument;
            var context = new ParcelSurveyScopeContext();
            foreach (Document document in AcadApp.DocumentManager)
            {
                if (document == null) continue;
                context.Documents.Add(new ParcelSurveyDocumentInfo
                {
                    DocumentId = GetDocumentId(document),
                    DocumentName = GetDocumentName(document),
                    IsActive = current == document
                });
            }
            if (current == null) return context;

            context.DocumentId = GetDocumentId(current);
            context.DocumentName = GetDocumentName(current);
            context.Regions = GetRegions(current);
            context.Parcels = store.GetParcels(context.DocumentId);
            foreach (ParcelSurveyParcelInfo parcel in context.Parcels)
            {
                if (string.Equals(parcel.ScopeType, "region",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ParcelSurveyRegionInfo region = context.Regions
                        .FirstOrDefault(x => Same(x.RegionId,
                            parcel.RegionId));
                    parcel.BoundaryValid = (region != null
                        && region.BoundaryValid) || EntityExists(current,
                            parcel.SourceObjectHandle);
                    parcel.RegionHandle = region == null
                        ? string.Empty : region.Handle;
                    parcel.RegionOwnedBoundary = region != null
                        && region.OwnedBoundary;
                }
                else
                {
                    parcel.BoundaryValid = parcel.BoundaryValid
                        && EntityExists(current,
                            parcel.SourceObjectHandle);
                }
            }
            string scopeType = store.GetCurrentScopeType(context.DocumentId);
            string selectedId = string.Equals(scopeType, "parcel",
                StringComparison.OrdinalIgnoreCase)
                ? store.GetCurrentParcelId(context.DocumentId)
                : string.Equals(scopeType, "region",
                    StringComparison.OrdinalIgnoreCase)
                    ? store.GetCurrentRegionId(context.DocumentId)
                    : string.Empty;
            ParcelSurveyParcelInfo selected = context.Parcels.FirstOrDefault(
                x => Same(x.ScopeType, scopeType)
                    && Same(scopeType == "region" ? x.RegionId : x.ParcelId,
                        selectedId));
            if (selected == null)
            {
                if (!string.IsNullOrWhiteSpace(selectedId))
                    store.SelectScope(context.DocumentId, "whole",
                        string.Empty);
                return context;
            }
            context.RecordId = selected.RecordId;
            context.ScopeType = selected.ScopeType;
            context.RegionId = selected.RegionId;
            context.RegionName = selected.ParcelName;
            context.ParcelId = selected.ParcelId;
            context.ParcelName = selected.ParcelName;
            context.BindingValid = selected.BoundaryValid;
            return context;
        }

        public ParcelSurveyRegionInfo RenameRegion(string regionId,
            string name)
        {
            Document document = CurrentDocument();
            ParcelSurveyRegionInfo existing = FindRegion(document, regionId);
            if (existing == null) return null;
            string parcelName = (name ?? string.Empty).Trim();
            if (parcelName.Length == 0) return null;
            ObjectId id = FindRegionObjectId(document, regionId);
            using (document.LockDocument())
            using (Transaction transaction = document.Database
                .TransactionManager.StartTransaction())
            {
                Polyline polyline = transaction.GetObject(id,
                    OpenMode.ForWrite, false) as Polyline;
                WriteRegion(polyline, existing.RegionId, parcelName,
                    existing.CreatedAt, existing.OwnedBoundary);
                transaction.Commit();
            }
            return FindRegion(document, regionId);
        }

        public void DeleteRegion(string regionId)
        {
            Document document = CurrentDocument();
            ParcelSurveyRegionInfo info = FindRegion(document, regionId);
            if (info == null) return;
            ObjectId id = FindRegionObjectId(document, regionId);
            using (document.LockDocument())
            using (Transaction transaction = document.Database
                .TransactionManager.StartTransaction())
            {
                Polyline polyline = transaction.GetObject(id,
                    OpenMode.ForWrite, false) as Polyline;
                if (info.OwnedBoundary) polyline.Erase();
                else ClearRegion(polyline);
                transaction.Commit();
            }
        }

        public IList<ParcelSurveyRegionInfo> GetRegions(Document document)
        {
            var result = new List<ParcelSurveyRegionInfo>();
            if (document == null) return result;
            using (Transaction transaction = document.Database
                .TransactionManager.StartOpenCloseTransaction())
            {
                BlockTableRecord space = transaction.GetObject(
                    document.Database.CurrentSpaceId, OpenMode.ForRead,
                    false) as BlockTableRecord;
                if (space != null)
                    foreach (ObjectId id in space)
                    {
                        Polyline polyline;
                        try { polyline = transaction.GetObject(id,
                            OpenMode.ForRead, false) as Polyline; }
                        catch { continue; }
                        ParcelSurveyRegionInfo info = ReadRegion(polyline);
                        if (info != null) result.Add(info);
                    }
                transaction.Commit();
            }
            result.Sort((left, right) => string.Compare(left.RegionName,
                right.RegionName, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        public static string GetDocumentId(Document document)
        {
            if (document == null) return string.Empty;
            try
            {
                string fingerprint = Convert.ToString(
                    document.Database.FingerprintGuid,
                    CultureInfo.InvariantCulture) ?? string.Empty;
                Guid parsed;
                if (!string.IsNullOrWhiteSpace(fingerprint)
                    && (!Guid.TryParse(fingerprint, out parsed)
                        || parsed != Guid.Empty))
                    return "fp:" + fingerprint.Trim().ToLowerInvariant();
            }
            catch { }
            return "name:" + (document.Name ?? string.Empty).Trim()
                .ToLowerInvariant();
        }

        public static string GetDocumentName(Document document)
        {
            if (document == null) return "未打开图纸";
            string name = document.Name ?? string.Empty;
            try { name = Path.GetFileName(name); } catch { }
            return string.IsNullOrWhiteSpace(name) ? "未命名图纸" : name;
        }

        private Document CurrentDocument()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) Notify("当前没有可用的 CAD 图纸。",
                CDBoxNotificationLevel.Warning);
            return document;
        }

        private ParcelSurveyRegionInfo FindRegion(Document document,
            string regionId)
        {
            return GetRegions(document).FirstOrDefault(x => Same(
                x.RegionId, regionId));
        }

        private static ObjectId FindRegionObjectId(Document document,
            string regionId)
        {
            if (document == null || string.IsNullOrWhiteSpace(regionId))
                return ObjectId.Null;
            using (Transaction transaction = document.Database
                .TransactionManager.StartOpenCloseTransaction())
            {
                BlockTableRecord space = transaction.GetObject(
                    document.Database.CurrentSpaceId, OpenMode.ForRead,
                    false) as BlockTableRecord;
                if (space != null)
                    foreach (ObjectId id in space)
                    {
                        Polyline polyline;
                        try { polyline = transaction.GetObject(id,
                            OpenMode.ForRead, false) as Polyline; }
                        catch { continue; }
                        ParcelSurveyRegionInfo info = ReadRegion(polyline);
                        if (info != null && Same(info.RegionId, regionId))
                        {
                            transaction.Commit();
                            return id;
                        }
                    }
                transaction.Commit();
            }
            return ObjectId.Null;
        }

        private static ParcelSurveyRegionInfo ReadRegion(Polyline polyline)
        {
            if (polyline == null) return null;
            ResultBuffer buffer = polyline.GetXDataForApplication(
                RegionRegAppName);
            if (buffer == null) return null;
            TypedValue[] values = buffer.AsArray();
            if (values == null || values.Length < 4) return null;
            return new ParcelSurveyRegionInfo
            {
                RegionId = Convert.ToString(values[1].Value,
                    CultureInfo.InvariantCulture) ?? string.Empty,
                RegionName = Convert.ToString(values[2].Value,
                    CultureInfo.InvariantCulture) ?? string.Empty,
                CreatedAt = Convert.ToString(values[3].Value,
                    CultureInfo.InvariantCulture) ?? string.Empty,
                OwnedBoundary = values.Length >= 5 && string.Equals(
                    Convert.ToString(values[4].Value,
                        CultureInfo.InvariantCulture), "owned",
                    StringComparison.OrdinalIgnoreCase),
                Handle = polyline.Handle.ToString(),
                BoundaryValid = polyline.Closed
                    && polyline.NumberOfVertices >= 3
            };
        }

        private static void WriteRegion(Polyline polyline, string regionId,
            string regionName, string createdAt, bool owned)
        {
            if (polyline == null) throw new InvalidOperationException(
                "区域边界无效。");
            polyline.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName,
                    RegionRegAppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                    regionId ?? string.Empty),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                    regionName ?? string.Empty),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                    createdAt ?? string.Empty),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                    owned ? "owned" : "bound"));
        }

        private static void ClearRegion(Polyline polyline)
        {
            if (polyline == null) return;
            polyline.XData = new ResultBuffer(new TypedValue(
                (int)DxfCode.ExtendedDataRegAppName, RegionRegAppName));
        }

        private void Notify(string message, CDBoxNotificationLevel level)
        {
            _notifications.Show("地籍调查区域", message, level);
        }

        private static bool Same(string left, string right)
        {
            return string.Equals((left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool EntityExists(Document document,
            string handleText)
        {
            long value;
            if (document == null || string.IsNullOrWhiteSpace(handleText)
                || !long.TryParse(handleText, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out value)) return false;
            try
            {
                ObjectId id = document.Database.GetObjectId(false,
                    new Handle(value), 0);
                return !id.IsNull && !id.IsErased;
            }
            catch { return false; }
        }
    }
}
