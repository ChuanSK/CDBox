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
            PromptResult prompt = document.Editor.Drag(jig);
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
                    settings.Style("坐标网格");
                LongitudinalProfileEntityStyle headerStyle =
                    settings.Style("表头");
                LongitudinalProfileEntityStyle rowStyle =
                    settings.Style("表头栏");

                try
                {
                    DrawTable(database, transaction, profile, settings,
                        layout, insertionPoint, headerTextStyle,
                        headerStyle, rowStyle, result);
                }
                catch (System.Exception ex)
                {
                    throw new InvalidOperationException(
                        "绘制纵断面数据表失败：" + ex.Message, ex);
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
                        "绘制纵断面图面失败：" + ex.Message, ex);
                }
                try
                {
                    DrawScaleMark(database, transaction, settings, layout,
                        insertionPoint, headerTextStyle, result);
                }
                catch (System.Exception ex)
                {
                    throw new InvalidOperationException(
                        "绘制纵断面比例标识失败：" + ex.Message, ex);
                }
                transaction.Commit();
            }
            result.Message = "纵断面图已生成。";
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
                layout.TableBottom, layout.PlotRight, layout.TableTop,
                settings.LayerName, rowStyle, result);

            foreach (LongitudinalProfileRowLayout row in layout.Rows)
            {
                if (row.Bottom > layout.TableBottom + 1e-8)
                {
                    AddLine(db, tr, P(origin, layout.HeaderLeft, row.Bottom),
                        P(origin, layout.HeaderRight, row.Bottom),
                        settings.LayerName, rowStyle, result);
                    AddLine(db, tr, P(origin, layout.DataLeft, row.Bottom),
                        P(origin, layout.PlotRight, row.Bottom),
                        settings.LayerName, rowStyle, result);
                }
                TextHorizontalMode headerMode =
                    TextHorizontalMode.TextCenter;
                double headerX =
                    (layout.HeaderLeft + layout.HeaderRight) / 2.0;
                if (settings.HeaderTextAlignment.IndexOf("左",
                    StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    headerMode = TextHorizontalMode.TextLeft;
                    headerX = layout.HeaderLeft + 2.0;
                }
                else if (settings.HeaderTextAlignment.IndexOf("右",
                    StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    headerMode = TextHorizontalMode.TextRight;
                    headerX = layout.HeaderRight - 2.0;
                }
                AddText(db, tr, P(origin, headerX, row.Center),
                    row.Settings.Name,
                    Math.Min(settings.HeaderTextHeight,
                        row.Settings.Height * 0.65),
                    settings.HeaderTextColorIndex,
                    headerTextStyle, 0.0, settings.LayerName, result,
                    headerMode);
            }

            foreach (LongitudinalProfileNodeData node in profile.Nodes)
            {
                double x = layout.X(node.CumulativeDistance);
                AddLine(db, tr, P(origin, x, layout.TableBottom),
                    P(origin, x, layout.TableTop), settings.LayerName,
                    rowStyle, result);
            }

            string elevationFormat =
                "F" + settings.ElevationDecimals.ToString(
                    CultureInfo.InvariantCulture);
            string valueFormat =
                "F" + settings.ValueDecimals.ToString(
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
                node => node.PipeBottomDepth.ToString(valueFormat,
                    CultureInfo.InvariantCulture), result);
            DrawNodeValues(db, tr, profile, settings, layout, origin,
                layout.Row("WellDepth"),
                node => node.WellDepth.ToString(valueFormat,
                    CultureInfo.InvariantCulture), result);

            LongitudinalProfileRowLayout pipeRow =
                layout.Row("DiameterSlope");
            LongitudinalProfileRowLayout distanceRow =
                layout.Row("PlanDistance");
            string slopeFormat =
                "F" + settings.SlopeDecimals.ToString(
                    CultureInfo.InvariantCulture);
            for (int i = 0; i < profile.Spans.Count; i++)
            {
                LongitudinalProfileSpanData span = profile.Spans[i];
                double x1 = layout.X(
                    profile.Nodes[i].CumulativeDistance);
                double x2 = layout.X(
                    profile.Nodes[i + 1].CumulativeDistance);
                double x = (x1 + x2) / 2.0;
                double textHeight =
                    Math.Min(pipeRow.Settings.TextHeight,
                        Math.Max(1.0, (x2 - x1) / 8.0));
                string diameter = string.IsNullOrWhiteSpace(span.Diameter)
                    ? "DN" : span.Diameter;
                AddText(db, tr, P(origin, x,
                        pipeRow.Center + textHeight * 0.65),
                    diameter, textHeight, settings.HeaderTextColorIndex,
                    ResolveTextStyle(db,
                        pipeRow.Settings.TextStyleName, tr),
                    0.0, settings.LayerName, result);
                AddText(db, tr, P(origin, x,
                        pipeRow.Center - textHeight * 0.65),
                    "i=" + Math.Abs(span.SlopePermille).ToString(
                        slopeFormat, CultureInfo.InvariantCulture) + "‰",
                    textHeight, settings.HeaderTextColorIndex,
                    ResolveTextStyle(db,
                        pipeRow.Settings.TextStyleName, tr),
                    0.0, settings.LayerName, result);
                AddText(db, tr, P(origin, x, distanceRow.Center),
                    "L=" + span.PlanLength.ToString(valueFormat,
                        CultureInfo.InvariantCulture),
                    distanceRow.Settings.TextHeight,
                    settings.HeaderTextColorIndex,
                    ResolveTextStyle(db,
                        distanceRow.Settings.TextStyleName, tr),
                    0.0, settings.LayerName, result);
            }

            LongitudinalProfileRowLayout numberRow =
                layout.Row("WellNumber");
            for (int i = 0; i < profile.Nodes.Count; i++)
            {
                LongitudinalProfileNodeData node = profile.Nodes[i];
                AddText(db, tr, P(origin,
                        layout.X(node.CumulativeDistance),
                        numberRow.Center),
                    node.NodeNo, numberRow.Settings.TextHeight,
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
                    value(node), row.Settings.TextHeight,
                    settings.HeaderTextColorIndex, textStyle,
                    Math.PI / 2.0, settings.LayerName, result);
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
                AddLine(db, tr, P(origin, layout.PlotLeft, y),
                    P(origin, layout.PlotRight, y),
                    settings.LayerName, gridStyle, result);
                AddText(db, tr, P(origin,
                        layout.StaffLeft - settings.HeaderChartGap,
                        y),
                    elevation.ToString(
                        "F" + settings.ElevationDecimals,
                        CultureInfo.InvariantCulture),
                    2.1, 7, textStyle, 0.0, settings.LayerName, result,
                    TextHorizontalMode.TextRight);
                elevation += settings.ElevationGridInterval;
            }

            double totalDistance =
                profile.Nodes.Last().CumulativeDistance;
            double distance = 0.0;
            guard = 0;
            while (distance <= totalDistance + 1e-8
                && guard++ < 10000)
            {
                double x = layout.X(distance);
                AddLine(db, tr, P(origin, x, layout.ChartBottom),
                    P(origin, x, layout.ChartTop), settings.LayerName,
                    gridStyle, result);
                distance += settings.HorizontalGridInterval;
            }
            if (Math.Abs(layout.X(totalDistance) - layout.PlotRight) > 1e-7)
            {
                AddLine(db, tr,
                    P(origin, layout.PlotRight, layout.ChartBottom),
                    P(origin, layout.PlotRight, layout.ChartTop),
                    settings.LayerName, gridStyle, result);
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
                settings.Style("表头栏");
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
            var invert = new List<Point3d>();
            foreach (LongitudinalProfileNodeData node in profile.Nodes)
            {
                double x = layout.X(node.CumulativeDistance);
                ground.Add(P(origin, x, layout.Y(node.GroundElevation)));
                invert.Add(P(origin, x,
                    layout.Y(node.DesignInvertElevation)));
                AddLine(db, tr, P(origin, x, layout.ChartBottom),
                    P(origin, x,
                        layout.Y(node.DesignInvertElevation)),
                    settings.LayerName, null, result, GuideColor,
                    "DASHED");
                AddLine(db, tr,
                    P(origin, x,
                        layout.Y(node.DesignInvertElevation)),
                    P(origin, x, layout.Y(node.GroundElevation)),
                    settings.LayerName, null, result, GroundColor);
                AddSquare(db, tr,
                    P(origin, x,
                        layout.Y(node.DesignInvertElevation)),
                    1.5, settings.LayerName, GroundColor, result);
                AddText(db, tr,
                    P(origin, x, layout.ChartTop + 3.0),
                    node.NodeNo, 2.5, 7, textStyle, 0.0,
                    settings.LayerName, result);
            }
            AddPolyline(db, tr, ground, settings.LayerName,
                GroundColor, result);
            AddPolyline(db, tr, invert, settings.LayerName,
                PipeColor, result);

            for (int i = 0; i < profile.Spans.Count; i++)
            {
                LongitudinalProfileSpanData span = profile.Spans[i];
                LongitudinalProfileNodeData from = profile.Nodes[i];
                LongitudinalProfileNodeData to = profile.Nodes[i + 1];
                double diameter = Math.Max(0.01, span.OuterDiameter);
                AddLine(db, tr,
                    P(origin, layout.X(from.CumulativeDistance),
                        layout.Y(from.DesignInvertElevation + diameter)),
                    P(origin, layout.X(to.CumulativeDistance),
                        layout.Y(to.DesignInvertElevation + diameter)),
                    settings.LayerName, null, result, PipeColor);
            }
        }

        private static void DrawScaleMark(
            Database db, Transaction tr,
            LongitudinalProfileSettings settings,
            LongitudinalProfileLayout layout, Point3d origin,
            ObjectId textStyle,
            LongitudinalProfileDrawingResult result)
        {
            double x = layout.HeaderLeft + 2.0;
            double y = layout.TableTop + 12.0;
            AddLine(db, tr, P(origin, x, y),
                P(origin, x + 10.0, y), settings.LayerName,
                null, result, ScaleColor);
            AddLine(db, tr, P(origin, x, y),
                P(origin, x, y + 10.0), settings.LayerName,
                null, result, 6);
            AddText(db, tr, P(origin, x + 5.0, y - 2.0),
                "横 1:" + settings.HorizontalScale.ToString("0",
                    CultureInfo.InvariantCulture),
                1.8, ScaleColor, textStyle, 0.0,
                settings.LayerName, result);
            AddText(db, tr, P(origin, x - 2.0, y + 5.0),
                "纵 1:" + settings.VerticalScale.ToString("0",
                    CultureInfo.InvariantCulture),
                1.8, 6, textStyle, Math.PI / 2.0,
                settings.LayerName, result);
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
            // AutoCAD 仅在非默认对齐方式已经生效后才接受
            // AlignmentPoint；反向设置会在正式落图时抛出
            // Autodesk.AutoCAD.Runtime.Exception(eNotApplicable)。
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
            // Solid 的第三、第四点顺序与折线不同。
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
            var options = new JigPromptPointOptions(
                "\n请选择纵断面图左下角插入点：")
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
            DrawRectangle(draw, 0, 0, _settings.HeaderWidth,
                _layout.TableTop);
            DrawRectangle(draw, _layout.DataLeft, 0,
                _layout.PlotRight, _layout.TableTop);
            DrawRectangle(draw, _layout.PlotLeft, _layout.ChartBottom,
                _layout.PlotRight, _layout.ChartTop);
            foreach (LongitudinalProfileRowLayout row in _layout.Rows)
            {
                DrawLine(draw, 0, row.Bottom,
                    _settings.HeaderWidth, row.Bottom);
                DrawLine(draw, _layout.DataLeft, row.Bottom,
                    _layout.PlotRight, row.Bottom);
                DrawText(draw, new Point3d(
                        _position.X + _settings.HeaderWidth / 2.0,
                        _position.Y + row.Center, _position.Z),
                    row.Settings.Name, row.Settings.TextHeight);
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
