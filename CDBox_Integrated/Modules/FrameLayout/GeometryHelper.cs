using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    internal static class GeometryHelper
    {
        public const double Eps = 1e-8;

        public static Vector3d GetPerpLeft(Vector3d u)
        {
            return new Vector3d(-u.Y, u.X, 0.0).GetNormal();
        }

        public static Point3d LocalToWorld(Point2d local, Point3d insertPoint, double rotation, double scaleX, double scaleY)
        {
            double x = local.X * scaleX;
            double y = local.Y * scaleY;

            double c = Math.Cos(rotation);
            double s = Math.Sin(rotation);

            return new Point3d(
                insertPoint.X + x * c - y * s,
                insertPoint.Y + x * s + y * c,
                insertPoint.Z
            );
        }

        public static Point3d ComputeInsertPointForLocalPoint(
            Point2d localPoint,
            Point3d targetWorld,
            double rotation,
            double scaleX,
            double scaleY)
        {
            double x = localPoint.X * scaleX;
            double y = localPoint.Y * scaleY;

            double c = Math.Cos(rotation);
            double s = Math.Sin(rotation);

            Vector3d rotated = new Vector3d(
                x * c - y * s,
                x * s + y * c,
                0.0
            );

            return targetWorld - rotated;
        }

        public static Point2d TransformWorldPointToBlockLocal(BlockReference br, Point3d worldPoint)
        {
            Matrix3d inv = br.BlockTransform.Inverse();
            Point3d local = worldPoint.TransformBy(inv);
            return new Point2d(local.X, local.Y);
        }

        public static Point2d NormalizeMin(Point2d a, Point2d b)
        {
            return new Point2d(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y));
        }

        public static Point2d NormalizeMax(Point2d a, Point2d b)
        {
            return new Point2d(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        public static bool TryGetEntityExtents(Entity ent, out Extents3d ext)
        {
            try
            {
                ext = ent.GeometricExtents;
                return true;
            }
            catch
            {
                ext = new Extents3d();
                return false;
            }
        }

        public static void AddPointToExtents(ref bool hasValue, ref Extents3d ext, Point3d pt)
        {
            if (!hasValue)
            {
                ext = new Extents3d(pt, pt);
                hasValue = true;
            }
            else
            {
                ext.AddPoint(pt);
            }
        }

        public static List<Point3d> CreateAxisAlignedRectangleCorners(Point3d p1, Point3d p2)
        {
            double minX = Math.Min(p1.X, p2.X);
            double maxX = Math.Max(p1.X, p2.X);
            double minY = Math.Min(p1.Y, p2.Y);
            double maxY = Math.Max(p1.Y, p2.Y);

            return new List<Point3d>
            {
                new Point3d(minX, minY, 0),
                new Point3d(maxX, minY, 0),
                new Point3d(maxX, maxY, 0),
                new Point3d(minX, maxY, 0)
            };
        }

        public static double Dot2d(Point3d p, Vector3d axis)
        {
            return p.X * axis.X + p.Y * axis.Y;
        }

        public static Point3d FromUv(double u, double v, Vector3d axisU, Vector3d axisV)
        {
            return new Point3d(
                axisU.X * u + axisV.X * v,
                axisU.Y * u + axisV.Y * v,
                0.0
            );
        }

        public static Polyline CreateRectanglePolyline(
            Point3d center,
            Vector3d axisU,
            Vector3d axisV,
            double width,
            double height)
        {
            Vector3d halfU = axisU.GetNormal() * (width * 0.5);
            Vector3d halfV = axisV.GetNormal() * (height * 0.5);

            Point3d p1 = center - halfU - halfV;
            Point3d p2 = center + halfU - halfV;
            Point3d p3 = center + halfU + halfV;
            Point3d p4 = center - halfU + halfV;

            Polyline pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
            pl.Closed = true;
            return pl;
        }

        public static string FormatPoint(Point3d p)
        {
            return string.Format("X={0:0.###}, Y={1:0.###}, Z={2:0.###}", p.X, p.Y, p.Z);
        }

        public static double NormalizeAngle(double angle)
        {
            double twoPi = Math.PI * 2.0;
            angle = angle % twoPi;
            if (angle < 0)
                angle += twoPi;
            return angle;
        }
    }
}
