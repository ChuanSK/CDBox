const assert=require('node:assert/strict');
const fs=require('node:fs');const path=require('node:path');const {pathToFileURL}=require('node:url');
const {chromium}=require('C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const {layers}=require('./UtilityWorkbenchFixture.cjs');
const directory=path.resolve(process.argv[2]||'artifacts/5.1.9/preview');
let checks=0;function check(value,message){assert.ok(value,message);checks++;}
async function latest(page,name){return page.evaluate(n=>{const m=previewMessages.filter(s=>s.startsWith('studio|'+n+'|')).pop();return m?decodeURIComponent(m.split('|').slice(2).join('|')):null;},name);}
async function noOverflow(page){return page.evaluate(()=>({document:document.documentElement.scrollWidth<=innerWidth,panels:[...document.querySelectorAll('.lm-grid-scroll,.rr-detail,.rr-list,.lm-action-bar')].every(n=>n.scrollWidth<=n.clientWidth+1)}));}
(async()=>{
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 try{
  for(const name of ['layers','rules','building'])for(const mode of ['light','dark']){
   const page=await browser.newPage({viewport:{width:1180,height:800}});
   await page.goto(pathToFileURL(path.join(directory,`${name}-${mode}.html`)).href);
   if(name==='layers')await page.evaluate(data=>CDBoxLayerManagerReceive(data),layers);
   for(const width of [720,900,1180,1520]){
    await page.setViewportSize({width,height:800});await page.waitForTimeout(80);
    const overflow=await noOverflow(page);check(overflow.document&&overflow.panels,`${name}/${mode}/${width} no horizontal overflow`);
   }
   await page.setViewportSize({width:1180,height:800});
   await page.screenshot({path:path.join(directory,`${name}-${mode}.png`)});
   await page.setViewportSize({width:720,height:680});
   await page.screenshot({path:path.join(directory,`${name}-${mode}-narrow.png`)});
   check(await page.evaluate(()=>document.body.dataset.appearance==='workbench'&&document.body.dataset.theme===previewAppearance.Theme),name+' native workbench theme');
   await page.evaluate(()=>{previewAppearance.LightTheme.Accent='#9B44D3';previewAppearance.DarkTheme.Accent='#9B44D3';CDBoxApplyWorkbenchTheme(previewAppearance);});
   await page.waitForTimeout(240);
   const primary=name==='layers'?'[data-action=auto-recognize]':name==='rules'?'#saveRulesButton':'#save';
   const accent=await page.locator(primary).evaluate(n=>getComputedStyle(n).backgroundColor);
   check(accent==='rgb(155, 68, 211)',name+' live custom accent '+accent);
   await page.emulateMedia({reducedMotion:'reduce'});
   check(await page.locator(primary).evaluate(n=>parseFloat(getComputedStyle(n).transitionDuration)===0),name+' reduced motion');
   check((await page.evaluate(()=>previewErrors)).length===0,name+' no page errors');
   await page.close();
  }
  // Layer selection and editing exercise the actual virtual component and CAD messages.
  const layer=await browser.newPage({viewport:{width:720,height:680}});
  await layer.goto(pathToFileURL(path.join(directory,'layers-light.html')).href);
  await layer.evaluate(data=>{CDBoxLayerManagerReceive(data);CDBoxLayerManagerObjectCounts({[data.layers[0].name]:25});},layers);
  check(await layer.locator('.lm-grid-row').count()<80,'large layer list remains virtual');
  await layer.locator('[data-role=grid-scroll]').evaluate(n=>n.scrollTop=n.scrollHeight);await layer.waitForTimeout(150);
  check(await layer.locator('.lm-name strong').last().textContent()===layers.layers[319].name,'virtual list reaches final layer');
  await layer.locator('[data-role=grid-scroll]').evaluate(n=>n.scrollTop=0);await layer.waitForTimeout(100);
  await layer.locator('[data-role=search]').fill('外轮廓-004');await layer.waitForTimeout(250);
  check(await layer.evaluate(()=>layerManagerEditor.visible.length)===1,'layer search filters');
  await layer.locator('[data-role=search]').fill('');await layer.waitForTimeout(250);
  await layer.locator('[data-row-check]').nth(0).check();await layer.locator('[data-row-check]').nth(2).click({modifiers:['Shift']});
  check(await layer.evaluate(()=>layerManagerEditor.selectedNames().length)===3,'shift contiguous selection');
  check((await noOverflow(layer)).panels,'batch editing fits narrow window');
  await layer.locator('[data-batch=tags]').fill('新增标签');await layer.locator('[data-action=apply-batch]').click();
  check(await layer.evaluate(()=>layerManagerEditor.dirtyRows().length)===3,'batch draft includes three layers');
  await layer.locator('[data-action=save]').click();
  const changes=JSON.parse(await latest(layer,'saveLayerMetadata')).changes;
  check(changes.length===3&&changes.every(c=>c.tags.includes('新增标签')),'layer save payload preserves batch changes');
  await layer.evaluate(()=>CDBoxLayerManagerSaveFailed('模拟写入失败'));
  check(await layer.locator('[data-action=save]').isEnabled(),'failed save keeps retry available');
  await layer.locator('[data-action=discard]').click();
  await layer.locator('[data-recognition-detail]').first().focus();await layer.keyboard.press('Enter');
  check(await layer.locator('[role=dialog]').isVisible(),'keyboard opens details');
  check((await layer.locator('.lm-detail-controls').textContent()).includes('25 · 可打印'),'hidden columns remain in details');
  await layer.getByLabel('图层标签',{exact:true}).fill('详情标签, 复核');await layer.getByRole('button',{name:'关闭',exact:true}).click();
  check(await layer.evaluate(()=>layerManagerEditor.dirtyRows()[0].tags.includes('详情标签')),'hidden tags remain editable');
  await layer.keyboard.press('Control+s');check(JSON.parse(await latest(layer,'saveLayerMetadata')).changes[0].tags.includes('复核'),'keyboard saves layer metadata');
  await layer.locator('[data-action=discard]').click();await layer.locator('[data-recognition-detail]').first().click();
  await layer.locator('.lm-detail-controls .lm-color-button').click();
  check(await latest(layer,'openLayerColorPicker')===layers.layers[0].name,'narrow detail invokes native color picker');
  await layer.locator('[data-action=open-recognition]').click();check(await latest(layer,'openAttributeRecognition')==='standalone','recognition tool route unchanged');
  await layer.locator('[data-action=create-default]').click();check(await layer.locator('[role=dialog]').isVisible(),'layer presets still open');
  await layer.keyboard.press('Escape');check(!await layer.locator('[role=dialog]').isVisible(),'escape dismisses layer dialog');
  check((await layer.evaluate(()=>previewErrors)).length===0,'layer interactions no errors');await layer.close();

  const rules=await browser.newPage({viewport:{width:1180,height:800}});
  await rules.goto(pathToFileURL(path.join(directory,'rules-light.html')).href);
  await rules.evaluate(()=>{
   const values=[{name:'污水识别',enabled:true,priority:100,scope:'图层名',matchMode:'通配符',pattern:'*污水*',parentGroup:'主管',parentClass:'污水',tagText:'PVC',applicableObjectTypes:'Polyline',source:'测试来源',confidenceBase:.87,stopAfterMatch:true},{name:'房屋规则',enabled:true,priority:50,matchMode:'包含',pattern:'房屋'}];
   recognitionRulesEditor.rules=CDBoxRecognitionRulesPage.cloneRules(values);recognitionRulesEditor.selectedIndex=0;recognitionRulesEditor.savedPayload=recognitionRulesEditor.buildPayload();recognitionRulesEditor.render();
  });
  await rules.locator('[data-field=name]').fill('污水与支管');await rules.locator('[data-field=tagText]').fill('PVC, 待复核');
  await rules.locator('.rr-pick').nth(1).click();await rules.locator('.rr-pick').nth(0).click();
  check(await rules.locator('[data-field=name]').inputValue()==='污水与支管','switching rules retains draft');
  await rules.locator('#saveRulesButton').click();let payload=await latest(rules,'saveRecognitionRules');
  const row=payload.split('\n')[0].split('\t').map(s=>Buffer.from(s,'base64').toString('utf8'));
  check(row.length===15&&row[2]==='污水与支管'&&row[9]==='PVC, 待复核'&&row[11]==='Polyline'&&row[12]==='测试来源'&&row[13]==='0.87','all rule payload fields retained');
  check(await rules.locator('#rulesStatus').textContent()==='未保存','save is not acknowledged before native success');
  await rules.locator('.rr-test summary').click();await rules.locator('#ruleTestInput').fill('污水主管\n建筑房屋');await rules.locator('#testRulesButton').click();
  const output=await rules.locator('#ruleTestOutput').textContent();check(output.includes('污水与支管 → 主管 / 污水')&&output.includes('\n\n')&&!output.includes('\\n'),'rule test outputs real line breaks');
  await rules.locator('#rulesSearch').fill('nothing matches');check(await rules.locator('#rulesEmpty').isVisible(),'empty search state');check(await rules.locator('#deleteRuleButton').isDisabled(),'no hidden selected rule can be deleted');
  await rules.locator('#addRuleButton').click();check(await rules.locator('#rulesSearch').inputValue()===''&&await rules.locator('[data-field=name]').inputValue()==='新规则','new rule is visible after filtered search');
  await rules.locator('[data-field=name]').fill('第三条');await rules.getByRole('button',{name:'上移',exact:true}).click();
  check(await rules.evaluate(()=>recognitionRulesEditor.rules[1].name)==='第三条','keyboard-friendly reorder');
  const namesBefore=await rules.evaluate(()=>recognitionRulesEditor.rules.map(r=>r.name));
  await rules.locator('.rr-drag').nth(0).dragTo(rules.locator('.rr-rule').nth(2));
  check(JSON.stringify(await rules.evaluate(()=>recognitionRulesEditor.rules.map(r=>r.name)))!==JSON.stringify(namesBefore),'drag reorder');
  rules.once('dialog',d=>d.dismiss());await rules.locator('#deleteRuleButton').click();check(await rules.locator('.rr-rule').count()===3,'cancel deletion preserves rule');
  rules.once('dialog',d=>d.accept());await rules.locator('#deleteRuleButton').click();check(await rules.locator('.rr-rule').count()===2,'confirmed rule deletion');
  await rules.keyboard.press('Control+f');check(await rules.locator('#rulesSearch').evaluate(n=>document.activeElement===n),'keyboard search');
  check((await rules.evaluate(()=>previewErrors)).length===0,'rule interactions no errors');await rules.close();

  const building=await browser.newPage({viewport:{width:640,height:620}});
  await building.goto(pathToFileURL(path.join(directory,'building-dark.html')).href);
  await building.locator('#textHeight').fill('0');await building.locator('#save').click();check(await latest(building,'saveBuildingLengthSettings')===null,'invalid height is not saved');
  await building.locator('#textHeight').fill('1.25');await building.locator('#adaptive').uncheck();await building.locator('#textStyle').selectOption('宋体');
  await building.locator('#linetype').selectOption('DASHED');await building.locator('#lineweight').selectOption('0.25');
  await building.locator('#textColor').click();const colorDraft=JSON.parse(await latest(building,'pickTextColor'));
  check(colorDraft.TextHeight===1.25&&colorDraft.TextStyleName==='宋体','color dialog receives current draft');
  await building.evaluate(()=>CDBoxBuildingColorPicked('text',{Type:3,R:145,G:40,B:205,DisplayName:'自定义紫色'}));
  await building.locator('#auxColor').click();check(await latest(building,'pickAuxiliaryColor')!==null,'auxiliary color route');
  await building.keyboard.press('Control+s');const saved=JSON.parse(await latest(building,'saveBuildingLengthSettings'));
  check(saved.TextHeight===1.25&&!saved.AdaptiveTextHeight&&saved.AuxiliaryLinetypeName==='DASHED'&&saved.AuxiliaryLineweight==='0.25'&&saved.TextColor.R===145,'building setting payload preserves all controls');
  await building.locator('#textHeight').fill('2');await building.evaluate(()=>CDBoxBuildingSettingsSaved());check(await building.locator('#saveState').textContent()==='未保存','save acknowledgement preserves later edits');
  await building.keyboard.press('Control+s');await building.evaluate(()=>CDBoxBuildingSettingsSaved());check(await building.locator('#saveState').textContent()==='已保存','successful save updates state');
  check((await noOverflow(building)).document,'annotation settings fit minimum width');
  check((await building.evaluate(()=>previewErrors)).length===0,'annotation settings no errors');await building.close();
  fs.writeFileSync(path.join(directory,'ui-test-result.json'),JSON.stringify({passed:checks,failed:0},null,2));
  console.log(`Utility workbench UI: ${checks} passed, 0 failed`);
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
