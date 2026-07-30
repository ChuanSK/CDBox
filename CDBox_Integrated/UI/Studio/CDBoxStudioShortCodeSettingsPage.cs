using System.Text;
using System.Web.Script.Serialization;
using TCPipeAutoDraw.Modules.ShortCodeRecognition;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioShortCodeSettingsPage
    {
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static string BuildStandaloneDocument(
            CDBoxStudioSettings studio)
        {
            return BuildDocument(studio, false);
        }

        public static string BuildEmbeddedSection(CDBoxStudioSettings studio)
        {
            string document = BuildDocument(studio, true);
            return "<section id=\"shortCodeSettingsPage\" style=\"display:none;height:calc(100vh - 48px);min-height:420px\">"
                + "<iframe id=\"shortCodeSettingsFrame\" title=\"简码识别设置\" style=\"display:block;width:100%;height:100%;border:0;background:transparent\" srcdoc=\""
                + Html(document) + "\"></iframe></section>";
        }

        private static string BuildDocument(CDBoxStudioSettings studio,
            bool embedded)
        {
            studio = studio ?? new CDBoxStudioSettings();
            studio.Normalize();
            string initial = Serializer.Serialize(
                ShortCodeRecognitionSettingsStore.Load());
            StringBuilder html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"zh-CN\"><head>")
                .Append("<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
                .Append("<title>简码识别设置</title><style>")
                .Append(BuildStyles())
                .Append("</style></head><body data-theme=\"")
                .Append(Html(studio.Theme)).Append("\" class=\"")
                .Append(studio.AnimationsEnabled
                    ? string.Empty : "no-animations")
                .Append("\"><main class=\"page\"><header><h1>简码识别设置</h1>")
                .Append("<button id=\"save\" class=\"primary\">保存设置</button></header>")
                .Append("<section class=\"card\"><label class=\"field\"><span>简码识别符号</span>")
                .Append("<input id=\"symbol\" maxlength=\"16\" autocomplete=\"off\" spellcheck=\"false\"></label></section>")
                .Append("<section class=\"choice-grid\">")
                .Append("<button id=\"previous\" class=\"choice\" type=\"button\"><strong>连接开头前一个点</strong></button>")
                .Append("<button id=\"next\" class=\"choice\" type=\"button\"><strong>连接结尾后一个点</strong></button>")
                .Append("<button id=\"close\" class=\"choice\" type=\"button\"><strong>自动闭合</strong></button>")
                .Append("</section></main><div id=\"toast\" class=\"toast\"></div><script>")
                .Append(BuildScript(embedded, initial))
                .Append("</script></body></html>");
            return html.ToString();
        }

        private static string BuildStyles()
        {
            return @"
:root{--bg:#f5f7fb;--panel:#fff;--panel2:#f8fafc;--text:#172033;--muted:#64748b;--line:#dce5f1;--brand:#326fea;--brand2:#7c3aed}
body[data-theme='fresh']{--bg:#f2f6ff;--panel2:#edf4ff;--line:#d7e5ff}
body[data-theme='dark']{--bg:#0f172a;--panel:#172033;--panel2:#111827;--text:#e5e7eb;--muted:#94a3b8;--line:#2b3a50;--brand:#60a5fa;--brand2:#a78bfa}
*{box-sizing:border-box}html,body{min-height:100%;margin:0;background:var(--bg);color:var(--text);font-family:'Microsoft YaHei UI','Segoe UI',sans-serif}button,input{font:inherit}
.page{min-height:100vh;padding:18px;background:linear-gradient(180deg,rgba(217,230,255,.42),transparent 240px),var(--bg)}
header{display:flex;align-items:center;justify-content:space-between;gap:16px;margin-bottom:14px}h1{margin:0;font-size:23px}
button{border:1px solid var(--line);border-radius:8px;background:var(--panel);color:var(--text);cursor:pointer}
.primary{border:0;padding:10px 18px;background:linear-gradient(135deg,var(--brand),var(--brand2));color:#fff;font-weight:750;box-shadow:0 10px 22px rgba(50,111,234,.18)}
.card,.choice{border:1px solid var(--line);border-radius:10px;background:var(--panel);box-shadow:0 8px 22px rgba(15,23,42,.045)}
.card{padding:18px}.field{display:grid;gap:8px}.field span{font-size:13px;font-weight:750;color:var(--muted)}
input{height:42px;width:100%;border:1px solid var(--line);border-radius:8px;padding:0 12px;background:var(--panel2);color:var(--text);outline:0;font-size:17px}
input:focus{border-color:var(--brand);box-shadow:0 0 0 3px rgba(50,111,234,.10)}
.choice-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;margin-top:12px}
.choice{min-height:96px;padding:18px;text-align:left;transition:border-color .18s ease,box-shadow .18s ease,transform .18s ease}
.choice:hover{transform:translateY(-1px);border-color:var(--brand)}
.choice.selected{border-color:var(--brand);background:linear-gradient(135deg,rgba(50,111,234,.11),rgba(124,58,237,.07));box-shadow:0 0 0 3px rgba(50,111,234,.08)}
.choice strong{font-size:15px}.toast{display:none;position:fixed;right:18px;bottom:18px;padding:10px 14px;border-radius:8px;background:#172033;color:#fff;box-shadow:0 14px 34px rgba(15,23,42,.24)}
.toast.show{display:block}.toast.error{background:#b91c1c}.no-animations *{transition:none!important;animation:none!important}
@media(max-width:620px){.choice-grid{grid-template-columns:1fr}}";
        }

        private static string BuildScript(bool embedded, string initial)
        {
            return @"
(function(){
var embedded=" + (embedded ? "true" : "false") + @",state=" + initial + @",timer=0;
function byId(id){return document.getElementById(id)}
function post(name,arg){try{if(embedded&&window.parent&&window.parent.CDBoxStudioPost){window.parent.CDBoxStudioPost(name,arg||'');return;}chrome.webview.postMessage('studio|'+name+'|'+encodeURIComponent(arg||''));}catch(ex){}}
function toast(text,kind){var t=byId('toast');t.textContent=text||'';t.className='toast show '+(kind||'');clearTimeout(timer);timer=setTimeout(function(){t.className='toast'},2200)}
function render(data){state=data||state||{};byId('symbol').value=state.RecognitionSymbol||'+';byId('previous').classList.toggle('selected',!!state.ConnectPreviousPoint);byId('next').classList.toggle('selected',!!state.ConnectNextPoint);byId('close').classList.toggle('selected',!!state.AutoClose)}
function toggle(name,id){state[name]=!state[name];byId(id).classList.toggle('selected',!!state[name])}
function payload(){return {RecognitionSymbol:(byId('symbol').value||'').trim(),ConnectPreviousPoint:!!state.ConnectPreviousPoint,ConnectNextPoint:!!state.ConnectNextPoint,AutoClose:!!state.AutoClose}}
byId('previous').onclick=function(){toggle('ConnectPreviousPoint','previous')};
byId('next').onclick=function(){toggle('ConnectNextPoint','next')};
byId('close').onclick=function(){toggle('AutoClose','close')};
byId('save').onclick=function(){post('saveShortCodeSettings',JSON.stringify(payload()))};
window.CDBoxShortCodeSettingsLoad=function(data){render(data)};
window.CDBoxShortCodeSettingsSaved=function(data){render(data);toast('设置已保存','success')};
window.CDBoxShortCodeSettingsError=function(message){toast(message||'保存失败','error')};
if(embedded&&window.parent){window.parent.CDBoxShortCodeSettingsLoad=window.CDBoxShortCodeSettingsLoad;window.parent.CDBoxShortCodeSettingsSaved=window.CDBoxShortCodeSettingsSaved;window.parent.CDBoxShortCodeSettingsError=window.CDBoxShortCodeSettingsError}
render(state);post('getShortCodeSettings','');
})();";
        }

        private static string Html(string value)
        {
            return (value ?? string.Empty).Replace("&", "&amp;")
                .Replace("\"", "&quot;").Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
