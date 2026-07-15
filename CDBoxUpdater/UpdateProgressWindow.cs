using System;
using System.Drawing;
using System.Media;
using System.Threading;
using System.Windows.Forms;

namespace CDBoxUpdater
{
    internal sealed class UpdateProgressWindowHost
    {
        private readonly UpdateProgressForm _form;

        private UpdateProgressWindowHost(UpdateProgressForm form)
        {
            _form = form;
        }

        public static UpdateProgressWindowHost Start(string targetVersion)
        {
            var ready = new ManualResetEventSlim(false);
            UpdateProgressForm form = null;
            Exception startupError = null;
            var thread = new Thread(new ThreadStart(delegate
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    form = new UpdateProgressForm(targetVersion);
                    IntPtr unused = form.Handle;
                    ready.Set();
                    Application.Run(form);
                }
                catch (Exception ex)
                {
                    startupError = ex;
                    ready.Set();
                }
            }));
            thread.Name = "CDBoxUpdater.UI";
            thread.IsBackground = false;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait();
            if (startupError != null) throw new InvalidOperationException("无法打开更新进度窗口。", startupError);
            return new UpdateProgressWindowHost(form);
        }

        public void Report(int percent, string message)
        {
            if (_form != null) _form.SetProgress(percent, message, "更新期间请勿打开 AutoCAD。", false, false);
        }

        public void Complete(bool success, string message)
        {
            if (_form != null)
            {
                _form.SetProgress(success ? 100 : 0,
                    success ? "更新完毕" : "更新失败",
                    message,
                    true,
                    success);
            }
        }

        public void CloseForRelaunch()
        {
            if (_form != null) _form.CloseForRelaunch();
        }
    }

    internal sealed class UpdateProgressForm : Form
    {
        private readonly Label _titleLabel;
        private readonly Label _statusLabel;
        private readonly Label _noticeLabel;
        private readonly ProgressBar _progressBar;
        private readonly Button _closeButton;
        private bool _canClose;

        public UpdateProgressForm(string targetVersion)
        {
            Text = "CDBox 更新器";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            ShowInTaskbar = true;
            TopMost = true;
            ClientSize = new Size(460, 218);
            BackColor = Color.FromArgb(247, 249, 252);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = SystemIcons.Information;

            _titleLabel = new Label
            {
                AutoSize = false,
                Location = new Point(26, 22),
                Size = new Size(408, 30),
                Font = new Font(Font.FontFamily, 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 42, 70),
                Text = string.IsNullOrWhiteSpace(targetVersion) ? "正在更新 CDBox" : "正在更新 CDBox · " + targetVersion
            };

            _statusLabel = new Label
            {
                AutoSize = false,
                Location = new Point(27, 62),
                Size = new Size(406, 24),
                ForeColor = Color.FromArgb(50, 70, 100),
                Text = "正在准备更新…"
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(27, 94),
                Size = new Size(406, 18),
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 24
            };

            _noticeLabel = new Label
            {
                AutoSize = false,
                Location = new Point(27, 124),
                Size = new Size(406, 38),
                ForeColor = Color.FromArgb(176, 88, 16),
                Text = "更新期间请勿打开 AutoCAD。"
            };

            _closeButton = new Button
            {
                Location = new Point(343, 173),
                Size = new Size(90, 32),
                Text = "关闭",
                Visible = false,
                FlatStyle = FlatStyle.System
            };
            _closeButton.Click += delegate
            {
                _canClose = true;
                Close();
            };

            Controls.Add(_titleLabel);
            Controls.Add(_statusLabel);
            Controls.Add(_progressBar);
            Controls.Add(_noticeLabel);
            Controls.Add(_closeButton);
            FormClosing += OnFormClosing;
        }

        public void SetProgress(int percent, string status, string notice, bool completed, bool success)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<int, string, string, bool, bool>(SetProgress), percent, status, notice, completed, success); }
                catch { }
                return;
            }

            _statusLabel.Text = status ?? string.Empty;
            _noticeLabel.Text = notice ?? string.Empty;
            if (percent < 0)
            {
                _progressBar.Style = ProgressBarStyle.Marquee;
                _progressBar.MarqueeAnimationSpeed = 24;
            }
            else
            {
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Value = Math.Max(0, Math.Min(100, percent));
            }

            if (!completed) return;
            _canClose = true;
            _closeButton.Visible = true;
            _closeButton.Focus();
            _titleLabel.Text = success ? "CDBox 更新完毕" : "CDBox 更新失败";
            _noticeLabel.ForeColor = success ? Color.FromArgb(18, 126, 82) : Color.FromArgb(183, 45, 45);
            try { if (success) SystemSounds.Asterisk.Play(); else SystemSounds.Hand.Play(); } catch { }
            try { Show(); WindowState = FormWindowState.Normal; Activate(); BringToFront(); } catch { }
        }

        public void CloseForRelaunch()
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(CloseForRelaunch)); } catch { }
                return;
            }
            _canClose = true;
            Close();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_canClose) return;
            e.Cancel = true;
            SystemSounds.Exclamation.Play();
            MessageBox.Show(this, "更新尚未完成，请勿关闭更新器或打开 AutoCAD。", "CDBox 更新器", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
