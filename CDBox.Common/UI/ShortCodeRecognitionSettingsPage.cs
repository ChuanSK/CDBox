using System;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using CDBox.Common.Features.ShortCodeRecognition;
using CDBox.Shared.UI;

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
            string initial = Serializer.Serialize(
                ShortCodeRecognitionSettingsStore.Load());
            var html = new StringBuilder();
            html.Append("<!doctype html><html lang='zh-CN'><head>")
                .Append("<meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>")
                .Append("<title>简码识别设置</title><style>")
                .Append(Styles())
                .Append("</style></head><body><main><header><h1>简码识别设置</h1>")
                .Append("<button id='save' class='primary'>保存设置</button></header>")
                .Append("<section class='card'><label><span>简码识别符号</span>")
                .Append("<input id='symbol' maxlength='16' autocomplete='off' spellcheck='false'></label></section>")
                .Append("<section class='choices'>")
                .Append("<button id='previous' class='choice' type='button'><strong>连接开头前一个点</strong></button>")
                .Append("<button id='next' class='choice' type='button'><strong>连接结尾后一个点</strong></button>")
                .Append("<button id='close' class='choice' type='button'><strong>自动闭合</strong></button>")
                .Append("</section></main><div id='toast' class='toast'></div><script>")
                .Append(Script(initial))
                .Append("</script></body></html>");
            return html.ToString();
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
:root{--bg:#f5f7fb;--panel:#fff;--panel2:#f8fafc;--text:#172033;--muted:#64748b;--line:#dce5f1;--brand:#326fea;--brand2:#7c3aed}
*{box-sizing:border-box}html,body{min-height:100%;margin:0;background:var(--bg);color:var(--text);font-family:'Microsoft YaHei UI','Segoe UI',sans-serif}button,input{font:inherit}main{min-height:100vh;padding:18px;background:linear-gradient(180deg,rgba(217,230,255,.42),transparent 240px),var(--bg)}header{display:flex;align-items:center;justify-content:space-between;gap:16px;margin-bottom:14px}h1{margin:0;font-size:23px}button{border:1px solid var(--line);border-radius:8px;background:var(--panel);color:var(--text);cursor:pointer}.primary{border:0;padding:10px 18px;background:linear-gradient(135deg,var(--brand),var(--brand2));color:#fff;font-weight:750}.card,.choice{border:1px solid var(--line);border-radius:10px;background:var(--panel);box-shadow:0 8px 22px rgba(15,23,42,.045)}.card{padding:18px}label{display:grid;gap:8px}label span{font-size:13px;font-weight:750;color:var(--muted)}input{height:42px;width:100%;border:1px solid var(--line);border-radius:8px;padding:0 12px;background:var(--panel2);color:var(--text);outline:0;font-size:17px}.choices{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;margin-top:12px}.choice{min-height:96px;padding:18px;text-align:left}.choice.selected{border-color:var(--brand);background:linear-gradient(135deg,rgba(50,111,234,.11),rgba(124,58,237,.07));box-shadow:0 0 0 3px rgba(50,111,234,.08)}.toast{display:none;position:fixed;right:18px;bottom:18px;padding:10px 14px;border-radius:8px;background:#172033;color:#fff}.toast.show{display:block}.toast.error{background:#b91c1c}@media(prefers-color-scheme:dark){:root{--bg:#0f172a;--panel:#172033;--panel2:#111827;--text:#e5e7eb;--muted:#94a3b8;--line:#2b3a50;--brand:#60a5fa;--brand2:#a78bfa}}@media(max-width:620px){.choices{grid-template-columns:1fr}}";
        }

        private static string Script(string initial)
        {
            return @"
(function(){var state=" + initial + @",timer=0;
function byId(id){return document.getElementById(id)}
function post(name,arg){try{chrome.webview.postMessage('studio|'+name+'|'+encodeURIComponent(arg||''));}catch(ex){}}
function toast(text,kind){var t=byId('toast');t.textContent=text||'';t.className='toast show '+(kind||'');clearTimeout(timer);timer=setTimeout(function(){t.className='toast'},2200)}
function render(data){state=data||state||{};byId('symbol').value=state.RecognitionSymbol||'+';byId('previous').classList.toggle('selected',!!state.ConnectPreviousPoint);byId('next').classList.toggle('selected',!!state.ConnectNextPoint);byId('close').classList.toggle('selected',!!state.AutoClose)}
function toggle(name,id){state[name]=!state[name];byId(id).classList.toggle('selected',!!state[name])}
function payload(){return {RecognitionSymbol:(byId('symbol').value||'').trim(),ConnectPreviousPoint:!!state.ConnectPreviousPoint,ConnectNextPoint:!!state.ConnectNextPoint,AutoClose:!!state.AutoClose}}
byId('previous').onclick=function(){toggle('ConnectPreviousPoint','previous')};byId('next').onclick=function(){toggle('ConnectNextPoint','next')};byId('close').onclick=function(){toggle('AutoClose','close')};byId('save').onclick=function(){post('saveShortCodeSettings',JSON.stringify(payload()))};window.CDBoxShortCodeSettingsLoad=function(data){render(data)};window.CDBoxShortCodeSettingsSaved=function(data){render(data);toast('设置已保存','success')};window.CDBoxShortCodeSettingsError=function(message){toast(message||'保存失败','error')};render(state);post('getShortCodeSettings','');})();";
        }
    }
}
