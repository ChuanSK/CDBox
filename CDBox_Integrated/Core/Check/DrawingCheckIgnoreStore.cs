using System.Collections.Generic;
using CDBox.Shared.Wastewater.Automation;

namespace TCPipeAutoDraw.Core.Check
{
    /// <summary>污水检查忽略记录的基础兼容门面。</summary>
    public static class DrawingCheckIgnoreStore
    {
        public static HashSet<string> LoadKeys(string documentKey)
        {
            IWastewaterInspectionService service =
                WastewaterInspectionRegistry.Current;
            return service == null
                ? new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
                : service.LoadIgnoredKeys(documentKey);
        }

        public static List<DrawingCheckIssue> LoadIssues(string documentKey,
            string documentId)
        {
            IWastewaterInspectionService service =
                WastewaterInspectionRegistry.Current;
            return service == null ? new List<DrawingCheckIssue>()
                : service.LoadIgnoredIssues(documentKey, documentId);
        }

        public static void Add(string documentKey,
            IEnumerable<DrawingCheckIssue> issues)
        {
            IWastewaterInspectionService service =
                WastewaterInspectionRegistry.Current;
            if (service != null) service.AddIgnoredIssues(documentKey, issues);
        }

        public static void Remove(string documentKey,
            IEnumerable<DrawingCheckIssue> issues)
        {
            IWastewaterInspectionService service =
                WastewaterInspectionRegistry.Current;
            if (service != null)
                service.RemoveIgnoredGroup(documentKey, issues);
        }

        public static void RemoveKeys(string documentKey,
            IEnumerable<string> ignoreKeys)
        {
            IWastewaterInspectionService service =
                WastewaterInspectionRegistry.Current;
            if (service != null)
                service.RemoveIgnoredKeys(documentKey, ignoreKeys);
        }
    }
}
