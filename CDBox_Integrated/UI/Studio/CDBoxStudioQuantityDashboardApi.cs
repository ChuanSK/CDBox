using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioQuantityDashboardApi
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 128 };

        public static string Serialize(object value) { return Serializer.Serialize(value); }

        public static T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return Serializer.Deserialize<T>(json);
        }

        public static QuantityDashboardContext GetContext(string payload)
        {
            QuantityDashboardRequest request = Deserialize<QuantityDashboardRequest>(payload) ?? new QuantityDashboardRequest();
            return QuantityDashboardService.BuildContext(request);
        }

        public static QuantityDashboardSnapshot GetSnapshot(string payload, Action<int, int, string> progress)
        {
            QuantityDashboardRequest request = Deserialize<QuantityDashboardRequest>(payload) ?? new QuantityDashboardRequest();
            return QuantityDashboardService.BuildSnapshot(request, progress);
        }

        public static QuantityDashboardContext CreateRectangleRegion(string payload)
        {
            QuantityDashboardRegionRequest request = Deserialize<QuantityDashboardRegionRequest>(payload) ?? new QuantityDashboardRegionRequest();
            Document doc = QuantityDashboardService.ResolveDocument(request.documentId);
            if (doc == null) throw new InvalidOperationException("未找到目标图纸。");
            QuantityDashboardRegionInfo region = QuantityDashboardRegionService.CreateRectangleRegion(doc);
            QuantityDashboardRequest next = new QuantityDashboardRequest
            {
                documentId = QuantityDashboardService.GetDocumentId(doc), scopeType = region == null ? "whole" : "region",
                regionId = region == null ? string.Empty : region.regionId, liveMode = true
            };
            return QuantityDashboardService.BuildContext(next);
        }

        public static QuantityDashboardContext BindExistingRegion(string payload)
        {
            QuantityDashboardRegionRequest request = Deserialize<QuantityDashboardRegionRequest>(payload) ?? new QuantityDashboardRegionRequest();
            Document doc = QuantityDashboardService.ResolveDocument(request.documentId);
            if (doc == null) throw new InvalidOperationException("未找到目标图纸。");
            QuantityDashboardRegionInfo region = QuantityDashboardRegionService.BindExistingClosedPolyline(doc);
            QuantityDashboardRequest next = new QuantityDashboardRequest
            {
                documentId = QuantityDashboardService.GetDocumentId(doc), scopeType = region == null ? "whole" : "region",
                regionId = region == null ? string.Empty : region.regionId, liveMode = true
            };
            return QuantityDashboardService.BuildContext(next);
        }

        public static QuantityDashboardContext RenameRegion(string payload)
        {
            QuantityDashboardRegionRequest request = Deserialize<QuantityDashboardRegionRequest>(payload) ?? new QuantityDashboardRegionRequest();
            Document doc = QuantityDashboardService.ResolveDocument(request.documentId);
            if (doc == null) throw new InvalidOperationException("未找到目标图纸。");
            QuantityDashboardRegionService.RenameRegion(doc, request.regionId, request.regionName);
            return QuantityDashboardService.BuildContext(new QuantityDashboardRequest { documentId = request.documentId, scopeType = "region", regionId = request.regionId, liveMode = true });
        }

        public static QuantityDashboardContext DeleteRegion(string payload)
        {
            QuantityDashboardRegionRequest request = Deserialize<QuantityDashboardRegionRequest>(payload) ?? new QuantityDashboardRegionRequest();
            Document doc = QuantityDashboardService.ResolveDocument(request.documentId);
            if (doc == null) throw new InvalidOperationException("未找到目标图纸。");
            QuantityDashboardRegionService.DeleteRegion(doc, request.regionId);
            return QuantityDashboardService.BuildContext(new QuantityDashboardRequest { documentId = request.documentId, scopeType = "whole", liveMode = true });
        }

        public static void LocateRegion(string payload)
        {
            QuantityDashboardRegionRequest request = Deserialize<QuantityDashboardRegionRequest>(payload) ?? new QuantityDashboardRegionRequest();
            Document doc = QuantityDashboardService.ResolveDocument(request.documentId);
            if (doc == null) throw new InvalidOperationException("未找到目标图纸。");
            ObjectId id = QuantityDashboardRegionService.FindRegionObjectId(doc, request.regionId);
            if (id.IsNull) throw new InvalidOperationException("未找到统计区域。");
            SelectAndZoom(doc, new[] { id }, true);
        }

        public static void ModifyRegionBoundary(string payload)
        {
            QuantityDashboardRegionRequest request = Deserialize<QuantityDashboardRegionRequest>(payload) ?? new QuantityDashboardRegionRequest();
            Document doc = QuantityDashboardService.ResolveDocument(request.documentId);
            if (doc == null) throw new InvalidOperationException("未找到目标图纸。");
            ObjectId id = QuantityDashboardRegionService.FindRegionObjectId(doc, request.regionId);
            if (id.IsNull) throw new InvalidOperationException("未找到统计区域。");
            doc.Editor.SetImpliedSelection(new[] { id });
            doc.SendStringToExecute("_.PEDIT ", true, false, false);
        }

        public static int SelectObjects(string payload, bool zoom)
        {
            QuantityDashboardObjectActionRequest request = Deserialize<QuantityDashboardObjectActionRequest>(payload) ?? new QuantityDashboardObjectActionRequest();
            Document doc = QuantityDashboardService.ResolveDocument(request.documentId);
            if (doc == null) throw new InvalidOperationException("未找到目标图纸。");
            List<ObjectId> ids = ResolveHandles(doc, request.handles);
            if (ids.Count == 0) throw new InvalidOperationException("对象已删除或不属于当前图纸。");
            SelectAndZoom(doc, ids, zoom);
            return ids.Count;
        }

        public static int OpenObjectEditor(string payload)
        {
            QuantityDashboardObjectActionRequest request = Deserialize<QuantityDashboardObjectActionRequest>(payload) ?? new QuantityDashboardObjectActionRequest();
            Document doc = QuantityDashboardService.ResolveDocument(request.documentId);
            if (doc == null) throw new InvalidOperationException("未找到目标图纸。");
            List<ObjectId> ids = ResolveHandles(doc, request.handles);
            if (ids.Count == 0) throw new InvalidOperationException("对象已删除或不属于当前图纸。");
            doc.Editor.SetImpliedSelection(ids.ToArray());
            doc.SendStringToExecute("SX ", true, false, false);
            return ids.Count;
        }

        public static string CopySummary(string payload)
        {
            QuantityDashboardSnapshot snapshot = ResolveSnapshot(payload);
            string text = QuantityDashboardExportService.BuildClipboardText(snapshot);
            Clipboard.SetText(text);
            return text;
        }

        public static string ExportReference(string payload)
        {
            QuantityDashboardSnapshot snapshot = ResolveSnapshot(payload);
            string drawing = snapshot.document == null ? "图纸" : Path.GetFileNameWithoutExtension(snapshot.document.name);
            string defaultName = SanitizeFileName(drawing + "_当前工程量参考_" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx");
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "导出当前工程量参考表";
                dialog.Filter = "Excel 工作簿 (*.xlsx)|*.xlsx";
                dialog.FileName = defaultName;
                dialog.AddExtension = true;
                if (dialog.ShowDialog(new AcadMainWindow()) != DialogResult.OK) return string.Empty;
                QuantityDashboardExportService.ExportReference(dialog.FileName, snapshot);
                return dialog.FileName;
            }
        }

        public static string SaveSnapshot(string payload)
        {
            QuantityDashboardSnapshot snapshot = ResolveSnapshot(payload);
            return QuantityDashboardCache.SaveNamedSnapshot(snapshot);
        }

        public static void RunFormalReport(string payload)
        {
            QuantityDashboardRequest request = Deserialize<QuantityDashboardRequest>(payload) ?? new QuantityDashboardRequest();
            Document doc = QuantityDashboardService.ResolveDocument(request.documentId);
            if (doc == null) throw new InvalidOperationException("未找到目标图纸。");
            doc.SendStringToExecute("GCL ", true, false, false);
        }

        private static QuantityDashboardSnapshot ResolveSnapshot(string payload)
        {
            QuantityDashboardRequest request = Deserialize<QuantityDashboardRequest>(payload) ?? new QuantityDashboardRequest();
            if (string.IsNullOrWhiteSpace(request.documentId)) request.documentId = QuantityDashboardService.GetDocumentId(Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument);
            QuantityDashboardSnapshot snapshot = QuantityDashboardCache.Load(QuantityDashboardService.GetCacheKey(request));
            if (snapshot == null) snapshot = QuantityDashboardService.BuildSnapshot(request, null);
            return snapshot;
        }

        private static List<ObjectId> ResolveHandles(Document doc, IEnumerable<string> handles)
        {
            var result = new List<ObjectId>();
            if (doc == null || handles == null) return result;
            Database db = doc.Database;
            foreach (string text in handles)
            {
                if (string.IsNullOrWhiteSpace(text)) continue;
                long raw;
                if (!long.TryParse(text.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out raw)) continue;
                try
                {
                    ObjectId id = db.GetObjectId(false, new Handle(raw), 0);
                    if (!id.IsNull && id.IsValid && !id.IsErased) result.Add(id);
                }
                catch
                {
                }
            }
            return result.Distinct().ToList();
        }

        private static void SelectAndZoom(Document doc, IEnumerable<ObjectId> ids, bool zoom)
        {
            ObjectId[] array = (ids ?? Enumerable.Empty<ObjectId>()).Where(x => !x.IsNull).Distinct().ToArray();
            doc.Editor.SetImpliedSelection(array);
            if (!zoom || array.Length == 0) return;
            Extents3d? total = null;
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in array)
                {
                    try
                    {
                        Entity entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                        if (entity == null) continue;
                        Extents3d ext = entity.GeometricExtents;
                        if (total == null) total = ext;
                        else
                        {
                            Extents3d merged = total.Value;
                            merged.AddExtents(ext);
                            total = merged;
                        }
                    }
                    catch
                    {
                    }
                }
                tr.Commit();
            }
            if (total == null) return;
            Extents3d bounds = total.Value;
            double width = Math.Max(1.0, (bounds.MaxPoint.X - bounds.MinPoint.X) * 1.25);
            double height = Math.Max(1.0, (bounds.MaxPoint.Y - bounds.MinPoint.Y) * 1.25);
            using (ViewTableRecord view = doc.Editor.GetCurrentView())
            {
                view.CenterPoint = new Point2d((bounds.MinPoint.X + bounds.MaxPoint.X) / 2.0, (bounds.MinPoint.Y + bounds.MaxPoint.Y) / 2.0);
                view.Width = width;
                view.Height = height;
                doc.Editor.SetCurrentView(view);
            }
        }

        private static string SanitizeFileName(string value)
        {
            string text = value ?? "工程量参考.xlsx";
            foreach (char c in Path.GetInvalidFileNameChars()) text = text.Replace(c, '_');
            return text;
        }
    }
}
