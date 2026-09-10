using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxThemeProfile
    {
        public string Preset { get; set; } = "codex";
        public string Accent { get; set; } = "#0169CC";
        public string Background { get; set; } = "#FFFFFF";
        public string Foreground { get; set; } = "#1A1C1F";
        public string AccentSource { get; set; } = "custom";
        public string UiFont { get; set; } = "system";
        public string ContentFont { get; set; } = "inherit";
        public int UiWeight { get; set; } = 400;
        public int ContentWeight { get; set; } = 400;
        public int Contrast { get; set; } = 50;

        public void Normalize(string mode)
        {
            var fallback = CDBoxThemeCatalog.Create("codex", mode);
            Preset = CDBoxThemeCatalog.Presets.Any(x => x.Id == Preset) ? Preset : "custom";
            Accent = Hex(Accent, fallback.Accent);
            Background = Hex(Background, fallback.Background);
            Foreground = Hex(Foreground, fallback.Foreground);
            AccentSource = AccentSource == "foreground" ? "foreground" : "custom";
            UiFont = CDBoxThemeCatalog.Fonts.Contains(UiFont) && UiFont != "inherit" ? UiFont : "system";
            ContentFont = CDBoxThemeCatalog.Fonts.Contains(ContentFont) ? ContentFont : "inherit";
            UiWeight = UiWeight == 500 || UiWeight == 600 ? UiWeight : 400;
            ContentWeight = ContentWeight == 500 || ContentWeight == 600 ? ContentWeight : 400;
            Contrast = Math.Max(0, Math.Min(100, Contrast));
        }
        internal static string Hex(string value, string fallback)
        {
            return Regex.IsMatch(value ?? "", "^#[0-9a-fA-F]{6}$") ? value.ToUpperInvariant() : fallback;
        }
    }
    internal sealed class CDBoxThemePreset
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public CDBoxThemeProfile Light { get; set; }
        public CDBoxThemeProfile Dark { get; set; }
    }
    internal static class CDBoxThemeCatalog
    {
        public static readonly string[] Fonts = { "system", "inherit", "segoe", "yahei", "sans", "serif", "mono" };
        public static readonly CDBoxThemePreset[] Presets = {
            Pair("codex", "Codex", "#FFFFFF", "#1A1C1F", "#0169CC", "#181818", "#DFDFDF", "#70B8FF"),
            Pair("github", "GitHub", "#FFFFFF", "#1F2328", "#0969DA", "#0D1117", "#E6EDF3", "#1F6FEB"),
            Pair("solarized", "Solarized", "#FDF6E3", "#657B83", "#268BD2", "#002B36", "#839496", "#268BD2"),
            Pair("nord", "Nord", "#ECEFF4", "#2E3440", "#5E81AC", "#2E3440", "#D8DEE9", "#88C0D0"),
            Pair("one", "One", "#FAFAFA", "#383A42", "#4078F2", "#282C34", "#ABB2BF", "#61AFEF")
        };
        private static CDBoxThemePreset Pair(string id, string name, string lb, string lf, string la, string db, string df, string da)
        {
            return new CDBoxThemePreset { Id = id, Name = name,
                Light = new CDBoxThemeProfile { Preset=id, Background=lb, Foreground=lf, Accent=la },
                Dark = new CDBoxThemeProfile { Preset=id, Background=db, Foreground=df, Accent=da } };
        }
        public static CDBoxThemeProfile Create(string id, string mode)
        {
            var preset = Presets.FirstOrDefault(x => x.Id == id) ?? Presets[0];
            var p = mode == "dark" ? preset.Dark : preset.Light;
            return new CDBoxThemeProfile { Preset=p.Preset, Accent=p.Accent, Background=p.Background, Foreground=p.Foreground };
        }
        public static string Json(object value) => new JavaScriptSerializer().Serialize(value).Replace("<", "\\u003c").Replace(">", "\\u003e").Replace("&", "\\u0026");
        public static string Font(string key)
        {
            switch (key) {
                case "segoe": return "'Segoe UI','Microsoft YaHei UI',sans-serif";
                case "yahei": return "'Microsoft YaHei UI','Segoe UI',sans-serif";
                case "sans": return "Arial,'Microsoft YaHei UI',sans-serif";
                case "serif": return "Georgia,'SimSun',serif";
                case "mono": return "Consolas,'Microsoft YaHei UI',monospace";
                default: return "'Segoe UI','Microsoft YaHei UI',sans-serif";
            }
        }
        public static Color Mix(string a, string b, double ratio)
        {
            Color x=ColorTranslator.FromHtml(a),y=ColorTranslator.FromHtml(b);
            return Color.FromArgb((int)Math.Round(x.R+(y.R-x.R)*ratio), (int)Math.Round(x.G+(y.G-x.G)*ratio), (int)Math.Round(x.B+(y.B-x.B)*ratio));
        }
        public static string Surface(CDBoxThemeProfile p, string mode)
        {
            if (p.Preset == "codex" && p.Background == (mode == "dark" ? "#181818" : "#FFFFFF"))
                return mode == "dark" ? "#000000" : "#F9F9F9";
            return ColorTranslator.ToHtml(Mix(p.Background,p.Foreground,mode == "dark" ? .035 : .025));
        }
        public static string Accent(CDBoxThemeProfile p) => p.AccentSource == "foreground" ? p.Foreground : p.Accent;
        public static string OnAccent(string accent)
        {
            Color c = ColorTranslator.FromHtml(accent);
            double linear(byte channel) { double v=channel/255.0; return v<=.04045 ? v/12.92 : Math.Pow((v+.055)/1.055,2.4); }
            double luminance=.2126*linear(c.R)+.7152*linear(c.G)+.0722*linear(c.B);
            return (luminance+.05)/.05 >= 1.05/(luminance+.05) ? "#000000" : "#FFFFFF";
        }
        public static string Css(CDBoxThemeProfile p, string mode)
        {
            string accent=p.AccentSource == "foreground" ? p.Foreground : p.Accent;
            string m(double t) => ColorTranslator.ToHtml(Mix(p.Background,p.Foreground,t));
            return "--bg:"+p.Background+";--panel:"+p.Background+";--sidebar:"+Surface(p,mode)+";--panel2:"+m(.045)
                +";--text:"+p.Foreground+";--muted:"+m(.57)+";--line:"+m(.06+p.Contrast*.0012)+";--line-strong:"+m(.15+p.Contrast*.002)
                +";--hover:"+m(.045)+";--selected:"+m(.09)+";--brand:"+accent+";--on-brand:"+OnAccent(accent)
                +";--focus:"+accent+";--focus-ring:"+ColorTranslator.ToHtml(Mix(p.Background,accent,.20))
                +";--ui-font:"+Font(p.UiFont)+";--content-font:"+Font(p.ContentFont == "inherit" ? p.UiFont : p.ContentFont)
                +";--ui-weight:"+p.UiWeight+";--content-weight:"+p.ContentWeight+";";
        }
    }
}
