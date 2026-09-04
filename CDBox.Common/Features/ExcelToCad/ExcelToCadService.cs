// Canonical implementation owned by CDBox.Common.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace TCPipeAutoDraw.Modules.ExcelToCad
{
    public sealed class ExcelToCadInsertResult
    {
        public ExcelToCadInsertResult()
        {
            ObjectId = ObjectId.Null;
            Message = string.Empty;
        }

        public ObjectId ObjectId { get; set; }
        public int EntityCount { get; set; }
        public string Message { get; set; }
    }

    public static class ExcelToCadService
    {
        public static ExcelToCadInsertResult Insert(Document document,
            ExcelTableModel model, ExcelToCadOptions sourceOptions, Point3d insertionPoint)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (model == null || model.RowCount <= 0 || model.ColumnCount <= 0)
                throw new InvalidOperationException("Excel 表格没有可转换的单元格。");
            if (sourceOptions == null) throw new ArgumentNullException("sourceOptions");
            ExcelToCadOptions options = sourceOptions.Clone();
            options.Normalize();

            Database database = document.Database;
            using (DocumentLock documentLock = document.LockDocument())
            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                ExcelToCadInsertResult result;
                switch (options.OutputType)
                {
                    case CadExcelTableOutputType.NativeTable:
                        result = InsertNativeTable(database, transaction, model, options,
                            insertionPoint);
                        break;
                    case CadExcelTableOutputType.Block:
                        result = InsertBlock(database, transaction, model, options,
                            insertionPoint);
                        break;
                    default:
                        BlockTableRecord space = transaction.GetObject(
                            database.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                        int count = DrawExplodedEntities(database, transaction, space, model,
                            options, insertionPoint);
                        result = new ExcelToCadInsertResult
                        {
                            EntityCount = count,
                            Message = "已生成分解线文字表格，共 " + count
                                + " 个 CAD 实体。"
                        };
                        break;
                }
                transaction.Commit();
                document.Editor.Regen();
                return result;
            }
        }

        private static ExcelToCadInsertResult InsertBlock(Database database,
            Transaction transaction, ExcelTableModel model, ExcelToCadOptions options,
            Point3d insertionPoint)
        {
            BlockTable blockTable = transaction.GetObject(database.BlockTableId,
                OpenMode.ForRead) as BlockTable;
            string blockName = CreateUniqueBlockName(blockTable, model.SheetName);
            var definition = new BlockTableRecord
            {
                Name = blockName
            };
            transaction.GetObject(database.BlockTableId, OpenMode.ForWrite);
            ObjectId definitionId = blockTable.Add(definition);
            transaction.AddNewlyCreatedDBObject(definition, true);
            int childCount = DrawExplodedEntities(database, transaction, definition, model,
                options, Point3d.Origin);

            BlockTableRecord space = transaction.GetObject(database.CurrentSpaceId,
                OpenMode.ForWrite) as BlockTableRecord;
            var reference = new BlockReference(insertionPoint, definitionId)
            {
                LayerId = ResolveLayerId(database, transaction,
                    options.ContentLayerName),
                Color = EntityModeColor(options)
            };
            ObjectId referenceId = space.AppendEntity(reference);
            transaction.AddNewlyCreatedDBObject(reference, true);
            return new ExcelToCadInsertResult
            {
                ObjectId = referenceId,
                EntityCount = 1,
                Message = "已生成块形式表格“" + blockName + "”，块内包含 "
                    + childCount + " 个实体。"
            };
        }

        private static ExcelToCadInsertResult InsertNativeTable(Database database,
            Transaction transaction, ExcelTableModel model, ExcelToCadOptions options,
            Point3d insertionPoint)
        {
#pragma warning disable CS0618
            Table table = new Table
            {
                Position = insertionPoint,
                LayerId = ResolveLayerId(database, transaction,
                    options.ContentLayerName),
                Color = EntityModeColor(options),
                TableStyle = database.Tablestyle
            };
            table.SetSize(model.RowCount, model.ColumnCount);
            TableGeometry geometry = BuildGeometry(model, options, Point3d.Origin);

            for (int column = 0; column < model.ColumnCount; column++)
                table.SetColumnWidth(column, geometry.ColumnWidths[column]);
            for (int row = 0; row < model.RowCount; row++)
                table.SetRowHeight(row, geometry.RowHeights[row]);

            for (int row = 0; row < model.RowCount; row++)
            {
                for (int column = 0; column < model.ColumnCount; column++)
                {
                    ExcelTableCell source = model.GetCell(row, column);
                    ExcelTableCellStyle style = source.Style ?? new ExcelTableCellStyle();
                    table.SetTextString(row, column,
                        FormatMText(source.Text, style));
                    table.SetAlignment(row, column, MapCellAlignment(style));
                    table.SetTextHeight(row, column, ResolveTextHeight(model,
                        options, style, geometry.RowHeights[row]));
                    table.SetContentColor(row, column,
                        options.PreserveTextColors
                            ? ToCadColor(style.TextColor, true)
                            : EntityModeColor(options));
                    if (options.PreserveBackgroundColors
                        && style.BackgroundColor != null)
                    {
                        table.SetBackgroundColor(row, column,
                            ToCadColor(style.BackgroundColor, false));
                        table.SetBackgroundColorNone(row, column, false);
                    }
                    else table.SetBackgroundColorNone(row, column, true);
                    if (style.RotationDegrees != 0)
                    {
                        try
                        {
                            table.SetRotation(row, column, 0,
                                DegreesToRadians(style.RotationDegrees));
                        }
                        catch { }
                    }
                    ApplyNativeCellBorders(table, row, column, model, options);
                }
            }

            if (options.PreserveMergedCells)
            {
                for (int i = 0; i < model.MergedRanges.Count; i++)
                {
                    ExcelMergedRange merge = model.MergedRanges[i];
                    table.MergeCells(CellRange.Create(table, merge.FirstRow,
                        merge.FirstColumn, merge.LastRow, merge.LastColumn));
                }
            }
            table.GenerateLayout();

            BlockTableRecord space = transaction.GetObject(database.CurrentSpaceId,
                OpenMode.ForWrite) as BlockTableRecord;
            ObjectId id = space.AppendEntity(table);
            transaction.AddNewlyCreatedDBObject(table, true);
            return new ExcelToCadInsertResult
            {
                ObjectId = id,
                EntityCount = 1,
                Message = "已生成 CAD 原生 TABLE，尺寸 "
                    + model.RowCount + " 行 × " + model.ColumnCount + " 列。"
            };
#pragma warning restore CS0618
        }

        private static int DrawExplodedEntities(Database database,
            Transaction transaction, BlockTableRecord owner, ExcelTableModel model,
            ExcelToCadOptions options, Point3d insertionPoint)
        {
            if (owner == null) throw new InvalidOperationException("无法访问当前绘图空间。");
            TableGeometry geometry = BuildGeometry(model, options, insertionPoint);
            ObjectId gridLayerId = ResolveLayerId(database, transaction,
                options.GridLayerName);
            ObjectId contentLayerId = ResolveLayerId(database, transaction,
                options.ContentLayerName);
            int count = 0;

            if (options.PreserveBackgroundColors)
            {
                for (int row = 0; row < model.RowCount; row++)
                {
                    for (int column = 0; column < model.ColumnCount; column++)
                    {
                        if (!model.IsMergedAnchor(row, column)) continue;
                        ExcelTableCell cell = model.GetEffectiveCell(row, column);
                        if (cell == null || cell.Style == null
                            || cell.Style.BackgroundColor == null) continue;
                        CellRectangle rectangle = GetCellRectangle(model, geometry, row,
                            column);
                        var solid = new Solid(
                            new Point3d(rectangle.Left, rectangle.Bottom, insertionPoint.Z),
                            new Point3d(rectangle.Right, rectangle.Bottom, insertionPoint.Z),
                            new Point3d(rectangle.Left, rectangle.Top, insertionPoint.Z),
                            new Point3d(rectangle.Right, rectangle.Top, insertionPoint.Z));
                        SetEntityLayer(solid, contentLayerId);
                        solid.Color = ToCadColor(cell.Style.BackgroundColor, false);
                        Append(owner, transaction, solid);
                        count++;
                    }
                }
            }

            if (options.DrawGridLines)
                count += DrawMergedGridLines(transaction, owner, model, geometry,
                    insertionPoint.Z, gridLayerId, options);

            for (int row = 0; row < model.RowCount; row++)
            {
                for (int column = 0; column < model.ColumnCount; column++)
                {
                    if (!model.IsMergedAnchor(row, column)) continue;
                    ExcelTableCell cell = model.GetEffectiveCell(row, column);
                    if (cell == null || string.IsNullOrEmpty(cell.Text)) continue;
                    CellRectangle rectangle = GetCellRectangle(model, geometry, row,
                        column);
                    count += DrawCellTextEntities(database, transaction, owner, model,
                        options, cell, rectangle, insertionPoint.Z, contentLayerId);
                }
            }
            return count;
        }

        private static int DrawMergedGridLines(Transaction transaction,
            BlockTableRecord owner, ExcelTableModel model, TableGeometry geometry,
            double elevation, ObjectId layerId, ExcelToCadOptions options)
        {
            int count = 0;
            for (int rowEdge = 0; rowEdge <= model.RowCount; rowEdge++)
            {
                int startColumn = -1;
                ExcelCellBorder activeBorder = null;
                for (int column = 0; column <= model.ColumnCount; column++)
                {
                    bool drawable = column < model.ColumnCount
                        && !IsInternalHorizontalMergeEdge(model, rowEdge, column);
                    ExcelCellBorder border = drawable
                        ? EffectiveGridBorder(ResolveHorizontalBorder(model, rowEdge,
                            column))
                        : null;
                    if (!drawable)
                    {
                        if (startColumn >= 0)
                        {
                            count += DrawGridLine(transaction, owner,
                                geometry.X[startColumn], geometry.Y[rowEdge],
                                geometry.X[column], geometry.Y[rowEdge], elevation,
                                activeBorder, layerId, options);
                            startColumn = -1;
                            activeBorder = null;
                        }
                    }
                    if (drawable && startColumn < 0)
                    {
                        startColumn = column;
                        activeBorder = border;
                    }
                    else if (drawable)
                    {
                        activeBorder = Stronger(activeBorder, border);
                    }
                }
            }

            for (int columnEdge = 0; columnEdge <= model.ColumnCount; columnEdge++)
            {
                int startRow = -1;
                ExcelCellBorder activeBorder = null;
                for (int row = 0; row <= model.RowCount; row++)
                {
                    bool drawable = row < model.RowCount
                        && !IsInternalVerticalMergeEdge(model, row, columnEdge);
                    ExcelCellBorder border = drawable
                        ? EffectiveGridBorder(ResolveVerticalBorder(model, row,
                            columnEdge))
                        : null;
                    if (!drawable)
                    {
                        if (startRow >= 0)
                        {
                            count += DrawGridLine(transaction, owner,
                                geometry.X[columnEdge], geometry.Y[startRow],
                                geometry.X[columnEdge], geometry.Y[row], elevation,
                                activeBorder, layerId, options);
                            startRow = -1;
                            activeBorder = null;
                        }
                    }
                    if (drawable && startRow < 0)
                    {
                        startRow = row;
                        activeBorder = border;
                    }
                    else if (drawable)
                    {
                        activeBorder = Stronger(activeBorder, border);
                    }
                }
            }

            return count;
        }

        private static int DrawGridLine(Transaction transaction,
            BlockTableRecord owner, double startX, double startY, double endX,
            double endY, double elevation, ExcelCellBorder border, ObjectId layerId,
            ExcelToCadOptions options)
        {
            if (Math.Abs(startX - endX) < 0.0000001
                && Math.Abs(startY - endY) < 0.0000001) return 0;
            var line = new Line(new Point3d(startX, startY, elevation),
                new Point3d(endX, endY, elevation));
            ApplyBorder(line, border, layerId, options);
            Append(owner, transaction, line);
            return 1;
        }

        private static ExcelCellBorder EffectiveGridBorder(ExcelCellBorder border)
        {
            return border != null && border.Visible ? border : DefaultGridBorder();
        }

        private static int DrawCellTextEntities(Database database,
            Transaction transaction, BlockTableRecord owner, ExcelTableModel model,
            ExcelToCadOptions options, ExcelTableCell cell, CellRectangle rectangle,
            double elevation, ObjectId layerId)
        {
            ExcelTableCellStyle style = cell.Style ?? new ExcelTableCellStyle();
            double baseHeight = ResolveTextHeight(model, options, style,
                rectangle.Top - rectangle.Bottom);
            double padding = Math.Max(options.TextHeight * 0.14, 0.001);
            double availableWidth = Math.Max(0.001,
                rectangle.Right - rectangle.Left - padding * 2.0);
            double availableHeight = Math.Max(0.001,
                rectangle.Top - rectangle.Bottom - padding * 2.0);
            List<string> lines = SplitDbTextLines(cell.Text, style.WrapText,
                availableWidth, baseHeight, style.Bold);
            if (lines.Count == 0) return 0;

            double linePitch = baseHeight * 1.18;
            double requiredHeight = lines.Count == 1
                ? baseHeight : baseHeight + (lines.Count - 1) * linePitch;
            if (requiredHeight > availableHeight)
            {
                double scale = availableHeight / requiredHeight;
                baseHeight *= scale;
                linePitch *= scale;
            }

            double blockHeight = lines.Count == 1
                ? baseHeight : baseHeight + (lines.Count - 1) * linePitch;
            double firstY;
            switch (style.VerticalAlignment)
            {
                case ExcelVerticalTextAlignment.Top:
                    firstY = rectangle.Top - padding - baseHeight * 0.5;
                    break;
                case ExcelVerticalTextAlignment.Bottom:
                    firstY = rectangle.Bottom + padding + blockHeight
                        - baseHeight * 0.5;
                    break;
                default:
                    firstY = (rectangle.Top + rectangle.Bottom) * 0.5
                        + blockHeight * 0.5 - baseHeight * 0.5;
                    break;
            }

            int count = 0;
            for (int index = 0; index < lines.Count; index++)
            {
                string lineText = lines[index];
                if (string.IsNullOrEmpty(lineText)) continue;
                double lineY = firstY - index * linePitch;
                string leftRun;
                string rightRun;
                if (TrySplitSpacedRuns(lineText, out leftRun, out rightRun))
                {
                    double runHeight = FitSeparatedRunHeight(leftRun, rightRun,
                        availableWidth, baseHeight, style.Bold);
                    DBText leftText = AppendCellDbText(database, transaction, owner,
                        options, style, leftRun, new Point3d(rectangle.Left + padding,
                            lineY, elevation), TextHorizontalMode.TextLeft, runHeight,
                        layerId);
                    DBText rightText = AppendCellDbText(database, transaction, owner,
                        options, style, rightRun, new Point3d(rectangle.Right - padding,
                            lineY, elevation), TextHorizontalMode.TextRight, runHeight,
                        layerId);
                    FitSeparatedDbTextPair(database, leftText, rightText, rectangle,
                        padding, Math.Max(0.001, linePitch * 0.96));
                    count += 2;
                    continue;
                }

                Point3d point = new Point3d(ResolveTextX(style, rectangle, padding),
                    lineY, elevation);
                DBText text = AppendCellDbText(database, transaction, owner, options,
                    style, lineText, point, MapDbTextHorizontalMode(style), baseHeight,
                    layerId);
                FitDbTextToCell(database, text, rectangle, padding,
                    Math.Max(0.001, linePitch * 0.96));
                count++;
            }
            return count;
        }

        private static DBText AppendCellDbText(Database database,
            Transaction transaction, BlockTableRecord owner, ExcelToCadOptions options,
            ExcelTableCellStyle style, string value, Point3d point,
            TextHorizontalMode horizontalMode, double height, ObjectId layerId)
        {
            var text = new DBText();
            try { text.SetDatabaseDefaults(database); } catch { }
            ObjectId textStyleId = ResolveExcelTextStyleId(database, transaction,
                style);
            if (!textStyleId.IsNull) text.TextStyleId = textStyleId;
            text.Height = Math.Max(0.001, height);
            text.TextString = FormatDbText(value, style);
            text.Rotation = DegreesToRadians(style.RotationDegrees);
            text.HorizontalMode = horizontalMode;
            text.VerticalMode = TextVerticalMode.TextVerticalMid;
            text.Position = point;
            text.AlignmentPoint = point;
            SetEntityLayer(text, layerId);
            text.Color = options.PreserveTextColors
                ? ToCadColor(style.TextColor, true)
                : EntityModeColor(options);
            Append(owner, transaction, text);
            try { text.AdjustAlignment(database); } catch { }
            return text;
        }

        private static ObjectId ResolveExcelTextStyleId(Database database,
            Transaction transaction, ExcelTableCellStyle style)
        {
            if (database == null || transaction == null || style == null
                || string.IsNullOrWhiteSpace(style.FontName))
                return database == null ? ObjectId.Null : database.Textstyle;
            string fontName = style.FontName.Trim();
            string styleName = "CDBox_Excel_" + SanitizeSymbolName(fontName)
                + (style.Bold ? "_B" : "_N") + (style.Italic ? "_I" : "_R");
            if (styleName.Length > 240) styleName = styleName.Substring(0, 240);
            try
            {
                TextStyleTable table = transaction.GetObject(
                    database.TextStyleTableId, OpenMode.ForRead) as TextStyleTable;
                if (table == null) return database.Textstyle;
                if (table.Has(styleName)) return table[styleName];

                TextStyleTableRecord current = transaction.GetObject(
                    database.Textstyle, OpenMode.ForRead) as TextStyleTableRecord;
                int characterSet = 0;
                int pitchAndFamily = 0;
                if (current != null)
                {
                    Autodesk.AutoCAD.GraphicsInterface.FontDescriptor currentFont =
                        current.Font;
                    characterSet = currentFont.CharacterSet;
                    pitchAndFamily = currentFont.PitchAndFamily;
                }
                var record = new TextStyleTableRecord
                {
                    Name = styleName,
                    TextSize = 0.0,
                    XScale = 1.0,
                    Font = new Autodesk.AutoCAD.GraphicsInterface.FontDescriptor(
                        fontName, style.Bold, style.Italic, characterSet,
                        pitchAndFamily)
                };
                table.UpgradeOpen();
                ObjectId id = table.Add(record);
                transaction.AddNewlyCreatedDBObject(record, true);
                return id;
            }
            catch
            {
                return database.Textstyle;
            }
        }

        private static bool TrySplitSpacedRuns(string value, out string left,
            out string right)
        {
            left = string.Empty;
            right = string.Empty;
            if (string.IsNullOrEmpty(value)) return false;
            int bestStart = -1;
            int bestLength = 0;
            int runStart = -1;
            for (int index = 0; index <= value.Length; index++)
            {
                bool whitespace = index < value.Length
                    && char.IsWhiteSpace(value[index]);
                if (whitespace)
                {
                    if (runStart < 0) runStart = index;
                    continue;
                }
                if (runStart >= 0)
                {
                    int length = index - runStart;
                    if (length > bestLength)
                    {
                        bestStart = runStart;
                        bestLength = length;
                    }
                    runStart = -1;
                }
            }
            if (bestStart < 0 || bestLength < 4) return false;
            left = value.Substring(0, bestStart).TrimEnd();
            right = value.Substring(bestStart + bestLength).TrimStart();
            return left.Length > 0 && right.Length > 0;
        }

        private static double FitSeparatedRunHeight(string left, string right,
            double availableWidth, double height, bool bold)
        {
            double combinedWidth = EstimateDbTextWidth(left, height, bold)
                + EstimateDbTextWidth(right, height, bold) + height * 0.75;
            if (combinedWidth <= availableWidth || combinedWidth <= 0.0000001)
                return height;
            return Math.Max(0.001, height * availableWidth / combinedWidth * 0.985);
        }

        private static void FitSeparatedDbTextPair(Database database,
            DBText left, DBText right, CellRectangle rectangle, double padding,
            double lineSlotHeight)
        {
            if (left == null || right == null) return;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Extents3d leftExtents;
                Extents3d rightExtents;
                try
                {
                    leftExtents = left.GeometricExtents;
                    rightExtents = right.GeometricExtents;
                }
                catch { break; }
                double gap = Math.Max(left.Height * 0.6, 0.001);
                double combinedWidth = Math.Abs(leftExtents.MaxPoint.X
                    - leftExtents.MinPoint.X)
                    + Math.Abs(rightExtents.MaxPoint.X - rightExtents.MinPoint.X)
                    + gap;
                double availableWidth = Math.Max(0.001,
                    rectangle.Right - rectangle.Left - padding * 2.0);
                double scale = combinedWidth > availableWidth
                    ? availableWidth / combinedWidth : 1.0;
                double actualHeight = Math.Max(
                    Math.Abs(leftExtents.MaxPoint.Y - leftExtents.MinPoint.Y),
                    Math.Abs(rightExtents.MaxPoint.Y - rightExtents.MinPoint.Y));
                if (actualHeight > lineSlotHeight && actualHeight > 0.0000001)
                    scale = Math.Min(scale, lineSlotHeight / actualHeight);
                if (scale >= 0.999) break;
                left.Height = Math.Max(0.001, left.Height * scale * 0.985);
                right.Height = Math.Max(0.001, right.Height * scale * 0.985);
                try { left.AdjustAlignment(database); } catch { }
                try { right.AdjustAlignment(database); } catch { }
            }
            KeepDbTextInsideCell(left, rectangle, padding);
            KeepDbTextInsideCell(right, rectangle, padding);
        }

        private static double ResolveTextX(ExcelTableCellStyle style,
            CellRectangle rectangle, double padding)
        {
            switch (style.HorizontalAlignment)
            {
                case ExcelHorizontalTextAlignment.Center:
                case ExcelHorizontalTextAlignment.Justify:
                    return (rectangle.Left + rectangle.Right) * 0.5;
                case ExcelHorizontalTextAlignment.Right:
                    return rectangle.Right - padding;
                default:
                    return rectangle.Left + padding;
            }
        }

        private static TextHorizontalMode MapDbTextHorizontalMode(
            ExcelTableCellStyle style)
        {
            switch (style.HorizontalAlignment)
            {
                case ExcelHorizontalTextAlignment.Center:
                case ExcelHorizontalTextAlignment.Justify:
                    return TextHorizontalMode.TextCenter;
                case ExcelHorizontalTextAlignment.Right:
                    return TextHorizontalMode.TextRight;
                default:
                    return TextHorizontalMode.TextLeft;
            }
        }

        private static List<string> SplitDbTextLines(string text, bool wrap,
            double availableWidth, double textHeight, bool bold)
        {
            string normalized = (text ?? string.Empty).Replace("\r\n", "\n")
                .Replace("\r", "\n").Replace("\\P", "\n");
            string[] sourceLines = normalized.Split(new[] { '\n' });
            var result = new List<string>();
            for (int i = 0; i < sourceLines.Length; i++)
            {
                string sourceLine = sourceLines[i] ?? string.Empty;
                if (!wrap || sourceLine.Length == 0)
                {
                    result.Add(sourceLine);
                    continue;
                }
                var current = new StringBuilder();
                for (int character = 0; character < sourceLine.Length; character++)
                {
                    char value = sourceLine[character];
                    string candidate = current.ToString() + value;
                    if (current.Length > 0 && EstimateDbTextWidth(candidate,
                        textHeight, bold) > availableWidth)
                    {
                        result.Add(current.ToString().TrimEnd());
                        current.Clear();
                        if (!char.IsWhiteSpace(value)) current.Append(value);
                    }
                    else current.Append(value);
                }
                result.Add(current.ToString().TrimEnd());
            }
            return result;
        }

        private static double EstimateDbTextWidth(string text, double height,
            bool bold)
        {
            double factor = 0.0;
            foreach (char value in text ?? string.Empty)
            {
                if (value > 127) factor += 1.0;
                else if (char.IsWhiteSpace(value)) factor += 0.34;
                else if ("ilI1.,:;'|!".IndexOf(value) >= 0) factor += 0.34;
                else if ("mwMW@#%".IndexOf(value) >= 0) factor += 0.92;
                else if (char.IsDigit(value)) factor += 0.58;
                else factor += 0.62;
            }
            if (bold) factor *= 1.05;
            return factor * Math.Max(0.001, height);
        }

        private static string FormatDbText(string text,
            ExcelTableCellStyle style)
        {
            string value = (text ?? string.Empty).Replace("\r", " ")
                .Replace("\n", " ");
            return style != null && style.Underline
                ? "%%u" + value + "%%u" : value;
        }

        private static void FitDbTextToCell(Database database, DBText text,
            CellRectangle rectangle, double padding, double lineSlotHeight)
        {
            if (text == null) return;
            double maxWidth = Math.Max(0.001,
                rectangle.Right - rectangle.Left - padding * 2.0);
            double maxHeight = Math.Max(0.001, Math.Min(lineSlotHeight,
                rectangle.Top - rectangle.Bottom - padding * 2.0));
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Extents3d extents;
                try { extents = text.GeometricExtents; }
                catch { return; }
                double actualWidth = Math.Abs(extents.MaxPoint.X
                    - extents.MinPoint.X);
                double actualHeight = Math.Abs(extents.MaxPoint.Y
                    - extents.MinPoint.Y);
                double scale = 1.0;
                if (actualWidth > maxWidth && actualWidth > 0.0000001)
                    scale = Math.Min(scale, maxWidth / actualWidth);
                if (actualHeight > maxHeight && actualHeight > 0.0000001)
                    scale = Math.Min(scale, maxHeight / actualHeight);
                if (scale >= 0.999) break;
                text.Height = Math.Max(0.001, text.Height * scale * 0.985);
                try { text.AdjustAlignment(database); } catch { }
            }
            KeepDbTextInsideCell(text, rectangle, padding);
        }

        private static void KeepDbTextInsideCell(DBText text,
            CellRectangle rectangle, double padding)
        {
            Extents3d extents;
            try { extents = text.GeometricExtents; }
            catch { return; }
            double left = rectangle.Left + padding;
            double right = rectangle.Right - padding;
            double bottom = rectangle.Bottom + padding;
            double top = rectangle.Top - padding;
            double offsetX = 0.0;
            double offsetY = 0.0;
            if (extents.MinPoint.X < left)
                offsetX = left - extents.MinPoint.X;
            if (extents.MaxPoint.X + offsetX > right)
                offsetX += right - (extents.MaxPoint.X + offsetX);
            if (extents.MinPoint.Y < bottom)
                offsetY = bottom - extents.MinPoint.Y;
            if (extents.MaxPoint.Y + offsetY > top)
                offsetY += top - (extents.MaxPoint.Y + offsetY);
            if (Math.Abs(offsetX) < 0.0000001
                && Math.Abs(offsetY) < 0.0000001) return;
            try
            {
                text.TransformBy(Matrix3d.Displacement(
                    new Vector3d(offsetX, offsetY, 0.0)));
            }
            catch { }
        }

        private static TableGeometry BuildGeometry(ExcelTableModel model,
            ExcelToCadOptions options, Point3d insertionPoint)
        {
            double defaultFontPixels = Math.Max(1.0,
                model.DefaultFontPoints * 96.0 / 72.0);
            double scale = options.TextHeight / defaultFontPixels;
            var geometry = new TableGeometry
            {
                X = new double[model.ColumnCount + 1],
                Y = new double[model.RowCount + 1],
                ColumnWidths = new double[model.ColumnCount],
                RowHeights = new double[model.RowCount]
            };
            geometry.X[0] = insertionPoint.X;
            for (int column = 0; column < model.ColumnCount; column++)
            {
                double pixels = model.ColumnPixelWidths != null
                    && column < model.ColumnPixelWidths.Length
                    ? model.ColumnPixelWidths[column] : 64.0;
                double width = Math.Max(0.001, pixels * scale);
                geometry.ColumnWidths[column] = width;
                geometry.X[column + 1] = geometry.X[column] + width;
            }
            geometry.Y[0] = insertionPoint.Y;
            for (int row = 0; row < model.RowCount; row++)
            {
                double pixels = model.RowPixelHeights != null
                    && row < model.RowPixelHeights.Length
                    ? model.RowPixelHeights[row] : 20.0;
                double height = Math.Max(0.001, pixels * scale);
                geometry.RowHeights[row] = height;
                geometry.Y[row + 1] = geometry.Y[row] - height;
            }
            return geometry;
        }

        private static CellRectangle GetCellRectangle(ExcelTableModel model,
            TableGeometry geometry, int row, int column)
        {
            ExcelMergedRange merge = model.FindMergedRange(row, column);
            int firstRow = merge == null ? row : merge.FirstRow;
            int lastRow = merge == null ? row : merge.LastRow;
            int firstColumn = merge == null ? column : merge.FirstColumn;
            int lastColumn = merge == null ? column : merge.LastColumn;
            return new CellRectangle
            {
                Left = geometry.X[firstColumn],
                Right = geometry.X[lastColumn + 1],
                Top = geometry.Y[firstRow],
                Bottom = geometry.Y[lastRow + 1]
            };
        }

        private static void ApplyNativeCellBorders(Table table, int row, int column,
            ExcelTableModel model, ExcelToCadOptions options)
        {
            SetNativeBorder(table, row, column, GridLineType.HorizontalTop,
                ResolveHorizontalBorder(model, row, column), options);
            SetNativeBorder(table, row, column, GridLineType.HorizontalBottom,
                ResolveHorizontalBorder(model, row + 1, column), options);
            SetNativeBorder(table, row, column, GridLineType.VerticalLeft,
                ResolveVerticalBorder(model, row, column), options);
            SetNativeBorder(table, row, column, GridLineType.VerticalRight,
                ResolveVerticalBorder(model, row, column + 1), options);
        }

        private static void SetNativeBorder(Table table, int row, int column,
            GridLineType gridLine, ExcelCellBorder border, ExcelToCadOptions options)
        {
#pragma warning disable CS0618
            if (!options.DrawGridLines)
            {
                table.SetGridVisibility(row, column, gridLine,
                    Visibility.Invisible);
                return;
            }
            if (border == null || !border.Visible) border = DefaultGridBorder();
            table.SetGridVisibility(row, column, gridLine, Visibility.Visible);
            table.SetGridColor(row, column, gridLine,
                EntityModeColor(options));
            table.SetGridLineWeight(row, column, gridLine,
                MapLineWeight(border.Style));
            table.SetGridLineStyle(row, column, gridLine,
                border.Style == ExcelBorderLineStyle.Double
                    ? GridLineStyle.Double : GridLineStyle.Single);
            if (border.Style == ExcelBorderLineStyle.Double)
                table.SetGridDoubleLineSpacing(row, column, gridLine, 0.08);
#pragma warning restore CS0618
        }

        private static ExcelCellBorder ResolveHorizontalBorder(ExcelTableModel model,
            int rowEdge, int column)
        {
            ExcelCellBorder upper = null;
            ExcelCellBorder lower = null;
            if (rowEdge > 0)
            {
                ExcelTableCell cell = model.GetEffectiveCell(rowEdge - 1, column);
                if (cell != null && cell.Style != null) upper = cell.Style.BottomBorder;
            }
            if (rowEdge < model.RowCount)
            {
                ExcelTableCell cell = model.GetEffectiveCell(rowEdge, column);
                if (cell != null && cell.Style != null) lower = cell.Style.TopBorder;
            }
            return Stronger(upper, lower);
        }

        private static ExcelCellBorder ResolveVerticalBorder(ExcelTableModel model,
            int row, int columnEdge)
        {
            ExcelCellBorder left = null;
            ExcelCellBorder right = null;
            if (columnEdge > 0)
            {
                ExcelTableCell cell = model.GetEffectiveCell(row, columnEdge - 1);
                if (cell != null && cell.Style != null) left = cell.Style.RightBorder;
            }
            if (columnEdge < model.ColumnCount)
            {
                ExcelTableCell cell = model.GetEffectiveCell(row, columnEdge);
                if (cell != null && cell.Style != null) right = cell.Style.LeftBorder;
            }
            return Stronger(left, right);
        }

        private static ExcelCellBorder Stronger(ExcelCellBorder first,
            ExcelCellBorder second)
        {
            int firstStrength = first == null ? 0 : first.Strength;
            int secondStrength = second == null ? 0 : second.Strength;
            return secondStrength > firstStrength ? second : first;
        }

        private static bool IsInternalHorizontalMergeEdge(ExcelTableModel model,
            int rowEdge, int column)
        {
            if (rowEdge <= 0 || rowEdge >= model.RowCount) return false;
            ExcelMergedRange above = model.FindMergedRange(rowEdge - 1, column);
            ExcelMergedRange below = model.FindMergedRange(rowEdge, column);
            return above != null && ReferenceEquals(above, below);
        }

        private static bool IsInternalVerticalMergeEdge(ExcelTableModel model,
            int row, int columnEdge)
        {
            if (columnEdge <= 0 || columnEdge >= model.ColumnCount) return false;
            ExcelMergedRange left = model.FindMergedRange(row, columnEdge - 1);
            ExcelMergedRange right = model.FindMergedRange(row, columnEdge);
            return left != null && ReferenceEquals(left, right);
        }

        private static void ApplyBorder(Line line, ExcelCellBorder border,
            ObjectId layerId, ExcelToCadOptions options)
        {
            SetEntityLayer(line, layerId);
            line.Color = EntityModeColor(options);
            line.LineWeight = MapLineWeight(border.Style);
        }

        private static void SetEntityLayer(Entity entity, ObjectId layerId)
        {
            if (entity == null || layerId.IsNull) return;
            entity.LayerId = layerId;
        }

        private static void Append(BlockTableRecord owner, Transaction transaction,
            Entity entity)
        {
            owner.AppendEntity(entity);
            transaction.AddNewlyCreatedDBObject(entity, true);
        }

        private static ExcelCellBorder DefaultGridBorder()
        {
            return new ExcelCellBorder
            {
                Style = ExcelBorderLineStyle.Hair,
                Color = new ExcelRgbColor(150, 160, 175)
            };
        }

        private static LineWeight MapLineWeight(ExcelBorderLineStyle style)
        {
            switch (style)
            {
                case ExcelBorderLineStyle.Double:
                case ExcelBorderLineStyle.Thick:
                    return LineWeight.LineWeight050;
                case ExcelBorderLineStyle.Medium:
                    return LineWeight.LineWeight025;
                case ExcelBorderLineStyle.Thin:
                case ExcelBorderLineStyle.Dashed:
                case ExcelBorderLineStyle.Dotted:
                    return LineWeight.LineWeight013;
                default:
                    return LineWeight.LineWeight005;
            }
        }

        private static Color ToCadColor(ExcelRgbColor color, bool adaptiveBlack)
        {
            if (color == null) return ByLayerColor();
            if (adaptiveBlack && color.Red < 24 && color.Green < 24 && color.Blue < 24)
                return Color.FromColorIndex(ColorMethod.ByAci, 7);
            return Color.FromRgb(color.Red, color.Green, color.Blue);
        }

        private static Color ByLayerColor()
        {
            return Color.FromColorIndex(ColorMethod.ByLayer, 256);
        }

        private static Color EntityModeColor(ExcelToCadOptions options)
        {
            return options != null
                && options.EntityColorMode == ExcelCadEntityColorMode.ByLayer
                ? ByLayerColor()
                : Color.FromColorIndex(ColorMethod.ByBlock, 0);
        }

        private static ObjectId ResolveLayerId(Database database,
            Transaction transaction, string layerName)
        {
            LayerTable table = transaction.GetObject(database.LayerTableId,
                OpenMode.ForRead) as LayerTable;
            string name = string.IsNullOrWhiteSpace(layerName)
                ? "0" : layerName.Trim();
            if (table != null && table.Has(name)) return table[name];
            if (table != null && table.Has("0")) return table["0"];
            return database.Clayer;
        }

        private static double ResolveTextHeight(ExcelTableModel model,
            ExcelToCadOptions options, ExcelTableCellStyle style, double rowHeight)
        {
            double defaultPoints = Math.Max(1.0, model.DefaultFontPoints);
            double fontPoints = style == null || style.FontPoints <= 0
                ? defaultPoints : style.FontPoints;
            double height = options.TextHeight * fontPoints / defaultPoints;
            if (style != null && style.ShrinkToFit)
                height = Math.Min(height, rowHeight * 0.82);
            return Math.Max(0.001, Math.Min(rowHeight * 0.88, height));
        }

        private static CellAlignment MapCellAlignment(ExcelTableCellStyle style)
        {
            bool left = style.HorizontalAlignment != ExcelHorizontalTextAlignment.Center
                && style.HorizontalAlignment != ExcelHorizontalTextAlignment.Right;
            bool right = style.HorizontalAlignment == ExcelHorizontalTextAlignment.Right;
            switch (style.VerticalAlignment)
            {
                case ExcelVerticalTextAlignment.Top:
                    return left ? CellAlignment.TopLeft
                        : (right ? CellAlignment.TopRight : CellAlignment.TopCenter);
                case ExcelVerticalTextAlignment.Bottom:
                    return left ? CellAlignment.BottomLeft
                        : (right ? CellAlignment.BottomRight
                        : CellAlignment.BottomCenter);
                default:
                    return left ? CellAlignment.MiddleLeft
                        : (right ? CellAlignment.MiddleRight
                        : CellAlignment.MiddleCenter);
            }
        }

        private static string FormatMText(string text, ExcelTableCellStyle style)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string value = EscapeMText(text);
            if (style != null && style.Underline) value = "\\L" + value + "\\l";
            if (style != null && style.Strikeout) value = "\\K" + value + "\\k";
            if (style != null && (!string.IsNullOrWhiteSpace(style.FontName)
                || style.Bold || style.Italic))
            {
                string font = SanitizeMTextFont(style.FontName);
                value = "{\\f" + font + "|b" + (style.Bold ? "1" : "0")
                    + "|i" + (style.Italic ? "1" : "0") + ";" + value + "}";
            }
            return value;
        }

        private static string EscapeMText(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", "\\P");
        }

        private static string SanitizeMTextFont(string value)
        {
            string font = string.IsNullOrWhiteSpace(value)
                ? "Microsoft YaHei" : value.Trim();
            return font.Replace("\\", string.Empty).Replace("{", string.Empty)
                .Replace("}", string.Empty).Replace("|", string.Empty)
                .Replace(";", string.Empty);
        }

        private static string CreateUniqueBlockName(BlockTable blockTable,
            string sheetName)
        {
            string baseName = "CDBox_Excel_" + SanitizeSymbolName(sheetName);
            if (baseName.Length > 180) baseName = baseName.Substring(0, 180);
            string candidate = baseName;
            int suffix = 1;
            while (blockTable.Has(candidate))
                candidate = baseName + "_" + suffix++.ToString(CultureInfo.InvariantCulture);
            return candidate;
        }

        private static string SanitizeSymbolName(string value)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "Table" : value.Trim();
            char[] invalid = { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', '=',
                '`', ',' };
            for (int i = 0; i < invalid.Length; i++) text = text.Replace(invalid[i], '_');
            return text;
        }

        private static double DegreesToRadians(int value)
        {
            return value * Math.PI / 180.0;
        }

        private sealed class TableGeometry
        {
            public double[] X { get; set; }
            public double[] Y { get; set; }
            public double[] ColumnWidths { get; set; }
            public double[] RowHeights { get; set; }
        }

        private sealed class CellRectangle
        {
            public double Left { get; set; }
            public double Right { get; set; }
            public double Top { get; set; }
            public double Bottom { get; set; }
        }
    }
}
