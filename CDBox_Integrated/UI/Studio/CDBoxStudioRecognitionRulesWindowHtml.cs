using System;
using System.Text;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioRecognitionRulesWindowHtml
    {
        public static string Build(CDBoxStudioSettings settings, string logFilePath)
        {
            return CDBoxStudioRecognitionRulesPage.BuildStandaloneDocument(settings, logFilePath);
        }
    }

    internal static class CDBoxStudioRecognitionRulesPage
    {
        public static string BuildEmbeddedSection()
        {
            var page = new StringBuilder();
            page.Append("<section id=\"recognitionRulesPage\" class=\"rules-page recognition-rules-page rr-embedded\" data-route=\"recognition-rules\" style=\"display:none\">");
            page.Append("<div class=\"settings-head\"><div><span class=\"kicker\">Studio Preview 5.1</span><h2>属性识别表</h2></div><div class=\"head-actions\"><button class=\"primary-btn\" id=\"openRecognitionWindow\">独立窗口打开</button><button class=\"ghost-btn\" id=\"openLegacyRecognition\">打开旧版属性识别表</button><button class=\"ghost-btn\" id=\"backToHomeFromRules\">返回总览</button></div></div>");
            page.Append(BuildEditorCard(false, null));
            page.Append("</section>");
            return page.ToString();
        }

        public static string BuildStandaloneDocument(CDBoxStudioSettings settings, string logFilePath)
        {
            settings = settings ?? new CDBoxStudioSettings();
            settings.Normalize();

            var html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"><title>CDBox Studio - 属性识别表</title><style>");
            html.Append(BuildStyles(true));
            html.Append("</style></head><body data-theme=\"").Append(HtmlAttr(settings.Theme)).Append("\" class=\"").Append(settings.AnimationsEnabled ? string.Empty : "no-animations").Append("\">");
            html.Append("<section id=\"recognitionRulesPage\" class=\"recognition-rules-page rr-standalone\" data-route=\"recognition-rules\">");
            html.Append("<main class=\"rr-page\">");
            html.Append(BuildEditorCard(true, logFilePath));
            html.Append("</main></section><div id=\"toastStack\" class=\"toast-stack\"></div>");
            html.Append("<script>\n");
            html.Append("var recognitionRulesData=").Append(CDBoxStudioRecognitionRules.BuildRulesJson(CDBoxStudioRecognitionRules.LoadRules())).Append(";\n");
            html.Append("var defaultRecognitionRulesData=").Append(CDBoxStudioRecognitionRules.BuildRulesJson(CDBoxStudioRecognitionRules.LoadDefaultRules())).Append(";\n");
            html.Append(BuildComponentScript());
            html.Append("\n(function(){\n");
            html.Append("  function post(name,arg){if(window.chrome&&chrome.webview){chrome.webview.postMessage('studio|'+name+'|'+encodeURIComponent(arg||''));}}\n");
            html.Append("  function toast(message,kind){var stack=document.getElementById('toastStack');if(!stack)return;var node=document.createElement('div');node.className='toast '+(kind||'info');node.textContent=message||'';stack.appendChild(node);setTimeout(function(){node.style.opacity='0';node.style.transform='translateX(10px)';},2600);setTimeout(function(){if(node.parentNode)node.parentNode.removeChild(node);},3100);} window.CDBoxStudioToast=toast;\n");
            html.Append("  try{window.CDBoxRecognitionRulesPage.create({rootId:'recognitionRulesPage',data:recognitionRulesData,defaultData:defaultRecognitionRulesData,toast:toast,post:post,standalone:true,autoOpenSearch:false});setTimeout(function(){post('ready','recognition-rules');},80);}catch(ex){var msg=(ex&&ex.stack)||String(ex||'未知错误');document.body.innerHTML='<div class=\"rr-error\"><div><h2>属性识别表页面初始化失败</h2><pre>'+String(msg).replace(/[&<>]/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;'}[c];})+'</pre></div></div>';post('pageError',msg);}\n");
            html.Append("})();\n");
            html.Append("</script></body></html>");
            return html.ToString();
        }

        public static string BuildStyles(bool standalone)
        {
            var css = new StringBuilder();
            css.Append(@"
.recognition-rules-page{color:var(--text)}
.recognition-rules-page button,.recognition-rules-page input,.recognition-rules-page select{font:inherit}
.recognition-rules-page button{color:inherit}
.recognition-rules-page .rr-card{background:var(--panel);border:1px solid var(--line);border-radius:18px;padding:18px;box-shadow:var(--shadow2);display:flex;flex-direction:column;min-height:0;overflow:hidden}
.rr-embedded .rr-card{min-height:560px}.rr-standalone{height:100vh;display:block;background:transparent}.rr-standalone .rr-page{height:100vh;padding:8px 20px 20px;background:var(--bg);overflow:hidden}.rr-standalone .rr-card{height:100%;animation:rrFadeUp .22s ease both}
.recognition-rules-page .rr-card-head{display:flex;align-items:flex-start;justify-content:space-between;gap:16px;margin-bottom:12px}.recognition-rules-page .rr-title strong{display:block;font-size:18px;line-height:1.35}.recognition-rules-page .rr-title em{display:block;color:var(--muted);font-style:normal;font-size:12px;margin-top:5px;line-height:1.6}.recognition-rules-page .rr-top-tools{display:flex;align-items:flex-start;justify-content:flex-end;gap:10px;min-width:44px}.recognition-rules-page .rr-toolbar{display:flex;align-items:center;justify-content:space-between;gap:14px;margin-bottom:12px}.recognition-rules-page .rr-left,.recognition-rules-page .rr-actions{display:flex;gap:8px;align-items:center;flex-wrap:wrap}.recognition-rules-page .rr-actions{justify-content:flex-end}.recognition-rules-page .rr-search-wrap{position:relative;display:flex;align-items:center;justify-content:flex-end;min-width:44px}.recognition-rules-page .rr-search-icon{width:38px;height:38px;border:1px solid var(--line);background:var(--panel2);border-radius:12px;cursor:pointer;display:inline-flex;align-items:center;justify-content:center}.recognition-rules-page .rr-search-icon:hover{border-color:rgba(59,130,246,.42);color:var(--brand)}.recognition-rules-page .rr-search{height:38px;width:0;opacity:0;pointer-events:none;border:1px solid var(--line);border-radius:10px;background:var(--panel2);padding:0;outline:0;color:var(--text);transition:width .18s ease,opacity .18s ease,padding .18s ease,margin .18s ease}.recognition-rules-page .rr-search-wrap.open .rr-search{width:330px;opacity:1;pointer-events:auto;padding:0 12px;margin-right:8px}.rr-embedded .rr-search-wrap.open .rr-search{width:min(360px,42vw)}.recognition-rules-page .rr-search:focus{border-color:rgba(59,130,246,.55);box-shadow:0 0 0 3px rgba(59,130,246,.12)}
.recognition-rules-page .rr-btn{height:38px;border:1px solid var(--line);background:var(--panel);border-radius:12px;padding:0 13px;cursor:pointer;white-space:nowrap;display:inline-flex;align-items:center;gap:6px;font-weight:700}.recognition-rules-page .rr-btn:hover{border-color:rgba(59,130,246,.42);color:var(--brand);background:var(--panel2)}.recognition-rules-page .rr-btn.primary{border:0;background:linear-gradient(135deg,var(--brand),var(--brand2));color:#fff;font-weight:800;box-shadow:0 13px 24px rgba(59,130,246,.20)}.recognition-rules-page .rr-btn.warning{background:#fff7ed;color:#c2410c;border-color:#fdba74}.recognition-rules-page .rr-btn.warning:hover{background:#ffedd5;color:#9a3412;border-color:#fb923c}.recognition-rules-page .rr-btn.danger{background:var(--panel);color:#b91c1c;border-color:#fecaca}.recognition-rules-page .rr-btn.danger:hover{background:#fee2e2;border-color:#f87171;color:#991b1b}.recognition-rules-page .rr-btn.legacy{background:var(--panel);color:var(--text);border-color:var(--line)}
.recognition-rules-page .rules-table-wrap,.recognition-rules-page .rr-table-wrap{flex:1;min-height:0;overflow:auto;border:1px solid var(--line);border-radius:18px;background:var(--panel)}.recognition-rules-page .rules-table{width:100%;min-width:1180px;border-collapse:separate;border-spacing:0}.recognition-rules-page .rules-table th,.recognition-rules-page .rules-table td{border-bottom:1px solid var(--line2, var(--line));padding:8px;text-align:left;font-size:13px;vertical-align:middle}.recognition-rules-page .rules-table th{position:sticky;top:0;background:var(--panel2);z-index:1;color:var(--muted);font-weight:800}.recognition-rules-page .rules-table tr{cursor:pointer}.recognition-rules-page .rules-table tr:hover{background:var(--panel2)}.recognition-rules-page .rules-table tr.selected{background:linear-gradient(135deg,rgba(59,130,246,.10),rgba(124,58,237,.08))}.recognition-rules-page .rules-table input[type=text],.recognition-rules-page .rules-table select{width:100%;border:1px solid var(--line);border-radius:8px;background:var(--panel);color:var(--text);padding:7px 8px;outline:0}.recognition-rules-page .rules-table input[type=text]:focus,.recognition-rules-page .rules-table select:focus{border-color:rgba(59,130,246,.50);box-shadow:0 0 0 2px rgba(59,130,246,.09)}.recognition-rules-page .rules-table input[type=checkbox]{width:18px;height:18px;accent-color:var(--brand)}.recognition-rules-page .rules-table td:nth-child(1){width:70px;color:var(--muted);font-weight:800;text-align:center}.recognition-rules-page .rules-table td:nth-child(2),.recognition-rules-page .rules-table td:nth-child(8){text-align:center;width:76px}.recognition-rules-page .rules-table td:nth-child(3){width:108px}.recognition-rules-page .rules-table td:nth-child(4){width:260px}.recognition-rules-page .rules-table td:nth-child(5){width:145px}.recognition-rules-page .rules-table td:nth-child(6){width:165px}.recognition-rules-page .rules-table td:nth-child(7){width:300px}
.recognition-rules-page .rules-table tr.dragging{opacity:.4}.recognition-rules-page .rules-table tr.drag-target td{background:rgba(59,130,246,.10)}.recognition-rules-page .rules-table td:first-child{cursor:grab}
.recognition-rules-page .empty-line{display:none;padding:22px;text-align:center;color:var(--muted);background:var(--panel2);border:1px dashed var(--line);border-radius:18px;margin-top:12px}.recognition-rules-page .rr-tips{margin-top:12px;border:1px solid var(--line2, var(--line));background:linear-gradient(180deg,var(--panel2),var(--panel));border-radius:14px;padding:11px 13px;color:var(--muted);font-size:12px;line-height:1.7}.recognition-rules-page .rr-tips strong{color:var(--text)}.recognition-rules-page .rr-tips span{display:inline-block;margin-right:14px}.recognition-rules-page .rr-path{font-size:11px;color:var(--muted);margin-top:7px;word-break:break-all;line-height:1.55}.rr-error{height:100vh;display:grid;place-items:center;background:var(--bg);padding:28px}.rr-error>div{max-width:720px;border:1px solid var(--line);border-radius:18px;background:var(--panel);box-shadow:var(--shadow2);padding:22px}.rr-error pre{white-space:pre-wrap;word-break:break-word;background:var(--panel2);border:1px solid var(--line);border-radius:12px;padding:12px;color:var(--muted)}
.toast-stack{position:fixed;right:24px;top:58px;z-index:50;display:flex;flex-direction:column;gap:10px}.toast{min-width:230px;max-width:390px;background:var(--panel);border:1px solid var(--line);border-left:4px solid var(--brand);border-radius:16px;padding:12px 14px;box-shadow:var(--shadow2);font-size:13px;color:var(--text);animation:toastIn .2s ease both}.toast.success{border-left-color:var(--ok)}.toast.warning{border-left-color:var(--warn)}.toast.error{border-left-color:var(--err)}.no-animations *,.no-animations *:before,.no-animations *:after{animation:none!important;transition:none!important;scroll-behavior:auto!important}@keyframes rrFadeUp{from{opacity:0;transform:translateY(8px)}to{opacity:1;transform:none}}@keyframes toastIn{from{opacity:0;transform:translateX(12px)}to{opacity:1;transform:none}}@media(max-width:1080px){.recognition-rules-page .rr-card-head,.recognition-rules-page .rr-toolbar{flex-direction:column;align-items:stretch}.recognition-rules-page .rr-top-tools,.recognition-rules-page .rr-actions,.recognition-rules-page .rr-search-wrap{justify-content:flex-start}.recognition-rules-page .rr-search-wrap.open .rr-search{width:100%;max-width:none}}
");
            if (standalone)
            {
                css.Insert(0, @":root{--bg:#f7f9fe;--panel:#fff;--panel2:#f8fbff;--muted:#64748b;--text:#162033;--line:#dce8f6;--line2:#e8eef8;--brand:#3b82f6;--brand2:#7c3aed;--ok:#10b981;--warn:#f59e0b;--err:#ef4444;--shadow:0 22px 46px rgba(30,41,59,.12);--shadow2:0 12px 28px rgba(30,41,59,.08)}body[data-theme='fresh']{--bg:#f3f7ff;--panel:#fff;--panel2:#f5f9ff;--muted:#65758f;--text:#172033;--line:#d7e6fb;--line2:#e4efff;--brand:#2563eb;--brand2:#9333ea}body[data-theme='dark']{--bg:#101827;--panel:#172033;--panel2:#111827;--muted:#94a3b8;--text:#e5e7eb;--line:#26364d;--line2:#233044;--brand:#60a5fa;--brand2:#a78bfa;--shadow:0 22px 46px rgba(0,0,0,.30);--shadow2:0 12px 28px rgba(0,0,0,.24)}*{box-sizing:border-box}html,body{width:100%;height:100%;margin:0;background:transparent;font-family:'Microsoft YaHei UI','Segoe UI',system-ui,sans-serif;color:var(--text);overflow:hidden}button,input,select{font:inherit}");
            }
            return css.ToString();
        }

        public static string BuildEmbeddedBridgeScript()
        {
            return BuildComponentScript();
        }

        private static string BuildEditorCard(bool standalone, string logFilePath)
        {
            string cardClass = standalone ? "rr-card" : "setting-card rules-card rr-card";
            var page = new StringBuilder();
            page.Append("<article class=\"").Append(cardClass).Append("\" data-recognition-rules=\"1\">");
            page.Append("<div class=\"rr-card-head\"><div class=\"rr-title\"><strong>规则表</strong></div><div class=\"rr-top-tools\"><div class=\"rr-search-wrap\" id=\"searchWrap\"><input id=\"rulesSearch\" class=\"rr-search\" placeholder=\"搜索：匹配内容、父属性、分类、标签...\" /><button id=\"toggleSearch\" class=\"rr-search-icon\" title=\"搜索 / Ctrl+F\">🔍</button></div></div></div>");
            page.Append("<div class=\"rr-toolbar\"><div class=\"rr-left\"><button id=\"addRuleButton\" class=\"rr-btn\">新增</button><button id=\"deleteRuleButton\" class=\"rr-btn danger\">删除</button><button id=\"restoreDefaultRulesButton\" class=\"rr-btn warning\">恢复默认</button><button id=\"saveRulesButton\" class=\"rr-btn primary\">保存</button></div><div class=\"rr-actions\">");
            if (standalone) page.Append("<button id=\"openLegacyRecognition\" class=\"rr-btn legacy\">打开旧版</button><button id=\"closeWindow\" class=\"rr-btn danger\">关闭</button>");
            page.Append("</div></div>");
            page.Append("<div class=\"rules-table-wrap rr-table-wrap\"><table class=\"rules-table\"><thead><tr><th>优先级</th><th>启用</th><th>匹配方式</th><th>匹配内容</th><th>父属性</th><th>分类</th><th>标签</th><th>命中停止</th></tr></thead><tbody id=\"rulesBody\"></tbody></table></div>");
            page.Append("<div id=\"rulesEmpty\" class=\"empty-line\">没有匹配的规则。</div>");
            page.Append("</article>");
            return page.ToString();
        }

        private static string BuildComponentScript()
        {
            return @"
(function(w){
  function cloneRules(source){return (source||[]).map(function(r){return {enabled:!!r.enabled,matchMode:r.matchMode||'通配符',pattern:r.pattern||'',parentGroup:r.parentGroup||'',parentClass:r.parentClass||'',tagText:r.tagText||'',stopAfterMatch:!!r.stopAfterMatch};});}
  function b64(value){return btoa(unescape(encodeURIComponent(value||'')));}
  function RecognitionRulesPage(options){this.options=options||{};this.root=document.getElementById(this.options.rootId||'recognitionRulesPage');if(!this.root)throw new Error('未找到属性识别表页面根节点。');this.rules=cloneRules(this.options.data);this.defaultRules=cloneRules(this.options.defaultData);this.selectedIndex=this.rules.length?0:-1;this.matchModes=['精确','包含','通配符','正则'];this.bound=false;}
  RecognitionRulesPage.prototype.$=function(selector){return this.root.querySelector(selector);};
  RecognitionRulesPage.prototype.post=function(name,arg){if(typeof this.options.post==='function')this.options.post(name,arg||'');};
  RecognitionRulesPage.prototype.toast=function(message,kind){if(typeof this.options.toast==='function')this.options.toast(message,kind||'info');};
  RecognitionRulesPage.prototype.ruleText=function(rule){return ((rule.matchMode||'')+' '+(rule.pattern||'')+' '+(rule.parentGroup||'')+' '+(rule.parentClass||'')+' '+(rule.tagText||'')).toLowerCase();};
  RecognitionRulesPage.prototype.cell=function(row,text){var cell=document.createElement('td');if(text!==undefined)cell.textContent=text;row.appendChild(cell);return cell;};
  RecognitionRulesPage.prototype.textInput=function(value,oninput){var input=document.createElement('input');input.type='text';input.value=value||'';input.addEventListener('click',function(ev){ev.stopPropagation();});input.addEventListener('input',function(){oninput(input.value);});return input;};
  RecognitionRulesPage.prototype.check=function(value,onchange){var input=document.createElement('input');input.type='checkbox';input.checked=!!value;input.addEventListener('click',function(ev){ev.stopPropagation();});input.addEventListener('change',function(){onchange(input.checked);});return input;};
  RecognitionRulesPage.prototype.modeSelect=function(value,onchange){var select=document.createElement('select');var current=value||'通配符';select.addEventListener('click',function(ev){ev.stopPropagation();});this.matchModes.forEach(function(mode){var option=document.createElement('option');option.value=mode;option.textContent=mode;if(mode===current)option.selected=true;select.appendChild(option);});select.addEventListener('change',function(){onchange(select.value);});return select;};
  RecognitionRulesPage.prototype.bindRowDrag=function(row,index){var self=this;row.draggable=true;row.title='拖动行可调整规则优先级';row.addEventListener('dragstart',function(ev){if(ev.target.closest('input,select,button')){ev.preventDefault();return;}self.dragIndex=index;row.classList.add('dragging');if(ev.dataTransfer)ev.dataTransfer.effectAllowed='move';});row.addEventListener('dragover',function(ev){if(self.dragIndex==null||self.dragIndex===index)return;ev.preventDefault();row.classList.add('drag-target');});row.addEventListener('dragleave',function(){row.classList.remove('drag-target');});row.addEventListener('drop',function(ev){if(self.dragIndex==null||self.dragIndex===index)return;ev.preventDefault();var from=self.dragIndex,box=row.getBoundingClientRect(),to=index+(ev.clientY>box.top+box.height/2?1:0),item=self.rules.splice(from,1)[0];if(from<to)to--;to=Math.max(0,Math.min(self.rules.length,to));self.rules.splice(to,0,item);self.selectedIndex=to;self.dragIndex=null;self.render();});row.addEventListener('dragend',function(){self.dragIndex=null;row.classList.remove('dragging','drag-target');});};
  RecognitionRulesPage.prototype.render=function(){var body=this.$('#rulesBody');if(!body)return;var search=this.$('#rulesSearch');var q=((search&&search.value)||'').trim().toLowerCase();body.innerHTML='';var visible=0;var self=this;this.rules.forEach(function(rule,index){if(q&&self.ruleText(rule).indexOf(q)<0)return;visible++;var row=document.createElement('tr');row.className=index===self.selectedIndex?'selected':'';row.addEventListener('click',function(){self.selectedIndex=index;self.render();});self.cell(row,String(index+1));self.cell(row).appendChild(self.check(rule.enabled,function(v){rule.enabled=v;}));self.cell(row).appendChild(self.modeSelect(rule.matchMode,function(v){rule.matchMode=v;}));self.cell(row).appendChild(self.textInput(rule.pattern,function(v){rule.pattern=v;}));self.cell(row).appendChild(self.textInput(rule.parentGroup,function(v){rule.parentGroup=v;}));self.cell(row).appendChild(self.textInput(rule.parentClass,function(v){rule.parentClass=v;}));self.cell(row).appendChild(self.textInput(rule.tagText,function(v){rule.tagText=v;}));self.cell(row).appendChild(self.check(rule.stopAfterMatch,function(v){rule.stopAfterMatch=v;}));self.bindRowDrag(row,index);body.appendChild(row);});var empty=this.$('#rulesEmpty');if(empty)empty.style.display=visible?'none':'block';};
  RecognitionRulesPage.prototype.addRule=function(){this.rules.push({enabled:true,matchMode:'通配符',pattern:'',parentGroup:'',parentClass:'',tagText:'',stopAfterMatch:true});this.selectedIndex=this.rules.length-1;this.render();};
  RecognitionRulesPage.prototype.deleteRule=function(){if(this.selectedIndex<0||this.selectedIndex>=this.rules.length)return;if(!confirm('删除当前选中的识别规则？'))return;this.rules.splice(this.selectedIndex,1);if(this.selectedIndex>=this.rules.length)this.selectedIndex=this.rules.length-1;this.render();};
  RecognitionRulesPage.prototype.restoreDefault=function(){if(!confirm('恢复默认属性识别表会覆盖当前页面中的编辑内容，保存前不会写入文件。是否继续？'))return;this.rules=cloneRules(this.defaultRules);this.selectedIndex=this.rules.length?0:-1;this.render();this.toast('已恢复为默认表，点击保存后生效','info');};
  RecognitionRulesPage.prototype.buildPayload=function(){return this.rules.map(function(r){return [r.enabled?'1':'0',r.matchMode||'通配符',r.pattern||'',r.parentGroup||'',r.parentClass||'',r.tagText||'',r.stopAfterMatch?'1':'0'].map(b64).join('\t');}).join('\n');};
  RecognitionRulesPage.prototype.openSearch=function(){var wrap=this.$('#searchWrap');var input=this.$('#rulesSearch');if(!wrap||!input)return;wrap.classList.add('open');setTimeout(function(){input.focus();input.select();},20);};
  RecognitionRulesPage.prototype.closeSearchIfEmpty=function(){var wrap=this.$('#searchWrap');var input=this.$('#rulesSearch');if(wrap&&input&&!input.value)wrap.classList.remove('open');};
  RecognitionRulesPage.prototype.bind=function(){if(this.bound)return;this.bound=true;var self=this;var bind=function(selector,event,handler){var node=self.$(selector);if(node)node.addEventListener(event,handler);};bind('#toggleSearch','click',function(){var wrap=self.$('#searchWrap');var input=self.$('#rulesSearch');if(!wrap||!input)return;if(wrap.classList.contains('open')){if(input.value){input.value='';self.render();}wrap.classList.remove('open');}else self.openSearch();});bind('#rulesSearch','input',function(){self.render();});bind('#rulesSearch','blur',function(){self.closeSearchIfEmpty();});bind('#addRuleButton','click',function(){self.addRule();});bind('#deleteRuleButton','click',function(){self.deleteRule();});bind('#restoreDefaultRulesButton','click',function(){self.restoreDefault();});bind('#saveRulesButton','click',function(){self.post('saveRecognitionRules',self.buildPayload());});bind('#openLegacyRecognition','click',function(){self.post('openLegacyRecognition','');});bind('#closeWindow','click',function(){self.post('close','');});document.addEventListener('keydown',function(ev){var visible=self.root.offsetParent!==null||self.options.standalone;if(!visible)return;if((ev.ctrlKey||ev.metaKey)&&String(ev.key).toLowerCase()==='f'){ev.preventDefault();self.openSearch();}else if(ev.key==='Escape'){self.closeSearchIfEmpty();}});};
  RecognitionRulesPage.prototype.init=function(){this.bind();this.render();return this;};
  w.CDBoxRecognitionRulesPage={create:function(options){return new RecognitionRulesPage(options).init();},cloneRules:cloneRules};
})(window);
";
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
