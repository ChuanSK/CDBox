using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using TCPipeAutoDraw.Core.Colors;
using TCPipeAutoDraw.UI.Controls;

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

            Text = "CDBox - ??????";
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

            top.Controls.Add(new Label { Text = "???", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            _txtFilter = new TextBox { Width = 220 };
            _txtFilter.TextChanged += delegate { ApplyFilter(); };
            top.Controls.Add(_txtFilter);

            top.Controls.Add(new Label { Text = "???", AutoSize = true, Padding = new Padding(12, 6, 0, 0) });
            _cboTagSearch = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDown };
            _cboTagSearch.KeyDown += TagSearchKeyDown;
            top.Controls.Add(_cboTagSearch);
            top.Controls.Add(MakeButton("????", delegate { SelectLayersByTag(false); }));
            top.Controls.Add(MakeButton("?????", delegate { SelectLayersByTag(true); }));

            top.Controls.Add(MakeButton("??", delegate { RefreshLayers(true); }));
            top.Controls.Add(MakeButton("????????", PickLayersFromObjects));
            top.Controls.Add(MakeButton("??", delegate { SetAllVisibleSelected(true); }));
            top.Controls.Add(MakeButton("??", InvertVisibleSelected));
            top.Controls.Add(MakeButton("????", delegate { SetAllVisibleSelected(false); }));

            _chkCountObjects = new CheckBox { Text = "?????", AutoSize = true, Checked = true, Padding = new Padding(12, 4, 0, 0) };
            _chkCountObjects.CheckedChanged += delegate { RefreshLayers(false); };
            top.Controls.Add(_chkCountObjects);

            _chkForceUnlockDelete = new CheckBox { Text = "???????", AutoSize = true, Checked = false, Padding = new Padding(12, 4, 0, 0) };
            top.Controls.Add(_chkForceUnlockDelete);

            _mainSplit = new SplitContainer();
            var split = _mainSplit;
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.FixedPanel = FixedPanel.Panel1;
            // ????????? Panel1MinSize/Panel2MinSize/SplitterDistance?
            // SplitContainer ?????? Width ?????????????? SplitterDistance ?????
            root.Controls.Add(split, 0, 1);

            var treeGroup = new GroupBox();
            treeGroup.Text = "??? / ??";
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

            actions.Controls.Add(MakeButton("????", delegate { SelectObjects(); }));
            actions.Controls.Add(MakeButton("????", delegate { DeleteObjects(); }));
            actions.Controls.Add(MakeButton("????", delegate { ApplyState(LayerStateAction.Lock, true); }));
            actions.Controls.Add(MakeButton("????", delegate { ApplyState(LayerStateAction.Unlock, true); }));
            actions.Controls.Add(MakeButton("????", delegate { ApplyState(LayerStateAction.TurnOff, true); }));
            actions.Controls.Add(MakeButton("??/??", ShowSelectedLayers));
            actions.Controls.Add(MakeButton("????", delegate { ApplyState(LayerStateAction.Freeze, true); }));
            actions.Controls.Add(MakeButton("????", delegate { ApplyState(LayerStateAction.Thaw, true); }));
            actions.Controls.Add(MakeButton("????", SetCurrentLayer));
            actions.Controls.Add(MakeButton("?????", OnlyShowCheckedLayers));
            actions.Controls.Add(MakeButton("????", ShowAllLayers));
            actions.Controls.Add(MakeButton("????", CreateLayers));
            actions.Controls.Add(MakeButton("???????", CreateDefaultPipeLayers));
            actions.Controls.Add(MakeButton("????/??", EditLayerMetadata));
            actions.Controls.Add(MakeButton("?????", OpenRecognitionRulesEditor));
            actions.Controls.Add(MakeButton("??????", AutoInferLayerMetadata));
            actions.Controls.Add(MakeButton("??", delegate { Close(); }));

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

            // ????? Panel1MinSize / Panel2MinSize ??????
            // ?? SplitContainer ??? DPI?????????AutoCAD ??????
            // ?? MinSize ?????? Width ????????
            // ??????????????????????????
            int width = _mainSplit.ClientSize.Width;
            int splitterWidth = _mainSplit.SplitterWidth;
            if (width <= splitterWidth + 80) return;

            int minDistance = Math.Max(25, _mainSplit.Panel1MinSize);
            int maxDistance = width - splitterWidth - Math.Max(25, _mainSplit.Panel2MinSize);
            if (maxDistance < minDistance) return;

            int target = 286; // ?2??????? 280px????????????
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
                // AutoCAD ?????????????????????
                // ??????????????????????
            }
        }

        private void BuildGridColumns()
        {
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "??",
                DataPropertyName = "Selected",
                Width = 56
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "???",
                DataPropertyName = "Name",
                Width = 300,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "???",
                DataPropertyName = "ParentGroup",
                Width = 90,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "??",
                DataPropertyName = "ParentClass",
                Width = 130,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "??",
                DataPropertyName = "TagText",
                Width = 190,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "??",
                DataPropertyName = "StatusText",
                Width = 110,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "???",
                DataPropertyName = "ObjectCount",
                Width = 70,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "??",
                DataPropertyName = "ColorIndex",
                Width = 60,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "??",
                DataPropertyName = "Linetype",
                Width = 120,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "??",
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
                WriteStatus("????????? " + _allLayers.Count + " ???????????/?????????????????????");
            }
            catch (Exception ex)
            {
                WriteStatus("?????" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, ex.Message, "????", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.ParentGroup) ? "??????" : x.ParentGroup.Trim())
                    .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);

                foreach (var group in groups)
                {
                    TreeNode groupNode = new TreeNode(group.Key + " (" + group.Count() + ")");
                    groupNode.Tag = LayerTreeNodeTag.CreateGroup(group.Key);
                    _tree.Nodes.Add(groupNode);

                    // ??????????????????????????????????
                    // ??????????????????????????
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
            WriteStatus("?" + (e.Node.Checked ? "??" : "????") + "???? " + layerNames.Count + " ???? ");
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
                WriteStatus("????????? ");
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
            WriteStatus("??????" + keyword + "??? " + count + " ???? ");
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
            WriteStatus(selected ? "????????????" : "????????????????????? ");
        }

        private void InvertVisibleSelected(object sender, EventArgs e)
        {
            foreach (LayerInfo item in _visibleLayers)
            {
                item.Selected = !item.Selected;
            }
            _grid.Refresh();
            RefreshTreeCheckStates();
            WriteStatus("???????? ");
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
                opts.MessageForAdding = "\n????????????????????";

                bool wasVisible = this.Visible;
                try
                {
                    if (wasVisible) this.Hide();
                    _doc.Editor.WriteHudMessage("\n?????????????????? ESC? ");
                    psr = _doc.Editor.GetHudSelection(opts);
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
                    WriteStatus("?????? ");
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
                WriteStatus("????????? " + picked.Count + " ???? ");
            }
            catch (Exception ex)
            {
                WriteStatus("?????" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, ex.Message, "????", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                WriteStatus("???????" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, ex.Message, "??????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteObjects()
        {
            try
            {
                List<string> names = GetCheckedLayerNames(true);
                if (names.Count == 0)
                {
                    WriteStatus("??????? ");
                    return;
                }

                DialogResult confirm = TCPipeAutoDraw.UI.CDBoxMessageBox.Show(
                    this,
                    "????????????????????\n\n?????" + names.Count + "\n?????",
                    "??????",
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
                WriteStatus("?????" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, ex.Message, "????", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                WriteStatus("?????" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, ex.Message, "????", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                WriteStatus("?????????" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, ex.Message, "????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                WriteStatus("??????" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, ex.Message, "?????", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                WriteStatus("???????" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, ex.Message, "??????", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    WriteStatus("???????" + ex.Message);
                    TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, ex.Message, "??????", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                WriteStatus("?????????" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, ex.Message, "????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void EditLayerMetadata(object sender, EventArgs e)
        {
            try
            {
                List<string> names = GetCheckedLayerNames(true);
                if (names.Count == 0)
                {
                    WriteStatus("???????????/?????? ");
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
                WriteStatus("?????????" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, ex.Message, "????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    WriteStatus("???????????????????????? ");
                }
            }
            catch (Exception ex)
            {
                WriteStatus("??????????" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, ex.Message, "?????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AutoInferLayerMetadata(object sender, EventArgs e)
        {
            try
            {
                List<string> names = GetCheckedLayerNames(true);
                if (names.Count == 0)
                {
                    WriteStatus("???????????????? ");
                    return;
                }

                DialogResult overwrite = TCPipeAutoDraw.UI.CDBoxMessageBox.Show(
                    this,
                    "???????????/???\n\n?????????????????????\n????????????????\n\n??????????????????",
                    "????????",
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
                WriteStatus("?????????" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, ex.Message, "????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WriteStatus(string message)
        {
            if (_lblStatus != null) _lblStatus.Text = message;
            try
            {
                _doc.Editor.WriteHudMessage("\n[????] " + message);
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
            Text = "?????";
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
            tip.Text = "??????????????????????{LayerName} ????{BaseName} ??????{Bracket} ?????{LeadingNumber} ?????{DN}?{PipeMaterial}?";
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
            actions.Controls.Add(MakeButton("????", AddRule));
            actions.Controls.Add(MakeButton("????", DeleteRules));
            actions.Controls.Add(MakeButton("??", MoveRuleUp));
            actions.Controls.Add(MakeButton("??", MoveRuleDown));
            actions.Controls.Add(MakeButton("?????", RestoreDefaultRules));
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
            _lblPath.Text = "?????" + filePath;
            _lblPath.ForeColor = System.Drawing.SystemColors.GrayText;
            bottom.Controls.Add(_lblPath, 0, 0);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Right;
            buttons.AutoSize = true;
            var ok = new Button { Text = "??", DialogResult = DialogResult.OK, AutoSize = true };
            ok.Click += delegate { if (_grid != null) _grid.EndEdit(); };
            var cancel = new Button { Text = "??", DialogResult = DialogResult.Cancel, AutoSize = true };
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
                HeaderText = "??",
                DataPropertyName = "Enabled",
                Width = 55
            });

            var modeColumn = new DataGridViewComboBoxColumn();
            modeColumn.HeaderText = "????";
            modeColumn.DataPropertyName = "MatchMode";
            modeColumn.Width = 90;
            modeColumn.Items.Add(LayerManagerService.MatchModeExact);
            modeColumn.Items.Add(LayerManagerService.MatchModeContains);
            modeColumn.Items.Add(LayerManagerService.MatchModeWildcard);
            modeColumn.Items.Add(LayerManagerService.MatchModeKeywords);
            modeColumn.Items.Add(LayerManagerService.MatchModeTemplate);
            modeColumn.Items.Add(LayerManagerService.MatchModeRegex);
            _grid.Columns.Add(modeColumn);

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "????",
                DataPropertyName = "Pattern",
                Width = 230
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "???",
                DataPropertyName = "ParentGroup",
                Width = 110
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "??",
                DataPropertyName = "ParentClass",
                Width = 150
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "??",
                DataPropertyName = "TagText",
                Width = 260
            });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "?????",
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
            DialogResult confirm = TCPipeAutoDraw.UI.CDBoxMessageBox.Show(this, "????????????????????????", "?????", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
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
            Text = "????";
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
                Text = "??????????????????/?????",
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
            colorPanel.Controls.Add(new Label { Text = "???", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            _numColor = new NumericUpDown();
            _numColor.Minimum = 1;
            _numColor.Maximum = 255;
            _numColor.Value = 7;
            _numColor.Width = 80;
            var colorButton = new Button { Width = 220, Height = 31, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            Action updateColorButton = delegate
            {
                CDBoxColor color = CDBoxColor.FromIndex((int)_numColor.Value);
                colorButton.Text = "?  " + color.DisplayName + " ? " + color.RgbText;
                colorButton.ForeColor = color.Index == 7
                    ? System.Drawing.SystemColors.ControlText
                    : System.Drawing.Color.FromArgb(color.R, color.G, color.B);
            };
            _numColor.ValueChanged += delegate { updateColorButton(); };
            colorButton.Click += delegate
            {
                CDBoxColor selected;
                if (!ColorPickerWindow.TryPick(CDBoxColor.FromIndex((int)_numColor.Value), out selected,
                    false, false, false, false, false)) return;
                _numColor.Value = CDBoxColorService.ToCompatibleColorIndex(selected, (short)_numColor.Value);
            };
            updateColorButton();
            colorPanel.Controls.Add(colorButton);
            root.Controls.Add(colorPanel, 0, 2);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Right;
            buttons.AutoSize = true;
            var ok = new Button { Text = "??", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "??", DialogResult = DialogResult.Cancel, AutoSize = true };
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
            Text = "???????/??";
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
            tip.Text = "?? " + layerCount + " ????? CDBox ???????????????????/????????????";
            tip.AutoSize = true;
            tip.Dock = DockStyle.Fill;
            root.Controls.Add(tip, 0, 0);
            root.SetColumnSpan(tip, 2);

            _txtParentGroup = new TextBox();
            _txtParentGroup.Text = parentGroup ?? string.Empty;
            AddRow(root, 1, "???", _txtParentGroup, "??????????????????");

            _txtParentClass = new TextBox();
            _txtParentClass.Text = parentClass ?? string.Empty;
            AddRow(root, 2, "??", _txtParentClass, "???110PVC??75PVC????????");

            _txtTags = new TextBox();
            _txtTags.Text = tagText ?? string.Empty;
            _txtTags.Multiline = true;
            _txtTags.ScrollBars = ScrollBars.Vertical;
            AddRow(root, 3, "??", _txtTags, "???????????????????????PVC?DN110");

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Right;
            buttons.AutoSize = true;
            var ok = new Button { Text = "??", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "??", DialogResult = DialogResult.Cancel, AutoSize = true };
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
