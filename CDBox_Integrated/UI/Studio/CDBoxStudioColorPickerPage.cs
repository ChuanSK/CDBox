using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using TCPipeAutoDraw.Core.Colors;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioColorPickerOptions
    {
        public bool AllowByLayer { get; set; }
        public bool AllowByBlock { get; set; }
        public bool AllowTrueColor { get; set; }
        public bool AllowColorBook { get; set; }
        public bool AllowStandard { get; set; }
    }

    internal static class CDBoxStudioColorPickerPage
    {
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static string BuildStandaloneDocument(CDBoxStudioSettings settings,
            CDBoxColor initial, CDBoxStudioColorPickerOptions options)
        {
            settings = settings ?? new CDBoxStudioSettings();
            settings.Normalize();
            initial = (initial ?? CDBoxColor.FromIndex(7)).Clone();
            options = options ?? new CDBoxStudioColorPickerOptions
            {
                AllowByLayer = true,
                AllowByBlock = true,
                AllowTrueColor = true,
                AllowColorBook = true,
                AllowStandard = true
            };

            var context = new ColorPickerContext
            {
                initial = initial,
                options = options,
                palette = new List<CDBoxColor>(CDBoxColorService.GetAciPalette()),
                standards = new List<CDBoxColor>(CDBoxStandardColorService.GetColors())
            };

            var html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
                .Append("<title>CDBox 颜色选择器</title><style>");
            html.Append(@"
:root{--bg:#f5f7fb;--panel:#fff;--panel2:#f8fafc;--text:#172033;--muted:#64748b;--line:#dce5f1;--brand:#326fea;--brand2:#7c3aed;--danger:#b91c1c}
body[data-theme='fresh']{--bg:#f2f6ff;--panel2:#edf4ff;--line:#d7e5ff}
body[data-theme='dark']{--bg:#0f172a;--panel:#172033;--panel2:#111827;--text:#e5e7eb;--muted:#94a3b8;--line:#2b3a50;--brand:#60a5fa;--brand2:#a78bfa}
*{box-sizing:border-box}html,body{height:100%;margin:0;overflow:hidden;background:var(--bg);color:var(--text);font-family:'Microsoft YaHei UI','Segoe UI',sans-serif}
button,input{font:inherit}.cp-page{height:100%;display:flex;flex-direction:column;gap:12px;padding:16px 18px 18px;background:linear-gradient(180deg,rgba(217,230,255,.46),transparent 230px),var(--bg)}
.cp-head{display:flex;align-items:center;justify-content:space-between;gap:16px;flex:0 0 auto}.cp-head h1{margin:0;font-size:23px}.cp-head p{margin:5px 0 0;color:var(--muted);font-size:12px}
.cp-preview{min-width:290px;display:flex;align-items:center;gap:11px;padding:9px 12px;border:1px solid var(--line);border-radius:12px;background:var(--panel);box-shadow:0 8px 20px rgba(30,50,90,.06)}
.cp-swatch{width:54px;height:38px;border:1px solid rgba(15,23,42,.25);border-radius:8px;flex:0 0 auto}.cp-preview-text{min-width:0}.cp-preview-text strong,.cp-preview-text small{display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.cp-preview-text small{margin-top:3px;color:var(--muted)}
.cp-card{min-height:0;flex:1 1 auto;display:flex;flex-direction:column;border:1px solid var(--line);border-radius:13px;background:var(--panel);box-shadow:0 12px 28px rgba(30,41,59,.07);overflow:hidden}
.cp-tabs{display:flex;gap:5px;padding:9px 10px;border-bottom:1px solid var(--line);background:var(--panel2)}.cp-tab{height:34px;border:1px solid transparent;border-radius:9px;background:transparent;color:var(--muted);padding:0 13px;font-weight:750;cursor:pointer}.cp-tab:hover{color:var(--text);background:var(--panel)}.cp-tab.active{border-color:var(--line);background:var(--panel);color:var(--brand);box-shadow:0 5px 14px rgba(30,50,90,.06)}
.cp-panels{min-height:0;flex:1 1 auto}.cp-panel{display:none;height:100%;overflow:auto;padding:14px}.cp-panel.active{display:block}
.cp-special{display:flex;gap:8px;margin-bottom:11px}.cp-btn{height:36px;border:1px solid var(--line);border-radius:10px;background:var(--panel);color:var(--text);padding:0 13px;font-weight:750;cursor:pointer}.cp-btn:hover{transform:translateY(-1px);border-color:rgba(50,111,234,.55);box-shadow:0 8px 18px rgba(50,111,234,.10)}.cp-btn.primary{border:0;color:#fff;background:linear-gradient(135deg,var(--brand),var(--brand2));box-shadow:0 9px 20px rgba(50,111,234,.20)}
.cp-palette{display:grid;grid-template-columns:repeat(auto-fill,minmax(30px,1fr));gap:5px}.cp-color{aspect-ratio:1;min-height:26px;border:1px solid rgba(15,23,42,.24);border-radius:6px;cursor:pointer;outline:0}.cp-color:hover{transform:scale(1.12);z-index:2;box-shadow:0 7px 15px rgba(15,23,42,.22)}.cp-color.selected{box-shadow:0 0 0 3px var(--panel),0 0 0 6px var(--brand);z-index:3}
.cp-true{display:grid;grid-template-columns:minmax(330px,1fr) 280px;gap:18px}.cp-sliders,.cp-fields,.cp-book{padding:15px;border:1px solid var(--line);border-radius:12px;background:var(--panel2)}.cp-field{display:grid;gap:6px;margin-bottom:12px}.cp-field>span{display:flex;justify-content:space-between;color:var(--muted);font-size:12px;font-weight:750}.cp-field input[type=range]{width:100%;accent-color:var(--brand)}.cp-field input[type=text],.cp-field input[type=number]{width:100%;height:38px;border:1px solid var(--line);border-radius:9px;background:var(--panel);color:var(--text);padding:0 10px;outline:0}.cp-field input:focus{border-color:var(--brand);box-shadow:0 0 0 3px rgba(50,111,234,.10)}
.cp-rgb{display:grid;grid-template-columns:repeat(3,1fr);gap:8px}.cp-hue{height:10px;border-radius:8px;background:linear-gradient(90deg,#f00,#ff0,#0f0,#0ff,#00f,#f0f,#f00)}
.cp-book{max-width:720px}.cp-note{color:var(--muted);font-size:12px;line-height:1.7}.cp-standard{display:grid;grid-template-columns:repeat(auto-fill,minmax(230px,1fr));gap:9px}.cp-standard-item{display:grid;grid-template-columns:32px 1fr auto;align-items:center;gap:10px;min-height:54px;padding:8px 10px;border:1px solid var(--line);border-radius:11px;background:var(--panel2);cursor:pointer}.cp-standard-item:hover,.cp-standard-item.selected{border-color:var(--brand);box-shadow:0 0 0 3px rgba(50,111,234,.09)}.cp-standard-item i{width:28px;height:28px;border:1px solid rgba(15,23,42,.22);border-radius:7px}.cp-standard-item strong{font-size:13px}.cp-standard-item small{color:var(--muted)}
.cp-foot{display:flex;align-items:center;justify-content:space-between;gap:12px;flex:0 0 auto}.cp-hint{color:var(--muted);font-size:12px}.cp-actions{display:flex;gap:8px}.cp-toast{position:fixed;right:20px;bottom:20px;max-width:420px;padding:11px 14px;border-radius:11px;background:#172033;color:#fff;box-shadow:0 16px 38px rgba(15,23,42,.22);z-index:20}.cp-toast.error{background:#b91c1c}
.no-animations *{animation:none!important;transition:none!important}@media(max-width:760px){.cp-page{overflow:auto}.cp-head{align-items:flex-start;flex-direction:column}.cp-preview{width:100%}.cp-card{min-height:560px}.cp-true{grid-template-columns:1fr}.cp-foot{align-items:flex-start;flex-direction:column}.cp-actions{align-self:flex-end}}
");
            html.Append("</style></head><body data-theme=\"").Append(Html(settings.Theme))
                .Append("\" class=\"").Append(settings.AnimationsEnabled ? string.Empty : "no-animations")
                .Append("\"><div id=\"app\"></div><script>window.CDBoxColorPickerContext=")
                .Append(Serializer.Serialize(context)).Append(";</script><script>");
            html.Append(@"
(function(){
var ctx=window.CDBoxColorPickerContext||{},opt=ctx.options||{},candidate=clone(ctx.initial||{}),active='';
function clone(v){return JSON.parse(JSON.stringify(v||{}));}
function esc(v){return String(v==null?'':v).replace(/[&<>']/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;',""'"":'&#39;'}[c]||c;});}
function post(n,a){try{chrome.webview.postMessage('studio|'+n+'|'+encodeURIComponent(a||''));}catch(ex){}}
function clamp(v,a,b){v=Number(v);return isFinite(v)?Math.max(a,Math.min(b,v)):a;}
function hex(c){return '#'+[c.R,c.G,c.B].map(function(x){return Math.round(clamp(x,0,255)).toString(16).padStart(2,'0');}).join('').toUpperCase();}
function name(c){if(c.Type===0)return '随层';if(c.Type===1)return '随块';if(c.DisplayName)return c.DisplayName;if(c.Type===2)return 'ACI '+c.Index;if(c.Type===4)return (c.BookName?c.BookName+' · ':'')+(c.ColorName||'配色系统');return 'RGB '+c.R+', '+c.G+', '+c.B;}
function detail(c){return hex(c)+' · RGB '+c.R+', '+c.G+', '+c.B+(c.Type===2?' · ACI '+c.Index:'');}
function initialTab(){if(candidate.Type===3&&opt.AllowTrueColor)return 'true';if(candidate.Type===4&&opt.AllowColorBook)return 'book';if(candidate.Type===5&&opt.AllowStandard)return 'standard';return 'aci';}
function tabButton(id,label,show){return show?`<button class='cp-tab' data-tab='${id}'>${label}</button>`:'';}
function render(){
 active=initialTab();
 document.getElementById('app').innerHTML=`<main class='cp-page'><header class='cp-head'><div><h1>选择颜色</h1><p>ACI、真彩色、配色系统和工程标准色使用同一颜色模型。</p></div><div class='cp-preview'><i class='cp-swatch' data-preview></i><div class='cp-preview-text'><strong data-name></strong><small data-detail></small></div></div></header><section class='cp-card'><nav class='cp-tabs'>${tabButton('aci','索引颜色',true)}${tabButton('true','真彩色',opt.AllowTrueColor)}${tabButton('book','配色系统',opt.AllowColorBook)}${tabButton('standard','CDBox 标准',opt.AllowStandard)}</nav><div class='cp-panels'><div class='cp-panel' data-panel='aci'>${aciPanel()}</div>${opt.AllowTrueColor?`<div class='cp-panel' data-panel='true'>${truePanel()}</div>`:''}${opt.AllowColorBook?`<div class='cp-panel' data-panel='book'>${bookPanel()}</div>`:''}${opt.AllowStandard?`<div class='cp-panel' data-panel='standard'>${standardPanel()}</div>`:''}</div></section><footer class='cp-foot'><span class='cp-hint'>悬停查看颜色信息；双击颜色可直接确认。</span><div class='cp-actions'><button class='cp-btn' data-act='cancel'>取消</button><button class='cp-btn primary' data-act='confirm'>确定</button></div></footer></main>`;
 bind();setTab(active);sync();
}
function aciPanel(){var special=`<div class='cp-special'>${opt.AllowByLayer?`<button class='cp-btn' data-special='layer'>随层</button>`:''}${opt.AllowByBlock?`<button class='cp-btn' data-special='block'>随块</button>`:''}</div>`;return special+`<div class='cp-palette'>${(ctx.palette||[]).map(function(c){return `<button class='cp-color' data-aci='${c.Index}' title='ACI ${c.Index} · RGB ${c.R}, ${c.G}, ${c.B} · ${hex(c)}' style='background:${hex(c)}'></button>`;}).join('')}</div>`;}
function truePanel(){return `<div class='cp-true'><div class='cp-sliders'><label class='cp-field'><span>色相 H <output data-out='h'>0°</output></span><input class='cp-hue' data-hsv='h' type='range' min='0' max='359' step='1'></label><label class='cp-field'><span>饱和度 S <output data-out='s'>0%</output></span><input data-hsv='s' type='range' min='0' max='100' step='1'></label><label class='cp-field'><span>明度 V <output data-out='v'>100%</output></span><input data-hsv='v' type='range' min='0' max='100' step='1'></label><p class='cp-note'>拖动 H / S / V 后，RGB、HEX 与顶部预览会同步更新。</p></div><div class='cp-fields'><div class='cp-rgb'>${['R','G','B'].map(function(x){return `<label class='cp-field'><span>${x}</span><input data-rgb='${x}' type='number' min='0' max='255'></label>`;}).join('')}</div><label class='cp-field'><span>HEX</span><input data-hex type='text' maxlength='7' placeholder='#FFFFFF'></label></div></div>`;}
function bookPanel(){return `<div class='cp-book'><label class='cp-field'><span>配色系统</span><input data-book='BookName' type='text' placeholder='例如 RAL'></label><label class='cp-field'><span>颜色名称</span><input data-book='ColorName' type='text' placeholder='例如 2004'></label><button class='cp-btn' data-act='browseBook'>从 AutoCAD 配色系统选择…</button><p class='cp-note'>优先使用 AutoCAD 已安装的配色系统。目标电脑无法解析色册时，将使用当前 RGB 作为显示回退色。</p></div>`;}
function standardPanel(){return `<div class='cp-standard'>${(ctx.standards||[]).map(function(c,i){return `<button class='cp-standard-item' data-standard='${i}'><i style='background:${hex(c)}'></i><strong>${esc(name(c))}</strong><small>${esc(c.Category||'未分类')}</small></button>`;}).join('')}</div>`;}
function bind(){
 document.querySelectorAll('[data-tab]').forEach(function(x){x.onclick=function(){setTab(x.dataset.tab);};});
 document.querySelectorAll('[data-aci]').forEach(function(x){x.onclick=function(){var i=Number(x.dataset.aci);candidate=clone((ctx.palette||[]).find(function(c){return Number(c.Index)===i;}));sync();};x.ondblclick=function(){x.onclick();confirm();};});
 document.querySelectorAll('[data-standard]').forEach(function(x){x.onclick=function(){candidate=clone(ctx.standards[Number(x.dataset.standard)]);sync();};x.ondblclick=function(){x.onclick();confirm();};});
 document.querySelectorAll('[data-special]').forEach(function(x){x.onclick=function(){candidate=x.dataset.special==='layer'?{Type:0,Index:256,R:255,G:255,B:255,DisplayName:'随层'}:{Type:1,Index:0,R:255,G:255,B:255,DisplayName:'随块'};sync();};});
 document.querySelectorAll('[data-hsv]').forEach(function(x){x.oninput=applyHsv;});
 document.querySelectorAll('[data-rgb]').forEach(function(x){x.oninput=applyRgb;});
 var hx=document.querySelector('[data-hex]');if(hx){hx.onchange=applyHex;hx.onkeydown=function(e){if(e.key==='Enter'){applyHex();e.preventDefault();}};}
 document.querySelectorAll('[data-book]').forEach(function(x){x.oninput=function(){candidate.Type=4;candidate[x.dataset.book]=x.value;candidate.DisplayName=(candidate.BookName?candidate.BookName+' · ':'')+(candidate.ColorName||'配色系统');syncPreview();};});
 document.querySelector('[data-act=cancel]').onclick=function(){post('cancelColorPicker','');};
 document.querySelector('[data-act=confirm]').onclick=confirm;
 var browse=document.querySelector('[data-act=browseBook]');if(browse)browse.onclick=function(){post('browseCadColorBook',JSON.stringify(candidate));};
}
function setTab(id){active=id;document.querySelectorAll('[data-tab]').forEach(function(x){x.classList.toggle('active',x.dataset.tab===id);});document.querySelectorAll('[data-panel]').forEach(function(x){x.classList.toggle('active',x.dataset.panel===id);});if(id==='true'&&candidate.Type!==3){candidate={Type:3,Index:0,R:candidate.R||255,G:candidate.G||255,B:candidate.B||255,DisplayName:''};sync();}}
function sync(){syncPreview();document.querySelectorAll('[data-aci]').forEach(function(x){x.classList.toggle('selected',candidate.Type===2&&Number(x.dataset.aci)===Number(candidate.Index));});document.querySelectorAll('[data-standard]').forEach(function(x){var c=ctx.standards[Number(x.dataset.standard)]||{};x.classList.toggle('selected',candidate.Type===5&&c.DisplayName===candidate.DisplayName);});var hsv=rgbToHsv(candidate.R,candidate.G,candidate.B);set('[data-hsv=h]',Math.round(hsv[0]));set('[data-hsv=s]',Math.round(hsv[1]*100));set('[data-hsv=v]',Math.round(hsv[2]*100));set('[data-rgb=R]',candidate.R);set('[data-rgb=G]',candidate.G);set('[data-rgb=B]',candidate.B);set('[data-hex]',hex(candidate));set('[data-book=BookName]',candidate.BookName||'');set('[data-book=ColorName]',candidate.ColorName||'');outputs();}
function syncPreview(){var sw=document.querySelector('[data-preview]'),nm=document.querySelector('[data-name]'),dt=document.querySelector('[data-detail]');if(sw)sw.style.background=hex(candidate);if(nm)nm.textContent=name(candidate);if(dt)dt.textContent=detail(candidate);}
function set(q,v){var x=document.querySelector(q);if(x&&document.activeElement!==x)x.value=v;}
function outputs(){['h','s','v'].forEach(function(k){var i=document.querySelector('[data-hsv='+k+']'),o=document.querySelector('[data-out='+k+']');if(o&&i)o.textContent=i.value+(k==='h'?'°':'%');});}
function applyHsv(){var h=Number(document.querySelector('[data-hsv=h]').value),s=Number(document.querySelector('[data-hsv=s]').value)/100,v=Number(document.querySelector('[data-hsv=v]').value)/100,r= hsvToRgb(h,s,v);candidate={Type:3,Index:0,R:r[0],G:r[1],B:r[2],DisplayName:''};sync();}
function applyRgb(){candidate={Type:3,Index:0,R:Math.round(clamp(document.querySelector('[data-rgb=R]').value,0,255)),G:Math.round(clamp(document.querySelector('[data-rgb=G]').value,0,255)),B:Math.round(clamp(document.querySelector('[data-rgb=B]').value,0,255)),DisplayName:''};sync();}
function applyHex(){var v=(document.querySelector('[data-hex]').value||'').trim(),m=v.match(/^#?([0-9a-f]{6})$/i);if(!m){toast('请输入 #RRGGBB 格式','error');return;}candidate={Type:3,Index:0,R:parseInt(m[1].slice(0,2),16),G:parseInt(m[1].slice(2,4),16),B:parseInt(m[1].slice(4,6),16),DisplayName:''};sync();}
function confirm(){if(active==='book'){var b=(document.querySelector('[data-book=BookName]').value||'').trim(),n=(document.querySelector('[data-book=ColorName]').value||'').trim();if(!b||!n){toast('请同时填写配色系统名称和颜色名称','error');return;}candidate.Type=4;candidate.BookName=b;candidate.ColorName=n;candidate.DisplayName=b+' · '+n;}post('confirmColorPicker',JSON.stringify(candidate));}
function rgbToHsv(r,g,b){r/=255;g/=255;b/=255;var mx=Math.max(r,g,b),mn=Math.min(r,g,b),d=mx-mn,h=0;if(d){if(mx===r)h=((g-b)/d)%6;else if(mx===g)h=(b-r)/d+2;else h=(r-g)/d+4;h*=60;if(h<0)h+=360;}return [h,mx?d/mx:0,mx];}
function hsvToRgb(h,s,v){var c=v*s,x=c*(1-Math.abs((h/60)%2-1)),m=v-c,r=0,g=0,b=0;if(h<60){r=c;g=x;}else if(h<120){r=x;g=c;}else if(h<180){g=c;b=x;}else if(h<240){g=x;b=c;}else if(h<300){r=x;b=c;}else{r=c;b=x;}return [Math.round((r+m)*255),Math.round((g+m)*255),Math.round((b+m)*255)];}
function toast(m,k){window.CDBoxStudioToast(m,k);}
window.CDBoxStudioToast=function(m,k){var old=document.querySelector('.cp-toast');if(old)old.remove();var x=document.createElement('div');x.className='cp-toast '+(k||'');x.textContent=m||'';document.body.appendChild(x);setTimeout(function(){x.remove();},2800);};
window.CDBoxColorPickerFromCad=function(c){candidate=clone(c||candidate);setTab('book');sync();};
document.addEventListener('keydown',function(e){if(e.key==='Escape'){post('cancelColorPicker','');e.preventDefault();}else if(e.key==='Enter'&&!e.target.matches('input')){confirm();e.preventDefault();}});
render();setTimeout(function(){post('ready','color-picker');},50);
})();
");
            html.Append("</script></body></html>");
            return html.ToString();
        }

        private static string Html(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("&", "&amp;").Replace("<", "&lt;")
                .Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        private sealed class ColorPickerContext
        {
            public CDBoxColor initial { get; set; }
            public CDBoxStudioColorPickerOptions options { get; set; }
            public List<CDBoxColor> palette { get; set; }
            public List<CDBoxColor> standards { get; set; }
        }
    }
}
