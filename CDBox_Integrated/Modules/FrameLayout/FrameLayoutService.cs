using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    internal class FrameLayoutService
    {
        public class LayoutResult
        {
            public int Count { get; set; }
            public int Columns { get; set; }
            public int Rows { get; set; }
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
