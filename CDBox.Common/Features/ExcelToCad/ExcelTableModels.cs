// Canonical implementation owned by CDBox.Common.
using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.Modules.ExcelToCad
{
    public enum ExcelTableRangeMode
    {
        UsedRange,
        LiveSelection,
        PrintArea
    }

    public enum CadExcelTableOutputType
    {
        ExplodedEntities,
        NativeTable,
        Block
    }

    public enum ExcelCadEntityColorMode
    {
        ByLayer,
        ByBlock
    }

    public enum ExcelHorizontalTextAlignment
    {
        General,
        Left,
        Center,
        Right,
        Justify
    }

    public enum ExcelVerticalTextAlignment
    {
        Top,
        Center,
        Bottom
    }

    public enum ExcelBorderLineStyle
    {
        None,
        Hair,
        Thin,
        Medium,
        Thick,
        Double,
        Dashed,
        Dotted
    }

    public sealed class ExcelToCadOptions
    {
        public ExcelToCadOptions()
        {
            FilePath = string.Empty;
            TemporarySourcePath = string.Empty;
            SheetIndex = 0;
            SheetName = string.Empty;
            RangeMode = ExcelTableRangeMode.UsedRange;
            SelectedRange = string.Empty;
            OutputType = CadExcelTableOutputType.ExplodedEntities;
            TextHeight = 2.5;
            PreserveBackgroundColors = true;
            PreserveTextColors = true;
            PreserveMergedCells = true;
            DrawGridLines = true;
            GridLayerName = "0";
            ContentLayerName = "0";
            EntityColorMode = ExcelCadEntityColorMode.ByBlock;
        }

        public string FilePath { get; set; }
        internal string TemporarySourcePath { get; set; }
        public int SheetIndex { get; set; }
        public string SheetName { get; set; }
        public ExcelTableRangeMode RangeMode { get; set; }
        public string SelectedRange { get; set; }
        public CadExcelTableOutputType OutputType { get; set; }
        public double TextHeight { get; set; }
        public bool PreserveBackgroundColors { get; set; }
        public bool PreserveTextColors { get; set; }
        public bool PreserveMergedCells { get; set; }
        public bool DrawGridLines { get; set; }
        public string GridLayerName { get; set; }
        public string ContentLayerName { get; set; }
        public ExcelCadEntityColorMode EntityColorMode { get; set; }

        public ExcelToCadOptions Clone()
        {
            return (ExcelToCadOptions)MemberwiseClone();
        }

        public void Normalize()
        {
            FilePath = (FilePath ?? string.Empty).Trim();
            TemporarySourcePath = (TemporarySourcePath ?? string.Empty).Trim();
            SheetName = (SheetName ?? string.Empty).Trim();
            SelectedRange = (SelectedRange ?? string.Empty).Trim();
            GridLayerName = string.IsNullOrWhiteSpace(GridLayerName)
                ? "0" : GridLayerName.Trim();
            ContentLayerName = string.IsNullOrWhiteSpace(ContentLayerName)
                ? "0" : ContentLayerName.Trim();
            if (SheetIndex < 0) SheetIndex = 0;
            if (!Enum.IsDefined(typeof(ExcelCadEntityColorMode), EntityColorMode))
                EntityColorMode = ExcelCadEntityColorMode.ByBlock;
            if (double.IsNaN(TextHeight) || double.IsInfinity(TextHeight)
                || TextHeight <= 0.01) TextHeight = 2.5;
            TextHeight = Math.Max(0.1, Math.Min(10000.0, TextHeight));
        }
    }

    public sealed class ExcelToCadDialogSettings
    {
        public ExcelToCadDialogSettings()
        {
            RangeMode = ExcelTableRangeMode.LiveSelection;
            OutputType = CadExcelTableOutputType.ExplodedEntities;
            TextHeight = 2.5;
            PreserveBackgroundColors = true;
            PreserveTextColors = true;
            PreserveMergedCells = true;
            DrawGridLines = true;
            GridLayerName = "0";
            ContentLayerName = "0";
            EntityColorMode = ExcelCadEntityColorMode.ByBlock;
        }

        public ExcelTableRangeMode RangeMode { get; set; }
        public CadExcelTableOutputType OutputType { get; set; }
        public double TextHeight { get; set; }
        public bool PreserveBackgroundColors { get; set; }
        public bool PreserveTextColors { get; set; }
        public bool PreserveMergedCells { get; set; }
        public bool DrawGridLines { get; set; }
        public string GridLayerName { get; set; }
        public string ContentLayerName { get; set; }
        public ExcelCadEntityColorMode EntityColorMode { get; set; }

        public void Normalize(double defaultTextHeight)
        {
            if (!Enum.IsDefined(typeof(ExcelTableRangeMode), RangeMode))
                RangeMode = ExcelTableRangeMode.LiveSelection;
            if (!Enum.IsDefined(typeof(CadExcelTableOutputType), OutputType))
                OutputType = CadExcelTableOutputType.ExplodedEntities;
            GridLayerName = string.IsNullOrWhiteSpace(GridLayerName)
                ? "0" : GridLayerName.Trim();
            ContentLayerName = string.IsNullOrWhiteSpace(ContentLayerName)
                ? "0" : ContentLayerName.Trim();
            if (!Enum.IsDefined(typeof(ExcelCadEntityColorMode), EntityColorMode))
                EntityColorMode = ExcelCadEntityColorMode.ByBlock;
            if (double.IsNaN(defaultTextHeight) || double.IsInfinity(defaultTextHeight)
                || defaultTextHeight <= 0.01) defaultTextHeight = 2.5;
            if (double.IsNaN(TextHeight) || double.IsInfinity(TextHeight)
                || TextHeight < 0.1 || TextHeight > 10000.0)
                TextHeight = Math.Max(0.1, Math.Min(10000.0, defaultTextHeight));
        }
    }

    public sealed class ExcelWorkbookInfo
    {
        public ExcelWorkbookInfo()
        {
            FilePath = string.Empty;
            Sheets = new List<ExcelWorksheetInfo>();
        }

        public string FilePath { get; set; }
        public int ActiveSheetIndex { get; set; }
        public List<ExcelWorksheetInfo> Sheets { get; private set; }
    }

    public sealed class ExcelWorksheetInfo
    {
        public ExcelWorksheetInfo()
        {
            Name = string.Empty;
            UsedRange = "A1";
            PrintArea = string.Empty;
        }

        public int Index { get; set; }
        public string Name { get; set; }
        public string UsedRange { get; set; }
        public string PrintArea { get; set; }
        public bool Hidden { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public sealed class ExcelTableModel
    {
        private ExcelTableCell[,] _cells;

        public ExcelTableModel()
        {
            SheetName = string.Empty;
            SourceRange = string.Empty;
            ColumnPixelWidths = new double[0];
            RowPixelHeights = new double[0];
            MergedRanges = new List<ExcelMergedRange>();
            DefaultFontPoints = 11.0;
            _cells = new ExcelTableCell[0, 0];
        }

        public string SheetName { get; set; }
        public string SourceRange { get; set; }
        public int RowCount { get { return _cells.GetLength(0); } }
        public int ColumnCount { get { return _cells.GetLength(1); } }
        public double[] ColumnPixelWidths { get; set; }
        public double[] RowPixelHeights { get; set; }
        public double DefaultFontPoints { get; set; }
        public List<ExcelMergedRange> MergedRanges { get; private set; }

        public void SetSize(int rows, int columns)
        {
            if (rows <= 0 || columns <= 0)
                throw new ArgumentOutOfRangeException("Excel 表格行列数必须大于 0。");
            _cells = new ExcelTableCell[rows, columns];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    _cells[row, column] = new ExcelTableCell
                    {
                        Row = row,
                        Column = column
                    };
                }
            }
        }

        public ExcelTableCell GetCell(int row, int column)
        {
            if (row < 0 || row >= RowCount || column < 0 || column >= ColumnCount)
                return null;
            return _cells[row, column];
        }

        public ExcelMergedRange FindMergedRange(int row, int column)
        {
            for (int i = 0; i < MergedRanges.Count; i++)
            {
                ExcelMergedRange range = MergedRanges[i];
                if (range.Contains(row, column)) return range;
            }
            return null;
        }

        public ExcelTableCell GetEffectiveCell(int row, int column)
        {
            ExcelMergedRange merge = FindMergedRange(row, column);
            return merge == null ? GetCell(row, column)
                : GetCell(merge.FirstRow, merge.FirstColumn);
        }

        public bool IsMergedAnchor(int row, int column)
        {
            ExcelMergedRange merge = FindMergedRange(row, column);
            return merge == null
                || (merge.FirstRow == row && merge.FirstColumn == column);
        }
    }

    public sealed class ExcelTableCell
    {
        public ExcelTableCell()
        {
            Text = string.Empty;
            Style = new ExcelTableCellStyle();
        }

        public int Row { get; set; }
        public int Column { get; set; }
        public int SourceRow { get; set; }
        public int SourceColumn { get; set; }
        public string Text { get; set; }
        public ExcelTableCellStyle Style { get; set; }
    }

    public sealed class ExcelTableCellStyle
    {
        public ExcelTableCellStyle()
        {
            FontName = "Microsoft YaHei";
            FontPoints = 11.0;
            HorizontalAlignment = ExcelHorizontalTextAlignment.General;
            VerticalAlignment = ExcelVerticalTextAlignment.Center;
            TopBorder = new ExcelCellBorder();
            RightBorder = new ExcelCellBorder();
            BottomBorder = new ExcelCellBorder();
            LeftBorder = new ExcelCellBorder();
        }

        public ExcelRgbColor TextColor { get; set; }
        public ExcelRgbColor BackgroundColor { get; set; }
        public string FontName { get; set; }
        public double FontPoints { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public bool Strikeout { get; set; }
        public bool WrapText { get; set; }
        public bool ShrinkToFit { get; set; }
        public int RotationDegrees { get; set; }
        public ExcelHorizontalTextAlignment HorizontalAlignment { get; set; }
        public ExcelVerticalTextAlignment VerticalAlignment { get; set; }
        public ExcelCellBorder TopBorder { get; set; }
        public ExcelCellBorder RightBorder { get; set; }
        public ExcelCellBorder BottomBorder { get; set; }
        public ExcelCellBorder LeftBorder { get; set; }
    }

    public sealed class ExcelCellBorder
    {
        public ExcelCellBorder()
        {
            Style = ExcelBorderLineStyle.None;
        }

        public ExcelBorderLineStyle Style { get; set; }
        public ExcelRgbColor Color { get; set; }
        public bool Visible { get { return Style != ExcelBorderLineStyle.None; } }

        public int Strength
        {
            get
            {
                switch (Style)
                {
                    case ExcelBorderLineStyle.Double: return 7;
                    case ExcelBorderLineStyle.Thick: return 6;
                    case ExcelBorderLineStyle.Medium: return 5;
                    case ExcelBorderLineStyle.Thin: return 4;
                    case ExcelBorderLineStyle.Dashed: return 3;
                    case ExcelBorderLineStyle.Dotted: return 2;
                    case ExcelBorderLineStyle.Hair: return 1;
                    default: return 0;
                }
            }
        }
    }

    public sealed class ExcelMergedRange
    {
        public int FirstRow { get; set; }
        public int LastRow { get; set; }
        public int FirstColumn { get; set; }
        public int LastColumn { get; set; }

        public bool Contains(int row, int column)
        {
            return row >= FirstRow && row <= LastRow
                && column >= FirstColumn && column <= LastColumn;
        }
    }

    public sealed class ExcelRgbColor
    {
        public ExcelRgbColor()
        {
        }

        public ExcelRgbColor(byte red, byte green, byte blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
    }
}
