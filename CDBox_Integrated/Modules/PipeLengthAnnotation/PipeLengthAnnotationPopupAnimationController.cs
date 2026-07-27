using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    /// <summary>
    /// Owns only the visual transition of the annotation HUD. CAD object lookup,
    /// persisted position and window lifetime stay in their existing owners.
    /// </summary>
    internal sealed class AnnotationHudPopupAnimationController
    {
        private const double CollapsedScale = 0.08;
        private static readonly Duration OpenDuration = new Duration(TimeSpan.FromMilliseconds(240));
        private static readonly Duration CloseDuration = new Duration(TimeSpan.FromMilliseconds(180));
        private readonly Window _window;
        private readonly FrameworkElement _visual;
        private readonly ScaleTransform _scale;
        private readonly TranslateTransform _translate;
        private int _animationVersion;
        private int _hoverAnimationVersion;

        public AnnotationHudPopupAnimationController(Window window, FrameworkElement visual)
        {
            _window = window ?? throw new ArgumentNullException("window");
            _visual = visual ?? throw new ArgumentNullException("visual");
            _scale = new ScaleTransform(1.0, 1.0);
            _translate = new TranslateTransform();
            var transforms = new TransformGroup();
            transforms.Children.Add(_scale);
            transforms.Children.Add(_translate);
            _visual.RenderTransform = transforms;
            _visual.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        public void PrepareForOpen()
        {
            CancelAnimations();
            EnableAnimationCache();
            _scale.ScaleX = CollapsedScale;
            _scale.ScaleY = CollapsedScale;
            _translate.X = 0.0;
            _translate.Y = 0.0;
            _visual.Opacity = 0.0;
        }

        public void Open(Point origin, Point target, Action completed)
        {
            int version = ++_animationVersion;
            StopAnimationClocks();

            double width = ResolveWidth();
            double height = ResolveHeight();
            double startLeft = origin.X - width / 2.0;
            double startTop = origin.Y - height / 2.0;
            _window.Left = startLeft;
            _window.Top = startTop;
            EnableAnimationCache();
            _scale.ScaleX = CollapsedScale;
            _scale.ScaleY = CollapsedScale;
            _translate.X = 0.0;
            _translate.Y = 0.0;
            _visual.Opacity = 0.0;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Begin(_window, Window.LeftProperty, startLeft, target.X, OpenDuration, ease);
            Begin(_window, Window.TopProperty, startTop, target.Y, OpenDuration, ease);
            Begin(_scale, ScaleTransform.ScaleXProperty, CollapsedScale, 1.0, OpenDuration, ease);
            Begin(_scale, ScaleTransform.ScaleYProperty, CollapsedScale, 1.0, OpenDuration, ease);

            var opacity = CreateAnimation(0.0, 1.0, OpenDuration, ease);
            opacity.Completed += delegate
            {
                if (version != _animationVersion) return;
                StopAnimationClocks();
                _window.Left = target.X;
                _window.Top = target.Y;
                ResetVisual();
                if (completed != null) completed();
            };
            _visual.BeginAnimation(UIElement.OpacityProperty, opacity, HandoffBehavior.SnapshotAndReplace);
        }

        public void Close(Point origin, Action completed)
        {
            int version = ++_animationVersion;
            EnableAnimationCache();
            double currentLeft = _window.Left;
            double currentTop = _window.Top;
            double currentScaleX = _scale.ScaleX;
            double currentScaleY = _scale.ScaleY;
            double currentTranslateX = _translate.X;
            double currentTranslateY = _translate.Y;
            double currentOpacity = _visual.Opacity;
            StopAnimationClocks();

            _window.Left = currentLeft;
            _window.Top = currentTop;
            _scale.ScaleX = currentScaleX;
            _scale.ScaleY = currentScaleY;
            _translate.X = currentTranslateX;
            _translate.Y = currentTranslateY;
            _visual.Opacity = currentOpacity;

            double endLeft = origin.X - ResolveWidth() / 2.0;
            double endTop = origin.Y - ResolveHeight() / 2.0;
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            Begin(_window, Window.LeftProperty, currentLeft, endLeft, CloseDuration, ease);
            Begin(_window, Window.TopProperty, currentTop, endTop, CloseDuration, ease);
            Begin(_scale, ScaleTransform.ScaleXProperty, currentScaleX, CollapsedScale, CloseDuration, ease);
            Begin(_scale, ScaleTransform.ScaleYProperty, currentScaleY, CollapsedScale, CloseDuration, ease);
            Begin(_translate, TranslateTransform.XProperty, currentTranslateX, 0.0, CloseDuration, ease);
            Begin(_translate, TranslateTransform.YProperty, currentTranslateY, 0.0, CloseDuration, ease);

            var opacity = CreateAnimation(currentOpacity, 0.0, CloseDuration, ease);
            opacity.Completed += delegate
            {
                if (version != _animationVersion) return;
                StopAnimationClocks();
                _window.Left = endLeft;
                _window.Top = endTop;
                _scale.ScaleX = CollapsedScale;
                _scale.ScaleY = CollapsedScale;
                _visual.Opacity = 0.0;
                if (completed != null) completed();
            };
            _visual.BeginAnimation(UIElement.OpacityProperty, opacity, HandoffBehavior.SnapshotAndReplace);
        }

        public void CancelAndReset()
        {
            CancelAnimations();
            ResetVisual();
        }

        public void SetHoverState(bool hovered)
        {
            int version = ++_hoverAnimationVersion;
            double current = _translate.Y;
            _translate.BeginAnimation(TranslateTransform.YProperty, null);
            _translate.Y = current;
            double target = hovered ? -3.0 : 0.0;
            if (Math.Abs(current - target) < 0.05)
            {
                _translate.Y = target;
                return;
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var animation = CreateAnimation(current, target,
                new Duration(TimeSpan.FromMilliseconds(140)), ease);
            animation.Completed += delegate
            {
                if (version != _hoverAnimationVersion) return;
                _translate.BeginAnimation(TranslateTransform.YProperty, null);
                _translate.Y = target;
            };
            _translate.BeginAnimation(TranslateTransform.YProperty, animation,
                HandoffBehavior.SnapshotAndReplace);
        }

        private void CancelAnimations()
        {
            _animationVersion++;
            StopAnimationClocks();
        }

        private void StopAnimationClocks()
        {
            _hoverAnimationVersion++;
            _window.BeginAnimation(Window.LeftProperty, null);
            _window.BeginAnimation(Window.TopProperty, null);
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _translate.BeginAnimation(TranslateTransform.XProperty, null);
            _translate.BeginAnimation(TranslateTransform.YProperty, null);
            _visual.BeginAnimation(UIElement.OpacityProperty, null);
        }

        private void ResetVisual()
        {
            _scale.ScaleX = 1.0;
            _scale.ScaleY = 1.0;
            _translate.X = 0.0;
            _translate.Y = 0.0;
            _visual.Opacity = 1.0;
            _visual.CacheMode = null;
        }

        private void EnableAnimationCache()
        {
            if (_visual.CacheMode == null)
            {
                _visual.CacheMode = new BitmapCache { RenderAtScale = 1.0 };
            }
        }

        private double ResolveWidth()
        {
            if (_window.ActualWidth > 1.0) return _window.ActualWidth;
            return !double.IsNaN(_window.Width) && _window.Width > 1.0 ? _window.Width : 330.0;
        }

        private double ResolveHeight()
        {
            if (_window.ActualHeight > 1.0) return _window.ActualHeight;
            return !double.IsNaN(_window.Height) && _window.Height > 1.0 ? _window.Height : 360.0;
        }

        private static DoubleAnimation CreateAnimation(double from, double to, Duration duration, IEasingFunction ease)
        {
            return new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = duration,
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            };
        }

        private static void Begin(DependencyObject target, DependencyProperty property, double from, double to, Duration duration, IEasingFunction ease)
        {
            var animation = CreateAnimation(from, to, duration, ease);
            if (target is Window window)
            {
                window.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
                return;
            }
            if (target is Animatable animatable)
            {
                animatable.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
                return;
            }
            throw new InvalidOperationException("动画目标不支持 WPF 动画。");
        }

    }
}
