extern alias WastewaterBusiness;
using QuantityDashboardCache = WastewaterBusiness::TCPipeAutoDraw.Modules.QuantityCalculation.QuantityDashboardCache;
using QuantityDashboardContext = WastewaterBusiness::TCPipeAutoDraw.Modules.QuantityCalculation.QuantityDashboardContext;
using QuantityDashboardRequest = WastewaterBusiness::TCPipeAutoDraw.Modules.QuantityCalculation.QuantityDashboardRequest;
using QuantityDashboardSnapshot = WastewaterBusiness::TCPipeAutoDraw.Modules.QuantityCalculation.QuantityDashboardSnapshot;
using WastewaterQuantityDashboardService = WastewaterBusiness::TCPipeAutoDraw.Modules.QuantityCalculation.WastewaterQuantityDashboardService;
using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using CDBox.Shared.Modules;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace CDBox.Wastewater.UI
{
    public sealed class WastewaterQuantityDashboardController : IDisposable
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 128 };
        public const string PageId = "wastewater-quantity-dashboard";
        private readonly ICDBoxPageService _pages;
        private readonly ICDBoxPageAppearanceService _appearance;
        private readonly IQuantityDashboardMonitorService _monitor;
        private readonly ICDBoxLogger _logger;

        public WastewaterQuantityDashboardController(ICDBoxPageService pages,
            ICDBoxPageAppearanceService appearance,
            IQuantityDashboardMonitorService monitor, ICDBoxLogger logger)
        {
            _pages = pages ?? throw new ArgumentNullException("pages");
            _appearance = appearance ?? throw new ArgumentNullException("appearance");
            _monitor = monitor ?? throw new ArgumentNullException("monitor");
            _logger = logger ?? throw new ArgumentNullException("logger");
            _monitor.Configure(BroadcastScript);
        }

        public CDBoxPageDefinition CreatePage()
        {
            var page = new CDBoxPageDefinition(PageId, "工程量动态看板",
                delegate
                {
                    CDBoxPageAppearance value = _appearance.GetAppearance()
                        ?? new CDBoxPageAppearance();
                    return WastewaterQuantityDashboardPage
                        .BuildStandaloneDocument(value.Theme,
                            value.AnimationsEnabled, value.LogFilePath);
                }, Route);
            page.Width = 1220;
            page.Height = 800;
            page.MinimumWidth = 920;
            page.MinimumHeight = 640;
            return page;
        }

        public void Dispose() { _monitor.Configure(null); }

        private CDBoxPageRouteResult Route(CDBoxPageRouteRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return new CDBoxPageRouteResult { Handled = false };
            CDBoxPageRouteResult result;
            if (TryRoute(request, out result)) return result;
            return new CDBoxPageRouteResult { Handled = false };
        }

        public bool TryRoute(CDBoxPageRouteRequest request,
            out CDBoxPageRouteResult result)
        {
            result = null;
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return false;
            string name = request.Name.Trim().ToLowerInvariant();
            switch (name)
            {
                case "getquantitycontext": result = GetContext(request.Argument); return true;
                case "getquantitysnapshot":
                case "refreshquantitysnapshot": result = GetSnapshot(request.Argument, name == "refreshquantitysnapshot"); return true;
                case "setquantitylivemode": result = SetLive(request.Argument); return true;
                case "createquantityregion": result = RegionInteraction("create", request.Argument); return true;
                case "selectquantityregionboundary": result = RegionInteraction("bind", request.Argument); return true;
                case "renamequantityregion": result = RegionAction("rename", request.Argument); return true;
                case "deletequantityregion": result = RegionAction("delete", request.Argument); return true;
                case "locatequantityregion": result = RegionInteraction("locate", request.Argument); return true;
                case "modifyquantityregion": result = RegionInteraction("modify", request.Argument); return true;
                case "selectquantityobjects": result = ObjectInteraction(request.Argument, false, false); return true;
                case "zoomquantityobjects": result = ObjectInteraction(request.Argument, true, false); return true;
                case "openquantityobjecteditor": result = ObjectInteraction(request.Argument, false, true); return true;
                case "copyquantitysummary": result = CopySummary(request.Argument); return true;
                case "exportquantityreference": result = ExportReference(request.Argument); return true;
                case "exportquantitycalculationprocess": result = ExportCalculationProcess(request.Argument); return true;
                case "exportquantityreport":
                case "openlegacyquantitycommand": result = FormalReportInteraction(request.Argument); return true;
                case "savequantitysnapshot": result = SaveSnapshot(request.Argument); return true;
                case "quantitydashboardopened": result = DashboardOpened(); return true;
                case "quantitydashboardpageerror": result = PageError(request.Argument); return true;
                default: return false;
            }
        }

        private CDBoxPageRouteResult GetContext(string payload)
        {
            var result = NewResult();
            try
            {
                QuantityDashboardContext context = WastewaterQuantityDashboardApi.GetContext(payload);
                result.ExecuteScript = "window.CDBoxQuantityDashboardContext && window.CDBoxQuantityDashboardContext(" + Serializer.Serialize(context) + ");";
                _logger.Info("工程量动态看板上下文已读取。图纸数：" + context.documents.Count + "，区域数：" + context.regions.Count + "。");
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "读取工程量上下文失败：" + ex.Message;
                result.ExecuteScript = "window.CDBoxQuantityDashboardContextFailed && window.CDBoxQuantityDashboardContextFailed(" + ToJs(ex.Message) + ");";
                _logger.Error("读取工程量动态看板上下文失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult GetSnapshot(string payload, bool manual)
        {
            var result = NewResult();
            try
            {
                Progress(0, 6, manual ? "正在手动刷新工程量……" : "正在计算工程量……");
                QuantityDashboardSnapshot snapshot = WastewaterQuantityDashboardApi.GetSnapshot(payload, Progress);
                result.ExecuteScript = "window.CDBoxQuantityDashboardReceive && window.CDBoxQuantityDashboardReceive(" + Serializer.Serialize(snapshot) + ");";
                result.ToastKind = "success";
                result.ToastMessage = manual ? "工程量已刷新" : null;
                _logger.Info("工程量动态看板统计完成：图纸=" + snapshot.document.name + "，范围=" + snapshot.scope.regionName + "，对象=" + snapshot.details.Count + "，完整度=" + snapshot.status.dataCompleteness.ToString("0.00") + "%。");
            }
            catch (Exception ex)
            {
                QuantityDashboardRequest request = null;
                try { request = WastewaterQuantityDashboardApi.Deserialize<QuantityDashboardRequest>(payload); } catch { }
                QuantityDashboardSnapshot cached = request == null ? null : QuantityDashboardCache.Load(WastewaterQuantityDashboardService.GetCacheKey(request));
                result.ToastKind = "error";
                result.ToastMessage = "工程量统计失败：" + ex.Message;
                result.ExecuteScript = "window.CDBoxQuantityDashboardFailed && window.CDBoxQuantityDashboardFailed(" + ToJs(ex.Message) + "," + Serializer.Serialize(cached) + ");";
                _logger.Error("工程量动态看板统计失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult SetLive(string payload)
        {
            var result = NewResult();
            bool enabled = string.Equals((payload ?? string.Empty).Trim(), "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals((payload ?? string.Empty).Trim(), "true", StringComparison.OrdinalIgnoreCase);
            _monitor.SetLiveEnabled(enabled);
            result.ToastKind = "success";
            result.ToastMessage = enabled ? "已开启实时统计" : "已关闭实时统计";
            return result;
        }

        private CDBoxPageRouteResult RegionInteraction(string action,
            string payload)
        {
            return new CDBoxPageRouteResult
            {
                Handled = true,
                InteractionToRun = delegate
                {
                    return RegionAction(action, payload);
                }
            };
        }

        private CDBoxPageRouteResult ObjectInteraction(string payload,
            bool zoom, bool editor)
        {
            return new CDBoxPageRouteResult
            {
                Handled = true,
                InteractionToRun = delegate
                {
                    return ObjectAction(payload, zoom, editor);
                }
            };
        }

        private CDBoxPageRouteResult FormalReportInteraction(string payload)
        {
            return new CDBoxPageRouteResult
            {
                Handled = true,
                RestorePageAfterAction = false,
                ActionToRun = delegate { RunFormalReport(payload); }
            };
        }

        private CDBoxPageRouteResult RegionAction(string action, string payload)
        {
            var result = NewResult();
            try
            {
                QuantityDashboardContext context = null;
                switch (action)
                {
                    case "create": context = WastewaterQuantityDashboardApi.CreateRectangleRegion(payload); break;
                    case "bind": context = WastewaterQuantityDashboardApi.BindExistingRegion(payload); break;
                    case "rename": context = WastewaterQuantityDashboardApi.RenameRegion(payload); break;
                    case "delete": context = WastewaterQuantityDashboardApi.DeleteRegion(payload); break;
                    case "locate": WastewaterQuantityDashboardApi.LocateRegion(payload); break;
                    case "modify": WastewaterQuantityDashboardApi.ModifyRegionBoundary(payload); break;
                    default: throw new InvalidOperationException("不支持的统计区域操作。");
                }
                if (context != null) result.ExecuteScript = "window.CDBoxQuantityDashboardRegionResult && window.CDBoxQuantityDashboardRegionResult(" + Serializer.Serialize(context) + ");";
                result.ToastKind = "success";
                result.ToastMessage = action == "create" ? "统计区域已创建" : action == "bind" ? "已绑定闭合多段线" : action == "rename" ? "统计区域已重命名" : action == "delete" ? "统计区域已删除" : action == "locate" ? "已定位统计区域" : "已进入边界编辑";
                _monitor.MarkDirty("region:" + action);
                _logger.Info("工程量统计区域操作完成：" + action + "。");
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "统计区域操作失败：" + ex.Message;
                result.ExecuteScript = "window.CDBoxQuantityDashboardActionFailed && window.CDBoxQuantityDashboardActionFailed(" + ToJs(ex.Message) + ");";
                _logger.Error("工程量统计区域操作失败：" + action, ex);
            }
            return result;
        }

        private CDBoxPageRouteResult ObjectAction(string payload, bool zoom, bool editor)
        {
            var result = NewResult();
            try
            {
                int count = editor ? WastewaterQuantityDashboardApi.OpenObjectEditor(payload) : WastewaterQuantityDashboardApi.SelectObjects(payload, zoom);
                result.ToastKind = "success";
                result.ToastMessage = editor ? "已打开 " + count + " 个对象的属性编辑器" : zoom ? "已定位 " + count + " 个对象" : "已选择 " + count + " 个对象";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = ex.Message;
                _logger.Error("工程量看板 CAD 对象联动失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult CopySummary(string payload)
        {
            var result = NewResult();
            try
            {
                WastewaterQuantityDashboardApi.CopySummary(payload);
                result.ToastKind = "success";
                result.ToastMessage = "工程量参考摘要已复制";
                _logger.Info("已复制工程量参考摘要。");
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "复制摘要失败：" + ex.Message;
                _logger.Error("复制工程量参考摘要失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult ExportReference(string payload)
        {
            var result = NewResult();
            try
            {
                string path = WastewaterQuantityDashboardApi.ExportReference(payload);
                if (string.IsNullOrWhiteSpace(path)) return result;
                result.ToastKind = "success";
                result.ToastMessage = "参考工程量表已导出";
                result.ExecuteScript = "window.CDBoxQuantityDashboardExported && window.CDBoxQuantityDashboardExported(" + ToJs(path) + ");";
                _logger.Info("工程量参考表导出完成：" + path);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "导出参考表失败：" + ex.Message;
                _logger.Error("导出工程量参考表失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult ExportCalculationProcess(string payload)
        {
            var result = NewResult();
            try
            {
                string path = WastewaterQuantityDashboardApi.ExportCalculationProcess(payload);
                if (string.IsNullOrWhiteSpace(path)) return result;
                result.ToastKind = "success";
                result.ToastMessage = "工程量计算过程已导出";
                result.ExecuteScript = "window.CDBoxQuantityDashboardExported && window.CDBoxQuantityDashboardExported(" + ToJs(path) + ");";
                _logger.Info("工程量计算过程导出完成：" + path);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "导出计算过程失败：" + ex.Message;
                _logger.Error("导出工程量计算过程失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult SaveSnapshot(string payload)
        {
            var result = NewResult();
            try
            {
                string path = WastewaterQuantityDashboardApi.SaveSnapshot(payload);
                result.ToastKind = "success";
                result.ToastMessage = "工程量快照已保存";
                result.ExecuteScript = "window.CDBoxQuantityDashboardSnapshotSaved && window.CDBoxQuantityDashboardSnapshotSaved(" + ToJs(path) + ");";
                _logger.Info("工程量快照已保存：" + path);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "保存快照失败：" + ex.Message;
                _logger.Error("保存工程量快照失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult RunFormalReport(string payload)
        {
            var result = NewResult();
            try
            {
                WastewaterQuantityDashboardApi.RunFormalReport(payload);
                result.ToastKind = "info";
                result.ToastMessage = "已进入正式工程量表生成流程";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "启动正式工程量表失败：" + ex.Message;
                _logger.Error("启动正式工程量表流程失败。", ex);
            }
            return result;
        }

        private CDBoxPageRouteResult DashboardOpened()
        {
            _monitor.Configure(BroadcastScript);
            return NewResult();
        }

        private CDBoxPageRouteResult PageError(string message)
        {
            _logger.Warn("工程量动态看板 WebView2 页面异常：" + (message ?? string.Empty));
            return new CDBoxPageRouteResult { Handled = true, ToastKind = "error", ToastMessage = "工程量页面异常，已写入 Studio 日志" };
        }

        private static CDBoxPageRouteResult NewResult()
        {
            return new CDBoxPageRouteResult { Handled = true, ToastKind = "info" };
        }

        private void Progress(int current, int total, string message)
        {
            double percent = total <= 0 ? 0.0 : Math.Max(0.0, Math.Min(100.0, current * 100.0 / total));
            string json = Serializer.Serialize(new { current = current, total = total, percent = percent, message = message ?? string.Empty });
            BroadcastScript("window.CDBoxQuantityDashboardProgress && window.CDBoxQuantityDashboardProgress(" + json + ");");
        }

        private void BroadcastScript(string script)
        {
            if (string.IsNullOrWhiteSpace(script)) return;
            try { _pages.TryExecuteScript(PageId, script); }
            catch (Exception ex)
            {
                _logger.Error("向工程量看板页面宿主广播脚本失败。", ex);
            }
        }

        private static string ToJs(string value)
        {
            if (value == null) return "''";
            return "'" + value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n") + "'";
        }
    }
}
