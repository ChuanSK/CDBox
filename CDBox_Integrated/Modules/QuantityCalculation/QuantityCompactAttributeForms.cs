using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    internal static class QuantityAttributeEditorFormFactory
    {
        public static Form Create(Document doc, QuantityPipeSelectionInfo info)
        {
            string kind = info == null || info.Attributes == null ? string.Empty : info.Attributes.ObjectKind;
            if (QuantityPipeAttributes.IsNodeKind(kind)) return new QuantityNodeWellAttributeForm(doc, info);
            if (QuantityPipeAttributes.IsBranchKind(kind)) return new QuantityBranchPipeAttributeForm(doc, info);
            return new QuantityMainPipeAttributeForm(doc, info);
        }
    }

    public sealed class QuantityMainPipeAttributeForm : QuantityCompactAttributeForm
    {
        public QuantityMainPipeAttributeForm(Document doc, QuantityPipeSelectionInfo info)
            : base(doc, info, QuantityPipeAttributes.KindMainPipe, "主管属性")
        {
        }
    }

    public sealed class QuantityNodeWellAttributeForm : QuantityCompactAttributeForm
    {
        public QuantityNodeWellAttributeForm(Document doc, QuantityPipeSelectionInfo info)
            : base(doc, info, QuantityPipeAttributes.KindNodeWell, "节点/检查井属性")
        {
        }
    }

    public sealed class QuantityBranchPipeAttributeForm : QuantityCompactAttributeForm
    {
        public QuantityBranchPipeAttributeForm(Document doc, QuantityPipeSelectionInfo info)
            : base(doc, info, QuantityPipeAttributes.KindBranchPipe, "支管属性")
        {
        }
    }

    public class QuantityCompactAttributeForm : Form
    {
        private readonly Document _doc;
        private readonly ObjectId _objectId;
        private readonly string _kind;
        private readonly double _cadLength;
        private QuantityPipeAttributes _attributes;
        private QuantityAttributeFieldsPanel _fields;
        private Label _lblInfo;
        private Label _lblStatus;

        protected QuantityCompactAttributeForm(Document doc, QuantityPipeSelectionInfo info, string kind, string title)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (info == null) throw new ArgumentNullException("info");
            _doc = doc;
            _objectId = info.ObjectId;
            _kind = kind;
            _cadLength = info.CadLength;
            _attributes = info.Attributes == null ? QuantityPipeAttributes.DefaultForKind(kind) : info.Attributes.Clone();
            _attributes.ObjectKind = kind;

            Text = title;
            Width = QuantityPipeAttributes.IsNodeKind(kind) ? 600 : 640;
            Height = QuantityPipeAttributes.IsNodeKind(kind) ? 760 : 740;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            BuildUi(title, info);
            _fields.LoadAttributes(_attributes, _cadLength);
        }

        private void BuildUi(string title, QuantityPipeSelectionInfo info)
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(10);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.AutoSize = true;
            titleLabel.Font = new System.Drawing.Font(titleLabel.Font.FontFamily, 11, System.Drawing.FontStyle.Bold);
            titleLabel.Padding = new Padding(0, 0, 0, 6);
            root.Controls.Add(titleLabel, 0, 0);

            _lblInfo = new Label();
            _lblInfo.AutoSize = true;
            _lblInfo.Text = "对象：" + info.ObjectTypeName + " / Handle " + info.HandleText
                + "    图层：" + (info.LayerName ?? string.Empty)
                + "    父属性：" + (_attributes.LayerParentGroup ?? string.Empty);
            root.Controls.Add(_lblInfo, 0, 1);

            _fields = new QuantityAttributeFieldsPanel(_kind, false);
            _fields.Dock = DockStyle.Fill;
            root.Controls.Add(_fields, 0, 2);

            var bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.AutoSize = true;
            bottom.ColumnCount = 1;
            bottom.RowCount = 2;
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(bottom, 0, 3);

            _lblStatus = new Label();
            _lblStatus.AutoSize = true;
            _lblStatus.Text = info.HasSavedAttributes ? "已读取已有属性。" : "未设置属性，已套用对应默认表。";
            _lblStatus.Padding = new Padding(0, 6, 0, 6);
            bottom.Controls.Add(_lblStatus, 0, 0);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            bottom.Controls.Add(buttons, 0, 1);

            AddButton(buttons, "关闭", 80, delegate { Close(); });
            AddButton(buttons, "确认", 90, delegate { ConfirmAttributes(); });
            AddButton(buttons, "刷新", 80, delegate { RefreshCurrentAttributes(); });
            AddButton(buttons, "按默认表重填", 120, delegate { ReloadDefault(); });
            if (QuantityPipeAttributes.IsMainPipeKind(_kind))
            {
                AddButton(buttons, "调换起终点", 110, delegate { SwapStartEndNodes(); });
                AddButton(buttons, "选终点井", 90, delegate { SelectNodeManually(false); });
                AddButton(buttons, "选起点井", 90, delegate { SelectNodeManually(true); });
            }
        }

        private static void AddButton(Control parent, string text, int width, EventHandler handler)
        {
            var button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 28;
            button.Margin = new Padding(4, 4, 0, 4);
            button.Click += handler;
            parent.Controls.Add(button);
        }

        private void ConfirmAttributes()
        {
            try
            {
                _fields.FillAttributes(_attributes);
                _attributes.ObjectKind = _kind;
                _attributes.Remark = string.Empty;
                QuantityPipeWriteResult result = QuantityPipeAttributeService.WritePipeAttributes(_doc, _objectId, _attributes);
                _lblStatus.Text = result.Message;
                if (!result.Success)
                {
                    TCPipeAutoDraw.UI.CDBoxMessageBox.Show(result.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "保存属性失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReloadDefault()
        {
            try
            {
                _fields.FillAttributes(_attributes, false);
                QuantityPipeAttributes attrs = QuantityPipeAttributeService.LoadDefaultProfileForObject(_doc, _objectId, _kind, _attributes);
                attrs.ObjectKind = _kind;
                _attributes = attrs;
                _fields.LoadAttributes(_attributes, _cadLength);
                _lblStatus.Text = "已按当前类型默认表重新填充。";
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "载入默认表失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshCurrentAttributes()
        {
            try
            {
                _fields.FillAttributes(_attributes, false);
                _attributes.ObjectKind = _kind;
                _attributes = QuantityPipeAttributeService.RefreshAttributesForObject(_doc, _objectId, _attributes);
                _fields.LoadAttributes(_attributes, _cadLength);
                _lblStatus.Text = "已刷新：自动识别规格、起终点，并重新计算平均开挖深度等相关数值。";
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "刷新属性失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SelectNodeManually(bool forStart)
        {
            try
            {
                _fields.FillAttributes(_attributes, false);
                _attributes.ObjectKind = QuantityPipeAttributes.KindMainPipe;

                FormWindowState previousState = WindowState;
                WindowState = FormWindowState.Minimized;
                System.Windows.Forms.Application.DoEvents();
                try
                {
                    _attributes = QuantityPipeAttributeService.SelectConnectedNodeForMainPipe(_doc, _objectId, _attributes, forStart);
                }
                finally
                {
                    WindowState = previousState == FormWindowState.Minimized
                        ? FormWindowState.Normal
                        : previousState;
                    Show();
                    Activate();
                }

                _fields.UpdateMainPipeStartEndFields(_attributes, true);
                string nodeNo = forStart ? _attributes.StartNode : _attributes.EndNode;
                _lblStatus.Text = (forStart ? "已手动选择起点井" : "已手动选择终点井")
                    + (string.IsNullOrWhiteSpace(nodeNo) ? "。" : "：" + nodeNo + "。");
            }
            catch (System.Exception ex)
            {
                Show();
                Activate();
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "手动选择节点失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReDetectNodes()
        {
            try
            {
                // 重新识别起终点只应更新起点井、终点井及对应深度。
                // 不能调用 ApplySmartDefaults，也不能重新 LoadAttributes，避免用户已编辑的结构层、开挖宽度等字段被默认表或自动计算覆盖。
                _fields.FillAttributes(_attributes, false);
                _attributes.StartNode = string.Empty;
                _attributes.EndNode = string.Empty;
                _attributes.StartDepth = 0.0;
                _attributes.EndDepth = 0.0;
                _attributes.ObjectKind = QuantityPipeAttributes.KindMainPipe;

                _attributes = QuantityPipeAttributeService.ReDetectConnectedNodeInfo(_doc, _objectId, _attributes);
                _fields.UpdateMainPipeStartEndFields(_attributes, true);
                _lblStatus.Text = "已重新识别起终点，并按当前结构层重新计算平均开挖深度。";
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "识别起终点失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SwapStartEndNodes()
        {
            try
            {
                _fields.SwapMainPipeStartEnd();
                _fields.FillAttributes(_attributes);
                _lblStatus.Text = "已调换起点与终点，确认后写入对象属性。";
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "调换起终点失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public sealed class QuantityDefaultProfileForm : Form
    {
        private QuantityAttributeFieldsPanel _mainPanel;
        private QuantityAttributeFieldsPanel _nodePanel;
        private QuantityAttributeFieldsPanel _branchPanel;
        private Label _lblStatus;

        public QuantityDefaultProfileForm()
        {
            Text = "属性默认表（SXMRB）";
            Width = 680;
            Height = 740;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            BuildUi();
            LoadDefaults();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(10);
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var note = new Label();
            note.AutoSize = true;
            note.Text = "用于设置主管、节点/检查井、支管三类对象的默认属性；属性编辑器识别到未设置属性对象时自动套用。";
            note.Padding = new Padding(0, 0, 0, 8);
            root.Controls.Add(note, 0, 0);

            var tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            root.Controls.Add(tabs, 0, 1);

            _mainPanel = AddTab(tabs, "主管默认表", QuantityPipeAttributes.KindMainPipe);
            _nodePanel = AddTab(tabs, "节点/井默认表", QuantityPipeAttributes.KindNodeWell);
            _branchPanel = AddTab(tabs, "支管默认表", QuantityPipeAttributes.KindBranchPipe);

            var bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.AutoSize = true;
            bottom.ColumnCount = 1;
            bottom.RowCount = 2;
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(bottom, 0, 2);

            _lblStatus = new Label();
            _lblStatus.AutoSize = true;
            _lblStatus.Padding = new Padding(0, 6, 0, 6);
            bottom.Controls.Add(_lblStatus, 0, 0);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            bottom.Controls.Add(buttons, 0, 1);

            AddButton(buttons, "关闭", 80, delegate { Close(); });
            AddButton(buttons, "保存默认表", 140, delegate { SaveDefaults(); });
            AddButton(buttons, "恢复内置默认", 120, delegate { ResetBuiltInDefaults(); });
        }

        private static QuantityAttributeFieldsPanel AddTab(TabControl tabs, string title, string kind)
        {
            var page = new TabPage(title);
            var panel = new QuantityAttributeFieldsPanel(kind, true);
            panel.Dock = DockStyle.Fill;
            page.Controls.Add(panel);
            tabs.TabPages.Add(page);
            return panel;
        }

        private static void AddButton(Control parent, string text, int width, EventHandler handler)
        {
            var button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 28;
            button.Margin = new Padding(4, 4, 0, 4);
            button.Click += handler;
            parent.Controls.Add(button);
        }

        private void LoadDefaults()
        {
            QuantityAttributeDefaults defaults = QuantityAttributeDefaultStore.Load();
            _mainPanel.LoadAttributes(defaults.MainPipe ?? QuantityPipeAttributes.DefaultMainPipe, 0.0);
            _nodePanel.LoadAttributes(defaults.NodeWell ?? QuantityPipeAttributes.DefaultNodeWell, 0.0);
            _branchPanel.LoadAttributes(defaults.BranchPipe ?? QuantityPipeAttributes.DefaultBranchPipe, 0.0);
            _lblStatus.Text = "已载入默认表。";
        }

        private void SaveDefaults()
        {
            try
            {
                var defaults = new QuantityAttributeDefaults();
                defaults.MainPipe = QuantityPipeAttributes.DefaultMainPipe.Clone();
                defaults.NodeWell = QuantityPipeAttributes.DefaultNodeWell.Clone();
                defaults.BranchPipe = QuantityPipeAttributes.DefaultBranchPipe.Clone();
                _mainPanel.FillAttributes(defaults.MainPipe);
                _nodePanel.FillAttributes(defaults.NodeWell);
                _branchPanel.FillAttributes(defaults.BranchPipe);
                defaults.MainPipe.ObjectKind = QuantityPipeAttributes.KindMainPipe;
                defaults.NodeWell.ObjectKind = QuantityPipeAttributes.KindNodeWell;
                defaults.BranchPipe.ObjectKind = QuantityPipeAttributes.KindBranchPipe;
                QuantityAttributeDefaultStore.Save(defaults);
                _lblStatus.Text = "默认表已保存。";
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "保存默认表失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetBuiltInDefaults()
        {
            _mainPanel.LoadAttributes(QuantityPipeAttributes.DefaultMainPipe, 0.0);
            _nodePanel.LoadAttributes(QuantityPipeAttributes.DefaultNodeWell, 0.0);
            _branchPanel.LoadAttributes(QuantityPipeAttributes.DefaultBranchPipe, 0.0);
            _lblStatus.Text = "已恢复为内置默认值，点击“保存全部默认表”后生效。";
        }
    }

    internal sealed class QuantityAttributeFieldsPanel : UserControl
    {
        private readonly string _kind;
        private readonly bool _defaultMode;
        private readonly Dictionary<string, Control> _controls;
        private TableLayoutPanel _layout;
        private Label _cadLengthLabel;
        private TextBox _averageDepthBox;
        private QuantityStructureLayerEditor _structureEditor;
        private bool _loadingAttributes;
        private bool _updatingDerivedValues;
        private double _lastPipeCushion;

        public QuantityAttributeFieldsPanel(string kind, bool defaultMode)
        {
            _kind = kind;
            _defaultMode = defaultMode;
            _controls = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
            BuildUi();
        }

        private void BuildUi()
        {
            AutoScroll = true;
            _layout = new TableLayoutPanel();
            _layout.Dock = DockStyle.Top;
            _layout.AutoSize = true;
            _layout.ColumnCount = 2;
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _layout.Padding = new Padding(2, 8, 8, 8);
            Controls.Add(_layout);

            AddCheck("Enabled", "参与统计");

            if (QuantityPipeAttributes.IsNodeKind(_kind)) BuildNodeFields();
            else if (QuantityPipeAttributes.IsBranchKind(_kind)) BuildBranchFields();
            else BuildMainFields();
        }

        private void BuildMainFields()
        {
            AddText("Material", "管材");
            AddText("Diameter", "规格/管径");
            AddCheck("DrawLengthWidthHeightAnnotation", "标注长宽高");
            if (!_defaultMode) AddCadLengthAndManualLength();
            if (!_defaultMode)
            {
                AddText("StartNode", "起点井");
                TextBox startDepthBox = AddNumber("StartDepth", "起点深度 m");
                AddText("EndNode", "终点井");
                TextBox endDepthBox = AddNumber("EndDepth", "终点深度 m");
                TextBox averageDepthBox = AddNumber("AverageDepth", "平均深度 m");

                EventHandler updateDepth = delegate { if (!_loadingAttributes) RecalculateMainPipeDerivedValuesFromDepthChange(); };
                startDepthBox.TextChanged += updateDepth;
                endDepthBox.TextChanged += updateDepth;
                averageDepthBox.TextChanged += delegate { if (!_loadingAttributes && !_updatingDerivedValues) UpdateStructureLayerEditor(false); };
            }
            AddNumber("TrenchWidth", "开挖宽度 m");
            AddNumber("RoadThickness", "原路面结构层 m");
            AddCombo("ExcavationType", "开挖方式", "机械开挖", "人工开挖");
            AddCombo("BackfillType", "回填类型", "中粗砂回填", "原土回填", "混合/特殊");
            AddStructureMultiline("BackfillStructure", "回填结构层\n每行一层");
            AddPipeDeductFields();
        }

        private void BuildBranchFields()
        {
            AddText("Material", "管材");
            AddText("Diameter", "规格/管径");
            AddCheck("DrawLengthWidthHeightAnnotation", "标注长宽高");
            if (!_defaultMode) AddCadLengthAndManualLength();
            ComboBox branchTypeCombo = AddCombo("BranchType", "支管类型", "砼恢复", "原土回填", "明管", "并埋", "雨水");
            branchTypeCombo.TextChanged += delegate { if (!_loadingAttributes) ApplyBranchTypeTemplateFromUi(); };
            AddCheck("BranchIncludeInCalculation", "加入计算");
            AddNumber("RoadThickness", "原路面结构层 m");
            AddNumber("TrenchWidth", "开挖宽度 m");
            TextBox branchDepthBox = AddNumber("BranchDepth", "开挖深度 m");
            branchDepthBox.TextChanged += delegate
            {
                if (!_loadingAttributes && ContainsAny(GetText("BranchType", string.Empty), "原土回填", "原土")) ApplyBranchTypeTemplateFromUi();
                else UpdateStructureLayerEditor(false);
            };
            AddCombo("ExcavationType", "开挖方式", "人工开挖", "机械开挖");
            AddCombo("BackfillType", "回填类型", "中粗砂回填", "原土回填", "混合/特殊");
            AddStructureMultiline("BackfillStructure", "回填结构层\n每行一层");
            AddPipeDeductFields();
        }

        private void BuildNodeFields()
        {
            if (!_defaultMode) AddText("NodeNo", "节点/井编号");
            AddText("WellSpec", "井规格/直径");
            AddCombo("WellMaterialType", "井材料类型", "成品塑料井", "砖砌井", "现浇混凝土井");
            AddCombo("WellCoverMaterial", "井盖类型/材料", "混凝土井盖", "铸铁井盖");
            AddCombo("WellType", "井类型", "检查井", "沉泥井", "跌水井");
            AddNumber("SiltWellDeductDepth500", "500沉泥扣减 m");
            AddNumber("SiltWellDeductDepth700", "700沉泥扣减 m");
            if (!_defaultMode)
            {
                AddNumber("GroundElevation", "自然标高 m");
                TextBox wellDepthBox = AddNumber("WellDepth", "井深 m");
                wellDepthBox.TextChanged += delegate { UpdateStructureLayerEditor(false); };
                AddNumber("ShaftLength", "井筒长度 m");
            }
            AddNumber("RoadThickness", "原路面结构层 m");
            AddNumber("ExcavationLength", "开挖长 m");
            AddNumber("ExcavationWidth", "开挖宽 m");
            AddCombo("ExcavationType", "开挖方式", "机械开挖", "人工开挖");
            AddCombo("BackfillType", "回填类型", "中粗砂回填", "原土回填", "混合/特殊");
            AddStructureMultiline("BackfillStructure", "井结构层");
            AddText("CoverPlate", "承压盖板");
        }

        private void AddPipeDeductFields()
        {
            TextBox pipeDiameterBox = AddNumber("PipeOuterDiameter", "管道外径 m");
            pipeDiameterBox.TextChanged += delegate { UpdateStructureLayerEditor(false); };
            AddCheck("DeductPipeVolume", "扣除管身体积");
        }

        private void AddCadLengthAndManualLength()
        {
            AddLabelValue("CadLength", "管线长度(m)");
            AddCheck("UseManualLength", "使用手动长度");
            AddNumber("ManualLength", "手动长度 m");
        }

        private void AddText(string key, string label)
        {
            var tb = new TextBox();
            tb.Dock = DockStyle.Fill;
            AddControl(key, label, tb);
        }

        private TextBox AddNumber(string key, string label)
        {
            var tb = new TextBox();
            tb.Dock = DockStyle.Left;
            tb.Width = 110;
            AddControl(key, label, tb);
            if (string.Equals(key, "AverageDepth", StringComparison.OrdinalIgnoreCase)) _averageDepthBox = tb;
            return tb;
        }

        private void AddMultiline(string key, string label)
        {
            var tb = new TextBox();
            tb.Dock = DockStyle.Fill;
            tb.Multiline = true;
            tb.Height = 48;
            tb.ScrollBars = ScrollBars.Vertical;
            AddControl(key, label, tb);
        }

        private void AddStructureMultiline(string key, string label)
        {
            var editor = new QuantityStructureLayerEditor(QuantityPipeAttributes.IsNodeKind(_kind));
            editor.Dock = DockStyle.Fill;
            editor.Height = 220;
            editor.RequestTotalHeight += delegate { return GetStructureTotalHeight(); };
            editor.RequestPipeDiameter += delegate { return GetPipeDiameterForStructureCheck(); };
            editor.StructureChanged += delegate { if (!_loadingAttributes) OnStructureLayerEditorChanged(); };
            _structureEditor = editor;
            AddControl(key, label, editor);
        }

        private ComboBox AddCombo(string key, string label, params string[] items)
        {
            var cb = new QuantityNoMouseWheelComboBox();
            cb.Dock = DockStyle.Fill;
            cb.DropDownStyle = ComboBoxStyle.DropDown;
            if (items != null && items.Length > 0) cb.Items.AddRange(items);
            AddControl(key, label, cb);
            return cb;
        }

        private void AddCheck(string key, string label)
        {
            var cb = new CheckBox();
            cb.AutoSize = true;
            AddControl(key, label, cb);
        }

        private void AddLabelValue(string key, string label)
        {
            var value = new Label();
            value.AutoSize = true;
            value.Padding = new Padding(0, 4, 0, 0);
            AddControl(key, label, value);
            if (string.Equals(key, "CadLength", StringComparison.OrdinalIgnoreCase)) _cadLengthLabel = value;

        }

        private void AddControl(string key, string label, Control control)
        {
            int row = _layout.RowCount;
            _layout.RowCount = row + 1;
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lbl = new Label();
            lbl.Text = label;
            lbl.AutoSize = true;
            lbl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lbl.Padding = new Padding(0, 5, 0, 0);
            _layout.Controls.Add(lbl, 0, row);
            _layout.Controls.Add(control, 1, row);
            control.Margin = new Padding(0, 2, 0, 2);
            _controls[key] = control;
        }

        public void LoadAttributes(QuantityPipeAttributes attrs, double cadLength)
        {
            if (attrs == null) attrs = QuantityPipeAttributes.DefaultForKind(_kind);
            _loadingAttributes = true;
            try
            {
                SetCheck("Enabled", attrs.Enabled);
                SetText("Material", attrs.Material);
                SetText("Diameter", attrs.Diameter);
                SetText("StartNode", attrs.StartNode);
                SetNumber("StartDepth", attrs.StartDepth);
                SetText("EndNode", attrs.EndNode);
                SetNumber("EndDepth", attrs.EndDepth);
                SetNumber("AverageDepth", ResolveAverageDepthForDisplay(attrs));
                SetNumber("TrenchWidth", attrs.TrenchWidth);
                SetNumber("RoadThickness", attrs.RoadThickness);
                SetText("ExcavationType", attrs.ExcavationType);
                SetText("BackfillType", attrs.BackfillType);
                SetText("BackfillStructure", QuantityPipeAttributes.NormalizeStructureLayerText(attrs.BackfillStructure));
                SetText("BranchType", attrs.BranchType);
                SetCheck("BranchIncludeInCalculation", attrs.BranchIncludeInCalculation);
                SetNumber("BranchDepth", attrs.BranchDepth);
                SetText("NodeNo", attrs.NodeNo);
                SetText("WellSpec", attrs.WellSpec);
                SetText("WellMaterialType", attrs.WellMaterialType);
                SetText("WellCoverMaterial", NormalizeWellCoverMaterial(attrs.WellCoverMaterial));
                SetText("WellType", attrs.WellType);
                SetNumber("SiltWellDeductDepth500", attrs.SiltWellDeductDepth500);
                SetNumber("SiltWellDeductDepth700", attrs.SiltWellDeductDepth700);
                SetNumber("GroundElevation", attrs.GroundElevation);
                SetNumber("WellDepth", attrs.WellDepth);
                SetNumber("ShaftLength", attrs.ShaftLength);
                SetNumber("ExcavationLength", attrs.ExcavationLength);
                SetNumber("ExcavationWidth", attrs.ExcavationWidth);
                SetText("CoverPlate", attrs.CoverPlate);
                SetNumber("SandCushionThickness", attrs.SandCushionThickness);
                SetNumber("GravelCushionThickness", attrs.GravelCushionThickness);
                SetNumber("C25RestoreThickness", attrs.C25RestoreThickness);
                SetNumber("PipeOuterDiameter", attrs.PipeOuterDiameter);
                SetCheck("DeductPipeVolume", attrs.DeductPipeVolume);
                SetCheck("UseManualLength", attrs.UseManualLength);
                SetNumber("ManualLength", attrs.ManualLength);
                SetCheck("DrawLengthWidthHeightAnnotation", attrs.DrawLengthWidthHeightAnnotation);
                if (_cadLengthLabel != null) _cadLengthLabel.Text = cadLength > 0 ? cadLength.ToString("0.00") : "-";
            }
            finally
            {
                _loadingAttributes = false;
            }

            UpdateStructureLayerEditor(false);
            _lastPipeCushion = GetPipeCushionForUi();
        }

        public void SwapMainPipeStartEnd()
        {
            if (!QuantityPipeAttributes.IsMainPipeKind(_kind)) return;

            string startNode = GetText("StartNode", string.Empty);
            string endNode = GetText("EndNode", string.Empty);
            double startDepth = GetNumber("StartDepth", 0.0);
            double endDepth = GetNumber("EndDepth", 0.0);

            SetText("StartNode", endNode);
            SetText("EndNode", startNode);
            SetNumber("StartDepth", endDepth);
            SetNumber("EndDepth", startDepth);

            RecalculateMainPipeDerivedValuesFromDepthChange();
        }

        public void UpdateMainPipeStartEndFields(QuantityPipeAttributes attrs, bool setAverageIfEmpty)
        {
            if (!QuantityPipeAttributes.IsMainPipeKind(_kind) || attrs == null) return;

            bool oldLoading = _loadingAttributes;
            _loadingAttributes = true;
            try
            {
                SetText("StartNode", attrs.StartNode);
                SetNumber("StartDepth", attrs.StartDepth);
                SetText("EndNode", attrs.EndNode);
                SetNumber("EndDepth", attrs.EndDepth);

                // 起点深度、终点深度已经是“井深 + 当前管线垫层”后的开挖深度，
                // 因此平均深度直接按当前起终点深度实时计算。
                if (setAverageIfEmpty)
                {
                    double average = CalculateMainPipeAverageDepthForUi();
                    if (average <= 0) average = ResolveAverageDepthForDisplay(attrs);
                    if (average > 0) SetNumber("AverageDepth", average);
                }
            }
            finally
            {
                _loadingAttributes = oldLoading;
            }
        }

        public void FillAttributes(QuantityPipeAttributes attrs)
        {
            FillAttributes(attrs, true);
        }

        public void FillAttributes(QuantityPipeAttributes attrs, bool recalculateStructure)
        {
            if (attrs == null) return;
            attrs.Enabled = GetCheck("Enabled", attrs.Enabled);
            attrs.ObjectKind = _kind;
            attrs.Material = GetText("Material", attrs.Material);
            attrs.Diameter = GetText("Diameter", attrs.Diameter);
            attrs.StartNode = GetText("StartNode", attrs.StartNode);
            attrs.StartDepth = GetNumber("StartDepth", attrs.StartDepth);
            attrs.EndNode = GetText("EndNode", attrs.EndNode);
            attrs.EndDepth = GetNumber("EndDepth", attrs.EndDepth);
            attrs.AverageDepth = GetNumber("AverageDepth", attrs.AverageDepth);
            if (QuantityPipeAttributes.IsMainPipeKind(_kind))
            {
                double averageDepth = CalculateMainPipeAverageDepthForUi();
                if (averageDepth > 0) attrs.AverageDepth = averageDepth;
            }
            attrs.TrenchWidth = GetNumber("TrenchWidth", attrs.TrenchWidth);
            attrs.RoadThickness = GetNumber("RoadThickness", attrs.RoadThickness);
            attrs.ExcavationType = GetText("ExcavationType", attrs.ExcavationType);
            attrs.BackfillType = GetText("BackfillType", attrs.BackfillType);
            if (recalculateStructure) UpdateStructureLayerEditor(true);
            attrs.BackfillStructure = QuantityPipeAttributes.NormalizeStructureLayerText(GetText("BackfillStructure", attrs.BackfillStructure));
            QuantityPipeAttributes.ApplyStructureLayerText(attrs);
            attrs.BranchType = GetText("BranchType", attrs.BranchType);
            attrs.BranchIncludeInCalculation = GetCheck("BranchIncludeInCalculation", attrs.BranchIncludeInCalculation);
            attrs.BranchDepth = GetNumber("BranchDepth", attrs.BranchDepth);
            attrs.NodeNo = GetText("NodeNo", attrs.NodeNo);
            attrs.WellSpec = GetText("WellSpec", attrs.WellSpec);
            attrs.WellMaterialType = GetText("WellMaterialType", attrs.WellMaterialType);
            attrs.WellCoverMaterial = GetText("WellCoverMaterial", attrs.WellCoverMaterial);
            attrs.WellType = GetText("WellType", attrs.WellType);
            attrs.SiltWellDeductDepth500 = GetNumber("SiltWellDeductDepth500", attrs.SiltWellDeductDepth500);
            attrs.SiltWellDeductDepth700 = GetNumber("SiltWellDeductDepth700", attrs.SiltWellDeductDepth700);
            attrs.GroundElevation = GetNumber("GroundElevation", attrs.GroundElevation);
            attrs.WellDepth = GetNumber("WellDepth", attrs.WellDepth);
            attrs.ShaftLength = GetNumber("ShaftLength", attrs.ShaftLength);
            attrs.ExcavationLength = GetNumber("ExcavationLength", attrs.ExcavationLength);
            attrs.ExcavationWidth = GetNumber("ExcavationWidth", attrs.ExcavationWidth);
            attrs.CoverPlate = GetText("CoverPlate", attrs.CoverPlate);
            attrs.SandCushionThickness = GetNumber("SandCushionThickness", attrs.SandCushionThickness);
            attrs.GravelCushionThickness = GetNumber("GravelCushionThickness", attrs.GravelCushionThickness);
            attrs.C25RestoreThickness = GetNumber("C25RestoreThickness", attrs.C25RestoreThickness);
            attrs.PipeOuterDiameter = GetNumber("PipeOuterDiameter", attrs.PipeOuterDiameter);
            attrs.DeductPipeVolume = GetCheck("DeductPipeVolume", attrs.DeductPipeVolume);
            attrs.UseManualLength = GetCheck("UseManualLength", attrs.UseManualLength);
            attrs.ManualLength = GetNumber("ManualLength", attrs.ManualLength);
            attrs.DrawLengthWidthHeightAnnotation = GetCheck("DrawLengthWidthHeightAnnotation", attrs.DrawLengthWidthHeightAnnotation);
            attrs.Remark = string.Empty;
        }

        private void ApplyBranchTypeTemplateFromUi()
        {
            if (!QuantityPipeAttributes.IsBranchKind(_kind)) return;

            string branchType = GetText("BranchType", string.Empty);
            double depth = GetNumber("BranchDepth", 0.0);
            if (depth <= 0) depth = 0.6;

            if (ContainsAny(branchType, "明管", "并埋"))
            {
                SetCheck("BranchIncludeInCalculation", false);
                SetText("BackfillType", "无结构层");
                SetText("BackfillStructure", string.Empty);
                UpdateStructureLayerEditor(false);
                return;
            }

            if (ContainsAny(branchType, "原土回填", "原土"))
            {
                SetCheck("BranchIncludeInCalculation", true);
                SetText("BackfillType", "原土回填");
                SetText("BackfillStructure", "原土回填 " + Format(depth) + " 管线层");
                UpdateStructureLayerEditor(false);
                return;
            }

            if (ContainsAny(branchType, "砼恢复", "混凝土恢复", "砼"))
            {
                SetCheck("BranchIncludeInCalculation", true);
                SetText("BackfillType", "中粗砂回填");
                SetText("BackfillStructure", "C25砼恢复 0.25 锁定" + Environment.NewLine + "中粗砂回填 0.25 管线层" + Environment.NewLine + "中粗砂垫层 0.10 锁定");
                UpdateStructureLayerEditor(false);
                return;
            }

            UpdateStructureLayerEditor(false);
        }

        private double CalculateMainPipeAverageDepthForUi()
        {
            if (!QuantityPipeAttributes.IsMainPipeKind(_kind)) return 0.0;

            // v22：起点深度、终点深度字段本身就是管线开挖深度。
            // 平均深度直接随当前起终点深度实时变化，不再重复叠加管线垫层。
            double startExcavationDepth = GetNumber("StartDepth", 0.0);
            double endExcavationDepth = GetNumber("EndDepth", 0.0);

            if (startExcavationDepth > 0 && endExcavationDepth > 0) return (startExcavationDepth + endExcavationDepth) / 2.0;
            if (startExcavationDepth > 0) return startExcavationDepth;
            if (endExcavationDepth > 0) return endExcavationDepth;
            return 0.0;
        }

        private static double ResolveAverageDepthForDisplay(QuantityPipeAttributes attrs)
        {
            if (attrs == null) return 0.0;
            if (attrs.AverageDepth > 0) return attrs.AverageDepth;
            if (attrs.StartDepth > 0 && attrs.EndDepth > 0) return (attrs.StartDepth + attrs.EndDepth) / 2.0;
            if (attrs.StartDepth > 0) return attrs.StartDepth;
            if (attrs.EndDepth > 0) return attrs.EndDepth;
            return 0.0;
        }

        private void RecalculateMainPipeDerivedValuesFromDepthChange()
        {
            if (!QuantityPipeAttributes.IsMainPipeKind(_kind))
            {
                UpdateStructureLayerEditor(false);
                return;
            }

            if (_updatingDerivedValues) return;
            _updatingDerivedValues = true;
            try
            {
                double average = CalculateMainPipeAverageDepthForUi();
                if (average > 0) SetNumber("AverageDepth", average);
                UpdateStructureLayerEditor(false);
                _lastPipeCushion = GetPipeCushionForUi();
            }
            finally
            {
                _updatingDerivedValues = false;
            }
        }

        private void OnStructureLayerEditorChanged()
        {
            if (_updatingDerivedValues) return;
            _updatingDerivedValues = true;
            try
            {
                if (QuantityPipeAttributes.IsMainPipeKind(_kind))
                {
                    double editedPipeCushion = GetPipeCushionForUi();
                    ApplyPipeCushionDeltaToEndpointDepths(editedPipeCushion - _lastPipeCushion);
                    SetMainPipeAverageFromCurrentEndpointDepths();

                    // 用新的平均深度重新分配非锁定结构层。若被自动分配的刚好也是管线垫层，
                    // 则再次把垫层差值同步到起终点深度，保证“结构层变，深度也变”。
                    UpdateStructureLayerEditor(false);
                    double recalculatedPipeCushion = GetPipeCushionForUi();
                    ApplyPipeCushionDeltaToEndpointDepths(recalculatedPipeCushion - editedPipeCushion);
                    SetMainPipeAverageFromCurrentEndpointDepths();
                    _lastPipeCushion = recalculatedPipeCushion;
                }
                else
                {
                    UpdateStructureLayerEditor(false);
                }
            }
            finally
            {
                _updatingDerivedValues = false;
            }
        }

        private void ApplyPipeCushionDeltaToEndpointDepths(double delta)
        {
            if (Math.Abs(delta) <= 0.000001) return;
            double startDepth = GetNumber("StartDepth", 0.0);
            double endDepth = GetNumber("EndDepth", 0.0);
            if (startDepth > 0) SetNumber("StartDepth", Math.Max(0.0, startDepth + delta));
            if (endDepth > 0) SetNumber("EndDepth", Math.Max(0.0, endDepth + delta));
        }

        private void SetMainPipeAverageFromCurrentEndpointDepths()
        {
            double average = CalculateMainPipeAverageDepthForUi();
            if (average > 0) SetNumber("AverageDepth", average);
        }

        private double GetPipeCushionForUi()
        {
            string structureText = GetText("BackfillStructure", string.Empty);
            double pipeCushion = 0.0;
            try
            {
                List<QuantityStructureLayer> layers = QuantityStructureLayer.Parse(structureText);
                pipeCushion = QuantityStructureLayer.SumHeight(layers, QuantityStructureLayer.IsSandCushion);
            }
            catch
            {
                pipeCushion = 0.0;
            }

            if (pipeCushion <= 0) pipeCushion = GetNumber("SandCushionThickness", 0.0);
            return pipeCushion < 0 ? 0.0 : pipeCushion;
        }

        private static string NormalizeWellCoverMaterial(string value)
        {
            value = value ?? string.Empty;
            if (ContainsAny(value, "混凝土", "砼")) return "混凝土井盖";
            if (ContainsAny(value, "铸铁", "球墨")) return "铸铁井盖";
            return value;
        }

        private void UpdateStructureLayerEditor(bool showWarnings)
        {
            if (_structureEditor == null) return;
            try
            {
                _structureEditor.RecalculateAutoLayers(GetStructureTotalHeight(), GetPipeDiameterForStructureCheck(), showWarnings);
            }
            catch
            {
                // 结构层只是辅助属性编辑，不应因自动计算影响窗体使用。
            }
        }

        private double GetStructureTotalHeight()
        {
            if (QuantityPipeAttributes.IsMainPipeKind(_kind))
            {
                double averageDepth = GetNumber("AverageDepth", 0.0);
                if (averageDepth > 0) return averageDepth;
                double startDepth = GetNumber("StartDepth", 0.0);
                double endDepth = GetNumber("EndDepth", 0.0);
                if (startDepth > 0 && endDepth > 0) return (startDepth + endDepth) / 2.0;
            }
            else if (QuantityPipeAttributes.IsBranchKind(_kind))
            {
                double branchDepth = GetNumber("BranchDepth", 0.0);
                if (branchDepth > 0) return branchDepth;
            }
            else if (QuantityPipeAttributes.IsNodeKind(_kind))
            {
                double wellDepth = GetNumber("WellDepth", 0.0);
                if (wellDepth > 0) return wellDepth;
            }

            if (_structureEditor != null) return _structureEditor.SumLayerHeights();
            return 0.0;
        }

        private double GetPipeDiameterForStructureCheck()
        {
            if (QuantityPipeAttributes.IsNodeKind(_kind)) return 0.0;
            return GetNumber("PipeOuterDiameter", 0.0);
        }

        private void SetText(string key, string value)
        {
            Control c;
            if (!_controls.TryGetValue(key, out c)) return;
            c.Text = value ?? string.Empty;
        }

        private void SetNumber(string key, double value)
        {
            if (Math.Abs(value) < 0.0000001) SetText(key, string.Empty);
            else SetText(key, value.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void SetCheck(string key, bool value)
        {
            Control c;
            if (!_controls.TryGetValue(key, out c)) return;
            CheckBox cb = c as CheckBox;
            if (cb != null) cb.Checked = value;
        }

        private string GetText(string key, string fallback)
        {
            Control c;
            if (!_controls.TryGetValue(key, out c)) return fallback ?? string.Empty;
            return c.Text == null ? string.Empty : c.Text.Trim();
        }

        private double GetNumber(string key, double fallback)
        {
            return QuantityPipeAttributes.ParseDouble(GetText(key, string.Empty), fallback);
        }

        private bool GetCheck(string key, bool fallback)
        {
            Control c;
            if (!_controls.TryGetValue(key, out c)) return fallback;
            CheckBox cb = c as CheckBox;
            return cb == null ? fallback : cb.Checked;
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            if (string.IsNullOrEmpty(text) || values == null) return false;
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (string.IsNullOrEmpty(value)) continue;
                if (text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static string Format(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class QuantityStructureLayerEditor : UserControl
    {
        private const string LayerTypeGeneral = "一般层";
        private const string LayerTypePipe = "管线层";
        private const string LayerTypeCushion = "垫层";
        public delegate double DoubleProvider();

        public event DoubleProvider RequestTotalHeight;
        public event DoubleProvider RequestPipeDiameter;
        public event EventHandler StructureChanged;

        private readonly DataGridView _grid;
        private readonly Label _statusLabel;
        private readonly bool _nodeWellMode;
        private bool _updating;
        private bool _warningShownInCurrentOperation;

        public QuantityStructureLayerEditor(bool nodeWellMode)
        {
            _nodeWellMode = nodeWellMode;
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            root.Controls.Add(buttons, 0, 0);

            AddSmallButton(buttons, "添加层", 70, delegate { AddLayer("回填层", 0.0, false, LayerTypeGeneral); RecalculateAutoLayers(false); RaiseStructureChanged(); });
            AddSmallButton(buttons, "删除层", 70, delegate { DeleteCurrentLayer(); RecalculateAutoLayers(false); RaiseStructureChanged(); });
            AddSmallButton(buttons, "上移", 55, delegate { MoveCurrentLayer(-1); RaiseStructureChanged(); });
            AddSmallButton(buttons, "下移", 55, delegate { MoveCurrentLayer(1); RaiseStructureChanged(); });
            AddSmallButton(buttons, "自动计算", 80, delegate { RecalculateAutoLayers(true); RaiseStructureChanged(); });

            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BackgroundColor = System.Drawing.SystemColors.Window;
            _grid.BorderStyle = BorderStyle.FixedSingle;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            _grid.Columns.Add(CreateTextColumn("LayerName", "层级名称", 180));
            _grid.Columns.Add(CreateTextColumn("LayerHeight", "高度 m", 70));
            _grid.Columns.Add(CreateCheckColumn("Locked", "锁定", 55));
            _grid.Columns.Add(CreateLayerTypeColumn());
            _grid.CurrentCellDirtyStateChanged += delegate
            {
                if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (_updating || e.RowIndex < 0) return;
                if (e.ColumnIndex == 2 || e.ColumnIndex == 3)
                {
                    if (e.ColumnIndex == 3 && IsPipeLayerRow(e.RowIndex)) EnsureSinglePipeLayer(e.RowIndex);
                    RecalculateAutoLayers(false);
                    RaiseStructureChanged();
                }
            };
            _grid.CellEndEdit += delegate { if (!_updating) { RecalculateAutoLayers(false); RaiseStructureChanged(); } };
            _grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            root.Controls.Add(_grid, 0, 1);

            _statusLabel = new Label();
            _statusLabel.AutoSize = true;
            _statusLabel.Padding = new Padding(0, 3, 0, 0);
            _statusLabel.Text = _nodeWellMode ? "非锁定层按井深自动分配；垫层位于井深之下，不参与扣减。" : "非锁定层会按总高自动分配；管线层高度应大于管径，垫层计入管线开挖深度。";
            root.Controls.Add(_statusLabel, 0, 2);
        }

        private void RaiseStructureChanged()
        {
            if (_updating) return;
            EventHandler handler = StructureChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public override string Text
        {
            get { return SerializeLayers(); }
            set { LoadLayers(value); }
        }

        private static DataGridViewTextBoxColumn CreateTextColumn(string name, string header, int width)
        {
            var col = new DataGridViewTextBoxColumn();
            col.Name = name;
            col.HeaderText = header;
            col.Width = width;
            return col;
        }

        private static DataGridViewCheckBoxColumn CreateCheckColumn(string name, string header, int width)
        {
            var col = new DataGridViewCheckBoxColumn();
            col.Name = name;
            col.HeaderText = header;
            col.Width = width;
            return col;
        }

        private static DataGridViewComboBoxColumn CreateLayerTypeColumn()
        {
            var col = new DataGridViewComboBoxColumn();
            col.Name = "LayerType";
            col.HeaderText = "层类型";
            col.Width = 78;
            col.FlatStyle = FlatStyle.Flat;
            col.Items.AddRange(LayerTypeGeneral, LayerTypePipe, LayerTypeCushion);
            return col;
        }

        private static void AddSmallButton(Control parent, string text, int width, EventHandler handler)
        {
            var btn = new Button();
            btn.Text = text;
            btn.Width = width;
            btn.Height = 25;
            btn.Margin = new Padding(0, 0, 4, 3);
            btn.Click += handler;
            parent.Controls.Add(btn);
        }

        private void AddLayer(string name, double height, bool locked, string layerType)
        {
            string normalizedType = NormalizeLayerType(layerType);
            _grid.Rows.Add(name ?? string.Empty, Format(height), locked, normalizedType);
            if (string.Equals(normalizedType, LayerTypePipe, StringComparison.Ordinal)) EnsureSinglePipeLayer(_grid.Rows.Count - 1);
            if (_grid.Rows.Count > 0) _grid.Rows[_grid.Rows.Count - 1].Selected = true;
        }

        private void DeleteCurrentLayer()
        {
            int index = CurrentRowIndex();
            if (index < 0 || index >= _grid.Rows.Count) return;
            bool wasPipeLayer = IsPipeLayerRow(index);
            _grid.Rows.RemoveAt(index);
            if (wasPipeLayer && !_nodeWellMode && _grid.Rows.Count > 0) SetCell(0, 3, LayerTypePipe);
        }

        private void MoveCurrentLayer(int offset)
        {
            int index = CurrentRowIndex();
            int target = index + offset;
            if (index < 0 || target < 0 || target >= _grid.Rows.Count) return;

            object[] values = RowValues(index);
            _grid.Rows.RemoveAt(index);
            _grid.Rows.Insert(target, values);
            _grid.ClearSelection();
            _grid.Rows[target].Selected = true;
            _grid.CurrentCell = _grid.Rows[target].Cells[0];
        }

        private int CurrentRowIndex()
        {
            if (_grid.CurrentCell != null) return _grid.CurrentCell.RowIndex;
            if (_grid.SelectedRows.Count > 0) return _grid.SelectedRows[0].Index;
            return -1;
        }

        private object[] RowValues(int index)
        {
            return new object[]
            {
                GetString(index, 0),
                GetString(index, 1),
                GetBool(index, 2),
                GetString(index, 3)
            };
        }

        private void LoadLayers(string text)
        {
            _updating = true;
            try
            {
                _grid.Rows.Clear();
                string normalized = QuantityPipeAttributes.NormalizeStructureLayerText(text);
                string[] lines = normalized.Replace("\r\n", "\n").Replace('\r', '\n').Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string raw in lines)
                {
                    ParsedStructureLayer parsed = ParseLayerLine(raw);
                    AddLayer(parsed.Name, parsed.Height, parsed.Locked, parsed.LayerType);
                }

                if (_grid.Rows.Count == 0)
                {
                    return;
                }

                if (!_nodeWellMode && !HasPipeLayer() && _grid.Rows.Count > 0)
                {
                    int index = GuessPipeLayerIndex();
                    SetCell(index, 3, LayerTypePipe);
                }
            }
            finally
            {
                _updating = false;
            }
        }

        private string SerializeLayers()
        {
            EndGridEdit();
            var lines = new List<string>();
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                string name = GetString(i, 0).Trim();
                if (name.Length == 0) name = "结构层";
                double height = ParseDouble(GetString(i, 1), 0.0);
                bool locked = GetBool(i, 2);
                string layerType = NormalizeLayerType(GetString(i, 3));

                string line = name + " " + Format(height);
                if (locked) line += " 锁定";
                if (string.Equals(layerType, LayerTypePipe, StringComparison.Ordinal)) line += " 管线层";
                else if (string.Equals(layerType, LayerTypeCushion, StringComparison.Ordinal)) line += " 垫层";
                lines.Add(line);
            }
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        public double SumLayerHeights()
        {
            EndGridEdit();
            double sum = 0.0;
            for (int i = 0; i < _grid.Rows.Count; i++) sum += ParseDouble(GetString(i, 1), 0.0);
            return sum;
        }

        public void RecalculateAutoLayers(double totalHeight, double pipeDiameter, bool showWarnings)
        {
            EndGridEdit();
            _warningShownInCurrentOperation = false;
            if (_grid.Rows.Count == 0) return;

            double lockedSum = 0.0;
            double ignoredBelowLayerSum = 0.0;
            var unlocked = new List<int>();
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                bool isBelowWellLayer = _nodeWellMode && IsCushionLayerRow(i);
                if (isBelowWellLayer)
                {
                        // 井的垫层位于用户填写的井深之下。
                        // 用户填写的井深只到垫层上方，所以自动计算井内结构层时不能再扣减垫层。
                    ignoredBelowLayerSum += ParseDouble(GetString(i, 1), 0.0);
                    continue;
                }

                if (GetBool(i, 2)) lockedSum += ParseDouble(GetString(i, 1), 0.0);
                else unlocked.Add(i);
            }

            if (totalHeight <= 0)
            {
                SetStatus("未取得有效总高，暂不自动计算非锁定层。", false, showWarnings);
                return;
            }

            if (unlocked.Count == 0)
            {
                double diff = totalHeight - lockedSum;
                if (Math.Abs(diff) > 0.001)
                {
                    SetStatus("全部层已锁定，但层高合计与总高不一致。", true, showWarnings);
                }
                else
                {
                    SetStatus(_nodeWellMode && ignoredBelowLayerSum > 0 ? "结构层高度与井深一致；垫层位于井深之下，未参与扣减。" : "结构层高度与总高一致。", false, false);
                }
                return;
            }

            double remaining = totalHeight - lockedSum;
            double each = remaining / unlocked.Count;
            _updating = true;
            try
            {
                foreach (int rowIndex in unlocked)
                {
                    SetCell(rowIndex, 1, Format(each));
                }
            }
            finally
            {
                _updating = false;
            }

            bool hasError = false;
            if (remaining < 0)
            {
                hasError = true;
                SetStatus("锁定层合计已超过总高，非锁定层计算为负值。", true, showWarnings);
            }

            int pipeRow = PipeLayerIndex();
            if (!_nodeWellMode && pipeRow >= 0 && pipeDiameter > 0)
            {
                double pipeLayerHeight = ParseDouble(GetString(pipeRow, 1), 0.0);
                if (pipeLayerHeight <= pipeDiameter)
                {
                    hasError = true;
                    SetStatus("管线所在层高度不大于管径，请人工调整。", true, showWarnings);
                }
            }

            if (!hasError)
            {
                string msg = unlocked.Count == 1 ? "已按总高自动计算非锁定层。" : "已按总高平均分配多个非锁定层。";
                if (_nodeWellMode && ignoredBelowLayerSum > 0) msg += "垫层位于井深之下，未参与扣减。";
                SetStatus(msg, false, false);
            }
        }

        public void RecalculateAutoLayers(bool showWarnings)
        {
            double totalHeight = RequestTotalHeight == null ? 0.0 : RequestTotalHeight();
            double pipeDiameter = RequestPipeDiameter == null ? 0.0 : RequestPipeDiameter();
            RecalculateAutoLayers(totalHeight, pipeDiameter, showWarnings);
        }

        private void SetStatus(string message, bool warning, bool popup)
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = warning ? System.Drawing.Color.DarkRed : System.Drawing.SystemColors.ControlText;
            if (warning && popup && !_warningShownInCurrentOperation)
            {
                _warningShownInCurrentOperation = true;
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(message + Environment.NewLine + "当前计算结果已保留，可直接手动修改。", "结构层计算提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void EnsureSinglePipeLayer(int keepIndex)
        {
            if (keepIndex < 0) return;
            _updating = true;
            try
            {
                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    if (i != keepIndex && IsPipeLayerRow(i)) SetCell(i, 3, LayerTypeGeneral);
                }
            }
            finally
            {
                _updating = false;
            }
        }

        private bool HasPipeLayer()
        {
            return PipeLayerIndex() >= 0;
        }

        private int PipeLayerIndex()
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (IsPipeLayerRow(i)) return i;
            }
            return -1;
        }

        private bool IsPipeLayerRow(int row)
        {
            if (row < 0 || row >= _grid.Rows.Count) return false;
            return string.Equals(NormalizeLayerType(GetString(row, 3)), LayerTypePipe, StringComparison.Ordinal);
        }

        private bool IsCushionLayerRow(int row)
        {
            if (row < 0 || row >= _grid.Rows.Count) return false;
            return string.Equals(NormalizeLayerType(GetString(row, 3)), LayerTypeCushion, StringComparison.Ordinal);
        }

        private int GuessPipeLayerIndex()
        {
            int candidate = -1;
            double maxHeight = double.MinValue;
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                string name = GetString(i, 0);
                if (ContainsAny(name, "回填", "原土", "中粗砂")) candidate = i;
                double h = ParseDouble(GetString(i, 1), 0.0);
                if (h > maxHeight)
                {
                    maxHeight = h;
                    if (candidate < 0) candidate = i;
                }
            }
            return candidate < 0 ? 0 : candidate;
        }

        private ParsedStructureLayer ParseLayerLine(string raw)
        {
            string line = raw == null ? string.Empty : raw.Trim();
            bool locked = ContainsAny(line, "锁定", "已锁") && !ContainsAny(line, "未锁", "不锁");
            double height = 0.0;
            string suffix = string.Empty;
            MatchCollection matches = Regex.Matches(line, @"[-+]?\d+(?:\.\d+)?");
            if (matches.Count > 0)
            {
                Match heightMatch = matches[matches.Count - 1];
                string valueText = heightMatch.Value;
                height = ParseDouble(valueText, 0.0);
                suffix = line.Substring(heightMatch.Index + heightMatch.Length).Trim();
                line = line.Substring(0, heightMatch.Index).Trim();
            }

            string layerType = ContainsAny(suffix, "井下层", "井下方垫层", "垫层")
                ? LayerTypeCushion
                : (ContainsAny(suffix, "管线层", "管道层", "管线所在层", "管道所在层") ? LayerTypePipe : LayerTypeGeneral);

            string name = line;
            name = name.Replace("管线所在层", string.Empty).Replace("管道所在层", string.Empty)
                       .Replace("井下方垫层", string.Empty).Replace("井下层", string.Empty)
                       .Replace("管线层", string.Empty).Replace("管道层", string.Empty)
                       .Replace("锁定", string.Empty).Replace("已锁", string.Empty)
                       .Replace("未锁", string.Empty).Replace("不锁", string.Empty)
                       .Replace("[", string.Empty).Replace("]", string.Empty)
                       .Replace("（", string.Empty).Replace("）", string.Empty)
                       .Replace("(", string.Empty).Replace(")", string.Empty)
                       .Trim();
            if (name.Length == 0) name = "结构层";
            return new ParsedStructureLayer(name, height, locked, layerType);
        }

        private static string NormalizeLayerType(string value)
        {
            if (string.Equals(value, LayerTypePipe, StringComparison.CurrentCultureIgnoreCase)) return LayerTypePipe;
            if (string.Equals(value, LayerTypeCushion, StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(value, "井下层", StringComparison.CurrentCultureIgnoreCase)) return LayerTypeCushion;
            return LayerTypeGeneral;
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(text) || values == null) return false;
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private void EndGridEdit()
        {
            try
            {
                // CheckBox / ComboBox 单元格在点击后可能还停留在编辑态。
                // 必须先提交 Dirty 值，再结束编辑，避免自动计算读取到上一状态。
                if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                if (_grid.IsCurrentCellInEditMode) _grid.EndEdit();
            }
            catch { }
        }

        private string GetString(int row, int col)
        {
            if (row < 0 || row >= _grid.Rows.Count) return string.Empty;
            object value = _grid.Rows[row].Cells[col].Value;
            return value == null ? string.Empty : value.ToString();
        }

        private bool GetBool(int row, int col)
        {
            if (row < 0 || row >= _grid.Rows.Count) return false;
            DataGridViewCell cell = _grid.Rows[row].Cells[col];

            object value = cell.Value;
            if (IsTruthy(value)) return true;

            // 当前正在编辑的 CheckBox 值有时还没写入 Value，尤其是在点击勾选后立刻点“自动计算”时。
            // 这里同时读取 EditedFormattedValue / FormattedValue，避免界面显示已勾选但计算仍按未勾选处理。
            try
            {
                if (_grid.CurrentCell != null && _grid.CurrentCell.RowIndex == row && _grid.CurrentCell.ColumnIndex == col)
                {
                    if (IsTruthy(cell.EditedFormattedValue)) return true;
                }
            }
            catch { }

            try
            {
                if (IsTruthy(cell.FormattedValue)) return true;
            }
            catch { }

            return false;
        }

        private static bool IsTruthy(object value)
        {
            if (value == null) return false;
            if (value is bool) return (bool)value;
            string text = value.ToString();
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            return string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "是", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "勾选", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "选中", StringComparison.OrdinalIgnoreCase);
        }

        private void SetCell(int row, int col, object value)
        {
            if (row < 0 || row >= _grid.Rows.Count) return;
            _grid.Rows[row].Cells[col].Value = value;
        }

        private static double ParseDouble(string text, double fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            double value;
            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value;
            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return value;
            return fallback;
        }

        private static string Format(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private sealed class ParsedStructureLayer
        {
            public readonly string Name;
            public readonly double Height;
            public readonly bool Locked;
            public readonly string LayerType;

            public ParsedStructureLayer(string name, double height, bool locked, string layerType)
            {
                Name = name;
                Height = height;
                Locked = locked;
                LayerType = NormalizeLayerType(layerType);
            }
        }
    }

    internal sealed class QuantityNoMouseWheelComboBox : ComboBox
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // 安全处理：不再拦截底层 WndProc 消息，避免 AutoCAD 宿主环境下卡死/闪退。
            // 未展开时不调用 base，尽量避免滚轮改变选项；展开时保留列表滚动。
            if (DroppedDown)
            {
                base.OnMouseWheel(e);
                return;
            }

            HandledMouseEventArgs handled = e as HandledMouseEventArgs;
            if (handled != null) handled.Handled = true;
            ReleaseComboFocus();
        }

        protected override void OnDropDownClosed(EventArgs e)
        {
            base.OnDropDownClosed(e);
            BeginReleaseComboFocus();
        }

        protected override void OnSelectionChangeCommitted(EventArgs e)
        {
            base.OnSelectionChangeCommitted(e);
            BeginReleaseComboFocus();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!DroppedDown) BeginReleaseComboFocus();
        }

        private void BeginReleaseComboFocus()
        {
            if (!IsHandleCreated || IsDisposed) return;
            try
            {
                BeginInvoke(new MethodInvoker(ReleaseComboFocus));
            }
            catch
            {
                // AutoCAD 关闭窗体或命令切换时可能已经释放句柄，忽略即可。
            }
        }

        private void ReleaseComboFocus()
        {
            if (IsDisposed || DroppedDown) return;
            try
            {
                Form form = FindForm();
                if (form != null && form.ActiveControl == this)
                {
                    form.ActiveControl = null;
                }

                if (Parent != null && Parent.CanFocus)
                {
                    Parent.Focus();
                }
            }
            catch
            {
                // 只影响防滚轮误触，不应影响属性编辑功能。
            }
        }
    }
}
