using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using TCPipeAutoDraw.Modules.AnnotationHud;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace TCPipeAutoDraw.UI.Studio
{
    /// <summary>
    /// 图框流程的统一 WPF 提示浮窗。
    /// </summary>
    internal sealed class CDBoxStudioFrameNoticeWindow :
        AnnotationHudWindowBase
    {
        private CDBoxStudioFrameNoticeWindow(string title,
            IList<string> messages)
            : base(string.IsNullOrWhiteSpace(title) ? "图框提示" : title, 440)
        {
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
            foreach (string message in messages
                .Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                stack.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(7),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brush("#F1C9C9"),
                    Background = Brush("#FFF7F7"),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 6),
                    Child = new TextBlock
                    {
                        Text = message,
                        Foreground = Brush("#8B3038"),
                        TextWrapping = TextWrapping.Wrap
                    }
                });
            }
            Body.Children.Add(new ScrollViewer
            {
                Content = stack,
                MaxHeight = 360,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            });

            var confirm = new Button
            {
                Content = "知道了",
                MinWidth = 82,
                Height = 32,
                Margin = new Thickness(0, 5, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                FontWeight = FontWeights.SemiBold
            };
            confirm.Click += delegate { CompleteDialogAnimated(true); };
            Body.Children.Add(confirm);

            UserMoved += delegate { SavePosition(this); };
            Loaded += WindowLoaded;
            Closed += delegate { SavePosition(this); };
        }

        public static void ShowNotice(string title, IList<string> messages,
            System.Windows.Forms.IWin32Window owner = null)
        {
            IList<string> normalized = (messages ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (normalized.Count == 0) return;
            var window = new CDBoxStudioFrameNoticeWindow(title, normalized);
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
                AcadApp.ShowModalWindow(window);
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
            CDBoxStudioFrameNoticeWindow window)
        {
            if (window == null || !window.HasInitializedPosition) return;
            double left;
            double top;
            if (window.TryGetRestingPosition(out left, out top))
                CDBoxStudioFrameChoicePositionStore.Save(left, top);
        }
    }
}
