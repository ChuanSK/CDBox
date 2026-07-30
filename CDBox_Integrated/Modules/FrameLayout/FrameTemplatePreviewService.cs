using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.IO;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    internal static class FrameTemplatePreviewService
    {
        private const int MaximumSegments = 3000;
        private const int MaximumDepth = 8;

        public static bool TryBuild(Transaction transaction,
            ObjectId blockTableRecordId,
            out List<FrameTemplatePreviewSegment> segments,
            out int entityCount)
        {
            segments = new List<FrameTemplatePreviewSegment>();
            entityCount = 0;
            if (transaction == null || blockTableRecordId.IsNull) return false;
            try
            {
                var path = new HashSet<ObjectId>();
                ReadBlock(transaction, blockTableRecordId,
                    new List<Matrix3d>(), path, 0, segments, ref entityCount);
                return segments.Count > 0;
            }
            catch
            {
                segments.Clear();
                entityCount = 0;
                return false;
            }
        }

        public static bool TryBuildFromSource(FrameTemplateCatalogItem template,
            out List<FrameTemplatePreviewSegment> segments,
            out int entityCount)
        {
            segments = new List<FrameTemplatePreviewSegment>();
            entityCount = 0;
            if (template == null) return false;
            string path = FrameTemplateCatalogStore.ResolveTemplatePath(
                template.SourceDwgPath);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            try
            {
                using (Database source = new Database(false, true))
                {
                    source.ReadDwgFile(path,
                        FileOpenMode.OpenForReadAndAllShare, true, string.Empty);
                    source.CloseInput(true);
                    using (Transaction transaction =
                        source.TransactionManager.StartTransaction())
                    {
                        BlockTable table = (BlockTable)transaction.GetObject(
                            source.BlockTableId, OpenMode.ForRead);
                        bool success = TryBuild(transaction,
                            table[BlockTableRecord.ModelSpace], out segments,
                            out entityCount);
                        transaction.Commit();
                        return success;
                    }
                }
            }
            catch
            {
                segments.Clear();
                entityCount = 0;
                return false;
            }
        }

        private static void ReadBlock(Transaction transaction,
            ObjectId blockTableRecordId, List<Matrix3d> transforms,
            HashSet<ObjectId> path, int depth,
            List<FrameTemplatePreviewSegment> segments, ref int entityCount)
        {
            if (depth > MaximumDepth || segments.Count >= MaximumSegments
                || path.Contains(blockTableRecordId)) return;
            path.Add(blockTableRecordId);
            BlockTableRecord record = transaction.GetObject(blockTableRecordId,
                OpenMode.ForRead, false) as BlockTableRecord;
            if (record == null)
            {
                path.Remove(blockTableRecordId);
                return;
            }

            foreach (ObjectId id in record)
            {
                if (segments.Count >= MaximumSegments) break;
                Entity entity = transaction.GetObject(id,
                    OpenMode.ForRead, false) as Entity;
                if (entity == null || entity.IsErased) continue;

                BlockReference reference = entity as BlockReference;
                if (reference != null)
                {
                    ObjectId nestedId = reference.IsDynamicBlock
                        ? reference.DynamicBlockTableRecord
                        : reference.BlockTableRecord;
                    var nestedTransforms = new List<Matrix3d>
                    {
                        reference.BlockTransform
                    };
                    nestedTransforms.AddRange(transforms);
                    ReadBlock(transaction, nestedId, nestedTransforms, path,
                        depth + 1, segments, ref entityCount);
                    continue;
                }

                entityCount++;
                AppendEntity(entity, transforms, segments);
            }
            path.Remove(blockTableRecordId);
        }

        private static void AppendEntity(Entity entity,
            IList<Matrix3d> transforms,
            List<FrameTemplatePreviewSegment> segments)
        {
            Line line = entity as Line;
            if (line != null)
            {
                Add(line.StartPoint, line.EndPoint, transforms, segments);
                return;
            }

            Polyline polyline = entity as Polyline;
            if (polyline != null && polyline.NumberOfVertices > 1)
            {
                for (int i = 1; i < polyline.NumberOfVertices; i++)
                    Add(polyline.GetPoint3dAt(i - 1),
                        polyline.GetPoint3dAt(i), transforms, segments);
                if (polyline.Closed)
                    Add(polyline.GetPoint3dAt(polyline.NumberOfVertices - 1),
                        polyline.GetPoint3dAt(0), transforms, segments);
                return;
            }

            Curve curve = entity as Curve;
            if (curve != null)
            {
                AppendCurve(curve, transforms, segments);
                return;
            }

            Extents3d extents;
            if (GeometryHelper.TryGetEntityExtents(entity, out extents))
                AppendExtents(extents, transforms, segments);
        }

        private static void AppendCurve(Curve curve,
            IList<Matrix3d> transforms,
            List<FrameTemplatePreviewSegment> segments)
        {
            try
            {
                double start = curve.StartParam;
                double end = curve.EndParam;
                int count = curve is Circle || curve is Ellipse ? 36 : 18;
                Point3d previous = curve.GetPointAtParameter(start);
                for (int i = 1; i <= count; i++)
                {
                    double parameter = start + (end - start) * i / count;
                    Point3d current = curve.GetPointAtParameter(parameter);
                    Add(previous, current, transforms, segments);
                    previous = current;
                }
            }
            catch
            {
                Extents3d extents;
                if (GeometryHelper.TryGetEntityExtents(curve, out extents))
                    AppendExtents(extents, transforms, segments);
            }
        }

        private static void AppendExtents(Extents3d extents,
            IList<Matrix3d> transforms,
            List<FrameTemplatePreviewSegment> segments)
        {
            Point3d a = new Point3d(extents.MinPoint.X, extents.MinPoint.Y, 0);
            Point3d b = new Point3d(extents.MaxPoint.X, extents.MinPoint.Y, 0);
            Point3d c = new Point3d(extents.MaxPoint.X, extents.MaxPoint.Y, 0);
            Point3d d = new Point3d(extents.MinPoint.X, extents.MaxPoint.Y, 0);
            Add(a, b, transforms, segments);
            Add(b, c, transforms, segments);
            Add(c, d, transforms, segments);
            Add(d, a, transforms, segments);
        }

        private static void Add(Point3d first, Point3d second,
            IList<Matrix3d> transforms,
            List<FrameTemplatePreviewSegment> segments)
        {
            if (segments.Count >= MaximumSegments) return;
            first = Transform(first, transforms);
            second = Transform(second, transforms);
            if (first.DistanceTo(second) <= 1e-8) return;
            segments.Add(new FrameTemplatePreviewSegment
            {
                X1 = first.X,
                Y1 = first.Y,
                X2 = second.X,
                Y2 = second.Y
            });
        }

        private static Point3d Transform(Point3d point,
            IList<Matrix3d> transforms)
        {
            for (int i = 0; i < transforms.Count; i++)
                point = point.TransformBy(transforms[i]);
            return point;
        }
    }
}
