using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    internal class NorthArrowService
    {
        public const string NorthArrowBlockName = "TC_NORTH_ARROW";

        public ObjectId EnsureNorthArrowBlock(Database db, Transaction tr)
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            if (bt.Has(NorthArrowBlockName))
                return bt[NorthArrowBlockName];

            bt.UpgradeOpen();

            BlockTableRecord btr = new BlockTableRecord();
            btr.Name = NorthArrowBlockName;

            ObjectId blockId = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);

            // 箭头块默认以原点为底部，沿本地 +Y 指向北。
            Line shaft = new Line(new Point3d(0, 0, 0), new Point3d(0, 8, 0));
            btr.AppendEntity(shaft);
            tr.AddNewlyCreatedDBObject(shaft, true);

            Polyline head = new Polyline();
            head.AddVertexAt(0, new Point2d(0, 10), 0, 0, 0);
            head.AddVertexAt(1, new Point2d(-2.2, 6.2), 0, 0, 0);
            head.AddVertexAt(2, new Point2d(2.2, 6.2), 0, 0, 0);
            head.Closed = true;

            btr.AppendEntity(head);
            tr.AddNewlyCreatedDBObject(head, true);

            DBText nText = new DBText();
            nText.TextString = "N";
            nText.Height = 3.5;
            nText.Position = new Point3d(-1.4, 11.2, 0);
            nText.Rotation = 0;

            btr.AppendEntity(nText);
            tr.AddNewlyCreatedDBObject(nText, true);

            return blockId;
        }

        public ObjectId InsertNorthArrow(
            Database db,
            Transaction tr,
            Point3d position,
            double scale)
        {
            ObjectId arrowBlockId = EnsureNorthArrowBlock(db, tr);

            BlockReference br = new BlockReference(position, arrowBlockId);

            // 箭头块默认 +Y 为北，旋转为 0 时指向 WCS 正北。
            br.Rotation = 0.0;
            br.ScaleFactors = new Scale3d(scale);

            CadDbHelper.EnsureLayer(db, tr, "TC_指北针");
            br.Layer = "TC_指北针";

            return CadDbHelper.AppendToModelSpace(db, tr, br);
        }
    }
}
