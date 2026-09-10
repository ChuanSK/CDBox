using System;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using CDBox.Common.Features.ShortCodeRecognition;
using CDBox.Shared.UI;
using TCPipeAutoDraw.UI.Studio;

namespace CDBox.Common.UI
{
    public static class ShortCodeRecognitionSettingsPage
    {
        public const string PageId = "common-short-code-settings";
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static CDBoxPageDefinition Create()
        {
            return new CDBoxPageDefinition(
                PageId,
                "简码识别设置",
                BuildDocument,
                Route)
            {
                Width = 760,
                Height = 480,
                MinimumWidth = 620,
                MinimumHeight = 420
            };
        }

        public static string BuildDocument()
        {
            return BuildDocument(ShortCodeRecognitionSettingsStore.Load(), CDBoxStudioSettingsStore.Load());
        }

        internal static string BuildDocument(ShortCodeRecognitionSettings values, CDBoxStudioSettings appearance)
        {
            string initial = CDBoxThemeCatalog.Json(values);
            var html = new StringBuilder();
            html.Append("<!doctype html><html lang='zh-CN'><head>")
                .Append("<meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>")
                .Append("<title>简码识别设置</title><style>")
                .Append(Styles())
                .Append("</style></head><body><main><header><h1>简码识别设置</h1>")
                .Append("<button id='save' class='primary'>保存设置</button></header>")
                .Append("<section class='card'><label><div><span>简码识别符号</span><p>用于识别坐标文件中的连接简码。</p></div>")
                .Append("<input id='symbol' maxlength='16' autocomplete='off' spellcheck='false'></label></section>")
                .Append("<section class='choices'>")
                .Append("<button id='previous' class='choice' type='button' role='switch' aria-checked='false'><strong>连接开头前一个点</strong></button>")
                .Append("<button id='next' class='choice' type='button' role='switch' aria-checked='false'><strong>连接结尾后一个点</strong></button>")
                .Append("<button id='close' class='choice' type='button' role='switch' aria-checked='false'><strong>自动闭合</strong></button>")
                .Append("</section></main><div id='toast' class='toast'></div><script>")
                .Append(Script(initial))
                .Append("</script></body></html>");
            return CommonWorkbench.Apply(html.ToString(), appearance);
        }

        private static CDBoxPageRouteResult Route(
            CDBoxPageRouteRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return new CDBoxPageRouteResult { Handled = false };
            string name = request.Name.Trim().ToLowerInvariant();
            if (name == "ready" || name == "getshortcodesettings")
                return SettingsResult("CDBoxShortCodeSettingsLoad");
            if (name != "saveshortcodesettings")
                return new CDBoxPageRouteResult { Handled = false };

            try
            {
                ShortCodeRecognitionSettings settings =
                    Serializer.Deserialize<ShortCodeRecognitionSettings>(
                        request.Argument ?? string.Empty)
                    ?? new ShortCodeRecognitionSettings();
                settings.Normalize();
                ShortCodeRecognitionSettingsStore.Save(settings);
                CDBoxPageRouteResult result = SettingsResult(
                    "CDBoxShortCodeSettingsSaved");
                result.ToastKind = "success";
                result.ToastMessage = "设置已保存";
                return result;
            }
            catch (Exception ex)
            {
                return new CDBoxPageRouteResult
                {
                    Handled = true,
                    ToastKind = "error",
                    ToastMessage = "保存失败：" + ex.Message,
                    ExecuteScript =
                        "window.CDBoxShortCodeSettingsError&&window.CDBoxShortCodeSettingsError("
                        + Serializer.Serialize(ex.Message) + ");"
                };
            }
        }

        private static CDBoxPageRouteResult SettingsResult(string function)
        {
            return new CDBoxPageRouteResult
            {
                Handled = true,
                ExecuteScript = "window." + function + "&&window."
                    + function + "("
                    + Serializer.Serialize(
                        ShortCodeRecognitionSettingsStore.Load()) + ");"
            };
        }

        private static string Styles()
        {
            return @"

main{min-height:100vh;padding:24px 28px}header{display:flex;justify-content:space-between;align-items:center;gap:16px;padding-bottom:20px;border-bottom:1px solid var(--line)}.card{padding:24px 0;border-bottom:1px solid var(--line)}.card label{display:grid;grid-template-columns:minmax(0,1fr) 180px;align-items:center;gap:20px}.card span{font-weight:600}.card p{font-size:12px;margin:5px 0 0}.choices{padding-top:18px;display:grid;gap:4px}.choice{display:flex;align-items:center;justify-content:space-between;gap:24px;min-height:48px;padding:10px 0;border:0;border-radius:0;text-align:left}.choice:after{content:'';width:30px;height:18px;border-radius:12px;flex-shrink:0;background:radial-gradient(circle at 9px 9px,var(--muted) 6px,transparent 6.5px),var(--selected)}.choice.selected:after{background:radial-gradient(circle at 21px 9px,var(--bg) 6px,transparent 6.5px),var(--focus)}.choice:hover{background:var(--hover)}.toast{display:none;position:fixed;right:20px;bottom:20px;padding:12px 16px;border-radius:8px;background:var(--text);color:var(--bg)}.toast.show{display:block}.toast.error{background:var(--danger);color:var(--bg)}@media(max-width:480px){main{padding:20px 18px}.card label{grid-template-columns:1fr}}
";
        }

        private static string Script(string initial)
        {
            return @"
(function(){var state=" + initial + @",timer=0;
function byId(id){return document.getElementById(id)}
function post(name,arg){try{chrome.webview.postMessage('studio|'+name+'|'+encodeURIComponent(arg||''));}catch(ex){}}
function toast(text,kind){var t=byId('toast');t.textContent=text||'';t.className='toast show '+(kind||'');clearTimeout(timer);timer=setTimeout(function(){t.className='toast'},2200)}
function render(data){state=data||state||{};byId('symbol').value=state.RecognitionSymbol||'+';byId('previous').classList.toggle('selected',!!state.ConnectPreviousPoint);byId('next').classList.toggle('selected',!!state.ConnectNextPoint);byId('close').classList.toggle('selected',!!state.AutoClose);[['previous','ConnectPreviousPoint'],['next','ConnectNextPoint'],['close','AutoClose']].forEach(function(x){byId(x[0]).setAttribute('aria-checked',String(!!state[x[1]]))})}
function toggle(name,id){state[name]=!state[name];byId(id).classList.toggle('selected',!!state[name]);byId(id).setAttribute('aria-checked',String(!!state[name]))}
function payload(){return {RecognitionSymbol:(byId('symbol').value||'').trim(),ConnectPreviousPoint:!!state.ConnectPreviousPoint,ConnectNextPoint:!!state.ConnectNextPoint,AutoClose:!!state.AutoClose}}
byId('previous').onclick=function(){toggle('ConnectPreviousPoint','previous')};byId('next').onclick=function(){toggle('ConnectNextPoint','next')};byId('close').onclick=function(){toggle('AutoClose','close')};byId('save').onclick=function(){post('saveShortCodeSettings',JSON.stringify(payload()))};window.CDBoxShortCodeSettingsLoad=function(data){render(data)};window.CDBoxShortCodeSettingsSaved=function(data){render(data);toast('设置已保存','success')};window.CDBoxShortCodeSettingsError=function(message){toast(message||'保存失败','error')};render(state);post('getShortCodeSettings','');})();";
        }
    }
}
