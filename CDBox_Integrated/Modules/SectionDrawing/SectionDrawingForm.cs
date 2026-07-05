﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace TCPipeAutoDraw.Modules.SectionDrawing
{
    public sealed class SectionDrawingForm : Form
    {
        private readonly Document _doc;
        private readonly SectionDrawingOptions _initialOptions;
        private BindingList<SectionLayerOptions> _layers;
        private bool _loading;
        private bool _syncingHeights;
        private int _selectedLayerIndex;

        private TextBox _txtSectionTitle;
        private NumericUpDown _numWidth;
        private NumericUpDown _numTotalHeight;
        private CheckBox _chkLockTotalHeight;
        private ComboBox _cmbTextStyle;
        private ComboBox _cmbUnifiedLayer;
        private ComboBox _cmbDimensionStyle;
        private CheckBox _chkTopDimension;
        private CheckBox _chkBottomDimension;
        private CheckBox _chkRightDimensions;
        private CheckBox _chkTotalHeightDimension;
        private CheckBox _chkDrawTitle;
        private Panel _leftLayerContainer;
        private Panel _rightLayerContainer;
        private SectionPreviewControl _preview;
       //private Label _lblStatus;
        private readonly Dictionary<int, Label> _rightLayerTitleLabels = new Dictionary<int, Label>();
        private readonly Dictionary<int, Label> _leftHatchSummaryLabels = new Dictionary<int, Label>();
        private readonly Dictionary<int, Label> _rightPipeSummaryLabels = new Dictionary<int, Label>();

        public SectionDrawingForm(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            _doc = doc;
            _initialOptions = SectionDrawingSettingsStore.Load();
            _layers = new BindingList<SectionLayerOptions>();
            foreach (SectionLayerOptions layer in _initialOptions.Layers)
            {
                _layers.Add(layer.Clone());
            }

            _selectedLayerIndex = 0;

            Text = "断面图生成（制作：氚）";
            Width = 1220;
            Height = 820;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;

            BuildUi();
            LoadInitialValues();
            NormalizeUnavailableHatchPatterns();
            RebuildLayerPanels();
            UpdatePreview();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                SectionDrawingSettingsStore.Save(ReadOptions(false));
            }
            catch { }
            base.OnFormClosing(e);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(10);
            root.ColumnCount = 1;
            root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            root.Controls.Add(BuildTopGroup(), 0, 0);
            root.Controls.Add(BuildMiddleGroup(), 0, 1);
            root.Controls.Add(BuildTitleGroup(), 0, 2);
            root.Controls.Add(BuildOtherGroup(), 0, 3);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            buttons.AutoSize = true;
            root.Controls.Add(buttons, 0, 5);

            var close = new Button();
            close.Text = "关闭";
            close.AutoSize = true;
            close.Click += delegate { Close(); };
            buttons.Controls.Add(close);

            var draw = new Button();
            draw.Text = "绘制断面";
            draw.Width = 150;
            draw.Height = 30;
            draw.Click += delegate { RunDraw(); };
            buttons.Controls.Add(draw);
        }

        private Control BuildTopGroup()
        {
            var top = new TableLayoutPanel();
            top.Dock = DockStyle.Top;
            top.AutoSize = true;
            top.ColumnCount = 2;
            top.RowCount = 1;
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

            top.Controls.Add(BuildLayerActionGroup(), 0, 0);
            top.Controls.Add(BuildWidthGroup(), 1, 0);
            return top;
        }

        private Control BuildLayerActionGroup()
        {
            var group = new GroupBox();
            group.Text = "层级操作";
            group.Dock = DockStyle.Fill;
            group.AutoSize = true;

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Top;
            buttons.AutoSize = true;
            buttons.WrapContents = false;
            buttons.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            buttons.Padding = new Padding(8);
            group.Controls.Add(buttons);

            buttons.Controls.Add(MakeLayerButton("添加层", AddLayer));
            buttons.Controls.Add(MakeLayerButton("删除层", DeleteSelectedLayer));
            buttons.Controls.Add(MakeLayerButton("上移", MoveSelectedLayerUp));
            buttons.Controls.Add(MakeLayerButton("下移", MoveSelectedLayerDown));
            return group;
        }

        private Control BuildWidthGroup()
        {
            var group = new GroupBox();
            group.Text = "宽高设置";
            group.Dock = DockStyle.Top;
            group.AutoSize = true;

            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.AutoSize = true;
            panel.ColumnCount = 8;
            panel.RowCount = 1;
            panel.Padding = new Padding(8);
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            group.Controls.Add(panel);

            var widthLabel = new Label();
            widthLabel.Text = "断面宽度";
            widthLabel.TextAlign = ContentAlignment.MiddleLeft;
            widthLabel.Dock = DockStyle.Fill;
            panel.Controls.Add(widthLabel, 0, 0);

            _numWidth = MakeNumber(0.01M, 100000M, 1.00M, 3);
            _numWidth.ValueChanged += delegate { UpdatePreview(); };
            panel.Controls.Add(_numWidth, 1, 0);

            var totalLabel = new Label();
            totalLabel.Text = "总高";
            totalLabel.TextAlign = ContentAlignment.MiddleLeft;
            totalLabel.Dock = DockStyle.Fill;
            panel.Controls.Add(totalLabel, 2, 0);

            _numTotalHeight = MakeNumber(0.001M, 100000M, 1.68M, 3);
            _numTotalHeight.Enabled = false;
            _numTotalHeight.ValueChanged += delegate
            {
                if (_loading || _syncingHeights) return;
                if (_chkLockTotalHeight != null && _chkLockTotalHeight.Checked)
                {
                    ApplyLockedTotalHeight(-1);
                    RebuildLayerPanels();
                }
                UpdatePreview();
            };
            panel.Controls.Add(_numTotalHeight, 3, 0);

            _chkLockTotalHeight = new CheckBox();
            _chkLockTotalHeight.Text = "锁定总高";
            _chkLockTotalHeight.AutoSize = true;
            _chkLockTotalHeight.Anchor = AnchorStyles.Left;
            _chkLockTotalHeight.CheckedChanged += delegate
            {
                if (_numTotalHeight != null) _numTotalHeight.Enabled = _chkLockTotalHeight.Checked;
                if (_loading || _syncingHeights) return;
                if (_chkLockTotalHeight.Checked)
                {
                    EnsureTotalHeightValue();
                    ApplyLockedTotalHeight(-1);
                    RebuildLayerPanels();
                }
                else
                {
                    UpdateTotalHeightFromLayers();
                }
                UpdatePreview();
            };
            panel.Controls.Add(_chkLockTotalHeight, 4, 0);

            _chkTopDimension = new CheckBox();
            _chkTopDimension.Text = "上方宽度标注";
            _chkTopDimension.AutoSize = true;
            _chkTopDimension.CheckedChanged += delegate { UpdatePreview(); };
            panel.Controls.Add(_chkTopDimension, 5, 0);

            _chkBottomDimension = new CheckBox();
            _chkBottomDimension.Text = "下方宽度标注";
            _chkBottomDimension.AutoSize = true;
            _chkBottomDimension.CheckedChanged += delegate { UpdatePreview(); };
            panel.Controls.Add(_chkBottomDimension, 6, 0);

            return group;
        }

        private Control BuildMiddleGroup()
        {
            var group = new GroupBox();
            group.Text = "断面编辑与预览";
            group.Dock = DockStyle.Fill;

            var main = new TableLayoutPanel();
            main.Dock = DockStyle.Fill;
            main.ColumnCount = 3;
            main.RowCount = 1;
            main.Padding = new Padding(8);
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            group.Controls.Add(main);

            main.Controls.Add(BuildLayerNotePanel(), 0, 0);
            main.Controls.Add(BuildPreviewPanel(), 1, 0);
            main.Controls.Add(BuildLayerHeightPipePanel(), 2, 0);

            return group;
        }

        private Control BuildLayerNotePanel()
        {
            var group = new GroupBox();
            group.Text = "各层注记 / 图案填充";
            group.Dock = DockStyle.Fill;

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Padding = new Padding(6);
            group.Controls.Add(root);

            _leftLayerContainer = new Panel();
            _leftLayerContainer.Dock = DockStyle.Fill;
            _leftLayerContainer.AutoScroll = true;
            root.Controls.Add(_leftLayerContainer, 0, 0);

            return group;
        }

        private Control BuildPreviewPanel()
        {
            var group = new GroupBox();
            group.Text = "预览框";
            group.Dock = DockStyle.Fill;

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Padding = new Padding(6);
            group.Controls.Add(root);

            _preview = new SectionPreviewControl();
            _preview.Dock = DockStyle.Fill;
            root.Controls.Add(_preview, 0, 0);

            return group;
        }

        private Control BuildLayerHeightPipePanel()
        {
            var group = new GroupBox();
            group.Text = "各层高度 / 管圆设置";
            group.Dock = DockStyle.Fill;

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 1;
            root.Padding = new Padding(6);
            group.Controls.Add(root);

            _rightLayerContainer = new Panel();
            _rightLayerContainer.Dock = DockStyle.Fill;
            _rightLayerContainer.AutoScroll = true;
            root.Controls.Add(_rightLayerContainer, 0, 0);

            return group;
        }

        private Control BuildTitleGroup()
        {
            var group = new GroupBox();
            group.Text = "管段注记编辑";
            group.Dock = DockStyle.Top;
            group.AutoSize = true;

            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.AutoSize = true;
            panel.ColumnCount = 4;
            panel.RowCount = 1;
            panel.Padding = new Padding(8);
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));
            group.Controls.Add(panel);

            _txtSectionTitle = new TextBox();
            _txtSectionTitle.Multiline = true;
            _txtSectionTitle.Height = 48;
            _txtSectionTitle.ScrollBars = ScrollBars.Vertical;
            _txtSectionTitle.AcceptsReturn = true;
            _txtSectionTitle.TextChanged += delegate { UpdatePreview(); };
            AddRow(panel, 0, 0, "管段注记", _txtSectionTitle);

            _chkDrawTitle = new CheckBox();
            _chkDrawTitle.Text = "生成最下方管段注记";
            _chkDrawTitle.AutoSize = true;
            _chkDrawTitle.CheckedChanged += delegate { UpdatePreview(); };
            panel.Controls.Add(_chkDrawTitle, 2, 0);

            return group;
        }

        private Control BuildOtherGroup()
        {
            var group = new GroupBox();
            group.Text = "其他设置";
            group.Dock = DockStyle.Top;
            group.AutoSize = true;

            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.AutoSize = true;
            panel.ColumnCount = 4;
            panel.RowCount = 4;
            panel.Padding = new Padding(8);
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            group.Controls.Add(panel);

            _cmbTextStyle = new ComboBox();
            _cmbTextStyle.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbTextStyle.SelectedIndexChanged += delegate { UpdatePreview(); };
            AddRow(panel, 0, 0, "字体样式", _cmbTextStyle);

            _cmbUnifiedLayer = new ComboBox();
            _cmbUnifiedLayer.DropDownStyle = ComboBoxStyle.DropDownList;
            AddRow(panel, 0, 2, "统一图层", _cmbUnifiedLayer);

            _cmbDimensionStyle = new ComboBox();
            _cmbDimensionStyle.DropDownStyle = ComboBoxStyle.DropDownList;
            AddRow(panel, 1, 0, "标注样式", _cmbDimensionStyle);

            _chkRightDimensions = new CheckBox();
            _chkRightDimensions.Text = "生成右侧各层高度标注";
            _chkRightDimensions.AutoSize = true;
            _chkRightDimensions.CheckedChanged += delegate { UpdatePreview(); };
            AddRow(panel, 1, 2, "层高标注", _chkRightDimensions);

            _chkTotalHeightDimension = new CheckBox();
            _chkTotalHeightDimension.Text = "生成右侧总高度标注";
            _chkTotalHeightDimension.AutoSize = true;
            _chkTotalHeightDimension.CheckedChanged += delegate { UpdatePreview(); };
            AddRow(panel, 2, 2, "总高标注", _chkTotalHeightDimension);

            return group;
        }

        private void LoadInitialValues()
        {
            _loading = true;
            try
            {
                _txtSectionTitle.Text = _initialOptions.SectionTitle;
                SetNumberValue(_numWidth, (decimal)_initialOptions.Width);
                SetNumberValue(_numTotalHeight, (decimal)GetInitialTotalHeight(_initialOptions));
                if (_chkLockTotalHeight != null) _chkLockTotalHeight.Checked = _initialOptions.LockTotalHeight;
                if (_numTotalHeight != null) _numTotalHeight.Enabled = _initialOptions.LockTotalHeight;
                _chkDrawTitle.Checked = _initialOptions.DrawTitle;
                _chkTopDimension.Checked = _initialOptions.DrawTopDimension;
                _chkBottomDimension.Checked = _initialOptions.DrawBottomDimension;
                _chkRightDimensions.Checked = _initialOptions.DrawRightDimensions;
                if (_chkTotalHeightDimension != null) _chkTotalHeightDimension.Checked = _initialOptions.DrawTotalHeightDimension;

                _cmbTextStyle.DataSource = LoadTextStyleNames(_initialOptions.TextStyleName);
                SelectComboValue(_cmbTextStyle, _initialOptions.TextStyleName);

                _cmbUnifiedLayer.DataSource = LoadLayerNames(PickUnifiedLayerName(_initialOptions));
                SelectComboValue(_cmbUnifiedLayer, PickUnifiedLayerName(_initialOptions));

                _cmbDimensionStyle.DataSource = LoadDimensionStyleNames(_initialOptions.DimensionStyleName);
                SelectDimensionStyle(_initialOptions.DimensionStyleName);
            }
            finally
            {
                _loading = false;
            }
        }

        private void NormalizeUnavailableHatchPatterns()
        {
            if (_layers == null || _layers.Count == 0) return;

            HashSet<string> available = HatchSettingDialog.GetAvailablePatternNameSet(_doc);
            if (available == null || available.Count == 0) return;

            var removed = new List<string>();
            for (int i = 0; i < _layers.Count; i++)
            {
                SectionLayerOptions layer = _layers[i];
                if (layer == null) continue;

                string pattern = NormalizeHatchNameForCheck(layer.HatchPatternName);
                if (IsNoHatch(pattern)) continue;

                if (!available.Contains(pattern))
                {
                    if (!removed.Contains(pattern)) removed.Add(pattern);
                    layer.HatchPatternName = string.Empty;
                    if (layer.HatchScale <= 0) layer.HatchScale = 1.0;
                    UpdateHatchSummary(i);
                }
            }

            if (removed.Count > 0)
            {
                //WriteLog("已跳过当前 CAD 未读取到的填充图案：" + string.Join("、", removed.ToArray()) + "。对应层改为无填充。", false);
            }
        }

        private static string NormalizeHatchNameForCheck(string patternName)
        {
            if (string.IsNullOrWhiteSpace(patternName)) return string.Empty;
            string value = patternName.Trim();
            return string.Equals(value, "无", StringComparison.CurrentCultureIgnoreCase) ? "无填充" : value;
        }

        private static bool IsNoHatch(string patternName)
        {
            if (string.IsNullOrWhiteSpace(patternName)) return true;
            return string.Equals(patternName, "无填充", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(patternName, "无", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(patternName, "NONE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(patternName, "NO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(patternName, "OFF", StringComparison.OrdinalIgnoreCase);
        }

        private void RebuildLayerPanels()
        {
            if (_leftLayerContainer == null || _rightLayerContainer == null) return;
            if (_selectedLayerIndex < 0) _selectedLayerIndex = 0;
            if (_selectedLayerIndex >= _layers.Count) _selectedLayerIndex = _layers.Count - 1;
            if (_selectedLayerIndex < 0) _selectedLayerIndex = 0;

            _rightLayerTitleLabels.Clear();
            _leftHatchSummaryLabels.Clear();
            _rightPipeSummaryLabels.Clear();
            _leftLayerContainer.Controls.Clear();
            _rightLayerContainer.Controls.Clear();

            var leftTable = MakeLayerRowsTable();
            var rightTable = MakeLayerRowsTable();
            _leftLayerContainer.Controls.Add(leftTable);
            _rightLayerContainer.Controls.Add(rightTable);

            for (int i = 0; i < _layers.Count; i++)
            {
                AddLayerRowStyles(leftTable, rightTable, i);
                leftTable.Controls.Add(CreateLeftLayerRow(i), 0, i);
                rightTable.Controls.Add(CreateRightLayerRow(i), 0, i);
            }
            UpdateTotalHeightFromLayers();
        }

        private TableLayoutPanel MakeLayerRowsTable()
        {
            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 1;
            table.RowCount = Math.Max(1, _layers.Count);
            table.Padding = new Padding(0);
            table.Margin = new Padding(0);
            return table;
        }

        private void AddLayerRowStyles(TableLayoutPanel leftTable, TableLayoutPanel rightTable, int index)
        {
            // 左右设置区只用于编辑参数，行高不再随断面层高变化，避免某层过薄时控件被压住。
            float part = 100.0f / Math.Max(1, _layers.Count);
            leftTable.RowStyles.Add(new RowStyle(SizeType.Percent, part));
            rightTable.RowStyles.Add(new RowStyle(SizeType.Percent, part));
        }

        private Control CreateLeftLayerRow(int index)
        {
            SectionLayerOptions layer = _layers[index];
            var box = MakeLayerBox(index);
            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 4;
            table.RowCount = 1;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            box.Controls.Add(table);

            int rowIndex = index;
            var drawCheck = new CheckBox();
            drawCheck.Text = "绘制";
            drawCheck.Checked = layer == null || layer.DrawLayer;
            drawCheck.AutoSize = true;
            drawCheck.Anchor = AnchorStyles.Left;
            drawCheck.Margin = new Padding(0, 5, 2, 3);
            drawCheck.CheckedChanged += delegate
            {
                if (rowIndex >= 0 && rowIndex < _layers.Count)
                {
                    _layers[rowIndex].DrawLayer = drawCheck.Checked;
                    UpdateRightLayerTitle(rowIndex);
                    HighlightSelectedLayerRows();
                    UpdatePreview();
                }
            };
            table.Controls.Add(drawCheck, 0, 0);

            var text = new TextBox();
            text.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            text.Margin = new Padding(3, 4, 3, 3);
            text.Text = layer.LeftLabel ?? string.Empty;
            text.TextChanged += delegate
            {
                if (rowIndex >= 0 && rowIndex < _layers.Count)
                {
                    _layers[rowIndex].LeftLabel = text.Text;
                    UpdateRightLayerTitle(rowIndex);
                    UpdatePreview();
                }
            };
            table.Controls.Add(text, 1, 0);

            var hatchBtn = MakeFixedInlineButton("图案填充", delegate { EditLayerHatch(rowIndex); }, 92);
            table.Controls.Add(hatchBtn, 2, 0);

            var hatchSummary = new Label();
            hatchSummary.Dock = DockStyle.Fill;
            hatchSummary.AutoEllipsis = true;
            hatchSummary.TextAlign = ContentAlignment.MiddleLeft;
            hatchSummary.Margin = new Padding(3, 3, 0, 3);
            _leftHatchSummaryLabels[rowIndex] = hatchSummary;
            UpdateHatchSummary(rowIndex);
            table.Controls.Add(hatchSummary, 3, 0);

            AttachSelectClick(box, index);
            return box;
        }

        private Control CreateRightLayerRow(int index)
        {
            SectionLayerOptions layer = _layers[index];
            var box = MakeLayerBox(index);
            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 5;
            table.RowCount = 1;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            box.Controls.Add(table);

            var title = MakeCellLabel(GetLayerTitleText(index));
            title.Font = new System.Drawing.Font(this.Font, System.Drawing.FontStyle.Bold);
            title.AutoEllipsis = true;
            table.Controls.Add(title, 0, 0);
            _rightLayerTitleLabels[index] = title;

            var height = MakeNumber(0.001M, 100000M, (decimal)Math.Max(0.001, layer.Height), 3);
            height.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            height.Margin = new Padding(3, 4, 3, 3);
            int rowIndex = index;
            height.ValueChanged += delegate
            {
                if (_syncingHeights) return;
                if (rowIndex >= 0 && rowIndex < _layers.Count)
                {
                    _layers[rowIndex].Height = (double)height.Value;
                    ApplyHeightRuleAfterLayerEdit(rowIndex);
                    RebuildLayerPanels();
                    UpdatePreview();
                }
            };
            table.Controls.Add(height, 1, 0);

            var lockCheck = new CheckBox();
            lockCheck.Text = "锁定";
            lockCheck.AutoSize = true;
            lockCheck.Checked = layer != null && layer.HeightLocked;
            lockCheck.Anchor = AnchorStyles.Left;
            lockCheck.Margin = new Padding(3, 4, 3, 3);
            lockCheck.CheckedChanged += delegate
            {
                if (rowIndex >= 0 && rowIndex < _layers.Count)
                {
                    _layers[rowIndex].HeightLocked = lockCheck.Checked;
                    if (!_syncingHeights && _chkLockTotalHeight != null && _chkLockTotalHeight.Checked)
                    {
                        ApplyLockedTotalHeight(rowIndex);
                        RebuildLayerPanels();
                    }
                    UpdatePreview();
                }
            };
            table.Controls.Add(lockCheck, 2, 0);

            var pipeBtn = MakeFixedInlineButton("添加管断面", delegate { EditLayerPipes(rowIndex); }, 106);
            table.Controls.Add(pipeBtn, 3, 0);

            var pipeSummary = new Label();
            pipeSummary.Dock = DockStyle.Fill;
            pipeSummary.AutoEllipsis = true;
            pipeSummary.TextAlign = ContentAlignment.MiddleLeft;
            pipeSummary.Margin = new Padding(3, 3, 0, 3);
            _rightPipeSummaryLabels[rowIndex] = pipeSummary;
            UpdatePipeSummary(rowIndex);
            table.Controls.Add(pipeSummary, 4, 0);

            AttachSelectClick(box, index);
            return box;
        }

        private Panel MakeLayerBox(int index)
        {
            var box = new Panel();
            box.Dock = DockStyle.Fill;
            box.Margin = new Padding(2);
            box.Padding = new Padding(5);
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Tag = index;
            box.BackColor = GetLayerRowBackColor(index);
            return box;
        }

        private void EditLayerHatch(int index)
        {
            if (index < 0 || index >= _layers.Count) return;
            _selectedLayerIndex = index;
            HighlightSelectedLayerRows();
            using (var dlg = new HatchSettingDialog(_doc, _layers[index]))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _layers[index].HatchPatternName = dlg.PatternName;
                    _layers[index].HatchScale = dlg.PatternScale;
                    _layers[index].HatchAngle = 0.0;
                    UpdateHatchSummary(index);
                    UpdatePreview();
                }
            }
        }

        private void EditLayerPipes(int index)
        {
            if (index < 0 || index >= _layers.Count) return;
            _selectedLayerIndex = index;
            HighlightSelectedLayerRows();
            using (var dlg = new PipeSettingDialog(GetLayerDisplayName(index), _layers[index].Pipes))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _layers[index].Pipes = dlg.Pipes;
                    UpdatePipeSummary(index);
                    UpdatePreview();
                }
            }
        }

        private void UpdateRightLayerTitle(int index)
        {
            Label label;
            if (_rightLayerTitleLabels.TryGetValue(index, out label) && label != null)
            {
                label.Text = GetLayerTitleText(index);
            }
        }

        private void UpdateHatchSummary(int index)
        {
            Label label;
            if (!_leftHatchSummaryLabels.TryGetValue(index, out label) || label == null || index < 0 || index >= _layers.Count) return;
            SectionLayerOptions layer = _layers[index];
            string pattern = layer.HatchPatternName;
            if (string.IsNullOrWhiteSpace(pattern)) pattern = "无";
            label.Text = pattern + " / 比例 " + layer.HatchScale.ToString("0.###");
        }

        private void UpdatePipeSummary(int index)
        {
            Label label;
            if (!_rightPipeSummaryLabels.TryGetValue(index, out label) || label == null || index < 0 || index >= _layers.Count) return;
            int count = _layers[index].Pipes == null ? 0 : _layers[index].Pipes.Count;
            label.Text = count <= 0 ? "无管圆" : count + " 个管圆";
        }

        private string GetLayerTitleText(int index)
        {
            string name = GetLayerDisplayName(index);
            if (index >= 0 && index < _layers.Count && _layers[index] != null && !_layers[index].DrawLayer)
            {
                return "不绘制 - " + name;
            }
            return name;
        }

        private string GetLayerDisplayName(int index)
        {
            if (index < 0 || index >= _layers.Count) return "未命名层";
            string name = _layers[index] == null ? string.Empty : _layers[index].LeftLabel;
            return string.IsNullOrWhiteSpace(name) ? "未命名" : name;
        }

        private Color GetLayerRowBackColor(int index)
        {
            if (index == _selectedLayerIndex) return Color.FromArgb(230, 242, 255);
            if (index >= 0 && index < _layers.Count && _layers[index] != null && !_layers[index].DrawLayer)
            {
                return Color.FromArgb(238, 238, 238);
            }
            return SystemColors.Control;
        }

        private void AttachSelectClick(Control control, int index)
        {
            if (control == null) return;
            EventHandler select = delegate
            {
                _selectedLayerIndex = index;
                HighlightSelectedLayerRows();
            };
            control.Click += select;
            control.GotFocus += select;
            control.MouseDown += delegate { select(control, EventArgs.Empty); };
            foreach (Control child in control.Controls)
            {
                AttachSelectClick(child, index);
            }
        }

        private void HighlightSelectedLayerRows()
        {
            PaintSelectedRows(_leftLayerContainer);
            PaintSelectedRows(_rightLayerContainer);
        }

        private void PaintSelectedRows(Control parent)
        {
            if (parent == null) return;
            foreach (Control c in parent.Controls)
            {
                PaintSelectedRowsRecursive(c);
            }
        }

        private void PaintSelectedRowsRecursive(Control control)
        {
            if (control == null) return;
            if (control is Panel && control.Tag is int)
            {
                int index = (int)control.Tag;
                control.BackColor = GetLayerRowBackColor(index);
            }
            foreach (Control child in control.Controls)
            {
                PaintSelectedRowsRecursive(child);
            }
        }

        private Label MakeCellLabel(string text)
        {
            var label = new Label();
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Dock = DockStyle.Fill;
            label.AutoSize = false;
            label.Padding = new Padding(0, 2, 0, 0);
            return label;
        }

        private Button MakeFixedInlineButton(string text, EventHandler click, int width)
        {
            var button = new Button();
            button.Text = text;
            button.AutoSize = false;
            button.Size = new Size(width, 26);
            button.MinimumSize = new Size(width, 26);
            button.MaximumSize = new Size(width, 26);
            button.Anchor = AnchorStyles.Left;
            button.Margin = new Padding(3, 4, 3, 3);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Click += click;
            return button;
        }

        private Button MakeLayerButton(string text, Action action)
        {
            var button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Margin = new Padding(2, 2, 4, 4);
            button.Click += delegate { action(); };
            return button;
        }

        private void AddLayer()
        {
            int insert = _selectedLayerIndex + 1;
            if (insert < 0 || insert > _layers.Count) insert = _layers.Count;
            _layers.Insert(insert, new SectionLayerOptions { LeftLabel = "自定义层", Height = 0.10, HeightLocked = false, HatchPatternName = string.Empty, HatchScale = 1.0, HatchAngle = 0.0 });
            _selectedLayerIndex = insert;
            if (_chkLockTotalHeight != null && _chkLockTotalHeight.Checked) ApplyLockedTotalHeight(insert);
            RebuildLayerPanels();
            UpdatePreview();
        }

        private void DeleteSelectedLayer()
        {
            if (_layers.Count <= 1)
            {
                MessageBox.Show("至少需要保留一层。", "断面图生成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int index = _selectedLayerIndex;
            if (index < 0 || index >= _layers.Count) return;
            _layers.RemoveAt(index);
            if (_selectedLayerIndex >= _layers.Count) _selectedLayerIndex = _layers.Count - 1;
            if (_chkLockTotalHeight != null && _chkLockTotalHeight.Checked) ApplyLockedTotalHeight(-1);
            RebuildLayerPanels();
            UpdatePreview();
        }

        private void MoveSelectedLayerUp()
        {
            int index = _selectedLayerIndex;
            if (index <= 0 || index >= _layers.Count) return;
            SectionLayerOptions item = _layers[index];
            _layers.RemoveAt(index);
            _layers.Insert(index - 1, item);
            _selectedLayerIndex = index - 1;
            RebuildLayerPanels();
            UpdatePreview();
        }

        private void MoveSelectedLayerDown()
        {
            int index = _selectedLayerIndex;
            if (index < 0 || index >= _layers.Count - 1) return;
            SectionLayerOptions item = _layers[index];
            _layers.RemoveAt(index);
            _layers.Insert(index + 1, item);
            _selectedLayerIndex = index + 1;
            RebuildLayerPanels();
            UpdatePreview();
        }

        private SectionDrawingOptions ReadOptions(bool showErrors)
        {
            var options = new SectionDrawingOptions();
            options.SectionTitle = _txtSectionTitle == null ? string.Empty : _txtSectionTitle.Text;
            options.Width = _numWidth == null ? 1.0 : (double)_numWidth.Value;
            options.TotalHeight = _numTotalHeight == null ? SumLayerHeights() : (double)_numTotalHeight.Value;
            options.LockTotalHeight = _chkLockTotalHeight != null && _chkLockTotalHeight.Checked;
            options.TextStyleName = _cmbTextStyle == null || _cmbTextStyle.SelectedItem == null ? string.Empty : _cmbTextStyle.SelectedItem.ToString();

            string unifiedLayer = ReadCombo(_cmbUnifiedLayer, "0");
            options.BorderLayerName = unifiedLayer;
            options.TextLayerName = unifiedLayer;
            options.HatchLayerName = unifiedLayer;
            options.DimensionLayerName = unifiedLayer;
            options.DimensionStyleName = ReadDimensionStyleName();

            options.LeftLabelWidth = 0.45;
            options.TopDimensionOffset = 0.08;
            options.BottomDimensionOffset = 0.08;
            options.RightDimensionOffset = 0.08;
            options.TitleOffset = 0.12;
            options.DrawTopDimension = _chkTopDimension == null || _chkTopDimension.Checked;
            options.DrawBottomDimension = _chkBottomDimension == null || _chkBottomDimension.Checked;
            options.DrawRightDimensions = _chkRightDimensions == null || _chkRightDimensions.Checked;
            options.DrawTotalHeightDimension = _chkTotalHeightDimension != null && _chkTotalHeightDimension.Checked;
            options.DrawTitle = _chkDrawTitle == null || _chkDrawTitle.Checked;

            options.Layers = new List<SectionLayerOptions>();
            foreach (SectionLayerOptions layer in _layers)
            {
                if (layer == null) continue;
                SectionLayerOptions clonedLayer = layer.Clone();
                clonedLayer.HatchAngle = 0.0;
                options.Layers.Add(clonedLayer);
            }

            if (options.Layers.Count == 0)
            {
                throw new InvalidOperationException("至少需要设置一层断面层级。");
            }

            bool hasPipe = false;
            for (int i = 0; i < options.Layers.Count; i++)
            {
                if (options.Layers[i].Height <= 0)
                {
                    throw new InvalidOperationException("第 " + (i + 1) + " 层高度必须大于 0。");
                }
                if (options.Layers[i].Pipes != null && options.Layers[i].Pipes.Count > 0)
                {
                    hasPipe = true;
                    for (int p = 0; p < options.Layers[i].Pipes.Count; p++)
                    {
                        options.Layers[i].Pipes[p].HostLayerIndex = i;
                    }
                }
            }

            options.DrawPipeCircle = hasPipe;
            options.Pipe = FindFirstPipe(options.Layers) ?? SectionPipeOptions.Default;
            options.TextHeight = CalculateAutoTextHeight(options);
            SectionLayoutCalculator.Normalize(options);
            return options;
        }

        private double GetInitialTotalHeight(SectionDrawingOptions options)
        {
            if (options != null && options.TotalHeight > 0) return options.TotalHeight;
            return SumLayerHeights();
        }

        private double SumLayerHeights()
        {
            double sum = 0.0;
            if (_layers != null)
            {
                for (int i = 0; i < _layers.Count; i++)
                {
                    if (_layers[i] != null && _layers[i].Height > 0) sum += _layers[i].Height;
                }
            }
            return sum <= 0 ? 0.001 : sum;
        }

        private void EnsureTotalHeightValue()
        {
            if (_numTotalHeight == null) return;
            if (_numTotalHeight.Value <= 0)
            {
                SetNumberValue(_numTotalHeight, (decimal)SumLayerHeights());
            }
        }

        private void UpdateTotalHeightFromLayers()
        {
            if (_numTotalHeight == null || _syncingHeights) return;
            if (_chkLockTotalHeight != null && _chkLockTotalHeight.Checked) return;

            try
            {
                _syncingHeights = true;
                SetNumberValue(_numTotalHeight, (decimal)SumLayerHeights());
            }
            finally
            {
                _syncingHeights = false;
            }
        }

        private void ApplyHeightRuleAfterLayerEdit(int changedIndex)
        {
            if (_chkLockTotalHeight != null && _chkLockTotalHeight.Checked)
            {
                ApplyLockedTotalHeight(changedIndex);
            }
            else
            {
                UpdateTotalHeightFromLayers();
            }
        }

        private void ApplyLockedTotalHeight(int changedIndex)
        {
            if (_layers == null || _layers.Count == 0 || _numTotalHeight == null) return;

            const double minHeight = 0.001;
            double target = (double)_numTotalHeight.Value;
            if (target <= 0) target = SumLayerHeights();

            try
            {
                _syncingHeights = true;

                var adjustable = new List<int>();
                double fixedSum = 0.0;
                for (int i = 0; i < _layers.Count; i++)
                {
                    SectionLayerOptions layer = _layers[i];
                    if (layer == null) continue;
                    if (i == changedIndex || layer.HeightLocked)
                    {
                        fixedSum += Math.Max(minHeight, layer.Height);
                    }
                    else
                    {
                        adjustable.Add(i);
                    }
                }

                double remaining = target - fixedSum;
                if (adjustable.Count > 0)
                {
                    if (remaining < minHeight * adjustable.Count) remaining = minHeight * adjustable.Count;

                    double currentAdjustableSum = 0.0;
                    for (int i = 0; i < adjustable.Count; i++)
                    {
                        currentAdjustableSum += Math.Max(minHeight, _layers[adjustable[i]].Height);
                    }

                    double assigned = 0.0;
                    for (int i = 0; i < adjustable.Count; i++)
                    {
                        int layerIndex = adjustable[i];
                        double value;
                        if (i == adjustable.Count - 1)
                        {
                            value = remaining - assigned;
                        }
                        else if (currentAdjustableSum > 0)
                        {
                            value = remaining * Math.Max(minHeight, _layers[layerIndex].Height) / currentAdjustableSum;
                        }
                        else
                        {
                            value = remaining / adjustable.Count;
                        }

                        if (value < minHeight) value = minHeight;
                        _layers[layerIndex].Height = value;
                        assigned += value;
                    }
                }
                else if (changedIndex >= 0 && changedIndex < _layers.Count && !_layers[changedIndex].HeightLocked)
                {
                    double otherSum = 0.0;
                    for (int i = 0; i < _layers.Count; i++)
                    {
                        if (i == changedIndex || _layers[i] == null) continue;
                        otherSum += Math.Max(minHeight, _layers[i].Height);
                    }
                    double value = target - otherSum;
                    if (value < minHeight) value = minHeight;
                    _layers[changedIndex].Height = value;
                }
            }
            finally
            {
                _syncingHeights = false;
            }
        }

        private SectionPipeOptions FindFirstPipe(List<SectionLayerOptions> layers)
        {
            if (layers == null) return null;
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] != null && layers[i].Pipes != null && layers[i].Pipes.Count > 0)
                {
                    return layers[i].Pipes[0].Clone();
                }
            }
            return null;
        }

        private double CalculateAutoTextHeight(SectionDrawingOptions options)
        {
            if (options == null || options.Layers == null || options.Layers.Count == 0) return 0.08;
            double minHeight = double.MaxValue;
            for (int i = 0; i < options.Layers.Count; i++)
            {
                if (options.Layers[i] != null && options.Layers[i].DrawLayer && options.Layers[i].Height > 0)
                {
                    minHeight = Math.Min(minHeight, options.Layers[i].Height);
                }
            }
            if (minHeight == double.MaxValue) minHeight = 0.15;
            double byLayer = minHeight * 0.42;
            double byWidth = Math.Max(0.02, options.Width * 0.08);
            double value = Math.Min(byLayer, byWidth);
            if (value < 0.02) value = 0.02;
            if (value > 0.10) value = 0.10;
            return value;
        }

        private string ReadCombo(ComboBox combo, string fallback)
        {
            if (combo == null || combo.SelectedItem == null) return fallback;
            string value = combo.SelectedItem.ToString();
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private string ReadDimensionStyleName()
        {
            if (_cmbDimensionStyle == null || _cmbDimensionStyle.SelectedItem == null) return string.Empty;
            string value = _cmbDimensionStyle.SelectedItem.ToString();
            if (value.StartsWith("当前尺寸样式", StringComparison.CurrentCultureIgnoreCase)) return string.Empty;
            return value;
        }

        private void UpdatePreview()
        {
            if (_loading || _preview == null) return;
            try
            {
                _preview.Options = ReadOptions(false);
            }
            catch
            {
            }
        }

        private static bool HasDrawableLayer(SectionDrawingOptions options)
        {
            if (options == null || options.Layers == null) return false;
            for (int i = 0; i < options.Layers.Count; i++)
            {
                if (options.Layers[i] != null && options.Layers[i].DrawLayer && options.Layers[i].Height > 0) return true;
            }
            return false;
        }

        private void RunDraw()
        {
            bool wasVisible = Visible;
            bool restoreForm = true;
            try
            {
                NormalizeUnavailableHatchPatterns();
                SectionDrawingOptions options = ReadOptions(true);
                if (!HasDrawableLayer(options)) throw new InvalidOperationException("至少需要勾选一层进行绘制。");
                SectionDrawingSettingsStore.Save(options);
                if (wasVisible) Hide();

                SectionDrawingResult result = SectionDrawingService.SelectPositionAndDraw(_doc, options);
                _doc.Editor.WriteMessage(result.ToEditorMessage());
                //WriteLog(result.Message + " " + result.ToEditorMessage().Trim(), !result.Success);

                if (result.Success)
                {
                    restoreForm = false;
                    Close();
                }
            }
            catch (Exception ex)
            {
                _doc.Editor.WriteMessage("\n[断面图生成] 失败：" + ex.Message);
                MessageBox.Show(ex.Message, "断面图生成失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (restoreForm && wasVisible && !IsDisposed)
                {
                    Show();
                    Activate();
                }
            }
        }

        private string PickUnifiedLayerName(SectionDrawingOptions options)
        {
            if (options == null) return "0";
            if (!string.IsNullOrWhiteSpace(options.BorderLayerName)) return options.BorderLayerName;
            if (!string.IsNullOrWhiteSpace(options.TextLayerName)) return options.TextLayerName;
            if (!string.IsNullOrWhiteSpace(options.DimensionLayerName)) return options.DimensionLayerName;
            if (!string.IsNullOrWhiteSpace(options.HatchLayerName)) return options.HatchLayerName;
            return "0";
        }

        private List<string> LoadTextStyleNames(string preferred)
        {
            var names = new List<string>();
            string currentStyleName = string.Empty;
            try
            {
                Database db = _doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    TextStyleTable tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    foreach (ObjectId id in tst)
                    {
                        TextStyleTableRecord style = tr.GetObject(id, OpenMode.ForRead, false) as TextStyleTableRecord;
                        if (style == null || style.IsErased || string.IsNullOrWhiteSpace(style.Name)) continue;
                        if (!ContainsIgnoreCase(names, style.Name)) names.Add(style.Name);
                    }
                    if (!db.Textstyle.IsNull)
                    {
                        TextStyleTableRecord current = tr.GetObject(db.Textstyle, OpenMode.ForRead, false) as TextStyleTableRecord;
                        if (current != null && !string.IsNullOrWhiteSpace(current.Name)) currentStyleName = current.Name;
                    }
                    tr.Commit();
                }
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(preferred) && !ContainsIgnoreCase(names, preferred)) names.Add(preferred);
            if (names.Count == 0) names.Add("STANDARD");
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            if (!string.IsNullOrWhiteSpace(preferred) && ContainsIgnoreCase(names, preferred)) MoveNameToTop(names, preferred);
            else if (ContainsIgnoreCase(names, "宋体")) MoveNameToTop(names, "宋体");
            else if (!string.IsNullOrWhiteSpace(currentStyleName) && ContainsIgnoreCase(names, currentStyleName)) MoveNameToTop(names, currentStyleName);
            return names;
        }

        private List<string> LoadLayerNames(string preferred)
        {
            var names = new List<string>();
            string currentLayerName = string.Empty;
            try
            {
                Database db = _doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId id in lt)
                    {
                        LayerTableRecord layer = tr.GetObject(id, OpenMode.ForRead, false) as LayerTableRecord;
                        if (layer == null || layer.IsErased || string.IsNullOrWhiteSpace(layer.Name)) continue;
                        if (!ContainsIgnoreCase(names, layer.Name)) names.Add(layer.Name);
                    }
                    if (!db.Clayer.IsNull)
                    {
                        LayerTableRecord current = tr.GetObject(db.Clayer, OpenMode.ForRead, false) as LayerTableRecord;
                        if (current != null && !string.IsNullOrWhiteSpace(current.Name)) currentLayerName = current.Name;
                    }
                    tr.Commit();
                }
            }
            catch { }

            if (names.Count == 0) names.Add("0");
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            if (!string.IsNullOrWhiteSpace(preferred) && ContainsIgnoreCase(names, preferred)) MoveNameToTop(names, preferred);
            else if (!string.IsNullOrWhiteSpace(currentLayerName) && ContainsIgnoreCase(names, currentLayerName)) MoveNameToTop(names, currentLayerName);
            else if (ContainsIgnoreCase(names, "0")) MoveNameToTop(names, "0");
            return names;
        }

        private List<string> LoadDimensionStyleNames(string preferred)
        {
            var names = new List<string>();
            names.Add("当前尺寸样式");
            string currentName = string.Empty;
            try
            {
                Database db = _doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DimStyleTable dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                    foreach (ObjectId id in dst)
                    {
                        DimStyleTableRecord style = tr.GetObject(id, OpenMode.ForRead, false) as DimStyleTableRecord;
                        if (style == null || style.IsErased || string.IsNullOrWhiteSpace(style.Name)) continue;
                        if (!ContainsIgnoreCase(names, style.Name)) names.Add(style.Name);
                    }
                    if (!db.Dimstyle.IsNull)
                    {
                        DimStyleTableRecord current = tr.GetObject(db.Dimstyle, OpenMode.ForRead, false) as DimStyleTableRecord;
                        if (current != null && !string.IsNullOrWhiteSpace(current.Name)) currentName = current.Name;
                    }
                    tr.Commit();
                }
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(preferred) && !ContainsIgnoreCase(names, preferred)) names.Add(preferred);
            if (!string.IsNullOrWhiteSpace(currentName) && !ContainsIgnoreCase(names, currentName)) names.Add(currentName);
            return names;
        }

        private void SelectDimensionStyle(string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName))
            {
                if (_cmbDimensionStyle.Items.Count > 0) _cmbDimensionStyle.SelectedIndex = 0;
            }
            else
            {
                SelectComboValue(_cmbDimensionStyle, styleName);
            }
        }

        private static bool ContainsIgnoreCase(List<string> names, string value)
        {
            if (names == null || string.IsNullOrWhiteSpace(value)) return false;
            foreach (string name in names)
            {
                if (string.Equals(name, value, StringComparison.CurrentCultureIgnoreCase)) return true;
            }
            return false;
        }

        private static void MoveNameToTop(List<string> names, string value)
        {
            if (names == null || string.IsNullOrWhiteSpace(value)) return;
            int index = names.FindIndex(x => string.Equals(x, value, StringComparison.CurrentCultureIgnoreCase));
            if (index <= 0) return;
            string item = names[index];
            names.RemoveAt(index);
            names.Insert(0, item);
        }

        private static void SelectComboValue(ComboBox combo, string value)
        {
            if (combo == null || combo.Items.Count == 0) return;
            int index = -1;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                string item = combo.Items[i] == null ? string.Empty : combo.Items[i].ToString();
                if (string.Equals(item, value, StringComparison.CurrentCultureIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
            combo.SelectedIndex = index >= 0 ? index : 0;
        }

        private static NumericUpDown MakeNumber(decimal min, decimal max, decimal value, int decimals)
        {
            var number = new NumericUpDown();
            number.Minimum = min;
            number.Maximum = max;
            number.DecimalPlaces = decimals;
            number.Value = value;
            number.Increment = decimals == 0 ? 1 : 0.01M;
            number.Width = 120;
            return number;
        }

        private static void SetNumberValue(NumericUpDown number, decimal value)
        {
            if (number == null) return;
            if (value < number.Minimum) value = number.Minimum;
            if (value > number.Maximum) value = number.Maximum;
            number.Value = value;
        }

        private static void AddRow(TableLayoutPanel panel, int row, int column, string labelText, Control control)
        {
            var label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Padding = new Padding(0, 4, 8, 4);
            panel.Controls.Add(label, column, row);

            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 4, 8, 4);
            panel.Controls.Add(control, column + 1, row);
        }

        private sealed class HatchSettingDialog : Form
        {
            private readonly Document _doc;
            private readonly NumericUpDown _numScale;
            private readonly Label _lblSelected;
            private readonly TabControl _tabs;
            private readonly List<ListView> _views = new List<ListView>();
            private readonly List<ImageList> _imageLists = new List<ImageList>();
            private string _selectedPatternName;

            public string PatternName
            {
                get
                {
                    string value = string.IsNullOrWhiteSpace(_selectedPatternName) ? "无填充" : _selectedPatternName.Trim();
                    return string.Equals(value, "无填充", StringComparison.CurrentCultureIgnoreCase) ? "无" : value;
                }
            }

            public double PatternScale { get { return (double)_numScale.Value; } }
            public double PatternAngle { get { return 0.0; } }

            public HatchSettingDialog(Document doc, SectionLayerOptions layer)
            {
                Text = "图案填充选择";
                Width = 560;
                Height = 620;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;

                _doc = doc;
                string preferred = layer == null ? string.Empty : layer.HatchPatternName;
                _selectedPatternName = NormalizePreferredPattern(preferred);

                var root = new TableLayoutPanel();
                root.Dock = DockStyle.Fill;
                root.Padding = new Padding(10);
                root.ColumnCount = 1;
                root.RowCount = 4;
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                Controls.Add(root);

                _lblSelected = new Label();
                _lblSelected.Dock = DockStyle.Fill;
                _lblSelected.AutoSize = true;
                _lblSelected.Padding = new Padding(4, 0, 4, 8);
                _lblSelected.TextAlign = ContentAlignment.MiddleLeft;
                root.Controls.Add(_lblSelected, 0, 0);

                _tabs = new TabControl();
                _tabs.Dock = DockStyle.Fill;
                root.Controls.Add(_tabs, 0, 1);

                BuildPatternTabs(doc, _selectedPatternName);

                var scalePanel = new TableLayoutPanel();
                scalePanel.Dock = DockStyle.Top;
                scalePanel.AutoSize = true;
                scalePanel.ColumnCount = 3;
                scalePanel.RowCount = 1;
                scalePanel.Padding = new Padding(0, 8, 0, 2);
                scalePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
                scalePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
                scalePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                root.Controls.Add(scalePanel, 0, 2);

                var scaleLabel = new Label();
                scaleLabel.Text = "填充比例";
                scaleLabel.Dock = DockStyle.Fill;
                scaleLabel.TextAlign = ContentAlignment.MiddleLeft;
                scalePanel.Controls.Add(scaleLabel, 0, 0);

                _numScale = MakeNumber(0M, 100000M, layer == null ? 1.0M : (decimal)Math.Max(0, layer.HatchScale), 3);
                _numScale.Dock = DockStyle.Fill;
                scalePanel.Controls.Add(_numScale, 1, 0);

                var buttons = new FlowLayoutPanel();
                buttons.Dock = DockStyle.Fill;
                buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
                buttons.AutoSize = true;
                buttons.Padding = new Padding(0, 6, 0, 0);
                root.Controls.Add(buttons, 0, 3);

                var ok = new Button();
                ok.Text = "确定";
                ok.DialogResult = DialogResult.OK;
                ok.Width = 78;
                ok.Height = 28;
                buttons.Controls.Add(ok);

                var cancel = new Button();
                cancel.Text = "取消";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Width = 78;
                cancel.Height = 28;
                buttons.Controls.Add(cancel);

                var removeCommon = new Button();
                removeCommon.Text = "移出常用";
                removeCommon.Width = 86;
                removeCommon.Height = 28;
                removeCommon.Click += delegate
                {
                    RemoveCommonPattern(_selectedPatternName);
                    RebuildPatternTabs(_selectedPatternName);
                };
                buttons.Controls.Add(removeCommon);

                var addCommon = new Button();
                addCommon.Text = "加入常用";
                addCommon.Width = 86;
                addCommon.Height = 28;
                addCommon.Click += delegate
                {
                    string name = NormalizePreferredPattern(_selectedPatternName);
                    if (!string.Equals(name, "无填充", StringComparison.CurrentCultureIgnoreCase))
                    {
                        AddOrUpdateCommonPattern(name, (double)_numScale.Value);
                        RebuildPatternTabs(name);
                    }
                };
                buttons.Controls.Add(addCommon);

                var none = new Button();
                none.Text = "无填充";
                none.Width = 78;
                none.Height = 28;
                none.Click += delegate { SelectPatternByName("无填充"); };
                buttons.Controls.Add(none);

                AcceptButton = ok;
                CancelButton = cancel;
                UpdateSelectedLabel();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    for (int i = 0; i < _imageLists.Count; i++)
                    {
                        if (_imageLists[i] != null) _imageLists[i].Dispose();
                    }
                }
                base.Dispose(disposing);
            }

            private void BuildPatternTabs(Document doc, string preferred)
            {
                List<PatternItem> items = LoadCadHatchPatternItems(doc);
                // 不再把上次保存的图案名强行塞入列表。
                // 如果当前 CAD 读取不到该图案，就不显示，避免选择到 CAD 无法生成的 Hatch。
                AddCommonPatternItems(items);

                AddPatternTab("ANSI", items, "ANSI", preferred);
                AddPatternTab("ISO", items, "ISO", preferred);
                AddPatternTab("其他预定义", items, "其他预定义", preferred);
                AddPatternTab("常用", items, "常用", preferred);
                AddPatternTab("自定义", items, "自定义", preferred);

                if (_tabs.TabPages.Count == 0)
                {
                    // 理论上至少会有“其他预定义”中的“无填充 / SOLID”。
                    // 这里保留一个空自定义页，避免窗口完全空白。
                    AddPatternTab("自定义", items, "自定义", preferred);
                }

                SelectPatternByName(preferred);
            }

            private void RebuildPatternTabs(string preferred)
            {
                for (int i = 0; i < _imageLists.Count; i++)
                {
                    if (_imageLists[i] != null) _imageLists[i].Dispose();
                }
                _imageLists.Clear();
                _views.Clear();
                _tabs.TabPages.Clear();
                BuildPatternTabs(_doc, preferred);
            }

            private void AddPatternTab(string title, List<PatternItem> allItems, string category, string preferred)
            {
                var list = new List<PatternItem>();
                for (int i = 0; i < allItems.Count; i++)
                {
                    PatternItem item = allItems[i];
                    if (item != null && string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase)) list.Add(item);
                }

                if (list.Count == 0 && !string.Equals(category, "自定义", StringComparison.OrdinalIgnoreCase)) return;

                var page = new TabPage(title);
                var view = new ListView();
                view.Dock = DockStyle.Fill;
                view.View = View.LargeIcon;
                view.MultiSelect = false;
                view.HideSelection = false;
                view.LabelWrap = true;
                view.BorderStyle = BorderStyle.FixedSingle;
                view.FullRowSelect = false;
                view.Activation = ItemActivation.OneClick;
                view.BackColor = SystemColors.Window;

                var images = new ImageList();
                images.ImageSize = new Size(48, 48);
                images.ColorDepth = ColorDepth.Depth32Bit;
                view.LargeImageList = images;
                _imageLists.Add(images);

                for (int i = 0; i < list.Count; i++)
                {
                    PatternItem item = list[i];
                    images.Images.Add(CreatePatternPreviewBitmap(item));
                    var lvi = new ListViewItem(item.Name, images.Images.Count - 1);
                    lvi.Tag = item;
                    lvi.ToolTipText = string.IsNullOrWhiteSpace(item.Description) ? item.Name : item.Name + " - " + item.Description;
                    view.Items.Add(lvi);
                }

                if (list.Count == 0)
                {
                    var empty = new Label();
                    empty.Text = "未读取到自定义 PAT 图案。";
                    empty.Dock = DockStyle.Fill;
                    empty.TextAlign = ContentAlignment.MiddleCenter;
                    page.Controls.Add(empty);
                }
                else
                {
                    view.SelectedIndexChanged += delegate
                    {
                        if (view.SelectedItems.Count > 0)
                        {
                            PatternItem item = view.SelectedItems[0].Tag as PatternItem;
                            if (item != null)
                            {
                                _selectedPatternName = item.Name;
                                if (string.Equals(item.Category, "常用", StringComparison.CurrentCultureIgnoreCase) && item.DefaultScale > 0)
                                {
                                    SetNumberValue(_numScale, (decimal)item.DefaultScale);
                                }
                                UpdateSelectedLabel();
                                ClearOtherViewSelections(view);
                            }
                        }
                    };
                    view.DoubleClick += delegate
                    {
                        if (view.SelectedItems.Count > 0)
                        {
                            DialogResult = DialogResult.OK;
                            Close();
                        }
                    };
                    page.Controls.Add(view);
                    _views.Add(view);
                }

                _tabs.TabPages.Add(page);
            }

            private void ClearOtherViewSelections(ListView keep)
            {
                for (int i = 0; i < _views.Count; i++)
                {
                    ListView view = _views[i];
                    if (view == null || object.ReferenceEquals(view, keep)) continue;
                    for (int j = 0; j < view.SelectedItems.Count; j++)
                    {
                        view.SelectedItems[j].Selected = false;
                    }
                }
            }

            private void SelectPatternByName(string patternName)
            {
                string target = NormalizePreferredPattern(patternName);
                bool found = false;

                for (int i = 0; i < _views.Count; i++)
                {
                    ListView view = _views[i];
                    if (view == null) continue;
                    for (int j = 0; j < view.Items.Count; j++)
                    {
                        ListViewItem item = view.Items[j];
                        bool hit = !found && string.Equals(item.Text, target, StringComparison.CurrentCultureIgnoreCase);
                        if (hit)
                        {
                            _tabs.SelectedTab = view.Parent as TabPage;
                            item.Selected = true;
                            item.Focused = true;
                            try { item.EnsureVisible(); } catch { }
                            _selectedPatternName = item.Text;
                            PatternItem patternItem = item.Tag as PatternItem;
                            if (patternItem != null && string.Equals(patternItem.Category, "常用", StringComparison.CurrentCultureIgnoreCase) && patternItem.DefaultScale > 0)
                            {
                                SetNumberValue(_numScale, (decimal)patternItem.DefaultScale);
                            }
                            found = true;
                        }
                        else
                        {
                            item.Selected = false;
                        }
                    }
                }

                if (!found)
                {
                    _selectedPatternName = "无填充";
                    for (int i = 0; i < _views.Count; i++)
                    {
                        ListView view = _views[i];
                        if (view == null) continue;
                        for (int j = 0; j < view.Items.Count; j++)
                        {
                            if (string.Equals(view.Items[j].Text, "无填充", StringComparison.CurrentCultureIgnoreCase))
                            {
                                _tabs.SelectedTab = view.Parent as TabPage;
                                view.Items[j].Selected = true;
                                view.Items[j].Focused = true;
                                try { view.Items[j].EnsureVisible(); } catch { }
                                break;
                            }
                        }
                    }
                }

                UpdateSelectedLabel();
            }

            private void UpdateSelectedLabel()
            {
                if (_lblSelected == null) return;
                string value = string.IsNullOrWhiteSpace(_selectedPatternName) ? "无填充" : _selectedPatternName;
                _lblSelected.Text = "当前选择：" + value;
            }

            private static string NormalizePreferredPattern(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return "无填充";
                value = value.Trim();
                if (string.Equals(value, "无", StringComparison.CurrentCultureIgnoreCase)) return "无填充";
                return value;
            }

            private static List<PatternItem> LoadCadHatchPatternItems(Document doc)
            {
                var result = new List<PatternItem>();
                AddPatternItem(result, "无填充", "其他预定义", "不生成填充");
                AddPatternItem(result, "SOLID", "其他预定义", "实体填充");

                List<PatternFileInfo> files = FindCadPatternFiles(doc);
                for (int i = 0; i < files.Count; i++)
                {
                    ParsePatternFile(files[i], result);
                }

                // 不再强行加入硬编码图案。
                // 除“无填充”和“SOLID”外，图案列表完全来自当前 CAD 可读取到的 .pat 文件。
                // 例如当前 CAD/CASS 环境中没有 ISO 图案，则 ISO 页不会出现对应图案。
                return result;
            }

            public static HashSet<string> GetAvailablePatternNameSet(Document doc)
            {
                var names = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                List<PatternItem> items = LoadCadHatchPatternItems(doc);
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] == null || string.IsNullOrWhiteSpace(items[i].Name)) continue;
                    names.Add(NormalizePreferredPattern(items[i].Name));
                }
                names.Add("无填充");
                names.Add("无");
                return names;
            }

            private static void AddCommonPatternItems(List<PatternItem> items)
            {
                List<CommonPatternInfo> common = LoadCommonPatterns();
                for (int i = 0; i < common.Count; i++)
                {
                    CommonPatternInfo c = common[i];
                    PatternItem source = FindPatternItem(items, c.Name);
                    // 常用页也不再强行加入当前 CAD 未读取到的图案。
                    // 例如 1064 或 ISO 图案在当前环境不存在时，不显示、不参与生成。
                    if (source == null) continue;
                    PatternItem item = AddPatternItem(items, c.Name, "常用", "常用图案，默认比例 " + c.Scale.ToString("0.###"));
                    item.DefaultScale = c.Scale;
                    if (source.Lines != null && source.Lines.Count > 0)
                    {
                        item.Lines = ClonePatternLines(source.Lines);
                    }
                }
            }

            private static PatternItem FindPatternItem(List<PatternItem> items, string name)
            {
                if (items == null || string.IsNullOrWhiteSpace(name)) return null;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null && !string.Equals(items[i].Category, "常用", StringComparison.CurrentCultureIgnoreCase) &&
                        string.Equals(items[i].Name, name, StringComparison.CurrentCultureIgnoreCase)) return items[i];
                }
                return null;
            }

            private static List<PatternLineDef> ClonePatternLines(List<PatternLineDef> lines)
            {
                var copy = new List<PatternLineDef>();
                if (lines == null) return copy;
                for (int i = 0; i < lines.Count; i++)
                {
                    PatternLineDef l = lines[i];
                    if (l == null) continue;
                    copy.Add(new PatternLineDef
                    {
                        Angle = l.Angle,
                        XOrigin = l.XOrigin,
                        YOrigin = l.YOrigin,
                        DeltaX = l.DeltaX,
                        DeltaY = l.DeltaY,
                        DashItems = l.DashItems == null ? new List<double>() : new List<double>(l.DashItems)
                    });
                }
                return copy;
            }

            private static List<PatternFileInfo> FindCadPatternFiles(Document doc)
            {
                var files = new List<PatternFileInfo>();
                Database db = doc == null ? null : doc.Database;

                string[] standardNames = new string[] { "acad.pat", "acadiso.pat" };
                for (int i = 0; i < standardNames.Length; i++)
                {
                    try
                    {
                        string path = HostApplicationServices.Current.FindFile(standardNames[i], db, FindFileHint.Default);
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) AddPatternFile(files, path, false);
                    }
                    catch { }
                }

                try
                {
                    object supportPathObj = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("ACADPREFIX");
                    string supportPaths = supportPathObj == null ? string.Empty : supportPathObj.ToString();
                    string[] parts = supportPaths.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string dir = parts[i].Trim();
                        if (dir.Length == 0 || !Directory.Exists(dir)) continue;
                        string[] patFiles = Directory.GetFiles(dir, "*.pat");
                        for (int j = 0; j < patFiles.Length; j++)
                        {
                            bool isStandard = IsStandardPatternFile(patFiles[j]);
                            AddPatternFile(files, patFiles[j], !isStandard);
                        }
                    }
                }
                catch { }

                return files;
            }

            private static void ParsePatternFile(PatternFileInfo fileInfo, List<PatternItem> result)
            {
                if (fileInfo == null || string.IsNullOrWhiteSpace(fileInfo.Path) || result == null) return;
                try
                {
                    string[] lines = File.ReadAllLines(fileInfo.Path, System.Text.Encoding.Default);
                    PatternItem current = null;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string raw = lines[i] == null ? string.Empty : lines[i];
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal)) continue;

                        if (line.Length >= 2 && line[0] == '*')
                        {
                            current = null;
                            if (line.StartsWith("*%", StringComparison.OrdinalIgnoreCase)) continue;

                            int comma = line.IndexOf(',');
                            string name = comma > 1 ? line.Substring(1, comma - 1).Trim() : line.Substring(1).Trim();
                            string description = comma > 0 && comma < line.Length - 1 ? line.Substring(comma + 1).Trim() : string.Empty;
                            string category = fileInfo.IsCustom ? "自定义" : GetPatternCategory(name);
                            current = AddPatternItem(result, name, category, description);
                            continue;
                        }

                        if (current != null)
                        {
                            PatternLineDef def = ParsePatternLine(line);
                            if (def != null) current.Lines.Add(def);
                        }
                    }
                }
                catch { }
            }

            private static PatternLineDef ParsePatternLine(string line)
            {
                if (string.IsNullOrWhiteSpace(line)) return null;
                string[] parts = line.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) return null;

                double angle;
                double xOrigin;
                double yOrigin;
                double deltaX;
                double deltaY;
                if (!TryParsePatternDouble(parts[0], out angle)) return null;
                if (!TryParsePatternDouble(parts[1], out xOrigin)) xOrigin = 0;
                if (!TryParsePatternDouble(parts[2], out yOrigin)) yOrigin = 0;
                if (!TryParsePatternDouble(parts[3], out deltaX)) deltaX = 0;
                if (!TryParsePatternDouble(parts[4], out deltaY)) deltaY = 0;

                var def = new PatternLineDef();
                def.Angle = angle;
                def.XOrigin = xOrigin;
                def.YOrigin = yOrigin;
                def.DeltaX = deltaX;
                def.DeltaY = deltaY;
                def.DashItems = new List<double>();
                for (int i = 5; i < parts.Length; i++)
                {
                    double d;
                    if (TryParsePatternDouble(parts[i], out d)) def.DashItems.Add(d);
                }
                return def;
            }

            private static bool TryParsePatternDouble(string text, out double value)
            {
                value = 0;
                if (text == null) return false;
                return double.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) ||
                       double.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out value);
            }

            private static string GetPatternCategory(string name)
            {
                if (string.IsNullOrWhiteSpace(name)) return "其他预定义";
                string upper = name.Trim().ToUpperInvariant();
                if (upper.StartsWith("ANSI", StringComparison.Ordinal)) return "ANSI";
                if (upper.StartsWith("ISO", StringComparison.Ordinal)) return "ISO";
                return "其他预定义";
            }

            private static bool IsStandardPatternFile(string path)
            {
                try
                {
                    string fileName = Path.GetFileName(path);
                    return string.Equals(fileName, "acad.pat", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(fileName, "acadiso.pat", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            }

            private static void AddPatternFile(List<PatternFileInfo> files, string path, bool isCustom)
            {
                if (files == null || string.IsNullOrWhiteSpace(path)) return;
                for (int i = 0; i < files.Count; i++)
                {
                    if (string.Equals(files[i].Path, path, StringComparison.OrdinalIgnoreCase)) return;
                }
                files.Add(new PatternFileInfo { Path = path, IsCustom = isCustom });
            }

            private static PatternItem AddPatternItem(List<PatternItem> items, string name, string category, string description)
            {
                if (items == null || string.IsNullOrWhiteSpace(name)) return new PatternItem();
                name = name.Trim();
                if (string.Equals(name, "无", StringComparison.CurrentCultureIgnoreCase)) name = "无填充";
                string cat = string.IsNullOrWhiteSpace(category) ? "其他预定义" : category;
                for (int i = 0; i < items.Count; i++)
                {
                    if (string.Equals(items[i].Name, name, StringComparison.CurrentCultureIgnoreCase) &&
                        string.Equals(items[i].Category, cat, StringComparison.CurrentCultureIgnoreCase)) return items[i];
                }
                var item = new PatternItem
                {
                    Name = name,
                    Category = cat,
                    Description = description ?? string.Empty,
                    Lines = new List<PatternLineDef>()
                };
                items.Add(item);
                return item;
            }

            private static Bitmap CreatePatternPreviewBitmap(PatternItem item)
            {
                string patternName = item == null ? string.Empty : item.Name;
                var bmp = new Bitmap(48, 48);
                using (Graphics g = Graphics.FromImage(bmp))
                using (var bg = new SolidBrush(Color.White))
                using (var pen = new Pen(Color.Black, 1))
                using (var grayPen = new Pen(Color.DimGray, 1))
                using (var blackBrush = new SolidBrush(Color.Black))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.FillRectangle(bg, 0, 0, bmp.Width, bmp.Height);
                    string upper = string.IsNullOrWhiteSpace(patternName) ? string.Empty : patternName.Trim().ToUpperInvariant();

                    if (string.Equals(upper, "SOLID", StringComparison.Ordinal))
                    {
                        g.FillRectangle(blackBrush, 4, 4, 40, 40);
                    }
                    else if (string.Equals(upper, "无填充", StringComparison.OrdinalIgnoreCase) || string.Equals(upper, "无", StringComparison.OrdinalIgnoreCase))
                    {
                        g.DrawLine(grayPen, 8, 8, 40, 40);
                        g.DrawLine(grayPen, 40, 8, 8, 40);
                    }
                    else if (item != null && item.Lines != null && item.Lines.Count > 0)
                    {
                        DrawPatPreview(g, pen, item.Lines, upper);
                    }
                    else if (upper.Contains("BRICK") || upper.Contains("B816") || upper.Contains("B88"))
                    {
                        DrawBrickPreview(g, pen);
                    }
                    else if (upper.Contains("CONC") || upper.Contains("GRAVEL") || upper.Contains("AR-RROOF") || upper.Contains("SAND") || upper.Contains("EARTH"))
                    {
                        DrawDotPreview(g, pen, upper.Contains("SAND") ? 28 : 42);
                    }
                    else if (upper.Contains("BOX") || upper.Contains("SQUARE") || upper.Contains("NET") || upper.Contains("DASH") || upper.Contains("LINE"))
                    {
                        DrawGridPreview(g, pen, upper);
                    }
                    else if (upper.Contains("HONEY") || upper.Contains("HEX"))
                    {
                        DrawHexPreview(g, pen);
                    }
                    else if (upper.Contains("PARQ") || upper.Contains("HBONE") || upper.Contains("HOUND"))
                    {
                        DrawCrossPreview(g, pen);
                    }
                    else if (upper.Contains("ANGLE") || upper.StartsWith("ANSI", StringComparison.Ordinal) || upper.StartsWith("ISO", StringComparison.Ordinal) || upper.Contains("STEEL") || upper.Contains("BRASS"))
                    {
                        DrawDiagonalPreview(g, pen, upper.Contains("ANSI37") || upper.Contains("CROSS"));
                    }
                    else
                    {
                        DrawGenericPreview(g, pen);
                    }

                    g.DrawRectangle(grayPen, 0, 0, 47, 47);
                }
                return bmp;
            }

            private static void DrawPatPreview(Graphics g, Pen pen, List<PatternLineDef> lines, string patternName)
            {
                if (g == null || pen == null || lines == null || lines.Count == 0)
                {
                    DrawGenericPreview(g, pen);
                    return;
                }

                System.Drawing.Drawing2D.GraphicsState state = g.Save();
                try
                {
                    Rectangle preview = new Rectangle(4, 4, 40, 40);
                    g.SetClip(preview);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

                    double scale = ComputePatternPreviewScale(lines, patternName);
                    double centerX;
                    double centerY;
                    ComputePatternPreviewCenter(lines, out centerX, out centerY);

                    int maxLines = Math.Min(lines.Count, 64);
                    for (int i = 0; i < maxLines; i++)
                    {
                        PatternLineDef def = lines[i];
                        if (def == null) continue;
                        DrawPatternFamily(g, pen, def, scale, centerX, centerY);
                    }
                }
                catch
                {
                    try { g.ResetClip(); } catch { }
                    DrawGenericPreview(g, pen);
                }
                finally
                {
                    g.Restore(state);
                }
            }

            private static double ComputePatternPreviewScale(List<PatternLineDef> lines, string patternName)
            {
                var values = new List<double>();
                for (int i = 0; i < lines.Count; i++)
                {
                    PatternLineDef def = lines[i];
                    if (def == null) continue;
                    double dy = Math.Abs(def.DeltaY);
                    double dx = Math.Abs(def.DeltaX);
                    if (dy > 0.000001) values.Add(dy);
                    if (dx > 0.000001 && dx < 10.0) values.Add(dx);
                    if (def.DashItems != null)
                    {
                        for (int j = 0; j < def.DashItems.Count; j++)
                        {
                            double d = Math.Abs(def.DashItems[j]);
                            if (d > 0.000001 && d < 10.0) values.Add(d);
                        }
                    }
                }

                double basis = MedianPositive(values);
                if (basis <= 0.000001) basis = 0.125;

                string upper = string.IsNullOrWhiteSpace(patternName) ? string.Empty : patternName.Trim().ToUpperInvariant();
                double target = 8.0;
                if (upper.Contains("CONC") || upper.Contains("SAND") || upper.Contains("EARTH") || upper.Contains("GRAVEL")) target = 5.0;
                if (upper.Contains("BRICK") || upper.Contains("B816") || upper.Contains("B88")) target = 7.0;
                if (upper.Contains("HEX") || upper.Contains("HONEY")) target = 7.0;

                double scale = target / basis;
                if (scale < 2.0) scale = 2.0;
                if (scale > 260.0) scale = 260.0;
                return scale;
            }

            private static double MedianPositive(List<double> values)
            {
                if (values == null || values.Count == 0) return 0.0;
                values.Sort();
                var filtered = new List<double>();
                for (int i = 0; i < values.Count; i++)
                {
                    double v = values[i];
                    if (v > 0.000001 && v < 1000.0) filtered.Add(v);
                }
                if (filtered.Count == 0) return 0.0;
                return filtered[filtered.Count / 2];
            }

            private static void ComputePatternPreviewCenter(List<PatternLineDef> lines, out double centerX, out double centerY)
            {
                centerX = 0.0;
                centerY = 0.0;
                if (lines == null || lines.Count == 0) return;

                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double maxX = double.MinValue;
                double maxY = double.MinValue;
                bool has = false;

                for (int i = 0; i < lines.Count; i++)
                {
                    PatternLineDef def = lines[i];
                    if (def == null) continue;
                    double x = def.XOrigin;
                    double y = def.YOrigin;
                    if (Math.Abs(x) > 100000.0 || Math.Abs(y) > 100000.0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    has = true;
                }

                if (!has) return;
                centerX = (minX + maxX) / 2.0;
                centerY = (minY + maxY) / 2.0;
            }

            private static void DrawPatternFamily(Graphics g, Pen pen, PatternLineDef def, double scale, double centerX, double centerY)
            {
                double angleRad = def.Angle * Math.PI / 180.0;
                double ux = Math.Cos(angleRad);
                double uy = Math.Sin(angleRad);
                double nx = -uy;
                double ny = ux;

                // PAT 中 delta-x 表示沿线方向位移，delta-y 表示相邻平行线间距。
                double stepX = def.DeltaX * ux + def.DeltaY * nx;
                double stepY = def.DeltaX * uy + def.DeltaY * ny;
                double stepLen = Math.Sqrt(stepX * stepX + stepY * stepY);
                if (stepLen < 0.000001)
                {
                    stepX = nx * 0.125;
                    stepY = ny * 0.125;
                    stepLen = 0.125;
                }

                int repeat = (int)Math.Ceiling(4.0 / Math.Max(stepLen * scale / 40.0, 0.05));
                if (repeat < 8) repeat = 8;
                if (repeat > 80) repeat = 80;

                double worldHalf = 60.0 / Math.Max(scale, 0.000001);
                for (int k = -repeat; k <= repeat; k++)
                {
                    double baseX = def.XOrigin + stepX * k;
                    double baseY = def.YOrigin + stepY * k;
                    DrawPatternDashLine(g, pen, baseX, baseY, ux, uy, def.DashItems, scale, centerX, centerY, worldHalf);
                }
            }

            private static void DrawPatternDashLine(Graphics g, Pen pen, double baseX, double baseY, double ux, double uy, List<double> dashItems, double scale, double centerX, double centerY, double worldHalf)
            {
                if (dashItems == null || dashItems.Count == 0)
                {
                    DrawPatternWorldLine(g, pen, baseX - ux * worldHalf, baseY - uy * worldHalf, baseX + ux * worldHalf, baseY + uy * worldHalf, scale, centerX, centerY);
                    return;
                }

                double cycle = 0.0;
                for (int i = 0; i < dashItems.Count; i++) cycle += Math.Abs(dashItems[i]);
                if (cycle < 0.000001)
                {
                    for (double dotT = -worldHalf; dotT <= worldHalf; dotT += 0.125)
                    {
                        DrawPatternWorldDot(g, pen, baseX + ux * dotT, baseY + uy * dotT, scale, centerX, centerY);
                    }
                    return;
                }

                double start = -worldHalf - cycle * 2.0;
                double end = worldHalf + cycle * 2.0;
                double dashT = start;
                int guard = 0;
                while (dashT < end && guard < 1000)
                {
                    for (int i = 0; i < dashItems.Count && dashT < end; i++)
                    {
                        double raw = dashItems[i];
                        double len = Math.Abs(raw);
                        double next = dashT + len;
                        if (raw > 0)
                        {
                            double a = Math.Max(dashT, -worldHalf);
                            double b = Math.Min(next, worldHalf);
                            if (b > a)
                            {
                                DrawPatternWorldLine(g, pen, baseX + ux * a, baseY + uy * a, baseX + ux * b, baseY + uy * b, scale, centerX, centerY);
                            }
                        }
                        else if (Math.Abs(raw) < 0.000001)
                        {
                            if (dashT >= -worldHalf && dashT <= worldHalf) DrawPatternWorldDot(g, pen, baseX + ux * dashT, baseY + uy * dashT, scale, centerX, centerY);
                        }
                        dashT = next;
                        guard++;
                    }
                }
            }

            private static void DrawPatternWorldLine(Graphics g, Pen pen, double x1, double y1, double x2, double y2, double scale, double centerX, double centerY)
            {
                PointF p1 = PatternWorldToPixel(x1, y1, scale, centerX, centerY);
                PointF p2 = PatternWorldToPixel(x2, y2, scale, centerX, centerY);
                g.DrawLine(pen, p1, p2);
            }

            private static void DrawPatternWorldDot(Graphics g, Pen pen, double x, double y, double scale, double centerX, double centerY)
            {
                PointF p = PatternWorldToPixel(x, y, scale, centerX, centerY);
                g.DrawRectangle(pen, p.X, p.Y, 1, 1);
            }

            private static PointF PatternWorldToPixel(double x, double y, double scale, double centerX, double centerY)
            {
                return new PointF((float)(24.0 + (x - centerX) * scale), (float)(24.0 - (y - centerY) * scale));
            }

            private static void DrawDiagonalPreview(Graphics g, Pen pen, bool cross)
            {
                for (int i = -48; i <= 96; i += 8)
                {
                    g.DrawLine(pen, i, 48, i + 48, 0);
                }
                if (cross)
                {
                    for (int i = -48; i <= 96; i += 8)
                    {
                        g.DrawLine(pen, i, 0, i + 48, 48);
                    }
                }
            }

            private static void DrawBrickPreview(Graphics g, Pen pen)
            {
                for (int y = 4; y <= 44; y += 8) g.DrawLine(pen, 4, y, 44, y);
                for (int y = 4; y < 44; y += 8)
                {
                    int offset = ((y / 8) % 2 == 0) ? 4 : 12;
                    for (int x = offset; x <= 44; x += 16) g.DrawLine(pen, x, y, x, y + 8);
                }
            }

            private static void DrawGridPreview(Graphics g, Pen pen, string upper)
            {
                int step = upper != null && upper.Contains("DASH") ? 10 : 8;
                for (int x = 4; x <= 44; x += step) g.DrawLine(pen, x, 4, x, 44);
                for (int y = 4; y <= 44; y += step) g.DrawLine(pen, 4, y, 44, y);
            }

            private static void DrawDotPreview(Graphics g, Pen pen, int count)
            {
                int seed = 17;
                for (int i = 0; i < count; i++)
                {
                    seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                    int x = 4 + (seed % 40);
                    seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                    int y = 4 + (seed % 40);
                    g.DrawEllipse(pen, x, y, 1, 1);
                }
                for (int i = 0; i < count / 5; i++)
                {
                    int x = 6 + (i * 11) % 34;
                    int y = 8 + (i * 17) % 32;
                    g.DrawLine(pen, x, y, x + 4, y + 2);
                }
            }

            private static void DrawHexPreview(Graphics g, Pen pen)
            {
                for (int y = 6; y <= 42; y += 14)
                {
                    for (int x = 7; x <= 42; x += 16)
                    {
                        Point[] pts = new Point[]
                        {
                            new Point(x, y + 4), new Point(x + 4, y), new Point(x + 10, y),
                            new Point(x + 14, y + 4), new Point(x + 10, y + 8), new Point(x + 4, y + 8),
                            new Point(x, y + 4)
                        };
                        g.DrawLines(pen, pts);
                    }
                }
            }

            private static void DrawCrossPreview(Graphics g, Pen pen)
            {
                for (int y = 4; y <= 40; y += 12)
                {
                    for (int x = 4; x <= 40; x += 12)
                    {
                        g.DrawRectangle(pen, x, y, 8, 8);
                        g.DrawLine(pen, x, y, x + 8, y + 8);
                        g.DrawLine(pen, x + 8, y, x, y + 8);
                    }
                }
            }

            private static void DrawGenericPreview(Graphics g, Pen pen)
            {
                for (int y = 5; y <= 44; y += 7)
                {
                    g.DrawLine(pen, 4, y, 44, y);
                }
                for (int x = 5; x <= 44; x += 11)
                {
                    g.DrawLine(pen, x, 4, x, 44);
                }
            }

            private sealed class PatternItem
            {
                public string Name;
                public string Category;
                public string Description;
                public double DefaultScale;
                public List<PatternLineDef> Lines = new List<PatternLineDef>();
            }

            private sealed class PatternLineDef
            {
                public double Angle;
                public double XOrigin;
                public double YOrigin;
                public double DeltaX;
                public double DeltaY;
                public List<double> DashItems = new List<double>();
            }

            private sealed class CommonPatternInfo
            {
                public string Name;
                public double Scale;
            }

            private static List<CommonPatternInfo> LoadCommonPatterns()
            {
                var list = new List<CommonPatternInfo>();
                string path = GetCommonPatternFilePath();
                try
                {
                    if (File.Exists(path))
                    {
                        string[] lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            string line = lines[i] == null ? string.Empty : lines[i].Trim();
                            if (line.Length == 0) continue;
                            string[] parts = line.Split('|');
                            if (parts.Length == 0) continue;
                            string name = parts[0].Trim();
                            double scale = 1.0;
                            if (parts.Length > 1) TryParsePatternDouble(parts[1], out scale);
                            if (scale <= 0) scale = 1.0;
                            AddCommonPattern(list, name, scale);
                        }
                        return list;
                    }
                }
                catch { }

                AddCommonPattern(list, "AR-CONC", 0.01);
                AddCommonPattern(list, "EARTH", 0.1);
                AddCommonPattern(list, "HEX", 0.1);
                AddCommonPattern(list, "1064", 0.01);
                return list;
            }

            private static void AddOrUpdateCommonPattern(string name, double scale)
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                var list = LoadCommonPatterns();
                bool found = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].Name, name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        list[i].Scale = scale <= 0 ? 1.0 : scale;
                        found = true;
                        break;
                    }
                }
                if (!found) AddCommonPattern(list, name, scale <= 0 ? 1.0 : scale);
                SaveCommonPatterns(list);
            }

            private static void RemoveCommonPattern(string name)
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                var list = LoadCommonPatterns();
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(list[i].Name, name, StringComparison.CurrentCultureIgnoreCase)) list.RemoveAt(i);
                }
                SaveCommonPatterns(list);
            }

            private static void AddCommonPattern(List<CommonPatternInfo> list, string name, double scale)
            {
                if (list == null || string.IsNullOrWhiteSpace(name)) return;
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].Name, name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        list[i].Scale = scale;
                        return;
                    }
                }
                list.Add(new CommonPatternInfo { Name = name.Trim(), Scale = scale <= 0 ? 1.0 : scale });
            }

            private static void SaveCommonPatterns(List<CommonPatternInfo> list)
            {
                try
                {
                    string path = GetCommonPatternFilePath();
                    string dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    var lines = new List<string>();
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (list[i] == null || string.IsNullOrWhiteSpace(list[i].Name)) continue;
                            lines.Add(list[i].Name.Trim() + "|" + list[i].Scale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                        }
                    }
                    File.WriteAllLines(path, lines.ToArray(), System.Text.Encoding.UTF8);
                }
                catch { }
            }

            private static string GetCommonPatternFilePath()
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CDBox");
                return Path.Combine(dir, "SectionDrawing.CommonPatterns.txt");
            }

            private sealed class PatternFileInfo
            {
                public string Path;
                public bool IsCustom;
            }
        }

        private sealed class PipeSettingDialog : Form
        {
            private readonly FlowLayoutPanel _rows;
            private readonly List<PipeRow> _pipeRows = new List<PipeRow>();

            public List<SectionPipeOptions> Pipes
            {
                get
                {
                    var list = new List<SectionPipeOptions>();
                    foreach (PipeRow row in _pipeRows)
                    {
                        if (row == null || row.Deleted) continue;
                        list.Add(row.ReadPipe());
                    }
                    return list;
                }
            }

            public PipeSettingDialog(string layerName, List<SectionPipeOptions> pipes)
            {
                Text = "管断面设置 - " + (string.IsNullOrWhiteSpace(layerName) ? "未命名层" : layerName);
                Width = 560;
                Height = 360;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;

                var root = new TableLayoutPanel();
                root.Dock = DockStyle.Fill;
                root.Padding = new Padding(10);
                root.ColumnCount = 1;
                root.RowCount = 3;
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                Controls.Add(root);

                var top = new FlowLayoutPanel();
                top.Dock = DockStyle.Fill;
                top.AutoSize = true;
                top.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
                root.Controls.Add(top, 0, 0);

                var add = new Button();
                add.Text = "添加管道";
                add.AutoSize = true;
                add.Click += delegate { AddPipeRow(SectionPipeOptions.Default); };
                top.Controls.Add(add);

                _rows = new FlowLayoutPanel();
                _rows.Dock = DockStyle.Fill;
                _rows.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
                _rows.WrapContents = false;
                _rows.AutoScroll = true;
                root.Controls.Add(_rows, 0, 1);

                if (pipes != null)
                {
                    foreach (SectionPipeOptions pipe in pipes)
                    {
                        if (pipe != null) AddPipeRow(pipe);
                    }
                }

                var buttons = new FlowLayoutPanel();
                buttons.Dock = DockStyle.Fill;
                buttons.AutoSize = true;
                buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
                root.Controls.Add(buttons, 0, 2);

                var ok = new Button();
                ok.Text = "确定";
                ok.DialogResult = DialogResult.OK;
                ok.AutoSize = true;
                buttons.Controls.Add(ok);

                var cancel = new Button();
                cancel.Text = "取消";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.AutoSize = true;
                buttons.Controls.Add(cancel);

                AcceptButton = ok;
                CancelButton = cancel;
            }

            private void AddPipeRow(SectionPipeOptions pipe)
            {
                var row = new PipeRow(pipe, delegate(PipeRow r)
                {
                    r.Deleted = true;
                    _rows.Controls.Remove(r.Root);
                    _pipeRows.Remove(r);
                });
                _pipeRows.Add(row);
                _rows.Controls.Add(row.Root);
            }

            private sealed class PipeRow
            {
                public Panel Root { get; private set; }
                public bool Deleted { get; set; }
                private readonly NumericUpDown _diameter;
                private readonly TextBox _text;
                private readonly ComboBox _mode;

                public PipeRow(SectionPipeOptions pipe, Action<PipeRow> deleteAction)
                {
                    if (pipe == null) pipe = SectionPipeOptions.Default;

                    Root = new Panel();
                    Root.Width = 510;
                    Root.Height = 72;
                    Root.BorderStyle = BorderStyle.FixedSingle;
                    Root.Padding = new Padding(6);
                    Root.Margin = new Padding(2, 2, 2, 6);

                    var table = new TableLayoutPanel();
                    table.Dock = DockStyle.Fill;
                    table.ColumnCount = 6;
                    table.RowCount = 2;
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
                    Root.Controls.Add(table);

                    table.Controls.Add(MakeSimpleLabel("管径(m)"), 0, 0);
                    _diameter = MakeNumber(0.001M, 100000M, (decimal)Math.Max(0.001, pipe.Diameter), 3);
                    _diameter.ValueChanged += delegate
                    {
                        if (string.IsNullOrWhiteSpace(_text.Text) || _text.Text.Trim().StartsWith("DN", StringComparison.OrdinalIgnoreCase))
                        {
                            _text.Text = SectionPipeOptions.BuildPipeText((double)_diameter.Value);
                        }
                    };
                    table.Controls.Add(_diameter, 1, 0);

                    table.Controls.Add(MakeSimpleLabel("注记"), 2, 0);
                    _text = new TextBox();
                    _text.Dock = DockStyle.Fill;
                    _text.Text = string.IsNullOrWhiteSpace(pipe.PipeText) ? SectionPipeOptions.BuildPipeText(pipe.Diameter) : pipe.PipeText;
                    table.Controls.Add(_text, 3, 0);

                    var auto = new Button();
                    auto.Text = "自动";
                    auto.AutoSize = true;
                    auto.Click += delegate { _text.Text = SectionPipeOptions.BuildPipeText((double)_diameter.Value); };
                    table.Controls.Add(auto, 4, 0);

                    var del = new Button();
                    del.Text = "删除";
                    del.AutoSize = true;
                    del.Click += delegate { if (deleteAction != null) deleteAction(this); };
                    table.Controls.Add(del, 5, 0);

                    table.Controls.Add(MakeSimpleLabel("竖向位置"), 0, 1);
                    _mode = new ComboBox();
                    _mode.Dock = DockStyle.Fill;
                    _mode.DropDownStyle = ComboBoxStyle.DropDownList;
                    _mode.Items.Add("该层底");
                    _mode.Items.Add("该层中");
                    _mode.SelectedIndex = pipe.VerticalMode == SectionPipeVerticalMode.LayerBottom ? 0 : 1;
                    table.Controls.Add(_mode, 1, 1);
                    table.SetColumnSpan(_mode, 2);
                }

                public SectionPipeOptions ReadPipe()
                {
                    return new SectionPipeOptions
                    {
                        Diameter = (double)_diameter.Value,
                        PipeText = _text.Text,
                        VerticalMode = _mode.SelectedIndex == 0 ? SectionPipeVerticalMode.LayerBottom : SectionPipeVerticalMode.LayerCenter
                    };
                }

                private static Label MakeSimpleLabel(string text)
                {
                    var label = new Label();
                    label.Text = text;
                    label.Dock = DockStyle.Fill;
                    label.TextAlign = ContentAlignment.MiddleLeft;
                    return label;
                }
            }
        }
    }
}
