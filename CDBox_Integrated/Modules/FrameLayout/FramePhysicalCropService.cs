using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    /// <summary>
    /// 对图框内的复制内容执行实体级裁切，不修改原图，也不依赖 XCLIP。
    /// </summary>
    internal static class FramePhysicalCropService
    {
        private const int MaximumExplodeDepth = 12;
        private const int MaximumProcessedEntities = 200000;

        private sealed class CropWorkItem
        {
            public ObjectId ObjectId { get; set; }
            public int Depth { get; set; }
        }

        private enum ExtentRelation
        {
            Outside,
            Inside,
            Crossing
        }

        public static void Crop(Transaction tr,
            BlockTableRecord contentBlock, IList<Point3d> boundary)
        {
            if (tr == null || contentBlock == null || boundary == null
                || boundary.Count < 3)
                return;

            List<Point2d> polygon = boundary
                .Select(x => new Point2d(x.X, x.Y))
                .ToList();
            var pending = new Queue<CropWorkItem>();
            foreach (ObjectId id in contentBlock)
            {
                pending.Enqueue(new CropWorkItem
                {
                    ObjectId = id,
                    Depth = 0
                });
            }

            int processed = 0;
            while (pending.Count > 0
                && processed < MaximumProcessedEntities)
            {
                CropWorkItem item = pending.Dequeue();
                processed++;
                if (item.ObjectId.IsNull || item.ObjectId.IsErased)
                    continue;

                Entity entity;
                try
                {
                    entity = tr.GetObject(item.ObjectId, OpenMode.ForWrite,
                        false) as Entity;
                }
                catch
                {
                    continue;
                }
                if (entity == null || entity.IsErased)
                    continue;

                Extents3d extents;
                bool hasExtents = GeometryHelper.TryGetEntityExtents(entity,
                    out extents);
                ExtentRelation relation = hasExtents
                    ? GetExtentRelation(extents, polygon)
                    : ExtentRelation.Crossing;
                if (relation == ExtentRelation.Outside)
                {
                    TryErase(entity);
                    continue;
                }
                if (relation == ExtentRelation.Inside)
                    continue;

                Curve curve = entity as Curve;
                if (curve != null)
                {
                    CropCurve(tr, contentBlock, curve, boundary, polygon);
                    continue;
                }

                if (IsPointLikeEntity(entity))
                {
                    Point3d anchor;
                    if (!TryGetEntityAnchor(entity, out anchor)
                        || !IsInsideOrOnBoundary(
                            new Point2d(anchor.X, anchor.Y), polygon))
                        TryErase(entity);
                    continue;
                }

                if (item.Depth < MaximumExplodeDepth
                    && TryExplodeForCrop(tr, contentBlock, entity,
                        item.Depth + 1, pending))
                    continue;

                // 少数不可分解的代理对象、光栅或填充只能按可见代表点判断。
                // 优先保留落点在区域内的对象，避免再次出现整类对象消失。
                Point3d representative;
                bool hasRepresentative =
                    TryGetEntityAnchor(entity, out representative);
                if (!hasRepresentative && hasExtents)
                {
                    representative = GetExtentsCenter(extents);
                    hasRepresentative = true;
                }
                // 无几何范围且无法分解的代理对象只有在 AutoCAD 的框交选择
                // 命中后才会进入内容块；此时保留比误删更可靠。
                if (!hasRepresentative)
                    continue;
                if (!IsInsideOrOnBoundary(
                        new Point2d(representative.X, representative.Y),
                        polygon))
                    TryErase(entity);
            }
        }

        private static void CropCurve(Transaction tr,
            BlockTableRecord contentBlock, Curve curve,
            IList<Point3d> boundary, IList<Point2d> polygon)
        {
            if (curve == null || curve.IsErased)
                return;

            var intersections = new Point3dCollection();
            using (Polyline clipBoundary = BuildClipBoundary(boundary,
                GetCurveElevation(curve)))
            {
                try
                {
                    curve.IntersectWith(clipBoundary,
                        Intersect.OnBothOperands, intersections,
                        IntPtr.Zero, IntPtr.Zero);
                }
                catch
                {
                    intersections.Clear();
                }
            }

            Point3dCollection splitPoints =
                NormalizeCurveSplitPoints(curve, intersections);
            if (splitPoints.Count == 0)
            {
                Point3d sample;
                if (!TryGetCurveSamplePoint(curve, out sample)
                    || !IsInsideOrOnBoundary(
                        new Point2d(sample.X, sample.Y), polygon))
                    TryErase(curve);
                return;
            }

            DBObjectCollection pieces;
            try
            {
                pieces = curve.GetSplitCurves(splitPoints);
            }
            catch
            {
                Point3d sample;
                if (!TryGetCurveSamplePoint(curve, out sample)
                    || !IsInsideOrOnBoundary(
                        new Point2d(sample.X, sample.Y), polygon))
                    TryErase(curve);
                return;
            }

            foreach (DBObject value in pieces)
            {
                Entity piece = value as Entity;
                if (piece == null)
                {
                    value.Dispose();
                    continue;
                }

                Curve pieceCurve = piece as Curve;
                Point3d sample;
                if (pieceCurve != null
                    && TryGetCurveSamplePoint(pieceCurve, out sample)
                    && IsInsideOrOnBoundary(
                        new Point2d(sample.X, sample.Y), polygon))
                {
                    contentBlock.AppendEntity(piece);
                    tr.AddNewlyCreatedDBObject(piece, true);
                }
                else
                {
                    piece.Dispose();
                }
            }

            // 即使没有保留段也要删除原曲线，防止完整框外部分重新显示。
            TryErase(curve);
        }

        private static Polyline BuildClipBoundary(
            IList<Point3d> boundary, double elevation)
        {
            var polyline = new Polyline(boundary.Count);
            for (int i = 0; i < boundary.Count; i++)
            {
                Point3d point = boundary[i];
                polyline.AddVertexAt(i, new Point2d(point.X, point.Y),
                    0.0, 0.0, 0.0);
            }
            polyline.Closed = true;
            polyline.Elevation = elevation;
            return polyline;
        }

        private static double GetCurveElevation(Curve curve)
        {
            try
            {
                return curve.StartPoint.Z;
            }
            catch
            {
                Extents3d extents;
                return GeometryHelper.TryGetEntityExtents(curve, out extents)
                    ? (extents.MinPoint.Z + extents.MaxPoint.Z) * 0.5
                    : 0.0;
            }
        }

        private static Point3dCollection NormalizeCurveSplitPoints(
            Curve curve, Point3dCollection intersections)
        {
            var values = new List<KeyValuePair<double, Point3d>>();
            if (curve == null || intersections == null)
                return new Point3dCollection();

            double startParam;
            double endParam;
            try
            {
                startParam = curve.StartParam;
                endParam = curve.EndParam;
            }
            catch
            {
                return new Point3dCollection();
            }

            foreach (Point3d point in intersections)
            {
                try
                {
                    Point3d closest = curve.GetClosestPointTo(point, false);
                    double parameter = curve.GetParameterAtPoint(closest);
                    double parameterTolerance = Math.Max(1e-9,
                        Math.Abs(endParam - startParam) * 1e-9);
                    if (!curve.Closed
                        && (Math.Abs(parameter - startParam)
                                <= parameterTolerance
                            || Math.Abs(parameter - endParam)
                                <= parameterTolerance))
                        continue;
                    if (values.Any(x => Math.Abs(x.Key - parameter)
                        <= parameterTolerance))
                        continue;
                    values.Add(new KeyValuePair<double, Point3d>(
                        parameter, closest));
                }
                catch
                {
                }
            }

            values.Sort((left, right) =>
                left.Key.CompareTo(right.Key));
            var result = new Point3dCollection();
            foreach (KeyValuePair<double, Point3d> value in values)
                result.Add(value.Value);
            return result;
        }

        private static bool TryGetCurveSamplePoint(Curve curve,
            out Point3d point)
        {
            point = Point3d.Origin;
            if (curve == null) return false;
            try
            {
                double start = curve.StartParam;
                double end = curve.EndParam;
                point = curve.GetPointAtParameter((start + end) * 0.5);
                return true;
            }
            catch
            {
                Extents3d extents;
                if (!GeometryHelper.TryGetEntityExtents(curve, out extents))
                    return false;
                point = GetExtentsCenter(extents);
                return true;
            }
        }

        private static bool TryExplodeForCrop(Transaction tr,
            BlockTableRecord contentBlock, Entity entity, int nextDepth,
            Queue<CropWorkItem> pending)
        {
            if (entity == null || !ShouldAttemptExplode(entity))
                return false;

            var exploded = new DBObjectCollection();
            try
            {
                entity.Explode(exploded);
            }
            catch
            {
                DisposeObjects(exploded);
                return false;
            }
            if (exploded.Count == 0)
                return false;

            var appended = new List<ObjectId>();
            foreach (DBObject value in exploded)
            {
                Entity child = value as Entity;
                if (child == null)
                {
                    value.Dispose();
                    continue;
                }
                try
                {
                    ObjectId childId = contentBlock.AppendEntity(child);
                    tr.AddNewlyCreatedDBObject(child, true);
                    appended.Add(childId);
                }
                catch
                {
                    // 单个分解结果无法加入时跳过，不影响其余可裁切结果。
                    child.Dispose();
                }
            }

            if (appended.Count == 0)
                return false;
            TryErase(entity);
            foreach (ObjectId id in appended)
            {
                pending.Enqueue(new CropWorkItem
                {
                    ObjectId = id,
                    Depth = nextDepth
                });
            }
            return true;
        }

        private static bool ShouldAttemptExplode(Entity entity)
        {
            if (entity == null || IsPointLikeEntity(entity))
                return false;
            string typeName = entity.GetType().Name ?? string.Empty;
            if (typeName.IndexOf("Raster",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Image",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Underlay",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Wipeout",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return true;
        }

        private static bool IsPointLikeEntity(Entity entity)
        {
            return entity is DBText
                || entity is MText
                || entity is AttributeReference
                || entity is AttributeDefinition
                || entity is DBPoint;
        }

        private static bool TryGetEntityAnchor(Entity entity,
            out Point3d point)
        {
            point = Point3d.Origin;
            if (entity == null) return false;

            DBText text = entity as DBText;
            if (text != null)
            {
                point = text.Position;
                return true;
            }
            MText mtext = entity as MText;
            if (mtext != null)
            {
                point = mtext.Location;
                return true;
            }
            AttributeReference attribute =
                entity as AttributeReference;
            if (attribute != null)
            {
                point = attribute.Position;
                return true;
            }
            AttributeDefinition definition =
                entity as AttributeDefinition;
            if (definition != null)
            {
                point = definition.Position;
                return true;
            }
            DBPoint dbPoint = entity as DBPoint;
            if (dbPoint != null)
            {
                point = dbPoint.Position;
                return true;
            }
            BlockReference reference = entity as BlockReference;
            if (reference != null)
            {
                point = reference.Position;
                return true;
            }

            Extents3d extents;
            if (!GeometryHelper.TryGetEntityExtents(entity, out extents))
                return false;
            point = GetExtentsCenter(extents);
            return true;
        }

        private static Point3d GetExtentsCenter(Extents3d extents)
        {
            return new Point3d(
                (extents.MinPoint.X + extents.MaxPoint.X) * 0.5,
                (extents.MinPoint.Y + extents.MaxPoint.Y) * 0.5,
                (extents.MinPoint.Z + extents.MaxPoint.Z) * 0.5);
        }

        private static ExtentRelation GetExtentRelation(
            Extents3d extents, IList<Point2d> polygon)
        {
            var corners = new[]
            {
                new Point2d(extents.MinPoint.X, extents.MinPoint.Y),
                new Point2d(extents.MaxPoint.X, extents.MinPoint.Y),
                new Point2d(extents.MaxPoint.X, extents.MaxPoint.Y),
                new Point2d(extents.MinPoint.X, extents.MaxPoint.Y)
            };
            if (corners.All(x => IsInsideOrOnBoundary(x, polygon)))
                return ExtentRelation.Inside;
            if (corners.Any(x => IsInsideOrOnBoundary(x, polygon)))
                return ExtentRelation.Crossing;

            double minX = extents.MinPoint.X;
            double maxX = extents.MaxPoint.X;
            double minY = extents.MinPoint.Y;
            double maxY = extents.MaxPoint.Y;
            if (polygon.Any(x => x.X >= minX && x.X <= maxX
                && x.Y >= minY && x.Y <= maxY))
                return ExtentRelation.Crossing;

            for (int i = 0; i < polygon.Count; i++)
            {
                Point2d a = polygon[i];
                Point2d b = polygon[(i + 1) % polygon.Count];
                if (SegmentsIntersect(a, b, corners[0], corners[1])
                    || SegmentsIntersect(a, b, corners[1], corners[2])
                    || SegmentsIntersect(a, b, corners[2], corners[3])
                    || SegmentsIntersect(a, b, corners[3], corners[0]))
                    return ExtentRelation.Crossing;
            }
            return ExtentRelation.Outside;
        }

        private static bool IsInsideOrOnBoundary(Point2d point,
            IList<Point2d> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            bool inside = false;
            for (int i = 0, j = polygon.Count - 1;
                i < polygon.Count; j = i++)
            {
                Point2d a = polygon[j];
                Point2d b = polygon[i];
                if (DistanceToSegmentSquared(point, a, b) <= 1e-12)
                    return true;
                bool crosses = (b.Y > point.Y) != (a.Y > point.Y);
                if (crosses)
                {
                    double x = (a.X - b.X) * (point.Y - b.Y)
                        / (a.Y - b.Y) + b.X;
                    if (point.X < x)
                        inside = !inside;
                }
            }
            return inside;
        }

        private static double DistanceToSegmentSquared(Point2d point,
            Point2d a, Point2d b)
        {
            Vector2d segment = b - a;
            double lengthSquared = segment.DotProduct(segment);
            if (lengthSquared <= 1e-20)
            {
                double endpointDistance = point.GetDistanceTo(a);
                return endpointDistance * endpointDistance;
            }
            Vector2d fromA = point - a;
            double t = Math.Max(0.0, Math.Min(1.0,
                fromA.DotProduct(segment) / lengthSquared));
            Point2d nearest = a + segment * t;
            double distance = point.GetDistanceTo(nearest);
            return distance * distance;
        }

        private static bool SegmentsIntersect(Point2d a1, Point2d a2,
            Point2d b1, Point2d b2)
        {
            double d1 = Cross(a2 - a1, b1 - a1);
            double d2 = Cross(a2 - a1, b2 - a1);
            double d3 = Cross(b2 - b1, a1 - b1);
            double d4 = Cross(b2 - b1, a2 - b1);
            const double tolerance = 1e-10;
            if (((d1 > tolerance && d2 < -tolerance)
                    || (d1 < -tolerance && d2 > tolerance))
                && ((d3 > tolerance && d4 < -tolerance)
                    || (d3 < -tolerance && d4 > tolerance)))
                return true;
            return Math.Abs(d1) <= tolerance
                    && DistanceToSegmentSquared(b1, a1, a2) <= tolerance
                || Math.Abs(d2) <= tolerance
                    && DistanceToSegmentSquared(b2, a1, a2) <= tolerance
                || Math.Abs(d3) <= tolerance
                    && DistanceToSegmentSquared(a1, b1, b2) <= tolerance
                || Math.Abs(d4) <= tolerance
                    && DistanceToSegmentSquared(a2, b1, b2) <= tolerance;
        }

        private static double Cross(Vector2d left, Vector2d right)
        {
            return left.X * right.Y - left.Y * right.X;
        }

        private static void TryErase(Entity entity)
        {
            if (entity == null || entity.IsErased) return;
            try
            {
                if (!entity.IsWriteEnabled)
                    entity.UpgradeOpen();
                entity.Erase();
            }
            catch
            {
            }
        }

        private static void DisposeObjects(DBObjectCollection values)
        {
            if (values == null) return;
            foreach (DBObject value in values)
            {
                try
                {
                    value.Dispose();
                }
                catch
                {
                }
            }
        }
    }
}
