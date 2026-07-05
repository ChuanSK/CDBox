using System;
using System.Drawing;
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
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Width = 560;
            Height = 270;

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
            root.Padding = new Padding(16, 14, 16, 14);
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var txtMessage = new TextBox();
            txtMessage.Dock = DockStyle.Fill;
            txtMessage.Multiline = true;
            txtMessage.ReadOnly = true;
            txtMessage.BorderStyle = BorderStyle.None;
            txtMessage.BackColor = SystemColors.Control;
            txtMessage.ForeColor = SystemColors.ControlText;
            txtMessage.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular);
            txtMessage.ScrollBars = ScrollBars.Vertical;
            txtMessage.WordWrap = true;
            txtMessage.TabStop = false;
            txtMessage.Text = message;
            root.Controls.Add(txtMessage, 0, 0);

            _chkDoNotAsk = new CheckBox();
            _chkDoNotAsk.Text = "以后不再提示";
            _chkDoNotAsk.AutoSize = true;
            _chkDoNotAsk.Margin = new Padding(0, 10, 0, 8);
            root.Controls.Add(_chkDoNotAsk, 0, 1);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.AutoSize = true;
            root.Controls.Add(buttons, 0, 2);

            _btnNo = new Button();
            _btnNo.Text = noText;
            _btnNo.Width = 96;
            _btnNo.Height = 30;
            _btnNo.DialogResult = DialogResult.No;
            buttons.Controls.Add(_btnNo);

            _btnYes = new Button();
            _btnYes.Text = yesText;
            _btnYes.Width = 96;
            _btnYes.Height = 30;
            _btnYes.DialogResult = DialogResult.Yes;
            buttons.Controls.Add(_btnYes);

            AcceptButton = _btnYes;
            CancelButton = _btnNo;
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
