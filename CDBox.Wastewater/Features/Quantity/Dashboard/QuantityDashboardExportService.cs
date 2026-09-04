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

        public static void ExportCalculationProcess(string filePath, QuantityDashboardSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("未指定保存路径。", "filePath");
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            if (snapshot.calculationAudit == null) throw new InvalidOperationException("当前统计结果不包含计算过程，请先刷新工程量看板。");
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            IWorkbook workbook = new XSSFWorkbook();
            ICellStyle titleStyle = CreateTitleStyle(workbook);
            ICellStyle headerStyle = CreateHeaderStyle(workbook);
            ICellStyle numberStyle = workbook.CreateCellStyle();
            numberStyle.DataFormat = workbook.CreateDataFormat().GetFormat("0.############");
            ICellStyle wrapStyle = workbook.CreateCellStyle();
            wrapStyle.WrapText = true;

            WriteAuditOverview(workbook, snapshot, titleStyle, headerStyle, numberStyle);
            WritePipeAuditSheet(workbook, snapshot, headerStyle, numberStyle, wrapStyle);
            WriteWellAuditSheet(workbook, snapshot, headerStyle, numberStyle, wrapStyle);
            WriteCalculationStepSheet(workbook, snapshot, headerStyle, numberStyle, wrapStyle);

            using (FileStream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                workbook.Write(stream);
            }
        }

        private static void WriteAuditOverview(IWorkbook workbook, QuantityDashboardSnapshot snapshot, ICellStyle titleStyle, ICellStyle headerStyle, ICellStyle numberStyle)
        {
            ISheet sheet = workbook.CreateSheet("说明与汇总");
            IRow title = sheet.CreateRow(0);
            title.CreateCell(0).SetCellValue("工程量计算过程审计表");
            title.GetCell(0).CellStyle = titleStyle;
            sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(0, 0, 0, 4));
            WriteMeta(sheet, 2, "图纸", snapshot.document == null ? string.Empty : snapshot.document.name);
            WriteMeta(sheet, 3, "统计范围", snapshot.scope == null ? string.Empty : snapshot.scope.regionName);
            WriteMeta(sheet, 4, "统计规则", snapshot.scope == null ? string.Empty : snapshot.scope.pipeRule);
            WriteMeta(sheet, 5, "计算时间", snapshot.status == null ? string.Empty : snapshot.status.updatedAt);
            WriteMeta(sheet, 6, "审计对象", "主管 " + snapshot.calculationAudit.mainPipes.Count + " 条；支管 " + snapshot.calculationAudit.branchPipes.Count + " 条；井 " + snapshot.calculationAudit.wells.Count + " 座");
            WriteMeta(sheet, 7, "数值说明", "计算全程使用未舍入实际值；工作表保留最多 12 位小数，界面汇总最后统一显示两位小数。");
            WriteMeta(sheet, 8, "使用方法", "先按 Handle 或节点编号找到对象，再到“逐项计算过程”核对公式、代入值及结果。零值项目也会保留，便于检查未计入原因。");

            IRow header = sheet.CreateRow(10);
            WriteHeader(header, headerStyle, new[] { "工程量项目", "看板汇总", "单位", "备注", "来源" });
            int rowIndex = 11;
            foreach (QuantityDashboardReferenceItem item in snapshot.referenceItems)
            {
                IRow row = sheet.CreateRow(rowIndex++);
                SetText(row, 0, item.item);
                SetNumber(row, 1, item.quantity, numberStyle);
                SetText(row, 2, item.unit);
                SetText(row, 3, item.remark);
                SetText(row, 4, item.source);
            }
            SetWidths(sheet, new[] { 28, 18, 12, 38, 22 });
            sheet.CreateFreezePane(0, 11);
        }

        private static void WritePipeAuditSheet(IWorkbook workbook, QuantityDashboardSnapshot snapshot, ICellStyle headerStyle, ICellStyle numberStyle, ICellStyle wrapStyle)
        {
            ISheet sheet = workbook.CreateSheet("管线输入与结果");
            string[] headers =
            {
                "类别", "序号", "Handle", "图层", "起点", "终点", "材质", "规格", "长度(m)", "起点深度(m)",
                "终点深度(m)", "平均深度(m)", "沟槽宽度(m)", "路面厚度(m)", "外径(m)", "扣减目标", "管身体积(m³)",
                "切缝(m)", "破碎(m²)", "道路废料(m³)", "机械开挖(m³)", "人工开挖(m³)", "砂垫层(m³)",
                "砂回填(m³)", "碎石垫层(m³)", "C25恢复(m³)", "原土回填(m³)", "C25包管(m³)", "外运(m³)",
                "支管类型", "计算状态", "计算来源", "公式摘要", "回填结构"
            };
            WriteHeader(sheet.CreateRow(0), headerStyle, headers);
            int rowIndex = 1;
            foreach (QuantityMainPipeCalculationRow row in snapshot.calculationAudit.mainPipes) WritePipeAuditRow(sheet.CreateRow(rowIndex++), "主管", row, numberStyle, wrapStyle);
            foreach (QuantityMainPipeCalculationRow row in snapshot.calculationAudit.branchPipes) WritePipeAuditRow(sheet.CreateRow(rowIndex++), "支管", row, numberStyle, wrapStyle);
            SetWidths(sheet, BuildWidths(headers.Length, 15));
            sheet.SetColumnWidth(3, 24 * 256);
            sheet.SetColumnWidth(32, 42 * 256);
            sheet.SetColumnWidth(33, 48 * 256);
            sheet.CreateFreezePane(4, 1);
            sheet.SetAutoFilter(new NPOI.SS.Util.CellRangeAddress(0, Math.Max(0, rowIndex - 1), 0, headers.Length - 1));
        }

        private static void WritePipeAuditRow(IRow target, string category, QuantityMainPipeCalculationRow row, ICellStyle numberStyle, ICellStyle wrapStyle)
        {
            int c = 0;
            SetText(target, c++, category); SetNumber(target, c++, row.Index, numberStyle); SetText(target, c++, row.HandleText); SetText(target, c++, row.LayerName);
            SetText(target, c++, row.StartNode); SetText(target, c++, row.EndNode); SetText(target, c++, row.Material); SetText(target, c++, row.Diameter);
            SetNumber(target, c++, row.Length, numberStyle); SetNumber(target, c++, row.StartDepth, numberStyle); SetNumber(target, c++, row.EndDepth, numberStyle);
            SetNumber(target, c++, row.AverageDepth, numberStyle); SetNumber(target, c++, row.TrenchWidth, numberStyle); SetNumber(target, c++, row.RoadThickness, numberStyle);
            SetNumber(target, c++, row.PipeOuterDiameter, numberStyle); SetText(target, c++, PipeDeductionTargetText(row.PipeDeductionTarget)); SetNumber(target, c++, row.PipeDeductionVolume, numberStyle);
            SetNumber(target, c++, row.RoadCutting, numberStyle); SetNumber(target, c++, row.RoadBreaking, numberStyle); SetNumber(target, c++, row.RoadWaste, numberStyle);
            SetNumber(target, c++, row.MechanicalExcavation, numberStyle); SetNumber(target, c++, row.ManualExcavation, numberStyle); SetNumber(target, c++, row.SandCushion, numberStyle);
            SetNumber(target, c++, row.SandBackfill, numberStyle); SetNumber(target, c++, row.GravelCushion, numberStyle); SetNumber(target, c++, row.C25Restore, numberStyle);
            SetNumber(target, c++, row.OriginalSoilBackfill, numberStyle); SetNumber(target, c++, row.C25PipeEncasement, numberStyle); SetNumber(target, c++, row.EarthworkOut, numberStyle);
            SetText(target, c++, row.BranchType); SetText(target, c++, row.DataStatus); SetText(target, c++, row.CalculationSource);
            SetText(target, c++, row.FormulaText, wrapStyle); SetText(target, c, row.BackfillStructure, wrapStyle);
        }

        private static void WriteWellAuditSheet(IWorkbook workbook, QuantityDashboardSnapshot snapshot, ICellStyle headerStyle, ICellStyle numberStyle, ICellStyle wrapStyle)
        {
            ISheet sheet = workbook.CreateSheet("井输入与结果");
            string[] headers =
            {
                "序号", "Handle", "图层", "节点编号", "井规格", "井类型", "井材质", "井盖材质", "地面标高(m)", "井深(m)",
                "井筒长(m)", "开挖长(m)", "开挖宽(m)", "路面厚度(m)", "井半径(m)", "井面积(m²)", "切缝(m)", "破碎(m²)",
                "道路废料(m³)", "机械开挖(m³)", "人工开挖(m³)", "砂垫层(m³)", "C25垫层(m³)", "盖板碎石(m³)",
                "盖板C25(m³)", "砂回填(m³)", "外运(m³)", "盖板数量", "井盖数量", "计算状态", "计算来源", "公式摘要", "回填结构"
            };
            WriteHeader(sheet.CreateRow(0), headerStyle, headers);
            int rowIndex = 1;
            foreach (QuantityWellCalculationRow row in snapshot.calculationAudit.wells)
            {
                IRow target = sheet.CreateRow(rowIndex++);
                int c = 0;
                SetNumber(target, c++, row.Index, numberStyle); SetText(target, c++, row.HandleText); SetText(target, c++, row.LayerName); SetText(target, c++, row.NodeNo);
                SetText(target, c++, row.WellSpec); SetText(target, c++, row.WellType); SetText(target, c++, row.WellMaterialType); SetText(target, c++, row.WellCoverMaterial);
                SetNumber(target, c++, row.GroundElevation, numberStyle); SetNumber(target, c++, row.WellDepth, numberStyle); SetNumber(target, c++, row.ShaftLength, numberStyle);
                SetNumber(target, c++, row.ExcavationLength, numberStyle); SetNumber(target, c++, row.ExcavationWidth, numberStyle); SetNumber(target, c++, row.RoadThickness, numberStyle);
                SetNumber(target, c++, row.WellRadius, numberStyle); SetNumber(target, c++, row.WellArea, numberStyle); SetNumber(target, c++, row.RoadCutting, numberStyle);
                SetNumber(target, c++, row.RoadBreaking, numberStyle); SetNumber(target, c++, row.RoadWaste, numberStyle); SetNumber(target, c++, row.MechanicalExcavation, numberStyle);
                SetNumber(target, c++, row.ManualExcavation, numberStyle); SetNumber(target, c++, row.SandCushion, numberStyle); SetNumber(target, c++, row.C25Cushion, numberStyle);
                SetNumber(target, c++, row.CoverPlateGravelCushion, numberStyle); SetNumber(target, c++, row.CoverPlateC25Foundation, numberStyle); SetNumber(target, c++, row.SandBackfill, numberStyle);
                SetNumber(target, c++, row.EarthworkOut, numberStyle); SetNumber(target, c++, row.CoverPlateCount, numberStyle); SetNumber(target, c++, row.WellCoverCount, numberStyle);
                SetText(target, c++, row.DataStatus); SetText(target, c++, row.CalculationSource); SetText(target, c++, row.FormulaText, wrapStyle); SetText(target, c, row.BackfillStructure, wrapStyle);
            }
            SetWidths(sheet, BuildWidths(headers.Length, 15));
            sheet.SetColumnWidth(2, 24 * 256); sheet.SetColumnWidth(31, 42 * 256); sheet.SetColumnWidth(32, 48 * 256);
            sheet.CreateFreezePane(3, 1);
            sheet.SetAutoFilter(new NPOI.SS.Util.CellRangeAddress(0, Math.Max(0, rowIndex - 1), 0, headers.Length - 1));
        }

        private static void WriteCalculationStepSheet(IWorkbook workbook, QuantityDashboardSnapshot snapshot, ICellStyle headerStyle, ICellStyle numberStyle, ICellStyle wrapStyle)
        {
            ISheet sheet = workbook.CreateSheet("逐项计算过程");
            string[] headers = { "类别", "对象", "Handle", "图层", "工程量项目", "公式", "实际代入值", "未舍入结果", "单位", "说明" };
            WriteHeader(sheet.CreateRow(0), headerStyle, headers);
            int rowIndex = 1;
            foreach (QuantityMainPipeCalculationRow row in snapshot.calculationAudit.mainPipes) rowIndex = WriteSteps(sheet, rowIndex, "主管", PipeObjectName(row), row.HandleText, row.LayerName, row.CalculationSteps, numberStyle, wrapStyle);
            foreach (QuantityMainPipeCalculationRow row in snapshot.calculationAudit.branchPipes) rowIndex = WriteSteps(sheet, rowIndex, "支管", PipeObjectName(row), row.HandleText, row.LayerName, row.CalculationSteps, numberStyle, wrapStyle);
            foreach (QuantityWellCalculationRow row in snapshot.calculationAudit.wells) rowIndex = WriteSteps(sheet, rowIndex, "井", string.IsNullOrWhiteSpace(row.NodeNo) ? row.WellSpec : row.NodeNo, row.HandleText, row.LayerName, row.CalculationSteps, numberStyle, wrapStyle);
            SetWidths(sheet, new[] { 10, 20, 14, 24, 20, 44, 48, 20, 10, 46 });
            sheet.CreateFreezePane(4, 1);
            sheet.SetAutoFilter(new NPOI.SS.Util.CellRangeAddress(0, Math.Max(0, rowIndex - 1), 0, headers.Length - 1));
        }

        private static int WriteSteps(ISheet sheet, int rowIndex, string category, string objectName, string handle, string layer, System.Collections.Generic.IEnumerable<QuantityCalculationStep> steps, ICellStyle numberStyle, ICellStyle wrapStyle)
        {
            if (steps == null) return rowIndex;
            foreach (QuantityCalculationStep step in steps)
            {
                if (step == null) continue;
                IRow row = sheet.CreateRow(rowIndex++);
                SetText(row, 0, category); SetText(row, 1, objectName); SetText(row, 2, handle); SetText(row, 3, layer);
                SetText(row, 4, step.ItemName); SetText(row, 5, step.Formula, wrapStyle); SetText(row, 6, step.Substitution, wrapStyle);
                SetNumber(row, 7, step.Result, numberStyle); SetText(row, 8, step.Unit); SetText(row, 9, step.Explanation, wrapStyle);
            }
            return rowIndex;
        }

        private static ICellStyle CreateTitleStyle(IWorkbook workbook)
        {
            ICellStyle style = workbook.CreateCellStyle();
            IFont font = workbook.CreateFont();
            font.IsBold = true;
            font.FontHeightInPoints = 16;
            style.SetFont(font);
            return style;
        }

        private static ICellStyle CreateHeaderStyle(IWorkbook workbook)
        {
            ICellStyle style = workbook.CreateCellStyle();
            IFont font = workbook.CreateFont();
            font.IsBold = true;
            style.SetFont(font);
            style.FillForegroundColor = IndexedColors.Grey25Percent.Index;
            style.FillPattern = FillPattern.SolidForeground;
            style.WrapText = true;
            return style;
        }

        private static void WriteHeader(IRow row, ICellStyle style, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                ICell cell = row.CreateCell(i);
                cell.SetCellValue(headers[i] ?? string.Empty);
                cell.CellStyle = style;
            }
        }

        private static void SetText(IRow row, int column, string value)
        {
            SetText(row, column, value, null);
        }

        private static void SetText(IRow row, int column, string value, ICellStyle style)
        {
            ICell cell = row.CreateCell(column);
            cell.SetCellValue(value ?? string.Empty);
            if (style != null) cell.CellStyle = style;
        }

        private static void SetNumber(IRow row, int column, double value, ICellStyle style)
        {
            ICell cell = row.CreateCell(column);
            cell.SetCellValue(double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : value);
            if (style != null) cell.CellStyle = style;
        }

        private static int[] BuildWidths(int count, int width)
        {
            int[] widths = new int[count];
            for (int i = 0; i < widths.Length; i++) widths[i] = width;
            return widths;
        }

        private static void SetWidths(ISheet sheet, int[] widths)
        {
            for (int i = 0; i < widths.Length; i++) sheet.SetColumnWidth(i, Math.Min(255, Math.Max(6, widths[i])) * 256);
        }

        private static string PipeObjectName(QuantityMainPipeCalculationRow row)
        {
            string nodes = (row.StartNode ?? string.Empty) + "-" + (row.EndNode ?? string.Empty);
            return nodes == "-" ? (row.Diameter ?? string.Empty) : nodes.Trim('-');
        }

        private static string PipeDeductionTargetText(string target)
        {
            if (target == "sand") return "中粗砂回填";
            if (target == "soil") return "原土回填";
            if (target == "encasement") return "C25包管";
            return string.Empty;
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
