using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using TCPipeAutoDraw.Modules.SurfaceAreaAnnotation;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    /// <summary>
    /// ?????????
    /// ???????????????????????????????????????
    /// </summary>
    public sealed class PipeLengthAnnotationForm : Form
    {
        private readonly Document _doc;
        private readonly PipeLengthAnnotationOptions _initialOptions;
        private NumericUpDown _numTextHeight;
        private NumericUpDown _numDecimals;
        private ComboBox _cmbFonts;
        private TextBox _txtTemplate;
        private CheckBox _chkBottomAnnotation;
        private NumericUpDown _numExcavationWidth;
        private NumericUpDown _numExcavationHeight;
        private NumericUpDown _numExcavationDepth;
        private TextBox _txtBottomTemplate;
        private RadioButton _radDefaultZJ;
        private RadioButton _radExistingLayer;
        private RadioButton _radCustomLayer;
        private ComboBox _cmbLayers;
        private TextBox _txtCustomLayer;
        private CheckBox _chkAutoLayerBySourceMetadata;
        private ComboBox _cmbLayerLinkMode;
        private TextBox _txtLayerSuffix;
        private TextBox _txtFallbackLayer;
        private CheckBox _chkWriteAutoLayerMetadata;
        private TextBox _txtSplitTags;
        //private TextBox _txtLog;

        public PipeLengthAnnotationForm(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            _doc = doc;
            _initialOptions = PipeLengthAnnotationSettingsStore.Load();

            Text = "????????????";
            Width = 800;
            Height = 700;
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
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var title = new Label();
            title.Text = "??????";
            title.AutoSize = true;
            title.Font = new System.Drawing.Font(title.Font.FontFamily, 11, System.Drawing.FontStyle.Bold);
            title.Padding = new Padding(0, 0, 0, 8);
            root.Controls.Add(title, 0, 0);

            var options = new TableLayoutPanel();
            options.Dock = DockStyle.Top;
            options.ColumnCount = 2;
            options.RowCount = 16;
            options.AutoSize = true;
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(options, 0, 1);

            _numTextHeight = MakeNumber(0.1M, 1000M, 1M, 1);
            SetNumberValue(_numTextHeight, (decimal)_initialOptions.TextHeight);
            AddRow(options, 0, "??????", _numTextHeight);

            _numDecimals = MakeNumber(0M, 6M, 2M, 0);
            SetNumberValue(_numDecimals, _initialOptions.DecimalPlaces);
            AddRow(options, 1, "?????", _numDecimals);

            _cmbFonts = new ComboBox();
            _cmbFonts.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbFonts.Width = 260;
            _cmbFonts.DataSource = LoadTextStyleNames();
            SelectDefaultTextStyle(_cmbFonts, _initialOptions.AnnotationFontName);
            AddRow(options, 2, "????", _cmbFonts);

            _radDefaultZJ = new RadioButton();
            _radDefaultZJ.Text = "???????ZJ???????????";
            _radDefaultZJ.Checked = true;
            _radDefaultZJ.AutoSize = true;
            _radDefaultZJ.CheckedChanged += delegate { if (_radDefaultZJ.Checked) UncheckOtherLayerModes(_radDefaultZJ); UpdateLayerControls(); };
            AddRow(options, 3, "????", _radDefaultZJ);

            var existingPanel = new FlowLayoutPanel();
            existingPanel.Dock = DockStyle.Fill;
            existingPanel.AutoSize = true;
            existingPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;

            _radExistingLayer = new RadioButton();
            _radExistingLayer.Text = "??????";
            _radExistingLayer.AutoSize = true;
            _radExistingLayer.CheckedChanged += delegate { if (_radExistingLayer.Checked) UncheckOtherLayerModes(_radExistingLayer); UpdateLayerControls(); };
            existingPanel.Controls.Add(_radExistingLayer);

            _cmbLayers = new ComboBox();
            _cmbLayers.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbLayers.Width = 260;
            _cmbLayers.DataSource = LoadLayerNames();
            _cmbLayers.SelectedIndexChanged += delegate { if (_cmbLayers.Focused) _radExistingLayer.Checked = true; };
            _cmbLayers.Click += delegate { _radExistingLayer.Checked = true; };
            existingPanel.Controls.Add(_cmbLayers);
            AddRow(options, 4, "", existingPanel);

            var customPanel = new FlowLayoutPanel();
            customPanel.Dock = DockStyle.Fill;
            customPanel.AutoSize = true;
            customPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;

            _radCustomLayer = new RadioButton();
            _radCustomLayer.Text = "?????";
            _radCustomLayer.AutoSize = true;
            _radCustomLayer.CheckedChanged += delegate { if (_radCustomLayer.Checked) UncheckOtherLayerModes(_radCustomLayer); UpdateLayerControls(); };
            customPanel.Controls.Add(_radCustomLayer);

            _txtCustomLayer = new TextBox();
            _txtCustomLayer.Text = string.IsNullOrWhiteSpace(_initialOptions.AnnotationLayerName) ? PipeLengthAnnotationOptions.Default.AnnotationLayerName : _initialOptions.AnnotationLayerName;
            _txtCustomLayer.Width = 260;
            _txtCustomLayer.Click += delegate { _radCustomLayer.Checked = true; };
            customPanel.Controls.Add(_txtCustomLayer);
            AddRow(options, 5, "", customPanel);

            Control linkPanel = BuildLayerLinkPanel();
            AddRow(options, 6, "????", linkPanel);

            _txtTemplate = new TextBox();
            _txtTemplate.Multiline = false;
            _txtTemplate.Text = string.IsNullOrWhiteSpace(_initialOptions.AnnotationTemplate) ? PipeLengthAnnotationOptions.Default.AnnotationTemplate : _initialOptions.AnnotationTemplate;
            AddRow(options, 7, "??????", _txtTemplate);

            _chkBottomAnnotation = new CheckBox();
            _chkBottomAnnotation.Text = "????????????????";
            _chkBottomAnnotation.AutoSize = true;
            _chkBottomAnnotation.Checked = _initialOptions.DrawBottomAnnotation;
            _chkBottomAnnotation.CheckedChanged += delegate { UpdateBottomAnnotationControls(); };
            AddRow(options, 8, "????", _chkBottomAnnotation);

            var excavationPanel = new FlowLayoutPanel();
            excavationPanel.Dock = DockStyle.Fill;
            excavationPanel.AutoSize = true;
            excavationPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;

            excavationPanel.Controls.Add(MakeInlineLabel("?"));
            _numExcavationWidth = MakeNumber(0M, 100000M, 0M, 3);
            SetNumberValue(_numExcavationWidth, (decimal)_initialOptions.ExcavationWidth);
            excavationPanel.Controls.Add(_numExcavationWidth);

            excavationPanel.Controls.Add(MakeInlineLabel("?"));
            _numExcavationHeight = MakeNumber(0M, 100000M, 0M, 3);
            SetNumberValue(_numExcavationHeight, (decimal)_initialOptions.ExcavationHeight);
            excavationPanel.Controls.Add(_numExcavationHeight);

            excavationPanel.Controls.Add(MakeInlineLabel("?"));
            _numExcavationDepth = MakeNumber(0M, 100000M, 0M, 3);
            SetNumberValue(_numExcavationDepth, (decimal)_initialOptions.ExcavationDepth);
            excavationPanel.Controls.Add(_numExcavationDepth);

            AddRow(options, 9, "???????", excavationPanel);

            _txtBottomTemplate = new TextBox();
            _txtBottomTemplate.Multiline = false;
            _txtBottomTemplate.Text = string.IsNullOrWhiteSpace(_initialOptions.BottomAnnotationTemplate) ? PipeLengthAnnotationOptions.Default.BottomAnnotationTemplate : _initialOptions.BottomAnnotationTemplate;
            AddRow(options, 10, "??????", _txtBottomTemplate);

            var buttons = new FlowLayoutPanel();
            buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            root.Controls.Add(buttons, 0, 3);

            var close = new Button();
            close.Text = "??";
            close.AutoSize = true;
            close.Click += delegate { Close(); };
            buttons.Controls.Add(close);

            var run = new Button();
            run.Text = "????";
            run.Width = 170;
            run.Height = 30;
            run.Click += delegate { RunCalculation(); };
            buttons.Controls.Add(run);

            SelectDefaultTextStyle(_cmbLayers, _initialOptions.SelectedLayerName);
            ApplyInitialLayerMode();
            UpdateLayerControls();
            UpdateLayerLinkControls();
            UpdateBottomAnnotationControls();
        }


        private Control BuildLayerLinkPanel()
        {
            var panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.AutoSize = true;
            panel.ColumnCount = 1;
            panel.RowCount = 6;
            for (int i = 0; i < 6; i++) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _chkAutoLayerBySourceMetadata = new CheckBox();
            _chkAutoLayerBySourceMetadata.Text = "??????????/??????????";
            _chkAutoLayerBySourceMetadata.AutoSize = true;
            _chkAutoLayerBySourceMetadata.Checked = _initialOptions.EnableSourceMetadataLayerLink;
            _chkAutoLayerBySourceMetadata.CheckedChanged += delegate { UpdateLayerLinkControls(); UpdateLayerControls(); };
            panel.Controls.Add(_chkAutoLayerBySourceMetadata, 0, 0);

            var modePanel = new FlowLayoutPanel();
            modePanel.AutoSize = true;
            modePanel.Dock = DockStyle.Fill;
            modePanel.Controls.Add(MakeInlineLabel("????"));
            _cmbLayerLinkMode = new ComboBox();
            _cmbLayerLinkMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbLayerLinkMode.Width = 210;
            _cmbLayerLinkMode.Items.Add("??????? ? ????");
            _cmbLayerLinkMode.Items.Add("????110PVC? ? 110PVC???");
            _cmbLayerLinkMode.Items.Add("?????? ? ????");
            _cmbLayerLinkMode.Items.Add("????+?????-????");
            _cmbLayerLinkMode.SelectedIndex = ToLayerLinkModeIndex(_initialOptions.LayerLinkMode);
            modePanel.Controls.Add(_cmbLayerLinkMode);
            panel.Controls.Add(modePanel, 0, 1);

            var suffixPanel = new FlowLayoutPanel();
            suffixPanel.AutoSize = true;
            suffixPanel.Dock = DockStyle.Fill;
            suffixPanel.Controls.Add(MakeInlineLabel("????"));
            _txtLayerSuffix = new TextBox();
            _txtLayerSuffix.Width = 90;
            _txtLayerSuffix.Text = string.IsNullOrWhiteSpace(_initialOptions.AutoAnnotationLayerSuffix) ? "??" : _initialOptions.AutoAnnotationLayerSuffix;
            suffixPanel.Controls.Add(_txtLayerSuffix);
            suffixPanel.Controls.Add(MakeInlineLabel("???"));
            _txtFallbackLayer = new TextBox();
            _txtFallbackLayer.Width = 120;
            _txtFallbackLayer.Text = string.IsNullOrWhiteSpace(_initialOptions.FallbackAnnotationLayerName) ? "?????" : _initialOptions.FallbackAnnotationLayerName;
            suffixPanel.Controls.Add(_txtFallbackLayer);
            panel.Controls.Add(suffixPanel, 0, 2);

            _chkWriteAutoLayerMetadata = new CheckBox();
            _chkWriteAutoLayerMetadata.Text = "??????????? CDBox ???/??";
            _chkWriteAutoLayerMetadata.AutoSize = true;
            _chkWriteAutoLayerMetadata.Checked = _initialOptions.WriteAutoAnnotationLayerMetadata;
            panel.Controls.Add(_chkWriteAutoLayerMetadata, 0, 3);

            var tagPanel = new FlowLayoutPanel();
            tagPanel.AutoSize = true;
            tagPanel.Dock = DockStyle.Fill;
            tagPanel.Controls.Add(MakeInlineLabel("???????"));
            _txtSplitTags = new TextBox();
            _txtSplitTags.Width = 280;
            _txtSplitTags.Text = _initialOptions.AnnotationSplitTagText ?? "????????????";
            tagPanel.Controls.Add(_txtSplitTags);
            panel.Controls.Add(tagPanel, 0, 4);

            var tip = new Label();
            tip.Text = "???????????ZJ/????/??????????????????????????????";
            tip.AutoSize = true;
            tip.ForeColor = System.Drawing.SystemColors.GrayText;
            panel.Controls.Add(tip, 0, 5);

            return panel;
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
                        names.Add(layer.Name);
                    }
                    tr.Commit();
                }
            }
            catch { }

            if (_initialOptions != null)
            {
                if (!string.IsNullOrWhiteSpace(_initialOptions.SelectedLayerName) && !ContainsIgnoreCase(names, _initialOptions.SelectedLayerName))
                {
                    names.Add(_initialOptions.SelectedLayerName);
                }

                if (!string.IsNullOrWhiteSpace(_initialOptions.AnnotationLayerName) && !ContainsIgnoreCase(names, _initialOptions.AnnotationLayerName))
                {
                    names.Add(_initialOptions.AnnotationLayerName);
                }
            }

            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            if (!ContainsIgnoreCase(names, "ZJ")) names.Insert(0, "ZJ");
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

            if (ContainsIgnoreCase(names, "??")) MoveNameToTop(names, "??");
            else if (!string.IsNullOrWhiteSpace(currentStyleName) && ContainsIgnoreCase(names, currentStyleName)) MoveNameToTop(names, currentStyleName);
            else MoveNameToTop(names, "STANDARD");

            return names;
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

        private static void SetNumberValue(NumericUpDown number, decimal value)
        {
            if (number == null) return;
            if (value < number.Minimum) value = number.Minimum;
            if (value > number.Maximum) value = number.Maximum;
            number.Value = value;
        }

        private void ApplyInitialLayerMode()
        {
            if (_initialOptions == null)
            {
                if (_radDefaultZJ != null) _radDefaultZJ.Checked = true;
                return;
            }

            if (_initialOptions.LayerMode == AnnotationLayerMode.ExistingLayer)
            {
                if (_radExistingLayer != null) _radExistingLayer.Checked = true;
                return;
            }

            if (_initialOptions.LayerMode == AnnotationLayerMode.CustomLayer)
            {
                if (_radCustomLayer != null) _radCustomLayer.Checked = true;
                return;
            }

            if (_radDefaultZJ != null) _radDefaultZJ.Checked = true;
        }

        private static Label MakeInlineLabel(string text)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            label.Padding = new Padding(8, 6, 2, 0);
            return label;
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

        private static void AddRow(TableLayoutPanel panel, int row, string labelText, Control control)
        {
            var label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Dock = DockStyle.Fill;
            label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            label.Padding = new Padding(0, 4, 8, 4);
            panel.Controls.Add(label, 0, row);

            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 4, 0, 4);
            panel.Controls.Add(control, 1, row);
        }

        private void UncheckOtherLayerModes(RadioButton selected)
        {
            if (_radDefaultZJ != null && !object.ReferenceEquals(selected, _radDefaultZJ)) _radDefaultZJ.Checked = false;
            if (_radExistingLayer != null && !object.ReferenceEquals(selected, _radExistingLayer)) _radExistingLayer.Checked = false;
            if (_radCustomLayer != null && !object.ReferenceEquals(selected, _radCustomLayer)) _radCustomLayer.Checked = false;
        }

        private void UpdateLayerControls()
        {
            bool autoLink = _chkAutoLayerBySourceMetadata != null && _chkAutoLayerBySourceMetadata.Checked;
            if (_radDefaultZJ != null) _radDefaultZJ.Enabled = !autoLink;
            if (_radExistingLayer != null) _radExistingLayer.Enabled = !autoLink;
            if (_radCustomLayer != null) _radCustomLayer.Enabled = !autoLink;
            if (_cmbLayers != null) _cmbLayers.Enabled = !autoLink && _radExistingLayer != null && _radExistingLayer.Checked;
            if (_txtCustomLayer != null) _txtCustomLayer.Enabled = !autoLink && _radCustomLayer != null && _radCustomLayer.Checked;
        }

        private void UpdateLayerLinkControls()
        {
            bool enabled = _chkAutoLayerBySourceMetadata != null && _chkAutoLayerBySourceMetadata.Checked;
            if (_cmbLayerLinkMode != null) _cmbLayerLinkMode.Enabled = enabled;
            if (_txtLayerSuffix != null) _txtLayerSuffix.Enabled = enabled;
            if (_txtFallbackLayer != null) _txtFallbackLayer.Enabled = enabled;
            if (_chkWriteAutoLayerMetadata != null) _chkWriteAutoLayerMetadata.Enabled = enabled;
            if (_txtSplitTags != null) _txtSplitTags.Enabled = enabled;
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

        private void UpdateBottomAnnotationControls()
        {
            bool enabled = _chkBottomAnnotation != null && _chkBottomAnnotation.Checked;
            if (_numExcavationWidth != null) _numExcavationWidth.Enabled = enabled;
            if (_numExcavationHeight != null) _numExcavationHeight.Enabled = enabled;
            if (_numExcavationDepth != null) _numExcavationDepth.Enabled = enabled;
            if (_txtBottomTemplate != null) _txtBottomTemplate.Enabled = enabled;
        }

        private PipeLengthAnnotationOptions ReadOptions()
        {
            AnnotationLayerMode mode = AnnotationLayerMode.DefaultZJ;
            if (_radExistingLayer.Checked) mode = AnnotationLayerMode.ExistingLayer;
            if (_radCustomLayer.Checked) mode = AnnotationLayerMode.CustomLayer;

            string selectedLayer = _cmbLayers.SelectedItem == null ? "ZJ" : _cmbLayers.SelectedItem.ToString();
            string customLayer = string.IsNullOrWhiteSpace(_txtCustomLayer.Text) ? "ZJ" : _txtCustomLayer.Text.Trim();
            string textStyleName = _cmbFonts == null || _cmbFonts.SelectedItem == null
                ? PipeLengthAnnotationOptions.Default.AnnotationFontName
                : _cmbFonts.SelectedItem.ToString();

            return new PipeLengthAnnotationOptions
            {
                TextHeight = (double)_numTextHeight.Value,
                DecimalPlaces = (int)_numDecimals.Value,
                AnnotationTemplate = _txtTemplate.Text,
                AnnotationFontName = textStyleName,
                LayerMode = mode,
                SelectedLayerName = selectedLayer,
                AnnotationLayerName = customLayer,
                DrawLeader = true,
                EnableSourceMetadataLayerLink = _chkAutoLayerBySourceMetadata != null && _chkAutoLayerBySourceMetadata.Checked,
                LayerLinkMode = FromLayerLinkModeIndex(_cmbLayerLinkMode == null ? 0 : _cmbLayerLinkMode.SelectedIndex),
                AutoAnnotationLayerSuffix = _txtLayerSuffix == null ? "??" : _txtLayerSuffix.Text,
                FallbackAnnotationLayerName = _txtFallbackLayer == null ? "?????" : _txtFallbackLayer.Text,
                WriteAutoAnnotationLayerMetadata = _chkWriteAutoLayerMetadata == null || _chkWriteAutoLayerMetadata.Checked,
                AnnotationSplitTagText = _txtSplitTags == null ? string.Empty : _txtSplitTags.Text,
                DrawBottomAnnotation = _chkBottomAnnotation != null && _chkBottomAnnotation.Checked,
                BottomAnnotationTemplate = _txtBottomTemplate == null ? PipeLengthAnnotationOptions.Default.BottomAnnotationTemplate : _txtBottomTemplate.Text,
                ExcavationWidth = _numExcavationWidth == null ? 0.0 : (double)_numExcavationWidth.Value,
                ExcavationHeight = _numExcavationHeight == null ? 0.0 : (double)_numExcavationHeight.Value,
                ExcavationDepth = _numExcavationDepth == null ? 0.0 : (double)_numExcavationDepth.Value
            };
        }

        private void RunCalculation()
        {
            bool wasVisible = Visible;
            bool restoreForm = true;
            try
            {
                PipeLengthAnnotationOptions options = ReadOptions();
                PipeLengthAnnotationSettingsStore.Save(options);
                if (wasVisible) Hide();

                while (true)
                {
                    PipeLengthAnnotationResult result = PipeLengthAnnotationService.SelectCalculateAndAnnotate(_doc, options);
                    _doc.Editor.WriteHudMessage(result.ToEditorMessage());
                    //WriteLog(result);

                    if (result.IsCancelled)
                    {
                        restoreForm = false;
                        Close();
                        break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
    }
}
