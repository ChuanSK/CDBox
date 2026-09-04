// Canonical implementation owned by CDBox.Common.
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    internal sealed class NorthArrowService
    {
        public const string NorthArrowBlockName = "CDBOX_NORTH_ARROW_V4";

        public ObjectId EnsureNorthArrowBlock(Database db, Transaction tr)
        {
            BlockTable table = (BlockTable)tr.GetObject(
                db.BlockTableId, OpenMode.ForRead);
            if (table.Has(NorthArrowBlockName)) return table[NorthArrowBlockName];

            table.UpgradeOpen();
            BlockTableRecord record = new BlockTableRecord
            {
                Name = NorthArrowBlockName,
                Origin = Point3d.Origin
            };
            ObjectId blockId = table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);

            const double centerX = 3.56249999871943;
            const double centerY = 5.65455036982894;
            Append(record, tr, Arc(centerX, centerY,
                3.5624999997078954, 1.7626182520236966,
                1.378974402137497));
            Append(record, tr, Arc(centerX, centerY,
                2.7375000001227119, 1.8788178657643637,
                1.2627747876035571));

            Append(record, tr, Polyline(3.56249999871943, 13.1545503698289,
                5.20646505546756, 3.46564992843196));
            Append(record, tr, Polyline(5.3572210711427, 2.57715170830488,
                5.79449951986317, 0));
            Append(record, tr, Polyline(3.56249999871943, 3.96705036982894,
                4.12088462838437, 2.97460394212976));
            Append(record, tr, Polyline(4.54044005228207, 2.22890597768128,
                5.79449951986317, 0));

            // 对应用户块中的两处 SOLID 填充。Solid 的第 3、4 点顺序
            // 与多段线顺序不同，按 A-B-D-C 传入可保持四边形不自交。
            Solid upperFill = new Solid(
                new Point3d(3.56249999871943, 13.1545503698289, 0),
                new Point3d(1.91853494208772, 3.46564992843196, 0),
                new Point3d(3.56249999871943, 3.96705036982894, 0),
                new Point3d(3.00411536905449, 2.97460394212976, 0));
            upperFill.ColorIndex = 256;
            Append(record, tr, upperFill);

            Solid lowerFill = new Solid(
                new Point3d(1.76777892629616, 2.57715170830488, 0),
                new Point3d(1.3305004775757, 0, 0),
                new Point3d(2.5845599451568, 2.22890597768128, 0),
                new Point3d(2.5845599451568, 2.22890597768128, 0));
            lowerFill.ColorIndex = 256;
            Append(record, tr, lowerFill);

            Append(record, tr, Polyline(1.91853494208772, 3.46564992843196,
                3.56249999871943, 13.1545503698289));
            Append(record, tr, Polyline(3.00411536905449, 2.97460394212976,
                3.56249999871943, 3.96705036982894));
            Append(record, tr, Polyline(1.76777892629616, 2.57715170830488,
                1.3305004775757, 0));
            Append(record, tr, Polyline(2.5845599451568, 2.22890597768128,
                1.3305004775757, 0));
            Append(record, tr, Polyline(3.56249999871943, 3.96705036982894,
                3.56249999871943, 13.1545503698289));

            DBText northText = new DBText();
            northText.TextString = "北";
            northText.Height = 3.0;
            northText.WidthFactor = 0.8;
            northText.HorizontalMode = TextHorizontalMode.TextMid;
            northText.VerticalMode = TextVerticalMode.TextBase;
            northText.Position = new Point3d(
                1.846298881401, 14.7851837737507, 0);
            northText.AlignmentPoint = new Point3d(
                3.56249999871943, 16.3438429916278, 0);
            northText.ColorIndex = 256;
            northText.TextStyleId = ResolveNorthTextStyle(db, tr);
            Append(record, tr, northText);
            try { northText.AdjustAlignment(db); } catch { }
            return blockId;
        }

        public ObjectId InsertNorthArrow(Database db, Transaction tr,
            Point3d position, double size, double rotation)
        {
            ObjectId blockId = EnsureNorthArrowBlock(db, tr);
            double scale = Math.Max(0.01, size / 20.0);
            BlockReference reference = new BlockReference(position, blockId)
            {
                Rotation = rotation,
                ScaleFactors = new Scale3d(scale)
            };
            CadDbHelper.EnsureGeneratedLayer(db, tr, "CDBOX_指北针");
            reference.Layer = "CDBOX_指北针";
            return CadDbHelper.AppendToModelSpace(db, tr, reference);
        }

        public ObjectId InsertNorthArrow(Database db, Transaction tr,
            Point3d position, double scale)
        {
            return InsertNorthArrow(db, tr, position, scale * 20.0, 0);
        }

        private static Arc Arc(double x, double y, double radius,
            double startAngle, double endAngle)
        {
            return new Arc(new Point3d(x, y, 0), radius,
                startAngle, endAngle)
            {
                ColorIndex = 256
            };
        }

        private static Polyline Polyline(double x1, double y1,
            double x2, double y2)
        {
            Polyline line = new Polyline();
            line.AddVertexAt(0, new Point2d(x1, y1), 0, 0, 0);
            line.AddVertexAt(1, new Point2d(x2, y2), 0, 0, 0);
            line.ColorIndex = 256;
            return line;
        }

        private static ObjectId ResolveNorthTextStyle(Database db,
            Transaction tr)
        {
            TextStyleTable styles = (TextStyleTable)tr.GetObject(
                db.TextStyleTableId, OpenMode.ForRead);
            if (styles.Has("宋体")) return styles["宋体"];
            const string styleName = "CDBOX_指北针宋体";
            if (styles.Has(styleName)) return styles[styleName];
            try
            {
                styles.UpgradeOpen();
                TextStyleTableRecord style = new TextStyleTableRecord
                {
                    Name = styleName,
                    FileName = "simsun.ttc"
                };
                ObjectId id = styles.Add(style);
                tr.AddNewlyCreatedDBObject(style, true);
                return id;
            }
            catch
            {
                return db.Textstyle;
            }
        }

        private static void Append(BlockTableRecord record, Transaction tr,
            Entity entity)
        {
            record.AppendEntity(entity);
            tr.AddNewlyCreatedDBObject(entity, true);
        }
    }
}
