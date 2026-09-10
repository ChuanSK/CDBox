using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TCPipeAutoDraw.Core.Modules;

namespace TCPipeAutoDraw.UI
{
    /// <summary>
    /// 统一插件界面。
    /// 当前版本改为由模块注册器驱动：主界面不再硬编码每个功能按钮。
    /// </summary>
    public sealed class MainToolboxForm : Form
    {
        private readonly IList<ITCModule> _modules;
        private Label _lblStatus;

        public MainToolboxForm(IEnumerable<ITCModule> modules)
        {
            if (modules == null) throw new ArgumentNullException("modules");

            _modules = new List<ITCModule>(modules);

            Text = "CDBOX 插件合集（制作：氚）";
            Width = 590;
            Height = 630;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;

            BuildUi();
            TCPipeAutoDraw.UI.Studio.CDBoxAccentAppearance.ApplyNative(this);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.RowCount = 4;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var title = new Label();
            title.Text = "氚的工具箱";
            title.Font = new Font(Font.FontFamily, 13, FontStyle.Bold);
            title.AutoSize = true;
            title.Padding = new Padding(0, 0, 0, 4);
            root.Controls.Add(title, 0, 0);

            var panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.FlowDirection = FlowDirection.TopDown;
            panel.WrapContents = false;
            panel.AutoScroll = true;
            root.Controls.Add(panel, 0, 1);

            foreach (ITCModule module in _modules)
            {
                panel.Controls.Add(MakeModuleButton(module));
            }

            _lblStatus = new Label();
            _lblStatus.Text = "当前界面由模块注册器自动生成。";
            _lblStatus.AutoSize = false;
            _lblStatus.Dock = DockStyle.Fill;
            _lblStatus.Height = 42;
            _lblStatus.Padding = new Padding(0, 8, 0, 8);
            root.Controls.Add(_lblStatus, 0, 2);

            var close = new Button();
            close.Text = "关闭";
            close.AutoSize = true;
            close.Anchor = AnchorStyles.Right;
            close.Click += delegate { Close(); };
            root.Controls.Add(close, 0, 3);
        }

        private Control MakeModuleButton(ITCModule module)
        {
            var container = new Panel();
            container.Width = 460;
            container.Height = 58;
            container.Margin = new Padding(0, 4, 0, 6);

            var button = new Button();
            button.Text = module.Name;
            button.Width = 150;
            button.Height = 42;
            button.Left = 0;
            button.Top = 6;
            button.Enabled = module.Enabled;
            button.Tag = module;
            button.Click += delegate { RunModule(module); };
            container.Controls.Add(button);

            var desc = new Label();
            desc.Left = 162;
            desc.Top = 4;
            desc.Width = 288;
            desc.Height = 42;
            desc.Text = BuildDescription(module);
            desc.ForeColor = module.Enabled ? SystemColors.ControlText : SystemColors.GrayText;
            container.Controls.Add(desc);

            return container;
        }

        private static string BuildDescription(ITCModule module)
        {
            string commandText = string.IsNullOrWhiteSpace(module.CommandName) ? "" : "命令：" + module.CommandName + "\r\n";
            return commandText + module.Description;
        }

        private void RunModule(ITCModule module)
        {
            if (module == null || !module.Enabled) return;

            bool wasVisible = Visible;
            try
            {
                if (wasVisible) Hide();
                module.Run();
                if (_lblStatus != null) _lblStatus.Text = "已运行模块：" + module.Name;
            }
            catch (System.Exception ex)
            {
                if (_lblStatus != null) _lblStatus.Text = module.Name + " 运行失败：" + ex.Message;
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, module.Name + "运行失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (wasVisible && !IsDisposed)
                {
                    Show();
                    Activate();
                }
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // MainToolboxForm
            // 
            this.ClientSize = new System.Drawing.Size(479, 544);
            this.Name = "MainToolboxForm";
            this.ResumeLayout(false);

        }
    }
}
