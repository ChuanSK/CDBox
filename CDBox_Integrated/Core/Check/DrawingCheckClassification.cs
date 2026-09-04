using CDBox.Shared.Wastewater.Automation;

namespace TCPipeAutoDraw.Core.Check
{
    /// <summary>污水对象分类规则的基础兼容门面。</summary>
    public static class DrawingCheckClassification
    {
        public static string NormalizeKind(string value)
        {
            IWastewaterInspectionService service =
                WastewaterInspectionRegistry.Current;
            return service == null ? string.Empty
                : service.NormalizeKind(value);
        }
    }
}
