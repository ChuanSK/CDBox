using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;

namespace TCPipeAutoDraw.Modules.AnnotationHud
{
    /// <summary>
    /// Shared visual shell for CDBox annotation HUDs. It owns the borderless window,
    /// opacity/glow settings, edge constraints, dragging and popup animation.
    /// Annotation-specific windows only build their fields inside Body.
    /// </summary>
    internal abstract class AnnotationHudWindowBase : Window
    {
        private readonly Border _animationRoot;
        private readonly DropShadowEffect _glowEffect;
        private readonly Brush _normalBorderBrush;
        private readonly Brush _hoverBorderBrush;
        private readonly AnnotationHudPopupAnimationController _popupAnimation;
        private bool _adjustingLocation;
        private bool _animationActive;
        private bool _closeAnimationInProgress;
        private bool _allowImmediateClose;
        private Point _animationOrigin;
        private double _restingLeft;
        private double _restingTop;
        private bool _hasRestingPosition;
        private double _normalOpacity = 0.68;
        private double _hoverOpacity = 1.0;

        protected StackPanel Body { get; private set; }
        protected Border AnimationRoot { get { return _animationRoot; } }
        public event EventHandler UserMoved;
        public bool HasInitializedPosition { get; private set; }
        public bool IsClosingAnimation { get { return _closeAnimationInProgress; } }

        protected AnnotationHudWindowBase(string title, double width)
        {
            Title = title ?? "CDBox 标注浮窗";
            Width = width;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 720;
            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = false;
            Opacity = _normalOpacity;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 12.5;

            _glowEffect = new DropShadowEffect
            {
                BlurRadius = 22,
                ShadowDepth = 0,
                Direction = 0,
                Opacity = 0.28,
                Color = Color.FromRgb(66, 139, 255)
            };
            _normalBorderBrush = Brush("#B8C8DAEE");
            _hoverBorderBrush = Brush("#FF73A9F5");
            _animationRoot = new Border
            {
                Background = Brush("#FFFFFFFF"),
                BorderBrush = _normalBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(14),
                Effect = _glowEffect,
                UseLayoutRounding = true
            };
            Content = _animationRoot;
            Body = new StackPanel();
            _animationRoot.Child = Body;
            _popupAnimation = new AnnotationHudPopupAnimationController(this, _animationRoot);

            PreviewKeyDown += WindowPreviewKeyDown;
            MouseEnter += delegate { SetHudHover(true); };
            MouseLeave += delegate { SetHudHover(false); };
            LocationChanged += delegate { WindowGeometryChanged(); };
            SizeChanged += delegate { WindowGeometryChanged(); };
        }

        public void ApplyAppearance(double normalOpacity, double hoverOpacity,
            bool glowEnabled, double glowIntensity)
        {
            _normalOpacity = Clamp(normalOpacity, 0.20, 1.0);
            _hoverOpacity = Clamp(hoverOpacity, 0.20, 1.0);
            _glowEffect.Opacity = glowEnabled ? Clamp(glowIntensity, 0.0, 1.0) : 0.0;
            Opacity = IsMouseOver ? _hoverOpacity : _normalOpacity;
        }

        protected void EnableDrag(UIElement surface)
        {
            if (surface != null) surface.MouseLeftButtonDown += DragHud;
        }

        public void ShowAnimated(Point origin, double? rememberedLeft, double? rememberedTop)
        {
            _animationOrigin = origin;
            _animationRoot.BorderBrush = _normalBorderBrush;
            _popupAnimation.PrepareForOpen();
            if (!IsVisible) Show();
            UpdateLayout();
            InitializePosition(origin, rememberedLeft, rememberedTop);
            Left = _restingLeft;
            Top = _restingTop;
            _animationActive = true;
            _closeAnimationInProgress = false;
            _animationRoot.IsHitTestVisible = false;
            _popupAnimation.Open(origin, new Point(_restingLeft, _restingTop), delegate
            {
                _animationActive = false;
                _animationRoot.IsHitTestVisible = true;
                ConstrainAndSnapToWorkingArea();
                CaptureRestingPosition();
                SetHudHover(IsMouseOver);
            });
        }

        public void SetAnimationOrigin(Point origin)
        {
            _animationOrigin = origin;
        }

        public void HideAnimated(Point origin)
        {
            _animationOrigin = origin;
            if (IsVisible) BeginCloseAnimation(false);
        }

        public void CloseImmediately()
        {
            _allowImmediateClose = true;
            _popupAnimation.CancelAndReset();
            Close();
        }

        public bool TryGetRestingPosition(out double left, out double top)
        {
            left = _restingLeft;
            top = _restingTop;
            return _hasRestingPosition;
        }

        public Point GetCursorScreenPosition()
        {
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            double scaleX = dpi.DpiScaleX <= 0.0 ? 1.0 : dpi.DpiScaleX;
            double scaleY = dpi.DpiScaleY <= 0.0 ? 1.0 : dpi.DpiScaleY;
            System.Drawing.Point cursor = System.Windows.Forms.Cursor.Position;
            return new Point(cursor.X / scaleX, cursor.Y / scaleY);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowImmediateClose && IsVisible)
            {
                e.Cancel = true;
                BeginCloseAnimation(true);
                return;
            }
            base.OnClosing(e);
        }

        protected virtual void OnEscapePressed()
        {
            Close();
        }

        protected void CompleteDialog(bool accepted)
        {
            _allowImmediateClose = true;
            _popupAnimation.CancelAndReset();
            DialogResult = accepted;
        }

        protected static SolidColorBrush Brush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private void SetHudHover(bool hovered)
        {
            Opacity = hovered ? _hoverOpacity : _normalOpacity;
            _animationRoot.BorderBrush = hovered ? _hoverBorderBrush : _normalBorderBrush;
            if (!_animationActive) _popupAnimation.SetHoverState(hovered);
        }

        private void InitializePosition(Point origin, double? rememberedLeft, double? rememberedTop)
        {
            if (HasInitializedPosition) return;
            HasInitializedPosition = true;
            Left = rememberedLeft ?? origin.X + 20.0;
            Top = rememberedTop ?? origin.Y + 20.0;
            ConstrainAndSnapToWorkingArea();
            CaptureRestingPosition();
            Opacity = IsMouseOver ? _hoverOpacity : _normalOpacity;
        }

        private void BeginCloseAnimation(bool closeWindow)
        {
            if (_closeAnimationInProgress || !IsVisible) return;
            if (!_animationActive) CaptureRestingPosition();
            _animationRoot.BorderBrush = _normalBorderBrush;
            _animationActive = true;
            _closeAnimationInProgress = true;
            _animationRoot.IsHitTestVisible = false;
            _popupAnimation.Close(_animationOrigin, delegate
            {
                if (closeWindow)
                {
                    _allowImmediateClose = true;
                    Close();
                    return;
                }
                Hide();
                _popupAnimation.CancelAndReset();
                Left = _restingLeft;
                Top = _restingTop;
                _closeAnimationInProgress = false;
                _animationActive = false;
                _animationRoot.IsHitTestVisible = true;
            });
        }

        private void DragHud(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || _animationActive) return;
            e.Handled = true;
            try
            {
                Opacity = _hoverOpacity;
                DragMove();
            }
            catch (InvalidOperationException) { }
            finally
            {
                ConstrainAndSnapToWorkingArea();
                CaptureRestingPosition();
                EventHandler handler = UserMoved;
                if (handler != null) handler(this, EventArgs.Empty);
                SetHudHover(IsMouseOver);
            }
        }

        private void ConstrainAndSnapToWorkingArea()
        {
            if (_adjustingLocation || _animationActive || !IsLoaded || double.IsNaN(Left) || double.IsNaN(Top)) return;
            try
            {
                _adjustingLocation = true;
                DpiScale dpi = VisualTreeHelper.GetDpi(this);
                double scaleX = dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
                double scaleY = dpi.DpiScaleY <= 0 ? 1.0 : dpi.DpiScaleY;
                double width = ActualWidth > 1 ? ActualWidth : Width;
                double height = ActualHeight > 1 ? ActualHeight : 360.0;
                var center = new System.Drawing.Point(
                    Convert.ToInt32((Left + width / 2.0) * scaleX),
                    Convert.ToInt32((Top + height / 2.0) * scaleY));
                System.Drawing.Rectangle pixels = System.Windows.Forms.Screen.FromPoint(center).WorkingArea;
                double workLeft = pixels.Left / scaleX;
                double workTop = pixels.Top / scaleY;
                double workRight = pixels.Right / scaleX;
                double workBottom = pixels.Bottom / scaleY;
                double left = Math.Max(workLeft, Math.Min(Left, workRight - width));
                double top = Math.Max(workTop, Math.Min(Top, workBottom - height));
                const double snapDistance = 16.0;
                if (Math.Abs(left - workLeft) <= snapDistance) left = workLeft;
                if (Math.Abs(workRight - (left + width)) <= snapDistance) left = workRight - width;
                if (Math.Abs(top - workTop) <= snapDistance) top = workTop;
                if (Math.Abs(workBottom - (top + height)) <= snapDistance) top = workBottom - height;
                if (Math.Abs(Left - left) > 0.1) Left = left;
                if (Math.Abs(Top - top) > 0.1) Top = top;
            }
            catch { }
            finally { _adjustingLocation = false; }
        }

        private void WindowGeometryChanged()
        {
            if (_animationActive) return;
            ConstrainAndSnapToWorkingArea();
            CaptureRestingPosition();
        }

        private void CaptureRestingPosition()
        {
            if (_animationActive || double.IsNaN(Left) || double.IsInfinity(Left)
                || double.IsNaN(Top) || double.IsInfinity(Top)) return;
            _restingLeft = Left;
            _restingTop = Top;
            _hasRestingPosition = true;
        }

        private void WindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || e.Handled) return;
            e.Handled = true;
            OnEscapePressed();
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return minimum;
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
