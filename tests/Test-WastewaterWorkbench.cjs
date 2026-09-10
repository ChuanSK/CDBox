const {chromium}=require('C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs=require('node:fs/promises');
const path=require('node:path');
const assert=require('node:assert/strict');
(async()=>{
 const out=path.resolve('artifacts/5.1.5');await fs.mkdir(path.join(out,'screenshots'),{recursive:true});
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1240,height:960}});
  await page.goto('http://127.0.0.1:8772/');await page.locator('#check').click();
  await page.waitForFunction(()=>!document.querySelector('#check').disabled,{},{timeout:90000});
  const report=await page.locator('#report').textContent();await fs.writeFile(path.join(out,'browser-checks.log'),report);
  if(report.includes('FAIL')||!(await page.locator('#status').innerText()).includes('项检查通过'))throw Error(report.split('\n').slice(-8).join('\n'));
  console.log(await page.locator('#status').innerText());
  const extra=[];
  const ok=(value,label)=>{assert.ok(value,label);extra.push('PASS '+label)};
  await page.goto('http://127.0.0.1:8772/defaults.html');
  await page.locator('[data-profile-input="0"]').fill('新默认管材');
  await page.getByRole('button',{name:'支管',exact:true}).click();
  await page.getByRole('button',{name:'主管',exact:true}).click();
  ok(await page.locator('[data-profile-input="0"]').inputValue()==='新默认管材','默认表切换类型保留草稿');
  await page.locator('#saveDefaultProfilesButton').click();
  ok(await page.evaluate(()=>previewMessages.some(x=>x.includes('|saveDefaultProfiles|')&&decodeURIComponent(x).includes(btoa(unescape(encodeURIComponent('新默认管材')))))),'默认表保存发送已修改值');
  ok(await page.getByRole('button',{name:'检查井',exact:true}).getAttribute('data-accent-action')===null,'检查井类型导航不误判为主操作');
  await page.goto('http://127.0.0.1:8772/dashboard.html');
  await page.locator('[data-summary-card=pipe]').click();
  ok(await page.locator('[data-role=card-expand] .qd-card-expand').evaluate(e=>getComputedStyle(e).opacity==='1'&&e.getBoundingClientRect().height>40),'汇总指标可展开查看组成');
  await page.locator('[data-row-action=edit]').click();
  ok(await page.evaluate(()=>previewMessages.some(x=>x.includes('|openQuantityObjectEditor|')&&decodeURIComponent(x).includes('A1'))),'明细编辑仍传递正确对象句柄');
  await page.goto('http://127.0.0.1:8772/longitudinal.html');
  await page.getByRole('button',{name:'保存设置',exact:true}).click();
  ok(await page.evaluate(()=>previewMessages.some(x=>x.includes('|saveLongitudinalProfileSettings|'))),'纵断面保存仍使用原路由');
  for(const name of ['dashboard','attributes','defaults','annotations','section','longitudinal']){
   await page.goto('http://127.0.0.1:8772/'+name+'.html');await page.waitForTimeout(350);
   const values=await page.locator('input').evaluateAll(es=>es.map(e=>[e.value,e.checked]));
   await page.evaluate(()=>{const p={Background:'#20252B',Foreground:'#E6EDF3',Accent:'#C77DFF',AccentSource:'custom',Preset:'custom',Contrast:60,UiFont:'yahei',ContentFont:'mono',UiWeight:400,ContentWeight:400};window.CDBoxApplyWorkbenchTheme({Theme:'dark',AnimationsEnabled:false,LightTheme:p,DarkTheme:p})});
   ok(await page.evaluate(()=>getComputedStyle(document.body).backgroundColor==='rgb(32, 37, 43)'),name+' 即时切换自定义主题');
   assert.deepEqual(await page.locator('input').evaluateAll(es=>es.map(e=>[e.value,e.checked])),values);
   extra.push('PASS '+name+' 切换主题保留字段');
   ok(await page.locator('button[data-accent-action],button.primary').first().evaluate(e=>getComputedStyle(e).backgroundColor==='rgb(199, 125, 255)'),name+' 主操作使用自定义强调色');
  }
  await fs.appendFile(path.join(out,'browser-checks.log'),'\n'+extra.join('\n'));
  console.log(extra.length+' additional interaction/theme checks passed.');
  await page.setViewportSize({width:1180,height:900});
  for(const name of ['dashboard','attributes','defaults','annotations','section','longitudinal']){
   await page.goto('http://127.0.0.1:8772/'+name+'.html');await page.waitForTimeout(600);
   for(const theme of ['light','dark']){
    await page.evaluate(theme=>{const base={Preset:'codex',AccentSource:'custom',Contrast:50,UiFont:'system',ContentFont:'inherit',UiWeight:400,ContentWeight:400};window.CDBoxApplyWorkbenchTheme({Theme:theme,AnimationsEnabled:false,LightTheme:{...base,Background:'#FFFFFF',Foreground:'#1A1C1F',Accent:'#0169CC'},DarkTheme:{...base,Background:'#181818',Foreground:'#DFDFDF',Accent:'#70B8FF'}})},theme);await page.waitForTimeout(250);
    await page.screenshot({path:path.join(out,'screenshots',name+'-'+theme+'.png'),fullPage:true});
    if(['dashboard','attributes'].includes(name)){
     const root=page.locator(name==='dashboard'?'.qd-page':'.qa-page');
     await root.evaluate(e=>e.scrollTop=e.scrollHeight);await page.waitForTimeout(200);
     await page.screenshot({path:path.join(out,'screenshots',name+'-'+theme+'-details.png')});
     await root.evaluate(e=>e.scrollTop=0);
    }
   }
  }
  console.log('Captured six real generated pages in light and dark themes.');
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
