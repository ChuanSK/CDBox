using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.AutoCAD.DatabaseServices;
using TCPipeAutoDraw.Core.Colors;
using TCPipeAutoDraw.UI.Controls;

namespace TCPipeAutoDraw.Modules.AnnotationHud
{
    internal sealed class SimpleAnnotationHudWindow : AnnotationHudWindowBase
    {
        private readonly TextBlock _header;
        private readonly TextBlock _summary;
        private readonly StackPanel _textRows;
        private readonly Button _moreToggle;
        private readonly StackPanel _morePanel;
        private readonly TextBox _textHeight;
        private readonly ComboBox _textStyle;
        private readonly ComboBox _layer;
        private readonly CDBoxColorPicker _primaryColor;
        private readonly CDBoxColorPicker _secondaryColor;
        private readonly TextBlock _secondaryColorLabel;
        private readonly ComboBox _linetype;
        private readonly ComboBox _lineWeight;
        private readonly TextBlock _linetypeLabel;
        private readonly TextBlock _lineWeightLabel;
        private readonly TextBlock _status;
        private readonly List<TextBox> _lineEditors = new List<TextBox>();
        private SimpleAnnotationHudModel _model;
        private bool _binding;
        private bool _committing;
        private Control _focusedEditor;
        private string _focusedValue;

        public Func<SimpleAnnotationHudModel, SimpleAnnotationHudModel> CommitHandler { get; set; }
        public string AnnotationId { get { return _model == null ? string.Empty : _model.AnnotationId; } }

        public SimpleAnnotationHudWindow() : base("CDBox 标注浮窗", 330)
        {
            _header = new TextBlock
            {
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#253550"),
                FontSize = 13,
                Cursor = Cursors.SizeAll,
                ToolTip = "拖动浮窗"
            };
            _summary = new TextBlock
            {
                Foreground = Brush("#65758A"),
                FontSize = 10.8,
                Margin = new Thickness(0, 2, 0, 7),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Cursor = Cursors.SizeAll,
                ToolTip = "拖动浮窗"
            };
            EnableDrag(_header);
            EnableDrag(_summary);
            Body.Children.Add(_header);
            Body.Children.Add(_summary);

            _textRows = new StackPanel();
            Body.Children.Add(_textRows);

            var actions = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            _moreToggle = CreateLinkButton("更多编辑  ▾");
            _moreToggle.HorizontalAlignment = HorizontalAlignment.Left;
            _moreToggle.Click += ToggleMore;
            actions.Children.Add(_moreToggle);
            Body.Children.Add(actions);

            _morePanel = new StackPanel { Visibility = System.Windows.Visibility.Collapsed, Margin = new Thickness(0, 6, 0, 0) };
            _morePanel.Children.Add(new Border
            {
                Height = 1,
                Background = Brush("#DCE5F1"),
                Margin = new Thickness(0, 0, 0, 6)
            });
            Body.Children.Add(_morePanel);

            var options = new Grid();
            options.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            options.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 7; i++) options.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _morePanel.Children.Add(options);
            _textStyle = AddComboRow(options, 0, "文字样式", out _);
            _textHeight = AddTextBoxRow(options, 1, "文字高度");
            _primaryColor = AddColorPickerRow(options, 2, "主文字颜色", out _);
            _secondaryColor = AddColorPickerRow(options, 3, "其他文字", out _secondaryColorLabel);
            _layer = AddComboRow(options, 4, "标注图层", out _);
            _linetype = AddComboRow(options, 5, "引线线型", out _linetypeLabel);
            _lineWeight = AddComboRow(options, 6, "引线线宽", out _lineWeightLabel);
            _lineWeight.DisplayMemberPath = "Name";
            _lineWeight.ItemsSource = BuildLineWeightOptions();

            _status = new TextBlock
            {
                Visibility = System.Windows.Visibility.Collapsed,
                Margin = new Thickness(1, 5, 1, 0),
                Foreground = Brush("#B43A3A"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap
            };
            Body.Children.Add(_status);

            HookTextEditor(_textHeight);
            HookImmediateCombo(_textStyle);
            HookImmediateCombo(_layer);
            HookImmediateCombo(_linetype);
            HookImmediateCombo(_lineWeight);
            _primaryColor.SelectedColorChanged += delegate { CommitCurrentModel(); };
            _secondaryColor.SelectedColorChanged += delegate { CommitCurrentModel(); };
        }

        public void SetModel(SimpleAnnotationHudModel model)
        {
            _binding = true;
            try
            {
                _model = model;
                if (model == null) return;
                _header.Text = model.Header ?? "标注编辑";
                _summary.Text = model.Summary ?? string.Empty;
                RebuildTextRows(model);
                _textHeight.Text = model.TextHeight.ToString("0.###", CultureInfo.InvariantCulture);
                SetStringItems(_textStyle, model.TextStyleNames, model.TextStyleName);
                SetStringItems(_layer, model.LayerNames, model.LayerName);
                SetStringItems(_linetype, model.LinetypeNames, model.LinetypeName);
                _primaryColor.SelectedColor = model.PrimaryColor;
                _secondaryColor.SelectedColor = model.SecondaryColor;
                SelectLineWeight(model.LineWeight);
                bool hasLeader = model.Kind == SimpleAnnotationKind.SurfaceArea && !model.LeaderObjectId.IsNull;
                _secondaryColorLabel.Text = model.Kind == SimpleAnnotationKind.Node ? "其他文字" : "引线颜色";
                _secondaryColor.Visibility = model.Kind == SimpleAnnotationKind.Node || hasLeader
                    ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                _secondaryColorLabel.Visibility = _secondaryColor.Visibility;
                _linetype.Visibility = hasLeader ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                _linetypeLabel.Visibility = _linetype.Visibility;
                _lineWeight.Visibility = hasLeader ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                _lineWeightLabel.Visibility = _lineWeight.Visibility;
                SetStatus(string.Empty, false);
            }
            finally { _binding = false; }
        }

        private void RebuildTextRows(SimpleAnnotationHudModel model)
        {
            _textRows.Children.Clear();
            _lineEditors.Clear();
            for (int i = 0; i < model.Lines.Count; i++)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var label = new TextBlock
                {
                    Text = model.Lines[i].Label,
                    Foreground = Brush("#607089"),
                    FontSize = 11.2,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 7, 0)
                };
                row.Children.Add(label);
                TextBox editor = CreateTextBox();
                editor.Text = model.Lines[i].Text ?? string.Empty;
                editor.Tag = i;
                HookTextEditor(editor);
                Grid.SetColumn(editor, 1);
                row.Children.Add(editor);
                _lineEditors.Add(editor);
                _textRows.Children.Add(row);
            }
        }

        private void ToggleMore(object sender, RoutedEventArgs e)
        {
            bool expanded = _morePanel.Visibility != System.Windows.Visibility.Visible;
            _morePanel.Visibility = expanded ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            _moreToggle.Content = expanded ? "收起编辑  ▴" : "更多编辑  ▾";
        }

        private void HookTextEditor(TextBox editor)
        {
            editor.GotKeyboardFocus += delegate(object sender, KeyboardFocusChangedEventArgs e)
            {
                _focusedEditor = sender as Control;
                TextBox box = _focusedEditor as TextBox;
                _focusedValue = box == null ? string.Empty : box.Text ?? string.Empty;
            };
            editor.LostKeyboardFocus += delegate(object sender, KeyboardFocusChangedEventArgs e)
            {
                if (_binding || _committing) return;
                TextBox box = sender as TextBox;
                if (box != null && !string.Equals(_focusedValue, box.Text ?? string.Empty, StringComparison.Ordinal))
                    CommitCurrentModel();
                _focusedEditor = null;
                _focusedValue = null;
            };
            editor.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                TextBox box = sender as TextBox;
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    CommitCurrentModel();
                    Keyboard.ClearFocus();
                }
                else if (e.Key == Key.Escape && box != null && _focusedValue != null
                    && !string.Equals(box.Text ?? string.Empty, _focusedValue, StringComparison.Ordinal))
                {
                    box.Text = _focusedValue;
                    e.Handled = true;
                }
            };
        }

        private void HookImmediateCombo(ComboBox combo)
        {
            combo.SelectionChanged += delegate { CommitCurrentModel(); };
        }

        private void CommitCurrentModel()
        {
            if (_binding || _committing || !IsLoaded || _model == null || CommitHandler == null) return;
            try
            {
                _committing = true;
                SimpleAnnotationHudModel saved = CommitHandler(ReadModel());
                if (saved != null) SetModel(saved);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
            finally { _committing = false; }
        }

        private SimpleAnnotationHudModel ReadModel()
        {
            double height;
            string raw = (_textHeight.Text ?? string.Empty).Trim().Replace(',', '.');
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out height) || height <= 0.0)
                throw new InvalidOperationException("请输入有效的文字高度。");
            for (int i = 0; i < _model.Lines.Count && i < _lineEditors.Count; i++)
                _model.Lines[i].Text = _lineEditors[i].Text ?? string.Empty;
            _model.TextHeight = height;
            _model.TextStyleName = Convert.ToString(_textStyle.SelectedItem, CultureInfo.CurrentCulture) ?? string.Empty;
            _model.LayerName = Convert.ToString(_layer.SelectedItem, CultureInfo.CurrentCulture) ?? string.Empty;
            _model.PrimaryColor = _primaryColor.SelectedColor;
            _model.SecondaryColor = _secondaryColor.SelectedColor;
            _model.LinetypeName = Convert.ToString(_linetype.SelectedItem, CultureInfo.CurrentCulture) ?? "ByLayer";
            LineWeightOption weight = _lineWeight.SelectedItem as LineWeightOption;
            if (weight != null) _model.LineWeight = weight.Value;
            return _model;
        }

        private void SetStatus(string message, bool isError)
        {
            _status.Text = message ?? string.Empty;
            _status.Foreground = isError ? Brush("#B43A3A") : Brush("#3B6B4A");
            _status.Visibility = string.IsNullOrWhiteSpace(message) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        private static TextBox CreateTextBox()
        {
            return new TextBox
            {
                MinHeight = 28,
                Padding = new Thickness(7, 3, 7, 3),
                BorderBrush = Brush("#C7D4E5"),
                BorderThickness = new Thickness(1),
                Background = Brush("#FAFCFF"),
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private static TextBox AddTextBoxRow(Grid grid, int row, string label)
        {
            TextBlock ignored;
            AddLabel(grid, row, label, out ignored);
            TextBox box = CreateTextBox();
            box.Margin = new Thickness(0, 2, 0, 2);
            Grid.SetRow(box, row);
            Grid.SetColumn(box, 1);
            grid.Children.Add(box);
            return box;
        }

        private static ComboBox AddComboRow(Grid grid, int row, string label, out TextBlock labelBlock)
        {
            AddLabel(grid, row, label, out labelBlock);
            var combo = new ComboBox
            {
                MinHeight = 28,
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 2, 0, 2),
                BorderBrush = Brush("#C7D4E5"),
                Background = Brush("#FAFCFF")
            };
            Grid.SetRow(combo, row);
            Grid.SetColumn(combo, 1);
            grid.Children.Add(combo);
            return combo;
        }

        private static CDBoxColorPicker AddColorPickerRow(Grid grid, int row, string label,
            out TextBlock labelBlock)
        {
            AddLabel(grid, row, label, out labelBlock);
            var picker = new CDBoxColorPicker
            {
                Margin = new Thickness(0, 2, 0, 2),
                AllowByLayer = true,
                AllowByBlock = true,
                AllowTrueColor = true,
                AllowColorBook = true,
                AllowCDBoxStandard = true
            };
            Grid.SetRow(picker, row);
            Grid.SetColumn(picker, 1);
            grid.Children.Add(picker);
            return picker;
        }

        private static void AddLabel(Grid grid, int row, string text, out TextBlock label)
        {
            label = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("#607089"),
                Margin = new Thickness(0, 2, 7, 2),
                FontSize = 11.5
            };
            Grid.SetRow(label, row);
            grid.Children.Add(label);
        }

        private static Button CreateLinkButton(string text)
        {
            return new Button
            {
                Content = text,
                MinHeight = 24,
                Padding = new Thickness(2, 1, 2, 1),
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = Brush("#3D63A7"),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
        }

        private static void SetStringItems(ComboBox combo, IList<string> items, string selected)
        {
            combo.ItemsSource = items ?? new List<string>();
            combo.SelectedItem = selected;
            if (combo.SelectedIndex < 0 && combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private void SelectLineWeight(LineWeight value)
        {
            foreach (object item in _lineWeight.Items)
            {
                LineWeightOption option = item as LineWeightOption;
                if (option != null && option.Value == value)
                {
                    _lineWeight.SelectedItem = option;
                    return;
                }
            }
            _lineWeight.SelectedIndex = 0;
        }

        private static IList<LineWeightOption> BuildLineWeightOptions()
        {
            return new[]
            {
                new LineWeightOption("随层", LineWeight.ByLayer),
                new LineWeightOption("随块", LineWeight.ByBlock),
                new LineWeightOption("默认", LineWeight.ByLineWeightDefault),
                new LineWeightOption("0.13 mm", LineWeight.LineWeight013),
                new LineWeightOption("0.18 mm", LineWeight.LineWeight018),
                new LineWeightOption("0.25 mm", LineWeight.LineWeight025),
                new LineWeightOption("0.35 mm", LineWeight.LineWeight035),
                new LineWeightOption("0.50 mm", LineWeight.LineWeight050),
                new LineWeightOption("0.70 mm", LineWeight.LineWeight070),
                new LineWeightOption("1.00 mm", LineWeight.LineWeight100)
            };
        }

        private sealed class LineWeightOption
        {
            public string Name { get; private set; }
            public LineWeight Value { get; private set; }
            public LineWeightOption(string name, LineWeight value) { Name = name; Value = value; }
        }
    }
}
