using System;
using System.Collections.Generic;

namespace CDBox.RealEstate.Models
{
    public sealed class ParcelSurveyFieldDefinition
    {
        public string Key { get; set; }
        public int Tab { get; set; }
        public string Group { get; set; }
        public string Label { get; set; }
        public string Kind { get; set; }
        public bool Required { get; set; }
        public bool AllowNotApplicable { get; set; }
        public bool SupportsProjectDefault { get; set; }
        public bool ReadOnly { get; set; }
        public string[] Options { get; set; }
        public string ConditionKey { get; set; }
        public string ConditionOperator { get; set; }
        public string ConditionValue { get; set; }
        public string Unit { get; set; }
        public string Help { get; set; }
        public ParcelFieldStatus DefaultStatus { get; set; }
    }

    public static class ParcelSurveyFieldKeys
    {
        public const string ParcelSeaCode = "parcel.parcelSeaCode";
        public const string PreliminaryParcelCode = "parcel.preliminaryCode";
        public const string ParcelCode = "parcel.parcelCode";
        public const string ParcelNumber = "parcel.parcelNumber";
        public const string RealEstateUnitNumber = "parcel.realEstateUnitNumber";
        public const string ParcelLocation = "parcel.location";
        public const string OwnerName = "rights.ownerName";
        public const string OwnerType = "rights.ownerType";
        public const string CertificateType = "rights.certificateType";
        public const string CertificateNumber = "rights.certificateNumber";
        public const string ContactAddress = "rights.contactAddress";
        public const string ContactPhone = "rights.contactPhone";
        public const string ParcelArea = "land.parcelArea";
        public const string BuildingFootprintTotal = "land.buildingFootprintTotal";
        public const string BuildingAreaTotal = "land.buildingAreaTotal";
    }

    public static class ParcelSurveyFieldCatalog
    {
        public static readonly IList<ParcelSurveyFieldDefinition> Fields =
            BuildFields().AsReadOnly();
        public static readonly IList<ParcelSurveyFieldDefinition> BuildingFields =
            BuildBuildingFields().AsReadOnly();

        private static List<ParcelSurveyFieldDefinition> BuildFields()
        {
            var x = new List<ParcelSurveyFieldDefinition>();
            // 项目与人员
            Add(x, "project.name", 1, "项目资料", "项目名称", "text", false, false, true);
            Add(x, "project.organization", 1, "项目资料", "调查单位（机构）", "text", true, false, true);
            Add(x, "project.surveyDate", 1, "项目资料", "调查日期", "date", true);
            Add(x, "project.formFiller", 1, "填表人员", "填表人", "text", true, false, true,
                help: "仅维护一次，自动用于基本表、界址标示表、界址签章表、界址说明表和调查审核表。" );
            Add(x, "project.formDate", 1, "填表人员", "填表日期", "date", true);
            Add(x, "project.rightsSurveyor", 1, "调查审核人员", "权属调查员姓名", "text", true, false, true);
            Add(x, "project.rightsSurveyDate", 1, "调查审核人员", "权属调查日期", "date", true);
            Add(x, "project.surveyor", 1, "调查审核人员", "测量员姓名", "text", true, false, true);
            Add(x, "project.measureDate", 1, "调查审核人员", "测量日期", "date", true);
            Add(x, "project.reviewer", 1, "调查审核人员", "审核人姓名", "text", true, false, true);
            Add(x, "project.reviewDate", 1, "调查审核人员", "审核日期", "date", true);
            Add(x, "project.countyCode", 1, "行政区划", "县级行政区代码", "text", true, false, true);
            Add(x, "project.cadastralDistrictCode", 1, "行政区划", "地籍区代码", "text", true, false, true);
            Add(x, "project.cadastralSubdistrictCode", 1, "行政区划", "地籍子区代码", "text", true, false, true);
            Add(x, "project.postalCode", 1, "行政区划", "邮政编码", "text", true, false, true);

            // 宗地基本信息
            Add(x, ParcelSurveyFieldKeys.ParcelSeaCode, 2, "代码与坐落", "宗地/宗海代码", "text", true);
            Add(x, ParcelSurveyFieldKeys.PreliminaryParcelCode, 2, "代码与坐落", "预编宗地代码", "text", true, true);
            Add(x, ParcelSurveyFieldKeys.ParcelCode, 2, "代码与坐落", "宗地代码", "text", true);
            Add(x, ParcelSurveyFieldKeys.ParcelNumber, 2, "代码与坐落", "宗地号", "text", true, false, false, true,
                ParcelFieldStatus.Automatic);
            Add(x, ParcelSurveyFieldKeys.RealEstateUnitNumber, 2, "代码与坐落", "不动产单元号", "text", true);
            Add(x, ParcelSurveyFieldKeys.ParcelLocation, 2, "代码与坐落", "宗地坐落", "textarea", true);
            Add(x, "parcel.mapScale", 2, "图幅信息", "所在图幅比例尺", "text", true, true, true);
            Add(x, "parcel.mapSheetNumber", 2, "图幅信息", "图幅号", "text", true, true);
            Add(x, "parcel.northBoundary", 2, "宗地四至", "北至", "textarea", true);
            Add(x, "parcel.eastBoundary", 2, "宗地四至", "东至", "textarea", true);
            Add(x, "parcel.southBoundary", 2, "宗地四至", "南至", "textarea", true);
            Add(x, "parcel.westBoundary", 2, "宗地四至", "西至", "textarea", true);
            Add(x, "parcel.description", 2, "补充说明", "宗地说明", "textarea");

            // 权利人与权属
            Add(x, "rights.landOwnershipType", 3, "权利人", "土地所有权类型", "select", true, false, true,
                options: O("国家", "集体", "其他"));
            Add(x, "rights.identityRoles", 3, "权利人", "身份角色", "multiselect", true,
                options: O("权利人", "实际使用人"));
            Add(x, ParcelSurveyFieldKeys.OwnerName, 3, "权利人", "权利人姓名/名称", "text", true);
            Add(x, ParcelSurveyFieldKeys.OwnerType, 3, "权利人", "权利人类型", "select", true,
                options: O("个人", "法人", "其他组织", "其他"));
            Add(x, ParcelSurveyFieldKeys.CertificateType, 3, "权利人", "证件种类", "select", true, false, true,
                options: O("居民身份证", "统一社会信用代码", "护照", "其他"));
            Add(x, ParcelSurveyFieldKeys.CertificateNumber, 3, "权利人", "证件号码", "text", true);
            Add(x, ParcelSurveyFieldKeys.ContactAddress, 3, "权利人", "通讯地址", "textarea", true);
            Add(x, ParcelSurveyFieldKeys.ContactPhone, 3, "权利人", "联系电话", "text", true);
            AddConditional(x, "rights.legalRepresentativeName", 3, "法定代表人或负责人", "姓名", "text",
                ParcelSurveyFieldKeys.OwnerType, "neq", "个人");
            AddConditional(x, "rights.legalRepresentativeCertificateType", 3, "法定代表人或负责人", "证件种类", "select",
                ParcelSurveyFieldKeys.OwnerType, "neq", "个人", O("居民身份证", "护照", "其他"));
            AddConditional(x, "rights.legalRepresentativeCertificateNumber", 3, "法定代表人或负责人", "证件号码", "text",
                ParcelSurveyFieldKeys.OwnerType, "neq", "个人");
            AddConditional(x, "rights.legalRepresentativePhone", 3, "法定代表人或负责人", "联系电话", "text",
                ParcelSurveyFieldKeys.OwnerType, "neq", "个人");
            Add(x, "rights.hasAgent", 3, "代理人", "存在代理人", "checkbox");
            AddConditional(x, "rights.agentName", 3, "代理人", "代理人姓名", "text", "rights.hasAgent", "true", "true");
            AddConditional(x, "rights.agentCertificateType", 3, "代理人", "证件种类", "select", "rights.hasAgent", "true", "true", O("居民身份证", "护照", "其他"));
            AddConditional(x, "rights.agentCertificateNumber", 3, "代理人", "证件号码", "text", "rights.hasAgent", "true", "true");
            AddConditional(x, "rights.agentPhone", 3, "代理人", "联系电话", "text", "rights.hasAgent", "true", "true");
            Add(x, "rights.rightType", 3, "权属信息", "权利类型", "select", true, false, true,
                options: O("国有建设用地使用权", "集体建设用地使用权", "宅基地使用权", "土地承包经营权", "其他"));
            Add(x, "rights.rightNature", 3, "权属信息", "权利性质", "select", true, false, true,
                options: O("出让", "划拨", "租赁", "作价出资（入股）", "授权经营", "家庭承包", "其他"));
            Add(x, "rights.sourceMaterial", 3, "权属信息", "土地权属来源证明材料", "textarea", true, true);
            Add(x, "rights.establishmentMode", 3, "权属信息", "权利设定方式", "select", true, false, true,
                options: O("地表", "地上", "地下"));
            Add(x, "rights.industryCode", 3, "权属信息", "国民经济行业分类代码", "text", true, true, true);
            Add(x, "rights.coOwnershipType", 3, "权属信息", "共有/共用情况", "select", true, false, true,
                options: O("单独所有", "共同共有", "按份共有"));
            AddConditional(x, "rights.coOwnerDescription", 3, "权属信息", "共有/共用权利人说明", "textarea",
                "rights.coOwnershipType", "neq", "单独所有");

            // 土地用途与面积
            Add(x, "land.grade", 4, "土地用途", "土地等级", "text", false, true);
            Add(x, "land.price", 4, "土地用途", "土地价格", "number", false, true, unit: "元");
            Add(x, "land.approvedUse", 4, "土地用途", "批准用途", "text", true, false, true);
            Add(x, "land.approvedUseCode", 4, "土地用途", "批准用途地类编码", "text", true);
            Add(x, "land.actualUse", 4, "土地用途", "实际用途", "text", true, false, true);
            Add(x, "land.actualUseCode", 4, "土地用途", "实际用途地类编码", "text", true);
            Add(x, "land.approvedArea", 4, "面积", "批准面积", "number", false, true, unit: "㎡");
            Add(x, ParcelSurveyFieldKeys.ParcelArea, 4, "面积", "宗地面积", "number", true, false, false, true,
                ParcelFieldStatus.Automatic, unit: "㎡", help: "同一数值自动用于基本表与调查审核表。" );
            Add(x, ParcelSurveyFieldKeys.BuildingFootprintTotal, 4, "面积", "建筑占地总面积", "number", true, false, false, true,
                ParcelFieldStatus.Automatic, unit: "㎡");
            Add(x, ParcelSurveyFieldKeys.BuildingAreaTotal, 4, "面积", "建筑总面积", "number", true, false, false, true,
                ParcelFieldStatus.Automatic, unit: "㎡");
            Add(x, "land.termStart", 4, "土地使用期限", "土地使用期限起始日期", "date");
            Add(x, "land.termEnd", 4, "土地使用期限", "土地使用期限终止日期", "date");
            Add(x, "land.termDescription", 4, "土地使用期限", "土地使用期限说明", "textarea");

            // 界址说明的最终业务文本
            Add(x, "boundary.pointDescription", 5, "界址说明", "界址点位说明", "textarea", true);
            Add(x, "boundary.lineDescription", 5, "界址说明", "界址线走向说明", "textarea", true);

            // 房屋公共信息
            Add(x, "house.unitType", 6, "房屋公共信息", "定着物单元类型", "select", true,
                options: O("幢", "层", "套", "间"));
            Add(x, "house.unitCode", 6, "房屋公共信息", "定着物单元代码", "text", true);
            Add(x, "house.location", 6, "房屋公共信息", "房地坐落", "textarea", true);
            Add(x, "house.followParcelOwner", 6, "房屋权利人", "沿用宗地权利人", "checkbox");
            AddConditional(x, "house.identityRoles", 6, "房屋权利人", "所有权人/实际使用人身份", "multiselect",
                "house.followParcelOwner", "false", "false", O("所有权人", "实际使用人"));
            AddConditional(x, "house.ownerName", 6, "房屋权利人", "房屋权利人姓名", "text",
                "house.followParcelOwner", "false", "false");
            AddConditional(x, "house.certificateType", 6, "房屋权利人", "证件种类", "select",
                "house.followParcelOwner", "false", "false", O("居民身份证", "统一社会信用代码", "护照", "其他"));
            AddConditional(x, "house.certificateNumber", 6, "房屋权利人", "证件号码", "text",
                "house.followParcelOwner", "false", "false");
            AddConditional(x, "house.address", 6, "房屋权利人", "住址", "textarea",
                "house.followParcelOwner", "false", "false");
            AddConditional(x, "house.phone", 6, "房屋权利人", "联系电话", "text",
                "house.followParcelOwner", "false", "false");
            AddConditional(x, "house.ownerType", 6, "房屋权利人", "房屋所有权人或实际使用人类型", "select",
                "house.followParcelOwner", "false", "false", O("个人", "法人", "其他组织", "其他"));
            Add(x, "house.plannedUse", 6, "房屋属性", "规划用途", "text", true);
            Add(x, "house.actualUse", 6, "房屋属性", "实际用途", "text", true);
            Add(x, "house.nature", 6, "房屋属性", "房屋性质", "text", true);
            Add(x, "house.coOwnership", 6, "房屋属性", "共有情况", "select", true,
                options: O("单独所有", "共同共有", "按份共有"));
            Add(x, "house.sharedArea", 6, "房屋属性", "共有建筑面积", "number", false, true, unit: "㎡");

            // 调查审核与导出
            Add(x, "audit.rightsNotes", 7, "权属调查", "权属调查记事", "textarea", true, false, true,
                help: "可使用项目标准文本并自动插入宗地面积，最终文字可直接修改。" );
            Add(x, "audit.surveyNotes", 7, "不动产测绘", "不动产测绘记事", "textarea", true, false, true);
            Add(x, "audit.deviceModel", 7, "不动产测绘", "测量设备型号", "text", true, false, true);
            Add(x, "audit.measureMethod", 7, "不动产测绘", "测量方法", "text", true, false, true);
            Add(x, "audit.areaMethod", 7, "不动产测绘", "面积计算方法", "text", true, false, true);
            Add(x, "audit.reviewOpinion", 7, "审核", "审核意见", "textarea", true, false, true);
            Add(x, "audit.signatureHandling", 7, "签章处理", "签章处理", "select", true,
                options: O("留空，打印后手写签章", "输出人员姓名", "使用经授权的电子签章"),
                help: "电子签章仅在以后建立明确授权机制后支持。" );
            return x;
        }

        private static List<ParcelSurveyFieldDefinition> BuildBuildingFields()
        {
            var x = new List<ParcelSurveyFieldDefinition>();
            Add(x, "building.number", 6, "幢信息", "幢号", "text", true);
            Add(x, "building.householdNumber", 6, "幢信息", "户号", "text");
            Add(x, "building.totalUnits", 6, "幢信息", "总套数", "number");
            Add(x, "building.totalFloors", 6, "幢信息", "总层数", "number", true);
            Add(x, "building.floor", 6, "幢信息", "所在层", "text");
            Add(x, "building.structure", 6, "幢信息", "房屋结构", "text", true);
            Add(x, "building.completionDate", 6, "幢信息", "竣工时间", "date");
            Add(x, "building.layout", 6, "幢信息", "户型", "text");
            Add(x, "building.orientation", 6, "幢信息", "朝向", "text", true);
            Add(x, "building.footprintArea", 6, "面积", "占地面积", "number", true, false, false, true,
                ParcelFieldStatus.Automatic, unit: "㎡");
            Add(x, "building.area", 6, "面积", "建筑面积", "number", true, false, false, false,
                ParcelFieldStatus.Automatic, unit: "㎡");
            Add(x, "building.exclusiveArea", 6, "面积", "专有建筑面积", "number", false, true, unit: "㎡");
            Add(x, "building.allocatedArea", 6, "面积", "分摊建筑面积", "number", false, true, unit: "㎡");
            Add(x, "building.propertySource", 6, "产权与墙体", "产权来源", "textarea", false, true);
            foreach (string direction in O("东", "南", "西", "北"))
                Add(x, "building.wall" + direction, 6, "产权与墙体", direction + "墙归属", "select", true,
                    options: O("自有墙", "共有墙", "借墙", "他有墙", "未确认"));
            Add(x, "building.sketch", 6, "成果资料", "房产草图", "text", false, true,
                help: "保存 CAD 自动生成成果或导入图片的引用。" );
            Add(x, "building.notes", 6, "成果资料", "附加说明", "textarea");
            Add(x, "building.reviewOpinion", 6, "成果资料", "调查成果审核意见", "textarea", true, false, true);
            return x;
        }

        private static void Add(List<ParcelSurveyFieldDefinition> target,
            string key, int tab, string group, string label, string kind,
            bool required = false, bool allowNotApplicable = false,
            bool supportsProjectDefault = false, bool readOnly = false,
            ParcelFieldStatus defaultStatus = ParcelFieldStatus.Manual,
            string[] options = null, string unit = null, string help = null)
        {
            target.Add(new ParcelSurveyFieldDefinition
            {
                Key = key,
                Tab = tab,
                Group = group,
                Label = label,
                Kind = kind,
                Required = required,
                AllowNotApplicable = allowNotApplicable,
                SupportsProjectDefault = supportsProjectDefault,
                ReadOnly = readOnly,
                DefaultStatus = defaultStatus,
                Options = options ?? new string[0],
                Unit = unit ?? string.Empty,
                Help = help ?? string.Empty
            });
        }

        private static void AddConditional(List<ParcelSurveyFieldDefinition> target,
            string key, int tab, string group, string label, string kind,
            string conditionKey, string conditionOperator, string conditionValue,
            string[] options = null)
        {
            Add(target, key, tab, group, label, kind, true, false, false, false,
                ParcelFieldStatus.Manual, options);
            ParcelSurveyFieldDefinition value = target[target.Count - 1];
            value.ConditionKey = conditionKey;
            value.ConditionOperator = conditionOperator;
            value.ConditionValue = conditionValue;
        }

        private static string[] O(params string[] values) { return values; }
    }
}
