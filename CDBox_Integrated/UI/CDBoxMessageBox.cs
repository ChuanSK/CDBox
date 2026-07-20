using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TCPipeAutoDraw.UI
{
    internal sealed class CDBoxMessageBox : Form
    {
        private const int Radius = 16;
        private readonly MessageBoxDefaultButton _defaultButton;

        private CDBoxMessageBox(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            _defaultButton = defaultButton;
            Text = string.IsNullOrWhiteSpace(caption) ? "CDBox" : caption;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(247, 249, 254);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            Padding = new Padding(1);
            Width = 520;
            BuildUi(text ?? string.Empty, buttons, icon);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyRegion();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            ApplyRegion();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(Color.FromArgb(214, 225, 241)))
            using (GraphicsPath path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
                e.Graphics.DrawPath(pen, path);
        }

        private void BuildUi(string message, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(22, 0, 22, 18), BackColor = BackColor };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var title = new Label { Dock = DockStyle.Fill, Text = Text, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold), ForeColor = Color.FromArgb(22, 32, 51) };
            title.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) NativeDrag(); };
            root.Controls.Add(title, 0, 0);

            var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 14, 0, 18), BackColor = BackColor };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Label symbol = CreateIcon(icon);
            content.Controls.Add(symbol, 0, 0);

            var text = new Label { AutoSize = true, MaximumSize = new Size(410, 0), Text = message, ForeColor = Color.FromArgb(51, 65, 85), Font = new Font(Font.FontFamily, 9.5F), Padding = new Padding(0, 3, 0, 0), UseMnemonic = false };
            content.Controls.Add(text, 1, 0);
            root.Controls.Add(content, 0, 1);

            var buttonBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = BackColor, Padding = new Padding(0, 6, 0, 0) };
            root.Controls.Add(buttonBar, 0, 2);
            List<ButtonSpec> specs = GetButtons(buttons);
            var created = new List<Button>();
            foreach (ButtonSpec spec in specs)
            {
                Button button = CreateButton(spec.Text, spec.Result, spec.Primary);
                buttonBar.Controls.Add(button);
                created.Add(button);
            }

            int defaultIndex = _defaultButton == MessageBoxDefaultButton.Button2 ? 1 : (_defaultButton == MessageBoxDefaultButton.Button3 ? 2 : 0);
            if (defaultIndex >= created.Count) defaultIndex = 0;
            if (created.Count > 0) AcceptButton = created[defaultIndex];
            foreach (Button button in created)
                if (button.DialogResult == DialogResult.Cancel || button.DialogResult == DialogResult.No) { CancelButton = button; break; }

            int estimatedLines = Math.Max(1, (message.Length / 38) + message.Split(new[] { '\n' }).Length);
            Height = Math.Max(210, Math.Min(520, 178 + estimatedLines * 22));
        }

        private Label CreateIcon(MessageBoxIcon icon)
        {
            string symbol = "i";
            Color back = Color.FromArgb(226, 239, 255), fore = Color.FromArgb(37, 99, 235);
            if (icon == MessageBoxIcon.Warning || icon == MessageBoxIcon.Exclamation) { symbol = "!"; back = Color.FromArgb(255, 247, 219); fore = Color.FromArgb(180, 83, 9); }
            else if (icon == MessageBoxIcon.Error || icon == MessageBoxIcon.Hand || icon == MessageBoxIcon.Stop) { symbol = "×"; back = Color.FromArgb(254, 226, 226); fore = Color.FromArgb(185, 28, 28); }
            else if (icon == MessageBoxIcon.Question) { symbol = "?"; back = Color.FromArgb(237, 233, 254); fore = Color.FromArgb(109, 40, 217); }
            return new Label { Text = symbol, Width = 42, Height = 42, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 18F, FontStyle.Bold), BackColor = back, ForeColor = fore, Margin = new Padding(0, 0, 16, 0) };
        }

        private static Button CreateButton(string text, DialogResult result, bool primary)
        {
            var button = new Button { Text = text, DialogResult = result, AutoSize = true, MinimumSize = new Size(92, 38), Height = 38, Padding = new Padding(14, 0, 14, 0), Margin = new Padding(9, 0, 0, 0), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(207, 218, 234);
            button.BackColor = primary ? Color.FromArgb(50, 111, 234) : Color.White;
            button.ForeColor = primary ? Color.White : Color.FromArgb(51, 65, 85);
            return button;
        }

        private static List<ButtonSpec> GetButtons(MessageBoxButtons buttons)
        {
            if (buttons == MessageBoxButtons.OKCancel) return new List<ButtonSpec> { new ButtonSpec("确定", DialogResult.OK, true), new ButtonSpec("取消", DialogResult.Cancel, false) };
            if (buttons == MessageBoxButtons.YesNo) return new List<ButtonSpec> { new ButtonSpec("是", DialogResult.Yes, true), new ButtonSpec("否", DialogResult.No, false) };
            if (buttons == MessageBoxButtons.YesNoCancel) return new List<ButtonSpec> { new ButtonSpec("是", DialogResult.Yes, true), new ButtonSpec("否", DialogResult.No, false), new ButtonSpec("取消", DialogResult.Cancel, false) };
            if (buttons == MessageBoxButtons.RetryCancel) return new List<ButtonSpec> { new ButtonSpec("重试", DialogResult.Retry, true), new ButtonSpec("取消", DialogResult.Cancel, false) };
            if (buttons == MessageBoxButtons.AbortRetryIgnore) return new List<ButtonSpec> { new ButtonSpec("中止", DialogResult.Abort, false), new ButtonSpec("重试", DialogResult.Retry, true), new ButtonSpec("忽略", DialogResult.Ignore, false) };
            return new List<ButtonSpec> { new ButtonSpec("确定", DialogResult.OK, true) };
        }

        public static DialogResult Show(string text) { return Show(null, text, "CDBox", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(string text, string caption) { return Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) { return Show(null, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) { return Show(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton) { return Show(null, text, caption, buttons, icon, defaultButton); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) { return Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            using (var dialog = new CDBoxMessageBox(text, caption, buttons, icon, defaultButton))
                return owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        }

        private void ApplyRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using (GraphicsPath path = Rounded(new Rectangle(0, 0, Width, Height), Radius)) Region = new Region(path);
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90); path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90); path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90); path.CloseFigure();
            return path;
        }

        private void NativeDrag()
        {
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
        }

        private sealed class ButtonSpec
        {
            public ButtonSpec(string text, DialogResult result, bool primary) { Text = text; Result = result; Primary = primary; }
            public string Text; public DialogResult Result; public bool Primary;
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")] internal static extern bool ReleaseCapture();
            [System.Runtime.InteropServices.DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        }
    }
}
