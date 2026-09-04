using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using CDBox.RealEstate.Models;

namespace CDBox.RealEstate.Services
{
    public static class ParcelSurveyWordExporter
    {
        public const string TemplateFileName = "地籍调查表.docx";
        public const string DefaultExportFileName = "地籍调查表.docx";

        private const string DocumentPartName = "word/document.xml";
        private const int BoundaryRowsPerPage = 17;
        private const int SignatureRowsPerPage = 6;
        private const string CheckMark = "√";

        private static readonly XNamespace W =
            "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private static readonly XNamespace W14 =
            "http://schemas.microsoft.com/office/word/2010/wordml";
        private static readonly XNamespace MC =
            "http://schemas.openxmlformats.org/markup-compatibility/2006";
        private static readonly XNamespace XmlNamespace =
            "http://www.w3.org/XML/1998/namespace";

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
                    "未找到地籍调查表 Word 模板，请确认插件 Templates 文件夹中存在“"
                    + TemplateFileName + "”。", templatePath);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("未指定地籍调查表保存路径。",
                    "outputPath");
            if (string.Equals(Path.GetFullPath(templatePath),
                Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "导出文件不能直接覆盖地籍调查表 Word 模板。");

            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory)
                && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            IList<ParcelSurveyExcelExporter.BoundaryExportRow> boundaryRows =
                ParcelSurveyExcelExporter.BuildBoundaryRows(record);
            int boundaryPages = Math.Max(1, (boundaryRows.Count
                + BoundaryRowsPerPage - 1) / BoundaryRowsPerPage);
            int signaturePages = Math.Max(1,
                (record.Boundary.SignatureGroups.Count
                    + SignatureRowsPerPage - 1) / SignatureRowsPerPage);
            int housePages = Math.Max(1, record.Buildings.Count);

            File.Copy(templatePath, outputPath, true);
            RewriteDocument(outputPath, record, boundaryRows, boundaryPages,
                signaturePages, housePages);
            VerifyExport(outputPath, record, boundaryRows.Count,
                boundaryPages, signaturePages, housePages);
        }

        private static void RewriteDocument(string outputPath,
            ParcelSurveyRecord record,
            IList<ParcelSurveyExcelExporter.BoundaryExportRow> boundaryRows,
            int boundaryPages, int signaturePages, int housePages)
        {
            using (FileStream stream = new FileStream(outputPath,
                FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive package = new ZipArchive(stream,
                ZipArchiveMode.Update, false))
            {
                ZipArchiveEntry entry = package.GetEntry(DocumentPartName);
                if (entry == null) throw new InvalidDataException(
                    "地籍调查表 Word 模板缺少 document.xml。");
                XDocument document;
                using (Stream input = entry.Open())
                    document = XDocument.Load(input,
                        LoadOptions.PreserveWhitespace);

                XElement body = document.Root == null ? null
                    : document.Root.Element(W + "body");
                if (body == null) throw new InvalidDataException(
                    "地籍调查表 Word 模板缺少正文。");
                List<XElement> tables = body.Elements(W + "tbl").ToList();
                if (tables.Count < 8) throw new InvalidDataException(
                    "地籍调查表 Word 模板表格结构不完整。");

                XElement cover = tables[0];
                XElement basic = tables[1];
                XElement firstBoundary = tables[2];
                XElement secondBoundary = tables[3];
                XElement signature = tables[4];
                XElement descriptions = tables[5];
                XElement audit = tables[6];
                XElement house = tables[7];

                IList<PageBlock> boundaryBlocks = PrepareBoundaryPages(
                    firstBoundary, secondBoundary, boundaryPages);
                IList<PageBlock> signatureBlocks = PreparePagedTableBlocks(
                    signature, signaturePages);
                IList<PageBlock> houseBlocks = PreparePagedTableBlocks(
                    house, housePages, true);

                WriteCover(cover, record);
                WriteBasic(basic, record);
                WriteBoundaryMarks(boundaryBlocks, record, boundaryRows);
                WriteBoundarySignatures(signatureBlocks, record);
                WriteBoundaryDescriptions(descriptions, record);
                WriteAuditArea(audit, record);
                WriteHouses(houseBlocks, record);

                entry.Delete();
                ZipArchiveEntry replacement = package.CreateEntry(
                    DocumentPartName, CompressionLevel.Optimal);
                using (Stream output = replacement.Open())
                using (XmlWriter writer = XmlWriter.Create(output,
                    new XmlWriterSettings
                    {
                        Encoding = new System.Text.UTF8Encoding(false),
                        Indent = false,
                        OmitXmlDeclaration = false,
                        CloseOutput = false
                    }))
                    document.Save(writer);
            }
        }

        private static void WriteCover(XElement table,
            ParcelSurveyRecord record)
        {
            XElement code = FindParagraph(table, "宗地/宗海代码");
            XElement organization = FindParagraph(table, "调查单位");
            XElement surveyDate = FindParagraph(table, "调查时间");
            SetUnderlinedValueAfterLabel(code, "宗地/宗海代码：",
                Value(record, ParcelSurveyFieldKeys.ParcelSeaCode));
            SetUnderlinedValueAfterLabel(organization, "调查单位（机构）：",
                Value(record, "project.organization"));
            SetInlineFields(surveyDate, new[]
            {
                new InlineField("调查时间：", ChineseDate(
                    Value(record, "project.surveyDate")))
            });
        }

        private static void WriteBasic(XElement table,
            ParcelSurveyRecord record)
        {
            SetCellText(table, 1, 2, Value(record,
                "rights.landOwnershipType"));
            SetCheckboxesByOrder(CellAt(table, 2, 1),
                IsSelected(record, "rights.identityRoles", "权利人"),
                IsSelected(record, "rights.identityRoles", "实际使用人"));
            SetCellText(table, 2, 2, Value(record,
                ParcelSurveyFieldKeys.OwnerName));
            SetCellText(table, 2, 12, Value(record,
                ParcelSurveyFieldKeys.OwnerType));
            SetCellText(table, 3, 12, Value(record,
                ParcelSurveyFieldKeys.CertificateType));
            SetCellText(table, 4, 12, Value(record,
                ParcelSurveyFieldKeys.CertificateNumber));
            SetCellText(table, 5, 12, Contact(record,
                ParcelSurveyFieldKeys.ContactAddress,
                ParcelSurveyFieldKeys.ContactPhone));
            SetCellText(table, 6, 2, Value(record, "rights.rightType"));
            SetCellText(table, 6, 10, Value(record, "rights.rightNature"));
            SetCellText(table, 6, 15, Value(record,
                "rights.sourceMaterial"));
            SetCellText(table, 7, 2, Value(record,
                ParcelSurveyFieldKeys.ParcelLocation));

            bool personal = string.Equals(Value(record,
                ParcelSurveyFieldKeys.OwnerType), "个人",
                StringComparison.OrdinalIgnoreCase);
            SetCellText(table, 8, 2, personal ? "/" : Value(record,
                "rights.legalRepresentativeName"));
            SetCellText(table, 8, 7, personal ? "/" : Value(record,
                "rights.legalRepresentativeCertificateType"));
            SetCellText(table, 8, 17, personal ? "/" : Value(record,
                "rights.legalRepresentativePhone"));
            SetCellText(table, 9, 7, personal ? "/" : Value(record,
                "rights.legalRepresentativeCertificateNumber"));

            bool hasAgent = Boolean(record, "rights.hasAgent");
            SetCellText(table, 10, 2, ConditionalValue(record, hasAgent,
                "rights.agentName"));
            SetCellText(table, 10, 7, ConditionalValue(record, hasAgent,
                "rights.agentCertificateType"));
            SetCellText(table, 10, 17, ConditionalValue(record, hasAgent,
                "rights.agentPhone"));
            SetCellText(table, 11, 7, ConditionalValue(record, hasAgent,
                "rights.agentCertificateNumber"));

            SetCellText(table, 12, 2, Value(record,
                "rights.establishmentMode"));
            SetCellText(table, 13, 2, FieldText(record,
                "rights.industryCode"));
            SetCellText(table, 15, 2, FieldText(record,
                ParcelSurveyFieldKeys.PreliminaryParcelCode));
            SetCellText(table, 15, 11, Value(record,
                ParcelSurveyFieldKeys.ParcelCode));
            SetCellText(table, 16, 2, Value(record,
                ParcelSurveyFieldKeys.RealEstateUnitNumber));
            SetCellText(table, 17, 5, Value(record, "parcel.mapScale"));
            SetCellText(table, 18, 5, Value(record,
                "parcel.mapSheetNumber"));
            SetCellText(table, 19, 2, BoundaryText("北", Value(record,
                "parcel.northBoundary")));
            SetCellText(table, 20, 2, BoundaryText("东", Value(record,
                "parcel.eastBoundary")));
            SetCellText(table, 21, 2, BoundaryText("南", Value(record,
                "parcel.southBoundary")));
            SetCellText(table, 22, 2, BoundaryText("西", Value(record,
                "parcel.westBoundary")));
            SetCellText(table, 23, 2, FieldText(record, "land.grade"));
            SetCellText(table, 23, 13, FieldText(record, "land.price"));
            SetCellText(table, 24, 2, Value(record, "land.approvedUse"));
            SetCellText(table, 24, 11, Value(record, "land.actualUse"));
            SetCellText(table, 25, 4, Value(record,
                "land.approvedUseCode"));
            SetCellText(table, 25, 16, Value(record,
                "land.actualUseCode"));
            SetCellText(table, 26, 2, FieldText(record,
                "land.approvedArea", "0.00"));
            SetCellText(table, 26, 8, FieldText(record,
                ParcelSurveyFieldKeys.ParcelArea, "0.00"));
            SetCellText(table, 26, 16, FieldText(record,
                ParcelSurveyFieldKeys.BuildingFootprintTotal, "0.00"));
            SetCellText(table, 28, 16, FieldText(record,
                ParcelSurveyFieldKeys.BuildingAreaTotal, "0.00"));
            SetCellText(table, 29, 2, LandTerm(record));
            SetCellText(table, 30, 2, CoOwnership(record));
            SetCellText(table, 32, 2, Value(record,
                "parcel.description"));
            SetElementText(NextElement(table), Footer(record));
        }

        private static void WriteBoundaryMarks(IList<PageBlock> pages,
            ParcelSurveyRecord record,
            IList<ParcelSurveyExcelExporter.BoundaryExportRow> rows)
        {
            for (int page = 0; page < pages.Count; page++)
            {
                XElement table = pages[page].Table;
                ClearBoundaryMarkPage(table);
                int offset = page * BoundaryRowsPerPage;
                int count = Math.Min(BoundaryRowsPerPage,
                    Math.Max(0, rows.Count - offset));
                if (count > 0)
                {
                    ParcelSurveyExcelExporter.BoundaryExportRow first =
                        rows[offset];
                    SetCellText(table, 5, 0, first.StartPointNumber);
                    SetCheck(table, 5, MarkerColumns,
                        first.StartMarkerType);
                    for (int i = 0; i < count; i++)
                    {
                        ParcelSurveyExcelExporter.BoundaryExportRow item =
                            rows[offset + i];
                        int segmentRow = 5 + i * 2;
                        int endpointRow = 6 + i * 2;
                        SetCellText(table, segmentRow, 6,
                            FormatDecimal(item.Distance, "0.00"));
                        SetCheck(table, segmentRow, CategoryColumns,
                            item.LineCategory);
                        SetCheck(table, segmentRow, PositionColumns,
                            item.LinePosition);
                        SetCellText(table, segmentRow, 18, string.Empty);
                        SetCellText(table, endpointRow, 0,
                            item.EndPointNumber);
                        SetCheck(table, endpointRow, MarkerColumns,
                            item.EndMarkerType);
                    }
                }
                SetElementText(pages[page].Footer, Footer(record));
            }
        }

        private static void WriteBoundarySignatures(IList<PageBlock> pages,
            ParcelSurveyRecord record)
        {
            const bool outputNames = false;
            for (int page = 0; page < pages.Count; page++)
            {
                XElement table = pages[page].Table;
                for (int row = 3; row < 3 + SignatureRowsPerPage; row++)
                    for (int column = 0; column < 7; column++)
                        ClearElementText(CellAt(table, row, column));
                int offset = page * SignatureRowsPerPage;
                int count = Math.Min(SignatureRowsPerPage, Math.Max(0,
                    record.Boundary.SignatureGroups.Count - offset));
                for (int i = 0; i < count; i++)
                {
                    ParcelBoundarySignatureGroupRecord group =
                        record.Boundary.SignatureGroups[offset + i];
                    int row = 3 + i;
                    SetCellText(table, row, 0, group.StartPointNumber);
                    SetCellText(table, row, 1, SlashIfEmpty(
                        group.MiddlePointNumbers));
                    SetCellText(table, row, 2, group.EndPointNumber);
                    SetCellText(table, row, 3, Neighbor(group));
                    bool leaveBlank = group.PreservePaperSignatureBlank
                        || !outputNames;
                    SetCellText(table, row, 4, leaveBlank ? string.Empty
                        : group.NeighborRepresentative);
                    SetCellText(table, row, 5, leaveBlank ? string.Empty
                        : group.ParcelRepresentative);
                    SetCellText(table, row, 6,
                        ChineseDate(group.ConfirmationDate));
                }
                SetElementText(pages[page].Footer, Footer(record));
            }
        }

        private static void WriteBoundaryDescriptions(XElement table,
            ParcelSurveyRecord record)
        {
            SetCellText(table, 1, 1, Value(record,
                "boundary.pointDescription"));
            SetCellText(table, 2, 1, Value(record,
                "boundary.lineDescription"));
            SetElementText(NextElement(table), Footer(record));
        }

        private static void WriteAuditArea(XElement table,
            ParcelSurveyRecord record)
        {
            const string prefix = "经实地调查测量，该不动产宗地面积为：";
            const string suffix = "平方米";
            XElement target = table.Descendants(W + "t").FirstOrDefault(x =>
                (x.Value ?? string.Empty).Contains(prefix));
            if (target == null) throw new InvalidDataException(
                "地籍调查表 Word 模板的权属调查记事中缺少宗地面积插入位置。");
            string text = target.Value ?? string.Empty;
            int start = text.IndexOf(prefix, StringComparison.Ordinal);
            int end = text.IndexOf(suffix, start + prefix.Length,
                StringComparison.Ordinal);
            if (end < 0) throw new InvalidDataException(
                "地籍调查表 Word 模板的权属调查记事中缺少面积单位。");
            string area = FieldText(record, ParcelSurveyFieldKeys.ParcelArea,
                "0.00");
            target.Value = text.Substring(0, start + prefix.Length) + area
                + text.Substring(end);
        }

        private static void WriteHouses(IList<PageBlock> pages,
            ParcelSurveyRecord record)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                ParcelBuildingRecord building = i < record.Buildings.Count
                    ? record.Buildings[i] : new ParcelBuildingRecord();
                building.Normalize();
                WriteHouse(pages[i], record, building);
            }
        }

        private static void WriteHouse(PageBlock page,
            ParcelSurveyRecord record, ParcelBuildingRecord building)
        {
            XElement table = page.Table;
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

            SetInlineFields(CellAt(table, 1, 0), new[]
            {
                new InlineField("不动产单元代码", "：" + unitCode)
            });
            SetInlineFields(CellAt(table, 1, 2), new[]
            {
                new InlineField("县级行政区代码：",
                    Value(record, "project.countyCode")),
                new InlineField("地籍区代码：",
                    Value(record, "project.cadastralDistrictCode")),
                new InlineField("地籍子区代码：",
                    Value(record, "project.cadastralSubdistrictCode")),
                new InlineField("宗地号：",
                    Value(record, ParcelSurveyFieldKeys.ParcelNumber)),
                new InlineField("定着物单元（房屋）代码：", unitCode)
            });
            string unitType = Value(record, "house.unitType");
            SetCheckboxesByOrder(CellAt(table, 2, 2),
                string.Equals(unitType, "幢", StringComparison.Ordinal),
                string.Equals(unitType, "层", StringComparison.Ordinal),
                string.Equals(unitType, "套", StringComparison.Ordinal),
                string.Equals(unitType, "间", StringComparison.Ordinal));
            SetCellText(table, 2, 15, Value(record, "project.name"));
            SetCellText(table, 3, 1, Value(record, "house.location"));
            SetCellText(table, 3, 16, Value(record, "project.postalCode"));
            string roles = followOwner
                ? Value(record, "rights.identityRoles")
                : Value(record, "house.identityRoles");
            SetCheckboxesByOrder(CellAt(table, 4, 0),
                ContainsSelection(roles, "权利人")
                    || ContainsSelection(roles, "所有权人"),
                ContainsSelection(roles, "实际使用人"));
            SetCellText(table, 4, 1, ownerName);
            SetCellText(table, 4, 13, certificateType);
            SetCellText(table, 5, 13, certificateNumber);
            SetCellText(table, 6, 13, Contact(address, phone));
            SetCellText(table, 7, 1, ownerType);
            SetCellText(table, 7, 9, Value(record, "house.plannedUse"));
            SetCellText(table, 7, 14, Value(record, "house.coOwnership"));
            SetCellText(table, 8, 1, Value(record, "house.nature"));
            SetCellText(table, 8, 9, Value(record, "house.actualUse"));
            SetCellText(table, 9, 1, FieldText(record,
                "house.sharedArea", "0.00"));

            SetCellText(table, 12, 1, BuildingField(building,
                "building.number"));
            SetCellText(table, 12, 2, BuildingField(building,
                "building.householdNumber"));
            SetCellText(table, 12, 3, BuildingField(building,
                "building.totalUnits"));
            SetCellText(table, 12, 4, BuildingField(building,
                "building.totalFloors"));
            SetCellText(table, 12, 5, BuildingField(building,
                "building.floor"));
            SetCellText(table, 12, 6, BuildingField(building,
                "building.structure"));
            SetCellText(table, 12, 7, BuildingField(building,
                "building.completionDate"));
            SetCellText(table, 12, 8, BuildingField(building,
                "building.layout"));
            SetCellText(table, 12, 9, BuildingField(building,
                "building.orientation"));
            SetCellText(table, 12, 11, BuildingField(building,
                "building.footprintArea", "0.00"));
            SetCellText(table, 12, 12, BuildingField(building,
                "building.area", "0.00"));
            SetCellText(table, 12, 13, BuildingField(building,
                "building.exclusiveArea", "0.00"));
            SetCellText(table, 12, 14, BuildingField(building,
                "building.allocatedArea", "0.00"));
            SetCellText(table, 12, 16, BuildingField(building,
                "building.propertySource"));
            SetCellText(table, 12, 17, BuildingField(building,
                "building.wall东"));
            SetCellText(table, 12, 18, BuildingField(building,
                "building.wall南"));
            SetCellText(table, 12, 19, BuildingField(building,
                "building.wall西"));
            SetCellText(table, 12, 20, BuildingField(building,
                "building.wall北"));
            SetCellText(table, 13, 1, BuildingField(building,
                "building.sketch"));
            SetCellText(table, 13, 14, BuildingField(building,
                "building.notes"));
            SetCellText(table, 14, 14, BuildingField(building,
                "building.reviewOpinion"));
            SetInlineFields(page.Footer, new[]
            {
                new InlineField("调查员：",
                    Value(record, "project.rightsSurveyor")),
                new InlineField("日期：", ChineseDate(
                    Value(record, "project.rightsSurveyDate")))
            });
        }

        private static IList<PageBlock> PrepareBoundaryPages(
            XElement firstTable, XElement secondTable, int pageCount)
        {
            PageBlock first = BlockWithTitle(firstTable);
            PageBlock second = BlockWithTitle(secondTable);
            var result = new List<PageBlock> { first };
            if (pageCount == 1)
            {
                second.Title.Remove();
                second.Table.Remove();
                second.Footer.Remove();
                return result;
            }
            result.Add(second);
            PageBlock last = second;
            while (result.Count < pageCount)
            {
                var clone = new PageBlock
                {
                    Title = new XElement(first.Title),
                    Table = new XElement(first.Table),
                    Footer = new XElement(first.Footer)
                };
                last.Footer.AddAfterSelf(clone.Title, clone.Table,
                    clone.Footer);
                result.Add(clone);
                last = clone;
            }
            return result;
        }

        private static IList<PageBlock> PreparePagedTableBlocks(
            XElement templateTable, int pageCount,
            bool forcePageBreakBeforeClone = false)
        {
            XElement footer = NextElement(templateTable);
            if (footer == null || footer.Name != W + "p")
                throw new InvalidDataException(
                    "地籍调查表 Word 模板的分页表格缺少页脚段落。");
            var first = new PageBlock
            {
                Table = templateTable,
                Footer = footer
            };
            var result = new List<PageBlock> { first };
            PageBlock last = first;
            while (result.Count < pageCount)
            {
                var clone = new PageBlock
                {
                    Table = new XElement(templateTable),
                    Footer = new XElement(footer)
                };
                if (forcePageBreakBeforeClone)
                    last.Footer.AddAfterSelf(CreatePageBreakParagraph(),
                        clone.Table, clone.Footer);
                else
                    last.Footer.AddAfterSelf(clone.Table, clone.Footer);
                result.Add(clone);
                last = clone;
            }
            return result;
        }

        private static XElement CreatePageBreakParagraph()
        {
            return new XElement(W + "p",
                new XElement(W + "r",
                    new XElement(W + "br",
                        new XAttribute(W + "type", "page"))));
        }

        private static PageBlock BlockWithTitle(XElement table)
        {
            XElement title = PreviousElement(table);
            XElement footer = NextElement(table);
            if (title == null || title.Name != W + "p"
                || footer == null || footer.Name != W + "p")
                throw new InvalidDataException(
                    "地籍调查表 Word 模板的界址标示表页块不完整。");
            return new PageBlock
            {
                Title = title,
                Table = table,
                Footer = footer
            };
        }

        private static void ClearBoundaryMarkPage(XElement table)
        {
            for (int row = 5; row < 40; row++)
                for (int column = 0; column < 19; column++)
                    ClearElementText(CellAt(table, row, column));
        }

        private static void SetCheck(XElement table, int row,
            IDictionary<string, int> columns, string selected)
        {
            foreach (int column in columns.Values)
                ClearElementText(CellAt(table, row, column));
            int target;
            if (!string.IsNullOrWhiteSpace(selected)
                && columns.TryGetValue(selected.Trim(), out target))
                SetCellText(table, row, target, CheckMark);
        }

        private static XElement FindParagraph(XElement element,
            string text)
        {
            XElement paragraph = element.Descendants(W + "p")
                .FirstOrDefault(x => ElementText(x).IndexOf(text,
                    StringComparison.Ordinal) >= 0);
            if (paragraph == null) throw new InvalidDataException(
                "地籍调查表 Word 模板缺少字段：“" + text + "”。");
            return paragraph;
        }

        private static XElement CellAt(XElement table, int rowIndex,
            int logicalColumn)
        {
            List<XElement> rows = table.Elements(W + "tr").ToList();
            if (rowIndex < 0 || rowIndex >= rows.Count)
                throw new InvalidDataException("Word 模板表格行索引越界。");
            int start = 0;
            foreach (XElement cell in rows[rowIndex].Elements(W + "tc"))
            {
                int span = 1;
                XElement spanElement = cell.Element(W + "tcPr")
                    ?.Element(W + "gridSpan");
                if (spanElement != null)
                    int.TryParse((string)spanElement.Attribute(W + "val"),
                        NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out span);
                if (span < 1) span = 1;
                if (logicalColumn >= start && logicalColumn < start + span)
                    return cell;
                start += span;
            }
            throw new InvalidDataException("Word 模板表格列索引越界。");
        }

        private static void SetCellText(XElement table, int row,
            int column, string value)
        {
            SetElementText(CellAt(table, row, column), value);
        }

        private static void SetCheckboxesByOrder(XElement element,
            params bool[] selected)
        {
            if (element == null) throw new InvalidDataException(
                "Word 模板缺少勾选项位置。");
            EnsureClickableCheckboxNamespaces(element.Document);
            var boxes = new List<CheckboxTarget>();
            foreach (XElement text in element.Descendants(W + "t"))
            {
                string value = text.Value ?? string.Empty;
                for (int position = 0; position < value.Length; position++)
                    if (IsCheckboxCharacter(value[position]))
                        boxes.Add(new CheckboxTarget(text, position));
            }
            if (boxes.Count < selected.Length)
                throw new InvalidDataException(
                    "地籍调查表 Word 模板中的勾选项数量不足。");
            for (int index = 0; index < selected.Length; index++)
                boxes[index].Selected = selected[index];

            int controlId = 410100 + element.Document
                .Descendants(W + "sdt").Count();
            foreach (IGrouping<XElement, CheckboxTarget> group in boxes
                .Take(selected.Length).GroupBy(x => x.Text))
            {
                XElement text = group.Key;
                XElement run = text.Parent;
                if (run == null || run.Name != W + "r"
                    || run.Elements(W + "t").Count() != 1)
                    throw new InvalidDataException(
                        "地籍调查表 Word 模板中的复选框格式不受支持。");

                string value = text.Value ?? string.Empty;
                int start = 0;
                var replacement = new List<XElement>();
                foreach (CheckboxTarget target in group.OrderBy(x =>
                    x.Position))
                {
                    if (target.Position > start)
                        replacement.Add(CloneRunWithText(run,
                            value.Substring(start, target.Position - start)));
                    replacement.Add(CreateClickableCheckbox(run,
                        target.Selected, controlId++));
                    start = target.Position + 1;
                }
                if (start < value.Length)
                    replacement.Add(CloneRunWithText(run,
                        value.Substring(start)));
                run.AddBeforeSelf(replacement);
                run.Remove();
            }
        }

        private static bool IsCheckboxCharacter(char value)
        {
            return value == '□' || value == '√' || value == '☑'
                || value == '☐';
        }

        private static void EnsureClickableCheckboxNamespaces(
            XDocument document)
        {
            XElement root = document == null ? null : document.Root;
            if (root == null) throw new InvalidDataException(
                "Word 模板缺少文档根节点。");
            XNamespace currentW14 = root.GetNamespaceOfPrefix("w14");
            if (currentW14 == null)
                root.Add(new XAttribute(XNamespace.Xmlns + "w14",
                    W14.NamespaceName));
            else if (currentW14 != W14)
                throw new InvalidDataException(
                    "Word 模板的 w14 命名空间不兼容。");

            XNamespace currentMc = root.GetNamespaceOfPrefix("mc");
            if (currentMc == null)
                root.Add(new XAttribute(XNamespace.Xmlns + "mc",
                    MC.NamespaceName));
            else if (currentMc != MC)
                throw new InvalidDataException(
                    "Word 模板的 mc 命名空间不兼容。");

            XAttribute ignorable = root.Attribute(MC + "Ignorable");
            var prefixes = new HashSet<string>((ignorable == null
                ? string.Empty : ignorable.Value).Split(new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);
            if (prefixes.Add("w14"))
            {
                string value = string.Join(" ", prefixes);
                if (ignorable == null)
                    root.Add(new XAttribute(MC + "Ignorable", value));
                else
                    ignorable.Value = value;
            }
        }

        private static XElement CloneRunWithText(XElement templateRun,
            string value)
        {
            XElement run = new XElement(templateRun);
            XElement text = run.Elements(W + "t").Single();
            text.Value = value ?? string.Empty;
            SetPreserveSpace(text);
            return run;
        }

        private static XElement CreateClickableCheckbox(
            XElement templateRun, bool selected, int controlId)
        {
            XElement displayRun = CloneRunWithText(templateRun,
                selected ? "☑" : "☐");
            return new XElement(W + "sdt",
                new XElement(W + "sdtPr",
                    new XElement(W + "alias",
                        new XAttribute(W + "val", "CDBox 复选框")),
                    new XElement(W + "tag",
                        new XAttribute(W + "val", "CDBoxCheckbox")),
                    new XElement(W + "id",
                        new XAttribute(W + "val", controlId.ToString(
                            CultureInfo.InvariantCulture))),
                    new XElement(W14 + "checkbox",
                        new XElement(W14 + "checked",
                            new XAttribute(W14 + "val",
                                selected ? "1" : "0")),
                        new XElement(W14 + "checkedState",
                            new XAttribute(W14 + "val", "2611"),
                            new XAttribute(W14 + "font", "MS Gothic")),
                        new XElement(W14 + "uncheckedState",
                            new XAttribute(W14 + "val", "2610"),
                            new XAttribute(W14 + "font", "MS Gothic")))),
                new XElement(W + "sdtContent", displayRun));
        }

        private static void SetUnderlinedValueAfterLabel(XElement paragraph,
            string label, string value)
        {
            if (paragraph == null) throw new InvalidDataException(
                "Word 模板缺少带下划线的填写位置。");
            List<XElement> texts = paragraph.Descendants(W + "t").ToList();
            string full = string.Concat(texts.Select(x => x.Value));
            int labelStart = full.IndexOf(label, StringComparison.Ordinal);
            if (labelStart < 0) throw new InvalidDataException(
                "地籍调查表 Word 模板缺少字段：“" + label + "”。");
            int labelEnd = labelStart + label.Length;
            int position = 0;
            var slot = new List<XElement>();
            foreach (XElement text in texts)
            {
                int end = position + (text.Value ?? string.Empty).Length;
                XElement runProperties = text.Parent == null ? null
                    : text.Parent.Element(W + "rPr");
                XElement underline = runProperties == null ? null
                    : runProperties.Element(W + "u");
                string underlineValue = underline == null ? string.Empty
                    : ((string)underline.Attribute(W + "val") ?? "single");
                if (end > labelEnd && underline != null
                    && !string.Equals(underlineValue, "none",
                        StringComparison.OrdinalIgnoreCase))
                    slot.Add(text);
                position = end;
            }
            if (slot.Count == 0) throw new InvalidDataException(
                "地籍调查表 Word 模板字段“" + label
                + "”后缺少带下划线的填写位置。");
            int capacity = slot.Sum(x => (x.Value ?? string.Empty).Length);
            value = value ?? string.Empty;
            string padded = value + new string(' ', Math.Max(1,
                capacity - value.Length));
            slot[0].Value = padded;
            SetPreserveSpace(slot[0]);
            for (int i = 1; i < slot.Count; i++) slot[i].Value = string.Empty;
        }

        private static void SetInlineFields(XElement element,
            IList<InlineField> fields)
        {
            if (element == null) throw new InvalidDataException(
                "Word 模板缺少待填写位置。");
            if (fields == null || fields.Count == 0) return;
            List<XElement> texts = element.Descendants(W + "t").ToList();
            string full = string.Concat(texts.Select(x => x.Value));
            var labelStarts = new int[fields.Count];
            int searchFrom = 0;
            for (int i = 0; i < fields.Count; i++)
            {
                labelStarts[i] = full.IndexOf(fields[i].Label, searchFrom,
                    StringComparison.Ordinal);
                if (labelStarts[i] < 0) throw new InvalidDataException(
                    "地籍调查表 Word 模板缺少字段：“"
                    + fields[i].Label + "”。");
                searchFrom = labelStarts[i] + fields[i].Label.Length;
            }
            var spans = new List<TextSpan>();
            int position = 0;
            foreach (XElement text in texts)
            {
                string current = text.Value ?? string.Empty;
                spans.Add(new TextSpan(text, position,
                    position + current.Length));
                position += current.Length;
            }
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                int start = labelStarts[i] + fields[i].Label.Length;
                int end = i + 1 < fields.Count ? labelStarts[i + 1]
                    : full.Length;
                string value = fields[i].Value ?? string.Empty;
                if (i + 1 < fields.Count) value += "    ";
                ReplaceTextSpan(element, spans, start, end, value);
            }
        }

        private static void ReplaceTextSpan(XElement element,
            IList<TextSpan> spans, int start, int end, string value)
        {
            List<TextSpan> affected = spans.Where(x => x.End > start
                && x.Start < end).ToList();
            if (affected.Count == 0)
            {
                XElement lastText = element.Descendants(W + "t")
                    .LastOrDefault();
                XElement paragraph = element.Name == W + "p" ? element
                    : element.Descendants(W + "p").LastOrDefault();
                if (paragraph == null) throw new InvalidDataException(
                    "Word 模板缺少可继承样式的段落。");
                XElement run = lastText == null ? null : lastText.Parent;
                XElement clone = new XElement(W + "r");
                XElement properties = run == null ? null
                    : run.Element(W + "rPr");
                if (properties != null) clone.Add(new XElement(properties));
                var text = new XElement(W + "t", value);
                SetPreserveSpace(text);
                clone.Add(text);
                paragraph.Add(clone);
                return;
            }
            TextSpan target = affected.FirstOrDefault(x =>
            {
                string source = x.Text.Value ?? string.Empty;
                int localStart = Math.Max(0, start - x.Start);
                int localEnd = Math.Min(source.Length, end - x.Start);
                return localEnd > localStart && source.Substring(localStart,
                    localEnd - localStart).Trim().Length == 0;
            }) ?? affected[0];
            foreach (TextSpan span in affected)
            {
                string source = span.Text.Value ?? string.Empty;
                int localStart = Math.Max(0, start - span.Start);
                int localEnd = Math.Min(source.Length, end - span.Start);
                string replacement = span == target ? value : string.Empty;
                span.Text.Value = source.Substring(0, localStart)
                    + replacement + source.Substring(localEnd);
                SetPreserveSpace(span.Text);
            }
        }

        private static void SetElementText(XElement element, string value)
        {
            if (element == null) throw new InvalidDataException(
                "Word 模板缺少待填写位置。");
            value = (value ?? string.Empty).Replace("\r\n", "\n")
                .Replace('\r', '\n');
            List<XElement> texts = element.Descendants(W + "t").ToList();
            XElement first = texts.FirstOrDefault();
            if (first == null)
            {
                XElement paragraph = element.Name == W + "p" ? element
                    : element.Descendants(W + "p").FirstOrDefault();
                if (paragraph == null)
                {
                    paragraph = new XElement(W + "p");
                    element.Add(paragraph);
                }
                XElement run = paragraph.Elements(W + "r").FirstOrDefault();
                if (run == null)
                {
                    run = new XElement(W + "r");
                    paragraph.Add(run);
                }
                first = new XElement(W + "t");
                run.Add(first);
                texts.Add(first);
            }
            foreach (XElement text in texts) text.Value = string.Empty;
            foreach (XElement br in element.Descendants(W + "br").ToList())
                br.Remove();
            string[] lines = value.Split('\n');
            first.Value = lines.Length == 0 ? string.Empty : lines[0];
            SetPreserveSpace(first);
            XElement parentRun = first.Parent;
            for (int i = 1; i < lines.Length; i++)
            {
                parentRun.Add(new XElement(W + "br"));
                var text = new XElement(W + "t", lines[i]);
                SetPreserveSpace(text);
                parentRun.Add(text);
            }
        }

        private static bool IsSelected(ParcelSurveyRecord record,
            string key, string option)
        {
            ParcelSurveyFieldValue field = record.Field(key);
            if (field != null && field.Selections != null
                && field.Selections.Any(x => string.Equals((x ?? string.Empty)
                    .Trim(), option, StringComparison.OrdinalIgnoreCase)))
                return true;
            return ContainsSelection(Value(record, key), option);
        }

        private static bool ContainsSelection(string value, string option)
        {
            return (value ?? string.Empty).Split(new[] { '、', ',', '，',
                    ';', '；', '/', '|', '\n', '\r' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Any(x => string.Equals(x.Trim(), option,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static void ClearElementText(XElement element)
        {
            if (element == null) return;
            foreach (XElement text in element.Descendants(W + "t"))
                text.Value = string.Empty;
            foreach (XElement br in element.Descendants(W + "br").ToList())
                br.Remove();
        }

        private static void SetPreserveSpace(XElement text)
        {
            string value = text.Value ?? string.Empty;
            if (value.Length > 0 && (char.IsWhiteSpace(value[0])
                || char.IsWhiteSpace(value[value.Length - 1])
                || value.Contains("  ")))
                text.SetAttributeValue(XmlNamespace + "space", "preserve");
            else
                text.SetAttributeValue(XmlNamespace + "space", null);
        }

        private static XElement PreviousElement(XElement element)
        {
            return element == null ? null
                : element.ElementsBeforeSelf().LastOrDefault();
        }

        private static XElement NextElement(XElement element)
        {
            return element == null ? null
                : element.ElementsAfterSelf().FirstOrDefault();
        }

        private static string ElementText(XElement element)
        {
            return element == null ? string.Empty : string.Concat(
                element.Descendants(W + "t").Select(x => x.Value));
        }

        private static string FieldText(ParcelSurveyRecord record,
            string key, string numericFormat = "0.##")
        {
            ParcelSurveyFieldValue field = record.Field(key);
            if (field == null) return string.Empty;
            if (field.Status == ParcelFieldStatus.NotApplicable) return "/";
            if (field.NumericValue.HasValue)
                return field.NumericValue.Value.ToString(numericFormat,
                    CultureInfo.InvariantCulture);
            if (field.Selections != null && field.Selections.Count > 0)
                return string.Join("、", field.Selections);
            return field.TextValue ?? string.Empty;
        }

        private static string BuildingField(ParcelBuildingRecord building,
            string key, string numericFormat = "0.##")
        {
            ParcelSurveyFieldValue field;
            if (building == null || !building.Fields.TryGetValue(key,
                out field) || field == null) return string.Empty;
            if (field.Status == ParcelFieldStatus.NotApplicable) return "/";
            if (field.NumericValue.HasValue)
                return field.NumericValue.Value.ToString(numericFormat,
                    CultureInfo.InvariantCulture);
            if (field.Selections != null && field.Selections.Count > 0)
                return string.Join("、", field.Selections);
            return field.TextValue ?? string.Empty;
        }

        private static string FormatDecimal(decimal? value, string format)
        {
            return value.HasValue ? value.Value.ToString(format,
                CultureInfo.InvariantCulture) : string.Empty;
        }

        private static string Value(ParcelSurveyRecord record, string key)
        {
            return ParcelSurveyExcelExporter.Value(record, key);
        }

        private static bool Boolean(ParcelSurveyRecord record, string key)
        {
            return ParcelSurveyExcelExporter.Boolean(record, key);
        }

        private static string Contact(ParcelSurveyRecord record,
            string addressKey, string phoneKey)
        {
            return ParcelSurveyExcelExporter.Contact(record, addressKey,
                phoneKey);
        }

        private static string Contact(string address, string phone)
        {
            return ParcelSurveyExcelExporter.Contact(address, phone);
        }

        private static string BoundaryText(string direction, string value)
        {
            return ParcelSurveyExcelExporter.BoundaryText(direction, value);
        }

        private static string ConditionalValue(ParcelSurveyRecord record,
            bool condition, string key)
        {
            return ParcelSurveyExcelExporter.ConditionalValue(record,
                condition, key);
        }

        private static string LandTerm(ParcelSurveyRecord record)
        {
            return ParcelSurveyExcelExporter.LandTerm(record);
        }

        private static string CoOwnership(ParcelSurveyRecord record)
        {
            return ParcelSurveyExcelExporter.CoOwnership(record);
        }

        private static string Footer(ParcelSurveyRecord record)
        {
            return ParcelSurveyExcelExporter.Footer(record);
        }

        private static string Neighbor(
            ParcelBoundarySignatureGroupRecord group)
        {
            return ParcelSurveyExcelExporter.Neighbor(group);
        }

        private static string ChineseDate(string value)
        {
            return ParcelSurveyExcelExporter.ChineseDate(value);
        }

        private static string SlashIfEmpty(string value)
        {
            return ParcelSurveyExcelExporter.SlashIfEmpty(value);
        }

        private static string ResolveTemplatePath()
        {
            string assemblyDirectory = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                string location = typeof(ParcelSurveyWordExporter).Assembly
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

        private static void VerifyExport(string outputPath,
            ParcelSurveyRecord record, int boundaryRowCount,
            int boundaryPages, int signaturePages, int housePages)
        {
            using (FileStream stream = new FileStream(outputPath,
                FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive package = new ZipArchive(stream,
                ZipArchiveMode.Read, false))
            {
                ZipArchiveEntry entry = package.GetEntry(DocumentPartName);
                if (entry == null) throw new InvalidDataException(
                    "导出回读失败：Word 文档缺少 document.xml。");
                XDocument document;
                using (Stream input = entry.Open())
                    document = XDocument.Load(input,
                        LoadOptions.PreserveWhitespace);
                XElement body = document.Root?.Element(W + "body");
                if (body == null) throw new InvalidDataException(
                    "导出回读失败：Word 文档缺少正文。");
                List<XElement> tables = body.Elements(W + "tbl").ToList();
                List<XElement> marks = tables.Where(IsBoundaryMarkTable)
                    .ToList();
                if (marks.Count != boundaryPages)
                    throw new InvalidDataException(
                        "导出回读失败：界址标示表页数不正确。");
                int signatureCount = tables.Count(IsBoundarySignatureTable);
                if (signatureCount != signaturePages)
                    throw new InvalidDataException(
                        "导出回读失败：界址签章表页数不正确。");
                int houseCount = tables.Count(IsHouseTable);
                if (houseCount != housePages)
                    throw new InvalidDataException(
                        "导出回读失败：房屋调查表页数不正确。");
                foreach (XElement mark in marks)
                    for (int row = 5; row < 40; row++)
                        if (!string.IsNullOrWhiteSpace(ElementText(
                            CellAt(mark, row, 18))))
                            throw new InvalidDataException(
                                "导出回读失败：界址标示表备注列必须为空。");
                if (boundaryRowCount > 0 && string.IsNullOrWhiteSpace(
                    ElementText(CellAt(marks[0], 5, 0))))
                    throw new InvalidDataException(
                        "导出回读失败：界址标示表缺少界址点号。");

                XElement basic = tables.FirstOrDefault(IsBasicTable);
                string expectedCode = Value(record,
                    ParcelSurveyFieldKeys.ParcelCode);
                string actualCode = basic == null ? string.Empty
                    : ElementText(CellAt(basic, 15, 11));
                if (!string.Equals(expectedCode, actualCode,
                    StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "导出回读失败：宗地代码未正确写入基本表。");

                XElement audit = tables.FirstOrDefault(IsAuditTable);
                string auditText = ElementText(audit);
                string expectedArea = FieldText(record,
                    ParcelSurveyFieldKeys.ParcelArea, "0.00");
                string expectedSentence =
                    "该不动产宗地面积为：" + expectedArea + "平方米";
                if (audit == null || auditText.IndexOf(expectedSentence,
                    StringComparison.Ordinal) < 0)
                    throw new InvalidDataException(
                        "导出回读失败：调查审核表宗地面积未正确写入。");
            }
        }

        private static bool IsBoundaryMarkTable(XElement table)
        {
            string text = ElementText(table);
            return text.Contains("界址点号") && text.Contains("界标种类")
                && text.Contains("界址线位置") && text.Contains("备注");
        }

        private static bool IsBoundarySignatureTable(XElement table)
        {
            return ElementText(table).StartsWith("界址签章表",
                StringComparison.Ordinal);
        }

        private static bool IsHouseTable(XElement table)
        {
            return ElementText(table).StartsWith("房屋基本信息调查表",
                StringComparison.Ordinal);
        }

        private static bool IsBasicTable(XElement table)
        {
            return ElementText(table).StartsWith("宗地基本信息表",
                StringComparison.Ordinal);
        }

        private static bool IsAuditTable(XElement table)
        {
            return ElementText(table).StartsWith("调查审核表",
                StringComparison.Ordinal);
        }

        private sealed class InlineField
        {
            public InlineField(string label, string value)
            {
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
            }

            public string Label { get; private set; }
            public string Value { get; private set; }
        }

        private sealed class TextSpan
        {
            public TextSpan(XElement text, int start, int end)
            {
                Text = text;
                Start = start;
                End = end;
            }

            public XElement Text { get; private set; }
            public int Start { get; private set; }
            public int End { get; private set; }
        }

        private sealed class CheckboxTarget
        {
            public CheckboxTarget(XElement text, int position)
            {
                Text = text;
                Position = position;
            }

            public XElement Text { get; private set; }
            public int Position { get; private set; }
            public bool Selected { get; set; }
        }

        private sealed class PageBlock
        {
            public XElement Title { get; set; }
            public XElement Table { get; set; }
            public XElement Footer { get; set; }
        }
    }
}
