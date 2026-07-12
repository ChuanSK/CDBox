using System;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    public static class QuantityDashboardExportService
    {
        public static void ExportReference(string filePath, QuantityDashboardSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("未指定保存路径。", "filePath");
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("当前工程量参考");
            ICellStyle titleStyle = workbook.CreateCellStyle();
            IFont titleFont = workbook.CreateFont();
            titleFont.IsBold = true;
            titleFont.FontHeightInPoints = 16;
            titleStyle.SetFont(titleFont);
            ICellStyle headerStyle = workbook.CreateCellStyle();
            IFont headerFont = workbook.CreateFont();
            headerFont.IsBold = true;
            headerStyle.SetFont(headerFont);
            headerStyle.FillForegroundColor = IndexedColors.Grey25Percent.Index;
            headerStyle.FillPattern = FillPattern.SolidForeground;
            ICellStyle numberStyle = workbook.CreateCellStyle();
            numberStyle.DataFormat = workbook.CreateDataFormat().GetFormat("0.00");
            ICellStyle percentStyle = workbook.CreateCellStyle();
            percentStyle.DataFormat = workbook.CreateDataFormat().GetFormat("0.00%");

            IRow title = sheet.CreateRow(0);
            title.CreateCell(0).SetCellValue("当前工程量参考");
            title.GetCell(0).CellStyle = titleStyle;
            sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(0, 0, 0, 4));

            WriteMeta(sheet, 2, "图纸", snapshot.document == null ? string.Empty : snapshot.document.name);
            WriteMeta(sheet, 3, "统计范围", snapshot.scope == null ? string.Empty : snapshot.scope.regionName);
            WriteMeta(sheet, 4, "统计规则", snapshot.scope == null ? string.Empty : snapshot.scope.pipeRule);
            WriteMeta(sheet, 5, "更新时间", snapshot.status == null ? string.Empty : snapshot.status.updatedAt);
            WriteMeta(sheet, 6, "数据完整度", snapshot.status == null ? string.Empty : snapshot.status.dataCompleteness.ToString("0.00") + "%");
            WriteMeta(sheet, 7, "说明", "当前结果为基于图纸现有属性的工程量参考估算，用于阶段预算和施工调整，不作为最终结算依据。");

            IRow header = sheet.CreateRow(9);
            string[] headers = { "工程量项目", "工程量", "单位", "备注", "估算来源" };
            for (int i = 0; i < headers.Length; i++)
            {
                ICell cell = header.CreateCell(i);
                cell.SetCellValue(headers[i]);
                cell.CellStyle = headerStyle;
            }

            int rowIndex = 10;
            foreach (QuantityDashboardReferenceItem item in snapshot.referenceItems)
            {
                IRow row = sheet.CreateRow(rowIndex++);
                row.CreateCell(0).SetCellValue(item.item ?? string.Empty);
                ICell quantity = row.CreateCell(1);
                quantity.SetCellValue(item.quantity);
                quantity.CellStyle = numberStyle;
                row.CreateCell(2).SetCellValue(item.unit ?? string.Empty);
                row.CreateCell(3).SetCellValue(item.remark ?? string.Empty);
                row.CreateCell(4).SetCellValue(item.source ?? string.Empty);
            }

            rowIndex += 2;
            IRow sourceHeader = sheet.CreateRow(rowIndex++);
            sourceHeader.CreateCell(0).SetCellValue("估算来源占比");
            sourceHeader.GetCell(0).CellStyle = headerStyle;
            WriteSource(sheet, percentStyle, ref rowIndex, "属性/几何计算", snapshot.sourceSummary.propertyOrGeometryPercent);
            WriteSource(sheet, percentStyle, ref rowIndex, "默认参数估算", snapshot.sourceSummary.defaultEstimatePercent);
            WriteSource(sheet, percentStyle, ref rowIndex, "仅数量统计", snapshot.sourceSummary.countOnlyPercent);
            WriteSource(sheet, percentStyle, ref rowIndex, "无法计算", snapshot.sourceSummary.failedPercent);

            sheet.SetColumnWidth(0, 26 * 256);
            sheet.SetColumnWidth(1, 16 * 256);
            sheet.SetColumnWidth(2, 12 * 256);
            sheet.SetColumnWidth(3, 34 * 256);
            sheet.SetColumnWidth(4, 22 * 256);
            sheet.CreateFreezePane(0, 10);

            using (FileStream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                workbook.Write(stream);
            }
        }

        public static string BuildClipboardText(QuantityDashboardSnapshot snapshot)
        {
            if (snapshot == null) return string.Empty;
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            text.AppendLine("当前工程量参考");
            text.AppendLine("图纸：" + (snapshot.document == null ? string.Empty : snapshot.document.name));
            text.AppendLine("范围：" + (snapshot.scope == null ? string.Empty : snapshot.scope.regionName));
            text.AppendLine("规则：" + (snapshot.scope == null ? string.Empty : snapshot.scope.pipeRule));
            text.AppendLine("更新时间：" + (snapshot.status == null ? string.Empty : snapshot.status.updatedAt));
            text.AppendLine();
            text.AppendLine("工程量项目\t工程量\t单位\t备注\t估算来源");
            foreach (QuantityDashboardReferenceItem item in snapshot.referenceItems)
            {
                text.Append(item.item).Append('\t').Append(item.quantity.ToString("0.00")).Append('\t').Append(item.unit).Append('\t').Append(item.remark).Append('\t').Append(item.source).AppendLine();
            }
            text.AppendLine();
            text.AppendLine("说明：当前结果用于阶段预算和施工调整，不作为最终结算依据。");
            return text.ToString();
        }

        private static void WriteMeta(ISheet sheet, int rowIndex, string name, string value)
        {
            IRow row = sheet.CreateRow(rowIndex);
            row.CreateCell(0).SetCellValue(name);
            row.CreateCell(1).SetCellValue(value ?? string.Empty);
            sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(rowIndex, rowIndex, 1, 4));
        }

        private static void WriteSource(ISheet sheet, ICellStyle percentStyle, ref int rowIndex, string name, double percent)
        {
            IRow row = sheet.CreateRow(rowIndex++);
            row.CreateCell(0).SetCellValue(name);
            ICell valueCell = row.CreateCell(1);
            valueCell.SetCellValue(percent / 100.0);
            valueCell.CellStyle = percentStyle;
        }
    }
}
