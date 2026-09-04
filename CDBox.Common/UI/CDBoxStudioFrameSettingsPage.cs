// Canonical independent page owned by CDBox.Common.
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using TCPipeAutoDraw.Modules.FrameLayout;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioFrameSettingsPage
    {
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static string BuildStandaloneDocument(CDBoxStudioSettings studio)
        {
            return BuildDocument(studio, false);
        }

        public static string BuildEmbeddedSection(CDBoxStudioSettings studio)
        {
            string document = BuildDocument(studio, true);
            return "<section id=\"frameSettingsPage\" style=\"display:none;height:calc(100vh - 48px);min-height:650px\">"
                + "<iframe id=\"frameSettingsFrame\" title=\"图框设置\" style=\"display:block;width:100%;height:100%;border:0;background:transparent\" srcdoc=\""
                + Html(document) + "\"></iframe></section>";
        }

        public static object BuildContext()
        {
            FrameTemplateCatalog catalog = FrameTemplateCatalogStore.Load();
            bool catalogChanged = false;
            foreach (FrameTemplateCatalogItem template in catalog.Templates)
            {
                if (template.PreviewSegments != null
                    && template.PreviewSegments.Count > 0) continue;
                List<FrameTemplatePreviewSegment> segments;
                int entityCount;
                if (!FrameTemplatePreviewService.TryBuildFromSource(template,
                    out segments, out entityCount)) continue;
                template.PreviewSegments = segments;
                template.PreviewEntityCount = entityCount;
                catalogChanged = true;
            }
            if (catalogChanged)
            {
                try { FrameTemplateCatalogStore.Save(catalog); } catch { }
            }
            FrameLayoutSettings frameSettings = FrameLayoutSettingsStore.Load();
            Document activeDocument = Autodesk.AutoCAD.ApplicationServices
                .Application.DocumentManager.MdiActiveDocument;
            frameSettings.ScaleText = FrameScaleTextResolver.Resolve(
                activeDocument == null ? null : activeDocument.Database,
                frameSettings.ScaleText);
            return new
            {
                templates = catalog.Templates,
                papers = FramePaperSizes.GetAll(),
                settings = frameSettings,
                textStyles = LoadTextStyles()
            };
        }

        private static string BuildDocument(CDBoxStudioSettings studio,
            bool embedded)
        {
            studio = studio ?? new CDBoxStudioSettings();
            studio.Normalize();
            string initial = Serializer.Serialize(BuildContext());
            StringBuilder html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
                .Append("<title>图框设置</title><style>").Append(BuildStyles())
                .Append("</style></head><body data-theme=\"")
                .Append(Html(studio.Theme)).Append("\" class=\"")
                .Append(studio.AnimationsEnabled ? "" : "no-animations")
                .Append("\"><main class=\"fr-page\">")
                .Append("<header class=\"fr-head\"><h1>图框设置</h1><div class=\"fr-actions\">")
                .Append("<button data-action=\"add\">添加模板</button>")
                .Append("<button data-action=\"cut\">布置裁图区域</button>")
                .Append("<button data-action=\"layout\">布置图框</button>")
                .Append("<button data-action=\"place\">直接布框</button>")
                .Append("<button id=\"save\" class=\"primary\">保存设置</button></div></header>")
                .Append("<div class=\"fr-grid\"><section class=\"fr-card fr-library\">")
                .Append("<div class=\"fr-library-layout\"><div class=\"fr-library-tools\"><div id=\"paperTabs\" class=\"fr-tabs\"></div><div class=\"fr-view-buttons\">")
                .Append("<button id=\"singleView\" title=\"单列显示\" aria-label=\"单列显示\"><svg viewBox=\"0 0 18 18\"><rect x=\"2\" y=\"3\" width=\"14\" height=\"4\" rx=\"1\"/><rect x=\"2\" y=\"11\" width=\"14\" height=\"4\" rx=\"1\"/></svg></button>")
                .Append("<button id=\"doubleView\" title=\"双列显示\" aria-label=\"双列显示\"><svg viewBox=\"0 0 18 18\"><rect x=\"2\" y=\"3\" width=\"6\" height=\"4\" rx=\"1\"/><rect x=\"10\" y=\"3\" width=\"6\" height=\"4\" rx=\"1\"/><rect x=\"2\" y=\"11\" width=\"6\" height=\"4\" rx=\"1\"/><rect x=\"10\" y=\"11\" width=\"6\" height=\"4\" rx=\"1\"/></svg></button>")
                .Append("</div></div><div id=\"templateList\" class=\"fr-template-list\"></div></div></section>")
                .Append("<section class=\"fr-column\"><div class=\"fr-card\"><div class=\"fr-card-head\"><strong>裁图区域与向内留白</strong><div class=\"fr-inline-actions\"><button id=\"makeDefault\">设为默认</button><button id=\"deleteTemplate\" class=\"danger\">删除</button></div></div>")
                .Append("<div class=\"fr-card-body fr-template-editor\"><div class=\"fr-preview\" id=\"preview\"><div class=\"fr-sheet\"><svg id=\"previewGeometry\" class=\"fr-geometry\" viewBox=\"0 0 1000 1000\" preserveAspectRatio=\"none\"></svg><div class=\"fr-selected\"></div><div class=\"fr-valid\"></div><span id=\"previewName\"></span></div></div>")
                .Append("<div class=\"fr-template-options\"><div class=\"fr-fields two\"><label><span>模板名称</span><input id=\"templateName\"></label><label><span>图幅</span><select id=\"paperSize\"></select></label></div>")
                .Append("<div class=\"fr-fields two fr-margins\"><label><span>区域内上留白</span><input id=\"marginTop\" type=\"number\" min=\"0\" step=\"0.1\"></label><label><span>区域内右留白</span><input id=\"marginRight\" type=\"number\" min=\"0\" step=\"0.1\"></label><label><span>区域内下留白</span><input id=\"marginBottom\" type=\"number\" min=\"0\" step=\"0.1\"></label><label><span>区域内左留白</span><input id=\"marginLeft\" type=\"number\" min=\"0\" step=\"0.1\"></label></div></div></div></div>")
                .Append("<div class=\"fr-card fr-settings\"><div class=\"fr-card-head\"><strong>指北针与比例</strong></div><div class=\"fr-card-body fr-setting-grid\">")
                .Append("<div class=\"fr-setting-panel\"><label class=\"fr-switch\"><input id=\"drawNorth\" type=\"checkbox\"><span>绘制指北针</span></label>")
                .Append("<div class=\"fr-fields two\"><label><span>方向</span><select id=\"northDirection\"><option value=\"Auto\">自动确定</option><option value=\"WorldNorth\">图纸正北</option><option value=\"FrameUp\">图框向上</option></select></label><label><span>大小</span><input id=\"northSize\" type=\"number\" step=\"0.1\"></label><label><span>参考位置</span><select id=\"northRef\"></select></label><label><span>水平间距</span><input id=\"northX\" type=\"number\" step=\"0.1\"></label><label><span>竖向间距</span><input id=\"northY\" type=\"number\" step=\"0.1\"></label></div></div>")
                .Append("<div class=\"fr-setting-panel\"><label class=\"fr-switch\"><input id=\"drawScale\" type=\"checkbox\"><span>绘制比例标注</span></label>")
                .Append("<div class=\"fr-fields two\"><label><span>当前图纸比例</span><input id=\"scaleText\" readonly></label><label><span>文字高度</span><input id=\"scaleHeight\" type=\"number\" step=\"0.1\"></label><label><span>文字颜色</span><input id=\"scaleColor\" type=\"number\" min=\"1\" max=\"255\"></label><label><span>文字样式</span><select id=\"scaleStyle\"></select></label><label><span>参考位置</span><select id=\"scaleRef\"></select></label><label><span>水平间距</span><input id=\"scaleX\" type=\"number\" step=\"0.1\"></label><label><span>竖向间距</span><input id=\"scaleY\" type=\"number\" step=\"0.1\"></label></div></div>")
                .Append("</div></div><div class=\"fr-card\"><div class=\"fr-card-head\"><strong>布框设置</strong></div><div class=\"fr-card-body fr-fields three\"><label><span>每行图框数</span><input id=\"perRow\" type=\"number\" min=\"1\" max=\"100\"></label><label><span>水平间距</span><input id=\"horizontalGap\" type=\"number\" min=\"0\" step=\"0.1\"></label><label><span>竖向间距</span><input id=\"verticalGap\" type=\"number\" min=\"0\" step=\"0.1\"></label></div></div></section></div></main><div id=\"toast\" class=\"fr-toast\"></div><script>")
                .Append(BuildScript(embedded, initial))
                .Append("</script></body></html>");
            return html.ToString();
        }

        private static string BuildStyles()
        {
            return @"
:root{--bg:#f5f7fb;--panel:#fff;--panel2:#f8fafc;--text:#172033;--muted:#64748b;--line:#dce5f1;--brand:#326fea;--brand2:#7c3aed;--danger:#c2414d}
body[data-theme='fresh']{--bg:#f2f6ff;--panel2:#edf4ff;--line:#d7e5ff}
body[data-theme='dark']{--bg:#0f172a;--panel:#172033;--panel2:#111827;--text:#e5e7eb;--muted:#94a3b8;--line:#2b3a50;--brand:#60a5fa;--brand2:#a78bfa}
*{box-sizing:border-box}html,body{min-height:100%;margin:0;background:var(--bg);color:var(--text);font-family:'Microsoft YaHei UI','Segoe UI',sans-serif}button,input,select{font:inherit}.fr-page{min-height:100vh;padding:16px 18px 18px;background:linear-gradient(180deg,rgba(217,230,255,.45),transparent 260px),var(--bg)}
.fr-head{display:flex;justify-content:space-between;align-items:center;gap:15px;margin-bottom:12px}.fr-head h1{margin:0;font-size:23px}.fr-actions,.fr-inline-actions{display:flex;gap:7px;flex-wrap:wrap}button{height:34px;border:1px solid var(--line);border-radius:9px;padding:0 12px;background:var(--panel);color:var(--text);font-weight:720;cursor:pointer}button:hover{transform:translateY(-1px);border-color:var(--brand);box-shadow:0 7px 17px rgba(50,111,234,.12)}button.primary{height:38px;border:0;color:#fff;background:linear-gradient(135deg,var(--brand),var(--brand2));padding:0 19px}.danger{color:var(--danger)}
	.fr-grid{display:grid;grid-template-columns:minmax(420px,.9fr) minmax(600px,1.1fr);gap:12px;align-items:start}.fr-column{display:grid;gap:12px}.fr-card{border:1px solid var(--line);border-radius:12px;background:var(--panel);overflow:hidden;box-shadow:0 10px 25px rgba(30,50,90,.05)}.fr-card-head{min-height:45px;padding:9px 12px;display:flex;align-items:center;justify-content:space-between;gap:10px;background:var(--panel2);border-bottom:1px solid var(--line);font-size:14px}.fr-card-body{padding:12px}
	.fr-library{min-height:698px}.fr-library-layout{min-height:698px}.fr-library-tools{min-height:52px;display:flex;align-items:center;justify-content:space-between;gap:10px;padding:8px 10px;border-bottom:1px solid var(--line);background:var(--panel2)}.fr-tabs{display:flex;align-items:center;gap:6px;min-width:0;overflow-x:auto}.fr-tabs button{height:32px;min-width:38px;padding:0 9px;font-size:11px;flex:0 0 auto}.fr-tabs button.active{border-color:var(--brand);color:var(--brand);background:rgba(50,111,234,.1);box-shadow:0 0 0 2px rgba(50,111,234,.08)}.fr-view-buttons{display:flex;gap:5px;flex:0 0 auto}.fr-view-buttons button{display:grid;place-items:center;width:31px;height:29px;padding:0}.fr-view-buttons button.active{border-color:var(--brand);color:var(--brand);background:rgba(50,111,234,.1)}.fr-view-buttons svg{width:17px;height:17px;fill:currentColor}.fr-template-list{padding:11px;display:grid;grid-template-columns:1fr;gap:10px}.fr-template-list.double{grid-template-columns:repeat(2,minmax(0,1fr));align-items:start}.fr-template{min-width:0;display:grid;grid-template-columns:132px minmax(0,1fr);align-items:center;gap:12px;padding:10px;border:1px solid var(--line);border-radius:10px;background:var(--panel2);cursor:pointer}.fr-template-list.double .fr-template{display:flex;flex-direction:column;align-items:stretch;gap:8px;padding:9px}.fr-template:hover,.fr-template.active{border-color:var(--brand);box-shadow:0 0 0 3px rgba(50,111,234,.08)}.fr-mini-sheet{position:relative;width:132px;aspect-ratio:1.48;border:1px solid var(--muted);background:var(--panel);overflow:hidden}.fr-template-list.double .fr-mini-sheet{width:100%;aspect-ratio:1.48;min-height:104px}.fr-mini-sheet i{position:absolute;border:1px solid #ef4444;z-index:2}.fr-template strong{display:block;margin:2px 0 5px;font-size:13px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.fr-template small{display:block;color:var(--muted);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.fr-template-list.double .fr-template>div:last-child{min-width:0}.fr-default{display:inline-block;margin-left:6px;padding:2px 5px;border-radius:5px;background:rgba(50,111,234,.11);color:var(--brand);font-size:10px}.fr-empty{grid-column:1/-1;padding:80px 15px;text-align:center;color:var(--muted)}
	.fr-template-editor{display:grid;grid-template-columns:minmax(260px,.9fr) minmax(300px,1.1fr);gap:12px;align-items:stretch}.fr-preview{min-height:190px;height:100%;display:flex;align-items:center;justify-content:center;border:1px solid var(--line);border-radius:10px;background:linear-gradient(45deg,var(--panel2) 25%,transparent 25%,transparent 75%,var(--panel2) 75%),linear-gradient(45deg,var(--panel2) 25%,transparent 25%,transparent 75%,var(--panel2) 75%);background-size:16px 16px;background-position:0 0,8px 8px}.fr-template-options{display:flex;flex-direction:column;justify-content:center;min-width:0}.fr-margins{margin-top:12px}.fr-sheet{position:relative;width:280px;height:180px;border:1px solid var(--text);background:var(--panel);overflow:hidden}.fr-geometry{position:absolute;inset:0;width:100%;height:100%;z-index:1}.fr-geometry line{stroke:var(--text);stroke-width:2;vector-effect:non-scaling-stroke}.fr-mini-sheet .fr-geometry line{stroke-width:1}.fr-selected,.fr-valid{position:absolute;z-index:2;pointer-events:none}.fr-selected{border:1px dashed #f59e0b}.fr-valid{border:2px solid #ef4444}.fr-sheet>span{position:absolute;left:50%;top:50%;z-index:3;transform:translate(-50%,-50%);padding:3px 6px;border-radius:5px;background:color-mix(in srgb,var(--panel) 80%,transparent);color:var(--muted);font-size:11px;white-space:nowrap}
	.fr-fields{display:grid;gap:9px}.fr-fields.two{grid-template-columns:repeat(2,minmax(0,1fr))}.fr-fields.three{grid-template-columns:repeat(3,minmax(0,1fr))}.fr-fields.four{grid-template-columns:repeat(4,minmax(0,1fr));margin-top:9px}.fr-fields label{display:grid;gap:5px;min-width:0}.fr-fields label>span{font-size:10px;font-weight:750;color:var(--muted)}input,select{width:100%;height:34px;border:1px solid var(--line);border-radius:8px;background:var(--panel2);color:var(--text);padding:0 9px;outline:0}input:focus,select:focus{border-color:var(--brand);box-shadow:0 0 0 3px rgba(50,111,234,.09)}
	.fr-setting-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.fr-setting-panel{position:relative;padding:11px;border:1px solid var(--line);border-radius:10px;background:var(--panel2)}.fr-switch{display:flex;align-items:center;gap:8px;margin-bottom:10px;font-weight:800;font-size:13px}.fr-switch input{width:17px;height:17px;accent-color:var(--brand)}
	.fr-toast{display:none;position:fixed;right:18px;bottom:18px;z-index:80;padding:10px 13px;border-radius:9px;background:#172033;color:#fff;box-shadow:0 14px 34px rgba(15,23,42,.24)}.fr-toast.show{display:block}.fr-toast.error{background:#b91c1c}.no-animations *{transition:none!important;animation:none!important}
	@media(max-width:1050px){.fr-grid{grid-template-columns:1fr}.fr-library,.fr-library-layout{min-height:0}.fr-setting-grid{grid-template-columns:1fr}}@media(max-width:720px){.fr-head{align-items:flex-start;flex-direction:column}.fr-template-editor{grid-template-columns:1fr}.fr-fields.two,.fr-fields.three,.fr-fields.four{grid-template-columns:1fr 1fr}.fr-template-list.double{grid-template-columns:1fr}}";
        }

        private static string BuildScript(bool embedded, string initialJson)
        {
            return @"
(function(){
var embedded=" + (embedded ? "true" : "false") + @",ctx=" + initialJson + @",paper='A3',selectedId='',toastTimer=0,templateView=String((ctx.settings||{}).TemplateViewMode||'Double').toLowerCase()==='single'?'single':'double';
function post(n,a){try{if(embedded&&window.parent&&window.parent.CDBoxStudioPost){window.parent.CDBoxStudioPost(n,a||'');return;}chrome.webview.postMessage('studio|'+n+'|'+encodeURIComponent(a||''));}catch(ex){}}
function byId(id){return document.getElementById(id)}function num(id){return Number(byId(id).value||0)}function esc(v){return String(v==null?'':v).replace(/[&<>""']/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;','""':'&quot;',""'"":'&#39;'}[c]||c})}
function showToast(text,kind){var t=byId('toast');t.textContent=text||'';t.className='fr-toast show '+(kind||'');clearTimeout(toastTimer);toastTimer=setTimeout(function(){t.className='fr-toast'},2200)}
function current(){return (ctx.templates||[]).find(function(x){return x.Id===selectedId})||null}
function fillSelect(id,items,value){var x=byId(id);x.innerHTML='';items.forEach(function(item){var o=document.createElement('option');o.value=item.value==null?item:item.value;o.textContent=item.text==null?item:item.text;x.appendChild(o)});if(value!=null)x.value=value}
function paperNames(){var names=(ctx.papers||[]).map(function(x){return x.Name});(ctx.templates||[]).forEach(function(x){if(names.indexOf(x.PaperSize)<0)names.push(x.PaperSize)});return names}
	var refs=[{value:'TopLeft',text:'裁图区域左上角'},{value:'TopCenter',text:'裁图区域上中'},{value:'TopRight',text:'裁图区域右上角'},{value:'MiddleLeft',text:'裁图区域左中'},{value:'Center',text:'裁图区域中心'},{value:'MiddleRight',text:'裁图区域右中'},{value:'BottomLeft',text:'裁图区域左下角'},{value:'BottomCenter',text:'裁图区域下中'},{value:'BottomRight',text:'裁图区域右下角'}];
function renderTabs(){var tabs=byId('paperTabs'),available=paperNames();tabs.innerHTML=available.map(function(x){return '<button class=""'+(paper===x?'active':'')+'"" data-paper=""'+esc(x)+'"">'+esc(x)+'</button>'}).join('');tabs.querySelectorAll('button').forEach(function(b){b.onclick=function(){paper=b.dataset.paper;var first=(ctx.templates||[]).find(function(x){return x.PaperSize===paper});selectedId=first?first.Id:'';renderTabs();renderList();renderTemplate()}})}
function geometry(t){var list=t&&t.PreviewSegments||[],minX=Number(t&&t.FrameMinX||0),maxX=Number(t&&t.FrameMaxX||1),minY=Number(t&&t.FrameMinY||0),maxY=Number(t&&t.FrameMaxY||1),w=Math.max(.000001,maxX-minX),h=Math.max(.000001,maxY-minY);return list.slice(0,3000).map(function(s){var x1=1000*(Number(s.X1)-minX)/w,x2=1000*(Number(s.X2)-minX)/w,y1=1000*(maxY-Number(s.Y1))/h,y2=1000*(maxY-Number(s.Y2))/h;if(!isFinite(x1+x2+y1+y2))return '';return '<line x1=""'+x1.toFixed(2)+'"" y1=""'+y1.toFixed(2)+'"" x2=""'+x2.toFixed(2)+'"" y2=""'+y2.toFixed(2)+'""></line>'}).join('')}
	function box(t,effective){var sx=Math.max(.000001,Math.abs(Number(t.ScaleX)||1)),sy=Math.max(.000001,Math.abs(Number(t.ScaleY)||1)),fminX=Math.min(Number(t.FrameMinX),Number(t.FrameMaxX)),fmaxX=Math.max(Number(t.FrameMinX),Number(t.FrameMaxX)),fminY=Math.min(Number(t.FrameMinY),Number(t.FrameMaxY)),fmaxY=Math.max(Number(t.FrameMinY),Number(t.FrameMaxY)),minX=effective?Number(t.EffectiveValidMinX):Math.min(Number(t.ValidMinX),Number(t.ValidMaxX)),maxX=effective?Number(t.EffectiveValidMaxX):Math.max(Number(t.ValidMinX),Number(t.ValidMaxX)),minY=effective?Number(t.EffectiveValidMinY):Math.min(Number(t.ValidMinY),Number(t.ValidMaxY)),maxY=effective?Number(t.EffectiveValidMaxY):Math.max(Number(t.ValidMinY),Number(t.ValidMaxY)),fw=Math.max(.001,(fmaxX-fminX)*sx),fh=Math.max(.001,(fmaxY-fminY)*sy);return {left:Math.max(0,100*(minX-fminX)*sx/fw),right:Math.max(0,100*(fmaxX-maxX)*sx/fw),top:Math.max(0,100*(fmaxY-maxY)*sy/fh),bottom:Math.max(0,100*(minY-fminY)*sy/fh)}}
	function cssBox(b){return 'left:'+b.left+'%;right:'+b.right+'%;top:'+b.top+'%;bottom:'+b.bottom+'%'}
	function mini(t){return '<div class=""fr-mini-sheet""><svg class=""fr-geometry"" viewBox=""0 0 1000 1000"" preserveAspectRatio=""none"">'+geometry(t)+'</svg><i style=""'+cssBox(box(t,true))+'""></i></div>'}
function renderViewButtons(){var list=byId('templateList');list.classList.toggle('double',templateView==='double');byId('singleView').classList.toggle('active',templateView==='single');byId('doubleView').classList.toggle('active',templateView==='double')}
function setTemplateView(mode){templateView=mode==='single'?'single':'double';ctx.settings=ctx.settings||{};ctx.settings.TemplateViewMode=templateView==='single'?'Single':'Double';renderViewButtons();post('saveFrameTemplateView',templateView)}
function renderList(){var list=byId('templateList'),items=(ctx.templates||[]).filter(function(x){return x.PaperSize===paper});renderViewButtons();if(!items.length){list.innerHTML='<div class=""fr-empty"">当前图幅暂无模板</div>';return}if(!items.some(function(x){return x.Id===selectedId}))selectedId=items[0].Id;list.innerHTML=items.map(function(t){var count=Number(t.PreviewEntityCount||0);return '<article class=""fr-template '+(t.Id===selectedId?'active':'')+'"" data-id=""'+t.Id+'"">'+mini(t)+'<div><strong>'+esc(t.TemplateName)+(t.IsDefault?'<em class=""fr-default"">默认</em>':'')+'</strong><small>'+Number(t.FrameWidth||0).toFixed(2)+' × '+Number(t.FrameHeight||0).toFixed(2)+(count?' · '+count+' 个实体':'')+'</small></div></article>'}).join('');list.querySelectorAll('[data-id]').forEach(function(x){x.onclick=function(){selectedId=x.dataset.id;renderList();renderTemplate()}})}
	function renderPreview(t){var preview=byId('preview'),selected=preview.querySelector('.fr-selected'),valid=preview.querySelector('.fr-valid'),sheet=preview.querySelector('.fr-sheet'),svg=byId('previewGeometry');if(!t){sheet.style.opacity='.35';selected.style.cssText='display:none';valid.style.cssText='display:none';svg.innerHTML='';byId('previewName').textContent='';return}sheet.style.opacity='1';var fw=Math.max(.001,t.FrameWidth||1),fh=Math.max(.001,t.FrameHeight||1),ratio=fw/fh,maxW=Math.max(160,Math.min(330,(preview.clientWidth||354)-24)),maxH=Math.max(120,Math.min(190,(preview.clientHeight||214)-24)),w=maxW,h=maxW/ratio;if(h>maxH){h=maxH;w=maxH*ratio}sheet.style.width=Math.max(80,w)+'px';sheet.style.height=Math.max(60,h)+'px';svg.innerHTML=geometry(t);selected.style.cssText='display:block;'+cssBox(box(t,false));valid.style.cssText='display:block;'+cssBox(box(t,true));byId('previewName').textContent=(t.PreviewSegments&&t.PreviewSegments.length)?'':'未读取到几何预览'}
function renderTemplate(){var t=current(),disabled=!t;['templateName','paperSize','marginTop','marginRight','marginBottom','marginLeft','makeDefault','deleteTemplate'].forEach(function(id){byId(id).disabled=disabled});renderPreview(t);if(!t){byId('templateName').value='';return}byId('templateName').value=t.TemplateName||'';byId('paperSize').value=t.PaperSize||'A3';byId('marginTop').value=Number(t.MarginTop||0).toFixed(3);byId('marginRight').value=Number(t.MarginRight||0).toFixed(3);byId('marginBottom').value=Number(t.MarginBottom||0).toFixed(3);byId('marginLeft').value=Number(t.MarginLeft||0).toFixed(3);byId('makeDefault').disabled=!!t.IsDefault}
function renderSettings(){var s=ctx.settings||{};byId('drawNorth').checked=!!s.DrawNorthArrow;byId('northDirection').value=s.NorthDirectionMode||'Auto';byId('northRef').value=s.NorthReferencePosition||'TopRight';byId('northX').value=s.NorthOffsetX;byId('northY').value=s.NorthOffsetY;byId('northSize').value=s.NorthSize;byId('drawScale').checked=!!s.DrawScaleLabel;byId('scaleText').value=s.ScaleText||'1:500';byId('scaleHeight').value=s.ScaleTextHeight;byId('scaleColor').value=s.ScaleColorIndex;byId('scaleStyle').value=s.ScaleTextStyle||'Standard';byId('scaleRef').value=s.ScaleReferencePosition||'TopRight';byId('scaleX').value=s.ScaleOffsetX;byId('scaleY').value=s.ScaleOffsetY;byId('perRow').value=s.FramesPerRow;byId('horizontalGap').value=s.HorizontalGap;byId('verticalGap').value=s.VerticalGap}
function render(){renderTabs();renderList();renderTemplate();renderSettings();renderViewButtons()}
function payload(){var t=current();return {templateId:t?t.Id:'',templateName:byId('templateName').value,paperSize:byId('paperSize').value,marginTop:num('marginTop'),marginRight:num('marginRight'),marginBottom:num('marginBottom'),marginLeft:num('marginLeft'),settings:{DrawNorthArrow:byId('drawNorth').checked,NorthDirectionMode:byId('northDirection').value,NorthReferencePosition:byId('northRef').value,NorthOffsetX:num('northX'),NorthOffsetY:num('northY'),NorthSize:num('northSize'),DrawScaleLabel:byId('drawScale').checked,ScaleText:byId('scaleText').value,ScaleTextHeight:num('scaleHeight'),ScaleColorIndex:num('scaleColor'),ScaleTextStyle:byId('scaleStyle').value,ScaleReferencePosition:byId('scaleRef').value,ScaleOffsetX:num('scaleX'),ScaleOffsetY:num('scaleY'),FramesPerRow:num('perRow'),HorizontalGap:num('horizontalGap'),VerticalGap:num('verticalGap'),TemplateViewMode:templateView==='single'?'Single':'Double'}}}
fillSelect('paperSize',paperNames(),'A3');fillSelect('northRef',refs,'TopRight');fillSelect('scaleRef',refs,'TopRight');fillSelect('scaleStyle',(ctx.textStyles||[]),'Standard');
document.querySelectorAll('[data-action]').forEach(function(x){x.onclick=function(){post('runFrameAction',x.dataset.action)}});byId('save').onclick=function(){post('saveFrameSettings',JSON.stringify(payload()))};byId('singleView').onclick=function(){setTemplateView('single')};byId('doubleView').onclick=function(){setTemplateView('double')};byId('deleteTemplate').onclick=function(){var t=current();if(t&&confirm('删除模板“'+t.TemplateName+'”？'))post('deleteFrameTemplate',t.Id)};byId('makeDefault').onclick=function(){var t=current();if(t)post('setDefaultFrameTemplate',t.Id)};
function load(data){var old=selectedId;ctx=data||ctx;templateView=String((ctx.settings||{}).TemplateViewMode||'Double').toLowerCase()==='single'?'single':'double';if(old&&(ctx.templates||[]).some(function(x){return x.Id===old}))selectedId=old;else{var first=(ctx.templates||[]).find(function(x){return x.PaperSize===paper})||(ctx.templates||[])[0];selectedId=first?first.Id:'';if(first)paper=first.PaperSize}fillSelect('paperSize',paperNames(),paper);fillSelect('scaleStyle',(ctx.textStyles||[]),'Standard');render()}
window.CDBoxFrameSettingsLoad=load;window.CDBoxFrameSettingsSaved=function(data){load(data);showToast('设置已保存','success')};window.CDBoxFrameSettingsError=function(message){showToast(message,'error')};if(embedded&&window.parent){window.parent.CDBoxFrameSettingsLoad=load;window.parent.CDBoxFrameSettingsSaved=window.CDBoxFrameSettingsSaved;window.parent.CDBoxFrameSettingsError=window.CDBoxFrameSettingsError}
load(ctx);post('getFrameSettings','');
})();";
        }

        private static IList<string> LoadTextStyles()
        {
            List<string> result = new List<string>();
            Document document = Autodesk.AutoCAD.ApplicationServices.Application
                .DocumentManager.MdiActiveDocument;
            try
            {
                if (document != null)
                {
                    using (Transaction transaction = document.Database
                        .TransactionManager.StartOpenCloseTransaction())
                    {
                        TextStyleTable table = (TextStyleTable)transaction.GetObject(
                            document.Database.TextStyleTableId, OpenMode.ForRead);
                        foreach (ObjectId id in table)
                        {
                            TextStyleTableRecord record = transaction.GetObject(id,
                                OpenMode.ForRead, false) as TextStyleTableRecord;
                            if (record != null && !record.IsErased
                                && !string.IsNullOrWhiteSpace(record.Name))
                                result.Add(record.Name);
                        }
                        transaction.Commit();
                    }
                }
            }
            catch { }
            if (!result.Any(x => string.Equals(x, "Standard",
                StringComparison.OrdinalIgnoreCase))) result.Add("Standard");
            result.Sort(StringComparer.CurrentCultureIgnoreCase);
            return result;
        }

        private static string Html(string value)
        {
            return (value ?? string.Empty).Replace("&", "&amp;")
                .Replace("\"", "&quot;").Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
