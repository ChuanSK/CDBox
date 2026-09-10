namespace TCPipeAutoDraw.UI.Studio { internal static class CDBoxWorkbenchThemeScript { public static string Build() { return @"window.CDBoxApplyWorkbenchTheme=function(settings){
 const mode=settings.Theme==='dark'?'dark':'light',p=mode==='dark'?settings.DarkTheme:settings.LightTheme;
 document.body.dataset.theme=mode;document.body.classList.toggle('no-animations',!settings.AnimationsEnabled);
 const rgb=h=>[1,3,5].map(i=>parseInt(h.slice(i,i+2),16));
 const mix=(a,b,t)=>{a=rgb(a);b=rgb(b);return '#'+a.map((v,i)=>Math.round(v+(b[i]-v)*t).toString(16).padStart(2,'0')).join('');};
 const m=t=>mix(p.Background,p.Foreground,t),accent=p.AccentSource==='foreground'?p.Foreground:p.Accent;
 const linear=v=>{v/=255;return v<=.04045?v/12.92:Math.pow((v+.055)/1.055,2.4)},a=rgb(accent).map(linear),l=.2126*a[0]+.7152*a[1]+.0722*a[2],onAccent=(l+.05)/.05>=1.05/(l+.05)?'#000000':'#FFFFFF';
 const sidebar=p.Preset==='codex'&&p.Background.toUpperCase()===(mode==='dark'?'#181818':'#FFFFFF')?(mode==='dark'?'#000000':'#F9F9F9'):m(mode==='dark'?.035:.025);
 const fonts={system:""'Segoe UI','Microsoft YaHei UI',sans-serif"",segoe:""'Segoe UI','Microsoft YaHei UI',sans-serif"",yahei:""'Microsoft YaHei UI','Segoe UI',sans-serif"",sans:""Arial,'Microsoft YaHei UI',sans-serif"",serif:""Georgia,'SimSun',serif"",mono:""Consolas,'Microsoft YaHei UI',monospace""};
 const vars={bg:p.Background,panel:p.Background,sidebar:sidebar,panel2:m(.045),text:p.Foreground,muted:m(.57),line:m(.06+p.Contrast*.0012),'line-strong':m(.15+p.Contrast*.002),hover:m(.045),selected:m(.09),brand:accent,'on-brand':onAccent,focus:accent,'focus-ring':mix(p.Background,accent,.2),'ui-font':fonts[p.UiFont]||fonts.system,'content-font':fonts[p.ContentFont==='inherit'?p.UiFont:p.ContentFont]||fonts.system,'ui-weight':p.UiWeight,'content-weight':p.ContentWeight};
 Object.entries(vars).forEach(([k,v])=>document.body.style.setProperty('--'+k,v));
 if(window.CDBoxApplyAccentTheme)window.CDBoxApplyAccentTheme(settings);
};"; } } }