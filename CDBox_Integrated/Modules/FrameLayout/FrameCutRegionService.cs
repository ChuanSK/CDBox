using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Globalization;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    public sealed class FrameCutRegionInfo
    {
        public ObjectId ObjectId { get; set; }
        public string TemplateId { get; set; }
        public string PaperSize { get; set; }
        public double Rotation { get; set; }
        public List<Point3d> Boundary { get; set; }

        public Point3d Center
        {
            get
            {
                if (Boundary == null || Boundary.Count == 0) return Point3d.Origin;
                double x = 0;
                double y = 0;
                foreach (Point3d point in Boundary)
                {
                    x += point.X;
                    y += point.Y;
                }
                return new Point3d(x / Boundary.Count, y / Boundary.Count, 0);
            }
        }
    }

    internal sealed class FrameCutRegionService
    {
        public const string RegionLayerName = "CDBOX_裁图区域";
        public const string MetadataKey = "CDBOX_FRAME_CUT_REGION";

        public ObjectId CreateRectangle(Database db, Transaction tr,
            Point3d lowerLeft, double width, double height, double rotation,
            string templateId, string paperSize)
        {
            if (width <= GeometryHelper.Eps || height <= GeometryHelper.Eps)
                throw new InvalidOperationException("裁图区域尺寸无效。");

            Vector3d axisU = new Vector3d(Math.Cos(rotation),
                Math.Sin(rotation), 0).GetNormal();
            Vector3d axisV = GeometryHelper.GetPerpLeft(axisU);
            Point3d center = lowerLeft + axisU * (width * 0.5)
                + axisV * (height * 0.5);
            ObjectId id = CreateRectanglePreview(db, tr, center, width,
                height, rotation);
            FinalizeRectangle(tr, id, templateId, paperSize, rotation);
            return id;
        }

        public ObjectId CreateRectanglePreview(Database db, Transaction tr,
            Point3d center, double width, double height,
            double rectangleRotation)
        {
            if (width <= GeometryHelper.Eps || height <= GeometryHelper.Eps)
                throw new InvalidOperationException("裁图区域尺寸无效。");

            Vector3d axisU = new Vector3d(Math.Cos(rectangleRotation),
                Math.Sin(rectangleRotation), 0).GetNormal();
            Vector3d axisV = GeometryHelper.GetPerpLeft(axisU);
            Polyline rectangle = GeometryHelper.CreateRectanglePolyline(center,
                axisU, axisV, width, height);
            CadDbHelper.EnsureLayer(db, tr, RegionLayerName);
            rectangle.Layer = RegionLayerName;
            rectangle.ColorIndex = 1;
            return CadDbHelper.AppendToModelSpace(db, tr, rectangle);
        }

        public void FinalizeRectangle(Transaction tr, ObjectId rectangleId,
            string templateId, string paperSize, double cutRotation)
        {
            if (tr == null || rectangleId.IsNull)
                throw new InvalidOperationException("裁图矩形无效。");
            Entity rectangle = tr.GetObject(rectangleId, OpenMode.ForWrite,
                false) as Entity;
            if (rectangle == null)
                throw new InvalidOperationException("找不到裁图矩形。");
            AttachMetadata(tr, rectangle, templateId, paperSize, cutRotation);
        }

        public void AttachMetadata(Transaction tr, Entity entity,
            string templateId, string paperSize, double rotation)
        {
            if (tr == null || entity == null) return;
            if (!entity.IsWriteEnabled) entity.UpgradeOpen();
            if (entity.ExtensionDictionary.IsNull)
                entity.CreateExtensionDictionary();
            DBDictionary dictionary = (DBDictionary)tr.GetObject(
                entity.ExtensionDictionary, OpenMode.ForWrite);
            Xrecord record;
            if (dictionary.Contains(MetadataKey))
            {
                record = (Xrecord)tr.GetObject(dictionary.GetAt(MetadataKey),
                    OpenMode.ForWrite);
            }
            else
            {
                record = new Xrecord();
                dictionary.SetAt(MetadataKey, record);
                tr.AddNewlyCreatedDBObject(record, true);
            }
            record.Data = new ResultBuffer(
                new TypedValue((int)DxfCode.Text, "2"),
                new TypedValue((int)DxfCode.Text, templateId ?? string.Empty),
                new TypedValue((int)DxfCode.Text, paperSize ?? string.Empty),
                new TypedValue((int)DxfCode.Real, rotation),
                new TypedValue((int)DxfCode.Text,
                    DateTime.Now.ToString("o", CultureInfo.InvariantCulture)));
        }

        public bool TryRead(Transaction tr, Entity entity,
            out FrameCutRegionInfo info)
        {
            info = null;
            if (tr == null || entity == null || entity.ExtensionDictionary.IsNull)
                return false;
            try
            {
                DBDictionary dictionary = (DBDictionary)tr.GetObject(
                    entity.ExtensionDictionary, OpenMode.ForRead);
                if (!dictionary.Contains(MetadataKey)) return false;
                Xrecord record = (Xrecord)tr.GetObject(
                    dictionary.GetAt(MetadataKey), OpenMode.ForRead);
                TypedValue[] values = record.Data == null
                    ? null : record.Data.AsArray();
                if (values == null || values.Length < 4) return false;
                List<Point3d> boundary;
                if (!TryGetBoundary(entity, out boundary)) return false;
                info = new FrameCutRegionInfo
                {
                    ObjectId = entity.ObjectId,
                    TemplateId = Convert.ToString(values[1].Value),
                    PaperSize = Convert.ToString(values[2].Value),
                    Rotation = Convert.ToDouble(values[3].Value,
                        CultureInfo.InvariantCulture),
                    Boundary = boundary
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool HasMetadata(Transaction tr, Entity entity)
        {
            if (tr == null || entity == null || entity.ExtensionDictionary.IsNull)
                return false;
            try
            {
                DBDictionary dictionary = (DBDictionary)tr.GetObject(
                    entity.ExtensionDictionary, OpenMode.ForRead);
                return dictionary.Contains(MetadataKey);
            }
            catch
            {
                return false;
            }
        }

        public bool ValidateBoundary(Entity entity, double rotation,
            double maxWidth, double maxHeight, out string message)
        {
            List<Point3d> points;
            if (!TryGetBoundary(entity, out points))
            {
                message = "所选对象不是可用的闭合曲线。";
                return false;
            }
            Vector3d axisU = new Vector3d(Math.Cos(rotation),
                Math.Sin(rotation), 0).GetNormal();
            Vector3d axisV = GeometryHelper.GetPerpLeft(axisU);
            double minU;
            double maxU;
            double minV;
            double maxV;
            GetProjectedExtents(points, axisU, axisV,
                out minU, out maxU, out minV, out maxV);
            double width = maxU - minU;
            double height = maxV - minV;
            if (width > maxWidth + 1e-6 || height > maxHeight + 1e-6)
            {
                message = string.Format(CultureInfo.CurrentCulture,
                    "闭合曲线尺寸 {0:0.###} × {1:0.###} 超过该模板裁图区域 {2:0.###} × {3:0.###}。",
                    width, height, maxWidth, maxHeight);
                return false;
            }
            message = string.Format(CultureInfo.CurrentCulture,
                "裁图区域 {0:0.###} × {1:0.###}", width, height);
            return true;
        }

        public IList<FrameCutRegionInfo> ReadSelectedRegions(Editor editor,
            Database db)
        {
            List<FrameCutRegionInfo> result = new List<FrameCutRegionInfo>();
            PromptSelectionOptions options = new PromptSelectionOptions
            {
                MessageForAdding = "\n选择已布置的裁图区域："
            };
            PromptSelectionResult selection = editor.GetHudSelection(options);
            if (selection.Status != PromptStatus.OK) return result;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selected in selection.Value)
                {
                    if (selected == null || selected.ObjectId.IsNull) continue;
                    Entity entity = tr.GetObject(selected.ObjectId,
                        OpenMode.ForRead, false) as Entity;
                    FrameCutRegionInfo info;
                    if (entity != null && TryRead(tr, entity, out info))
                        result.Add(info);
                }
                tr.Commit();
            }
            return result;
        }

        public static bool TryGetBoundary(Entity entity,
            out List<Point3d> points)
        {
            points = new List<Point3d>();
            Polyline polyline = entity as Polyline;
            if (polyline != null)
            {
                if (!polyline.Closed || polyline.NumberOfVertices < 3) return false;
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    Point2d point = polyline.GetPoint2dAt(i);
                    points.Add(new Point3d(point.X, point.Y, polyline.Elevation));
                }
                return true;
            }

            Curve curve = entity as Curve;
            if (curve == null || !curve.Closed) return false;
            try
            {
                double start = curve.StartParam;
                double end = curve.EndParam;
                const int samples = 64;
                for (int i = 0; i < samples; i++)
                {
                    double parameter = start + (end - start) * i / samples;
                    points.Add(curve.GetPointAtParameter(parameter));
                }
                return points.Count >= 3;
            }
            catch
            {
                Extents3d extents;
                if (!GeometryHelper.TryGetEntityExtents(entity, out extents))
                    return false;
                points.AddRange(GeometryHelper.CreateAxisAlignedRectangleCorners(
                    extents.MinPoint, extents.MaxPoint));
                return true;
            }
        }

        public static void GetProjectedExtents(IList<Point3d> points,
            Vector3d axisU, Vector3d axisV, out double minU, out double maxU,
            out double minV, out double maxV)
        {
            minU = double.MaxValue;
            maxU = double.MinValue;
            minV = double.MaxValue;
            maxV = double.MinValue;
            foreach (Point3d point in points)
            {
                double u = GeometryHelper.Dot2d(point, axisU);
                double v = GeometryHelper.Dot2d(point, axisV);
                minU = Math.Min(minU, u);
                maxU = Math.Max(maxU, u);
                minV = Math.Min(minV, v);
                maxV = Math.Max(maxV, v);
            }
        }
    }
}
