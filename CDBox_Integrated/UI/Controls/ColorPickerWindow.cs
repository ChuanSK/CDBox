using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TCPipeAutoDraw.Core.Colors;
using AcadCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcadColorDialog = Autodesk.AutoCAD.Windows.ColorDialog;

namespace TCPipeAutoDraw.UI.Controls
{
    internal sealed class ColorPickerWindow : Window
    {
        private readonly TabControl _tabs;
        private readonly Border _currentSwatch;
        private readonly TextBlock _currentName;
        private readonly TextBlock _currentDetail;
        private readonly Slider _hue;
        private readonly Slider _saturation;
        private readonly Slider _brightness;
        private readonly TextBox _red;
        private readonly TextBox _green;
        private readonly TextBox _blue;
        private readonly TextBox _hex;
        private readonly TextBox _bookName;
        private readonly TextBox _bookColorName;
        private readonly WrapPanel _aciCells;
        private readonly List<Button> _aciButtons = new List<Button>();
        private CDBoxColor _candidate;
        private bool _syncing;

        public CDBoxColor SelectedColor { get; private set; }

        public ColorPickerWindow(CDBoxColor initial, bool allowByLayer, bool allowByBlock,
            bool allowTrueColor, bool allowColorBook, bool allowStandard)
        {
            Title = "CDBox 颜色选择器";
            Width = 720;
            Height = 590;
            MinWidth = 620;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 12.5;
            Background = Brush("#F4F7FB");
            _candidate = (initial ?? CDBoxColor.FromIndex(7)).Clone();
            SelectedColor = _candidate.Clone();

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _currentSwatch = new Border
            {
                Width = 58,
                Height = 42,
                CornerRadius = new CornerRadius(8),
                BorderBrush = Brush("#45000000"),
                BorderThickness = new Thickness(1)
            };
            header.Children.Add(_currentSwatch);
            var currentText = new StackPanel { Margin = new Thickness(12, 1, 0, 0) };
            _currentName = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brush("#172033") };
            _currentDetail = new TextBlock { Margin = new Thickness(0, 4, 0, 0), Foreground = Brush("#65758A") };
            currentText.Children.Add(_currentName);
            currentText.Children.Add(_currentDetail);
            Grid.SetColumn(currentText, 1);
            header.Children.Add(currentText);
            root.Children.Add(header);

            _tabs = new TabControl
            {
                Background = Brush("#FFFFFFFF"),
                BorderBrush = Brush("#D9E4F2"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8)
            };
            Grid.SetRow(_tabs, 1);
            root.Children.Add(_tabs);

            _aciCells = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
            _tabs.Items.Add(new TabItem { Header = "索引颜色", Content = BuildAciTab(allowByLayer, allowByBlock) });

            Grid trueGrid = BuildTrueColorTab(out _hue, out _saturation, out _brightness,
                out _red, out _green, out _blue, out _hex);
            var trueTab = new TabItem { Header = "真彩色", Content = trueGrid, IsEnabled = allowTrueColor };
            _tabs.Items.Add(trueTab);

            Grid bookGrid = BuildColorBookTab(out _bookName, out _bookColorName);
            var bookTab = new TabItem { Header = "配色系统", Content = bookGrid, IsEnabled = allowColorBook };
            _tabs.Items.Add(bookTab);

            var standardTab = new TabItem { Header = "CDBox 标准", Content = BuildStandardTab(), IsEnabled = allowStandard };
            _tabs.Items.Add(standardTab);

            var footer = new Grid { Margin = new Thickness(0, 13, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var hint = new TextBlock
            {
                Text = "悬停颜色可查看 ACI、RGB 与 HEX；双击可直接确认。",
                Foreground = Brush("#718096"),
                VerticalAlignment = VerticalAlignment.Center
            };
            footer.Children.Add(hint);
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            var cancel = MakeButton("取消", false);
            cancel.Click += delegate { DialogResult = false; };
            var ok = MakeButton("确定", true);
            ok.Margin = new Thickness(8, 0, 0, 0);
            ok.Click += Confirm;
            actions.Children.Add(cancel);
            actions.Children.Add(ok);
            Grid.SetColumn(actions, 1);
            footer.Children.Add(actions);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            HookTrueColorEvents();
            SelectInitialTab(_candidate, trueTab, bookTab, standardTab);
            SyncControlsFromCandidate();
            PreviewKeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
            };
        }

        public static bool TryPick(CDBoxColor initial, out CDBoxColor selected,
            bool allowByLayer = true, bool allowByBlock = true, bool allowTrueColor = true,
            bool allowColorBook = true, bool allowStandard = true, Window owner = null)
        {
            var dialog = new ColorPickerWindow(initial, allowByLayer, allowByBlock,
                allowTrueColor, allowColorBook, allowStandard);
            if (owner != null) dialog.Owner = owner;
            bool accepted = owner != null ? dialog.ShowDialog() == true : AcadCoreApp.ShowModalWindow(dialog) == true;
            selected = accepted ? dialog.SelectedColor : null;
            return accepted;
        }

        private FrameworkElement BuildAciTab(bool allowByLayer, bool allowByBlock)
        {
            var root = new DockPanel { Margin = new Thickness(4) };
            var special = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 9) };
            if (allowByLayer) special.Children.Add(MakeSpecialColorButton("随层", CDBoxColor.ByLayer()));
            if (allowByBlock)
            {
                Button button = MakeSpecialColorButton("随块", CDBoxColor.ByBlock());
                button.Margin = new Thickness(7, 0, 0, 0);
                special.Children.Add(button);
            }
            DockPanel.SetDock(special, Dock.Top);
            root.Children.Add(special);
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            scroll.Content = _aciCells;
            root.Children.Add(scroll);
            foreach (CDBoxColor color in CDBoxColorService.GetAciPalette())
            {
                var button = new Button
                {
                    Width = 29,
                    Height = 29,
                    Margin = new Thickness(2),
                    Padding = new Thickness(0),
                    Background = Brush(color.Hex),
                    BorderBrush = Brush("#45000000"),
                    BorderThickness = new Thickness(1),
                    Tag = color,
                    ToolTip = "ACI Index: " + color.Index + "\nRGB: " + color.R + ", " + color.G + ", " + color.B + "\nHEX: " + color.Hex
                };
                button.Click += AciColorClick;
                button.MouseDoubleClick += delegate { SetCandidate((CDBoxColor)button.Tag); Confirm(button, new RoutedEventArgs()); };
                _aciButtons.Add(button);
                _aciCells.Children.Add(button);
            }
            return root;
        }

        private static Grid BuildTrueColorTab(out Slider hue, out Slider saturation, out Slider brightness,
            out TextBox red, out TextBox green, out TextBox blue, out TextBox hex)
        {
            var grid = new Grid { Margin = new Thickness(8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            var sliders = new StackPanel { Margin = new Thickness(0, 4, 20, 0) };
            hue = MakeSlider(0, 359, 1);
            hue.Background = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(Colors.Red, 0), new GradientStop(Colors.Yellow, .17),
                new GradientStop(Colors.Lime, .33), new GradientStop(Colors.Cyan, .5),
                new GradientStop(Colors.Blue, .67), new GradientStop(Colors.Magenta, .83),
                new GradientStop(Colors.Red, 1)
            }, 0);
            sliders.Children.Add(MakeCaption("色相 H"));
            sliders.Children.Add(hue);
            saturation = MakeSlider(0, 100, 1);
            sliders.Children.Add(MakeCaption("饱和度 S"));
            sliders.Children.Add(saturation);
            brightness = MakeSlider(0, 100, 1);
            sliders.Children.Add(MakeCaption("明度 V"));
            sliders.Children.Add(brightness);
            sliders.Children.Add(new TextBlock
            {
                Text = "拖动 H / S / V 后，RGB、HEX 和颜色预览会同步更新。",
                Margin = new Thickness(0, 18, 0, 0),
                Foreground = Brush("#718096"),
                TextWrapping = TextWrapping.Wrap
            });
            grid.Children.Add(sliders);

            var inputs = new Grid { Margin = new Thickness(6, 3, 0, 0) };
            inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 5; i++) inputs.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            red = AddInput(inputs, 0, "R", "255");
            green = AddInput(inputs, 1, "G", "255");
            blue = AddInput(inputs, 2, "B", "255");
            hex = AddInput(inputs, 3, "HEX", "#FFFFFF");
            Grid.SetColumnSpan(inputs.Children[inputs.Children.Count - 2], 1);
            var note = new TextBlock { Text = "支持 #RRGGBB 或 #RGB", Foreground = Brush("#718096"), Margin = new Thickness(0, 5, 0, 0) };
            Grid.SetRow(note, 4);
            Grid.SetColumn(note, 1);
            inputs.Children.Add(note);
            Grid.SetColumn(inputs, 1);
            grid.Children.Add(inputs);
            return grid;
        }

        private Grid BuildColorBookTab(out TextBox bookName, out TextBox colorName)
        {
            var grid = new Grid { Margin = new Thickness(16) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 5; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            bookName = AddInput(grid, 0, "配色系统", string.Empty);
            colorName = AddInput(grid, 1, "颜色名称", string.Empty);
            var browse = MakeButton("从 AutoCAD 配色系统选择…", false);
            browse.HorizontalAlignment = HorizontalAlignment.Left;
            browse.Margin = new Thickness(0, 10, 0, 2);
            browse.Click += PickFromCadColorBook;
            Grid.SetRow(browse, 2);
            Grid.SetColumn(browse, 1);
            grid.Children.Add(browse);
            var info = new TextBlock
            {
                Text = "优先通过上方按钮浏览本机 AutoCAD 已安装的配色系统；也可手动输入系统名和颜色名，例如 RAL / 2004。写入时保留 Color Book 名称。",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush("#65758A"),
                LineHeight = 22,
                Margin = new Thickness(0, 15, 0, 0)
            };
            Grid.SetRow(info, 3);
            Grid.SetColumn(info, 0);
            Grid.SetColumnSpan(info, 2);
            grid.Children.Add(info);
            var fallback = new TextBlock
            {
                Text = "若目标电脑无法解析该色册，CDBox 会使用当前 RGB 作为显示回退色。回退色可在“真彩色”页调整。",
                Foreground = Brush("#3D63A7"),
                Margin = new Thickness(0, 12, 0, 0)
            };
            Grid.SetRow(fallback, 4);
            Grid.SetColumnSpan(fallback, 2);
            grid.Children.Add(fallback);
            return grid;
        }

        private void PickFromCadColorBook(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new AcadColorDialog { IncludeByBlockByLayer = false };
                dialog.Color = CDBoxColorService.ToCadColor(_candidate);
                dialog.SetDialogTabs(AcadColorDialog.ColorTabs.ColorBookTab);
                if (dialog.ShowModal() != true) return;
                CDBoxColor selected = CDBoxColorService.FromCadColor(dialog.Color);
                if (selected.Type != CDBoxColorType.ColorBook)
                {
                    MessageBox.Show(this, "请选择配色系统中的颜色。", "CDBox 颜色选择器",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                SetCandidate(selected);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法读取 AutoCAD 配色系统：" + ex.Message,
                    "CDBox 颜色选择器", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private FrameworkElement BuildStandardTab()
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(7) };
            var list = new StackPanel();
            scroll.Content = list;
            foreach (CDBoxColor color in CDBoxStandardColorService.GetColors())
            {
                var button = new Button
                {
                    Margin = new Thickness(0, 0, 0, 7),
                    Padding = new Thickness(10, 8, 10, 8),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = Brushes.White,
                    BorderBrush = Brush("#D9E4F2"),
                    Tag = color
                };
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(5), Background = Brush(color.Hex), BorderBrush = Brush("#44000000"), BorderThickness = new Thickness(1) });
                var label = new TextBlock { Text = color.DisplayName, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(label, 1);
                row.Children.Add(label);
                var category = new TextBlock { Text = color.Category, Foreground = Brush("#718096"), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(category, 2);
                row.Children.Add(category);
                button.Content = row;
                button.Click += delegate { SetCandidate(((CDBoxColor)button.Tag).Clone()); };
                button.MouseDoubleClick += delegate { SetCandidate(((CDBoxColor)button.Tag).Clone()); Confirm(button, new RoutedEventArgs()); };
                list.Children.Add(button);
            }
            return scroll;
        }

        private void HookTrueColorEvents()
        {
            _hue.ValueChanged += HsvChanged;
            _saturation.ValueChanged += HsvChanged;
            _brightness.ValueChanged += HsvChanged;
            HookInput(_red, ApplyRgbInputs);
            HookInput(_green, ApplyRgbInputs);
            HookInput(_blue, ApplyRgbInputs);
            HookInput(_hex, ApplyHexInput);
        }

        private static void HookInput(TextBox box, Action action)
        {
            box.LostKeyboardFocus += delegate { action(); };
            box.PreviewKeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key != Key.Enter) return;
                action();
                e.Handled = true;
            };
        }

        private void AciColorClick(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SetCandidate(button == null ? null : button.Tag as CDBoxColor);
        }

        private Button MakeSpecialColorButton(string label, CDBoxColor color)
        {
            var button = MakeButton(label, false);
            button.Tag = color;
            button.Click += delegate { SetCandidate((CDBoxColor)button.Tag); };
            return button;
        }

        private void HsvChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncing) return;
            byte r;
            byte g;
            byte b;
            CDBoxColorConverter.HsvToRgb(_hue.Value, _saturation.Value / 100.0, _brightness.Value / 100.0, out r, out g, out b);
            SetCandidate(CDBoxColor.FromRgb(r, g, b));
        }

        private void ApplyRgbInputs()
        {
            int r;
            int g;
            int b;
            if (!int.TryParse(_red.Text, out r) || !int.TryParse(_green.Text, out g) || !int.TryParse(_blue.Text, out b)) return;
            SetCandidate(CDBoxColor.FromRgb((byte)Math.Max(0, Math.Min(255, r)),
                (byte)Math.Max(0, Math.Min(255, g)), (byte)Math.Max(0, Math.Min(255, b))));
        }

        private void ApplyHexInput()
        {
            byte r;
            byte g;
            byte b;
            if (!CDBoxColorConverter.TryParseHex(_hex.Text, out r, out g, out b)) return;
            SetCandidate(CDBoxColor.FromRgb(r, g, b));
        }

        private void SetCandidate(CDBoxColor color)
        {
            if (color == null) return;
            _candidate = color.Clone();
            SyncControlsFromCandidate();
        }

        private void SyncControlsFromCandidate()
        {
            if (_candidate == null) return;
            _syncing = true;
            try
            {
                _currentSwatch.Background = Brush(_candidate.Hex);
                _currentName.Text = _candidate.ToString();
                _currentDetail.Text = _candidate.Hex + " · " + _candidate.RgbText;
                double h;
                double s;
                double v;
                CDBoxColorConverter.RgbToHsv(_candidate.R, _candidate.G, _candidate.B, out h, out s, out v);
                _hue.Value = h;
                _saturation.Value = s * 100.0;
                _brightness.Value = v * 100.0;
                _red.Text = _candidate.R.ToString(CultureInfo.InvariantCulture);
                _green.Text = _candidate.G.ToString(CultureInfo.InvariantCulture);
                _blue.Text = _candidate.B.ToString(CultureInfo.InvariantCulture);
                _hex.Text = _candidate.Hex;
                if (_candidate.Type == CDBoxColorType.ColorBook)
                {
                    _bookName.Text = _candidate.BookName ?? string.Empty;
                    _bookColorName.Text = _candidate.ColorName ?? string.Empty;
                }
                foreach (Button button in _aciButtons)
                {
                    CDBoxColor value = button.Tag as CDBoxColor;
                    bool selected = value != null && _candidate.Type == CDBoxColorType.IndexColor && value.Index == _candidate.Index;
                    button.BorderBrush = selected ? Brush("#FF2563EB") : Brush("#45000000");
                    button.BorderThickness = selected ? new Thickness(3) : new Thickness(1);
                }
            }
            finally { _syncing = false; }
        }

        private void Confirm(object sender, RoutedEventArgs e)
        {
            if (_tabs.SelectedIndex == 1)
                ApplyRgbInputs();
            else if (_tabs.SelectedIndex == 2)
            {
                string book = (_bookName.Text ?? string.Empty).Trim();
                string name = (_bookColorName.Text ?? string.Empty).Trim();
                if (book.Length == 0 || name.Length == 0)
                {
                    MessageBox.Show(this, "请同时填写配色系统名称和颜色名称。", "CDBox 颜色选择器", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                _candidate.Type = CDBoxColorType.ColorBook;
                _candidate.BookName = book;
                _candidate.ColorName = name;
                _candidate.DisplayName = book + " · " + name;
            }
            SelectedColor = (_candidate ?? CDBoxColor.FromIndex(7)).Clone();
            DialogResult = true;
        }

        private static void SelectInitialTab(CDBoxColor color, TabItem trueTab, TabItem bookTab, TabItem standardTab)
        {
            if (color == null) return;
            if ((color.Type == CDBoxColorType.TrueColor) && trueTab.IsEnabled) trueTab.IsSelected = true;
            else if (color.Type == CDBoxColorType.ColorBook && bookTab.IsEnabled) bookTab.IsSelected = true;
            else if (color.Type == CDBoxColorType.CDBoxStandard && standardTab.IsEnabled) standardTab.IsSelected = true;
        }

        private static Slider MakeSlider(double min, double max, double tick)
        {
            return new Slider
            {
                Minimum = min,
                Maximum = max,
                TickFrequency = tick,
                IsSnapToTickEnabled = false,
                Height = 34,
                Margin = new Thickness(0, 1, 0, 12)
            };
        }

        private static TextBlock MakeCaption(string text)
        {
            return new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Foreground = Brush("#4B5D75") };
        }

        private static TextBox AddInput(Grid grid, int row, string label, string value)
        {
            var caption = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("#607089"),
                Margin = new Thickness(0, 5, 8, 5)
            };
            Grid.SetRow(caption, row);
            grid.Children.Add(caption);
            var box = new TextBox
            {
                Text = value,
                MinHeight = 34,
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 4, 0, 4),
                BorderBrush = Brush("#C7D4E5"),
                Background = Brush("#FAFCFF"),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(box, row);
            Grid.SetColumn(box, 1);
            grid.Children.Add(box);
            return box;
        }

        private static Button MakeButton(string text, bool primary)
        {
            return new Button
            {
                Content = text,
                MinWidth = 82,
                MinHeight = 36,
                Padding = new Thickness(13, 5, 13, 5),
                Background = primary ? Brush("#326FEA") : Brushes.White,
                Foreground = primary ? Brushes.White : Brush("#26364D"),
                BorderBrush = primary ? Brush("#326FEA") : Brush("#C7D4E5"),
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
        }

        private static SolidColorBrush Brush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
