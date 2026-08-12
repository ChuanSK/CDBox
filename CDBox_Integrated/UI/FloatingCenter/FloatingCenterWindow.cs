using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using TCPipeAutoDraw.Core.FloatingCenter;
using TCPipeAutoDraw.UI.Studio;

namespace TCPipeAutoDraw.UI.FloatingCenter
{
    internal sealed class FloatingCenterWindow : Window
    {
        private const double CollapsedWidth = 72.0;
        private const double CollapsedHeight = 72.0;
        private const double ExpandedWidth = 382.0;
        private const double PreferredExpandedHeight = 510.0;
        private readonly Grid _animationRoot;
        private readonly Border _shell;
        private readonly Border _panelGlowSurface;
        private readonly Grid _collapsedRoot;
        private readonly Grid _panelRoot;
        private readonly Border _ballFace;
        private readonly Path _progressArc;
        private readonly Ellipse _progressTrack;
        private readonly RotateTransform _progressRotation;
        private readonly Border _badge;
        private readonly TextBlock _badgeText;
        private readonly TextBlock _documentName;
        private readonly TextBlock _statusText;
        private readonly TextBlock _activityText;
        private readonly TextBlock _updatedText;
        private Button _refreshButton;
        private Button _syncAllButton;
        private readonly StackPanel _items;
        private readonly Button _taskTab;
        private readonly Button _historyTab;
        private readonly Button _ignoredTab;
        private readonly DropShadowEffect _glow;
        private readonly DropShadowEffect _ballGlow;
        private readonly DispatcherTimer _indeterminateTimer;
        private FloatingCenterViewSnapshot _snapshot;
        private CDBoxStudioSettings _settings;
        private FloatingPositionState _collapsedPosition;
        private bool _expanded;
        private int _viewMode;
        private bool _animationActive;
        private bool _anchorRight;
        private bool _anchorBottom;
        private bool _positionInitialized;
        private DateTime _lastSnapshotUpdate;

        public event EventHandler HideRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler ExpandedChanged;
        public event EventHandler AnchorChanged;
        public event EventHandler<FloatingCenterActionEventArgs> ActionRequested;

        public bool IsExpanded { get { return _expanded; } }

        public FloatingCenterWindow()
        {
            Title = "CDBox 悬浮球";
            Width = CollapsedWidth;
            Height = CollapsedHeight;
            MinWidth = CollapsedWidth;
            MinHeight = CollapsedHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = false;
            ShowActivated = false;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 12.0;

            _glow = new DropShadowEffect
            {
                BlurRadius = 22,
                ShadowDepth = 0,
                Opacity = 0.28,
                Color = Color.FromRgb(66, 139, 255)
            };
            _ballGlow = new DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 0,
                Opacity = 0.28,
                Color = Color.FromRgb(66, 139, 255)
            };
            _animationRoot = new Grid
            {
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1.0, 1.0)
            };
            _shell = new Border
            {
                Margin = new Thickness(10),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(28),
                UseLayoutRounding = true
            };
            _panelGlowSurface = new Border
            {
                Margin = new Thickness(10),
                BorderBrush = Brush("#7A428BFF"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Effect = _glow,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            _animationRoot.Children.Add(_panelGlowSurface);
            _animationRoot.Children.Add(_shell);
            Content = _animationRoot;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextHintingMode(this, TextHintingMode.Fixed);

            _collapsedRoot = new Grid { Cursor = Cursors.Hand };
            _ballFace = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(22),
                Background = Brush("#3B82F6"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = _ballGlow,
                Child = new TextBlock
                {
                    Text = "CD",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            _progressTrack = new Ellipse
            {
                Width = 52,
                Height = 52,
                Stroke = Brush("#334B6B94"),
                StrokeThickness = 2.5,
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _progressRotation = new RotateTransform(0, 26, 26);
            _progressArc = new Path
            {
                Width = 52,
                Height = 52,
                Stroke = Brush("#FF93C5FD"),
                StrokeThickness = 3.0,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Stretch = Stretch.None,
                RenderTransform = _progressRotation,
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _badgeText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _badge = new Border
            {
                MinWidth = 18,
                Height = 18,
                Padding = new Thickness(4, 0, 4, 0),
                CornerRadius = new CornerRadius(9),
                Background = Brush("#EF4444"),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1.5),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 5, 4, 0),
                Child = _badgeText,
                Visibility = Visibility.Collapsed
            };
            _collapsedRoot.Children.Add(_progressTrack);
            _collapsedRoot.Children.Add(_progressArc);
            _collapsedRoot.Children.Add(_ballFace);
            _collapsedRoot.Children.Add(_badge);
            _collapsedRoot.MouseLeftButtonDown += DragOrToggle;

            _panelRoot = new Grid
            {
                Visibility = Visibility.Collapsed,
                Opacity = 0.0,
                Margin = new Thickness(4),
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };
            _panelRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _panelRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _panelRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _panelRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid header = BuildHeader();
            Grid.SetRow(header, 0);
            _panelRoot.Children.Add(header);

            Grid tabs = new Grid { Margin = new Thickness(10, 0, 10, 8) };
            tabs.ColumnDefinitions.Add(new ColumnDefinition());
            tabs.ColumnDefinitions.Add(new ColumnDefinition());
            tabs.ColumnDefinitions.Add(new ColumnDefinition());
            _taskTab = TabButton("任务");
            _historyTab = TabButton("历史");
            _ignoredTab = TabButton("已忽略");
            _taskTab.Click += delegate { _viewMode = 0; RenderItems(); };
            _historyTab.Click += delegate { _viewMode = 1; RenderItems(); };
            _ignoredTab.Click += delegate { _viewMode = 2; RenderItems(); };
            Grid.SetColumn(_historyTab, 1);
            Grid.SetColumn(_ignoredTab, 2);
            tabs.Children.Add(_taskTab);
            tabs.Children.Add(_historyTab);
            tabs.Children.Add(_ignoredTab);
            Grid.SetRow(tabs, 1);
            _panelRoot.Children.Add(tabs);

            _items = new StackPanel { Margin = new Thickness(10, 0, 10, 8) };
            var scroll = new ScrollViewer
            {
                Content = _items,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetRow(scroll, 2);
            _panelRoot.Children.Add(scroll);

            Grid footer = BuildFooter();
            Grid.SetRow(footer, 3);
            _panelRoot.Children.Add(footer);

            var host = new Grid();
            host.Children.Add(_collapsedRoot);
            host.Children.Add(_panelRoot);
            _shell.Child = host;

            _documentName = Find<TextBlock>(header, "DocumentName");
            _statusText = Find<TextBlock>(header, "StatusText");
            _activityText = Find<TextBlock>(header, "ActivityText");
            _updatedText = Find<TextBlock>(header, "UpdatedText");

            _indeterminateTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(32), DispatcherPriority.Render,
                delegate { _progressRotation.Angle = (_progressRotation.Angle + 5.0) % 360.0; },
                Dispatcher);
            MouseEnter += delegate { ApplyHover(true); };
            MouseLeave += delegate { ApplyHover(false); };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape && _expanded)
                {
                    e.Handled = true;
                    SetExpanded(false);
                }
            };
            LocationChanged += delegate { RaiseAnchorChanged(); };
            SizeChanged += delegate { RaiseAnchorChanged(); };
            SourceInitialized += delegate
            {
                FloatingWindowInterop.SetNoActivate(this);
            };
        }

        public void ApplySettings(CDBoxStudioSettings settings)
        {
            _settings = settings ?? new CDBoxStudioSettings();
            _settings.Normalize();
            _glow.Opacity = _settings.AnnotationHudGlowEnabled
                ? _settings.AnnotationHudGlowIntensity : 0.0;
            _ballGlow.Opacity = _settings.AnnotationHudGlowEnabled
                ? _settings.AnnotationHudGlowIntensity : 0.0;
            ApplyOpacity(IsMouseOver);
        }

        public void SetSnapshot(FloatingCenterViewSnapshot snapshot)
        {
            _snapshot = snapshot;
            FloatingStatusSnapshot status = snapshot == null ? null : snapshot.Status;
            bool changed = status != null && _lastSnapshotUpdate != default(DateTime)
                && status.UpdatedAt > _lastSnapshotUpdate;
            if (status != null) _lastSnapshotUpdate = status.UpdatedAt;
            FloatingHealthState health = status == null
                ? FloatingHealthState.Normal : status.Health;
            Brush healthBrush = Brush(FloatingCenterPresentation.HealthColor(health));
            _ballFace.Background = healthBrush;
            _glow.Color = ((SolidColorBrush)healthBrush).Color;
            _panelGlowSurface.BorderBrush = healthBrush;
            _ballGlow.Color = ((SolidColorBrush)healthBrush).Color;

            string badge = FloatingCenterPresentation.Badge(
                status == null ? 0 : status.TaskGroupCount);
            _badgeText.Text = badge;
            _badge.Visibility = badge.Length == 0
                ? Visibility.Collapsed : Visibility.Visible;

            IList<FloatingMessage> active = snapshot == null || snapshot.Active == null
                ? new List<FloatingMessage>() : snapshot.Active;
            bool working = status != null &&
                status.Activity == FloatingActivityState.Working;
            bool waitingForCad = status != null &&
                status.Activity == FloatingActivityState.WaitingForCadInput;
            _collapsedRoot.IsHitTestVisible = !waitingForCad;
            _panelRoot.IsHitTestVisible = !waitingForCad;
            FloatingWindowInterop.SetClickThrough(this, waitingForCad);
            UpdateProgress(active, working);

            if (_documentName != null)
                _documentName.Text = string.IsNullOrWhiteSpace(snapshot == null
                    ? null : snapshot.DocumentName) ? "当前图纸" : snapshot.DocumentName;
            if (_statusText != null)
                _statusText.Text = FloatingCenterPresentation.HealthText(health);
            if (_activityText != null)
                _activityText.Text = FloatingCenterPresentation.ActivityText(
                    status == null ? FloatingActivityState.Idle : status.Activity);
            if (_updatedText != null)
                _updatedText.Text = status == null ? string.Empty :
                    "更新于 " + status.UpdatedAt.ToLocalTime().ToString("HH:mm:ss");
            if (_refreshButton != null)
                _refreshButton.Content = active.Any(x => x != null &&
                    x.Kind == FloatingMessageKind.Progress && string.Equals(
                        x.Source, "DrawingCheck",
                        StringComparison.OrdinalIgnoreCase))
                    ? "取消检查" : "重新检查";
            if (_syncAllButton != null)
            {
                int syncCount = active.Count(x => x != null &&
                    x.Kind == FloatingMessageKind.Sync && string.Equals(
                        x.Source, "SyncCenter",
                        StringComparison.OrdinalIgnoreCase));
                _syncAllButton.Visibility = syncCount > 0
                    ? Visibility.Visible : Visibility.Collapsed;
                _syncAllButton.Content = syncCount > 1
                    ? "全部同步 " + syncCount : "同步";
            }
            if (_ignoredTab != null)
            {
                int ignoredCount = snapshot == null || snapshot.Ignored == null
                    ? 0 : snapshot.Ignored.Count;
                _ignoredTab.Content = ignoredCount > 0
                    ? "已忽略 " + ignoredCount : "已忽略";
            }
            if (_expanded) RenderItems();
            if (changed && IsVisible && AnimationsEnabled) Pulse();
        }

        public void ShowShell(bool animate)
        {
            if (!IsVisible)
            {
                Opacity = 0.0;
                Show();
                InitializePosition();
                if (animate && AnimationsEnabled) AnimateInitialShow();
                else ApplyOpacity(IsMouseOver);
            }
            else
            {
                Visibility = Visibility.Visible;
                InitializePosition();
            }
        }

        public void HideShell()
        {
            if (_expanded) SetExpanded(false, false);
            if (IsVisible) Hide();
        }

        public void CloseImmediately()
        {
            _indeterminateTimer.Stop();
            Close();
        }

        public void ResetPosition()
        {
            FloatingCenterPositionStore.Reset();
            _positionInitialized = false;
            if (_expanded) SetExpanded(false, false);
            InitializePosition();
        }

        public void OpenPanel()
        {
            if (!_expanded) SetExpanded(true);
        }

        public void ClosePanelImmediately()
        {
            if (!_expanded && !_animationActive) return;
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            _animationActive = false;
            _expanded = false;
            FloatingPositionState target = _collapsedPosition ??
                new FloatingPositionState { Left = Left, Top = Top };
            Width = CollapsedWidth;
            Height = CollapsedHeight;
            Left = target.Left;
            Top = target.Top;
            CompleteGeometryAnimation(false);
            EventHandler handler = ExpandedChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public Rect GetCollapsedAnchor()
        {
            FloatingPositionState state = _collapsedPosition;
            double left = state == null ? Left : state.Left;
            double top = state == null ? Top : state.Top;
            return new Rect(left, top, CollapsedWidth, CollapsedHeight);
        }

        private Grid BuildHeader()
        {
            var header = new Grid
            {
                Margin = new Thickness(10, 8, 8, 8),
                Cursor = Cursors.SizeAll
            };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = Brush("#3B82F6"),
                Child = new TextBlock
                {
                    Text = "CD",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            header.Children.Add(icon);
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock
            {
                Name = "DocumentName",
                Text = "当前图纸",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13.5,
                Foreground = Brush("#172033"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var statusLine = new StackPanel { Orientation = Orientation.Horizontal };
            statusLine.Children.Add(new TextBlock
            {
                Name = "StatusText",
                Text = "当前图纸正常",
                Foreground = Brush("#52647C"),
                FontSize = 11
            });
            statusLine.Children.Add(new TextBlock
            {
                Name = "ActivityText",
                Text = "空闲",
                Margin = new Thickness(8, 0, 0, 0),
                Foreground = Brush("#7C5CFC"),
                FontSize = 11
            });
            titles.Children.Add(statusLine);
            Grid.SetColumn(titles, 1);
            header.Children.Add(titles);

            Button collapse = IconButton("−", "收起");
            collapse.Click += delegate { SetExpanded(false); };
            Grid.SetColumn(collapse, 2);
            header.Children.Add(collapse);
            header.MouseLeftButtonDown += DragExpanded;
            return header;
        }

        private Grid BuildFooter()
        {
            var footer = new Grid { Margin = new Thickness(10, 0, 10, 9) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var updated = new TextBlock
            {
                Name = "UpdatedText",
                Foreground = Brush("#8A98AA"),
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            footer.Children.Add(updated);
            _refreshButton = FooterButton("重新检查");
            _refreshButton.Click += delegate
            {
                RaiseAction("drawing-check.refresh");
            };
            Grid.SetColumn(_refreshButton, 1);
            footer.Children.Add(_refreshButton);
            _syncAllButton = FooterButton("同步");
            _syncAllButton.Margin = new Thickness(6, 0, 0, 0);
            _syncAllButton.Visibility = Visibility.Collapsed;
            _syncAllButton.Click += delegate { RaiseAction("sync.run-all"); };
            Grid.SetColumn(_syncAllButton, 2);
            footer.Children.Add(_syncAllButton);
            Button settings = FooterButton("设置");
            settings.Margin = new Thickness(6, 0, 0, 0);
            settings.Click += delegate
            {
                EventHandler handler = SettingsRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            Grid.SetColumn(settings, 3);
            footer.Children.Add(settings);
            Button hide = FooterButton("暂时隐藏");
            hide.Margin = new Thickness(6, 0, 0, 0);
            hide.Click += delegate
            {
                EventHandler handler = HideRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            Grid.SetColumn(hide, 4);
            footer.Children.Add(hide);
            return footer;
        }

        private void RenderItems()
        {
            _items.Children.Clear();
            SetTabVisual(_taskTab, _viewMode == 0);
            SetTabVisual(_historyTab, _viewMode == 1);
            SetTabVisual(_ignoredTab, _viewMode == 2);
            IList<FloatingMessage> source = new List<FloatingMessage>();
            if (_snapshot != null)
            {
                if (_viewMode == 1) source = _snapshot.History ?? source;
                else if (_viewMode == 2) source = _snapshot.Ignored ?? source;
                else source = _snapshot.Active ?? source;
            }
            IEnumerable<FloatingMessage> visible = source.Where(x => x != null)
                .Take(_viewMode == 0 ? 30 : 20);
            if (!visible.Any())
            {
                _items.Children.Add(new TextBlock
                {
                    Text = _viewMode == 1 ? "本次会话暂无历史记录" :
                        (_viewMode == 2 ? "当前没有已忽略的问题" :
                            "当前没有待处理任务"),
                    Foreground = Brush("#7B8BA0"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 34, 0, 34)
                });
                return;
            }
            foreach (FloatingMessage message in visible)
                _items.Children.Add(BuildMessageCard(message));
        }

        private UIElement BuildMessageCard(FloatingMessage message)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var glyph = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = Brush(MessageColor(message.Kind)),
                Child = new TextBlock
                {
                    Text = FloatingCenterPresentation.KindGlyph(message.Kind),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            grid.Children.Add(glyph);
            var text = new StackPanel();
            text.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(message.Title) ? "CDBox" : message.Title,
                Foreground = Brush("#172033"),
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            if (!string.IsNullOrWhiteSpace(message.Summary))
                text.Children.Add(new TextBlock
                {
                    Text = message.Summary,
                    Foreground = Brush("#617188"),
                    FontSize = 11,
                    Margin = new Thickness(0, 3, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 48
                });
            if (message.RepeatCount > 1)
                text.Children.Add(new TextBlock
                {
                    Text = "重复 " + message.RepeatCount + " 次",
                    Foreground = Brush("#8A98AA"),
                    FontSize = 10,
                    Margin = new Thickness(0, 3, 0, 0)
                });
            if (message.Actions != null && message.Actions.Count > 0)
            {
                var actions = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 7, 0, 0)
                };
                foreach (FloatingAction action in message.Actions)
                {
                    if (action == null || string.IsNullOrWhiteSpace(action.Text))
                        continue;
                    FloatingAction current = action;
                    Button button = FooterButton(current.Text);
                    button.Height = 26;
                    button.Padding = new Thickness(9, 0, 9, 0);
                    button.Margin = new Thickness(0, 0, 6, 0);
                    if (current.IsPrimary)
                    {
                        button.Background = Brush("#EAF2FF");
                        button.BorderBrush = Brush("#A9C8FF");
                        button.Foreground = Brush("#2563EB");
                    }
                    else if (current.IsDestructive)
                    {
                        button.Background = Brush("#FEF2F2");
                        button.BorderBrush = Brush("#FECACA");
                        button.Foreground = Brush("#DC2626");
                    }
                    button.Click += delegate { RaiseAction(current.Id); };
                    actions.Children.Add(button);
                }
                if (actions.Children.Count > 0) text.Children.Add(actions);
            }
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            return new Border
            {
                Child = grid,
                Background = Brush("#F7F9FC"),
                BorderBrush = Brush("#DEE6F0"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 7)
            };
        }

        private void RaiseAction(string actionId)
        {
            EventHandler<FloatingCenterActionEventArgs> handler = ActionRequested;
            if (handler != null)
                handler(this, new FloatingCenterActionEventArgs(actionId));
        }

        private void DragOrToggle(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || _animationActive) return;
            e.Handled = true;
            double beforeLeft = Left;
            double beforeTop = Top;
            try { DragMove(); }
            catch (InvalidOperationException) { }
            double distance = Math.Abs(Left - beforeLeft) + Math.Abs(Top - beforeTop);
            ConstrainAndSave(true);
            if (distance < 2.0) SetExpanded(true);
        }

        private void DragExpanded(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || _animationActive ||
                IsButtonSource(e.OriginalSource as DependencyObject)) return;
            e.Handled = true;
            try { DragMove(); }
            catch (InvalidOperationException) { }
            ConstrainExpanded();
            CaptureCollapsedAnchorFromExpanded();
            FloatingCenterPositionStore.Save(_collapsedPosition);
        }

        private void SetExpanded(bool value)
        {
            SetExpanded(value, true);
        }

        private void SetExpanded(bool value, bool animate)
        {
            if (_expanded == value || _animationActive) return;
            if (value)
            {
                ResetRootScale();
                CaptureCollapsedPosition();
                PrepareExpandedGeometry(out double targetLeft, out double targetTop,
                    out double targetHeight);
                _expanded = true;
                ApplyShellMode(true);
                _panelRoot.Visibility = Visibility.Visible;
                _collapsedRoot.Visibility = Visibility.Collapsed;
                RenderItems();
                AnimateGeometry(ExpandedWidth, targetHeight, targetLeft, targetTop,
                    true, animate && AnimationsEnabled);
            }
            else
            {
                _expanded = false;
                FloatingPositionState target = _collapsedPosition ??
                    new FloatingPositionState { Left = Left, Top = Top };
                AnimateGeometry(CollapsedWidth, CollapsedHeight, target.Left,
                    target.Top, false, animate && AnimationsEnabled);
            }
            EventHandler handler = ExpandedChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void AnimateGeometry(double width, double height, double left,
            double top, bool opening, bool animate)
        {
            if (!animate)
            {
                BeginAnimation(WidthProperty, null);
                BeginAnimation(HeightProperty, null);
                BeginAnimation(LeftProperty, null);
                BeginAnimation(TopProperty, null);
                Width = width;
                Height = height;
                Left = left;
                Top = top;
                CompleteGeometryAnimation(opening);
                return;
            }

            _animationActive = true;
            var ease = new CubicEase
            {
                EasingMode = opening ? EasingMode.EaseOut : EasingMode.EaseIn
            };
            TimeSpan duration = TimeSpan.FromMilliseconds(opening ? 210 : 170);
            BeginAnimation(WidthProperty, new DoubleAnimation(width, duration)
                { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
            BeginAnimation(HeightProperty, new DoubleAnimation(height, duration)
                { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
            BeginAnimation(LeftProperty, new DoubleAnimation(left, duration)
                { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
            var topAnimation = new DoubleAnimation(top, duration)
                { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
            topAnimation.Completed += delegate
            {
                BeginAnimation(WidthProperty, null);
                BeginAnimation(HeightProperty, null);
                BeginAnimation(LeftProperty, null);
                BeginAnimation(TopProperty, null);
                Width = width;
                Height = height;
                Left = left;
                Top = top;
                CompleteGeometryAnimation(opening);
                _animationActive = false;
            };
            BeginAnimation(TopProperty, topAnimation);
            _panelRoot.BeginAnimation(OpacityProperty,
                new DoubleAnimation(opening ? 1.0 : 0.0, duration));
        }

        private void CompleteGeometryAnimation(bool opening)
        {
            _panelRoot.BeginAnimation(OpacityProperty, null);
            _panelRoot.Opacity = opening ? 1.0 : 0.0;
            _panelRoot.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
            _collapsedRoot.Visibility = opening ? Visibility.Collapsed : Visibility.Visible;
            if (!opening)
            {
                ApplyShellMode(false);
                ConstrainAndSave(false);
            }
        }

        private void PrepareExpandedGeometry(out double left, out double top,
            out double height)
        {
            FloatingWorkArea area = FloatingCenterScreenMetrics.GetCurrentWorkArea(this);
            height = Math.Min(PreferredExpandedHeight, Math.Max(300.0,
                area.Height * 0.70));
            double ballCenterX = Left + CollapsedWidth / 2.0;
            double ballCenterY = Top + CollapsedHeight / 2.0;
            _anchorRight = ballCenterX > area.Left + area.Width / 2.0;
            _anchorBottom = ballCenterY > area.Top + area.Height / 2.0;
            left = _anchorRight ? Left + CollapsedWidth - ExpandedWidth : Left;
            top = _anchorBottom ? Top + CollapsedHeight - height : Top;
            FloatingResolvedPosition result = FloatingCenterPlacement.ConstrainAndSnap(
                left, top, area, ExpandedWidth, height, false);
            left = result.State.Left;
            top = result.State.Top;
        }

        private void InitializePosition()
        {
            if (_positionInitialized || !IsLoaded) return;
            UpdateLayout();
            FloatingResolvedPosition restored = FloatingCenterPlacement.Restore(
                FloatingCenterPositionStore.Load(),
                FloatingCenterScreenMetrics.GetWorkAreas(this),
                CollapsedWidth, CollapsedHeight);
            _collapsedPosition = restored.State;
            Left = restored.State.Left;
            Top = restored.State.Top;
            _positionInitialized = true;
        }

        private void CaptureCollapsedPosition()
        {
            FloatingWorkArea area = FloatingCenterScreenMetrics.GetCurrentWorkArea(this);
            _collapsedPosition = FloatingCenterPlacement.ConstrainAndSnap(Left,
                Top, area, CollapsedWidth, CollapsedHeight,
                _settings == null || _settings.FloatingCenterSnapToEdges).State;
            FloatingCenterPositionStore.Save(_collapsedPosition);
        }

        private void CaptureCollapsedAnchorFromExpanded()
        {
            double left = _anchorRight ? Left + Width - CollapsedWidth : Left;
            double top = _anchorBottom ? Top + Height - CollapsedHeight : Top;
            FloatingWorkArea area = FloatingCenterScreenMetrics.GetCurrentWorkArea(this);
            _collapsedPosition = FloatingCenterPlacement.ConstrainAndSnap(left,
                top, area, CollapsedWidth, CollapsedHeight,
                _settings == null || _settings.FloatingCenterSnapToEdges).State;
        }

        private void ConstrainAndSave(bool snap)
        {
            FloatingWorkArea area = FloatingCenterScreenMetrics.GetCurrentWorkArea(this);
            FloatingResolvedPosition value = FloatingCenterPlacement.ConstrainAndSnap(
                Left, Top, area, CollapsedWidth, CollapsedHeight,
                snap && (_settings == null || _settings.FloatingCenterSnapToEdges));
            Left = value.State.Left;
            Top = value.State.Top;
            _collapsedPosition = value.State;
            FloatingCenterPositionStore.Save(value.State);
        }

        private void ConstrainExpanded()
        {
            FloatingWorkArea area = FloatingCenterScreenMetrics.GetCurrentWorkArea(this);
            FloatingResolvedPosition value = FloatingCenterPlacement.ConstrainAndSnap(
                Left, Top, area, ActualWidth > 1 ? ActualWidth : Width,
                ActualHeight > 1 ? ActualHeight : Height, false);
            Left = value.State.Left;
            Top = value.State.Top;
        }

        private void UpdateProgress(IList<FloatingMessage> active, bool working)
        {
            if (!working)
            {
                _indeterminateTimer.Stop();
                _progressArc.Visibility = Visibility.Collapsed;
                _progressTrack.Visibility = Visibility.Collapsed;
                return;
            }
            _progressTrack.Visibility = Visibility.Visible;
            _progressArc.Visibility = Visibility.Visible;
            double? progress = FloatingCenterPresentation.OverallProgress(active);
            if (!progress.HasValue)
            {
                _progressArc.Data = ArcGeometry(0.0, 82.0);
                if (!_indeterminateTimer.IsEnabled) _indeterminateTimer.Start();
            }
            else
            {
                _indeterminateTimer.Stop();
                _progressRotation.Angle = 0;
                _progressArc.Data = ArcGeometry(0.0, Math.Max(2.0,
                    progress.Value / 100.0 * 359.8));
            }
        }

        private static Geometry ArcGeometry(double startDegrees, double sweepDegrees)
        {
            const double center = 26.0;
            const double radius = 23.0;
            double start = (startDegrees - 90.0) * Math.PI / 180.0;
            double end = (startDegrees + sweepDegrees - 90.0) * Math.PI / 180.0;
            var figure = new PathFigure
            {
                StartPoint = new Point(center + radius * Math.Cos(start),
                    center + radius * Math.Sin(start)),
                IsClosed = false
            };
            figure.Segments.Add(new ArcSegment
            {
                Point = new Point(center + radius * Math.Cos(end),
                    center + radius * Math.Sin(end)),
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = sweepDegrees > 180.0
            });
            return new PathGeometry(new[] { figure });
        }

        private void AnimateInitialShow()
        {
            var scale = _animationRoot.RenderTransform as ScaleTransform;
            if (scale == null) return;
            Opacity = 0.0;
            scale.ScaleX = 0.78;
            scale.ScaleY = 0.78;
            TimeSpan duration = TimeSpan.FromMilliseconds(190);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(OpacityProperty, new DoubleAnimation(
                IsMouseOver ? HoverOpacity : NormalOpacity, duration)
                { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1.0, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1.0, duration) { EasingFunction = ease });
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

        private void ApplyOpacity(bool hovered)
        {
            BeginAnimation(OpacityProperty, null);
            if (_expanded)
            {
                Opacity = 1.0;
                _shell.Background = BrushWithOpacity("#FFFFFF",
                    hovered ? HoverOpacity : NormalOpacity);
            }
            else Opacity = hovered ? HoverOpacity : NormalOpacity;
        }

        private void ApplyHover(bool hovered)
        {
            ApplyOpacity(hovered);
            var scale = _animationRoot.RenderTransform as ScaleTransform;
            if (scale == null || _animationActive) return;
            double target = !_expanded && hovered ? 1.035 : 1.0;
            if (!AnimationsEnabled)
            {
                scale.ScaleX = target;
                scale.ScaleY = target;
                return;
            }
            TimeSpan duration = TimeSpan.FromMilliseconds(hovered ? 130 : 110);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(target, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(target, duration) { EasingFunction = ease });
        }

        private void ResetRootScale()
        {
            var scale = _animationRoot.RenderTransform as ScaleTransform;
            if (scale == null) return;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1.0;
            scale.ScaleY = 1.0;
        }

        private void RaiseAnchorChanged()
        {
            EventHandler handler = AnchorChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void Pulse()
        {
            var scale = _ballFace.RenderTransform as ScaleTransform;
            if (scale == null)
            {
                scale = new ScaleTransform(1.0, 1.0);
                _ballFace.RenderTransform = scale;
                _ballFace.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,
                KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.10,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(105))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        private void ApplyShellMode(bool expanded)
        {
            if (expanded)
            {
                _shell.Background = Brush("#F9FFFFFF");
                _shell.BorderBrush = Brush("#C6CFDCEE");
                _shell.BorderThickness = new Thickness(1);
                _shell.CornerRadius = new CornerRadius(14);
                _shell.Effect = null;
                _panelGlowSurface.Visibility = Visibility.Visible;
                _ballFace.Effect = null;
                ApplyOpacity(IsMouseOver);
            }
            else
            {
                _shell.Background = Brushes.Transparent;
                _shell.BorderBrush = Brushes.Transparent;
                _shell.BorderThickness = new Thickness(0);
                _shell.CornerRadius = new CornerRadius(28);
                _shell.Effect = null;
                _panelGlowSurface.Visibility = Visibility.Collapsed;
                _ballFace.Effect = _ballGlow;
                ApplyOpacity(IsMouseOver);
            }
        }

        private static Button TabButton(string text)
        {
            return new Button
            {
                Content = text,
                Height = 31,
                Margin = new Thickness(2),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Brush("#617188"),
                Cursor = Cursors.Hand
            };
        }

        private static void SetTabVisual(Button button, bool selected)
        {
            if (button == null) return;
            button.Background = selected ? Brush("#EAF2FF") : Brushes.Transparent;
            button.Foreground = selected ? Brush("#2563EB") : Brush("#617188");
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }

        private static Button IconButton(string text, string tooltip)
        {
            return new Button
            {
                Content = text,
                ToolTip = tooltip,
                Width = 30,
                Height = 30,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Brush("#617188"),
                FontSize = 18,
                Cursor = Cursors.Hand
            };
        }

        private static Button FooterButton(string text)
        {
            return new Button
            {
                Content = text,
                Height = 30,
                Padding = new Thickness(11, 0, 11, 0),
                BorderBrush = Brush("#D9E2EE"),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Foreground = Brush("#52647C"),
                Cursor = Cursors.Hand
            };
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

        private static T Find<T>(DependencyObject root, string name)
            where T : FrameworkElement
        {
            if (root == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int index = 0; index < count; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, index);
                T match = child as T;
                if (match != null && string.Equals(match.Name, name,
                    StringComparison.Ordinal)) return match;
                T nested = Find<T>(child, name);
                if (nested != null) return nested;
            }
            return null;
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

        private static SolidColorBrush Brush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private static SolidColorBrush BrushWithOpacity(string hex,
            double opacity)
        {
            Color color = (Color)ColorConverter.ConvertFromString(hex);
            color.A = (byte)Math.Round(Math.Max(0.20,
                Math.Min(1.0, opacity)) * 255.0);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
