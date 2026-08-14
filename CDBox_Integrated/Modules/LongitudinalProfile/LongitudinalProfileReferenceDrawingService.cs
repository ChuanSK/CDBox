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
    /// 按“纵断面参考.dwg”逐实体复刻的纵断面绘制器。
    /// 图层、实体类型、字体、字高、文字附着点及所有相对坐标均取自
    /// 管立得纵断面炸开后的实体；污水纵断节点/主管则按原代理对象
    /// 的 Explode 几何重建。
    /// </summary>
    internal static class LongitudinalProfileReferenceDrawingService
    {
        internal const string HeaderLayer = "315井号";
        internal const string ScaleLayer = "纵断面图比例标注";
        internal const string GridLayer = "坐标网格线";
        internal const string GroundLayer = "自然地面线";
        internal const string GraphLayer = "纵断面图";
        internal const string TitleLayer = "纵断面图名称标注";
        internal const string NodeLabelLayer = "污水纵断节点标注";
        internal const string NodeLayer = "污水纵断节点";
        internal const string PipeLayer = "污水纵断主管";
        internal const string BranchLayer = "污水纵断支管";
        internal const string BranchLabelLayer = "污水纵断支管标注";

        private const string HzStyle = "HZ";
        private const string FangSongStyle = "仿宋体";
        private const string ArrowBlock = "箭头2";
        private const string SewageSectionBlock = "污水断面";
        private const double MTextHeight = 1.25;
        private const double MTextLineSpacing = 0.792;
        private const double ElevationTitleXOffset = 1.577537097;
        private const double ElevationTitleYOffset = 0.63649368;
        private const double NodeGlobalWidth = 0.15;
        private const double PipeGlobalWidth = 0.1;
        private const double BranchSymbolRadius = 0.5;
        private const double BranchLeaderLength = 10.185714;
        private const double BranchTextInset = 1.0;
        private const double BranchTextOffset = 0.4;
        private const double BranchLabelStagger = 4.873641;
        private const double BranchStaggerX = 0.007685;

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
            var jig = new ReferencePlacementJig(profile, settings,
                layout, document.Database);
            PromptResult prompt = document.Editor.DragWithHud(jig,
                "请选择纵断面图落图位置（单击确定）");
            if (prompt.Status != PromptStatus.OK)
            {
                return new LongitudinalProfileDrawingResult
                {
                    Success = false,
                    Message = "已取消纵断面绘制。"
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
                throw new ArgumentException("纵断面至少需要两个井节点。",
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
                ObjectId sewageSection = EnsureSewageSectionBlock(db, tr);
                ObjectId headerTextStyle = ResolveTextStyle(db, tr,
                    settings.HeaderTextStyleName, fangSong);

                DrawHeader(db, tr, settings, layout, insertionPoint,
                    headerTextStyle, result);
                DrawDataTable(db, tr, profile, settings, layout,
                    insertionPoint, hz, fangSong, result);
                DrawChart(db, tr, profile, settings, layout,
                    insertionPoint, hz, fangSong, result);
                DrawProfileObjects(db, tr, profile, layout,
                    insertionPoint, result);
                DrawWellConnections(db, tr, profile, settings, layout,
                    insertionPoint, hz, sewageSection, result);
                DrawNodeLabels(db, tr, profile, layout, insertionPoint,
                    hz, result);
                DrawScale(db, tr, settings, layout, insertionPoint,
                    hz, arrow, result);
                DrawTitle(db, tr, layout, insertionPoint, fangSong,
                    result);
                WrapProfileInBlock(db, tr, result);
                tr.Commit();
            }

            result.Message = "纵断面图已按参考样式生成。";
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
                AddCenteredHeaderText(db, tr, origin,
                    row.Settings.Name,
                    row.Center,
                    (layout.HeaderLeft + layout.HeaderRight) / 2.0,
                    settings.HeaderTextHeight,
                    settings.HeaderTextColorIndex, textStyle, result);
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
            DrawOptionalNodeRow(db, tr, profile, layout, origin,
                "GroundElevation", n => FormatFixed(n.GroundElevation,
                    settings.ElevationDecimals), hz, result);
            DrawOptionalNodeRow(db, tr, profile, layout, origin,
                "DesignInvertElevation", n => FormatFixed(
                    n.DesignInvertElevation, settings.ElevationDecimals),
                hz, result);
            DrawOptionalNodeRow(db, tr, profile, layout, origin,
                "PipeBottomDepth", n => FormatTrimmed(n.PipeBottomDepth,
                    settings.ValueDecimals), hz, result);
            DrawOptionalNodeRow(db, tr, profile, layout, origin,
                "WellDepth", n => FormatTrimmed(n.WellDepth,
                    settings.ValueDecimals), hz, result);

            List<LongitudinalProfileRowLayout> pipeRows =
                layout.RowsFor("DiameterSlope").ToList();
            List<LongitudinalProfileRowLayout> distanceRows =
                layout.RowsFor("PlanDistance").ToList();
            foreach (LongitudinalProfileRowLayout row in pipeRows)
                AddRowTopAndBottom(db, tr, origin, layout, row, result);
            foreach (LongitudinalProfileRowLayout row in distanceRows)
                AddRowTopAndBottom(db, tr, origin, layout, row, result);
            for (int i = 0; i < profile.Spans.Count; i++)
            {
                double x1 = layout.X(
                    profile.Nodes[i].CumulativeDistance);
                double x2 = layout.X(
                    profile.Nodes[i + 1].CumulativeDistance);
                foreach (LongitudinalProfileRowLayout pipeRow in pipeRows)
                {
                    DrawCellSides(db, tr, origin, x1, x2, pipeRow,
                        result);
                    // 参考纵断面仅在存在实际坡度时绘制坡向斜线；水平管段不画斜线。
                    if (Math.Abs(profile.Spans[i].SlopePercent) > 1e-8)
                        AddLine(db, tr, At(origin, x1, pipeRow.Top),
                            At(origin, x2, pipeRow.Bottom), GraphLayer,
                            256, "ByLayer", LineWeight.ByLayer, result);
                }
                foreach (LongitudinalProfileRowLayout distanceRow in
                    distanceRows)
                    DrawCellSides(db, tr, origin, x1, x2, distanceRow,
                        result);

                LongitudinalProfileSpanData span = profile.Spans[i];
                double width = x2 - x1;
                foreach (LongitudinalProfileRowLayout pipeRow in pipeRows)
                {
                    ObjectId style = ResolveTextStyle(db, tr,
                        pipeRow.Settings.TextStyleName, hz);
                    double height = RowTextHeight(pipeRow);
                    short color = pipeRow.Settings.TextColorIndex;
                    AddMText(db, tr,
                        At(origin, x1 + width * 0.25, pipeRow.Center),
                        Width08(NormalizeDiameter(span.Diameter)),
                        height, color, style, 0.0,
                        AttachmentPoint.MiddleCenter, GraphLayer, result,
                        0.6);
                    AddMText(db, tr,
                        At(origin, x1 + width * 0.75, pipeRow.Center),
                        Width08("i=" + FormatTrimmed(
                            Math.Abs(span.SlopePercent),
                            settings.SlopeDecimals)),
                        height, color, style, 0.0,
                        AttachmentPoint.MiddleCenter, GraphLayer, result,
                        0.6);
                }
                foreach (LongitudinalProfileRowLayout distanceRow in
                    distanceRows)
                    AddMText(db, tr,
                        At(origin, (x1 + x2) / 2.0,
                            distanceRow.Center),
                        Width08("L=" + FormatTrimmed(span.PlanLength,
                            settings.ValueDecimals)),
                        RowTextHeight(distanceRow),
                        distanceRow.Settings.TextColorIndex,
                        ResolveTextStyle(db, tr,
                            distanceRow.Settings.TextStyleName, hz), 0.0,
                        AttachmentPoint.MiddleCenter, GraphLayer, result,
                        0.6);
            }

            DrawFoundationRow(db, tr, profile, layout, origin,
                fangSong, result);

            foreach (LongitudinalProfileRowLayout numberRow in
                layout.RowsFor("WellNumber"))
            {
                AddRowTopAndBottom(db, tr, origin, layout, numberRow,
                    result);
                foreach (LongitudinalProfileNodeData node in profile.Nodes)
                    AddMText(db, tr,
                        At(origin, layout.X(node.CumulativeDistance),
                            numberRow.Center),
                        Width08(node.NodeNo), RowTextHeight(numberRow),
                        numberRow.Settings.TextColorIndex,
                        ResolveTextStyle(db, tr,
                            numberRow.Settings.TextStyleName, hz), 0.0,
                        AttachmentPoint.MiddleCenter, GraphLayer, result,
                        0.6);
            }
        }

        private static void DrawOptionalNodeRow(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileLayout layout, Point3d origin,
            string key, Func<LongitudinalProfileNodeData, string> value,
            ObjectId fallbackStyle,
            LongitudinalProfileDrawingResult result)
        {
            foreach (LongitudinalProfileRowLayout row in
                layout.RowsFor(key))
            {
                AddRowTopAndBottom(db, tr, origin, layout, row, result);
                DrawNodeRowTexts(db, tr, profile, layout, origin, row,
                    value, fallbackStyle, result);
            }
        }

        private static void DrawNodeRowTexts(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileLayout layout, Point3d origin,
            LongitudinalProfileRowLayout row,
            Func<LongitudinalProfileNodeData, string> value,
            ObjectId fallbackStyle,
            LongitudinalProfileDrawingResult result)
        {
            short color = row.Settings.TextColorIndex;
            ObjectId style = ResolveTextStyle(db, tr,
                row.Settings.TextStyleName, fallbackStyle);
            foreach (LongitudinalProfileNodeData node in profile.Nodes)
            {
                AddMText(db, tr,
                    At(origin, layout.X(node.CumulativeDistance),
                        row.Center),
                    Width08(value(node)), RowTextHeight(row), color, style,
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
            foreach (LongitudinalProfileRowLayout row in
                layout.RowsFor("PipeFoundation"))
            {
                ObjectId style = ResolveTextStyle(db, tr,
                    row.Settings.TextStyleName, fangSong);
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
                            Height08Width08(value), RowTextHeight(row),
                            row.Settings.TextColorIndex,
                            style, 0.0, AttachmentPoint.MiddleCenter,
                            GraphLayer, result, 0.6);
                    start = end;
                }
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
                "高程(米)", MTextHeight, 256, fangSong, 0.0,
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
                    span.StartInvertElevation);
                double secondInvert = layout.Y(
                    span.EndInvertElevation);
                double pipeHeight = Math.Max(0.01,
                    span.OuterDiameter) * layout.VerticalFactor;

                AddTwoPointPolyline(db, tr,
                    At(origin, x1, layout.Y(first.GroundElevation)),
                    At(origin, x2, layout.Y(second.GroundElevation)),
                    GroundLayer, 256, 0.0, result);
                AddTwoPointPolyline(db, tr,
                    At(origin, x1 + 0.5, firstInvert + pipeHeight),
                    At(origin, x2 - 0.5, secondInvert + pipeHeight),
                    PipeLayer, 3, PipeGlobalWidth, "ByLayer",
                    LineWeight.LineWeight020, result);
                AddTwoPointPolyline(db, tr,
                    At(origin, x1 + 0.5, firstInvert),
                    At(origin, x2 - 0.5, secondInvert),
                    PipeLayer, 3, PipeGlobalWidth, "ByLayer",
                    LineWeight.LineWeight020, result);
            }

            DrawBoundaryExtension(db, tr, profile, layout, origin,
                profile.StartExtension, result);
            DrawBoundaryExtension(db, tr, profile, layout, origin,
                profile.EndExtension, result);
            for (int i = 0; i < profile.Nodes.Count; i++)
                DrawProfileNode(db, tr, profile, layout, origin, i,
                    result);
        }

        private static void DrawBoundaryExtension(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileLayout layout, Point3d origin,
            LongitudinalProfileBoundaryExtensionData extension,
            LongitudinalProfileDrawingResult result)
        {
            if (extension == null || profile == null
                || profile.Nodes == null || profile.Nodes.Count == 0)
                return;

            LongitudinalProfileNodeData node = extension.AtStart
                ? profile.Nodes[0]
                : profile.Nodes[profile.Nodes.Count - 1];
            double centerX = layout.X(node.CumulativeDistance);
            double direction = extension.AtStart ? -1.0 : 1.0;
            double wallX = centerX + direction * 0.5;
            double outsideX = wallX
                + direction * LongitudinalProfileLayoutCalculator
                    .BoundaryExtensionLength;
            double boundaryInvertY = layout.Y(
                extension.BoundaryInvertElevation);
            double sampleDistance = LongitudinalProfileLayoutCalculator
                .BoundaryExtensionLength
                / Math.Max(1e-12, layout.HorizontalFactor);
            double ratio = Math.Min(1.0, sampleDistance
                / Math.Max(1e-12, extension.PlanLength));
            double outsideElevation = extension.BoundaryInvertElevation
                + (extension.OutsideInvertElevation
                    - extension.BoundaryInvertElevation) * ratio;
            double outsideInvertY = layout.Y(outsideElevation);
            double pipeHeight = Math.Max(0.01,
                extension.OuterDiameter) * layout.VerticalFactor;

            AddTwoPointPolyline(db, tr,
                At(origin, outsideX, outsideInvertY + pipeHeight),
                At(origin, wallX, boundaryInvertY + pipeHeight),
                PipeLayer, 3, PipeGlobalWidth, "ByLayer",
                LineWeight.LineWeight020, result);
            AddTwoPointPolyline(db, tr,
                At(origin, outsideX, outsideInvertY),
                At(origin, wallX, boundaryInvertY),
                PipeLayer, 3, PipeGlobalWidth, "ByLayer",
                LineWeight.LineWeight020, result);
            double groundY = layout.Y(node.GroundElevation);
            double bottomY = layout.Y(
                node.GroundElevation - node.WellDepth);
            DrawBoundaryBreak(db, tr, layout, origin, result,
                outsideX, groundY, bottomY, extension,
                extension.AtStart ? 1.0 : -1.0,
                outsideInvertY);
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
                "ACAD_ISO04W100", LineWeight.ByLineWeightDefault,
                result);
            LongitudinalProfileBoundaryExtensionData boundary =
                index == 0 ? profile.StartExtension
                : (index == profile.Nodes.Count - 1
                    ? profile.EndExtension : null);
            DrawOpenWellOutline(db, tr, layout, origin, result, x,
                groundY, invertY, bottomY, incoming, outgoing,
                boundary);
        }

        private static void DrawOpenWellOutline(
            Database db, Transaction tr,
            LongitudinalProfileLayout layout, Point3d origin,
            LongitudinalProfileDrawingResult result, double x,
            double groundY, double invertY, double bottomY,
            LongitudinalProfileSpanData incoming,
            LongitudinalProfileSpanData outgoing,
            LongitudinalProfileBoundaryExtensionData boundary)
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
            if (boundary != null && boundary.AtStart)
            {
                openLeft = TryGetBoundaryOpening(boundary, layout,
                    bottomY, groundY, out leftBottom, out leftTop);
            }
            if (boundary != null && !boundary.AtStart)
            {
                openRight = TryGetBoundaryOpening(boundary, layout,
                    bottomY, groundY, out rightBottom, out rightTop);
            }

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

        private static void DrawBoundaryBreak(
            Database db, Transaction tr,
            LongitudinalProfileLayout layout, Point3d origin,
            LongitudinalProfileDrawingResult result, double breakX,
            double groundY, double bottomY,
            LongitudinalProfileBoundaryExtensionData boundary,
            double interiorDirection, double outsideInvertY)
        {
            double pipeHeight = Math.Max(0.01,
                boundary.OuterDiameter) * layout.VerticalFactor;
            double centerY = outsideInvertY + pipeHeight / 2.0;
            double half = LongitudinalProfileLayoutCalculator
                .BoundaryBreakHalfSize;
            double straight = LongitudinalProfileLayoutCalculator
                .BoundaryBreakStraightLength;
            double innerX = breakX + interiorDirection * half;
            double outerX = breakX - interiorDirection * half;

            AddLine(db, tr,
                At(origin, breakX, centerY + half + straight),
                At(origin, breakX, centerY + half), PipeLayer, 0,
                "ByLayer", LineWeight.ByBlock, result);
            AddLine(db, tr, At(origin, breakX, centerY + half),
                At(origin, innerX, centerY + half), PipeLayer, 0,
                "ByLayer", LineWeight.ByBlock, result);
            AddLine(db, tr, At(origin, innerX, centerY + half),
                At(origin, outerX, centerY - half), PipeLayer, 0,
                "ByLayer", LineWeight.ByBlock, result);
            AddLine(db, tr, At(origin, outerX, centerY - half),
                At(origin, breakX, centerY - half), PipeLayer, 0,
                "ByLayer", LineWeight.ByBlock, result);
            AddLine(db, tr, At(origin, breakX, centerY - half),
                At(origin, breakX, centerY - half - straight),
                PipeLayer, 0,
                "ByLayer", LineWeight.ByBlock, result);
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

        private static bool TryGetBoundaryOpening(
            LongitudinalProfileBoundaryExtensionData boundary,
            LongitudinalProfileLayout layout, double bottomY,
            double groundY, out double openingBottom,
            out double openingTop)
        {
            openingBottom = bottomY;
            openingTop = bottomY;
            if (boundary == null) return false;
            openingBottom = Math.Max(bottomY,
                layout.Y(boundary.BoundaryInvertElevation));
            openingTop = Math.Min(groundY, openingBottom
                + Math.Max(0.01, boundary.OuterDiameter)
                    * layout.VerticalFactor);
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
                NodeGlobalWidth, "ByLayer", LineWeight.LineWeight030,
                result);
        }

        private static void DrawWellConnections(
            Database db, Transaction tr,
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId hz, ObjectId sewageSection,
            LongitudinalProfileDrawingResult result)
        {
            if (profile.Connections == null
                || profile.Connections.Count == 0) return;
            foreach (LongitudinalProfileNodeData node in profile.Nodes)
            {
                List<LongitudinalProfileConnectionData> connections =
                    profile.Connections.Where(x => x != null
                        && string.Equals(Clean(x.NodeNo),
                            Clean(node.NodeNo),
                            StringComparison.CurrentCultureIgnoreCase))
                        .ToList();
                if (connections.Count == 0) continue;
                double x = layout.X(node.CumulativeDistance);
                double wellBottomY = layout.Y(
                    node.DesignInvertElevation);
                AddBlockReference(db, tr, sewageSection,
                    At(origin, x, wellBottomY + BranchSymbolRadius),
                    1.0, 0.0, BranchLayer, 256, result);
                DrawConnectionSide(db, tr, connections.Where(x =>
                        string.Equals(x.Side, "左侧",
                            StringComparison.Ordinal)).ToList(),
                    true, node, settings, layout, origin, hz, result);
                DrawConnectionSide(db, tr, connections.Where(x =>
                        !string.Equals(x.Side, "左侧",
                            StringComparison.Ordinal)).ToList(),
                    false, node, settings, layout, origin, hz, result);
            }
        }

        private static void DrawConnectionSide(
            Database db, Transaction tr,
            IList<LongitudinalProfileConnectionData> connections,
            bool leftSide, LongitudinalProfileNodeData node,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId hz, LongitudinalProfileDrawingResult result)
        {
            if (connections == null || connections.Count == 0) return;
            List<LongitudinalProfileConnectionData> ordered = connections
                .OrderByDescending(x => x.InvertElevation).ToList();
            double x = layout.X(node.CumulativeDistance);
            double wellBottomY = layout.Y(
                node.DesignInvertElevation);
            double direction = leftSide ? -1.0 : 1.0;
            for (int i = 0; i < ordered.Count; i++)
            {
                LongitudinalProfileConnectionData connection = ordered[i];
                double labelY = wellBottomY
                    - i * BranchLabelStagger;
                double staggerX = i * BranchStaggerX;
                double bendX = x + direction
                    * (BranchSymbolRadius + staggerX);
                double shelfOuterX = x
                    + direction * (BranchLeaderLength + staggerX);
                AddPolyline(db, tr, new[]
                {
                    At(origin, x, wellBottomY),
                    At(origin, bendX, labelY),
                    At(origin, shelfOuterX, labelY)
                }, false, BranchLabelLayer, 7, 0.0, "ByLayer",
                    LineWeight.ByLayer, result);

                double textX = x + direction * BranchTextInset;
                AttachmentPoint elevationAttachment = leftSide
                    ? AttachmentPoint.BottomRight
                    : AttachmentPoint.BottomLeft;
                AttachmentPoint labelAttachment = leftSide
                    ? AttachmentPoint.TopRight
                    : AttachmentPoint.TopLeft;
                AddMText(db, tr,
                    At(origin, textX, labelY + BranchTextOffset),
                    Width08(FormatFixed(connection.InvertElevation,
                        settings.ElevationDecimals)), MTextHeight, 7, hz,
                    0.0, elevationAttachment, BranchLabelLayer,
                    result, 0.6);
                AddMText(db, tr,
                    At(origin, textX, labelY - BranchTextOffset),
                    "W " + NormalizeDiameter(connection.Diameter)
                        + " ({\\H0.8x;"
                        + (leftSide ? "左侧" : "右侧") + "})",
                    MTextHeight, 7, hz, 0.0,
                    labelAttachment, BranchLabelLayer, result,
                    0.6);
            }
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
                "{\\H0.8x;\\W0.8;横}{\\W0.8; 1 : "
                    + settings.HorizontalScale.ToString("0",
                        CultureInfo.InvariantCulture) + "}",
                MTextHeight, 4, hz, 0.0,
                AttachmentPoint.BottomLeft, ScaleLayer, result);
            AddMText(db, tr, At(origin, 0.4, baseY + 1.65),
                "{\\H0.8x;\\W0.8;竖}{\\W0.8; 1 : "
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
                Height08Width08("污水管纵断面图"), 1.0, 5,
                fangSong, 0.0, AttachmentPoint.TopCenter, TitleLayer,
                result);
            // 管立得标题下划线使用独立固定宽度，不等于 MText 的外接框。
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
            Database db, Transaction tr, Point3d origin, string value,
            double centerY, double centerX, double height, short color,
            ObjectId style,
            LongitudinalProfileDrawingResult result)
        {
            var text = new DBText();
            text.SetDatabaseDefaults(db);
            text.TextString = Clean(value);
            text.Height = Math.Max(0.1, height);
            text.WidthFactor = 0.8;
            text.Position = At(origin, centerX, centerY);
            text.HorizontalMode = TextHorizontalMode.TextCenter;
            text.VerticalMode = TextVerticalMode.TextVerticalMid;
            text.AlignmentPoint = text.Position;
            text.Rotation = 0.0;
            text.Layer = HeaderLayer;
            text.ColorIndex = color;
            text.Linetype = "Continuous";
            text.LineWeight = LineWeight.ByLayer;
            if (!style.IsNull) text.TextStyleId = style;
            Append(db, tr, text, result);
            try { text.AdjustAlignment(db); } catch { }
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

        private static void AddCircle(
            Database db, Transaction tr, Point3d center, double radius,
            string layer, short color,
            LongitudinalProfileDrawingResult result)
        {
            var circle = new Circle(center, Vector3d.ZAxis,
                Math.Max(0.01, radius))
            {
                Layer = layer,
                ColorIndex = color,
                Linetype = "ByLayer",
                LineWeight = LineWeight.ByLayer
            };
            Append(db, tr, circle, result);
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
            result.EntityIds.Add(entity.ObjectId);
            result.EntityCount++;
        }

        private static void WrapProfileInBlock(Database db, Transaction tr,
            LongitudinalProfileDrawingResult result)
        {
            if (db == null || tr == null || result == null ||
                result.EntityIds.Count == 0) return;
            BlockTable table = (BlockTable)tr.GetObject(db.BlockTableId,
                OpenMode.ForWrite);
            string name = "CDBOX_ZDM_" + Guid.NewGuid().ToString("N");
            var definition = new BlockTableRecord
            {
                Name = name,
                Origin = Point3d.Origin
            };
            ObjectId definitionId = table.Add(definition);
            tr.AddNewlyCreatedDBObject(definition, true);
            foreach (ObjectId id in result.EntityIds.ToList())
            {
                Entity source = tr.GetObject(id, OpenMode.ForWrite,
                    false) as Entity;
                if (source == null || source.IsErased) continue;
                Entity clone = source.Clone() as Entity;
                if (clone == null) continue;
                definition.AppendEntity(clone);
                tr.AddNewlyCreatedDBObject(clone, true);
                source.Erase();
            }
            BlockTableRecord space = (BlockTableRecord)tr.GetObject(
                db.CurrentSpaceId, OpenMode.ForWrite);
            var reference = new BlockReference(Point3d.Origin, definitionId);
            space.AppendEntity(reference);
            tr.AddNewlyCreatedDBObject(reference, true);
            result.EntityIds.Clear();
            result.EntityIds.Add(reference.ObjectId);
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
                LineWeight.LineWeight020);
            EnsureLayer(db, tr, BranchLayer, 7);
            EnsureLayer(db, tr, BranchLabelLayer, 7);
            EnsureLineType(db, tr, "ACAD_ISO04W100");
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
            ObjectId id = CadLayerService.EnsureGeneratedLayer(db, tr, name,
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

            string[] sources = string.Equals(clean, "ACAD_ISO04W100",
                StringComparison.OrdinalIgnoreCase)
                ? new[] { "acadiso.lin", "acad.lin" }
                : new[] { "cass.lin", "acad.lin", "acadiso.lin" };
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
                    AsciiDescription = "CDBox " + clean,
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

        private static ObjectId ResolveTextStyle(
            Database db, Transaction tr, string name, ObjectId fallback)
        {
            try
            {
                string clean = Clean(name);
                if (clean.Length == 0) return fallback;
                TextStyleTable table = (TextStyleTable)tr.GetObject(
                    db.TextStyleTableId, OpenMode.ForRead);
                return table.Has(clean) ? table[clean] : fallback;
            }
            catch { return fallback; }
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

        private static ObjectId EnsureSewageSectionBlock(
            Database db, Transaction tr)
        {
            BlockTable table = (BlockTable)tr.GetObject(
                db.BlockTableId, OpenMode.ForRead);
            if (table.Has(SewageSectionBlock))
                return table[SewageSectionBlock];
            table.UpgradeOpen();
            var block = new BlockTableRecord
            {
                Name = SewageSectionBlock,
                Origin = Point3d.Origin
            };
            ObjectId id = table.Add(block);
            tr.AddNewlyCreatedDBObject(block, true);

            var outline = new Autodesk.AutoCAD.DatabaseServices.Polyline
            {
                Layer = "0",
                ColorIndex = 0,
                Linetype = "ByLayer",
                LineWeight = LineWeight.ByLayer,
                Closed = true
            };
            outline.AddVertexAt(0,
                new Point2d(-BranchSymbolRadius, 0.0), 1.0, 0.0, 0.0);
            outline.AddVertexAt(1,
                new Point2d(BranchSymbolRadius, 0.0), 1.0, 0.0, 0.0);
            block.AppendEntity(outline);
            tr.AddNewlyCreatedDBObject(outline, true);

            AddBlockLine(block, tr,
                new Point3d(-BranchSymbolRadius, 0.0, 0.0),
                new Point3d(BranchSymbolRadius, 0.0, 0.0));

            Hatch hatch = null;
            try
            {
                hatch = new Hatch();
                hatch.SetDatabaseDefaults(db);
                hatch.Layer = "0";
                hatch.ColorIndex = 0;
                hatch.Linetype = "ByLayer";
                hatch.LineWeight = LineWeight.ByLayer;
                block.AppendEntity(hatch);
                tr.AddNewlyCreatedDBObject(hatch, true);
                hatch.Associative = false;
                hatch.SetHatchPattern(HatchPatternType.PreDefined,
                    "SOLID");
                var points = new Point2dCollection
                {
                    new Point2d(-BranchSymbolRadius, 0.0),
                    new Point2d(BranchSymbolRadius, 0.0)
                };
                var bulges = new DoubleCollection { 1.0, 0.0 };
                hatch.AppendLoop(HatchLoopTypes.Polyline
                    | HatchLoopTypes.Outermost, points, bulges);
                hatch.EvaluateHatch(true);
            }
            catch
            {
                // 圆轮廓与直径线仍可完整表达接入口；填充失败不阻断纵断面生成。
                try
                {
                    if (hatch != null && !hatch.IsErased)
                        hatch.Erase();
                }
                catch { }
            }
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
                .Replace('－', '-');
            // “纵断面参考”中节点横线由管立得自己的 SHX 字宽表计算，
            // 并非 AutoCAD MText.GeometricExtents。下面的 1/105 字宽单位
            // 来自参考图逐字反算；保留相邻数字的原始微调后，W-1～W-19
            // 的每个横线端点与参考图完全一致。
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

        private static string FormatFixed(double value, int decimals)
        {
            decimals = Math.Max(0, Math.Min(6, decimals));
            if (Math.Abs(value) < Math.Pow(10.0, -decimals) * 0.5)
                value = 0.0;
            return value.ToString("F" + decimals,
                CultureInfo.InvariantCulture);
        }

        private static double RowTextHeight(
            LongitudinalProfileRowLayout row)
        {
            return row == null || row.Settings == null
                ? MTextHeight
                : Math.Max(0.1, row.Settings.TextHeight);
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private sealed class ReferencePlacementJig : DrawJig
        {
            private readonly LongitudinalProfileData _profile;
            private readonly LongitudinalProfileSettings _settings;
            private readonly LongitudinalProfileLayout _layout;
            private readonly Database _database;
            private readonly ObjectId _textStyleId;
            private Point3d _position;

            public ReferencePlacementJig(
                LongitudinalProfileData profile,
                LongitudinalProfileSettings settings,
                LongitudinalProfileLayout layout, Database database)
            {
                _profile = profile;
                _settings = settings;
                _layout = layout;
                _database = database;
                _textStyleId = database == null
                    ? ObjectId.Null : database.Textstyle;
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
                DrawPreviewTable(draw);
                DrawPreviewChart(draw);
                DrawPreviewProfile(draw);
                DrawPreviewConnections(draw);
                DrawPreviewLabels(draw);
                return true;
            }

            private void DrawPreviewTable(WorldDraw draw)
            {
                DrawRectangle(draw, _layout.HeaderLeft,
                    _layout.TableBottom, _layout.HeaderRight,
                    _layout.TableTop, 255);
                foreach (LongitudinalProfileRowLayout row in _layout.Rows)
                {
                    if (row.Bottom > _layout.TableBottom + 1e-8)
                        DrawLine(draw, _layout.HeaderLeft, row.Bottom,
                            _layout.HeaderRight, row.Bottom, 255);
                    DrawText(draw,
                        (_layout.HeaderLeft + _layout.HeaderRight) / 2.0,
                        row.Center, row.Settings.Name,
                        _settings.HeaderTextHeight,
                        0.0, _settings.HeaderTextColorIndex, true);

                    DrawLine(draw, _layout.DataLeft, row.Top,
                        _layout.DataRight, row.Top, 256);
                    DrawLine(draw, _layout.DataLeft, row.Bottom,
                        _layout.DataRight, row.Bottom, 256);
                    DrawPreviewRow(draw, row);
                }
            }

            private void DrawPreviewRow(WorldDraw draw,
                LongitudinalProfileRowLayout row)
            {
                string key = row.Settings.Key ?? string.Empty;
                double height = Math.Max(0.1, row.Settings.TextHeight);
                short color = row.Settings.TextColorIndex;
                if (key == "DiameterSlope" || key == "PlanDistance")
                {
                    for (int i = 0; i < _profile.Spans.Count; i++)
                    {
                        double x1 = _layout.X(
                            _profile.Nodes[i].CumulativeDistance);
                        double x2 = _layout.X(
                            _profile.Nodes[i + 1].CumulativeDistance);
                        DrawLine(draw, x1, row.Top, x1, row.Bottom, 256);
                        DrawLine(draw, x2, row.Top, x2, row.Bottom, 256);
                        LongitudinalProfileSpanData span =
                            _profile.Spans[i];
                        if (key == "DiameterSlope")
                        {
                            if (Math.Abs(span.SlopePercent) > 1e-8)
                                DrawLine(draw, x1, row.Top, x2,
                                    row.Bottom, 256);
                            DrawText(draw, x1 + (x2 - x1) * 0.25,
                                row.Center,
                                NormalizeDiameter(span.Diameter), height,
                                0.0, color, true);
                            DrawText(draw, x1 + (x2 - x1) * 0.75,
                                row.Center, "i=" + FormatTrimmed(
                                    Math.Abs(span.SlopePercent),
                                    _settings.SlopeDecimals),
                                height, 0.0, color, true);
                        }
                        else
                        {
                            DrawText(draw, (x1 + x2) / 2.0,
                                row.Center, "L=" + FormatTrimmed(
                                    span.PlanLength,
                                    _settings.ValueDecimals),
                                height, 0.0, color, true);
                        }
                    }
                    return;
                }

                if (key == "PipeFoundation")
                {
                    int start = 0;
                    while (start < _profile.Spans.Count)
                    {
                        string value = Clean(
                            _profile.Spans[start].Foundation);
                        int end = start + 1;
                        while (end < _profile.Spans.Count
                            && string.Equals(value,
                                Clean(_profile.Spans[end].Foundation),
                                StringComparison.CurrentCultureIgnoreCase))
                            end++;
                        double x1 = _layout.X(
                            _profile.Nodes[start].CumulativeDistance);
                        double x2 = _layout.X(
                            _profile.Nodes[end].CumulativeDistance);
                        DrawLine(draw, x1, row.Top, x1, row.Bottom, 256);
                        DrawLine(draw, x2, row.Top, x2, row.Bottom, 256);
                        DrawText(draw, (x1 + x2) / 2.0, row.Center,
                            value, height, 0.0, color, true);
                        start = end;
                    }
                    return;
                }

                foreach (LongitudinalProfileNodeData node in _profile.Nodes)
                {
                    string text;
                    if (key == "GroundElevation")
                        text = FormatFixed(node.GroundElevation,
                            _settings.ElevationDecimals);
                    else if (key == "DesignInvertElevation")
                        text = FormatFixed(node.DesignInvertElevation,
                            _settings.ElevationDecimals);
                    else if (key == "PipeBottomDepth")
                        text = FormatTrimmed(node.PipeBottomDepth,
                            _settings.ValueDecimals);
                    else if (key == "WellDepth")
                        text = FormatTrimmed(node.WellDepth,
                            _settings.ValueDecimals);
                    else if (key == "WellNumber")
                        text = node.NodeNo;
                    else continue;
                    DrawText(draw, _layout.X(node.CumulativeDistance),
                        row.Center, text, height,
                        key == "WellNumber" ? 0.0 : Math.PI / 2.0,
                        color, true);
                }
            }

            private void DrawPreviewChart(WorldDraw draw)
            {
                double elevation = _layout.DatumElevation;
                int guard = 0;
                while (elevation <= _layout.TopElevation + 1e-8
                    && guard++ < 2000)
                {
                    double y = _layout.Y(elevation);
                    DrawLine(draw, _layout.PlotLeft, y,
                        _layout.PlotRight, y, 8);
                    DrawText(draw, _layout.StaffLeft - 2.5, y,
                        FormatFixed(elevation,
                            _settings.ElevationDecimals),
                        MTextHeight, 0.0, 256, false);
                    elevation += _settings.ElevationGridInterval;
                }
                double plotDistance = (_layout.PlotRight
                    - _layout.PlotLeft) / _layout.HorizontalFactor;
                for (double distance = 0.0;
                    distance <= plotDistance + 1e-8;
                    distance += _settings.HorizontalGridInterval)
                {
                    double x = _layout.X(distance);
                    DrawLine(draw, x, _layout.ChartBottom, x,
                        _layout.ChartTop, 8);
                }
                DrawRectangle(draw, _layout.StaffLeft,
                    _layout.ChartBottom, _layout.StaffRight,
                    _layout.ChartTop, 0);
                elevation = _layout.DatumElevation;
                int band = 0;
                while (elevation < _layout.TopElevation - 1e-8
                    && band < 2000)
                {
                    double next = Math.Min(_layout.TopElevation,
                        elevation + _settings.ElevationGridInterval);
                    DrawLine(draw, _layout.StaffLeft,
                        _layout.Y(elevation), _layout.StaffRight,
                        _layout.Y(elevation), 0);
                    if (band % 2 == 0)
                    {
                        double centerX = (_layout.StaffLeft
                            + _layout.StaffRight) / 2.0;
                        DrawPolyline(draw, new[]
                        {
                            new Point2d(centerX, _layout.Y(elevation)),
                            new Point2d(centerX, _layout.Y(next))
                        }, false, 0,
                            _layout.StaffRight - _layout.StaffLeft,
                            LineWeight.ByLayer);
                    }
                    elevation = next;
                    band++;
                }
                DrawText(draw, _layout.StaffLeft
                        - ElevationTitleXOffset,
                    _layout.ChartTop + ElevationTitleYOffset,
                    "高程(米)", MTextHeight, 0.0, 256, false);
            }

            private void DrawPreviewProfile(WorldDraw draw)
            {
                for (int i = 0; i < _profile.Spans.Count; i++)
                {
                    LongitudinalProfileNodeData first = _profile.Nodes[i];
                    LongitudinalProfileNodeData second =
                        _profile.Nodes[i + 1];
                    LongitudinalProfileSpanData span = _profile.Spans[i];
                    double x1 = _layout.X(first.CumulativeDistance);
                    double x2 = _layout.X(second.CumulativeDistance);
                    double y1 = _layout.Y(span.StartInvertElevation);
                    double y2 = _layout.Y(span.EndInvertElevation);
                    double pipeHeight = Math.Max(0.01,
                        span.OuterDiameter) * _layout.VerticalFactor;
                    DrawLine(draw, x1, _layout.Y(first.GroundElevation),
                        x2, _layout.Y(second.GroundElevation), 8);
                    DrawPolyline(draw, new[]
                    {
                        new Point2d(x1 + 0.5, y1 + pipeHeight),
                        new Point2d(x2 - 0.5, y2 + pipeHeight)
                    }, false, 3, PipeGlobalWidth,
                        LineWeight.LineWeight020);
                    DrawPolyline(draw, new[]
                    {
                        new Point2d(x1 + 0.5, y1),
                        new Point2d(x2 - 0.5, y2)
                    }, false, 3, PipeGlobalWidth,
                        LineWeight.LineWeight020);
                }
                DrawPreviewBoundary(draw, _profile.StartExtension);
                DrawPreviewBoundary(draw, _profile.EndExtension);
                for (int i = 0; i < _profile.Nodes.Count; i++)
                    DrawPreviewWell(draw, i);
            }

            private void DrawPreviewBoundary(WorldDraw draw,
                LongitudinalProfileBoundaryExtensionData extension)
            {
                if (extension == null) return;
                LongitudinalProfileNodeData node = extension.AtStart
                    ? _profile.Nodes[0]
                    : _profile.Nodes[_profile.Nodes.Count - 1];
                double direction = extension.AtStart ? -1.0 : 1.0;
                double wallX = _layout.X(node.CumulativeDistance)
                    + direction * 0.5;
                double outsideX = wallX + direction
                    * LongitudinalProfileLayoutCalculator
                        .BoundaryExtensionLength;
                double boundaryY = _layout.Y(
                    extension.BoundaryInvertElevation);
                double sampleDistance =
                    LongitudinalProfileLayoutCalculator
                        .BoundaryExtensionLength
                    / Math.Max(1e-12, _layout.HorizontalFactor);
                double ratio = Math.Min(1.0, sampleDistance
                    / Math.Max(1e-12, extension.PlanLength));
                double outsideElevation =
                    extension.BoundaryInvertElevation
                    + (extension.OutsideInvertElevation
                        - extension.BoundaryInvertElevation) * ratio;
                double outsideY = _layout.Y(outsideElevation);
                double pipeHeight = Math.Max(0.01,
                    extension.OuterDiameter) * _layout.VerticalFactor;
                DrawPolyline(draw, new[]
                {
                    new Point2d(outsideX, outsideY + pipeHeight),
                    new Point2d(wallX, boundaryY + pipeHeight)
                }, false, 3, PipeGlobalWidth, LineWeight.LineWeight020);
                DrawPolyline(draw, new[]
                {
                    new Point2d(outsideX, outsideY),
                    new Point2d(wallX, boundaryY)
                }, false, 3, PipeGlobalWidth, LineWeight.LineWeight020);
                DrawPreviewBreak(draw, outsideX,
                    _layout.Y(node.GroundElevation),
                    _layout.Y(node.GroundElevation - node.WellDepth),
                    outsideY + pipeHeight / 2.0,
                    extension.AtStart ? 1.0 : -1.0);
            }

            private void DrawPreviewBreak(WorldDraw draw, double x,
                double top, double bottom, double center,
                double interiorDirection)
            {
                double half = LongitudinalProfileLayoutCalculator
                    .BoundaryBreakHalfSize;
                double straight = LongitudinalProfileLayoutCalculator
                    .BoundaryBreakStraightLength;
                double inner = x + interiorDirection * half;
                double outer = x - interiorDirection * half;
                DrawLine(draw, x, center + half + straight, x,
                    center + half, 0);
                DrawLine(draw, x, center + half, inner,
                    center + half, 0);
                DrawLine(draw, inner, center + half, outer,
                    center - half, 0);
                DrawLine(draw, outer, center - half, x,
                    center - half, 0);
                DrawLine(draw, x, center - half, x,
                    center - half - straight, 0);
            }

            private void DrawPreviewWell(WorldDraw draw, int index)
            {
                LongitudinalProfileNodeData node = _profile.Nodes[index];
                LongitudinalProfileSpanData incoming = index > 0
                    ? _profile.Spans[index - 1] : null;
                LongitudinalProfileSpanData outgoing =
                    index < _profile.Spans.Count
                        ? _profile.Spans[index] : null;
                LongitudinalProfileBoundaryExtensionData boundary =
                    index == 0 ? _profile.StartExtension
                    : (index == _profile.Nodes.Count - 1
                        ? _profile.EndExtension : null);
                double x = _layout.X(node.CumulativeDistance);
                double ground = _layout.Y(node.GroundElevation);
                double invert = _layout.Y(node.DesignInvertElevation);
                double bottom = _layout.Y(
                    node.GroundElevation - node.WellDepth);
                DrawLine(draw, x, ground, x, _layout.ChartBottom, 2);
                double leftBottom;
                double leftTop;
                double rightBottom;
                double rightTop;
                bool openLeft = TryGetWellOpening(incoming, _layout,
                    invert, bottom, ground, out leftBottom, out leftTop);
                bool openRight = TryGetWellOpening(outgoing, _layout,
                    invert, bottom, ground, out rightBottom,
                    out rightTop);
                if (boundary != null && boundary.AtStart)
                    openLeft = TryGetBoundaryOpening(boundary, _layout,
                        bottom, ground, out leftBottom, out leftTop);
                if (boundary != null && !boundary.AtStart)
                    openRight = TryGetBoundaryOpening(boundary, _layout,
                        bottom, ground, out rightBottom, out rightTop);
                DrawPreviewWellOutline(draw, x - 0.5, x + 0.5,
                    ground, bottom, openLeft, leftBottom, leftTop,
                    openRight, rightBottom, rightTop);
            }

            private void DrawPreviewWellOutline(WorldDraw draw,
                double left, double right, double ground, double bottom,
                bool openLeft, double leftBottom, double leftTop,
                bool openRight, double rightBottom, double rightTop)
            {
                if (openLeft && openRight)
                {
                    DrawWellPart(draw, new[]
                    {
                        new Point2d(left, leftTop),
                        new Point2d(left, ground),
                        new Point2d(right, ground),
                        new Point2d(right, rightTop)
                    }, false);
                    DrawWellPart(draw, new[]
                    {
                        new Point2d(right, rightBottom),
                        new Point2d(right, bottom),
                        new Point2d(left, bottom),
                        new Point2d(left, leftBottom)
                    }, false);
                    return;
                }
                if (openLeft)
                {
                    DrawWellPart(draw, new[]
                    {
                        new Point2d(left, leftTop),
                        new Point2d(left, ground),
                        new Point2d(right, ground),
                        new Point2d(right, bottom),
                        new Point2d(left, bottom),
                        new Point2d(left, leftBottom)
                    }, false);
                    return;
                }
                if (openRight)
                {
                    DrawWellPart(draw, new[]
                    {
                        new Point2d(right, rightTop),
                        new Point2d(right, ground),
                        new Point2d(left, ground),
                        new Point2d(left, bottom),
                        new Point2d(right, bottom),
                        new Point2d(right, rightBottom)
                    }, false);
                    return;
                }
                DrawWellPart(draw, new[]
                {
                    new Point2d(left, ground),
                    new Point2d(right, ground),
                    new Point2d(right, bottom),
                    new Point2d(left, bottom)
                }, true);
            }

            private void DrawWellPart(WorldDraw draw,
                IList<Point2d> points, bool closed)
            {
                DrawPolyline(draw, points, closed, 256,
                    NodeGlobalWidth, LineWeight.LineWeight030);
            }

            private void DrawPreviewConnections(WorldDraw draw)
            {
                if (_profile.Connections == null) return;
                foreach (LongitudinalProfileNodeData node in _profile.Nodes)
                {
                    List<LongitudinalProfileConnectionData> connections =
                        _profile.Connections.Where(x => x != null
                            && string.Equals(Clean(x.NodeNo),
                                Clean(node.NodeNo),
                                StringComparison
                                    .CurrentCultureIgnoreCase)).ToList();
                    if (connections.Count == 0) continue;
                    double x = _layout.X(node.CumulativeDistance);
                    double baseY = _layout.Y(node.DesignInvertElevation);
                    DrawCircle(draw, x, baseY + BranchSymbolRadius,
                        BranchSymbolRadius, 256);
                    DrawLine(draw, x - BranchSymbolRadius,
                        baseY + BranchSymbolRadius,
                        x + BranchSymbolRadius,
                        baseY + BranchSymbolRadius, 256);
                    DrawPreviewConnectionSide(draw, node,
                        connections.Where(c => c.Side == "左侧")
                            .OrderByDescending(c => c.InvertElevation)
                            .ToList(), true);
                    DrawPreviewConnectionSide(draw, node,
                        connections.Where(c => c.Side != "左侧")
                            .OrderByDescending(c => c.InvertElevation)
                            .ToList(), false);
                }
            }

            private void DrawPreviewConnectionSide(WorldDraw draw,
                LongitudinalProfileNodeData node,
                IList<LongitudinalProfileConnectionData> connections,
                bool leftSide)
            {
                double x = _layout.X(node.CumulativeDistance);
                double baseY = _layout.Y(node.DesignInvertElevation);
                double direction = leftSide ? -1.0 : 1.0;
                for (int i = 0; i < connections.Count; i++)
                {
                    LongitudinalProfileConnectionData connection =
                        connections[i];
                    double labelY = baseY - i * BranchLabelStagger;
                    double staggerX = i * BranchStaggerX;
                    double bendX = x + direction
                        * (BranchSymbolRadius + staggerX);
                    double outerX = x + direction
                        * (BranchLeaderLength + staggerX);
                    DrawPolyline(draw, new[]
                    {
                        new Point2d(x, baseY),
                        new Point2d(bendX, labelY),
                        new Point2d(outerX, labelY)
                    }, false, 7, 0.0, LineWeight.ByLayer);
                    double textX = x + direction * BranchTextInset;
                    DrawText(draw, textX,
                        labelY + BranchTextOffset,
                        FormatFixed(connection.InvertElevation,
                            _settings.ElevationDecimals),
                        MTextHeight, 0.0, 7, !leftSide);
                    DrawText(draw, textX,
                        labelY - BranchTextOffset,
                        "W " + NormalizeDiameter(connection.Diameter)
                            + " (" + (leftSide ? "左侧" : "右侧") + ")",
                        MTextHeight, 0.0, 7, !leftSide);
                }
            }

            private void DrawPreviewLabels(WorldDraw draw)
            {
                double labelY = _layout.ChartTop + 5.0;
                foreach (LongitudinalProfileNodeData node in _profile.Nodes)
                {
                    double x = _layout.X(node.CumulativeDistance);
                    double ground = _layout.Y(node.GroundElevation);
                    DrawLine(draw, x, ground, x, labelY, 256);
                    DrawLine(draw, x, labelY,
                        x + Math.Max(3.0,
                            0.5 + Clean(node.NodeNo).Length * 0.72),
                        labelY, 256);
                    DrawText(draw, x + 0.5, labelY + 0.4,
                        node.NodeNo, MTextHeight, 0.0, 7, false);
                }
                DrawText(draw,
                    (_layout.DataLeft + _layout.DataRight) / 2.0,
                    -2.5, "污水管纵断面图", 1.0, 0.0, 5, true);
                double titleCenter = (_layout.DataLeft
                    + _layout.DataRight) / 2.0;
                DrawLine(draw, titleCenter - 2.6929134, -3.9,
                    titleCenter + 2.6929134, -3.9, 7,
                    LineWeight.LineWeight060);
                DrawLine(draw, titleCenter - 2.6929134, -4.4,
                    titleCenter + 2.6929134, -4.4, 7);
                DrawText(draw, 1.65, _layout.TableTop + 2.9,
                    "横 1 : " + _settings.HorizontalScale.ToString(
                        "0", CultureInfo.InvariantCulture),
                    MTextHeight, 0.0, 4, false);
                DrawText(draw, 0.4, _layout.TableTop + 4.15,
                    "纵 1 : " + _settings.VerticalScale.ToString(
                        "0", CultureInfo.InvariantCulture),
                    MTextHeight, Math.PI / 2.0, 4, false);
            }

            private void DrawRectangle(WorldDraw draw,
                double left, double bottom, double right, double top,
                short color)
            {
                DrawPolyline(draw, new[]
                {
                    new Point2d(left, bottom),
                    new Point2d(right, bottom),
                    new Point2d(right, top),
                    new Point2d(left, top)
                }, true, color, 0.0, LineWeight.ByLayer);
            }

            private void DrawLine(WorldDraw draw,
                double x1, double y1, double x2, double y2,
                short color = 256,
                LineWeight lineWeight = LineWeight.ByLayer)
            {
                using (var line = new Line(At(x1, y1), At(x2, y2)))
                {
                    line.ColorIndex = color;
                    line.LineWeight = lineWeight;
                    draw.Geometry.Draw(line);
                }
            }

            private void DrawPolyline(WorldDraw draw,
                IList<Point2d> points, bool closed, short color,
                double constantWidth, LineWeight lineWeight)
            {
                if (points == null || points.Count < 2) return;
                using (var polyline =
                    new Autodesk.AutoCAD.DatabaseServices.Polyline())
                {
                    polyline.ColorIndex = color;
                    polyline.LineWeight = lineWeight;
                    polyline.Elevation = _position.Z;
                    polyline.ConstantWidth = constantWidth;
                    for (int i = 0; i < points.Count; i++)
                    {
                        Point3d point = At(points[i].X, points[i].Y);
                        polyline.AddVertexAt(i,
                            new Point2d(point.X, point.Y), 0.0,
                            constantWidth, constantWidth);
                    }
                    polyline.Closed = closed;
                    draw.Geometry.Draw(polyline);
                }
            }

            private void DrawCircle(WorldDraw draw, double x, double y,
                double radius, short color)
            {
                if (radius <= 0.0) return;
                using (var circle = new Circle(At(x, y),
                    Vector3d.ZAxis, radius))
                {
                    circle.ColorIndex = color;
                    circle.LineWeight = LineWeight.ByLayer;
                    draw.Geometry.Draw(circle);
                }
            }

            private void DrawText(WorldDraw draw, double x, double y,
                string value, double height, double rotation,
                short color, bool centered)
            {
                string text = Clean(value).Replace("\\P", " ")
                    .Replace("\r", " ").Replace("\n", " ");
                if (text.Length == 0 || height <= 0.0) return;
                double width = Math.Max(height * 0.55,
                    text.Length * height * 0.55);
                Vector3d direction = new Vector3d(Math.Cos(rotation),
                    Math.Sin(rotation), 0.0);
                Vector3d normal = new Vector3d(-Math.Sin(rotation),
                    Math.Cos(rotation), 0.0);
                Point3d basePoint = At(x, y)
                    - direction * (centered ? width / 2.0 : 0.0)
                    - normal * (height * 0.35);
                using (var textEntity = new DBText())
                {
                    if (_database != null)
                    {
                        try { textEntity.SetDatabaseDefaults(_database); }
                        catch { }
                    }
                    textEntity.TextString = text;
                    textEntity.Height = height;
                    textEntity.Position = basePoint;
                    textEntity.Rotation = rotation;
                    textEntity.ColorIndex = color;
                    if (!_textStyleId.IsNull)
                        textEntity.TextStyleId = _textStyleId;
                    draw.Geometry.Draw(textEntity);
                }
            }

            private Point3d At(double x, double y)
            {
                return new Point3d(_position.X + x,
                    _position.Y + y, _position.Z);
            }
        }
    }
}
