using System;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 基础程序集仅保留图纸身份解析。工程量统计实现位于
    /// CDBox.Wastewater.dll。
    /// </summary>
    public static class QuantityDashboardService
    {
        public static Document ResolveDocument(string documentId)
        {
            string id = (documentId ?? string.Empty).Trim();
            Document active = AcadApp.DocumentManager.MdiActiveDocument;
            if (id.Length == 0) return active;
            foreach (Document document in AcadApp.DocumentManager)
                if (document != null && string.Equals(GetDocumentId(document),
                    id, StringComparison.OrdinalIgnoreCase)) return document;
            return null;
        }

        public static string GetDocumentId(Document document)
        {
            return document == null ? string.Empty
                : (document.Name ?? string.Empty).Trim();
        }
    }
}
