using System;
using System.Collections.Generic;
using System.Linq;

namespace CDBox.RealEstate.Models
{
    public sealed class ParcelBoundaryDescriptionDraft
    {
        public string PointDescription { get; set; }
        public string LineDescription { get; set; }
        public string NorthBoundary { get; set; }
        public string EastBoundary { get; set; }
        public string SouthBoundary { get; set; }
        public string WestBoundary { get; set; }

        public ParcelBoundaryDescriptionDraft()
        {
            PointDescription = string.Empty;
            LineDescription = string.Empty;
            NorthBoundary = string.Empty;
            EastBoundary = string.Empty;
            SouthBoundary = string.Empty;
            WestBoundary = string.Empty;
        }
    }

    public static class ParcelBoundaryDescriptionGenerator
    {
        public static ParcelBoundaryDescriptionDraft Generate(
            ParcelSurveyRecord record)
        {
            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            IList<ParcelBoundaryPointRecord> points = record.Boundary.Points;
            IList<ParcelBoundarySegmentRecord> segments =
                record.Boundary.Segments;
            Tuple<decimal, decimal> center = GeometricCenter(points);
            decimal centerX = center.Item1;
            decimal centerY = center.Item2;

            var result = new ParcelBoundaryDescriptionDraft
            {
                PointDescription = BuildPointDescription(points, segments,
                    centerX, centerY),
                LineDescription = BuildLineDescription(points, segments)
            };
            IDictionary<string, string> boundaries = BuildFourBoundaries(
                points, segments, centerX, centerY);
            result.NorthBoundary = boundaries["北"];
            result.EastBoundary = boundaries["东"];
            result.SouthBoundary = boundaries["南"];
            result.WestBoundary = boundaries["西"];
            return result;
        }

        public static ParcelBoundaryDescriptionDraft Apply(
            ParcelSurveyRecord record, bool overwriteManual)
        {
            if (record == null) throw new ArgumentNullException("record");
            record.Normalize();
            ParcelBoundaryDescriptionDraft draft = Generate(record);
            ApplyRowDescriptions(record, overwriteManual);
            Set(record, "boundary.pointDescription",
                draft.PointDescription, overwriteManual);
            Set(record, "boundary.lineDescription",
                draft.LineDescription, overwriteManual);
            Set(record, "parcel.northBoundary",
                draft.NorthBoundary, overwriteManual);
            Set(record, "parcel.eastBoundary",
                draft.EastBoundary, overwriteManual);
            Set(record, "parcel.southBoundary",
                draft.SouthBoundary, overwriteManual);
            Set(record, "parcel.westBoundary",
                draft.WestBoundary, overwriteManual);
            return draft;
        }

        private static void ApplyRowDescriptions(ParcelSurveyRecord record,
            bool overwriteManual)
        {
            IList<ParcelBoundaryPointRecord> points = record.Boundary.Points;
            IList<ParcelBoundarySegmentRecord> segments =
                record.Boundary.Segments;
            Tuple<decimal, decimal> center = GeometricCenter(points);
            decimal centerX = center.Item1;
            decimal centerY = center.Item2;
            foreach (ParcelBoundaryPointRecord point in points)
            {
                if (!overwriteManual && point.Status == ParcelFieldStatus.Manual
                    && !string.IsNullOrWhiteSpace(point.Description)) continue;
                ParcelBoundarySegmentRecord segment = FindSegment(point,
                    segments);
                point.Description = (point.PointNumber ?? string.Empty)
                    + "位于本宗地" + Compass(point, centerX, centerY)
                    + "方向" + PointLocation(segment);
                if (point.Status != ParcelFieldStatus.Manual || overwriteManual)
                    point.Status = ParcelFieldStatus.Automatic;
            }
            foreach (ParcelBoundarySegmentRecord segment in segments)
            {
                if (!overwriteManual && !string.IsNullOrWhiteSpace(
                    segment.Description)) continue;
                ParcelBoundaryPointRecord start = FindPoint(points,
                    segment.StartPointNumber);
                ParcelBoundaryPointRecord end = FindPoint(points,
                    segment.EndPointNumber);
                string direction = !string.IsNullOrWhiteSpace(
                    segment.Direction) ? segment.Direction.Trim()
                    : Direction(start, end);
                string middle = NormalizeMiddle(segment.MiddlePointNumbers);
                segment.Description = "由 " + (segment.StartPointNumber
                    ?? string.Empty) + " 向" + direction
                    + "方向沿本宗地" + LineLocation(segment)
                    + (string.IsNullOrWhiteSpace(middle) ? string.Empty
                        : "经 " + middle + " ") + "至 "
                    + (segment.EndPointNumber ?? string.Empty);
            }
        }

        private static string BuildPointDescription(
            IList<ParcelBoundaryPointRecord> points,
            IList<ParcelBoundarySegmentRecord> segments,
            decimal centerX, decimal centerY)
        {
            var groups = new List<PointDescriptionGroup>();
            foreach (ParcelBoundaryPointRecord point in points)
            {
                string direction = Compass(point, centerX, centerY);
                ParcelBoundarySegmentRecord segment = FindSegment(point,
                    segments);
                string location = PointLocation(segment);
                string key = direction + "|" + location;
                PointDescriptionGroup last = groups.LastOrDefault();
                if (last == null || !string.Equals(last.Key, key,
                    StringComparison.Ordinal))
                {
                    last = new PointDescriptionGroup
                    {
                        Key = key,
                        Direction = direction,
                        Location = location
                    };
                    groups.Add(last);
                }
                last.PointNumbers.Add(point.PointNumber ?? string.Empty);
            }
            return string.Join(Environment.NewLine, groups.Where(x =>
                x.PointNumbers.Count > 0).Select(x => PointRange(
                    x.PointNumbers) + " 位于本宗地" + x.Direction
                    + "方向" + x.Location + "；"));
        }

        private static string BuildLineDescription(
            IList<ParcelBoundaryPointRecord> points,
            IList<ParcelBoundarySegmentRecord> segments)
        {
            var lines = new List<string>();
            foreach (ParcelBoundarySegmentRecord segment in segments)
            {
                ParcelBoundaryPointRecord start = FindPoint(points,
                    segment.StartPointNumber);
                ParcelBoundaryPointRecord end = FindPoint(points,
                    segment.EndPointNumber);
                string direction = !string.IsNullOrWhiteSpace(
                    segment.Direction) ? segment.Direction.Trim()
                    : Direction(start, end);
                string middle = NormalizeMiddle(segment.MiddlePointNumbers);
                string through = string.IsNullOrWhiteSpace(middle)
                    ? string.Empty : "经 " + middle.Replace("、", "、") + " ";
                lines.Add((segment.StartPointNumber ?? string.Empty)
                    + " 至 " + (segment.EndPointNumber ?? string.Empty)
                    + "：由 " + (segment.StartPointNumber ?? string.Empty)
                    + " 向" + direction + "方向沿本宗地"
                    + LineLocation(segment) + through + "至 "
                    + (segment.EndPointNumber ?? string.Empty) + "；");
            }
            return string.Join(Environment.NewLine, lines);
        }

        private static IDictionary<string, string> BuildFourBoundaries(
            IList<ParcelBoundaryPointRecord> points,
            IList<ParcelBoundarySegmentRecord> segments,
            decimal centerX, decimal centerY)
        {
            var grouped = new Dictionary<string, List<string>>
            {
                { "北", new List<string>() },
                { "东", new List<string>() },
                { "南", new List<string>() },
                { "西", new List<string>() }
            };
            foreach (ParcelBoundarySegmentRecord segment in segments)
            {
                List<ParcelBoundaryPointRecord> path = SegmentPoints(points,
                    segment);
                decimal x = path.Where(p => p.X.HasValue)
                    .Select(p => p.X.Value).DefaultIfEmpty(centerX).Average();
                decimal y = path.Where(p => p.Y.HasValue)
                    .Select(p => p.Y.Value).DefaultIfEmpty(centerY).Average();
                string cardinal = Cardinal(x - centerX, y - centerY);
                string text = (segment.StartPointNumber ?? string.Empty)
                    + "-" + (segment.EndPointNumber ?? string.Empty)
                    + " 至本宗地" + LineLocation(segment);
                string neighbor = Neighbor(segment);
                if (!string.IsNullOrWhiteSpace(neighbor))
                    text += "，接" + neighbor;
                grouped[cardinal].Add(text);
            }
            return grouped.ToDictionary(x => x.Key, x => x.Value.Count == 0
                ? string.Empty : string.Join("，", x.Value) + "；");
        }

        private static string LineLocation(ParcelBoundarySegmentRecord segment)
        {
            if (segment == null) return "界址点";
            string category = (segment.LineCategory ?? string.Empty).Trim();
            string position = (segment.LinePosition ?? string.Empty).Trim();
            if (category == "围墙" || category == "墙壁")
            {
                if (position == "外") return "外墙脚";
                if (position == "中") return "围墙中线";
                if (position == "内") return "内墙脚";
                return category;
            }
            if (category == "门墩") return "门墩脚";
            if (category == "道路") return "道路边线";
            if (category == "田埂") return "田埂中线";
            if (category == "沟渠") return "沟渠中线";
            if (category == "铁丝网") return "铁丝网中心线";
            if (category == "界址线") return "界址线";
            return string.IsNullOrWhiteSpace(category) ? "界址点" : category;
        }

        private static string PointLocation(
            ParcelBoundarySegmentRecord segment)
        {
            if (segment == null) return "界址点";
            string category = (segment.LineCategory ?? string.Empty).Trim();
            return category == "界址线" || string.IsNullOrWhiteSpace(category)
                ? "界址点" : LineLocation(segment);
        }

        private static string Neighbor(ParcelBoundarySegmentRecord segment)
        {
            string owner = (segment.NeighborOwner ?? string.Empty).Trim();
            string code = (segment.NeighborParcelCode ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(owner))
            {
                if (ContainsAny(owner, "道", "巷", "路", "沟", "渠",
                    "河", "塘", "山", "空地")) return owner;
                return owner + "用地";
            }
            return string.IsNullOrWhiteSpace(code)
                ? string.Empty : "相邻宗地（" + code + "）";
        }

        private static ParcelBoundarySegmentRecord FindSegment(
            ParcelBoundaryPointRecord point,
            IEnumerable<ParcelBoundarySegmentRecord> segments)
        {
            string number = point == null ? string.Empty : point.PointNumber;
            return segments.Where(x => Same(x.StartPointNumber, number)
                    || Same(x.EndPointNumber, number)
                    || MiddleNumbers(x.MiddlePointNumbers).Any(y =>
                        Same(y, number)))
                .OrderByDescending(EntityPriority)
                .FirstOrDefault();
        }

        private static int EntityPriority(ParcelBoundarySegmentRecord segment)
        {
            string category = segment == null
                ? string.Empty : (segment.LineCategory ?? string.Empty).Trim();
            if (category == "门墩") return 80;
            if (category == "围墙") return 70;
            if (category == "墙壁") return 60;
            if (category == "铁丝网") return 50;
            if (category == "道路") return 40;
            if (category == "田埂") return 30;
            if (category == "沟渠") return 20;
            if (category == "界址线"
                || string.IsNullOrWhiteSpace(category)) return 0;
            return 10;
        }

        private static List<ParcelBoundaryPointRecord> SegmentPoints(
            IList<ParcelBoundaryPointRecord> points,
            ParcelBoundarySegmentRecord segment)
        {
            var numbers = new List<string> { segment.StartPointNumber };
            numbers.AddRange(MiddleNumbers(segment.MiddlePointNumbers));
            numbers.Add(segment.EndPointNumber);
            return numbers.Select(x => FindPoint(points, x))
                .Where(x => x != null).ToList();
        }

        private static ParcelBoundaryPointRecord FindPoint(
            IEnumerable<ParcelBoundaryPointRecord> points, string number)
        {
            return points.FirstOrDefault(x => Same(x.PointNumber, number));
        }

        private static string Compass(ParcelBoundaryPointRecord point,
            decimal centerX, decimal centerY)
        {
            if (point == null || !point.X.HasValue || !point.Y.HasValue)
                return string.Empty;
            return Compass((double)(point.X.Value - centerX),
                (double)(point.Y.Value - centerY));
        }

        private static string Direction(ParcelBoundaryPointRecord start,
            ParcelBoundaryPointRecord end)
        {
            if (start == null || end == null || !start.X.HasValue
                || !start.Y.HasValue || !end.X.HasValue || !end.Y.HasValue)
                return string.Empty;
            return Compass((double)(end.X.Value - start.X.Value),
                (double)(end.Y.Value - start.Y.Value));
        }

        private static string Compass(double dx, double dy)
        {
            const double tolerance = 1e-9;
            if (Math.Abs(dx) <= tolerance && Math.Abs(dy) <= tolerance)
                return "中心";
            if (Math.Abs(dx) <= tolerance) return dy > 0 ? "北" : "南";
            if (Math.Abs(dy) <= tolerance) return dx > 0 ? "东" : "西";
            if (dx > 0) return dy > 0 ? "东北" : "东南";
            return dy > 0 ? "西北" : "西南";
        }

        private static Tuple<decimal, decimal> GeometricCenter(
            IList<ParcelBoundaryPointRecord> points)
        {
            List<ParcelBoundaryPointRecord> valid = (points
                ?? new List<ParcelBoundaryPointRecord>())
                .Where(x => x != null && x.X.HasValue && x.Y.HasValue)
                .ToList();
            if (valid.Count == 0) return Tuple.Create(0m, 0m);
            if (valid.Count < 3)
                return Tuple.Create(valid.Average(x => x.X.Value),
                    valid.Average(x => x.Y.Value));

            decimal twiceArea = 0m;
            decimal weightedX = 0m;
            decimal weightedY = 0m;
            for (int i = 0; i < valid.Count; i++)
            {
                ParcelBoundaryPointRecord current = valid[i];
                ParcelBoundaryPointRecord next = valid[(i + 1) % valid.Count];
                decimal cross = current.X.Value * next.Y.Value
                    - next.X.Value * current.Y.Value;
                twiceArea += cross;
                weightedX += (current.X.Value + next.X.Value) * cross;
                weightedY += (current.Y.Value + next.Y.Value) * cross;
            }
            if (Math.Abs(twiceArea) <= 0.000000000001m)
                return Tuple.Create(valid.Average(x => x.X.Value),
                    valid.Average(x => x.Y.Value));
            return Tuple.Create(weightedX / (3m * twiceArea),
                weightedY / (3m * twiceArea));
        }

        private static string Cardinal(decimal dx, decimal dy)
        {
            if (Math.Abs(dx) >= Math.Abs(dy)) return dx >= 0 ? "东" : "西";
            return dy >= 0 ? "北" : "南";
        }

        private static IEnumerable<string> MiddleNumbers(string value)
        {
            return (value ?? string.Empty).Split(new[] { '、', ',', '，',
                ';', '；', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim());
        }

        private static string NormalizeMiddle(string value)
        {
            return string.Join("、", MiddleNumbers(value));
        }

        private static string PointRange(IList<string> points)
        {
            if (points == null || points.Count == 0) return string.Empty;
            return points.Count == 1 ? points[0]
                : points[0] + "-" + points[points.Count - 1];
        }

        private static bool Same(string left, string right)
        {
            return string.Equals((left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            return terms.Any(x => value.IndexOf(x,
                StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void Set(ParcelSurveyRecord record, string key,
            string generated, bool overwriteManual)
        {
            ParcelSurveyFieldValue value = record.Field(key);
            if (value == null || string.IsNullOrWhiteSpace(generated)) return;
            if (!overwriteManual && value.Status == ParcelFieldStatus.Manual
                && !string.IsNullOrWhiteSpace(value.TextValue)) return;
            value.TextValue = generated;
            value.Status = ParcelFieldStatus.Automatic;
            value.Confirmed = false;
        }

        private sealed class PointDescriptionGroup
        {
            public string Key { get; set; }
            public string Direction { get; set; }
            public string Location { get; set; }
            public List<string> PointNumbers { get; private set; }

            public PointDescriptionGroup()
            {
                Key = string.Empty;
                Direction = string.Empty;
                Location = string.Empty;
                PointNumbers = new List<string>();
            }
        }
    }
}
