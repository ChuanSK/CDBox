using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 基于固定 xls 模板填充工程量数据。
    /// 模板只负责表头、标题、列宽、边框、合并单元格等格式；代码只写入数据行和合计行。
    /// </summary>
    public static class QuantityExcelXmlExporter
    {
        private const string PipeSheetName = "管道埋设工程量计算式";
        private const string WellSheetName = "成品塑料污水检查井计算式";
        private const string TemplateFileName = "工程量计算表模板.xls";
        private const int PipeColumnCount = 39;
        private const int WellColumnCount = 39;
        private const int DataStartRowIndex = 4;      // Excel 第 5 行，模板预留第一条数据行
        private const int SummaryRowIndex = 5;        // Excel 第 6 行，模板预留合计行

        private static readonly Dictionary<short, ICellStyle> NumberStyleCache = new Dictionary<short, ICellStyle>();
        private static readonly Dictionary<short, ICellStyle> RedTextStyleCache = new Dictionary<short, ICellStyle>();

        public static void Export(string filePath, QuantityCalculationReport report)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("未指定保存路径。", "filePath");
            if (report == null) report = new QuantityCalculationReport();

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            string templatePath = ResolveTemplatePath();
            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            {
                throw new FileNotFoundException("未找到工程量表模板文件。请将模板放在插件所在目录的 Templates 文件夹下。", TemplateFileName);
            }

            if (string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(templatePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("输出文件不能直接覆盖工程量表模板，请另存为其他文件名。");
            }

            NumberStyleCache.Clear();
            RedTextStyleCache.Clear();

            HSSFWorkbook workbook = OpenTemplateWorkbook(templatePath);
            WritePipeSheet(workbook, report.MainPipes);
            WriteWellSheet(workbook, report.Wells);

            using (FileStream output = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                workbook.Write(output);
            }
        }

        private static string ResolveTemplatePath()
        {
            // 固定读取插件所在目录下的 Templates 文件夹。
            // 不再复制到 %AppData%，避免用户替换模板后插件仍读取旧模板。
            string assemblyDir = GetAssemblyDirectory();
            string pluginTemplate = Path.Combine(assemblyDir, "Templates", TemplateFileName);
            if (File.Exists(pluginTemplate)) return pluginTemplate;

            string baseDirTemplate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", TemplateFileName);
            if (File.Exists(baseDirTemplate)) return baseDirTemplate;

            return pluginTemplate;
        }

        private static string GetAssemblyDirectory()
        {
            try
            {
                string location = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrWhiteSpace(location))
                {
                    string dir = Path.GetDirectoryName(location);
                    if (!string.IsNullOrWhiteSpace(dir)) return dir;
                }
            }
            catch
            {
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static HSSFWorkbook OpenTemplateWorkbook(string templatePath)
        {
            try
            {
                using (FileStream input = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return new HSSFWorkbook(input);
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message ?? string.Empty;
                if (message.IndexOf("Invalid built-in function index", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new InvalidOperationException(
                        "读取工程量模板失败：模板中存在 NPOI 无法处理的 Excel 公式记录。" +
                        "请先用 Excel 或 WPS 打开模板并另存为 .xls，然后放到插件所在目录的 Templates 文件夹下。", ex);
                }
                throw;
            }
        }

        private static void WritePipeSheet(IWorkbook workbook, IList<QuantityMainPipeCalculationRow> rows)
        {
            ISheet sheet = workbook.GetSheet(PipeSheetName);
            if (sheet == null) throw new InvalidOperationException("模板中未找到工作表：" + PipeSheetName);

            int dataCount = rows == null ? 0 : rows.Count;
            PrepareDataRows(sheet, DataStartRowIndex, SummaryRowIndex, Math.Max(1, dataCount), PipeColumnCount);

            double totalDn200 = 0.0;
            double totalDn300 = 0.0;
            double[] totals = new double[12];

            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    QuantityMainPipeCalculationRow row = rows[i];
                    if (row == null) continue;

                    int rowIndex = DataStartRowIndex + i;
                    IRow excelRow = EnsureRow(sheet, rowIndex, PipeColumnCount);
                    ClearRowValues(excelRow, PipeColumnCount);

                    bool isDn300 = ContainsAny(row.Diameter, "300", "DN300", "D300");
                    if (isDn300) totalDn300 += row.Length;
                    else totalDn200 += row.Length;

                    QuantityPipeLayerHeights h = QuantityPipeLayerHeights.FromRow(row);
                    double pipeRadius = row.PipeOuterDiameter > 0 ? row.PipeOuterDiameter / 2.0 : InferPipeRadius(row.Diameter);
                    string pipeVolumeFormula = "3.14*" + Format(pipeRadius) + "^2*" + Format(row.Length);

                    SetText(excelRow, 0, row.StartNode);
                    SetText(excelRow, 1, row.EndNode);
                    SetText(excelRow, 2, row.Material);
                    if (isDn300)
                    {
                        SetBlank(excelRow, 3);
                        SetText(excelRow, 4, "DN200");
                        SetNumber(excelRow, 5, row.Length);
                        SetText(excelRow, 6, string.IsNullOrWhiteSpace(row.Diameter) ? "DN300" : row.Diameter);
                    }
                    else
                    {
                        SetNumber(excelRow, 3, row.Length);
                        SetText(excelRow, 4, string.IsNullOrWhiteSpace(row.Diameter) ? "DN200" : row.Diameter);
                        SetBlank(excelRow, 5);
                        SetText(excelRow, 6, "DN300");
                    }
                    SetNumber(excelRow, 7, row.StartDepth);
                    SetNumber(excelRow, 8, row.EndDepth);
                    SetNumber(excelRow, 9, row.AverageDepth);
                    SetNumber(excelRow, 10, row.TrenchWidth);
                    SetNumber(excelRow, 11, row.RoadThickness);

                    SetFormulaAndValue(excelRow, 12, 13, row.RoadCutting, row.RoadThickness > 0 ? Format(row.Length) + "*2" : string.Empty);
                    SetFormulaAndValue(excelRow, 14, 15, row.RoadBreaking, row.RoadThickness > 0 ? Format(row.Length) + "*" + Format(row.TrenchWidth) : string.Empty);
                    SetFormulaAndValue(excelRow, 16, 17, row.RoadWaste, row.RoadThickness > 0 ? Format(row.Length) + "*" + Format(row.TrenchWidth) + "*" + Format(row.RoadThickness) : string.Empty);
                    SetFormulaAndValue(excelRow, 18, 19, row.MechanicalExcavation, row.MechanicalExcavation > 0 ? Format(row.Length) + "*" + Format(row.TrenchWidth) + "*(" + Format(row.AverageDepth) + "-" + Format(row.RoadThickness) + ")" : string.Empty);
                    SetFormulaAndValue(excelRow, 20, 21, row.ManualExcavation, row.ManualExcavation > 0 ? Format(row.Length) + "*" + Format(row.TrenchWidth) + "*(" + Format(row.AverageDepth) + "-" + Format(row.RoadThickness) + ")" : string.Empty);

                    SetNumber(excelRow, 22, h.SandCushionHeight);
                    SetFormulaAndValue(excelRow, 23, 24, row.SandCushion, h.SandCushionHeight > 0 ? Format(row.Length) + "*" + Format(row.TrenchWidth) + "*" + Format(h.SandCushionHeight) : string.Empty);

                    string sandBackfillFormula = string.Empty;
                    if (row.SandBackfill > 0)
                    {
                        if (h.SandBackfillHeight > 0)
                            sandBackfillFormula = Format(row.Length) + "*" + Format(row.TrenchWidth) + "*" + Format(h.SandBackfillHeight) + (row.PipeDeductionTarget == "sand" ? "-" + pipeVolumeFormula : string.Empty);
                        else
                            sandBackfillFormula = Format(row.Length) + "*" + Format(row.TrenchWidth) + "*(" + Format(row.AverageDepth) + "-" + Format(h.C25RestoreHeight) + "-" + Format(h.GravelHeight) + "-" + Format(h.SandCushionHeight) + ")" + (row.PipeDeductionTarget == "sand" ? "-" + pipeVolumeFormula : string.Empty);
                    }
                    SetFormulaAndValue(excelRow, 25, 26, row.SandBackfill, sandBackfillFormula);

                    SetNumber(excelRow, 27, h.GravelHeight);
                    SetFormulaAndValue(excelRow, 28, 29, row.GravelCushion, h.GravelHeight > 0 ? Format(row.Length) + "*" + Format(row.TrenchWidth) + "*" + Format(h.GravelHeight) : string.Empty);

                    SetNumber(excelRow, 30, h.C25RestoreHeight);
                    SetFormulaAndValue(excelRow, 31, 32, row.C25Restore, h.C25RestoreHeight > 0 ? Format(row.Length) + "*" + Format(row.TrenchWidth) + "*" + Format(h.C25RestoreHeight) : string.Empty);

                    double soilHeight = h.OriginalSoilBackfillHeight;
                    double pipeBaseArea = row.Length * row.TrenchWidth;
                    if (soilHeight <= 0 && pipeBaseArea > 0 && row.OriginalSoilBackfill > 0)
                        soilHeight = (row.OriginalSoilBackfill + (row.PipeDeductionTarget == "soil" ? row.PipeDeductionVolume : 0.0)) / pipeBaseArea;
                    string soilFormula = row.OriginalSoilBackfill > 0
                        ? Format(row.Length) + "*" + Format(row.TrenchWidth) + "*" + Format(soilHeight) + (row.PipeDeductionTarget == "soil" ? "-" + pipeVolumeFormula : string.Empty)
                        : string.Empty;
                    SetFormulaAndValue(excelRow, 33, 34, row.OriginalSoilBackfill, soilFormula);
                    string encasementFormula = row.C25PipeEncasement > 0
                        ? Format(row.Length) + "*" + Format(row.TrenchWidth) + "*" + Format(h.PipeEncasementHeight) + (row.PipeDeductionTarget == "encasement" ? "-" + pipeVolumeFormula : string.Empty)
                        : string.Empty;
                    SetFormulaAndValue(excelRow, 35, 36, row.C25PipeEncasement, encasementFormula);
                    SetNumber(excelRow, 37, row.EarthworkOut);
                    SetText(excelRow, 38, string.Empty);

                    totals[0] += row.RoadCutting;
                    totals[1] += row.RoadBreaking;
                    totals[2] += row.RoadWaste;
                    totals[3] += row.MechanicalExcavation;
                    totals[4] += row.ManualExcavation;
                    totals[5] += row.SandCushion;
                    totals[6] += row.SandBackfill;
                    totals[7] += row.GravelCushion;
                    totals[8] += row.C25Restore;
                    totals[9] += row.OriginalSoilBackfill;
                    totals[10] += row.C25PipeEncasement;
                    totals[11] += row.EarthworkOut;
                }
            }

            int summaryRowIndex = DataStartRowIndex + Math.Max(1, dataCount);
            IRow summary = EnsureRow(sheet, summaryRowIndex, PipeColumnCount);
            ClearRowValues(summary, PipeColumnCount);
            SetText(summary, 0, "合计");
            SetNumber(summary, 3, totalDn200, false);
            SetText(summary, 4, "DN200");
            SetNumber(summary, 5, totalDn300, false);
            SetText(summary, 6, "DN300");
            SetNumber(summary, 13, totals[0], false);
            SetNumber(summary, 15, totals[1], false);
            SetNumber(summary, 17, totals[2], false);
            SetNumber(summary, 19, totals[3], false);
            SetNumber(summary, 21, totals[4], false);
            SetNumber(summary, 24, totals[5], false);
            SetNumber(summary, 26, totals[6], false);
            SetNumber(summary, 29, totals[7], false);
            SetNumber(summary, 32, totals[8], false);
            SetNumber(summary, 34, totals[9], false);
            SetNumber(summary, 36, totals[10], false);
            SetNumber(summary, 37, totals[11], false);
            // 模板行已通过插入方式下移，合计行下方内容应保留，不再清空。
        }

        private static void WriteWellSheet(IWorkbook workbook, IList<QuantityWellCalculationRow> rows)
        {
            ISheet sheet = workbook.GetSheet(WellSheetName);
            if (sheet == null) throw new InvalidOperationException("模板中未找到工作表：" + WellSheetName);

            int dataCount = rows == null ? 0 : rows.Count;
            PrepareDataRows(sheet, DataStartRowIndex, SummaryRowIndex, Math.Max(1, dataCount), WellColumnCount);

            double[] totals = new double[11];
            int cover1200 = 0;
            int cover1600 = 0;
            int cover700 = 0;
            int cover500 = 0;
            int cover500Concrete = 0;

            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    QuantityWellCalculationRow row = rows[i];
                    if (row == null) continue;

                    int rowIndex = DataStartRowIndex + i;
                    IRow excelRow = EnsureRow(sheet, rowIndex, WellColumnCount);
                    ClearRowValues(excelRow, WellColumnCount);

                    QuantityWellLayerHeights h = QuantityWellLayerHeights.FromRow(row);
                    string checkType = BuildWellCheckType(row);
                    bool isSiltWell = ContainsAny(checkType, "沉泥");
                    string coverPlateColumn = GetCoverPlateColumn(row.CoverPlate);
                    string wellCoverColumn = GetWellCoverColumn(row.WellCoverMaterial);

                    SetText(excelRow, 0, row.NodeNo);
                    SetNumber(excelRow, 1, row.WellDepth);
                    SetText(excelRow, 2, checkType, isSiltWell);
                    SetNumber(excelRow, 3, row.WellRadius);
                    SetNumber(excelRow, 4, row.WellArea);
                    SetNumber(excelRow, 5, row.RoadThickness);
                    SetNumber(excelRow, 6, row.ExcavationLength);
                    SetNumber(excelRow, 7, row.ExcavationWidth);
                    SetFormulaAndValue(excelRow, 8, 9, row.RoadCutting, row.RoadThickness > 0 ? Format(row.ExcavationLength) + "*2+" + Format(row.ExcavationWidth) + "*2" : string.Empty);
                    SetFormulaAndValue(excelRow, 10, 11, row.RoadBreaking, row.RoadThickness > 0 ? Format(row.ExcavationLength) + "*" + Format(row.ExcavationWidth) : string.Empty);
                    SetFormulaAndValue(excelRow, 12, 13, row.RoadWaste, row.RoadThickness > 0 ? Format(row.ExcavationLength) + "*" + Format(row.ExcavationWidth) + "*" + Format(row.RoadThickness) : string.Empty);
                    SetFormulaAndValue(excelRow, 14, 15, row.MechanicalExcavation, row.MechanicalExcavation > 0 ? Format(row.ExcavationLength) + "*" + Format(row.ExcavationWidth) + "*(" + Format(row.WellDepth) + "-" + Format(row.RoadThickness) + "+" + Format(h.WellBottomCushionHeight) + ")" : string.Empty);
                    SetFormulaAndValue(excelRow, 16, 17, row.ManualExcavation, row.ManualExcavation > 0 ? Format(row.ExcavationLength) + "*" + Format(row.ExcavationWidth) + "*(" + Format(row.WellDepth) + "-" + Format(row.RoadThickness) + "+" + Format(h.WellBottomCushionHeight) + ")" : string.Empty);
                    SetNumber(excelRow, 18, h.SandCushionHeight);
                    SetFormulaAndValue(excelRow, 19, 20, row.SandCushion, h.SandCushionHeight > 0 ? Format(row.ExcavationLength) + "*" + Format(row.ExcavationWidth) + "*" + Format(h.SandCushionHeight) : string.Empty);
                    SetNumber(excelRow, 21, h.C25CushionHeight);
                    SetFormulaAndValue(excelRow, 22, 23, row.C25Cushion, h.C25CushionHeight > 0 ? Format(row.ExcavationLength) + "*" + Format(row.ExcavationWidth) + "*" + Format(h.C25CushionHeight) : string.Empty);
                    SetNumber(excelRow, 24, h.CoverGravelHeight);
                    SetFormulaAndValue(excelRow, 25, 26, row.CoverPlateGravelCushion, h.CoverGravelHeight > 0 ? "(" + Format(row.ExcavationLength) + "*" + Format(row.ExcavationWidth) + "-" + Format(row.WellArea) + ")*" + Format(h.CoverGravelHeight) : string.Empty);
                    SetNumber(excelRow, 27, h.CoverC25Height);
                    SetFormulaAndValue(excelRow, 28, 29, row.CoverPlateC25Foundation, h.CoverC25Height > 0 ? "(" + Format(row.ExcavationLength) + "*" + Format(row.ExcavationWidth) + "-" + Format(row.WellArea) + ")*" + Format(h.CoverC25Height) : string.Empty);
                    string sandBackfillFormula = string.Empty;
                    if (row.SandBackfill > 0)
                    {
                        if (h.SandBackfillHeight > 0) sandBackfillFormula = "(" + Format(row.ExcavationLength) + "*" + Format(row.ExcavationWidth) + "-" + Format(row.WellArea) + ")*" + Format(h.SandBackfillHeight);
                        else sandBackfillFormula = "(" + Format(row.ExcavationLength) + "*" + Format(row.ExcavationWidth) + "-" + Format(row.WellArea) + ")*(" + Format(row.WellDepth) + "-" + Format(h.C25CushionHeight) + "-" + Format(h.CoverGravelHeight) + "-" + Format(h.CoverC25Height) + ")";
                    }
                    SetFormulaAndValue(excelRow, 30, 31, row.SandBackfill, sandBackfillFormula);
                    SetNumber(excelRow, 32, row.EarthworkOut);
                    SetText(excelRow, 33, coverPlateColumn == "1200" ? row.CoverPlate : string.Empty);
                    SetText(excelRow, 34, coverPlateColumn == "1600" ? row.CoverPlate : string.Empty);
                    SetText(excelRow, 35, wellCoverColumn == "700" ? row.WellCoverMaterial : string.Empty);
                    SetText(excelRow, 36, wellCoverColumn == "500" ? row.WellCoverMaterial : string.Empty);
                    SetText(excelRow, 37, wellCoverColumn == "500砼" ? row.WellCoverMaterial : string.Empty);
                    SetBlank(excelRow, 38);

                    totals[0] += row.RoadCutting;
                    totals[1] += row.RoadBreaking;
                    totals[2] += row.RoadWaste;
                    totals[3] += row.MechanicalExcavation;
                    totals[4] += row.ManualExcavation;
                    totals[5] += row.SandCushion;
                    totals[6] += row.C25Cushion;
                    totals[7] += row.CoverPlateGravelCushion;
                    totals[8] += row.CoverPlateC25Foundation;
                    totals[9] += row.SandBackfill;
                    totals[10] += row.EarthworkOut;
                    if (row.CoverPlateCount > 0)
                    {
                        if (coverPlateColumn == "1600") cover1600 += row.CoverPlateCount;
                        else cover1200 += row.CoverPlateCount;
                    }
                    if (row.WellCoverCount > 0)
                    {
                        if (wellCoverColumn == "700") cover700 += row.WellCoverCount;
                        else if (wellCoverColumn == "500砼") cover500Concrete += row.WellCoverCount;
                        else cover500 += row.WellCoverCount;
                    }
                }
            }

            int summaryRowIndex = DataStartRowIndex + Math.Max(1, dataCount);
            IRow summary = EnsureRow(sheet, summaryRowIndex, WellColumnCount);
            ClearRowValues(summary, WellColumnCount);
            SetText(summary, 0, "合计");
            SetNumber(summary, 9, totals[0], false);
            SetNumber(summary, 11, totals[1], false);
            SetNumber(summary, 13, totals[2], false);
            SetNumber(summary, 15, totals[3], false);
            SetNumber(summary, 17, totals[4], false);
            SetNumber(summary, 20, totals[5], false);
            SetNumber(summary, 23, totals[6], false);
            SetNumber(summary, 26, totals[7], false);
            SetNumber(summary, 29, totals[8], false);
            SetNumber(summary, 31, totals[9], false);
            SetNumber(summary, 32, totals[10], false);
            SetText(summary, 33, cover1200 > 0 ? cover1200.ToString(CultureInfo.InvariantCulture) + "个" : string.Empty);
            SetText(summary, 34, cover1600 > 0 ? cover1600.ToString(CultureInfo.InvariantCulture) + "个" : string.Empty);
            SetText(summary, 35, cover700 > 0 ? cover700.ToString(CultureInfo.InvariantCulture) + "个" : string.Empty);
            SetText(summary, 36, cover500 > 0 ? cover500.ToString(CultureInfo.InvariantCulture) + "个" : string.Empty);
            SetText(summary, 37, cover500Concrete > 0 ? cover500Concrete.ToString(CultureInfo.InvariantCulture) + "个" : string.Empty);
            // 模板行已通过插入方式下移，合计行下方内容应保留，不再清空。
        }

        private static void PrepareDataRows(ISheet sheet, int dataStartRow, int summaryRow, int dataRowCount, int columnCount)
        {
            dataRowCount = Math.Max(1, dataRowCount);

            IRow dataTemplateRow = EnsureRow(sheet, dataStartRow, columnCount);
            RowTemplate dataTemplate = RowTemplate.Capture(dataTemplateRow, columnCount);

            // 模板中已预留 1 条数据行。
            // 当实际有 N 条数据时，只需要在“合计”行之上再插入 N-1 行，
            // 最终合计行上方刚好是 N 条数据行。
            int insertCount = Math.Max(0, dataRowCount - 1);
            if (insertCount > 0)
            {
                InsertRowsManually(sheet, summaryRow, insertCount, columnCount);
            }

            for (int i = 0; i < dataRowCount; i++)
            {
                int rowIndex = dataStartRow + i;
                IRow row = EnsureRow(sheet, rowIndex, columnCount);
                dataTemplate.ApplyTo(row);
                ClearRowValues(row, columnCount);
            }

            IRow summary = EnsureRow(sheet, dataStartRow + dataRowCount, columnCount);
            ClearRowValues(summary, columnCount);
        }

        private static void InsertRowsManually(ISheet sheet, int insertAtRow, int insertCount, int columnCount)
        {
            if (sheet == null || insertCount <= 0) return;

            MoveMergedRegions(sheet, insertAtRow, insertCount);

            int lastRow = sheet.LastRowNum;
            for (int r = lastRow; r >= insertAtRow; r--)
            {
                IRow source = sheet.GetRow(r);
                IRow target = sheet.GetRow(r + insertCount) ?? sheet.CreateRow(r + insertCount);

                if (source == null)
                {
                    ClearRowValues(target, columnCount);
                    continue;
                }

                CopyRow(source, target, columnCount);
                ClearRowValues(source, columnCount);
            }
        }

        private static void MoveMergedRegions(ISheet sheet, int startRow, int offset)
        {
            if (sheet == null || offset <= 0) return;

            for (int i = sheet.NumMergedRegions - 1; i >= 0; i--)
            {
                CellRangeAddress region = sheet.GetMergedRegion(i);
                if (region == null) continue;

                if (region.FirstRow >= startRow)
                {
                    sheet.RemoveMergedRegion(i);
                    CellRangeAddress moved = new CellRangeAddress(
                        region.FirstRow + offset,
                        region.LastRow + offset,
                        region.FirstColumn,
                        region.LastColumn);
                    sheet.AddMergedRegion(moved);
                }
                else if (region.LastRow >= startRow)
                {
                    sheet.RemoveMergedRegion(i);
                    CellRangeAddress expanded = new CellRangeAddress(
                        region.FirstRow,
                        region.LastRow + offset,
                        region.FirstColumn,
                        region.LastColumn);
                    sheet.AddMergedRegion(expanded);
                }
            }
        }

        private static void CopyRow(IRow source, IRow target, int columnCount)
        {
            if (source == null || target == null) return;

            target.Height = source.Height;
            for (int c = 0; c < columnCount; c++)
            {
                ICell src = source.GetCell(c);
                ICell dst = EnsureCell(target, c);
                CopyCell(src, dst);
            }
        }

        private static void CopyCell(ICell source, ICell target)
        {
            if (target == null) return;
            if (source == null)
            {
                target.SetCellType(CellType.Blank);
                return;
            }

            if (source.CellStyle != null) target.CellStyle = source.CellStyle;

            switch (source.CellType)
            {
                case CellType.String:
                    target.SetCellValue(source.StringCellValue ?? string.Empty);
                    break;
                case CellType.Numeric:
                    target.SetCellValue(source.NumericCellValue);
                    break;
                case CellType.Boolean:
                    target.SetCellValue(source.BooleanCellValue);
                    break;
                case CellType.Error:
                    target.SetCellErrorValue(source.ErrorCellValue);
                    break;
                case CellType.Formula:
                    // 不复制模板公式，避免 NPOI 在 .xls 模板中遇到不兼容公式记录。
                    target.SetCellType(CellType.Blank);
                    break;
                default:
                    target.SetCellType(CellType.Blank);
                    break;
            }
        }

        private static IRow EnsureRow(ISheet sheet, int rowIndex, int columnCount)
        {
            IRow row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
            for (int c = 0; c < columnCount; c++) EnsureCell(row, c);
            return row;
        }

        private static ICell EnsureCell(IRow row, int columnIndex)
        {
            return row.GetCell(columnIndex) ?? row.CreateCell(columnIndex);
        }

        private static void CopyRowStyle(IRow source, IRow target, int columnCount)
        {
            if (source == null || target == null) return;
            target.Height = source.Height;
            for (int c = 0; c < columnCount; c++)
            {
                ICell src = source.GetCell(c);
                ICell dst = EnsureCell(target, c);
                if (src != null && src.CellStyle != null) dst.CellStyle = src.CellStyle;
            }
        }

        private static void ClearRowValues(IRow row, int columnCount)
        {
            if (row == null) return;
            for (int c = 0; c < columnCount; c++) SetBlank(row, c);
        }

        private static void ClearRowsAfter(ISheet sheet, int rowIndex, int columnCount)
        {
            for (int r = rowIndex + 1; r <= sheet.LastRowNum; r++)
            {
                IRow row = sheet.GetRow(r);
                if (row == null) continue;
                ClearRowValues(row, columnCount);
            }
        }

        private static void SetBlank(IRow row, int columnIndex)
        {
            ICell cell = EnsureCell(row, columnIndex);
            cell.SetCellType(CellType.Blank);
        }

        private static void SetText(IRow row, int columnIndex, string value)
        {
            SetText(row, columnIndex, value, false);
        }

        private static void SetText(IRow row, int columnIndex, string value, bool red)
        {
            ICell cell = EnsureCell(row, columnIndex);
            if (red) cell.CellStyle = GetRedTextStyle(row.Sheet.Workbook, cell.CellStyle);
            cell.SetCellValue(value ?? string.Empty);
        }

        private static void SetNumber(IRow row, int columnIndex, double value)
        {
            SetNumber(row, columnIndex, value, true);
        }

        private static void SetNumber(IRow row, int columnIndex, double value, bool blankIfZero)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) value = 0.0;
            value = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            if (blankIfZero && Math.Abs(value) < 0.0000001)
            {
                SetBlank(row, columnIndex);
                return;
            }

            ICell cell = EnsureCell(row, columnIndex);
            cell.CellStyle = GetNumberStyle(row.Sheet.Workbook, cell.CellStyle);
            cell.SetCellValue(value);
        }

        private static void SetFormulaAndValue(IRow row, int formulaColumn, int valueColumn, double value, string formulaText)
        {
            if (string.IsNullOrWhiteSpace(formulaText) && Math.Abs(value) < 0.0000001)
            {
                SetBlank(row, formulaColumn);
                SetBlank(row, valueColumn);
                return;
            }

            SetText(row, formulaColumn, formulaText ?? string.Empty);
            SetNumber(row, valueColumn, value);
        }

        private static ICellStyle GetNumberStyle(IWorkbook workbook, ICellStyle baseStyle)
        {
            short key = baseStyle == null ? (short)-1 : baseStyle.Index;
            ICellStyle style;
            if (NumberStyleCache.TryGetValue(key, out style)) return style;

            style = workbook.CreateCellStyle();
            if (baseStyle != null) style.CloneStyleFrom(baseStyle);
            style.DataFormat = workbook.CreateDataFormat().GetFormat("0.00");
            NumberStyleCache[key] = style;
            return style;
        }

        private static ICellStyle GetRedTextStyle(IWorkbook workbook, ICellStyle baseStyle)
        {
            short key = baseStyle == null ? (short)-1 : baseStyle.Index;
            ICellStyle style;
            if (RedTextStyleCache.TryGetValue(key, out style)) return style;

            style = workbook.CreateCellStyle();
            if (baseStyle != null) style.CloneStyleFrom(baseStyle);

            IFont baseFont = baseStyle == null ? null : baseStyle.GetFont(workbook);
            IFont redFont = workbook.CreateFont();
            if (baseFont != null)
            {
                redFont.FontName = baseFont.FontName;
                redFont.FontHeightInPoints = baseFont.FontHeightInPoints;
                redFont.IsBold = baseFont.IsBold;
                redFont.IsItalic = baseFont.IsItalic;
                redFont.Underline = baseFont.Underline;
            }
            redFont.Color = IndexedColors.Red.Index;
            style.SetFont(redFont);
            RedTextStyleCache[key] = style;
            return style;
        }

        private static string BuildWellCheckType(QuantityWellCalculationRow row)
        {
            if (row == null) return string.Empty;
            string spec = row.WellSpec ?? string.Empty;
            string material = row.WellMaterialType ?? string.Empty;
            string type = row.WellType ?? string.Empty;

            if (ContainsAny(spec, "检查井", "沉泥井", "跌水井"))
            {
                if (!string.IsNullOrWhiteSpace(material) && spec.IndexOf(material, StringComparison.CurrentCultureIgnoreCase) < 0)
                {
                    return spec + "（" + material + "）";
                }
                return spec;
            }

            string result = string.Empty;
            if (!string.IsNullOrWhiteSpace(spec)) result += spec;
            if (!string.IsNullOrWhiteSpace(material)) result += material;
            if (!string.IsNullOrWhiteSpace(type)) result += type;
            return result;
        }

        private static string GetCoverPlateColumn(string coverPlate)
        {
            if (ContainsAny(coverPlate, "1600", "1.6")) return "1600";
            return "1200";
        }

        private static string GetWellCoverColumn(string wellCover)
        {
            if (ContainsAny(wellCover, "700", "φ700", "Φ700")) return "700";
            if (ContainsAny(wellCover, "钢筋", "混凝土", "砼")) return "500砼";
            return "500";
        }

        private static double InferPipeRadius(string diameter)
        {
            if (string.IsNullOrWhiteSpace(diameter)) return 0.0;
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(diameter, @"(?<n>\d{2,4})");
            if (!m.Success) return 0.0;
            double value;
            if (!double.TryParse(m.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return 0.0;
            return value / 2000.0;
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(text) || values == null) return false;
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static string Format(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) value = 0.0;
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private sealed class QuantityPipeLayerHeights
        {
            public double SandCushionHeight;
            public double SandBackfillHeight;
            public double GravelHeight;
            public double C25RestoreHeight;
            public double OriginalSoilBackfillHeight;
            public double PipeEncasementHeight;

            public static QuantityPipeLayerHeights FromRow(QuantityMainPipeCalculationRow row)
            {
                QuantityPipeLayerHeights h = new QuantityPipeLayerHeights();
                if (row == null) return h;
                List<QuantityStructureLayer> layers = QuantityStructureLayer.Parse(row.BackfillStructure);
                h.SandCushionHeight = SumHeight(layers, QuantityStructureLayer.IsSandCushion);
                h.SandBackfillHeight = SumHeight(layers, QuantityStructureLayer.IsSandBackfill);
                h.GravelHeight = SumHeight(layers, QuantityStructureLayer.IsGravel);
                h.C25RestoreHeight = SumHeight(layers, IsPipeC25RestoreLayer);
                h.OriginalSoilBackfillHeight = SumHeight(layers, QuantityStructureLayer.IsOriginalSoilBackfill);
                h.PipeEncasementHeight = SumHeight(layers, IsPipeEncasementLayer);

                double baseArea = row.Length * row.TrenchWidth;
                if (h.SandBackfillHeight <= 0 && baseArea > 0 && row.SandBackfill > 0)
                    h.SandBackfillHeight = (row.SandBackfill + (row.PipeDeductionTarget == "sand" ? row.PipeDeductionVolume : 0.0)) / baseArea;
                if (h.SandCushionHeight <= 0 && baseArea > 0 && row.SandCushion > 0) h.SandCushionHeight = row.SandCushion / baseArea;
                if (h.GravelHeight <= 0 && baseArea > 0 && row.GravelCushion > 0) h.GravelHeight = row.GravelCushion / baseArea;
                if (h.C25RestoreHeight <= 0 && baseArea > 0 && row.C25Restore > 0) h.C25RestoreHeight = row.C25Restore / baseArea;
                if (h.PipeEncasementHeight <= 0 && baseArea > 0 && row.C25PipeEncasement > 0)
                    h.PipeEncasementHeight = (row.C25PipeEncasement + (row.PipeDeductionTarget == "encasement" ? row.PipeDeductionVolume : 0.0)) / baseArea;
                return h;
            }

            private static bool IsPipeC25RestoreLayer(QuantityStructureLayer layer)
            {
                if (layer == null) return false;
                return QuantityStructureLayer.IsC25Restore(layer);
            }

            private static bool IsPipeEncasementLayer(QuantityStructureLayer layer)
            {
                if (layer == null) return false;
                return QuantityStructureLayer.IsConcretePipeEncasement(layer);
            }
        }

        private sealed class QuantityWellLayerHeights
        {
            public double SandCushionHeight;
            public double WellBottomCushionHeight;
            public double C25CushionHeight;
            public double CoverGravelHeight;
            public double CoverC25Height;
            public double SandBackfillHeight;

            public static QuantityWellLayerHeights FromRow(QuantityWellCalculationRow row)
            {
                QuantityWellLayerHeights h = new QuantityWellLayerHeights();
                if (row == null) return h;
                List<QuantityStructureLayer> layers = QuantityStructureLayer.Parse(row.BackfillStructure);
                h.SandCushionHeight = SumHeight(layers, QuantityStructureLayer.IsSandCushion);
                h.WellBottomCushionHeight = SumHeight(layers, IsWellBottomLayer);
                if (h.WellBottomCushionHeight <= 0) h.WellBottomCushionHeight = h.SandCushionHeight;
                h.C25CushionHeight = SumHeight(layers, IsWellC25CushionLayer);
                h.CoverGravelHeight = SumHeight(layers, IsCoverPlateGravelLayer);
                h.CoverC25Height = SumHeight(layers, IsCoverPlateC25Layer);
                h.SandBackfillHeight = SumHeight(layers, QuantityStructureLayer.IsSandBackfill);

                double area = row.ExcavationLength * row.ExcavationWidth;
                double backfillArea = Math.Max(0.0, area - row.WellArea);
                if (h.SandCushionHeight <= 0 && area > 0 && row.SandCushion > 0) h.SandCushionHeight = row.SandCushion / area;
                if (h.C25CushionHeight <= 0 && area > 0 && row.C25Cushion > 0) h.C25CushionHeight = row.C25Cushion / area;
                if (h.CoverGravelHeight <= 0 && backfillArea > 0 && row.CoverPlateGravelCushion > 0) h.CoverGravelHeight = row.CoverPlateGravelCushion / backfillArea;
                if (h.CoverC25Height <= 0 && backfillArea > 0 && row.CoverPlateC25Foundation > 0) h.CoverC25Height = row.CoverPlateC25Foundation / backfillArea;
                if (h.SandBackfillHeight <= 0 && backfillArea > 0 && row.SandBackfill > 0) h.SandBackfillHeight = row.SandBackfill / backfillArea;
                return h;
            }

            private static bool IsWellBottomLayer(QuantityStructureLayer layer)
            {
                return layer != null && layer.IsPipeLayer;
            }

            private static bool IsWellC25CushionLayer(QuantityStructureLayer layer)
            {
                if (layer == null) return false;
                string text = (layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty);
                if (!QuantityStructureLayer.IsC25(layer)) return false;
                if (QuantityStructureLayer.IsCoverPlateLayer(layer)) return false;
                return QuantityStructureLayer.ContainsAny(text, "垫层", "基础");
            }

            private static bool IsCoverPlateGravelLayer(QuantityStructureLayer layer)
            {
                return QuantityStructureLayer.IsCoverPlateLayer(layer) && QuantityStructureLayer.IsGravel(layer);
            }

            private static bool IsCoverPlateC25Layer(QuantityStructureLayer layer)
            {
                return QuantityStructureLayer.IsCoverPlateLayer(layer) && QuantityStructureLayer.IsC25(layer);
            }
        }

        private sealed class RowTemplate
        {
            private readonly short _height;
            private readonly ICellStyle[] _styles;

            private RowTemplate(short height, ICellStyle[] styles)
            {
                _height = height;
                _styles = styles ?? new ICellStyle[0];
            }

            public static RowTemplate Capture(IRow row, int columnCount)
            {
                ICellStyle[] styles = new ICellStyle[columnCount];
                if (row != null)
                {
                    for (int c = 0; c < columnCount; c++)
                    {
                        ICell cell = row.GetCell(c);
                        if (cell != null) styles[c] = cell.CellStyle;
                    }
                    return new RowTemplate(row.Height, styles);
                }
                return new RowTemplate((short)-1, styles);
            }

            public void ApplyTo(IRow row)
            {
                if (row == null) return;
                if (_height > 0) row.Height = _height;
                for (int c = 0; c < _styles.Length; c++)
                {
                    ICell cell = row.GetCell(c) ?? row.CreateCell(c);
                    if (_styles[c] != null) cell.CellStyle = _styles[c];
                }
            }
        }

        private static double SumHeight(List<QuantityStructureLayer> layers, Predicate<QuantityStructureLayer> predicate)
        {
            return QuantityStructureLayer.SumHeight(layers, predicate);
        }
    }
}
