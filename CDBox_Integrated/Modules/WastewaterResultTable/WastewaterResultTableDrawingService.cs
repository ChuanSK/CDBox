using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using TCPipeAutoDraw.Core.Cad;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace TCPipeAutoDraw.Modules.WastewaterResultTable
{
    internal static class WastewaterResultTableDrawingService
    {
        private const string PreferredTextStyle = "FS";
        private const double RowHeight = 6.0;
        private const double HeaderHeight = 6.0;
        private const double HeaderTextHeight = 1.75;
        private const double DataTextHeight = 1.5;
        public static void Run(Document document)
        {
            if (document == null) return;
            Editor editor = document.Editor;
            ObjectId[] selectedIds = SelectRange(document);
            if (selectedIds == null || selectedIds.Length == 0) return;

            IList<WastewaterResultTableRow> rows = ReadRows(document,
                selectedIds);
            if (rows.Count == 0)
            {
                editor.WriteHudMessage(
                    "\n[污水管成果表] 选择范围内没有带属性的井对象。");
                return;
            }

            ObjectId textStyleId = ResolveTextStyle(document.Database);
            var jig = new WastewaterResultTablePlacementJig(rows,
                textStyleId);
            PromptResult placement = editor.DragWithHud(jig,
                "移动成果表预览，单击确定左下角插入点");
            if (placement.Status != PromptStatus.OK) return;

            Draw(document.Database, jig.Position, rows);
            editor.WriteHudMessage("\n[污水管成果表] 已生成，井数量："
                + rows.Count + "。");
        }

        internal static IList<WastewaterResultTableRow> ReadRows(
            Document document)
        {
            return ReadRows(document, QuantityPipeAttributeService
                .FindObjectsWithSavedAttributes(document));
        }

        internal static IList<WastewaterResultTableRow> ReadRows(
            Document document, IEnumerable<ObjectId> objectIds)
        {
            var rows = new List<WastewaterResultTableRow>();
            Database database = document.Database;
            using (Transaction transaction = database.TransactionManager
                .StartTransaction())
            {
                foreach (ObjectId id in objectIds)
                {
                    if (id.IsNull || id.IsErased) continue;
                    QuantityPipeAttributes attributes;
                    if (!QuantityPipeAttributeService.TryReadSavedAttributes(
                            database, transaction, id, out attributes)
                        || attributes == null
                        || !QuantityPipeAttributes.IsNodeKind(
                            attributes.ObjectKind))
                        continue;

                    Entity entity = transaction.GetObject(id,
                        OpenMode.ForRead, false) as Entity;
                    Point3d center;
                    if (entity == null || !TryGetWellCenter(entity, out center))
                        continue;
                    rows.Add(new WastewaterResultTableRow
                    {
                        NodeNo = attributes.NodeNo ?? string.Empty,
                        PositionX = center.X,
                        PositionY = center.Y,
                        GroundElevation = attributes.GroundElevation,
                        WellDepth = attributes.WellDepth,
                        WellSpec = attributes.WellSpec ?? string.Empty
                    });
                }
                transaction.Commit();
            }
            return WastewaterResultTableFormatter.SortRows(rows);
        }

        private static ObjectId[] SelectRange(Document document)
        {
            Editor editor = document.Editor;
            try
            {
                PromptSelectionResult implied = editor.SelectImplied();
                if (implied.Status == PromptStatus.OK
                    && implied.Value != null && implied.Value.Count > 0)
                {
                    ObjectId[] ids = implied.Value.GetObjectIds();
                    editor.SetImpliedSelection(new ObjectId[0]);
                    return ids;
                }
            }
            catch
            {
            }

            var options = new PromptSelectionOptions
            {
                MessageForAdding =
                    "\n框选或点选需要生成成果表的井对象：",
                MessageForRemoval = "\n移除不需要生成的对象："
            };
            PromptSelectionResult selected = editor.GetHudSelection(options);
            return selected.Status == PromptStatus.OK
                   && selected.Value != null
                ? selected.Value.GetObjectIds()
                : new ObjectId[0];
        }

        private static bool TryGetWellCenter(Entity entity,
            out Point3d center)
        {
            BlockReference block = entity as BlockReference;
            if (block != null)
            {
                center = block.Position;
                return true;
            }
            DBPoint point = entity as DBPoint;
            if (point != null)
            {
                center = point.Position;
                return true;
            }
            Circle circle = entity as Circle;
            if (circle != null)
            {
                center = circle.Center;
                return true;
            }
            try
            {
                Extents3d extents = entity.GeometricExtents;
                center = new Point3d(
                    (extents.MinPoint.X + extents.MaxPoint.X) / 2.0,
                    (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0,
                    (extents.MinPoint.Z + extents.MaxPoint.Z) / 2.0);
                return true;
            }
            catch
            {
                center = Point3d.Origin;
                return false;
            }
        }

        private static void Draw(Database database, Point3d origin,
            IList<WastewaterResultTableRow> rows)
        {
            using (Transaction transaction = database.TransactionManager
                .StartTransaction())
            {
                ObjectId layerId = EnsureLayer(database, transaction);
                ObjectId textStyleId = ResolveTextStyle(database, transaction);
                BlockTableRecord space = transaction.GetObject(
                    database.CurrentSpaceId, OpenMode.ForWrite, false)
                    as BlockTableRecord;
                if (space == null)
                    throw new InvalidOperationException("无法打开当前绘图空间。");

                double dataHeight = rows.Count * RowHeight;
                DrawGrid(space, transaction, origin, dataHeight, layerId);
                DrawHeaders(space, transaction, origin, dataHeight, layerId,
                    textStyleId);
                DrawData(space, transaction, origin, dataHeight, rows,
                    layerId, textStyleId);
                transaction.Commit();
            }
        }

        private static void DrawGrid(BlockTableRecord space,
            Transaction transaction, Point3d origin, double dataHeight,
            ObjectId layerId)
        {
            double top = dataHeight + HeaderHeight;
            for (int row = 0; row <= (int)Math.Round(
                     dataHeight / RowHeight); row++)
            {
                double y = row * RowHeight;
                AddLine(space, transaction, origin, 0.0, y, 80.0, y,
                    layerId);
            }
            AddLine(space, transaction, origin, 0.0, top, 80.0, top,
                layerId);

            double[] fullVerticals = { 0.0, 6.0, 18.0, 42.0, 54.0, 64.0, 80.0 };
            foreach (double x in fullVerticals)
                AddLine(space, transaction, origin, x, 0.0, x, top,
                    layerId);
            AddLine(space, transaction, origin, 30.0, 0.0, 30.0,
                dataHeight + HeaderHeight / 2.0, layerId);
            AddLine(space, transaction, origin, 18.0,
                dataHeight + HeaderHeight / 2.0, 42.0,
                dataHeight + HeaderHeight / 2.0, layerId);
        }

        private static void DrawHeaders(BlockTableRecord space,
            Transaction transaction, Point3d origin, double dataHeight,
            ObjectId layerId, ObjectId textStyleId)
        {
            double middle = dataHeight + HeaderHeight / 2.0;
            AddHeaderText(space, transaction, origin, 3.0, middle,
                "序号", layerId, textStyleId);
            AddHeaderText(space, transaction, origin, 12.0, middle,
                "井编号", layerId, textStyleId);
            AddHeaderText(space, transaction, origin, 30.0,
                dataHeight + HeaderHeight * 0.75, "井坐标(m)", layerId,
                textStyleId);
            AddHeaderText(space, transaction, origin, 24.0,
                dataHeight + HeaderHeight * 0.25, "横坐标Y", layerId,
                textStyleId);
            AddHeaderText(space, transaction, origin, 36.0,
                dataHeight + HeaderHeight * 0.25, "纵坐标X", layerId,
                textStyleId);
            AddHeaderText(space, transaction, origin, 48.0, middle,
                "井底标高(m)", layerId, textStyleId);
            AddHeaderText(space, transaction, origin, 59.0, middle,
                "井深(m)", layerId, textStyleId);
            AddHeaderText(space, transaction, origin, 72.0, middle,
                "规格(mm)", layerId, textStyleId);
        }

        private static void DrawData(BlockTableRecord space,
            Transaction transaction, Point3d origin, double dataHeight,
            IList<WastewaterResultTableRow> rows, ObjectId layerId,
            ObjectId textStyleId)
        {
            double[] centers = { 3.0, 12.0, 24.0, 36.0, 48.0, 59.0, 72.0 };
            for (int index = 0; index < rows.Count; index++)
            {
                WastewaterResultTableRow row = rows[index];
                double y = dataHeight - (index + 0.5) * RowHeight;
                string[] values = BuildDataValues(row, index);
                for (int column = 0; column < values.Length; column++)
                    AddDataText(space, transaction, origin, centers[column],
                        y, values[column], layerId, textStyleId);
            }
        }

        private static void AddLine(BlockTableRecord space,
            Transaction transaction, Point3d origin, double x1, double y1,
            double x2, double y2, ObjectId layerId)
        {
            var line = new Line(new Point3d(origin.X + x1,
                    origin.Y + y1, origin.Z),
                new Point3d(origin.X + x2, origin.Y + y2, origin.Z))
            {
                LayerId = layerId,
                Color = Color.FromColorIndex(ColorMethod.ByBlock, 0),
                LineWeight = LineWeight.ByLayer
            };
            space.AppendEntity(line);
            transaction.AddNewlyCreatedDBObject(line, true);
        }

        private static void AddHeaderText(BlockTableRecord space,
            Transaction transaction, Point3d origin, double x, double y,
            string value, ObjectId layerId, ObjectId textStyleId)
        {
            Point3d point = new Point3d(origin.X + x, origin.Y + y,
                origin.Z);
            var text = new DBText
            {
                Position = point,
                TextString = value ?? string.Empty,
                Height = HeaderTextHeight,
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                LayerId = layerId,
                TextStyleId = textStyleId,
                Color = Color.FromColorIndex(ColorMethod.ByBlock, 0),
                LineWeight = LineWeight.ByBlock
            };
            text.AlignmentPoint = point;
            space.AppendEntity(text);
            transaction.AddNewlyCreatedDBObject(text, true);
            text.AdjustAlignment(space.Database);
        }

        private static void AddDataText(BlockTableRecord space,
            Transaction transaction, Point3d origin, double x, double y,
            string value, ObjectId layerId, ObjectId textStyleId)
        {
            var text = new MText
            {
                Location = new Point3d(origin.X + x, origin.Y + y,
                    origin.Z),
                Contents = "{\\W0.8;" + EscapeMText(value) + "}",
                TextHeight = DataTextHeight,
                Attachment = AttachmentPoint.MiddleCenter,
                LayerId = layerId,
                TextStyleId = textStyleId,
                Color = Color.FromColorIndex(ColorMethod.ByBlock, 0),
                LineWeight = LineWeight.ByLayer
            };
            space.AppendEntity(text);
            transaction.AddNewlyCreatedDBObject(text, true);
        }

        private static string EscapeMText(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\")
                .Replace("{", "\\{").Replace("}", "\\}");
        }

        private static ObjectId EnsureLayer(Database database,
            Transaction transaction)
        {
            return CadLayerService.EnsureGeneratedLayer(database, transaction,
                WastewaterResultTableDefaults.EntityLayerName, 7);
        }

        private static ObjectId ResolveTextStyle(Database database,
            Transaction transaction)
        {
            TextStyleTable table = transaction.GetObject(
                database.TextStyleTableId, OpenMode.ForRead, false)
                as TextStyleTable;
            return table != null && table.Has(PreferredTextStyle)
                ? table[PreferredTextStyle]
                : database.Textstyle;
        }

        private static ObjectId ResolveTextStyle(Database database)
        {
            using (Transaction transaction = database.TransactionManager
                .StartOpenCloseTransaction())
            {
                ObjectId result = ResolveTextStyle(database, transaction);
                transaction.Commit();
                return result;
            }
        }

        private sealed class WastewaterResultTablePlacementJig : DrawJig
        {
            private readonly IList<WastewaterResultTableRow> _rows;
            private readonly ObjectId _textStyleId;
            private Point3d _position;

            public WastewaterResultTablePlacementJig(
                IList<WastewaterResultTableRow> rows,
                ObjectId textStyleId)
            {
                _rows = rows ?? new List<WastewaterResultTableRow>();
                _textStyleId = textStyleId;
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
                double dataHeight = _rows.Count * RowHeight;
                DrawPreviewGrid(draw, dataHeight);
                DrawPreviewHeaders(draw, dataHeight);
                DrawPreviewData(draw, dataHeight);
                return true;
            }

            private void DrawPreviewGrid(WorldDraw draw, double dataHeight)
            {
                double top = dataHeight + HeaderHeight;
                for (int row = 0; row <= _rows.Count; row++)
                {
                    double y = row * RowHeight;
                    DrawPreviewLine(draw, 0.0, y, 80.0, y);
                }
                DrawPreviewLine(draw, 0.0, top, 80.0, top);

                double[] fullVerticals =
                    { 0.0, 6.0, 18.0, 42.0, 54.0, 64.0, 80.0 };
                foreach (double x in fullVerticals)
                    DrawPreviewLine(draw, x, 0.0, x, top);
                DrawPreviewLine(draw, 30.0, 0.0, 30.0,
                    dataHeight + HeaderHeight / 2.0);
                DrawPreviewLine(draw, 18.0,
                    dataHeight + HeaderHeight / 2.0, 42.0,
                    dataHeight + HeaderHeight / 2.0);
            }

            private void DrawPreviewHeaders(WorldDraw draw,
                double dataHeight)
            {
                double middle = dataHeight + HeaderHeight / 2.0;
                DrawPreviewText(draw, 3.0, middle, "序号",
                    HeaderTextHeight, false);
                DrawPreviewText(draw, 12.0, middle, "井编号",
                    HeaderTextHeight, false);
                DrawPreviewText(draw, 30.0,
                    dataHeight + HeaderHeight * 0.75, "井坐标(m)",
                    HeaderTextHeight, false);
                DrawPreviewText(draw, 24.0,
                    dataHeight + HeaderHeight * 0.25, "横坐标Y",
                    HeaderTextHeight, false);
                DrawPreviewText(draw, 36.0,
                    dataHeight + HeaderHeight * 0.25, "纵坐标X",
                    HeaderTextHeight, false);
                DrawPreviewText(draw, 48.0, middle, "井底标高(m)",
                    HeaderTextHeight, false);
                DrawPreviewText(draw, 59.0, middle, "井深(m)",
                    HeaderTextHeight, false);
                DrawPreviewText(draw, 72.0, middle, "规格(mm)",
                    HeaderTextHeight, false);
            }

            private void DrawPreviewData(WorldDraw draw, double dataHeight)
            {
                double[] centers =
                    { 3.0, 12.0, 24.0, 36.0, 48.0, 59.0, 72.0 };
                for (int index = 0; index < _rows.Count; index++)
                {
                    WastewaterResultTableRow row = _rows[index];
                    double y = dataHeight - (index + 0.5) * RowHeight;
                    string[] values = BuildDataValues(row, index);
                    for (int column = 0; column < values.Length; column++)
                        DrawPreviewText(draw, centers[column], y,
                            values[column], DataTextHeight, true);
                }
            }

            private void DrawPreviewLine(WorldDraw draw, double x1,
                double y1, double x2, double y2)
            {
                using (var line = new Line(
                           At(_position, x1, y1),
                           At(_position, x2, y2)))
                {
                    line.ColorIndex = 7;
                    draw.Geometry.Draw(line);
                }
            }

            private void DrawPreviewText(WorldDraw draw, double x, double y,
                string value, double height, bool width08)
            {
                using (var text = new MText
                       {
                           Location = At(_position, x, y),
                           Contents = width08
                               ? "{\\W0.8;" + EscapeMText(value) + "}"
                               : EscapeMText(value),
                           TextHeight = height,
                           Attachment = AttachmentPoint.MiddleCenter,
                           ColorIndex = 7
                       })
                {
                    if (!_textStyleId.IsNull)
                        text.TextStyleId = _textStyleId;
                    draw.Geometry.Draw(text);
                }
            }
        }

        private static string[] BuildDataValues(
            WastewaterResultTableRow row, int index)
        {
            return new[]
            {
                (index + 1).ToString(),
                row.NodeNo ?? string.Empty,
                WastewaterResultTableFormatter.Coordinate(row.PositionX),
                WastewaterResultTableFormatter.Coordinate(row.PositionY),
                WastewaterResultTableFormatter.Elevation(
                    row.BottomElevation),
                WastewaterResultTableFormatter.Depth(row.WellDepth),
                WastewaterResultTableFormatter.Diameter(row.WellSpec)
            };
        }

        private static Point3d At(Point3d origin, double x, double y)
        {
            return new Point3d(origin.X + x, origin.Y + y, origin.Z);
        }
    }
}
