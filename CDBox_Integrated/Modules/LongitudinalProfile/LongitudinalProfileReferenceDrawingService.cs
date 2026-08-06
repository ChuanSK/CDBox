using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using TCPipeAutoDraw.Core.Cad;

namespace TCPipeAutoDraw.Modules.LongitudinalProfile
{
    /// <summary>
    /// ???????.dwg??????????????
    /// ?????????????????????????????
    /// ???????????????????/?????????
    /// ? Explode ?????
    /// </summary>
    internal static class LongitudinalProfileReferenceDrawingService
    {
        internal const string HeaderLayer = "315??";
        internal const string ScaleLayer = "????????";
        internal const string GridLayer = "?????";
        internal const string GroundLayer = "?????";
        internal const string GraphLayer = "????";
        internal const string TitleLayer = "????????";
        internal const string NodeLabelLayer = "????????";
        internal const string NodeLayer = "??????";
        internal const string PipeLayer = "??????";

        private const string HzStyle = "HZ";
        private const string FangSongStyle = "???";
        private const string ArrowBlock = "??2";
        private const double MTextHeight = 1.25;
        private const double HeaderTextHeight = 2.0;
        private const double MTextLineSpacing = 0.792;
        private const double HeaderBaselineOffset = 1.06050424;
        private const double ElevationTitleXOffset = 1.577537097;
        private const double ElevationTitleYOffset = 0.63649368;
        private const double ProfileGlobalWidth = 0.15;

        public static LongitudinalProfileDrawingResult SelectPositionAndDraw(
            Document document, LongitudinalProfileData profile,
            LongitudinalProfileSettings sourceSettings)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (profile == null) throw new ArgumentNullException("profile");
            LongitudinalProfileSettings settings =
                (sourceSettings ?? new LongitudinalProfileSettings()).Clone();
            settings.Normalize();
            LongitudinalProfileLayout layout =
                LongitudinalProfileLayoutCalculator.Calculate(profile,
                    settings);
            var jig = new ReferencePlacementJig(profile, layout);
            PromptResult prompt = document.Editor.DragWithHud(jig,
                "?????????????");
            if (prompt.Status != PromptStatus.OK)
            {
                return new LongitudinalProfileDrawingResult
                {
                    Success = false,
                    Message = "?????????"
                };
            }
            return Draw(document, profile, settings, jig.Position);
        }

        public static LongitudinalProfileDrawingResult Draw(
            Document document, LongitudinalProfileData profile,
            LongitudinalProfileSettings sourceSettings,
            Point3d insertionPoint)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (profile == null) throw new ArgumentNullException("profile");
            if (profile.Nodes == null || profile.Nodes.Count < 2)
                throw new ArgumentException("?????????????",
                    "profile");

            LongitudinalProfileSettings settings =
                (sourceSettings ?? new LongitudinalProfileSettings()).Clone();
            settings.Normalize();
            LongitudinalProfileLayout layout =
                LongitudinalProfileLayoutCalculator.Calculate(profile,
                    settings);
            var result = new LongitudinalProfileDrawingResult
            {
                Success = true
            };

            Database db = document.Database;
            using (DocumentLock documentLock = document.LockDocument())
            using (Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                EnsureReferenceLayers(db, tr);
                ObjectId hz = EnsureTextStyle(db, tr, HzStyle,
                    "rs.shx", "hztxt.shx", 0.8);
                ObjectId fangSong = EnsureTextStyle(db, tr,
                    FangSongStyle, "simfang.ttf", string.Empty, 0.8);
                ObjectId arrow = EnsureArrowBlock(db, tr);

                DrawHeader(db, tr, settings, layout, insertionPoint,
                    fangSong, result);
                DrawDataTable(db, tr, profile, settings, layout,
                    insertionPoint, hz, fangSong, result);
                DrawChart(db, tr, profile, settings, layout,
                    insertionPoint, hz, fangSong, result);
                DrawProfileObjects(db, tr, profile, layout,
                    insertionPoint, result);
                DrawNodeLabels(db, tr, profile, layout, insertionPoint,
                    hz, result);
                DrawScale(db, tr, settings, layout, insertionPoint,
                    hz, arrow, result);
                DrawTitle(db, tr, layout, insertionPoint, fangSong,
                    result);
                tr.Commit();
            }

            result.Message = "?????????????";
            return result;
        }

        private static void DrawHeader(
            Database db, Transaction tr,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId textStyle, LongitudinalProfileDrawingResult result)
        {
            short byLayer = 256;
            AddLine(db, tr, At(origin, layout.HeaderLeft,
                    layout.TableBottom),
                At(origin, layout.HeaderRight, layout.TableBottom),
                HeaderLayer, byLayer, "Continuous", LineWeight.ByLayer,
                result);
            AddLine(db, tr, At(origin, layout.HeaderLeft, layout.TableTop),
                At(origin, layout.HeaderRight, layout.TableTop),
                HeaderLayer, byLayer, "Continuous", LineWeight.ByLayer,
                result);
            AddLine(db, tr, At(origin, layout.HeaderLeft,
                    layout.TableBottom),
                At(origin, layout.HeaderLeft, layout.TableTop),
                HeaderLayer, byLayer, "Continuous", LineWeight.ByLayer,
                result);
            AddLine(db, tr, At(origin, layout.HeaderRight,
                    layout.TableBottom),
                At(origin, layout.HeaderRight, layout.TableTop),
                HeaderLayer, byLayer, "Continuous", LineWeight.ByLayer,
                result);

            for (int i = 0; i < layout.Rows.Count; i++)
            {
                LongitudinalProfileRowLayout row = layout.Rows[i];
                if (row.Bottom > layout.TableBottom + 1e-9)
                    AddLine(db, tr,
                        At(origin, layout.HeaderLeft, row.Bottom),
                        At(origin, layout.HeaderRight, row.Bottom),
                        HeaderLayer, byLayer, "Continuous",
                        LineWeight.ByLayer, result);
                AddCenteredHeaderText(db, tr, origin, row.Settings.Key,
                    row.Settings.Name,
                    row.Center - HeaderBaselineOffset,
                    (layout.HeaderLeft + layout.HeaderRight) / 2.0,
                    textStyle, result);
            }
        }

        private static void DrawDataTable(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId hz, ObjectId fangSong,
            LongitudinalProfileDrawingResult result)
        {
            LongitudinalProfileRowLayout[] valueRows =
            {
                layout.Row("GroundElevation"),
                layout.Row("DesignInvertElevation"),
                layout.Row("PipeBottomDepth"),
                layout.Row("WellDepth")
            };
            foreach (LongitudinalProfileRowLayout row in valueRows)
                AddRowTopAndBottom(db, tr, origin, layout, row, result);

            DrawNodeRowTexts(db, tr, profile, settings, layout, origin,
                valueRows[0], n => n.GroundElevation.ToString(
                    "F2",
                    CultureInfo.InvariantCulture), 256, hz, result);
            DrawNodeRowTexts(db, tr, profile, settings, layout, origin,
                valueRows[1], n => n.DesignInvertElevation.ToString(
                    "F2",
                    CultureInfo.InvariantCulture), 7, hz, result);
            DrawNodeRowTexts(db, tr, profile, settings, layout, origin,
                valueRows[2], n => FormatTrimmed(n.PipeBottomDepth,
                    settings.ValueDecimals), 7, hz, result);
            DrawNodeRowTexts(db, tr, profile, settings, layout, origin,
                valueRows[3], n => FormatTrimmed(n.WellDepth,
                    settings.ValueDecimals), 7, hz, result);

            LongitudinalProfileRowLayout pipeRow =
                layout.Row("DiameterSlope");
            LongitudinalProfileRowLayout distanceRow =
                layout.Row("PlanDistance");
            AddRowTopAndBottom(db, tr, origin, layout, pipeRow, result);
            AddRowTopAndBottom(db, tr, origin, layout, distanceRow, result);
            for (int i = 0; i < profile.Spans.Count; i++)
            {
                double x1 = layout.X(
                    profile.Nodes[i].CumulativeDistance);
                double x2 = layout.X(
                    profile.Nodes[i + 1].CumulativeDistance);
                DrawCellSides(db, tr, origin, x1, x2, pipeRow, result);
                // ??????????????????????????????
                if (Math.Abs(profile.Spans[i].SlopePercent) > 1e-8)
                {
                    AddLine(db, tr, At(origin, x1, pipeRow.Top),
                        At(origin, x2, pipeRow.Bottom), GraphLayer, 256,
                        "ByLayer", LineWeight.ByLayer, result);
                }
                DrawCellSides(db, tr, origin, x1, x2, distanceRow,
                    result);

                LongitudinalProfileSpanData span = profile.Spans[i];
                double width = x2 - x1;
                AddMText(db, tr,
                    At(origin, x1 + width * 0.25, pipeRow.Center),
                    Width08(NormalizeDiameter(span.Diameter)),
                    MTextHeight, 256, hz, 0.0,
                    AttachmentPoint.MiddleCenter, GraphLayer, result, 0.6);
                AddMText(db, tr,
                    At(origin, x1 + width * 0.75, pipeRow.Center),
                    Width08("i=" + FormatTrimmed(
                        Math.Abs(span.SlopePercent),
                        settings.SlopeDecimals)),
                    MTextHeight, 256, hz, 0.0,
                    AttachmentPoint.MiddleCenter, GraphLayer, result, 0.6);
                AddMText(db, tr,
                    At(origin, (x1 + x2) / 2.0,
                        distanceRow.Center),
                    Width08("L=" + FormatTrimmed(span.PlanLength,
                        settings.ValueDecimals)),
                    MTextHeight, 256, hz, 0.0,
                    AttachmentPoint.MiddleCenter, GraphLayer, result, 0.6);
            }

            DrawFoundationRow(db, tr, profile, layout, origin,
                fangSong, result);

            LongitudinalProfileRowLayout numberRow =
                layout.Row("WellNumber");
            AddRowTopAndBottom(db, tr, origin, layout, numberRow, result);
            foreach (LongitudinalProfileNodeData node in profile.Nodes)
            {
                AddMText(db, tr,
                    At(origin, layout.X(node.CumulativeDistance),
                        numberRow.Center),
                    Width08(node.NodeNo), MTextHeight, 256, hz, 0.0,
                    AttachmentPoint.MiddleCenter, GraphLayer, result, 0.6);
            }
        }

        private static void DrawNodeRowTexts(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            LongitudinalProfileRowLayout row,
            Func<LongitudinalProfileNodeData, string> value,
            short color, ObjectId hz,
            LongitudinalProfileDrawingResult result)
        {
            foreach (LongitudinalProfileNodeData node in profile.Nodes)
            {
                AddMText(db, tr,
                    At(origin, layout.X(node.CumulativeDistance),
                        row.Center),
                    Width08(value(node)), MTextHeight, color, hz,
                    Math.PI / 2.0, AttachmentPoint.MiddleCenter,
                    GraphLayer, result, 0.6);
            }
        }

        private static void DrawFoundationRow(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId fangSong, LongitudinalProfileDrawingResult result)
        {
            LongitudinalProfileRowLayout row =
                layout.Row("PipeFoundation");
            int start = 0;
            while (start < profile.Spans.Count)
            {
                string value = Clean(profile.Spans[start].Foundation);
                int end = start + 1;
                while (end < profile.Spans.Count
                    && string.Equals(value,
                        Clean(profile.Spans[end].Foundation),
                        StringComparison.CurrentCultureIgnoreCase))
                    end++;
                double x1 = layout.X(
                    profile.Nodes[start].CumulativeDistance);
                double x2 = layout.X(
                    profile.Nodes[end].CumulativeDistance);
                AddLine(db, tr, At(origin, x1, row.Top),
                    At(origin, x2, row.Top), GraphLayer, 256,
                    "ByLayer", LineWeight.ByLayer, result);
                AddLine(db, tr, At(origin, x1, row.Bottom),
                    At(origin, x2, row.Bottom), GraphLayer, 256,
                    "ByLayer", LineWeight.ByLayer, result);
                DrawCellSides(db, tr, origin, x1, x2, row, result);
                if (value.Length > 0)
                    AddMText(db, tr,
                        At(origin, (x1 + x2) / 2.0, row.Center),
                        Height08Width08(value), MTextHeight, 7,
                        fangSong, 0.0, AttachmentPoint.MiddleCenter,
                        GraphLayer, result, 0.6);
                start = end;
            }
        }

        private static void AddRowTopAndBottom(
            Database db, Transaction tr, Point3d origin,
            LongitudinalProfileLayout layout,
            LongitudinalProfileRowLayout row,
            LongitudinalProfileDrawingResult result)
        {
            AddLine(db, tr, At(origin, layout.DataLeft, row.Top),
                At(origin, layout.DataRight, row.Top), GraphLayer, 256,
                "ByLayer", LineWeight.ByLayer, result);
            AddLine(db, tr, At(origin, layout.DataLeft, row.Bottom),
                At(origin, layout.DataRight, row.Bottom), GraphLayer, 256,
                "ByLayer", LineWeight.ByLayer, result);
        }

        private static void DrawCellSides(
            Database db, Transaction tr, Point3d origin,
            double x1, double x2, LongitudinalProfileRowLayout row,
            LongitudinalProfileDrawingResult result)
        {
            AddLine(db, tr, At(origin, x1, row.Top),
                At(origin, x1, row.Bottom), GraphLayer, 256,
                "ByLayer", LineWeight.ByLayer, result);
            AddLine(db, tr, At(origin, x2, row.Top),
                At(origin, x2, row.Bottom), GraphLayer, 256,
                "ByLayer", LineWeight.ByLayer, result);
        }

        private static void DrawChart(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId hz, ObjectId fangSong,
            LongitudinalProfileDrawingResult result)
        {
            double elevation = layout.DatumElevation;
            int guard = 0;
            while (elevation <= layout.TopElevation + 1e-8
                && guard++ < 2000)
            {
                double y = layout.Y(elevation);
                AddLine(db, tr, At(origin, layout.PlotLeft, y),
                    At(origin, layout.PlotRight, y), GridLayer, 8,
                    "ByBlock", LineWeight.ByBlock, result);
                AddMText(db, tr,
                    At(origin, layout.StaffLeft - 2.5, y),
                    elevation.ToString(
                        "F" + settings.ElevationDecimals,
                        CultureInfo.InvariantCulture),
                    MTextHeight, 256, hz, 0.0,
                    AttachmentPoint.TopRight, GraphLayer, result, 1.0);
                elevation += settings.ElevationGridInterval;
            }

            double plotDistance =
                (layout.PlotRight - layout.PlotLeft)
                / layout.HorizontalFactor;
            double distance = 0.0;
            guard = 0;
            while (distance <= plotDistance + 1e-8
                && guard++ < 10000)
            {
                double x = layout.X(distance);
                AddLine(db, tr, At(origin, x, layout.ChartBottom),
                    At(origin, x, layout.ChartTop), GridLayer, 8,
                    "ByBlock", LineWeight.ByBlock, result);
                distance += settings.HorizontalGridInterval;
            }

            DrawElevationStaff(db, tr, settings, layout, origin, result);
            AddMText(db, tr,
                At(origin,
                    layout.StaffLeft - ElevationTitleXOffset,
                    layout.ChartTop + ElevationTitleYOffset),
                "??(?)", MTextHeight, 256, fangSong, 0.0,
                AttachmentPoint.BottomLeft, GraphLayer, result, 1.0);
        }

        private static void DrawElevationStaff(
            Database db, Transaction tr,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            LongitudinalProfileDrawingResult result)
        {
            double elevation = layout.DatumElevation;
            int index = 0;
            while (elevation < layout.TopElevation - 1e-8
                && index < 2000)
            {
                double next = Math.Min(layout.TopElevation,
                    elevation + settings.ElevationGridInterval);
                double y1 = layout.Y(elevation);
                double y2 = layout.Y(next);
                AddLine(db, tr,
                    At(origin, layout.StaffLeft, y1),
                    At(origin, layout.StaffRight, y1),
                    GraphLayer, 0, "ByLayer", LineWeight.ByLayer,
                    result);
                AddLine(db, tr,
                    At(origin, layout.StaffLeft, y1),
                    At(origin, layout.StaffLeft, y2),
                    GraphLayer, 0, "ByLayer", LineWeight.ByLayer,
                    result);
                AddLine(db, tr,
                    At(origin, layout.StaffRight, y1),
                    At(origin, layout.StaffRight, y2),
                    GraphLayer, 0, "ByLayer", LineWeight.ByLayer,
                    result);
                if (index % 2 == 0)
                    AddTwoPointPolyline(db, tr,
                        At(origin,
                            (layout.StaffLeft + layout.StaffRight) / 2.0,
                            y1),
                        At(origin,
                            (layout.StaffLeft + layout.StaffRight) / 2.0,
                            y2),
                        GraphLayer, 0,
                        layout.StaffRight - layout.StaffLeft, result);
                elevation = next;
                index++;
            }
            AddLine(db, tr,
                At(origin, layout.StaffLeft, layout.ChartTop),
                At(origin, layout.StaffRight, layout.ChartTop),
                GraphLayer, 0, "ByLayer", LineWeight.ByLayer,
                result);
        }

        private static void DrawProfileObjects(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileLayout layout, Point3d origin,
            LongitudinalProfileDrawingResult result)
        {
            for (int i = 0; i < profile.Spans.Count; i++)
            {
                LongitudinalProfileNodeData first = profile.Nodes[i];
                LongitudinalProfileNodeData second = profile.Nodes[i + 1];
                LongitudinalProfileSpanData span = profile.Spans[i];
                double x1 = layout.X(first.CumulativeDistance);
                double x2 = layout.X(second.CumulativeDistance);
                double firstInvert = layout.Y(
                    first.DesignInvertElevation);
                double secondInvert = layout.Y(
                    second.DesignInvertElevation);
                double pipeHeight = Math.Max(0.01,
                    span.OuterDiameter) * layout.VerticalFactor;

                AddTwoPointPolyline(db, tr,
                    At(origin, x1, layout.Y(first.GroundElevation)),
                    At(origin, x2, layout.Y(second.GroundElevation)),
                    GroundLayer, 256, 0.0, result);
                AddTwoPointPolyline(db, tr,
                    At(origin, x1 + 0.5, firstInvert + pipeHeight),
                    At(origin, x2 - 0.5, secondInvert + pipeHeight),
                    PipeLayer, 3, ProfileGlobalWidth, "ByLayer",
                    LineWeight.LineWeight030, result);
                AddTwoPointPolyline(db, tr,
                    At(origin, x1 + 0.5, firstInvert),
                    At(origin, x2 - 0.5, secondInvert),
                    PipeLayer, 3, ProfileGlobalWidth, "ByLayer",
                    LineWeight.LineWeight030, result);
            }

            for (int i = 0; i < profile.Nodes.Count; i++)
                DrawProfileNode(db, tr, profile, layout, origin, i,
                    result);
        }

        private static void DrawProfileNode(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileLayout layout, Point3d origin, int index,
            LongitudinalProfileDrawingResult result)
        {
            LongitudinalProfileNodeData node = profile.Nodes[index];
            LongitudinalProfileSpanData incoming = index > 0
                ? profile.Spans[index - 1] : null;
            LongitudinalProfileSpanData outgoing =
                index < profile.Spans.Count ? profile.Spans[index] : null;
            double x = layout.X(node.CumulativeDistance);
            double groundY = layout.Y(node.GroundElevation);
            double invertY = layout.Y(node.DesignInvertElevation);
            double bottomY = layout.Y(
                node.GroundElevation - node.WellDepth);

            AddTwoPointPolyline(db, tr, At(origin, x, groundY),
                At(origin, x, layout.ChartBottom), NodeLayer, 2, 0.0,
                "X9", LineWeight.ByLineWeightDefault, result);
            DrawOpenWellOutline(db, tr, layout, origin, result, x,
                groundY, invertY, bottomY, incoming, outgoing);
        }

        private static void DrawOpenWellOutline(
            Database db, Transaction tr,
            LongitudinalProfileLayout layout, Point3d origin,
            LongitudinalProfileDrawingResult result, double x,
            double groundY, double invertY, double bottomY,
            LongitudinalProfileSpanData incoming,
            LongitudinalProfileSpanData outgoing)
        {
            double left = x - 0.5;
            double right = x + 0.5;
            double leftBottom;
            double leftTop;
            double rightBottom;
            double rightTop;
            bool openLeft = TryGetWellOpening(incoming, layout,
                invertY, bottomY, groundY, out leftBottom, out leftTop);
            bool openRight = TryGetWellOpening(outgoing, layout,
                invertY, bottomY, groundY, out rightBottom, out rightTop);

            if (openLeft && openRight)
            {
                AddWellOutlinePolyline(db, tr, origin, result, new[]
                {
                    new Point2d(left, leftTop),
                    new Point2d(left, groundY),
                    new Point2d(right, groundY),
                    new Point2d(right, rightTop)
                }, false);
                AddWellOutlinePolyline(db, tr, origin, result, new[]
                {
                    new Point2d(right, rightBottom),
                    new Point2d(right, bottomY),
                    new Point2d(left, bottomY),
                    new Point2d(left, leftBottom)
                }, false);
                return;
            }

            if (openLeft)
            {
                AddWellOutlinePolyline(db, tr, origin, result, new[]
                {
                    new Point2d(left, leftTop),
                    new Point2d(left, groundY),
                    new Point2d(right, groundY),
                    new Point2d(right, bottomY),
                    new Point2d(left, bottomY),
                    new Point2d(left, leftBottom)
                }, false);
                return;
            }

            if (openRight)
            {
                AddWellOutlinePolyline(db, tr, origin, result, new[]
                {
                    new Point2d(right, rightTop),
                    new Point2d(right, groundY),
                    new Point2d(left, groundY),
                    new Point2d(left, bottomY),
                    new Point2d(right, bottomY),
                    new Point2d(right, rightBottom)
                }, false);
                return;
            }

            AddWellOutlinePolyline(db, tr, origin, result, new[]
            {
                new Point2d(left, groundY),
                new Point2d(right, groundY),
                new Point2d(right, bottomY),
                new Point2d(left, bottomY)
            }, true);
        }

        private static bool TryGetWellOpening(
            LongitudinalProfileSpanData span,
            LongitudinalProfileLayout layout, double invertY,
            double bottomY, double groundY, out double openingBottom,
            out double openingTop)
        {
            openingBottom = invertY;
            openingTop = invertY;
            if (span == null) return false;
            openingBottom = Math.Max(bottomY, invertY);
            openingTop = Math.Min(groundY, invertY + Math.Max(0.01,
                span.OuterDiameter) * layout.VerticalFactor);
            return openingTop > openingBottom + 1e-8;
        }

        private static void AddWellOutlinePolyline(
            Database db, Transaction tr, Point3d origin,
            LongitudinalProfileDrawingResult result,
            IList<Point2d> points, bool closed)
        {
            var worldPoints = new List<Point3d>();
            foreach (Point2d point in points)
                worldPoints.Add(At(origin, point.X, point.Y));
            AddPolyline(db, tr, worldPoints, closed, NodeLayer, 256,
                ProfileGlobalWidth, "ByLayer", LineWeight.LineWeight030,
                result);
        }

        private static void DrawNodeLabels(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId hz, LongitudinalProfileDrawingResult result)
        {
            double labelY = layout.ChartTop + 5.0;
            foreach (LongitudinalProfileNodeData node in profile.Nodes)
            {
                double x = layout.X(node.CumulativeDistance);
                double groundY = layout.Y(node.GroundElevation);
                MText text = AddMText(db, tr,
                    At(origin, x + 0.5, labelY + 0.4),
                    Width08(node.NodeNo), MTextHeight, 7, hz, 0.0,
                    AttachmentPoint.BottomLeft, NodeLabelLayer, result);
                double endX = x + ReferenceNodeLabelShelfLength(
                    Clean(node.NodeNo), text);
                AddLine(db, tr, At(origin, x, groundY),
                    At(origin, x, labelY), NodeLabelLayer, 256,
                    "ByLayer", LineWeight.ByLayer, result);
                AddLine(db, tr, At(origin, x, labelY),
                    At(origin, endX, labelY), NodeLabelLayer, 256,
                    "ByLayer", LineWeight.ByLayer, result);
            }
        }

        private static void DrawScale(
            Database db, Transaction tr,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId hz, ObjectId arrow,
            LongitudinalProfileDrawingResult result)
        {
            double baseY = layout.TableTop + 2.5;
            AddBlockReference(db, tr, arrow,
                At(origin, 3.75, baseY), 0.5, 0.0,
                ScaleLayer, 6, result);
            AddBlockReference(db, tr, arrow,
                At(origin, 0.0, baseY + 3.75), 0.5,
                Math.PI / 2.0, ScaleLayer, 6, result);
            AddMText(db, tr, At(origin, 1.65, baseY + 0.4),
                "{\\H0.8x;\\W0.8;?}{\\W0.8; 1 : "
                    + settings.HorizontalScale.ToString("0",
                        CultureInfo.InvariantCulture) + "}",
                MTextHeight, 4, hz, 0.0,
                AttachmentPoint.BottomLeft, ScaleLayer, result);
            AddMText(db, tr, At(origin, 0.4, baseY + 1.65),
                "{\\H0.8x;\\W0.8;?}{\\W0.8; 1 : "
                    + settings.VerticalScale.ToString("0",
                        CultureInfo.InvariantCulture) + "}",
                MTextHeight, 4, hz, Math.PI / 2.0,
                AttachmentPoint.TopLeft, ScaleLayer, result);
        }

        private static void DrawTitle(
            Database db, Transaction tr,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId fangSong, LongitudinalProfileDrawingResult result)
        {
            double centerX = (layout.DataLeft + layout.DataRight) / 2.0;
            MText title = AddMText(db, tr,
                At(origin, centerX, -2.5),
                Height08Width08("???????"), 1.0, 5,
                fangSong, 0.0, AttachmentPoint.TopCenter, TitleLayer,
                result);
            // ???????????????????? MText ?????
            double left = centerX - 2.6929134;
            double right = centerX + 2.6929134;
            AddLine(db, tr, At(origin, left, -3.9),
                At(origin, right, -3.9), TitleLayer, 7, "ByLayer",
                LineWeight.LineWeight060, result);
            AddLine(db, tr, At(origin, left, -4.4),
                At(origin, right, -4.4), TitleLayer, 7, "ByLayer",
                LineWeight.ByLineWeightDefault, result);
        }

        private static void AddCenteredHeaderText(
            Database db, Transaction tr, Point3d origin, string key,
            string value,
            double baselineY, double centerX, ObjectId style,
            LongitudinalProfileDrawingResult result)
        {
            var text = new DBText();
            text.SetDatabaseDefaults(db);
            text.TextString = Clean(value);
            text.Height = HeaderTextHeight;
            text.WidthFactor = 0.8;
            text.Position = At(origin, 0.0, baselineY);
            text.HorizontalMode = TextHorizontalMode.TextLeft;
            text.VerticalMode = TextVerticalMode.TextBase;
            text.Rotation = 0.0;
            text.Layer = HeaderLayer;
            text.ColorIndex = 7;
            text.Linetype = "Continuous";
            text.LineWeight = LineWeight.ByLayer;
            if (!style.IsNull) text.TextStyleId = style;
            Append(db, tr, text, result);
            double referenceLeft;
            if (TryGetReferenceHeaderLeft(key, out referenceLeft))
            {
                text.Position = At(origin,
                    referenceLeft + centerX - 11.25, baselineY);
                return;
            }
            try
            {
                Extents3d extents = text.GeometricExtents;
                double currentCenter =
                    (extents.MinPoint.X + extents.MaxPoint.X) / 2.0;
                double targetCenter = origin.X + centerX;
                text.TransformBy(Matrix3d.Displacement(
                    new Vector3d(targetCenter - currentCenter, 0.0, 0.0)));
            }
            catch
            {
                text.Position = At(origin, centerX, baselineY);
                text.HorizontalMode = TextHorizontalMode.TextCenter;
                text.AlignmentPoint = text.Position;
                try { text.AdjustAlignment(db); } catch { }
            }
        }

        private static MText AddMText(
            Database db, Transaction tr, Point3d location,
            string contents, double height, short color,
            ObjectId style, double rotation, AttachmentPoint attachment,
            string layer, LongitudinalProfileDrawingResult result,
            double lineSpacing = MTextLineSpacing)
        {
            var text = new MText();
            text.SetDatabaseDefaults(db);
            text.Contents = contents ?? string.Empty;
            text.TextHeight = height;
            text.Location = location;
            text.Rotation = rotation;
            text.Attachment = attachment;
            text.LineSpacingFactor = lineSpacing;
            text.Width = 0.0;
            text.Layer = layer;
            text.ColorIndex = color;
            text.Linetype = "ByLayer";
            text.LineWeight = LineWeight.ByLayer;
            if (!style.IsNull) text.TextStyleId = style;
            Append(db, tr, text, result);
            return text;
        }

        private static void AddLine(
            Database db, Transaction tr, Point3d start, Point3d end,
            string layer, short color, string lineType,
            LineWeight lineWeight,
            LongitudinalProfileDrawingResult result)
        {
            var line = new Line(start, end)
            {
                Layer = layer,
                ColorIndex = color,
                LineWeight = lineWeight
            };
            if (!string.IsNullOrWhiteSpace(lineType))
            {
                try { line.Linetype = lineType; } catch { }
            }
            Append(db, tr, line, result);
        }

        private static void AddTwoPointPolyline(
            Database db, Transaction tr, Point3d first, Point3d second,
            string layer, short color, double constantWidth,
            LongitudinalProfileDrawingResult result)
        {
            AddTwoPointPolyline(db, tr, first, second, layer, color,
                constantWidth, "ByLayer", LineWeight.ByLayer, result);
        }

        private static void AddTwoPointPolyline(
            Database db, Transaction tr, Point3d first, Point3d second,
            string layer, short color, double constantWidth,
            string lineType, LineWeight lineWeight,
            LongitudinalProfileDrawingResult result)
        {
            AddPolyline(db, tr, new[] { first, second }, false, layer,
                color, constantWidth, lineType, lineWeight, result);
        }

        private static void AddPolyline(
            Database db, Transaction tr, IList<Point3d> points,
            bool closed, string layer, short color, double constantWidth,
            string lineType, LineWeight lineWeight,
            LongitudinalProfileDrawingResult result)
        {
            if (points == null || points.Count < 2) return;
            var polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline
            {
                Layer = layer,
                ColorIndex = color,
                LineWeight = lineWeight,
                Elevation = points[0].Z,
                ConstantWidth = constantWidth
            };
            if (!string.IsNullOrWhiteSpace(lineType))
            {
                try { polyline.Linetype = lineType; } catch { }
            }
            for (int i = 0; i < points.Count; i++)
            {
                Point3d point = points[i];
                polyline.AddVertexAt(i, new Point2d(point.X, point.Y),
                    0.0, constantWidth, constantWidth);
            }
            polyline.Closed = closed;
            Append(db, tr, polyline, result);
        }

        private static void AddBlockReference(
            Database db, Transaction tr, ObjectId blockId,
            Point3d position, double scale, double rotation,
            string layer, short color,
            LongitudinalProfileDrawingResult result)
        {
            var reference = new BlockReference(position, blockId)
            {
                ScaleFactors = new Scale3d(scale),
                Rotation = rotation,
                Layer = layer,
                ColorIndex = color,
                Linetype = "ByLayer",
                LineWeight = LineWeight.ByLayer
            };
            Append(db, tr, reference, result);
        }

        private static void Append(
            Database db, Transaction tr, Entity entity,
            LongitudinalProfileDrawingResult result)
        {
            BlockTableRecord space = (BlockTableRecord)tr.GetObject(
                db.CurrentSpaceId, OpenMode.ForWrite);
            space.AppendEntity(entity);
            tr.AddNewlyCreatedDBObject(entity, true);
            result.EntityCount++;
        }

        private static void EnsureReferenceLayers(
            Database db, Transaction tr)
        {
            EnsureLayer(db, tr, HeaderLayer, 255);
            EnsureLayer(db, tr, ScaleLayer, 7);
            EnsureLayer(db, tr, GridLayer, 8);
            EnsureLayer(db, tr, GroundLayer, 8);
            EnsureLayer(db, tr, GraphLayer, 7);
            EnsureLayer(db, tr, TitleLayer, 7);
            EnsureLayer(db, tr, NodeLabelLayer, 7);
            EnsureLayer(db, tr, NodeLayer, 7,
                LineWeight.LineWeight030);
            EnsureLayer(db, tr, PipeLayer, 3,
                LineWeight.LineWeight030);
            EnsureLineType(db, tr, "X9");
        }

        private static ObjectId EnsureLayer(
            Database db, Transaction tr, string name, short color)
        {
            return EnsureLayer(db, tr, name, color, null);
        }

        private static ObjectId EnsureLayer(
            Database db, Transaction tr, string name, short color,
            LineWeight? lineWeight)
        {
            ObjectId id = CadLayerService.EnsureLayer(db, tr, name,
                color);
            try
            {
                LayerTableRecord layer = (LayerTableRecord)tr.GetObject(
                    id, OpenMode.ForWrite);
                layer.Color = Color.FromColorIndex(ColorMethod.ByAci,
                    color);
                if (lineWeight.HasValue)
                    layer.LineWeight = lineWeight.Value;
            }
            catch { }
            return id;
        }

        private static void EnsureLineType(
            Database db, Transaction tr, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            string clean = name.Trim();
            LinetypeTable table = (LinetypeTable)tr.GetObject(
                db.LinetypeTableId, OpenMode.ForRead);
            if (table.Has(clean)) return;

            string[] sources = { "cass.lin", "acad.lin", "acadiso.lin" };
            foreach (string source in sources)
            {
                try { db.LoadLineTypeFile(clean, source); } catch { }
                table = (LinetypeTable)tr.GetObject(db.LinetypeTableId,
                    OpenMode.ForRead);
                if (table.Has(clean)) return;
            }

            try
            {
                table.UpgradeOpen();
                var record = new LinetypeTableRecord
                {
                    Name = clean,
                    AsciiDescription = "CDBox X9",
                    PatternLength = 1.0,
                    NumDashes = 2
                };
                record.SetDashLengthAt(0, 0.5);
                record.SetDashLengthAt(1, -0.5);
                table.Add(record);
                tr.AddNewlyCreatedDBObject(record, true);
            }
            catch { }
        }

        private static ObjectId EnsureTextStyle(
            Database db, Transaction tr, string name, string font,
            string bigFont, double xScale)
        {
            try
            {
                TextStyleTable table = (TextStyleTable)tr.GetObject(
                    db.TextStyleTableId, OpenMode.ForRead);
                if (table.Has(name)) return table[name];
                table.UpgradeOpen();
                var style = new TextStyleTableRecord
                {
                    Name = name,
                    FileName = font,
                    BigFontFileName = bigFont,
                    XScale = xScale
                };
                ObjectId id = table.Add(style);
                tr.AddNewlyCreatedDBObject(style, true);
                return id;
            }
            catch
            {
                return db.Textstyle;
            }
        }

        private static ObjectId EnsureArrowBlock(
            Database db, Transaction tr)
        {
            BlockTable table = (BlockTable)tr.GetObject(
                db.BlockTableId, OpenMode.ForRead);
            if (table.Has(ArrowBlock)) return table[ArrowBlock];
            table.UpgradeOpen();
            var block = new BlockTableRecord
            {
                Name = ArrowBlock,
                Origin = Point3d.Origin
            };
            ObjectId id = table.Add(block);
            tr.AddNewlyCreatedDBObject(block, true);
            AddBlockLine(block, tr, new Point3d(7.5, 0.0, 0.0),
                new Point3d(-7.5, 0.0, 0.0));
            AddBlockLine(block, tr, new Point3d(7.5, 0.0, 0.0),
                new Point3d(2.5, -0.5, 0.0));
            AddBlockLine(block, tr, new Point3d(7.5, 0.0, 0.0),
                new Point3d(2.5, 0.5, 0.0));
            return id;
        }

        private static void AddBlockLine(
            BlockTableRecord block, Transaction tr,
            Point3d first, Point3d second)
        {
            var line = new Line(first, second)
            {
                Layer = "0",
                ColorIndex = 0,
                Linetype = "ByLayer",
                LineWeight = LineWeight.ByLayer
            };
            block.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
        }

        private static Point3d At(Point3d origin, double x, double y)
        {
            return new Point3d(origin.X + x, origin.Y + y, origin.Z);
        }

        private static string Width08(string value)
        {
            return "{\\W0.8;" + Clean(value) + "}";
        }

        private static string Height08Width08(string value)
        {
            return "{\\H0.8x;\\W0.8;" + Clean(value) + "}";
        }

        private static string NormalizeDiameter(string value)
        {
            string text = Clean(value);
            if (text.Length == 0) return "DN";
            return Regex.IsMatch(text, @"^\d+(?:\.\d+)?$")
                ? "DN" + text : text;
        }

        private static double ReferenceNodeLabelShelfLength(
            string value, MText fallbackText)
        {
            string text = Clean(value).ToUpperInvariant()
                .Replace('?', '-');
            // ??????????????????? SHX ??????
            // ?? AutoCAD MText.GeometricExtents???? 1/105 ????
            // ???????????????????????W-1?W-19
            // ????????????????
            var units = new Dictionary<char, double>
            {
                { 'W', 120.5 }, { '-', 88.0 },
                { '0', 100.0 }, { '1', 64.0 },
                { '2', 100.0 }, { '3', 100.0 },
                { '4', 104.0 }, { '5', 100.0 },
                { '6', 96.0 }, { '7', 100.0 },
                { '8', 100.0 }, { '9', 96.0 }
            };
            double total = 0.0;
            for (int i = 0; i < text.Length; i++)
            {
                double width;
                if (!units.TryGetValue(text[i], out width))
                {
                    try
                    {
                        return Math.Max(3.0,
                            0.5 + fallbackText.ActualWidth);
                    }
                    catch
                    {
                        return Math.Max(3.0,
                            0.5 + text.Length * 0.72);
                    }
                }
                total += width;
            }
            return Math.Max(3.0, 0.5 + total / 105.0);
        }

        private static bool TryGetReferenceHeaderLeft(
            string key, out double x)
        {
            switch ((key ?? string.Empty).Trim())
            {
                case "GroundElevation": x = 3.8835355; return true;
                case "DesignInvertElevation": x = 2.9035355; return true;
                case "PipeBottomDepth": x = 5.2835355; return true;
                case "WellDepth": x = 8.8535355; return true;
                case "DiameterSlope": x = 5.2835355; return true;
                case "PlanDistance": x = 6.5435355; return true;
                case "PipeFoundation": x = 6.4735355; return true;
                case "WellNumber": x = 7.6635355; return true;
                default: x = 0.0; return false;
            }
        }

        private static string FormatTrimmed(double value, int decimals)
        {
            decimals = Math.Max(0, Math.Min(6, decimals));
            if (Math.Abs(value) < Math.Pow(10.0, -decimals) * 0.5)
                value = 0.0;
            string text = value.ToString("F" + decimals,
                CultureInfo.InvariantCulture);
            return decimals == 0 ? text
                : text.TrimEnd('0').TrimEnd('.');
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private sealed class ReferencePlacementJig : DrawJig
        {
            private readonly LongitudinalProfileData _profile;
            private readonly LongitudinalProfileLayout _layout;
            private Point3d _position;

            public ReferencePlacementJig(
                LongitudinalProfileData profile,
                LongitudinalProfileLayout layout)
            {
                _profile = profile;
                _layout = layout;
                _position = Point3d.Origin;
            }

            public Point3d Position { get { return _position; } }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var options = new JigPromptPointOptions("\n ")
                {
                    UserInputControls =
                        UserInputControls.Accept3dCoordinates
                        | UserInputControls.NoZeroResponseAccepted
                };
                PromptPointResult result = prompts.AcquirePoint(options);
                if (result.Status != PromptStatus.OK)
                    return SamplerStatus.Cancel;
                if (result.Value.DistanceTo(_position) < 1e-8)
                    return SamplerStatus.NoChange;
                _position = result.Value;
                return SamplerStatus.OK;
            }

            protected override bool WorldDraw(WorldDraw draw)
            {
                if (draw == null || draw.Geometry == null) return true;
                DrawLine(draw, 0.0, 0.0, _layout.HeaderRight, 0.0);
                DrawLine(draw, 0.0, _layout.TableTop,
                    _layout.HeaderRight, _layout.TableTop);
                DrawLine(draw, 0.0, 0.0, 0.0, _layout.TableTop);
                DrawLine(draw, _layout.HeaderRight, 0.0,
                    _layout.HeaderRight, _layout.TableTop);
                foreach (LongitudinalProfileRowLayout row in _layout.Rows)
                {
                    DrawLine(draw, 0.0, row.Bottom,
                        _layout.HeaderRight, row.Bottom);
                    DrawLine(draw, _layout.DataLeft, row.Bottom,
                        _layout.DataRight, row.Bottom);
                }
                DrawLine(draw, _layout.PlotLeft, _layout.ChartBottom,
                    _layout.PlotRight, _layout.ChartBottom);
                DrawLine(draw, _layout.PlotLeft, _layout.ChartTop,
                    _layout.PlotRight, _layout.ChartTop);
                for (int i = 0; i < _profile.Nodes.Count; i++)
                {
                    LongitudinalProfileNodeData node = _profile.Nodes[i];
                    double x = _layout.X(node.CumulativeDistance);
                    DrawLine(draw, x, _layout.ChartBottom, x,
                        _layout.Y(node.GroundElevation));
                    if (i + 1 < _profile.Nodes.Count)
                    {
                        LongitudinalProfileNodeData next =
                            _profile.Nodes[i + 1];
                        DrawLine(draw, x,
                            _layout.Y(node.GroundElevation),
                            _layout.X(next.CumulativeDistance),
                            _layout.Y(next.GroundElevation));
                        DrawLine(draw, x,
                            _layout.Y(node.DesignInvertElevation),
                            _layout.X(next.CumulativeDistance),
                            _layout.Y(next.DesignInvertElevation));
                    }
                }
                return true;
            }

            private void DrawLine(WorldDraw draw,
                double x1, double y1, double x2, double y2)
            {
                draw.Geometry.WorldLine(At(x1, y1), At(x2, y2));
            }

            private Point3d At(double x, double y)
            {
                return new Point3d(_position.X + x,
                    _position.Y + y, _position.Z);
            }
        }
    }
}
