using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TCPipeAutoDraw.Modules.AnnotationHud;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioFrameChoiceItem
    {
        public string Value { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public string Badge { get; set; }
    }

    /// <summary>
    /// 图框流程使用的通用选择浮窗。直接复用标注浮窗的 WPF 外壳，
    /// 从而共享透明度、悬停、光圈、边缘吸附和开关动画设置。
    /// </summary>
    internal sealed class CDBoxStudioFrameChoiceWindow :
        AnnotationHudWindowBase
    {
        private readonly IList<CDBoxStudioFrameChoiceItem> _items;
        private readonly List<Border> _rows = new List<Border>();
        private int _selectedIndex;
        private bool _mouseConfirmEnabled;
        private DateTime _openedAtUtc;
        private DispatcherTimer _mouseReleaseTimer;

        private CDBoxStudioFrameChoiceWindow(string title,
            IList<CDBoxStudioFrameChoiceItem> items, string defaultValue)
            : base(string.IsNullOrWhiteSpace(title) ? "请选择" : title.Trim(),
                380)
        {
            _items = (items ?? new List<CDBoxStudioFrameChoiceItem>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Value))
                .ToList();
            _selectedIndex = Math.Max(0, _items.ToList().FindIndex(x =>
                string.Equals(x.Value, defaultValue,
                    StringComparison.OrdinalIgnoreCase)));
            MaxHeight = 560;

            var titleBlock = new TextBlock
            {
                Text = Title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#253550"),
                Margin = new Thickness(0, 0, 0, 9),
                Cursor = Cursors.SizeAll
            };
            EnableDrag(titleBlock);
            Body.Children.Add(titleBlock);

            var stack = new StackPanel();
            Body.Children.Add(new ScrollViewer
            {
                Content = stack,
                MaxHeight = 410,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            });

            for (int i = 0; i < _items.Count; i++)
            {
                int index = i;
                CDBoxStudioFrameChoiceItem item = _items[i];
                var row = new Border
                {
                    CornerRadius = new CornerRadius(7),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brush("#D8E2EF"),
                    Background = Brushes.White,
                    Padding = new Thickness(9, 8, 9, 8),
                    Margin = new Thickness(0, 0, 0, 6),
                    Cursor = Cursors.Hand
                };
                var content = new Grid();
                content.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = new GridLength(32) });
                content.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = new GridLength(1, GridUnitType.Star) });
                content.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = GridLength.Auto });
                content.Children.Add(new TextBlock
                {
                    Text = (i + 1).ToString(),
                    Foreground = Brush("#3D63A7"),
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                });
                var itemTitle = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(item.Title)
                        ? item.Value : item.Title,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush("#253550"),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(itemTitle, 1);
                content.Children.Add(itemTitle);
                if (!string.IsNullOrWhiteSpace(item.Badge))
                {
                    var badge = new Border
                    {
                        Background = Brush("#EDF3FC"),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(6, 3, 6, 3),
                        Margin = new Thickness(8, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = item.Badge,
                            Foreground = Brush("#5E718A"),
                            FontSize = 10
                        }
                    };
                    Grid.SetColumn(badge, 2);
                    content.Children.Add(badge);
                }
                row.Child = content;
                row.MouseEnter += delegate { SelectIndex(index); };
                row.MouseLeftButtonDown += delegate(
                    object sender, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    SelectIndex(index);
                    if (!_mouseConfirmEnabled
                        || (DateTime.UtcNow - _openedAtUtc)
                            .TotalMilliseconds < 160.0) return;
                    ConfirmSelection();
                };
                _rows.Add(row);
                stack.Children.Add(row);
            }

            PreviewKeyDown += KeyDownHandler;
            PreviewMouseWheel += MouseWheelHandler;
            Loaded += WindowLoaded;
            UserMoved += delegate { SavePosition(this); };
            Closed += delegate
            {
                SavePosition(this);
                if (_mouseReleaseTimer != null) _mouseReleaseTimer.Stop();
                _mouseReleaseTimer = null;
            };
        }

        public string SelectedValue { get; private set; }

        public static bool TryChoose(string title, string subtitle,
            IList<CDBoxStudioFrameChoiceItem> items, string defaultValue,
            out string selectedValue,
            System.Windows.Forms.IWin32Window owner = null)
        {
            selectedValue = string.Empty;
            IList<CDBoxStudioFrameChoiceItem> normalized =
                (items ?? new List<CDBoxStudioFrameChoiceItem>())
                    .Where(x => x != null
                        && !string.IsNullOrWhiteSpace(x.Value)).ToList();
            if (normalized.Count == 0) return false;

            var window = new CDBoxStudioFrameChoiceWindow(title, normalized,
                defaultValue);
            CDBoxStudioSettings settings = CDBoxStudioSettingsStore.Load();
            window.ApplyAppearance(settings.AnnotationHudNormalOpacity,
                settings.AnnotationHudHoverOpacity,
                settings.AnnotationHudGlowEnabled,
                settings.AnnotationHudGlowIntensity);
            try
            {
                IntPtr handle = owner == null ? IntPtr.Zero : owner.Handle;
                if (handle == IntPtr.Zero && AcadApp.MainWindow != null)
                    handle = AcadApp.MainWindow.Handle;
                if (handle != IntPtr.Zero)
                    new WindowInteropHelper(window).Owner = handle;
                bool accepted = AcadApp.ShowModalWindow(window) == true;
                selectedValue = accepted
                    ? window.SelectedValue ?? string.Empty : string.Empty;
                return accepted && !string.IsNullOrWhiteSpace(selectedValue);
            }
            finally
            {
                if (window.IsVisible) window.CloseImmediately();
            }
        }

        protected override void OnEscapePressed()
        {
            CompleteDialogAnimated(false);
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            _openedAtUtc = DateTime.UtcNow;
            _mouseConfirmEnabled =
                Mouse.LeftButton == MouseButtonState.Released;
            if (!_mouseConfirmEnabled)
            {
                _mouseReleaseTimer = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(40), DispatcherPriority.Input,
                    delegate
                    {
                        if (Mouse.LeftButton != MouseButtonState.Released)
                            return;
                        _mouseConfirmEnabled = true;
                        if (_mouseReleaseTimer != null)
                            _mouseReleaseTimer.Stop();
                        _mouseReleaseTimer = null;
                    }, Dispatcher);
                _mouseReleaseTimer.Start();
            }
            SelectIndex(_selectedIndex, true);
            double left;
            double top;
            bool remembered =
                CDBoxStudioFrameChoicePositionStore.TryLoad(out left, out top);
            ShowAnimated(GetCursorScreenPosition(),
                remembered ? (double?)left : null,
                remembered ? (double?)top : null);
            Activate();
            Focus();
        }

        private static void SavePosition(
            CDBoxStudioFrameChoiceWindow window)
        {
            if (window == null || !window.HasInitializedPosition) return;
            double left;
            double top;
            if (window.TryGetRestingPosition(out left, out top))
                CDBoxStudioFrameChoicePositionStore.Save(left, top);
        }

        private void KeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab || e.Key == Key.Down || e.Key == Key.Up)
            {
                int delta = e.Key == Key.Up || (e.Key == Key.Tab
                    && (Keyboard.Modifiers & ModifierKeys.Shift)
                    == ModifierKeys.Shift) ? -1 : 1;
                SelectIndex(_selectedIndex + delta);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                ConfirmSelection();
                e.Handled = true;
            }
        }

        private void MouseWheelHandler(object sender, MouseWheelEventArgs e)
        {
            SelectIndex(_selectedIndex + (e.Delta > 0 ? -1 : 1));
            e.Handled = true;
        }

        private void SelectIndex(int index, bool force = false)
        {
            if (_items.Count == 0) return;
            index %= _items.Count;
            if (index < 0) index += _items.Count;
            if (!force && index == _selectedIndex) return;
            _selectedIndex = index;
            for (int i = 0; i < _rows.Count; i++)
            {
                bool selected = i == _selectedIndex;
                _rows[i].Background = selected
                    ? Brush("#EAF2FF") : Brushes.White;
                _rows[i].BorderBrush = selected
                    ? Brush("#5D96EA") : Brush("#D8E2EF");
                _rows[i].BorderThickness = selected
                    ? new Thickness(2) : new Thickness(1);
            }
        }

        private void ConfirmSelection()
        {
            if (_items.Count == 0) return;
            SelectedValue = _items[_selectedIndex].Value;
            CompleteDialogAnimated(true);
        }
    }
}
