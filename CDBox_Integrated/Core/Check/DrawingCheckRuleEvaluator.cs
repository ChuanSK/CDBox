using System.Collections.Generic;
using CDBox.Shared.Wastewater.Automation;

namespace TCPipeAutoDraw.Core.Check
{
    /// <summary>
    /// 基础组件兼容门面。实际污水检查规则位于 CDBox.Wastewater.dll。
    /// </summary>
    public static class DrawingCheckRuleEvaluator
    {
        public const string LayerParentMissingRule = "layer.parent.missing";
        public const string LayerParentInvalidRule = "layer.parent.invalid";
        public const string ObjectClassificationRule =
            "object.classification.invalid";
        public const string AttributeCompletenessRule =
            "attribute.core.missing";
        public const string PipeRelationRule = "pipe.relationship.invalid";
        public const string DataValidityRule = "attribute.value.invalid";
        public const string AnnotationBindingRule =
            "annotation.binding.invalid";
        public const string PipeInvertConsistencyRule =
            "pipe.invert.consistency";

        public static List<DrawingCheckIssue> Evaluate(string documentId,
            IEnumerable<DrawingCheckLayerSnapshot> layers,
            IEnumerable<DrawingCheckObjectSnapshot> objects,
            IEnumerable<DrawingCheckAnnotationSnapshot> annotations)
        {
            IWastewaterInspectionService service =
                WastewaterInspectionRegistry.Current;
            return service == null ? new List<DrawingCheckIssue>()
                : service.Evaluate(documentId, layers, objects, annotations);
        }

        public static List<DrawingCheckGroup> Group(string documentId,
            IEnumerable<DrawingCheckIssue> issues)
        {
            return Group(documentId, issues,
                DrawingCheckIssueStatus.Active);
        }

        public static List<DrawingCheckGroup> Group(string documentId,
            IEnumerable<DrawingCheckIssue> issues,
            DrawingCheckIssueStatus status)
        {
            IWastewaterInspectionService service =
                WastewaterInspectionRegistry.Current;
            return service == null ? new List<DrawingCheckGroup>()
                : service.Group(documentId, issues, status);
        }
    }
}
