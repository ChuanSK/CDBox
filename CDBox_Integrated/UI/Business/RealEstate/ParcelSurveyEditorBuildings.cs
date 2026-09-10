namespace CDBox.RealEstate.UI
{
    /// <summary>Master list, compact summary and category editor for one building at a time.</summary>
    internal static class ParcelSurveyEditorBuildings
    {
        public static string Build()
        {
            return @"
var buildingCategories=['基本信息','面积','权属','其他'];
function groundName(n){n=String(n||'').trim();return n==='1'||/^(第一层|一层|1层|一楼|1楼|首层|底层|1F)(?![至到\-])/i.test(n);}
function groundRow(r){return r.IsGroundFloor==null?groundName(r.Name):r.IsGroundFloor;}
function rowArea(r){return Math.round(((Number(r.Calculation.FullArea)-Number(r.Calculation.HollowArea)+Number(r.Calculation.HalfEnclosedArea)/2)*Number(r.Count||1)+Number.EPSILON)*100)/100;}
function synchronizeBuildingAreas(){
 var total=0,missing=false,footprint=0,footMissing=false;
 (draft.Buildings||[]).forEach(function(b){
  var area=value('building.area',b.Fields),foot=value('building.footprintArea',b.Fields);
  if(b.HasFloorAreas){var rows=b.FloorAreas||[],ground=rows.filter(groundRow);area.NumericValue=rows.length?rows.reduce((n,r)=>n+rowArea(r),0):null;area.Status=0;area.TextValue='';area.Confirmed=rows.length>0;foot.NumericValue=ground.length?ground.reduce((n,r)=>n+rowArea(r),0):null;value('building.floor',b.Fields).TextValue=[...new Set(rows.map(r=>r.Name).filter(Boolean))].join('、');}
  else foot.NumericValue=groundName(value('building.floor',b.Fields).TextValue)?area.NumericValue:null;
  foot.Status=0;foot.TextValue='';foot.Confirmed=foot.NumericValue!=null;
  if(Number(area.Status)!==4){if(area.NumericValue==null)missing=true;else total+=Number(area.NumericValue);}
  if(foot.NumericValue==null)footMissing=true;else footprint+=Number(foot.NumericValue);
 });
 [['land.buildingAreaTotal',total,missing],['land.buildingFootprintTotal',footprint,footMissing]].forEach(function(x){var v=value(x[0]);v.NumericValue=x[2]?null:Math.round((x[1]+Number.EPSILON)*100)/100;v.Status=0;v.TextValue='';v.Confirmed=!x[2];});
 document.querySelectorAll('[data-derived-area]').forEach(function(card){var b=card.dataset.building?draft.Buildings.find(function(x){return x.Id===card.dataset.building;}):null,v=value(card.dataset.key,b?b.Fields:draft.Fields),display=card.querySelector('.field-display');if(display)display.textContent=v.NumericValue==null?'等待对应层次面积完整后汇总':Number(v.NumericValue).toFixed(2);});
}
window.CDBoxParcelBuildingsChanged=function(record){
 if(!record||record.Id!==draft.Id||Number(record.BuildingsRevision||0)<=Number(draft.BuildingsRevision||0))return;
 syncAll();captureView();var revision=Number(draft.BuildingsRevision||0);
 (record.Buildings||[]).forEach(function(b){if(!b.HasFloorAreas&&!b.AreaCalculation)return;var current=draft.Buildings.find(x=>x.Id===b.Id);if(!current){if(Number(b.AddedRevision)>revision||(b.FloorAreas||[]).some(r=>Number(r.AddedRevision)>revision))draft.Buildings.push(b);return;}current.FloorAreas=current.FloorAreas||[];(b.FloorAreas||[]).forEach(function(row){if(Number(row.AddedRevision)>revision&&!current.FloorAreas.some(x=>x.Id===row.Id))current.FloorAreas.push(row);});current.HasFloorAreas=true;});
 draft.BuildingsRevision=record.BuildingsRevision;synchronizeBuildingAreas();renderTabs();renderContent();restoreView();scheduleAutoSave();
};
function buildingAreaDetails(b){if(!b.HasFloorAreas)return '';return `<details class='building-area-calculation'><summary>层次与面积明细 · ${(b.FloorAreas||[]).length} 条</summary><div class='floor-heading'><span>层次名称</span><span>计入层数</span><span>底层占地</span><span>建筑面积</span><span></span></div>${(b.FloorAreas||[]).map(function(r){return `<div class='floor-entry' data-floor-id='${attr(r.Id)}' data-floor-building='${attr(b.Id)}'><div class='floor-heading'><input data-floor-name aria-label='层次名称' value='${attr(r.Name||'')}' placeholder='可输入相同层次名'><input data-floor-count aria-label='计入层数' type='number' min='1' max='1000' step='1' value='${r.Count||1}'><label><input data-floor-ground aria-label='计入底层占地' type='checkbox'${groundRow(r)?' checked':''}> 1 层</label><strong data-floor-result>${rowArea(r).toFixed(2)} ㎡</strong><button type='button' class='icon-btn danger' data-remove-floor='${attr(r.Id)}' aria-label='删除层次'>×</button></div><details><summary>计算式</summary><p data-floor-formula>[${esc(String(r.Calculation.Formula).split(' = ')[0])}]${Number(r.Count)>1?' × '+r.Count:''} = ${rowArea(r).toFixed(2)} ㎡</p>${(r.Calculation.Components||[]).map(p=>`<p>${esc(p.LayerName)}：${esc(p.Formula)} × ${p.Factor}</p>`).join('')}</details></div>`;}).join('')}<p>同名层次分别保存；底层明细相加生成占地面积。</p></details>`;}
function bindFloorAreas(){
 document.querySelectorAll('[data-floor-id]').forEach(function(el){var b=draft.Buildings.find(x=>x.Id===el.dataset.floorBuilding),r=b.FloorAreas.find(x=>x.Id===el.dataset.floorId),name=el.querySelector('[data-floor-name]'),count=el.querySelector('[data-floor-count]'),ground=el.querySelector('[data-floor-ground]');
 function changed(){synchronizeBuildingAreas();el.querySelector('[data-floor-result]').textContent=rowArea(r).toFixed(2)+' ㎡';el.querySelector('[data-floor-formula]').textContent='['+String(r.Calculation.Formula).split(' = ')[0]+']'+(Number(r.Count)>1?' × '+r.Count:'')+' = '+rowArea(r).toFixed(2)+' ㎡';refreshBuildingList();renderTabs();scheduleAutoSave();}
 name.oninput=function(){r.Name=name.value;r.IsGroundFloor=groundName(r.Name);ground.checked=r.IsGroundFloor;if(r.IsGroundFloor){r.Count=1;count.value='1';}changed();};
 count.oninput=function(){var n=Number(count.value);count.setCustomValidity(r.IsGroundFloor&&n!==1?'底层计入层数必须为 1':'');if(count.checkValidity()){r.Count=n;changed();}};count.onchange=function(){if(!count.checkValidity()){count.reportValidity();count.value=r.Count;count.setCustomValidity('');}};
 ground.onchange=function(){r.IsGroundFloor=ground.checked;if(r.IsGroundFloor){r.Count=1;count.value='1';}changed();};
 el.querySelector('[data-remove-floor]').onclick=function(){syncAll();captureView();b.FloorAreas=b.FloorAreas.filter(x=>x.Id!==r.Id);renderContent();restoreView();scheduleAutoSave();};
 });
}
function buildingCategory(d){return d.Group==='面积'?1:d.Group==='产权与墙体'?2:d.Group==='成果资料'?3:0;}
function buildingTitle(b){return value('building.number',b.Fields).TextValue||'未编号房屋';}
function buildingArea(b){var v=value('building.area',b.Fields);return v.NumericValue==null?'面积待填':Number(v.NumericValue).toFixed(2)+' ㎡';}
function buildingSummaryValue(d,v){return (d.Key==='building.area'||d.Key==='building.footprintArea')&&v.NumericValue!=null?Number(v.NumericValue).toFixed(2):displayFieldValue(d,v);}
function currentBuilding(){return draft.Buildings.find(function(b){return b.Id===view.buildingId;})||draft.Buildings[0];}
function renderBuildings(){
 var selected=currentBuilding();if(selected)view.buildingId=selected.Id;
 var list=draft.Buildings.map(function(b){var pending=fieldProgress(ctx.BuildingFields,b.Fields).pending;return `<button class='building-item${selected&&selected.Id===b.Id?' active':''}' type='button' data-select-building='${attr(b.Id)}' aria-pressed='${selected&&selected.Id===b.Id}'><span class='building-item-main'><strong>${esc(buildingTitle(b))}</strong><span>${esc(buildingArea(b))}</span></span><small>${pending?pending+' 项待处理':'已完成'}</small></button>`;}).join('');
 var detail=`<div class='empty'>从左侧新增房屋后开始录入。</div>`;
 if(selected){
  var b=selected,category=Math.max(0,Math.min(3,Number(view.buildingCategory)||0));
  var keys=['building.number','building.householdNumber','building.totalFloors','building.floor','building.structure','building.area','building.footprintArea'];
  var summary=keys.map(function(key){var d=ctx.BuildingFields.find(function(x){return x.Key===key;});return `<div><dt>${esc(d.Label)}</dt><dd data-building-summary='${attr(key)}'>${esc(buildingSummaryValue(d,value(key,b.Fields)))}${d.Unit&&value(key,b.Fields).NumericValue!=null?' '+esc(d.Unit):''}</dd></div>`;}).join('');
  var editor=view.buildingEditing?`<div class='building-categories' role='tablist' aria-label='房屋属性分类'>${buildingCategories.map(function(name,i){var pending=fieldProgress(ctx.BuildingFields.filter(function(d){return buildingCategory(d)===i;}),b.Fields).pending;return `<button type='button' role='tab' id='building-category-${i}' aria-selected='${category===i}' aria-controls='building-fields' tabindex='${category===i?0:-1}' data-building-category='${i}'>${name}${pending?`<small>${pending}</small>`:''}</button>`;}).join('')}</div><div id='building-fields' role='tabpanel' aria-labelledby='building-category-${category}' class='field-grid'>${ctx.BuildingFields.filter(function(d){return buildingCategory(d)===category;}).map(function(d){return renderField(d,b.Fields,'building-'+b.Id,b.Id);}).join('')}</div>`:`<dl class='building-summary'>${summary}</dl>`;
  detail=`<div class='building-detail-head'><h3>${esc(buildingTitle(b))}</h3><div class='section-tools'><button class='mini' data-building-edit type='button' aria-expanded='${!!view.buildingEditing}'>${view.buildingEditing?'收起详情':'编辑详情'}</button><button class='icon-btn danger' type='button' data-remove-building='${attr(b.Id)}' aria-label='删除${attr(buildingTitle(b))}' title='删除此幢'>×</button></div></div>${editor}${buildingAreaDetails(b)}`;
 }
 return `<section class='building-workbench'><div class='building-workbench-head'><h2>房屋调查</h2><div class='section-tools'><span>${draft.Buildings.length} 间房屋</span><button class='mini primary' data-capture-building type='button'>添加房屋</button></div></div><div class='building-master-detail'><aside class='building-master' aria-label='房屋列表'><h3>房屋列表</h3><div class='building-list'>${list||`<p class='building-list-empty'>尚无房屋</p>`}</div><button class='building-add' data-add='building' type='button'>+ 手工录入房屋</button></aside><section class='building-detail' aria-label='当前房屋详情'>${detail}</section></div></section>`;
}
function bindBuildings(){
 bindFloorAreas();
 document.querySelectorAll('[data-capture-building]').forEach(function(button){button.onclick=function(){afterAutoSave(function(){post('addBuildingFromCad',JSON.stringify(draft));});};});
 document.querySelectorAll('[data-select-building]').forEach(function(button){button.onclick=function(){syncAll();captureView();view.buildingId=button.dataset.selectBuilding;view.buildingEditing=false;saveView();renderContent();restoreView();};});
 document.querySelectorAll('[data-building-edit]').forEach(function(button){button.onclick=function(){syncAll();captureView();view.buildingEditing=!view.buildingEditing;saveView();renderContent();restoreView();};});
 document.querySelectorAll('[data-building-category]').forEach(function(button){button.onclick=function(){syncAll();captureView();view.buildingCategory=Number(button.dataset.buildingCategory);saveView();renderContent();restoreView();document.getElementById('building-category-'+view.buildingCategory).focus({preventScroll:true});};button.onkeydown=function(e){if(['ArrowLeft','ArrowRight','Home','End'].indexOf(e.key)<0)return;e.preventDefault();var i=Number(button.dataset.buildingCategory),next=e.key==='Home'?0:e.key==='End'?3:(i+(e.key==='ArrowRight'?1:3))%4;document.getElementById('building-category-'+next).click();};});
}
function refreshBuildingList(){
 document.querySelectorAll('[data-select-building]').forEach(function(button){var b=draft.Buildings.find(function(x){return x.Id===button.dataset.selectBuilding;});if(!b)return;button.querySelector('strong').textContent=buildingTitle(b);button.querySelector('.building-item-main>span').textContent=buildingArea(b);var pending=fieldProgress(ctx.BuildingFields,b.Fields).pending;button.querySelector('small').textContent=pending?pending+' 项待处理':'已完成';});
 var b=currentBuilding(),heading=document.querySelector('.building-detail-head h3');if(b&&heading)heading.textContent=buildingTitle(b);
 if(b)document.querySelectorAll('[data-building-summary],[data-derived-floor]').forEach(function(el){var key=el.dataset.buildingSummary||'building.floor',d=ctx.BuildingFields.find(x=>x.Key===key),v=value(key,b.Fields);el.textContent=buildingSummaryValue(d,v)+(d.Unit&&v.NumericValue!=null?' '+d.Unit:'');});
 if(b)document.querySelectorAll('[data-building-category]').forEach(function(button){var pending=fieldProgress(ctx.BuildingFields.filter(function(d){return buildingCategory(d)===Number(button.dataset.buildingCategory);}),b.Fields).pending;var badge=button.querySelector('small');if(pending&&!badge){badge=document.createElement('small');button.appendChild(badge);}if(badge){badge.textContent=pending;badge.hidden=!pending;}});
}
";
        }
    }
}
