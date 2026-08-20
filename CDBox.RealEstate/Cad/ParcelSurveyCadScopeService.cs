using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.Settings;
using CDBox.RealEstate.UI;
using CDBox.Shared.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace CDBox.RealEstate.Cad
{
    public sealed class ParcelSurveyCadScopeService
    {
        public const string RegionLayerName = "CDBox-地籍调查区域";
        public const string RegionRegAppName = "CDBoxParcelSurveyRegion";

        private readonly ICDBoxPromptService _prompts;
        private readonly ICDBoxNotificationService _notifications;
        private readonly ICDBoxLogger _logger;

        public ParcelSurveyCadScopeService(ICDBoxPromptService prompts,
            ICDBoxNotificationService notifications, ICDBoxLogger logger)
        {
            _prompts = prompts ?? throw new ArgumentNullException("prompts");
            _notifications = notifications
                ?? throw new ArgumentNullException("notifications");
            _logger = logger ?? throw new ArgumentNullException("logger");
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
            string scopeType = store.GetCurrentScopeType(context.DocumentId);
            if (string.Equals(scopeType, "parcel",
                StringComparison.OrdinalIgnoreCase))
            {
                string selectedParcel = store.GetCurrentParcelId(
                    context.DocumentId);
                ParcelSurveyParcelInfo parcel = context.Parcels.FirstOrDefault(
                    x => Same(x.ParcelId, selectedParcel));
                if (parcel != null)
                {
                    context.ScopeType = "parcel";
                    context.ParcelId = parcel.ParcelId;
                    context.ParcelName = parcel.ParcelName;
                    context.RegionName = string.Empty;
                    return context;
                }
                store.SelectScope(context.DocumentId, "whole",
                    string.Empty);
                return context;
            }
            if (!string.Equals(scopeType, "region",
                StringComparison.OrdinalIgnoreCase)) return context;
            string selected = store.GetCurrentRegionId(context.DocumentId);
            ParcelSurveyRegionInfo region = context.Regions.FirstOrDefault(x =>
                Same(x.RegionId, selected));
            if (region == null)
            {
                if (!string.IsNullOrWhiteSpace(selected))
                    store.SelectScope(context.DocumentId, "whole",
                        string.Empty);
                return context;
            }
            context.ScopeType = "region";
            context.RegionId = region.RegionId;
            context.RegionName = region.RegionName;
            return context;
        }

        public Document ActivateDocument(string documentId)
        {
            Document document = ResolveDocument(documentId);
            if (document == null)
                throw new InvalidOperationException("未找到目标图纸。");
            if (AcadApp.DocumentManager.MdiActiveDocument != document)
                AcadApp.DocumentManager.MdiActiveDocument = document;
            return document;
        }

        public ParcelSurveyRegionInfo CreateRectangleRegion()
        {
            Document document = CurrentDocument();
            if (document == null) return null;
            try
            {
                PromptPointResult first;
                using (_prompts.Begin("新建地籍区域",
                    "指定地籍调查区域的第一个角点；按 Esc 取消。"))
                    first = document.Editor.GetPoint("\n ");
                if (first.Status != PromptStatus.OK) return null;
                PromptPointResult second;
                using (_prompts.Begin("新建地籍区域",
                    "指定区域的对角点；按 Esc 取消。"))
                    second = document.Editor.GetCorner(new PromptCornerOptions(
                        "\n ", first.Value));
                if (second.Status != PromptStatus.OK) return null;
                string name;
                string fallback = "宗地区域 " + DateTime.Now.ToString(
                    "HHmmss", CultureInfo.InvariantCulture);
                if (!ParcelBoundaryCadDialogs.TryGetRegionName(
                    "新建地籍调查区域", fallback, out name)) return null;

                string regionId = Guid.NewGuid().ToString("N");
                string createdAt = DateTime.Now.ToString("o",
                    CultureInfo.InvariantCulture);
                ObjectId created = ObjectId.Null;
                using (document.LockDocument())
                using (Transaction transaction = document.Database
                    .TransactionManager.StartTransaction())
                {
                    EnsureLayer(document.Database, transaction);
                    EnsureRegApp(document.Database, transaction);
                    Point3d a = first.Value;
                    Point3d b = second.Value;
                    var polyline = new Polyline(4);
                    polyline.SetDatabaseDefaults(document.Database);
                    polyline.Layer = RegionLayerName;
                    polyline.AddVertexAt(0, new Point2d(a.X, a.Y), 0, 0, 0);
                    polyline.AddVertexAt(1, new Point2d(b.X, a.Y), 0, 0, 0);
                    polyline.AddVertexAt(2, new Point2d(b.X, b.Y), 0, 0, 0);
                    polyline.AddVertexAt(3, new Point2d(a.X, b.Y), 0, 0, 0);
                    polyline.Closed = true;
                    WriteRegion(polyline, regionId, name, createdAt, true);
                    BlockTableRecord space = (BlockTableRecord)transaction
                        .GetObject(document.Database.CurrentSpaceId,
                            OpenMode.ForWrite);
                    created = space.AppendEntity(polyline);
                    transaction.AddNewlyCreatedDBObject(polyline, true);
                    transaction.Commit();
                }
                Notify("已新建地籍调查区域“" + name + "”。",
                    CDBoxNotificationLevel.Success);
                return ReadRegion(document, created);
            }
            catch (Exception ex)
            {
                _logger.Error("新建地籍调查区域失败。", ex);
                Notify("新建地籍调查区域失败：" + ex.Message,
                    CDBoxNotificationLevel.Error);
                return null;
            }
        }

        public ParcelSurveyRegionInfo BindExistingRegion()
        {
            Document document = CurrentDocument();
            if (document == null) return null;
            try
            {
                var options = new PromptEntityOptions("\n ");
                options.SetRejectMessage("\n请选择闭合二维多段线。\n");
                options.AddAllowedClass(typeof(Polyline), true);
                PromptEntityResult selected;
                using (_prompts.Begin("选择地籍区域",
                    "选择已有闭合多段线作为地籍调查区域；按 Esc 取消。"))
                    selected = document.Editor.GetEntity(options);
                if (selected.Status != PromptStatus.OK) return null;
                string name;
                string fallback = "宗地区域 " + DateTime.Now.ToString(
                    "HHmmss", CultureInfo.InvariantCulture);
                if (!ParcelBoundaryCadDialogs.TryGetRegionName(
                    "命名地籍调查区域", fallback, out name)) return null;

                string regionId = Guid.NewGuid().ToString("N");
                string createdAt = DateTime.Now.ToString("o",
                    CultureInfo.InvariantCulture);
                using (document.LockDocument())
                using (Transaction transaction = document.Database
                    .TransactionManager.StartTransaction())
                {
                    Polyline polyline = transaction.GetObject(selected.ObjectId,
                        OpenMode.ForWrite, false) as Polyline;
                    if (polyline == null || !polyline.Closed
                        || polyline.NumberOfVertices < 3)
                        throw new InvalidOperationException(
                            "区域边界必须闭合且至少包含三个顶点。");
                    EnsureRegApp(document.Database, transaction);
                    WriteRegion(polyline, regionId, name, createdAt, false);
                    transaction.Commit();
                }
                Notify("已绑定地籍调查区域“" + name + "”。",
                    CDBoxNotificationLevel.Success);
                return FindRegion(document, regionId);
            }
            catch (Exception ex)
            {
                _logger.Error("绑定地籍调查区域失败。", ex);
                Notify("绑定地籍调查区域失败：" + ex.Message,
                    CDBoxNotificationLevel.Error);
                return null;
            }
        }

        public ParcelSurveyRegionInfo RenameRegion(string regionId)
        {
            Document document = CurrentDocument();
            ParcelSurveyRegionInfo existing = FindRegion(document, regionId);
            if (existing == null) throw new InvalidOperationException(
                "未找到地籍调查区域。");
            string name;
            if (!ParcelBoundaryCadDialogs.TryGetRegionName("重命名地籍区域",
                existing.RegionName, out name)) return null;
            ObjectId id = FindRegionObjectId(document, regionId);
            using (document.LockDocument())
            using (Transaction transaction = document.Database
                .TransactionManager.StartTransaction())
            {
                Polyline polyline = transaction.GetObject(id,
                    OpenMode.ForWrite, false) as Polyline;
                WriteRegion(polyline, existing.RegionId, name,
                    existing.CreatedAt, existing.OwnedBoundary);
                transaction.Commit();
            }
            return FindRegion(document, regionId);
        }

        public void DeleteRegion(string regionId)
        {
            Document document = CurrentDocument();
            ParcelSurveyRegionInfo info = FindRegion(document, regionId);
            if (info == null) throw new InvalidOperationException(
                "未找到地籍调查区域。");
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

        public void LocateRegion(string regionId)
        {
            Document document = CurrentDocument();
            ObjectId id = FindRegionObjectId(document, regionId);
            if (id.IsNull) throw new InvalidOperationException(
                "未找到地籍调查区域。");
            document.Editor.SetImpliedSelection(new[] { id });
            Extents3d bounds;
            using (Transaction transaction = document.Database
                .TransactionManager.StartOpenCloseTransaction())
            {
                Entity entity = transaction.GetObject(id, OpenMode.ForRead,
                    false) as Entity;
                bounds = entity.GeometricExtents;
                transaction.Commit();
            }
            using (ViewTableRecord view = document.Editor.GetCurrentView())
            {
                view.CenterPoint = new Point2d(
                    (bounds.MinPoint.X + bounds.MaxPoint.X) / 2,
                    (bounds.MinPoint.Y + bounds.MaxPoint.Y) / 2);
                view.Width = Math.Max(1,
                    (bounds.MaxPoint.X - bounds.MinPoint.X) * 1.25);
                view.Height = Math.Max(1,
                    (bounds.MaxPoint.Y - bounds.MinPoint.Y) * 1.25);
                document.Editor.SetCurrentView(view);
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

        private Document ResolveDocument(string documentId)
        {
            foreach (Document document in AcadApp.DocumentManager)
                if (document != null && Same(GetDocumentId(document),
                    documentId)) return document;
            return null;
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

        private ParcelSurveyRegionInfo ReadRegion(Document document,
            ObjectId id)
        {
            if (document == null || id.IsNull) return null;
            using (Transaction transaction = document.Database
                .TransactionManager.StartOpenCloseTransaction())
            {
                Polyline polyline = transaction.GetObject(id,
                    OpenMode.ForRead, false) as Polyline;
                ParcelSurveyRegionInfo info = ReadRegion(polyline);
                transaction.Commit();
                return info;
            }
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

        private static void EnsureRegApp(Database database,
            Transaction transaction)
        {
            RegAppTable table = (RegAppTable)transaction.GetObject(
                database.RegAppTableId, OpenMode.ForRead);
            if (table.Has(RegionRegAppName)) return;
            table.UpgradeOpen();
            var record = new RegAppTableRecord { Name = RegionRegAppName };
            table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        private static void EnsureLayer(Database database,
            Transaction transaction)
        {
            LayerTable table = (LayerTable)transaction.GetObject(
                database.LayerTableId, OpenMode.ForRead);
            LayerTableRecord layer;
            if (table.Has(RegionLayerName))
            {
                layer = transaction.GetObject(table[RegionLayerName],
                    OpenMode.ForWrite, false) as LayerTableRecord;
            }
            else
            {
                table.UpgradeOpen();
                layer = new LayerTableRecord { Name = RegionLayerName };
                table.Add(layer);
                transaction.AddNewlyCreatedDBObject(layer, true);
            }
            if (layer == null) return;
            layer.IsPlottable = false;
            layer.Color = Color.FromColorIndex(ColorMethod.ByAci, 8);
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
    }
}
