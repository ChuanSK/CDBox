using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using TCPipeAutoDraw.Core.Cad;

namespace TCPipeAutoDraw.Modules.LongitudinalProfile
{
    public sealed class LongitudinalProfileDrawingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int EntityCount { get; set; }

        public LongitudinalProfileDrawingResult()
        {
            Message = string.Empty;
        }
    }

    public static class LongitudinalProfileDrawingService
    {
        private const short GroundColor = 7;
        private const short PipeColor = 3;
        private const short GuideColor = 2;
        private const short ScaleColor = 4;

        public static LongitudinalProfileDrawingResult
            SelectPositionAndDraw(
                Document document,
                LongitudinalProfileData profile,
                LongitudinalProfileSettings settings)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (profile == null) throw new ArgumentNullException("profile");
            settings = (settings ?? new LongitudinalProfileSettings()).Clone();
            settings.Normalize();
            LongitudinalProfileLayout layout =
                LongitudinalProfileLayoutCalculator.Calculate(profile,
                    settings);
            ObjectId textStyleId = ResolveTextStyle(
                document.Database, settings.HeaderTextStyleName);
            var jig = new LongitudinalProfilePlacementJig(profile,
                settings, layout, textStyleId);
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
            Document document,
            LongitudinalProfileData profile,
            LongitudinalProfileSettings sourceSettings,
            Point3d insertionPoint)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (profile == null) throw new ArgumentNullException("profile");
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
            Database database = document.Database;
            using (DocumentLock documentLock = document.LockDocument())
            using (Transaction transaction =
                database.TransactionManager.StartTransaction())
            {
                CadLayerService.EnsureLayer(database, transaction,
                    settings.LayerName, 7);
                ObjectId headerTextStyle = ResolveTextStyle(database,
                    settings.HeaderTextStyleName, transaction);
                LongitudinalProfileEntityStyle gridStyle =
                    settings.Style("????");
                LongitudinalProfileEntityStyle headerStyle =
                    settings.Style("??");
                LongitudinalProfileEntityStyle rowStyle =
                    settings.Style("???");

                try
                {
                    DrawTable(database, transaction, profile, settings,
                        layout, insertionPoint, headerTextStyle,
                        headerStyle, rowStyle, result);
                }
                catch (System.Exception ex)
                {
                    throw new InvalidOperationException(
                        "???????????" + ex.Message, ex);
                }
                try
                {
                    DrawChart(database, transaction, profile, settings,
                        layout, insertionPoint, headerTextStyle, gridStyle,
                        result);
                }
                catch (System.Exception ex)
                {
                    throw new InvalidOperationException(
                        "??????????" + ex.Message, ex);
                }
                try
                {
                    DrawScaleMark(database, transaction, settings, layout,
                        insertionPoint, headerTextStyle, result);
                }
                catch (System.Exception ex)
                {
                    throw new InvalidOperationException(
                        "????????????" + ex.Message, ex);
                }
                transaction.Commit();
            }
            result.Message = "????????";
            return result;
        }

        private static void DrawTable(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId headerTextStyle,
            LongitudinalProfileEntityStyle headerStyle,
            LongitudinalProfileEntityStyle rowStyle,
            LongitudinalProfileDrawingResult result)
        {
            DrawRectangle(db, tr, origin, layout.HeaderLeft,
                layout.TableBottom, layout.HeaderRight, layout.TableTop,
                settings.LayerName, headerStyle, result);
            DrawRectangle(db, tr, origin, layout.DataLeft,
                layout.TableBottom, layout.DataRight, layout.TableTop,
                settings.LayerName, rowStyle, result);

            foreach (LongitudinalProfileRowLayout row in layout.Rows)
            {
                if (row.Bottom > layout.TableBottom + 1e-8)
                {
                    AddLine(db, tr, P(origin, layout.HeaderLeft, row.Bottom),
                        P(origin, layout.HeaderRight, row.Bottom),
                        settings.LayerName, rowStyle, result);
                    AddLine(db, tr, P(origin, layout.DataLeft, row.Bottom),
                        P(origin, layout.DataRight, row.Bottom),
                        settings.LayerName, rowStyle, result);
                }
                TextHorizontalMode headerMode =
                    TextHorizontalMode.TextCenter;
                double headerX =
                    (layout.HeaderLeft + layout.HeaderRight) / 2.0;
                if (settings.HeaderTextAlignment.IndexOf("?",
                    StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    headerMode = TextHorizontalMode.TextLeft;
                    headerX = layout.HeaderLeft + layout.Scale(4.0);
                }
                else if (settings.HeaderTextAlignment.IndexOf("?",
                    StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    headerMode = TextHorizontalMode.TextRight;
                    headerX = layout.HeaderRight - layout.Scale(4.0);
                }
                AddText(db, tr, P(origin, headerX, row.Center),
                    row.Settings.Name,
                    Math.Min(layout.Scale(settings.HeaderTextHeight),
                        (row.Top - row.Bottom) * 0.65),
                    settings.HeaderTextColorIndex,
                    headerTextStyle, 0.0, settings.LayerName, result,
                    headerMode);
            }

            string elevationFormat =
                "F" + settings.ElevationDecimals.ToString(
                    CultureInfo.InvariantCulture);
            DrawNodeValues(db, tr, profile, settings, layout, origin,
                layout.Row("GroundElevation"),
                node => node.GroundElevation.ToString(elevationFormat,
                    CultureInfo.InvariantCulture), result);
            DrawNodeValues(db, tr, profile, settings, layout, origin,
                layout.Row("DesignInvertElevation"),
                node => node.DesignInvertElevation.ToString(
                    elevationFormat, CultureInfo.InvariantCulture), result);
            DrawNodeValues(db, tr, profile, settings, layout, origin,
                layout.Row("PipeBottomDepth"),
                node => FormatTrimmed(node.PipeBottomDepth,
                    settings.ValueDecimals), result);
            DrawNodeValues(db, tr, profile, settings, layout, origin,
                layout.Row("WellDepth"),
                node => FormatTrimmed(node.WellDepth,
                    settings.ValueDecimals), result);

            LongitudinalProfileRowLayout pipeRow =
                layout.Row("DiameterSlope");
            LongitudinalProfileRowLayout distanceRow =
                layout.Row("PlanDistance");
            LongitudinalProfileRowLayout foundationRow =
                layout.Row("PipeFoundation");
            foreach (LongitudinalProfileNodeData node in profile.Nodes)
            {
                double nodeX = layout.X(node.CumulativeDistance);
                AddLine(db, tr, P(origin, nodeX, distanceRow.Bottom),
                    P(origin, nodeX, pipeRow.Top), settings.LayerName,
                    rowStyle, result);
            }
            for (int i = 0; i < profile.Spans.Count; i++)
            {
                LongitudinalProfileSpanData span = profile.Spans[i];
                double x1 = layout.X(
                    profile.Nodes[i].CumulativeDistance);
                double x2 = layout.X(
                    profile.Nodes[i + 1].CumulativeDistance);
                double width = Math.Max(0.01, x2 - x1);
                double textHeight = layout.Scale(
                    pipeRow.Settings.TextHeight);
                string diameter = string.IsNullOrWhiteSpace(span.Diameter)
                    ? "DN" : span.Diameter;
                AddLine(db, tr, P(origin, x1, pipeRow.Top),
                    P(origin, x2, pipeRow.Bottom), settings.LayerName,
                    rowStyle, result);
                AddText(db, tr, P(origin, x1 + width * 0.25,
                        pipeRow.Center),
                    diameter, textHeight, settings.HeaderTextColorIndex,
                    ResolveTextStyle(db,
                        pipeRow.Settings.TextStyleName, tr),
                    0.0, settings.LayerName, result);
                AddText(db, tr, P(origin, x1 + width * 0.75,
                        pipeRow.Center),
                    "i=" + FormatTrimmed(Math.Abs(span.SlopePercent),
                        settings.SlopeDecimals),
                    textHeight, settings.HeaderTextColorIndex,
                    ResolveTextStyle(db,
                        pipeRow.Settings.TextStyleName, tr),
                    0.0, settings.LayerName, result);
                AddText(db, tr, P(origin, (x1 + x2) / 2.0,
                        distanceRow.Center),
                    "L=" + FormatTrimmed(span.PlanLength,
                        settings.ValueDecimals),
                    layout.Scale(distanceRow.Settings.TextHeight),
                    settings.HeaderTextColorIndex,
                    ResolveTextStyle(db,
                        distanceRow.Settings.TextStyleName, tr),
                    0.0, settings.LayerName, result);
            }

            DrawFoundationValues(db, tr, profile, settings, layout,
                origin, foundationRow, rowStyle, result);

            LongitudinalProfileRowLayout numberRow =
                layout.Row("WellNumber");
            for (int i = 0; i < profile.Nodes.Count; i++)
            {
                LongitudinalProfileNodeData node = profile.Nodes[i];
                AddText(db, tr, P(origin,
                        layout.X(node.CumulativeDistance),
                        numberRow.Center),
                    node.NodeNo, layout.Scale(numberRow.Settings.TextHeight),
                    settings.HeaderTextColorIndex,
                    ResolveTextStyle(db,
                        numberRow.Settings.TextStyleName, tr),
                    0.0, settings.LayerName, result);
            }
        }

        private static void DrawNodeValues(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            LongitudinalProfileRowLayout row,
            Func<LongitudinalProfileNodeData, string> value,
            LongitudinalProfileDrawingResult result)
        {
            ObjectId textStyle =
                ResolveTextStyle(db, row.Settings.TextStyleName, tr);
            foreach (LongitudinalProfileNodeData node in profile.Nodes)
            {
                AddText(db, tr,
                    P(origin, layout.X(node.CumulativeDistance), row.Center),
                    value(node), layout.Scale(row.Settings.TextHeight),
                    settings.HeaderTextColorIndex, textStyle,
                    Math.PI / 2.0, settings.LayerName, result);
            }
        }

        private static void DrawFoundationValues(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            LongitudinalProfileRowLayout row,
            LongitudinalProfileEntityStyle rowStyle,
            LongitudinalProfileDrawingResult result)
        {
            if (profile.Spans.Count == 0) return;
            ObjectId textStyle = ResolveTextStyle(db,
                row.Settings.TextStyleName, tr);
            int start = 0;
            while (start < profile.Spans.Count)
            {
                string value = CleanLabel(profile.Spans[start].Foundation);
                int end = start + 1;
                while (end < profile.Spans.Count
                    && string.Equals(value,
                        CleanLabel(profile.Spans[end].Foundation),
                        StringComparison.CurrentCultureIgnoreCase))
                    end++;
                double x1 = layout.X(
                    profile.Nodes[start].CumulativeDistance);
                double x2 = layout.X(
                    profile.Nodes[end].CumulativeDistance);
                AddLine(db, tr, P(origin, x1, row.Bottom),
                    P(origin, x1, row.Top), settings.LayerName,
                    rowStyle, result);
                if (end == profile.Spans.Count)
                    AddLine(db, tr, P(origin, x2, row.Bottom),
                        P(origin, x2, row.Top), settings.LayerName,
                        rowStyle, result);
                if (value.Length > 0)
                    AddText(db, tr, P(origin, (x1 + x2) / 2.0,
                            row.Center), value,
                        layout.Scale(row.Settings.TextHeight),
                        settings.HeaderTextColorIndex, textStyle, 0.0,
                        settings.LayerName, result);
                start = end;
            }
        }

        private static void DrawChart(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId textStyle,
            LongitudinalProfileEntityStyle gridStyle,
            LongitudinalProfileDrawingResult result)
        {
            DrawRectangle(db, tr, origin, layout.PlotLeft,
                layout.ChartBottom, layout.PlotRight, layout.ChartTop,
                settings.LayerName, gridStyle, result);

            double elevation = layout.DatumElevation;
            int guard = 0;
            while (elevation <= layout.TopElevation + 1e-8
                && guard++ < 1000)
            {
                double y = layout.Y(elevation);
                if (y > layout.ChartBottom + 1e-8
                    && y < layout.ChartTop - 1e-8)
                    AddLine(db, tr, P(origin, layout.PlotLeft, y),
                        P(origin, layout.PlotRight, y),
                        settings.LayerName, gridStyle, result);
                AddText(db, tr, P(origin,
                        layout.StaffLeft
                            - layout.Scale(settings.HeaderChartGap),
                        y),
                    elevation.ToString(
                        "F" + settings.ElevationDecimals,
                        CultureInfo.InvariantCulture),
                    layout.Scale(2.5), 7, textStyle, 0.0,
                    settings.LayerName, result,
                    TextHorizontalMode.TextRight);
                elevation += settings.ElevationGridInterval;
            }
            AddText(db, tr, P(origin,
                    layout.StaffLeft
                        - layout.Scale(settings.HeaderChartGap) * 0.3,
                    layout.ChartTop + layout.Scale(1.25)),
                "??(?)", layout.Scale(2.5), 7, textStyle, 0.0,
                settings.LayerName, result, TextHorizontalMode.TextRight);

            double plotDistance = (layout.PlotRight - layout.PlotLeft)
                / layout.HorizontalFactor;
            double distance = settings.HorizontalGridInterval;
            guard = 0;
            while (distance < plotDistance - 1e-8
                && guard++ < 10000)
            {
                double x = layout.X(distance);
                AddLine(db, tr, P(origin, x, layout.ChartBottom),
                    P(origin, x, layout.ChartTop), settings.LayerName,
                    gridStyle, result);
                distance += settings.HorizontalGridInterval;
            }

            DrawElevationStaff(db, tr, settings, layout, origin, result);
            DrawProfileLines(db, tr, profile, settings, layout, origin,
                textStyle, result);
        }

        private static void DrawElevationStaff(
            Database db, Transaction tr,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            LongitudinalProfileDrawingResult result)
        {
            LongitudinalProfileEntityStyle style =
                settings.Style("???");
            DrawRectangle(db, tr, origin, layout.StaffLeft,
                layout.ChartBottom, layout.StaffRight, layout.ChartTop,
                settings.LayerName, style, result);
            double elevation = layout.DatumElevation;
            int index = 0;
            while (elevation < layout.TopElevation - 1e-8
                && index < 1000)
            {
                double next = Math.Min(layout.TopElevation,
                    elevation + settings.ElevationGridInterval);
                if (index % 2 == 0)
                    AddSolid(db, tr,
                        P(origin, layout.StaffLeft, layout.Y(elevation)),
                        P(origin, layout.StaffRight, layout.Y(elevation)),
                        P(origin, layout.StaffLeft, layout.Y(next)),
                        P(origin, layout.StaffRight, layout.Y(next)),
                        settings.LayerName, 7, result);
                elevation = next;
                index++;
            }
        }

        private static void DrawProfileLines(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId textStyle,
            LongitudinalProfileDrawingResult result)
        {
            var ground = new List<Point3d>();
            foreach (LongitudinalProfileNodeData node in profile.Nodes)
            {
                double x = layout.X(node.CumulativeDistance);
                ground.Add(P(origin, x, layout.Y(node.GroundElevation)));
            }
            AddPolyline(db, tr, ground, settings.LayerName,
                GroundColor, result);

            for (int i = 0; i < profile.Spans.Count; i++)
            {
                LongitudinalProfileSpanData span = profile.Spans[i];
                LongitudinalProfileNodeData from = profile.Nodes[i];
                LongitudinalProfileNodeData to = profile.Nodes[i + 1];
                double diameter = Math.Max(0.01, span.OuterDiameter);
                double fromX = layout.X(from.CumulativeDistance) + 0.5;
                double toX = layout.X(to.CumulativeDistance) - 0.5;
                if (toX <= fromX + 1e-8)
                {
                    fromX = layout.X(from.CumulativeDistance);
                    toX = layout.X(to.CumulativeDistance);
                }
                AddLine(db, tr,
                    P(origin, fromX,
                        layout.Y(from.DesignInvertElevation)),
                    P(origin, toX,
                        layout.Y(to.DesignInvertElevation)),
                    settings.LayerName, null, result, PipeColor);
                AddLine(db, tr,
                    P(origin, fromX,
                        layout.Y(from.DesignInvertElevation + diameter)),
                    P(origin, toX,
                        layout.Y(to.DesignInvertElevation + diameter)),
                    settings.LayerName, null, result, PipeColor);
            }

            for (int i = 0; i < profile.Nodes.Count; i++)
                DrawProfileNode(db, tr, profile, settings, layout,
                    origin, textStyle, result, i);
        }

        private static void DrawProfileNode(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId textStyle,
            LongitudinalProfileDrawingResult result, int index)
        {
            LongitudinalProfileNodeData node = profile.Nodes[index];
            LongitudinalProfileSpanData incoming = index > 0
                ? profile.Spans[index - 1] : null;
            LongitudinalProfileSpanData outgoing =
                index < profile.Spans.Count ? profile.Spans[index] : null;
            double x = layout.X(node.CumulativeDistance);
            double groundY = layout.Y(node.GroundElevation);
            double wellBottomY = layout.Y(
                node.GroundElevation - node.WellDepth);

            AddLine(db, tr, P(origin, x, layout.ChartBottom),
                P(origin, x, groundY), settings.LayerName, null, result,
                GuideColor, "DASHED");
            AddLine(db, tr, P(origin, x - 0.5, groundY),
                P(origin, x + 0.5, groundY), settings.LayerName,
                null, result, GroundColor);
            DrawWellSide(db, tr, settings, layout, origin, result,
                x - 0.5, groundY, wellBottomY, node, incoming);
            DrawWellSide(db, tr, settings, layout, origin, result,
                x + 0.5, groundY, wellBottomY, node, outgoing);
            AddLine(db, tr, P(origin, x - 0.5, wellBottomY),
                P(origin, x + 0.5, wellBottomY), settings.LayerName,
                null, result, GroundColor);

            double labelY = layout.ChartTop + 5.0;
            AddLine(db, tr, P(origin, x, groundY),
                P(origin, x, labelY), settings.LayerName, null, result,
                GroundColor);
            double labelWidth = Math.Max(3.0,
                CleanLabel(node.NodeNo).Length * 0.72);
            AddLine(db, tr, P(origin, x, labelY),
                P(origin, x + labelWidth, labelY), settings.LayerName,
                null, result, GroundColor);
            AddText(db, tr, P(origin, x + 0.5, labelY + 0.4),
                node.NodeNo, layout.Scale(2.5), 7, textStyle, 0.0,
                settings.LayerName, result, TextHorizontalMode.TextLeft);
        }

        private static void DrawWellSide(
            Database db, Transaction tr,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            LongitudinalProfileDrawingResult result,
            double x, double groundY, double wellBottomY,
            LongitudinalProfileNodeData node,
            LongitudinalProfileSpanData adjacent)
        {
            if (adjacent == null)
            {
                AddLine(db, tr, P(origin, x, groundY),
                    P(origin, x, wellBottomY), settings.LayerName,
                    null, result, GroundColor);
                return;
            }

            double invertY = layout.Y(node.DesignInvertElevation);
            double topY = layout.Y(node.DesignInvertElevation
                + Math.Max(0.01, adjacent.OuterDiameter));
            if (groundY > topY + 1e-8)
                AddLine(db, tr, P(origin, x, groundY),
                    P(origin, x, topY), settings.LayerName,
                    null, result, GroundColor);
            if (invertY > wellBottomY + 1e-8)
                AddLine(db, tr, P(origin, x, invertY),
                    P(origin, x, wellBottomY), settings.LayerName,
                    null, result, GroundColor);
        }

        private static void DrawScaleMark(
            Database db, Transaction tr,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId textStyle,
            LongitudinalProfileDrawingResult result)
        {
            double x = layout.HeaderLeft;
            double y = layout.TableTop + 2.5;
            double axis = 3.75;
            double wing = 0.55;
            AddLine(db, tr, P(origin, x, y),
                P(origin, x + axis, y), settings.LayerName,
                null, result, ScaleColor);
            AddLine(db, tr, P(origin, x, y),
                P(origin, x, y + axis), settings.LayerName,
                null, result, 6);
            AddLine(db, tr, P(origin, x + axis, y),
                P(origin, x + axis - wing, y + wing * 0.45),
                settings.LayerName, null, result, ScaleColor);
            AddLine(db, tr, P(origin, x + axis, y),
                P(origin, x + axis - wing, y - wing * 0.45),
                settings.LayerName, null, result, ScaleColor);
            AddLine(db, tr, P(origin, x, y + axis),
                P(origin, x - wing * 0.45, y + axis - wing),
                settings.LayerName, null, result, 6);
            AddLine(db, tr, P(origin, x, y + axis),
                P(origin, x + wing * 0.45, y + axis - wing),
                settings.LayerName, null, result, 6);
            AddText(db, tr, P(origin, x + axis / 2.0, y + 0.4),
                "? 1 : " + settings.HorizontalScale.ToString("0",
                    CultureInfo.InvariantCulture),
                layout.Scale(2.5), ScaleColor, textStyle, 0.0,
                settings.LayerName, result);
            AddText(db, tr, P(origin, x + 0.4, y + axis / 2.0),
                "? 1 : " + settings.VerticalScale.ToString("0",
                    CultureInfo.InvariantCulture),
                layout.Scale(2.5), 6, textStyle, Math.PI / 2.0,
                settings.LayerName, result);

            AddText(db, tr, P(origin,
                    (layout.PlotLeft + layout.DataRight) / 2.0,
                    layout.TableBottom - 2.5),
                "???????", layout.Scale(2.0), 1,
                textStyle, 0.0, settings.LayerName, result);
        }

        private static string FormatTrimmed(double value, int decimals)
        {
            decimals = Math.Max(0, Math.Min(6, decimals));
            if (Math.Abs(value) < Math.Pow(10.0, -decimals) * 0.5)
                value = 0.0;
            string text = value.ToString("F" + decimals,
                CultureInfo.InvariantCulture);
            return decimals <= 0 ? text
                : text.TrimEnd('0').TrimEnd('.');
        }

        private static string CleanLabel(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static Point3d P(Point3d origin, double x, double y)
        {
            return new Point3d(origin.X + x, origin.Y + y, origin.Z);
        }

        private static void DrawRectangle(
            Database db, Transaction tr, Point3d origin,
            double left, double bottom, double right, double top,
            string layer, LongitudinalProfileEntityStyle style,
            LongitudinalProfileDrawingResult result)
        {
            AddPolyline(db, tr, new[]
            {
                P(origin, left, bottom), P(origin, right, bottom),
                P(origin, right, top), P(origin, left, top),
                P(origin, left, bottom)
            }, layer, style == null ? (short)7 : style.ColorIndex,
                result, style);
        }

        private static void AddSquare(
            Database db, Transaction tr, Point3d center,
            double size, string layer, short color,
            LongitudinalProfileDrawingResult result)
        {
            double h = size / 2.0;
            AddPolyline(db, tr, new[]
            {
                new Point3d(center.X-h,center.Y-h,center.Z),
                new Point3d(center.X+h,center.Y-h,center.Z),
                new Point3d(center.X+h,center.Y+h,center.Z),
                new Point3d(center.X-h,center.Y+h,center.Z),
                new Point3d(center.X-h,center.Y-h,center.Z)
            }, layer, color, result);
        }

        private static void AddLine(
            Database db, Transaction tr, Point3d start, Point3d end,
            string layer, LongitudinalProfileEntityStyle style,
            LongitudinalProfileDrawingResult result,
            short? overrideColor = null, string lineType = null)
        {
            var entity = new Line(start, end) { Layer = layer };
            ApplyStyle(entity, db, tr, style, overrideColor, lineType);
            Append(db, tr, entity, result);
        }

        private static void AddPolyline(
            Database db, Transaction tr, IEnumerable<Point3d> points,
            string layer, short color,
            LongitudinalProfileDrawingResult result,
            LongitudinalProfileEntityStyle style = null)
        {
            var polyline =
                new Autodesk.AutoCAD.DatabaseServices.Polyline
                { Layer = layer };
            int index = 0;
            foreach (Point3d point in points)
                polyline.AddVertexAt(index++,
                    new Point2d(point.X, point.Y), 0.0, 0.0, 0.0);
            polyline.Elevation = points.First().Z;
            ApplyStyle(polyline, db, tr, style, color, null);
            Append(db, tr, polyline, result);
        }

        private static void AddText(
            Database db, Transaction tr, Point3d position,
            string text, double height, short color,
            ObjectId textStyle, double rotation, string layer,
            LongitudinalProfileDrawingResult result,
            TextHorizontalMode horizontal =
                TextHorizontalMode.TextCenter)
        {
            var entity = new DBText();
            entity.SetDatabaseDefaults(db);
            entity.TextString = text ?? string.Empty;
            entity.Height = Math.Max(0.1, height);
            entity.HorizontalMode = horizontal;
            entity.VerticalMode = TextVerticalMode.TextVerticalMid;
            entity.Position = position;
            // AutoCAD ?????????????????
            // AlignmentPoint??????????????
            // Autodesk.AutoCAD.Runtime.Exception(eNotApplicable)?
            entity.AlignmentPoint = position;
            entity.Rotation = rotation;
            entity.Layer = layer;
            entity.Color =
                Color.FromColorIndex(ColorMethod.ByAci, color);
            if (!textStyle.IsNull) entity.TextStyleId = textStyle;
            Append(db, tr, entity, result);
            try { entity.AdjustAlignment(db); } catch { }
        }

        private static void AddSolid(
            Database db, Transaction tr,
            Point3d p1, Point3d p2, Point3d p3, Point3d p4,
            string layer, short color,
            LongitudinalProfileDrawingResult result)
        {
            // Solid ???????????????
            var entity = new Solid(p1, p2, p4, p3)
            {
                Layer = layer,
                Color = Color.FromColorIndex(ColorMethod.ByAci, color)
            };
            Append(db, tr, entity, result);
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

        private static void ApplyStyle(
            Entity entity, Database db, Transaction tr,
            LongitudinalProfileEntityStyle style,
            short? overrideColor, string overrideLineType)
        {
            short color = overrideColor
                ?? (style == null ? (short)7 : style.ColorIndex);
            entity.Color =
                Color.FromColorIndex(ColorMethod.ByAci, color);
            string lineType = !string.IsNullOrWhiteSpace(overrideLineType)
                ? overrideLineType
                : style == null ? string.Empty : style.LineTypeName;
            TrySetLineType(entity, db, tr, lineType);
            if (style != null)
            {
                entity.LinetypeScale = style.LineTypeScale;
                LineWeight weight;
                if (Enum.TryParse(style.LineWeight, true, out weight))
                    entity.LineWeight = weight;
            }
        }

        private static void TrySetLineType(
            Entity entity, Database db, Transaction tr, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            string clean = name.Trim();
            try
            {
                LinetypeTable table = (LinetypeTable)tr.GetObject(
                    db.LinetypeTableId, OpenMode.ForRead);
                if (!table.Has(clean)
                    && !string.Equals(clean, "ByBlock",
                        StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(clean, "ByLayer",
                        StringComparison.OrdinalIgnoreCase))
                {
                    try { db.LoadLineTypeFile(clean, "acad.lin"); }
                    catch { }
                }
                table = (LinetypeTable)tr.GetObject(
                    db.LinetypeTableId, OpenMode.ForRead);
                if (table.Has(clean)) entity.Linetype = clean;
            }
            catch { }
        }

        private static ObjectId ResolveTextStyle(
            Database database, string name)
        {
            using (Transaction transaction =
                database.TransactionManager.StartTransaction())
            {
                ObjectId id =
                    ResolveTextStyle(database, name, transaction);
                transaction.Commit();
                return id;
            }
        }

        private static ObjectId ResolveTextStyle(
            Database database, string name, Transaction transaction)
        {
            try
            {
                TextStyleTable table = (TextStyleTable)transaction.GetObject(
                    database.TextStyleTableId, OpenMode.ForRead);
                string clean = string.IsNullOrWhiteSpace(name)
                    ? "Standard" : name.Trim();
                return table.Has(clean)
                    ? table[clean] : database.Textstyle;
            }
            catch { return database.Textstyle; }
        }
    }

    internal sealed class LongitudinalProfilePlacementJig : DrawJig
    {
        private readonly LongitudinalProfileData _profile;
        private readonly LongitudinalProfileSettings _settings;
        private readonly LongitudinalProfileLayout _layout;
        private readonly ObjectId _textStyleId;
        private Point3d _position;

        public Point3d Position { get { return _position; } }

        public LongitudinalProfilePlacementJig(
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout,
            ObjectId textStyleId)
        {
            _profile = profile;
            _settings = settings;
            _layout = layout;
            _textStyleId = textStyleId;
            _position = Point3d.Origin;
        }

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
            DrawRectangle(draw, 0, 0, _layout.HeaderRight,
                _layout.TableTop);
            DrawRectangle(draw, _layout.DataLeft, 0,
                _layout.DataRight, _layout.TableTop);
            DrawRectangle(draw, _layout.PlotLeft, _layout.ChartBottom,
                _layout.PlotRight, _layout.ChartTop);
            foreach (LongitudinalProfileRowLayout row in _layout.Rows)
            {
                DrawLine(draw, 0, row.Bottom,
                    _layout.HeaderRight, row.Bottom);
                DrawLine(draw, _layout.DataLeft, row.Bottom,
                    _layout.DataRight, row.Bottom);
                DrawText(draw, new Point3d(
                        _position.X + _layout.HeaderRight / 2.0,
                        _position.Y + row.Center, _position.Z),
                    row.Settings.Name,
                    _layout.Scale(row.Settings.TextHeight));
            }
            var ground = new Point3dCollection();
            var invert = new Point3dCollection();
            foreach (LongitudinalProfileNodeData node in _profile.Nodes)
            {
                double x = _layout.X(node.CumulativeDistance);
                ground.Add(new Point3d(_position.X + x,
                    _position.Y + _layout.Y(node.GroundElevation),
                    _position.Z));
                invert.Add(new Point3d(_position.X + x,
                    _position.Y + _layout.Y(
                        node.DesignInvertElevation), _position.Z));
            }
            draw.Geometry.Polyline(ground, Vector3d.ZAxis, IntPtr.Zero);
            draw.Geometry.Polyline(invert, Vector3d.ZAxis, IntPtr.Zero);
            foreach (LongitudinalProfileNodeData node in _profile.Nodes)
            {
                double x = _layout.X(node.CumulativeDistance);
                DrawLine(draw, x, _layout.ChartBottom, x,
                    _layout.Y(node.GroundElevation));
            }
            return true;
        }

        private void DrawRectangle(
            WorldDraw draw, double left, double bottom,
            double right, double top)
        {
            var points = new Point3dCollection
            {
                At(left, bottom), At(right, bottom), At(right, top),
                At(left, top), At(left, bottom)
            };
            draw.Geometry.Polyline(points, Vector3d.ZAxis, IntPtr.Zero);
        }

        private void DrawLine(
            WorldDraw draw, double x1, double y1, double x2, double y2)
        {
            draw.Geometry.WorldLine(At(x1, y1), At(x2, y2));
        }

        private void DrawText(
            WorldDraw draw, Point3d position, string text, double height)
        {
            string value = text ?? string.Empty;
            double safeHeight = Math.Max(0.1, height);
            double estimatedWidth = Math.Max(safeHeight,
                value.Length * safeHeight * 0.62);
            Point3d basePoint = new Point3d(
                position.X - estimatedWidth / 2.0,
                position.Y - safeHeight * 0.35, position.Z);
            using (var dbText = new DBText
            {
                Position = basePoint,
                HorizontalMode = TextHorizontalMode.TextLeft,
                VerticalMode = TextVerticalMode.TextBase,
                Height = safeHeight,
                TextString = value,
                TextStyleId = _textStyleId
            })
            {
                draw.Geometry.Draw(dbText);
            }
        }

        private Point3d At(double x, double y)
        {
            return new Point3d(_position.X + x, _position.Y + y,
                _position.Z);
        }
    }
}
