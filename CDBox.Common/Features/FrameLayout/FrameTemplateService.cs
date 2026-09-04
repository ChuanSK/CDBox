// Canonical implementation owned by CDBox.Common.
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    internal class FrameTemplateService
    {
        public const string DefaultImportedBlockName = "TC_FRAME_TEMPLATE";

        public FrameTemplateInfo CreateInfoFromBlockReference(
            Database db,
            Transaction tr,
            BlockReference br,
            Point3d validCorner1,
            Point3d validCorner2)
        {
            if (br == null)
                throw new ArgumentNullException("br");

            Point2d local1 = GeometryHelper.TransformWorldPointToBlockLocal(br, validCorner1);
            Point2d local2 = GeometryHelper.TransformWorldPointToBlockLocal(br, validCorner2);

            Point2d validMin = GeometryHelper.NormalizeMin(local1, local2);
            Point2d validMax = GeometryHelper.NormalizeMax(local1, local2);

            string blockName = GetBlockReferenceEffectiveName(tr, br);

            ObjectId btrId = br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord;
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

            bool hasExt;
            Extents3d ext = CadDbHelper.GetBlockDefinitionExtents(tr, btr, out hasExt);

            FrameTemplateInfo info = new FrameTemplateInfo();
            info.TemplateName = blockName;
            info.BlockName = blockName;
            info.ScaleX = br.ScaleFactors.X;
            info.ScaleY = br.ScaleFactors.Y;
            info.ScaleZ = br.ScaleFactors.Z;
            info.ValidMin = validMin;
            info.ValidMax = validMax;
            info.FrameMin = hasExt ? new Point2d(ext.MinPoint.X, ext.MinPoint.Y) : validMin;
            info.FrameMax = hasExt ? new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y) : validMax;
            info.SavedAt = DateTime.Now;

            return info;
        }

        public ObjectId ImportTemplateDwgAsBlock(
            Document doc,
            string dwgPath,
            Point3d insertPoint,
            out string newBlockName)
        {
            if (doc == null)
                throw new ArgumentNullException("doc");

            if (string.IsNullOrWhiteSpace(dwgPath) || !File.Exists(dwgPath))
                throw new FileNotFoundException("模板 DWG 文件不存在。", dwgPath);

            Database targetDb = doc.Database;

            using (Database sourceDb = new Database(false, true))
            {
                sourceDb.ReadDwgFile(dwgPath, FileOpenMode.OpenForReadAndAllShare, true, "");
                sourceDb.CloseInput(true);

                if (!EnsureImportModelSpace(sourceDb))
                    throw new InvalidOperationException(
                        "模板 DWG 的模型空间和布局空间中均没有可导入的图形对象。");

                string rawName = SanitizeBlockName(
                    Path.GetFileNameWithoutExtension(dwgPath));
                if (string.IsNullOrWhiteSpace(rawName))
                    rawName = DefaultImportedBlockName;
                using (Transaction nameTr =
                    targetDb.TransactionManager.StartTransaction())
                {
                    newBlockName = CadDbHelper.MakeUniqueBlockName(targetDb,
                        nameTr, rawName);
                    nameTr.Commit();
                }

                // 整图插入由 AutoCAD 复制嵌套块、图层、样式、代理对象及
                // 关联依赖，比逐实体 WblockCloneObjects 更适合模板图纸。
                ObjectId newBtrId = targetDb.Insert(newBlockName, sourceDb,
                    false);
                if (newBtrId.IsNull)
                    throw new InvalidOperationException(
                        "AutoCAD 未能创建模板图纸块定义。");

                using (Transaction targetTr =
                    targetDb.TransactionManager.StartTransaction())
                {
                    BlockReference br = new BlockReference(insertPoint, newBtrId);
                    br.Layer = "0";
                    BlockTableRecord definition = targetTr.GetObject(newBtrId,
                        OpenMode.ForRead, false) as BlockTableRecord;
                    bool hasExtents = false;
                    Extents3d extents = new Extents3d();
                    if (definition != null)
                        extents = CadDbHelper.GetBlockDefinitionExtents(
                            targetTr, definition, out hasExtents);
                    if (hasExtents)
                    {
                        Point3d localCenter = new Point3d(
                            (extents.MinPoint.X + extents.MaxPoint.X) * 0.5,
                            (extents.MinPoint.Y + extents.MaxPoint.Y) * 0.5,
                            (extents.MinPoint.Z + extents.MaxPoint.Z) * 0.5);
                        Point3d displayedCenter =
                            localCenter.TransformBy(br.BlockTransform);
                        br.Position += insertPoint - displayedCenter;
                    }
                    ObjectId brId = CadDbHelper.AppendToModelSpace(targetDb, targetTr, br);
                    targetTr.Commit();
                    return brId;
                }
            }
        }

        private static bool EnsureImportModelSpace(Database sourceDb)
        {
            if (sourceDb == null) return false;
            using (Transaction tr =
                sourceDb.TransactionManager.StartTransaction())
            {
                BlockTable table = tr.GetObject(sourceDb.BlockTableId,
                    OpenMode.ForRead, false) as BlockTable;
                if (table == null) return false;
                BlockTableRecord modelSpace = tr.GetObject(
                    table[BlockTableRecord.ModelSpace], OpenMode.ForRead,
                    false) as BlockTableRecord;
                if (modelSpace == null) return false;
                if (GetDrawableEntityIds(tr, modelSpace).Count > 0)
                {
                    tr.Commit();
                    return true;
                }

                ObjectIdCollection bestLayoutEntities = null;
                foreach (ObjectId id in table)
                {
                    BlockTableRecord record = tr.GetObject(id,
                        OpenMode.ForRead, false) as BlockTableRecord;
                    if (record == null || !record.IsLayout
                        || record.ObjectId == modelSpace.ObjectId)
                        continue;
                    ObjectIdCollection ids =
                        GetDrawableEntityIds(tr, record);
                    if (ids.Count > 0 && (bestLayoutEntities == null
                        || ids.Count > bestLayoutEntities.Count))
                        bestLayoutEntities = ids;
                }
                if (bestLayoutEntities == null
                    || bestLayoutEntities.Count == 0)
                    return false;

                modelSpace.UpgradeOpen();
                sourceDb.DeepCloneObjects(bestLayoutEntities,
                    modelSpace.ObjectId, new IdMapping(), false);
                tr.Commit();
                return true;
            }
        }

        private static ObjectIdCollection GetDrawableEntityIds(
            Transaction tr, BlockTableRecord record)
        {
            var result = new ObjectIdCollection();
            if (tr == null || record == null) return result;
            foreach (ObjectId id in record)
            {
                if (id.IsNull || id.IsErased) continue;
                try
                {
                    Entity entity = tr.GetObject(id, OpenMode.ForRead,
                        false) as Entity;
                    if (entity != null && !(entity is Viewport))
                        result.Add(id);
                }
                catch
                {
                }
            }
            return result;
        }

        private static string SanitizeBlockName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DefaultImportedBlockName;
            char[] invalid = { '<', '>', '/', '\\', '"', ':', ';', '?',
                '*', '|', ',', '=', '`' };
            string result = value.Trim();
            foreach (char character in invalid)
                result = result.Replace(character, '_');
            return string.IsNullOrWhiteSpace(result)
                ? DefaultImportedBlockName : result;
        }

        public void ExportBlockDefinition(Database db, ObjectId blockTableRecordId,
            string targetPath)
        {
            if (db == null) throw new ArgumentNullException("db");
            if (blockTableRecordId.IsNull)
                throw new InvalidOperationException("图框块定义无效。");
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("模板保存路径为空。", "targetPath");

            string directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using (Database exported = db.Wblock(blockTableRecordId))
            {
                exported.SaveAs(targetPath, DwgVersion.Current);
            }
        }

        public ObjectId EnsureTemplateBlock(Database targetDb, Transaction targetTr,
            FrameTemplateCatalogItem template)
        {
            if (targetDb == null) throw new ArgumentNullException("targetDb");
            if (targetTr == null) throw new ArgumentNullException("targetTr");
            if (template == null) throw new ArgumentNullException("template");

            BlockTable targetBt = (BlockTable)targetTr.GetObject(
                targetDb.BlockTableId, OpenMode.ForRead);
            if (targetBt.Has(template.BlockName))
                return targetBt[template.BlockName];

            string sourcePath = FrameTemplateCatalogStore.ResolveTemplatePath(
                template.SourceDwgPath);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException(
                    "模板资源文件不存在，请在图框设置中重新添加该模板。", sourcePath);

            using (Database sourceDb = new Database(false, true))
            {
                sourceDb.ReadDwgFile(sourcePath,
                    FileOpenMode.OpenForReadAndAllShare, true, string.Empty);
                sourceDb.CloseInput(true);

                targetBt.UpgradeOpen();
                BlockTableRecord newBtr = new BlockTableRecord
                {
                    Name = template.BlockName,
                    Origin = Point3d.Origin
                };
                ObjectId newBtrId = targetBt.Add(newBtr);
                targetTr.AddNewlyCreatedDBObject(newBtr, true);

                ObjectIdCollection sourceIds = new ObjectIdCollection();
                using (Transaction sourceTr =
                    sourceDb.TransactionManager.StartTransaction())
                {
                    BlockTable sourceBt = (BlockTable)sourceTr.GetObject(
                        sourceDb.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord sourceMs = (BlockTableRecord)sourceTr.GetObject(
                        sourceBt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    foreach (ObjectId id in sourceMs)
                    {
                        if (id.IsNull || id.IsErased) continue;
                        if (sourceTr.GetObject(id, OpenMode.ForRead, false) is Entity)
                            sourceIds.Add(id);
                    }
                    if (sourceIds.Count == 0)
                        throw new InvalidOperationException("模板资源中没有可用图形。");

                    IdMapping mapping = new IdMapping();
                    sourceDb.WblockCloneObjects(sourceIds, newBtrId, mapping,
                        DuplicateRecordCloning.Ignore, false);
                    sourceTr.Commit();
                }
                return newBtrId;
            }
        }

        public static string BuildCatalogBlockName(string templateId)
        {
            string suffix = string.IsNullOrWhiteSpace(templateId)
                ? Guid.NewGuid().ToString("N").Substring(0, 12)
                : new string(templateId.Where(char.IsLetterOrDigit).ToArray());
            if (suffix.Length > 16) suffix = suffix.Substring(0, 16);
            if (suffix.Length == 0) suffix = Guid.NewGuid().ToString("N").Substring(0, 12);
            return "CDBOX_FRAME_" + suffix.ToUpperInvariant();
        }

        public static string GetBlockReferenceEffectiveName(Transaction tr, BlockReference br)
        {
            try
            {
                ObjectId id = br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord;
                BlockTableRecord btr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord;

                if (btr != null)
                    return btr.Name;
            }
            catch
            {
            }

            return br.Name;
        }
    }
}
