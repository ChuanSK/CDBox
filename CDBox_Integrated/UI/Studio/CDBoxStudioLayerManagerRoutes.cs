using System;
using System.Windows.Forms;
using TCPipeAutoDraw.Modules.LayerManager;
using TCPipeAutoDraw.UI;
using TCPipeAutoDraw.Core.Colors;
using TCPipeAutoDraw.UI.Controls;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioLayerManagerRoutes
    {
        public static bool TryRoute(CDBoxStudioRouteRequest request, bool standalone, out CDBoxStudioRouteResult result)
        {
            result = null;
            if (request == null || string.IsNullOrWhiteSpace(request.Name)) return false;

            string name = request.Name.Trim().ToLowerInvariant();
            switch (name)
            {
                case "getlayermanagerdata":
                case "refreshlayermanager":
                    result = LoadDataResult(name == "refreshlayermanager" ? "图层数据已刷新" : null);
                    return true;
                case "getlayerobjectcounts":
                    result = LoadObjectCountsResult();
                    return true;
                case "savelayermetadata":
                    result = SaveMetadataResult(request.Argument);
                    return true;
                case "runlayeraction":
                    result = RunActionResult(request.Argument);
                    return true;
                case "setcurrentlayer":
                    result = SetCurrentResult(request.Argument);
                    return true;
                case "openlayercolorpicker":
                    result = OpenLayerColorPickerResult(request.Argument);
                    return true;
                case "autorecognizelayerattributes":
                    result = AutoRecognizeResult(request.Argument);
                    return true;
                case "createdefaultpipelayers":
                    result = CreateDefaultLayersResult();
                    return true;
                case "openattributerecognition":
                    result = OpenRecognitionResult();
                    return true;
                case "openlegacylayermanager":
                    result = OpenLegacyResult();
                    return true;
                case "layermanagerpageerror":
                    CDBoxStudioLogger.Warn("图层管理器 WebView2 页面异常：" + (request.Argument ?? string.Empty));
                    result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "error", ToastMessage = "图层管理器页面异常，已写入 Studio 日志" };
                    return true;
                default:
                    return false;
            }
        }

        private static CDBoxStudioRouteResult LoadDataResult(string toast)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success", ToastMessage = toast };
            try
            {
                string json = CDBoxStudioLayerManagerApi.BuildEnvelopeJson(false);
                result.ExecuteScript = "window.CDBoxLayerManagerReceive && window.CDBoxLayerManagerReceive(" + json + ");";
                CDBoxStudioLogger.Info("已读取图层管理器 WebView2 主数据。");
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "图层数据读取失败：" + ex.Message;
                result.ExecuteScript = "window.CDBoxLayerManagerLoadFailed && window.CDBoxLayerManagerLoadFailed(" + ToJsString(ex.Message) + ");";
                CDBoxStudioLogger.Error("读取图层管理器 WebView2 数据失败。", ex);
            }
            return result;
        }

        private static CDBoxStudioRouteResult LoadObjectCountsResult()
        {
            var result = new CDBoxStudioRouteResult { Handled = true };
            try
            {
                string json = CDBoxStudioLayerManagerApi.BuildObjectCountsJson();
                result.ExecuteScript = "window.CDBoxLayerManagerObjectCounts && window.CDBoxLayerManagerObjectCounts(" + json + ");";
            }
            catch (Exception ex)
            {
                result.ExecuteScript = "window.CDBoxLayerManagerObjectCountFailed && window.CDBoxLayerManagerObjectCountFailed(" + ToJsString(ex.Message) + ");";
                CDBoxStudioLogger.Error("统计图层对象数量失败。", ex);
            }
            return result;
        }

        private static CDBoxStudioRouteResult SaveMetadataResult(string payload)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioLayerSaveResult saved = CDBoxStudioLayerManagerApi.SaveMetadata(payload);
                string savedJson = CDBoxStudioLayerManagerApi.Serialize(saved);
                string envelopeJson = CDBoxStudioLayerManagerApi.BuildEnvelopeJson(false);
                result.ExecuteScript = "window.CDBoxLayerManagerSaveResult && window.CDBoxLayerManagerSaveResult(" + savedJson + "," + envelopeJson + ");";
                result.ToastKind = saved.failCount > 0 ? "warning" : "success";
                result.ToastMessage = saved.failCount > 0
                    ? "图层属性部分保存成功：成功 " + saved.successCount + "，失败 " + saved.failCount
                    : "图层属性已保存：" + saved.successCount + " 个图层";
                CDBoxStudioLogger.Info("图层属性批量保存完成。成功 " + saved.successCount + "，失败 " + saved.failCount + "。");
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "图层属性保存失败：" + ex.Message;
                result.ExecuteScript = "window.CDBoxLayerManagerSaveFailed && window.CDBoxLayerManagerSaveFailed(" + ToJsString(ex.Message) + ");";
                CDBoxStudioLogger.Error("保存图层管理器元数据失败。", ex);
            }
            return result;
        }

        private static CDBoxStudioRouteResult RunActionResult(string payload)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioLayerActionResult action = CDBoxStudioLayerManagerApi.RunAction(payload);
                bool needsRefresh = action.action != "selectobjects" && action.action != "deleteobjects" && (action.failCount > 0 || action.skipCount > 0);
                if (needsRefresh)
                {
                    string envelope = CDBoxStudioLayerManagerApi.BuildEnvelopeJson(false);
                    result.ExecuteScript = "window.CDBoxLayerManagerRefreshResult && window.CDBoxLayerManagerRefreshResult(" + CDBoxStudioLayerManagerApi.Serialize(action) + "," + envelope + ");";
                }
                else
                {
                    result.ExecuteScript = "window.CDBoxLayerManagerActionResult && window.CDBoxLayerManagerActionResult(" + CDBoxStudioLayerManagerApi.Serialize(action) + ");";
                }
                result.ToastKind = action.failCount > 0 || action.skipCount > 0 ? "warning" : "success";
                result.ToastMessage = action.message;
                CDBoxStudioLogger.Info("执行图层操作 " + action.action + "：" + action.message);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "图层操作失败：" + ex.Message;
                result.ExecuteScript = "window.CDBoxLayerManagerActionFailed && window.CDBoxLayerManagerActionFailed(" + ToJsString(ex.Message) + ");";
                CDBoxStudioLogger.Error("执行图层批量操作失败。", ex);
            }
            return result;
        }

        private static CDBoxStudioRouteResult SetCurrentResult(string layerName)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioLayerActionResult action = CDBoxStudioLayerManagerApi.SetCurrentLayer(layerName);
                result.ExecuteScript = "window.CDBoxLayerManagerActionResult && window.CDBoxLayerManagerActionResult(" + CDBoxStudioLayerManagerApi.Serialize(action) + ");";
                result.ToastKind = action.successCount > 0 ? "success" : "warning";
                result.ToastMessage = action.message;
                CDBoxStudioLogger.Info(action.message);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "设置当前图层失败：" + ex.Message;
                CDBoxStudioLogger.Error("设置当前图层失败。", ex);
            }
            return result;
        }

        private static CDBoxStudioRouteResult OpenLayerColorPickerResult(string layerName)
        {
            var result = new CDBoxStudioRouteResult { Handled = true };
            string name = (layerName ?? string.Empty).Trim();
            try
            {
                CDBoxColor current = CDBoxStudioLayerManagerApi.GetLayerColor(name);
                CDBoxColor selected;
                if (!ColorPickerWindow.TryPick(current, out selected, false, false, true, true, true)) return result;
                CDBoxStudioLayerActionResult action = CDBoxStudioLayerManagerApi.SetLayerColor(name, selected);
                string envelope = CDBoxStudioLayerManagerApi.BuildEnvelopeJson(false);
                result.ExecuteScript = "window.CDBoxLayerManagerReceive && window.CDBoxLayerManagerReceive(" + envelope + ");";
                result.ToastKind = "success";
                result.ToastMessage = action.message;
                CDBoxStudioLogger.Info(action.message);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "图层颜色修改失败：" + ex.Message;
                CDBoxStudioLogger.Error("修改图层颜色失败。", ex);
            }
            return result;
        }

        private static CDBoxStudioRouteResult AutoRecognizeResult(string payload)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioLayerActionResult action = CDBoxStudioLayerManagerApi.AutoRecognize(payload);
                string envelope = CDBoxStudioLayerManagerApi.BuildEnvelopeJson(false);
                result.ExecuteScript = "window.CDBoxLayerManagerRefreshResult && window.CDBoxLayerManagerRefreshResult(" + CDBoxStudioLayerManagerApi.Serialize(action) + "," + envelope + ");";
                result.ToastKind = action.failCount > 0 ? "warning" : "success";
                result.ToastMessage = action.message;
                CDBoxStudioLogger.Info("自动识别图层属性：" + action.message);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "自动识别属性失败：" + ex.Message;
                CDBoxStudioLogger.Error("自动识别图层属性失败。", ex);
            }
            return result;
        }

        private static CDBoxStudioRouteResult CreateDefaultLayersResult()
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioLayerActionResult action = CDBoxStudioLayerManagerApi.CreateDefaultPipeLayers();
                string envelope = CDBoxStudioLayerManagerApi.BuildEnvelopeJson(false);
                result.ExecuteScript = "window.CDBoxLayerManagerRefreshResult && window.CDBoxLayerManagerRefreshResult(" + CDBoxStudioLayerManagerApi.Serialize(action) + "," + envelope + ");";
                result.ToastKind = action.failCount > 0 ? "warning" : "success";
                result.ToastMessage = action.message;
                CDBoxStudioLogger.Info("创建管线默认层：" + action.message);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "创建管线默认层失败：" + ex.Message;
                CDBoxStudioLogger.Error("创建管线默认层失败。", ex);
            }
            return result;
        }

        private static CDBoxStudioRouteResult OpenRecognitionResult()
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioRecognitionRulesWindow.ShowWindow(new AcadMainWindow());
                result.ToastMessage = "已打开新版属性识别表";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "属性识别表打开失败：" + ex.Message;
                CDBoxStudioLogger.Error("从图层管理器打开属性识别表失败。", ex);
            }
            return result;
        }

        private static CDBoxStudioRouteResult OpenLegacyResult()
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc == null) throw new InvalidOperationException("未找到当前图纸。");
                using (var form = new LayerManagerForm(doc))
                {
                    form.ShowDialog(new AcadMainWindow());
                }
                string envelope = CDBoxStudioLayerManagerApi.BuildEnvelopeJson(false);
                result.ExecuteScript = "window.CDBoxLayerManagerReceive && window.CDBoxLayerManagerReceive(" + envelope + ");";
                result.ToastMessage = "旧版图层管理器已关闭，页面已刷新";
                CDBoxStudioLogger.Info("从新版图层管理器打开旧 WinForms 图层管理器。");
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "旧版图层管理器打开失败：" + ex.Message;
                CDBoxStudioLogger.Error("打开旧版图层管理器失败。", ex);
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
