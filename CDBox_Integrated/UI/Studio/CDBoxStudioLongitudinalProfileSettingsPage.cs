using System.Text;
using System.Web.Script.Serialization;
using TCPipeAutoDraw.Modules.LongitudinalProfile;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioLongitudinalProfileSettingsPage
    {
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static string BuildStandaloneDocument(
            CDBoxStudioSettings studio)
        {
            return BuildDocument(studio, false);
        }

        public static string BuildEmbeddedSection(
            CDBoxStudioSettings studio)
        {
            string document = BuildDocument(studio, true);
            return "<section id=\"longitudinalProfileSettingsPage\" style=\"display:none;height:calc(100vh - 48px);min-height:600px\">"
                + "<iframe id=\"longitudinalProfileSettingsFrame\" title=\"纵断面设置\" style=\"display:block;width:100%;height:100%;border:0;background:transparent\" srcdoc=\""
                + Html(document) + "\"></iframe></section>";
        }

        private static string BuildDocument(
            CDBoxStudioSettings studio, bool embedded)
        {
            studio = studio ?? new CDBoxStudioSettings();
            studio.Normalize();
            string initial = Serializer.Serialize(
                LongitudinalProfileSettingsStore.Load());
            var html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"zh-CN\"><head>")
                .Append("<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
                .Append("<title>纵断面设置</title><style>")
                .Append(Styles())
                .Append("</style></head><body data-theme=\"")
                .Append(Html(studio.Theme)).Append("\" class=\"")
                .Append(studio.AnimationsEnabled
                    ? string.Empty : "no-animations")
                .Append("\"><main class=\"page\"><header><h1>纵断面设置</h1>")
                .Append("<button id=\"save\" class=\"primary\">保存设置</button></header>")
                .Append("<section class=\"card\"><h2>表头与比例</h2><div class=\"form-grid\">")
                .Append(Field("表头宽度", "headerWidth", "number", "0.1"))
                .Append(Field("与表头栏横向净距", "headerGap", "number", "0.1"))
                .Append(Field("横向比例", "horizontalScale", "number", "1"))
                .Append(Field("纵向比例", "verticalScale", "number", "1"))
                .Append(Field("水平网格间距", "horizontalGrid", "number", "0.1"))
                .Append(Field("高程网格间距", "elevationGrid", "number", "0.1"))
                .Append(Field("高程上下留量", "elevationPadding", "number", "0.1"))
                .Append(Field("绘图图层", "layerName", "text", null))
                .Append("</div></section>")
                .Append("<section class=\"card\"><h2>表头文字</h2><div class=\"form-grid\">")
                .Append(Field("文字样式", "headerTextStyle", "text", null))
                .Append(Field("文字高度", "headerTextHeight", "number", "0.1"))
                .Append(Field("文字颜色（ACI）", "headerTextColor", "number", "1"))
                .Append("<label class=\"field\"><span>文字对齐</span><div class=\"segmented\" id=\"alignment\">")
                .Append("<button type=\"button\" data-value=\"左对齐\">左对齐</button>")
                .Append("<button type=\"button\" data-value=\"中间对齐\">中间对齐</button>")
                .Append("<button type=\"button\" data-value=\"右对齐\">右对齐</button>")
                .Append("</div></label>")
                .Append(Field("高程小数位", "elevationDecimals", "number", "1"))
                .Append(Field("数值小数位", "valueDecimals", "number", "1"))
                .Append(Field("坡度小数位", "slopeDecimals", "number", "1"))
                .Append("</div></section>")
                .Append("<section class=\"card\"><h2>表头栏</h2>")
                .Append("<div class=\"table-wrap\"><table><thead><tr><th>栏名称</th><th>行间距</th><th>文字高度</th><th>文字样式</th></tr></thead><tbody id=\"rows\"></tbody></table></div></section>")
                .Append("<section class=\"card\"><h2>显示样式</h2>")
                .Append("<div class=\"table-wrap\"><table><thead><tr><th>类型</th><th>颜色（ACI）</th><th>线型</th><th>线型比例</th><th>线宽</th></tr></thead><tbody id=\"styles\"></tbody></table></div></section>")
                .Append("</main><div id=\"toast\" class=\"toast\"></div><script>")
                .Append(Script(embedded, initial))
                .Append("</script></body></html>");
            return html.ToString();
        }

        private static string Field(
            string label, string id, string type, string step)
        {
            return "<label class=\"field\"><span>" + Html(label)
                + "</span><input id=\"" + Html(id) + "\" type=\""
                + Html(type) + "\""
                + (string.IsNullOrWhiteSpace(step)
                    ? string.Empty : " step=\"" + Html(step) + "\"")
                + "></label>";
        }

        private static string Styles()
        {
            return @"
:root{--bg:#f5f7fb;--panel:#fff;--panel2:#f8fafc;--text:#172033;--muted:#64748b;--line:#dce5f1;--brand:#326fea;--brand2:#7c3aed}
body[data-theme='fresh']{--bg:#f2f6ff;--panel2:#edf4ff;--line:#d7e5ff}
body[data-theme='dark']{--bg:#0f172a;--panel:#172033;--panel2:#111827;--text:#e5e7eb;--muted:#94a3b8;--line:#2b3a50;--brand:#60a5fa;--brand2:#a78bfa}
*{box-sizing:border-box}html,body{min-height:100%;margin:0;background:var(--bg);color:var(--text);font-family:'Microsoft YaHei UI','Segoe UI',sans-serif}button,input,select{font:inherit}
.page{min-height:100vh;padding:18px;background:linear-gradient(180deg,rgba(217,230,255,.42),transparent 260px),var(--bg)}
header{position:sticky;top:0;z-index:5;display:flex;align-items:center;justify-content:space-between;gap:16px;margin:-4px -4px 14px;padding:4px;background:linear-gradient(180deg,var(--bg) 78%,transparent)}
h1{margin:0;font-size:23px}h2{margin:0 0 14px;font-size:15px}
button{border:1px solid var(--line);border-radius:8px;background:var(--panel);color:var(--text);cursor:pointer}
.primary{border:0;padding:10px 18px;background:linear-gradient(135deg,var(--brand),var(--brand2));color:#fff;font-weight:750;box-shadow:0 10px 22px rgba(50,111,234,.18)}
.card{margin-bottom:12px;padding:16px;border:1px solid var(--line);border-radius:10px;background:var(--panel);box-shadow:0 8px 22px rgba(15,23,42,.045)}
.form-grid{display:grid;grid-template-columns:repeat(4,minmax(145px,1fr));gap:12px}
.field{display:grid;gap:7px}.field>span{font-size:12px;font-weight:750;color:var(--muted)}
input,select{width:100%;height:38px;border:1px solid var(--line);border-radius:7px;padding:0 10px;background:var(--panel2);color:var(--text);outline:0}
input:focus,select:focus{border-color:var(--brand);box-shadow:0 0 0 3px rgba(50,111,234,.09)}
.segmented{display:flex;height:38px}.segmented button{flex:1;border-radius:0}.segmented button:first-child{border-radius:7px 0 0 7px}.segmented button:last-child{border-radius:0 7px 7px 0}.segmented button+button{margin-left:-1px}.segmented button.selected{position:relative;border-color:var(--brand);background:rgba(50,111,234,.10);color:var(--brand);font-weight:750}
.table-wrap{overflow:auto;border:1px solid var(--line);border-radius:8px}table{width:100%;border-collapse:collapse;min-width:680px}th,td{height:42px;padding:6px 8px;border-bottom:1px solid var(--line);text-align:left}th{font-size:12px;color:var(--muted);background:var(--panel2)}tr:last-child td{border-bottom:0}td input,td select{height:32px;background:transparent}
.toast{display:none;position:fixed;right:18px;bottom:18px;z-index:10;padding:10px 14px;border-radius:8px;background:#172033;color:#fff;box-shadow:0 14px 34px rgba(15,23,42,.24)}.toast.show{display:block}.toast.error{background:#b91c1c}.no-animations *{transition:none!important;animation:none!important}
@media(max-width:900px){.form-grid{grid-template-columns:repeat(2,minmax(145px,1fr))}}@media(max-width:560px){.form-grid{grid-template-columns:1fr}}";
        }

        private static string Script(bool embedded, string initial)
        {
            return @"
(function(){
var embedded=" + (embedded ? "true" : "false") + @",state=" + initial + @",timer=0;
function byId(id){return document.getElementById(id)}
function post(name,arg){try{if(embedded&&window.parent&&window.parent.CDBoxStudioPost){window.parent.CDBoxStudioPost(name,arg||'');return}chrome.webview.postMessage('studio|'+name+'|'+encodeURIComponent(arg||''))}catch(ex){}}
function toast(text,kind){var t=byId('toast');t.textContent=text||'';t.className='toast show '+(kind||'');clearTimeout(timer);timer=setTimeout(function(){t.className='toast'},2200)}
function number(id,fallback){var value=Number(byId(id).value);return Number.isFinite(value)?value:fallback}
function esc(value){return String(value==null?'':value).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/""/g,'&quot;')}
function render(data){
 state=data||state||{};var values={headerWidth:state.HeaderWidth,headerGap:state.HeaderChartGap,horizontalScale:state.HorizontalScale,verticalScale:state.VerticalScale,horizontalGrid:state.HorizontalGridInterval,elevationGrid:state.ElevationGridInterval,elevationPadding:state.ElevationPadding,layerName:state.LayerName,headerTextStyle:state.HeaderTextStyleName,headerTextHeight:state.HeaderTextHeight,headerTextColor:state.HeaderTextColorIndex,elevationDecimals:state.ElevationDecimals,valueDecimals:state.ValueDecimals,slopeDecimals:state.SlopeDecimals};
 Object.keys(values).forEach(function(id){byId(id).value=values[id]==null?'':values[id]});
 document.querySelectorAll('#alignment button').forEach(function(button){button.classList.toggle('selected',button.dataset.value===(state.HeaderTextAlignment||'中间对齐'))});
 var rows=byId('rows');rows.innerHTML='';(state.Rows||[]).forEach(function(row,index){var tr=document.createElement('tr');tr.dataset.index=index;tr.innerHTML='<td><input data-key=""Name"" value=""'+esc(row.Name)+'""></td><td><input data-key=""Height"" type=""number"" step=""0.1"" value=""'+row.Height+'""></td><td><input data-key=""TextHeight"" type=""number"" step=""0.1"" value=""'+row.TextHeight+'""></td><td><input data-key=""TextStyleName"" value=""'+esc(row.TextStyleName||'HZ')+'""></td>';rows.appendChild(tr)});
 var styles=byId('styles');styles.innerHTML='';(state.Styles||[]).forEach(function(item,index){var tr=document.createElement('tr');tr.dataset.index=index;tr.innerHTML='<td><strong>'+esc(item.Name)+'</strong></td><td><input data-key=""ColorIndex"" type=""number"" min=""0"" max=""256"" value=""'+item.ColorIndex+'""></td><td><input data-key=""LineTypeName"" value=""'+esc(item.LineTypeName||'ByBlock')+'""></td><td><input data-key=""LineTypeScale"" type=""number"" step=""0.1"" value=""'+item.LineTypeScale+'""></td><td><select data-key=""LineWeight""><option>ByBlock</option><option>ByLayer</option><option>LineWeight013</option><option>LineWeight025</option><option>LineWeight050</option></select></td>';styles.appendChild(tr);tr.querySelector('select').value=item.LineWeight||'ByBlock'});
}
function payload(){
 var rows=(state.Rows||[]).map(function(row,index){var tr=byId('rows').querySelector('tr[data-index=""'+index+'""]');var copy=Object.assign({},row);tr.querySelectorAll('[data-key]').forEach(function(input){var key=input.dataset.key;copy[key]=(key==='Height'||key==='TextHeight')?Number(input.value):input.value});return copy});
 var styles=(state.Styles||[]).map(function(item,index){var tr=byId('styles').querySelector('tr[data-index=""'+index+'""]');var copy=Object.assign({},item);tr.querySelectorAll('[data-key]').forEach(function(input){var key=input.dataset.key;copy[key]=(key==='ColorIndex'||key==='LineTypeScale')?Number(input.value):input.value});return copy});
 var selected=document.querySelector('#alignment button.selected');
 return {HeaderWidth:number('headerWidth',45),HeaderChartGap:number('headerGap',5),HeaderTextHeight:number('headerTextHeight',6),HeaderTextStyleName:byId('headerTextStyle').value,HeaderTextColorIndex:number('headerTextColor',7),HeaderTextAlignment:selected?selected.dataset.value:'中间对齐',HorizontalScale:number('horizontalScale',1000),VerticalScale:number('verticalScale',100),HorizontalGridInterval:number('horizontalGrid',5),ElevationGridInterval:number('elevationGrid',1),ElevationPadding:number('elevationPadding',1),ElevationDecimals:number('elevationDecimals',3),ValueDecimals:number('valueDecimals',2),SlopeDecimals:number('slopeDecimals',2),LayerName:byId('layerName').value,Rows:rows,Styles:styles}
}
document.querySelectorAll('#alignment button').forEach(function(button){button.onclick=function(){document.querySelectorAll('#alignment button').forEach(function(x){x.classList.toggle('selected',x===button)})}});
byId('save').onclick=function(){post('saveLongitudinalProfileSettings',JSON.stringify(payload()))};
window.CDBoxLongitudinalProfileSettingsLoad=function(data){render(data)};
window.CDBoxLongitudinalProfileSettingsSaved=function(data){render(data);toast('设置已保存','success')};
window.CDBoxLongitudinalProfileSettingsError=function(message){toast(message||'保存失败','error')};
if(embedded&&window.parent){window.parent.CDBoxLongitudinalProfileSettingsLoad=window.CDBoxLongitudinalProfileSettingsLoad;window.parent.CDBoxLongitudinalProfileSettingsSaved=window.CDBoxLongitudinalProfileSettingsSaved;window.parent.CDBoxLongitudinalProfileSettingsError=window.CDBoxLongitudinalProfileSettingsError}
render(state);post('getLongitudinalProfileSettings','');
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
