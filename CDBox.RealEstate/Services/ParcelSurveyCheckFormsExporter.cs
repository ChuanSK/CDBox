using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CDBox.RealEstate.Models;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace CDBox.RealEstate.Services
{
    public static class ParcelSurveyCheckFormsExporter
    {
        public const string TemplateFileName = "四张检查表.xls";
        public const string DefaultExportFileName = "四张检查表.xls";

        private const string CoordinateSheet = "坐标检测成果表";
        private const string DistanceSheet = "间距检测成果表";
        private const string PointSheet = "点测量成果表";
        private const string BuildingEdgeSheet = "房屋边长检查记录表";
        private const int RowsPerPage = 19;

        public static void Export(string outputPath, ParcelSurveyRecord record)
        {
            Export(ResolveTemplatePath(), outputPath, record);
        }

        public static void Export(string templatePath, string outputPath,
            ParcelSurveyRecord record)
        {
            if (string.IsNullOrWhiteSpace(templatePath)
                || !File.Exists(templatePath))
                throw new FileNotFoundException(
                    "未找到四张检查表模板，请确认插件 Templates 文件夹中存在“"
                    + TemplateFileName + "”。", templatePath);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("未指定四张检查表保存路径。",
                    "outputPath");
            if (string.Equals(Path.GetFullPath(templatePath),
                Path.GetFullPath(outputPath),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "导出文件不能直接覆盖四张检查表模板。");

            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory)
                && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            IList<ParcelBoundaryPointRecord> points = record.Boundary.Points;
            IList<ParcelSurveyExcelExporter.BoundaryExportRow> edges =
                ParcelSurveyExcelExporter.BuildBoundaryRows(record);
            int pointPages = PageCount(points.Count);
            int edgePages = PageCount(edges.Count);

            HSSFWorkbook workbook = OpenTemplate(templatePath);
            try
            {
                WriteCoordinates(PreparePagedSheets(workbook,
                    CoordinateSheet, pointPages), points);
                WriteDistances(PreparePagedSheets(workbook,
                    DistanceSheet, edgePages), edges);
                WritePoints(PreparePagedSheets(workbook, PointSheet,
                    pointPages), points);
                WriteBuildingEdges(PreparePagedSheets(workbook,
                    BuildingEdgeSheet, edgePages), record, edges);
                using (FileStream output = new FileStream(outputPath,
                    FileMode.Create, FileAccess.Write, FileShare.None))
                    workbook.Write(output);
            }
            finally
            {
                try { workbook.Close(); } catch { }
            }

            VerifyExport(outputPath, record, pointPages, edgePages);
        }

        private static void WriteCoordinates(IList<ISheet> sheets,
            IList<ParcelBoundaryPointRecord> points)
        {
            for (int page = 0; page < sheets.Count; page++)
            {
                ISheet sheet = sheets[page];
                for (int row = 0; row < RowsPerPage; row++)
                {
                    int index = page * RowsPerPage + row;
                    ParcelBoundaryPointRecord point = index < points.Count
                        ? points[index] : null;
                    SetText(sheet, 3 + row, 0,
                        point == null ? string.Empty : point.PointNumber);
                    SetNumber(sheet, 3 + row, 1,
                        point == null ? null : point.X);
                    SetNumber(sheet, 3 + row, 2,
                        point == null ? null : point.Y);
                }
            }
        }

        private static void WriteDistances(IList<ISheet> sheets,
            IList<ParcelSurveyExcelExporter.BoundaryExportRow> edges)
        {
            for (int page = 0; page < sheets.Count; page++)
            {
                ISheet sheet = sheets[page];
                for (int row = 0; row < RowsPerPage; row++)
                {
                    int index = page * RowsPerPage + row;
                    ParcelSurveyExcelExporter.BoundaryExportRow edge =
                        index < edges.Count ? edges[index] : null;
                    SetText(sheet, 2 + row, 0, edge == null
                        ? string.Empty : PointPair(edge));
                    SetNumber(sheet, 2 + row, 1,
                        edge == null ? null : edge.Distance);
                }
            }
        }

        private static void WritePoints(IList<ISheet> sheets,
            IList<ParcelBoundaryPointRecord> points)
        {
            for (int page = 0; page < sheets.Count; page++)
            {
                ISheet sheet = sheets[page];
                for (int row = 0; row < RowsPerPage; row++)
                {
                    int index = page * RowsPerPage + row;
                    ParcelBoundaryPointRecord point = index < points.Count
                        ? points[index] : null;
                    SetText(sheet, 2 + row, 0,
                        point == null ? string.Empty : point.PointNumber);
                    SetNumber(sheet, 2 + row, 1,
                        point == null ? null : point.X);
                    SetNumber(sheet, 2 + row, 2,
                        point == null ? null : point.Y);
                    SetText(sheet, 2 + row, 3,
                        point == null ? string.Empty : point.MarkerType);
                }
            }
        }

        private static void WriteBuildingEdges(IList<ISheet> sheets,
            ParcelSurveyRecord record,
            IList<ParcelSurveyExcelExporter.BoundaryExportRow> edges)
        {
            string buildingNumber = record.Buildings.Count == 0
                ? string.Empty : BuildingValue(record.Buildings[0],
                    "building.number");
            for (int page = 0; page < sheets.Count; page++)
            {
                ISheet sheet = sheets[page];
                SetText(sheet, 1, 0, "房屋编号：" + buildingNumber);
                for (int row = 0; row < RowsPerPage; row++)
                {
                    int index = page * RowsPerPage + row;
                    ParcelSurveyExcelExporter.BoundaryExportRow edge =
                        index < edges.Count ? edges[index] : null;
                    SetText(sheet, 3 + row, 0, edge == null
                        ? string.Empty : (index + 1).ToString());
                    SetNumber(sheet, 3 + row, 1,
                        edge == null ? null : edge.Distance);
                }
            }
        }

        private static string PointPair(
            ParcelSurveyExcelExporter.BoundaryExportRow edge)
        {
            string start = (edge.StartPointNumber ?? string.Empty).Trim();
            string end = (edge.EndPointNumber ?? string.Empty).Trim();
            if (start.Length == 0) return end;
            if (end.Length == 0) return start;
            return start + "-" + end;
        }

        private static string BuildingValue(ParcelBuildingRecord building,
            string key)
        {
            ParcelSurveyFieldValue value;
            if (building == null || building.Fields == null
                || !building.Fields.TryGetValue(key, out value)
                || value == null) return string.Empty;
            if (value.Status == ParcelFieldStatus.NotApplicable) return "/";
            if (value.NumericValue.HasValue)
                return value.NumericValue.Value.ToString("0.##",
                    System.Globalization.CultureInfo.InvariantCulture);
            return value.TextValue ?? string.Empty;
        }

        private static IList<ISheet> PreparePagedSheets(
            HSSFWorkbook workbook, string templateName, int pageCount)
        {
            ISheet template = RequireSheet(workbook, templateName);
            var result = new List<ISheet> { template };
            int templateIndex = workbook.GetSheetIndex(template);
            for (int page = 2; page <= pageCount; page++)
            {
                string name = templateName + page;
                ISheet existing = workbook.GetSheet(name);
                if (existing == null)
                {
                    existing = workbook.CloneSheet(templateIndex);
                    workbook.SetSheetName(workbook.GetSheetIndex(existing),
                        name);
                    workbook.SetSheetOrder(name, templateIndex + page - 1);
                    existing = workbook.GetSheet(name);
                }
                result.Add(existing);
            }
            return result;
        }

        private static int PageCount(int count)
        {
            return Math.Max(1, (count + RowsPerPage - 1) / RowsPerPage);
        }

        private static HSSFWorkbook OpenTemplate(string templatePath)
        {
            using (FileStream input = new FileStream(templatePath,
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return new HSSFWorkbook(input);
        }

        private static ISheet RequireSheet(HSSFWorkbook workbook,
            string name)
        {
            ISheet sheet = workbook.GetSheet(name);
            if (sheet == null) throw new InvalidOperationException(
                "四张检查表模板中缺少工作表：“" + name + "”。");
            return sheet;
        }

        private static ICell EnsureCell(ISheet sheet, int rowIndex,
            int columnIndex)
        {
            IRow row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
            return row.GetCell(columnIndex) ?? row.CreateCell(columnIndex);
        }

        private static void SetText(ISheet sheet, int row, int column,
            string value)
        {
            EnsureCell(sheet, row, column).SetCellValue(value ?? string.Empty);
        }

        private static void SetNumber(ISheet sheet, int row, int column,
            decimal? value)
        {
            ICell cell = EnsureCell(sheet, row, column);
            if (value.HasValue) cell.SetCellValue((double)decimal.Round(
                value.Value, 2, MidpointRounding.AwayFromZero));
            else cell.SetCellValue(string.Empty);
        }

        private static string ResolveTemplatePath()
        {
            string assemblyDirectory = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                string location = typeof(ParcelSurveyCheckFormsExporter)
                    .Assembly.Location;
                if (!string.IsNullOrWhiteSpace(location))
                    assemblyDirectory = Path.GetDirectoryName(location);
            }
            catch { }
            string pluginPath = Path.Combine(
                assemblyDirectory ?? string.Empty, "Templates",
                TemplateFileName);
            if (File.Exists(pluginPath)) return pluginPath;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "Templates", TemplateFileName);
        }

        private static void VerifyExport(string outputPath,
            ParcelSurveyRecord record, int pointPages, int edgePages)
        {
            using (FileStream input = new FileStream(outputPath,
                FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                HSSFWorkbook workbook = new HSSFWorkbook(input);
                try
                {
                    for (int page = 2; page <= pointPages; page++)
                    {
                        RequireSheet(workbook, CoordinateSheet + page);
                        RequireSheet(workbook, PointSheet + page);
                    }
                    for (int page = 2; page <= edgePages; page++)
                    {
                        RequireSheet(workbook, DistanceSheet + page);
                        RequireSheet(workbook, BuildingEdgeSheet + page);
                    }
                    AssertFormula(RequireSheet(workbook, CoordinateSheet),
                        3, 5);
                    AssertFormula(RequireSheet(workbook, DistanceSheet),
                        2, 3);
                    AssertFormula(RequireSheet(workbook,
                        BuildingEdgeSheet), 3, 3);
                    if (record.Boundary.Points.Count > 0)
                    {
                        string expected = record.Boundary.Points[0]
                            .PointNumber ?? string.Empty;
                        string actual = RequireSheet(workbook,
                            CoordinateSheet).GetRow(3).GetCell(0).ToString();
                        if (!string.Equals(expected, actual,
                            StringComparison.Ordinal))
                            throw new InvalidDataException(
                                "导出回读失败：坐标检测成果表点号未正确写入。");
                    }
                }
                finally
                {
                    try { workbook.Close(); } catch { }
                }
            }
        }

        private static void AssertFormula(ISheet sheet, int row, int column)
        {
            ICell cell = sheet.GetRow(row).GetCell(column);
            if (cell == null || cell.CellType != CellType.Formula)
                throw new InvalidDataException("导出回读失败：工作表“"
                    + sheet.SheetName + "”中的差值公式被改动。");
        }
    }
}
