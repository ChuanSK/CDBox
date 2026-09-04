// Canonical implementation owned by CDBox.Common.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using NPOI.HSSF.UserModel;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace TCPipeAutoDraw.Modules.ExcelToCad
{
    public static class ExcelTableReader
    {
        public const int MaximumCellCount = 50000;

        public static ExcelWorkbookInfo Inspect(string filePath)
        {
            filePath = NormalizeWorkbookPath(filePath);
            FileStream stream = null;
            IWorkbook workbook = null;
            try
            {
                workbook = OpenWorkbook(filePath, out stream);
                var info = new ExcelWorkbookInfo
                {
                    FilePath = filePath,
                    ActiveSheetIndex = Math.Max(0, Math.Min(workbook.ActiveSheetIndex,
                        workbook.NumberOfSheets - 1))
                };
                for (int i = 0; i < workbook.NumberOfSheets; i++)
                {
                    ISheet sheet = workbook.GetSheetAt(i);
                    CellRangeAddress used = FindUsedRange(sheet);
                    info.Sheets.Add(new ExcelWorksheetInfo
                    {
                        Index = i,
                        Name = workbook.GetSheetName(i),
                        UsedRange = FormatRange(used),
                        PrintArea = workbook.GetPrintArea(i) ?? string.Empty,
                        Hidden = workbook.IsSheetHidden(i) || workbook.IsSheetVeryHidden(i)
                    });
                }
                return info;
            }
            finally
            {
                DisposeWorkbook(workbook);
                if (stream != null) stream.Dispose();
            }
        }

        public static ExcelTableModel Read(ExcelToCadOptions sourceOptions)
        {
            if (sourceOptions == null) throw new ArgumentNullException("sourceOptions");
            ExcelToCadOptions options = sourceOptions.Clone();
            options.Normalize();
            string filePath = NormalizeWorkbookPath(
                string.IsNullOrWhiteSpace(options.TemporarySourcePath)
                    ? options.FilePath : options.TemporarySourcePath);

            FileStream stream = null;
            IWorkbook workbook = null;
            try
            {
                workbook = OpenWorkbook(filePath, out stream);
                int sheetIndex = ResolveSheetIndex(workbook, options);
                ISheet sheet = workbook.GetSheetAt(sheetIndex);
                CellRangeAddress sourceRange = ResolveRange(workbook, sheet, sheetIndex, options);
                return BuildModel(workbook, sheet, sourceRange, options);
            }
            finally
            {
                DisposeWorkbook(workbook);
                if (stream != null) stream.Dispose();
            }
        }

        public static string NormalizeRangeAddress(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string range = value.Trim();
            int bang = range.LastIndexOf('!');
            if (bang >= 0 && bang + 1 < range.Length) range = range.Substring(bang + 1);
            range = range.Replace("$", string.Empty).Trim();
            CellRangeAddress parsed = CellRangeAddress.ValueOf(range);
            return FormatRange(parsed);
        }

        private static ExcelTableModel BuildModel(IWorkbook workbook, ISheet sheet,
            CellRangeAddress sourceRange, ExcelToCadOptions options)
        {
            var sourceRows = new List<int>();
            for (int row = sourceRange.FirstRow; row <= sourceRange.LastRow; row++)
            {
                IRow excelRow = sheet.GetRow(row);
                if (IsRowHidden(excelRow)) continue;
                sourceRows.Add(row);
            }
            var sourceColumns = new List<int>();
            for (int column = sourceRange.FirstColumn; column <= sourceRange.LastColumn; column++)
            {
                if (!sheet.IsColumnHidden(column)) sourceColumns.Add(column);
            }
            if (sourceRows.Count == 0 || sourceColumns.Count == 0)
                throw new InvalidOperationException("选择区域内没有可见的行或列。");
            long cellCount = (long)sourceRows.Count * sourceColumns.Count;
            if (cellCount > MaximumCellCount)
                throw new InvalidOperationException("所选区域包含 " + cellCount.ToString("N0",
                    CultureInfo.CurrentCulture) + " 个单元格，超过单次转换上限 "
                    + MaximumCellCount.ToString("N0", CultureInfo.CurrentCulture)
                    + "。请缩小选择区域后重试。");

            var rowMap = new Dictionary<int, int>();
            var columnMap = new Dictionary<int, int>();
            for (int i = 0; i < sourceRows.Count; i++) rowMap[sourceRows[i]] = i;
            for (int i = 0; i < sourceColumns.Count; i++) columnMap[sourceColumns[i]] = i;

            var model = new ExcelTableModel
            {
                SheetName = sheet.SheetName,
                SourceRange = FormatRange(sourceRange),
                DefaultFontPoints = ResolveDefaultFontPoints(workbook),
                ColumnPixelWidths = new double[sourceColumns.Count],
                RowPixelHeights = new double[sourceRows.Count]
            };
            model.SetSize(sourceRows.Count, sourceColumns.Count);

            double defaultDigitWidth = ResolveDefaultDigitWidth(workbook);
            for (int column = 0; column < sourceColumns.Count; column++)
            {
                double pixels;
                try
                {
                    pixels = ConvertExcelColumnWidthToPixels(
                        sheet.GetColumnWidth(sourceColumns[column]),
                        defaultDigitWidth);
                }
                catch
                {
                    try
                    {
                        pixels = sheet.GetColumnWidthInPixels(
                            sourceColumns[column]);
                    }
                    catch
                    {
                        pixels = sheet.GetColumnWidth(sourceColumns[column])
                            / 256.0 * 7.0;
                    }
                }
                model.ColumnPixelWidths[column] = SanitizeDimension(pixels, 24.0);
            }
            for (int row = 0; row < sourceRows.Count; row++)
            {
                IRow excelRow = sheet.GetRow(sourceRows[row]);
                double points = excelRow == null ? sheet.DefaultRowHeightInPoints
                    : excelRow.HeightInPoints;
                model.RowPixelHeights[row] = SanitizeDimension(points * 96.0 / 72.0, 18.0);
            }

            var formatter = new DataFormatter(CultureInfo.CurrentCulture)
            {
                UseCachedValuesForFormulaCells = true
            };
            IFormulaEvaluator evaluator = null;
            try
            {
                evaluator = workbook.GetCreationHelper().CreateFormulaEvaluator();
                evaluator.IgnoreMissingWorkbooks = true;
            }
            catch { }

            for (int row = 0; row < sourceRows.Count; row++)
            {
                IRow excelRow = sheet.GetRow(sourceRows[row]);
                for (int column = 0; column < sourceColumns.Count; column++)
                {
                    int sourceColumn = sourceColumns[column];
                    ICell excelCell = excelRow == null ? null : excelRow.GetCell(sourceColumn,
                        MissingCellPolicy.RETURN_BLANK_AS_NULL);
                    ExcelTableCell cell = model.GetCell(row, column);
                    cell.SourceRow = sourceRows[row];
                    cell.SourceColumn = sourceColumn;
                    cell.Text = FormatCell(excelCell, formatter, evaluator);
                    cell.Style = ReadCellStyle(workbook, sheet, excelRow, excelCell,
                        sourceColumn);
                }
            }

            if (options.PreserveMergedCells)
            {
                AddMergedRanges(workbook, sheet, sourceRange, sourceRows, sourceColumns,
                    rowMap, columnMap, model, formatter, evaluator);
            }
            return model;
        }

        private static void AddMergedRanges(IWorkbook workbook, ISheet sheet,
            CellRangeAddress sourceRange, IList<int> sourceRows, IList<int> sourceColumns,
            IDictionary<int, int> rowMap, IDictionary<int, int> columnMap,
            ExcelTableModel model, DataFormatter formatter, IFormulaEvaluator evaluator)
        {
            for (int i = 0; i < sheet.NumMergedRegions; i++)
            {
                CellRangeAddress merge = sheet.GetMergedRegion(i);
                int firstRow = Math.Max(merge.FirstRow, sourceRange.FirstRow);
                int lastRow = Math.Min(merge.LastRow, sourceRange.LastRow);
                int firstColumn = Math.Max(merge.FirstColumn, sourceRange.FirstColumn);
                int lastColumn = Math.Min(merge.LastColumn, sourceRange.LastColumn);
                if (firstRow > lastRow || firstColumn > lastColumn) continue;

                List<int> visibleRows = sourceRows.Where(x => x >= firstRow
                    && x <= lastRow).ToList();
                List<int> visibleColumns = sourceColumns.Where(x => x >= firstColumn
                    && x <= lastColumn).ToList();
                if (visibleRows.Count == 0 || visibleColumns.Count == 0) continue;
                int targetFirstRow = rowMap[visibleRows[0]];
                int targetLastRow = rowMap[visibleRows[visibleRows.Count - 1]];
                int targetFirstColumn = columnMap[visibleColumns[0]];
                int targetLastColumn = columnMap[visibleColumns[visibleColumns.Count - 1]];
                if (targetFirstRow == targetLastRow
                    && targetFirstColumn == targetLastColumn) continue;

                model.MergedRanges.Add(new ExcelMergedRange
                {
                    FirstRow = targetFirstRow,
                    LastRow = targetLastRow,
                    FirstColumn = targetFirstColumn,
                    LastColumn = targetLastColumn
                });

                IRow anchorRow = sheet.GetRow(merge.FirstRow);
                ICell anchorCell = anchorRow == null ? null : anchorRow.GetCell(
                    merge.FirstColumn, MissingCellPolicy.RETURN_BLANK_AS_NULL);
                ExcelTableCell target = model.GetCell(targetFirstRow, targetFirstColumn);
                target.Text = FormatCell(anchorCell, formatter, evaluator);
                target.Style = ReadCellStyle(workbook, sheet, anchorRow, anchorCell,
                    merge.FirstColumn);
            }
        }

        private static ExcelTableCellStyle ReadCellStyle(IWorkbook workbook, ISheet sheet,
            IRow row, ICell cell, int column)
        {
            ICellStyle style = cell == null ? null : cell.CellStyle;
            if (style == null && row != null && row.IsFormatted) style = row.RowStyle;
            if (style == null)
            {
                try { style = sheet.GetColumnStyle(column); }
                catch { }
            }
            if (style == null) style = workbook.GetCellStyleAt(0);

            IFont font = null;
            try { font = style.GetFont(workbook); }
            catch
            {
                try { font = workbook.GetFontAt(style.FontIndex); }
                catch { }
            }
            var result = new ExcelTableCellStyle
            {
                FontName = font == null || string.IsNullOrWhiteSpace(font.FontName)
                    ? "Microsoft YaHei" : font.FontName,
                FontPoints = font == null || font.FontHeightInPoints <= 0
                    ? ResolveDefaultFontPoints(workbook) : font.FontHeightInPoints,
                Bold = font != null && font.IsBold,
                Italic = font != null && font.IsItalic,
                Underline = font != null && font.Underline != FontUnderlineType.None,
                Strikeout = font != null && font.IsStrikeout,
                WrapText = style.WrapText,
                ShrinkToFit = style.ShrinkToFit,
                RotationDegrees = NormalizeRotation(style.Rotation),
                HorizontalAlignment = MapHorizontalAlignment(style.Alignment, cell),
                VerticalAlignment = MapVerticalAlignment(style.VerticalAlignment),
                TextColor = ResolveFontColor(workbook, font),
                BackgroundColor = ResolveFillColor(workbook, style),
                TopBorder = ReadBorder(workbook, style, BorderSide.Top),
                RightBorder = ReadBorder(workbook, style, BorderSide.Right),
                BottomBorder = ReadBorder(workbook, style, BorderSide.Bottom),
                LeftBorder = ReadBorder(workbook, style, BorderSide.Left)
            };
            return result;
        }

        private static ExcelCellBorder ReadBorder(IWorkbook workbook, ICellStyle style,
            BorderSide side)
        {
            BorderStyle lineStyle;
            short colorIndex;
            IColor directColor = null;
            XSSFCellStyle xssf = style as XSSFCellStyle;
            switch (side)
            {
                case BorderSide.Top:
                    lineStyle = style.BorderTop;
                    colorIndex = style.TopBorderColor;
                    if (xssf != null) directColor = xssf.TopBorderXSSFColor;
                    break;
                case BorderSide.Right:
                    lineStyle = style.BorderRight;
                    colorIndex = style.RightBorderColor;
                    if (xssf != null) directColor = xssf.RightBorderXSSFColor;
                    break;
                case BorderSide.Bottom:
                    lineStyle = style.BorderBottom;
                    colorIndex = style.BottomBorderColor;
                    if (xssf != null) directColor = xssf.BottomBorderXSSFColor;
                    break;
                default:
                    lineStyle = style.BorderLeft;
                    colorIndex = style.LeftBorderColor;
                    if (xssf != null) directColor = xssf.LeftBorderXSSFColor;
                    break;
            }
            return new ExcelCellBorder
            {
                Style = MapBorderStyle(lineStyle),
                Color = ResolveColor(workbook, directColor, colorIndex,
                    new ExcelRgbColor(0, 0, 0))
            };
        }

        private static ExcelRgbColor ResolveFillColor(IWorkbook workbook, ICellStyle style)
        {
            if (style == null || style.FillPattern == FillPattern.NoFill) return null;
            IColor color = style.FillForegroundColorColor;
            XSSFCellStyle xssf = style as XSSFCellStyle;
            if (xssf != null && xssf.FillForegroundXSSFColor != null)
                color = xssf.FillForegroundXSSFColor;
            return ResolveColor(workbook, color, style.FillForegroundColor, null);
        }

        private static ExcelRgbColor ResolveFontColor(IWorkbook workbook, IFont font)
        {
            if (font == null) return new ExcelRgbColor(0, 0, 0);
            IColor color = null;
            XSSFFont xssf = font as XSSFFont;
            if (xssf != null)
            {
                try { color = xssf.GetXSSFColor(); }
                catch { }
            }
            HSSFFont hssf = font as HSSFFont;
            if (hssf != null && workbook is HSSFWorkbook hssfWorkbook)
            {
                try { color = hssf.GetHSSFColor(hssfWorkbook); }
                catch { }
            }
            return ResolveColor(workbook, color, font.Color,
                new ExcelRgbColor(0, 0, 0));
        }

        private static ExcelRgbColor ResolveColor(IWorkbook workbook, IColor color,
            short indexedColor, ExcelRgbColor fallback)
        {
            byte[] rgb = null;
            XSSFColor xssfColor = color as XSSFColor;
            if (xssfColor != null)
            {
                try { rgb = xssfColor.RGBWithTint; }
                catch { rgb = xssfColor.RGB; }
            }
            if (rgb == null && color != null)
            {
                try { rgb = color.RGB; }
                catch { }
            }
            ExcelRgbColor resolved = FromRgbBytes(rgb);
            if (resolved != null) return resolved;

            if (workbook is HSSFWorkbook hssf)
            {
                try
                {
                    HSSFColor paletteColor = hssf.GetCustomPalette().GetColor(indexedColor);
                    if (paletteColor != null)
                    {
                        resolved = FromRgbBytes(paletteColor.GetTriplet());
                        if (resolved != null) return resolved;
                    }
                }
                catch { }
            }
            try
            {
                Dictionary<int, HSSFColor> colors = HSSFColor.GetIndexHash();
                HSSFColor indexed;
                if (colors.TryGetValue(indexedColor, out indexed))
                {
                    resolved = FromRgbBytes(indexed.GetTriplet());
                    if (resolved != null) return resolved;
                }
            }
            catch { }
            return fallback;
        }

        private static ExcelRgbColor FromRgbBytes(byte[] rgb)
        {
            if (rgb == null || rgb.Length < 3) return null;
            int offset = rgb.Length >= 4 ? rgb.Length - 3 : 0;
            return new ExcelRgbColor(rgb[offset], rgb[offset + 1], rgb[offset + 2]);
        }

        private static string FormatCell(ICell cell, DataFormatter formatter,
            IFormulaEvaluator evaluator)
        {
            if (cell == null) return string.Empty;
            if (cell.CellType == CellType.Formula && evaluator != null)
            {
                try
                {
                    CellValue evaluated = evaluator.Evaluate(cell);
                    if (evaluated != null)
                        return FormatEvaluatedFormula(cell, evaluated, formatter);
                }
                catch { }
            }
            try
            {
                return evaluator == null ? formatter.FormatCellValue(cell)
                    : formatter.FormatCellValue(cell, evaluator);
            }
            catch
            {
                try { return formatter.FormatCellValue(cell); }
                catch { return cell.ToString() ?? string.Empty; }
            }
        }

        private static string FormatEvaluatedFormula(ICell source, CellValue evaluated,
            DataFormatter formatter)
        {
            switch (evaluated.CellType)
            {
                case CellType.Numeric:
                    ICellStyle style = source.CellStyle;
                    int formatIndex = style == null ? 0 : style.DataFormat;
                    string format = style == null ? "General"
                        : style.GetDataFormatString();
                    try
                    {
                        return formatter.FormatRawCellContents(evaluated.NumberValue,
                            formatIndex, string.IsNullOrWhiteSpace(format)
                                ? "General" : format);
                    }
                    catch
                    {
                        return evaluated.NumberValue.ToString("G15",
                            CultureInfo.CurrentCulture);
                    }
                case CellType.String:
                    return evaluated.StringValue ?? string.Empty;
                case CellType.Boolean:
                    return evaluated.BooleanValue ? "TRUE" : "FALSE";
                case CellType.Error:
                    return FormulaError.ForInt(evaluated.ErrorValue).String;
                case CellType.Blank:
                    return string.Empty;
                default:
                    return source.CellFormula ?? string.Empty;
            }
        }

        private static ExcelHorizontalTextAlignment MapHorizontalAlignment(
            HorizontalAlignment alignment, ICell cell)
        {
            switch (alignment)
            {
                case HorizontalAlignment.Center:
                case HorizontalAlignment.CenterSelection:
                    return ExcelHorizontalTextAlignment.Center;
                case HorizontalAlignment.Right:
                    return ExcelHorizontalTextAlignment.Right;
                case HorizontalAlignment.Justify:
                case HorizontalAlignment.Distributed:
                    return ExcelHorizontalTextAlignment.Justify;
                case HorizontalAlignment.Left:
                    return ExcelHorizontalTextAlignment.Left;
                default:
                    return IsRightAlignedByDefault(cell)
                        ? ExcelHorizontalTextAlignment.Right
                        : ExcelHorizontalTextAlignment.Left;
            }
        }

        private static bool IsRightAlignedByDefault(ICell cell)
        {
            if (cell == null) return false;
            CellType type = cell.CellType == CellType.Formula
                ? cell.CachedFormulaResultType : cell.CellType;
            return type == CellType.Numeric;
        }

        private static ExcelVerticalTextAlignment MapVerticalAlignment(
            VerticalAlignment alignment)
        {
            if (alignment == VerticalAlignment.Top) return ExcelVerticalTextAlignment.Top;
            if (alignment == VerticalAlignment.Bottom) return ExcelVerticalTextAlignment.Bottom;
            return ExcelVerticalTextAlignment.Center;
        }

        private static ExcelBorderLineStyle MapBorderStyle(BorderStyle style)
        {
            switch (style)
            {
                case BorderStyle.Hair:
                    return ExcelBorderLineStyle.Hair;
                case BorderStyle.Thin:
                    return ExcelBorderLineStyle.Thin;
                case BorderStyle.Medium:
                case BorderStyle.MediumDashed:
                case BorderStyle.MediumDashDot:
                case BorderStyle.MediumDashDotDot:
                    return ExcelBorderLineStyle.Medium;
                case BorderStyle.Thick:
                    return ExcelBorderLineStyle.Thick;
                case BorderStyle.Double:
                    return ExcelBorderLineStyle.Double;
                case BorderStyle.Dotted:
                    return ExcelBorderLineStyle.Dotted;
                case BorderStyle.Dashed:
                case BorderStyle.DashDot:
                case BorderStyle.DashDotDot:
                case BorderStyle.SlantedDashDot:
                    return ExcelBorderLineStyle.Dashed;
                default:
                    return ExcelBorderLineStyle.None;
            }
        }

        private static int NormalizeRotation(short rotation)
        {
            if (rotation == 255) return 90;
            if (rotation > 90) return 90 - rotation;
            return rotation;
        }

        private static CellRangeAddress ResolveRange(IWorkbook workbook, ISheet sheet,
            int sheetIndex, ExcelToCadOptions options)
        {
            switch (options.RangeMode)
            {
                case ExcelTableRangeMode.LiveSelection:
                    if (string.IsNullOrWhiteSpace(options.SelectedRange))
                        throw new InvalidOperationException("尚未从 Excel 读取到有效选择区域。");
                    return ParseRange(options.SelectedRange);
                case ExcelTableRangeMode.PrintArea:
                    string printArea = workbook.GetPrintArea(sheetIndex);
                    if (string.IsNullOrWhiteSpace(printArea))
                        throw new InvalidOperationException("当前工作表未设置打印区域。");
                    return ParseAreaReference(workbook, printArea, sheet.SheetName);
                default:
                    return FindUsedRange(sheet);
            }
        }

        private static CellRangeAddress ParseAreaReference(IWorkbook workbook,
            string reference, string sheetName)
        {
            AreaReference[] areas = AreaReference.GenerateContiguous(
                workbook.SpreadsheetVersion, reference);
            int firstRow = int.MaxValue;
            int firstColumn = int.MaxValue;
            int lastRow = -1;
            int lastColumn = -1;
            for (int i = 0; i < areas.Length; i++)
            {
                CellReference first = areas[i].FirstCell;
                CellReference last = areas[i].LastCell;
                if (!string.IsNullOrWhiteSpace(first.SheetName)
                    && !string.Equals(first.SheetName, sheetName,
                        StringComparison.CurrentCultureIgnoreCase)) continue;
                firstRow = Math.Min(firstRow, first.Row);
                firstColumn = Math.Min(firstColumn, first.Col);
                lastRow = Math.Max(lastRow, last.Row);
                lastColumn = Math.Max(lastColumn, last.Col);
            }
            if (lastRow < firstRow || lastColumn < firstColumn)
                throw new InvalidOperationException("无法解析当前工作表的打印区域。");
            return new CellRangeAddress(firstRow, lastRow, firstColumn, lastColumn);
        }

        private static CellRangeAddress ParseRange(string value)
        {
            string normalized = NormalizeRangeAddress(value);
            return CellRangeAddress.ValueOf(normalized);
        }

        private static CellRangeAddress FindUsedRange(ISheet sheet)
        {
            int firstRow = int.MaxValue;
            int lastRow = -1;
            int firstColumn = int.MaxValue;
            int lastColumn = -1;
            IEnumerator rows = sheet.GetEnumerator();
            while (rows.MoveNext())
            {
                IRow row = rows.Current as IRow;
                if (row == null) continue;
                IList<ICell> cells = row.Cells;
                for (int i = 0; i < cells.Count; i++)
                {
                    ICell cell = cells[i];
                    if (!IsUsedCell(cell)) continue;
                    firstRow = Math.Min(firstRow, cell.RowIndex);
                    lastRow = Math.Max(lastRow, cell.RowIndex);
                    firstColumn = Math.Min(firstColumn, cell.ColumnIndex);
                    lastColumn = Math.Max(lastColumn, cell.ColumnIndex);
                }
            }
            for (int i = 0; i < sheet.NumMergedRegions; i++)
            {
                CellRangeAddress merge = sheet.GetMergedRegion(i);
                firstRow = Math.Min(firstRow, merge.FirstRow);
                lastRow = Math.Max(lastRow, merge.LastRow);
                firstColumn = Math.Min(firstColumn, merge.FirstColumn);
                lastColumn = Math.Max(lastColumn, merge.LastColumn);
            }
            if (lastRow < 0 || lastColumn < 0)
            {
                CellAddress active = sheet.ActiveCell;
                int row = active == null ? 0 : active.Row;
                int column = active == null ? 0 : active.Column;
                return new CellRangeAddress(row, row, column, column);
            }
            return new CellRangeAddress(firstRow, lastRow, firstColumn, lastColumn);
        }

        private static bool IsUsedCell(ICell cell)
        {
            if (cell == null) return false;
            if (cell.CellType != CellType.Blank) return true;
            try { return cell.CellStyle != null && cell.CellStyle.Index != 0; }
            catch { return false; }
        }

        private static bool IsRowHidden(IRow row)
        {
            if (row == null) return false;
            try
            {
                if (row.ZeroHeight) return true;
            }
            catch { }

            // HSSFRow.Hidden is declared by NPOI's shared interface but throws
            // NotImplementedException for legacy .xls workbooks.
            try { return row.Hidden == true; }
            catch (NotImplementedException) { return false; }
            catch { return false; }
        }

        private static int ResolveSheetIndex(IWorkbook workbook, ExcelToCadOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.SheetName))
            {
                int named = workbook.GetSheetIndex(options.SheetName);
                if (named >= 0) return named;
            }
            if (options.SheetIndex < 0 || options.SheetIndex >= workbook.NumberOfSheets)
                throw new InvalidOperationException("选择的工作表已不存在，请重新选择 Excel 文件。");
            return options.SheetIndex;
        }

        private static double ResolveDefaultFontPoints(IWorkbook workbook)
        {
            try
            {
                IFont font = workbook.GetFontAt(0);
                if (font != null && font.FontHeightInPoints > 0)
                    return font.FontHeightInPoints;
            }
            catch { }
            return 11.0;
        }

        private static double ResolveDefaultDigitWidth(IWorkbook workbook)
        {
            try
            {
                IFont excelFont = workbook.GetFontAt(0);
                string fontName = excelFont == null
                    || string.IsNullOrWhiteSpace(excelFont.FontName)
                    ? "Microsoft YaHei" : excelFont.FontName.Trim();
                float fontPoints = (float)(excelFont == null
                    || excelFont.FontHeightInPoints <= 0
                    ? 11.0 : excelFont.FontHeightInPoints);
                FontStyle fontStyle = FontStyle.Regular;
                if (excelFont != null && excelFont.IsBold)
                    fontStyle |= FontStyle.Bold;
                if (excelFont != null && excelFont.IsItalic)
                    fontStyle |= FontStyle.Italic;
                using (var bitmap = new Bitmap(32, 32))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                using (var font = new Font(fontName, fontPoints, fontStyle,
                    GraphicsUnit.Point))
                {
                    double maximum = 0.0;
                    for (int digit = 0; digit <= 9; digit++)
                    {
                        SizeF size = graphics.MeasureString(
                            digit.ToString(CultureInfo.InvariantCulture), font,
                            256, StringFormat.GenericTypographic);
                        maximum = Math.Max(maximum, size.Width);
                    }
                    if (maximum > 0.1)
                        return Math.Max(1.0, Math.Round(maximum,
                            MidpointRounding.AwayFromZero));
                }
            }
            catch { }
            return 7.0;
        }

        private static double ConvertExcelColumnWidthToPixels(double widthUnits,
            double maximumDigitWidth)
        {
            if (widthUnits <= 0) return 0.0;
            double digitWidth = Math.Max(1.0, maximumDigitWidth);
            return Math.Truncate(((widthUnits
                + Math.Truncate(128.0 / digitWidth)) / 256.0) * digitWidth);
        }

        private static string NormalizeWorkbookPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new InvalidOperationException("请先选择 Excel 文件。");
            string fullPath = Path.GetFullPath(filePath.Trim());
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Excel 文件不存在。", fullPath);
            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (extension != ".xls" && extension != ".xlsx" && extension != ".xlsm")
                throw new NotSupportedException("仅支持 .xls、.xlsx 和 .xlsm 工作簿。");
            return fullPath;
        }

        private static IWorkbook OpenWorkbook(string filePath, out FileStream stream)
        {
            stream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            try
            {
                return string.Equals(Path.GetExtension(filePath), ".xls",
                    StringComparison.OrdinalIgnoreCase)
                    ? (IWorkbook)new HSSFWorkbook(stream)
                    : new XSSFWorkbook(stream);
            }
            catch
            {
                stream.Dispose();
                stream = null;
                throw;
            }
        }

        private static void DisposeWorkbook(IWorkbook workbook)
        {
            IDisposable disposable = workbook as IDisposable;
            if (disposable != null)
            {
                try { disposable.Dispose(); }
                catch { }
            }
        }

        private static string FormatRange(CellRangeAddress range)
        {
            string first = CellReference.ConvertNumToColString(range.FirstColumn)
                + (range.FirstRow + 1).ToString(CultureInfo.InvariantCulture);
            string last = CellReference.ConvertNumToColString(range.LastColumn)
                + (range.LastRow + 1).ToString(CultureInfo.InvariantCulture);
            return string.Equals(first, last, StringComparison.Ordinal)
                ? first : first + ":" + last;
        }

        private static double SanitizeDimension(double value, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.01)
                return fallback;
            return Math.Max(1.0, Math.Min(100000.0, value));
        }

        private enum BorderSide
        {
            Top,
            Right,
            Bottom,
            Left
        }
    }
}
