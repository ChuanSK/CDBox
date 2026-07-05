using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace TCPipeAutoDraw.Core.Cad
{
    /// <summary>
    /// 公共绘图服务。尽量把 AutoCAD API 细节集中在这里，业务模块只描述“画什么”。
    /// </summary>
    public static class CadDrawService
    {
        public static ObjectId DrawPolyline(Database db, Transaction tr, IReadOnlyList<Point3d> points, string layerName, short colorIndex)
        {
            if (points == null || points.Count < 2) return ObjectId.Null;

            CadLayerService.EnsureLayer(db, tr, layerName, colorIndex);
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            var pl = new Polyline();
            for (int i = 0; i < points.Count; i++)
            {
                pl.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
            }
            pl.Elevation = points[0].Z;
            pl.Layer = layerName;

            ObjectId id = btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            return id;
        }

        public static ObjectId DrawText(Database db, Transaction tr, Point3d position, string text, double height, string layerName, short colorIndex)
        {
            return DrawText(db, tr, position, text, height, layerName, colorIndex, null);
        }

        public static ObjectId DrawText(Database db, Transaction tr, Point3d position, string text, double height, string layerName, short colorIndex, string textStyleName)
        {
            if (string.IsNullOrWhiteSpace(text)) return ObjectId.Null;

            CadLayerService.EnsureLayer(db, tr, layerName, colorIndex);
            ObjectId textStyleId = GetExistingTextStyle(db, tr, textStyleName);
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            var dbText = new DBText();
            dbText.Position = position;
            dbText.Height = height <= 0 ? 2.5 : height;
            dbText.TextString = text;
            dbText.Layer = layerName;
            if (!textStyleId.IsNull) dbText.TextStyleId = textStyleId;

            ObjectId id = btr.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);
            return id;
        }


        public static ObjectId DrawMText(Database db, Transaction tr, Point3d position, string text, double height, string layerName, short colorIndex)
        {
            return DrawMText(db, tr, position, text, height, layerName, colorIndex, AttachmentPoint.MiddleCenter, null);
        }

        public static ObjectId DrawMText(Database db, Transaction tr, Point3d position, string text, double height, string layerName, short colorIndex, AttachmentPoint attachment)
        {
            return DrawMText(db, tr, position, text, height, layerName, colorIndex, attachment, null);
        }

        public static ObjectId DrawMText(Database db, Transaction tr, Point3d position, string text, double height, string layerName, short colorIndex, AttachmentPoint attachment, string textStyleName)
        {
            if (string.IsNullOrWhiteSpace(text)) return ObjectId.Null;

            CadLayerService.EnsureLayer(db, tr, layerName, colorIndex);
            ObjectId textStyleId = GetExistingTextStyle(db, tr, textStyleName);
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            var mtext = new MText();
            mtext.Location = position;
            mtext.TextHeight = height <= 0 ? 2.5 : height;
            mtext.Contents = NormalizeMTextContents(text);
            mtext.Layer = layerName;
            mtext.Attachment = attachment;
            if (!textStyleId.IsNull) mtext.TextStyleId = textStyleId;

            ObjectId id = btr.AppendEntity(mtext);
            tr.AddNewlyCreatedDBObject(mtext, true);
            return id;
        }

        private static ObjectId GetExistingTextStyle(Database db, Transaction tr, string textStyleName)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(textStyleName)) return ObjectId.Null;

            textStyleName = textStyleName.Trim();

            try
            {
                TextStyleTable tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                if (tst.Has(textStyleName)) return tst[textStyleName];

                // 不创建新的文字样式，只在当前图形已有文字样式中做一次大小写不敏感匹配。
                foreach (ObjectId id in tst)
                {
                    TextStyleTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as TextStyleTableRecord;
                    if (record == null || record.IsErased) continue;
                    if (string.Equals(record.Name, textStyleName, StringComparison.CurrentCultureIgnoreCase)) return id;
                }
            }
            catch
            {
                // 找不到或读取失败时返回空，让 AutoCAD 使用当前默认文字样式。
            }

            return ObjectId.Null;
        }

        private static string NormalizeMTextContents(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\P");
        }

        public static ObjectId DrawCircle(Database db, Transaction tr, Point3d center, double radius, string layerName, short colorIndex)
        {
            if (radius <= 0) return ObjectId.Null;

            CadLayerService.EnsureLayer(db, tr, layerName, colorIndex);
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            var circle = new Circle(center, Vector3d.ZAxis, radius);
            circle.Layer = layerName;

            ObjectId id = btr.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);
            return id;
        }

        public static Point3d Offset(Point3d p, double dx, double dy)
        {
            return new Point3d(p.X + dx, p.Y + dy, p.Z);
        }

        public static Point3d GetMidPointAlongPath(IReadOnlyList<Point3d> points)
        {
            if (points == null || points.Count == 0) return Point3d.Origin;
            if (points.Count == 1) return points[0];

            double total = 0;
            for (int i = 1; i < points.Count; i++)
            {
                total += points[i - 1].DistanceTo(points[i]);
            }

            if (total <= 1e-9) return points[0];

            double half = total / 2.0;
            double acc = 0;

            for (int i = 1; i < points.Count; i++)
            {
                Point3d a = points[i - 1];
                Point3d b = points[i];
                double seg = a.DistanceTo(b);
                if (seg <= 1e-9) continue;

                if (acc + seg >= half)
                {
                    double t = (half - acc) / seg;
                    return new Point3d(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
                }
                acc += seg;
            }
            return points[points.Count - 1];
        }
    }
}
