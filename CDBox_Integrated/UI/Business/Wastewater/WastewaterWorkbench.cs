using System;

namespace TCPipeAutoDraw.UI.Studio
{
    // Presentation-only opt-in. CAD routes and calculation state remain page-owned.
    internal static class WastewaterWorkbench
    {
        public static string Apply(string html, string page, string theme, bool animations)
        {
            var settings = CDBoxStudioSettingsStore.Load();
            settings.Theme = theme;
            settings.AnimationsEnabled = animations;
            return Apply(html, page, settings);
        }

        public static string Apply(string html, string page, CDBoxStudioSettings settings)
        {
            html = CommonWorkbench.Apply(html, settings);
            return html.Replace("<body data-appearance=", "<body data-wastewater='" + page + "' data-appearance=")
                .Replace("</head>", "<style id='wastewater-workbench'>" + Styles + "</style></head>")
                .Replace("</body>", "<script>" + SectionScript + "</script></body>");
        }

        internal const string Styles = @"
body[data-wastewater]{--input:var(--panel2);--line2:var(--line);--shadow:none;--shadow2:none;--warn:#b36b00;--warn-bg:var(--manual);--err:var(--danger);--err-bg:var(--danger-soft);font-size:13px}
body[data-wastewater] :is(.qd-page,.qa-page,.sd-page,.page,main){background:var(--bg);color:var(--text);background-image:none}
body[data-wastewater] :is(.qd-page,.qa-page,.sd-page){padding:0 24px 24px;gap:0;min-height:0}
body[data-wastewater] :is(h1,h2){font-size:19px;font-weight:600;letter-spacing:-.2px}body[data-wastewater] h3{font-size:14px;font-weight:600}
body[data-wastewater] :is(.qd-head,.qa-head,.sd-head,header){padding:18px 0 16px;margin:0;gap:14px;border-bottom:1px solid var(--line);box-shadow:none;background:var(--bg);align-items:center}
body[data-wastewater] :is(.qd-head-actions,.qa-actions,.sd-head-actions,header>div:last-child){display:flex;gap:8px;flex-wrap:wrap;align-items:center}
body[data-wastewater] :is(button,.qd-btn,.qa-btn,.sd-btn,.qd-mini,.sd-mini){font-family:var(--ui-font);font-weight:500;border-radius:6px;box-shadow:none;transform:none;min-height:30px}
body[data-wastewater] button:hover{transform:none;box-shadow:none}
body[data-wastewater] :is(.qd-btn,.qd-mini,.sd-mini):not(.primary):not([data-accent-action]){background:var(--panel);color:var(--text);border-color:var(--line)}
body[data-wastewater] :is(button.danger,.qd-btn.danger,.qd-mini.danger,.sd-mini.danger){color:var(--danger)}
body[data-wastewater] :is(input,select,textarea){font-family:var(--content-font);font-weight:var(--content-weight);background:var(--panel2);color:var(--text);border-color:var(--line-strong);border-radius:5px}
body[data-wastewater] :is(input,select){height:32px}
body[data-wastewater] :is(input[type=checkbox],input[type=radio]){width:16px;height:16px;min-height:0;padding:0;accent-color:var(--brand)}
body[data-wastewater] textarea{padding:7px 9px;font-size:13px}
body[data-wastewater] :is(.qd-panel,.qd-card,.qa-card,.sd-card,.card,.group,.qd-context,.qa-object){border:0;border-radius:0;box-shadow:none;background:transparent;background-image:none}
body[data-wastewater] :is(.qa-field,.qd-field,.sd-field,.field,.check){border:0;border-radius:0;box-shadow:none;background:transparent;padding:0}
body[data-wastewater] :is(.qd-field>label,.qa-field>span,.sd-field>span,.field>span){font-size:12px;color:var(--muted);font-weight:500}
body[data-wastewater] :is(.qa-readonly,input[readonly]){border:0;background:transparent;color:var(--text);padding-left:0}
body[data-wastewater] :is(.qa-switch,.sd-check,.check-field,.check){display:flex;align-items:center;gap:9px;min-height:32px}
body[data-wastewater] :is(.qa-switch b,.check-field span){font-weight:400;color:var(--muted)}
body[data-wastewater] :is(.qd-panel,.qa-card,.sd-card,.card,.group){padding:18px 0;margin:0;border-bottom:1px solid var(--line)}
body[data-wastewater] :is(.qd-panel-head,.qa-structure-head,.sd-card-head,.card-title){padding:0 0 12px;margin:0;background:transparent;border:0;display:flex;align-items:center;justify-content:space-between;gap:12px}
body[data-wastewater] :is(.sd-card-body,.qa-card>main){padding:0}
body[data-wastewater] :is(.qa-grid,.sd-fields,.qd-fields,.form-grid){grid-template-columns:repeat(2,minmax(0,1fr));gap:16px 24px}
body[data-wastewater] :is(.qd-table-wrap,.qa-structure-scroll,.sd-table-wrap,.table-wrap,.layer-wrap){border:0;border-radius:0;max-width:100%;overflow:auto;background:transparent}
body[data-wastewater] :is(table,th,td){border-color:var(--line);color:var(--text)}
body[data-wastewater] th{background:var(--panel2);color:var(--muted);font-weight:500}
body[data-wastewater] td{background:transparent}body[data-wastewater] tbody tr:hover>td{background:var(--hover)}
body[data-wastewater] :is(.qa-structure,.structure-editor,.structure-preview,.qd-help){border:0;border-radius:0;box-shadow:none;background:transparent;padding:0}
body[data-wastewater] :is(.qd-help,.structure-preview){color:var(--muted);border-top:1px solid var(--line);padding-top:12px}
.ww-section{padding:18px 0;border-bottom:1px solid var(--line);min-width:0;grid-column:1/-1}
.ww-section>summary{display:flex;align-items:center;gap:8px;cursor:pointer;list-style:none;font-size:14px;font-weight:600;min-height:30px;color:var(--text)}
.ww-section>summary::-webkit-details-marker{display:none}.ww-section>summary:before{content:'›';width:10px;color:var(--muted);font-size:17px}.ww-section[open]>summary:before{content:'⌄'}
.ww-section>summary:focus-visible{outline:2px solid var(--focus);outline-offset:3px}.ww-section[open]>summary{margin-bottom:12px}
body[data-wastewater] .ww-section .qa-structure{grid-column:1/-1}
body[data-wastewater] .qa-object{display:flex;flex-wrap:wrap;gap:16px 28px;padding:14px 0;margin:0;border-bottom:1px solid var(--line)}
body[data-wastewater] .qa-object>div{padding:0 28px 0 0;border:0;border-right:1px solid var(--line);border-radius:0;background:transparent}
body[data-wastewater] .qa-object>div:last-child{border-right:0}
body[data-wastewater] .qa-status{border:0;border-radius:0;background:transparent;margin:0;padding:10px 0;color:var(--muted)}
body[data-wastewater] .qa-status:empty{display:none}body[data-wastewater] .qa-status.warning{color:var(--warn)}
body[data-wastewater] .qa-node-actions{padding:10px 0;margin:0;background:transparent;border:0}
body[data-wastewater] .qa-page>main{padding:0}
body[data-wastewater] .qa-field:has(.qa-switch){display:flex;align-items:center;justify-content:space-between;gap:12px}
body[data-wastewater] .qa-structure table{table-layout:fixed;min-width:0;width:100%}
body[data-wastewater] .qa-structure th:nth-child(1),body[data-wastewater] .qa-structure th:nth-child(2),body[data-wastewater] .qa-structure th:last-child{width:34px}
body[data-wastewater] .qa-structure th:nth-child(4){width:100px}body[data-wastewater] .qa-structure th:nth-child(5){width:52px}body[data-wastewater] .qa-structure th:nth-child(6){width:100px}
body[data-wastewater] .qa-structure td{padding:7px 5px}
body[data-wastewater='dashboard'] .qd-context{padding:14px 0;border-bottom:1px solid var(--line)}
body[data-wastewater='dashboard'] .qd-panel-head>div:last-child{display:flex;gap:8px;flex-wrap:wrap}
body[data-wastewater='dashboard'] .qd-context-grid{grid-template-columns:auto minmax(160px,1fr) minmax(130px,.7fr) minmax(140px,.8fr) auto}
body[data-wastewater='dashboard'] :is(.qd-context label,.qd-status,.qd-follow,.qd-chip,.qd-anchor button,.qd-note,.qd-source){color:var(--muted)}
body[data-wastewater='dashboard'] :is(.qd-follow,.qd-note,.qd-source>div,.qd-metric,.qd-breakdown){border:0;border-radius:0;background:transparent}
body[data-wastewater='dashboard'] .qd-anchor{top:0;background:var(--bg);border-bottom:1px solid var(--line);padding:10px 0;margin:0;overflow-x:auto}
body[data-wastewater='dashboard'] .qd-anchor button.active{background:var(--accent-soft);color:var(--brand)}
body[data-wastewater='dashboard'] .qd-summary-grid{gap:0;grid-template-columns:repeat(3,minmax(0,1fr));margin:0;padding:12px 0;border-bottom:1px solid var(--line)}
body[data-wastewater='dashboard'] .qd-summary-card{padding:14px 20px;border:0;border-right:1px solid var(--line);border-radius:0;box-shadow:none;min-height:96px;background:transparent;transform:none}
body[data-wastewater='dashboard'] .qd-summary-card:nth-child(3n){border-right:0}
body[data-wastewater='dashboard'] .qd-summary-card:hover{background:var(--hover)}
body[data-wastewater='dashboard'] .qd-summary-card:before,body[data-wastewater='dashboard'] .qd-summary-card:after{display:none}
body[data-wastewater='dashboard'] .qd-summary-card :is(strong,b){color:var(--text)}
body[data-wastewater='dashboard'] .qd-summary-card :is(span,small){color:var(--muted)}
body[data-wastewater='dashboard'] :is(.qd-summary-card.expanded,.qd-summary-card.active){background:var(--accent-soft)}
body[data-wastewater='dashboard'] :is(.qd-card-expand,.qd-metric-grid,.qd-breakdowns){border-color:var(--line)}
body[data-wastewater='dashboard'] .qd-card-expand{max-height:none;opacity:1;padding:12px 0;border-bottom:1px solid var(--line)}
body[data-wastewater='dashboard'] .qd-metric-grid>div{background:transparent;border-radius:0;padding:10px}
body[data-wastewater='dashboard'] :is(.qd-ref-table td:nth-child(2),.qd-metric-grid strong,.qd-breakdown strong,.qd-breakdown h4,.qd-status strong,.qd-card-expand strong,.qd-detail-row strong,.qd-loading strong,.qd-error strong){color:var(--text)}
body[data-wastewater='dashboard'] :is(.qd-status-line,.qd-card-expand div,.qd-metric-grid span,.qd-metric-grid em,.qd-breakdown button,.qd-detail-row small){color:var(--muted)}
body[data-wastewater='dashboard'] .qd-breakdown button:hover{background:var(--hover)}
body[data-wastewater='dashboard'] .qd-source-badge{background:var(--auto);color:var(--auto-text)}
body[data-wastewater='dashboard'] .qd-quality-item>span{background:var(--panel2);color:var(--muted)}
body[data-wastewater='dashboard'] .qd-quality-item.warning>span{background:var(--manual);color:var(--manual-text)}
body[data-wastewater='dashboard'] .qd-quality-item.error>span{background:var(--danger-soft);color:var(--danger)}
body[data-wastewater='dashboard'] .qd-quality-ok{background:transparent;border-radius:0;color:var(--ok)}
body[data-wastewater='dashboard'] :is(.qd-detail-head,.qd-detail-row,.qd-quality-item,.qd-loading,.qd-error){background:var(--panel);color:var(--text);border-color:var(--line);border-radius:0;box-shadow:none}
body[data-wastewater='dashboard'] .qd-detail-head{background:var(--panel2)}
body[data-wastewater='dashboard'] .qd-detail-scroll{border:0;border-radius:0;height:350px}
body[data-wastewater='dashboard'] .qd-detail-head,body[data-wastewater='dashboard'] .qd-detail-virtual{min-width:960px}
body[data-wastewater='dashboard'] .qd-row-actions button{background:var(--panel);color:var(--text);border-color:var(--line)}
body[data-wastewater='dashboard'] :is(.qd-bars i,.qd-group-bars i,.qd-progress i){background:var(--brand)}
body[data-wastewater='dashboard'] .qd-modal{background:var(--panel);color:var(--text);border-color:var(--line);box-shadow:var(--popup-shadow);border-radius:10px}
body[data-wastewater='dashboard'] :is(.qd-ref-table,.qd-source,.qd-quality-item) :is(small,span){color:var(--muted)}
body[data-wastewater='section'] .sd-work{padding-top:0;grid-template-columns:minmax(0,1fr) minmax(300px,.65fr);gap:24px;overflow:auto}
body[data-wastewater='section'] .sd-editor{padding:0;min-width:0;overflow:visible}
body[data-wastewater='section'] .sd-preview-card{border:0;border-left:1px solid var(--line);border-radius:0;box-shadow:none;padding:18px 0 0 24px;background:transparent;min-width:0}
body[data-wastewater='section'] .sd-preview-stage{border-radius:6px}
body[data-wastewater='section'] .sd-preview-head{color:var(--text);padding:0 0 12px;border:0}
body[data-wastewater='section'] .sd-preview-meta{color:var(--muted)}
body[data-wastewater='section'] .sd-preview-tools button{background:var(--panel);color:var(--text);border-color:var(--line)}
body[data-wastewater='section'] .sd-head small{display:none}
body[data-wastewater='section'] .sd-table{min-width:640px}body[data-wastewater='section'] .sd-layer-row.selected td{background:var(--accent-soft)}
body[data-wastewater='section'] .sd-modal{background:var(--panel);color:var(--text);border-color:var(--line);box-shadow:var(--popup-shadow);border-radius:10px}
body[data-wastewater='longitudinal'] .page{max-width:1600px;margin:auto;padding:0 24px 24px}
body[data-wastewater='longitudinal'] .top-grid{gap:0 28px;grid-template-columns:repeat(2,minmax(0,1fr))}
body[data-wastewater='longitudinal'] .styles-card{grid-column:1/-1}
body[data-wastewater='longitudinal'] .segmented{background:var(--panel2);border-color:var(--line);border-radius:6px}
body[data-wastewater='longitudinal'] .segmented button.active{background:var(--accent-soft);color:var(--brand);box-shadow:none}
body[data-wastewater='annotations'] main{margin:0;max-width:none;padding:0;display:grid;grid-template-columns:206px minmax(0,1fr);grid-template-rows:auto 1fr;min-height:100vh}
body[data-wastewater='annotations'] .ww-annotation-head{grid-column:2;padding:18px 24px;flex-wrap:wrap}
body[data-wastewater='annotations'] header small{display:none}
body[data-wastewater='annotations'] nav{grid-column:1;grid-row:1/3;display:flex;flex-direction:column;gap:5px;background:var(--sidebar);border:0;border-right:1px solid var(--line);padding:20px 12px;margin:0}
body[data-wastewater='annotations'] nav:before{content:'标注设置';font-weight:600;color:var(--muted);padding:4px 10px 16px}
body[data-wastewater='annotations'] nav button{height:auto;min-height:36px;flex:0 0 auto;padding:8px 10px;border:0;border-radius:6px;text-align:left;background:transparent;color:var(--text);box-shadow:none;font-size:13px}
body[data-wastewater='annotations'] nav button.active{background:var(--accent-soft);color:var(--brand)}
body[data-wastewater='annotations'] main>section{grid-column:2;padding:0 24px 28px;display:block;border:0;box-shadow:none;background:transparent;border-radius:0;min-width:0}
body[data-wastewater='annotations'] .ww-fields{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px 24px}
body[data-wastewater='annotations'] .wide{grid-column:1/-1}
body[data-wastewater='annotations'] .run{margin-top:20px;width:auto}
body[data-wastewater='annotations'] .color{padding:7px 0;display:flex;flex-direction:row;align-items:center;justify-content:space-between;border-bottom:1px solid var(--line)}
body[data-wastewater='defaults'] .qd-page{padding:0;height:100%}
body[data-wastewater='defaults'] .qd-card{padding:0;display:grid;grid-template-columns:200px minmax(0,1fr);grid-template-rows:auto minmax(0,1fr);height:100%;overflow:hidden}
body[data-wastewater='defaults'] .ww-default-head{grid-column:2;grid-row:1;display:flex;justify-content:space-between;padding:18px 24px;border-bottom:1px solid var(--line)}
body[data-wastewater='defaults'] .qd-tabs-row{grid-column:1;grid-row:1/3;display:block;overflow:auto;padding:20px 12px;margin:0;border:0;border-right:1px solid var(--line);background:var(--sidebar)}
body[data-wastewater='defaults'] .qd-tabs-row:before{content:'对象类型';display:block;font-weight:600;color:var(--muted);padding:4px 10px 16px}
body[data-wastewater='defaults'] .qd-tabs{flex-direction:column;gap:5px}
body[data-wastewater='defaults'] .qd-tab{border:0;border-radius:6px;text-align:left;white-space:normal;padding:8px 10px;background:transparent;color:var(--text);font-weight:500}
body[data-wastewater='defaults'] .qd-tab.active{background:var(--accent-soft);color:var(--brand)}
body[data-wastewater='defaults'] .qd-editor{grid-column:2;grid-row:2;padding:20px 24px 28px}
body[data-wastewater='defaults'] .qd-field:has(.check-field){display:flex;align-items:center;justify-content:space-between;gap:12px}
body[data-wastewater='defaults'] .qd-field:has(.structure-editor){grid-column:1/-1;display:grid;grid-template-columns:minmax(0,1fr) auto;gap:12px;align-items:center;border-top:1px solid var(--line);padding-top:18px}
body[data-wastewater='defaults'] .qd-field:has(.structure-editor)>label{grid-column:1;grid-row:1;margin:0;color:var(--text);font-size:14px;font-weight:600}
body[data-wastewater='defaults'] .structure-editor{display:contents}
body[data-wastewater='defaults'] .structure-head{display:none}
body[data-wastewater='defaults'] .structure-toolbar{grid-column:2;grid-row:1;border:0;border-radius:0;padding:0;margin:0;background:transparent}
body[data-wastewater='defaults'] :is(.layer-wrap,.structure-preview){grid-column:1/-1;min-width:0}
body[data-wastewater='defaults'] .layer-table{min-width:650px}body[data-wastewater='defaults'] .layer-table .name-col{min-width:140px}
body[data-wastewater='defaults'] .qd-note{padding:0 0 16px;margin:0 0 18px;border:0;border-bottom:1px solid var(--line);background:transparent}
@media(max-width:1100px){body[data-wastewater='dashboard'] .qd-context-grid{grid-template-columns:repeat(2,minmax(0,1fr))}body[data-wastewater='section'] .sd-work{grid-template-columns:minmax(0,1fr)}body[data-wastewater='section'] .sd-preview-card{border:0;padding:18px 0;min-height:350px}body[data-wastewater='section'] .sd-page{overflow:auto}.sd-work{flex:0 0 auto}}
@media(max-width:760px){body[data-wastewater] :is(.qd-page,.qa-page,.sd-page,.page){padding-left:16px;padding-right:16px}body[data-wastewater] :is(.qd-head,.qa-head,.sd-head){flex-wrap:wrap}body[data-wastewater='dashboard'] .qd-summary-grid{grid-template-columns:repeat(2,minmax(0,1fr))}body[data-wastewater='dashboard'] .qd-summary-card{padding:14px 10px;border-right:0}body[data-wastewater='annotations'] main,body[data-wastewater='defaults'] .qd-card{grid-template-columns:154px minmax(0,1fr)}body[data-wastewater='longitudinal'] .top-grid{grid-template-columns:1fr}body[data-wastewater='annotations'] :is(header,main>section),body[data-wastewater='defaults'] :is(.qd-editor,.ww-default-head){padding-left:16px;padding-right:16px}}
@media(max-width:560px){body[data-wastewater] :is(.qa-grid,.sd-fields,.qd-fields,.form-grid,.ww-fields){grid-template-columns:1fr}body[data-wastewater='annotations'] main,body[data-wastewater='defaults'] .qd-card{display:block}body[data-wastewater='annotations'] nav,body[data-wastewater='defaults'] .qd-tabs-row{border-right:0;border-bottom:1px solid var(--line)}body[data-wastewater='annotations'] nav,body[data-wastewater='defaults'] .qd-tabs{flex-direction:row;flex-wrap:wrap}body[data-wastewater='annotations'] nav:before,body[data-wastewater='defaults'] .qd-tabs-row:before{display:none}body[data-wastewater='defaults'] .quantity-defaults-standalone{overflow:auto}body[data-wastewater='defaults'] .qd-card{height:auto;overflow:visible}body[data-wastewater='defaults'] .qd-editor{overflow:visible}body[data-wastewater] .qa-structure table{min-width:520px}}
";

        private const string SectionScript = @"
(function(){
 const state=new Map();
 document.addEventListener('toggle',function(e){const d=e.target;if(d.matches('details[data-ww-section]'))state.set(d.dataset.wwSection,d.open);},true);
 function restore(){
 document.querySelectorAll('details[data-ww-section]').forEach(d=>{if(state.has(d.dataset.wwSection)&&d.open!==state.get(d.dataset.wwSection))d.open=state.get(d.dataset.wwSection);});
 document.querySelectorAll('[data-wastewater=section] .sd-card,[data-wastewater=longitudinal] .card').forEach((panel,index)=>{
  if(panel.dataset.wwReady)return;const title=panel.querySelector('h2,h3');if(!title)return;
  panel.dataset.wwReady='true';const head=title.parentElement.matches('.sd-card-head,.card-title')?title.parentElement:title;
  const button=document.createElement('button');button.type='button';button.className='wb-toggle';button.textContent=title.textContent;
  const key=document.body.dataset.wastewater+'-'+title.textContent,content=document.createElement('div');content.id='ww-panel-'+index;
  while(head.nextSibling)content.appendChild(head.nextSibling);panel.appendChild(content);
  title.replaceWith(button);content.hidden=state.get(key)===false;button.setAttribute('aria-expanded',String(!content.hidden));button.setAttribute('aria-controls',content.id);
  button.onclick=()=>{content.hidden=!content.hidden;state.set(key,!content.hidden);button.setAttribute('aria-expanded',String(!content.hidden));window.dispatchEvent(new Event('resize'));};
 });}
 restore();
 new MutationObserver(restore).observe(document.body,{childList:true,subtree:true});
})();";
    }
}
