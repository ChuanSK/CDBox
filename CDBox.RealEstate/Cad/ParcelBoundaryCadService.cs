using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.UI;
using CDBox.Shared.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using WorldDraw = Autodesk.AutoCAD.GraphicsInterface.WorldDraw;

namespace CDBox.RealEstate.Cad
{
    public sealed class ParcelBoundaryCadService
    {
        private readonly ICDBoxPromptService _prompts;
        private readonly ICDBoxNotificationService _notifications;
        private readonly ICDBoxLogger _logger;

        public ParcelBoundaryCadService(ICDBoxPromptService prompts,
            ICDBoxNotificationService notifications, ICDBoxLogger logger)
        {
            _prompts = prompts ?? throw new ArgumentNullException("prompts");
            _notifications = notifications
                ?? throw new ArgumentNullException("notifications");
            _logger = logger ?? throw new ArgumentNullException("logger");
        }

        public ParcelBoundaryCadSelection SelectOwnershipBoundary()
        {
            return SelectOwnershipBoundary(string.Empty);
        }

        public ParcelBoundaryCadSelection SelectOwnershipBoundary(
            string currentOwnerName)
        {
            Document document = CurrentDocument();
            if (document == null) return null;
            try
            {
                var options = new PromptEntityOptions("\n ");
                options.SetRejectMessage("\n请选择闭合二维多段线。\n");
                options.AddAllowedClass(typeof(Polyline), false);
                options.AddAllowedClass(typeof(Polyline2d), false);
                PromptEntityResult selected;
                using (_prompts.Begin("选择权属线",
                    "请选择闭合的宗地权属线；按 Esc 取消。"))
                    selected = document.Editor.GetEntity(options);
                if (selected.Status != PromptStatus.OK) return null;

                ParcelBoundaryCadSelection result;
                ObjectId textStyleId;
                using (Transaction transaction = document.Database
                    .TransactionManager.StartOpenCloseTransaction())
                {
                    Entity entity = transaction.GetObject(selected.ObjectId,
                        OpenMode.ForRead, false) as Entity;
                    Polyline polyline = entity as Polyline;
                    Polyline2d legacy = entity as Polyline2d;
                    if (polyline == null && legacy == null)
                        throw new InvalidOperationException(
                            "请选择二维多段线或 CASS 旧式二维多段线作为权属线。");
                    bool closed = polyline != null
                        ? polyline.Closed : legacy.Closed;
                    if (!closed)
                        throw new InvalidOperationException(
                            "请选择已闭合的二维多段线作为权属线。");
                    result = polyline != null
                        ? ReadPolyline(polyline)
                        : ReadPolyline(legacy, transaction);
                    if (result.Vertices.Count < 3)
                        throw new InvalidOperationException(
                            "权属线至少需要三个界址节点。");
                    try { textStyleId = document.Database.Textstyle; }
                    catch { textStyleId = ObjectId.Null; }
                    transaction.Commit();
                }

                string ownerName;
                if (!ParcelBoundaryCadDialogs.TryGetOwnerName(
                    currentOwnerName, out ownerName)) return null;
                result.OwnerName = ownerName;

                var candidates = result.Vertices.Select((vertex, index) =>
                    new PointCandidate(index, "节点 " + (index + 1),
                        ToPoint(vertex))).ToList();
                PointCandidate start = SelectPoint(document, candidates,
                    textStyleId, "选取界址起点",
                    "移动鼠标选择界址起点；预览引线自动吸附权属线节点，单击确认。");
                if (start == null) return null;
                result.SelectedStartSourceIndex = start.Index;
                string prefix;
                int startNumber;
                if (!ParcelBoundaryCadDialogs.TryGetStartingPoint(out prefix,
                    out startNumber)) return null;
                bool clockwise;
                if (!ParcelBoundaryCadDialogs.TryGetNumberingDirection(
                    result.NativeClockwise, out clockwise)) return null;
                result.StartPointPrefix = prefix;
                result.StartPointNumber = startNumber;
                result.ConfiguredClockwise = clockwise;
                Notify("已识别权属线 " + result.Vertices.Count
                    + " 个节点并完成界址点编号。",
                    CDBoxNotificationLevel.Success);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error("识别宗地权属线失败。", ex);
                Notify("识别宗地权属线失败：" + ex.Message,
                    CDBoxNotificationLevel.Error);
                return null;
            }
        }

        public ParcelBoundaryRangeSelection SelectBoundaryRange(
            ParcelSurveyRecord record, bool forSignature)
        {
            Document document = CurrentDocument();
            if (document == null) return null;
            try
            {
                record = record ?? new ParcelSurveyRecord();
                record.Normalize();
                List<ParcelBoundaryPointRecord> points = record.Boundary.Points
                    .Where(x => x != null && x.X.HasValue && x.Y.HasValue
                        && !string.IsNullOrWhiteSpace(x.PointNumber)).ToList();
                if (points.Count < 2)
                    throw new InvalidOperationException(
                        "请先通过权属线识别至少两个有效界址点。");

                ObjectId textStyleId;
                using (Transaction transaction = document.Database
                    .TransactionManager.StartOpenCloseTransaction())
                {
                    try { textStyleId = document.Database.Textstyle; }
                    catch { textStyleId = ObjectId.Null; }
                    transaction.Commit();
                }
                var candidates = points.Select((point, index) =>
                    new PointCandidate(index, point.PointNumber,
                        new Point3d((double)point.X.Value,
                            (double)point.Y.Value, 0))).ToList();
                string subject = forSignature ? "签章界址线" : "界址段";
                PointCandidate start = SelectPoint(document, candidates,
                    textStyleId, "选择" + subject + "起点",
                    "移动鼠标选择" + subject
                    + "起点；引线仅吸附已识别界址点并预览点号。");
                if (start == null) return null;
                List<PointCandidate> endCandidates = candidates.Where(x =>
                    x.Index != start.Index).ToList();
                PointCandidate end = SelectPoint(document, endCandidates,
                    textStyleId, "选择" + subject + "终点",
                    "移动鼠标选择" + subject
                    + "终点；引线仅吸附已识别界址点并预览点号。");
                if (end == null) return null;

                ParcelBoundaryRangeSelection range = BuildRange(points,
                    start.Index, end.Index);
                Notify("已选择 " + range.StartPointNumber + " 至 "
                    + range.EndPointNumber + "，请在弹出窗口填写"
                    + subject + "信息。", CDBoxNotificationLevel.Success);
                return range;
            }
            catch (Exception ex)
            {
                _logger.Error("选择界址范围失败。", ex);
                Notify("选择界址范围失败：" + ex.Message,
                    CDBoxNotificationLevel.Error);
                return null;
            }
        }

        public IList<ParcelBoundarySegmentRecord>
            SelectBoundarySegmentsContinuously(ParcelSurveyRecord record)
        {
            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            int applied = 0;
            while (true)
            {
                ParcelBoundaryRangeSelection range = SelectBoundaryRange(
                    record, false);
                if (range == null) break;
                ParcelBoundarySegmentRecord existing = record.Boundary.Segments
                    .FirstOrDefault(x => SameRange(x.StartPointNumber,
                        x.EndPointNumber, range.StartPointNumber,
                        range.EndPointNumber));
                ParcelBoundarySegmentRecord segment =
                    ParcelBoundaryCadDialogs.EditSegment(range, existing);
                if (segment == null) break;
                record.Boundary.Segments.RemoveAll(x => SameRange(
                    x.StartPointNumber, x.EndPointNumber,
                    segment.StartPointNumber, segment.EndPointNumber));
                record.Boundary.Segments.Add(segment);
                SortSegments(record);
                applied++;
            }
            if (applied > 0)
                Notify("本轮已填入 " + applied
                    + " 个界址段；连续选择已结束。",
                    CDBoxNotificationLevel.Success);
            return record.Boundary.Segments;
        }

        public ParcelBoundarySignatureGroupRecord SelectSignatureGroup(
            ParcelSurveyRecord record)
        {
            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            ParcelBoundaryRangeSelection range = SelectBoundaryRange(record,
                true);
            if (range == null) return null;
            ParcelBoundarySignatureGroupRecord existing = record.Boundary
                .SignatureGroups.FirstOrDefault(x => SameRange(
                    x.StartPointNumber, x.EndPointNumber,
                    range.StartPointNumber, range.EndPointNumber));
            ParcelBoundarySegmentRecord segment = record.Boundary.Segments
                .FirstOrDefault(x => SameRange(x.StartPointNumber,
                    x.EndPointNumber, range.StartPointNumber,
                    range.EndPointNumber));
            string representative = record.Field("rights.ownerName")
                .TextValue;
            return ParcelBoundaryCadDialogs.EditSignature(range, existing,
                segment, representative);
        }

        public static ParcelBoundaryRangeSelection BuildRange(
            IList<ParcelBoundaryPointRecord> points, int startIndex,
            int endIndex)
        {
            if (points == null || points.Count < 2)
                throw new ArgumentException("界址点不足。", "points");
            if (startIndex < 0 || startIndex >= points.Count
                || endIndex < 0 || endIndex >= points.Count
                || startIndex == endIndex)
                throw new ArgumentOutOfRangeException("startIndex",
                    "界址段起终点无效。");

            var middle = new List<string>();
            decimal distance = 0m;
            int current = startIndex;
            int guard = 0;
            while (current != endIndex && guard++ <= points.Count)
            {
                int next = (current + 1) % points.Count;
                ParcelBoundaryPointRecord point = points[current];
                ParcelBoundaryPointRecord nextPoint = points[next];
                distance += point.DistanceToNext.HasValue
                    && point.DistanceToNext.Value > 0
                    ? point.DistanceToNext.Value
                    : Distance(point, nextPoint);
                current = next;
                if (current != endIndex)
                    middle.Add(points[current].PointNumber ?? string.Empty);
            }
            if (current != endIndex)
                throw new InvalidOperationException("无法沿编号方向形成界址范围。");

            ParcelBoundaryPointRecord start = points[startIndex];
            ParcelBoundaryPointRecord end = points[endIndex];
            return new ParcelBoundaryRangeSelection
            {
                StartPointNumber = start.PointNumber ?? string.Empty,
                MiddlePointNumbers = string.Join("、",
                    middle.Where(x => !string.IsNullOrWhiteSpace(x))),
                EndPointNumber = end.PointNumber ?? string.Empty,
                Distance = decimal.Round(distance, 2,
                    MidpointRounding.AwayFromZero),
                Direction = Direction(start, end)
            };
        }

        private static ParcelBoundaryCadSelection ReadPolyline(Polyline polyline)
        {
            var vertices = new List<BoundaryVertexSeed>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                vertices.Add(new BoundaryVertexSeed(polyline.GetPoint3dAt(i),
                    SafeBulge(polyline, i)));
            }
            return BuildSelection(polyline, vertices, SafeArea(polyline));
        }

        private static ParcelBoundaryCadSelection ReadPolyline(
            Polyline2d polyline, Transaction transaction)
        {
            var vertices = new List<BoundaryVertexSeed>();
            foreach (ObjectId vertexId in polyline)
            {
                Vertex2d vertex = transaction.GetObject(vertexId,
                    OpenMode.ForRead, false) as Vertex2d;
                if (vertex == null) continue;
                vertices.Add(new BoundaryVertexSeed(vertex.Position,
                    vertex.Bulge));
            }
            return BuildSelection(polyline, vertices, SafeArea(polyline));
        }

        private static ParcelBoundaryCadSelection BuildSelection(Entity source,
            IList<BoundaryVertexSeed> rawVertices, double sourceArea)
        {
            var vertices = new List<BoundaryVertexSeed>();
            foreach (BoundaryVertexSeed vertex in rawVertices
                ?? new BoundaryVertexSeed[0])
            {
                if (vertex == null) continue;
                if (vertices.Count > 0 && SamePoint(
                    vertices[vertices.Count - 1].Point, vertex.Point))
                    continue;
                vertices.Add(vertex);
            }
            if (vertices.Count > 2 && SamePoint(vertices[0].Point,
                vertices[vertices.Count - 1].Point))
                vertices.RemoveAt(vertices.Count - 1);

            var result = new ParcelBoundaryCadSelection
            {
                SourceObjectHandle = source.Handle.ToString(),
                SourceLayerName = source.Layer ?? string.Empty,
                Area = decimal.Round((decimal)Math.Abs(sourceArea), 2,
                    MidpointRounding.AwayFromZero)
            };
            double signedArea = 0;
            int count = vertices.Count;
            for (int i = 0; i < count; i++)
            {
                BoundaryVertexSeed vertex = vertices[i];
                Point3d point = vertex.Point;
                Point3d next = vertices[(i + 1) % count].Point;
                signedArea += point.X * next.Y - next.X * point.Y;
                double length = SegmentLength(point, next, vertex.Bulge);
                result.Vertices.Add(new ParcelBoundaryCadVertex
                {
                    SourceIndex = i,
                    X = ToDecimal(point.X),
                    Y = ToDecimal(point.Y),
                    Z = ToDecimal(point.Z),
                    DistanceToNext = ToDecimal(Math.Abs(length))
                });
            }
            if (result.Area <= 0 && Math.Abs(signedArea) > 0)
                result.Area = decimal.Round((decimal)(Math.Abs(signedArea)
                    / 2.0), 2, MidpointRounding.AwayFromZero);
            result.NativeClockwise = signedArea < 0;
            return result;
        }

        private static double SafeArea(Curve curve)
        {
            try { return curve == null ? 0 : curve.Area; }
            catch { return 0; }
        }

        private static double SafeBulge(Polyline polyline, int index)
        {
            try { return polyline.GetBulgeAt(index); }
            catch { return 0; }
        }

        private static double SegmentLength(Point3d start, Point3d end,
            double bulge)
        {
            double chord = start.DistanceTo(end);
            double absolute = Math.Abs(bulge);
            if (chord <= 1e-9 || absolute <= 1e-9) return chord;
            double angle = 4.0 * Math.Atan(absolute);
            double radius = chord * (1.0 + absolute * absolute)
                / (4.0 * absolute);
            return Math.Abs(radius * angle);
        }

        private static bool SamePoint(Point3d left, Point3d right)
        {
            return left.DistanceTo(right) <= 0.000001;
        }

        private PointCandidate SelectPoint(Document document,
            List<PointCandidate> candidates, ObjectId textStyleId,
            string title, string message)
        {
            if (candidates == null || candidates.Count == 0) return null;
            var jig = new BoundaryPointSelectJig(candidates, textStyleId);
            PromptResult result;
            using (_prompts.Begin(title, message))
                result = document.Editor.Drag(jig);
            return result.Status == PromptStatus.OK
                ? jig.SelectedCandidate : null;
        }

        private Document CurrentDocument()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null)
                Notify("当前没有可用的 CAD 图纸。",
                    CDBoxNotificationLevel.Warning);
            return document;
        }

        private void Notify(string message, CDBoxNotificationLevel level)
        {
            _notifications.Show("界址调查", message, level);
        }

        private static Point3d ToPoint(ParcelBoundaryCadVertex vertex)
        {
            return new Point3d((double)vertex.X, (double)vertex.Y,
                (double)vertex.Z);
        }

        private static decimal ToDecimal(double value)
        {
            return decimal.Round((decimal)value, 6,
                MidpointRounding.AwayFromZero);
        }

        private static decimal Distance(ParcelBoundaryPointRecord left,
            ParcelBoundaryPointRecord right)
        {
            if (left == null || right == null || !left.X.HasValue
                || !left.Y.HasValue || !right.X.HasValue || !right.Y.HasValue)
                return 0m;
            decimal dx = right.X.Value - left.X.Value;
            decimal dy = right.Y.Value - left.Y.Value;
            return (decimal)Math.Sqrt((double)(dx * dx + dy * dy));
        }

        private static bool SameRange(string leftStart, string leftEnd,
            string rightStart, string rightEnd)
        {
            return string.Equals((leftStart ?? string.Empty).Trim(),
                    (rightStart ?? string.Empty).Trim(),
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals((leftEnd ?? string.Empty).Trim(),
                    (rightEnd ?? string.Empty).Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void SortSegments(ParcelSurveyRecord record)
        {
            var order = record.Boundary.Points
                .Select((point, index) => new { point.PointNumber, index })
                .Where(x => !string.IsNullOrWhiteSpace(x.PointNumber))
                .ToDictionary(x => x.PointNumber, x => x.index,
                    StringComparer.OrdinalIgnoreCase);
            record.Boundary.Segments.Sort((left, right) =>
            {
                int leftIndex;
                int rightIndex;
                if (!order.TryGetValue(left.StartPointNumber ?? string.Empty,
                    out leftIndex)) leftIndex = int.MaxValue;
                if (!order.TryGetValue(right.StartPointNumber ?? string.Empty,
                    out rightIndex)) rightIndex = int.MaxValue;
                return leftIndex.CompareTo(rightIndex);
            });
        }

        private static string Direction(ParcelBoundaryPointRecord start,
            ParcelBoundaryPointRecord end)
        {
            double dx = (double)((end.X ?? 0m) - (start.X ?? 0m));
            double dy = (double)((end.Y ?? 0m) - (start.Y ?? 0m));
            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            if (angle < 0) angle += 360;
            string[] names = { "东", "东北", "北", "西北",
                "西", "西南", "南", "东南" };
            int index = (int)Math.Round(angle / 45.0,
                MidpointRounding.AwayFromZero) % 8;
            return names[index];
        }

        private sealed class PointCandidate
        {
            public PointCandidate(int index, string label, Point3d position)
            {
                Index = index;
                Label = label ?? string.Empty;
                Position = position;
            }
            public int Index { get; private set; }
            public string Label { get; private set; }
            public Point3d Position { get; private set; }
        }

        private sealed class BoundaryVertexSeed
        {
            public BoundaryVertexSeed(Point3d point, double bulge)
            {
                Point = point;
                Bulge = bulge;
            }

            public Point3d Point { get; private set; }
            public double Bulge { get; private set; }
        }

        private sealed class BoundaryPointSelectJig : DrawJig
        {
            private const double DuplicateTolerance = 0.001;
            private readonly List<PointCandidate> _candidates;
            private readonly ObjectId _textStyleId;
            private Point3d _pickPoint;
            private bool _sampled;

            public BoundaryPointSelectJig(List<PointCandidate> candidates,
                ObjectId textStyleId)
            {
                _candidates = candidates ?? new List<PointCandidate>();
                _textStyleId = textStyleId;
                _pickPoint = _candidates.Count > 0
                    ? _candidates[0].Position : Point3d.Origin;
                SelectedCandidate = FindNearest(_candidates, _pickPoint);
            }

            public PointCandidate SelectedCandidate { get; private set; }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var options = new JigPromptPointOptions("\n ")
                {
                    UserInputControls = UserInputControls.Accept3dCoordinates
                        | UserInputControls.NoZeroResponseAccepted
                };
                PromptPointResult result = prompts.AcquirePoint(options);
                if (result.Status != PromptStatus.OK)
                    return SamplerStatus.Cancel;
                if (_sampled && result.Value.DistanceTo(_pickPoint)
                    < DuplicateTolerance) return SamplerStatus.NoChange;
                _sampled = true;
                _pickPoint = result.Value;
                SelectedCandidate = FindNearest(_candidates, _pickPoint);
                return SamplerStatus.OK;
            }

            protected override bool WorldDraw(WorldDraw draw)
            {
                if (draw == null || draw.Geometry == null) return true;
                SelectedCandidate = FindNearest(_candidates, _pickPoint);
                if (SelectedCandidate == null) return true;
                using (var leader = new Polyline())
                {
                    leader.AddVertexAt(0, new Point2d(
                        SelectedCandidate.Position.X,
                        SelectedCandidate.Position.Y), 0, 0, 0);
                    leader.AddVertexAt(1, new Point2d(_pickPoint.X,
                        _pickPoint.Y), 0, 0, 0);
                    leader.ColorIndex = 1;
                    draw.Geometry.Draw(leader);
                }
                DrawPreviewText(draw, _pickPoint, SelectedCandidate.Label,
                    _textStyleId);
                return true;
            }

            private static PointCandidate FindNearest(
                IEnumerable<PointCandidate> candidates, Point3d point)
            {
                PointCandidate nearest = null;
                double distance = double.MaxValue;
                foreach (PointCandidate candidate in candidates)
                {
                    double current = candidate.Position.DistanceTo(point);
                    if (current >= distance) continue;
                    distance = current;
                    nearest = candidate;
                }
                return nearest;
            }

            private static void DrawPreviewText(WorldDraw draw,
                Point3d position, string text, ObjectId textStyleId)
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                try
                {
                    using (var preview = new DBText
                    {
                        Height = 1.0,
                        TextString = text,
                        ColorIndex = 1,
                        HorizontalMode = TextHorizontalMode.TextCenter,
                        VerticalMode = TextVerticalMode.TextVerticalMid,
                        Position = position,
                        AlignmentPoint = position
                    })
                    {
                        if (!textStyleId.IsNull)
                            preview.TextStyleId = textStyleId;
                        draw.Geometry.Draw(preview);
                    }
                }
                catch
                {
                    try
                    {
                        draw.Geometry.Text(position, Vector3d.ZAxis,
                            Vector3d.XAxis, 1.0, 1.0, 0.0, text);
                    }
                    catch { }
                }
            }
        }
    }
}
