using System;
using System.Drawing;
using System.Windows.Forms;

namespace TCPipeAutoDraw.UI
{
    /// <summary>
    /// CDBox 通用进度提示窗。
    /// 采用非模态窗口，供 AutoCAD 主线程中的长耗时同步任务使用。
    /// </summary>
    public sealed class CDBoxProgressForm : Form
    {
        private readonly IWin32Window _owner;
        private readonly Label _lblStatus;
        private readonly Label _lblPercent;
        private readonly ProgressBar _progressBar;
        private bool _shown;
        private DateTime _lastDoEventsUtc;

        public CDBoxProgressForm(string title, IWin32Window owner)
        {
            _owner = owner;
            Text = string.IsNullOrWhiteSpace(title) ? "处理中" : title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            ControlBox = false;
            ClientSize = new Size(420, 118);
            Padding = new Padding(14, 12, 14, 12);

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 2;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            _lblStatus = new Label();
            _lblStatus.AutoSize = false;
            _lblStatus.Dock = DockStyle.Fill;
            _lblStatus.Text = "正在处理，请稍候...";
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(_lblStatus, 0, 0);
            root.SetColumnSpan(_lblStatus, 2);

            _progressBar = new ProgressBar();
            _progressBar.Dock = DockStyle.Fill;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = 100;
            _progressBar.Value = 0;
            root.Controls.Add(_progressBar, 0, 1);
            root.SetColumnSpan(_progressBar, 2);

            _lblPercent = new Label();
            _lblPercent.AutoSize = false;
            _lblPercent.Dock = DockStyle.Fill;
            _lblPercent.Text = "0 / 0";
            _lblPercent.TextAlign = ContentAlignment.MiddleRight;
            root.Controls.Add(_lblPercent, 1, 2);

            _lastDoEventsUtc = DateTime.UtcNow;
        }

        public void Report(int current, int total, string message)
        {
            if (IsDisposed) return;

            try
            {
                if (total <= 0) total = 1;
                if (current < 0) current = 0;
                if (current > total) current = total;

                if (_progressBar.Style != ProgressBarStyle.Blocks) _progressBar.Style = ProgressBarStyle.Blocks;
                if (_progressBar.Value > total) _progressBar.Value = total;
                if (_progressBar.Maximum != total) _progressBar.Maximum = total;
                if (_progressBar.Value != current) _progressBar.Value = current;

                _lblStatus.Text = string.IsNullOrWhiteSpace(message) ? "正在处理，请稍候..." : message;

                int percent = (int)Math.Round(current * 100.0 / Math.Max(total, 1));
                if (percent < 0) percent = 0;
                if (percent > 100) percent = 100;
                _lblPercent.Text = current + " / " + total + "（" + percent + "%）";

                EnsureShown();
                Refresh();
                PumpUiIfNeeded();
            }
            catch
            {
                // 进度窗只做提示，不应影响实际业务执行。
            }
        }

        public void ReportMarquee(string message)
        {
            if (IsDisposed) return;

            try
            {
                _progressBar.Style = ProgressBarStyle.Marquee;
                _lblStatus.Text = string.IsNullOrWhiteSpace(message) ? "正在处理，请稍候..." : message;
                _lblPercent.Text = string.Empty;
                EnsureShown();
                Refresh();
                PumpUiIfNeeded();
            }
            catch
            {
            }
        }

        public void Complete(string message)
        {
            Report(1, 1, string.IsNullOrWhiteSpace(message) ? "处理完成。" : message);
        }

        private void EnsureShown()
        {
            if (_shown) return;
            _shown = true;
            if (_owner == null) Show();
            else Show(_owner);
        }

        private void PumpUiIfNeeded()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - _lastDoEventsUtc).TotalMilliseconds < 40) return;
            _lastDoEventsUtc = now;
            Application.DoEvents();
        }
    }
}
