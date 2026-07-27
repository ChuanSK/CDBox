using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using TCPipeAutoDraw.Modules.NodeAnnotation;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;
using TCPipeAutoDraw.Modules.SurfaceAreaAnnotation;
using TCPipeAutoDraw.Core.Colors;
using TCPipeAutoDraw.UI.Controls;

namespace TCPipeAutoDraw.Modules.AnnotationSettings
{
    /// <summary>
    /// 标注设置统一界面。
    /// 合并表面积标注、管线长度标注、节点标注三类设置，侧边栏/合集界面统一进入本窗口。
    /// </summary>
    public sealed class AnnotationSettingsForm : Form
    {
        private readonly Document _doc;
        private readonly SurfaceAreaAnnotationOptions _surfaceInitialOptions;
        private readonly PipeLengthAnnotationOptions _pipeInitialOptions;
        private readonly NodeAnnotationOptions _nodeInitialOptions;
        private readonly List<string> _layerNames;
        private readonly List<string> _textStyleNames;

        private TabControl _tabs;
        private Button _btnRunCurrent;

        private NumericUpDown _surfInterval;
        private NumericUpDown _surfTextHeight;
        private NumericUpDown _surfDecimals;
        private CheckBox _surfKeepCassObjects;
        private ComboBox _surfFonts;
        private TextBox _surfTemplate;
        private RadioButton _surfRadDefaultZJ;
        private RadioButton _surfRadExistingLayer;
        private RadioButton _surfRadCustomLayer;
        private ComboBox _surfLayers;
        private TextBox _surfCustomLayer;

        private NumericUpDown _pipeTextHeight;
        private NumericUpDown _pipePrecision;
        private ComboBox _pipeFonts;
        private TextBox _pipeTemplate;
        private RadioButton _pipeRadDefaultZJ;
        private RadioButton _pipeRadExistingLayer;
        private RadioButton _pipeRadCustomLayer;
        private ComboBox _pipeLayers;
        private TextBox _pipeCustomLayer;
        private CheckBox _pipeAutoLayerBySourceMetadata;
        private ComboBox _pipeLayerLinkMode;
        private TextBox _pipeLayerSuffix;
        private TextBox _pipeFallbackLayer;
        private CheckBox _pipeWriteAutoLayerMetadata;
        private TextBox _pipeSplitTags;
        private CheckBox _pipeBottomAnnotation;
        private NumericUpDown _pipeExcavationWidth;
        private NumericUpDown _pipeExcavationHeight;
        private NumericUpDown _pipeExcavationDepth;
        private TextBox _pipeBottomTemplate;

        private NumericUpDown _nodeTextHeight;
        private ComboBox _nodeFonts;
        private RadioButton _nodeRadDefaultZJ;
        private RadioButton _nodeRadExistingLayer;
        private RadioButton _nodeRadCustomLayer;
        private ComboBox _nodeLayers;
        private TextBox _nodeCustomLayer;
        private NumericUpDown _nodeLineSpacing;
        private NumericUpDown _nodeNoColor;
        private NumericUpDown _nodeTextColor;
        private NumericUpDown _nodePreviewLeaderColor;

        public AnnotationSettingsForm(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            _doc = doc;
            _surfaceInitialOptions = SurfaceAreaAnnotationSettingsStore.Load();
            _pipeInitialOptions = PipeLengthAnnotationSettingsStore.Load();
            _nodeInitialOptions = NodeAnnotationSettingsStore.Load();
            _layerNames = LoadLayerNames();
            _textStyleNames = LoadTextStyleNames();

            Text = "标注设置（制作：氚）";
            Width = 840;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;

            BuildUi();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var title = new Label();
            title.Text = "标注设置";
            title.AutoSize = true;
            title.Font = new System.Drawing.Font(title.Font.FontFamily, 12.5f, System.Drawing.FontStyle.Bold);
            title.Padding = new Padding(0, 0, 0, 8);
            root.Controls.Add(title, 0, 0);

            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.TabPages.Add(BuildSurfaceTab());
            _tabs.TabPages.Add(BuildPipeTab());
            _tabs.TabPages.Add(BuildNodeTab());
            _tabs.SelectedIndexChanged += delegate { UpdateRunButtonText(); };
            root.Controls.Add(_tabs, 0, 1);

            var tip = new Label();
            tip.Text = "说明：本界面统一保存三类标注设置；命令行直跑标注时会继续读取这里保存的上次设置。";
            tip.AutoSize = true;
            tip.ForeColor = SystemColors.GrayText;
            tip.Padding = new Padding(0, 8, 0, 4);
            root.Controls.Add(tip, 0, 2);

            var buttons = new FlowLayoutPanel();
            buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            root.Controls.Add(buttons, 0, 3);

            var close = new Button();
            close.Text = "关闭";
            close.AutoSize = true;
            close.Click += delegate { Close(); };
            buttons.Controls.Add(close);

            var save = new Button();
            save.Text = "保存设置";
            save.Width = 120;
            save.Height = 30;
            save.Click += delegate { SaveAllSettings(true); };
            buttons.Controls.Add(save);

            _btnRunCurrent = new Button();
            _btnRunCurrent.Width = 150;
            _btnRunCurrent.Height = 30;
            _btnRunCurrent.Click += delegate { RunCurrentAnnotation(); };
            buttons.Controls.Add(_btnRunCurrent);
            UpdateRunButtonText();
        }

        private TabPage BuildSurfaceTab()
        {
            var page = new TabPage("表面积");
            var panel = CreateScrollablePanel();
            page.Controls.Add(panel);

            var table = CreateOptionsTable(10);
            panel.Controls.Add(table);

            _surfInterval = MakeNumber(0.1M, 1000M, 5M, 1);
            SetNumberValue(_surfInterval, (decimal)_surfaceInitialOptions.BoundaryInterval);
            AddRow(table, 0, "边界插值间隔（米）", _surfInterval);

            var calcLabel = MakeValueLabel("调用 CASS surfacearea 计算（固定）");
            AddRow(table, 1, "计算方式", calcLabel);

            _surfKeepCassObjects = new CheckBox();
            _surfKeepCassObjects.Text = "保留 CASS 生成的三角网和三角面积文字（默认不保留）";
            _surfKeepCassObjects.AutoSize = true;
            _surfKeepCassObjects.Checked = !_surfaceInitialOptions.DeleteCassGeneratedObjects;
            AddRow(table, 2, "CASS生成物", _surfKeepCassObjects);

            _surfTextHeight = MakeNumber(0.1M, 1000M, 1M, 1);
            SetNumberValue(_surfTextHeight, (decimal)_surfaceInitialOptions.TextHeight);
            AddRow(table, 3, "注记文字高度", _surfTextHeight);

            _surfDecimals = MakeNumber(0M, 6M, 2M, 0);
            SetNumberValue(_surfDecimals, _surfaceInitialOptions.DecimalPlaces);
            AddRow(table, 4, "面积小数位", _surfDecimals);

            _surfFonts = MakeCombo(_textStyleNames, 260);
            SelectDefaultTextStyle(_surfFonts, _surfaceInitialOptions.AnnotationFontName);
            AddRow(table, 5, "字体样式", _surfFonts);

            BuildLayerRows(
                table,
                6,
                _surfaceInitialOptions.LayerMode,
                _surfaceInitialOptions.SelectedLayerName,
                _surfaceInitialOptions.AnnotationLayerName,
                out _surfRadDefaultZJ,
                out _surfRadExistingLayer,
                out _surfRadCustomLayer,
                out _surfLayers,
                out _surfCustomLayer,
                UpdateSurfaceLayerControls);

            _surfTemplate = new TextBox();
            _surfTemplate.Multiline = false;
            _surfTemplate.Text = string.IsNullOrWhiteSpace(_surfaceInitialOptions.AnnotationTemplate)
                ? SurfaceAreaAnnotationOptions.Default.AnnotationTemplate
                : _surfaceInitialOptions.AnnotationTemplate;
            AddRow(table, 9, "注记模板", _surfTemplate);

            UpdateSurfaceLayerControls();
            return page;
        }

        private TabPage BuildPipeTab()
        {
            var page = new TabPage("管线长度");
            var panel = CreateScrollablePanel();
            page.Controls.Add(panel);

            var table = CreateOptionsTable(14);
            panel.Controls.Add(table);

            _pipeTextHeight = MakeNumber(0.1M, 1000M, 1M, 1);
            SetNumberValue(_pipeTextHeight, (decimal)_pipeInitialOptions.TextHeight);
            AddRow(table, 0, "注记文字高度", _pipeTextHeight);

            _pipePrecision = MakeNumber(0M, 6M, 2M, 0);
            SetNumberValue(_pipePrecision, _pipeInitialOptions.DecimalPlaces);
            AddRow(table, 1, "精确位数", _pipePrecision);

            _pipeFonts = MakeCombo(_textStyleNames, 260);
            SelectDefaultTextStyle(_pipeFonts, _pipeInitialOptions.AnnotationFontName);
            AddRow(table, 2, "字体样式", _pipeFonts);

            BuildLayerRows(
                table,
                3,
                _pipeInitialOptions.LayerMode,
                _pipeInitialOptions.SelectedLayerName,
                _pipeInitialOptions.AnnotationLayerName,
                out _pipeRadDefaultZJ,
                out _pipeRadExistingLayer,
                out _pipeRadCustomLayer,
                out _pipeLayers,
                out _pipeCustomLayer,
                UpdatePipeLayerControls);

            AddRow(table, 6, "属性联动", BuildPipeLayerLinkPanel());

            _pipeTemplate = new TextBox();
            _pipeTemplate.Multiline = false;
            _pipeTemplate.Text = string.IsNullOrWhiteSpace(_pipeInitialOptions.AnnotationTemplate)
                ? PipeLengthAnnotationOptions.Default.AnnotationTemplate
                : _pipeInitialOptions.AnnotationTemplate;
            AddRow(table, 7, "上方注记模板", _pipeTemplate);

            _pipeBottomAnnotation = new CheckBox();
            _pipeBottomAnnotation.Text = "对象未设置属性仍生成横线下方注记";
            _pipeBottomAnnotation.AutoSize = true;
            _pipeBottomAnnotation.Checked = _pipeInitialOptions.DrawBottomAnnotation;
            _pipeBottomAnnotation.CheckedChanged += delegate { UpdatePipeBottomAnnotationControls(); };
            AddRow(table, 8, "下方注记", _pipeBottomAnnotation);

            var excavationPanel = new FlowLayoutPanel();
            excavationPanel.Dock = DockStyle.Fill;
            excavationPanel.AutoSize = true;
            excavationPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;

            excavationPanel.Controls.Add(MakeInlineLabel("宽"));
            _pipeExcavationWidth = MakeNumber(0M, 100000M, 0M, 3);
            SetNumberValue(_pipeExcavationWidth, (decimal)_pipeInitialOptions.ExcavationWidth);
            excavationPanel.Controls.Add(_pipeExcavationWidth);

            excavationPanel.Controls.Add(MakeInlineLabel("高"));
            _pipeExcavationHeight = MakeNumber(0M, 100000M, 0M, 3);
            SetNumberValue(_pipeExcavationHeight, (decimal)_pipeInitialOptions.ExcavationHeight);
            excavationPanel.Controls.Add(_pipeExcavationHeight);

            excavationPanel.Controls.Add(MakeInlineLabel("深"));
            _pipeExcavationDepth = MakeNumber(0M, 100000M, 0M, 3);
            SetNumberValue(_pipeExcavationDepth, (decimal)_pipeInitialOptions.ExcavationDepth);
            excavationPanel.Controls.Add(_pipeExcavationDepth);
            AddRow(table, 9, "自定义开挖参数", excavationPanel);

            _pipeBottomTemplate = new TextBox();
            _pipeBottomTemplate.Multiline = false;
            _pipeBottomTemplate.Text = string.IsNullOrWhiteSpace(_pipeInitialOptions.BottomAnnotationTemplate)
                ? PipeLengthAnnotationOptions.Default.BottomAnnotationTemplate
                : _pipeInitialOptions.BottomAnnotationTemplate;
            AddRow(table, 10, "下方注记模板", _pipeBottomTemplate);

            var precisionTip = MakeValueLabel("精确位数同时作用于长度、宽、高、深的输出格式。开挖参数输入仍可保留 3 位，最终注记按精确位数显示。");
            precisionTip.ForeColor = SystemColors.GrayText;
            AddRow(table, 11, "", precisionTip);

            UpdatePipeLayerControls();
            UpdatePipeLayerLinkControls();
            UpdatePipeBottomAnnotationControls();
            return page;
        }

        private TabPage BuildNodeTab()
        {
            var page = new TabPage("节点");
            var panel = CreateScrollablePanel();
            page.Controls.Add(panel);

            var table = CreateOptionsTable(10);
            panel.Controls.Add(table);

            _nodeTextHeight = MakeNumber(0.1M, 1000M, 1M, 1);
            SetNumberValue(_nodeTextHeight, (decimal)_nodeInitialOptions.TextHeight);
            AddRow(table, 0, "注记文字高度", _nodeTextHeight);

            var precisionLabel = MakeValueLabel("2（井深、井筒按既有规则固定保留两位）");
            AddRow(table, 1, "深度小数位", precisionLabel);

            _nodeFonts = MakeCombo(_textStyleNames, 260);
            SelectDefaultTextStyle(_nodeFonts, _nodeInitialOptions.AnnotationFontName);
            AddRow(table, 2, "字体样式", _nodeFonts);

            BuildNodeLayerRows(table, 3);

            _nodeLineSpacing = MakeNumber(0.5M, 5M, 1.45M, 2);
            SetNumberValue(_nodeLineSpacing, (decimal)_nodeInitialOptions.LineSpacingFactor);
            AddRow(table, 6, "行距系数", _nodeLineSpacing);

            var colorPanel = new FlowLayoutPanel();
            colorPanel.Dock = DockStyle.Fill;
            colorPanel.AutoSize = true;
            colorPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            colorPanel.Controls.Add(MakeInlineLabel("节点编号"));
            _nodeNoColor = MakeNumber(1M, 255M, 1M, 0);
            SetNumberValue(_nodeNoColor, _nodeInitialOptions.NodeNoColorIndex);
            colorPanel.Controls.Add(MakeColorIndexButton(_nodeNoColor));
            colorPanel.Controls.Add(MakeInlineLabel("普通文字"));
            _nodeTextColor = MakeNumber(1M, 255M, 7M, 0);
            SetNumberValue(_nodeTextColor, _nodeInitialOptions.TextColorIndex);
            colorPanel.Controls.Add(MakeColorIndexButton(_nodeTextColor));
            colorPanel.Controls.Add(MakeInlineLabel("预览引线"));
            _nodePreviewLeaderColor = MakeNumber(1M, 255M, 1M, 0);
            SetNumberValue(_nodePreviewLeaderColor, _nodeInitialOptions.PreviewLeaderColorIndex);
            colorPanel.Controls.Add(MakeColorIndexButton(_nodePreviewLeaderColor));
            AddRow(table, 7, "颜色", colorPanel);

            var tip = MakeValueLabel("点击颜色预览打开 CDBox 颜色选择器；节点标注设置当前按 ACI 保存。");
            tip.ForeColor = SystemColors.GrayText;
            AddRow(table, 8, "", tip);

            UpdateNodeLayerControls();
            return page;
        }

        private Control BuildPipeLayerLinkPanel()
        {
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.AutoSize = true;
            panel.ColumnCount = 1;
            panel.RowCount = 6;
            for (int i = 0; i < 6; i++) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _pipeAutoLayerBySourceMetadata = new CheckBox();
            _pipeAutoLayerBySourceMetadata.Text = "根据被标注管线父属性/标签自动分配注记图层";
            _pipeAutoLayerBySourceMetadata.AutoSize = true;
            _pipeAutoLayerBySourceMetadata.Checked = _pipeInitialOptions.EnableSourceMetadataLayerLink;
            _pipeAutoLayerBySourceMetadata.CheckedChanged += delegate { UpdatePipeLayerLinkControls(); UpdatePipeLayerControls(); };
            panel.Controls.Add(_pipeAutoLayerBySourceMetadata, 0, 0);

            var modePanel = new FlowLayoutPanel();
            modePanel.AutoSize = true;
            modePanel.Dock = DockStyle.Fill;
            modePanel.Controls.Add(MakeInlineLabel("分配方式"));
            _pipeLayerLinkMode = new ComboBox();
            _pipeLayerLinkMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _pipeLayerLinkMode.Width = 210;
            _pipeLayerLinkMode.Items.Add("按父属性：支管 → 支管注记");
            _pipeLayerLinkMode.Items.Add("按分类：110PVC管 → 110PVC管注记");
            _pipeLayerLinkMode.Items.Add("按标签：明管 → 明管注记");
            _pipeLayerLinkMode.Items.Add("按父属性+标签：支管-明管注记");
            _pipeLayerLinkMode.SelectedIndex = ToLayerLinkModeIndex(_pipeInitialOptions.LayerLinkMode);
            modePanel.Controls.Add(_pipeLayerLinkMode);
            panel.Controls.Add(modePanel, 0, 1);

            var suffixPanel = new FlowLayoutPanel();
            suffixPanel.AutoSize = true;
            suffixPanel.Dock = DockStyle.Fill;
            suffixPanel.Controls.Add(MakeInlineLabel("图层后缀"));
            _pipeLayerSuffix = new TextBox();
            _pipeLayerSuffix.Width = 90;
            _pipeLayerSuffix.Text = string.IsNullOrWhiteSpace(_pipeInitialOptions.AutoAnnotationLayerSuffix) ? "注记" : _pipeInitialOptions.AutoAnnotationLayerSuffix;
            suffixPanel.Controls.Add(_pipeLayerSuffix);
            suffixPanel.Controls.Add(MakeInlineLabel("未识别"));
            _pipeFallbackLayer = new TextBox();
            _pipeFallbackLayer.Width = 120;
            _pipeFallbackLayer.Text = string.IsNullOrWhiteSpace(_pipeInitialOptions.FallbackAnnotationLayerName) ? "未分类注记" : _pipeInitialOptions.FallbackAnnotationLayerName;
            suffixPanel.Controls.Add(_pipeFallbackLayer);
            panel.Controls.Add(suffixPanel, 0, 2);

            _pipeWriteAutoLayerMetadata = new CheckBox();
            _pipeWriteAutoLayerMetadata.Text = "自动创建注记图层时写入 CDBox 父属性/标签";
            _pipeWriteAutoLayerMetadata.AutoSize = true;
            _pipeWriteAutoLayerMetadata.Checked = _pipeInitialOptions.WriteAutoAnnotationLayerMetadata;
            panel.Controls.Add(_pipeWriteAutoLayerMetadata, 0, 3);

            var tagPanel = new FlowLayoutPanel();
            tagPanel.AutoSize = true;
            tagPanel.Dock = DockStyle.Fill;
            tagPanel.Controls.Add(MakeInlineLabel("标签分层只匹配"));
            _pipeSplitTags = new TextBox();
            _pipeSplitTags.Width = 280;
            _pipeSplitTags.Text = _pipeInitialOptions.AnnotationSplitTagText ?? "明管、并埋、雨水、砼恢复";
            tagPanel.Controls.Add(_pipeSplitTags);
            panel.Controls.Add(tagPanel, 0, 4);

            var tip = MakeValueLabel("开启后会覆盖上方“默认ZJ/已有图层/自定义图层”的实际落层，但仍保留这些设置作为关闭联动时使用。");
            tip.ForeColor = SystemColors.GrayText;
            panel.Controls.Add(tip, 0, 5);

            return panel;
        }

        private Panel CreateScrollablePanel()
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = true;
            return panel;
        }

        private static TableLayoutPanel CreateOptionsTable(int rowCount)
        {
            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Top;
            table.AutoSize = true;
            table.ColumnCount = 2;
            table.RowCount = rowCount;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < rowCount; i++) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return table;
        }

        private void BuildLayerRows(
            TableLayoutPanel table,
            int startRow,
            AnnotationLayerMode initialMode,
            string selectedLayerName,
            string annotationLayerName,
            out RadioButton radDefaultZJ,
            out RadioButton radExistingLayer,
            out RadioButton radCustomLayer,
            out ComboBox layers,
            out TextBox customLayer,
            Action updateAction)
        {
            radDefaultZJ = new RadioButton();
            radDefaultZJ.Text = "默认注记图层：ZJ（图中没有时自动创建）";
            radDefaultZJ.AutoSize = true;
            AddRow(table, startRow, "注记图层", radDefaultZJ);

            var existingPanel = new FlowLayoutPanel();
            existingPanel.Dock = DockStyle.Fill;
            existingPanel.AutoSize = true;
            existingPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            radExistingLayer = new RadioButton();
            radExistingLayer.Text = "选择已有图层";
            radExistingLayer.AutoSize = true;
            existingPanel.Controls.Add(radExistingLayer);
            layers = MakeCombo(_layerNames, 260);
            SelectDefaultTextStyle(layers, selectedLayerName);
            existingPanel.Controls.Add(layers);
            AddRow(table, startRow + 1, "", existingPanel);

            var customPanel = new FlowLayoutPanel();
            customPanel.Dock = DockStyle.Fill;
            customPanel.AutoSize = true;
            customPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            radCustomLayer = new RadioButton();
            radCustomLayer.Text = "自定义图层";
            radCustomLayer.AutoSize = true;
            customPanel.Controls.Add(radCustomLayer);
            customLayer = new TextBox();
            customLayer.Width = 260;
            customLayer.Text = string.IsNullOrWhiteSpace(annotationLayerName) ? "ZJ" : annotationLayerName;
            customPanel.Controls.Add(customLayer);
            AddRow(table, startRow + 2, "", customPanel);

            RadioButton defaultButton = radDefaultZJ;
            RadioButton existingButton = radExistingLayer;
            RadioButton customButton = radCustomLayer;
            ComboBox layerCombo = layers;
            TextBox customText = customLayer;

            defaultButton.CheckedChanged += delegate
            {
                if (defaultButton.Checked)
                {
                    existingButton.Checked = false;
                    customButton.Checked = false;
                    if (updateAction != null) updateAction();
                }
            };
            existingButton.CheckedChanged += delegate
            {
                if (existingButton.Checked)
                {
                    defaultButton.Checked = false;
                    customButton.Checked = false;
                    if (updateAction != null) updateAction();
                }
            };
            customButton.CheckedChanged += delegate
            {
                if (customButton.Checked)
                {
                    defaultButton.Checked = false;
                    existingButton.Checked = false;
                    if (updateAction != null) updateAction();
                }
            };
            layerCombo.Click += delegate { existingButton.Checked = true; };
            layerCombo.SelectedIndexChanged += delegate { if (layerCombo.Focused) existingButton.Checked = true; };
            customText.Click += delegate { customButton.Checked = true; };

            if (initialMode == AnnotationLayerMode.ExistingLayer) existingButton.Checked = true;
            else if (initialMode == AnnotationLayerMode.CustomLayer) customButton.Checked = true;
            else defaultButton.Checked = true;
        }

        private void BuildNodeLayerRows(TableLayoutPanel table, int startRow)
        {
            _nodeRadDefaultZJ = new RadioButton();
            _nodeRadDefaultZJ.Text = "默认注记图层：ZJ（图中没有时自动创建）";
            _nodeRadDefaultZJ.AutoSize = true;
            AddRow(table, startRow, "注记图层", _nodeRadDefaultZJ);

            var existingPanel = new FlowLayoutPanel();
            existingPanel.Dock = DockStyle.Fill;
            existingPanel.AutoSize = true;
            existingPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            _nodeRadExistingLayer = new RadioButton();
            _nodeRadExistingLayer.Text = "选择已有图层";
            _nodeRadExistingLayer.AutoSize = true;
            existingPanel.Controls.Add(_nodeRadExistingLayer);
            _nodeLayers = MakeCombo(_layerNames, 260);
            SelectDefaultTextStyle(_nodeLayers, _nodeInitialOptions.AnnotationLayerName);
            existingPanel.Controls.Add(_nodeLayers);
            AddRow(table, startRow + 1, "", existingPanel);

            var customPanel = new FlowLayoutPanel();
            customPanel.Dock = DockStyle.Fill;
            customPanel.AutoSize = true;
            customPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            _nodeRadCustomLayer = new RadioButton();
            _nodeRadCustomLayer.Text = "自定义图层";
            _nodeRadCustomLayer.AutoSize = true;
            customPanel.Controls.Add(_nodeRadCustomLayer);
            _nodeCustomLayer = new TextBox();
            _nodeCustomLayer.Width = 260;
            _nodeCustomLayer.Text = string.IsNullOrWhiteSpace(_nodeInitialOptions.AnnotationLayerName) ? "ZJ" : _nodeInitialOptions.AnnotationLayerName;
            customPanel.Controls.Add(_nodeCustomLayer);
            AddRow(table, startRow + 2, "", customPanel);

            _nodeRadDefaultZJ.CheckedChanged += delegate
            {
                if (_nodeRadDefaultZJ.Checked)
                {
                    _nodeRadExistingLayer.Checked = false;
                    _nodeRadCustomLayer.Checked = false;
                    UpdateNodeLayerControls();
                }
            };
            _nodeRadExistingLayer.CheckedChanged += delegate
            {
                if (_nodeRadExistingLayer.Checked)
                {
                    _nodeRadDefaultZJ.Checked = false;
                    _nodeRadCustomLayer.Checked = false;
                    UpdateNodeLayerControls();
                }
            };
            _nodeRadCustomLayer.CheckedChanged += delegate
            {
                if (_nodeRadCustomLayer.Checked)
                {
                    _nodeRadDefaultZJ.Checked = false;
                    _nodeRadExistingLayer.Checked = false;
                    UpdateNodeLayerControls();
                }
            };
            _nodeLayers.Click += delegate { _nodeRadExistingLayer.Checked = true; };
            _nodeLayers.SelectedIndexChanged += delegate { if (_nodeLayers.Focused) _nodeRadExistingLayer.Checked = true; };
            _nodeCustomLayer.Click += delegate { _nodeRadCustomLayer.Checked = true; };

            string layerName = _nodeInitialOptions.AnnotationLayerName;
            if (string.IsNullOrWhiteSpace(layerName) || string.Equals(layerName, "ZJ", StringComparison.CurrentCultureIgnoreCase))
            {
                _nodeRadDefaultZJ.Checked = true;
            }
            else if (ContainsIgnoreCase(_layerNames, layerName))
            {
                _nodeRadExistingLayer.Checked = true;
            }
            else
            {
                _nodeRadCustomLayer.Checked = true;
            }
        }

        private void UpdateRunButtonText()
        {
            if (_btnRunCurrent == null || _tabs == null) return;
            switch (_tabs.SelectedIndex)
            {
                case 0:
                    _btnRunCurrent.Text = "计算并标注";
                    break;
                case 1:
                    _btnRunCurrent.Text = "标注长度";
                    break;
                case 2:
                    _btnRunCurrent.Text = "节点标注";
                    break;
                default:
                    _btnRunCurrent.Text = "运行标注";
                    break;
            }
        }

        private void UpdateSurfaceLayerControls()
        {
            if (_surfLayers != null) _surfLayers.Enabled = _surfRadExistingLayer != null && _surfRadExistingLayer.Checked;
            if (_surfCustomLayer != null) _surfCustomLayer.Enabled = _surfRadCustomLayer != null && _surfRadCustomLayer.Checked;
        }

        private void UpdatePipeLayerControls()
        {
            bool autoLink = _pipeAutoLayerBySourceMetadata != null && _pipeAutoLayerBySourceMetadata.Checked;
            if (_pipeRadDefaultZJ != null) _pipeRadDefaultZJ.Enabled = !autoLink;
            if (_pipeRadExistingLayer != null) _pipeRadExistingLayer.Enabled = !autoLink;
            if (_pipeRadCustomLayer != null) _pipeRadCustomLayer.Enabled = !autoLink;
            if (_pipeLayers != null) _pipeLayers.Enabled = !autoLink && _pipeRadExistingLayer != null && _pipeRadExistingLayer.Checked;
            if (_pipeCustomLayer != null) _pipeCustomLayer.Enabled = !autoLink && _pipeRadCustomLayer != null && _pipeRadCustomLayer.Checked;
        }

        private void UpdatePipeLayerLinkControls()
        {
            bool enabled = _pipeAutoLayerBySourceMetadata != null && _pipeAutoLayerBySourceMetadata.Checked;
            if (_pipeLayerLinkMode != null) _pipeLayerLinkMode.Enabled = enabled;
            if (_pipeLayerSuffix != null) _pipeLayerSuffix.Enabled = enabled;
            if (_pipeFallbackLayer != null) _pipeFallbackLayer.Enabled = enabled;
            if (_pipeWriteAutoLayerMetadata != null) _pipeWriteAutoLayerMetadata.Enabled = enabled;
            if (_pipeSplitTags != null) _pipeSplitTags.Enabled = enabled;
        }

        private void UpdatePipeBottomAnnotationControls()
        {
            bool enabled = _pipeBottomAnnotation != null && _pipeBottomAnnotation.Checked;
            if (_pipeExcavationWidth != null) _pipeExcavationWidth.Enabled = enabled;
            if (_pipeExcavationHeight != null) _pipeExcavationHeight.Enabled = enabled;
            if (_pipeExcavationDepth != null) _pipeExcavationDepth.Enabled = enabled;
            if (_pipeBottomTemplate != null) _pipeBottomTemplate.Enabled = enabled;
        }

        private void UpdateNodeLayerControls()
        {
            if (_nodeLayers != null) _nodeLayers.Enabled = _nodeRadExistingLayer != null && _nodeRadExistingLayer.Checked;
            if (_nodeCustomLayer != null) _nodeCustomLayer.Enabled = _nodeRadCustomLayer != null && _nodeRadCustomLayer.Checked;
        }

        private SurfaceAreaAnnotationOptions ReadSurfaceOptions()
        {
            AnnotationLayerMode mode = AnnotationLayerMode.DefaultZJ;
            if (_surfRadExistingLayer != null && _surfRadExistingLayer.Checked) mode = AnnotationLayerMode.ExistingLayer;
            if (_surfRadCustomLayer != null && _surfRadCustomLayer.Checked) mode = AnnotationLayerMode.CustomLayer;

            string selectedLayer = _surfLayers == null || _surfLayers.SelectedItem == null ? "ZJ" : _surfLayers.SelectedItem.ToString();
            string customLayer = _surfCustomLayer == null || string.IsNullOrWhiteSpace(_surfCustomLayer.Text) ? "ZJ" : _surfCustomLayer.Text.Trim();
            string textStyleName = _surfFonts == null || _surfFonts.SelectedItem == null ? SurfaceAreaAnnotationOptions.Default.AnnotationFontName : _surfFonts.SelectedItem.ToString();

            return new SurfaceAreaAnnotationOptions
            {
                BoundaryInterval = (double)_surfInterval.Value,
                TextHeight = (double)_surfTextHeight.Value,
                DecimalPlaces = (int)_surfDecimals.Value,
                AnnotationTemplate = _surfTemplate.Text,
                CalculationMode = SurfaceAreaCalculationMode.CassCommand,
                CassSurfaceLogPath = string.Empty,
                DeleteCassGeneratedObjects = !(_surfKeepCassObjects != null && _surfKeepCassObjects.Checked),
                AnnotationFontName = textStyleName,
                LayerMode = mode,
                SelectedLayerName = selectedLayer,
                AnnotationLayerName = customLayer,
                UseBoundaryLayerForAnnotation = false,
                DrawLeader = true
            };
        }

        private PipeLengthAnnotationOptions ReadPipeOptions()
        {
            AnnotationLayerMode mode = AnnotationLayerMode.DefaultZJ;
            if (_pipeRadExistingLayer != null && _pipeRadExistingLayer.Checked) mode = AnnotationLayerMode.ExistingLayer;
            if (_pipeRadCustomLayer != null && _pipeRadCustomLayer.Checked) mode = AnnotationLayerMode.CustomLayer;

            string selectedLayer = _pipeLayers == null || _pipeLayers.SelectedItem == null ? "ZJ" : _pipeLayers.SelectedItem.ToString();
            string customLayer = _pipeCustomLayer == null || string.IsNullOrWhiteSpace(_pipeCustomLayer.Text) ? "ZJ" : _pipeCustomLayer.Text.Trim();
            string textStyleName = _pipeFonts == null || _pipeFonts.SelectedItem == null ? PipeLengthAnnotationOptions.Default.AnnotationFontName : _pipeFonts.SelectedItem.ToString();

            return new PipeLengthAnnotationOptions
            {
                TextHeight = (double)_pipeTextHeight.Value,
                DecimalPlaces = (int)_pipePrecision.Value,
                AnnotationTemplate = _pipeTemplate.Text,
                AnnotationFontName = textStyleName,
                LayerMode = mode,
                SelectedLayerName = selectedLayer,
                AnnotationLayerName = customLayer,
                DrawLeader = true,
                EnableSourceMetadataLayerLink = _pipeAutoLayerBySourceMetadata != null && _pipeAutoLayerBySourceMetadata.Checked,
                LayerLinkMode = FromLayerLinkModeIndex(_pipeLayerLinkMode == null ? 0 : _pipeLayerLinkMode.SelectedIndex),
                AutoAnnotationLayerSuffix = _pipeLayerSuffix == null ? "注记" : _pipeLayerSuffix.Text,
                FallbackAnnotationLayerName = _pipeFallbackLayer == null ? "未分类注记" : _pipeFallbackLayer.Text,
                WriteAutoAnnotationLayerMetadata = _pipeWriteAutoLayerMetadata == null || _pipeWriteAutoLayerMetadata.Checked,
                AnnotationSplitTagText = _pipeSplitTags == null ? string.Empty : _pipeSplitTags.Text,
                DrawBottomAnnotation = _pipeBottomAnnotation != null && _pipeBottomAnnotation.Checked,
                BottomAnnotationTemplate = _pipeBottomTemplate == null ? PipeLengthAnnotationOptions.Default.BottomAnnotationTemplate : _pipeBottomTemplate.Text,
                ExcavationWidth = _pipeExcavationWidth == null ? 0.0 : (double)_pipeExcavationWidth.Value,
                ExcavationHeight = _pipeExcavationHeight == null ? 0.0 : (double)_pipeExcavationHeight.Value,
                ExcavationDepth = _pipeExcavationDepth == null ? 0.0 : (double)_pipeExcavationDepth.Value
            };
        }

        private NodeAnnotationOptions ReadNodeOptions()
        {
            string layerName = "ZJ";
            if (_nodeRadExistingLayer != null && _nodeRadExistingLayer.Checked)
            {
                layerName = _nodeLayers == null || _nodeLayers.SelectedItem == null ? "ZJ" : _nodeLayers.SelectedItem.ToString();
            }
            else if (_nodeRadCustomLayer != null && _nodeRadCustomLayer.Checked)
            {
                layerName = _nodeCustomLayer == null || string.IsNullOrWhiteSpace(_nodeCustomLayer.Text) ? "ZJ" : _nodeCustomLayer.Text.Trim();
            }

            string textStyleName = _nodeFonts == null || _nodeFonts.SelectedItem == null ? NodeAnnotationOptions.Default.AnnotationFontName : _nodeFonts.SelectedItem.ToString();

            return new NodeAnnotationOptions
            {
                TextHeight = (double)_nodeTextHeight.Value,
                DecimalPlaces = 2,
                AnnotationFontName = textStyleName,
                AnnotationLayerName = layerName,
                LineSpacingFactor = (double)_nodeLineSpacing.Value,
                NodeNoColorIndex = (short)_nodeNoColor.Value,
                TextColorIndex = (short)_nodeTextColor.Value,
                PreviewLeaderColorIndex = (short)_nodePreviewLeaderColor.Value
            };
        }

        private void SaveAllSettings(bool showMessage)
        {
            SurfaceAreaAnnotationSettingsStore.Save(ReadSurfaceOptions());
            PipeLengthAnnotationSettingsStore.Save(ReadPipeOptions());
            NodeAnnotationSettingsStore.Save(ReadNodeOptions());

            if (showMessage)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new TCPipeAutoDraw.UI.AcadMainWindow(), "标注设置已保存。", "标注设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RunCurrentAnnotation()
        {
            if (_tabs == null) return;
            if (_tabs.SelectedIndex == 0) RunSurfaceAnnotation();
            else if (_tabs.SelectedIndex == 1) RunPipeLengthAnnotation();
            else if (_tabs.SelectedIndex == 2) RunNodeAnnotation();
        }

        private void RunSurfaceAnnotation()
        {
            bool wasVisible = Visible;
            bool restoreForm = true;
            try
            {
                SaveAllSettings(false);
                SurfaceAreaAnnotationOptions options = ReadSurfaceOptions();
                SurfaceAreaAnnotationSettingsStore.Save(options);
                restoreForm = false;
                if (wasVisible) Hide();

                SurfaceAreaAnnotationResult result = SurfaceAreaAnnotationService.SelectCalculateAndAnnotate(_doc, options);
                _doc.Editor.WriteMessage(result.ToEditorMessage());

                if (result.Success || result.AsyncStarted)
                {
                    restoreForm = false;
                    Close();
                }
                else
                {
                    restoreForm = true;
                }
            }
            catch (Exception ex)
            {
                restoreForm = true;
                _doc.Editor.WriteMessage("\n[表面积标注] 失败：" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "表面积标注失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void RunPipeLengthAnnotation()
        {
            bool wasVisible = Visible;
            bool restoreForm = true;
            try
            {
                SaveAllSettings(false);
                PipeLengthAnnotationOptions options = ReadPipeOptions();
                PipeLengthAnnotationSettingsStore.Save(options);
                if (wasVisible) Hide();

                while (true)
                {
                    PipeLengthAnnotationResult result = PipeLengthAnnotationService.SelectCalculateAndAnnotate(_doc, options);
                    _doc.Editor.WriteMessage(result.ToEditorMessage());
                    if (result.IsCancelled)
                    {
                        restoreForm = false;
                        Close();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _doc.Editor.WriteMessage("\n[管线长度标注] 失败：" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "管线长度标注失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void RunNodeAnnotation()
        {
            bool wasVisible = Visible;
            bool restoreForm = true;
            try
            {
                SaveAllSettings(false);
                NodeAnnotationOptions options = ReadNodeOptions();
                NodeAnnotationSettingsStore.Save(options);
                if (wasVisible) Hide();

                while (true)
                {
                    NodeAnnotationResult result = NodeAnnotationService.SelectAndAnnotate(_doc, options);
                    _doc.Editor.WriteMessage(result.ToEditorMessage());
                    if (!result.Success)
                    {
                        restoreForm = false;
                        Close();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _doc.Editor.WriteMessage("\n[节点标注] 失败：" + ex.Message);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "节点标注失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private List<string> LoadLayerNames()
        {
            var names = new List<string>();
            try
            {
                Database db = _doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId id in lt)
                    {
                        LayerTableRecord layer = tr.GetObject(id, OpenMode.ForRead, false) as LayerTableRecord;
                        if (layer == null || layer.IsErased) continue;
                        if (!ContainsIgnoreCase(names, layer.Name)) names.Add(layer.Name);
                    }
                    tr.Commit();
                }
            }
            catch { }

            AddNameIfMissing(names, "ZJ");
            AddNameIfMissing(names, _surfaceInitialOptions.SelectedLayerName);
            AddNameIfMissing(names, _surfaceInitialOptions.AnnotationLayerName);
            AddNameIfMissing(names, _pipeInitialOptions.SelectedLayerName);
            AddNameIfMissing(names, _pipeInitialOptions.AnnotationLayerName);
            AddNameIfMissing(names, _nodeInitialOptions.AnnotationLayerName);

            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            MoveNameToTop(names, "ZJ");
            return names;
        }

        private List<string> LoadTextStyleNames()
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

            if (names.Count == 0) names.Add("STANDARD");
            names.Sort(StringComparer.CurrentCultureIgnoreCase);

            if (ContainsIgnoreCase(names, "宋体")) MoveNameToTop(names, "宋体");
            else if (!string.IsNullOrWhiteSpace(currentStyleName) && ContainsIgnoreCase(names, currentStyleName)) MoveNameToTop(names, currentStyleName);
            else MoveNameToTop(names, "STANDARD");

            return names;
        }

        private static ComboBox MakeCombo(List<string> items, int width)
        {
            var combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Width = width;
            if (items != null)
            {
                foreach (string item in items)
                {
                    if (!string.IsNullOrWhiteSpace(item)) combo.Items.Add(item);
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            return combo;
        }

        private static Label MakeValueLabel(string text)
        {
            var label = new Label();
            label.Text = text ?? string.Empty;
            label.AutoSize = true;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Padding = new Padding(0, 4, 0, 4);
            return label;
        }

        private static Label MakeInlineLabel(string text)
        {
            var label = new Label();
            label.Text = text ?? string.Empty;
            label.AutoSize = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Padding = new Padding(8, 6, 2, 0);
            return label;
        }

        private static Button MakeColorIndexButton(NumericUpDown valueControl)
        {
            var button = new Button
            {
                AutoSize = false,
                Width = 148,
                Height = 31,
                Margin = new Padding(2, 1, 8, 1),
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat
            };
            Action update = delegate
            {
                CDBoxColor color = CDBoxColor.FromIndex((int)valueControl.Value);
                button.Text = "■  " + color.DisplayName;
                button.ForeColor = color.Index == 7
                    ? SystemColors.ControlText
                    : System.Drawing.Color.FromArgb(color.R, color.G, color.B);
            };
            valueControl.ValueChanged += delegate { update(); };
            button.Click += delegate
            {
                CDBoxColor selected;
                if (!ColorPickerWindow.TryPick(CDBoxColor.FromIndex((int)valueControl.Value), out selected,
                    false, false, false, false, false)) return;
                valueControl.Value = CDBoxColorService.ToCompatibleColorIndex(selected, (short)valueControl.Value);
            };
            update();
            return button;
        }

        private static NumericUpDown MakeNumber(decimal min, decimal max, decimal value, int decimalPlaces)
        {
            var number = new NumericUpDown();
            number.Minimum = min;
            number.Maximum = max;
            number.Value = value;
            number.DecimalPlaces = decimalPlaces == 0 ? 0 : Math.Max(2, decimalPlaces);
            number.Increment = decimalPlaces == 0 ? 1 : 0.01M;
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

        private static void AddRow(TableLayoutPanel panel, int row, string labelText, Control control)
        {
            var label = new Label();
            label.Text = labelText ?? string.Empty;
            label.AutoSize = true;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Padding = new Padding(0, 4, 8, 4);
            panel.Controls.Add(label, 0, row);

            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 4, 0, 4);
            panel.Controls.Add(control, 1, row);
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

        private static void AddNameIfMissing(List<string> names, string value)
        {
            if (names == null || string.IsNullOrWhiteSpace(value)) return;
            if (!ContainsIgnoreCase(names, value)) names.Add(value);
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

        private static void SelectDefaultTextStyle(ComboBox combo, string styleName)
        {
            if (combo == null || combo.Items.Count == 0) return;

            int index = -1;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                string item = combo.Items[i] == null ? string.Empty : combo.Items[i].ToString();
                if (string.Equals(item, styleName, StringComparison.CurrentCultureIgnoreCase))
                {
                    index = i;
                    break;
                }
            }

            combo.SelectedIndex = index >= 0 ? index : 0;
        }

        private static int ToLayerLinkModeIndex(AnnotationLayerLinkMode mode)
        {
            switch (mode)
            {
                case AnnotationLayerLinkMode.ParentClass:
                    return 1;
                case AnnotationLayerLinkMode.FirstMatchedTag:
                    return 2;
                case AnnotationLayerLinkMode.ParentGroupAndTag:
                    return 3;
                default:
                    return 0;
            }
        }

        private static AnnotationLayerLinkMode FromLayerLinkModeIndex(int index)
        {
            switch (index)
            {
                case 1:
                    return AnnotationLayerLinkMode.ParentClass;
                case 2:
                    return AnnotationLayerLinkMode.FirstMatchedTag;
                case 3:
                    return AnnotationLayerLinkMode.ParentGroupAndTag;
                default:
                    return AnnotationLayerLinkMode.ParentGroup;
            }
        }
    }
}
