using System;
using System.Drawing;

namespace TCPipeAutoDraw.UI.Studio
{
    /// <summary>
    /// Opt-in workbench appearance. The settings page and accepted parcel editor share the workbench theme system.
    /// Neutral surface/text values follow Codex desktop's electron light/dark tokens.
    /// </summary>
    internal static class CDBoxWorkbenchAppearance
    {
        public static bool IsEnabledFor(string pageId)
        {
            return string.Equals(pageId, "realestate-parcel-survey-editor",
                StringComparison.Ordinal) || pageId == "cdbox-settings" || pageId == "realestate-building-capture"
                || pageId == "common-home" || pageId == "common-short-code-settings"
                || pageId == "common-frame-settings" || pageId == "common-excel-to-cad"
                || pageId == "wastewater-quantity-dashboard" || pageId == "quantity-attribute-editor"
                || pageId == "wastewater-annotation-settings" || pageId == "wastewater-section-drawing"
                || pageId == "wastewater-longitudinal-profile-settings" || pageId == "default-profiles"
                || pageId == "base-layer-manager" || pageId == "base-layer-recognition-rules"
                || pageId == "realestate-building-length-settings";
        }

        // The legacy fresh preference uses the neutral light palette in this pilot.
        // Keep the persisted setting intact so other pages retain their appearance.
        public static string ResolveTheme(string theme)
        {
            return string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase)
                ? "dark" : "light";
        }

        public static Color ChromeBackground(string theme)
        {
            var settings = CDBoxStudioSettingsStore.Load();
            return ColorTranslator.FromHtml(CDBoxThemeCatalog.Surface(ResolveTheme(theme) == "dark" ? settings.DarkTheme : settings.LightTheme, ResolveTheme(theme)));
        }

        public static Color Foreground(string theme)
        {
            var settings = CDBoxStudioSettingsStore.Load();
            return ColorTranslator.FromHtml((ResolveTheme(theme) == "dark" ? settings.DarkTheme : settings.LightTheme).Foreground);
        }

        public static Color HoverBackground(string theme)
        {
            var settings = CDBoxStudioSettingsStore.Load();
            var p = ResolveTheme(theme) == "dark" ? settings.DarkTheme : settings.LightTheme;
            return CDBoxThemeCatalog.Mix(p.Background, p.Foreground, .045);
        }

        public static string BuildThemeStyles() { return BuildThemeStyles(CDBoxStudioSettingsStore.Load()); }

        public static string BuildThemeStyles(CDBoxStudioSettings settings)
        {
            settings.Normalize();
            return @"
/* Explicit opt-in; these tokens never replace the legacy Studio theme globally. */
body[data-appearance='workbench']{
 color-scheme:light;
 --bg:#fff;--panel:#fff;--panel2:#f9f9f9;--sidebar:#f9f9f9;
 --text:#1a1c1f;--muted:#606266;--line:rgba(26,28,31,.08);--line-strong:rgba(26,28,31,.18);
 --hover:rgba(26,28,31,.04);--selected:rgba(26,28,31,.08);
 --brand:#1a1c1f;--on-brand:#fff;--focus:#0169cc;--focus-ring:rgba(1,105,204,.16);
 --ok:#16794b;--danger:#b42318;--danger-soft:#fdf0ef;
 --auto:#edf5ff;--auto-text:#005ba8;--default:#f2effa;--default-text:#6842a6;
 --import:#edf8f1;--import-text:#16794b;--manual:#f8f4e8;--manual-text:#806016;
 --na:#f0f0f0;--na-text:#606266;--overlay:rgba(0,0,0,.32);
 --popup-shadow:0 8px 28px rgba(0,0,0,.12);
}
body[data-appearance='workbench'][data-theme='dark']{
 color-scheme:dark;
 --bg:#181818;--panel:#181818;--panel2:#212121;--sidebar:#000;
 --text:#dfdfdf;--muted:#a6a6a6;--line:rgba(255,255,255,.10);--line-strong:rgba(255,255,255,.22);
 --hover:rgba(255,255,255,.05);--selected:rgba(255,255,255,.10);
 --brand:#dfdfdf;--on-brand:#181818;--focus:#70b8ff;--focus-ring:rgba(112,184,255,.20);
 --ok:#75c99e;--danger:#ff938a;--danger-soft:#382321;
 --auto:#1d2d3b;--auto-text:#9bcbfa;--default:#2b2537;--default-text:#c9b1ef;
 --import:#20332a;--import-text:#8fd4ad;--manual:#332e20;--manual-text:#dbc389;
 --na:#292929;--na-text:#ababab;--overlay:rgba(0,0,0,.60);
 --popup-shadow:0 8px 28px rgba(0,0,0,.40);
}
" + "body[data-appearance='workbench']{" + CDBoxThemeCatalog.Css(settings.LightTheme,"light") + "}body[data-appearance='workbench'][data-theme='dark']{" + CDBoxThemeCatalog.Css(settings.DarkTheme,"dark") + "}body[data-appearance='workbench']{font-family:var(--ui-font);font-weight:var(--ui-weight)}body[data-appearance='workbench'] .field-editor input,body[data-appearance='workbench'] .field-editor textarea,body[data-appearance='workbench'] .field-display{font-family:var(--content-font);font-weight:var(--content-weight)}";
        }
    }
}
