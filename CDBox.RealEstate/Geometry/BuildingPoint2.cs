using System;

namespace CDBox.RealEstate.Geometry
{
    public struct BuildingPoint2
    {
        public BuildingPoint2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; private set; }
        public double Y { get; private set; }

        public static BuildingPoint2 operator +(BuildingPoint2 left,
            BuildingPoint2 right)
        {
            return new BuildingPoint2(left.X + right.X, left.Y + right.Y);
        }

        public static BuildingPoint2 operator -(BuildingPoint2 left,
            BuildingPoint2 right)
        {
            return new BuildingPoint2(left.X - right.X, left.Y - right.Y);
        }

        public static BuildingPoint2 operator *(BuildingPoint2 value,
            double scale)
        {
            return new BuildingPoint2(value.X * scale, value.Y * scale);
        }

        public double Length
        {
            get { return Math.Sqrt(X * X + Y * Y); }
        }
    }
}
