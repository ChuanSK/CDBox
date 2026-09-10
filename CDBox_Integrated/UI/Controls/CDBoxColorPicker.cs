using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TCPipeAutoDraw.Core.Colors;

namespace TCPipeAutoDraw.UI.Controls
{
    /// <summary>
    /// CDBox 统一颜色入口。控件只公开 CDBoxColor，不向调用方暴露 CAD ColorIndex。
    /// </summary>
    internal sealed class CDBoxColorPicker : Button
    {
        private readonly Border _swatch;
        private readonly TextBlock _name;
        private readonly TextBlock _detail;
        private CDBoxColor _selectedColor;

        public event EventHandler SelectedColorChanged;

        public bool AllowByLayer { get; set; }
        public bool AllowByBlock { get; set; }
        public bool AllowTrueColor { get; set; }
        public bool AllowColorBook { get; set; }
        public bool AllowCDBoxStandard { get; set; }

        public CDBoxColor SelectedColor
        {
            get { return (_selectedColor ?? CDBoxColor.FromIndex(7)).Clone(); }
            set
            {
                _selectedColor = (value ?? CDBoxColor.FromIndex(7)).Clone();
                UpdateContent();
            }
        }

        public CDBoxColorPicker()
        {
            AllowByLayer = true;
            AllowByBlock = true;
            AllowTrueColor = true;
            AllowColorBook = true;
            AllowCDBoxStandard = true;
            MinHeight = 34;
            Padding = new Thickness(7, 3, 7, 3);
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            Background = Brush("#FAFCFF");
            BorderBrush = Brush("#C7D4E5");
            BorderThickness = new Thickness(1);
            Cursor = Cursors.Hand;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _swatch = new Border
            {
                Width = 19,
                Height = 19,
                CornerRadius = new CornerRadius(4),
                BorderBrush = Brush("#55000000"),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(_swatch);
            var text = new StackPanel { Margin = new Thickness(5, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center };
            _name = new TextBlock { FontSize = 11.5, Foreground = Brush("#31415A"), TextTrimming = TextTrimming.CharacterEllipsis };
            _detail = new TextBlock { FontSize = 9.5, Foreground = Brush("#718096"), TextTrimming = TextTrimming.CharacterEllipsis };
            text.Children.Add(_name);
            text.Children.Add(_detail);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            var arrow = new TextBlock { Text = "▾", Foreground = Brush("#66758B"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(arrow, 2);
            grid.Children.Add(arrow);
            Content = grid;
            _selectedColor = CDBoxColor.FromIndex(7);
            UpdateContent();
            Click += OpenPicker;
        }

        private void OpenPicker(object sender, RoutedEventArgs e)
        {
            Window owner = Window.GetWindow(this);
            CDBoxColor selected;
            if (!ColorPickerWindow.TryPick(_selectedColor, out selected, AllowByLayer,
                AllowByBlock, AllowTrueColor, AllowColorBook, AllowCDBoxStandard,
                owner)) return;
            _selectedColor = selected;
            UpdateContent();
            EventHandler handler = SelectedColorChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void UpdateContent()
        {
            CDBoxColor color = _selectedColor ?? CDBoxColor.FromIndex(7);
            _swatch.Background = Brush(color.Hex);
            _name.Text = color.ToString();
            _detail.Text = color.Type == CDBoxColorType.IndexColor
                ? color.RgbText
                : color.Type == CDBoxColorType.ByLayer || color.Type == CDBoxColorType.ByBlock
                    ? "使用 CAD 对象继承颜色"
                    : color.Hex + " · " + color.RgbText;
            ToolTip = _name.Text + "\n" + _detail.Text;
        }

        private static SolidColorBrush Brush(string hex)
        {
            if (TCPipeAutoDraw.UI.Studio.CDBoxAccentAppearance.IsAccentHex(hex)) return (SolidColorBrush)TCPipeAutoDraw.UI.Studio.CDBoxAccentAppearance.AccentBrush;
            if (TCPipeAutoDraw.UI.Studio.CDBoxAccentAppearance.IsAccentSoftHex(hex)) return (SolidColorBrush)TCPipeAutoDraw.UI.Studio.CDBoxAccentAppearance.SoftBrush;
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
