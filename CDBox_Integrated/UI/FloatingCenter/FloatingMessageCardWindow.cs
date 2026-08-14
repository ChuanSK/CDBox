using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using TCPipeAutoDraw.Core.FloatingCenter;
using TCPipeAutoDraw.UI.Studio;

namespace TCPipeAutoDraw.UI.FloatingCenter
{
    internal sealed class FloatingMessageCardEventArgs : EventArgs
    {
        public FloatingMessageCardEventArgs(FloatingMessage message)
        {
            Message = message == null ? null : message.Clone();
        }

        public FloatingMessage Message { get; private set; }
    }

    /// <summary>
    /// A single no-activate host containing all current toast cards. Cards are
    /// independently timed and ordered by importance, so one notification can
    /// never delay another notification.
    /// </summary>
    internal sealed class FloatingMessageCardWindow : Window
    {
        private sealed class CardState
        {
            public FloatingMessage Message;
            public Border Visual;
            public double RemainingSeconds;
            public bool AutoClose;
            public bool Closing;
        }

        private readonly ScrollViewer _scroll;
        private readonly StackPanel _cards;
        private readonly DispatcherTimer _countdown;
        private readonly Dictionary<string, CardState> _states =
            new Dictionary<string, CardState>(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastTick;
        private bool _hovered;
        private CDBoxStudioSettings _settings;

        public event EventHandler<FloatingMessageCardEventArgs> CardDismissed;
        public event EventHandler<FloatingMessageCardEventArgs> DetailsRequested;

        public int MessageCount { get { return _states.Count; } }

        public FloatingMessageCardWindow()
        {
            Title = "CDBox 提示";
            Width = 354;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 580;
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

            _cards = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };
            _scroll = new ScrollViewer
            {
                Content = _cards,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = true,
                MaxHeight = 552,
                Background = Brushes.Transparent
            };
            Content = _scroll;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextHintingMode(this, TextHintingMode.Fixed);

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

        public bool ContainsMessage(string messageId)
        {
            return !string.IsNullOrWhiteSpace(messageId) &&
                _states.ContainsKey(messageId);
        }

        public void UpsertMessage(FloatingMessage message, Rect anchor,
            CDBoxStudioSettings settings)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.Id)) return;
            _settings = settings ?? _settings ?? new CDBoxStudioSettings();
            _settings.Normalize();
            CardState state;
            bool added = !_states.TryGetValue(message.Id, out state);
            if (added)
            {
                state = new CardState();
                _states[message.Id] = state;
            }
            state.Message = message.Clone();
            state.AutoClose = IsAutoClosing(message);
            state.RemainingSeconds = _settings.FloatingCenterAutoCloseSeconds;
            state.Closing = false;
            Border oldVisual = state.Visual;
            state.Visual = BuildCard(state);
            if (oldVisual != null) _cards.Children.Remove(oldVisual);
            ReorderCards();

            if (!IsVisible)
            {
                Opacity = 0.0;
                Show();
            }
            FloatingWindowInterop.SetNoActivate(this);
            FloatingWindowInterop.SetClickThrough(this, false);
            UpdateLayout();
            PositionNear(anchor);
            ApplyOpacity(IsMouseOver);
            _lastTick = DateTime.UtcNow;
            if (_states.Values.Any(x => x.AutoClose)) _countdown.Start();
            if (added) AnimateCardOpen(state.Visual);
            else Pulse(state.Visual);
        }

        public void RemoveMessage(string messageId, bool animate)
        {
            CardState state;
            if (string.IsNullOrWhiteSpace(messageId) ||
                !_states.TryGetValue(messageId, out state)) return;
            if (animate && AnimationsEnabled && state.Visual != null)
                AnimateCardClose(state);
            else RemoveState(state, true);
        }

        public void RemoveInactivePersistent(string documentId,
            ISet<string> activeMessageIds)
        {
            foreach (CardState state in _states.Values.ToList())
            {
                FloatingMessage message = state.Message;
                if (message != null && message.IsPersistent && string.Equals(
                    message.DocumentId, documentId,
                    StringComparison.OrdinalIgnoreCase) &&
                    (activeMessageIds == null ||
                     !activeMessageIds.Contains(message.Id ?? string.Empty)))
                    RemoveState(state, false);
            }
        }

        public void Reposition(Rect anchor)
        {
            if (IsVisible) PositionNear(anchor);
        }

        public void CloseImmediately()
        {
            _countdown.Stop();
            _states.Clear();
            _cards.Children.Clear();
            if (IsVisible) Hide();
        }

        protected override void OnClosed(EventArgs e)
        {
            _countdown.Stop();
            base.OnClosed(e);
        }

        private Border BuildCard(CardState state)
        {
            FloatingMessage message = state.Message;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var glyph = new TextBlock
            {
                Text = FloatingCenterPresentation.KindGlyph(message.Kind),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var glyphHost = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brush(MessageColor(message.Kind)),
                Child = glyph,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 0, 0)
            };
            grid.Children.Add(glyphHost);

            var content = new StackPanel { Margin = new Thickness(4, 0, 6, 0) };
            content.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(message.Title) ? "CDBox" : message.Title,
                Foreground = Brush("#172033"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12.5,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            content.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(message.Summary)
                    ? message.Detail ?? string.Empty : message.Summary,
                Foreground = Brush("#53657D"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 62,
                Margin = new Thickness(0, 3, 0, 0)
            });
            if (message.RepeatCount > 1)
            {
                content.Children.Add(new TextBlock
                {
                    Text = "重复 " + message.RepeatCount + " 次",
                    Foreground = Brush("#8795A8"),
                    FontSize = 10,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }
            if (message.Kind == FloatingMessageKind.Progress)
            {
                var progress = new ProgressBar
                {
                    Height = 4,
                    Minimum = 0,
                    Maximum = 100,
                    Margin = new Thickness(0, 8, 0, 0),
                    IsHitTestVisible = false,
                    IsIndeterminate = message.IsIndeterminate || !message.Progress.HasValue
                };
                if (message.Progress.HasValue)
                    progress.Value = Math.Max(0.0, Math.Min(100.0,
                        message.Progress.Value));
                content.Children.Add(progress);
            }
            Grid.SetColumn(content, 1);
            grid.Children.Add(content);

            bool closeable = message.Kind != FloatingMessageKind.Prompt &&
                message.Kind != FloatingMessageKind.Progress &&
                !message.RequiresDecision;
            if (closeable)
            {
                var close = new Button
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
                close.Click += delegate(object sender, RoutedEventArgs e)
                {
                    e.Handled = true;
                    RemoveMessage(message.Id, true);
                };
                Grid.SetColumn(close, 2);
                grid.Children.Add(close);
            }

            var card = new Border
            {
                Margin = new Thickness(2, 4, 8, 4),
                Padding = new Thickness(12, 11, 10, 11),
                Background = Brush("#FBFFFFFF"),
                BorderBrush = Brush("#CAD7E5F4"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                Cursor = message.Kind == FloatingMessageKind.Prompt
                    ? Cursors.Arrow : Cursors.Hand,
                Child = grid,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1.0, 1.0),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 16,
                    ShadowDepth = 0,
                    Opacity = 0.20,
                    Color = Color.FromRgb(60, 118, 210)
                }
            };
            card.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (message.Kind == FloatingMessageKind.Prompt ||
                    IsButtonSource(e.OriginalSource as DependencyObject)) return;
                EventHandler<FloatingMessageCardEventArgs> handler = DetailsRequested;
                if (handler != null) handler(this,
                    new FloatingMessageCardEventArgs(message));
            };
            return card;
        }

        private void ReorderCards()
        {
            _cards.Children.Clear();
            foreach (CardState state in _states.Values
                .OrderByDescending(x => FloatingMessagePresentationQueue
                    .PresentationRank(x.Message))
                .ThenByDescending(x => MessageVersion(x.Message)))
                _cards.Children.Add(state.Visual);
        }

        private void CountdownTick(object sender, EventArgs e)
        {
            DateTime now = DateTime.UtcNow;
            double elapsed = (now - _lastTick).TotalSeconds;
            _lastTick = now;
            if (_hovered) return;
            foreach (CardState state in _states.Values.ToList())
            {
                if (!state.AutoClose || state.Closing) continue;
                state.RemainingSeconds -= elapsed;
                if (state.RemainingSeconds <= 0.0) AnimateCardClose(state);
            }
            if (!_states.Values.Any(x => x.AutoClose && !x.Closing))
                _countdown.Stop();
        }

        private void AnimateCardOpen(Border card)
        {
            if (card == null || !AnimationsEnabled) return;
            card.Opacity = 0.0;
            var scale = card.RenderTransform as ScaleTransform;
            if (scale != null) { scale.ScaleX = 0.88; scale.ScaleY = 0.88; }
            TimeSpan duration = TimeSpan.FromMilliseconds(170);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            card.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1.0, duration) { EasingFunction = ease });
            if (scale != null)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(1.0, duration) { EasingFunction = ease });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(1.0, duration) { EasingFunction = ease });
            }
        }

        private void AnimateCardClose(CardState state)
        {
            if (state == null || state.Closing) return;
            state.Closing = true;
            if (!AnimationsEnabled || state.Visual == null)
            {
                RemoveState(state, true);
                return;
            }
            var animation = new DoubleAnimation(0.0,
                TimeSpan.FromMilliseconds(140));
            animation.Completed += delegate { RemoveState(state, true); };
            state.Visual.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private void RemoveState(CardState state, bool notify)
        {
            if (state == null || state.Message == null) return;
            FloatingMessage message = state.Message.Clone();
            _states.Remove(message.Id ?? string.Empty);
            if (state.Visual != null) _cards.Children.Remove(state.Visual);
            if (_states.Count == 0)
            {
                _countdown.Stop();
                Hide();
            }
            else
            {
                UpdateLayout();
            }
            if (notify)
            {
                EventHandler<FloatingMessageCardEventArgs> handler = CardDismissed;
                if (handler != null) handler(this,
                    new FloatingMessageCardEventArgs(message));
            }
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
            double top = anchor.Top;
            FloatingResolvedPosition positioned =
                FloatingCenterPlacement.ConstrainAndSnap(left, top, area,
                    width, Math.Min(height, MaxHeight), false);
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

        private void Pulse(Border card)
        {
            if (!AnimationsEnabled || card == null) return;
            var scale = card.RenderTransform as ScaleTransform;
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
            if (message == null || message.RequiresDecision) return false;
            return message.Kind != FloatingMessageKind.Prompt &&
                message.Kind != FloatingMessageKind.Progress &&
                message.Kind != FloatingMessageKind.Decision;
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

        private static DateTime MessageVersion(FloatingMessage message)
        {
            if (message == null) return DateTime.MinValue;
            return message.UpdatedAt == default(DateTime)
                ? message.CreatedAt : message.UpdatedAt;
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
            var brush = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
