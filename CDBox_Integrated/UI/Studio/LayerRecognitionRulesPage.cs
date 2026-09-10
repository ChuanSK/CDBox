using System;
using System.Text;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using TCPipeAutoDraw.Modules.LayerManager;

namespace TCPipeAutoDraw.UI.Studio
{
    public static class LayerRecognitionRulesPage
    {
        public const string PageId = "base-layer-recognition-rules";

        public static CDBoxPageDefinition Create(ICDBoxLogger logger)
        {
            if (logger == null) throw new ArgumentNullException("logger");
            return new CDBoxPageDefinition(PageId, "属性识别表",
                BuildDocument,
                delegate(CDBoxPageRouteRequest request)
                {
                    return Route(request, logger);
                })
            {
                Width = 1520,
                Height = 855,
                MinimumWidth = 720,
                MinimumHeight = 580
            };
        }
        public static string BuildEmbeddedSection()
        {
            var page = new StringBuilder();
            page.Append("<section id=\"recognitionRulesPage\" class=\"rules-page recognition-rules-page rr-embedded\" data-route=\"recognition-rules\" style=\"display:none\">");
            page.Append("<div class=\"settings-head\"><div><span class=\"kicker\">CDBox Studio</span><h2>属性识别表</h2></div></div>");
            page.Append(BuildEditorCard(false, null));
            page.Append("</section>");
            return page.ToString();
        }

        public static string BuildDocument()
        {
            var html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"><title>CDBox Studio - 属性识别表</title><style>");
            html.Append(BuildStyles(true));
            html.Append("</style></head><body>");
            html.Append("<section id=\"recognitionRulesPage\" class=\"recognition-rules-page rr-standalone\" data-route=\"recognition-rules\">");
            html.Append("<main class=\"rr-page\">");
            html.Append(BuildEditorCard(true, LayerRecognitionRulesStore.RulesFilePath));
            html.Append("</main></section><div id=\"toastStack\" class=\"toast-stack\"></div>");
            html.Append("<script>\n");
            html.Append("var recognitionRulesData=").Append(LayerRecognitionRulesStore.BuildRulesJson(LayerRecognitionRulesStore.LoadRules())).Append(";\n");
            html.Append("var defaultRecognitionRulesData=").Append(LayerRecognitionRulesStore.BuildRulesJson(LayerRecognitionRulesStore.LoadDefaultRules())).Append(";\n");
            html.Append(BuildComponentScript());
            html.Append("\n(function(){\n");
            html.Append("  function post(name,arg){if(window.chrome&&chrome.webview){chrome.webview.postMessage('studio|'+name+'|'+encodeURIComponent(arg||''));}}\n");
            html.Append("  function toast(message,kind){var stack=document.getElementById('toastStack');if(!stack)return;var node=document.createElement('div');node.className='toast '+(kind||'info');node.textContent=message||'';stack.appendChild(node);setTimeout(function(){node.style.opacity='0';node.style.transform='translateX(10px)';},2600);setTimeout(function(){if(node.parentNode)node.parentNode.removeChild(node);},3100);} window.CDBoxStudioToast=toast;\n");
            html.Append("  try{window.recognitionRulesEditor=window.CDBoxRecognitionRulesPage.create({rootId:'recognitionRulesPage',data:recognitionRulesData,defaultData:defaultRecognitionRulesData,toast:toast,post:post,standalone:true,autoOpenSearch:false});setTimeout(function(){post('ready','recognition-rules');},80);}catch(ex){var msg=(ex&&ex.stack)||String(ex||'未知错误');document.body.innerHTML='<div class=\"rr-error\"><div><h2>属性识别表页面初始化失败</h2><pre>'+String(msg).replace(/[&<>]/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;'}[c];})+'</pre></div></div>';post('pageError',msg);}\n");
            html.Append("})();\n");
            html.Append("</script></body></html>");
            return UtilityWorkbench.Apply(html.ToString());
        }

        private static CDBoxPageRouteResult Route(
            CDBoxPageRouteRequest request, ICDBoxLogger logger)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return new CDBoxPageRouteResult { Handled = false };

            string name = request.Name.Trim().ToLowerInvariant();
            if (name == "ready")
                return new CDBoxPageRouteResult { Handled = true };
            if (name == "saverecognitionrules")
            {
                try
                {
                    int count = LayerRecognitionRulesStore.SavePayload(
                        request.Argument);
                    logger.Info("属性识别表已保存。规则数量：" + count
                        + "，路径："
                        + LayerRecognitionRulesStore.RulesFilePath);
                    return new CDBoxPageRouteResult
                    {
                        Handled = true,
                        RefreshPage = true,
                        ToastKind = "success",
                        ToastMessage = "属性识别表已保存：" + count
                            + " 条规则"
                    };
                }
                catch (Exception ex)
                {
                    logger.Error("保存属性识别表失败。", ex);
                    return new CDBoxPageRouteResult
                    {
                        Handled = true,
                        ToastKind = "error",
                        ToastMessage = "属性识别表保存失败：" + ex.Message
                    };
                }
            }
            if (name == "openlegacyrecognition")
                return new CDBoxPageRouteResult
                {
                    Handled = true,
                    ToastKind = "info",
                    ToastMessage = "属性识别表现已统一使用独立页面。"
                };
            if (name == "pageerror")
            {
                logger.Warn("属性识别表页面异常："
                    + (request.Argument ?? string.Empty));
                return new CDBoxPageRouteResult
                {
                    Handled = true,
                    ToastKind = "error",
                    ToastMessage = "页面异常，已写入日志"
                };
            }
            return new CDBoxPageRouteResult { Handled = false };
        }

        public static string BuildStyles(bool standalone)
        {
            return UtilityWorkbench.Read("rules.css");
        }

        public static string BuildEmbeddedBridgeScript()
        {
            return BuildComponentScript();
        }

        private static string BuildEditorCard(bool standalone, string logFilePath)
        {
            return UtilityWorkbench.Read("rules.html");
        }

        private static string BuildComponentScript()
        {
            return UtilityWorkbench.Read("rules.js");
        }

        private static string Html(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        private static string HtmlAttr(string text)
        {
            return Html(text).Replace("\r", string.Empty).Replace("\n", " ");
        }
    }
}
