using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using TCPipeAutoDraw.Core.Modules;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.UI
{
    internal sealed class CDBoxSidebarControl : UserControl
    {
        private static readonly Color Back = Color.FromArgb(45, 45, 48);
        private static readonly Color CardBack = Color.FromArgb(56, 56, 60);
        private static readonly Color Fore = Color.FromArgb(238, 238, 238);
        private static readonly Color Muted = Color.FromArgb(176, 180, 188);
        private static readonly Color Accent = Color.FromArgb(73, 135, 244);
        private static readonly Color Issue = Color.FromArgb(255, 194, 92);

        private readonly FlowLayoutPanel _content;
        private readonly Panel _currentCard;
        private readonly Panel _actionsCard;
        private readonly Panel _issuesCard;
        private readonly Panel _quickCard;
        private readonly Timer _refreshTimer;
        private IList<ITCModule> _modules;
        private string _lastSignature;
        private int _pollCount;

        public CDBoxSidebarControl()
        {
            _modules = new List<ITCModule>();
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            BackColor = Back;
            ForeColor = Fore;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            _content = new FlowLayoutPanel();
            _content.Dock = DockStyle.Fill;
            _content.FlowDirection = FlowDirection.TopDown;
            _content.WrapContents = false;
            _content.AutoScroll = true;
            _content.Padding = new Padding(6);
            _content.Margin = Padding.Empty;
            _content.BackColor = Back;
            Controls.Add(_content);

            _content.Controls.Add(BuildHeader());
            _currentCard = CreateCard("当前对象");
            _actionsCard = CreateCard("快捷操作");
            _issuesCard = CreateCard("问题摘要");
            _quickCard = CreateCard("收藏 / 最近");
            _content.Controls.Add(_currentCard);
            _content.Controls.Add(_actionsCard);
            _content.Controls.Add(_issuesCard);
            _content.Controls.Add(_quickCard);

            _refreshTimer = new Timer();
            _refreshTimer.Interval = 750;
            _refreshTimer.Tick += OnRefreshTick;
            _refreshTimer.Start();
            SizeChanged += delegate { ResizeCards(); };
            RefreshContext(true);
        }

        public void SetModules(IEnumerable<ITCModule> modules)
        {
            _modules = modules == null ? new List<ITCModule>() : new List<ITCModule>(modules);
            BuildQuickAccess();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _refreshTimer != null) _refreshTimer.Dispose();
            base.Dispose(disposing);
        }

        private Control BuildHeader()
        {
            var panel = new TableLayoutPanel
            {
                Height = 52,
                Margin = new Padding(0, 0, 0, 6),
                BackColor = Back,
                ColumnCount = 3,
                RowCount = 1
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            var title = new Label
            {
                Text = "CDBox",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(4, 4, 0, 0),
                ForeColor = Fore,
                Font = new Font(Font.FontFamily, 13F, FontStyle.Bold)
            };
            var refresh = CreateTextButton("刷新", false);
            refresh.Width = 54;
            refresh.Height = 30;
            refresh.MinimumSize = Size.Empty;
            refresh.Dock = DockStyle.Top;
            refresh.Click += delegate { RefreshContext(true); };
            var settings = CreateTextButton("设置", false);
            settings.Width = 54;
            settings.Height = 30;
            settings.MinimumSize = Size.Empty;
            settings.Dock = DockStyle.Top;
            settings.Click += delegate { ShowSettings(); };
            panel.Controls.Add(title, 0, 0);
            panel.Controls.Add(refresh, 1, 0);
            panel.Controls.Add(settings, 2, 0);
            return panel;
        }

        private Panel CreateCard(string title)
        {
            var card = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                BackColor = CardBack,
                Margin = new Padding(0, 0, 0, 6),
                Padding = new Padding(10, 9, 10, 10),
                MinimumSize = new Size(200, 48)
            };
            var layout = new FlowLayoutPanel
            {
                Name = "Body",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Dock = DockStyle.Top,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = CardBack
            };
            layout.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 7),
                ForeColor = Fore,
                Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold)
            });
            card.Controls.Add(layout);
            return card;
        }

        private void OnRefreshTick(object sender, EventArgs e)
        {
            if (!Visible || IsDisposed) return;
            _pollCount++;
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            string signature = CDBoxSidebarContextService.GetSelectionSignature(doc);
            if (!string.Equals(signature, _lastSignature, StringComparison.Ordinal) || _pollCount >= 6)
            {
                _pollCount = 0;
                RefreshContext(false);
            }
        }

        private void RefreshContext(bool force)
        {
            if (IsDisposed) return;
            try
            {
                Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                string signature = CDBoxSidebarContextService.GetSelectionSignature(doc);
                if (!force && string.Equals(signature, _lastSignature, StringComparison.Ordinal) && _pollCount != 0) return;
                CDBoxSidebarContext context = CDBoxSidebarContextService.ReadCurrent(doc);
                _lastSignature = context.Signature;
                BuildCurrentCard(context);
                BuildActions(context);
                BuildIssues(context);
                BuildQuickAccess();
                ResizeCards();
            }
            catch
            {
                // CAD 正在切换文档或执行命令时，保留上一次稳定摘要。
            }
        }

        private void BuildCurrentCard(CDBoxSidebarContext context)
        {
            FlowLayoutPanel body = GetBody(_currentCard);
            ClearBody(body);
            body.Controls.Add(CreateLabel(context.Title, 10F, FontStyle.Bold, Fore, new Padding(0, 0, 0, 2)));
            body.Controls.Add(CreateLabel(context.Subtitle, 8.5F, FontStyle.Regular, Muted, new Padding(0, 0, 0, 7)));
            foreach (CDBoxSidebarField field in context.Fields)
            {
                var row = new TableLayoutPanel
                {
                    ColumnCount = 2,
                    RowCount = 1,
                    Height = 24,
                    Margin = Padding.Empty,
                    BackColor = CardBack
                };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                row.Controls.Add(CreateLabel(field.Name, 8.5F, FontStyle.Regular, Muted, Padding.Empty), 0, 0);
                row.Controls.Add(CreateLabel(field.Value, 8.5F, FontStyle.Regular, Fore, Padding.Empty), 1, 0);
                body.Controls.Add(row);
            }
        }

        private void BuildActions(CDBoxSidebarContext context)
        {
            FlowLayoutPanel body = GetBody(_actionsCard);
            ClearBody(body);
            var flow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = Padding.Empty,
                BackColor = CardBack
            };
            foreach (CDBoxSidebarAction action in context.Actions)
            {
                Button button = CreateTextButton(action.Text, true);
                string command = action.CommandName;
                button.Click += delegate { RunAcadCommand(command); };
                flow.Controls.Add(button);
            }
            body.Controls.Add(flow);
        }

        private void BuildIssues(CDBoxSidebarContext context)
        {
            _issuesCard.Visible = context.Issues.Count > 0;
            FlowLayoutPanel body = GetBody(_issuesCard);
            ClearBody(body);
            foreach (string issue in context.Issues)
            {
                body.Controls.Add(CreateLabel("● " + issue, 8.5F, FontStyle.Regular, Issue, new Padding(0, 0, 0, 4)));
            }
        }

        private void BuildQuickAccess()
        {
            if (_quickCard == null) return;
            FlowLayoutPanel body = GetBody(_quickCard);
            ClearBody(body);
            var flow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = Padding.Empty,
                BackColor = CardBack
            };

            CDBoxStudioState state = CDBoxStudioStateStore.Load();
            var ids = state.FavoriteIds.Concat(state.RecentItems.OrderByDescending(x => x.LastUsedUtc).Select(x => x.Id))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();
            if (ids.Count == 0)
            {
                ids.Add("module:layer-manager");
                ids.Add("module:quantity-pipe-attributes");
                ids.Add("module:quantity-calculation");
            }

            foreach (string id in ids)
            {
                ITCModule module = ResolveModule(id);
                string command = ResolveCommand(id);
                if (module == null && string.IsNullOrWhiteSpace(command)) continue;
                string text = module == null ? GetCommandTitle(command) : module.Name;
                Button button = CreateWideButton(text);
                if (module != null)
                {
                    ITCModule captured = module;
                    button.Click += delegate { RunModule(captured); };
                }
                else
                {
                    string captured = command;
                    button.Click += delegate { RunAcadCommand(captured); };
                }
                flow.Controls.Add(button);
            }
            Button studio = CreateWideButton("打开全部功能");
            studio.Click += delegate { RunAcadCommand("CDSTUDIO"); };
            flow.Controls.Add(studio);
            body.Controls.Add(flow);
        }

        private void ResizeCards()
        {
            int width = Math.Max(200, _content.ClientSize.Width - _content.Padding.Horizontal - (SystemInformation.VerticalScrollBarWidth + 2));
            foreach (Control control in _content.Controls)
            {
                control.Width = width;
                FlowLayoutPanel body = control.Controls["Body"] as FlowLayoutPanel;
                if (body == null) continue;
                body.Width = Math.Max(160, width - 20);
                foreach (Control child in body.Controls)
                {
                    if (child is TableLayoutPanel || child is FlowLayoutPanel) child.Width = body.Width;
                }
            }
        }

        private static FlowLayoutPanel GetBody(Panel card)
        {
            return card.Controls["Body"] as FlowLayoutPanel;
        }

        private static void ClearBody(FlowLayoutPanel body)
        {
            while (body.Controls.Count > 1) body.Controls.RemoveAt(body.Controls.Count - 1);
        }

        private Label CreateLabel(string text, float size, FontStyle style, Color color, Padding margin)
        {
            return new Label
            {
                Text = string.IsNullOrWhiteSpace(text) ? "—" : text,
                AutoSize = true,
                MaximumSize = new Size(220, 0),
                Margin = margin,
                ForeColor = color,
                Font = new Font(Font.FontFamily, size, style),
                BackColor = CardBack
            };
        }

        private Button CreateTextButton(string text, bool accent)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 32,
                MinimumSize = new Size(72, 32),
                Margin = new Padding(0, 0, 6, 6),
                Padding = new Padding(8, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Fore,
                BackColor = accent ? Accent : CardBack,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = accent ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(82, 84, 90);
            return button;
        }

        private Button CreateWideButton(string text)
        {
            Button button = CreateTextButton(text, false);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Margin = new Padding(0, 0, 0, 4);
            button.Width = 210;
            return button;
        }

        private ITCModule ResolveModule(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("module:", StringComparison.OrdinalIgnoreCase)) return null;
            string moduleId = id.Substring(7);
            return _modules.FirstOrDefault(x => x != null && x.Enabled && string.Equals(x.Id, moduleId, StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolveCommand(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && id.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase) ? id.Substring(4) : string.Empty;
        }

        private static string GetCommandTitle(string command)
        {
            switch ((command ?? string.Empty).ToUpperInvariant())
            {
                case "PLDM": return "批量生成断面";
                case "SXMRB": return "属性默认表";
                case "SXQC": return "属性清除";
                case "CDSET": return "CDBox 设置";
                default: return command;
            }
        }

        private void RunModule(ITCModule module)
        {
            if (module == null || !module.Enabled) return;
            try
            {
                MarkRecent("module:" + module.Id);
                module.Run();
            }
            catch (Exception ex)
            {
                CDBoxMessageBox.Show(new AcadMainWindow(), ex.Message,
                    module.Name + "运行失败", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void RunAcadCommand(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName)) return;
            try
            {
                Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    CDBoxMessageBox.Show(new AcadMainWindow(),
                        "未找到当前图纸。", "CDBox", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
                string stateActionId = GetStateActionId(commandName);
                if (!string.IsNullOrWhiteSpace(stateActionId)) MarkRecent(stateActionId);
                doc.SendStringToExecute(commandName.Trim() + " ", true, false, false);
            }
            catch (Exception ex)
            {
                CDBoxMessageBox.Show(new AcadMainWindow(), ex.Message,
                    "CDBox 命令执行失败", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void MarkRecent(string id)
        {
            CDBoxStudioState state = CDBoxStudioStateStore.Load();
            state.MarkRecent(id);
            CDBoxStudioStateStore.Save(state);
        }

        private static string GetStateActionId(string commandName)
        {
            switch ((commandName ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "TCGL":
                case "CDLAYER": return "module:layer-manager";
                case "SX": return "module:quantity-pipe-attributes";
                case "GCBZ": return "module:pipe-length-annotation";
                case "JDBZ": return "module:node-annotation";
                case "GCL": return "module:quantity-calculation";
                case "PLDM": return "cmd:PLDM";
                case "SXMRB": return "cmd:SXMRB";
                case "SXQC": return "cmd:SXQC";
                case "CDSET": return "cmd:CDSET";
                default: return string.Empty;
            }
        }

        private static void ShowSettings()
        {
            Studio.CDBoxStudioSettingsWindow.ShowWindow(new AcadMainWindow());
        }
    }
}
