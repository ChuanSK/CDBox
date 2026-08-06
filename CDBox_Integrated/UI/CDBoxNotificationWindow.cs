using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TCPipeAutoDraw.Modules.AnnotationHud;
using WinForms = System.Windows.Forms;

namespace TCPipeAutoDraw.UI
{
    internal enum CDBoxNotificationKind
    {
        Information,
        Success,
        Warning,
        Error,
        Question
    }

    internal sealed class CDBoxNotificationWindow : AnnotationHudWindowBase
    {
        private readonly bool _modeless;
        private readonly bool _autoClose;
        private readonly WinForms.DialogResult _escapeResult;
        private readonly CheckBox _doNotAskCheckBox;
        private readonly DispatcherTimer _autoCloseTimer;
        private TextBlock _countdownTextBlock;
        private TimeSpan _autoCloseRemaining;
        private DateTime _autoCloseDeadlineUtc;
        private bool _closeRequested;

        private static readonly TimeSpan AutoCloseDuration = TimeSpan.FromSeconds(5);

        public CDBoxNotificationWindow(string title, string message,
            CDBoxNotificationKind kind, WinForms.MessageBoxButtons buttons,
            WinForms.MessageBoxDefaultButton defaultButton, bool modeless,
            bool autoClose, bool showDoNotAsk, string yesText = null,
            string noText = null)
            : base(string.IsNullOrWhiteSpace(title) ? "CDBox" : title.Trim(),
                ResolveWidth(message))
        {
            _modeless = modeless;
            _autoClose = autoClose;
            MaxHeight = 580;
            ShowActivated = !modeless;
            Focusable = !modeless;

            IList<NotificationButtonSpec> buttonSpecs = modeless
                ? new List<NotificationButtonSpec>()
                : BuildButtons(buttons, yesText, noText);
            _escapeResult = ResolveEscapeResult(buttonSpecs);
            Result = _escapeResult;

            Grid header = BuildHeader(kind);
            Body.Children.Add(header);

            var messageBlock = new TextBlock
            {
                Text = NormalizeMessage(message),
                Foreground = Brush("#334155"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20,
                FontSize = 12.5
            };
            Body.Children.Add(new Border
            {
                Background = ResolveMessageBackground(kind),
                BorderBrush = ResolveMessageBorder(kind),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(11, 9, 11, 9),
                Margin = new Thickness(0, 0, 0, modeless ? 0 : 10),
                Child = new ScrollViewer
                {
                    Content = messageBlock,
                    MaxHeight = 310,
                    VerticalScrollBarVisibility =
                        ScrollBarVisibility.Auto
                }
            });

            if (showDoNotAsk)
            {
                _doNotAskCheckBox = new CheckBox
                {
                    Content = "以后不再提示",
                    Foreground = Brush("#52647D"),
                    Margin = new Thickness(1, 1, 0, 10)
                };
                Body.Children.Add(_doNotAskCheckBox);
            }

            if (buttonSpecs.Count > 0)
                Body.Children.Add(BuildButtonBar(buttonSpecs, defaultButton));

            Loaded += WindowLoaded;
            UserMoved += delegate { SavePosition(); };
            Closed += delegate
            {
                if (_autoCloseTimer != null) _autoCloseTimer.Stop();
                SavePosition();
            };

            if (_autoClose)
            {
                _autoCloseRemaining = AutoCloseDuration;
                _autoCloseTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200)
                };
                _autoCloseTimer.Tick += delegate
                {
                    UpdateAutoCloseCountdown();
                };
                MouseEnter += delegate
                {
                    PauseAutoCloseCountdown();
                };
                MouseLeave += delegate
                {
                    ResumeAutoCloseCountdown();
                };
            }
        }

        public WinForms.DialogResult Result { get; private set; }

        public bool DoNotAskAgain
        {
            get
            {
                return _doNotAskCheckBox != null
                    && _doNotAskCheckBox.IsChecked == true;
            }
        }

        public void RequestAnimatedClose()
        {
            if (_closeRequested) return;
            _closeRequested = true;
            if (_autoCloseTimer != null) _autoCloseTimer.Stop();
            Close();
        }

        protected override void OnEscapePressed()
        {
            if (_modeless)
            {
                RequestAnimatedClose();
                return;
            }
            Complete(_escapeResult);
        }

        private Grid BuildHeader(CDBoxNotificationKind kind)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            grid.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(34) });
            grid.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });
            if (_modeless)
            {
                if (_autoClose)
                    grid.ColumnDefinitions.Add(new ColumnDefinition
                        { Width = new GridLength(44) });
                grid.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = new GridLength(30) });
            }

            Border icon = BuildIcon(kind);
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var titleBlock = new TextBlock
            {
                Text = Title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#253550"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Cursor = Cursors.SizeAll
            };
            EnableDrag(titleBlock);
            Grid.SetColumn(titleBlock, 1);
            grid.Children.Add(titleBlock);

            if (_modeless)
            {
                int closeColumn = 2;
                if (_autoClose)
                {
                    _countdownTextBlock = new TextBlock
                    {
                        Text = FormatCountdown(AutoCloseDuration),
                        Foreground = Brush("#64748B"),
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = "自动关闭倒计时"
                    };
                    Grid.SetColumn(_countdownTextBlock, 2);
                    grid.Children.Add(_countdownTextBlock);
                    closeColumn = 3;
                }
                var close = new Button
                {
                    Content = "×",
                    Width = 26,
                    Height = 26,
                    Padding = new Thickness(0),
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    Foreground = Brush("#64748B"),
                    FontSize = 17,
                    Cursor = Cursors.Hand,
                    ToolTip = "关闭"
                };
                close.Click += delegate { RequestAnimatedClose(); };
                Grid.SetColumn(close, closeColumn);
                grid.Children.Add(close);
            }
            return grid;
        }

        private void ResumeAutoCloseCountdown()
        {
            if (!_autoClose || _autoCloseTimer == null || _closeRequested) return;
            if (_autoCloseRemaining <= TimeSpan.Zero)
            {
                RequestAnimatedClose();
                return;
            }
            _autoCloseDeadlineUtc = DateTime.UtcNow + _autoCloseRemaining;
            UpdateCountdownText(_autoCloseRemaining, false);
            _autoCloseTimer.Start();
        }

        private void PauseAutoCloseCountdown()
        {
            if (!_autoClose || _autoCloseTimer == null || _closeRequested) return;
            if (_autoCloseTimer.IsEnabled)
            {
                _autoCloseRemaining = _autoCloseDeadlineUtc - DateTime.UtcNow;
                if (_autoCloseRemaining < TimeSpan.Zero) _autoCloseRemaining = TimeSpan.Zero;
                _autoCloseTimer.Stop();
            }
            UpdateCountdownText(_autoCloseRemaining, true);
        }

        private void UpdateAutoCloseCountdown()
        {
            if (_closeRequested || _autoCloseTimer == null) return;
            _autoCloseRemaining = _autoCloseDeadlineUtc - DateTime.UtcNow;
            if (_autoCloseRemaining <= TimeSpan.Zero)
            {
                _autoCloseRemaining = TimeSpan.Zero;
                UpdateCountdownText(_autoCloseRemaining, false);
                _autoCloseTimer.Stop();
                RequestAnimatedClose();
                return;
            }
            UpdateCountdownText(_autoCloseRemaining, false);
        }

        private void UpdateCountdownText(TimeSpan remaining, bool paused)
        {
            if (_countdownTextBlock == null) return;
            _countdownTextBlock.Text = paused ? "暂停" : FormatCountdown(remaining);
        }

        private static string FormatCountdown(TimeSpan remaining)
        {
            int seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
            return seconds.ToString() + "s";
        }

        private static Border BuildIcon(CDBoxNotificationKind kind)
        {
            string symbol = "i";
            string background = "#E2EFFF";
            string foreground = "#2563EB";
            if (kind == CDBoxNotificationKind.Success)
            {
                symbol = "✓";
                background = "#DCFCE7";
                foreground = "#047857";
            }
            else if (kind == CDBoxNotificationKind.Warning)
            {
                symbol = "!";
                background = "#FEF3C7";
                foreground = "#B45309";
            }
            else if (kind == CDBoxNotificationKind.Error)
            {
                symbol = "×";
                background = "#FEE2E2";
                foreground = "#B91C1C";
            }
            else if (kind == CDBoxNotificationKind.Question)
            {
                symbol = "?";
                background = "#EDE9FE";
                foreground = "#6D28D9";
            }
            return new Border
            {
                Width = 27,
                Height = 27,
                CornerRadius = new CornerRadius(13.5),
                Background = Brush(background),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = symbol,
                    Foreground = Brush(foreground),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private UIElement BuildButtonBar(
            IList<NotificationButtonSpec> specs,
            WinForms.MessageBoxDefaultButton defaultButton)
        {
            var bar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            int defaultIndex = defaultButton
                == WinForms.MessageBoxDefaultButton.Button2 ? 1
                : (defaultButton
                    == WinForms.MessageBoxDefaultButton.Button3 ? 2 : 0);
            if (defaultIndex >= specs.Count) defaultIndex = 0;
            for (int i = specs.Count - 1; i >= 0; i--)
            {
                NotificationButtonSpec spec = specs[i];
                var button = new Button
                {
                    Content = spec.Text,
                    MinWidth = 82,
                    Height = 32,
                    Padding = new Thickness(13, 0, 13, 0),
                    Margin = new Thickness(8, 0, 0, 0),
                    FontWeight = FontWeights.SemiBold,
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(spec.Primary ? 0 : 1),
                    BorderBrush = Brush("#CFDAEA"),
                    Background = spec.Primary
                        ? Brush("#326FEA") : Brushes.White,
                    Foreground = spec.Primary
                        ? Brushes.White : Brush("#334155"),
                    IsDefault = i == defaultIndex
                };
                WinForms.DialogResult result = spec.Result;
                button.Click += delegate { Complete(result); };
                bar.Children.Add(button);
            }
            return bar;
        }

        private void Complete(WinForms.DialogResult result)
        {
            if (_closeRequested) return;
            _closeRequested = true;
            Result = result;
            CompleteDialogAnimated(result != WinForms.DialogResult.Cancel
                && result != WinForms.DialogResult.No);
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            double left;
            double top;
            bool remembered = CDBoxNotificationPositionStore.TryLoad(
                out left, out top);
            ShowAnimated(GetCursorScreenPosition(),
                remembered ? (double?)left : null,
                remembered ? (double?)top : null);
            if (!_modeless)
            {
                Activate();
                Focus();
            }
            if (_autoCloseTimer != null) ResumeAutoCloseCountdown();
        }

        private void SavePosition()
        {
            if (!HasInitializedPosition) return;
            double left;
            double top;
            if (TryGetRestingPosition(out left, out top))
                CDBoxNotificationPositionStore.Save(left, top);
        }

        private static IList<NotificationButtonSpec> BuildButtons(
            WinForms.MessageBoxButtons buttons, string yesText,
            string noText)
        {
            if (buttons == WinForms.MessageBoxButtons.OKCancel)
                return new List<NotificationButtonSpec>
                {
                    new NotificationButtonSpec("确定",
                        WinForms.DialogResult.OK, true),
                    new NotificationButtonSpec("取消",
                        WinForms.DialogResult.Cancel, false)
                };
            if (buttons == WinForms.MessageBoxButtons.YesNo)
                return new List<NotificationButtonSpec>
                {
                    new NotificationButtonSpec(
                        string.IsNullOrWhiteSpace(yesText) ? "是" : yesText,
                        WinForms.DialogResult.Yes, true),
                    new NotificationButtonSpec(
                        string.IsNullOrWhiteSpace(noText) ? "否" : noText,
                        WinForms.DialogResult.No, false)
                };
            if (buttons == WinForms.MessageBoxButtons.YesNoCancel)
                return new List<NotificationButtonSpec>
                {
                    new NotificationButtonSpec(
                        string.IsNullOrWhiteSpace(yesText) ? "是" : yesText,
                        WinForms.DialogResult.Yes, true),
                    new NotificationButtonSpec(
                        string.IsNullOrWhiteSpace(noText) ? "否" : noText,
                        WinForms.DialogResult.No, false),
                    new NotificationButtonSpec("取消",
                        WinForms.DialogResult.Cancel, false)
                };
            if (buttons == WinForms.MessageBoxButtons.RetryCancel)
                return new List<NotificationButtonSpec>
                {
                    new NotificationButtonSpec("重试",
                        WinForms.DialogResult.Retry, true),
                    new NotificationButtonSpec("取消",
                        WinForms.DialogResult.Cancel, false)
                };
            if (buttons == WinForms.MessageBoxButtons.AbortRetryIgnore)
                return new List<NotificationButtonSpec>
                {
                    new NotificationButtonSpec("中止",
                        WinForms.DialogResult.Abort, false),
                    new NotificationButtonSpec("重试",
                        WinForms.DialogResult.Retry, true),
                    new NotificationButtonSpec("忽略",
                        WinForms.DialogResult.Ignore, false)
                };
            return new List<NotificationButtonSpec>
            {
                new NotificationButtonSpec("确定",
                    WinForms.DialogResult.OK, true)
            };
        }

        private static WinForms.DialogResult ResolveEscapeResult(
            IList<NotificationButtonSpec> specs)
        {
            NotificationButtonSpec cancel = specs.FirstOrDefault(x =>
                x.Result == WinForms.DialogResult.Cancel);
            if (cancel != null) return cancel.Result;
            NotificationButtonSpec no = specs.FirstOrDefault(x =>
                x.Result == WinForms.DialogResult.No);
            if (no != null) return no.Result;
            return specs.Count > 0
                ? specs[0].Result : WinForms.DialogResult.Cancel;
        }

        private static double ResolveWidth(string message)
        {
            int length = string.IsNullOrWhiteSpace(message)
                ? 0 : message.Length;
            return length > 220 ? 520 : (length > 80 ? 470 : 420);
        }

        private static string NormalizeMessage(string value)
        {
            string message = (value ?? string.Empty)
                .Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            return string.IsNullOrWhiteSpace(message) ? "操作已完成。" : message;
        }

        private static Brush ResolveMessageBackground(
            CDBoxNotificationKind kind)
        {
            if (kind == CDBoxNotificationKind.Error) return Brush("#FFF7F7");
            if (kind == CDBoxNotificationKind.Warning) return Brush("#FFFBEB");
            if (kind == CDBoxNotificationKind.Success) return Brush("#F0FDF4");
            if (kind == CDBoxNotificationKind.Question) return Brush("#FAF5FF");
            return Brush("#F7FAFE");
        }

        private static Brush ResolveMessageBorder(CDBoxNotificationKind kind)
        {
            if (kind == CDBoxNotificationKind.Error) return Brush("#F1C9C9");
            if (kind == CDBoxNotificationKind.Warning) return Brush("#F4D6A2");
            if (kind == CDBoxNotificationKind.Success) return Brush("#BBE4C9");
            if (kind == CDBoxNotificationKind.Question) return Brush("#D8CCF2");
            return Brush("#D8E2EF");
        }

        private sealed class NotificationButtonSpec
        {
            public NotificationButtonSpec(string text,
                WinForms.DialogResult result, bool primary)
            {
                Text = text;
                Result = result;
                Primary = primary;
            }

            public string Text { get; private set; }
            public WinForms.DialogResult Result { get; private set; }
            public bool Primary { get; private set; }
        }
    }
}
