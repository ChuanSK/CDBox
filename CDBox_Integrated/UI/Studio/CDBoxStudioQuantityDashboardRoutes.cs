using System;
using TCPipeAutoDraw.Core.Modules;

namespace TCPipeAutoDraw.UI.Studio
{
    /// <summary>
    /// 旧综合 Studio 内嵌看板的兼容路由。业务页面已迁入污水模块，
    /// 旧入口统一打开模块独立页。
    /// </summary>
    internal static class CDBoxStudioQuantityDashboardRoutes
    {
        public static void Configure(Action<string> scriptSink) { }

        public static void Unconfigure(Action<string> scriptSink) { }

        public static bool TryRoute(CDBoxStudioRouteRequest request,
            out CDBoxStudioRouteResult result)
        {
            result = null;
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return false;
            string name = request.Name.Trim().ToLowerInvariant();
            switch (name)
            {
                case "getquantitycontext":
                case "getquantitysnapshot":
                case "refreshquantitysnapshot":
                case "setquantitylivemode":
                case "createquantityregion":
                case "selectquantityregionboundary":
                case "renamequantityregion":
                case "deletequantityregion":
                case "locatequantityregion":
                case "modifyquantityregion":
                case "selectquantityobjects":
                case "zoomquantityobjects":
                case "openquantityobjecteditor":
                case "copyquantitysummary":
                case "exportquantityreference":
                case "exportquantitycalculationprocess":
                case "exportquantityreport":
                case "openlegacyquantitycommand":
                case "savequantitysnapshot":
                case "quantitydashboardopened":
                case "quantitydashboardpageerror":
                    break;
                default:
                    return false;
            }
            WastewaterModuleHost.OpenQuantityDashboard();
            result = new CDBoxStudioRouteResult
            {
                Handled = true,
                ToastKind = "info",
                ToastMessage = "工程量看板已由污水模块打开"
            };
            return true;
        }
    }
}
