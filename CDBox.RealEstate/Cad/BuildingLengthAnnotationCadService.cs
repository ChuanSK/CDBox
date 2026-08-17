using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CDBox.RealEstate.Geometry;
using CDBox.RealEstate.Settings;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcadColor = Autodesk.AutoCAD.Colors.Color;

namespace CDBox.RealEstate.Cad
{
    internal sealed class BuildingLengthAnnotationCadService
    {
        private const string AnnotationLayerName =
            "CDBox-不动产-建筑边长注记";
        private readonly ICDBoxPromptService _prompts;
        private readonly ICDBoxNotificationService _notifications;
        private readonly ICDBoxLogger _logger;

        public BuildingLengthAnnotationCadService(
            ICDBoxPromptService prompts,
            ICDBoxNotificationService notifications,
            ICDBoxLogger logger)
        {
            _prompts = prompts ?? throw new ArgumentNullException("prompts");
            _notifications = notifications
                ?? throw new ArgumentNullException("notifications");
            _logger = logger ?? throw new ArgumentNullException("logger");
        }

        public void Execute()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                Notify("当前没有可用的 CAD 图纸。",
                    CDBoxNotificationLevel.Warning);
                return;
            }

            PromptEntityResult selected;
            var options = new PromptEntityOptions("\n ");
            options.SetRejectMessage("\n请选择闭合的二维多段线。\n");
            options.AddAllowedClass(typeof(Polyline), true);
            using (_prompts.Begin("选择建筑物",
                "请选择建筑物闭合复合线；按 Esc 取消。"))
                selected = document.Editor.GetEntity(options);
            if (selected.Status != PromptStatus.OK) return;

            try
            {
                BuildingLengthAnnotationSettings settings =
                    BuildingLengthAnnotationSettingsStore.Load();
                BuildingAnnotationPlan plan;
                double elevation;
                using (DocumentLock documentLock = document.LockDocument())
                {
                    ReadPlan(document.Database, selected.ObjectId, settings,
                        out plan, out elevation);
                    WritePlan(document.Database, plan, elevation, settings);
                }

                string summary = "已注记 " + plan.BoundarySegments.Count
                    + " 条建筑外边";
                if (plan.AuxiliarySegments.Count > 0)
                    summary += "，并生成 " + plan.AuxiliarySegments.Count
                        + " 条面积计算辅助线";
                summary += "；统一文字高度 "
                    + plan.TextHeight.ToString("0.###") + "。";
                Notify(summary, CDBoxNotificationLevel.Success);
                if (!string.IsNullOrWhiteSpace(plan.Warning))
                    Notify(plan.Warning, CDBoxNotificationLevel.Warning);
            }
            catch (Exception ex)
            {
                _logger.Error("建筑物边长注记失败。", ex);
                Notify("建筑物边长注记失败：" + ex.Message,
                    CDBoxNotificationLevel.Error);
            }
        }

        public BuildingAnnotationCadCatalog ReadCatalog()
        {
            var result = new BuildingAnnotationCadCatalog();
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                result.TextStyles.Add("Standard");
                result.Linetypes.Add("Continuous");
                return result;
            }
            try
            {
                using (Transaction transaction = document.Database
                    .TransactionManager.StartOpenCloseTransaction())
                {
                    TextStyleTable styles = transaction.GetObject(
                        document.Database.TextStyleTableId, OpenMode.ForRead,
                        false) as TextStyleTable;
                    if (styles != null)
                        foreach (ObjectId id in styles)
                        {
                            TextStyleTableRecord record = transaction.GetObject(
                                id, OpenMode.ForRead, false)
                                as TextStyleTableRecord;
                            if (record != null
                                && !string.IsNullOrWhiteSpace(record.Name))
                                result.TextStyles.Add(record.Name);
                        }

                    LinetypeTable linetypes = transaction.GetObject(
                        document.Database.LinetypeTableId, OpenMode.ForRead,
                        false) as LinetypeTable;
                    if (linetypes != null)
                        foreach (ObjectId id in linetypes)
                        {
                            LinetypeTableRecord record = transaction.GetObject(
                                id, OpenMode.ForRead, false)
                                as LinetypeTableRecord;
                            if (record != null
                                && !string.IsNullOrWhiteSpace(record.Name))
                                result.Linetypes.Add(record.Name);
                        }
                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                _logger.Warn("读取 CAD 文字样式或线型失败：" + ex.Message);
            }
            EnsureDefault(result.TextStyles, "Standard");
            EnsureDefault(result.Linetypes, "Continuous");
            return result;
        }

        private static void ReadPlan(Database database, ObjectId sourceId,
            BuildingLengthAnnotationSettings settings,
            out BuildingAnnotationPlan plan, out double elevation)
        {
            using (Transaction transaction = database.TransactionManager
                .StartOpenCloseTransaction())
            {
                Polyline polyline = transaction.GetObject(sourceId,
                    OpenMode.ForRead, false) as Polyline;
                if (polyline == null)
                    throw new InvalidOperationException(
                        "选择对象不是二维多段线。");
                if (!polyline.Closed)
                    throw new InvalidOperationException(
                        "请选择已闭合的建筑物多段线。");
                if (polyline.NumberOfVertices < 3)
                    throw new InvalidOperationException(
                        "建筑物闭合线至少需要三个顶点。");
                for (int index = 0; index < polyline.NumberOfVertices; index++)
                    if (Math.Abs(polyline.GetBulgeAt(index)) > 1e-9)
                        throw new InvalidOperationException(
                            "当前功能暂不支持含圆弧段的建筑物边界。");

                var points = new List<BuildingPoint2>(
                    polyline.NumberOfVertices);
                elevation = polyline.GetPoint3dAt(0).Z;
                for (int index = 0; index < polyline.NumberOfVertices; index++)
                {
                    Point3d point = polyline.GetPoint3dAt(index);
                    points.Add(new BuildingPoint2(point.X, point.Y));
                }
                plan = BuildingLengthAnnotationPlanner.Create(points,
                    settings.TextHeight, settings.AdaptiveTextHeight);
                transaction.Commit();
            }
        }

        private static void WritePlan(Database database,
            BuildingAnnotationPlan plan, double elevation,
            BuildingLengthAnnotationSettings settings)
        {
            using (Transaction transaction = database.TransactionManager
                .StartTransaction())
            {
                ObjectId layerId = EnsureLayer(database, transaction);
                ObjectId textStyleId = ResolveTextStyle(database, transaction,
                    settings.TextStyleName);
                ObjectId linetypeId = ResolveLinetype(database, transaction,
                    settings.AuxiliaryLinetypeName);
                BlockTableRecord space = transaction.GetObject(
                    database.CurrentSpaceId, OpenMode.ForWrite, false)
                    as BlockTableRecord;
                if (space == null)
                    throw new InvalidOperationException(
                        "无法写入当前 CAD 空间。");

                foreach (BuildingPlannedSegment segment in
                    plan.BoundarySegments.Concat(plan.AuxiliarySegments))
                    AppendText(database, transaction, space, segment,
                        elevation, plan.TextHeight, layerId, textStyleId,
                        settings.TextColor);

                LineWeight lineweight = ResolveLineweight(
                    settings.AuxiliaryLineweight);
                foreach (BuildingPlannedSegment segment in
                    plan.AuxiliarySegments)
                {
                    var line = new Line(ToPoint(segment.Start, elevation),
                        ToPoint(segment.End, elevation))
                    {
                        LayerId = layerId,
                        LineWeight = lineweight,
                        Color = ToCadColor(settings.AuxiliaryColor)
                    };
                    if (!linetypeId.IsNull) line.LinetypeId = linetypeId;
                    space.AppendEntity(line);
                    transaction.AddNewlyCreatedDBObject(line, true);
                }
                transaction.Commit();
            }
        }

        private static void AppendText(Database database,
            Transaction transaction, BlockTableRecord space,
            BuildingPlannedSegment segment, double elevation,
            double textHeight, ObjectId layerId, ObjectId textStyleId,
            CDBoxModuleColor color)
        {
            Point3d position = ToPoint(segment.TextPosition, elevation);
            var text = new DBText
            {
                TextString = segment.Text,
                Height = textHeight,
                Rotation = segment.TextRotation,
                Position = position,
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = position,
                LayerId = layerId,
                Color = ToCadColor(color)
            };
            if (!textStyleId.IsNull) text.TextStyleId = textStyleId;
            space.AppendEntity(text);
            transaction.AddNewlyCreatedDBObject(text, true);
            try { text.AdjustAlignment(database); }
            catch { }
        }

        private static ObjectId EnsureLayer(Database database,
            Transaction transaction)
        {
            LayerTable table = transaction.GetObject(database.LayerTableId,
                OpenMode.ForRead, false) as LayerTable;
            if (table == null)
                throw new InvalidOperationException("无法读取 CAD 图层表。");
            if (table.Has(AnnotationLayerName)) return table[AnnotationLayerName];
            table.UpgradeOpen();
            var layer = new LayerTableRecord
            {
                Name = AnnotationLayerName,
                Color = AcadColor.FromColorIndex(ColorMethod.ByAci, 7)
            };
            ObjectId id = table.Add(layer);
            transaction.AddNewlyCreatedDBObject(layer, true);
            return id;
        }

        private static ObjectId ResolveTextStyle(Database database,
            Transaction transaction, string name)
        {
            TextStyleTable table = transaction.GetObject(
                database.TextStyleTableId, OpenMode.ForRead, false)
                as TextStyleTable;
            if (table != null && !string.IsNullOrWhiteSpace(name)
                && table.Has(name)) return table[name];
            return database.Textstyle;
        }

        private static ObjectId ResolveLinetype(Database database,
            Transaction transaction, string name)
        {
            LinetypeTable table = transaction.GetObject(
                database.LinetypeTableId, OpenMode.ForRead, false)
                as LinetypeTable;
            if (table != null && !string.IsNullOrWhiteSpace(name)
                && table.Has(name)) return table[name];
            if (table != null && table.Has("Continuous"))
                return table["Continuous"];
            return ObjectId.Null;
        }

        private static AcadColor ToCadColor(CDBoxModuleColor value)
        {
            value = value ?? CDBoxModuleColor.FromIndex(7);
            switch (value.Type)
            {
                case CDBoxModuleColorType.ByLayer:
                    return AcadColor.FromColorIndex(ColorMethod.ByLayer, 256);
                case CDBoxModuleColorType.ByBlock:
                    return AcadColor.FromColorIndex(ColorMethod.ByBlock, 0);
                case CDBoxModuleColorType.IndexColor:
                    return AcadColor.FromColorIndex(ColorMethod.ByAci,
                        (short)Math.Max(1, Math.Min(255, value.Index)));
                case CDBoxModuleColorType.ColorBook:
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(value.BookName)
                            && !string.IsNullOrWhiteSpace(value.ColorName))
                            return AcadColor.FromNames(value.ColorName.Trim(),
                                value.BookName.Trim());
                    }
                    catch { }
                    return AcadColor.FromRgb(value.R, value.G, value.B);
                default:
                    return AcadColor.FromRgb(value.R, value.G, value.B);
            }
        }

        private static LineWeight ResolveLineweight(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "byblock": return LineWeight.ByBlock;
                case "default": return LineWeight.ByLineWeightDefault;
                case "0.05": return LineWeight.LineWeight005;
                case "0.09": return LineWeight.LineWeight009;
                case "0.13": return LineWeight.LineWeight013;
                case "0.15": return LineWeight.LineWeight015;
                case "0.18": return LineWeight.LineWeight018;
                case "0.20": return LineWeight.LineWeight020;
                case "0.25": return LineWeight.LineWeight025;
                case "0.30": return LineWeight.LineWeight030;
                case "0.35": return LineWeight.LineWeight035;
                case "0.40": return LineWeight.LineWeight040;
                case "0.50": return LineWeight.LineWeight050;
                case "0.60": return LineWeight.LineWeight060;
                case "0.70": return LineWeight.LineWeight070;
                case "0.80": return LineWeight.LineWeight080;
                case "0.90": return LineWeight.LineWeight090;
                case "1.00": return LineWeight.LineWeight100;
                default: return LineWeight.ByLayer;
            }
        }

        private static Point3d ToPoint(BuildingPoint2 point,
            double elevation)
        {
            return new Point3d(point.X, point.Y, elevation);
        }

        private static void EnsureDefault(IList<string> values,
            string value)
        {
            if (!values.Any(x => string.Equals(x, value,
                StringComparison.OrdinalIgnoreCase))) values.Add(value);
        }

        private void Notify(string message, CDBoxNotificationLevel level)
        {
            _notifications.Show("建筑物边长注记", message, level);
        }
    }
}
