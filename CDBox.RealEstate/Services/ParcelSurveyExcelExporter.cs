using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CDBox.RealEstate.Models;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace CDBox.RealEstate.Services
{
    public static class ParcelSurveyExcelExporter
    {
        public const string TemplateFileName = "权籍调查表.xls";
        public const string DefaultExportFileName = "权籍调查表.xls";
        private const string HomeSheet = "首页";
        private const string BasicSheet = "基本表";
        private const string BoundaryMarkSheet = "界址标示表1";
        private const string BoundarySignatureSheet = "界址签章表";
        private const string BoundaryDescriptionSheet = "界址说明表";
        private const string HouseSheet = "房屋调查表";
        private const int BoundaryRowsPerPage = 26;
        private const int SignatureRowsPerPage = 13;
        private const string CheckMark = "√";

        private static readonly IDictionary<string, int> MarkerColumns =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "钢钉", 1 }, { "水泥桩", 2 }, { "石灰桩", 3 },
                { "喷涂", 4 }, { "木桩", 5 }
            };

        private static readonly IDictionary<string, int> CategoryColumns =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "围墙", 7 }, { "墙壁", 8 }, { "门墩", 9 },
                { "道路", 10 }, { "田埂", 11 }, { "沟渠", 12 },
                { "铁丝网", 13 }, { "界址线", 14 }
            };

        private static readonly IDictionary<string, int> PositionColumns =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "内", 15 }, { "中", 16 }, { "外", 17 }
            };

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
                    "未找到权籍调查表模板，请确认插件 Templates 文件夹中存在“"
                    + TemplateFileName + "”。", templatePath);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("未指定权籍调查表保存路径。",
                    "outputPath");
            if (string.Equals(Path.GetFullPath(templatePath),
                Path.GetFullPath(outputPath),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "导出文件不能直接覆盖权籍调查表模板。");

            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory)
                && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            IList<BoundaryExportRow> boundaryRows = BuildBoundaryRows(record);
            int boundaryPages = Math.Max(1, (boundaryRows.Count
                + BoundaryRowsPerPage - 1) / BoundaryRowsPerPage);
            int signaturePages = Math.Max(1,
                (record.Boundary.SignatureGroups.Count
                    + SignatureRowsPerPage - 1) / SignatureRowsPerPage);
            int housePages = Math.Max(1, record.Buildings.Count);

            HSSFWorkbook workbook = OpenTemplate(templatePath);
            try
            {
                WriteHome(workbook, record);
                WriteBasic(workbook, record);
                WriteBoundaryMarks(workbook, record, boundaryRows,
                    boundaryPages);
                WriteBoundarySignatures(workbook, record, signaturePages);
                WriteBoundaryDescriptions(workbook, record);
                WriteHouses(workbook, record, housePages);
                using (FileStream output = new FileStream(outputPath,
                    FileMode.Create, FileAccess.Write, FileShare.None))
                    workbook.Write(output);
            }
            finally
            {
                try { workbook.Close(); } catch { }
            }

            VerifyExport(outputPath, record, boundaryRows.Count,
                boundaryPages, signaturePages, housePages);
        }

        private static void WriteHome(HSSFWorkbook workbook,
            ParcelSurveyRecord record)
        {
            ISheet sheet = RequireSheet(workbook, HomeSheet);
            SetText(sheet, 17, 6, Value(record,
                ParcelSurveyFieldKeys.ParcelSeaCode));
            SetText(sheet, 19, 6, Value(record, "project.organization"));
            string surveyDate = ChineseDate(Value(record,
                "project.surveyDate"));
            SetText(sheet, 28, 0, string.IsNullOrWhiteSpace(surveyDate)
                ? string.Empty : "调查时间：" + surveyDate);
        }

        private static void WriteBasic(HSSFWorkbook workbook,
            ParcelSurveyRecord record)
        {
            ISheet sheet = RequireSheet(workbook, BasicSheet);
            SetText(sheet, 1, 6, Value(record, "rights.landOwnershipType"));
            SetText(sheet, 2, 3, Value(record, "rights.identityRoles"));
            SetText(sheet, 2, 6, Value(record,
                ParcelSurveyFieldKeys.OwnerName));
            SetText(sheet, 2, 17, Value(record,
                ParcelSurveyFieldKeys.OwnerType));
            SetText(sheet, 3, 17, Value(record,
                ParcelSurveyFieldKeys.CertificateType));
            SetText(sheet, 4, 17, Value(record,
                ParcelSurveyFieldKeys.CertificateNumber));
            SetText(sheet, 5, 17, Contact(record,
                ParcelSurveyFieldKeys.ContactAddress,
                ParcelSurveyFieldKeys.ContactPhone));
            SetText(sheet, 6, 6, Value(record, "rights.rightType"));
            SetText(sheet, 6, 13, Value(record, "rights.rightNature"));
            SetText(sheet, 6, 20, Value(record, "rights.sourceMaterial"));
            SetText(sheet, 9, 6, Value(record,
                ParcelSurveyFieldKeys.ParcelLocation));
            SetText(sheet, 9, 33, string.Empty);

            bool personal = string.Equals(Value(record,
                ParcelSurveyFieldKeys.OwnerType), "个人",
                StringComparison.OrdinalIgnoreCase);
            SetText(sheet, 10, 6, personal ? "/" : Value(record,
                "rights.legalRepresentativeName"));
            SetText(sheet, 10, 13, personal ? "/" : Value(record,
                "rights.legalRepresentativeCertificateType"));
            SetText(sheet, 10, 22, personal ? "/" : Value(record,
                "rights.legalRepresentativePhone"));
            SetText(sheet, 11, 13, personal ? "/" : Value(record,
                "rights.legalRepresentativeCertificateNumber"));

            bool hasAgent = Boolean(record, "rights.hasAgent");
            SetText(sheet, 12, 6, ConditionalValue(record, hasAgent,
                "rights.agentName"));
            SetText(sheet, 12, 13, ConditionalValue(record, hasAgent,
                "rights.agentCertificateType"));
            SetText(sheet, 12, 22, ConditionalValue(record, hasAgent,
                "rights.agentPhone"));
            SetText(sheet, 13, 13, ConditionalValue(record, hasAgent,
                "rights.agentCertificateNumber"));

            SetText(sheet, 14, 6, Value(record,
                "rights.establishmentMode"));
            SetField(sheet, 15, 6, record, "rights.industryCode");
            SetField(sheet, 16, 6, record,
                ParcelSurveyFieldKeys.PreliminaryParcelCode);
            SetText(sheet, 16, 19, Value(record,
                ParcelSurveyFieldKeys.ParcelCode));
            SetText(sheet, 17, 6, Value(record,
                ParcelSurveyFieldKeys.RealEstateUnitNumber));
            SetText(sheet, 18, 10, Value(record, "parcel.mapScale"));
            SetText(sheet, 19, 10, Value(record,
                "parcel.mapSheetNumber"));
            SetText(sheet, 20, 6, BoundaryText("北", Value(record,
                "parcel.northBoundary")));
            SetText(sheet, 21, 6, BoundaryText("东", Value(record,
                "parcel.eastBoundary")));
            SetText(sheet, 22, 6, BoundaryText("南", Value(record,
                "parcel.southBoundary")));
            SetText(sheet, 23, 6, BoundaryText("西", Value(record,
                "parcel.westBoundary")));
            SetField(sheet, 24, 6, record, "land.grade");
            SetField(sheet, 24, 19, record, "land.price");
            SetText(sheet, 25, 6, Value(record, "land.approvedUse"));
            SetText(sheet, 25, 17, Value(record, "land.actualUse"));
            SetText(sheet, 26, 10, Value(record, "land.approvedUseCode"));
            SetText(sheet, 26, 24, Value(record, "land.actualUseCode"));
            SetField(sheet, 27, 6, record, "land.approvedArea");
            SetField(sheet, 27, 13, record,
                ParcelSurveyFieldKeys.ParcelArea);
            SetField(sheet, 27, 24, record,
                ParcelSurveyFieldKeys.BuildingFootprintTotal);
            SetField(sheet, 28, 24, record,
                ParcelSurveyFieldKeys.BuildingAreaTotal);
            SetText(sheet, 29, 6, LandTerm(record));
            SetText(sheet, 30, 6, CoOwnership(record));
            SetText(sheet, 33, 6, Value(record, "parcel.description"));
            SetText(sheet, 36, 0, Footer(record));
        }

        private static void WriteBoundaryMarks(HSSFWorkbook workbook,
            ParcelSurveyRecord record, IList<BoundaryExportRow> rows,
            int pageCount)
        {
            IList<ISheet> sheets = PreparePagedSheets(workbook,
                BoundaryMarkSheet, pageCount, page => "界址标示表" + page);
            for (int page = 0; page < sheets.Count; page++)
            {
                ISheet sheet = sheets[page];
                ClearBoundaryMarkPage(sheet);
                int offset = page * BoundaryRowsPerPage;
                int count = Math.Min(BoundaryRowsPerPage,
                    Math.Max(0, rows.Count - offset));
                if (count > 0)
                {
                    BoundaryExportRow first = rows[offset];
                    SetText(sheet, 3, 0, first.StartPointNumber);
                    SetMarker(sheet, 3, first.StartMarkerType);
                    for (int i = 0; i < count; i++)
                    {
                        BoundaryExportRow item = rows[offset + i];
                        int segmentRow = 3 + i * 2;
                        int endpointRow = 4 + i * 2;
                        SetNumber(sheet, segmentRow, 6, item.Distance);
                        SetCheck(sheet, segmentRow, CategoryColumns,
                            item.LineCategory);
                        SetCheck(sheet, segmentRow, PositionColumns,
                            item.LinePosition);
                        SetText(sheet, segmentRow, 18, string.Empty);
                        SetText(sheet, endpointRow, 0,
                            item.EndPointNumber);
                        SetMarker(sheet, endpointRow,
                            item.EndMarkerType);
                    }
                }
                SetText(sheet, 56, 0, Footer(record));
            }
        }

        private static void WriteBoundarySignatures(HSSFWorkbook workbook,
            ParcelSurveyRecord record, int pageCount)
        {
            IList<ISheet> sheets = PreparePagedSheets(workbook,
                BoundarySignatureSheet, pageCount,
                page => page == 1 ? BoundarySignatureSheet
                    : BoundarySignatureSheet + page);
            const bool outputNames = false;
            for (int page = 0; page < sheets.Count; page++)
            {
                ISheet sheet = sheets[page];
                for (int i = 0; i < SignatureRowsPerPage; i++)
                    for (int column = 0; column < 7; column++)
                        SetText(sheet, 3 + i, column, string.Empty);
                int offset = page * SignatureRowsPerPage;
                int count = Math.Min(SignatureRowsPerPage,
                    Math.Max(0, record.Boundary.SignatureGroups.Count
                        - offset));
                for (int i = 0; i < count; i++)
                {
                    ParcelBoundarySignatureGroupRecord group = record.Boundary
                        .SignatureGroups[offset + i];
                    int row = 3 + i;
                    SetText(sheet, row, 0, group.StartPointNumber);
                    SetText(sheet, row, 1, SlashIfEmpty(
                        group.MiddlePointNumbers));
                    SetText(sheet, row, 2, group.EndPointNumber);
                    SetText(sheet, row, 3, Neighbor(group));
                    bool leaveBlank = group.PreservePaperSignatureBlank
                        || !outputNames;
                    SetText(sheet, row, 4, leaveBlank ? string.Empty
                        : group.NeighborRepresentative);
                    SetText(sheet, row, 5, leaveBlank ? string.Empty
                        : group.ParcelRepresentative);
                    SetText(sheet, row, 6,
                        ChineseDate(group.ConfirmationDate));
                }
                SetText(sheet, 16, 0, Footer(record));
            }
        }

        private static void WriteBoundaryDescriptions(HSSFWorkbook workbook,
            ParcelSurveyRecord record)
        {
            ISheet sheet = RequireSheet(workbook, BoundaryDescriptionSheet);
            SetText(sheet, 1, 1, Value(record,
                "boundary.pointDescription"));
            SetText(sheet, 2, 1, Value(record,
                "boundary.lineDescription"));
            SetText(sheet, 4, 0, Footer(record));
        }

        private static void WriteHouses(HSSFWorkbook workbook,
            ParcelSurveyRecord record, int pageCount)
        {
            IList<ISheet> sheets = PreparePagedSheets(workbook, HouseSheet,
                pageCount, page => page == 1 ? HouseSheet
                    : HouseSheet + page);
            for (int i = 0; i < sheets.Count; i++)
            {
                ParcelBuildingRecord building = i < record.Buildings.Count
                    ? record.Buildings[i] : new ParcelBuildingRecord();
                building.Normalize();
                WriteHouse(sheets[i], record, building);
            }
        }

        private static void WriteHouse(ISheet sheet, ParcelSurveyRecord record,
            ParcelBuildingRecord building)
        {
            bool followOwner = Boolean(record, "house.followParcelOwner");
            string ownerName = followOwner
                ? Value(record, ParcelSurveyFieldKeys.OwnerName)
                : Value(record, "house.ownerName");
            string ownerType = followOwner
                ? Value(record, ParcelSurveyFieldKeys.OwnerType)
                : Value(record, "house.ownerType");
            string certificateType = followOwner
                ? Value(record, ParcelSurveyFieldKeys.CertificateType)
                : Value(record, "house.certificateType");
            string certificateNumber = followOwner
                ? Value(record, ParcelSurveyFieldKeys.CertificateNumber)
                : Value(record, "house.certificateNumber");
            string address = followOwner
                ? Value(record, ParcelSurveyFieldKeys.ContactAddress)
                : Value(record, "house.address");
            string phone = followOwner
                ? Value(record, ParcelSurveyFieldKeys.ContactPhone)
                : Value(record, "house.phone");
            string unitCode = Value(record, "house.unitCode");
            string parcelNumber = Value(record,
                ParcelSurveyFieldKeys.ParcelNumber);

            SetText(sheet, 1, 0, "不动产单元代码：" + unitCode);
            SetText(sheet, 1, 6, "县级行政区代码："
                + Value(record, "project.countyCode"));
            SetText(sheet, 1, 16, "地籍区代码："
                + Value(record, "project.cadastralDistrictCode"));
            SetText(sheet, 1, 24, "地籍子区代码："
                + Value(record, "project.cadastralSubdistrictCode")
                + "    宗地号：" + parcelNumber
                + "    定着物单元（房屋）代码：" + unitCode);
            SetText(sheet, 2, 6, Value(record, "house.unitType"));
            SetText(sheet, 2, 37, Value(record, "project.name"));
            SetText(sheet, 3, 4, Value(record, "house.location"));
            SetText(sheet, 3, 38, Value(record, "project.postalCode"));
            string roles = followOwner
                ? Value(record, "rights.identityRoles")
                : Value(record, "house.identityRoles");
            roles = roles.Replace("权利人", "所有权人");
            SetText(sheet, 4, 0, roles);
            SetText(sheet, 4, 4, ownerName);
            SetText(sheet, 4, 31, certificateType);
            SetText(sheet, 5, 31, certificateNumber);
            SetText(sheet, 6, 31, Contact(address, phone));
            SetText(sheet, 7, 4, ownerType);
            SetText(sheet, 7, 24, Value(record, "house.plannedUse"));
            SetText(sheet, 7, 35, Value(record, "house.coOwnership"));
            SetText(sheet, 8, 4, Value(record, "house.nature"));
            SetText(sheet, 8, 24, Value(record, "house.actualUse"));
            SetField(sheet, 9, 4, record, "house.sharedArea");

            SetBuildingField(sheet, 12, 4, building, "building.number");
            SetBuildingField(sheet, 12, 6, building,
                "building.householdNumber");
            SetBuildingField(sheet, 12, 8, building,
                "building.totalUnits");
            SetBuildingField(sheet, 12, 10, building,
                "building.totalFloors");
            SetBuildingField(sheet, 12, 12, building, "building.floor");
            SetBuildingField(sheet, 12, 14, building,
                "building.structure");
            SetBuildingField(sheet, 12, 18, building,
                "building.completionDate");
            SetBuildingField(sheet, 12, 22, building, "building.layout");
            SetBuildingField(sheet, 12, 24, building,
                "building.orientation");
            SetBuildingField(sheet, 12, 26, building,
                "building.footprintArea");
            SetBuildingField(sheet, 12, 29, building, "building.area");
            SetBuildingField(sheet, 12, 31, building,
                "building.exclusiveArea");
            SetBuildingField(sheet, 12, 35, building,
                "building.allocatedArea");
            SetBuildingField(sheet, 12, 38, building,
                "building.propertySource");
            SetBuildingField(sheet, 12, 42, building, "building.wall东");
            SetBuildingField(sheet, 12, 44, building, "building.wall南");
            SetBuildingField(sheet, 12, 46, building, "building.wall西");
            SetBuildingField(sheet, 12, 48, building, "building.wall北");
            SetBuildingField(sheet, 13, 4, building, "building.sketch");
            SetBuildingField(sheet, 13, 35, building, "building.notes");
            SetBuildingField(sheet, 14, 35, building,
                "building.reviewOpinion");
            SetText(sheet, 15, 1, "调查员：");
            SetText(sheet, 15, 32, "日期："
                + ChineseDate(Value(record, "project.rightsSurveyDate")));
        }

        internal static IList<BoundaryExportRow> BuildBoundaryRows(
            ParcelSurveyRecord record)
        {
            IList<ParcelBoundaryPointRecord> points = record.Boundary.Points;
            if (points.Count < 2)
                return record.Boundary.Segments.Select(segment =>
                    new BoundaryExportRow
                    {
                        StartPointNumber = segment.StartPointNumber,
                        EndPointNumber = segment.EndPointNumber,
                        Distance = segment.Distance,
                        LineCategory = segment.LineCategory,
                        LinePosition = segment.LinePosition,
                        Description = segment.Description
                    }).ToList();

            var order = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < points.Count; i++)
                if (!string.IsNullOrWhiteSpace(points[i].PointNumber)
                    && !order.ContainsKey(points[i].PointNumber))
                    order.Add(points[i].PointNumber, i);
            var segmentsByEdge = new Dictionary<int,
                ParcelBoundarySegmentRecord>();
            foreach (ParcelBoundarySegmentRecord segment in
                record.Boundary.Segments)
            {
                int start;
                int end;
                if (!order.TryGetValue(segment.StartPointNumber
                        ?? string.Empty, out start)
                    || !order.TryGetValue(segment.EndPointNumber
                        ?? string.Empty, out end) || start == end) continue;
                int current = start;
                int guard = 0;
                while (current != end && guard++ < points.Count)
                {
                    if (!segmentsByEdge.ContainsKey(current))
                        segmentsByEdge.Add(current, segment);
                    current = (current + 1) % points.Count;
                }
            }

            int edgeCount = record.Boundary.ParcelBoundaryClosed
                ? points.Count : points.Count - 1;
            var result = new List<BoundaryExportRow>();
            for (int i = 0; i < edgeCount; i++)
            {
                ParcelBoundaryPointRecord start = points[i];
                ParcelBoundaryPointRecord end = points[(i + 1)
                    % points.Count];
                ParcelBoundarySegmentRecord segment;
                segmentsByEdge.TryGetValue(i, out segment);
                result.Add(new BoundaryExportRow
                {
                    StartPointNumber = start.PointNumber,
                    EndPointNumber = end.PointNumber,
                    StartMarkerType = start.MarkerType,
                    EndMarkerType = end.MarkerType,
                    Distance = start.DistanceToNext.HasValue
                        && start.DistanceToNext.Value > 0
                        ? start.DistanceToNext : Distance(start, end),
                    LineCategory = segment == null ? string.Empty
                        : segment.LineCategory,
                    LinePosition = segment == null ? string.Empty
                        : segment.LinePosition,
                    Description = segment == null ? string.Empty
                        : segment.Description
                });
            }
            return result;
        }

        private static IList<ISheet> PreparePagedSheets(HSSFWorkbook workbook,
            string templateName, int pageCount, Func<int, string> nameFactory)
        {
            ISheet template = RequireSheet(workbook, templateName);
            var result = new List<ISheet> { template };
            int templateIndex = workbook.GetSheetIndex(template);
            for (int page = 2; page <= pageCount; page++)
            {
                ISheet clone = workbook.CloneSheet(templateIndex);
                int cloneIndex = workbook.GetSheetIndex(clone);
                string name = nameFactory(page);
                workbook.SetSheetName(cloneIndex, name);
                workbook.SetSheetOrder(name, templateIndex + page - 1);
                result.Add(workbook.GetSheet(name));
            }
            return result;
        }

        private static void ClearBoundaryMarkPage(ISheet sheet)
        {
            SetText(sheet, 3, 0, string.Empty);
            for (int column = 1; column <= 5; column++)
                SetText(sheet, 3, column, string.Empty);
            for (int i = 0; i < BoundaryRowsPerPage; i++)
            {
                int segmentRow = 3 + i * 2;
                int endpointRow = 4 + i * 2;
                for (int column = 6; column <= 18; column++)
                    SetText(sheet, segmentRow, column, string.Empty);
                for (int column = 0; column <= 5; column++)
                    SetText(sheet, endpointRow, column, string.Empty);
            }
        }

        private static void SetMarker(ISheet sheet, int row, string marker)
        {
            SetCheck(sheet, row, MarkerColumns, marker);
        }

        private static void SetCheck(ISheet sheet, int row,
            IDictionary<string, int> columns, string selected)
        {
            foreach (int column in columns.Values)
                SetText(sheet, row, column, string.Empty);
            int target;
            if (!string.IsNullOrWhiteSpace(selected)
                && columns.TryGetValue(selected.Trim(), out target))
                SetText(sheet, row, target, CheckMark);
        }

        private static string ResolveTemplatePath()
        {
            string assemblyDirectory = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                string location = typeof(ParcelSurveyExcelExporter).Assembly
                    .Location;
                if (!string.IsNullOrWhiteSpace(location))
                    assemblyDirectory = Path.GetDirectoryName(location);
            }
            catch { }
            string pluginPath = Path.Combine(assemblyDirectory ?? string.Empty,
                "Templates", TemplateFileName);
            if (File.Exists(pluginPath)) return pluginPath;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "Templates", TemplateFileName);
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
                "权籍调查表模板中缺少工作表：“" + name + "”。");
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

        private static void SetField(ISheet sheet, int row, int column,
            ParcelSurveyRecord record, string key)
        {
            ParcelSurveyFieldValue field = record.Field(key);
            if (field == null)
            {
                SetText(sheet, row, column, string.Empty);
                return;
            }
            if (field.Status == ParcelFieldStatus.NotApplicable)
            {
                SetText(sheet, row, column, "/");
                return;
            }
            if (field.NumericValue.HasValue)
                SetNumber(sheet, row, column, field.NumericValue);
            else
                SetText(sheet, row, column, Value(record, key));
        }

        private static void SetBuildingField(ISheet sheet, int row,
            int column, ParcelBuildingRecord building, string key)
        {
            ParcelSurveyFieldValue field;
            if (!building.Fields.TryGetValue(key, out field) || field == null)
            {
                SetText(sheet, row, column, string.Empty);
                return;
            }
            if (field.Status == ParcelFieldStatus.NotApplicable)
                SetText(sheet, row, column, "/");
            else if (field.NumericValue.HasValue)
                SetNumber(sheet, row, column, field.NumericValue);
            else if (field.Selections != null && field.Selections.Count > 0)
                SetText(sheet, row, column,
                    string.Join("、", field.Selections));
            else
                SetText(sheet, row, column, field.TextValue);
        }

        internal static string Value(ParcelSurveyRecord record, string key)
        {
            ParcelSurveyFieldValue field = record.Field(key);
            if (field == null) return string.Empty;
            if (field.Status == ParcelFieldStatus.NotApplicable) return "/";
            if (field.NumericValue.HasValue)
                return field.NumericValue.Value.ToString("0.##",
                    CultureInfo.InvariantCulture);
            if (field.Selections != null && field.Selections.Count > 0)
                return string.Join("、", field.Selections);
            return field.TextValue ?? string.Empty;
        }

        internal static bool Boolean(ParcelSurveyRecord record, string key)
        {
            ParcelSurveyFieldValue field = record.Field(key);
            return field != null && field.BooleanValue;
        }

        internal static string Contact(ParcelSurveyRecord record,
            string addressKey, string phoneKey)
        {
            return Contact(Value(record, addressKey), Value(record, phoneKey));
        }

        internal static string Contact(string address, string phone)
        {
            address = (address ?? string.Empty).Trim();
            phone = (phone ?? string.Empty).Trim();
            if (address.Length == 0) return phone;
            if (phone.Length == 0) return address;
            return address + "，联系电话：" + phone;
        }

        internal static string BoundaryText(string direction, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return direction + "：" + (value ?? string.Empty).Trim();
        }

        internal static string ConditionalValue(ParcelSurveyRecord record,
            bool condition, string key)
        {
            ParcelSurveyFieldValue field = record.Field(key);
            if (field != null
                && field.Status == ParcelFieldStatus.NotApplicable) return "/";
            return condition ? Value(record, key) : string.Empty;
        }

        internal static string LandTerm(ParcelSurveyRecord record)
        {
            string start = ChineseDate(Value(record, "land.termStart"));
            string end = ChineseDate(Value(record, "land.termEnd"));
            string description = Value(record, "land.termDescription");
            var values = new List<string>();
            if (!string.IsNullOrWhiteSpace(start)
                || !string.IsNullOrWhiteSpace(end))
                values.Add(start + " 至 " + end);
            if (!string.IsNullOrWhiteSpace(description))
                values.Add(description.Trim());
            return string.Join("；", values);
        }

        internal static string CoOwnership(ParcelSurveyRecord record)
        {
            string type = Value(record, "rights.coOwnershipType");
            string description = Value(record, "rights.coOwnerDescription");
            if (string.IsNullOrWhiteSpace(description)) return type;
            if (string.IsNullOrWhiteSpace(type)) return description;
            return type + "；" + description;
        }

        internal static string Footer(ParcelSurveyRecord record)
        {
            string filler = Value(record, "project.formFiller");
            string date = ChineseDate(Value(record, "project.formDate"));
            if (string.IsNullOrWhiteSpace(filler)
                && string.IsNullOrWhiteSpace(date)) return string.Empty;
            return "填表人：" + filler
                + "                                             填表时间："
                + date;
        }

        internal static string Neighbor(
            ParcelBoundarySignatureGroupRecord group)
        {
            string owner = (group.NeighborOwner ?? string.Empty).Trim();
            string code = (group.NeighborParcelCode ?? string.Empty).Trim();
            if (owner.Length == 0) return code;
            if (code.Length == 0) return owner;
            return owner + "（" + code + "）";
        }

        internal static string ChineseDate(string value)
        {
            value = (value ?? string.Empty).Trim();
            DateTime date;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date)
                ? date.ToString("yyyy年M月d日", CultureInfo.InvariantCulture)
                : value;
        }

        internal static string SlashIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "/" : value.Trim();
        }

        private static decimal? Distance(ParcelBoundaryPointRecord left,
            ParcelBoundaryPointRecord right)
        {
            if (left == null || right == null || !left.X.HasValue
                || !left.Y.HasValue || !right.X.HasValue || !right.Y.HasValue)
                return null;
            decimal dx = right.X.Value - left.X.Value;
            decimal dy = right.Y.Value - left.Y.Value;
            return decimal.Round((decimal)Math.Sqrt((double)(dx * dx
                + dy * dy)), 2, MidpointRounding.AwayFromZero);
        }

        private static string CellText(ISheet sheet, int row, int column)
        {
            IRow targetRow = sheet.GetRow(row);
            ICell cell = targetRow == null ? null : targetRow.GetCell(column);
            return cell == null ? string.Empty : cell.ToString();
        }

        private static void VerifyExport(string outputPath,
            ParcelSurveyRecord record, int boundaryRowCount,
            int boundaryPages, int signaturePages, int housePages)
        {
            using (FileStream input = new FileStream(outputPath,
                FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                HSSFWorkbook workbook = new HSSFWorkbook(input);
                try
                {
                    for (int page = 1; page <= boundaryPages; page++)
                        RequireSheet(workbook, "界址标示表" + page);
                    for (int page = 2; page <= signaturePages; page++)
                        RequireSheet(workbook, BoundarySignatureSheet + page);
                    for (int page = 2; page <= housePages; page++)
                        RequireSheet(workbook, HouseSheet + page);
                    string expectedCode = Value(record,
                        ParcelSurveyFieldKeys.ParcelCode);
                    string actualCode = CellText(RequireSheet(workbook,
                        BasicSheet), 16, 19);
                    if (!string.Equals(expectedCode, actualCode,
                        StringComparison.Ordinal))
                        throw new InvalidDataException(
                            "导出回读失败：宗地代码未正确写入基本表。");
                    if (boundaryRowCount > 0)
                    {
                        string firstPoint = CellText(RequireSheet(workbook,
                            BoundaryMarkSheet), 3, 0);
                        if (string.IsNullOrWhiteSpace(firstPoint))
                            throw new InvalidDataException(
                                "导出回读失败：界址标示表缺少界址点号。");
                    }
                }
                finally
                {
                    try { workbook.Close(); } catch { }
                }
            }
        }

        internal sealed class BoundaryExportRow
        {
            public string StartPointNumber { get; set; }
            public string EndPointNumber { get; set; }
            public string StartMarkerType { get; set; }
            public string EndMarkerType { get; set; }
            public decimal? Distance { get; set; }
            public string LineCategory { get; set; }
            public string LinePosition { get; set; }
            public string Description { get; set; }
        }
    }
}
