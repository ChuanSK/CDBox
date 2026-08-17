using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.Settings;
using CDBox.Shared.UI;

namespace CDBox.RealEstate.UI
{
    public static class ParcelSurveyEditorPage
    {
        public const string PageId = "realestate-parcel-survey-editor";
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static CDBoxPageDefinition Create(ParcelSurveyStore store)
        {
            if (store == null) throw new ArgumentNullException("store");
            return new CDBoxPageDefinition(PageId, "宗地调查数据编辑器",
                delegate
                {
                    ParcelSurveyRecord record = store.Current();
                    return BuildHtml(record, ParcelSurveyValidator.Validate(record));
                },
                delegate(CDBoxPageRouteRequest request)
                {
                    return Route(request, store);
                })
            {
                Width = 1380,
                Height = 900,
                MinimumWidth = 1040,
                MinimumHeight = 700
            };
        }

        public static string BuildHtml(ParcelSurveyRecord record,
            ParcelSurveyValidationResult validation)
        {
            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            validation = validation ?? ParcelSurveyValidator.Validate(record);
            var context = new PageContext
            {
                Record = record,
                Validation = validation,
                Fields = ParcelSurveyFieldCatalog.Fields,
                BuildingFields = ParcelSurveyFieldCatalog.BuildingFields,
                Statuses = new List<StatusOption>
                {
                    new StatusOption(0, "自动"),
                    new StatusOption(1, "默认"),
                    new StatusOption(2, "导入"),
                    new StatusOption(3, "人工"),
                    new StatusOption(4, "不适用")
                },
                MarkerTypes = new[] { "钢钉", "水泥桩", "石灰桩", "喷涂", "木桩", "其他" },
                LineCategories = new[] { "围墙", "墙壁", "门墩", "道路", "田埂", "沟渠", "铁丝网", "界址线", "其他" },
                LinePositions = new[] { "内", "中", "外", "待确认" }
            };

            var html = new StringBuilder(70000);
            html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
                .Append("<title>宗地调查数据编辑器</title><style>")
                .Append(@"
:root{--bg:#f3f6fb;--panel:#fff;--panel2:#f8faff;--text:#172033;--muted:#66758b;--line:#d9e2ef;--brand:#316fe8;--brand2:#7047eb;--ok:#087f5b;--warn:#b7791f;--danger:#b42318;--auto:#e8f2ff;--default:#eeeafe;--import:#e7f8ef;--manual:#fff4d8;--na:#edf0f4}
*{box-sizing:border-box}html,body{margin:0;min-height:100%;background:var(--bg);color:var(--text);font-family:'Microsoft YaHei UI','Segoe UI',sans-serif}button,input,select,textarea{font:inherit}.page{min-height:100vh}.top{position:sticky;top:0;z-index:20;padding:16px 22px 13px;border-bottom:1px solid var(--line);background:rgba(255,255,255,.97);box-shadow:0 8px 24px rgba(30,55,95,.08)}.top-row{display:flex;align-items:center;justify-content:space-between;gap:20px}.identity{min-width:280px}.kicker{font-size:10px;font-weight:900;letter-spacing:.14em;color:var(--brand)}h1{margin:4px 0 3px;font-size:22px}.parcel{font-size:12px;color:var(--muted)}.completion{display:flex;align-items:center;gap:10px;min-width:240px}.ring{width:48px;height:48px;display:grid;place-items:center;border-radius:50%;background:conic-gradient(var(--brand) calc(var(--p)*1%),#e5ebf4 0);position:relative}.ring:after{content:'';position:absolute;inset:5px;background:#fff;border-radius:50%}.ring strong{z-index:1;font-size:11px}.metrics{display:flex;gap:9px}.metric{min-width:74px;padding:7px 9px;border:1px solid var(--line);border-radius:10px;background:var(--panel2);text-align:center}.metric b{display:block;font-size:16px}.metric small{font-size:10px;color:var(--muted)}.actions{display:flex;gap:8px}.btn{height:36px;padding:0 14px;border:1px solid var(--line);border-radius:9px;background:#fff;color:var(--text);font-weight:800;cursor:pointer}.btn:hover{border-color:var(--brand);color:var(--brand)}.btn.primary{border:0;color:#fff;background:linear-gradient(135deg,var(--brand),var(--brand2))}.btn:disabled{opacity:.42;cursor:not-allowed}.tabs{position:sticky;top:106px;z-index:15;display:flex;gap:6px;padding:10px 22px;border-bottom:1px solid var(--line);background:rgba(243,246,251,.97);overflow:auto}.tab{padding:9px 13px;border:1px solid transparent;border-radius:9px;background:transparent;color:var(--muted);font-size:12px;font-weight:800;white-space:nowrap;cursor:pointer}.tab.active{border-color:#cddcff;background:#fff;color:var(--brand);box-shadow:0 5px 14px rgba(40,80,150,.08)}.content{max-width:1460px;margin:auto;padding:18px 22px 50px}.intro{display:flex;justify-content:space-between;gap:18px;margin-bottom:14px;padding:13px 15px;border:1px solid #cadcff;border-radius:12px;background:#edf4ff}.intro strong{font-size:13px}.intro p{margin:4px 0 0;color:#51647f;font-size:11px;line-height:1.6}.legend{display:flex;flex-wrap:wrap;align-items:center;gap:6px}.badge,.status-select{border:1px solid transparent;border-radius:999px;font-size:10px;font-weight:850}.badge{padding:4px 8px}.s0{background:var(--auto);color:#245da9}.s1{background:var(--default);color:#5b3cb0}.s2{background:var(--import);color:#087047}.s3{background:var(--manual);color:#8a5a00}.s4{background:var(--na);color:#596477}.group{margin-bottom:16px;border:1px solid var(--line);border-radius:15px;background:var(--panel);box-shadow:0 10px 26px rgba(30,55,95,.045);overflow:hidden}.group-head{display:flex;justify-content:space-between;align-items:center;padding:12px 15px;border-bottom:1px solid var(--line);background:linear-gradient(90deg,var(--panel2),#fff)}.group-head strong{font-size:13px}.group-head small{color:var(--muted)}.field-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;padding:14px}.field-card{min-width:0;padding:10px;border:1px solid #e2e8f1;border-radius:11px;background:#fff}.field-card.wide{grid-column:span 2}.field-card.full{grid-column:1/-1}.field-card.conditional{border-left:3px solid #b9a7f5}.field-head{display:flex;align-items:center;justify-content:space-between;gap:8px;margin-bottom:7px}.field-label{font-size:11px;font-weight:850}.required{color:var(--danger);margin-left:3px}.status-select{width:66px;height:24px;padding:0 5px;outline:0}.control{width:100%;height:36px;border:1px solid var(--line);border-radius:8px;background:#fff;color:var(--text);padding:0 9px;outline:0}.control:focus{border-color:var(--brand);box-shadow:0 0 0 3px rgba(49,111,232,.09)}textarea.control{height:72px;padding:8px 9px;resize:vertical;line-height:1.55}select[multiple].control{height:66px;padding:4px}.check-control{height:36px;display:flex;align-items:center;gap:8px}.hint{display:block;margin-top:5px;color:var(--muted);font-size:9px;line-height:1.5}.na-note{display:none;height:36px;align-items:center;color:#596477;font-size:11px}.field-card.na .control,.field-card.na .check-control{display:none}.field-card.na .na-note{display:flex}.table-wrap{overflow:auto}.data-table{width:100%;min-width:1040px;border-collapse:collapse}.data-table th,.data-table td{padding:7px 6px;border-bottom:1px solid #e4e9f1;text-align:left;vertical-align:top}.data-table th{position:sticky;top:0;background:#f6f8fc;color:#53627a;font-size:9px;white-space:nowrap}.data-table td{font-size:10px}.cell{width:100%;min-width:70px;height:31px;padding:0 7px;border:1px solid var(--line);border-radius:7px;background:#fff}.cell.small{min-width:48px}.cell.long{min-width:150px}.cell:read-only{background:#f0f3f7;color:#657287}.row-actions{display:flex;gap:5px}.mini{height:29px;padding:0 8px;border:1px solid var(--line);border-radius:7px;background:#fff;color:var(--brand);cursor:pointer}.mini.danger{color:var(--danger)}.section-tools{display:flex;align-items:center;justify-content:space-between;gap:12px;padding:10px 14px}.section-tools p{margin:0;color:var(--muted);font-size:10px}.closed{display:flex;align-items:center;gap:7px;font-size:11px;font-weight:750}.pages{padding:8px 14px;color:var(--muted);font-size:10px;background:#fafbfe}.building{margin:14px;border:1px solid #dfe6f1;border-radius:12px;overflow:hidden}.building-head{display:flex;justify-content:space-between;align-items:center;padding:10px 12px;background:#f6f8fc}.building-head strong{font-size:12px}.empty{padding:32px;text-align:center;color:var(--muted);font-size:12px}.issues{display:grid;gap:7px;padding:14px}.issue{padding:9px 11px;border-radius:9px;background:#fff2f0;color:#8f2018;font-size:11px}.issue.warn{background:#fff7e6;color:#855d08}.issue.ok{background:#eaf8f1;color:#087047}.export-flow{padding:14px;display:flex;flex-wrap:wrap;align-items:center;gap:7px;color:#50617a;font-size:11px}.flow-step{padding:7px 9px;border-radius:8px;background:#edf3fb}.arrow{color:#94a3b8}.toast{position:fixed;right:22px;bottom:20px;z-index:50;max-width:440px;padding:11px 14px;border-radius:10px;background:#172033;color:#fff;box-shadow:0 16px 38px rgba(15,23,42,.28)}.toast.success{background:var(--ok)}.toast.error{background:var(--danger)}
@media(max-width:1180px){.top-row{flex-wrap:wrap}.tabs{top:160px}.field-grid{grid-template-columns:repeat(2,minmax(0,1fr))}.metrics{order:3}.content{padding-inline:14px}}@media(max-width:760px){.top{padding:12px}.tabs{top:220px;padding-inline:12px}.field-grid{grid-template-columns:1fr}.field-card.wide{grid-column:auto}.intro{flex-direction:column}.actions{flex-wrap:wrap}}
")
                .Append("</style></head><body><main class=\"page\" data-page=\"")
                .Append(PageId).Append("\"><header class=\"top\"><div class=\"top-row\">")
                .Append("<div class=\"identity\"><span class=\"kicker\">CDBox · 不动产</span><h1>宗地调查数据编辑器</h1><div id=\"currentParcel\" class=\"parcel\"></div></div>")
                .Append("<div class=\"completion\"><div id=\"ring\" class=\"ring\"><strong id=\"completeValue\"></strong></div><div><b>完整度</b><small>按当前必填业务字段计算</small></div></div>")
                .Append("<div class=\"metrics\"><div class=\"metric\"><b id=\"missingCount\"></b><small>必填缺失</small></div><div class=\"metric\"><b id=\"pendingCount\"></b><small>待确认</small></div><div class=\"metric\"><b id=\"paginationCount\"></b><small>分页异常</small></div></div>")
                .Append("<div class=\"actions\"><button id=\"saveParcel\" class=\"btn\" type=\"button\">保存宗地</button><button id=\"checkData\" class=\"btn\" type=\"button\">检查数据</button><button id=\"exportSurvey\" class=\"btn primary\" type=\"button\">导出调查表</button></div>")
                .Append("</div></header><nav id=\"tabs\" class=\"tabs\"></nav><section id=\"content\" class=\"content\"></section></main>")
                .Append("<script>window.CDBoxParcelSurveyContext=")
                .Append(SafeJson(context)).Append(";</script><script>")
                .Append(BuildScript())
                .Append("</script></body></html>");
            return html.ToString();
        }

        private static CDBoxPageRouteResult Route(CDBoxPageRouteRequest request,
            ParcelSurveyStore store)
        {
            var result = new CDBoxPageRouteResult { Handled = true };
            if (request == null) return result;
            string name = (request.Name ?? string.Empty).Trim().ToLowerInvariant();
            if (name == "ready") return result;
            try
            {
                ParcelSurveyRecord record = Serializer.Deserialize<ParcelSurveyRecord>(
                    request.Argument ?? string.Empty) ?? new ParcelSurveyRecord();
                record.Normalize();
                if (name == "saveparcel")
                {
                    store.Save(record);
                    result.RefreshPage = true;
                    result.ToastKind = "success";
                    result.ToastMessage = "当前宗地调查数据已保存。";
                    return result;
                }
                if (name == "checkparcel")
                {
                    store.Save(record);
                    ParcelSurveyValidationResult validation =
                        ParcelSurveyValidator.Validate(record);
                    result.RefreshPage = true;
                    result.ToastKind = validation.CanExport ? "success" : "error";
                    result.ToastMessage = validation.CanExport
                        ? "数据检查通过，已具备调查表导出条件。"
                        : "检查完成：缺失 " + validation.RequiredMissingCount
                            + " 项，待确认 " + validation.PendingConfirmationCount
                            + " 项，另有 " + validation.Issues.Count + " 条检查信息。";
                    return result;
                }
                if (name == "exportparcel")
                {
                    store.Save(record);
                    ParcelSurveyValidationResult validation =
                        ParcelSurveyValidator.Validate(record);
                    if (!validation.CanExport)
                    {
                        result.ToastKind = "error";
                        result.ToastMessage = "当前数据未通过导出条件检查。";
                        result.RefreshPage = true;
                    }
                    else
                    {
                        result.ToastKind = "success";
                        result.ToastMessage = "宗地业务数据已通过导出检查；XLS/PDF 模板适配器将在后续导出功能中接入。";
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "宗地调查数据无效：" + ex.Message;
                return result;
            }
            result.Handled = false;
            return result;
        }

        private static string SafeJson(object value)
        {
            return Serializer.Serialize(value).Replace("</", "<\\/")
                .Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
        }

        private static string BuildScript()
        {
            return @"
(function(){
var ctx=window.CDBoxParcelSurveyContext||{},draft=clone(ctx.Record||{}),activeTab=1;
var tabNames=['项目与人员','宗地基本信息','权利人与权属','土地用途与面积','界址调查','房屋调查','调查审核与导出'];
function clone(v){return JSON.parse(JSON.stringify(v||{}));}
function post(n,a){try{chrome.webview.postMessage('studio|'+n+'|'+encodeURIComponent(a||''));}catch(e){toast('无法连接页面宿主。','error');}}
function esc(v){return String(v==null?'':v).replace(/[&<>']/g,function(c){return c==='&'?'&amp;':c==='<'?'&lt;':c==='>'?'&gt;':'&#39;';});}
function attr(v){return esc(v).replace(/`/g,'&#96;');}
function ensure(){draft.Fields=draft.Fields||{};draft.Boundary=draft.Boundary||{};draft.Boundary.Points=draft.Boundary.Points||[];draft.Boundary.Segments=draft.Boundary.Segments||[];draft.Boundary.SignatureGroups=draft.Boundary.SignatureGroups||[];draft.Buildings=draft.Buildings||[];draft.LayoutDiagnostics=draft.LayoutDiagnostics||{};}
function value(key,fields){fields=fields||draft.Fields;return fields[key]||(fields[key]={TextValue:'',NumericValue:null,BooleanValue:false,Selections:[],Status:3,Confirmed:false});}
function def(key){return (ctx.Fields||[]).find(function(x){return x.Key===key;});}
function statusName(n){var s=(ctx.Statuses||[]).find(function(x){return Number(x.Value)===Number(n);});return s?s.Label:'人工';}
function statusOptions(d,v){return (ctx.Statuses||[]).filter(function(x){return Number(x.Value)!==4||d.AllowNotApplicable;}).map(function(x){return `<option value='${x.Value}'${Number(x.Value)===Number(v)?' selected':''}>${esc(x.Label)}</option>`;}).join('');}
function optionList(items,current,empty){var h=empty===false?'':`<option value=''>请选择</option>`;return h+(items||[]).map(function(x){return `<option value='${attr(x)}'${String(x)===String(current)?' selected':''}>${esc(x)}</option>`;}).join('');}
function conditionActive(d,fields){if(!d.ConditionKey)return true;var c=value(d.ConditionKey,fields),op=d.ConditionOperator||'';if(op==='true')return !!c.BooleanValue;if(op==='false')return !c.BooleanValue;if(op==='neq')return !!c.TextValue&&c.TextValue!==d.ConditionValue;return c.TextValue===d.ConditionValue;}
function renderTop(){var v=ctx.Validation||{},p=value('parcel.parcelSeaCode').TextValue||value('parcel.parcelCode').TextValue||('未命名宗地 · '+String(draft.Id||'').slice(0,8));document.getElementById('currentParcel').textContent='当前宗地：'+p;var pc=Number(v.CompletenessPercent)||0,ring=document.getElementById('ring');ring.style.setProperty('--p',pc);document.getElementById('completeValue').textContent=pc+'%';document.getElementById('missingCount').textContent=v.RequiredMissingCount||0;document.getElementById('pendingCount').textContent=v.PendingConfirmationCount||0;document.getElementById('paginationCount').textContent=v.PaginationAnomalyCount||0;document.getElementById('exportSurvey').disabled=!v.CanExport;}
function renderTabs(){document.getElementById('tabs').innerHTML=tabNames.map(function(n,i){return `<button class='tab${activeTab===i+1?' active':''}' data-tab='${i+1}' type='button'>${i+1}. ${n}</button>`;}).join('');}
function fieldControl(d,v,scope){var id=scope+'-'+d.Key.replace(/[^a-zA-Z0-9]/g,'-'),disabled=d.ReadOnly?' readonly':'';if(d.Kind==='textarea')return `<textarea id='${id}' class='control' data-role='value'${disabled}>${esc(v.TextValue||'')}</textarea>`;if(d.Kind==='select')return `<select id='${id}' class='control' data-role='value'>${optionList(d.Options,v.TextValue)}</select>`;if(d.Kind==='multiselect')return `<select id='${id}' class='control' data-role='value' multiple>${(d.Options||[]).map(function(x){return `<option value='${attr(x)}'${(v.Selections||[]).indexOf(x)>=0?' selected':''}>${esc(x)}</option>`;}).join('')}</select>`;if(d.Kind==='checkbox')return `<label class='check-control'><input data-role='value' type='checkbox'${v.BooleanValue?' checked':''}> <span>${v.BooleanValue?'是':'否'}</span></label>`;var type=d.Kind==='number'?'number':d.Kind==='date'?'date':'text',step=d.Kind==='number'?' step=.01':'';return `<input id='${id}' class='control' data-role='value' type='${type}' value='${attr(d.Kind==='number'?(v.NumericValue==null?'':v.NumericValue):(v.TextValue||''))}'${step}${disabled}>`;}
function renderField(d,fields,scope,buildingId){var v=value(d.Key,fields),wide=d.Kind==='textarea'?' wide':'',conditional=d.ConditionKey?' conditional':'',hidden=!conditionActive(d,fields)?' style=display:none':'',na=Number(v.Status)===4?' na':'';return `<div class='field-card${wide}${conditional}${na}' data-key='${attr(d.Key)}' data-kind='${attr(d.Kind)}' data-scope='${scope}'${buildingId?` data-building='${buildingId}'`:''}${hidden}><div class='field-head'><span class='field-label'>${esc(d.Label)}${d.Required?`<i class='required'>*</i>`:''}${d.Unit?` <small>(${esc(d.Unit)})</small>`:''}</span><select class='status-select s${Number(v.Status)||0}' data-role='status'>${statusOptions(d,v.Status)}</select></div>${fieldControl(d,v,scope)}<div class='na-note'>明确输出 /</div>${d.Help?`<small class='hint'>${esc(d.Help)}</small>`:''}</div>`;}
function groupsFor(fields,definitions,scope,buildingId){var defs=(definitions||[]).filter(function(d){return d.Tab===activeTab;}),groups=[];defs.forEach(function(d){if(groups.indexOf(d.Group)<0)groups.push(d.Group);});return groups.map(function(g){return `<section class='group'><div class='group-head'><strong>${esc(g)}</strong><small>业务字段仅维护一份</small></div><div class='field-grid'>${defs.filter(function(d){return d.Group===g;}).map(function(d){return renderField(d,fields,scope,buildingId);}).join('')}</div></section>`;}).join('');}
function intro(){return `<div class='intro'><div><strong>${esc(tabNames[activeTab-1])}</strong><p>状态用于记录数据来源；“不适用”会明确输出 /，不会与尚未填写混淆。</p></div><div class='legend'>${(ctx.Statuses||[]).map(function(s){return `<span class='badge s${s.Value}'>${esc(s.Label)}</span>`;}).join('')}</div></div>`;}
function renderContent(){var h=intro();if(activeTab===5)h+=renderBoundary();else if(activeTab===6)h+=linkedReferences('house')+groupsFor(draft.Fields,ctx.Fields,'parcel','')+renderBuildings();else h+=(activeTab===7?linkedReferences('audit'):'')+groupsFor(draft.Fields,ctx.Fields,'parcel','');if(activeTab===7)h+=renderAuditFooter();document.getElementById('content').innerHTML=h;bindContent();}
function linkedReferences(kind){var items=kind==='house'?[['项目名称','project.name'],['邮政编码','project.postalCode']]:[['填表人','project.formFiller'],['填表日期','project.formDate'],['权属调查员','project.rightsSurveyor'],['权属调查日期','project.rightsSurveyDate'],['测量员','project.surveyor'],['测量日期','project.measureDate'],['审核人','project.reviewer'],['审核日期','project.reviewDate']];if(kind==='house'&&value('house.followParcelOwner').BooleanValue)items=items.concat([['沿用的宗地权利人','rights.ownerName'],['证件种类','rights.certificateType'],['证件号码','rights.certificateNumber'],['通讯地址','rights.contactAddress'],['联系电话','rights.contactPhone']]);return `<section class='group'><div class='group-head'><strong>${kind==='house'?'跨表共用信息':'调查审核人员引用'}</strong><small>只在“项目与人员”或“权利人与权属”页签维护一次</small></div><div class='field-grid'>${items.map(function(x){var d=def(x[1]),v=value(x[1]);return `<div class='field-card'><div class='field-head'><span class='field-label'>${esc(x[0])}</span><span class='badge s${v.Status}'>${statusName(v.Status)}</span></div><div class='control' style='display:flex;align-items:center;background:#f0f3f7'>${esc(d?v.DisplayValue||v.TextValue:v.TextValue)||'尚未填写'}</div><small class='hint'>引用业务字段：${esc(x[1])}</small></div>`;}).join('')}</div></section>`;}
function renderBoundary(){var b=draft.Boundary,v=ctx.Validation||{};return `<section class='group'><div class='group-head'><strong>CAD 宗地几何</strong><small>坐标由 CAD 读取并只读显示</small></div><div class='section-tools'><label class='closed'><input id='boundaryClosed' type='checkbox'${b.ParcelBoundaryClosed?' checked':''}> 宗地权属线已闭合</label><button class='mini' data-add='point' type='button'>+ 添加界址点</button></div>${pointTable()}</section><section class='group'><div class='group-head'><strong>界址段列表</strong><small>界址标示表每页最多 26 段</small></div><div class='section-tools'><p>连续段应首尾相接；相邻宗未识别时必须明确标记已处理。</p><button class='mini' data-add='segment' type='button'>+ 添加界址段</button></div>${segmentTable()}<div class='pages'>预计生成界址标示表 ${v.BoundarySegmentPageCount||1} 页；超过 26 段自动生成续表。</div></section><section class='group'><div class='group-head'><strong>界址签章分组</strong><small>同一邻宗的连续界址段自动合并，每页最多 13 组</small></div><div class='section-tools'><p>签章状态只记录业务处理，不会伪造签章。</p><div><button class='mini' data-auto-signature='1' type='button'>按邻宗自动分组</button> <button class='mini' data-add='signature' type='button'>+ 添加签章组</button></div></div>${signatureTable()}<div class='pages'>预计生成界址签章表 ${v.SignatureGroupPageCount||1} 页；超过 13 组自动续表。</div></section>`+groupsFor(draft.Fields,ctx.Fields,'parcel','');}
function pointTable(){var rows=draft.Boundary.Points.map(function(p,i){return `<tr data-point='${i}'><td>${i+1}</td><td><input class='cell' data-p='PointNumber' value='${attr(p.PointNumber||'')}'></td><td><input class='cell' value='${attr(p.X==null?'':p.X)}' readonly></td><td><input class='cell' value='${attr(p.Y==null?'':p.Y)}' readonly></td><td><select class='cell' data-p='MarkerType'>${optionList(ctx.MarkerTypes,p.MarkerType)}</select></td><td><input class='cell long' data-p='Description' value='${attr(p.Description||'')}'></td><td><select class='cell' data-p='Status'>${statusOptions({AllowNotApplicable:false},p.Status)}</select></td><td><label><input data-p='Confirmed' type='checkbox'${p.Confirmed?' checked':''}> 已确认</label></td><td><button class='mini danger' data-remove-point='${i}' type='button'>删除</button></td></tr>`;}).join('');return `<div class='table-wrap'><table class='data-table'><thead><tr><th>序号</th><th>界址点号</th><th>X 坐标</th><th>Y 坐标</th><th>界标种类</th><th>点位说明</th><th>状态</th><th>确认</th><th></th></tr></thead><tbody>${rows||`<tr><td colspan='9' class='empty'>尚无界址点，请从 CAD 识别或添加记录。</td></tr>`}</tbody></table></div>`;}
function segmentTable(){var rows=draft.Boundary.Segments.map(function(s,i){return `<tr data-segment='${i}'><td>${Math.floor(i/26)+1}</td><td><input class='cell small' data-p='StartPointNumber' value='${attr(s.StartPointNumber||'')}'></td><td><input class='cell small' data-p='EndPointNumber' value='${attr(s.EndPointNumber||'')}'></td><td><input class='cell small' data-p='Distance' type='number' step='0.01' value='${attr(s.Distance==null?'':s.Distance)}'></td><td><select class='cell' data-p='LineCategory'>${optionList(ctx.LineCategories,s.LineCategory)}</select></td><td><select class='cell' data-p='LinePosition'>${optionList(ctx.LinePositions,s.LinePosition)}</select></td><td><input class='cell' data-p='NeighborParcelCode' value='${attr(s.NeighborParcelCode||'')}'></td><td><input class='cell' data-p='NeighborOwner' value='${attr(s.NeighborOwner||'')}'></td><td><input class='cell small' data-p='Direction' value='${attr(s.Direction||'')}'></td><td><input class='cell long' data-p='Description' value='${attr(s.Description||'')}'></td><td><select class='cell' data-p='Status'>${statusOptions({AllowNotApplicable:false},s.Status)}</select></td><td><label><input data-p='NeighborHandled' type='checkbox'${s.NeighborHandled?' checked':''}>邻宗已处理</label><br><label><input data-p='Confirmed' type='checkbox'${s.Confirmed?' checked':''}>已确认</label></td><td><button class='mini danger' data-remove-segment='${i}' type='button'>删除</button></td></tr>`;}).join('');return `<div class='table-wrap'><table class='data-table'><thead><tr><th>页</th><th>起点号</th><th>终点号</th><th>距离</th><th>线类别</th><th>线位置</th><th>相邻宗地代码</th><th>相邻权利人</th><th>方向</th><th>段说明</th><th>状态</th><th>处理/确认</th><th></th></tr></thead><tbody>${rows||`<tr><td colspan='13' class='empty'>尚无界址段。</td></tr>`}</tbody></table></div>`;}
function signatureTable(){var rows=draft.Boundary.SignatureGroups.map(function(g,i){return `<tr data-signature='${i}'><td>${Math.floor(i/13)+1}</td><td><input class='cell small' data-p='StartPointNumber' value='${attr(g.StartPointNumber||'')}'></td><td><input class='cell' data-p='MiddlePointNumbers' value='${attr(g.MiddlePointNumbers||'')}'></td><td><input class='cell small' data-p='EndPointNumber' value='${attr(g.EndPointNumber||'')}'></td><td><input class='cell' data-p='NeighborOwner' value='${attr(g.NeighborOwner||'')}'></td><td><input class='cell' data-p='NeighborParcelCode' value='${attr(g.NeighborParcelCode||'')}'></td><td><input class='cell' data-p='NeighborRepresentative' value='${attr(g.NeighborRepresentative||'')}'></td><td><input class='cell' data-p='ParcelRepresentative' value='${attr(g.ParcelRepresentative||'')}'></td><td><input class='cell' data-p='ConfirmationDate' type='date' value='${attr(g.ConfirmationDate||'')}'></td><td><input class='cell' data-p='SignatureStatus' value='${attr(g.SignatureStatus||'')}'></td><td><select class='cell' data-p='Status'>${statusOptions({AllowNotApplicable:false},g.Status)}</select></td><td><label><input data-p='PreservePaperSignatureBlank' type='checkbox'${g.PreservePaperSignatureBlank?' checked':''}>留纸签空白</label><br><label><input data-p='Confirmed' type='checkbox'${g.Confirmed?' checked':''}>已确认</label></td><td><button class='mini danger' data-remove-signature='${i}' type='button'>删除</button></td></tr>`;}).join('');return `<div class='table-wrap'><table class='data-table'><thead><tr><th>页</th><th>起点</th><th>中间点</th><th>终点</th><th>邻宗权利人</th><th>邻宗代码</th><th>邻宗指界人</th><th>本宗指界人</th><th>指界日期</th><th>签章状态</th><th>来源</th><th>处理/确认</th><th></th></tr></thead><tbody>${rows||`<tr><td colspan='13' class='empty'>尚无签章组，可按连续邻宗自动整理后添加。</td></tr>`}</tbody></table></div>`;}
function renderBuildings(){var rows=draft.Buildings.map(function(b,i){var title=value('building.number',b.Fields).TextValue||('房屋 '+(i+1));return `<article class='building'><div class='building-head'><strong>${esc(title)}</strong><button class='mini danger' data-remove-building='${attr(b.Id)}' type='button'>删除此幢</button></div><div class='field-grid'>${(ctx.BuildingFields||[]).map(function(d){return renderField(d,b.Fields,'building-'+b.Id,b.Id);}).join('')}</div></article>`;}).join('');return `<section class='group'><div class='group-head'><strong>每幢房屋</strong><small>每幢生成一张房屋调查表</small></div><div class='section-tools'><p>建筑总面积与占地总面积将在检查时与各幢数值汇总比对。</p><button class='mini' data-add='building' type='button'>+ 新增一幢</button></div>${rows||`<div class='empty'>尚未添加房屋。</div>`}</section>`;}
function renderAuditFooter(){var issues=(ctx.Validation&&ctx.Validation.Issues)||[],h=issues.length?issues.map(function(x){return `<div class='issue${x.Severity==='warning'?' warn':''}'>${esc(x.Message)}</div>`;}).join(''):`<div class='issue ok'>当前宗地已通过全部业务数据检查。</div>`;return `<section class='group'><div class='group-head'><strong>数据检查结果</strong><small>导出按钮仅在全部条件通过后启用</small></div><div class='issues'>${h}</div></section><section class='group'><div class='group-head'><strong>最终导出流程</strong><small>界面保存业务数据，不保存 Excel 单元格坐标</small></div><div class='export-flow'><span class='flow-step'>填写 / 自动识别</span><span class='arrow'>→</span><span class='flow-step'>完整性检查</span><span class='arrow'>→</span><span class='flow-step'>生成预览</span><span class='arrow'>→</span><span class='flow-step'>用户确认</span><span class='arrow'>→</span><span class='flow-step'>导出 XLS</span><span class='arrow'>→</span><span class='flow-step'>导出 PDF</span><span class='arrow'>→</span><span class='flow-step'>回读检查</span></div></section>`;}
function syncFields(){document.querySelectorAll('.field-card[data-key]').forEach(function(card){var key=card.dataset.key,kind=card.dataset.kind,fields=draft.Fields;if(card.dataset.building){var b=draft.Buildings.find(function(x){return x.Id===card.dataset.building;});if(!b)return;fields=b.Fields;}var v=value(key,fields),s=card.querySelector('[data-role=status]'),c=card.querySelector('[data-role=value]'),old=Number(v.Status);v.Status=Number(s?s.value:v.Status);if(v.Status===4){v.TextValue='/';v.NumericValue=null;v.BooleanValue=false;v.Selections=[];v.Confirmed=true;return;}if(old===4){v.TextValue='';v.NumericValue=null;v.BooleanValue=false;v.Selections=[];v.Confirmed=false;return;}if(!c)return;if(kind==='number'){v.NumericValue=c.value===''?null:Number(c.value);v.TextValue='';}else if(kind==='checkbox'){v.BooleanValue=!!c.checked;v.TextValue='';}else if(kind==='multiselect'){v.Selections=Array.from(c.selectedOptions).map(function(o){return o.value;});v.TextValue='';}else{v.TextValue=c.value||'';v.NumericValue=null;}});}
function syncTables(){var closed=document.getElementById('boundaryClosed');if(closed)draft.Boundary.ParcelBoundaryClosed=closed.checked;document.querySelectorAll('tr[data-point]').forEach(function(row){var p=draft.Boundary.Points[Number(row.dataset.point)];row.querySelectorAll('[data-p]').forEach(function(c){var k=c.dataset.p;p[k]=c.type==='checkbox'?c.checked:c.value;});});document.querySelectorAll('tr[data-segment]').forEach(function(row){var s=draft.Boundary.Segments[Number(row.dataset.segment)];row.querySelectorAll('[data-p]').forEach(function(c){var k=c.dataset.p,val=c.type==='checkbox'?c.checked:c.value;s[k]=k==='Distance'?(val===''?null:Number(val)):k==='Status'?Number(val):val;});});document.querySelectorAll('tr[data-signature]').forEach(function(row){var g=draft.Boundary.SignatureGroups[Number(row.dataset.signature)];row.querySelectorAll('[data-p]').forEach(function(c){var k=c.dataset.p;g[k]=c.type==='checkbox'?c.checked:k==='Status'?Number(c.value):c.value;});});}
function syncAll(){syncFields();syncTables();ensure();}
function bindContent(){document.querySelectorAll('[data-role=status]').forEach(function(s){s.onchange=function(){syncAll();renderContent();};});document.querySelectorAll('.check-control input').forEach(function(c){c.onchange=function(){var span=c.parentElement.querySelector('span');if(span)span.textContent=c.checked?'是':'否';};});var conditionKeys=(ctx.Fields||[]).filter(function(d){return !!d.ConditionKey;}).map(function(d){return d.ConditionKey;});document.querySelectorAll('.field-card[data-key]').forEach(function(card){if(conditionKeys.indexOf(card.dataset.key)<0)return;var c=card.querySelector('[data-role=value]');if(c)c.addEventListener('change',function(){syncAll();renderContent();});});document.querySelectorAll('[data-add]').forEach(function(b){b.onclick=function(){syncAll();add(b.dataset.add);renderContent();};});document.querySelectorAll('[data-auto-signature]').forEach(function(b){b.onclick=function(){syncAll();autoSignatureGroups();renderContent();};});document.querySelectorAll('[data-remove-point]').forEach(function(b){b.onclick=function(){syncAll();draft.Boundary.Points.splice(Number(b.dataset.removePoint),1);renderContent();};});document.querySelectorAll('[data-remove-segment]').forEach(function(b){b.onclick=function(){syncAll();draft.Boundary.Segments.splice(Number(b.dataset.removeSegment),1);renderContent();};});document.querySelectorAll('[data-remove-signature]').forEach(function(b){b.onclick=function(){syncAll();draft.Boundary.SignatureGroups.splice(Number(b.dataset.removeSignature),1);renderContent();};});document.querySelectorAll('[data-remove-building]').forEach(function(b){b.onclick=function(){syncAll();draft.Buildings=draft.Buildings.filter(function(x){return x.Id!==b.dataset.removeBuilding;});renderContent();};});}
function autoSignatureGroups(){var groups=[],current=null;(draft.Boundary.Segments||[]).forEach(function(s){var key=(s.NeighborParcelCode||'')+'|'+(s.NeighborOwner||'');if(!current||current._key!==key){current={_key:key,StartPointNumber:s.StartPointNumber||'',MiddlePointNumbers:'',EndPointNumber:s.EndPointNumber||'',NeighborOwner:s.NeighborOwner||'',NeighborParcelCode:s.NeighborParcelCode||'',NeighborRepresentative:'',ParcelRepresentative:'',ConfirmationDate:'',SignatureStatus:'待签章',PreservePaperSignatureBlank:true,Confirmed:false,Status:0,_middle:[]};groups.push(current);}else{current._middle.push(s.StartPointNumber||'');current.EndPointNumber=s.EndPointNumber||'';}});groups.forEach(function(g){g.MiddlePointNumbers=g._middle.filter(Boolean).join('、');delete g._middle;delete g._key;});draft.Boundary.SignatureGroups=groups;toast('已按连续相邻宗生成 '+groups.length+' 个签章组，请继续确认指界与签章信息。','success');}
function add(type){if(type==='point'){var n=draft.Boundary.Points.length+1;draft.Boundary.Points.push({Sequence:n,PointNumber:'J'+n,X:null,Y:null,MarkerType:'',Description:'',Confirmed:false,Status:0});}else if(type==='segment'){var a=draft.Boundary.Segments.length,b=draft.Boundary.Points;draft.Boundary.Segments.push({StartPointNumber:b[a]?b[a].PointNumber:'',EndPointNumber:b[a+1]?b[a+1].PointNumber:(b.length&&a===b.length-1?b[0].PointNumber:''),Distance:null,LineCategory:'',LinePosition:'待确认',NeighborParcelCode:'',NeighborOwner:'',Direction:'',Description:'',Confirmed:false,NeighborHandled:false,Status:0});}else if(type==='signature'){draft.Boundary.SignatureGroups.push({StartPointNumber:'',MiddlePointNumbers:'',EndPointNumber:'',NeighborOwner:'',NeighborParcelCode:'',NeighborRepresentative:'',ParcelRepresentative:'',ConfirmationDate:'',SignatureStatus:'',PreservePaperSignatureBlank:true,Confirmed:false,Status:0});}else if(type==='building'){var fs={};(ctx.BuildingFields||[]).forEach(function(d){fs[d.Key]={TextValue:'',NumericValue:null,BooleanValue:false,Selections:[],Status:d.DefaultStatus,Confirmed:false};});draft.Buildings.push({Id:'b'+Date.now()+Math.random().toString(16).slice(2),Fields:fs});}}
function toast(m,k){var old=document.querySelector('.toast');if(old)old.remove();var x=document.createElement('div');x.className='toast '+(k||'');x.textContent=m||'';document.body.appendChild(x);setTimeout(function(){x.remove();},3200);}
function init(){ensure();renderTop();renderTabs();renderContent();document.getElementById('tabs').onclick=function(e){var b=e.target.closest('[data-tab]');if(!b)return;syncAll();activeTab=Number(b.dataset.tab);renderTabs();renderContent();window.scrollTo(0,0);};document.getElementById('saveParcel').onclick=function(){syncAll();post('saveParcel',JSON.stringify(draft));};document.getElementById('checkData').onclick=function(){syncAll();post('checkParcel',JSON.stringify(draft));};document.getElementById('exportSurvey').onclick=function(){syncAll();post('exportParcel',JSON.stringify(draft));};window.CDBoxStudioToast=toast;setTimeout(function(){post('ready','parcel-survey-editor');},50);}
init();
})();
";
        }

        private sealed class PageContext
        {
            public ParcelSurveyRecord Record { get; set; }
            public ParcelSurveyValidationResult Validation { get; set; }
            public IList<ParcelSurveyFieldDefinition> Fields { get; set; }
            public IList<ParcelSurveyFieldDefinition> BuildingFields { get; set; }
            public List<StatusOption> Statuses { get; set; }
            public string[] MarkerTypes { get; set; }
            public string[] LineCategories { get; set; }
            public string[] LinePositions { get; set; }
        }

        private sealed class StatusOption
        {
            public StatusOption(int value, string label)
            {
                Value = value;
                Label = label;
            }
            public int Value { get; set; }
            public string Label { get; set; }
        }
    }
}
