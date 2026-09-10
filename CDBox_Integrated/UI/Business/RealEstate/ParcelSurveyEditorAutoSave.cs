namespace CDBox.RealEstate.UI
{
    /// <summary>Single-flight, acknowledged autosave; navigation waits for the latest draft.</summary>
    internal static class ParcelSurveyEditorAutoSave
    {
        public static string Build()
        {
            return @"
var autoSaveTimer=0,autoSaveTimeout=0,autoSaveFlight=null,autoSaveSaved='',autoSaveSerial=0,autoSaveContinuation=null,autoSaveSuspended=false;
function saveStatus(text,failed){var s=document.getElementById('autoSaveStatus');s.textContent=text;s.classList.toggle('failed',!!failed);document.getElementById('retryAutoSave').hidden=!failed;}
function snapshotDraft(){syncAll();return JSON.stringify(draft);}
function scheduleAutoSave(){
 if(!canEdit||autoSaveSuspended)return;
 var data=snapshotDraft();clearTimeout(autoSaveTimer);
 if(data===autoSaveSaved&&!autoSaveFlight)return;
 saveStatus('待保存',false);autoSaveTimer=setTimeout(flushAutoSave,600);
}
function flushAutoSave(){
 clearTimeout(autoSaveTimer);if(autoSaveSuspended)return;
 if(!canEdit){finishSaveContinuation();return;}
 var data=snapshotDraft();
 if(autoSaveFlight)return;
 if(data===autoSaveSaved){saveStatus('已自动保存',false);finishSaveContinuation();return;}
 autoSaveFlight={id:++autoSaveSerial,data:data};saveStatus('正在保存…',false);
 post('autoSaveParcel',JSON.stringify({RequestId:autoSaveFlight.id,Record:JSON.parse(data)}));
 autoSaveTimeout=setTimeout(function(){if(!autoSaveFlight)return;saveStatus('保存未确认，请重试',true);autoSaveFlight=null;autoSaveContinuation=null;},10000);
}
window.CDBoxParcelAutoSaved=function(id,ok,message){
 if(!autoSaveFlight||autoSaveFlight.id!==id)return;
 clearTimeout(autoSaveTimeout);var data=autoSaveFlight.data;autoSaveFlight=null;
 if(!ok){autoSaveContinuation=null;saveStatus(message||'自动保存失败，请重试',true);return;}
 autoSaveSaved=data;
 if(snapshotDraft()!==autoSaveSaved){flushAutoSave();return;}
 saveStatus('已自动保存',false);finishSaveContinuation();
};
function finishSaveContinuation(){var next=autoSaveContinuation;autoSaveContinuation=null;if(next)next();}
function afterAutoSave(next){autoSaveContinuation=next;flushAutoSave();}
function leaveParcel(next){afterAutoSave(function(){autoSaveSuspended=true;clearTimeout(autoSaveTimer);clearTimeout(textareaSaveTimer);next();});}
window.CDBoxParcelScopeActionFailed=function(){autoSaveSuspended=false;document.getElementById('scopeRegion').value=scope.RecordId;};
window.CDBoxRequestParcelClose=function(){afterAutoSave(function(){post('closeParcelSaved','');});};
function bindAutoSave(){
 autoSaveSaved=snapshotDraft();saveStatus(canEdit?'已自动保存':'未选择宗地',false);
 var content=document.getElementById('content');
 ['input','change','click','keydown'].forEach(function(event){content.addEventListener(event,function(){queueMicrotask(scheduleAutoSave);});});
 content.addEventListener('focusout',function(){queueMicrotask(flushAutoSave);});
 window.addEventListener('blur',flushAutoSave);
 document.addEventListener('visibilitychange',function(){if(document.hidden)flushAutoSave();});
 document.getElementById('retryAutoSave').onclick=flushAutoSave;
 var menu=document.getElementById('parcelActions'),button=document.getElementById('parcelMore'),items=document.getElementById('parcelActionItems'),timer=0,pinned=false;
 function open(show){clearTimeout(timer);menu.classList.toggle('open',show);items.inert=!show;button.setAttribute('aria-expanded',String(show));}
 button.onclick=function(){pinned=!pinned;open(pinned);};
 menu.onmouseenter=function(){open(true);};
 menu.onmouseleave=function(){if(!pinned)timer=setTimeout(function(){if(!menu.contains(document.activeElement))open(false);},180);};
 menu.onfocusout=function(){setTimeout(function(){if(!pinned&&!menu.contains(document.activeElement)&&!menu.matches(':hover'))open(false);},0);};
 document.addEventListener('click',function(e){if(!menu.contains(e.target)||e.target.closest('[data-scope-action]')){pinned=false;open(false);}});
 document.addEventListener('keydown',function(e){if(e.key==='Escape'&&menu.classList.contains('open')){pinned=false;open(false);button.focus();}});
}
";
        }
    }
}
