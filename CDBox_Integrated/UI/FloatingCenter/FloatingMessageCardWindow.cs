using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using TCPipeAutoDraw.Core.FloatingCenter;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace TCPipeAutoDraw.UI.FloatingCenter
{
    internal sealed class FloatingMessageCardWindow : Window
    {
        private readonly Border _root;
        private readonly Border _shadowSurface;
        private readonly Border _glyphHost;
        private readonly TextBlock _glyph;
        private readonly TextBlock _title;
        private readonly TextBlock _message;
        private readonly TextBlock _repeat;
        private readonly ProgressBar _progress;
        private readonly Button _close;
        private readonly DispatcherTimer _countdown;
        private FloatingMessage _current;
        private DateTime _lastTick;
        private double _remainingSeconds;
        private bool _autoClose;
        private bool _hovered;
        private bool _closing;
        private CDBoxStudioSettings _settings;

        public event EventHandler CardClosed;
        public event EventHandler DetailsRequested;

        public string MessageId
        {
            get { return _current == null ? string.Empty : _current.Id ?? string.Empty; }
        }

        public string DocumentId
        {
            get { return _current == null ? string.Empty : _current.DocumentId ?? string.Empty; }
        }

        public FloatingMessageKind MessageKind
        {
            get { return _current == null ? FloatingMessageKind.Information : _current.Kind; }
        }

        public bool MessageIsPersistent
        {
            get { return _current != null && _current.IsPersistent; }
        }

        public FloatingMessageCardWindow()
        {
            Title = "CDBox 提示";
            Width = 330;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 220;
            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = false;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 12;

            _root = new Border
            {
                Margin = new Thickness(12),
                Padding = new Thickness(12, 11, 10, 11),
                Background = Brush("#FBFFFFFF"),
                BorderBrush = Brush("#CAD7E5F4"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                Cursor = Cursors.Hand,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1.0, 1.0)
            };
            _shadowSurface = new Border
            {
                Margin = new Thickness(12),
                BorderBrush = Brush("#7A428BFF"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                IsHitTestVisible = false,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.24,
                    Color = Color.FromRgb(60, 118, 210)
                }
            };
            var visualHost = new Grid();
            visualHost.Children.Add(_shadowSurface);
            visualHost.Children.Add(_root);
            Content = visualHost;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextHintingMode(this, TextHintingMode.Fixed);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _root.Child = grid;

            _glyph = new TextBlock
            {
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _glyphHost = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brush("#3B82F6"),
                Child = _glyph,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 0, 0)
            };
            grid.Children.Add(_glyphHost);

            var content = new StackPanel { Margin = new Thickness(4, 0, 6, 0) };
            _title = new TextBlock
            {
                Foreground = Brush("#172033"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12.5,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _message = new TextBlock
            {
                Foreground = Brush("#53657D"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 62,
                Margin = new Thickness(0, 3, 0, 0)
            };
            _repeat = new TextBlock
            {
                Foreground = Brush("#8795A8"),
                FontSize = 10,
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = Visibility.Collapsed
            };
            _progress = new ProgressBar
            {
                Height = 4,
                Minimum = 0,
                Maximum = 100,
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            content.Children.Add(_title);
            content.Children.Add(_message);
            content.Children.Add(_repeat);
            content.Children.Add(_progress);
            Grid.SetColumn(content, 1);
            grid.Children.Add(content);

            _close = new Button
            {
                Content = "×",
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Brush("#7B8BA0"),
                FontSize = 16,
                Cursor = Cursors.Hand
            };
            _close.Click += delegate(object sender, RoutedEventArgs e)
            {
                e.Handled = true;
                BeginClose();
            };
            Grid.SetColumn(_close, 2);
            grid.Children.Add(_close);

            _root.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (IsButtonSource(e.OriginalSource as DependencyObject)) return;
                EventHandler handler = DetailsRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            MouseEnter += delegate
            {
                _hovered = true;
                ApplyOpacity(true);
            };
            MouseLeave += delegate
            {
                _hovered = false;
                ApplyOpacity(false);
                _lastTick = DateTime.UtcNow;
            };
            _countdown = new DispatcherTimer(TimeSpan.FromMilliseconds(100),
                DispatcherPriority.Background, CountdownTick, Dispatcher);
            SourceInitialized += delegate
            {
                FloatingWindowInterop.SetNoActivate(this);
            };
        }

        public void ShowMessage(FloatingMessage message, Rect anchor,
            CDBoxStudioSettings settings)
        {
            if (message == null) return;
            CancelPendingClose();
            _current = message.Clone();
            _settings = settings ?? new CDBoxStudioSettings();
            _settings.Normalize();
            RenderMessage();
            _closing = false;
            _autoClose = IsAutoClosing(_current);
            _remainingSeconds = _settings.FloatingCenterAutoCloseSeconds;
            _lastTick = DateTime.UtcNow;
            if (_autoClose) _countdown.Start();
            else _countdown.Stop();

            if (!IsVisible)
            {
                Opacity = 0.0;
                Show();
            }
            FloatingWindowInterop.SetNoActivate(this);
            FloatingWindowInterop.SetClickThrough(this,
                _current.Kind == FloatingMessageKind.Prompt);
            UpdateLayout();
            PositionNear(anchor);
            ApplyOpacity(IsMouseOver);
            AnimateOpen();
        }

        public void UpdateMessage(FloatingMessage message, Rect anchor,
            CDBoxStudioSettings settings)
        {
            if (message == null) return;
            CancelPendingClose();
            _current = message.Clone();
            _settings = settings ?? _settings ?? new CDBoxStudioSettings();
            _settings.Normalize();
            RenderMessage();
            FloatingWindowInterop.SetClickThrough(this,
                _current.Kind == FloatingMessageKind.Prompt);
            if (IsAutoClosing(_current))
            {
                _autoClose = true;
                _remainingSeconds = _settings.FloatingCenterAutoCloseSeconds;
                _lastTick = DateTime.UtcNow;
                _countdown.Start();
            }
            else
            {
                _autoClose = false;
                _countdown.Stop();
            }
            UpdateLayout();
            PositionNear(anchor);
            Pulse();
        }

        public void Reposition(Rect anchor)
        {
            if (IsVisible) PositionNear(anchor);
        }

        public void CloseImmediately()
        {
            _countdown.Stop();
            CancelPendingClose();
            if (IsVisible) Hide();
            RaiseClosed();
        }

        public void BeginClose()
        {
            if (_closing) return;
            _closing = true;
            _countdown.Stop();
            if (!IsVisible || !AnimationsEnabled)
            {
                Hide();
                RaiseClosed();
                return;
            }
            var scale = _root.RenderTransform as ScaleTransform;
            TimeSpan duration = TimeSpan.FromMilliseconds(140);
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            BeginAnimation(OpacityProperty, new DoubleAnimation(0.0, duration)
                { EasingFunction = ease });
            if (scale != null)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(0.92, duration) { EasingFunction = ease });
                var closing = new DoubleAnimation(0.92, duration)
                    { EasingFunction = ease };
                closing.Completed += delegate
                {
                    Hide();
                    BeginAnimation(OpacityProperty, null);
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    scale.ScaleX = 1.0;
                    scale.ScaleY = 1.0;
                    RaiseClosed();
                };
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, closing);
            }
            else
            {
                var timer = new DispatcherTimer { Interval = duration };
                timer.Tick += delegate
                {
                    timer.Stop();
                    Hide();
                    RaiseClosed();
                };
                timer.Start();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _countdown.Stop();
            base.OnClosed(e);
        }

        private void RenderMessage()
        {
            _title.Text = string.IsNullOrWhiteSpace(_current.Title)
                ? "CDBox" : _current.Title;
            _message.Text = string.IsNullOrWhiteSpace(_current.Summary)
                ? _current.Detail ?? string.Empty : _current.Summary;
            _glyph.Text = FloatingCenterPresentation.KindGlyph(_current.Kind);
            _glyphHost.Background = Brush(MessageColor(_current.Kind));
            _repeat.Text = _current.RepeatCount > 1
                ? "重复 " + _current.RepeatCount + " 次" : string.Empty;
            _repeat.Visibility = _current.RepeatCount > 1
                ? Visibility.Visible : Visibility.Collapsed;
            bool progress = _current.Kind == FloatingMessageKind.Progress;
            _progress.Visibility = progress ? Visibility.Visible : Visibility.Collapsed;
            if (progress)
            {
                _progress.IsIndeterminate = _current.IsIndeterminate ||
                    !_current.Progress.HasValue;
                if (_current.Progress.HasValue)
                    _progress.Value = Math.Max(0.0, Math.Min(100.0,
                        _current.Progress.Value));
            }
            bool closeable = _current.Kind != FloatingMessageKind.Prompt &&
                _current.Kind != FloatingMessageKind.Progress &&
                !_current.RequiresDecision;
            _close.Visibility = closeable ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PositionNear(Rect anchor)
        {
            FloatingWorkArea area = WorkAreaForAnchor(anchor);
            double width = ActualWidth > 1.0 ? ActualWidth : Width;
            double height = ActualHeight > 1.0 ? ActualHeight : 130.0;
            const double gap = 6.0;
            double rightSpace = area.Right - anchor.Right;
            double leftSpace = anchor.Left - area.Left;
            double left = rightSpace >= width + gap || rightSpace >= leftSpace
                ? anchor.Right + gap : anchor.Left - width - gap;
            double top = anchor.Top + (anchor.Height - height) / 2.0;
            FloatingResolvedPosition positioned =
                FloatingCenterPlacement.ConstrainAndSnap(left, top, area,
                    width, height, false);
            Left = positioned.State.Left;
            Top = positioned.State.Top;
        }

        private FloatingWorkArea WorkAreaForAnchor(Rect anchor)
        {
            foreach (FloatingWorkArea area in
                FloatingCenterScreenMetrics.GetWorkAreas(this))
            {
                Point center = new Point(anchor.Left + anchor.Width / 2.0,
                    anchor.Top + anchor.Height / 2.0);
                if (center.X >= area.Left && center.X <= area.Right &&
                    center.Y >= area.Top && center.Y <= area.Bottom) return area;
            }
            return FloatingCenterScreenMetrics.GetCurrentWorkArea(this);
        }

        private void CountdownTick(object sender, EventArgs e)
        {
            DateTime now = DateTime.UtcNow;
            if (!_hovered) _remainingSeconds -= (now - _lastTick).TotalSeconds;
            _lastTick = now;
            if (_autoClose && _remainingSeconds <= 0.0) BeginClose();
        }

        private void AnimateOpen()
        {
            var scale = _root.RenderTransform as ScaleTransform;
            if (!AnimationsEnabled || scale == null)
            {
                Opacity = IsMouseOver ? HoverOpacity : NormalOpacity;
                return;
            }
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 0.88;
            scale.ScaleY = 0.88;
            TimeSpan duration = TimeSpan.FromMilliseconds(170);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(OpacityProperty, new DoubleAnimation(
                IsMouseOver ? HoverOpacity : NormalOpacity, duration)
                { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1.0, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1.0, duration) { EasingFunction = ease });
        }

        private void Pulse()
        {
            if (!AnimationsEnabled) return;
            var scale = _root.RenderTransform as ScaleTransform;
            if (scale == null) return;
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,
                KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.025,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        private void ApplyOpacity(bool hovered)
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = hovered ? HoverOpacity : NormalOpacity;
        }

        private void CancelPendingClose()
        {
            _closing = false;
            BeginAnimation(OpacityProperty, null);
            var scale = _root.RenderTransform as ScaleTransform;
            if (scale != null)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scale.ScaleX = 1.0;
                scale.ScaleY = 1.0;
            }
        }

        private void RaiseClosed()
        {
            _closing = false;
            _current = null;
            EventHandler handler = CardClosed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private bool AnimationsEnabled
        {
            get { return _settings == null || _settings.AnimationsEnabled; }
        }

        private double NormalOpacity
        {
            get { return _settings == null ? 0.68 : _settings.AnnotationHudNormalOpacity; }
        }

        private double HoverOpacity
        {
            get { return _settings == null ? 1.0 : _settings.AnnotationHudHoverOpacity; }
        }

        private static bool IsAutoClosing(FloatingMessage message)
        {
            return message != null && !message.IsPersistent &&
                (message.Kind == FloatingMessageKind.Information ||
                 message.Kind == FloatingMessageKind.Success);
        }

        private static string MessageColor(FloatingMessageKind kind)
        {
            if (kind == FloatingMessageKind.Error) return "#EF4444";
            if (kind == FloatingMessageKind.Warning) return "#F59E0B";
            if (kind == FloatingMessageKind.Success) return "#10B981";
            if (kind == FloatingMessageKind.Sync || kind == FloatingMessageKind.Check)
                return "#7C5CFC";
            return "#3B82F6";
        }

        private static bool IsButtonSource(DependencyObject value)
        {
            while (value != null)
            {
                if (value is Button) return true;
                value = VisualTreeHelper.GetParent(value);
            }
            return false;
        }

        private static SolidColorBrush Brush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
