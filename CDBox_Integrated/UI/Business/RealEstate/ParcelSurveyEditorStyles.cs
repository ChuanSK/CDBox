namespace CDBox.RealEstate.UI
{
    /// <summary>Parcel-only layout and controls; business behavior stays in the page.</summary>
    internal static class ParcelSurveyEditorStyles
    {
        public static string Build()
        {
            return @"
:root{--sidebar-width:184px}
:root{--top-height:56px}
*{box-sizing:border-box}
html,body{height:100%;margin:0;overflow:hidden}
body{background:var(--bg);color:var(--text);font:13px/1.5 'Segoe UI','Microsoft YaHei UI',sans-serif}
button,input,select,textarea{font:inherit;color:inherit}
button{cursor:pointer}
button:disabled{opacity:.4;cursor:not-allowed}
button,input,select,textarea{transition:background-color .12s,border-color .12s}
button:focus-visible,input:focus-visible,select:focus-visible,textarea:focus-visible{outline:2px solid var(--focus);outline-offset:2px}
input::placeholder,textarea::placeholder{color:var(--muted)}
.shell{position:relative;height:100vh;display:grid;grid-template-columns:var(--sidebar-width) minmax(0,1fr);grid-template-rows:var(--top-height) minmax(0,1fr);grid-template-areas:'sidebar top' 'sidebar workspace';background:var(--bg);transition:grid-template-columns .24s cubic-bezier(.2,.7,.2,1)}
.top{grid-area:top;display:flex;align-items:center;padding:10px 20px;border-bottom:1px solid var(--line);background:var(--panel);z-index:8}
.toolbar{display:flex;width:100%;min-width:0;align-items:center;gap:8px;white-space:nowrap}
.parcel-picker{display:flex;min-width:0;align-items:center;gap:10px;margin-right:0}
.parcel-picker strong{flex:0 0 auto;font-size:12px;font-weight:500;color:var(--muted)}
.parcel-picker select{width:220px;min-width:120px;height:32px;padding:0 28px 0 10px;border:1px solid var(--line-strong);border-radius:6px;background:var(--panel);text-overflow:ellipsis}
.toolbar-btn{width:104px;flex:0 0 104px;font-size:11px;line-height:1}
.toolbar-btn,.nav-item,.btn,.export-option,.mini,.icon-btn{font-weight:400}
.toolbar-btn,.btn,.mini{height:32px;padding:0 10px;border:1px solid var(--line-strong);border-radius:6px;background:transparent;white-space:nowrap}
.toolbar-btn:not(.primary){border-color:transparent}
.toolbar-btn:hover:not(:disabled),.btn:hover:not(:disabled),.mini:hover:not(:disabled),.icon-btn:hover:not(:disabled){background:var(--hover)}
.toolbar-btn.primary,.btn.primary{background:var(--brand);color:var(--on-brand);border-color:var(--brand)}
.toolbar-btn.primary:hover:not(:disabled),.btn.primary:hover:not(:disabled){background:var(--brand);opacity:.86}
.toolbar-btn.danger,.mini.danger,.icon-btn.danger{color:var(--danger)}
.toolbar-btn.danger:hover:not(:disabled),.mini.danger:hover:not(:disabled),.icon-btn.danger:hover:not(:disabled){background:var(--danger-soft)}

.parcel-actions{position:relative;display:flex;flex:0 0 auto;align-items:center}
.more-button{width:32px;flex-basis:32px;font-size:20px;letter-spacing:1px;padding:0}
.parcel-action-items{position:absolute;left:100%;top:0;display:flex;align-items:center;gap:4px;padding:0 4px;background:var(--panel);border-radius:6px;box-shadow:var(--popup-shadow);opacity:0;transform:translateX(-8px);pointer-events:none;visibility:hidden;transition:opacity .18s,transform .22s,visibility .22s}
.parcel-actions.open .parcel-action-items{opacity:1;transform:translateX(0);pointer-events:auto;visibility:visible}
.auto-save{display:flex;align-items:center;justify-content:flex-end;gap:6px;margin-left:auto;min-width:0;color:var(--muted);font-size:11px}.auto-save span{overflow:hidden;text-overflow:ellipsis}.auto-save .failed{color:var(--danger)}

.sidebar{position:absolute;left:0;top:0;bottom:0;width:var(--sidebar-width);display:flex;min-height:0;flex-direction:column;border-right:1px solid var(--line);background:var(--sidebar);z-index:10;transform:translateX(0);opacity:1;transition:transform .24s cubic-bezier(.2,.7,.2,1),opacity .2s ease}
.sidebar{padding:0 8px 12px}
.nav{display:flex;flex:1;flex-direction:column}
.nav{min-width:0;gap:5px;padding:10px 0 0;overflow-x:hidden;overflow-y:auto}
.nav-item{display:flex;align-items:center;gap:16px;min-width:0;width:100%;padding:9px;font-size:12px;white-space:nowrap;border:0;border-radius:6px;background:transparent;color:var(--muted);text-align:left}
.nav-label{min-width:0;overflow:hidden;text-overflow:ellipsis}.nav-count{margin-left:auto;min-width:20px;text-align:right;color:var(--focus);font-size:12px;font-weight:600;font-variant-numeric:tabular-nums}.nav-count[hidden]{display:none}
.nav-item:hover{background:var(--hover);color:var(--text)}
.nav-item.active{color:var(--text);background:var(--selected)}
.sidebar-edge{display:none;position:absolute;left:0;top:0;bottom:0;width:12px;z-index:9}
.shell.sidebar-collapsed{grid-template-columns:0 minmax(0,1fr)}
.shell.sidebar-collapsed .sidebar{transform:translateX(-101%);opacity:0;pointer-events:none}
.shell.sidebar-collapsed .sidebar-edge{display:block}
.shell.sidebar-collapsed.sidebar-preview .sidebar{transform:translateX(0);opacity:1;pointer-events:auto;box-shadow:var(--popup-shadow);border-radius:0 8px 8px 0;overflow:hidden}
.workspace{grid-area:workspace;display:flex;min-width:0;min-height:0;flex-direction:column;container-type:inline-size;container-name:parcel-workspace}
.content{flex:1;min-height:0;overflow:auto;padding:8px 28px 40px;scrollbar-gutter:stable}
.content.switching{animation:reFade .12s ease both}
.group{max-width:1460px;margin:0 auto;padding:0 0 12px;border:0;border-radius:0;background:transparent;box-shadow:none}
.group+.group{border-top:1px solid var(--line)}
.group-head{display:flex;justify-content:space-between;align-items:center;gap:12px;min-height:48px;padding:9px 0}
.group-head strong{font-size:13px;font-weight:600}
.group-toggle{display:flex;align-items:center;gap:8px;min-width:0;padding:4px 0;border:0;background:transparent;text-align:left;white-space:nowrap}
.group-toggle:hover strong{color:var(--focus)}.group-chevron{font-size:18px;line-height:16px;transform:rotate(90deg);transition:transform .22s ease}.is-collapsed>.group-head .group-chevron{transform:rotate(0)}
.group-progress{color:var(--muted);font-size:11px;font-weight:400}.group-progress.pending{color:var(--focus)}
.group-body{display:grid;grid-template-rows:1fr;opacity:1;transition:grid-template-rows .24s cubic-bezier(.2,.7,.2,1),opacity .2s ease}.group-inner{min-height:0;overflow:hidden}
.is-collapsed>.group-body{grid-template-rows:0fr;opacity:0}.group.is-collapsed{padding-bottom:0}
.field-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));column-gap:32px;row-gap:0;padding:0}
.field-card{min-width:0;padding:7px 0 10px;border:0;border-radius:0;background:transparent}
.field-card.wide{grid-column:span 2}.field-card.full{grid-column:1/-1}
.field-display{display:none;width:100%;min-height:34px;padding:6px 9px;border:1px solid transparent;border-radius:5px;background:transparent;text-align:left;white-space:pre-wrap;overflow-wrap:anywhere;font-size:13px;line-height:1.6}
.read-mode:not(.is-editing)>.field-display{display:block}.read-mode:not(.is-editing)>.field-editor{display:none}
.field-display:hover{background:var(--hover);border-color:var(--line-strong)}.field-editor{min-width:0}
.na>.field-display,.na>.field-editor{display:none!important}
.toggle-field{display:flex;align-items:center;gap:8px;min-height:42px}.toggle-field .field-head{display:contents}.toggle-field .field-editor{order:-1}.toggle-field .check-control{min-height:28px}.toggle-field .check-control span{display:none}.toggle-field .status-select{margin-left:auto;order:3}.toggle-field .field-display{width:auto;min-height:28px;padding-block:2px}.toggle-field .na-note{min-height:28px}
.inherited-owner{grid-column:1/-1;padding-left:24px}.reference-value{min-height:34px;padding:6px 0;color:var(--muted);overflow-wrap:anywhere}
.field-head{display:flex;align-items:center;justify-content:space-between;gap:8px;min-height:24px;margin-bottom:5px}
.field-label{font-size:12px;font-weight:400}.field-label small{color:var(--muted);font-size:11px}
.required{color:var(--danger);margin-left:3px;font-style:normal}
.badge,.status-select{border:0;border-radius:4px;font-size:11px;font-weight:400}
.badge{padding:3px 7px}.status-select{width:66px;height:24px;padding:0 5px;cursor:pointer}
.s0{background:var(--auto);color:var(--auto-text)}.s1{background:var(--default);color:var(--default-text)}.s2{background:var(--import);color:var(--import-text)}.s3{background:var(--manual);color:var(--manual-text)}.s4{background:var(--na);color:var(--na-text)}
.control{width:100%;min-width:0;height:34px;border:1px solid var(--line-strong);border-radius:5px;background:var(--panel);padding:0 9px}
.control:hover,.cell:hover{border-color:var(--muted)}
.control:focus,.cell:focus{border-color:var(--focus);box-shadow:0 0 0 2px var(--focus-ring);outline:0}
.control-action{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:7px;align-items:center}.control-action .mini{height:34px}
textarea.control{height:72px;width:100%!important;max-width:100%;min-width:0;padding:8px 9px;resize:vertical;overflow-y:hidden;line-height:1.6}
input.control:read-only,textarea.control:read-only{background:var(--panel2);color:var(--muted)}
.check-control{min-height:34px;display:flex;align-items:center;gap:8px}
.check-control input,.multi-check input,.closed input{width:15px;height:15px;accent-color:var(--brand)}
.multi-check-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:4px 12px}
.multi-check{min-height:32px;display:flex;align-items:center;gap:7px;padding:3px 0;cursor:pointer;font-size:12px}
.na-note{display:none;min-height:34px;align-items:center;color:var(--na-text);font-size:12px}
.field-card.na .control,.field-card.na .control-action,.field-card.na .check-control,.field-card.na .multi-check-grid{display:none}.field-card.na .na-note{display:flex}
.table-wrap{overflow-x:auto;overflow-y:visible;border-top:1px solid var(--line)}
.data-table{width:100%;border-collapse:separate;border-spacing:0;table-layout:fixed;font-variant-numeric:tabular-nums}
.point-table,.segment-table,.signature-table{min-width:0}
.data-table th,.data-table td{padding:7px 6px;border-bottom:1px solid var(--line);text-align:left;vertical-align:middle;font-size:12px}
.data-table th{position:sticky;top:0;z-index:2;background:var(--panel2);color:var(--muted);font-size:11px;font-weight:500;white-space:nowrap}
.data-table tbody tr:hover{background:var(--hover)}
.cell{width:100%;min-width:0;height:30px;padding:0 6px;border:1px solid transparent;border-radius:4px;background:transparent;outline:0;font-size:12px}
input.cell:read-only{color:var(--muted)}
.cell.point-number{width:52px}.point-number-head,.point-number-data{width:58px;max-width:58px}
.delete-head,.delete-data{width:44px;text-align:center!important}
.col-point-coordinate{width:122px}.col-marker{width:112px}.col-segment-point{width:66px}.col-segment-middle{width:92px}.col-segment-distance{width:76px}.col-segment-category{width:88px}.col-segment-position{width:78px}.col-segment-owner{width:112px}.col-segment-direction{width:76px}.col-signature-point{width:68px}.col-signature-middle{width:94px}.col-signature-owner{width:118px}.col-signature-person{width:112px}.col-signature-date{width:116px}
.mini{height:28px;font-size:12px}.icon-btn{border:0;background:transparent;border-radius:4px;width:28px;height:28px;padding:0;font-size:18px;line-height:1}
.section-tools{display:flex;align-items:center;justify-content:flex-end;gap:8px;flex:0 0 auto;padding:0}.closed{display:flex;align-items:center;gap:7px;font-size:12px;white-space:nowrap}
.building{margin:0;border:0;border-top:1px solid var(--line);border-radius:0;padding:0 0 16px}
.building-head{display:flex;justify-content:space-between;align-items:center;padding:14px 0 4px}.building-head strong{font-size:12px;font-weight:600}
/* The list stays compact; only one building and one field category is mounted. */
.building-workbench{max-width:1460px;margin:0 auto 12px;padding-bottom:24px;border-bottom:1px solid var(--line)}
.building-workbench-head{display:flex;align-items:center;gap:12px;min-height:48px}.building-workbench h2{margin:0;font-size:14px;font-weight:600}.building-workbench-head>span{font-size:11px;color:var(--muted)}
.building-master-detail{display:grid;grid-template-columns:190px minmax(0,1fr);gap:24px;align-items:start}
.building-master{border-right:1px solid var(--line);padding-right:16px;position:sticky;top:0}.building-master h3{margin:0 0 10px;font-size:12px;font-weight:500;color:var(--muted)}
.building-list{max-height:360px;overflow:auto;scrollbar-width:thin}.building-list-empty{color:var(--muted);font-size:12px;padding:12px 8px}
.building-item{display:block;width:100%;padding:10px 9px;margin-bottom:4px;border:0;border-radius:5px;background:transparent;text-align:left}.building-item:hover,.building-add:hover{background:var(--hover)}.building-item.active{background:var(--selected)}
.building-item-main{display:flex;align-items:baseline;gap:8px;justify-content:space-between}.building-item strong{min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;font-size:12px;font-weight:500}.building-item-main>span{flex:0 0 auto;font-size:11px;font-variant-numeric:tabular-nums;color:var(--muted)}.building-item small{display:block;margin-top:3px;font-size:10px;color:var(--focus)}
.building-add{width:100%;margin-top:8px;padding:9px;border:0;border-top:1px solid var(--line);border-radius:0;background:transparent;text-align:left;font-size:12px}
.building-detail{min-width:0}.building-detail-head{display:flex;align-items:center;justify-content:space-between;gap:12px;min-height:42px;padding-bottom:10px;border-bottom:1px solid var(--line)}.building-detail-head h3{min-width:0;margin:0;font-size:14px;font-weight:600;overflow-wrap:anywhere}
.building-summary{margin:12px 0 0}.building-summary>div{display:grid;grid-template-columns:100px minmax(0,1fr);gap:16px;padding:8px 0}.building-summary dt{color:var(--muted);font-size:12px}.building-summary dd{margin:0;overflow-wrap:anywhere;font-variant-numeric:tabular-nums}
.building-categories{display:flex;gap:16px;margin:8px 0 10px;border-bottom:1px solid var(--line)}.building-categories button{display:flex;align-items:center;gap:5px;padding:10px 0;border:0;border-bottom:2px solid transparent;background:transparent;color:var(--muted);font-size:12px}.building-categories button[aria-selected=true]{border-bottom-color:var(--focus);color:var(--text)}.building-categories small{color:var(--focus);font-size:10px}
@container parcel-workspace (max-width:900px){.building-master-detail{grid-template-columns:170px minmax(0,1fr);gap:18px}.building-master{padding-right:12px}.building-categories{gap:12px}}
.empty{padding:48px 16px;text-align:center;color:var(--muted);font-size:12px}
.export-menu{position:relative;flex:0 0 104px}
.export-options{position:absolute;right:0;top:calc(100% + 6px);z-index:40;width:224px;padding:5px;border:1px solid var(--line-strong);border-radius:8px;background:var(--panel);box-shadow:var(--popup-shadow)}
.export-options[hidden]{display:none}
.export-option{display:flex;width:100%;align-items:center;justify-content:space-between;gap:12px;padding:9px 10px;border:0;border-radius:4px;background:transparent;text-align:left;font-size:12px}
.export-option:hover{background:var(--hover)}.export-option small{color:var(--muted);font-size:11px}
.toast{position:fixed;right:22px;bottom:20px;z-index:50;max-width:440px;padding:10px 14px;border:1px solid var(--line-strong);border-radius:8px;background:var(--panel);color:var(--text);box-shadow:var(--popup-shadow);animation:reFade .15s ease both}
.toast.success{border-left:3px solid var(--ok)}.toast.error{border-left:3px solid var(--danger)}
.modal-mask{position:fixed;inset:0;z-index:45;display:grid;place-items:center;padding:24px;background:var(--overlay);animation:reFade .12s ease both}
.modal{width:min(660px,100%);max-height:calc(100vh - 48px);overflow:auto;border:1px solid var(--line-strong);border-radius:10px;background:var(--panel);box-shadow:var(--popup-shadow)}
.modal-head{display:flex;justify-content:space-between;align-items:flex-start;gap:16px;padding:18px 20px;border-bottom:1px solid var(--line)}
.modal-head h2{margin:0;font-size:15px;font-weight:600}.modal-head p{margin:6px 0 0;color:var(--muted);font-size:12px;line-height:1.55}
.modal-body{padding:18px 20px}.modal-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.modal-field{display:grid;gap:6px}.modal-field.full{grid-column:1/-1}.modal-field>span{font-size:12px}
.modal-foot{display:flex;justify-content:flex-end;gap:8px;padding:12px 20px;border-top:1px solid var(--line)}
.choice-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:10px}.choice{display:flex;align-items:center;gap:9px;padding:12px;border:1px solid var(--line-strong);border-radius:5px;cursor:pointer}.choice:has(input:checked){border-color:var(--focus);background:var(--hover)}
.range-summary{margin-bottom:13px;padding:10px 0;border-bottom:1px solid var(--line);color:var(--muted);font-size:12px}
.source-meta{display:flex;flex-wrap:wrap;gap:12px;margin-top:8px}.source-meta span{color:var(--muted);font-size:11px}
@container parcel-workspace (max-width:1100px){.segment-table .col-segment-owner{display:none}}
@container parcel-workspace (max-width:1040px){.signature-table .col-signature-date{display:none}}
@container parcel-workspace (max-width:980px){.segment-table .col-segment-middle{display:none}}
@container parcel-workspace (max-width:920px){.signature-table .col-signature-self{display:none}}
@container parcel-workspace (max-width:900px){.point-table .col-point-coordinate{display:none}}
@container parcel-workspace (max-width:880px){.segment-table .col-segment-direction{display:none}}
@container parcel-workspace (max-width:800px){.segment-table .col-segment-distance,.signature-table .col-signature-neighbor{display:none}.group-progress{font-size:10px}.section-tools{gap:5px}.mini{font-size:11px;padding-inline:7px}.group-head{gap:8px}}
@keyframes reFade{from{opacity:0}to{opacity:1}}
.floor-heading{display:grid;grid-template-columns:minmax(80px,1fr)64px 80px 90px 24px;gap:8px;align-items:center;margin:8px 0;font-size:12px}.floor-heading input{min-width:0;width:100%;height:30px;padding:4px 7px;border:1px solid var(--line-strong);border-radius:5px;background:var(--panel)}.floor-heading label{display:flex;align-items:center;gap:5px}.floor-heading input[type=checkbox]{width:14px;height:14px;margin:0;padding:0;accent-color:var(--brand)}.floor-entry{padding:10px 0;border-bottom:1px solid var(--line)}.floor-entry details{font-size:12px}.floor-entry p{overflow-wrap:anywhere}.building-area-calculation{margin-top:20px;border-top:1px solid var(--line);padding-top:16px}.building-area-calculation summary{cursor:pointer;color:var(--brand);font-size:13px}.building-area-calculation p{font-family:var(--content-font);font-size:12px;overflow-wrap:anywhere}.building-area-calculation>div{padding:10px 0;border-top:1px solid var(--line);font-size:12px}.building-area-calculation strong{font-weight:500}
.no-animations *,.no-animations *:before,.no-animations *:after{animation:none!important;transition:none!important;scroll-behavior:auto!important}
@media(prefers-reduced-motion:reduce){*,*:before,*:after{animation:none!important;transition:none!important}}
@media(max-width:1180px){.top{padding-inline:14px}.content{padding-inline:20px}.parcel-picker select{width:190px}.field-grid{column-gap:24px}}
@media(max-width:840px){.top{padding-inline:10px}.toolbar{gap:5px}.parcel-picker{gap:6px}.parcel-picker select{width:150px}.content{padding-inline:16px}.field-grid{column-gap:20px}}
";
        }
    }
}
