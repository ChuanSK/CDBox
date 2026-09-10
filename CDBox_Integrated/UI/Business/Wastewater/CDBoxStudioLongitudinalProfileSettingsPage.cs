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
            string rowCatalog = Serializer.Serialize(
                LongitudinalProfileSettings.DefaultRows());
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
                .Append("<div class=\"top-grid\"><section class=\"card compact\"><h2>表头与图面</h2><div class=\"form-grid three\">")
                .Append(Field("表头宽度", "headerWidth", "number", "0.1"))
                .Append(Field("与表头栏横向净距", "headerGap", "number", "0.1"))
                .Append(Field("水平网格间距", "horizontalGrid", "number", "0.1"))
                .Append(Field("高程网格间距", "elevationGrid", "number", "0.1"))
                .Append(Field("高程上下留量", "elevationPadding", "number", "0.1"))
                .Append(Field("绘图图层", "layerName", "text", null))
                .Append("</div></section>")
                .Append("<section class=\"card compact\"><h2>表头文字</h2><div class=\"form-grid three\">")
                .Append(Field("文字高度", "headerTextHeight", "number", "0.1"))
                .Append("<label class=\"field\"><span>文字样式</span><select id=\"headerTextStyle\"></select></label>")
                .Append("<label class=\"field\"><span>文字颜色</span><div id=\"headerTextColor\"></div></label>")
                .Append("<label class=\"field\"><span>文字对齐</span><div class=\"segmented\" id=\"alignment\">")
                .Append("<button type=\"button\" data-value=\"左对齐\">左对齐</button>")
                .Append("<button type=\"button\" data-value=\"中间对齐\">中间对齐</button>")
                .Append("<button type=\"button\" data-value=\"右对齐\">右对齐</button>")
                .Append("</div></label>")
                .Append(Field("高程小数位", "elevationDecimals", "number", "1"))
                .Append(Field("数值小数位", "valueDecimals", "number", "1"))
                .Append(Field("坡度小数位", "slopeDecimals", "number", "1"))
                .Append("</div></section>")
                .Append("<section class=\"card compact styles-card\"><h2>显示样式</h2>")
                .Append("<div class=\"table-wrap\"><table class=\"styles-table\"><thead><tr><th>类型</th><th>颜色</th><th>线型</th><th>比例</th><th>线宽</th></tr></thead><tbody id=\"styles\"></tbody></table></div></section></div>")
                .Append("<section class=\"card rows-card\"><div class=\"card-title\"><h2>表头栏目</h2><div class=\"row-tools\"><select id=\"addRowType\"></select><button type=\"button\" id=\"addRow\">新增</button></div></div>")
                .Append("<div class=\"table-wrap\"><table class=\"rows-table\"><thead><tr><th class=\"drag-col\"></th><th>栏类型</th><th>行间距</th><th>文字高度</th><th>文字样式</th><th>文本颜色</th><th class=\"action-col\"></th></tr></thead><tbody id=\"rows\"></tbody></table></div></section>")
                .Append("</main><div id=\"toast\" class=\"toast\"></div><script>")
                .Append(Script(embedded, initial, rowCatalog))
                .Append("</script></body></html>");
            return TCPipeAutoDraw.UI.Studio.WastewaterWorkbench.Apply(html.ToString(), "longitudinal", studio);
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
.page{min-height:100vh;padding:16px 18px 26px;background:linear-gradient(180deg,rgba(217,230,255,.46),transparent 250px),var(--bg)}
header{position:sticky;top:0;z-index:5;display:flex;align-items:center;justify-content:space-between;gap:16px;margin:-2px -2px 12px;padding:2px;background:linear-gradient(180deg,var(--bg) 78%,transparent)}
h1{margin:0;font-size:24px}h2{margin:0 0 11px;font-size:15px}
button{border:1px solid var(--line);border-radius:9px;background:var(--panel);color:var(--text);cursor:pointer;font-weight:700}
button:hover{transform:translateY(-1px);border-color:#b9c9e1}.primary{border:0;padding:9px 17px;background:linear-gradient(135deg,var(--brand),var(--brand2));color:#fff;font-weight:750;box-shadow:0 8px 18px rgba(50,111,234,.18)}
.top-grid{display:grid;grid-template-columns:repeat(2,minmax(360px,1fr));gap:12px;margin-bottom:12px}.card{padding:14px;border:1px solid var(--line);border-radius:12px;background:var(--panel);box-shadow:0 8px 22px rgba(30,50,90,.055)}.compact{min-width:0}.styles-card{grid-column:1/-1}
.form-grid{display:grid;grid-template-columns:repeat(4,minmax(120px,1fr));gap:10px}.form-grid.two{grid-template-columns:repeat(2,minmax(118px,1fr))}.form-grid.three{grid-template-columns:repeat(3,minmax(110px,1fr))}
.field{display:grid;gap:5px;min-width:0}.field>span{font-size:11px;font-weight:750;color:var(--muted)}
input,select{width:100%;height:36px;border:1px solid var(--line);border-radius:8px;padding:0 9px;background:var(--panel2);color:var(--text);outline:0}
input:focus,select:focus{border-color:var(--brand);box-shadow:0 0 0 3px rgba(50,111,234,.09)}
.segmented{display:flex;height:34px}.segmented button{flex:1;border-radius:0;font-size:12px}.segmented button:first-child{border-radius:6px 0 0 6px}.segmented button:last-child{border-radius:0 6px 6px 0}.segmented button+button{margin-left:-1px}.segmented button.selected{position:relative;border-color:var(--brand);background:rgba(50,111,234,.10);color:var(--brand);font-weight:750}
.table-wrap{overflow:auto;border:1px solid var(--line);border-radius:9px}table{width:100%;border-collapse:collapse;min-width:640px}th,td{height:40px;padding:5px 7px;border-bottom:1px solid var(--line);text-align:left}th{font-size:11px;color:var(--muted);background:var(--panel2);white-space:nowrap}tr:last-child td{border-bottom:0}td input,td select{height:32px;background:var(--panel2)}.styles-table{min-width:520px}.styles-table th,.styles-table td{padding:4px 6px}.styles-table input,.styles-table select{font-size:12px}
.card-title{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-bottom:10px}.card-title h2{margin:0}.row-tools{display:flex;gap:7px}.row-tools select{width:180px}.row-tools button{padding:0 15px}.rows-table{min-width:820px}.drag-col{width:38px}.action-col{width:58px}.drag-handle{width:30px;height:30px;border:0;background:transparent;color:var(--muted);font-size:17px;cursor:grab;user-select:none}.drag-handle:active{cursor:grabbing}.dragging{opacity:.38}.icon-btn{border:1px solid var(--line);background:var(--panel2);border-radius:8px;width:30px;height:30px;cursor:pointer;font-weight:900}.icon-btn:hover{border-color:rgba(59,130,246,.45);background:rgba(59,130,246,.08)}.icon-btn.danger{color:#b91c1c;border-color:#fecaca;background:var(--panel)}.icon-btn.danger:hover{background:#fee2e2}.icon-btn:disabled{opacity:.42;cursor:not-allowed;transform:none}.color-button{display:flex;align-items:center;gap:7px;width:100%;height:32px;padding:0 8px;background:var(--panel2);white-space:nowrap}.color-button i{width:14px;height:14px;border-radius:4px;border:1px solid rgba(100,116,139,.45);flex:0 0 auto}.color-button span{overflow:hidden;text-overflow:ellipsis}.empty-option{color:var(--muted)}
.toast{display:none;position:fixed;right:18px;bottom:18px;z-index:10;padding:10px 14px;border-radius:8px;background:#172033;color:#fff;box-shadow:0 14px 34px rgba(15,23,42,.24)}.toast.show{display:block}.toast.error{background:#b91c1c}.no-animations *{transition:none!important;animation:none!important}
@media(max-width:980px){.top-grid{grid-template-columns:1fr}.styles-card{grid-column:auto}}@media(max-width:680px){.form-grid.two,.form-grid.three{grid-template-columns:1fr}.page{padding:12px}}";
        }

        private static string Script(bool embedded, string initial,
            string rowCatalog)
        {
            return @"
(function(){
var embedded=" + (embedded ? "true" : "false") + @",state=" + initial + @",catalog=" + rowCatalog + @",textStyles=['宋体','HZ'],timer=0,dragIndex=-1;
function byId(id){return document.getElementById(id)}
function post(name,arg){try{if(embedded&&window.parent&&window.parent.CDBoxStudioPost){window.parent.CDBoxStudioPost(name,arg||'');return}chrome.webview.postMessage('studio|'+name+'|'+encodeURIComponent(arg||''))}catch(ex){}}
function toast(text,kind){var t=byId('toast');t.textContent=text||'';t.className='toast show '+(kind||'');clearTimeout(timer);timer=setTimeout(function(){t.className='toast'},2200)}
function number(id,fallback){var value=Number(byId(id).value);return Number.isFinite(value)?value:fallback}
function esc(value){return String(value==null?'':value).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/""/g,'&quot;')}
function colorHex(index){var map={1:'#ff0000',2:'#ffff00',3:'#00ff00',4:'#00ffff',5:'#0000ff',6:'#ff00ff',7:'#f3f4f6',8:'#808080',9:'#c0c0c0',256:'#94a3b8'};return map[index]||'#64748b'}
function colorName(index){return index===256?'随层':'ACI '+index}
function colorButton(path,index,name,hex){return '<button type=""button"" class=""color-button"" data-color-path=""'+path+'"" data-color-value=""'+index+'""><i style=""background:'+(hex||colorHex(index))+'""></i><span>'+esc(name||colorName(index))+'</span></button>'}
function catalogItem(key){return catalog.find(function(x){return x.Key===key})||catalog[0]}
function textStyleOptions(current){var values=textStyles.slice();if(current&&values.indexOf(current)<0)values.unshift(current);return values.map(function(x){return '<option value=""'+esc(x)+'"" '+(x===current?'selected':'')+'>'+esc(x)+'</option>'}).join('')}
function captureRows(){document.querySelectorAll('#rows tr[data-index]').forEach(function(tr){var i=Number(tr.dataset.index),row=state.Rows[i];if(!row)return;row.Height=Number(tr.querySelector('[data-key=Height]').value);row.TextHeight=Number(tr.querySelector('[data-key=TextHeight]').value);row.TextStyleName=tr.querySelector('[data-key=TextStyleName]').value;row.TextColorIndex=Number(tr.querySelector('[data-color-value]').dataset.colorValue)})}
function typeOptions(current){return catalog.map(function(item){return '<option value=""'+esc(item.Key)+'"" '+(item.Key===current?'selected':'')+'>'+esc(item.Name)+'</option>'}).join('')}
function renderAddOptions(){var select=byId('addRowType');select.innerHTML=catalog.map(function(x){return '<option value=""'+esc(x.Key)+'"">'+esc(x.Name)+'</option>'}).join('');select.disabled=!catalog.length;byId('addRow').disabled=!catalog.length}
function bindColorButtons(root){root.querySelectorAll('[data-color-path]').forEach(function(button){button.onclick=function(){post('openLongitudinalProfileColorPicker',JSON.stringify({path:button.dataset.colorPath,index:Number(button.dataset.colorValue||7)}))}})}
function renderRows(){var body=byId('rows');body.innerHTML='';(state.Rows||[]).forEach(function(row,index){var tr=document.createElement('tr'),handle;tr.dataset.index=index;tr.innerHTML='<td><button type=""button"" class=""drag-handle"" draggable=""true"" title=""拖动排序"">⋮⋮</button></td><td><select data-key=""Key"">'+typeOptions(row.Key)+'</select></td><td><input data-key=""Height"" type=""number"" step=""0.1"" value=""'+row.Height+'""></td><td><input data-key=""TextHeight"" type=""number"" step=""0.1"" value=""'+row.TextHeight+'""></td><td><select data-key=""TextStyleName"">'+textStyleOptions(row.TextStyleName||'宋体')+'</select></td><td>'+colorButton('row:'+index,row.TextColorIndex,row.TextColorName,row.TextColorHex)+'</td><td><button type=""button"" class=""icon-btn danger"" data-delete-row title=""删除"" '+(state.Rows.length<=1?'disabled':'')+'>×</button></td>';body.appendChild(tr);tr.querySelector('[data-key=Key]').onchange=function(e){captureRows();var item=catalogItem(e.target.value),old=state.Rows[index];state.Rows[index]=Object.assign({},item,{Height:old.Height||item.Height,TextHeight:old.TextHeight||item.TextHeight,TextStyleName:old.TextStyleName||item.TextStyleName,TextColorIndex:old.TextColorIndex||item.TextColorIndex});renderRows()};tr.querySelector('[data-delete-row]').onclick=function(){if(state.Rows.length<=1)return;captureRows();state.Rows.splice(index,1);renderRows()};handle=tr.querySelector('.drag-handle');handle.ondragstart=function(e){captureRows();dragIndex=index;tr.classList.add('dragging');if(e.dataTransfer)e.dataTransfer.effectAllowed='move'};handle.ondragend=function(){dragIndex=-1;tr.classList.remove('dragging')};tr.ondragover=function(e){if(dragIndex<0)return;e.preventDefault()};tr.ondrop=function(e){e.preventDefault();if(dragIndex<0||dragIndex===index)return;var moved=state.Rows.splice(dragIndex,1)[0];state.Rows.splice(index,0,moved);dragIndex=-1;renderRows()}});bindColorButtons(body);renderAddOptions()}
function renderStyles(){var styles=byId('styles');styles.innerHTML='';(state.Styles||[]).forEach(function(item,index){var tr=document.createElement('tr');tr.dataset.index=index;tr.innerHTML='<td><strong>'+esc(item.Name)+'</strong></td><td>'+colorButton('style:'+index,item.ColorIndex,item.ColorName,item.ColorHex)+'</td><td><input data-key=""LineTypeName"" value=""'+esc(item.LineTypeName||'ByBlock')+'""></td><td><input data-key=""LineTypeScale"" type=""number"" step=""0.1"" value=""'+item.LineTypeScale+'""></td><td><select data-key=""LineWeight""><option>ByBlock</option><option>ByLayer</option><option>LineWeight013</option><option>LineWeight025</option><option>LineWeight050</option></select></td>';styles.appendChild(tr);tr.querySelector('select').value=item.LineWeight||'ByBlock'});bindColorButtons(styles)}
function render(data){
 state=data||state||{};state.Rows=state.Rows||[];state.Styles=state.Styles||[];var values={headerWidth:state.HeaderWidth,headerGap:state.HeaderChartGap,headerTextHeight:state.HeaderTextHeight,horizontalGrid:state.HorizontalGridInterval,elevationGrid:state.ElevationGridInterval,elevationPadding:state.ElevationPadding,layerName:state.LayerName,elevationDecimals:state.ElevationDecimals,valueDecimals:state.ValueDecimals,slopeDecimals:state.SlopeDecimals};
 Object.keys(values).forEach(function(id){byId(id).value=values[id]==null?'':values[id]});
 byId('headerTextStyle').innerHTML=textStyleOptions(state.HeaderTextStyleName||'宋体');
 byId('headerTextColor').innerHTML=colorButton('header',state.HeaderTextColorIndex,state.HeaderTextColorName,state.HeaderTextColorHex);bindColorButtons(byId('headerTextColor'));
 document.querySelectorAll('#alignment button').forEach(function(button){button.classList.toggle('selected',button.dataset.value===(state.HeaderTextAlignment||'中间对齐'))});
 renderRows();renderStyles();
}
function payload(){
 captureRows();var rows=(state.Rows||[]).map(function(row){var item=catalogItem(row.Key);return {Key:item.Key,Name:item.Name,Height:row.Height,TextHeight:row.TextHeight,TextStyleName:row.TextStyleName,TextColorIndex:row.TextColorIndex}});
 var styles=(state.Styles||[]).map(function(item,index){var tr=byId('styles').querySelector('tr[data-index=""'+index+'""]'),copy=Object.assign({},item);tr.querySelectorAll('[data-key]').forEach(function(input){var key=input.dataset.key;copy[key]=key==='LineTypeScale'?Number(input.value):input.value});copy.ColorIndex=Number(tr.querySelector('[data-color-value]').dataset.colorValue);return copy});
 var selected=document.querySelector('#alignment button.selected');
 return {HeaderWidth:number('headerWidth',45),HeaderChartGap:number('headerGap',5),HeaderTextHeight:number('headerTextHeight',6),HeaderTextStyleName:byId('headerTextStyle').value,HeaderTextColorIndex:Number(byId('headerTextColor').querySelector('[data-color-value]').dataset.colorValue||7),HeaderTextAlignment:selected?selected.dataset.value:'中间对齐',HorizontalGridInterval:number('horizontalGrid',5),ElevationGridInterval:number('elevationGrid',1),ElevationPadding:number('elevationPadding',1),ElevationDecimals:number('elevationDecimals',3),ValueDecimals:number('valueDecimals',2),SlopeDecimals:number('slopeDecimals',2),LayerName:byId('layerName').value,Rows:rows,Styles:styles}
}
document.querySelectorAll('#alignment button').forEach(function(button){button.onclick=function(){document.querySelectorAll('#alignment button').forEach(function(x){x.classList.toggle('selected',x===button)})}});
byId('addRow').onclick=function(){captureRows();var item=catalogItem(byId('addRowType').value);if(!item)return;state.Rows.push(Object.assign({},item));renderRows()};
byId('save').onclick=function(){post('saveLongitudinalProfileSettings',JSON.stringify(payload()))};
window.CDBoxLongitudinalProfileColorSelected=function(value){if(!value||!value.path)return;var parts=value.path.split(':'),index=Number(parts[1]);if(parts[0]==='header'){state.HeaderTextColorIndex=value.index;state.HeaderTextColorName=value.name;state.HeaderTextColorHex=value.cssColor;byId('headerTextColor').innerHTML=colorButton('header',value.index,value.name,value.cssColor);bindColorButtons(byId('headerTextColor'))}else if(parts[0]==='row'&&state.Rows[index]){state.Rows[index].TextColorIndex=value.index;state.Rows[index].TextColorName=value.name;state.Rows[index].TextColorHex=value.cssColor;renderRows()}else if(parts[0]==='style'&&state.Styles[index]){state.Styles[index].ColorIndex=value.index;state.Styles[index].ColorName=value.name;state.Styles[index].ColorHex=value.cssColor;renderStyles()}};
window.CDBoxLongitudinalProfileTextStylesLoad=function(values){textStyles=Array.isArray(values)&&values.length?values:['宋体','HZ'];render(state)};
window.CDBoxLongitudinalProfileSettingsLoad=function(data){render(data)};
window.CDBoxLongitudinalProfileSettingsSaved=function(data){render(data);toast('设置已保存','success')};
window.CDBoxLongitudinalProfileSettingsError=function(message){toast(message||'保存失败','error')};
if(embedded&&window.parent){window.parent.CDBoxLongitudinalProfileSettingsLoad=window.CDBoxLongitudinalProfileSettingsLoad;window.parent.CDBoxLongitudinalProfileSettingsSaved=window.CDBoxLongitudinalProfileSettingsSaved;window.parent.CDBoxLongitudinalProfileSettingsError=window.CDBoxLongitudinalProfileSettingsError;window.parent.CDBoxLongitudinalProfileColorSelected=window.CDBoxLongitudinalProfileColorSelected;window.parent.CDBoxLongitudinalProfileTextStylesLoad=window.CDBoxLongitudinalProfileTextStylesLoad}
render(state);post('getLongitudinalProfileSettings','');post('getLongitudinalProfileTextStyles','');
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
