using System;
using System.Drawing;
using System.Windows.Forms;
using TCPipeAutoDraw.Core.Startup;

namespace TCPipeAutoDraw.UI
{
    internal sealed class CDBoxSettingsForm : Form
    {
        private CheckBox _chkPromptInstall;
        private CheckBox _chkPromptSidebar;
        private CheckBox _chkAutoShowSidebar;
        private Label _lblInstallInfo;

        public CDBoxSettingsForm()
        {
            Text = "设置";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Width = 520;
            Height = 500;

            BuildUi();
            LoadSettingsToUi();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(14);
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var title = new Label();
            title.Text = "启动与安装设置";
            title.Font = new Font(Font.FontFamily, 11.5f, FontStyle.Bold);
            title.AutoSize = true;
            title.Padding = new Padding(0, 0, 0, 8);
            root.Controls.Add(title, 0, 0);

            var checkPanel = new FlowLayoutPanel();
            checkPanel.Dock = DockStyle.Fill;
            checkPanel.FlowDirection = FlowDirection.TopDown;
            checkPanel.WrapContents = false;
            checkPanel.AutoSize = true;
            root.Controls.Add(checkPanel, 0, 1);

            _chkPromptInstall = new CheckBox();
            _chkPromptInstall.Text = "插件加载后，提示安装到 CAD 所在目录";
            _chkPromptInstall.AutoSize = true;
            _chkPromptInstall.Margin = new Padding(0, 2, 0, 6);
            checkPanel.Controls.Add(_chkPromptInstall);

            _chkPromptSidebar = new CheckBox();
            _chkPromptSidebar.Text = "插件加载后，提示显示 CDBox 侧边栏";
            _chkPromptSidebar.AutoSize = true;
            _chkPromptSidebar.Margin = new Padding(0, 2, 0, 6);
            checkPanel.Controls.Add(_chkPromptSidebar);

            _chkAutoShowSidebar = new CheckBox();
            _chkAutoShowSidebar.Text = "插件加载后自动显示 CDBox 侧边栏（不弹提示时生效）";
            _chkAutoShowSidebar.AutoSize = true;
            _chkAutoShowSidebar.Margin = new Padding(0, 2, 0, 6);
            checkPanel.Controls.Add(_chkAutoShowSidebar);

            var group = new GroupBox();
            group.Text = "安装状态";
            group.Dock = DockStyle.Fill;
            group.Padding = new Padding(10);
            root.Controls.Add(group, 0, 2);

            _lblInstallInfo = new Label();
            _lblInstallInfo.Dock = DockStyle.Fill;
            _lblInstallInfo.AutoSize = false;
            _lblInstallInfo.TextAlign = ContentAlignment.TopLeft;
            group.Controls.Add(_lblInstallInfo);

            var installButtons = new FlowLayoutPanel();
            installButtons.FlowDirection = FlowDirection.LeftToRight;
            installButtons.AutoSize = true;
            installButtons.Dock = DockStyle.Fill;
            root.Controls.Add(installButtons, 0, 3);

            var btnInstall = new Button();
            btnInstall.Text = "安装/修复自动加载";
            btnInstall.Width = 140;
            btnInstall.Height = 30;
            btnInstall.Click += Install;
            installButtons.Controls.Add(btnInstall);

            var btnUninstall = new Button();
            btnUninstall.Text = "卸载自动加载";
            btnUninstall.Width = 120;
            btnUninstall.Height = 30;
            btnUninstall.Click += Uninstall;
            installButtons.Controls.Add(btnUninstall);

            var btnUpdate = new Button();
            btnUpdate.Text = "更新插件";
            btnUpdate.Width = 100;
            btnUpdate.Height = 30;
            btnUpdate.Click += UpdatePlugin;
            installButtons.Controls.Add(btnUpdate);

            var bottom = new FlowLayoutPanel();
            bottom.FlowDirection = FlowDirection.RightToLeft;
            bottom.AutoSize = true;
            bottom.Dock = DockStyle.Fill;
            root.Controls.Add(bottom, 0, 4);

            var btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Width = 88;
            btnCancel.Height = 30;
            btnCancel.DialogResult = DialogResult.Cancel;
            bottom.Controls.Add(btnCancel);

            var btnSave = new Button();
            btnSave.Text = "保存";
            btnSave.Width = 88;
            btnSave.Height = 30;
            btnSave.Click += SaveAndClose;
            bottom.Controls.Add(btnSave);

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private void LoadSettingsToUi()
        {
            CDBoxAppSettings settings = CDBoxAppSettingsStore.Load();
            _chkPromptInstall.Checked = settings.PromptInstallOnLoad;
            _chkPromptSidebar.Checked = settings.PromptSidebarOnLoad;
            _chkAutoShowSidebar.Checked = settings.AutoShowSidebarOnLoad;
            RefreshInstallInfo();
        }

        private void SaveAndClose(object sender, EventArgs e)
        {
            SaveSettingsFromUi();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void SaveSettingsFromUi()
        {
            CDBoxAppSettings settings = CDBoxAppSettingsStore.Load();
            settings.PromptInstallOnLoad = _chkPromptInstall.Checked;
            settings.PromptSidebarOnLoad = _chkPromptSidebar.Checked;
            settings.AutoShowSidebarOnLoad = _chkAutoShowSidebar.Checked;
            CDBoxAppSettingsStore.Save(settings);
        }

        private void Install(object sender, EventArgs e)
        {
            SaveSettingsFromUi();
            CDBoxInstallResult result = CDBoxInstaller.InstallToCadDirectory();
            CDBoxAppSettings settings = CDBoxAppSettingsStore.Load();
            if (result.Success) settings.InstalledPath = result.InstallRoot;
            CDBoxAppSettingsStore.Save(settings);
            RefreshInstallInfo();
            MessageBox.Show(new AcadMainWindow(), result.Message + "\r\n\r\n安装目录：" + result.InstallRoot, result.Success ? "CDBox 安装完成" : "CDBox 安装失败", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }


        private void UpdatePlugin(object sender, EventArgs e)
        {
            SaveSettingsFromUi();

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择完整构建输出目录中的新版 CDBox.dll";
                dialog.Filter = "CDBox.dll|CDBox.dll|DLL 文件 (*.dll)|*.dll|所有文件 (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;

                if (dialog.ShowDialog(new AcadMainWindow()) != DialogResult.OK) return;

                CDBoxInstallResult result = CDBoxInstaller.ScheduleUpdateFromDll(dialog.FileName);
                CDBoxAppSettings settings = CDBoxAppSettingsStore.Load();
                if (result.Success) settings.InstalledPath = result.InstallRoot;
                CDBoxAppSettingsStore.Save(settings);
                RefreshInstallInfo();

                MessageBox.Show(new AcadMainWindow(), result.Message + "\r\n\r\n安装目录：" + result.InstallRoot, result.Success ? "CDBox 更新" : "CDBox 更新失败", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
        }

        private void Uninstall(object sender, EventArgs e)
        {
            DialogResult confirm = MessageBox.Show(new AcadMainWindow(), "确定卸载 CDBox 自动加载并删除安装目录吗？\r\n\r\n当前已加载的插件本次 CAD 会话仍可继续使用，重启 CAD 后不再自动加载。", "卸载 CDBox", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            CDBoxInstallResult result = CDBoxInstaller.Uninstall();
            CDBoxAppSettings settings = CDBoxAppSettingsStore.Load();
            settings.InstalledPath = string.Empty;
            CDBoxAppSettingsStore.Save(settings);
            RefreshInstallInfo();
            MessageBox.Show(new AcadMainWindow(), result.Message + "\r\n\r\n安装目录：" + result.InstallRoot, result.Success ? "CDBox 卸载" : "CDBox 卸载提示", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void RefreshInstallInfo()
        {
            string registered = CDBoxInstaller.GetRegisteredLoaderPath();
            string installRoot = CDBoxInstaller.GetInstallRoot();
            string status = CDBoxInstaller.IsInstalled() ? "已安装" : "未安装";
            string running = CDBoxInstaller.IsRunningFromInstallFolder() ? "是" : "否";

            _lblInstallInfo.Text =
                "状态：" + status + "\r\n" +
                "CAD 目录：" + CDBoxInstaller.GetCadDirectory() + "\r\n" +
                "安装目录：" + installRoot + "\r\n" +
                "注册加载：" + (string.IsNullOrWhiteSpace(registered) ? "未注册" : registered) + "\r\n" +
                "当前是否从安装目录运行：" + running + "\r\n\r\n" +
                "更新方式：点击“更新插件”选择新版 CDBox.dll，关闭 CAD 后自动替换，下次启动生效。" + "\r\n" +
                "设置文件：" + CDBoxAppSettingsStore.GetSettingsPath();
        }
    }
}
