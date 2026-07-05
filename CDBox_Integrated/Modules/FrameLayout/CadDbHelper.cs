using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    internal static class CadDbHelper
    {
        public static ObjectId EnsureLayer(Database db, Transaction tr, string layerName)
        {
            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            if (lt.Has(layerName))
                return lt[layerName];

            lt.UpgradeOpen();

            LayerTableRecord ltr = new LayerTableRecord();
            ltr.Name = layerName;

            ObjectId id = lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);

            return id;
        }

        public static ObjectId AppendToModelSpace(Database db, Transaction tr, Entity ent)
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            ObjectId id = ms.AppendEntity(ent);
            tr.AddNewlyCreatedDBObject(ent, true);
            return id;
        }

        public static ObjectId GetModelSpaceId(Database db, Transaction tr)
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return bt[BlockTableRecord.ModelSpace];
        }

        public static string MakeUniqueBlockName(Database db, Transaction tr, string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "TC_FRAME_TEMPLATE";

            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            string name = baseName;
            int index = 1;

            while (bt.Has(name))
            {
                name = baseName + "_" + index.ToString("000");
                index++;
            }

            return name;
        }

        public static bool BlockExists(Database db, Transaction tr, string blockName)
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return bt.Has(blockName);
        }

        public static ObjectId GetBlockId(Database db, Transaction tr, string blockName)
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            if (!bt.Has(blockName))
                return ObjectId.Null;

            return bt[blockName];
        }

        public static Extents3d GetBlockDefinitionExtents(Transaction tr, BlockTableRecord btr, out bool hasExtents)
        {
            hasExtents = false;
            Extents3d ext = new Extents3d();

            foreach (ObjectId id in btr)
            {
                if (id.IsNull || id.IsErased)
                    continue;

                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null)
                    continue;

                Extents3d e;
                if (!GeometryHelper.TryGetEntityExtents(ent, out e))
                    continue;

                GeometryHelper.AddPointToExtents(ref hasExtents, ref ext, e.MinPoint);
                GeometryHelper.AddPointToExtents(ref hasExtents, ref ext, e.MaxPoint);
            }

            return ext;
        }

        public static void CopyLayerFromSourceIfNeeded(Database targetDb, Transaction targetTr, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return;

            EnsureLayer(targetDb, targetTr, layerName);
        }
    }
}
