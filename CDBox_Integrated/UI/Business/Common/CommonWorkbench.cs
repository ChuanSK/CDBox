using System.Text.RegularExpressions;

namespace TCPipeAutoDraw.UI.Studio
{
    // Shared presentation for the opted-in Common pages; business routes stay with each page.
    internal static class CommonWorkbench
    {
        public static string Apply(string document, CDBoxStudioSettings settings = null)
        {
            settings = settings ?? CDBoxStudioSettingsStore.Load();
            settings.Normalize();
            document = Regex.Replace(document, "<body[^>]*>", "<body data-appearance='workbench' data-theme='"
                + CDBoxWorkbenchAppearance.ResolveTheme(settings.Theme) + "' class='"
                + (settings.AnimationsEnabled ? "" : "no-animations") + "'>", RegexOptions.IgnoreCase);
            return CDBoxAccentAppearance.Attach(document.Replace("</head>", "<style>" + CDBoxWorkbenchAppearance.BuildThemeStyles(settings)
                + BaseStyles + "</style></head>").Replace("</body>", "<script>"
                + CDBoxWorkbenchThemeScript.Build() + "</script></body>"), settings);
        }

        private const string BaseStyles = @"
*{box-sizing:border-box}html,body{margin:0;min-height:100%;background:var(--bg);color:var(--text)}html:has(body[data-theme='dark']){color-scheme:dark}body{font-size:13px;line-height:1.5}
button,input,select{font:inherit}button{min-height:32px;border:1px solid var(--line);border-radius:7px;padding:5px 12px;background:var(--panel);color:var(--text);cursor:pointer;transition:background .18s,border-color .18s}button:hover{background:var(--hover);border-color:var(--line-strong)}button:disabled{opacity:.45;cursor:not-allowed}.primary,button.primary{background:var(--brand);border-color:var(--brand);color:var(--on-brand)}.primary:hover{opacity:.87}.danger{color:var(--danger)}
button:focus-visible,summary:focus-visible,a:focus-visible{outline:2px solid var(--focus);outline-offset:3px}input,select{min-width:0;width:100%;height:34px;padding:5px 9px;border:1px solid var(--line);border-radius:6px;background:var(--panel2);color:var(--text);font-family:var(--content-font);font-weight:var(--content-weight)}input:focus,select:focus{outline:2px solid var(--focus-ring);border-color:var(--focus)}input[readonly]{background:transparent;border-color:transparent;color:var(--muted)}input[type=checkbox],input[type=radio]{width:16px;height:16px;flex:0 0 16px;accent-color:var(--focus);margin:0}h1{font-size:20px;letter-spacing:-.3px;font-weight:600;margin:0}h2{font-size:14px;font-weight:600;margin:0}strong{font-weight:600}p{color:var(--muted)}[hidden]{display:none!important}.no-animations *{animation:none!important;transition:none!important}@media(prefers-reduced-motion:reduce){*{animation:none!important;transition:none!important}}
.wb-toggle{border:0;background:transparent;padding:4px 0;display:flex;align-items:center;gap:8px;text-align:left;font-weight:600}.wb-toggle:before{content:'⌄';width:12px;color:var(--muted)}.wb-toggle[aria-expanded=false]:before{content:'›'}
";

        // Keep values mounted while collapsing, so toggling a section never discards a draft.
        public const string SectionScript = @"
document.querySelectorAll('.fr-card-head,.ex-card-head').forEach(function(head,index){
 const title=head.querySelector('strong,h2'),body=head.nextElementSibling;if(!title||!body)return;
 const toggle=document.createElement('button');toggle.type='button';toggle.className='wb-toggle';toggle.textContent=title.textContent;toggle.setAttribute('aria-expanded','true');body.id=body.id||'section-body-'+index;toggle.setAttribute('aria-controls',body.id);title.replaceWith(toggle);
 toggle.onclick=function(){body.hidden=!body.hidden;toggle.setAttribute('aria-expanded',String(!body.hidden));window.dispatchEvent(new Event('resize'));};
});";
    }
}
