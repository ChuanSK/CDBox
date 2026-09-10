using System;
using System.Linq;
using System.Web.Script.Serialization;
using CDBox.RealEstate.Models;
using CDBox.Shared.UI;
using TCPipeAutoDraw.UI.Studio;

namespace CDBox.RealEstate.UI
{
    public static class BuildingCapturePage
    {
        public const string PageId = "realestate-building-capture";
        public static CDBoxPageDefinition Create(BuildingCaptureContext context,
            Func<BuildingCaptureInput, ParcelSurveyRecord> commit, Action annotate, Action selectNext, Action openSurvey)
        {
            return new CDBoxPageDefinition(PageId, "添加房屋", () => BuildHtml(context), request => {
                var result = new CDBoxPageRouteResult { Handled = true };
                if (request.Name == "saveBuilding")
                {
                    var input = new JavaScriptSerializer().Deserialize<BuildingCaptureInput>(request.Argument);
                    result.InteractionToRun = () => {
                        try
                        {
                            var saved = commit(input);
                            return new CDBoxPageRouteResult { Handled = true,
                                ExecuteScript = "window.CDBoxBuildingAdded(" + Json(saved.ParcelName) + ");",
                                ToastMessage = "层次面积已加入房屋调查。", ToastKind = "success" };
                        }
                        catch (Exception ex)
                        {
                            return new CDBoxPageRouteResult { Handled = true,
                                ExecuteScript = "window.CDBoxBuildingFailed(" + Json(ex.Message) + ");",
                                ToastMessage = ex.Message, ToastKind = "error" };
                        }
                    };
                }
                else if(request.Name=="annotateBuilding")
                {
                    result.InteractionToRun=()=>{
                        try{annotate();return new CDBoxPageRouteResult{Handled=true,ExecuteScript="window.CDBoxBuildingAnnotated();"};}
                        catch(Exception ex){return new CDBoxPageRouteResult{Handled=true,ExecuteScript="window.CDBoxBuildingAnnotationFailed("+Json(ex.Message)+");"};}
                    };
                }
                else if (request.Name == "selectNextBuilding") { result.ActionToRun = selectNext; }
                else if (request.Name == "openBuildingSurvey") { result.ActionToRun = openSurvey; result.RestorePageAfterAction = false; }
                return result;
            }) { Width = 1080, Height = 800, MinimumWidth = 720, MinimumHeight = 620, ReplaceExistingPage = true };
        }

        public static string BuildHtml(BuildingCaptureContext context)
        {
            if (context == null || context.Calculation == null) throw new ArgumentNullException(nameof(context));
            return CommonWorkbench.Apply(@"<!doctype html><html lang='zh-CN'><head><meta charset='utf-8'><title>添加房屋</title><style>
*{box-sizing:border-box}body{margin:0}.capture{max-width:1440px;margin:auto;padding:0 24px 24px}header{display:flex;align-items:center;justify-content:space-between;gap:20px;padding:18px 0;border-bottom:1px solid var(--line)}header p{margin:4px 0 0;font-size:12px}.actions{display:flex;gap:8px;flex-wrap:wrap}.summary{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));border-bottom:1px solid var(--line);padding:20px 0;gap:20px}.metric span{display:block;color:var(--muted);font-size:12px}.metric strong{display:block;font-size:24px;margin-top:6px;font-weight:600}.metric:last-child strong{color:var(--brand)}.layout{display:grid;grid-template-columns:300px minmax(0,1fr);gap:28px}.assignment{padding:22px 0;border-right:1px solid var(--line);padding-right:28px}.assignment h2,.calculation h2{font-size:14px;margin:0 0 16px}.fields{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px 14px}.field{min-width:0}.wide{grid-column:1/-1}.field label{display:block;color:var(--muted);font-size:12px;margin-bottom:6px}.hint{color:var(--muted);font-size:12px;margin:18px 0}.calculation{padding:22px 0;min-width:0}.formula{font-family:var(--content-font);font-size:15px;margin:0 0 18px}.component{border-bottom:1px solid var(--line);padding:14px 0}.component summary{cursor:pointer;display:flex;gap:12px;align-items:center;list-style:none}.component summary:before{content:'›';color:var(--muted)}.component[open] summary:before{content:'⌄'}.component summary strong{margin-left:auto;white-space:nowrap}.component summary small{color:var(--muted)}.term-list{margin:14px 0 0;padding:0;list-style:none;display:grid;gap:8px}.term-list li{display:flex;gap:12px;justify-content:space-between;color:var(--muted);font-family:var(--content-font);font-size:12px}.term-list b{font-weight:400;color:var(--text)}#message{padding:12px 0;color:var(--danger)}#message:empty,#excluded:empty{display:none}.success{color:var(--ok)!important}#toast{position:fixed;right:20px;bottom:20px;padding:12px;background:var(--panel);border:1px solid var(--line);border-radius:7px;display:none}.done .fields{opacity:.65}.done input,.done select{pointer-events:none}@media(max-width:820px){.layout{grid-template-columns:1fr}.assignment{border-right:0;border-bottom:1px solid var(--line);padding-right:0}.summary{grid-template-columns:repeat(2,minmax(0,1fr))}.capture{padding:0 16px 20px}header{flex-wrap:wrap}}
</style></head><body><main class='capture'><header><div><h1>添加房屋</h1><p id='drawing'></p></div><div class='actions'><button id='save' class='primary'>添加房屋</button><button id='annotate' class='primary'>生成注记</button><button id='next'>重新选择</button><button id='survey' hidden>查看房屋调查</button></div></header><section class='summary' id='summary'></section><div id='excluded' class='hint'></div><div id='message' role='status' aria-live='polite'></div><div class='layout'><section class='assignment'><h2>所属宗地与房屋信息</h2><div class='fields'><div class='field wide'><label for='parcel'>所属宗地 *</label><select id='parcel' required></select></div><div class='field wide'><label for='target'>所属幢</label><select id='target'></select></div><div class='field building-only'><label for='number'>幢号</label><input id='number' autocomplete='off'></div><div class='field building-only'><label for='household'>户号</label><input id='household' autocomplete='off'></div><div class='field'><label for='floor'>层次名称</label><input id='floor' value='一层' placeholder='如：二至三层通道'></div><div class='field'><label for='count'>计入层数</label><input id='count' type='number' min='1' max='1000' step='1' value='1'></div><div class='field wide'><label style='display:flex;align-items:center;gap:8px'><input id='ground' type='checkbox' checked style='width:auto'>底层（1 层），计入占地面积</label></div><div class='field building-only'><label for='floors'>总层数</label><input id='floors' type='number' min='1' step='1'></div><div class='field wide building-only'><label for='structure'>建筑结构</label><input id='structure' placeholder='可留空'></div></div><p class='hint'>可新建房屋，或将本次面积追加为已有房屋的一条层次。同名层次分别保留；跨层通道请明确计入层数。添加数据与生成注记可分别操作。</p></section><section class='calculation'><h2>面积计算 · ㎡</h2><p class='formula' id='formula'></p><div id='components'></div><p class='hint'>边长、高度和圆弧半径按两位小数计算，圆弧角度保留六位小数。加权面积乘计入层数后保留两位小数；计算式与层次明细随房屋保存，可导出房产文档。</p></section></div></main><div id='toast'></div><script>const context=" + Json(context) + @";
const el=id=>document.getElementById(id),esc=v=>String(v==null?'':v).replace(/[&<>""']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','""':'&quot;',""'"":'&#39;'}[c])),n=v=>Number(v).toFixed(2);
function post(name,data){chrome.webview.postMessage('studio|'+name+'|'+encodeURIComponent(data||''));}
el('drawing').textContent=context.DocumentName+' · 逐个选择或框选 · 每次添加一条层次';const calc=context.Calculation;el('excluded').textContent=(calc.ExcludedHollowHandles||[]).length?'已忽略 '+calc.ExcludedHollowHandles.length+' 个未完全位于房屋或半封图形内的中空图形。':'';
el('summary').innerHTML=[['全面积',calc.FullArea],['扣减中空',calc.HollowArea],['半封全面积',calc.HalfEnclosedArea],['建筑面积',calc.Area]].map(x=>`<div class='metric'><span>${x[0]} · ㎡</span><strong>${n(x[1])}</strong></div>`).join('');
el('formula').textContent=calc.Formula+' ㎡';el('parcel').innerHTML='<option value="""">请选择所属宗地</option>'+(context.Parcels||[]).map(p=>`<option value='${esc(p.RecordId)}'>${esc(p.ParcelName||p.OwnerName||'未命名宗地')}</option>`).join('');el('parcel').value=context.SelectedRecordId||'';
el('components').innerHTML=calc.Components.map((p,i)=>`<details class='component'><summary><span>${i+1}. ${esc(p.LayerName)} <small>${['全面积','中空扣减','半封计半'][p.Role]}</small></span><strong>${n(p.Area)} ㎡ × ${p.Factor}</strong></summary><ul class='term-list'>${p.Terms.map(t=>`<li><span>${t.Method==='rectangle'?'矩形':t.Method==='arcSegment'?'圆弧弓形':'三角形'} ${t.Sequence}</span><b>${esc(t.Formula)} = ${Number(t.Area).toFixed(5)} ㎡</b></li>`).join('')}</ul></details>`).join('');
function targets(){el('target').innerHTML='<option value="""">新建一幢房屋</option>'+(context.Buildings||[]).filter(b=>b.RecordId===el('parcel').value).map(b=>`<option value='${esc(b.BuildingId)}'>${esc(b.BuildingNumber||'未编号房屋')} · ${esc(b.BuildingId.slice(0,6))}</option>`).join('');targetChanged();}
function targetChanged(){document.querySelectorAll('.building-only').forEach(e=>e.hidden=!!el('target').value);el('save').textContent=el('target').value?'添加层次':'添加房屋';}
el('parcel').onchange=targets;el('target').onchange=targetChanged;targets();el('floor').oninput=()=>{el('ground').checked=el('floor').value.trim()==='1'||/^(第一层|一层|1层|一楼|1楼|首层|底层|1F)(?![至到\-])/i.test(el('floor').value.trim());};
function refreshAmount(){var count=Math.max(1,Number(el('count').value)||1),total=Math.round(((Number(calc.FullArea)-Number(calc.HollowArea)+Number(calc.HalfEnclosedArea)/2)*count+Number.EPSILON)*100)/100;el('summary').lastElementChild.querySelector('span').textContent='本次建筑面积 · ㎡';el('summary').lastElementChild.querySelector('strong').textContent=n(total);el('formula').textContent=count===1?calc.Formula+' ㎡':'['+String(calc.Formula).split(' = ')[0]+'] × '+count+' = '+n(total)+' ㎡';}el('count').oninput=refreshAmount;refreshAmount();
let saving=false,done=false,annotating=false,annotated=false;
function busy(){el('save').disabled=saving||annotating||done;el('annotate').disabled=saving||annotating||annotated;el('next').disabled=saving||annotating;}
el('save').onclick=()=>{if(saving||done||annotating)return;el('message').textContent='';if(!el('parcel').value){el('message').textContent='请选择所属宗地。';el('parcel').focus();return;}if(!el('floors').checkValidity()||!el('count').checkValidity()){(!el('count').checkValidity()?el('count'):el('floors')).reportValidity();return;}if(el('ground').checked&&Number(el('count').value)!==1){el('message').textContent='底层计入占地时，计入层数必须为 1。';return;}saving=true;busy();el('save').textContent='正在添加…';post('saveBuilding',JSON.stringify({RecordId:el('parcel').value,TargetBuildingId:el('target').value,BuildingNumber:el('number').value,HouseholdNumber:el('household').value,Floor:el('floor').value,TotalFloors:el('floors').value,Structure:el('structure').value,FloorCount:Number(el('count').value),IsGroundFloor:el('ground').checked}));};
window.CDBoxBuildingFailed=message=>{saving=false;busy();targetChanged();el('message').className='';el('message').textContent=message;};
window.CDBoxBuildingAdded=name=>{saving=false;done=true;document.body.classList.add('done');el('save').hidden=true;el('next').textContent='继续选择';el('survey').hidden=false;document.querySelectorAll('.fields input,.fields select').forEach(e=>e.disabled=true);el('message').className='success';el('message').textContent='已加入 '+(name||'所选宗地')+' 的房屋调查，建筑面积和占地面积已同步。';busy();};
el('annotate').onclick=()=>{if(annotated||annotating||saving)return;annotating=true;busy();post('annotateBuilding','');};
window.CDBoxBuildingAnnotated=()=>{annotating=false;annotated=true;el('annotate').textContent='已生成注记';busy();el('message').className='success';el('message').textContent=done?'房屋已添加，注记已生成。':'注记已生成，可继续添加房屋数据。';};
window.CDBoxBuildingAnnotationFailed=message=>{annotating=false;busy();el('message').className='';el('message').textContent=message;};
el('next').onclick=()=>post('selectNextBuilding','');el('survey').onclick=()=>post('openBuildingSurvey','');window.CDBoxStudioToast=message=>{el('toast').textContent=message;el('toast').style.display='block';setTimeout(()=>el('toast').style.display='none',2500);};
</script></body></html>");
        }
        private static string Json(object value) => new JavaScriptSerializer { MaxJsonLength = int.MaxValue }
            .Serialize(value).Replace("<", "\\u003c").Replace(">", "\\u003e").Replace("&", "\\u0026");
    }
}
