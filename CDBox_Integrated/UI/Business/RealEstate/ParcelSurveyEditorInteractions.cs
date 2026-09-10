namespace CDBox.RealEstate.UI
{
    /// <summary>Progress, inline editing and collapsible sections inside the editor's closure.</summary>
    internal static class ParcelSurveyEditorInteractions
    {
        public static string Build()
        {
            return @"
var groupCatalog={},progressTimer=0;
function fieldHasValue(d,v){
 if(Number(v.Status)===4)return !!d.AllowNotApplicable;
 if(d.Kind==='checkbox')return !d.Required||!!v.BooleanValue;
 if(d.Kind==='number')return v.NumericValue!==null&&v.NumericValue!==''&&isFinite(Number(v.NumericValue));
 if(d.Kind==='multiselect')return (v.Selections||[]).length>0;
 var text=String(v.TextValue||'').trim();
 return !!text&&text!=='/'&&text!=='待确认'&&text!=='未确认'&&(d.Kind!=='select'||(d.Options||[]).indexOf(text)>=0);
}
function fieldProgress(definitions,fields){
 var active=(definitions||[]).filter(function(d){return conditionActive(d,fields);});
 var pending=active.filter(function(d){return d.Required&&!fieldHasValue(d,value(d.Key,fields));}).length;
 var required=active.filter(function(d){return d.Required;});
 var allTrusted=active.length>0&&active.every(function(d){var v=value(d.Key,fields);return (Number(v.Status)===0||Number(v.Status)===1||Number(v.Status)===4)&&fieldHasValue(d,v);});
 return {pending:pending,complete:active.length>0&&pending===0&&(required.length>0||allTrusted),count:active.length};
}
function boundaryProgress(kind){
 var b=draft.Boundary,rows=kind==='points'?b.Points:kind==='segments'?b.Segments:b.SignatureGroups;
 var pending=rows.length?0:1,numbers=b.Points.map(function(p){return p.PointNumber;});
 function missing(v){return v===null||v===undefined||String(v).trim()==='';}
 rows.forEach(function(row){
  var keys=kind==='points'?['PointNumber','X','Y','MarkerType']:kind==='segments'?['StartPointNumber','EndPointNumber','Distance','LineCategory','LinePosition']:['StartPointNumber','EndPointNumber','NeighborOwner'];
  keys.forEach(function(k){if(missing(row[k])||row[k]==='待确认'||row[k]==='未确认'||(k==='Distance'&&Number(row[k])<=0))pending++;});
  if(kind==='points'&&numbers.filter(function(n){return n===row.PointNumber;}).length>1)pending++;
  if(kind==='segments'||kind==='signatures'){
   ['StartPointNumber','EndPointNumber'].forEach(function(k){if(!missing(row[k])&&numbers.indexOf(row[k])<0)pending++;});
  }
  if(kind==='segments'&&!row.NeighborOwner&&!row.NeighborParcelCode)pending++;
 });
 if(kind==='points'){if(!b.ParcelBoundaryClosed)pending++;if(rows.length>0&&rows.length<3)pending++;}
 return {pending:pending,complete:rows.length>0&&pending===0,count:rows.length,unit:'条'};
}
function tabPending(tab){
 if(!canEdit)return 0;
 var pending=fieldProgress((ctx.Fields||[]).filter(function(d){return d.Tab===tab;}),draft.Fields).pending;
 if(tab===5)['points','segments','signatures'].forEach(function(k){pending+=boundaryProgress(k).pending;});
 if(tab===6)(draft.Buildings||[]).forEach(function(b){pending+=fieldProgress(ctx.BuildingFields,b.Fields).pending;});
 return pending;
}
function progressText(meta){return meta.pending?meta.pending+' 项待处理':meta.complete?'已完成 · '+meta.count+(meta.unit||'项'):meta.count+(meta.unit||'项');}
function groupOpen(key,meta){
 var state=(view.groups||{})[key];
 return state ? state.open!==false : true;
}
function renderGroup(key,title,body,readProgress,tools,extraClass){
 var meta=readProgress(),open=groupOpen(key,meta),id='group-'+encodeURIComponent(key);
 groupCatalog[key]=readProgress;
 return `<section class='group ${extraClass||''}${open?'':' is-collapsed'}' data-group='${attr(key)}'><div class='group-head'><button class='group-toggle' type='button' aria-expanded='${open}' aria-controls='${attr(id)}'><span class='group-chevron' aria-hidden='true'>›</span><strong>${esc(title)}</strong><span class='group-progress${meta.pending?' pending':''}'>${progressText(meta)}</span></button>${tools?`<div class='section-tools'>${tools}</div>`:''}</div><div class='group-body' id='${attr(id)}'${open?'':' inert'}><div class='group-inner'>${body}</div></div></section>`;
}
function setGroupOpen(group,open){
 group.classList.toggle('is-collapsed',!open);
 var body=group.querySelector(':scope > .group-body'),toggle=group.querySelector(':scope > .group-head .group-toggle');
 body.inert=!open;toggle.setAttribute('aria-expanded',String(open));
 if(open)requestAnimationFrame(function(){group.querySelectorAll('textarea[data-resize-key]').forEach(fitTextarea);});
}
function rememberGroup(group,open){
 var key=group.dataset.group,meta=groupCatalog[key]();view.groups=view.groups||{};
 view.groups[key]={open:open,complete:meta.complete};saveView();setGroupOpen(group,open);
}
function refreshProgress(){
 refreshBuildingList();
 document.querySelectorAll('[data-tab]').forEach(function(button){
  var count=tabPending(Number(button.dataset.tab)),badge=button.querySelector('.nav-count');
  badge.textContent=count;badge.hidden=!count;button.title=tabNames[Number(button.dataset.tab)-1]+(count?' · '+count+' 项待处理':' · 无待处理字段');
 });
 document.querySelectorAll('[data-group]').forEach(function(group){
  var read=groupCatalog[group.dataset.group];if(!read)return;var meta=read();
  var progress=group.querySelector(':scope > .group-head .group-progress');progress.textContent=progressText(meta);progress.classList.toggle('pending',!!meta.pending);
 });
}
function cardField(card){var fields=draft.Fields;if(card.dataset.building){var b=draft.Buildings.find(function(x){return x.Id===card.dataset.building;});if(b)fields=b.Fields;}return value(card.dataset.key,fields);}
function displayFieldValue(d,v){
 if(Number(v.Status)===4)return '/';
 if(d.Kind==='checkbox')return v.BooleanValue?'是':'否';
 if(d.Kind==='number')return v.NumericValue==null?'未生成 / 点击填写':String(v.NumericValue);
 if(d.Kind==='multiselect')return (v.Selections||[]).join('、')||'未填写';
 return v.TextValue||'未填写 / 点击编辑';
}
function comparableField(v){return JSON.stringify([v.TextValue||'',v.NumericValue,v.BooleanValue||false,v.Selections||[]]);}
function beginFieldEdit(card){
 if(!card||!card.classList.contains('read-mode'))return;
 card._originalField=clone(cardField(card));card.classList.add('is-editing');
 var input=card.querySelector('[data-role=value]');
 if(input&&input.matches('input,textarea'))input.readOnly=false;
 if(input&&input.matches('textarea'))fitTextarea(input);
 var focus=input&&input.matches('input,select,textarea')?input:card.querySelector('.field-editor input');
 if(focus){focus.focus();if(focus.type==='text')focus.select();}
}
function finishFieldEdit(card){
 if(!card||!card.classList.contains('is-editing'))return;
 card.classList.remove('is-editing');var v=cardField(card),d=(card.dataset.building?ctx.BuildingFields:ctx.Fields).find(function(x){return x.Key===card.dataset.key;});
 if(Number(v.Status)!==0&&Number(v.Status)!==1)card.classList.remove('read-mode');
 var display=card.querySelector('.field-display');if(display)display.textContent=displayFieldValue(d,v);
 card._originalField=null;
}
function bindWorkbench(){
 var content=document.getElementById('content');
 content.addEventListener('click',function(e){
  var toggle=e.target.closest('.group-toggle');
  if(toggle){syncAll();var group=toggle.closest('[data-group]');rememberGroup(group,group.classList.contains('is-collapsed'));return;}
  var display=e.target.closest('[data-edit-field]');if(display)beginFieldEdit(display.closest('.field-card'));
 });
 content.addEventListener('input',function(e){
  var card=e.target.closest('.field-card[data-key]');syncAll();
  if(card&&card._originalField&&!e.target.matches('[data-role=status]')){var v=cardField(card);if(comparableField(v)!==comparableField(card._originalField)){v.Status=3;v.Confirmed=true;var s=card.querySelector('[data-role=status]');s.value='3';s.className='status-select s3';}}
  refreshProgress(false);
 });
 content.addEventListener('change',function(){syncAll();refreshProgress(false);});
 content.addEventListener('focusout',function(e){
  var card=e.target.closest('.field-card');clearTimeout(progressTimer);
  progressTimer=setTimeout(function(){if(card&&!card.contains(document.activeElement))finishFieldEdit(card);syncAll();refreshProgress(true);},160);
 });
 content.addEventListener('keydown',function(e){
  var card=e.target.closest('.field-card.is-editing');if(!card)return;
  if(e.key==='Escape'&&card._originalField){e.preventDefault();Object.assign(cardField(card),card._originalField);captureView();renderContent();restoreView();}
  else if(e.key==='Enter'&&!e.target.matches('textarea')){e.preventDefault();e.target.blur();}
 });
}
";
        }
    }
}
