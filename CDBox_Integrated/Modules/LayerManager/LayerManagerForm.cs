using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace TCPipeAutoDraw.Modules.LayerManager
{
    public class LayerManagerForm : Form
    {
        private readonly Document _doc;
        private List<LayerInfo> _allLayers;
        private BindingList<LayerInfo> _visibleLayers;

        private TreeView _tree;
        private DataGridView _grid;
        private TextBox _txtFilter;
        private ComboBox _cboTagSearch;
        private Label _lblStatus;
        private CheckBox _chkCountObjects;
        private CheckBox _chkForceUnlockDelete;
        private SplitContainer _mainSplit;
        private bool _updatingTreeChecks;

        public LayerManagerForm(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            _doc = doc;
            _allLayers = new List<LayerInfo>();
            _visibleLayers = new BindingList<LayerInfo>();

            Text = "CDBox - 图层批量管理";
            Width = 1515;
            Height = 760;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = true;

            BuildUi();
            Shown += delegate { BeginInvoke(new Action(ApplyDefaultSplitterDistance)); };
            RefreshLayers(true);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.RowCount = 4;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var top = new FlowLayoutPanel();
            top.Dock = DockStyle.Fill;
            top.AutoSize = true;
            top.Padding = new Padding(8, 8, 8, 4);
            top.WrapContents = true;
            root.Controls.Add(top, 0, 0);

            top.Controls.Add(new Label { Text = "筛选：", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            _txtFilter = new TextBox { Width = 220 };
            _txtFilter.TextChanged += delegate { ApplyFilter(); };
            top.Controls.Add(_txtFilter);

            top.Controls.Add(new Label { Text = "标签：", AutoSize = true, Padding = new Padding(12, 6, 0, 0) });
            _cboTagSearch = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDown };
            _cboTagSearch.KeyDown += TagSearchKeyDown;
            top.Controls.Add(_cboTagSearch);
            top.Controls.Add(MakeButton("勾选标签", delegate { SelectLayersByTag(false); }));
            top.Controls.Add(MakeButton("仅勾选标签", delegate { SelectLayersByTag(true); }));

            top.Controls.Add(MakeButton("刷新", delegate { RefreshLayers(true); }));
            top.Controls.Add(MakeButton("拾取对象加入勾选", PickLayersFromObjects));
            top.Controls.Add(MakeButton("全选", delegate { SetAllVisibleSelected(true); }));
            top.Controls.Add(MakeButton("反选", InvertVisibleSelected));
            top.Controls.Add(MakeButton("清空勾选", delegate { SetAllVisibleSelected(false); }));

            _chkCountObjects = new CheckBox { Text = "统计对象数", AutoSize = true, Checked = true, Padding = new Padding(12, 4, 0, 0) };
            _chkCountObjects.CheckedChanged += delegate { RefreshLayers(false); };
            top.Controls.Add(_chkCountObjects);

            _chkForceUnlockDelete = new CheckBox { Text = "删除时临时解锁", AutoSize = true, Checked = false, Padding = new Padding(12, 4, 0, 0) };
            top.Controls.Add(_chkForceUnlockDelete);

            _mainSplit = new SplitContainer();
            var split = _mainSplit;
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.FixedPanel = FixedPanel.Panel1;
            // 不在初始化阶段设置 Panel1MinSize/Panel2MinSize/SplitterDistance。
            // SplitContainer 在加入布局前 Width 很小，设置最小宽度也可能触发 SplitterDistance 越界异常。
            root.Controls.Add(split, 0, 1);

            var treeGroup = new GroupBox();
            treeGroup.Text = "父属性 / 分类";
            treeGroup.Dock = DockStyle.Fill;
            treeGroup.Padding = new Padding(6);
            split.Panel1.Controls.Add(treeGroup);

            _tree = new TreeView();
            _tree.Dock = DockStyle.Fill;
            _tree.CheckBoxes = true;
            _tree.HideSelection = false;
            _tree.AfterCheck += LayerTreeAfterCheck;
            treeGroup.Controls.Add(_tree);

            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = true;
            _grid.EditMode = DataGridViewEditMode.EditOnEnter;
            _grid.CellDoubleClick += GridCellDoubleClick;
            _grid.CellFormatting += GridCellFormatting;
            _grid.CurrentCellDirtyStateChanged += GridCurrentCellDirtyStateChanged;
            _grid.CellValueChanged += GridCellValueChanged;
            BuildGridColumns();
            split.Panel2.Controls.Add(_grid);

            var actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.AutoSize = true;
            actions.Padding = new Padding(8, 4, 8, 4);
            actions.WrapContents = true;
            root.Controls.Add(actions, 0, 2);

            actions.Controls.Add(MakeButton("选中对象", delegate { SelectObjects(); }));
            actions.Controls.Add(MakeButton("删除对象", delegate { DeleteObjects(); }));
            actions.Controls.Add(MakeButton("锁定图层", delegate { ApplyState(LayerStateAction.Lock, true); }));
            actions.Controls.Add(MakeButton("解锁图层", delegate { ApplyState(LayerStateAction.Unlock, true); }));
            actions.Controls.Add(MakeButton("隐藏图层", delegate { ApplyState(LayerStateAction.TurnOff, true); }));
            actions.Controls.Add(MakeButton("显示/解冻", ShowSelectedLayers));
            actions.Controls.Add(MakeButton("冻结图层", delegate { ApplyState(LayerStateAction.Freeze, true); }));
            actions.Controls.Add(MakeButton("解冻图层", delegate { ApplyState(LayerStateAction.Thaw, true); }));
            actions.Controls.Add(MakeButton("设为当前", SetCurrentLayer));
            actions.Controls.Add(MakeButton("仅显示勾选", OnlyShowCheckedLayers));
            actions.Controls.Add(MakeButton("显示全部", ShowAllLayers));
            actions.Controls.Add(MakeButton("新建图层", CreateLayers));
            actions.Controls.Add(MakeButton("创建管线默认层", CreateDefaultPipeLayers));
            actions.Controls.Add(MakeButton("设置父类/标签", EditLayerMetadata));
            actions.Controls.Add(MakeButton("属性识别表", OpenRecognitionRulesEditor));
            actions.Controls.Add(MakeButton("自动识别属性", AutoInferLayerMetadata));
            actions.Controls.Add(MakeButton("关闭", delegate { Close(); }));

            _lblStatus = new Label();
            _lblStatus.Dock = DockStyle.Fill;
            _lblStatus.AutoSize = true;
            _lblStatus.Padding = new Padding(10, 6, 10, 8);
            root.Controls.Add(_lblStatus, 0, 3);
        }

        private void ApplyDefaultSplitterDistance()
        {
            if (_mainSplit == null || _mainSplit.IsDisposed) return;
            if (!_mainSplit.IsHandleCreated) return;

            // 这里不使用 Panel1MinSize / Panel2MinSize 作为强约束，
            // 因为 SplitContainer 在不同 DPI、窗口初始化阶段、AutoCAD 宿主窗口中，
            // 这些 MinSize 很容易和当前 Width 组合出非法范围。
            // 只在控件真正可见后，用当前实际宽度安全夹取目标宽度。
            int width = _mainSplit.ClientSize.Width;
            int splitterWidth = _mainSplit.SplitterWidth;
            if (width <= splitterWidth + 80) return;

            int minDistance = Math.Max(25, _mainSplit.Panel1MinSize);
            int maxDistance = width - splitterWidth - Math.Max(25, _mainSplit.Panel2MinSize);
            if (maxDistance < minDistance) return;

            int target = 286; // 图2样式：左侧树约 280px，右侧表格保留主要空间。
            if (target < minDistance) target = minDistance;
            if (target > maxDistance) target = maxDistance;

            try
            {
                if (_mainSplit.SplitterDistance != target)
                {
                    _mainSplit.SplitterDistance = target;
                }
            }
            catch
            {
                // AutoCAD 宿主窗口布局过程中偶发无效尺寸，跳过即可；
                // 这只影响左侧树初始宽度，不影响图层管理功能。
            }
        }

        private void BuildGridColumns()
        {
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "勾选",
                DataPropertyName = "Selected",
                Width = 56
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "图层名",
                DataPropertyName = "Name",
                Width = 300,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "父属性",
                DataPropertyName = "ParentGroup",
                Width = 90,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "分类",
                DataPropertyName = "ParentClass",
                Width = 130,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "标签",
                DataPropertyName = "TagText",
                Width = 190,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "状态",
                DataPropertyName = "StatusText",
                Width = 110,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "对象数",
                DataPropertyName = "ObjectCount",
                Width = 70,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "颜色",
                DataPropertyName = "ColorIndex",
                Width = 60,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "线型",
                DataPropertyName = "Linetype",
                Width = 120,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "打印",
                DataPropertyName = "IsPlottable",
                Width = 56,
                ReadOnly = true
            });
        }

        private Button MakeButton(string text, EventHandler handler)
        {
            var button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Margin = new Padding(3, 3, 3, 3);
            button.Click += handler;
            return button;
        }

        private void RefreshLayers(bool keepChecks)
        {
            try
            {
                HashSet<string> checkedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (keepChecks && _allLayers != null)
                {
                    foreach (LayerInfo item in _allLayers)
                    {
                        if (item.Selected) checkedNames.Add(item.Name);
                    }
                }

                _allLayers = LayerManagerService.GetLayers(_doc, _chkCountObjects == null || _chkCountObjects.Checked);
                foreach (LayerInfo item in _allLayers)
                {
                    item.Selected = checkedNames.Contains(item.Name);
                }
                RebuildLayerTree();
                UpdateTagSearchList();
                ApplyFilter();
                WriteStatus("已刷新图层列表，共 " + _allLayers.Count + " 个图层。左侧可按父属性/分类勾选图层，标签框可快速勾选同标签图层。");
            }
            catch (Exception ex)
            {
                WriteStatus("刷新失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "刷新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilter()
        {
            string keyword = _txtFilter == null ? string.Empty : _txtFilter.Text.Trim();
            IEnumerable<LayerInfo> query = _allLayers;
            if (keyword.Length > 0)
            {
                query = query.Where(delegate (LayerInfo x)
                {
                    return x.Name.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0
                        || x.StatusText.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0
                        || x.ParentGroup.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0
                        || x.ParentClass.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0
                        || x.TagText.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0;
                });
            }

            _visibleLayers = new BindingList<LayerInfo>(query.ToList());
            _grid.DataSource = _visibleLayers;
        }

        private void RebuildLayerTree()
        {
            if (_tree == null) return;
            _updatingTreeChecks = true;
            try
            {
                _tree.BeginUpdate();
                _tree.Nodes.Clear();

                var groups = _allLayers
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.ParentGroup) ? "未设置父属性" : x.ParentGroup.Trim())
                    .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);

                foreach (var group in groups)
                {
                    TreeNode groupNode = new TreeNode(group.Key + " (" + group.Count() + ")");
                    groupNode.Tag = LayerTreeNodeTag.CreateGroup(group.Key);
                    _tree.Nodes.Add(groupNode);

                    // 未设置分类时不再生成“未设置分类”节点，直接把图层挂在父属性节点下。
                    // 这样左侧树更简洁，也避免大量未分类图层被额外包一层。
                    var layersWithoutClass = group
                        .Where(x => string.IsNullOrWhiteSpace(x.ParentClass))
                        .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase);

                    foreach (LayerInfo layer in layersWithoutClass)
                    {
                        TreeNode layerNode = new TreeNode(layer.Name);
                        layerNode.Tag = LayerTreeNodeTag.CreateLayer(layer.Name);
                        groupNode.Nodes.Add(layerNode);
                    }

                    var classes = group
                        .Where(x => !string.IsNullOrWhiteSpace(x.ParentClass))
                        .GroupBy(x => x.ParentClass.Trim())
                        .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);

                    foreach (var cls in classes)
                    {
                        TreeNode classNode = new TreeNode(cls.Key + " (" + cls.Count() + ")");
                        classNode.Tag = LayerTreeNodeTag.CreateClass(group.Key, cls.Key);
                        groupNode.Nodes.Add(classNode);

                        foreach (LayerInfo layer in cls.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
                        {
                            TreeNode layerNode = new TreeNode(layer.Name);
                            layerNode.Tag = LayerTreeNodeTag.CreateLayer(layer.Name);
                            classNode.Nodes.Add(layerNode);
                        }
                    }

                    groupNode.Expand();
                }

                RefreshTreeCheckStatesCore();
                _tree.EndUpdate();
            }
            finally
            {
                _updatingTreeChecks = false;
            }
        }

        private void RefreshTreeCheckStates()
        {
            if (_tree == null) return;
            _updatingTreeChecks = true;
            try
            {
                _tree.BeginUpdate();
                RefreshTreeCheckStatesCore();
                _tree.EndUpdate();
            }
            finally
            {
                _updatingTreeChecks = false;
            }
        }

        private bool RefreshTreeCheckStatesCore()
        {
            bool allChecked = true;
            foreach (TreeNode node in _tree.Nodes)
            {
                if (!RefreshNodeCheckState(node)) allChecked = false;
            }
            return allChecked;
        }

        private bool RefreshNodeCheckState(TreeNode node)
        {
            LayerTreeNodeTag tag = node.Tag as LayerTreeNodeTag;
            if (tag != null && tag.NodeKind == "Layer")
            {
                LayerInfo layer = FindLayer(tag.LayerName);
                node.Checked = layer != null && layer.Selected;
                return node.Checked;
            }

            bool hasChild = node.Nodes.Count > 0;
            bool allChildrenChecked = hasChild;
            foreach (TreeNode child in node.Nodes)
            {
                if (!RefreshNodeCheckState(child)) allChildrenChecked = false;
            }
            node.Checked = hasChild && allChildrenChecked;
            return node.Checked;
        }

        private void LayerTreeAfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_updatingTreeChecks || e == null || e.Node == null) return;

            List<string> layerNames = CollectLayerNames(e.Node);
            if (layerNames.Count == 0) return;

            HashSet<string> nameSet = new HashSet<string>(layerNames, StringComparer.OrdinalIgnoreCase);
            foreach (LayerInfo item in _allLayers)
            {
                if (nameSet.Contains(item.Name)) item.Selected = e.Node.Checked;
            }

            _updatingTreeChecks = true;
            try
            {
                SetNodeAndChildrenChecked(e.Node, e.Node.Checked);
                UpdateAncestorCheckStates(e.Node.Parent);
            }
            finally
            {
                _updatingTreeChecks = false;
            }

            if (_grid != null) _grid.Refresh();
            WriteStatus("已" + (e.Node.Checked ? "勾选" : "取消勾选") + "树节点下 " + layerNames.Count + " 个图层。 ");
        }

        private static void SetNodeAndChildrenChecked(TreeNode node, bool isChecked)
        {
            if (node == null) return;
            node.Checked = isChecked;
            foreach (TreeNode child in node.Nodes)
            {
                SetNodeAndChildrenChecked(child, isChecked);
            }
        }

        private static void UpdateAncestorCheckStates(TreeNode node)
        {
            while (node != null)
            {
                bool allChecked = node.Nodes.Count > 0;
                foreach (TreeNode child in node.Nodes)
                {
                    if (!child.Checked)
                    {
                        allChecked = false;
                        break;
                    }
                }
                node.Checked = allChecked;
                node = node.Parent;
            }
        }

        private static List<string> CollectLayerNames(TreeNode node)
        {
            var names = new List<string>();
            CollectLayerNamesCore(node, names);
            return names;
        }

        private static void CollectLayerNamesCore(TreeNode node, List<string> names)
        {
            if (node == null || names == null) return;
            LayerTreeNodeTag tag = node.Tag as LayerTreeNodeTag;
            if (tag != null && tag.NodeKind == "Layer" && !string.IsNullOrWhiteSpace(tag.LayerName))
            {
                names.Add(tag.LayerName);
                return;
            }

            foreach (TreeNode child in node.Nodes)
            {
                CollectLayerNamesCore(child, names);
            }
        }

        private LayerInfo FindLayer(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName) || _allLayers == null) return null;
            return _allLayers.FirstOrDefault(x => string.Equals(x.Name, layerName, StringComparison.OrdinalIgnoreCase));
        }

        private void UpdateTagSearchList()
        {
            if (_cboTagSearch == null) return;
            string oldText = _cboTagSearch.Text;
            var tags = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (LayerInfo item in _allLayers)
            {
                foreach (string tag in LayerMetadata.ParseTags(item.TagText))
                {
                    if (!string.IsNullOrWhiteSpace(tag)) tags.Add(tag);
                }
            }

            _cboTagSearch.BeginUpdate();
            _cboTagSearch.Items.Clear();
            foreach (string tag in tags) _cboTagSearch.Items.Add(tag);
            _cboTagSearch.Text = oldText;
            _cboTagSearch.EndUpdate();
        }

        private void TagSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            SelectLayersByTag(false);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void SelectLayersByTag(bool replaceSelection)
        {
            string keyword = _cboTagSearch == null ? string.Empty : (_cboTagSearch.Text ?? string.Empty).Trim();
            if (keyword.Length == 0)
            {
                WriteStatus("请输入或选择标签。 ");
                return;
            }

            if (replaceSelection)
            {
                foreach (LayerInfo item in _allLayers) item.Selected = false;
            }

            int count = 0;
            foreach (LayerInfo item in _allLayers)
            {
                List<string> tags = LayerMetadata.ParseTags(item.TagText);
                bool exact = tags.Any(t => string.Equals(t, keyword, StringComparison.CurrentCultureIgnoreCase));
                bool fuzzy = !exact && tags.Any(t => t.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0);
                if (exact || fuzzy)
                {
                    item.Selected = true;
                    count++;
                }
            }

            if (_grid != null) _grid.Refresh();
            RefreshTreeCheckStates();
            WriteStatus("已根据标签“" + keyword + "”勾选 " + count + " 个图层。 ");
        }

        private void GridCurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_grid != null && _grid.IsCurrentCellDirty)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void GridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e == null || e.RowIndex < 0 || e.ColumnIndex < 0 || _grid == null) return;
            DataGridViewColumn column = _grid.Columns[e.ColumnIndex];
            if (column != null && string.Equals(column.DataPropertyName, "Selected", StringComparison.OrdinalIgnoreCase))
            {
                RefreshTreeCheckStates();
            }
        }

        private void GridCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _visibleLayers.Count) return;
            _visibleLayers[e.RowIndex].Selected = !_visibleLayers[e.RowIndex].Selected;
            _grid.Refresh();
            RefreshTreeCheckStates();
        }

        private void GridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _visibleLayers.Count) return;
            LayerInfo item = _visibleLayers[e.RowIndex];
            DataGridViewRow row = _grid.Rows[e.RowIndex];
            if (item.IsCurrent)
            {
                row.DefaultCellStyle.Font = new System.Drawing.Font(_grid.Font, System.Drawing.FontStyle.Bold);
            }
            else
            {
                row.DefaultCellStyle.Font = _grid.Font;
            }
            if (item.IsOff || item.IsFrozen)
            {
                row.DefaultCellStyle.ForeColor = System.Drawing.SystemColors.GrayText;
            }
            else
            {
                row.DefaultCellStyle.ForeColor = System.Drawing.SystemColors.ControlText;
            }
        }

        private void SetAllVisibleSelected(bool selected)
        {
            foreach (LayerInfo item in _visibleLayers)
            {
                item.Selected = selected;
            }
            _grid.Refresh();
            RefreshTreeCheckStates();
            WriteStatus(selected ? "已勾选当前列表全部图层。" : "已清空当前列表勾选。仍可通过筛选分批处理。 ");
        }

        private void InvertVisibleSelected(object sender, EventArgs e)
        {
            foreach (LayerInfo item in _visibleLayers)
            {
                item.Selected = !item.Selected;
            }
            _grid.Refresh();
            RefreshTreeCheckStates();
            WriteStatus("已反选当前列表。 ");
        }

        private List<string> GetCheckedLayerNames(bool allowSingleSelectedRow)
        {
            _grid.EndEdit();
            var names = new List<string>();
            foreach (LayerInfo item in _allLayers)
            {
                if (item.Selected) names.Add(item.Name);
            }

            if (names.Count == 0 && allowSingleSelectedRow)
            {
                foreach (DataGridViewRow row in _grid.SelectedRows)
                {
                    LayerInfo item = row.DataBoundItem as LayerInfo;
                    if (item != null && !names.Contains(item.Name)) names.Add(item.Name);
                }
            }
            return names;
        }

        private string GetFirstCheckedLayerName()
        {
            List<string> names = GetCheckedLayerNames(true);
            if (names.Count == 0) return string.Empty;
            return names[0];
        }

        private void PickLayersFromObjects(object sender, EventArgs e)
        {
            try
            {
                HashSet<string> picked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                PromptSelectionResult psr;
                var opts = new PromptSelectionOptions();
                opts.MessageForAdding = "\n请选择对象，插件会勾选这些对象所在图层：";

                bool wasVisible = this.Visible;
                try
                {
                    if (wasVisible) this.Hide();
                    _doc.Editor.WriteMessage("\n请选择对象，完成后回车确认；取消请按 ESC。 ");
                    psr = _doc.Editor.GetSelection(opts);
                }
                finally
                {
                    if (wasVisible)
                    {
                        this.Show();
                        this.Activate();
                    }
                }

                if (psr.Status != PromptStatus.OK || psr.Value == null)
                {
                    WriteStatus("未拾取对象。 ");
                    return;
                }

                using (Transaction tr = _doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in psr.Value.GetObjectIds())
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                        if (ent != null && !string.IsNullOrEmpty(ent.Layer)) picked.Add(ent.Layer);
                    }
                    tr.Commit();
                }

                foreach (LayerInfo item in _allLayers)
                {
                    if (picked.Contains(item.Name)) item.Selected = true;
                }

                ApplyFilter();
                RefreshTreeCheckStates();
                WriteStatus("已从拾取对象中勾选 " + picked.Count + " 个图层。 ");
            }
            catch (Exception ex)
            {
                WriteStatus("拾取失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "拾取失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SelectObjects()
        {
            try
            {
                List<string> names = GetCheckedLayerNames(true);
                LayerOperationResult result = LayerManagerService.SelectObjectsByLayers(_doc, names);
                WriteStatus(result.Message);
            }
            catch (Exception ex)
            {
                WriteStatus("选中对象失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "选中对象失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteObjects()
        {
            try
            {
                List<string> names = GetCheckedLayerNames(true);
                if (names.Count == 0)
                {
                    WriteStatus("请先勾选图层。 ");
                    return;
                }

                DialogResult confirm = MessageBox.Show(
                    this,
                    "将删除所选图层上的对象，不删除图层本身。\n\n图层数量：" + names.Count + "\n是否继续？",
                    "确认删除对象",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (confirm != DialogResult.Yes) return;

                LayerOperationResult result = LayerManagerService.DeleteObjectsByLayers(_doc, names, _chkForceUnlockDelete.Checked);
                WriteStatus(result.Message);
                RefreshLayers(true);
            }
            catch (Exception ex)
            {
                WriteStatus("删除失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyState(LayerStateAction action, bool refreshAfter)
        {
            try
            {
                List<string> names = GetCheckedLayerNames(true);
                LayerOperationResult result = LayerManagerService.SetLayerState(_doc, names, action);
                WriteStatus(result.Message);
                if (refreshAfter) RefreshLayers(true);
            }
            catch (Exception ex)
            {
                WriteStatus("处理失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "处理失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowSelectedLayers(object sender, EventArgs e)
        {
            ApplyState(LayerStateAction.TurnOn, false);
            ApplyState(LayerStateAction.Thaw, true);
        }

        private void SetCurrentLayer(object sender, EventArgs e)
        {
            try
            {
                string name = GetFirstCheckedLayerName();
                LayerOperationResult result = LayerManagerService.SetCurrentLayer(_doc, name);
                WriteStatus(result.Message);
                RefreshLayers(true);
            }
            catch (Exception ex)
            {
                WriteStatus("设置当前图层失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "设置当前图层失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnlyShowCheckedLayers(object sender, EventArgs e)
        {
            try
            {
                List<string> names = GetCheckedLayerNames(true);
                LayerOperationResult result = LayerManagerService.SetOnlySelectedLayersVisible(_doc, names);
                WriteStatus(result.Message);
                RefreshLayers(true);
            }
            catch (Exception ex)
            {
                WriteStatus("仅显示失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "仅显示失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowAllLayers(object sender, EventArgs e)
        {
            try
            {
                LayerOperationResult result = LayerManagerService.ShowAndThawAllLayers(_doc);
                WriteStatus(result.Message);
                RefreshLayers(true);
            }
            catch (Exception ex)
            {
                WriteStatus("显示全部失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "显示全部失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CreateLayers(object sender, EventArgs e)
        {
            using (var dialog = new CreateLayersForm())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    List<string> names = LayerManagerService.ParseLayerNames(dialog.LayerNameText);
                    LayerOperationResult result = LayerManagerService.CreateLayers(_doc, names, dialog.ColorIndex);
                    WriteStatus(result.Message);
                    RefreshLayers(true);
                }
                catch (Exception ex)
                {
                    WriteStatus("新建图层失败：" + ex.Message);
                    MessageBox.Show(this, ex.Message, "新建图层失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void CreateDefaultPipeLayers(object sender, EventArgs e)
        {
            try
            {
                LayerOperationResult result = LayerManagerService.CreateDefaultPipeLayers(_doc);
                WriteStatus(result.Message);
                RefreshLayers(true);
            }
            catch (Exception ex)
            {
                WriteStatus("创建默认图层失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "创建默认图层失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void EditLayerMetadata(object sender, EventArgs e)
        {
            try
            {
                List<string> names = GetCheckedLayerNames(true);
                if (names.Count == 0)
                {
                    WriteStatus("请先勾选需要设置父属性/标签的图层。 ");
                    return;
                }

                LayerInfo first = _allLayers.FirstOrDefault(x => string.Equals(x.Name, names[0], StringComparison.CurrentCultureIgnoreCase));
                string parentGroup = first == null ? string.Empty : first.ParentGroup;
                string parentClass = first == null ? string.Empty : first.ParentClass;
                string tagText = first == null ? string.Empty : first.TagText;

                using (var dialog = new LayerMetadataEditForm(parentGroup, parentClass, tagText, names.Count))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    LayerMetadata metadata = LayerMetadata.FromText(dialog.ParentGroup, dialog.ParentClass, dialog.TagText);
                    LayerOperationResult result = LayerManagerService.SetLayerMetadata(_doc, names, metadata);
                    WriteStatus(result.Message);
                    RefreshLayers(true);
                }
            }
            catch (Exception ex)
            {
                WriteStatus("设置图层属性失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "设置图层属性失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenRecognitionRulesEditor(object sender, EventArgs e)
        {
            try
            {
                using (var dialog = new LayerRecognitionRulesForm(LayerManagerService.LoadRecognitionRules(), LayerManagerService.GetRecognitionRulesFilePath()))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    LayerManagerService.SaveRecognitionRules(dialog.Rules);
                    WriteStatus("属性识别表已保存。之后自动识别属性将按该表执行。 ");
                }
            }
            catch (Exception ex)
            {
                WriteStatus("打开属性识别表失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "属性识别表", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AutoInferLayerMetadata(object sender, EventArgs e)
        {
            try
            {
                List<string> names = GetCheckedLayerNames(true);
                if (names.Count == 0)
                {
                    WriteStatus("请先勾选需要自动识别属性的图层。 ");
                    return;
                }

                DialogResult overwrite = MessageBox.Show(
                    this,
                    "是否覆盖已存在的父属性/标签？\n\n选择“是”：根据属性识别表重新识别并覆盖。\n选择“否”：只给空属性图层补全。\n\n可点击“属性识别表”自定义识别规则。",
                    "自动识别图层属性",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (overwrite == DialogResult.Cancel) return;

                LayerOperationResult result = LayerManagerService.AutoInferLayerMetadata(_doc, names, overwrite == DialogResult.Yes);
                WriteStatus(result.Message);
                RefreshLayers(true);
            }
            catch (Exception ex)
            {
                WriteStatus("自动识别属性失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "自动识别属性失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WriteStatus(string message)
        {
            if (_lblStatus != null) _lblStatus.Text = message;
            try
            {
                _doc.Editor.WriteMessage("\n[图层管理] " + message);
            }
            catch
            {
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // LayerManagerForm
            // 
            this.ClientSize = new System.Drawing.Size(469, 437);
            this.Name = "LayerManagerForm";
            this.ResumeLayout(false);

        }
    }

    internal sealed class LayerTreeNodeTag
    {
        public string NodeKind { get; private set; }
        public string ParentGroup { get; private set; }
        public string ParentClass { get; private set; }
        public string LayerName { get; private set; }

        private LayerTreeNodeTag()
        {
            NodeKind = string.Empty;
            ParentGroup = string.Empty;
            ParentClass = string.Empty;
            LayerName = string.Empty;
        }

        public static LayerTreeNodeTag CreateGroup(string parentGroup)
        {
            return new LayerTreeNodeTag { NodeKind = "Group", ParentGroup = parentGroup ?? string.Empty };
        }

        public static LayerTreeNodeTag CreateClass(string parentGroup, string parentClass)
        {
            return new LayerTreeNodeTag
            {
                NodeKind = "Class",
                ParentGroup = parentGroup ?? string.Empty,
                ParentClass = parentClass ?? string.Empty
            };
        }

        public static LayerTreeNodeTag CreateLayer(string layerName)
        {
            return new LayerTreeNodeTag { NodeKind = "Layer", LayerName = layerName ?? string.Empty };
        }
    }

    internal class LayerRecognitionRulesForm : Form
    {
        private BindingList<LayerRecognitionRule> _rules;
        private DataGridView _grid;
        private Label _lblPath;

        public List<LayerRecognitionRule> Rules
        {
            get { return _rules == null ? new List<LayerRecognitionRule>() : _rules.Select(r => r == null ? new LayerRecognitionRule() : r.Clone()).ToList(); }
        }

        public LayerRecognitionRulesForm(IEnumerable<LayerRecognitionRule> rules, string filePath)
        {
            Text = "属性识别表";
            Width = 1080;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;

            _rules = new BindingList<LayerRecognitionRule>((rules == null ? LayerManagerService.GetDefaultRecognitionRules() : rules.Select(r => r == null ? new LayerRecognitionRule() : r.Clone())).ToList());
            BuildUi(filePath ?? string.Empty);
        }

        private void BuildUi(string filePath)
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(10);
            root.RowCount = 4;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var tip = new Label();
            tip.AutoSize = true;
            tip.Dock = DockStyle.Fill;
            tip.Text = "自动识别属性将按本表从上到下匹配。可用变量：{LayerName} 图层名、{BaseName} 去括号主名、{Bracket} 括号内容、{LeadingNumber} 前置数字、{DN}、{PipeMaterial}。";
            root.Controls.Add(tip, 0, 0);

            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = true;
            _grid.EditMode = DataGridViewEditMode.EditOnEnter;
            _grid.CurrentCellDirtyStateChanged += delegate
            {
                if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.DataError += delegate { };
            BuildRuleColumns();
            _grid.DataSource = _rules;
            root.Controls.Add(_grid, 0, 1);

            var actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.AutoSize = true;
            actions.WrapContents = true;
            actions.Padding = new Padding(0, 6, 0, 4);
            actions.Controls.Add(MakeButton("添加规则", AddRule));
            actions.Controls.Add(MakeButton("删除规则", DeleteRules));
            actions.Controls.Add(MakeButton("上移", MoveRuleUp));
            actions.Controls.Add(MakeButton("下移", MoveRuleDown));
            actions.Controls.Add(MakeButton("恢复默认表", RestoreDefaultRules));
            root.Controls.Add(actions, 0, 2);

            var bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.ColumnCount = 2;
            bottom.RowCount = 1;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.Controls.Add(bottom, 0, 3);

            _lblPath = new Label();
            _lblPath.AutoSize = true;
            _lblPath.Text = "保存位置：" + filePath;
            _lblPath.ForeColor = System.Drawing.SystemColors.GrayText;
            bottom.Controls.Add(_lblPath, 0, 0);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Right;
            buttons.AutoSize = true;
            var ok = new Button { Text = "保存", DialogResult = DialogResult.OK, AutoSize = true };
            ok.Click += delegate { if (_grid != null) _grid.EndEdit(); };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            bottom.Controls.Add(buttons, 1, 0);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void BuildRuleColumns()
        {
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "启用",
                DataPropertyName = "Enabled",
                Width = 55
            });

            var modeColumn = new DataGridViewComboBoxColumn();
            modeColumn.HeaderText = "匹配方式";
            modeColumn.DataPropertyName = "MatchMode";
            modeColumn.Width = 90;
            modeColumn.Items.Add(LayerManagerService.MatchModeExact);
            modeColumn.Items.Add(LayerManagerService.MatchModeContains);
            modeColumn.Items.Add(LayerManagerService.MatchModeWildcard);
            modeColumn.Items.Add(LayerManagerService.MatchModeRegex);
            _grid.Columns.Add(modeColumn);

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "匹配内容",
                DataPropertyName = "Pattern",
                Width = 230
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "父属性",
                DataPropertyName = "ParentGroup",
                Width = 110
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "分类",
                DataPropertyName = "ParentClass",
                Width = 150
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "标签",
                DataPropertyName = "TagText",
                Width = 260
            });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "命中后停止",
                DataPropertyName = "StopAfterMatch",
                Width = 95
            });
        }

        private Button MakeButton(string text, EventHandler handler)
        {
            var button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Margin = new Padding(3);
            button.Click += handler;
            return button;
        }

        private void AddRule(object sender, EventArgs e)
        {
            _rules.Add(new LayerRecognitionRule());
            if (_grid != null && _grid.Rows.Count > 0)
            {
                _grid.ClearSelection();
                int index = _grid.Rows.Count - 1;
                _grid.Rows[index].Selected = true;
                _grid.CurrentCell = _grid.Rows[index].Cells[0];
            }
        }

        private void DeleteRules(object sender, EventArgs e)
        {
            if (_grid == null || _grid.SelectedRows.Count == 0) return;
            var indices = new List<int>();
            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                if (row.Index >= 0 && row.Index < _rules.Count) indices.Add(row.Index);
            }
            indices.Sort();
            indices.Reverse();
            foreach (int index in indices) _rules.RemoveAt(index);
        }

        private void MoveRuleUp(object sender, EventArgs e)
        {
            MoveCurrentRule(-1);
        }

        private void MoveRuleDown(object sender, EventArgs e)
        {
            MoveCurrentRule(1);
        }

        private void MoveCurrentRule(int offset)
        {
            if (_grid == null || _grid.CurrentRow == null) return;
            int index = _grid.CurrentRow.Index;
            int target = index + offset;
            if (index < 0 || index >= _rules.Count || target < 0 || target >= _rules.Count) return;

            LayerRecognitionRule item = _rules[index];
            _rules.RemoveAt(index);
            _rules.Insert(target, item);
            _grid.ClearSelection();
            _grid.Rows[target].Selected = true;
            _grid.CurrentCell = _grid.Rows[target].Cells[0];
        }

        private void RestoreDefaultRules(object sender, EventArgs e)
        {
            DialogResult confirm = MessageBox.Show(this, "恢复默认属性识别表会覆盖当前编辑内容，是否继续？", "恢复默认表", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;
            _rules = new BindingList<LayerRecognitionRule>(LayerManagerService.GetDefaultRecognitionRules().Select(r => r.Clone()).ToList());
            _grid.DataSource = _rules;
        }
    }

    internal class CreateLayersForm : Form
    {
        private TextBox _txtNames;
        private NumericUpDown _numColor;

        public string LayerNameText
        {
            get { return _txtNames.Text; }
        }

        public short ColorIndex
        {
            get { return (short)_numColor.Value; }
        }

        public CreateLayersForm()
        {
            Text = "新建图层";
            Width = 460;
            Height = 320;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(10);
            root.RowCount = 4;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "输入图层名。支持一行一个，也支持逗号/分号分隔：",
                AutoSize = true
            }, 0, 0);

            _txtNames = new TextBox();
            _txtNames.Multiline = true;
            _txtNames.ScrollBars = ScrollBars.Vertical;
            _txtNames.Dock = DockStyle.Fill;
            root.Controls.Add(_txtNames, 0, 1);

            var colorPanel = new FlowLayoutPanel();
            colorPanel.Dock = DockStyle.Fill;
            colorPanel.AutoSize = true;
            colorPanel.Controls.Add(new Label { Text = "颜色 ACI：", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            _numColor = new NumericUpDown();
            _numColor.Minimum = 1;
            _numColor.Maximum = 255;
            _numColor.Value = 7;
            _numColor.Width = 80;
            colorPanel.Controls.Add(_numColor);
            root.Controls.Add(colorPanel, 0, 2);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Right;
            buttons.AutoSize = true;
            var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 3);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }

    internal class LayerMetadataEditForm : Form
    {
        private TextBox _txtParentGroup;
        private TextBox _txtParentClass;
        private TextBox _txtTags;

        public string ParentGroup
        {
            get { return _txtParentGroup.Text == null ? string.Empty : _txtParentGroup.Text.Trim(); }
        }

        public string ParentClass
        {
            get { return _txtParentClass.Text == null ? string.Empty : _txtParentClass.Text.Trim(); }
        }

        public string TagText
        {
            get { return _txtTags.Text == null ? string.Empty : _txtTags.Text.Trim(); }
        }

        public LayerMetadataEditForm(string parentGroup, string parentClass, string tagText, int layerCount)
        {
            Text = "设置图层父属性/标签";
            Width = 650;
            Height = 420;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.RowCount = 5;
            root.ColumnCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var tip = new Label();
            tip.Text = "将为 " + layerCount + " 个图层写入 CDBox 元数据。父属性用于主归属，分类用于规格/类型，标签用于横向筛选。";
            tip.AutoSize = true;
            tip.Dock = DockStyle.Fill;
            root.Controls.Add(tip, 0, 0);
            root.SetColumnSpan(tip, 2);

            _txtParentGroup = new TextBox();
            _txtParentGroup.Text = parentGroup ?? string.Empty;
            AddRow(root, 1, "父属性", _txtParentGroup, "例如：主管、支管、注记、道路、构筑物");

            _txtParentClass = new TextBox();
            _txtParentClass.Text = parentClass ?? string.Empty;
            AddRow(root, 2, "分类", _txtParentClass, "例如：110PVC管、75PVC管、管线长度注记");

            _txtTags = new TextBox();
            _txtTags.Text = tagText ?? string.Empty;
            _txtTags.Multiline = true;
            _txtTags.ScrollBars = ScrollBars.Vertical;
            AddRow(root, 3, "标签", _txtTags, "多个标签可用顿号、逗号、分号分隔，例如：明管、PVC、DN110");

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Right;
            buttons.AutoSize = true;
            var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 4);
            root.SetColumnSpan(buttons, 2);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private static void AddRow(TableLayoutPanel root, int row, string labelText, Control control, string placeholderTip)
        {
            var label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Dock = DockStyle.Fill;
            label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            label.Padding = new Padding(0, 6, 8, 4);
            root.Controls.Add(label, 0, row);

            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.RowCount = 2;
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            control.Dock = DockStyle.Fill;
            panel.Controls.Add(control, 0, 0);

            var tip = new Label();
            tip.Text = placeholderTip;
            tip.AutoSize = true;
            tip.ForeColor = System.Drawing.SystemColors.GrayText;
            panel.Controls.Add(tip, 0, 1);

            root.Controls.Add(panel, 1, row);
        }
    }

}
