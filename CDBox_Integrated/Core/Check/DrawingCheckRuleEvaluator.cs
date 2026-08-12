using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.Core.Check
{
    public static class DrawingCheckRuleEvaluator
    {
        public const string LayerParentMissingRule = "layer.parent.missing";
        public const string LayerParentInvalidRule = "layer.parent.invalid";
        public const string ObjectClassificationRule = "object.classification.invalid";
        public const string AttributeCompletenessRule = "attribute.core.missing";
        public const string PipeRelationRule = "pipe.relationship.invalid";
        public const string DataValidityRule = "attribute.value.invalid";
        public const string AnnotationBindingRule = "annotation.binding.invalid";

        public static List<DrawingCheckIssue> Evaluate(string documentId,
            IEnumerable<DrawingCheckLayerSnapshot> layers,
            IEnumerable<DrawingCheckObjectSnapshot> objects,
            IEnumerable<DrawingCheckAnnotationSnapshot> annotations)
        {
            string id = string.IsNullOrWhiteSpace(documentId)
                ? "__global__" : documentId.Trim();
            var issues = new List<DrawingCheckIssue>();
            List<DrawingCheckObjectSnapshot> objectList = (objects ??
                Enumerable.Empty<DrawingCheckObjectSnapshot>())
                .Where(x => x != null).ToList();
            Dictionary<string, List<DrawingCheckObjectSnapshot>> nodes =
                objectList.Where(x => NormalizeKind(x.ObjectKind) == "井" &&
                        !string.IsNullOrWhiteSpace(x.NodeNo))
                    .GroupBy(x => x.NodeNo.Trim(),
                        StringComparer.CurrentCultureIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.ToList(),
                        StringComparer.CurrentCultureIgnoreCase);
            foreach (DrawingCheckLayerSnapshot layer in layers ??
                Enumerable.Empty<DrawingCheckLayerSnapshot>())
                EvaluateLayer(id, layer, issues);
            foreach (DrawingCheckObjectSnapshot item in objectList)
                EvaluateObject(id, item, nodes, issues);
            foreach (DrawingCheckAnnotationSnapshot annotation in annotations ??
                Enumerable.Empty<DrawingCheckAnnotationSnapshot>())
                EvaluateAnnotation(id, annotation, issues);
            return issues;
        }

        public static List<DrawingCheckGroup> Group(string documentId,
            IEnumerable<DrawingCheckIssue> issues)
        {
            return Group(documentId, issues, DrawingCheckIssueStatus.Active);
        }

        public static List<DrawingCheckGroup> Group(string documentId,
            IEnumerable<DrawingCheckIssue> issues,
            DrawingCheckIssueStatus status)
        {
            string id = string.IsNullOrWhiteSpace(documentId)
                ? "__global__" : documentId.Trim();
            return (issues ?? Enumerable.Empty<DrawingCheckIssue>())
                .Where(x => x != null && x.Status == status)
                .GroupBy(x => (x.RuleId ?? string.Empty) + "|" + x.Severity)
                .Select(group =>
                {
                    List<DrawingCheckIssue> values = group.ToList();
                    DrawingCheckIssue first = values[0];
                    List<string> handles = values.SelectMany(x =>
                            x.ObjectHandles ?? new List<string>())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    return new DrawingCheckGroup
                    {
                        Id = StableGroupId(id, first.RuleId, first.Severity,
                            status),
                        DocumentId = id,
                        RuleId = first.RuleId,
                        Category = first.Category,
                        Severity = first.Severity,
                        Title = first.Title,
                        Summary = BuildGroupSummary(first, values.Count,
                            handles.Count),
                        IssueCount = values.Count,
                        ObjectCount = handles.Count,
                        ObjectHandles = handles,
                        IssueIds = values.Select(x => x.Id).ToList(),
                        UpdatedAt = values.Max(x => x.UpdatedAt),
                        Status = status
                    };
                })
                .OrderByDescending(x => x.Severity)
                .ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static void EvaluateLayer(string documentId,
            DrawingCheckLayerSnapshot layer, IList<DrawingCheckIssue> issues)
        {
            if (layer == null || IsSystemLayer(layer.LayerName) ||
                layer.ObjectHandles == null || layer.ObjectHandles.Count == 0)
                return;
            if (string.IsNullOrWhiteSpace(layer.ParentGroup))
                issues.Add(NewIssue(documentId, LayerParentMissingRule, "图层",
                    DrawingCheckSeverity.Warning, "图层父属性缺失",
                    "图层“" + layer.LayerName + "”尚未设置父属性。",
                    layer.ObjectHandles));
            else if (NormalizeKind(layer.ParentGroup).Length == 0)
                issues.Add(NewIssue(documentId, LayerParentInvalidRule, "图层",
                    DrawingCheckSeverity.Error, "图层父属性无效",
                    "图层“" + layer.LayerName + "”的父属性“"
                    + layer.ParentGroup + "”不属于主管、支管或井。",
                    layer.ObjectHandles));
        }

        private static void EvaluateObject(string documentId,
            DrawingCheckObjectSnapshot item,
            IDictionary<string, List<DrawingCheckObjectSnapshot>> nodes,
            IList<DrawingCheckIssue> issues)
        {
            if (item == null || item.IsSpecialObject) return;
            string expected = NormalizeKind(item.LayerParentGroup);
            string actual = NormalizeKind(item.ObjectKind);
            if (expected.Length > 0 && actual.Length == 0)
            {
                issues.Add(NewIssue(documentId, ObjectClassificationRule,
                    "对象分类", DrawingCheckSeverity.Error,
                    "对象类型无法识别", "图层“" + item.LayerName
                    + "”已设为“" + expected + "”，但对象类型不受支持。",
                    One(item.Handle)));
                return;
            }
            if (expected.Length > 0 && actual.Length > 0 &&
                !string.Equals(expected, actual,
                    StringComparison.CurrentCultureIgnoreCase))
            {
                issues.Add(NewIssue(documentId, ObjectClassificationRule,
                    "对象分类", DrawingCheckSeverity.Error,
                    "对象与图层分类冲突", "对象属性为“" + actual
                    + "”，所在图层父属性为“" + expected + "”。",
                    One(item.Handle)));
            }
            if (actual.Length == 0) return;
            if (!item.HasSavedAttributes)
            {
                issues.Add(NewIssue(documentId, AttributeCompletenessRule,
                    "属性完整性", DrawingCheckSeverity.Warning,
                    "对象尚未写入属性", "可编辑对象尚未通过属性编辑器保存属性。",
                    One(item.Handle)));
                return;
            }

            var missing = new List<string>();
            if (actual == "主管")
            {
                Required(item.Diameter, "管径", missing);
                Required(item.Material, "材料", missing);
                if (item.AverageDepth <= 0) missing.Add("平均深度");
                Required(item.BackfillStructure, "结构层", missing);
                if (string.IsNullOrWhiteSpace(item.StartNode) ||
                    string.IsNullOrWhiteSpace(item.EndNode))
                    issues.Add(NewIssue(documentId, PipeRelationRule,
                        "管线关系", DrawingCheckSeverity.Error,
                        "主管端点关系缺失", "主管缺少起点井或终点井关系。",
                        One(item.Handle)));
                else
                {
                    EvaluateEndpoint(documentId, item, item.StartNode, true,
                        nodes, issues);
                    EvaluateEndpoint(documentId, item, item.EndNode, false,
                        nodes, issues);
                }
                if (!string.IsNullOrWhiteSpace(item.StartNode) &&
                    string.Equals(item.StartNode.Trim(),
                        (item.EndNode ?? string.Empty).Trim(),
                        StringComparison.CurrentCultureIgnoreCase))
                    issues.Add(NewIssue(documentId, PipeRelationRule,
                        "管线关系", DrawingCheckSeverity.Error,
                        "主管起终点相同", "主管的起点井和终点井不能相同。",
                        One(item.Handle)));
                if (item.PipeLayerBelowDiameter)
                    issues.Add(NewIssue(documentId, DataValidityRule,
                        "数据合法性", DrawingCheckSeverity.Error,
                        "管线层厚度小于管道外径",
                        "结构层中的管线层无法容纳当前管道外径。",
                        One(item.Handle)));
                if (item.PipeOuterDiameter <= 0 &&
                    !string.IsNullOrWhiteSpace(item.Diameter))
                    issues.Add(NewIssue(documentId, DataValidityRule,
                        "数据合法性", DrawingCheckSeverity.Error,
                        "管径无法换算", "管径未能换算为有效的管道外径。",
                        One(item.Handle)));
                else if (item.AverageDepth > 0 && item.PipeOuterDiameter > 0 &&
                    item.AverageDepth + 0.0000001 < item.PipeOuterDiameter)
                    issues.Add(NewIssue(documentId, DataValidityRule,
                        "数据合法性", DrawingCheckSeverity.Error,
                        "开挖深度小于管道外径",
                        "平均开挖深度无法容纳当前管道外径。",
                        One(item.Handle)));
            }
            else if (actual == "支管")
            {
                Required(item.BranchType, "支管类型", missing);
                Required(item.Diameter, "管径", missing);
                Required(item.Material, "材料", missing);
                if (item.BranchIncludeInCalculation && item.BranchDepth <= 0)
                    missing.Add("支管深度");
                if (item.PipeOuterDiameter <= 0 &&
                    !string.IsNullOrWhiteSpace(item.Diameter))
                    issues.Add(NewIssue(documentId, DataValidityRule,
                        "数据合法性", DrawingCheckSeverity.Error,
                        "管径无法换算", "管径未能换算为有效的管道外径。",
                        One(item.Handle)));
            }
            else if (actual == "井")
            {
                Required(item.NodeNo, "编号", missing);
                if (item.WellDepth <= 0)
                    issues.Add(NewIssue(documentId, DataValidityRule,
                        "数据合法性", DrawingCheckSeverity.Error,
                        "井深无效", "井深必须大于零。", One(item.Handle)));
                Required(item.WellSpec, "井规格", missing);
                Required(item.WellType, "井类型", missing);
                Required(item.WellCoverMaterial, "井盖", missing);
                Required(item.WellMaterialType, "材料", missing);
            }
            if (missing.Count > 0)
                issues.Add(NewIssue(documentId, AttributeCompletenessRule,
                    "属性完整性", DrawingCheckSeverity.Warning,
                    "核心属性不完整", actual + "缺少："
                    + string.Join("、", missing.ToArray()) + "。",
                    One(item.Handle)));
        }

        private static void EvaluateAnnotation(string documentId,
            DrawingCheckAnnotationSnapshot annotation,
            IList<DrawingCheckIssue> issues)
        {
            if (annotation == null) return;
            bool detached = string.Equals(annotation.BindingState, "Detached",
                StringComparison.OrdinalIgnoreCase);
            bool invalid = string.Equals(annotation.BindingState, "Invalid",
                StringComparison.OrdinalIgnoreCase) || (!detached &&
                (!annotation.SourceExists ||
                 string.IsNullOrWhiteSpace(annotation.SourceHandle)));
            if (!invalid) return;
            issues.Add(NewIssue(documentId, AnnotationBindingRule, "标注",
                DrawingCheckSeverity.Error, "标注关联失效",
                "标注关联的源对象已删除或绑定状态无效。",
                One(annotation.AnnotationHandle)));
        }

        private static void EvaluateEndpoint(string documentId,
            DrawingCheckObjectSnapshot pipe, string nodeNo, bool start,
            IDictionary<string, List<DrawingCheckObjectSnapshot>> nodes,
            IList<DrawingCheckIssue> issues)
        {
            List<DrawingCheckObjectSnapshot> candidates;
            if (nodes == null || !nodes.TryGetValue((nodeNo ?? string.Empty)
                .Trim(), out candidates) || candidates.Count == 0)
            {
                issues.Add(NewIssue(documentId, PipeRelationRule,
                    "管线关系", DrawingCheckSeverity.Error,
                    "关联井不存在", (start ? "起点井“" : "终点井“")
                    + nodeNo + "”未在当前图纸中找到。", One(pipe.Handle)));
                return;
            }
            bool hasPoint = start ? pipe.HasStartPoint : pipe.HasEndPoint;
            if (!hasPoint || !candidates.Any(x => x.HasNodePosition)) return;
            double x = start ? pipe.StartX : pipe.EndX;
            double y = start ? pipe.StartY : pipe.EndY;
            bool connected = candidates.Where(node => node.HasNodePosition)
                .Any(node =>
            {
                double dx = x - node.NodeX;
                double dy = y - node.NodeY;
                double tolerance = Math.Max(0.05,
                    node.NodeConnectionTolerance);
                return Math.Sqrt(dx * dx + dy * dy) <= tolerance;
            });
            if (!connected)
                issues.Add(NewIssue(documentId, PipeRelationRule,
                    "管线关系", DrawingCheckSeverity.Warning,
                    "管线端点与关联井未连接", (start ? "起点" : "终点")
                    + "与井“" + nodeNo + "”的几何位置不相接。",
                    One(pipe.Handle)));
        }

        private static DrawingCheckIssue NewIssue(string documentId,
            string ruleId, string category, DrawingCheckSeverity severity,
            string title, string message, IEnumerable<string> handles)
        {
            List<string> values = (handles ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            string identity = ruleId + "|" + string.Join(",", values.ToArray())
                + "|" + message;
            return new DrawingCheckIssue
            {
                Id = StableId(documentId + "|" + identity),
                DocumentId = documentId,
                RuleId = ruleId,
                Category = category,
                Severity = severity,
                Title = title,
                Message = message,
                Description = message,
                ObjectHandles = values,
                UpdatedAt = DateTime.UtcNow,
                IgnoreKey = StableId(identity)
            };
        }

        private static string BuildGroupSummary(DrawingCheckIssue first,
            int issueCount, int objectCount)
        {
            string count = objectCount > 0 ? objectCount + " 个对象"
                : issueCount + " 项";
            return count + " · " + (first.Message ?? string.Empty);
        }

        private static string NormalizeKind(string value)
        {
            string text = (value ?? string.Empty).Replace(" ", string.Empty)
                .Replace("　", string.Empty).Replace("/", string.Empty)
                .Replace("、", string.Empty);
            if (text.IndexOf("支", StringComparison.CurrentCultureIgnoreCase) >= 0)
                return "支管";
            if (text.IndexOf("井", StringComparison.CurrentCultureIgnoreCase) >= 0)
                return "井";
            if (text.IndexOf("主管", StringComparison.CurrentCultureIgnoreCase) >= 0)
                return "主管";
            return string.Empty;
        }

        private static bool IsSystemLayer(string value)
        {
            string name = (value ?? string.Empty).Trim();
            return name.Length == 0 || name == "0" ||
                string.Equals(name, "Defpoints", StringComparison.OrdinalIgnoreCase)
                || name.IndexOf('|') >= 0;
        }

        private static void Required(string value, string label,
            ICollection<string> missing)
        {
            if (string.IsNullOrWhiteSpace(value)) missing.Add(label);
        }

        private static IEnumerable<string> One(string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) yield return value;
        }

        private static string StableGroupId(string documentId, string ruleId,
            DrawingCheckSeverity severity, DrawingCheckIssueStatus status)
        {
            return StableId(documentId + "|" + ruleId + "|" + severity
                + "|" + status);
        }

        private static string StableId(string value)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                foreach (char c in value ?? string.Empty)
                {
                    hash ^= c;
                    hash *= 1099511628211UL;
                }
                return hash.ToString("x16");
            }
        }
    }
}
