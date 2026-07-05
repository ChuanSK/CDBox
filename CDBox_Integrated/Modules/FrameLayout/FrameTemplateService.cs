using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.IO;

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

                using (Transaction targetTr = targetDb.TransactionManager.StartTransaction())
                {
                    string rawName = Path.GetFileNameWithoutExtension(dwgPath);
                    if (string.IsNullOrWhiteSpace(rawName))
                        rawName = DefaultImportedBlockName;

                    newBlockName = CadDbHelper.MakeUniqueBlockName(targetDb, targetTr, rawName);

                    BlockTable targetBt = (BlockTable)targetTr.GetObject(targetDb.BlockTableId, OpenMode.ForWrite);

                    BlockTableRecord newBtr = new BlockTableRecord();
                    newBtr.Name = newBlockName;

                    ObjectId newBtrId = targetBt.Add(newBtr);
                    targetTr.AddNewlyCreatedDBObject(newBtr, true);

                    ObjectIdCollection sourceIds = new ObjectIdCollection();

                    using (Transaction sourceTr = sourceDb.TransactionManager.StartTransaction())
                    {
                        BlockTable sourceBt = (BlockTable)sourceTr.GetObject(sourceDb.BlockTableId, OpenMode.ForRead);
                        BlockTableRecord sourceMs = (BlockTableRecord)sourceTr.GetObject(
                            sourceBt[BlockTableRecord.ModelSpace],
                            OpenMode.ForRead
                        );

                        foreach (ObjectId id in sourceMs)
                        {
                            if (id.IsNull || id.IsErased)
                                continue;

                            Entity ent = sourceTr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent == null)
                                continue;

                            sourceIds.Add(id);
                        }

                        if (sourceIds.Count == 0)
                            throw new InvalidOperationException("模板 DWG 模型空间中没有可导入的图形对象。");

                        IdMapping mapping = new IdMapping();
                        sourceDb.WblockCloneObjects(
                            sourceIds,
                            newBtrId,
                            mapping,
                            DuplicateRecordCloning.Replace,
                            false
                        );

                        sourceTr.Commit();
                    }

                    BlockReference br = new BlockReference(insertPoint, newBtrId);
                    br.Layer = "0";

                    ObjectId brId = CadDbHelper.AppendToModelSpace(targetDb, targetTr, br);

                    targetTr.Commit();

                    return brId;
                }
            }
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
