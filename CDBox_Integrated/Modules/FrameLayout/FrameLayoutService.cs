using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    internal class FrameLayoutService
    {
        public class LayoutResult
        {
            public LayoutResult()
            {
                Warnings = new List<string>();
            }

            public int Count { get; set; }
            public int Columns { get; set; }
            public int Rows { get; set; }
            public List<string> Warnings { get; private set; }
        }

        public LayoutResult LayoutSelectedRegions(Editor editor, Database db,
            Transaction tr, IList<FrameCutRegionInfo> regions,
            IDictionary<string, FrameTemplateCatalogItem> templates,
            Point3d firstFrameLowerLeft, FrameLayoutSettings settings)
        {
            if (editor == null) throw new ArgumentNullException("editor");
            if (regions == null || regions.Count == 0)
                throw new InvalidOperationException("没有可布置的裁图区域。");
            if (templates == null || templates.Count == 0)
                throw new InvalidOperationException("没有匹配的图框模板。");
            settings = settings ?? new FrameLayoutSettings();
            settings.Normalize();

            List<FrameTemplateCatalogItem> orderedTemplates =
                new List<FrameTemplateCatalogItem>();
            foreach (FrameCutRegionInfo region in regions)
            {
                FrameTemplateCatalogItem template;
                if (!templates.TryGetValue(region.TemplateId ?? string.Empty,
                    out template) || template == null)
                    throw new InvalidOperationException("裁图区域缺少对应图框模板："
                        + (region.PaperSize ?? "未分类"));
                double boundaryRotation = region.BoundaryRotation
                    ?? region.Rotation;
                Vector3d axisU = new Vector3d(Math.Cos(boundaryRotation),
                    Math.Sin(boundaryRotation), 0).GetNormal();
                Vector3d axisV = GeometryHelper.GetPerpLeft(axisU);
                double minU;
                double maxU;
                double minV;
                double maxV;
                FrameCutRegionService.GetProjectedExtents(region.Boundary,
                    axisU, axisV, out minU, out maxU, out minV, out maxV);
                if (!FrameCutRegionMath.Fits(maxU - minU, maxV - minV,
                        template.ValidWidth, template.ValidHeight))
                    throw new InvalidOperationException(string.Format(
                        "裁图区域尺寸 {0:0.###} × {1:0.###} 超过模板“{2}”的可用区域 {3:0.###} × {4:0.###}。",
                        maxU - minU, maxV - minV, template.TemplateName,
                        template.ValidWidth, template.ValidHeight));
                orderedTemplates.Add(template);
            }

            double maxWidth = orderedTemplates.Max(x => x.FrameWidth);
            double maxHeight = orderedTemplates.Max(x => x.FrameHeight);
            int perRow = settings.FramesPerRow;
            FrameTemplateService templateService = new FrameTemplateService();
            FrameAnnotationService annotationService = new FrameAnnotationService();
            Dictionary<string, ObjectId> blockIds =
                new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < orderedTemplates.Count; i++)
            {
                FrameTemplateCatalogItem item = orderedTemplates[i];
                if (!blockIds.ContainsKey(item.Id))
                    blockIds[item.Id] = templateService.EnsureTemplateBlock(db, tr, item);
            }

            int count = 0;
            List<string> resultWarnings = new List<string>();
            for (int i = 0; i < regions.Count; i++)
            {
                int row = i / perRow;
                int column = i % perRow;
                FrameTemplateCatalogItem template = orderedTemplates[i];
                Point3d cellLowerLeft = firstFrameLowerLeft
                    + Vector3d.XAxis * (column * (maxWidth + settings.HorizontalGap))
                    + Vector3d.YAxis * (row * (maxHeight + settings.VerticalGap));
                Point3d frameLowerLeft = cellLowerLeft;
                Point3d insertPoint = InsertFrame(db, tr, blockIds[template.Id],
                    template, frameLowerLeft, 0.0);
                Point3d validCenter = GeometryHelper.LocalToWorld(
                    template.ToTemplateInfo().ValidCenterLocal, insertPoint, 0.0,
                    template.ScaleX, template.ScaleY);
                InsertClippedRegionContent(editor, db, tr, regions[i],
                    validCenter);
                try
                {
                    annotationService.Draw(db, tr, template, insertPoint, 0.0,
                        settings);
                }
                catch (System.Exception ex)
                {
                    // 附加标注失败不应回滚已经生成的图框及裁图内容。
                    resultWarnings.Add("第 " + (i + 1)
                        + " 个图框附加标注失败：" + ex.Message);
                }
                count++;
            }

            LayoutResult layoutResult = new LayoutResult
            {
                Count = count,
                Columns = Math.Min(perRow, count),
                Rows = (int)Math.Ceiling(count / (double)perRow)
            };
            layoutResult.Warnings.AddRange(resultWarnings);
            return layoutResult;
        }

        public LayoutResult PlaceFrames(Database db, Transaction tr,
            FrameTemplateCatalogItem template, Point3d firstFrameLowerLeft,
            int count, FrameLayoutSettings settings)
        {
            if (template == null) throw new ArgumentNullException("template");
            if (count <= 0) throw new InvalidOperationException("图框数量应大于 0。");
            settings = settings ?? new FrameLayoutSettings();
            settings.Normalize();

            ObjectId blockId;
            try
            {
                blockId = new FrameTemplateService()
                    .EnsureTemplateBlock(db, tr, template);
            }
            catch (System.Exception ex)
            {
                throw new InvalidOperationException("准备图框模板块失败："
                    + ex.Message, ex);
            }
            FrameAnnotationService annotationService = new FrameAnnotationService();
            List<string> warnings = new List<string>();
            int perRow = settings.FramesPerRow;
            for (int i = 0; i < count; i++)
            {
                int row = i / perRow;
                int column = i % perRow;
                Point3d lowerLeft = firstFrameLowerLeft
                    + Vector3d.XAxis * (column
                        * (template.FrameWidth + settings.HorizontalGap))
                    + Vector3d.YAxis * (row
                        * (template.FrameHeight + settings.VerticalGap));
                Point3d insertPoint;
                try
                {
                    insertPoint = InsertFrame(db, tr, blockId, template,
                        lowerLeft, 0.0);
                }
                catch (System.Exception ex)
                {
                    throw new InvalidOperationException("插入第 " + (i + 1)
                        + " 个图框块失败：" + ex.Message, ex);
                }
                try
                {
                    annotationService.Draw(db, tr, template, insertPoint, 0.0,
                        settings);
                }
                catch (System.Exception ex)
                {
                    warnings.Add("第 " + (i + 1)
                        + " 个图框附加标注失败：" + ex.Message);
                }
            }
            LayoutResult result = new LayoutResult
            {
                Count = count,
                Columns = Math.Min(perRow, count),
                Rows = (int)Math.Ceiling(count / (double)perRow)
            };
            result.Warnings.AddRange(warnings);
            return result;
        }

        private static Point3d InsertFrame(Database db, Transaction tr,
            ObjectId blockId, FrameTemplateCatalogItem template,
            Point3d outerLowerLeft, double rotation)
        {
            Point3d insertPoint = GeometryHelper.ComputeInsertPointForLocalPoint(
                new Point2d(template.FrameMinX, template.FrameMinY),
                outerLowerLeft, rotation, template.ScaleX, template.ScaleY);
            BlockReference reference = new BlockReference(insertPoint, blockId)
            {
                Rotation = rotation,
                ScaleFactors = new Scale3d(template.ScaleX, template.ScaleY,
                    template.ScaleZ)
            };
            CadDbHelper.AppendToModelSpace(db, tr, reference);
            return insertPoint;
        }

        private static void InsertClippedRegionContent(Editor editor, Database db,
            Transaction tr, FrameCutRegionInfo region, Point3d targetCenter)
        {
            if (region == null || region.Boundary == null
                || region.Boundary.Count < 3) return;

            Point3dCollection polygon = new Point3dCollection();
            foreach (Point3d point in region.Boundary)
                polygon.Add(new Point3d(point.X, point.Y, 0));
            ObjectIdCollection sourceIds = CollectRegionSourceIds(editor, db,
                tr, region, polygon);
            if (sourceIds.Count == 0) return;

            List<ObjectId> relockLayers = UnlockLockedLayers(db, tr);
            try
            {
                InsertClippedRegionContentCore(db, tr, region, targetCenter,
                    sourceIds);
            }
            finally
            {
                RestoreLockedLayers(tr, relockLayers);
            }
        }

        private static void InsertClippedRegionContentCore(Database db,
            Transaction tr, FrameCutRegionInfo region, Point3d targetCenter,
            ObjectIdCollection sourceIds)
        {
            BlockTable table = (BlockTable)tr.GetObject(db.BlockTableId,
                OpenMode.ForWrite);
            BlockTableRecord contentBlock = new BlockTableRecord
            {
                Name = CadDbHelper.MakeUniqueBlockName(db, tr,
                    "CDBOX_CROP_" + Guid.NewGuid().ToString("N").Substring(0, 10)),
                Origin = Point3d.Origin
            };
            ObjectId contentBlockId = table.Add(contentBlock);
            tr.AddNewlyCreatedDBObject(contentBlock, true);
            int clonedCount = 0;
            var protectedNestedReferences = new HashSet<ObjectId>();
            foreach (ObjectId sourceId in sourceIds)
            {
                Entity source = null;
                try
                {
                    source = tr.GetObject(sourceId, OpenMode.ForRead, false)
                        as Entity;
                }
                catch
                {
                }
                if (source == null) continue;

                if (source is BlockReference)
                {
                    ObjectId nestedBlockId = CreateCroppedSourceBlock(db, tr,
                        table, sourceId, region.Boundary);
                    if (!nestedBlockId.IsNull)
                    {
                        var nestedReference = new BlockReference(
                            Point3d.Origin, nestedBlockId);
                        try
                        {
                            ObjectId nestedReferenceId =
                                contentBlock.AppendEntity(nestedReference);
                            tr.AddNewlyCreatedDBObject(nestedReference, true);
                            protectedNestedReferences.Add(nestedReferenceId);
                            clonedCount++;
                        }
                        catch
                        {
                            nestedReference.Dispose();
                        }
                    }
                    continue;
                }

                ObjectId clonedId;
                if (CloneEntityIntoBlock(db, tr, sourceId, contentBlock,
                        contentBlockId, out clonedId))
                    clonedCount++;
            }
            if (clonedCount == 0)
            {
                contentBlock.Erase();
                return;
            }

            Vector3d sourceAxisU = new Vector3d(Math.Cos(region.Rotation),
                Math.Sin(region.Rotation), 0).GetNormal();
            Vector3d sourceAxisV = GeometryHelper.GetPerpLeft(sourceAxisU);
            double minU;
            double maxU;
            double minV;
            double maxV;
            FrameCutRegionService.GetProjectedExtents(region.Boundary,
                sourceAxisU, sourceAxisV, out minU, out maxU,
                out minV, out maxV);
            Point3d sourceCenter = GeometryHelper.FromUv(
                (minU + maxU) * 0.5, (minV + maxV) * 0.5,
                sourceAxisU, sourceAxisV);
            double rotation = -region.Rotation;
            double c = Math.Cos(rotation);
            double s = Math.Sin(rotation);
            Vector3d rotatedSource = new Vector3d(
                sourceCenter.X * c - sourceCenter.Y * s,
                sourceCenter.X * s + sourceCenter.Y * c, 0);
            Point3d insertPoint = targetCenter - rotatedSource;
            BlockReference contentReference = new BlockReference(insertPoint,
                contentBlockId)
            {
                Rotation = rotation
            };
            CadDbHelper.AppendToModelSpace(db, tr, contentReference);
            FramePhysicalCropService.Crop(tr, contentBlock, region.Boundary,
                protectedNestedReferences);
            contentReference.RecordGraphicsModified(true);
        }

        private static ObjectId CreateCroppedSourceBlock(Database db,
            Transaction tr, BlockTable table, ObjectId sourceId,
            IList<Point3d> boundary)
        {
            var clippedBlock = new BlockTableRecord
            {
                Name = CadDbHelper.MakeUniqueBlockName(db, tr,
                    "CDBOX_CLIPPED_SOURCE_"
                    + Guid.NewGuid().ToString("N").Substring(0, 10)),
                Origin = Point3d.Origin
            };
            ObjectId clippedBlockId = table.Add(clippedBlock);
            tr.AddNewlyCreatedDBObject(clippedBlock, true);
            ObjectId clonedSourceId;
            if (!CloneEntityIntoBlock(db, tr, sourceId, clippedBlock,
                    clippedBlockId, out clonedSourceId))
            {
                clippedBlock.Erase();
                return ObjectId.Null;
            }

            // 原块必须先展开并完成实体级裁剪，再将幸存内容作为独立块嵌套。
            // 不能直接引用原块定义，否则外层块炸开后会恢复整块框外内容。
            FramePhysicalCropService.Crop(tr, clippedBlock, boundary, null,
                new HashSet<ObjectId> { clonedSourceId });
            if (!HasLiveEntity(tr, clippedBlock))
            {
                clippedBlock.Erase();
                return ObjectId.Null;
            }
            return clippedBlockId;
        }

        private static bool CloneEntityIntoBlock(Database db, Transaction tr,
            ObjectId sourceId, BlockTableRecord targetBlock,
            ObjectId targetBlockId, out ObjectId clonedId)
        {
            clonedId = ObjectId.Null;
            try
            {
                var single = new ObjectIdCollection { sourceId };
                var mapping = new IdMapping();
                db.DeepCloneObjects(single, targetBlockId, mapping, false);
                foreach (IdPair pair in mapping)
                {
                    if (pair.Key == sourceId && pair.IsCloned)
                    {
                        clonedId = pair.Value;
                        break;
                    }
                }
                return !clonedId.IsNull;
            }
            catch
            {
                // 个别代理对象不支持 DeepCloneObjects 时尝试实体自身克隆。
                try
                {
                    Entity source = tr.GetObject(sourceId,
                        OpenMode.ForRead, false) as Entity;
                    Entity clone = source == null
                        ? null : source.Clone() as Entity;
                    if (clone == null) return false;
                    clonedId = targetBlock.AppendEntity(clone);
                    tr.AddNewlyCreatedDBObject(clone, true);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static bool HasLiveEntity(Transaction tr,
            BlockTableRecord block)
        {
            foreach (ObjectId id in block)
            {
                if (id.IsNull || id.IsErased) continue;
                try
                {
                    Entity entity = tr.GetObject(id, OpenMode.ForRead,
                        false) as Entity;
                    if (entity != null && !entity.IsErased) return true;
                }
                catch
                {
                }
            }
            return false;
        }

        private static List<ObjectId> UnlockLockedLayers(Database db,
            Transaction tr)
        {
            var result = new List<ObjectId>();
            if (db == null || tr == null) return result;
            LayerTable table = tr.GetObject(db.LayerTableId,
                OpenMode.ForRead, false) as LayerTable;
            if (table == null) return result;
            foreach (ObjectId id in table)
            {
                try
                {
                    LayerTableRecord layer = tr.GetObject(id,
                        OpenMode.ForRead, false) as LayerTableRecord;
                    if (layer == null || !layer.IsLocked) continue;
                    layer.UpgradeOpen();
                    layer.IsLocked = false;
                    result.Add(id);
                }
                catch
                {
                    // 外部参照依赖层等不可写图层保持原状。
                }
            }
            return result;
        }

        private static void RestoreLockedLayers(Transaction tr,
            IList<ObjectId> layerIds)
        {
            if (tr == null || layerIds == null) return;
            foreach (ObjectId id in layerIds)
            {
                try
                {
                    if (id.IsNull || id.IsErased) continue;
                    LayerTableRecord layer = tr.GetObject(id,
                        OpenMode.ForWrite, false) as LayerTableRecord;
                    if (layer != null) layer.IsLocked = true;
                }
                catch
                {
                }
            }
        }

        private static ObjectIdCollection CollectRegionSourceIds(Editor editor,
            Database db, Transaction tr, FrameCutRegionInfo region,
            Point3dCollection polygon)
        {
            var candidates = new HashSet<ObjectId>();
            try
            {
                PromptSelectionResult selected =
                    editor.SelectCrossingPolygon(polygon);
                if (selected.Status == PromptStatus.OK
                    && selected.Value != null)
                {
                    foreach (SelectedObject selectedObject in selected.Value)
                    {
                        if (selectedObject != null
                            && !selectedObject.ObjectId.IsNull)
                            candidates.Add(selectedObject.ObjectId);
                    }
                }
            }
            catch
            {
            }

            Vector3d axisU = new Vector3d(Math.Cos(region.Rotation),
                Math.Sin(region.Rotation), 0).GetNormal();
            Vector3d axisV = GeometryHelper.GetPerpLeft(axisU);
            double minU;
            double maxU;
            double minV;
            double maxV;
            FrameCutRegionService.GetProjectedExtents(region.Boundary,
                axisU, axisV, out minU, out maxU, out minV, out maxV);

            BlockTableRecord space = tr.GetObject(db.CurrentSpaceId,
                OpenMode.ForRead, false) as BlockTableRecord;
            if (space == null) return new ObjectIdCollection();

            var ordered = new List<ObjectId>();
            foreach (ObjectId id in GetFullDrawOrder(tr, space))
            {
                if (id.IsNull || id == region.ObjectId) continue;
                Entity entity;
                try
                {
                    entity = tr.GetObject(id, OpenMode.ForRead, false)
                        as Entity;
                }
                catch
                {
                    continue;
                }
                if (entity == null || IsGeneratedFrameEntity(entity, tr))
                    continue;

                bool include = candidates.Contains(id);
                if (!include)
                {
                    Extents3d extents;
                    include = GeometryHelper.TryGetEntityExtents(entity,
                        out extents)
                        && IntersectsProjectedRegion(extents, axisU, axisV,
                            minU, maxU, minV, maxV);
                }
                if (include) ordered.Add(id);
            }
            return new ObjectIdCollection(ordered.ToArray());
        }

        private static IEnumerable<ObjectId> GetFullDrawOrder(Transaction tr,
            BlockTableRecord space)
        {
            try
            {
                DrawOrderTable table = tr.GetObject(space.DrawOrderTableId,
                    OpenMode.ForRead, false) as DrawOrderTable;
                ObjectIdCollection ids = table == null
                    ? null : table.GetFullDrawOrder(0);
                if (ids != null && ids.Count > 0)
                    return ids.Cast<ObjectId>().ToList();
            }
            catch
            {
            }
            return space.Cast<ObjectId>().ToList();
        }

        private static bool IntersectsProjectedRegion(Extents3d extents,
            Vector3d axisU, Vector3d axisV, double minU, double maxU,
            double minV, double maxV)
        {
            List<Point3d> corners =
                GeometryHelper.CreateAxisAlignedRectangleCorners(
                    extents.MinPoint, extents.MaxPoint);
            double entityMinU;
            double entityMaxU;
            double entityMinV;
            double entityMaxV;
            FrameCutRegionService.GetProjectedExtents(corners, axisU, axisV,
                out entityMinU, out entityMaxU, out entityMinV,
                out entityMaxV);
            const double tolerance = 1e-6;
            return entityMaxU >= minU - tolerance
                && entityMinU <= maxU + tolerance
                && entityMaxV >= minV - tolerance
                && entityMinV <= maxV + tolerance;
        }

        private static bool IsGeneratedFrameEntity(Entity entity, Transaction tr)
        {
            if (FrameCutRegionService.HasMetadata(tr, entity)
                || string.Equals(entity.Layer, FrameCutRegionService.RegionLayerName,
                StringComparison.OrdinalIgnoreCase)
                || string.Equals(entity.Layer, "CDBOX_指北针",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(entity.Layer, "CDBOX_比例标注",
                    StringComparison.OrdinalIgnoreCase))
                return true;
            BlockReference reference = entity as BlockReference;
            if (reference == null) return false;
            string name = FrameTemplateService.GetBlockReferenceEffectiveName(tr,
                reference);
            return name.StartsWith("CDBOX_FRAME_",
                StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("CDBOX_CROP_",
                    StringComparison.OrdinalIgnoreCase);
        }

        public LayoutResult LayoutFramesForRectangle(
            Database db,
            Transaction tr,
            FrameTemplateInfo template,
            Point3d rectCorner1,
            Point3d rectCorner2,
            Vector3d cutDirection,
            double overlap)
        {
            if (template == null)
                throw new ArgumentNullException("template");

            string msg;
            if (!template.IsValid(out msg))
                throw new InvalidOperationException(msg);

            if (cutDirection.Length < GeometryHelper.Eps)
                throw new InvalidOperationException("裁图方向无效。");

            ObjectId blockId = CadDbHelper.GetBlockId(db, tr, template.BlockName);
            if (blockId.IsNull)
                throw new InvalidOperationException("当前图中找不到模板块：" + template.BlockName);

            Vector3d axisU = new Vector3d(cutDirection.X, cutDirection.Y, 0).GetNormal();
            Vector3d axisV = GeometryHelper.GetPerpLeft(axisU);

            double rotation = Math.Atan2(axisU.Y, axisU.X);

            double validW = template.ValidWidthWorld;
            double validH = template.ValidHeightWorld;

            if (validW <= GeometryHelper.Eps || validH <= GeometryHelper.Eps)
                throw new InvalidOperationException("模板有效绘制区域尺寸无效。");

            if (overlap < 0)
                overlap = 0;

            if (overlap >= validW)
                throw new InvalidOperationException("搭接长度不能大于或等于单幅有效宽度。");

            double stepU = validW - overlap;
            double stepV = validH;

            List<Point3d> corners = GeometryHelper.CreateAxisAlignedRectangleCorners(rectCorner1, rectCorner2);

            double uMin = double.MaxValue;
            double uMax = double.MinValue;
            double vMin = double.MaxValue;
            double vMax = double.MinValue;

            foreach (Point3d p in corners)
            {
                double u = GeometryHelper.Dot2d(p, axisU);
                double v = GeometryHelper.Dot2d(p, axisV);

                uMin = Math.Min(uMin, u);
                uMax = Math.Max(uMax, u);
                vMin = Math.Min(vMin, v);
                vMax = Math.Max(vMax, v);
            }

            double lenU = uMax - uMin;
            double lenV = vMax - vMin;

            int cols = Math.Max(1, (int)Math.Ceiling(Math.Max(0, lenU - validW) / stepU) + 1);
            int rows = Math.Max(1, (int)Math.Ceiling(lenV / stepV));

            CadDbHelper.EnsureLayer(db, tr, "TC_裁图范围");

            NorthArrowService north = new NorthArrowService();

            int count = 0;

            for (int row = 0; row < rows; row++)
            {
                double cellCenterV = vMin + validH * 0.5 + row * stepV;

                for (int col = 0; col < cols; col++)
                {
                    double cellCenterU = uMin + validW * 0.5 + col * stepU;
                    Point3d validCenterWorld = GeometryHelper.FromUv(cellCenterU, cellCenterV, axisU, axisV);

                    InsertOneFrame(
                        db,
                        tr,
                        blockId,
                        template,
                        validCenterWorld,
                        rotation,
                        validW,
                        validH,
                        north
                    );

                    count++;
                }
            }

            template.LastOverlap = overlap;
            template.Save(FrameTemplateInfo.GetDefaultConfigPath());

            return new LayoutResult
            {
                Count = count,
                Columns = cols,
                Rows = rows
            };
        }

        private void InsertOneFrame(
            Database db,
            Transaction tr,
            ObjectId blockId,
            FrameTemplateInfo template,
            Point3d validCenterWorld,
            double rotation,
            double validW,
            double validH,
            NorthArrowService north)
        {
            Point2d validCenterLocal = template.ValidCenterLocal;

            Point3d insertPoint = GeometryHelper.ComputeInsertPointForLocalPoint(
                validCenterLocal,
                validCenterWorld,
                rotation,
                template.ScaleX,
                template.ScaleY
            );

            BlockReference br = new BlockReference(insertPoint, blockId);
            br.Rotation = rotation;
            br.ScaleFactors = new Scale3d(template.ScaleX, template.ScaleY, template.ScaleZ);

            CadDbHelper.AppendToModelSpace(db, tr, br);

            // 生成每幅有效绘制区域矩形，用于检查分幅位置。
            Vector3d axisU = new Vector3d(Math.Cos(rotation), Math.Sin(rotation), 0).GetNormal();
            Vector3d axisV = GeometryHelper.GetPerpLeft(axisU);

            Polyline clipRect = GeometryHelper.CreateRectanglePolyline(
                validCenterWorld,
                axisU,
                axisV,
                validW,
                validH
            );
            clipRect.Layer = "TC_裁图范围";

            CadDbHelper.AppendToModelSpace(db, tr, clipRect);

            // 指北针放在有效绘制区域右上角内侧。
            Point2d rt = template.ValidRightTopLocal;
            Point2d arrowLocal = new Point2d(
                rt.X - Math.Abs(template.NorthOffsetX),
                rt.Y - Math.Abs(template.NorthOffsetY)
            );

            Point3d arrowWorld = GeometryHelper.LocalToWorld(
                arrowLocal,
                insertPoint,
                rotation,
                template.ScaleX,
                template.ScaleY
            );

            double arrowScale = Math.Max(Math.Abs(template.ScaleX), Math.Abs(template.ScaleY));
            north.InsertNorthArrow(db, tr, arrowWorld, arrowScale);
        }
    }
}
