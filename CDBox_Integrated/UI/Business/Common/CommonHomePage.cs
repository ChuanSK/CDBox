using System;
using System.Net;
using CDBox.Shared.UI;
using TCPipeAutoDraw.UI.Studio;

namespace CDBox.Common.UI
{
    internal static class CommonHomePage
    {
        public static CDBoxPageDefinition Create(string version)
        {
            return CreateWithTools(version, null, null, null);
        }

        public static CDBoxPageDefinition CreateWithTools(string version, Action shortCode, Action frame, Action excel)
        {
            return new CDBoxPageDefinition("common-home", "CDBox 公共业务",
                delegate { return BuildDocument(version); },
                delegate(CDBoxPageRouteRequest request)
                {
                    if (request == null) return new CDBoxPageRouteResult();
                    if (string.Equals(request.Name, "ready", StringComparison.OrdinalIgnoreCase))
                        return new CDBoxPageRouteResult { Handled = true };
                    if (!string.Equals(request.Name, "openCommonTool", StringComparison.OrdinalIgnoreCase))
                        return new CDBoxPageRouteResult();
                    Action action = request.Argument == "short-code" ? shortCode
                        : request.Argument == "frame" ? frame : request.Argument == "excel" ? excel : null;
                    return new CDBoxPageRouteResult { Handled = action != null, ActionToRun = action, RestorePageAfterAction = false };
                }) { Width = 860, Height = 610, MinimumWidth = 620, MinimumHeight = 460 };
        }

        internal static string BuildDocument(string version, CDBoxStudioSettings appearance = null)
        {
            return CommonWorkbench.Apply(@"<!doctype html><html lang='zh-CN'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>公共业务</title><style>
main{max-width:1000px;margin:auto;padding:32px}header{display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid var(--line);padding-bottom:22px}header small{color:var(--muted)}.intro{margin:22px 0}.tool{width:100%;display:grid;grid-template-columns:40px minmax(0,1fr) auto;gap:18px;align-items:center;text-align:left;border:0;border-bottom:1px solid var(--line);border-radius:0;padding:22px 8px}.tool .number{font-size:12px;color:var(--muted);font-variant-numeric:tabular-nums}.tool strong{display:block;font-size:15px}.tool p{font-size:12px;margin:5px 0 0}.tool .arrow{color:var(--muted);font-size:20px}@media(max-width:640px){main{padding:22px}.tool{gap:12px;grid-template-columns:24px minmax(0,1fr) auto}}
</style></head><body><main><header><h1>公共业务</h1><small>v" + WebUtility.HtmlEncode(version ?? "") + @"</small></header><p class='intro'>坐标识别、图框布置与表格转换。</p>
<button class='tool' data-tool='short-code'><span class='number'>01</span><span><strong>简码识别设置</strong><p>设置识别符号、点位连接和闭合方式。</p></span><span class='arrow' aria-hidden='true'>›</span></button>
<button class='tool' data-tool='frame'><span class='number'>02</span><span><strong>图框设置</strong><p>管理图框模板，调整裁图留白、指北针与布框参数。</p></span><span class='arrow' aria-hidden='true'>›</span></button>
<button class='tool' data-tool='excel'><span class='number'>03</span><span><strong>Excel 转 CAD 表格</strong><p>选择工作簿与单元格范围，设置图层和输出形式。</p></span><span class='arrow' aria-hidden='true'>›</span></button>
</main><script>document.querySelectorAll('[data-tool]').forEach(function(button){button.onclick=function(){try{chrome.webview.postMessage('studio|openCommonTool|'+encodeURIComponent(button.dataset.tool));}catch(e){}}});</script></body></html>", appearance);
        }
    }
}
