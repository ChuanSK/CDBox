// These enhancements run inside the existing layer component closure.
// Preserve selection, virtual scrolling, presets and the native CAD route contract.
var previousLayerInit = LayerManagerPage.prototype.init;
LayerManagerPage.prototype.init = function(){
  previousLayerInit.call(this);
  var self=this,scroll=this.$('[data-role="grid-scroll"]');
  if(scroll&&window.ResizeObserver){this.gridResizeObserver=new ResizeObserver(function(){self.renderVirtual(true);});this.gridResizeObserver.observe(scroll);}
  document.addEventListener('keydown',function(e){if(self.root.getClientRects().length&&(e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='s'){e.preventDefault();if(self.isDirty())self.save();}});
  return this;
};
var previousLayerDetails = LayerManagerPage.prototype.showRecognitionDetails;
LayerManagerPage.prototype.showRecognitionDetails = function(row) {
  previousLayerDetails.call(this, row);
  var self=this, body=this.$('[data-role="modal-body"]'), controls=document.createElement('div');
  controls.className='lm-detail-controls';
  controls.innerHTML='<label>图层颜色<button class="lm-color-button" type="button"><span class="lm-color"><i style="background:'+html(row.colorHex||'#888')+'"></i><span>'+html(row.colorName||row.colorHex||'随层')+'</span></span></button></label>'
    +'<label>标签<input aria-label="图层标签" value="'+html(splitTags(row.tags).join(', '))+'" placeholder="逗号分隔多个标签"></label>'
    +'<label>线型<strong>'+html(row.linetype||'—')+'</strong></label><label>对象与打印<strong>'+html(self.counts[row.name]==null?'未统计':String(self.counts[row.name]))+' · '+(row.isPlottable?'可打印':'不可打印')+'</strong></label>';
  body.insertBefore(controls,body.firstChild);
  controls.querySelector('button').onclick=function(){self.hideModal();self.post('openLayerColorPicker',row.name);};
  controls.querySelector('input').onchange=function(){row.tags=splitTags(this.value);self.onRowEdited(row,true);};
};
var previousLayerRender = LayerManagerPage.prototype.renderVirtual;
LayerManagerPage.prototype.renderVirtual = function(force) {
  previousLayerRender.call(this,force);
  this.$$('[data-recognition-detail]').forEach(function(node){
    node.setAttribute('role','button');node.tabIndex=0;node.setAttribute('aria-label','查看图层详情与标签');
    node.onkeydown=function(e){if(e.key==='Enter'||e.key===' '){e.preventDefault();node.click();}};
  });
};
var previousShowModal = LayerManagerPage.prototype.showModal;
LayerManagerPage.prototype.showModal = function(title,body,buttons) {
  this.modalReturnFocus=document.activeElement;
  var focusedRow=this.modalReturnFocus&&this.modalReturnFocus.closest('[data-layer]');this.modalReturnLayer=focusedRow&&focusedRow.getAttribute('data-layer');
  previousShowModal.call(this,title,body,buttons);
  var modal=this.$('.lm-modal'), self=this;
  modal.setAttribute('role','dialog');modal.setAttribute('aria-modal','true');modal.setAttribute('aria-label',title);
  modal.tabIndex=-1;modal.focus();
  modal.onkeydown=function(e){
    if(e.key==='Escape'){e.preventDefault();self.hideModal();return;}
    if(e.key!=='Tab')return;
    var nodes=[].slice.call(modal.querySelectorAll('button:not(:disabled),input:not(:disabled),select:not(:disabled),textarea:not(:disabled),[tabindex="0"]')).filter(function(n){return n.offsetParent!==null;});
    var first=nodes[0],last=nodes[nodes.length-1];
    if(!first){e.preventDefault();return;}
    if(e.shiftKey&&(document.activeElement===first||document.activeElement===modal)){e.preventDefault();last.focus();}
    else if(!e.shiftKey&&(document.activeElement===last||document.activeElement===modal)){e.preventDefault();first.focus();}
  };
};
var previousHideModal = LayerManagerPage.prototype.hideModal;
LayerManagerPage.prototype.hideModal = function(){previousHideModal.call(this);var target=this.modalReturnFocus;if(!target||!target.isConnected)target=this.modalReturnLayer&&this.$('[data-layer="'+this.cssEscape(this.modalReturnLayer)+'"] [data-recognition-detail]');if(target)target.focus();};
