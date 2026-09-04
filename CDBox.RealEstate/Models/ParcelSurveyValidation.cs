using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CDBox.RealEstate.Models
{
    public sealed class ParcelSurveyValidationIssue
    {
        public string Code { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
        public string FieldKey { get; set; }
    }

    public sealed class ParcelSurveyValidationResult
    {
        public int CompletenessPercent { get; set; }
        public int RequiredMissingCount { get; set; }
        public int PendingConfirmationCount { get; set; }
        public int PaginationAnomalyCount { get; set; }
        public int BoundarySegmentPageCount { get; set; }
        public int SignatureGroupPageCount { get; set; }
        public bool PassesDataChecks { get; set; }
        public bool CanExport { get; set; }
        public List<ParcelSurveyValidationIssue> Issues { get; set; }

        public ParcelSurveyValidationResult()
        {
            Issues = new List<ParcelSurveyValidationIssue>();
        }
    }

    public static class ParcelSurveyValidator
    {
        public static ParcelSurveyValidationResult Validate(ParcelSurveyRecord record)
        {
            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            var result = new ParcelSurveyValidationResult();
            int required = 0;
            int completed = 0;

            foreach (ParcelSurveyFieldDefinition definition in ParcelSurveyFieldCatalog.Fields)
            {
                if (!definition.Required || !IsActive(record.Fields, definition)) continue;
                required++;
                ParcelSurveyFieldValue value = record.Field(definition.Key);
                bool valid = value != null && value.HasValue(definition.Kind)
                    && (value.Status != ParcelFieldStatus.NotApplicable
                        || definition.AllowNotApplicable);
                if (valid) completed++;
                else
                {
                    result.RequiredMissingCount++;
                    Add(result, "required", "error",
                        definition.Label + "尚未填写，也未明确标记为不适用。",
                        definition.Key);
                }
            }

            foreach (ParcelBuildingRecord building in record.Buildings)
            {
                foreach (ParcelSurveyFieldDefinition definition in
                    ParcelSurveyFieldCatalog.BuildingFields)
                {
                    if (!definition.Required) continue;
                    required++;
                    ParcelSurveyFieldValue value;
                    building.Fields.TryGetValue(definition.Key, out value);
                    bool valid = value != null && value.HasValue(definition.Kind)
                        && (value.Status != ParcelFieldStatus.NotApplicable
                            || definition.AllowNotApplicable);
                    if (valid) completed++;
                    else
                    {
                        result.RequiredMissingCount++;
                        Add(result, "building-required", "error",
                            BuildingName(building) + "的“" + definition.Label
                            + "”尚未填写。", definition.Key);
                    }
                }
            }

            result.CompletenessPercent = required == 0 ? 100
                : (int)Math.Round(completed * 100m / required,
                    MidpointRounding.AwayFromZero);
            ValidateBoundary(record, result);
            ValidateCodes(record, result);
            ValidateAreas(record, result);
            ValidateTemplateResidue(record, result);
            ValidateLayout(record, result);

            result.BoundarySegmentPageCount = Math.Max(1,
                (int)Math.Ceiling(record.Boundary.Segments.Count / 26m));
            result.SignatureGroupPageCount = Math.Max(1,
                (int)Math.Ceiling(record.Boundary.SignatureGroups.Count / 13m));
            result.PassesDataChecks = result.RequiredMissingCount == 0
                && result.PendingConfirmationCount == 0
                && result.PaginationAnomalyCount == 0
                && !result.Issues.Any(x => string.Equals(x.Severity, "error",
                    StringComparison.OrdinalIgnoreCase));
            // 数据检查只用于提示和质量控制，不再阻止导出。用户可以在任意
            // 完整度下导出模板，尚未填写的业务字段由导出器保持为空。
            result.CanExport = true;
            return result;
        }

        public static bool IsActive(
            IDictionary<string, ParcelSurveyFieldValue> fields,
            ParcelSurveyFieldDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(
                definition.ConditionKey)) return true;
            ParcelSurveyFieldValue condition;
            if (fields == null || !fields.TryGetValue(
                definition.ConditionKey, out condition) || condition == null)
                return false;
            string op = definition.ConditionOperator ?? string.Empty;
            if (string.Equals(op, "true", StringComparison.OrdinalIgnoreCase))
                return condition.BooleanValue;
            if (string.Equals(op, "false", StringComparison.OrdinalIgnoreCase))
                return !condition.BooleanValue;
            string actual = condition.TextValue ?? string.Empty;
            if (string.Equals(op, "neq", StringComparison.OrdinalIgnoreCase))
                return !string.IsNullOrWhiteSpace(actual)
                    && !string.Equals(actual, definition.ConditionValue,
                        StringComparison.OrdinalIgnoreCase);
            return string.Equals(actual, definition.ConditionValue,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateBoundary(ParcelSurveyRecord record,
            ParcelSurveyValidationResult result)
        {
            if (!record.Boundary.ParcelBoundaryClosed)
                Add(result, "boundary-open", "error", "宗地权属线尚未确认闭合。", string.Empty);
            if (record.Boundary.Points.Count < 3)
                Add(result, "boundary-points", "error", "界址点少于 3 个，无法形成有效宗地。", string.Empty);
            if (record.Boundary.Segments.Count < 3)
                Add(result, "boundary-segments", "error", "界址段少于 3 段，无法形成有效宗地。", string.Empty);

            var pointNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ParcelBoundaryPointRecord point in record.Boundary.Points)
            {
                if (string.IsNullOrWhiteSpace(point.PointNumber)
                    || !pointNumbers.Add(point.PointNumber))
                    Add(result, "point-number", "error", "界址点号为空或重复。", string.Empty);
                if (!point.X.HasValue || !point.Y.HasValue)
                    Add(result, "point-coordinate", "error",
                        (point.PointNumber ?? "界址点") + "缺少 CAD 坐标。", string.Empty);
                if (string.IsNullOrWhiteSpace(point.MarkerType))
                    Add(result, "point-marker", "error",
                        (point.PointNumber ?? "界址点") + "尚未选择界标种类。", string.Empty);
            }

            for (int i = 0; i < record.Boundary.Segments.Count; i++)
            {
                ParcelBoundarySegmentRecord segment = record.Boundary.Segments[i];
                string name = (segment.StartPointNumber ?? "?") + "-"
                    + (segment.EndPointNumber ?? "?");
                if (!pointNumbers.Contains(segment.StartPointNumber ?? string.Empty)
                    || !pointNumbers.Contains(segment.EndPointNumber ?? string.Empty))
                    Add(result, "segment-point", "error", name + "引用了不存在的界址点。", string.Empty);
                foreach (string middle in SplitPointNumbers(
                    segment.MiddlePointNumbers, record.Boundary.Points))
                    if (!pointNumbers.Contains(middle))
                        Add(result, "segment-middle-point", "error",
                            name + "引用了不存在的中间界址点“" + middle
                            + "”。", string.Empty);
                if (!segment.Distance.HasValue || segment.Distance.Value <= 0)
                    Add(result, "segment-distance", "error", name + "缺少有效界址距离。", string.Empty);
                if (string.IsNullOrWhiteSpace(segment.LineCategory))
                    Add(result, "segment-category", "error", name + "尚未确认界址线类别。", string.Empty);
                if (string.IsNullOrWhiteSpace(segment.LinePosition)
                    || string.Equals(segment.LinePosition, "待确认",
                        StringComparison.OrdinalIgnoreCase))
                    Add(result, "segment-position", "error", name + "尚未确认界址线位置。", string.Empty);
                if (string.IsNullOrWhiteSpace(segment.NeighborParcelCode)
                    && string.IsNullOrWhiteSpace(segment.NeighborOwner))
                    Add(result, "segment-neighbor", "error", name + "的相邻宗地尚未识别或处理。", string.Empty);
                if (i + 1 < record.Boundary.Segments.Count)
                {
                    ParcelBoundarySegmentRecord next = record.Boundary.Segments[i + 1];
                    if (!Same(segment.EndPointNumber, next.StartPointNumber))
                        Add(result, "segment-chain", "error", name + "与下一界址段未首尾相接。", string.Empty);
                }
            }
            if (record.Boundary.Segments.Count > 0)
            {
                ParcelBoundarySegmentRecord first = record.Boundary.Segments[0];
                ParcelBoundarySegmentRecord last = record.Boundary.Segments[
                    record.Boundary.Segments.Count - 1];
                if (!Same(last.EndPointNumber, first.StartPointNumber))
                    Add(result, "segment-close", "error", "界址段末段与首段未闭合。", string.Empty);
            }
        }

        private static void ValidateCodes(ParcelSurveyRecord record,
            ParcelSurveyValidationResult result)
        {
            CheckCode(record, result, ParcelSurveyFieldKeys.ParcelCode, "宗地代码", 6);
            CheckCode(record, result, ParcelSurveyFieldKeys.RealEstateUnitNumber,
                "不动产单元号", 8);
        }

        private static void CheckCode(ParcelSurveyRecord record,
            ParcelSurveyValidationResult result, string key, string label, int minimumLength)
        {
            ParcelSurveyFieldValue field = record.Field(key);
            string value = field == null ? string.Empty : field.TextValue;
            if (!string.IsNullOrWhiteSpace(value) && value != "/"
                && value.Trim().Length < minimumLength)
                Add(result, "code-format", "error", label + "格式无效。", key);
        }

        private static void ValidateAreas(ParcelSurveyRecord record,
            ParcelSurveyValidationResult result)
        {
            decimal? parcelArea = Number(record, ParcelSurveyFieldKeys.ParcelArea);
            if (!parcelArea.HasValue || parcelArea.Value <= 0)
                Add(result, "parcel-area", "error", "宗地面积必须为大于 0 的数值。",
                    ParcelSurveyFieldKeys.ParcelArea);
            decimal footprint = Sum(record.Buildings, "building.footprintArea");
            decimal area = Sum(record.Buildings, "building.area");
            decimal? footprintTotal = Number(record,
                ParcelSurveyFieldKeys.BuildingFootprintTotal);
            decimal? areaTotal = Number(record, ParcelSurveyFieldKeys.BuildingAreaTotal);
            if (record.Buildings.Count > 0 && (!footprintTotal.HasValue
                || Math.Abs(footprintTotal.Value - footprint) > 0.01m))
                Add(result, "footprint-total", "error",
                    "建筑占地总面积与各幢占地面积汇总不一致。",
                    ParcelSurveyFieldKeys.BuildingFootprintTotal);
            if (record.Buildings.Count > 0 && (!areaTotal.HasValue
                || Math.Abs(areaTotal.Value - area) > 0.01m))
                Add(result, "building-area-total", "error",
                    "建筑总面积与各幢建筑面积汇总不一致。",
                    ParcelSurveyFieldKeys.BuildingAreaTotal);
        }

        private static void ValidateTemplateResidue(ParcelSurveyRecord record,
            ParcelSurveyValidationResult result)
        {
            foreach (KeyValuePair<string, ParcelSurveyFieldValue> pair in record.Fields)
            {
                string value = pair.Value == null ? string.Empty : pair.Value.TextValue;
                if (LooksLikeTemplateResidue(value))
                    Add(result, "template-residue", "error",
                        "检测到遗留的示例或模板占位内容。", pair.Key);
            }
            foreach (ParcelBoundaryPointRecord point in record.Boundary.Points)
                if (LooksLikeTemplateResidue(point.PointNumber))
                    Add(result, "sample-point", "error", "检测到示例界址点号。", string.Empty);
        }

        private static void ValidateLayout(ParcelSurveyRecord record,
            ParcelSurveyValidationResult result)
        {
            result.PaginationAnomalyCount = Math.Max(0,
                record.LayoutDiagnostics.AbnormalPaginationCount);
            if (record.LayoutDiagnostics.TextOverflowCount > 0)
                Add(result, "text-overflow", "error", "导出预检发现文本溢出。", string.Empty);
            if (record.LayoutDiagnostics.FooterOrphanRisk)
            {
                result.PaginationAnomalyCount++;
                Add(result, "footer-orphan", "error",
                    "界址说明表存在将填表人单独挤到下一页的风险。", string.Empty);
            }
        }

        private static decimal? Number(ParcelSurveyRecord record, string key)
        {
            ParcelSurveyFieldValue value = record.Field(key);
            return value == null ? null : value.NumericValue;
        }

        private static decimal Sum(IEnumerable<ParcelBuildingRecord> buildings,
            string key)
        {
            decimal total = 0;
            foreach (ParcelBuildingRecord building in buildings)
            {
                ParcelSurveyFieldValue value;
                if (building.Fields.TryGetValue(key, out value)
                    && value != null && value.NumericValue.HasValue)
                    total += value.NumericValue.Value;
            }
            return total;
        }

        private static string BuildingName(ParcelBuildingRecord building)
        {
            ParcelSurveyFieldValue value;
            return building.Fields.TryGetValue("building.number", out value)
                && value != null && !string.IsNullOrWhiteSpace(value.TextValue)
                ? value.TextValue + "幢" : "未编号房屋";
        }

        private static bool LooksLikeTemplateResidue(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string lower = value.Trim().ToLowerInvariant();
            return lower.Contains("示例") || lower.Contains("样例")
                || lower.Contains("placeholder") || lower.Contains("xxxx")
                || lower.Contains("待替换");
        }

        private static bool Same(string left, string right)
        {
            return string.Equals((left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> SplitPointNumbers(string value,
            IEnumerable<ParcelBoundaryPointRecord> points)
        {
            return ParcelBoundaryPointNumberFormatter.ExpandMiddle(value,
                (points ?? Enumerable.Empty<ParcelBoundaryPointRecord>())
                    .Where(x => x != null).Select(x => x.PointNumber));
        }

        private static void Add(ParcelSurveyValidationResult result,
            string code, string severity, string message, string fieldKey)
        {
            result.Issues.Add(new ParcelSurveyValidationIssue
            {
                Code = code,
                Severity = severity,
                Message = message,
                FieldKey = fieldKey ?? string.Empty
            });
        }
    }
}
