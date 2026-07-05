using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TCPipeAutoDraw.Core.Modules;

namespace TCPipeAutoDraw.UI
{
    internal sealed class CDBoxSidebarControl : UserControl
    {
        private static readonly Color CadBackColor = Color.FromArgb(54, 54, 54);
        private static readonly Color CadPanelBackColor = Color.FromArgb(48, 48, 48);
        private static readonly Color CadForeColor = Color.FromArgb(225, 225, 225);

        private static readonly Color CadBackColor1 = Color.FromArgb(54, 54, 54);
        private static readonly Color CadPanelBackColor1 = Color.FromArgb(48, 48, 48);
        private static readonly Color CadForeColor1 = Color.FromArgb(255, 238, 255);

        private readonly TreeView _tree;
        private IList<ITCModule> _modules;

        public CDBoxSidebarControl()
        {
            _modules = new List<ITCModule>();

            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            BorderStyle = BorderStyle.None;
            BackColor = CadBackColor1;
            ForeColor = CadForeColor1;

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Margin = Padding.Empty;
            root.Padding = new Padding(6, 6, 6, 6);
            root.BackColor = CadBackColor1;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var title = new Label();
            title.Text = "超重氢工具箱";
            title.Dock = DockStyle.Top;
            title.Margin = Padding.Empty;
            title.AutoSize = true;
            title.Font = new Font(Font.FontFamily, 12.5f, FontStyle.Bold);
            title.ForeColor = CadForeColor1;
            title.BackColor = CadBackColor1;
            title.Padding = new Padding(4, 0, 0, 6);
            root.Controls.Add(title, 0, 0);

            _tree = new TreeView();
            _tree.Dock = DockStyle.Fill;
            _tree.Margin = Padding.Empty;
            _tree.BorderStyle = BorderStyle.None;
            _tree.HideSelection = false;
            _tree.ShowLines = true;
            _tree.ShowPlusMinus = true;
            _tree.ShowRootLines = false;
            _tree.FullRowSelect = true;
            _tree.ItemHeight = 28;
            _tree.Indent = 18;
            _tree.BackColor = CadPanelBackColor1;
            _tree.ForeColor = CadForeColor1;
            _tree.Font = new Font(Font.FontFamily, 10f, FontStyle.Regular);
            _tree.NodeMouseClick += OnNodeMouseClick;
            root.Controls.Add(_tree, 0, 1);

            var tips = new Label();
            tips.Text = "   PRESENT  DAY\n          PRESENT  TIME";
            tips.AutoSize = false;
            tips.Dock = DockStyle.Fill;
            tips.Height = 36;
            tips.ForeColor = SystemColors.GrayText;
            root.Controls.Add(tips, 0, 4);
        }

        public void SetModules(IEnumerable<ITCModule> modules)
        {
            _modules = modules == null ? new List<ITCModule>() : new List<ITCModule>(modules);
            RebuildTree();
        }

        private void RebuildTree()
        {
            if (_tree == null) return;

            _tree.BeginUpdate();
            try
            {
                _tree.Nodes.Clear();

                TreeNode common = CreateCategoryNode("常用功能");
                AddModuleNode(common, FindModule("layer-manager"));
                //(common, FindModule("surface-area-annotation"));
                //(common, FindModule("pipe-length-annotation"));
                //(common, FindModule("node-annotation"));
                //(common, FindModule("section-drawing"));
                //(common, "批量生成断面", "PLDM");
                AddCategoryIfNotEmpty(common);

                TreeNode annotation = CreateCategoryNode("标注");
                AddModuleNode(annotation, FindModule("surface-area-annotation"));
                AddModuleNode(annotation, FindModule("pipe-length-annotation"));
                AddModuleNode(annotation, FindModule("node-annotation"));
                AddCategoryIfNotEmpty(annotation);

                TreeNode section = CreateCategoryNode("断面");
                AddModuleNode(section, FindModule("section-drawing"));
                AddCommandNode(section, "批量生成断面", "PLDM");
                AddCategoryIfNotEmpty(section);

                TreeNode property = CreateCategoryNode("污水管线属性");
                AddModuleNode(property, FindModule("quantity-pipe-attributes"));
                AddCommandNode(property, "属性默认表", "SXMRB");
                AddCommandNode(property, "属性清除", "SXQC");
                AddCategoryIfNotEmpty(property);

                TreeNode quantity = CreateCategoryNode("工程量");
                AddModuleNode(quantity, FindModule("quantity-calculation"));
                AddCategoryIfNotEmpty(quantity);

                TreeNode frame = CreateCategoryNode("图框工具");
                AddModuleNode(frame, FindModule("frame-template-add"));
                AddModuleNode(frame, FindModule("frame-cut-layout"));
                AddCategoryIfNotEmpty(frame);

                TreeNode others = CreateCategoryNode("其他");
                foreach (ITCModule module in _modules)
                {
                    if (module == null || !module.Enabled) continue;
                    if (IsKnownModule(module.Id)) continue;
                    AddModuleNode(others, module);
                }
                AddCategoryIfNotEmpty(others);

                TreeNode settings = CreateCategoryNode("设置");
                TreeNode settingsNode = CreateActionNode("CDBox 设置", "settings");
                settings.Nodes.Add(settingsNode);
                _tree.Nodes.Add(settings);

                _tree.ExpandAll();
            }
            finally
            {
                _tree.EndUpdate();
            }
        }

        private TreeNode CreateCategoryNode(string text)
        {
            TreeNode node = new TreeNode(text);
            node.NodeFont = new Font(_tree.Font, FontStyle.Bold);
            node.ForeColor = CadForeColor1;
            return node;
        }

        private TreeNode CreateActionNode(string text, object tag)
        {
            TreeNode node = new TreeNode(text);
            node.Tag = tag;
            node.ForeColor = CadForeColor1;
            return node;
        }

        private void AddModuleNode(TreeNode parent, ITCModule module)
        {
            if (parent == null || module == null || !module.Enabled) return;
            parent.Nodes.Add(CreateActionNode(module.Name, module));
        }

        private void AddCommandNode(TreeNode parent, string text, string commandName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(commandName)) return;
            parent.Nodes.Add(CreateActionNode(text, "cmd:" + commandName.Trim()));
        }

        private void AddCategoryIfNotEmpty(TreeNode node)
        {
            if (node != null && node.Nodes.Count > 0) _tree.Nodes.Add(node);
        }

        private ITCModule FindModule(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            foreach (ITCModule module in _modules)
            {
                if (module == null) continue;
                if (string.Equals(module.Id, id, StringComparison.OrdinalIgnoreCase)) return module;
            }
            return null;
        }

        private bool IsKnownModule(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            switch (id.ToLowerInvariant())
            {
                case "layer-manager":
                case "surface-area-annotation":
                case "pipe-length-annotation":
                case "node-annotation":
                case "section-drawing":
                case "frame-template-add":
                case "frame-cut-layout":
                case "quantity-pipe-attributes":
                case "quantity-calculation":
                    return true;
                default:
                    return false;
            }
        }

        private void OnNodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e == null || e.Node == null) return;
            _tree.SelectedNode = e.Node;

            if (e.Node.Tag == null)
            {
                return;
            }

            ITCModule module = e.Node.Tag as ITCModule;
            if (module != null)
            {
                RunModule(module);
                return;
            }

            string action = e.Node.Tag as string;
            if (string.Equals(action, "settings", StringComparison.OrdinalIgnoreCase))
            {
                ShowSettings();
                return;
            }

            if (!string.IsNullOrWhiteSpace(action) && action.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase))
            {
                RunAcadCommand(action.Substring(4));
            }
        }

        private void RunModule(ITCModule module)
        {
            if (module == null || !module.Enabled) return;

            try
            {
                module.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show(new AcadMainWindow(), ex.Message, module.Name + "运行失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunAcadCommand(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName)) return;

            try
            {
                Autodesk.AutoCAD.ApplicationServices.Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show(new AcadMainWindow(), "未找到当前图纸。", "CDBox", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                doc.SendStringToExecute(commandName.Trim() + " ", true, false, false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(new AcadMainWindow(), ex.Message, "CDBox 命令执行失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowSettings()
        {
            using (var form = new CDBoxSettingsForm())
            {
                form.ShowDialog(new AcadMainWindow());
            }
        }
    }
}
