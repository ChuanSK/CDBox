using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace TCPipeAutoDraw.UI.Studio
{
    // Accent-only integration for all hosted pages. Legacy layouts and surface palettes stay intact.
    internal static class CDBoxAccentAppearance
    {
        private static CDBoxThemeProfile CurrentProfile
        {
            get { var s=CDBoxStudioSettingsStore.Load(); return s.Theme=="dark" ? s.DarkTheme : s.LightTheme; }
        }
        public static Color AccentColor => ColorTranslator.FromHtml(CDBoxThemeCatalog.Accent(CurrentProfile));
        public static Color OnAccentColor => ColorTranslator.FromHtml(CDBoxThemeCatalog.OnAccent(CDBoxThemeCatalog.Accent(CurrentProfile)));
        public static bool IsAccentHex(string hex) => Regex.IsMatch(hex ?? "", "^#(316FE8|326FEA|2563EB|3B82F6|6D28D9|7C5CFC|3D63A7|244A84|2458B8|5D96EA)$", RegexOptions.IgnoreCase);
        public static bool IsAccentSoftHex(string hex) => Regex.IsMatch(hex ?? "", "^#(EAF2FF|EDF4FF|E2EFFF|F3EEFF|EAF1FB|EDE9FE)$", RegexOptions.IgnoreCase);
        private static System.Windows.Media.SolidColorBrush _accentBrush, _onAccentBrush, _softBrush;
        public static System.Windows.Media.Brush AccentBrush => _accentBrush ?? (_accentBrush = WpfBrush(AccentColor));
        public static System.Windows.Media.Brush OnAccentBrush => _onAccentBrush ?? (_onAccentBrush = WpfBrush(OnAccentColor));
        public static System.Windows.Media.Brush SoftBrush
        {
            get { var p=CurrentProfile; return _softBrush ?? (_softBrush = WpfBrush(CDBoxThemeCatalog.Mix(p.Background,CDBoxThemeCatalog.Accent(p),.12))); }
        }
        private static System.Windows.Media.SolidColorBrush WpfBrush(Color c)
        {
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(c.R,c.G,c.B));
        }
        public static void RefreshNative(CDBoxStudioSettings settings)
        {
            var p=settings.Theme=="dark"?settings.DarkTheme:settings.LightTheme;var a=CDBoxThemeCatalog.Accent(p);
            UpdateBrush(_accentBrush,ColorTranslator.FromHtml(a));
            UpdateBrush(_onAccentBrush,ColorTranslator.FromHtml(CDBoxThemeCatalog.OnAccent(a)));
            UpdateBrush(_softBrush,CDBoxThemeCatalog.Mix(p.Background,a,.12));
        }
        private static void UpdateBrush(System.Windows.Media.SolidColorBrush brush,Color c)
        {
            if(brush==null)return;
            Action update=()=>brush.Color=System.Windows.Media.Color.FromRgb(c.R,c.G,c.B);
            if(brush.CheckAccess())update();else brush.Dispatcher.BeginInvoke(update);
        }

        public static void ApplyNative(Control root)
        {
            var accent=AccentColor;var foreground=OnAccentColor;
            ApplyNative(root,accent,foreground);
        }
        private static void ApplyNative(Control root,Color accent,Color foreground)
        {
            var button=root as Button;
            if(button!=null && Regex.IsMatch(button.Text ?? "", @"^(保存|导出|应用|确认|确定|完成|生成|绘制|布置|标注|下一步|知道了)"))
            {
                button.UseVisualStyleBackColor=false;button.FlatStyle=FlatStyle.Flat;
                button.BackColor=accent;button.ForeColor=foreground;button.FlatAppearance.BorderColor=accent;
            }
            foreach(Control child in root.Controls)ApplyNative(child,accent,foreground);
        }

        public static string Attach(string html, CDBoxStudioSettings settings = null)
        {
            if(string.IsNullOrEmpty(html)||html.Contains("<style id='cdbox-accent-style'>"))return html;
            settings=settings ?? CDBoxStudioSettingsStore.Load();settings.Normalize();
            string css=Colors(settings.LightTheme,"light")+Colors(settings.DarkTheme,"dark")+Styles;
            html=html.Replace("</head>","<style id='cdbox-accent-style'>"+css+"</style></head>");
            return html.Replace("</body>","<script>"+Script+"\nwindow.CDBoxApplyAccentTheme("+CDBoxThemeCatalog.Json(settings)+");</script></body>");
        }
        private static string Colors(CDBoxThemeProfile p,string mode)
        {
            string a=CDBoxThemeCatalog.Accent(p);
            return (mode=="light"?"body:not([data-theme='dark'])":"body[data-theme='dark']")
                +"{--brand:"+a+"!important;--brand2:"+a+"!important;--focus:"+a+"!important;--on-brand:"
                +CDBoxThemeCatalog.OnAccent(a)+"!important;}";
        }
        internal const string Styles = @"
body{--accent-soft:color-mix(in srgb,var(--brand) 12%,var(--panel,#fff));--accent-ring:color-mix(in srgb,var(--brand) 24%,transparent)}
body button.primary,body .primary-btn,body .btn-primary,body button.save,body button[data-accent-action],body input[data-accent-action]{background:var(--brand)!important;background-image:none!important;color:var(--on-brand)!important;border-color:var(--brand)!important;box-shadow:none!important}
body .primary-soft{background:var(--accent-soft)!important;color:var(--brand)!important;border-color:var(--brand)!important}
body button:focus-visible,body [role=button]:focus-visible{outline-color:var(--brand)!important}
body input:focus,body select:focus,body textarea:focus{border-color:var(--brand)!important;box-shadow:0 0 0 2px var(--accent-ring)!important}
body input[type=checkbox],body input[type=radio],body input[type=range]{accent-color:var(--brand)!important}
body .nav-item.active,body .profile-tab.active,body nav button.active,body .settings-nav button[aria-current=page],body .segmented button[aria-pressed=true],body .segmented button.selected,body .fr-tabs button.active,body .fr-view-buttons button.active{color:var(--brand)!important;background:var(--accent-soft)!important;border-color:var(--brand)!important}
body .switch input:checked+span,body .switch input:checked+.slider,body .toggle:checked,body .update-progress span{background:var(--brand)!important;border-color:var(--brand)!important}
body .toggle:checked:before,body .switch input:checked+.slider:before{background:var(--on-brand)!important}
body .logo,body .icon{background:var(--brand)!important;color:var(--on-brand)!important}
body .nav-count,body .group-progress.pending,body .type-mark{color:var(--brand)!important}
body .ex-choice.selected,body .fr-template.active{background:var(--accent-soft)!important;border-color:var(--brand)!important;color:var(--brand)!important}
body .side-tip button{background:var(--brand)!important;color:var(--on-brand)!important}
";
        private const string Script = @"
(function(){
 let settings,queued=false;
 const action=/^(保存|导出|应用|确认|确定|完成|生成|绘制|布置|标注|添加|新增|导入|刷新|重新生成|开始|执行|计算|检查|复制|填写|直接|在图中|设为默认|重试|下一步)/;
 function mark(){queued=false;document.querySelectorAll('button,input[type=submit],input[type=button]').forEach(b=>{
  if(b.closest('[role=menu],nav,.sidebar,.settings-nav,.fr-tabs')||b.matches('[role=menuitem],.export-option,.group-toggle,.wb-toggle,.qd-tab,[data-tab],[data-mode],.danger,[data-remove],.delete')){b.removeAttribute('data-accent-action');return;}
  const yes=action.test((b.textContent||b.value||'').trim());if(yes)b.setAttribute('data-accent-action','');else b.removeAttribute('data-accent-action');
 });}
 function colors(doc){
  if(!doc||!doc.body)return;const p=doc.body.dataset.theme==='dark'?settings.DarkTheme:settings.LightTheme,a=p.AccentSource==='foreground'?p.Foreground:p.Accent;
  const c=[1,3,5].map(i=>parseInt(a.slice(i,i+2),16)/255).map(v=>v<=.04045?v/12.92:Math.pow((v+.055)/1.055,2.4)),l=.2126*c[0]+.7152*c[1]+.0722*c[2],on=(l+.05)/.05>=1.05/(l+.05)?'#000000':'#FFFFFF';
  for(const [k,v] of Object.entries({brand:a,brand2:a,focus:a,'on-brand':on}))doc.body.style.setProperty('--'+k,v,'important');
 }
 window.CDBoxApplyAccentTheme=function(s,updateMode){settings=s;if(updateMode)document.body.dataset.theme=s.Theme==='dark'?'dark':'light';colors(document);mark();};
 new MutationObserver(records=>{if(records.some(r=>r.type==='attributes')){if(settings)colors(document);}else if(!queued){queued=true;requestAnimationFrame(mark);}}).observe(document.body,{childList:true,subtree:true,characterData:true,attributes:true,attributeFilter:['data-theme']});
})();";
    }
}
