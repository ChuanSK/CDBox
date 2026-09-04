using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CDBox.Shared.Components;

namespace CDBox.Setup
{
    internal sealed class InstallerForm : Form
    {
        private readonly IReadOnlyList<CadInstallation> _installations;
        private readonly CDBoxComponentManifest _componentManifest;
        private readonly InstallerLaunchOptions _launchOptions;
        private readonly List<CadTargetView> _targetViews =
            new List<CadTargetView>();
        private readonly Dictionary<string, ComponentTreeItemControl>
            _componentViews = new Dictionary<string,
                ComponentTreeItemControl>(StringComparer.OrdinalIgnoreCase);
        private readonly ToolTip _toolTip = new ToolTip();
        private readonly Timer _environmentTimer;

        private FlowLayoutPanel _componentList;
        private Label _detailTitle;
        private Label _detailDescription;
        private Label _detailCompatibility;
        private FlowLayoutPanel _detailGroups;
        private Label _environmentWarning;
        private Label _operationStatus;
        private Label _pendingChanges;
        private InstallerButton _logToggleButton;
        private InstallerButton _manageButton;
        private InstallerButton _cancelButton;
        private InstallerButton _primaryButton;
        private ProgressBar _progressBar;
        private RoundedPanel _logCard;
        private TextBox _logBox;
        private ContextMenuStrip _managementMenu;
        private InstallerPlan _currentPlan;
        private ComponentTreeItemControl _selectedComponent;
        private PrerequisiteStatus _prerequisites =
            new PrerequisiteStatus();
        private bool _repairRequested;
        private bool _busy;
        private bool _suppressEvents;
        private bool _logExpanded;

        public InstallerForm(IReadOnlyList<CadInstallation> installations)
            : this(installations, new InstallerLaunchOptions())
        {
        }

        public InstallerForm(IReadOnlyList<CadInstallation> installations,
            InstallerLaunchOptions launchOptions)
        {
            _installations = installations
                ?? throw new ArgumentNullException(nameof(installations));
            _launchOptions = launchOptions ?? new InstallerLaunchOptions();
            _componentManifest =
                InstallerPayload.ReadAvailableComponentManifest();

            Text = "CDBox 组件管理 · "
                + _componentManifest.ProductVersion;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 700);
            ClientSize = new Size(1000, 780);
            BackColor = InstallerTheme.Window;
            Font = new Font("Microsoft YaHei UI", 9F,
                FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildLayout();
            InitializeSelections();

            _environmentTimer = new Timer { Interval = 2000 };
            _environmentTimer.Tick += delegate { RefreshPlan(); };
            Shown += delegate
            {
                DetectEnvironment(true);
                RefreshPlan();
                _environmentTimer.Start();
            };
            FormClosed += delegate
            {
                _environmentTimer.Stop();
                _environmentTimer.Dispose();
                _toolTip.Dispose();
            };
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 14, 20, 16),
                ColumnCount = 1,
                RowCount = 6
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 194));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            Controls.Add(root);

            root.Controls.Add(CreateHeader(), 0, 0);
            root.Controls.Add(CreateTargetCard(), 0, 1);
            root.Controls.Add(CreateComponentWorkspace(), 0, 2);
            root.Controls.Add(CreateStatusCard(), 0, 3);
            _logCard = CreateLogCard();
            root.Controls.Add(_logCard, 0, 4);
            root.Controls.Add(CreateFooter(), 0, 5);
            _logCard.Tag = root;
        }

        private Control CreateHeader()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            var title = new Label
            {
                Text = "组件管理",
                AutoSize = true,
                Location = new Point(0, 5),
                ForeColor = InstallerTheme.Text,
                Font = new Font("Microsoft YaHei UI", 18F,
                    FontStyle.Bold, GraphicsUnit.Point)
            };
            var subtitle = new Label
            {
                Text = "安装、更新、增减业务模块与维护 CDBox",
                AutoSize = true,
                Location = new Point(2, 42),
                ForeColor = InstallerTheme.Secondary
            };
            var version = new Label
            {
                Text = "当前安装包  " + _componentManifest.ProductVersion,
                AutoSize = false,
                Size = new Size(250, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = InstallerTheme.Secondary,
                Location = new Point(Math.Max(0, ClientSize.Width - 294), 8)
            };
            _environmentWarning = new Label
            {
                AutoSize = false,
                Size = new Size(430, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = InstallerTheme.Warning,
                Location = new Point(Math.Max(0, ClientSize.Width - 474), 36),
                Visible = false
            };
            panel.Controls.Add(title);
            panel.Controls.Add(subtitle);
            panel.Controls.Add(version);
            panel.Controls.Add(_environmentWarning);
            panel.Resize += delegate
            {
                version.Left = Math.Max(0, panel.Width - version.Width);
                _environmentWarning.Left = Math.Max(0,
                    panel.Width - _environmentWarning.Width);
            };
            return panel;
        }

        private Control CreateTargetCard()
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(14, 8, 14, 8)
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var heading = new Panel { Dock = DockStyle.Fill };
            heading.Controls.Add(new Label
            {
                Text = "安装目标",
                AutoSize = true,
                Location = new Point(0, 4),
                ForeColor = InstallerTheme.Text,
                Font = new Font(Font.FontFamily, 10F, FontStyle.Bold)
            });
            var refresh = new LinkLabel
            {
                Text = "重新检测",
                AutoSize = true,
                LinkColor = InstallerTheme.Accent,
                ActiveLinkColor = InstallerTheme.Accent,
                VisitedLinkColor = InstallerTheme.Accent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(850, 5)
            };
            refresh.Click += delegate
            {
                DetectEnvironment(true);
                RefreshPlan();
            };
            heading.Controls.Add(refresh);
            heading.Resize += delegate
            {
                refresh.Left = Math.Max(0, heading.Width - refresh.Width);
            };
            layout.Controls.Add(heading, 0, 0);
            layout.Controls.Add(CreateTargetHeader(), 0, 1);

            var targetList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = InstallerTheme.Card,
                Margin = new Padding(0)
            };
            foreach (CadInstallation installation in _installations)
            {
                CadTargetView view = CreateTargetRow(installation);
                _targetViews.Add(view);
                targetList.Controls.Add(view.Row);
            }
            targetList.Resize += delegate
            {
                foreach (CadTargetView view in _targetViews)
                    view.Row.Width = Math.Max(200,
                        targetList.ClientSize.Width - 3);
            };
            layout.Controls.Add(targetList, 0, 2);
            card.Controls.Add(layout);
            return card;
        }

        private Control CreateTargetHeader()
        {
            var header = CreateTargetTable();
            header.BackColor = Color.FromArgb(249, 250, 252);
            header.Controls.Add(HeaderLabel(string.Empty), 0, 0);
            header.Controls.Add(HeaderLabel("AutoCAD 版本"), 1, 0);
            header.Controls.Add(HeaderLabel("运行状态"), 2, 0);
            header.Controls.Add(HeaderLabel("CDBox 版本"), 3, 0);
            header.Controls.Add(HeaderLabel(string.Empty), 4, 0);
            return header;
        }

        private CadTargetView CreateTargetRow(CadInstallation installation)
        {
            var row = CreateTargetTable();
            row.Dock = DockStyle.None;
            row.Width = 900;
            row.Height = 34;
            row.Margin = new Padding(0);
            var checkBox = new ModernCheckBox
            {
                Checked = false,
                Enabled = installation.IsDetected,
                Margin = new Padding(7, 7, 0, 0),
                Tag = installation
            };
            var version = new Label
            {
                Text = installation.Version.DisplayName,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = installation.IsDetected
                    ? InstallerTheme.Text : InstallerTheme.Weak
            };
            var status = BodyLabel(string.Empty);
            var installedVersion = BodyLabel("—");
            var more = new Button
            {
                Text = "···",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = InstallerTheme.Card,
                ForeColor = InstallerTheme.Secondary,
                Cursor = Cursors.Hand,
                TabStop = false,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            more.FlatAppearance.BorderSize = 0;
            var view = new CadTargetView
            {
                Installation = installation,
                Row = row,
                CheckBox = checkBox,
                StatusLabel = status,
                InstalledVersionLabel = installedVersion,
                MoreButton = more
            };
            checkBox.CheckedChanged += delegate
            {
                if (!_suppressEvents) RefreshPlan();
            };
            more.Click += delegate { ShowTargetMenu(view); };
            string pathTip = installation.IsDetected
                ? installation.InstallDirectory
                : "未检测到安装目录";
            _toolTip.SetToolTip(row, pathTip);
            _toolTip.SetToolTip(version, pathTip);
            row.Controls.Add(checkBox, 0, 0);
            row.Controls.Add(version, 1, 0);
            row.Controls.Add(status, 2, 0);
            row.Controls.Add(installedVersion, 3, 0);
            row.Controls.Add(more, 4, 0);
            return view;
        }

        private static TableLayoutPanel CreateTargetTable()
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 28,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            return table;
        }

        private static Label HeaderLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = InstallerTheme.Secondary,
                Font = new Font("Microsoft YaHei UI", 8.5F,
                    FontStyle.Regular, GraphicsUnit.Point)
            };
        }

        private static Label BodyLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = InstallerTheme.Secondary
            };
        }

        private Control CreateComponentWorkspace()
        {
            var split = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
            split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var treeCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 6, 8),
                Padding = new Padding(8)
            };
            _componentList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = InstallerTheme.Card
            };
            foreach (CDBoxComponentDefinition component in
                _componentManifest.Components)
            {
                var item = new ComponentTreeItemControl(component,
                    InstallerPresentationCatalog.Find(component.Id),
                    SelectComponent, ComponentSelectionChanged);
                _componentViews[component.Id] = item;
                _componentList.Controls.Add(item);
            }
            _componentList.Resize += delegate
            {
                foreach (ComponentTreeItemControl item in
                    _componentViews.Values)
                    item.Width = Math.Max(220,
                        _componentList.ClientSize.Width - 3);
            };
            treeCard.Controls.Add(_componentList);

            var detailCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 0, 8),
                Padding = new Padding(18, 14, 18, 14)
            };
            var detailLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _detailTitle = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = InstallerTheme.Text,
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _detailDescription = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = InstallerTheme.Secondary,
                AutoEllipsis = true
            };
            _detailCompatibility = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = InstallerTheme.Success,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _detailGroups = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = InstallerTheme.Card
            };
            detailLayout.Controls.Add(_detailTitle, 0, 0);
            detailLayout.Controls.Add(_detailDescription, 0, 1);
            detailLayout.Controls.Add(_detailCompatibility, 0, 2);
            detailLayout.Controls.Add(_detailGroups, 0, 3);
            detailCard.Controls.Add(detailLayout);

            split.Controls.Add(treeCard, 0, 0);
            split.Controls.Add(detailCard, 1, 0);
            return split;
        }

        private Control CreateStatusCard()
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(14, 10, 14, 10)
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            var left = new Panel { Dock = DockStyle.Fill };
            left.Controls.Add(new Label
            {
                Text = "当前状态",
                AutoSize = true,
                Location = new Point(0, 0),
                ForeColor = InstallerTheme.Secondary
            });
            _operationStatus = new Label
            {
                AutoSize = false,
                Size = new Size(430, 44),
                Location = new Point(0, 24),
                ForeColor = InstallerTheme.Text,
                AutoEllipsis = true
            };
            left.Controls.Add(_operationStatus);
            left.Resize += delegate
            {
                _operationStatus.Width = Math.Max(0,
                    left.ClientSize.Width);
            };
            var right = new Panel { Dock = DockStyle.Fill };
            right.Controls.Add(new Label
            {
                Text = "待应用更改",
                AutoSize = true,
                Location = new Point(0, 0),
                ForeColor = InstallerTheme.Secondary
            });
            _pendingChanges = new Label
            {
                AutoSize = false,
                Size = new Size(350, 44),
                Location = new Point(0, 24),
                ForeColor = InstallerTheme.Text,
                AutoEllipsis = true
            };
            right.Controls.Add(_pendingChanges);
            right.Resize += delegate
            {
                _pendingChanges.Width = Math.Max(0,
                    right.ClientSize.Width);
            };
            _logToggleButton = new InstallerButton
            {
                Text = "查看日志",
                Size = new Size(76, 28),
                Anchor = AnchorStyles.None,
                Margin = new Padding(4, 0, 0, 0)
            };
            _logToggleButton.Click += delegate { ToggleLog(); };
            layout.Controls.Add(left, 0, 0);
            layout.Controls.Add(right, 1, 0);
            layout.Controls.Add(_logToggleButton, 2, 0);
            card.Controls.Add(layout);
            return card;
        }

        private RoundedPanel CreateLogCard()
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(10),
                Visible = false
            };
            _logBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = InstallerTheme.Card,
                ForeColor = InstallerTheme.Secondary,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5F, FontStyle.Regular)
            };
            card.Controls.Add(_logBox);
            return card;
        }

        private Control CreateFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Continuous,
                Margin = new Padding(0, 17, 14, 16)
            };
            _manageButton = new InstallerButton
            {
                Text = "更多操作  ···",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 6, 8)
            };
            BuildManagementMenu();
            _manageButton.Click += delegate
            {
                _managementMenu.Show(_manageButton,
                    new Point(0, -_managementMenu.PreferredSize.Height));
            };
            _cancelButton = new InstallerButton
            {
                Text = "关闭",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 6, 8)
            };
            _cancelButton.Click += delegate { Close(); };
            _primaryButton = new InstallerButton
            {
                Text = "请选择安装目标",
                Primary = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 8)
            };
            _primaryButton.Click += async delegate
            {
                await ApplyCurrentPlanAsync();
            };
            footer.Controls.Add(_progressBar, 0, 0);
            footer.Controls.Add(_manageButton, 1, 0);
            footer.Controls.Add(_cancelButton, 2, 0);
            footer.Controls.Add(_primaryButton, 3, 0);
            return footer;
        }

        private void BuildManagementMenu()
        {
            _managementMenu = new ContextMenuStrip
            {
                Font = Font,
                ShowImageMargin = false
            };
            var repair = new ToolStripMenuItem("修复所选安装")
            {
                Name = "repair"
            };
            repair.Click += delegate
            {
                _repairRequested = true;
                RefreshPlan();
            };
            var open = new ToolStripMenuItem("查看安装目录")
            {
                Name = "open"
            };
            open.Click += delegate { OpenSelectedInstallDirectory(); };
            var uninstall = new ToolStripMenuItem("卸载所选 CDBox")
            {
                Name = "uninstall",
                ForeColor = InstallerTheme.Danger
            };
            uninstall.Click += async delegate
            {
                await UninstallSelectedAsync();
            };
            _managementMenu.Items.Add(repair);
            _managementMenu.Items.Add(open);
            _managementMenu.Items.Add(new ToolStripSeparator());
            _managementMenu.Items.Add(uninstall);
        }

        private void InitializeSelections()
        {
            _suppressEvents = true;
            try
            {
                bool explicitTarget = !string.IsNullOrWhiteSpace(
                    _launchOptions.TargetCadExecutablePath);
                foreach (CadTargetView target in _targetViews)
                    target.CheckBox.Checked = InstallerTargetSelection
                        .ShouldSelectByDefault(target.Installation,
                            explicitTarget
                                ? _launchOptions.TargetCadExecutablePath
                                : string.Empty,
                            BundleInstallService.IsInstalled(
                                target.Installation));

                string[] selection;
                if (_launchOptions.SelectedComponentIds != null
                    && _launchOptions.SelectedComponentIds.Length > 0)
                {
                    selection = CDBoxComponentBundle.NormalizeSelection(
                        _componentManifest,
                        _launchOptions.SelectedComponentIds);
                }
                else
                {
                    selection = InstallerPlanEvaluator
                        .DefaultComponentSelection(_componentManifest);
                }
                var selected = new HashSet<string>(selection,
                    StringComparer.OrdinalIgnoreCase);
                foreach (ComponentTreeItemControl item in
                    _componentViews.Values)
                    item.CheckBox.Checked = item.Component.Required
                        || selected.Contains(item.Component.Id);
            }
            finally { _suppressEvents = false; }

            ComponentTreeItemControl first = _componentViews.Values
                .FirstOrDefault();
            if (first != null) SelectComponent(first);
        }

        private void ComponentSelectionChanged()
        {
            if (_suppressEvents) return;
            _repairRequested = false;
            RefreshPlan();
        }

        private void SelectComponent(ComponentTreeItemControl selected)
        {
            _selectedComponent = selected;
            foreach (ComponentTreeItemControl item in
                _componentViews.Values)
                item.SetSelected(ReferenceEquals(item, selected));
            RenderComponentDetails();
        }

        private void RenderComponentDetails()
        {
            if (_selectedComponent == null) return;
            CDBoxComponentDefinition component =
                _selectedComponent.Component;
            InstallerComponentPresentation presentation =
                InstallerPresentationCatalog.Find(component.Id);
            _detailTitle.Text = component.DisplayName;
            _detailDescription.Text = presentation.Description;
            _detailGroups.Controls.Clear();
            foreach (InstallerFeatureGroup group in presentation.Groups)
            {
                var groupPanel = new Panel
                {
                    Height = 66,
                    Width = Math.Max(250,
                        _detailGroups.ClientSize.Width - 4),
                    Margin = new Padding(0, 0, 0, 6),
                    BackColor = Color.FromArgb(249, 250, 252)
                };
                groupPanel.Controls.Add(new Label
                {
                    Text = group.Name,
                    AutoSize = true,
                    Location = new Point(10, 7),
                    ForeColor = InstallerTheme.Text,
                    Font = new Font(Font.FontFamily, 9F,
                        FontStyle.Bold)
                });
                groupPanel.Controls.Add(new Label
                {
                    Text = string.Join("  ·  ", group.Features),
                    AutoSize = false,
                    AutoEllipsis = false,
                    Location = new Point(10, 29),
                    Size = new Size(Math.Max(160,
                        groupPanel.Width - 20), 32),
                    ForeColor = InstallerTheme.Secondary
                });
                _detailGroups.Controls.Add(groupPanel);
            }
            _detailGroups.Resize -= DetailGroupsOnResize;
            _detailGroups.Resize += DetailGroupsOnResize;
            UpdateDetailCompatibility();
        }

        private void DetailGroupsOnResize(object sender, EventArgs e)
        {
            foreach (Control group in _detailGroups.Controls)
            {
                group.Width = Math.Max(250,
                    _detailGroups.ClientSize.Width - 4);
                if (group.Controls.Count > 1)
                    group.Controls[1].Width = Math.Max(160,
                        group.Width - 20);
            }
        }

        private void UpdateDetailCompatibility()
        {
            if (_selectedComponent == null || _currentPlan == null) return;
            string id = _selectedComponent.Component.Id;
            string[] incompatible;
            if (_currentPlan.IncompatibleTargets.TryGetValue(id,
                    out incompatible))
            {
                _detailCompatibility.Text = "不兼容："
                    + string.Join("、", incompatible);
                _detailCompatibility.ForeColor = InstallerTheme.Danger;
            }
            else
            {
                _detailCompatibility.Text = "与当前所选 AutoCAD 兼容";
                _detailCompatibility.ForeColor = InstallerTheme.Success;
            }
        }

        private void DetectEnvironment(bool writeLog)
        {
            _prerequisites = PrerequisiteService.Detect();
            var missing = new List<string>();
            if (!_prerequisites.DotNetFramework48Installed)
                missing.Add(".NET Framework 4.8");
            if (!_prerequisites.WebView2Installed)
                missing.Add("WebView2 Runtime");
            _environmentWarning.Visible = missing.Count > 0;
            _environmentWarning.Text = missing.Count == 0
                ? string.Empty
                : "安装时将自动补齐：" + string.Join("、", missing);
            if (!writeLog) return;
            AppendLog(missing.Count == 0
                ? "运行环境检查通过。"
                : "缺少运行组件：" + string.Join("、", missing)
                    + "；执行安装时将自动处理。");
        }

        private void RefreshPlan()
        {
            if (_busy) return;
            InstallerTargetSnapshot[] snapshots = CaptureSnapshots();
            _currentPlan = InstallerPlanEvaluator.Evaluate(
                _componentManifest, snapshots,
                SelectedComponentIds(), _repairRequested);
            string[] runningCadPaths = BundleInstallService
                .GetRunningAutoCadExecutablePaths();
            bool selectedCadRunning = false;

            foreach (CadTargetView view in _targetViews)
            {
                InstallerTargetSnapshot snapshot = snapshots.First(x =>
                    ReferenceEquals(x.Installation, view.Installation));
                bool installed = snapshot.Installed;
                string installedVersion = installed
                    ? BundleInstallService.ReadInstalledProductVersion(
                        view.Installation) : string.Empty;
                bool cadRunning = BundleInstallService.IsAutoCadRunning(
                    view.Installation, runningCadPaths);
                if (view.CheckBox.Checked && cadRunning)
                    selectedCadRunning = true;
                view.InstalledVersionLabel.Text = installed
                    ? (string.IsNullOrWhiteSpace(installedVersion)
                        ? "已安装" : installedVersion) : "—";
                if (!view.Installation.IsDetected)
                {
                    view.StatusLabel.Text = "未检测到";
                    view.StatusLabel.ForeColor = InstallerTheme.Weak;
                }
                else if (cadRunning)
                {
                    view.StatusLabel.Text = "AutoCAD 运行中";
                    view.StatusLabel.ForeColor = InstallerTheme.Warning;
                }
                else if (installed && !snapshot.InstallationHealthy)
                {
                    view.StatusLabel.Text = "需要修复";
                    view.StatusLabel.ForeColor = InstallerTheme.Danger;
                }
                else
                {
                    view.StatusLabel.Text = "可用";
                    view.StatusLabel.ForeColor = InstallerTheme.Success;
                }
            }

            foreach (KeyValuePair<string, ComponentTreeItemControl> pair in
                _componentViews)
            {
                InstallerComponentState state;
                if (_currentPlan.ComponentStates.TryGetValue(pair.Key,
                        out state)) pair.Value.SetState(state);
            }

            _operationStatus.Text = selectedCadRunning
                ? "所选 AutoCAD 版本正在运行。关闭该版本后即可继续。"
                : _currentPlan.Summary;
            _operationStatus.ForeColor = selectedCadRunning
                ? InstallerTheme.Warning : InstallerTheme.Text;
            _pendingChanges.Text = _currentPlan.Changes.Length == 0
                ? "无待应用更改"
                : string.Join(Environment.NewLine,
                    _currentPlan.Changes.Take(2).Select(x => x.Description))
                    + (_currentPlan.Changes.Length > 2
                        ? Environment.NewLine + "另有 "
                            + (_currentPlan.Changes.Length - 2) + " 项…"
                        : string.Empty);
            _primaryButton.Text = selectedCadRunning
                ? "请先关闭所选 AutoCAD"
                : _currentPlan.PrimaryButtonText;
            _primaryButton.Enabled = !_busy && !selectedCadRunning
                && _currentPlan.CanExecute;
            bool hasInstalledSelection = snapshots.Any(x => x.Selected
                && x.Installed);
            _manageButton.Enabled = !_busy && hasInstalledSelection;
            RefreshManagementMenu(hasInstalledSelection);
            UpdateDetailCompatibility();
        }

        private InstallerTargetSnapshot[] CaptureSnapshots()
        {
            return _targetViews.Select(view => new InstallerTargetSnapshot
            {
                Installation = view.Installation,
                Selected = view.CheckBox.Checked,
                Installed = BundleInstallService.IsInstalled(
                    view.Installation),
                InstallationHealthy = !BundleInstallService.IsInstalled(
                        view.Installation)
                    || BundleInstallService.IsInstallationHealthy(
                        view.Installation, _componentManifest),
                InstalledVersion = BundleInstallService
                    .ReadInstalledProductVersion(view.Installation),
                InstalledComponentIds = BundleInstallService
                    .ReadInstalledComponentIds(view.Installation,
                        _componentManifest)
            }).ToArray();
        }

        private string[] SelectedComponentIds()
        {
            return _componentViews.Values
                .Where(x => x.CheckBox.Checked || x.Component.Required)
                .Select(x => x.Component.Id).ToArray();
        }

        private CadInstallation[] SelectedInstallations()
        {
            return _targetViews.Where(x => x.CheckBox.Checked
                    && x.Installation.IsDetected)
                .Select(x => x.Installation).ToArray();
        }

        private async Task ApplyCurrentPlanAsync()
        {
            if (_currentPlan == null || !_currentPlan.CanExecute) return;
            CadInstallation[] targets = SelectedInstallations();
            string[] components = SelectedComponentIds();
            SetBusy(true);
            _progressBar.Value = 0;
            AppendLog("准备对 " + string.Join("、",
                targets.Select(x => x.Version.DisplayName)) + " 应用组件："
                + string.Join("、", _componentManifest.Components
                    .Where(x => components.Contains(x.Id,
                        StringComparer.OrdinalIgnoreCase))
                    .Select(x => x.DisplayName)));
            try
            {
                Tuple<PrerequisiteStatus, IReadOnlyList<InstallResult>>
                    outcome = await Task.Run(() =>
                    {
                        PrerequisiteStatus prerequisites =
                            PrerequisiteService.EnsureRequiredComponents(
                                ReportProgress);
                        using (InstallerPayload payload =
                            InstallerPayload.Open())
                        {
                            IReadOnlyList<InstallResult> results =
                                BundleInstallService.Install(
                                    payload.BundleDirectory, targets,
                                    payload.ComponentManifest, components,
                                    ReportProgress);
                            return Tuple.Create(prerequisites, results);
                        }
                    });
                _prerequisites = outcome.Item1;
                int succeeded = outcome.Item2.Count(x => x.Succeeded);
                int failed = outcome.Item2.Count - succeeded;
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Value = failed == 0 ? 100 : 0;
                _repairRequested = false;
                DetectEnvironment(false);
                AppendLog(failed == 0
                    ? "操作完成。"
                    : "操作完成，但有 " + failed + " 个目标失败。");
                MessageBox.Show(this,
                    failed == 0
                        ? "CDBox 已成功应用到 " + succeeded
                            + " 个 AutoCAD 版本。"
                        : "成功 " + succeeded + " 个，失败 " + failed
                            + " 个。请查看安装日志。",
                    Text, MessageBoxButtons.OK,
                    failed == 0 ? MessageBoxIcon.Information
                        : MessageBoxIcon.Warning);
                if (failed > 0) SetLogExpanded(true);
            }
            catch (Exception ex)
            {
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Value = 0;
                AppendLog("操作失败：" + ex.Message);
                SetLogExpanded(true);
                MessageBox.Show(this, ex.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
                RefreshPlan();
            }
        }

        private async Task UninstallSelectedAsync()
        {
            CadInstallation[] selected = SelectedInstallations()
                .Where(BundleInstallService.IsInstalled).ToArray();
            if (selected.Length == 0) return;
            DialogResult confirmation = MessageBox.Show(this,
                "将从所选 AutoCAD 中删除 CDBox 插件和加载注册。用户配置数据会保留。是否继续？",
                "卸载 CDBox", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes) return;
            SetBusy(true);
            try
            {
                IReadOnlyList<InstallResult> results = await Task.Run(() =>
                    BundleInstallService.Uninstall(selected,
                        ReportProgress));
                int succeeded = results.Count(x => x.Succeeded);
                int failed = results.Count - succeeded;
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Value = failed == 0 ? 100 : 0;
                AppendLog(failed == 0 ? "卸载完成。"
                    : "部分目标卸载失败。");
                MessageBox.Show(this,
                    failed == 0
                        ? "已从 " + succeeded
                            + " 个 AutoCAD 版本卸载 CDBox。"
                        : "卸载成功 " + succeeded + " 个，失败 "
                            + failed + " 个。",
                    Text, MessageBoxButtons.OK,
                    failed == 0 ? MessageBoxIcon.Information
                        : MessageBoxIcon.Warning);
                if (failed > 0) SetLogExpanded(true);
            }
            catch (Exception ex)
            {
                AppendLog("卸载失败：" + ex.Message);
                SetLogExpanded(true);
                MessageBox.Show(this, ex.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
                RefreshPlan();
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            foreach (CadTargetView view in _targetViews)
                view.CheckBox.Enabled = !busy
                    && view.Installation.IsDetected;
            foreach (ComponentTreeItemControl item in
                _componentViews.Values)
                item.CheckBox.Enabled = !busy
                    && item.Component.CanChangeSelection;
            _manageButton.Enabled = !busy;
            _cancelButton.Enabled = !busy;
            UseWaitCursor = busy;
            if (busy)
            {
                _primaryButton.Enabled = false;
                _progressBar.Style = ProgressBarStyle.Marquee;
                _operationStatus.Text = "正在处理安装文件…";
                _operationStatus.ForeColor = InstallerTheme.Accent;
            }
        }

        private void RefreshManagementMenu(bool hasInstalledSelection)
        {
            _managementMenu.Items["repair"].Enabled =
                hasInstalledSelection && !_busy;
            _managementMenu.Items["open"].Enabled =
                hasInstalledSelection && !_busy;
            _managementMenu.Items["uninstall"].Enabled =
                hasInstalledSelection && !_busy;
        }

        private void ShowTargetMenu(CadTargetView view)
        {
            var menu = new ContextMenuStrip
            {
                Font = Font,
                ShowImageMargin = false
            };
            var path = new ToolStripMenuItem("查看 AutoCAD 安装目录")
            {
                Enabled = view.Installation.IsDetected
            };
            path.Click += delegate
            {
                OpenDirectory(view.Installation.InstallDirectory);
            };
            var bundle = new ToolStripMenuItem("查看 CDBox 安装目录")
            {
                Enabled = BundleInstallService.IsInstalled(
                    view.Installation)
            };
            bundle.Click += delegate
            {
                OpenDirectory(BundleInstallService.GetInstalledBundlePath(
                    view.Installation));
            };
            menu.Items.Add(path);
            menu.Items.Add(bundle);
            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(view.MoreButton,
                new Point(0, view.MoreButton.Height));
        }

        private void OpenSelectedInstallDirectory()
        {
            CadInstallation selected = SelectedInstallations()
                .FirstOrDefault(BundleInstallService.IsInstalled);
            if (selected != null)
                OpenDirectory(BundleInstallService.GetInstalledBundlePath(
                    selected));
        }

        private void OpenDirectory(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)
                    || !Directory.Exists(path))
                    throw new DirectoryNotFoundException(
                        "目录不存在：" + path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + path + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ToggleLog()
        {
            SetLogExpanded(!_logExpanded);
        }

        private void SetLogExpanded(bool expanded)
        {
            _logExpanded = expanded;
            var root = _logCard.Tag as TableLayoutPanel;
            if (root == null) return;
            root.RowStyles[4].Height = expanded ? 142 : 0;
            _logCard.Visible = expanded;
            _logToggleButton.Text = expanded ? "收起日志" : "查看日志";
        }

        private void ReportProgress(string message)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() =>
            {
                _operationStatus.Text = message;
                AppendLog(message);
            }));
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (_logBox.TextLength > 0)
                _logBox.AppendText(Environment.NewLine);
            _logBox.AppendText(DateTime.Now.ToString("HH:mm:ss")
                + "  " + message);
        }

        private sealed class CadTargetView
        {
            public CadInstallation Installation { get; set; }
            public TableLayoutPanel Row { get; set; }
            public ModernCheckBox CheckBox { get; set; }
            public Label StatusLabel { get; set; }
            public Label InstalledVersionLabel { get; set; }
            public Button MoreButton { get; set; }
        }
    }
}
