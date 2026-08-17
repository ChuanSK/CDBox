using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CDBox.RealEstate.Geometry
{
    /// <summary>
    /// 与 CAD 无关的建筑边长规划器。一个规划结果只产生一个文字高度，
    /// 以保证同一建筑的外边与辅助线注记视觉一致。
    /// </summary>
    public static class BuildingLengthAnnotationPlanner
    {
        private const double RelativeTolerance = 1e-7;
        private const double DirectionTolerance = 1e-5;

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
            double signedArea = SignedArea(points);
            if (Math.Abs(signedArea) <= tolerance * tolerance)
                throw new ArgumentException("建筑物闭合线面积过小或顶点共线。", "source");

            var plan = new BuildingAnnotationPlan();
            for (int index = 0; index < points.Count; index++)
            {
                BuildingPoint2 start = points[index];
                BuildingPoint2 end = points[(index + 1) % points.Count];
                plan.BoundarySegments.Add(CreateSegment(start, end, false));
            }

            plan.IsOrthogonal = IsOrthogonal(points, tolerance);
            plan.CanCalculateAreaFromBoundary = points.Count == 3
                || (points.Count == 4
                    ? HasEqualOppositeLengths(plan.BoundarySegments)
                    : plan.IsOrthogonal);
            if (!plan.CanCalculateAreaFromBoundary && points.Count == 4)
            {
                AddQuadrilateralAuxiliaries(points, plan, tolerance);
                if (plan.AuxiliarySegments.Count == 0)
                    plan.Warning = "该非正交四边形无法自动生成有效面积计算辅助线，请人工复核。";
            }
            else if (!plan.CanCalculateAreaFromBoundary && points.Count > 4)
            {
                if (!AddTriangulationAuxiliaries(points, plan, tolerance))
                    plan.Warning = "该非正交建筑无法完整拆分为三角形，请人工复核面积计算辅助线。";
            }

            IEnumerable<BuildingPlannedSegment> all =
                plan.BoundarySegments.Concat(plan.AuxiliarySegments);
            plan.TextHeight = adaptiveTextHeight
                ? ResolveSharedTextHeight(all, configuredTextHeight)
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
            return result;
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
                if (Math.Min(Math.Abs(1.0 - parallel),
                        Math.Abs(1.0 - perpendicular)) > DirectionTolerance)
                    return false;
            }
            return true;
        }

        private static bool HasEqualOppositeLengths(
            IList<BuildingPlannedSegment> segments)
        {
            if (segments == null || segments.Count != 4) return false;
            return string.Equals(segments[0].Text, segments[2].Text,
                       StringComparison.Ordinal)
                   && string.Equals(segments[1].Text, segments[3].Text,
                       StringComparison.Ordinal);
        }

        private static void AddQuadrilateralAuxiliaries(
            IList<BuildingPoint2> points, BuildingAnnotationPlan plan,
            double tolerance)
        {
            DiagonalCandidate first = CreateDiagonalCandidate(points, 0, 2,
                1, 3, tolerance);
            DiagonalCandidate second = CreateDiagonalCandidate(points, 1, 3,
                0, 2, tolerance);
            DiagonalCandidate selected = first == null ? second :
                second == null || first.Length >= second.Length ? first : second;
            if (selected == null) return;

            plan.AuxiliarySegments.Add(CreateSegment(
                selected.Start, selected.End, true));
            plan.AuxiliarySegments.Add(CreateSegment(
                selected.SideA, selected.FootA, true));
            plan.AuxiliarySegments.Add(CreateSegment(
                selected.SideB, selected.FootB, true));
        }

        private static DiagonalCandidate CreateDiagonalCandidate(
            IList<BuildingPoint2> points, int startIndex, int endIndex,
            int sideAIndex, int sideBIndex, double tolerance)
        {
            BuildingPoint2 start = points[startIndex];
            BuildingPoint2 end = points[endIndex];
            BuildingPoint2 axis = end - start;
            double lengthSquared = Dot(axis, axis);
            if (lengthSquared <= tolerance * tolerance) return null;

            BuildingPoint2 sideA = points[sideAIndex];
            BuildingPoint2 sideB = points[sideBIndex];
            double parameterA = Dot(sideA - start, axis) / lengthSquared;
            double parameterB = Dot(sideB - start, axis) / lengthSquared;
            double parameterTolerance = tolerance / Math.Sqrt(lengthSquared);
            if (parameterA < -parameterTolerance || parameterA > 1 + parameterTolerance
                || parameterB < -parameterTolerance || parameterB > 1 + parameterTolerance)
                return null;

            double sideSignA = Cross(axis, sideA - start);
            double sideSignB = Cross(axis, sideB - start);
            double sideTolerance = tolerance * Math.Sqrt(lengthSquared);
            if (Math.Abs(sideSignA) <= sideTolerance
                || Math.Abs(sideSignB) <= sideTolerance
                || sideSignA * sideSignB >= 0) return null;

            return new DiagonalCandidate
            {
                Start = start,
                End = end,
                SideA = sideA,
                SideB = sideB,
                FootA = start + axis * parameterA,
                FootB = start + axis * parameterB,
                Length = Math.Sqrt(lengthSquared)
            };
        }

        private static bool AddTriangulationAuxiliaries(
            IList<BuildingPoint2> points, BuildingAnnotationPlan plan,
            double tolerance)
        {
            var remaining = Enumerable.Range(0, points.Count).ToList();
            var diagonals = new HashSet<string>(
                StringComparer.Ordinal);
            double orientation = Math.Sign(SignedArea(points));
            double areaTolerance = tolerance * tolerance;
            int guard = points.Count * points.Count;

            while (remaining.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int offset = 0; offset < remaining.Count; offset++)
                {
                    int previousIndex = remaining[(offset + remaining.Count - 1)
                        % remaining.Count];
                    int currentIndex = remaining[offset];
                    int nextIndex = remaining[(offset + 1) % remaining.Count];
                    BuildingPoint2 previous = points[previousIndex];
                    BuildingPoint2 current = points[currentIndex];
                    BuildingPoint2 next = points[nextIndex];
                    double turn = Cross(current - previous, next - current);
                    if (turn * orientation <= areaTolerance) continue;

                    bool containsVertex = false;
                    foreach (int candidateIndex in remaining)
                    {
                        if (candidateIndex == previousIndex
                            || candidateIndex == currentIndex
                            || candidateIndex == nextIndex) continue;
                        if (!PointInTriangle(points[candidateIndex], previous,
                            current, next, orientation, areaTolerance)) continue;
                        containsVertex = true;
                        break;
                    }
                    if (containsVertex) continue;

                    if (!IsOriginalBoundary(previousIndex, nextIndex,
                        points.Count))
                    {
                        int first = Math.Min(previousIndex, nextIndex);
                        int second = Math.Max(previousIndex, nextIndex);
                        string key = first + ":" + second;
                        if (diagonals.Add(key))
                            plan.AuxiliarySegments.Add(CreateSegment(
                                previous, next, true));
                    }
                    remaining.RemoveAt(offset);
                    clipped = true;
                    break;
                }
                if (!clipped) return false;
            }
            return remaining.Count == 3
                && plan.AuxiliarySegments.Count == points.Count - 3;
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
                Text = length.ToString("0.00", CultureInfo.InvariantCulture),
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
                sum += current.X * next.Y - next.X * current.Y;
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

        private sealed class DiagonalCandidate
        {
            public BuildingPoint2 Start;
            public BuildingPoint2 End;
            public BuildingPoint2 SideA;
            public BuildingPoint2 SideB;
            public BuildingPoint2 FootA;
            public BuildingPoint2 FootB;
            public double Length;
        }
    }
}
