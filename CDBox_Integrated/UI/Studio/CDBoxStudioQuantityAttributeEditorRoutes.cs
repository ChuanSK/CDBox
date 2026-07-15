using System;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioQuantityAttributeEditorRoutes
    {
        public static bool TryRoute(CDBoxStudioRouteRequest request, out CDBoxStudioRouteResult result)
        {
            result = null;
            if (request == null || string.IsNullOrWhiteSpace(request.Name)) return false;
            string name = request.Name.Trim().ToLowerInvariant();
            try
            {
                CDBoxStudioQuantityAttributeEditorContext context = null;
                if (name == "getquantityattributecontext") context = CDBoxStudioQuantityAttributeEditorApi.GetContext(request.Argument);
                else if (name == "selectquantityattributeobject") context = CDBoxStudioQuantityAttributeEditorApi.Select(request.Argument);
                else if (name == "savequantityattributes") context = CDBoxStudioQuantityAttributeEditorApi.Save(request.Argument);
                else if (name == "calculatequantitydraft") context = CDBoxStudioQuantityAttributeEditorApi.CalculateDraft(request.Argument);
                else if (name == "refreshquantityattributes") context = CDBoxStudioQuantityAttributeEditorApi.Refresh(request.Argument);
                else if (name == "reloadquantityattributedefault") context = CDBoxStudioQuantityAttributeEditorApi.ReloadDefault(request.Argument);
                else if (name == "selectquantityattributenode") context = CDBoxStudioQuantityAttributeEditorApi.SelectNode(request.Argument);
                else if (name == "openlegacyquantityattributeeditor")
                {
                    CDBoxStudioQuantityAttributeEditorApi.OpenLegacy(request.Argument);
                    result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "info", ToastMessage = "已关闭旧版属性编辑器" };
                    return true;
                }
                else if (name == "quantityattributeeditorpageerror")
                {
                    CDBoxStudioLogger.Warn("属性编辑器页面异常：" + request.Argument);
                    result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "error", ToastMessage = "属性编辑器页面异常，已写入日志" };
                    return true;
                }
                else return false;

                bool draftCalculation = name == "calculatequantitydraft";
                result = new CDBoxStudioRouteResult { Handled = true, ToastKind = name == "savequantityattributes" ? "success" : "info", ToastMessage = draftCalculation ? null : context.message,
                    ExecuteScript = "window.CDBoxQuantityAttributeEditorLoad && window.CDBoxQuantityAttributeEditorLoad(" + CDBoxStudioQuantityAttributeEditorApi.Serialize(context) + ");" };
                return true;
            }
            catch (Exception ex)
            {
                result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "error", ToastMessage = ex.Message,
                    ExecuteScript = "window.CDBoxQuantityAttributeEditorFailed && window.CDBoxQuantityAttributeEditorFailed(" + ToJs(ex.Message) + ");" };
                CDBoxStudioLogger.Error("属性编辑器路由失败：" + request.Name, ex);
                return true;
            }
        }

        private static string ToJs(string value) { return "'" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n") + "'"; }
    }
}
