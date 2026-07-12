using System;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioAnnotationSettingsRoutes
    {
        public static bool TryRoute(CDBoxStudioRouteRequest request, bool standalone, out CDBoxStudioRouteResult result)
        {
            result = null;
            if (request == null || string.IsNullOrWhiteSpace(request.Name)) return false;

            string name = request.Name.Trim().ToLowerInvariant();
            switch (name)
            {
                case "getannotationsettings":
                    result = LoadSettingsResult();
                    return true;

                case "saveannotationsettings":
                    result = SaveSettingsResult(request.Argument);
                    return true;

                case "resetannotationsettings":
                    result = ResetSettingsResult(request.Argument);
                    return true;

                case "openlegacyannotationsettings":
                    result = OpenLegacyResult();
                    return true;

                case "annotationsettingspageerror":
                case "pageerror":
                    CDBoxStudioLogger.Warn("标注设置 WebView2 页面异常：" + (request.Argument ?? string.Empty));
                    result = new CDBoxStudioRouteResult
                    {
                        Handled = true,
                        ToastKind = "error",
                        ToastMessage = "标注设置页面异常，已写入 Studio 日志"
                    };
                    return true;

                default:
                    return false;
            }
        }

        public static CDBoxStudioRouteResult BuildReloadResult(string toastMessage)
        {
            CDBoxStudioRouteResult result = LoadSettingsResult();
            result.ToastKind = "success";
            result.ToastMessage = toastMessage;
            return result;
        }

        private static CDBoxStudioRouteResult LoadSettingsResult()
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "info" };
            try
            {
                string json = CDBoxStudioAnnotationSettingsApi.BuildEnvelopeJson();
                result.ExecuteScript = "window.CDBoxAnnotationSettingsReceive && window.CDBoxAnnotationSettingsReceive(" + json + ");";
                CDBoxStudioLogger.Info("已读取标注设置 WebView2 页面数据。");
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "标注设置读取失败：" + ex.Message;
                result.ExecuteScript = "window.CDBoxAnnotationSettingsLoadFailed && window.CDBoxAnnotationSettingsLoadFailed(" + ToJsString(ex.Message) + ");";
                CDBoxStudioLogger.Error("读取标注设置 WebView2 页面数据失败。", ex);
            }
            return result;
        }

        private static CDBoxStudioRouteResult SaveSettingsResult(string payload)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioAnnotationSettingsApi.SavePayload(payload);
                result.ToastMessage = "标注设置已保存";
                result.ExecuteScript = "window.CDBoxAnnotationSettingsSaved && window.CDBoxAnnotationSettingsSaved();";
                CDBoxStudioLogger.Info("标注设置 WebView2 页面已保存表面积、管线长度和节点三类配置。");
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "标注设置保存失败：" + ex.Message;
                result.ExecuteScript = "window.CDBoxAnnotationSettingsSaveFailed && window.CDBoxAnnotationSettingsSaveFailed(" + ToJsString(ex.Message) + ");";
                CDBoxStudioLogger.Error("保存标注设置 WebView2 页面数据失败。", ex);
            }
            return result;
        }

        private static CDBoxStudioRouteResult ResetSettingsResult(string section)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "info" };
            try
            {
                string json = CDBoxStudioAnnotationSettingsApi.BuildDefaultSectionJson(section);
                result.ExecuteScript = "window.CDBoxAnnotationSettingsApplyDefault && window.CDBoxAnnotationSettingsApplyDefault(" + json + ");";
                result.ToastMessage = "已恢复当前页面默认值，保存后生效";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "恢复默认失败：" + ex.Message;
                CDBoxStudioLogger.Error("恢复标注设置默认值失败。", ex);
            }
            return result;
        }

        private static CDBoxStudioRouteResult OpenLegacyResult()
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioAnnotationSettingsApi.OpenLegacyWindow();
                string json = CDBoxStudioAnnotationSettingsApi.BuildEnvelopeJson();
                result.ExecuteScript = "window.CDBoxAnnotationSettingsReceive && window.CDBoxAnnotationSettingsReceive(" + json + ");";
                result.ToastMessage = "旧版标注设置已关闭，Web 页面已重新读取设置";
                CDBoxStudioLogger.Info("已从标注设置 WebView2 页面打开旧版 WinForms 标注设置。");
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "旧版标注设置打开失败：" + ex.Message;
                CDBoxStudioLogger.Error("打开旧版 WinForms 标注设置失败。", ex);
            }
            return result;
        }

        private static string ToJsString(string value)
        {
            if (value == null) return "''";
            return "'" + value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n") + "'";
        }
    }
}
