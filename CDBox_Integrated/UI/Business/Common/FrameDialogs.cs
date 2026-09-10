using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioFrameChoiceWindow
    {
        public static bool TryChoose(string title, string subtitle,
            IList<CDBoxStudioFrameChoiceItem> items, string defaultValue,
            out string selectedValue, IWin32Window owner = null)
        {
            selectedValue = string.Empty;
            List<CDBoxStudioFrameChoiceItem> choices =
                (items ?? new List<CDBoxStudioFrameChoiceItem>())
                    .Where(x => x != null
                        && !string.IsNullOrWhiteSpace(x.Value)).ToList();
            if (choices.Count == 0) return false;

            using (var form = new Form())
            using (var list = new ListBox())
            using (var ok = new Button())
            using (var cancel = new Button())
            using (var hint = new Label())
            {
                form.Text = string.IsNullOrWhiteSpace(title)
                    ? "请选择" : title.Trim();
                form.Font = new Font("Microsoft YaHei UI", 9F);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(470,
                    Math.Min(520, 160 + choices.Count * 54));

                hint.Text = subtitle ?? string.Empty;
                hint.AutoEllipsis = true;
                hint.SetBounds(16, 14, 438, 34);
                list.SetBounds(16, 52, 438,
                    form.ClientSize.Height - 106);
                list.DisplayMember = "Title";
                // Do not use DataSource here.  Under older AutoCAD/WinForms
                // hosts (notably AutoCAD 2016), currency-manager binding can
                // be deferred until the form receives a BindingContext.  At
                // this point Items may therefore still be empty, making the
                // otherwise valid SelectedIndex = 0 throw an
                // ArgumentOutOfRangeException.  The choices are a fixed
                // snapshot, so populate them synchronously instead.
                list.Items.AddRange(choices.Cast<object>().ToArray());
                list.IntegralHeight = false;
                list.BorderStyle = BorderStyle.None;
                list.DrawMode = DrawMode.OwnerDrawFixed;
                list.ItemHeight = 38;
                var appearance = CDBoxStudioSettingsStore.Load();
                var profile = appearance.Theme == "dark" ? appearance.DarkTheme : appearance.LightTheme;
                Color background = ColorTranslator.FromHtml(profile.Background);
                Color foreground = ColorTranslator.FromHtml(profile.Foreground);
                Color selectedBackground = CDBoxThemeCatalog.Mix(profile.Background, profile.Foreground, .10);
                list.DrawItem += delegate(object sender, DrawItemEventArgs e)
                {
                    if (e.Index < 0 || e.Index >= choices.Count) return;
                    using (var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? selectedBackground : background))
                        e.Graphics.FillRectangle(brush, e.Bounds);
                    var bounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, choices[e.Index].Title, form.Font, bounds, foreground,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    e.DrawFocusRectangle();
                };

                int selected = choices.FindIndex(x => string.Equals(
                    x.Value, defaultValue,
                    StringComparison.OrdinalIgnoreCase));
                int targetIndex = selected >= 0
                    && selected < list.Items.Count ? selected
                    : (list.Items.Count > 0 ? 0 : -1);
                if (targetIndex >= 0) list.SelectedIndex = targetIndex;

                ok.Text = "确定";
                ok.DialogResult = DialogResult.OK;
                ok.SetBounds(286, form.ClientSize.Height - 42, 80, 28);
                cancel.Text = "取消";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(374, form.ClientSize.Height - 42, 80, 28);
                form.Controls.AddRange(new Control[]
                    { hint, list, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                CommonDialogAppearance.Apply(form, ok);
                if (form.ShowDialog(owner) != DialogResult.OK
                    || list.SelectedItem == null) return false;

                selectedValue = ((CDBoxStudioFrameChoiceItem)
                    list.SelectedItem).Value ?? string.Empty;
                return selectedValue.Length > 0;
            }
        }
    }

    internal static class CDBoxStudioFrameNoticeWindow
    {
        public static void ShowNotice(string title, IList<string> messages,
            IWin32Window owner = null)
        {
            string message = string.Join(Environment.NewLine,
                (messages ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => "• " + x.Trim()));
            if (message.Length == 0) return;
            using (var form = new Form())
            using (var body = new TextBox())
            using (var ok = new Button())
            {
                form.Text = string.IsNullOrWhiteSpace(title) ? "图框提示" : title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(540, 320);
                form.MinimumSize = new Size(440, 260);
                body.Multiline = true;
                body.ReadOnly = true;
                body.BorderStyle = BorderStyle.None;
                body.ScrollBars = ScrollBars.Vertical;
                body.Text = message;
                body.SetBounds(24, 24, 492, 230);
                body.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                ok.Text = "知道了";
                ok.SetBounds(426, 270, 90, 32);
                ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                ok.DialogResult = DialogResult.OK;
                form.Controls.AddRange(new Control[] { body, ok });
                form.AcceptButton = ok;
                form.CancelButton = ok;
                CommonDialogAppearance.Apply(form, ok);
                form.ShowDialog(owner);
            }
        }
    }

    internal static class CommonDialogAppearance
    {
        public static void Apply(Form form, Button primary)
        {
            var settings = CDBoxStudioSettingsStore.Load();
            var profile = settings.Theme == "dark" ? settings.DarkTheme : settings.LightTheme;
            Color background = ColorTranslator.FromHtml(profile.Background);
            Color foreground = ColorTranslator.FromHtml(profile.Foreground);
            form.Font = new Font(FontFamily(profile.UiFont), 9F, profile.UiWeight >= 600 ? FontStyle.Bold : FontStyle.Regular);
            form.BackColor = background;
            form.ForeColor = foreground;
            foreach (Control control in form.Controls)
            {
                control.BackColor = background;
                control.ForeColor = foreground;
                var button = control as Button;
                if (button == null) continue;
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = CDBoxThemeCatalog.Mix(profile.Background, profile.Foreground, .06 + profile.Contrast * .0012);
            }
            primary.BackColor = CDBoxAccentAppearance.AccentColor;
            primary.ForeColor = CDBoxAccentAppearance.OnAccentColor;
        }

        private static string FontFamily(string key)
        {
            switch (key)
            {
                case "yahei": return "Microsoft YaHei UI";
                case "sans": return "Arial";
                case "serif": return "Georgia";
                case "mono": return "Consolas";
                default: return "Segoe UI";
            }
        }
    }
}
