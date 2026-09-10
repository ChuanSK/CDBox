using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CDBox.RealEstate.Models;

namespace CDBox.RealEstate.Geometry
{
    /// <summary>
    /// 与 CAD 无关的建筑边长规划器。一个规划结果只产生一个文字高度，
    /// 以保证同一建筑的外边与辅助线注记视觉一致。
    /// </summary>
    public static class BuildingLengthAnnotationPlanner
    {
        private const double RelativeTolerance = 1e-7;
        private const double DirectionTolerance = 1e-8;

        public static BuildingAnnotationPlan Create(
            IEnumerable<BuildingPoint2> source,
            double configuredTextHeight,
            bool adaptiveTextHeight)
        {
            List<BuildingPoint2> points = Normalize(source);
            if (points.Count < 3)
                throw new ArgumentException("建筑物闭合线至少需要三个有效顶点。", "source");
            if (!IsFinite(configuredTextHeight) || configuredTextHeight <= 0)
                throw new ArgumentOutOfRangeException("configuredTextHeight",
                    "注记文字高度必须大于零。");

            double scale = GeometryScale(points);
            double tolerance = Math.Max(1e-9, scale * RelativeTolerance);
            ValidateSimplePolygon(points, tolerance);
            double signedArea = SignedArea(points);
            if (Math.Abs(signedArea) <= tolerance * tolerance)
                throw new ArgumentException("建筑物闭合线面积过小或顶点共线。", "source");

            var plan = new BuildingAnnotationPlan();
            for (int index = 0; index < points.Count; index++)
            {
                BuildingPoint2 start = points[index];
                BuildingPoint2 end = points[(index + 1) % points.Count];
                plan.BoundarySegments.Add(CreateSegment(start, end, false));
                plan.Boundary.Add(new BuildingAreaBoundaryEdge { Start = Point(start), End = Point(end) });
            }

            plan.IsOrthogonal = IsOrthogonal(points, tolerance);
            plan.CanCalculateAreaFromBoundary = points.Count == 4 && plan.IsOrthogonal;
            if (plan.CanCalculateAreaFromBoundary)
                AddRectangleTerm(points, plan);
            else if (plan.IsOrthogonal)
                PartitionRectangles(points, plan, tolerance);
            else
                TriangulateWithHeights(points, plan, tolerance);
            plan.GeometryArea = Math.Abs(signedArea);
            if (Math.Abs(plan.AreaTerms.Sum(x => x.GeometryArea) - plan.GeometryArea)
                > Math.Max(1e-8, plan.GeometryArea * 1e-8))
                throw new ArgumentException("几何分割面积校验失败，请检查房屋边界。");
            plan.CalculatedArea = Math.Round(plan.AreaTerms.Sum(x => x.Area), 2,
                MidpointRounding.AwayFromZero);
            plan.AreaFormula = string.Join(" + ", plan.AreaTerms.Select(x => "(" + x.Formula + ")"))
                + " = " + plan.CalculatedArea.ToString("0.00", CultureInfo.InvariantCulture);

            IEnumerable<BuildingPlannedSegment> all =
                plan.BoundarySegments.Concat(plan.AuxiliarySegments);
            plan.TextHeight = adaptiveTextHeight
                ? ResolveSharedTextHeight(all.Where(s => s.Annotate), configuredTextHeight)
                : configuredTextHeight;
            PositionBoundaryText(plan.BoundarySegments, signedArea,
                plan.TextHeight);
            PositionAuxiliaryText(plan.AuxiliarySegments, plan.TextHeight);
            return plan;
        }

        private static List<BuildingPoint2> Normalize(
            IEnumerable<BuildingPoint2> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            var raw = source.ToList();
            if (raw.Any(x => !IsFinite(x.X) || !IsFinite(x.Y)))
                throw new ArgumentException("建筑物顶点包含无效坐标。", "source");
            if (raw.Count == 0) return raw;

            double scale = GeometryScale(raw);
            double tolerance = Math.Max(1e-9, scale * RelativeTolerance);
            var result = new List<BuildingPoint2>();
            foreach (BuildingPoint2 point in raw)
                if (result.Count == 0 || Distance(result[result.Count - 1], point) > tolerance)
                    result.Add(point);
            if (result.Count > 1 && Distance(result[0], result[result.Count - 1]) <= tolerance)
                result.RemoveAt(result.Count - 1);
            // Collinear intermediate vertices do not define another area term.
            bool changed = true;
            while (changed && result.Count > 3)
            {
                changed = false;
                for (int i = 0; i < result.Count; i++)
                {
                    var a = result[(i + result.Count - 1) % result.Count];
                    var b = result[i]; var c = result[(i + 1) % result.Count];
                    if (Math.Abs(Cross(b - a, c - a)) <= tolerance * Distance(a, c)
                        && Dot(b - a, b - c) <= 0)
                    { result.RemoveAt(i); changed = true; break; }
                }
            }
            if (result.Count >= 3)
            {
                if (SignedArea(result) < 0) result.Reverse();
                int first = Enumerable.Range(0, result.Count).OrderBy(i => result[i].X).ThenBy(i => result[i].Y).First();
                result = result.Skip(first).Concat(result.Take(first)).ToList();
            }
            return result;
        }

        private static BuildingAreaPoint Point(BuildingPoint2 p) => new BuildingAreaPoint { X = p.X, Y = p.Y };
        private static decimal Measurement(double length) => Math.Round((decimal)length, 2, MidpointRounding.AwayFromZero);
        private static string Number(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        private static void AddRectangleTerm(IList<BuildingPoint2> points, BuildingAnnotationPlan plan)
        {
            decimal width = Measurement(Distance(points[0], points[1]));
            decimal height = Measurement(Distance(points[1], points[2]));
            plan.AreaTerms.Add(new BuildingAreaTerm { Sequence = plan.AreaTerms.Count + 1, Method = "rectangle",
                Vertices = points.Select(Point).ToList(), BaseStart = Point(points[0]), BaseEnd = Point(points[1]),
                Apex = Point(points[2]), HeightFoot = Point(points[1]), BaseLength = width, Height = height,
                Area = width * height, GeometryArea = Math.Abs(SignedArea(points)),
                Formula = Number(width) + " × " + Number(height) });
        }

        private static void PartitionRectangles(IList<BuildingPoint2> points, BuildingAnnotationPlan plan, double tolerance)
        {
            var rectangles = BuildingRectanglePartition.Create(points, tolerance);
            // Only shared edges are cuts; neither exterior edges nor imaginary extensions get drawn.
            for (int a = 0; a < rectangles.Count; a++)
                for (int b = a + 1; b < rectangles.Count; b++)
                    for (int i = 0; i < 4; i++)
                        for (int j = 0; j < 4; j++)
                        {
                            var start = rectangles[a][i]; var end = rectangles[a][(i + 1) % 4];
                            var otherStart = rectangles[b][j]; var otherEnd = rectangles[b][(j + 1) % 4];
                            var axis = end - start; double length = axis.Length; var unit = axis * (1 / length);
                            if (Math.Abs(Cross(unit, otherStart - start)) > tolerance
                                || Math.Abs(Cross(unit, otherEnd - start)) > tolerance) continue;
                            double from = Math.Max(0, Math.Min(Dot(otherStart - start, unit), Dot(otherEnd - start, unit)));
                            double to = Math.Min(length, Math.Max(Dot(otherStart - start, unit), Dot(otherEnd - start, unit)));
                            if (to - from <= tolerance) continue;
                            var cut = CreateSegment(start + unit * from, start + unit * to, true);
                            cut.Annotate = false; plan.AuxiliarySegments.Add(cut);
                        }
            foreach (var rectangle in rectangles)
            {
                AddRectangleTerm(rectangle, plan);
                var term = plan.AreaTerms[plan.AreaTerms.Count - 1];
                string width = RectangleDimension(rectangle[0], rectangle[1], plan, tolerance);
                string height = RectangleDimension(rectangle[1], rectangle[2], plan, tolerance);
                term.Formula = width + " × " + height;
            }
            plan.CanCalculateAreaFromBoundary = !plan.AuxiliarySegments.Any(s => s.Annotate);
        }

        private static string RectangleDimension(BuildingPoint2 start, BuildingPoint2 end,
            BuildingAnnotationPlan plan, double tolerance)
        {
            var axis = end - start; double length = axis.Length;
            decimal value = Measurement(length);
            var parallel = plan.BoundarySegments.Where(s => Math.Abs(Cross(axis * (1 / length),
                (s.End - s.Start) * (1 / s.Length))) <= DirectionTolerance).ToList();
            if (parallel.Any(s => Math.Abs(s.Length - length) <= tolerance && Measurement(s.Length) == value))
                return Number(value);
            // Reuse two existing boundary labels only if their rounded arithmetic is exactly the measured dimension.
            foreach (var a in parallel)
                foreach (var b in parallel)
                    if (Math.Abs(a.Length - b.Length - length) <= tolerance
                        && Measurement(a.Length) - Measurement(b.Length) == value)
                        return "(" + Number(Measurement(a.Length)) + " − " + Number(Measurement(b.Length)) + ")";
            var existing = plan.AuxiliarySegments.FirstOrDefault(s =>
                Distance(s.Start, start) <= tolerance && Distance(s.End, end) <= tolerance
                || Distance(s.Start, end) <= tolerance && Distance(s.End, start) <= tolerance);
            if (existing != null) existing.Annotate = true;
            else
            {
                // The side already lies on the boundary or a cut; add just the missing calculation label.
                var dimension = CreateSegment(start, end, true); dimension.Draw = false;
                plan.AuxiliarySegments.Add(dimension);
            }
            return Number(value);
        }

        private static void TriangulateWithHeights(IList<BuildingPoint2> points, BuildingAnnotationPlan plan, double tolerance)
        {
            var remaining = Enumerable.Range(0, points.Count).ToList();
            var triangles = FindFan(points, tolerance);
            if (triangles.Count == 0) triangles = EarClip(points, tolerance);
            var diagonals = new HashSet<string>();
            foreach (int[] triangle in triangles)
            {
                for (int i = 0; i < 3; i++)
                {
                    int a = triangle[i], b = triangle[(i + 1) % 3];
                    string key = Math.Min(a, b) + ":" + Math.Max(a, b);
                    if (!IsOriginalBoundary(a, b, points.Count) && diagonals.Add(key))
                    {
                        var diagonal = CreateSegment(points[a], points[b], true);
                        diagonal.Annotate = false;
                        plan.AuxiliarySegments.Add(diagonal);
                    }
                }
                // The altitude onto the longest side lies inside even an obtuse triangle.
                int side = Enumerable.Range(0, 3).OrderByDescending(i =>
                    Distance(points[triangle[i]], points[triangle[(i + 1) % 3]])).First();
                var start = points[triangle[side]]; var end = points[triangle[(side + 1) % 3]];
                var apex = points[triangle[(side + 2) % 3]]; var axis = end - start;
                var baseSegment = plan.AuxiliarySegments.FirstOrDefault(s =>
                    (Distance(s.Start,start) < tolerance && Distance(s.End,end) < tolerance)
                    || (Distance(s.Start,end) < tolerance && Distance(s.End,start) < tolerance));
                if (baseSegment != null) baseSegment.Annotate = true;
                var foot = start + axis * (Dot(apex - start, axis) / Dot(axis, axis));
                var altitude = CreateSegment(apex, foot, true); altitude.IsHeight = true;
                if (altitude.Length <= tolerance) throw new ArgumentException("三角划分产生了零高度图形。");
                plan.AuxiliarySegments.Add(altitude);
                decimal length = Measurement(axis.Length), height = Measurement(altitude.Length);
                plan.AreaTerms.Add(new BuildingAreaTerm { Sequence = plan.AreaTerms.Count + 1, Method = "triangle",
                    Vertices = triangle.Select(i => Point(points[i])).ToList(),
                    BaseStart = Point(start), BaseEnd = Point(end), Apex = Point(apex), HeightFoot = Point(foot),
                    BaseLength = length, Height = height, Area = length * height / 2m,
                    GeometryArea = Math.Abs(Cross(axis, apex - start)) / 2,
                    Formula = Number(length) + " × " + Number(height) + " ÷ 2" });
            }
        }

        private static List<int[]> FindFan(IList<BuildingPoint2> points, double tolerance)
        {
            List<int[]> best = null; double bestLength = double.MaxValue;
            for (int root = 0; root < points.Count; root++)
            {
                var fan = new List<int[]>(); double length = 0; bool valid = true;
                for (int k = 1; k < points.Count - 1; k++)
                {
                    int a = (root + k) % points.Count, b = (root + k + 1) % points.Count;
                    if (Cross(points[a]-points[root],points[b]-points[root]) <= tolerance*tolerance
                        || !VisibleDiagonal(points,root,a,tolerance) || !VisibleDiagonal(points,root,b,tolerance))
                    { valid = false; break; }
                    fan.Add(new[] {root,a,b});
                    if (k > 1) length += Distance(points[root],points[a]);
                }
                if (valid && length < bestLength) { best = fan; bestLength = length; }
            }
            return best ?? new List<int[]>();
        }

        private static bool VisibleDiagonal(IList<BuildingPoint2> p, int a, int b, double tolerance)
        {
            if (a == b || IsOriginalBoundary(a,b,p.Count)) return true;
            var mid = Midpoint(p[a],p[b]); bool inside = false;
            for (int i=0,j=p.Count-1;i<p.Count;j=i++)
            {
                if ((p[i].Y>mid.Y)!=(p[j].Y>mid.Y)
                    && mid.X<(p[j].X-p[i].X)*(mid.Y-p[i].Y)/(p[j].Y-p[i].Y)+p[i].X) inside=!inside;
                if(i==a||j==a||i==b||j==b) continue;
                double c1=Cross(p[b]-p[a],p[i]-p[a]),c2=Cross(p[b]-p[a],p[j]-p[a]);
                double c3=Cross(p[j]-p[i],p[a]-p[i]),c4=Cross(p[j]-p[i],p[b]-p[i]);
                if(c1*c2 < -tolerance*tolerance && c3*c4 < -tolerance*tolerance) return false;
            }
            return inside;
        }

        private static List<int[]> EarClip(IList<BuildingPoint2> points, double tolerance)
        {
            var remaining=Enumerable.Range(0,points.Count).ToList(); var triangles=new List<int[]>();
            while(remaining.Count>3)
            {
                bool found=false;
                for(int i=0;i<remaining.Count;i++)
                {
                    int a=remaining[(i+remaining.Count-1)%remaining.Count],b=remaining[i],c=remaining[(i+1)%remaining.Count];
                    if(Cross(points[b]-points[a],points[c]-points[b])<=tolerance*tolerance)continue;
                    if(remaining.Any(v=>v!=a&&v!=b&&v!=c&&PointInTriangle(points[v],points[a],points[b],points[c],1,tolerance*tolerance)))continue;
                    triangles.Add(new[]{a,b,c});remaining.RemoveAt(i);found=true;break;
                }
                if(!found)throw new ArgumentException("房屋边界无法完整三角划分，请检查重叠边或自交。");
            }
            triangles.Add(remaining.ToArray());return triangles;
        }

        private static void ValidateSimplePolygon(IList<BuildingPoint2> points, double tolerance)
        {
            for (int i = 0; i < points.Count; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Count];
                var previous = points[(i + points.Count - 1) % points.Count];
                if (Math.Abs(Cross(a - previous, b - a)) <= tolerance * Distance(previous, b)
                    && Dot(a - previous, b - a) < 0)
                    throw new ArgumentException("房屋边界存在折返重叠边。");
                for (int j = i + 1; j < points.Count; j++)
                {
                    if (j == i + 1 || (i == 0 && j == points.Count - 1)) continue;
                    var c = points[j]; var d = points[(j + 1) % points.Count];
                    double eps = tolerance * Math.Max(Distance(a, b), Distance(c, d));
                    double c1 = Cross(b - a, c - a), c2 = Cross(b - a, d - a);
                    double c3 = Cross(d - c, a - c), c4 = Cross(d - c, b - c);
                    if (Math.Max(a.X, b.X) + tolerance < Math.Min(c.X, d.X)
                        || Math.Max(c.X, d.X) + tolerance < Math.Min(a.X, b.X)
                        || Math.Max(a.Y, b.Y) + tolerance < Math.Min(c.Y, d.Y)
                        || Math.Max(c.Y, d.Y) + tolerance < Math.Min(a.Y, b.Y)) continue;
                    if (((c1 >= -eps && c2 <= eps) || (c2 >= -eps && c1 <= eps))
                        && ((c3 >= -eps && c4 <= eps) || (c4 >= -eps && c3 <= eps)))
                        throw new ArgumentException("房屋边界存在自交、重复点或重叠边。");
                }
            }
        }

        private static bool IsOrthogonal(IList<BuildingPoint2> points,
            double tolerance)
        {
            BuildingPoint2 axis = default(BuildingPoint2);
            for (int index = 0; index < points.Count; index++)
            {
                BuildingPoint2 vector = points[(index + 1) % points.Count]
                    - points[index];
                if (vector.Length > tolerance)
                {
                    axis = vector * (1.0 / vector.Length);
                    break;
                }
            }
            if (axis.Length <= tolerance) return false;

            for (int index = 0; index < points.Count; index++)
            {
                BuildingPoint2 vector = points[(index + 1) % points.Count]
                    - points[index];
                double length = vector.Length;
                if (length <= tolerance) return false;
                BuildingPoint2 unit = vector * (1.0 / length);
                double parallel = Math.Abs(Dot(unit, axis));
                double perpendicular = Math.Abs(Cross(unit, axis));
                if (Math.Min(parallel, perpendicular) > DirectionTolerance)
                    return false;
            }
            return true;
        }

        private static bool PointInTriangle(BuildingPoint2 point,
            BuildingPoint2 first, BuildingPoint2 second,
            BuildingPoint2 third, double orientation,
            double tolerance)
        {
            double firstSide = Cross(second - first, point - first)
                * orientation;
            double secondSide = Cross(third - second, point - second)
                * orientation;
            double thirdSide = Cross(first - third, point - third)
                * orientation;
            return firstSide >= -tolerance && secondSide >= -tolerance
                && thirdSide >= -tolerance;
        }

        private static bool IsOriginalBoundary(int first, int second,
            int count)
        {
            int difference = Math.Abs(first - second);
            return difference == 1 || difference == count - 1;
        }

        private static BuildingPlannedSegment CreateSegment(
            BuildingPoint2 start, BuildingPoint2 end, bool auxiliary)
        {
            double length = Distance(start, end);
            return new BuildingPlannedSegment
            {
                Start = start,
                End = end,
                Length = length,
                Text = Measurement(length).ToString("0.00", CultureInfo.InvariantCulture),
                TextRotation = ReadableRotation(Math.Atan2(
                    end.Y - start.Y, end.X - start.X)),
                IsAuxiliary = auxiliary
            };
        }

        private static double ResolveSharedTextHeight(
            IEnumerable<BuildingPlannedSegment> segments,
            double configuredHeight)
        {
            double height = configuredHeight;
            foreach (BuildingPlannedSegment segment in segments)
            {
                int characters = Math.Max(3,
                    string.IsNullOrEmpty(segment.Text) ? 0 : segment.Text.Length);
                double fit = segment.Length / (characters * 0.72 + 0.8);
                height = Math.Min(height, fit);
            }
            return Math.Max(configuredHeight * 0.15, height) * 2.0;
        }

        private static void PositionBoundaryText(
            IEnumerable<BuildingPlannedSegment> segments,
            double signedArea, double textHeight)
        {
            double offset = textHeight * 0.95;
            foreach (BuildingPlannedSegment segment in segments)
            {
                BuildingPoint2 vector = segment.End - segment.Start;
                double length = vector.Length;
                BuildingPoint2 outward = signedArea > 0
                    ? new BuildingPoint2(vector.Y / length, -vector.X / length)
                    : new BuildingPoint2(-vector.Y / length, vector.X / length);
                segment.TextPosition = Midpoint(segment.Start, segment.End)
                    + outward * offset;
            }
        }

        private static void PositionAuxiliaryText(
            IList<BuildingPlannedSegment> segments, double textHeight)
        {
            for (int index = 0; index < segments.Count; index++)
            {
                BuildingPlannedSegment segment = segments[index];
                BuildingPoint2 vector = segment.End - segment.Start;
                double length = vector.Length;
                BuildingPoint2 normal = new BuildingPoint2(
                    -vector.Y / length, vector.X / length);
                if (index == 0) normal = normal * -1.0;
                segment.TextPosition = Midpoint(segment.Start, segment.End)
                    + normal * textHeight * 0.78;
            }
        }

        private static double ReadableRotation(double value)
        {
            while (value > Math.PI) value -= Math.PI * 2.0;
            while (value <= -Math.PI) value += Math.PI * 2.0;
            if (value > Math.PI / 2.0) value -= Math.PI;
            if (value < -Math.PI / 2.0) value += Math.PI;
            return value;
        }

        private static double SignedArea(IList<BuildingPoint2> points)
        {
            double sum = 0;
            for (int index = 0; index < points.Count; index++)
            {
                BuildingPoint2 current = points[index];
                BuildingPoint2 next = points[(index + 1) % points.Count];
                sum += Cross(current - points[0], next - points[0]);
            }
            return sum / 2.0;
        }

        private static double GeometryScale(IList<BuildingPoint2> points)
        {
            if (points == null || points.Count == 0) return 1.0;
            double minimumX = points.Min(x => x.X);
            double maximumX = points.Max(x => x.X);
            double minimumY = points.Min(x => x.Y);
            double maximumY = points.Max(x => x.Y);
            return Math.Max(1.0, Math.Max(maximumX - minimumX,
                maximumY - minimumY));
        }

        private static BuildingPoint2 Midpoint(BuildingPoint2 first,
            BuildingPoint2 second)
        {
            return new BuildingPoint2((first.X + second.X) / 2.0,
                (first.Y + second.Y) / 2.0);
        }

        private static double Distance(BuildingPoint2 first,
            BuildingPoint2 second)
        {
            return (second - first).Length;
        }

        private static double Dot(BuildingPoint2 first, BuildingPoint2 second)
        {
            return first.X * second.X + first.Y * second.Y;
        }

        private static double Cross(BuildingPoint2 first, BuildingPoint2 second)
        {
            return first.X * second.Y - first.Y * second.X;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

    }
}
