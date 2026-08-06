using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace TCPipeAutoDraw.Modules.SurfaceAreaAnnotation
{
    /// <summary>
    /// ????????
    /// ?? WinForms????? WPF ??????? AutoCAD 2023 / CASS11 ???????
    /// </summary>
    public sealed class SurfaceAreaAnnotationForm : Form
    {
        private readonly Document _doc;
        private readonly SurfaceAreaAnnotationOptions _initialOptions;
        private NumericUpDown _numInterval;
        private NumericUpDown _numTextHeight;
        private NumericUpDown _numDecimals;
        private ComboBox _cmbCalculationMode;
        private TextBox _txtCassLogPath;
        private Button _btnBrowseCassLog;
        private CheckBox _chkDeleteCassObjects;
        private ComboBox _cmbFonts;
        private TextBox _txtTemplate;
        private RadioButton _radDefaultZJ;
        private RadioButton _radExistingLayer;
        private RadioButton _radCustomLayer;
        private ComboBox _cmbLayers;
        private TextBox _txtCustomLayer;
        //private TextBox _txtLog;

        public SurfaceAreaAnnotationForm(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            _doc = doc;
            _initialOptions = SurfaceAreaAnnotationSettingsStore.Load();

            Text = "???????????";
            Width = 620;
            Height = 680;
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
            title.Text = "??? / ????";
            title.AutoSize = true;
            title.Font = new System.Drawing.Font(title.Font.FontFamily, 11, System.Drawing.FontStyle.Bold);
            title.Padding = new Padding(0, 0, 0, 8);
            root.Controls.Add(title, 0, 0);

            var options = new TableLayoutPanel();
            options.Dock = DockStyle.Top;
            options.ColumnCount = 2;
            options.RowCount = 10;
            options.AutoSize = true;
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(options, 0, 1);

            _numInterval = MakeNumber(0.1M, 1000M, 5M, 1);
            SetNumberValue(_numInterval, (decimal)_initialOptions.BoundaryInterval);
            AddRow(options, 0, "?????????", _numInterval);

            _cmbCalculationMode = new ComboBox();
            _cmbCalculationMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbCalculationMode.Items.Add("?????");
            _cmbCalculationMode.Items.Add("????");
            _cmbCalculationMode.SelectedIndex = _initialOptions.CalculationMode == SurfaceAreaCalculationMode.PlanArea ? 1 : 0;
            _cmbCalculationMode.SelectedIndexChanged += delegate { UpdateCalculationControls(); };
            AddRow(options, 1, "????", _cmbCalculationMode);

            _chkDeleteCassObjects = new CheckBox();
            _chkDeleteCassObjects.Text = "?? CASS ????????????????????";
            _chkDeleteCassObjects.Checked = !_initialOptions.DeleteCassGeneratedObjects;
            _chkDeleteCassObjects.AutoSize = true;
            AddRow(options, 2, "CASS???", _chkDeleteCassObjects);

            // ?????????? surface.log ????/????????????????
            _txtCassLogPath = new TextBox();
            _btnBrowseCassLog = new Button();

            _numTextHeight = MakeNumber(0.1M, 1000M, 1M, 1);
            SetNumberValue(_numTextHeight, (decimal)_initialOptions.TextHeight);
            AddRow(options, 3, "??????", _numTextHeight);

            _numDecimals = MakeNumber(0M, 6M, 2M, 0);
            SetNumberValue(_numDecimals, _initialOptions.DecimalPlaces);
            AddRow(options, 4, "?????", _numDecimals);

            _cmbFonts = new ComboBox();
            _cmbFonts.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbFonts.Width = 260;
            _cmbFonts.DataSource = LoadTextStyleNames();
            SelectDefaultTextStyle(_cmbFonts, _initialOptions.AnnotationFontName);
            AddRow(options, 5, "????", _cmbFonts);

            _radDefaultZJ = new RadioButton();
            _radDefaultZJ.Text = "???????ZJ???????????";
            _radDefaultZJ.Checked = true;
            _radDefaultZJ.AutoSize = true;
            _radDefaultZJ.CheckedChanged += delegate { if (_radDefaultZJ.Checked) UncheckOtherLayerModes(_radDefaultZJ); UpdateLayerControls(); };
            AddRow(options, 6, "????", _radDefaultZJ);

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
            AddRow(options, 7, "", existingPanel);

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
            _txtCustomLayer.Text = string.IsNullOrWhiteSpace(_initialOptions.AnnotationLayerName) ? SurfaceAreaAnnotationOptions.Default.AnnotationLayerName : _initialOptions.AnnotationLayerName;
            _txtCustomLayer.Width = 260;
            _txtCustomLayer.Click += delegate { _radCustomLayer.Checked = true; };
            customPanel.Controls.Add(_txtCustomLayer);
            AddRow(options, 8, "", customPanel);

            _txtTemplate = new TextBox();
            _txtTemplate.Multiline = false;
            _txtTemplate.Text = string.IsNullOrWhiteSpace(_initialOptions.AnnotationTemplate) ? SurfaceAreaAnnotationOptions.Default.AnnotationTemplate : _initialOptions.AnnotationTemplate;
            AddRow(options, 9, "????", _txtTemplate);

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
            run.Text = "?????";
            run.Width = 170;
            run.Height = 30;
            run.Click += delegate { RunCalculation(); };
            buttons.Controls.Add(run);

            SelectDefaultTextStyle(_cmbLayers, _initialOptions.SelectedLayerName);
            ApplyInitialLayerMode();
            UpdateLayerControls();
            UpdateCalculationControls();
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
            catch
            {
                // ????????? ZJ?????????
            }

            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            if (!names.Contains("ZJ")) names.Insert(0, "ZJ");
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
            catch
            {
                // ??????? STANDARD ?????????????
            }

            if (names.Count == 0) names.Add("STANDARD");
            names.Sort(StringComparer.CurrentCultureIgnoreCase);

            if (ContainsIgnoreCase(names, "??"))
            {
                MoveNameToTop(names, "??");
            }
            else if (!string.IsNullOrWhiteSpace(currentStyleName) && ContainsIgnoreCase(names, currentStyleName))
            {
                MoveNameToTop(names, currentStyleName);
            }
            else
            {
                MoveNameToTop(names, "STANDARD");
            }

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
            AnnotationLayerMode mode = _initialOptions == null ? AnnotationLayerMode.DefaultZJ : _initialOptions.LayerMode;
            if (mode == AnnotationLayerMode.ExistingLayer && _radExistingLayer != null)
            {
                _radExistingLayer.Checked = true;
            }
            else if (mode == AnnotationLayerMode.CustomLayer && _radCustomLayer != null)
            {
                _radCustomLayer.Checked = true;
            }
            else if (_radDefaultZJ != null)
            {
                _radDefaultZJ.Checked = true;
            }
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
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Padding = new Padding(0, 4, 8, 4);
            panel.Controls.Add(label, 0, row);

            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 4, 0, 4);
            panel.Controls.Add(control, 1, row);
        }

        private void UpdateCalculationControls()
        {
            bool useCass = _cmbCalculationMode == null || _cmbCalculationMode.SelectedIndex != 1;
            if (_chkDeleteCassObjects != null) _chkDeleteCassObjects.Enabled = useCass;
            if (_txtCassLogPath != null) _txtCassLogPath.Enabled = false;
            if (_btnBrowseCassLog != null) _btnBrowseCassLog.Enabled = false;
        }

        private void BrowseCassLogFile()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "?? CASS surface.log ??";
                dialog.Filter = "CASS surface.log|surface.log|???? (*.log)|*.log|???? (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;

                try
                {
                    if (!string.IsNullOrWhiteSpace(_doc.Database.Filename))
                    {
                        string folder = System.IO.Path.GetDirectoryName(_doc.Database.Filename);
                        if (!string.IsNullOrWhiteSpace(folder) && System.IO.Directory.Exists(folder))
                        {
                            dialog.InitialDirectory = folder;
                        }
                    }
                }
                catch { }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _txtCassLogPath.Text = dialog.FileName;
                }
            }
        }

        private void UncheckOtherLayerModes(RadioButton selected)
        {
            if (_radDefaultZJ != null && !object.ReferenceEquals(selected, _radDefaultZJ)) _radDefaultZJ.Checked = false;
            if (_radExistingLayer != null && !object.ReferenceEquals(selected, _radExistingLayer)) _radExistingLayer.Checked = false;
            if (_radCustomLayer != null && !object.ReferenceEquals(selected, _radCustomLayer)) _radCustomLayer.Checked = false;
        }

        private void UpdateLayerControls()
        {
            if (_cmbLayers != null) _cmbLayers.Enabled = _radExistingLayer != null && _radExistingLayer.Checked;
            if (_txtCustomLayer != null) _txtCustomLayer.Enabled = _radCustomLayer != null && _radCustomLayer.Checked;
        }

        private SurfaceAreaAnnotationOptions ReadOptions()
        {
            AnnotationLayerMode mode = AnnotationLayerMode.DefaultZJ;
            if (_radExistingLayer.Checked) mode = AnnotationLayerMode.ExistingLayer;
            if (_radCustomLayer.Checked) mode = AnnotationLayerMode.CustomLayer;

            string selectedLayer = _cmbLayers.SelectedItem == null ? "ZJ" : _cmbLayers.SelectedItem.ToString();
            string customLayer = string.IsNullOrWhiteSpace(_txtCustomLayer.Text) ? "ZJ" : _txtCustomLayer.Text.Trim();
            string textStyleName = _cmbFonts == null || _cmbFonts.SelectedItem == null
                ? SurfaceAreaAnnotationOptions.Default.AnnotationFontName
                : _cmbFonts.SelectedItem.ToString();

            return new SurfaceAreaAnnotationOptions
            {
                BoundaryInterval = (double)_numInterval.Value,
                TextHeight = (double)_numTextHeight.Value,
                DecimalPlaces = (int)_numDecimals.Value,
                AnnotationTemplate = _txtTemplate.Text,
                CalculationMode = _cmbCalculationMode != null && _cmbCalculationMode.SelectedIndex == 1
                    ? SurfaceAreaCalculationMode.PlanArea
                    : SurfaceAreaCalculationMode.CassCommand,
                CassSurfaceLogPath = _txtCassLogPath == null ? string.Empty : _txtCassLogPath.Text.Trim(),
                DeleteCassGeneratedObjects = !(_chkDeleteCassObjects != null && _chkDeleteCassObjects.Checked),
                AnnotationFontName = textStyleName,
                LayerMode = mode,
                SelectedLayerName = selectedLayer,
                AnnotationLayerName = customLayer,
                UseBoundaryLayerForAnnotation = false,
                DrawLeader = true
            };
        }

        private void RunCalculation()
        {
            bool wasVisible = Visible;
            bool restoreForm = true;
            try
            {
                SurfaceAreaAnnotationOptions options = ReadOptions();
                SurfaceAreaAnnotationSettingsStore.Save(options);
                // CASS ????? AutoCAD ??????????????????????????
                restoreForm = false;

                if (wasVisible) Hide();

                SurfaceAreaAnnotationResult result = SurfaceAreaAnnotationService.SelectCalculateAndAnnotate(_doc, options);
                _doc.Editor.WriteHudMessage(result.ToEditorMessage());
                //WriteLog(result);

                // ???????????????????
                // ??????????????
                if (result.Success || result.AsyncStarted)
                {
                    restoreForm = false;
                    Close();
                }
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "???????", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
