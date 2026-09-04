using System;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using TCPipeAutoDraw.Core.Colors;
using TCPipeAutoDraw.Modules.LayerManager;
using AcadColorDialog = Autodesk.AutoCAD.Windows.ColorDialog;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class LayerManagerPageRouter
    {
        private readonly ICDBoxLogger _logger;
        private readonly ICDBoxPageService _pages;

        public LayerManagerPageRouter(ICDBoxLogger logger, ICDBoxPageService pages)
        {
            _logger = logger ?? throw new ArgumentNullException("logger");
            _pages = pages ?? throw new ArgumentNullException("pages");
        }

        public CDBoxPageRouteResult Route(CDBoxPageRouteRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return new CDBoxPageRouteResult { Handled = false };

            string name = request.Name.Trim().ToLowerInvariant();
            switch (name)
            {
                case "ready":
                    return LoadDataResult(null);
                case "getlayermanagerdata":
                case "refreshlayermanager":
                    return LoadDataResult(name == "refreshlayermanager" ? "图层数据已刷新" : null);
                case "getlayerobjectcounts":
                    return LoadObjectCountsResult();
                case "savelayermetadata":
                    return SaveMetadataResult(request.Argument);
                case "runlayeraction":
                    return RunActionResult(request.Argument);
                case "setcurrentlayer":
                    return SetCurrentResult(request.Argument);
                case "openlayercolorpicker":
                    return OpenLayerColorPickerResult(request.Argument);
                case "autorecognizelayerattributes":
                    return AutoRecognizeResult(request.Argument);
                case "createdefaultpipelayers":
                    return CreateDefaultLayersResult(request.Argument);
                case "savelayerpreset":
                    return SaveLayerPresetResult(request.Argument);
                case "deletelayerpreset":
                    return DeleteLayerPresetResult(request.Argument);
                case "openattributerecognition":
                    return OpenRecognitionResult();
                case "openlegacylayermanager":
                    return OpenLegacyResult();
                case "layermanagerpageerror":
                    _logger.Warn("图层管理器页面异常：" + (request.Argument ?? string.Empty));
                    return new CDBoxPageRouteResult { Handled = true, ToastKind = "error", ToastMessage = "图层管理器页面异常，已写入日志" };
                default:
                    return new CDBoxPageRouteResult { Handled = false };
            }
        }

        private CDBoxPageRouteResult LoadDataResult(string toast)
        {
            var result = new CDBoxPageRouteResult { Handled = true, ToastKind = "success", ToastMessage = toast };
            try
            {
                string json = LayerManagerApi.BuildEnvelopeJson(false);
                result.ExecuteScript = "window.CDBoxLayerManagerReceive && window.CDBoxLayerManagerReceive(" + json + ");";
                _logger.Info("已读取图层管理器主数据。");
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "图层数据读取失败：" + ex.Message;
                result.ExecuteScript = "window.CDBoxLayerManagerLoadFailed && window.CDBoxLayerManagerLoadFailed(" + ToJsString(ex.Message) + ");";
                _logger.Error("读取图层管理器数据失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult LoadObjectCountsResult()
        {
            var result = new CDBoxPageRouteResult { Handled = true };
            try
            {
                string json = LayerManagerApi.BuildObjectCountsJson();
                result.ExecuteScript = "window.CDBoxLayerManagerObjectCounts && window.CDBoxLayerManagerObjectCounts(" + json + ");";
            }
            catch (Exception ex)
            {
                result.ExecuteScript = "window.CDBoxLayerManagerObjectCountFailed && window.CDBoxLayerManagerObjectCountFailed(" + ToJsString(ex.Message) + ");";
                _logger.Error("统计图层对象数量失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult SaveMetadataResult(string payload)
        {
            var result = new CDBoxPageRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioLayerSaveResult saved = LayerManagerApi.SaveMetadata(payload);
                string savedJson = LayerManagerApi.Serialize(saved);
                string envelopeJson = LayerManagerApi.BuildEnvelopeJson(false);
                result.ExecuteScript = "window.CDBoxLayerManagerSaveResult && window.CDBoxLayerManagerSaveResult(" + savedJson + "," + envelopeJson + ");";
                result.ToastKind = saved.failCount > 0 ? "warning" : "success";
                result.ToastMessage = saved.failCount > 0
                    ? "图层属性部分保存成功：成功 " + saved.successCount + "，失败 " + saved.failCount
                    : "图层属性已保存：" + saved.successCount + " 个图层";
                _logger.Info("图层属性批量保存完成。成功 " + saved.successCount + "，失败 " + saved.failCount + "。");
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "图层属性保存失败：" + ex.Message;
                result.ExecuteScript = "window.CDBoxLayerManagerSaveFailed && window.CDBoxLayerManagerSaveFailed(" + ToJsString(ex.Message) + ");";
                _logger.Error("保存图层管理器元数据失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult RunActionResult(string payload)
        {
            var result = new CDBoxPageRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioLayerActionResult action = LayerManagerApi.RunAction(payload);
                bool needsRefresh = action.action != "selectobjects" && action.action != "deleteobjects" && (action.failCount > 0 || action.skipCount > 0);
                if (needsRefresh)
                {
                    string envelope = LayerManagerApi.BuildEnvelopeJson(false);
                    result.ExecuteScript = "window.CDBoxLayerManagerRefreshResult && window.CDBoxLayerManagerRefreshResult(" + LayerManagerApi.Serialize(action) + "," + envelope + ");";
                }
                else
                {
                    result.ExecuteScript = "window.CDBoxLayerManagerActionResult && window.CDBoxLayerManagerActionResult(" + LayerManagerApi.Serialize(action) + ");";
                }
                result.ToastKind = action.failCount > 0 || action.skipCount > 0 ? "warning" : "success";
                result.ToastMessage = action.message;
                _logger.Info("执行图层操作 " + action.action + "：" + action.message);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "图层操作失败：" + ex.Message;
                result.ExecuteScript = "window.CDBoxLayerManagerActionFailed && window.CDBoxLayerManagerActionFailed(" + ToJsString(ex.Message) + ");";
                _logger.Error("执行图层批量操作失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult SetCurrentResult(string layerName)
        {
            var result = new CDBoxPageRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioLayerActionResult action = LayerManagerApi.SetCurrentLayer(layerName);
                result.ExecuteScript = "window.CDBoxLayerManagerActionResult && window.CDBoxLayerManagerActionResult(" + LayerManagerApi.Serialize(action) + ");";
                result.ToastKind = action.successCount > 0 ? "success" : "warning";
                result.ToastMessage = action.message;
                _logger.Info(action.message);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "设置当前图层失败：" + ex.Message;
                _logger.Error("设置当前图层失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult OpenLayerColorPickerResult(string layerName)
        {
            var result = new CDBoxPageRouteResult { Handled = true };
            string name = (layerName ?? string.Empty).Trim();
            try
            {
                CDBoxColor current = LayerManagerApi.GetLayerColor(name);
                var dialog = new AcadColorDialog { IncludeByBlockByLayer = false };
                dialog.Color = LayerColorService.ToCadColor(current);
                if (dialog.ShowModal() != true) return result;
                CDBoxColor selected = LayerColorService.FromCadColor(dialog.Color);
                CDBoxStudioLayerActionResult action = LayerManagerApi.SetLayerColor(name, selected);
                string envelope = LayerManagerApi.BuildEnvelopeJson(false);
                result.ExecuteScript = "window.CDBoxLayerManagerReceive && window.CDBoxLayerManagerReceive(" + envelope + ");";
                result.ToastKind = "success";
                result.ToastMessage = action.message;
                _logger.Info(action.message);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "图层颜色修改失败：" + ex.Message;
                _logger.Error("修改图层颜色失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult AutoRecognizeResult(string payload)
        {
            var result = new CDBoxPageRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioLayerActionResult action = LayerManagerApi.AutoRecognize(payload);
                string envelope = LayerManagerApi.BuildEnvelopeJson(false);
                result.ExecuteScript = "window.CDBoxLayerManagerRefreshResult && window.CDBoxLayerManagerRefreshResult(" + LayerManagerApi.Serialize(action) + "," + envelope + ");";
                result.ToastKind = action.failCount > 0 ? "warning" : "success";
                result.ToastMessage = action.message;
                _logger.Info("自动识别图层属性：" + action.message);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "自动识别属性失败：" + ex.Message;
                _logger.Error("自动识别图层属性失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult CreateDefaultLayersResult(string payload)
        {
            var result = new CDBoxPageRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioLayerActionResult action = LayerManagerApi.CreateDefaultPipeLayers(payload);
                string envelope = LayerManagerApi.BuildEnvelopeJson(false);
                result.ExecuteScript = "window.CDBoxLayerManagerRefreshResult && window.CDBoxLayerManagerRefreshResult(" + LayerManagerApi.Serialize(action) + "," + envelope + ");";
                result.ToastKind = action.failCount > 0 ? "warning" : "success";
                result.ToastMessage = action.message;
                _logger.Info("创建管线默认层：" + action.message);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "创建管线默认层失败：" + ex.Message;
                _logger.Error("创建管线默认层失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult SaveLayerPresetResult(string payload)
        {
            var result = new CDBoxPageRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioLayerActionResult action =
                    LayerManagerApi.SaveLayerPreset(payload);
                string envelope = LayerManagerApi.BuildEnvelopeJson(false);
                result.ExecuteScript = "window.CDBoxLayerManagerRefreshResult && window.CDBoxLayerManagerRefreshResult("
                    + LayerManagerApi.Serialize(action) + "," + envelope + ");";
                result.ToastMessage = action.message;
                _logger.Info(action.message);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "保存图层预设失败：" + ex.Message;
                _logger.Error("保存图层预设失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult DeleteLayerPresetResult(string payload)
        {
            var result = new CDBoxPageRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioLayerActionResult action =
                    LayerManagerApi.DeleteLayerPreset(payload);
                string envelope = LayerManagerApi.BuildEnvelopeJson(false);
                result.ExecuteScript = "window.CDBoxLayerManagerRefreshResult && window.CDBoxLayerManagerRefreshResult("
                    + LayerManagerApi.Serialize(action) + "," + envelope + ");";
                result.ToastKind = action.success ? "success" : "warning";
                result.ToastMessage = action.message;
                _logger.Info(action.message);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "删除图层预设失败：" + ex.Message;
                _logger.Error("删除图层预设失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult OpenRecognitionResult()
        {
            var result = new CDBoxPageRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                _pages.Show(LayerRecognitionRulesPage.Create(_logger));
                result.ToastMessage = "已打开新版属性识别表";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "属性识别表打开失败：" + ex.Message;
                _logger.Error("从图层管理器打开属性识别表失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult OpenLegacyResult()
        {
            return new CDBoxPageRouteResult
            {
                Handled = true,
                ToastKind = "info",
                ToastMessage = "图层管理器现已统一使用独立页面。"
            };
        }

        private static string ToJsString(string value)
        {
            if (value == null) return "''";
            return "'" + value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n") + "'";
        }
    }
}
