using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TCPipeAutoDraw.UI
{
    internal sealed class CDBoxPromptDialog : Form
    {
        private CheckBox _chkDoNotAsk;
        private Button _btnYes;
        private Button _btnNo;

        public CDBoxPromptDialog(string title, string message, string yesText, string noText)
        {
            Text = string.IsNullOrWhiteSpace(title) ? "CDBox" : title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Width = 570;
            Height = 300;
            BackColor = Color.FromArgb(247, 249, 254);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            BuildUi(message ?? string.Empty, string.IsNullOrWhiteSpace(yesText) ? "是" : yesText, string.IsNullOrWhiteSpace(noText) ? "否" : noText);
        }

        public bool DoNotAskAgain
        {
            get { return _chkDoNotAsk != null && _chkDoNotAsk.Checked; }
        }

        private void BuildUi(string message, string yesText, string noText)
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(22, 0, 22, 20);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var title = new Label();
            title.Text = Text;
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(22, 32, 51);
            root.Controls.Add(title, 0, 0);

            var txtMessage = new TextBox();
            txtMessage.Dock = DockStyle.Fill;
            txtMessage.Multiline = true;
            txtMessage.ReadOnly = true;
            txtMessage.BorderStyle = BorderStyle.None;
            txtMessage.BackColor = BackColor;
            txtMessage.ForeColor = Color.FromArgb(51, 65, 85);
            txtMessage.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular);
            txtMessage.ScrollBars = ScrollBars.Vertical;
            txtMessage.WordWrap = true;
            txtMessage.TabStop = false;
            txtMessage.Text = message;
            root.Controls.Add(txtMessage, 0, 1);

            _chkDoNotAsk = new CheckBox();
            _chkDoNotAsk.Text = "以后不再提示";
            _chkDoNotAsk.AutoSize = true;
            _chkDoNotAsk.Margin = new Padding(0, 10, 0, 8);
            root.Controls.Add(_chkDoNotAsk, 0, 2);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.AutoSize = true;
            root.Controls.Add(buttons, 0, 3);

            _btnNo = new Button();
            _btnNo.Text = noText;
            _btnNo.Width = 96;
            _btnNo.Height = 30;
            _btnNo.DialogResult = DialogResult.No;
            StyleButton(_btnNo, false);
            buttons.Controls.Add(_btnNo);

            _btnYes = new Button();
            _btnYes.Text = yesText;
            _btnYes.Width = 96;
            _btnYes.Height = 30;
            _btnYes.DialogResult = DialogResult.Yes;
            StyleButton(_btnYes, true);
            buttons.Controls.Add(_btnYes);

            AcceptButton = _btnYes;
            CancelButton = _btnNo;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            using (GraphicsPath path = Rounded(ClientRectangle, 16)) Region = new Region(path);
        }

        private static void StyleButton(Button button, bool primary)
        {
            button.Height = 38;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(207, 218, 234);
            button.BackColor = primary ? Color.FromArgb(50, 111, 234) : Color.White;
            button.ForeColor = primary ? Color.White : Color.FromArgb(51, 65, 85);
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static DialogResult ShowYesNo(IWin32Window owner, string title, string message, string yesText, string noText, out bool doNotAskAgain)
        {
            using (var dialog = new CDBoxPromptDialog(title, message, yesText, noText))
            {
                DialogResult result = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
                doNotAskAgain = dialog.DoNotAskAgain;
                return result;
            }
        }
    }
}
