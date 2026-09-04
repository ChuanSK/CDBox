// Canonical independent page owned by CDBox.Common.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TCPipeAutoDraw.Modules.ExcelToCad;

namespace TCPipeAutoDraw.UI.Studio
{
    public static class CDBoxStudioExcelToCadPage
    {
        public static string BuildStandaloneDocument(
            double defaultTextHeight,
            ExcelToCadDialogSettings savedSettings = null,
            IList<string> layerNames = null)
        {
            return BuildDocument(new CDBoxStudioSettings(),
                defaultTextHeight, savedSettings, layerNames, false);
        }

        internal static string BuildStandaloneDocument(CDBoxStudioSettings settings,
            double defaultTextHeight, ExcelToCadDialogSettings savedSettings = null,
            IList<string> layerNames = null)
        {
            return BuildDocument(settings, defaultTextHeight, savedSettings,
                layerNames, false);
        }

        internal static string BuildEmbeddedSection(CDBoxStudioSettings settings,
            double defaultTextHeight, ExcelToCadDialogSettings savedSettings = null,
            IList<string> layerNames = null)
        {
            string document = BuildDocument(settings, defaultTextHeight,
                savedSettings, layerNames, true);
            return "<section id=\"excelToCadPage\" style=\"display:none;height:calc(100vh - 48px);min-height:620px\">"
                + "<iframe id=\"excelToCadFrame\" title=\"Excel 转 CAD 表格\" style=\"display:block;width:100%;height:100%;border:0;background:transparent\" srcdoc=\""
                + Html(document) + "\"></iframe></section>";
        }

        private static string BuildDocument(CDBoxStudioSettings settings,
            double defaultTextHeight, ExcelToCadDialogSettings savedSettings,
            IList<string> layerNames, bool embedded)
        {
            settings = settings ?? new CDBoxStudioSettings();
            settings.Normalize();
            if (double.IsNaN(defaultTextHeight) || double.IsInfinity(defaultTextHeight)
                || defaultTextHeight <= 0.01) defaultTextHeight = 2.5;
            defaultTextHeight = Math.Max(0.1, Math.Min(10000.0, defaultTextHeight));
            savedSettings = savedSettings ?? new ExcelToCadDialogSettings
            {
                TextHeight = defaultTextHeight
            };
            savedSettings.Normalize(defaultTextHeight);
            defaultTextHeight = savedSettings.TextHeight;
            var layers = (layerNames ?? new List<string> { "0" })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (!layers.Any(x => string.Equals(x, "0",
                StringComparison.OrdinalIgnoreCase))) layers.Insert(0, "0");

            var html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
                .Append("<title>Excel 转 CAD 表格</title><style>");
            html.Append(@"
:root{--bg:#f5f7fb;--panel:#fff;--panel2:#f8fafc;--text:#172033;--muted:#64748b;--line:#dce5f1;--brand:#326fea;--brand2:#7c3aed;--ok:#059669;--warn:#d97706;--danger:#b91c1c}
body[data-theme='fresh']{--bg:#f2f6ff;--panel2:#edf4ff;--line:#d7e5ff}
body[data-theme='dark']{--bg:#0f172a;--panel:#172033;--panel2:#111827;--text:#e5e7eb;--muted:#94a3b8;--line:#2b3a50;--brand:#60a5fa;--brand2:#a78bfa}
*{box-sizing:border-box}html,body{height:100%;margin:0;background:var(--bg);color:var(--text);font-family:'Microsoft YaHei UI','Segoe UI',sans-serif}body{overflow:auto}
button,input,select{font:inherit}.ex-page{min-height:100%;display:flex;flex-direction:column;gap:12px;padding:16px 18px 18px;background:linear-gradient(180deg,rgba(217,230,255,.46),transparent 250px),var(--bg)}
.ex-head{display:flex;align-items:center;justify-content:space-between;gap:16px}.ex-head h1{margin:0;font-size:23px}
.ex-grid{display:grid;grid-template-columns:minmax(440px,1.08fr) minmax(390px,.92fr);gap:12px;flex:1 1 auto;min-height:0}.ex-column{display:flex;flex-direction:column;gap:12px;min-width:0}
.ex-card{border:1px solid var(--line);border-radius:13px;background:var(--panel);box-shadow:0 10px 24px rgba(30,50,90,.055);overflow:hidden}.ex-card.grow{flex:1 1 auto}.ex-card-head{padding:11px 13px;border-bottom:1px solid var(--line);background:var(--panel2)}.ex-card-head h2{margin:0;font-size:14px}.ex-card-body{padding:13px}
.ex-field{display:grid;gap:6px;margin-bottom:11px}.ex-field:last-child{margin-bottom:0}.ex-field>span{color:var(--muted);font-size:11px;font-weight:750}.ex-input-row{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:8px}.ex-input-row input[type=text],.ex-field input[type=text],.ex-field input[type=number],.ex-field select{width:100%;min-width:0;height:38px;border:1px solid var(--line);border-radius:9px;background:var(--panel2);color:var(--text);padding:0 10px;outline:0}.ex-input-row input:focus,.ex-field input:focus,.ex-field select:focus{border-color:var(--brand);box-shadow:0 0 0 3px rgba(50,111,234,.10)}
.ex-btn{height:38px;border:1px solid var(--line);border-radius:10px;background:var(--panel);color:var(--text);padding:0 13px;font-weight:750;cursor:pointer;white-space:nowrap}.ex-btn:hover{transform:translateY(-1px);border-color:rgba(50,111,234,.5);box-shadow:0 8px 18px rgba(50,111,234,.10)}.ex-btn.primary{border:0;color:#fff;background:linear-gradient(135deg,var(--brand),var(--brand2));box-shadow:0 9px 20px rgba(50,111,234,.20)}.ex-btn:disabled{opacity:.45;cursor:not-allowed;transform:none}
.ex-choices{display:grid;gap:8px}.ex-choice{position:relative;display:block;padding:11px 12px;border:1px solid var(--line);border-radius:11px;background:var(--panel2);cursor:pointer}.ex-choice:hover{border-color:rgba(50,111,234,.45)}.ex-choice.selected,.ex-choice:has(input:checked){border-color:var(--brand);background:rgba(50,111,234,.07);box-shadow:0 0 0 3px rgba(50,111,234,.09)}.ex-choice input[type=radio]{position:absolute;width:1px;height:1px;opacity:0;pointer-events:none}.ex-choice strong{display:block;font-size:13px}
.ex-live{display:none;margin:10px 0 0;padding-top:10px;border-top:1px solid var(--line)}.ex-live.show{display:block}.ex-status{margin-top:9px;padding:9px 10px;border-radius:9px;background:rgba(50,111,234,.07);color:var(--muted);font-size:11px;line-height:1.6}.ex-status:empty{display:none}.ex-status.ok{background:rgba(5,150,105,.10);color:var(--ok)}.ex-status.warn{background:rgba(217,119,6,.10);color:var(--warn)}.ex-status.error{background:rgba(185,28,28,.09);color:var(--danger)}
.ex-output{display:grid;grid-template-columns:repeat(3,1fr);gap:8px}.ex-output .ex-choice{min-height:52px;display:flex;align-items:center}.ex-checks{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px}.ex-check{display:flex;align-items:center;gap:8px;min-height:38px;padding:8px 9px;border:1px solid var(--line);border-radius:10px;background:var(--panel2);font-size:12px}.ex-check input{width:17px;height:17px;accent-color:var(--brand)}
.ex-subtitle{margin:15px 0 10px;padding-top:13px;border-top:1px solid var(--line);font-size:13px;font-weight:800}.ex-layer-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:9px}.ex-color-mode{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px}.ex-color-mode .ex-choice{min-height:38px;padding:9px 11px;text-align:center}
.ex-foot{display:flex;align-items:center;justify-content:flex-end;gap:12px}.ex-foot-status{margin-right:auto;min-width:0;color:var(--muted);font-size:12px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.ex-foot-status:empty{display:none}.ex-actions{display:flex;gap:8px;flex:0 0 auto}
.ex-modal-bg{display:none;position:fixed;inset:0;z-index:50;align-items:center;justify-content:center;padding:22px;background:rgba(15,23,42,.42);backdrop-filter:blur(4px)}.ex-modal-bg.show{display:flex}.ex-modal{width:min(540px,100%);border:1px solid var(--line);border-radius:13px;background:var(--panel);box-shadow:0 24px 70px rgba(15,23,42,.28);overflow:hidden}.ex-modal h3{margin:0;padding:14px 16px;border-bottom:1px solid var(--line);background:var(--panel2);font-size:15px}.ex-modal p{margin:0;padding:16px;color:var(--muted);line-height:1.7;white-space:pre-wrap}.ex-modal-actions{display:flex;justify-content:flex-end;gap:8px;padding:12px 16px;border-top:1px solid var(--line)}
.ex-toast{position:fixed;right:20px;bottom:20px;max-width:430px;padding:11px 14px;border-radius:11px;background:#172033;color:#fff;box-shadow:0 16px 38px rgba(15,23,42,.22);z-index:80}.ex-toast.error{background:#b91c1c}.ex-toast.success{background:#047857}
.no-animations *{animation:none!important;transition:none!important}@media(max-width:930px){.ex-grid{grid-template-columns:1fr}.ex-output{grid-template-columns:1fr}.ex-output .ex-choice{min-height:0}.ex-output strong{margin-top:0}}@media(max-width:600px){.ex-checks,.ex-layer-grid{grid-template-columns:1fr}.ex-foot{align-items:flex-start;flex-direction:column}.ex-actions{align-self:flex-end}}
");
            html.Append("</style></head><body data-theme=\"").Append(Html(settings.Theme))
                .Append("\" class=\"").Append(settings.AnimationsEnabled ? string.Empty : "no-animations")
                .Append("\"><main class=\"ex-page\"><header class=\"ex-head\"><h1>Excel 转 CAD 表格</h1></header><div class=\"ex-grid\"><div class=\"ex-column\">")
                .Append("<section class=\"ex-card\"><div class=\"ex-card-head\"><h2>Excel 数据</h2></div><div class=\"ex-card-body\">")
                .Append("<label class=\"ex-field\"><span>文件</span><div class=\"ex-input-row\"><input id=\"filePath\" type=\"text\" readonly placeholder=\"尚未选择工作簿\"><button class=\"ex-btn\" id=\"browse\">选择 Excel 文件</button></div></label>")
                .Append("<label class=\"ex-field\"><span>工作表</span><select id=\"sheet\" disabled><option>请先选择文件</option></select></label></div></section>")
                .Append("<section class=\"ex-card grow\"><div class=\"ex-card-head\"><h2>Excel 数据范围</h2></div><div class=\"ex-card-body\"><div class=\"ex-choices\">")
                .Append(RangeChoice("used", "所有使用的单元格",
                    savedSettings.RangeMode == ExcelTableRangeMode.UsedRange))
                .Append("<label class=\"ex-choice\"><input type=\"radio\" name=\"range\" value=\"live\"")
                .Append(savedSettings.RangeMode == ExcelTableRangeMode.LiveSelection ? " checked" : string.Empty)
                .Append("><span><strong>用户在 Excel 中选择的区域</strong>")
                .Append("<div class=\"ex-live\" id=\"liveBox\"><div class=\"ex-input-row\"><input id=\"selectedRange\" type=\"text\" placeholder=\"例如 A1:F20\"><button class=\"ex-btn\" id=\"openExcel\">打开 Excel 并选择</button></div></div></span></label>")
                .Append(RangeChoice("print", "页面可打印区域",
                    savedSettings.RangeMode == ExcelTableRangeMode.PrintArea))
                .Append("</div><div class=\"ex-status\" id=\"rangeStatus\"></div></div></section></div>")
                .Append("<div class=\"ex-column\"><section class=\"ex-card\"><div class=\"ex-card-head\"><h2>CAD 表格类型</h2></div><div class=\"ex-card-body\"><div class=\"ex-output\">")
                .Append(OutputChoice("exploded", "分解线文字",
                    savedSettings.OutputType == CadExcelTableOutputType.ExplodedEntities))
                .Append(OutputChoice("table", "CAD 原生 TABLE",
                    savedSettings.OutputType == CadExcelTableOutputType.NativeTable))
                .Append(OutputChoice("block", "块形式",
                    savedSettings.OutputType == CadExcelTableOutputType.Block))
                .Append("</div></div></section><section class=\"ex-card grow\"><div class=\"ex-card-head\"><h2>表格设置</h2></div><div class=\"ex-card-body\"><div class=\"ex-checks\">")
                .Append(Check("background", "保留单元格背景色",
                    savedSettings.PreserveBackgroundColors))
                .Append(Check("textColors", "保留文字颜色",
                    savedSettings.PreserveTextColors))
                .Append(Check("merged", "保留合并单元格",
                    savedSettings.PreserveMergedCells))
                .Append(Check("grid", "绘制网格及边框",
                    savedSettings.DrawGridLines))
                .Append("</div><label class=\"ex-field\" style=\"margin-top:13px\"><span>基准文字高度（CAD 单位）</span><input id=\"textHeight\" type=\"number\" min=\"0.1\" max=\"10000\" step=\"0.001\" value=\"")
                .Append(defaultTextHeight.ToString("0.###", CultureInfo.InvariantCulture))
                .Append("\"></label><div class=\"ex-subtitle\">实体图层</div><div class=\"ex-layer-grid\">")
                .Append("<label class=\"ex-field\"><span>单元格线</span><select id=\"gridLayer\">")
                .Append(LayerOptions(layers, savedSettings.GridLayerName))
                .Append("</select></label><label class=\"ex-field\"><span>表格内容</span><select id=\"contentLayer\">")
                .Append(LayerOptions(layers, savedSettings.ContentLayerName))
                .Append("</select></label></div><div class=\"ex-color-mode\">")
                .Append(ColorModeChoice("layer", "颜色随层",
                    savedSettings.EntityColorMode == ExcelCadEntityColorMode.ByLayer))
                .Append(ColorModeChoice("block", "颜色随块",
                    savedSettings.EntityColorMode == ExcelCadEntityColorMode.ByBlock))
                .Append("</div></div></section></div></div>")
                .Append("<footer class=\"ex-foot\"><span class=\"ex-foot-status\" id=\"footStatus\"></span><div class=\"ex-actions\"><button class=\"ex-btn\" id=\"cancel\">取消</button><button class=\"ex-btn primary\" id=\"confirm\">生成到 CAD</button></div></footer></main>")
                .Append("<div class=\"ex-modal-bg\" id=\"fallback\"><div class=\"ex-modal\"><h3>无法读取 Excel 当前未保存状态</h3><p id=\"fallbackMessage\"></p><div class=\"ex-modal-actions\"><button class=\"ex-btn\" id=\"fallbackCancel\">返回检查</button><button class=\"ex-btn primary\" id=\"fallbackContinue\">读取最近保存版本</button></div></div></div><script>");
            html.Append(@"
(function(){
var embedded=").Append(embedded ? "true" : "false").Append(@";
var callbackHost=embedded&&window.parent?window.parent:window;
var workbook=null,connected=false,pollTimer=0,busy=false,pageActive=!embedded;
function post(n,a){try{if(embedded&&window.parent&&window.parent.CDBoxStudioPost){window.parent.CDBoxStudioPost(n,a||'');return;}chrome.webview.postMessage('studio|'+n+'|'+encodeURIComponent(a||''));}catch(ex){}}
function esc(v){return String(v==null?'':v).replace(/[&<>']/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;',""'"":'&#39;'}[c]||c;});}
function selectedSheet(){var x=document.getElementById('sheet'),o=x.options[x.selectedIndex];return o?{index:Number(o.dataset.index),name:o.value,used:o.dataset.used||'',print:o.dataset.print||''}:null;}
function payload(){var s=selectedSheet();return {filePath:document.getElementById('filePath').value,sheetIndex:s?s.index:0,sheetName:s?s.name:'',rangeMode:(document.querySelector('input[name=range]:checked')||{}).value||'live',selectedRange:document.getElementById('selectedRange').value,outputType:(document.querySelector('input[name=output]:checked')||{}).value||'exploded',textHeight:Number(document.getElementById('textHeight').value||0),preserveBackgroundColors:document.getElementById('background').checked,preserveTextColors:document.getElementById('textColors').checked,preserveMergedCells:document.getElementById('merged').checked,drawGridLines:document.getElementById('grid').checked,gridLayerName:document.getElementById('gridLayer').value,contentLayerName:document.getElementById('contentLayer').value,entityColorMode:(document.querySelector('input[name=entityColor]:checked')||{}).value||'block'};}
function setBusy(v,message){busy=!!v;document.getElementById('confirm').disabled=busy;document.getElementById('browse').disabled=busy;document.getElementById('footStatus').textContent=busy?(message||'正在处理…'):'';}
function summary(){var s=selectedSheet(),mode=(document.querySelector('input[name=range]:checked')||{}).value;if(!workbook||!s)return '请选择 Excel 文件与数据范围';if(mode==='used')return s.name+'!'+s.used;if(mode==='print')return s.print?s.name+'!'+s.print:'当前工作表未设置打印区域';var r=document.getElementById('selectedRange').value.trim();return r?s.name+'!'+r:'请在 Excel 中框选一个连续区域';}
function renderWorkbook(d){workbook=d||null;var file=document.getElementById('filePath'),sheet=document.getElementById('sheet');file.value=workbook?workbook.filePath||'':'';sheet.innerHTML='';(workbook&&workbook.sheets||[]).forEach(function(s){var o=document.createElement('option');o.value=s.name;o.textContent=s.name+(s.hidden?'（隐藏）':'');o.dataset.index=s.index;o.dataset.used=s.usedRange||'';o.dataset.print=s.printArea||'';sheet.appendChild(o);});sheet.disabled=!sheet.options.length;if(sheet.options.length){var preferred=workbook.selectedSheetName,found=false;[].slice.call(sheet.options).forEach(function(o,i){if((preferred&&o.value===preferred)||(!preferred&&Number(o.dataset.index)===Number(workbook.activeSheetIndex))){sheet.selectedIndex=i;found=true;}});if(!found)sheet.selectedIndex=0;}refreshRange();setBusy(false);}
function markChoices(){document.querySelectorAll('.ex-choice').forEach(function(x){var r=x.querySelector('input[type=radio]');x.classList.toggle('selected',!!(r&&r.checked));});}
function savePreferences(){post('saveExcelToCadPreferences',JSON.stringify(payload()));}
function refreshRange(){var mode=(document.querySelector('input[name=range]:checked')||{}).value||'live',s=selectedSheet(),status=document.getElementById('rangeStatus');document.getElementById('liveBox').classList.toggle('show',mode==='live');status.className='ex-status';if(mode==='used')status.textContent=s?'使用区域：'+s.used:'';else if(mode==='print'){status.textContent=s?(s.print?'打印区域：'+s.print:'当前工作表未设置打印区域。'):'';if(s&&!s.print)status.classList.add('error');}else{var r=document.getElementById('selectedRange').value.trim();status.textContent=r?'已读取：'+(s?s.name+'!':'')+r:(connected?'请在 Excel 中框选一个连续区域。':'');if(r)status.classList.add('ok');}markChoices();}
function startPoll(){connected=true;if(pollTimer)clearInterval(pollTimer);pollTimer=setInterval(function(){if(connected&&!busy&&pageActive)post('pollExcelSelection','');},420);refreshRange();}
document.getElementById('browse').onclick=function(){setBusy(true,'正在读取工作簿信息…');post('browseExcelWorkbook','');};
document.getElementById('openExcel').onclick=function(){setBusy(true,'正在打开 Excel/WPS…');post('openExcelSelection',document.getElementById('filePath').value);};
document.getElementById('sheet').onchange=refreshRange;document.getElementById('selectedRange').oninput=refreshRange;
document.querySelectorAll('input[name=range]').forEach(function(x){x.onchange=function(){refreshRange();savePreferences();};});
document.querySelectorAll('input[name=output],input[name=entityColor],#background,#textColors,#merged,#grid,#textHeight,#gridLayer,#contentLayer').forEach(function(x){x.onchange=function(){markChoices();savePreferences();};});
document.getElementById('cancel').onclick=function(){post('cancelExcelToCad',embedded?'embedded':'standalone');};
document.getElementById('confirm').onclick=function(){if(busy)return;setBusy(true,'正在校验 Excel 数据…');post('confirmExcelToCad',JSON.stringify(payload()));};
document.getElementById('fallbackCancel').onclick=function(){document.getElementById('fallback').classList.remove('show');setBusy(false);};
document.getElementById('fallbackContinue').onclick=function(){document.getElementById('fallback').classList.remove('show');setBusy(true,'正在读取最近保存版本…');post('confirmSavedExcelFallback','');};
callbackHost.CDBoxExcelWorkbookLoaded=function(d){renderWorkbook(d);toast('Excel 工作簿已读取','success');};
callbackHost.CDBoxExcelSelectionConnected=function(){startPoll();setBusy(false);toast('已连接 Excel，请框选一个连续区域','success');};
callbackHost.CDBoxExcelSelectionChanged=function(d){if(!d)return;if(d.workbook)renderWorkbook(d.workbook);if(d.sheetName){var sh=document.getElementById('sheet');[].slice.call(sh.options).forEach(function(o,i){if(o.value===d.sheetName)sh.selectedIndex=i;});}if(d.rangeAddress)document.getElementById('selectedRange').value=d.rangeAddress;connected=true;setBusy(false);refreshRange();};
callbackHost.CDBoxExcelSelectionStatus=function(message,kind){setBusy(false);var x=document.getElementById('rangeStatus');x.textContent=message||'';x.className='ex-status '+(kind||'');};
callbackHost.CDBoxExcelSnapshotFallback=function(message){document.getElementById('fallbackMessage').textContent=(message||'')+'\n\n可以改为读取工作簿最近一次保存的内容。';document.getElementById('fallback').classList.add('show');};
callbackHost.CDBoxExcelError=function(message){setBusy(false);toast(message||'操作失败','error');};
callbackHost.CDBoxExcelOperationCompleted=function(success,message){setBusy(false);toast(message||(success?'表格已生成':'操作已取消'),success?'success':'');};
callbackHost.CDBoxExcelSetActive=function(value){pageActive=!!value;};
if(embedded&&window.parent&&window.parent.CDBoxStudioExcelToCadIsActive)pageActive=!!window.parent.CDBoxStudioExcelToCadIsActive();
if(!embedded)callbackHost.CDBoxStudioToast=function(message,kind){toast(message,kind);};
function toast(message,kind){if(embedded&&window.parent&&window.parent.CDBoxStudioToast){window.parent.CDBoxStudioToast(message,kind);return;}var old=document.querySelector('.ex-toast');if(old)old.remove();var x=document.createElement('div');x.className='ex-toast '+(kind||'');x.textContent=message||'';document.body.appendChild(x);setTimeout(function(){x.remove();},3000);}
document.addEventListener('keydown',function(e){if(e.key==='Escape'&&!document.getElementById('fallback').classList.contains('show')){post('cancelExcelToCad',embedded?'embedded':'standalone');e.preventDefault();}});
refreshRange();markChoices();setTimeout(function(){post(embedded?'excelToCadEmbeddedReady':'ready','excel-to-cad');},50);
})();
");
            html.Append("</script></body></html>");
            return html.ToString();
        }

        private static string RangeChoice(string value, string title, bool selected)
        {
            return "<label class=\"ex-choice\"><input type=\"radio\" name=\"range\" value=\""
                + value + "\"" + (selected ? " checked" : string.Empty)
                + "><span><strong>" + title + "</strong></span></label>";
        }

        private static string OutputChoice(string value, string title, bool selected)
        {
            return "<label class=\"ex-choice\"><input type=\"radio\" name=\"output\" value=\""
                + value + "\"" + (selected ? " checked" : string.Empty)
                + "><span><strong>" + title + "</strong></span></label>";
        }

        private static string Check(string id, string title, bool selected)
        {
            return "<label class=\"ex-check\"><input id=\"" + id
                + "\" type=\"checkbox\"" + (selected ? " checked" : string.Empty)
                + ">" + title + "</label>";
        }

        private static string ColorModeChoice(string value, string title,
            bool selected)
        {
            return "<label class=\"ex-choice\"><input type=\"radio\" name=\"entityColor\" value=\""
                + value + "\"" + (selected ? " checked" : string.Empty)
                + "><strong>" + title + "</strong></label>";
        }

        private static string LayerOptions(IEnumerable<string> layers,
            string selected)
        {
            var html = new StringBuilder();
            foreach (string layer in layers ?? new string[0])
            {
                html.Append("<option value=\"").Append(Html(layer)).Append("\"");
                if (string.Equals(layer, selected,
                    StringComparison.CurrentCultureIgnoreCase))
                    html.Append(" selected");
                html.Append(">").Append(Html(layer)).Append("</option>");
            }
            return html.ToString();
        }

        private static string Html(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("&", "&amp;").Replace("<", "&lt;")
                .Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        }
    }
}
