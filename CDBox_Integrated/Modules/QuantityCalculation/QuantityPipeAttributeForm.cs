using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 工程量属性编辑器：支持主管、支管、节点/检查井属性写入与默认表维护。
    /// </summary>
    public sealed class QuantityPipeAttributeForm : Form
    {
        private readonly Document _doc;
        private ObjectId _currentObjectId;
        private string _currentLayerName;
        private double _currentCadLength;
        private QuantityPipeAttributes _attributes;

        private Label _lblObject;
        private Label _lblLayer;
        private Label _lblCadLength;
        private Label _lblStatus;
        private ComboBox _cmbKind;
        private PropertyGrid _grid;
        private TextBox _txtLog;
        private CheckBox _chkBatchKeepIdentity;
        private bool _loadingKind;

        public QuantityPipeAttributeForm(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            _doc = doc;
            _currentObjectId = ObjectId.Null;
            _currentLayerName = string.Empty;
            _currentCadLength = 0.0;
            _attributes = QuantityPipeAttributes.DefaultMainPipe.Clone();

            Text = "属性编辑器（主管 / 节点井 / 支管）";
            Width = 900;
            Height = 760;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;

            BuildUi();
            LoadToUi(_attributes);
            TryLoadPickfirstSelection();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.ColumnCount = 1;
            root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var title = new Label();
            title.Text = "属性编辑器：点选主管、节点/检查井或支管，按图层父属性识别类型并写入统计属性";
            title.AutoSize = true;
            title.Font = new System.Drawing.Font(title.Font.FontFamily, 11, System.Drawing.FontStyle.Bold);
            title.Padding = new Padding(0, 0, 0, 8);
            root.Controls.Add(title, 0, 0);

            var info = new TableLayoutPanel();
            info.Dock = DockStyle.Top;
            info.AutoSize = true;
            info.ColumnCount = 4;
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.Controls.Add(info, 0, 1);

            _lblObject = MakeValueLabel();
            _lblLayer = MakeValueLabel();
            _lblCadLength = MakeValueLabel();
            _lblStatus = MakeValueLabel();
            AddInfoRow(info, 0, "当前对象", _lblObject, "当前图层", _lblLayer);
            AddInfoRow(info, 1, "管线长度", _lblCadLength, "状态", _lblStatus);

            var kindPanel = new FlowLayoutPanel();
            kindPanel.Dock = DockStyle.Fill;
            kindPanel.AutoSize = true;
            kindPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            kindPanel.Controls.Add(MakeInlineLabel("对象类型"));
            _cmbKind = new ComboBox();
            _cmbKind.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbKind.Width = 160;
            _cmbKind.Items.Add(QuantityPipeAttributes.KindMainPipe);
            _cmbKind.Items.Add(QuantityPipeAttributes.KindNodeWell);
            _cmbKind.Items.Add(QuantityPipeAttributes.KindBranchPipe);
            _cmbKind.SelectedIndexChanged += delegate { ChangeKindFromCombo(); };
            kindPanel.Controls.Add(_cmbKind);
            kindPanel.Controls.Add(MakeInlineLabel("  提示：SX 命令可直接打开本编辑器，若先选中对象再输入 SX，会自动读取该对象。"));
            root.Controls.Add(kindPanel, 0, 2);

            _grid = new PropertyGrid();
            _grid.Dock = DockStyle.Fill;
            _grid.ToolbarVisible = false;
            _grid.HelpVisible = true;
            _grid.PropertySort = PropertySort.Categorized;
            _grid.PropertyValueChanged += delegate { RefreshComputedInfo(); };
            root.Controls.Add(_grid, 0, 3);

            _txtLog = new TextBox();
            _txtLog.Multiline = true;
            _txtLog.ReadOnly = true;
            _txtLog.ScrollBars = ScrollBars.Vertical;
            _txtLog.Height = 95;
            _txtLog.Dock = DockStyle.Fill;
            _txtLog.Text = "流程：① 选择对象并读取；② 按父属性识别并填默认；③ 修改属性；④ 保存到当前对象。\r\n"
                + "默认表分为主管、节点/检查井、支管三类，保存默认表后，未设置过属性的对象会自动套用对应默认值。\r\n"
                + "主管起终点可从附近已设置属性的节点/检查井读取编号和井深，属于非强绑定，读取后可手动修改。";
            root.Controls.Add(_txtLog, 0, 4);

            var bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.AutoSize = true;
            bottom.ColumnCount = 1;
            bottom.RowCount = 2;
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(bottom, 0, 5);

            _chkBatchKeepIdentity = new CheckBox();
            _chkBatchKeepIdentity.Text = "批量赋值时保留各对象已有编号/起终点/深度/手动长度";
            _chkBatchKeepIdentity.Checked = true;
            _chkBatchKeepIdentity.AutoSize = true;
            bottom.Controls.Add(_chkBatchKeepIdentity, 0, 0);

            var buttons = new FlowLayoutPanel();
            buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            bottom.Controls.Add(buttons, 0, 1);

            AddButton(buttons, "关闭", 80, delegate { Close(); });
            AddButton(buttons, "批量赋值", 100, delegate { BatchWriteAttributes(); });
            AddButton(buttons, "确认", 90, delegate { SaveCurrentAttributes(); Close(); });
            AddButton(buttons, "载入当前类型默认表", 150, delegate { LoadCurrentDefaultProfile(); });
            AddButton(buttons, "保存为当前类型默认表", 165, delegate { SaveCurrentDefaultProfile(); });
            AddButton(buttons, "按父属性识别并填默认", 165, delegate { ApplyDefaults(); });
            AddButton(buttons, "选择对象并读取", 130, delegate { SelectObject(); });
        }

        private void TryLoadPickfirstSelection()
        {
            try
            {
                QuantityPipeSelectionInfo info = QuantityPipeAttributeService.ReadFirstImpliedOrPrompt(_doc);
                if (info != null)
                {
                    LoadSelection(info);
                }
            }
            catch
            {
                RefreshComputedInfo();
            }
        }

        private void SelectObject()
        {
            try
            {
                Hide();
                QuantityPipeSelectionInfo info = QuantityPipeAttributeService.SelectQuantityObjectAndRead(_doc);
                Show();
                if (info == null) return;
                LoadSelection(info);
            }
            catch (System.Exception ex)
            {
                Show();
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "选择对象失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadSelection(QuantityPipeSelectionInfo info)
        {
            if (info == null) return;
            _currentObjectId = info.ObjectId;
            _currentLayerName = info.LayerName ?? string.Empty;
            _currentCadLength = info.CadLength;
            _attributes = info.Attributes == null ? QuantityPipeAttributes.DefaultForKind(info.InferredKind) : info.Attributes.Clone();
            if (string.IsNullOrWhiteSpace(_attributes.ObjectKind)) _attributes.ObjectKind = info.InferredKind;
            LoadToUi(_attributes);
            _lblObject.Text = info.ObjectTypeName + " / Handle " + info.HandleText;
            _lblLayer.Text = _currentLayerName;
            _lblStatus.Text = info.HasSavedAttributes ? "已读取已有属性" : "未设置属性，已套用默认表";
            AppendLog("已读取对象：" + _lblObject.Text + "，识别类型：" + _attributes.ObjectKind + "。");
        }

        private void LoadToUi(QuantityPipeAttributes attrs)
        {
            _attributes = attrs == null ? QuantityPipeAttributes.DefaultMainPipe.Clone() : attrs.Clone();
            SetComboKind(_attributes.ObjectKind);
            _grid.SelectedObject = _attributes;
            RefreshComputedInfo();
        }

        private void SetComboKind(string kind)
        {
            if (_cmbKind == null) return;
            string normalized = NormalizeKind(kind);
            if (_cmbKind.SelectedItem != null && string.Equals(_cmbKind.SelectedItem.ToString(), normalized, StringComparison.CurrentCultureIgnoreCase)) return;
            _loadingKind = true;
            try
            {
                for (int i = 0; i < _cmbKind.Items.Count; i++)
                {
                    if (string.Equals(_cmbKind.Items[i].ToString(), normalized, StringComparison.CurrentCultureIgnoreCase))
                    {
                        _cmbKind.SelectedIndex = i;
                        break;
                    }
                }
                if (_cmbKind.SelectedIndex < 0) _cmbKind.SelectedIndex = 0;
            }
            finally
            {
                _loadingKind = false;
            }
        }

        private void ChangeKindFromCombo()
        {
            if (_loadingKind || _attributes == null || _cmbKind.SelectedItem == null) return;
            string kind = _cmbKind.SelectedItem.ToString();
            if (string.Equals(_attributes.ObjectKind, kind, StringComparison.CurrentCultureIgnoreCase)) return;
            _attributes.ObjectKind = kind;
            RefreshComputedInfo();
            _grid.Refresh();
        }

        private string NormalizeKind(string kind)
        {
            if (QuantityPipeAttributes.IsNodeKind(kind)) return QuantityPipeAttributes.KindNodeWell;
            if (QuantityPipeAttributes.IsBranchKind(kind)) return QuantityPipeAttributes.KindBranchPipe;
            return QuantityPipeAttributes.KindMainPipe;
        }

        private void RefreshComputedInfo()
        {
            if (_attributes == null) return;
            _attributes.ObjectKind = NormalizeKind(_attributes.ObjectKind);
            if (_cmbKind != null && _cmbKind.SelectedItem != null && !string.Equals(_cmbKind.SelectedItem.ToString(), _attributes.ObjectKind, StringComparison.CurrentCultureIgnoreCase))
            {
                for (int i = 0; i < _cmbKind.Items.Count; i++)
                {
                    if (string.Equals(_cmbKind.Items[i].ToString(), _attributes.ObjectKind, StringComparison.CurrentCultureIgnoreCase))
                    {
                        _cmbKind.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (_lblCadLength != null)
            {
                _lblCadLength.Text = _currentCadLength > 0
                    ? _currentCadLength.ToString("0.00") + " m / 统计 " + _attributes.EffectiveLength(_currentCadLength).ToString("0.00") + " m"
                    : "--";
            }
            if (_grid != null) _grid.Refresh();
        }

        private QuantityPipeAttributes ReadFromUi()
        {
            if (_grid != null && _grid.SelectedObject is QuantityPipeAttributes)
            {
                _attributes = ((QuantityPipeAttributes)_grid.SelectedObject).Clone();
            }
            if (_cmbKind != null && _cmbKind.SelectedItem != null) _attributes.ObjectKind = _cmbKind.SelectedItem.ToString();
            return _attributes.Clone();
        }

        private void ApplyDefaults()
        {
            try
            {
                QuantityPipeAttributes attrs = ReadFromUi();
                if (_currentObjectId.IsNull) attrs = QuantityPipeAttributeService.ApplySmartDefaults(_doc, _currentLayerName, attrs);
                else attrs = QuantityPipeAttributeService.ApplySmartDefaults(_doc, _currentObjectId, attrs);
                LoadToUi(attrs);
                AppendLog("已按图层父属性/分类/标签重新识别并填入默认值。当前类型：" + attrs.ObjectKind + "。");
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "填充默认值失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveCurrentAttributes()
        {
            if (_currentObjectId.IsNull)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("请先选择对象。", "属性编辑器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                QuantityPipeWriteResult result = QuantityPipeAttributeService.WritePipeAttributes(_doc, _currentObjectId, ReadFromUi());
                AppendLog(result.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(result.Message, "属性编辑器", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BatchWriteAttributes()
        {
            try
            {
                Hide();
                QuantityPipeWriteResult result;
                using (var progress = CDBoxProgressSession.Start(_doc,
                    "属性批量赋值", "正在批量写入属性…",
                    "QuantityAttributes", "quantity-batch-write-progress"))
                {
                    result = QuantityPipeAttributeService.ApplyToSelection(_doc, ReadFromUi(), _chkBatchKeepIdentity.Checked, progress.Report);
                    progress.Complete(result.Message);
                }
                Show();
                AppendLog(result.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(result.Message, "属性编辑器", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (System.Exception ex)
            {
                Show();
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "批量赋值失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveCurrentDefaultProfile()
        {
            try
            {
                QuantityPipeAttributes attrs = ReadFromUi();
                QuantityPipeAttributeService.SaveDefaultProfile(attrs.ObjectKind, attrs);
                AppendLog("已保存“" + attrs.ObjectKind + "”默认表。对象编号、起终点、手动长度不会作为默认值保存。格式用于后续未设置对象自动填充。");
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("已保存“" + attrs.ObjectKind + "”默认表。", "属性编辑器", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "保存默认表失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadCurrentDefaultProfile()
        {
            try
            {
                string kind = _cmbKind.SelectedItem == null ? QuantityPipeAttributes.KindMainPipe : _cmbKind.SelectedItem.ToString();
                QuantityPipeAttributes defaults = QuantityPipeAttributeService.LoadDefaultProfile(kind);
                defaults.ObjectKind = NormalizeKind(kind);
                if (!string.IsNullOrWhiteSpace(_attributes.LayerParentGroup)) defaults.LayerParentGroup = _attributes.LayerParentGroup;
                if (!string.IsNullOrWhiteSpace(_attributes.LayerParentClass)) defaults.LayerParentClass = _attributes.LayerParentClass;
                if (!string.IsNullOrWhiteSpace(_attributes.LayerTags)) defaults.LayerTags = _attributes.LayerTags;
                LoadToUi(defaults);
                AppendLog("已载入“" + defaults.ObjectKind + "”默认表。可继续修改后保存到对象。 ");
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "载入默认表失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AppendLog(string text)
        {
            if (_txtLog == null) return;
            _txtLog.AppendText("\r\n" + DateTime.Now.ToString("HH:mm:ss") + "  " + text);
        }

        private Label MakeValueLabel()
        {
            var label = new Label();
            label.AutoSize = true;
            label.Dock = DockStyle.Fill;
            label.Text = "--";
            label.Padding = new Padding(0, 3, 0, 3);
            return label;
        }

        private Label MakeInlineLabel(string text)
        {
            var label = new Label();
            label.AutoSize = true;
            label.Text = text;
            label.Padding = new Padding(0, 6, 6, 0);
            return label;
        }

        private void AddInfoRow(TableLayoutPanel table, int row, string label1, Control value1, string label2, Control value2)
        {
            while (table.RowStyles.Count <= row) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(MakeInfoLabel(label1), 0, row);
            table.Controls.Add(value1, 1, row);
            table.Controls.Add(MakeInfoLabel(label2), 2, row);
            table.Controls.Add(value2, 3, row);
        }

        private Label MakeInfoLabel(string text)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Padding = new Padding(0, 3, 0, 3);
            return label;
        }

        private void AddButton(FlowLayoutPanel panel, string text, int width, EventHandler click)
        {
            var button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 30;
            button.Click += click;
            panel.Controls.Add(button);
        }
    }
}
