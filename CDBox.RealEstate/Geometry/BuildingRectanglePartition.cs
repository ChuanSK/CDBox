using System;
using System.Collections.Generic;
using System.Linq;

namespace CDBox.RealEstate.Geometry
{
    /// <summary>Partition in the building's own axes, so rotated orthogonal outlines behave identically.</summary>
    internal static class BuildingRectanglePartition
    {
        private sealed class Rectangle
        {
            public double Left, Right, Bottom, Top;
            public double Perimeter => 2 * (Right - Left + Top - Bottom);
        }

        internal static List<List<BuildingPoint2>> Create(IList<BuildingPoint2> points, double tolerance)
        {
            var origin = points[0];
            var x = points[1] - origin; x = x * (1 / x.Length);
            var y = new BuildingPoint2(-x.Y, x.X);
            var local = points.Select(p => new BuildingPoint2(Dot(p - origin, x), Dot(p - origin, y))).ToList();
            var horizontal = Sweep(local, tolerance);
            var vertical = Sweep(local.Select(p => new BuildingPoint2(p.Y, p.X)).ToList(), tolerance)
                .Select(r => new Rectangle { Left = r.Bottom, Right = r.Top, Bottom = r.Left, Top = r.Right }).ToList();
            // For a fixed outline, total rectangle perimeter = boundary perimeter + twice the cuts.
            var best = vertical.Count < horizontal.Count || (vertical.Count == horizontal.Count
                && vertical.Sum(r => r.Perimeter) < horizontal.Sum(r => r.Perimeter) - tolerance)
                ? vertical : horizontal;
            return best.OrderBy(r => r.Bottom).ThenBy(r => r.Left).Select(r => new List<BuildingPoint2> {
                origin + x * r.Left + y * r.Bottom, origin + x * r.Right + y * r.Bottom,
                origin + x * r.Right + y * r.Top, origin + x * r.Left + y * r.Top }).ToList();
        }

        private static List<Rectangle> Sweep(IList<BuildingPoint2> points, double tolerance)
        {
            var levels = new List<double>();
            foreach (double level in points.Select(p => p.Y).OrderBy(v => v))
                if (levels.Count == 0 || level - levels[levels.Count - 1] > tolerance) levels.Add(level);
            var result = new List<Rectangle>();
            var active = new List<Rectangle>();
            for (int i = 0; i + 1 < levels.Count; i++)
            {
                double bottom = levels[i], top = levels[i + 1], mid = (bottom + top) / 2;
                var crossings = new List<double>();
                for (int edge = 0; edge < points.Count; edge++)
                {
                    var a = points[edge]; var b = points[(edge + 1) % points.Count];
                    if ((a.Y > mid) != (b.Y > mid))
                        crossings.Add(a.X + (mid - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                }
                crossings.Sort();
                if (crossings.Count % 2 != 0) throw new ArgumentException("矩形分割交点不成对，请检查房屋边界。");
                var next = new List<Rectangle>();
                for (int j = 0; j < crossings.Count; j += 2)
                {
                    double left = crossings[j], right = crossings[j + 1];
                    if (right - left <= tolerance) continue;
                    var rectangle = active.FirstOrDefault(r => Math.Abs(r.Left - left) <= tolerance
                        && Math.Abs(r.Right - right) <= tolerance);
                    if (rectangle == null)
                    {
                        rectangle = new Rectangle { Left = left, Right = right, Bottom = bottom, Top = top };
                        result.Add(rectangle);
                    }
                    else rectangle.Top = top;
                    next.Add(rectangle);
                }
                active = next;
            }
            return result;
        }

        private static double Dot(BuildingPoint2 a, BuildingPoint2 b) => a.X * b.X + a.Y * b.Y;
    }
}
