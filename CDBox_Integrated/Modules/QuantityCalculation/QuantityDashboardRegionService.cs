using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    public static class QuantityDashboardRegionService
    {
        public const string RegionLayerName = "CDBox-???????";
        public const string RegionRegAppName = "CDBoxQuantityRegion";

        public static List<QuantityDashboardRegionInfo> GetRegions(Document doc)
        {
            List<QuantityDashboardRegionInfo> result = new List<QuantityDashboardRegionInfo>();
            if (doc == null) return result;
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord space = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead, false) as BlockTableRecord;
                if (space != null)
                {
                    foreach (ObjectId id in space)
                    {
                        Polyline pl = tr.GetObject(id, OpenMode.ForRead, false) as Polyline;
                        if (pl == null) continue;
                        QuantityDashboardRegionInfo info = ReadRegionInfo(pl);
                        if (info == null) continue;
                        info.boundaryValid = pl.Closed && pl.NumberOfVertices >= 3;
                        result.Add(info);
                    }
                }
                tr.Commit();
            }
            result.Sort(delegate(QuantityDashboardRegionInfo a, QuantityDashboardRegionInfo b)
            {
                return string.Compare(a.regionName, b.regionName, StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }

        public static QuantityDashboardRegionInfo CreateRectangleRegion(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            Editor ed = doc.Editor;
            PromptPointResult p1 = ed.GetHudPoint("\n???????????????");
            if (p1.Status != PromptStatus.OK) return null;
            PromptCornerOptions corner = new PromptCornerOptions("\n?????????????", p1.Value);
            PromptPointResult p2 = ed.GetHudCorner(corner);
            if (p2.Status != PromptStatus.OK) return null;
            string defaultName = "???? " + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
            string name = PromptRegionName(ed, defaultName);
            if (name == null) return null;

            string regionId = Guid.NewGuid().ToString("N");
            string createdAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            Database db = doc.Database;
            ObjectId createdId = ObjectId.Null;
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                EnsureRegionLayer(db, tr);
                EnsureRegApp(db, tr);
                Point3d a = p1.Value;
                Point3d b = p2.Value;
                Polyline pl = new Polyline(4);
                pl.SetDatabaseDefaults(db);
                pl.Layer = RegionLayerName;
                pl.AddVertexAt(0, new Point2d(a.X, a.Y), 0.0, 0.0, 0.0);
                pl.AddVertexAt(1, new Point2d(b.X, a.Y), 0.0, 0.0, 0.0);
                pl.AddVertexAt(2, new Point2d(b.X, b.Y), 0.0, 0.0, 0.0);
                pl.AddVertexAt(3, new Point2d(a.X, b.Y), 0.0, 0.0, 0.0);
                pl.Closed = true;
                WriteRegionInfo(pl, regionId, name, createdAt);
                BlockTableRecord space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                createdId = space.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);
                tr.Commit();
            }
            return ReadRegion(doc, regionId, createdId);
        }

        public static QuantityDashboardRegionInfo BindExistingClosedPolyline(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            Editor ed = doc.Editor;
            PromptEntityOptions options = new PromptEntityOptions("\n???????????????????");
            options.SetRejectMessage("\n????????????");
            options.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult selected = ed.GetHudEntity(options);
            if (selected.Status != PromptStatus.OK) return null;

            string defaultName = "???? " + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
            string name = PromptRegionName(ed, defaultName);
            if (name == null) return null;
            string regionId = Guid.NewGuid().ToString("N");
            string createdAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            Database db = doc.Database;
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Polyline pl = tr.GetObject(selected.ObjectId, OpenMode.ForWrite, false) as Polyline;
                if (pl == null || !pl.Closed || pl.NumberOfVertices < 3) throw new InvalidOperationException("????????????????????");
                EnsureRegionLayer(db, tr);
                EnsureRegApp(db, tr);
                pl.Layer = RegionLayerName;
                WriteRegionInfo(pl, regionId, name, createdAt);
                tr.Commit();
            }
            return ReadRegion(doc, regionId, selected.ObjectId);
        }

        public static QuantityDashboardRegionInfo RenameRegion(Document doc, string regionId, string newName)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            string name = (newName ?? string.Empty).Trim();
            if (name.Length == 0) throw new InvalidOperationException("?????????");
            Database db = doc.Database;
            ObjectId id = FindRegionObjectId(doc, regionId);
            if (id.IsNull) throw new InvalidOperationException("????????");
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Polyline pl = tr.GetObject(id, OpenMode.ForWrite, false) as Polyline;
                QuantityDashboardRegionInfo info = ReadRegionInfo(pl);
                if (info == null) throw new InvalidOperationException("??????????");
                EnsureRegApp(db, tr);
                WriteRegionInfo(pl, info.regionId, name, info.createdAt);
                tr.Commit();
            }
            return ReadRegion(doc, regionId, id);
        }

        public static void DeleteRegion(Document doc, string regionId)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            ObjectId id = FindRegionObjectId(doc, regionId);
            if (id.IsNull) throw new InvalidOperationException("????????");
            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                Entity entity = tr.GetObject(id, OpenMode.ForWrite, false) as Entity;
                if (entity != null) entity.Erase();
                tr.Commit();
            }
        }

        public static ObjectId FindRegionObjectId(Document doc, string regionId)
        {
            if (doc == null || string.IsNullOrWhiteSpace(regionId)) return ObjectId.Null;
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord space = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead, false) as BlockTableRecord;
                if (space != null)
                {
                    foreach (ObjectId id in space)
                    {
                        Polyline pl = tr.GetObject(id, OpenMode.ForRead, false) as Polyline;
                        QuantityDashboardRegionInfo info = ReadRegionInfo(pl);
                        if (info != null && string.Equals(info.regionId, regionId, StringComparison.OrdinalIgnoreCase))
                        {
                            tr.Commit();
                            return id;
                        }
                    }
                }
                tr.Commit();
            }
            return ObjectId.Null;
        }

        public static bool IsEntityIncluded(Entity entity, Polyline region)
        {
            if (entity == null) return false;
            if (region == null) return true;
            Curve curve = entity as Curve;
            if (curve != null && !(entity is Circle)) return IsCurveIncluded(curve, region);
            Point3d center;
            if (!TryGetEntityCenter(entity, out center)) return false;
            return IsPointInside(region, center);
        }

        public static bool IsCurveIncluded(Curve curve, Polyline region)
        {
            if (curve == null || region == null) return false;
            try
            {
                Point3dCollection points = new Point3dCollection();
                curve.IntersectWith(region, Intersect.OnBothOperands, points, IntPtr.Zero, IntPtr.Zero);
                if (points.Count > 0) return true;
            }
            catch
            {
            }
            try
            {
                if (IsPointInside(region, curve.StartPoint) || IsPointInside(region, curve.EndPoint)) return true;
                double start = curve.StartParam;
                double end = curve.EndParam;
                double middle = start + (end - start) * 0.5;
                return IsPointInside(region, curve.GetPointAtParameter(middle));
            }
            catch
            {
                return false;
            }
        }

        public static bool IsPointInside(Polyline polygon, Point3d point)
        {
            if (polygon == null || polygon.NumberOfVertices < 3) return false;
            bool inside = false;
            double x = point.X;
            double y = point.Y;
            int n = polygon.NumberOfVertices;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Point2d pi = polygon.GetPoint2dAt(i);
                Point2d pj = polygon.GetPoint2dAt(j);
                bool intersect = ((pi.Y > y) != (pj.Y > y)) &&
                    (x < (pj.X - pi.X) * (y - pi.Y) / ((pj.Y - pi.Y) == 0 ? 1e-12 : (pj.Y - pi.Y)) + pi.X);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        public static bool TryGetEntityCenter(Entity entity, out Point3d center)
        {
            center = Point3d.Origin;
            if (entity == null) return false;
            BlockReference block = entity as BlockReference;
            if (block != null) { center = block.Position; return true; }
            DBPoint dbPoint = entity as DBPoint;
            if (dbPoint != null) { center = dbPoint.Position; return true; }
            Circle circle = entity as Circle;
            if (circle != null) { center = circle.Center; return true; }
            try
            {
                Extents3d ext = entity.GeometricExtents;
                center = new Point3d((ext.MinPoint.X + ext.MaxPoint.X) / 2.0, (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0, (ext.MinPoint.Z + ext.MaxPoint.Z) / 2.0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static QuantityDashboardRegionInfo ReadRegion(Document doc, string regionId, ObjectId preferredId)
        {
            if (doc == null) return null;
            ObjectId id = preferredId;
            if (id.IsNull) id = FindRegionObjectId(doc, regionId);
            if (id.IsNull) return null;
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                Polyline pl = tr.GetObject(id, OpenMode.ForRead, false) as Polyline;
                QuantityDashboardRegionInfo info = ReadRegionInfo(pl);
                if (info != null) info.boundaryValid = pl != null && pl.Closed && pl.NumberOfVertices >= 3;
                tr.Commit();
                return info;
            }
        }

        private static QuantityDashboardRegionInfo ReadRegionInfo(Polyline pl)
        {
            if (pl == null) return null;
            ResultBuffer buffer = pl.GetXDataForApplication(RegionRegAppName);
            if (buffer == null) return null;
            TypedValue[] values = buffer.AsArray();
            if (values == null || values.Length < 4) return null;
            return new QuantityDashboardRegionInfo
            {
                regionId = Convert.ToString(values[1].Value, CultureInfo.InvariantCulture) ?? string.Empty,
                regionName = Convert.ToString(values[2].Value, CultureInfo.InvariantCulture) ?? string.Empty,
                createdAt = Convert.ToString(values[3].Value, CultureInfo.InvariantCulture) ?? string.Empty,
                handle = pl.Handle.ToString(),
                boundaryValid = pl.Closed && pl.NumberOfVertices >= 3
            };
        }

        private static void WriteRegionInfo(Polyline pl, string regionId, string regionName, string createdAt)
        {
            if (pl == null) return;
            pl.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegionRegAppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, regionId ?? string.Empty),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, regionName ?? string.Empty),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, createdAt ?? string.Empty));
        }

        private static void EnsureRegApp(Database db, Transaction tr)
        {
            RegAppTable table = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (table.Has(RegionRegAppName)) return;
            table.UpgradeOpen();
            RegAppTableRecord record = new RegAppTableRecord { Name = RegionRegAppName };
            table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        private static void EnsureRegionLayer(Database db, Transaction tr)
        {
            LayerTable table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (table.Has(RegionLayerName)) return;
            table.UpgradeOpen();
            LayerTableRecord layer = new LayerTableRecord
            {
                Name = RegionLayerName,
                IsPlottable = false,
                Color = Color.FromColorIndex(ColorMethod.ByAci, 8)
            };
            table.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
        }

        private static string PromptRegionName(Editor ed, string defaultName)
        {
            PromptStringOptions options = new PromptStringOptions("\n???????? <" + defaultName + ">?");
            options.AllowSpaces = true;
            PromptResult result = ed.GetHudString(options);
            if (result.Status != PromptStatus.OK) return null;
            string name = (result.StringResult ?? defaultName).Trim();
            return name.Length == 0 ? defaultName : name;
        }
    }
}
