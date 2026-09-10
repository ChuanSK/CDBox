using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Media = System.Windows.Media;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class WastewaterNativeAppearance
    {
        private static readonly List<WeakReference> Windows = new List<WeakReference>();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> StyledControls
            = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();
        private static readonly Media.SolidColorBrush Surface = new Media.SolidColorBrush();
        private static readonly Media.SolidColorBrush Input = new Media.SolidColorBrush();
        private static readonly Media.SolidColorBrush Text = new Media.SolidColorBrush();
        private static readonly Media.SolidColorBrush Muted = new Media.SolidColorBrush();
        private static readonly Media.SolidColorBrush Line = new Media.SolidColorBrush();
        private static CDBoxThemeProfile Profile;
        static WastewaterNativeAppearance() { Refresh(CDBoxStudioSettingsStore.Load()); }

        public static Color Background => Drawing(Surface);
        public static Color Foreground => Drawing(Text);
        public static Color Border => Drawing(Line);
        public static Color Selected => CDBoxThemeCatalog.Mix(Profile.Background,
            CDBoxThemeCatalog.Accent(Profile), .12);
        public static string FontName => CDBoxThemeCatalog.Font(Profile.UiFont).Split(',')[0].Trim('\'');
        private static Color Drawing(Media.SolidColorBrush brush) => Color.FromArgb(
            brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);
        private static void Update(Media.SolidColorBrush brush, Color color)
        {
            Action update = () => brush.Color = Media.Color.FromArgb(color.A, color.R, color.G, color.B);
            if (brush.Dispatcher.CheckAccess()) update(); else brush.Dispatcher.Invoke(update);
        }

        public static void Refresh(CDBoxStudioSettings settings)
        {
            settings.Normalize();
            Profile = settings.Theme == "dark" ? settings.DarkTheme : settings.LightTheme;
            Update(Surface, ColorTranslator.FromHtml(Profile.Background));
            Update(Input, CDBoxThemeCatalog.Mix(Profile.Background, Profile.Foreground, .035));
            Update(Text, ColorTranslator.FromHtml(Profile.Foreground));
            Update(Muted, CDBoxThemeCatalog.Mix(Profile.Background, Profile.Foreground, .62));
            Update(Line, CDBoxThemeCatalog.Mix(Profile.Background, Profile.Foreground, .18));
            for (int i = Windows.Count - 1; i >= 0; i--)
            {
                object target = Windows[i].Target;
                if (target == null) { Windows.RemoveAt(i); continue; }
                var form = target as Form;
                if (form != null) { if (!form.IsDisposed) Apply(form); continue; }
                var window = target as System.Windows.Window;
                if (window != null) window.Dispatcher.Invoke(new Action(() => {
                    window.Foreground = Text; window.FontFamily = new Media.FontFamily(FontName);
                }));
            }
        }

        public static void Register(Form form)
        {
            Windows.Add(new WeakReference(form));
            Apply(form);
        }
        public static void Register(System.Windows.Window window)
        {
            Windows.Add(new WeakReference(window));
            window.Foreground = Text;
            window.FontFamily = new Media.FontFamily(FontName);
        }
        private static void Apply(Control control)
        {
            // Custom CAD preview and color-swatch controls own their rendered colors.
            if (control.GetType().Name.Contains("Preview") || control.GetType().Name.Contains("ColorPicker")) return;
            object marker;
            if (!StyledControls.TryGetValue(control, out marker))
            {
                StyledControls.Add(control, new object());
                control.ControlAdded += (sender, e) => Apply(e.Control);
            }
            control.BackColor = control is TextBoxBase || control is ComboBox || control is NumericUpDown
                ? Drawing(Input) : Background;
            control.ForeColor = Foreground;
            if (control is Form && control.Font.Name != FontName)
                control.Font = new Font(FontName, control.Font.Size, control.Font.Style);
            var button = control as Button;
            if (button != null) { button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Border; }
            foreach (Control child in control.Controls) Apply(child);
            if (control is Form || control is Button) CDBoxAccentAppearance.ApplyNative(control);
            control.Invalidate();
        }

        public static Media.SolidColorBrush Brush(string hex)
        {
            switch (hex.ToUpperInvariant())
            {
                case "#FFFFFFFF": case "#FFFFFF": case "#FFF": return Surface;
                case "#FAFCFF": case "#F7FAFF": case "#F4F7FB": return Input;
                case "#253550": case "#31415A": case "#172033": return Text;
                case "#53637A": case "#607089": case "#65758A": return Muted;
                case "#B8C8DAEE": case "#C7D4E5": case "#DCE5F1": case "#D8E2EF": return Line;
                case "#FF73A9F5": return (Media.SolidColorBrush)CDBoxAccentAppearance.AccentBrush;
            }
            if (CDBoxAccentAppearance.IsAccentHex(hex)) return (Media.SolidColorBrush)CDBoxAccentAppearance.AccentBrush;
            if (CDBoxAccentAppearance.IsAccentSoftHex(hex)) return (Media.SolidColorBrush)CDBoxAccentAppearance.SoftBrush;
            var brush = new Media.SolidColorBrush((Media.Color)Media.ColorConverter.ConvertFromString(hex));
            brush.Freeze(); return brush;
        }
    }

    // Preserve GroupBox layout/keyboard semantics while drawing only a section rule.
    internal sealed class WastewaterSectionGroup : GroupBox
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            var size = TextRenderer.MeasureText(Text, Font);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Point(0, 0), ForeColor,
                TextFormatFlags.NoPrefix);
            using (var pen = new Pen(WastewaterNativeAppearance.Border))
                e.Graphics.DrawLine(pen, Math.Min(Width, size.Width + 12), size.Height / 2,
                    Width - 1, size.Height / 2);
        }
    }
}
