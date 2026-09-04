using System.Collections.Generic;
using CDBox.Shared.Wastewater.Automation;
using TCPipeAutoDraw.Core.Check;

namespace CDBox.Wastewater.Features.Inspection
{
    /// <summary>
    /// 污水图纸检查规则、状态机及忽略记录的模块入口。
    /// </summary>
    public sealed class WastewaterInspectionService
        : IWastewaterInspectionService
    {
        public IDrawingCheckManager CreateManager()
        {
            return new DrawingCheckManager();
        }

        public List<DrawingCheckIssue> Evaluate(string documentId,
            IEnumerable<DrawingCheckLayerSnapshot> layers,
            IEnumerable<DrawingCheckObjectSnapshot> objects,
            IEnumerable<DrawingCheckAnnotationSnapshot> annotations)
        {
            return DrawingCheckRuleEvaluator.Evaluate(documentId, layers,
                objects, annotations);
        }

        public List<DrawingCheckGroup> Group(string documentId,
            IEnumerable<DrawingCheckIssue> issues,
            DrawingCheckIssueStatus status)
        {
            return DrawingCheckRuleEvaluator.Group(documentId, issues,
                status);
        }

        public string NormalizeKind(string value)
        {
            return DrawingCheckClassification.NormalizeKind(value);
        }

        public HashSet<string> LoadIgnoredKeys(string documentKey)
        {
            return DrawingCheckIgnoreStore.LoadKeys(documentKey);
        }

        public List<DrawingCheckIssue> LoadIgnoredIssues(string documentKey,
            string documentId)
        {
            return DrawingCheckIgnoreStore.LoadIssues(documentKey,
                documentId);
        }

        public void AddIgnoredIssues(string documentKey,
            IEnumerable<DrawingCheckIssue> issues)
        {
            DrawingCheckIgnoreStore.Add(documentKey, issues);
        }

        public void RemoveIgnoredGroup(string documentKey,
            IEnumerable<DrawingCheckIssue> issues)
        {
            DrawingCheckIgnoreStore.Remove(documentKey, issues);
        }

        public void RemoveIgnoredKeys(string documentKey,
            IEnumerable<string> ignoreKeys)
        {
            DrawingCheckIgnoreStore.RemoveKeys(documentKey, ignoreKeys);
        }
    }
}
