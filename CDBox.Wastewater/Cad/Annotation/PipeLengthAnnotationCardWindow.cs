using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Autodesk.AutoCAD.DatabaseServices;
using TCPipeAutoDraw.Core.Colors;
using TCPipeAutoDraw.UI.Controls;
using TCPipeAutoDraw.Modules.AnnotationHud;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    /// <summary>
    /// Lightweight contextual HUD for a selected pipe length annotation.
    /// It intentionally has no title bar, drag surface, resize border or Apply button.
    /// </summary>
    internal sealed class PipeLengthAnnotationCardWindow : Window
    {
        private readonly TextBlock _bindingInfo;
        private readonly TextBox _userText;
        private readonly TextBlock _systemLength;
        private readonly Grid _bottomRow;
        private readonly TextBox _bottomText;
        private readonly Button _bindingAction;
        private readonly Button _moreToggle;
        private readonly StackPanel _morePanel;
        private readonly CheckBox _bottomEnabled;
        private readonly ComboBox _textStyle;
        private readonly TextBox _textHeight;
        private readonly ComboBox _layer;
        private readonly CDBoxColorPicker _textColor;
        private readonly CDBoxColorPicker _leaderColor;
        private readonly ComboBox _linetype;
        private readonly ComboBox _lineWeight;
        private readonly TextBlock _status;
        private readonly Border _animationRoot;
        private readonly DropShadowEffect _glowEffect;
        private readonly Brush _normalBorderBrush;
        private readonly Brush _hoverBorderBrush;
        private readonly AnnotationHudPopupAnimationController _popupAnimation;
        private PipeLengthAnnotationEditModel _model;
        private bool _binding;
        private bool _committing;
        private bool _adjustingLocation;
        private bool _animationActive;
        private bool _closeAnimationInProgress;
        private bool _allowImmediateClose;
        private Point _animationOrigin;
        private double _restingLeft;
        private double _restingTop;
        private bool _hasRestingPosition;
        private Control _focusedEditor;
        private string _focusedValue;
        private double _normalOpacity = 0.68;
        private double _hoverOpacity = 1.0;

        public Func<PipeLengthAnnotationEditModel, PipeLengthAnnotationEditModel> CommitHandler { get; set; }
        public Func<PipeLengthAnnotationEditModel, PipeLengthAnnotationEditModel> BindingHandler { get; set; }
        public event EventHandler UserMoved;
        public bool HasInitializedPosition { get; private set; }
        public bool IsClosingAnimation { get { return _closeAnimationInProgress; } }

        public PipeLengthAnnotationCardWindow()
        {
            Title = "管线长度标注";
            Width = 330;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 680;
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
            _popupAnimation = new AnnotationHudPopupAnimationController(this, _animationRoot);

            var root = new StackPanel();
            _animationRoot.Child = root;

            _bindingInfo = new TextBlock
            {
                Foreground = Brush("#53637A"),
                FontSize = 11.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(1, 0, 1, 6),
                Padding = new Thickness(0, 1, 0, 2),
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeAll,
                ToolTip = "拖动浮窗"
            };
            _bindingInfo.MouseLeftButtonDown += DragHud;
            root.Children.Add(_bindingInfo);

            var topRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _userText = CreateTextBox();
            _userText.MinWidth = 90;
            topRow.Children.Add(_userText);
            _systemLength = new TextBlock
            {
                MinWidth = 44,
                MinHeight = 28,
                Padding = new Thickness(7, 4, 7, 4),
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("#244A84"),
                Background = Brush("#EAF1FB"),
                ToolTip = "系统长度随绑定对象自动更新"
            };
            Grid.SetColumn(_systemLength, 1);
            topRow.Children.Add(_systemLength);
            root.Children.Add(topRow);

            _bottomRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            _bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _bottomText = CreateTextBox();
            _bottomText.ToolTip = "下侧补充标注";
            _bottomText.AcceptsReturn = true;
            _bottomText.TextWrapping = TextWrapping.Wrap;
            _bottomText.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _bottomText.VerticalContentAlignment = VerticalAlignment.Top;
            _bottomText.MinHeight = 52;
            _bottomText.MaxHeight = 112;
            _bottomRow.Children.Add(_bottomText);
            root.Children.Add(_bottomRow);

            var actions = new Grid { Margin = new Thickness(0, 1, 0, 0) };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _moreToggle = CreateLinkButton("更多编辑  ▾");
            _moreToggle.HorizontalAlignment = HorizontalAlignment.Left;
            _moreToggle.Click += ToggleMore;
            actions.Children.Add(_moreToggle);
            _bindingAction = CreateLinkButton("解除绑定");
            _bindingAction.HorizontalAlignment = HorizontalAlignment.Right;
            _bindingAction.Click += BindingActionClick;
            Grid.SetColumn(_bindingAction, 1);
            actions.Children.Add(_bindingAction);
            root.Children.Add(actions);

            _morePanel = new StackPanel
            {
                Visibility = System.Windows.Visibility.Collapsed,
                Margin = new Thickness(0, 6, 0, 0)
            };
            root.Children.Add(_morePanel);
            var divider = new Border
            {
                Height = 1,
                Background = Brush("#DCE5F1"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            _morePanel.Children.Add(divider);

            var options = new Grid();
            options.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            options.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 8; i++) options.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _morePanel.Children.Add(options);

            _textStyle = AddComboRow(options, 0, "文字样式");
            _textHeight = AddTextBoxRow(options, 1, "文字高度");
            _textColor = AddColorPickerRow(options, 2, "文字颜色");
            _leaderColor = AddColorPickerRow(options, 3, "引线颜色");
            _linetype = AddComboRow(options, 4, "引线线型");
            _lineWeight = AddComboRow(options, 5, "引线线宽");
            _layer = AddComboRow(options, 6, "标注图层");
            _bottomEnabled = new CheckBox
            {
                Content = "启用下侧标注",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 2),
                Foreground = Brush("#31415A")
            };
            Grid.SetRow(_bottomEnabled, 7);
            Grid.SetColumn(_bottomEnabled, 1);
            options.Children.Add(_bottomEnabled);

            _lineWeight.DisplayMemberPath = "Name";
            _lineWeight.ItemsSource = BuildLineWeightOptions();

            _status = new TextBlock
            {
                Visibility = System.Windows.Visibility.Collapsed,
                Margin = new Thickness(1, 5, 1, 0),
                Foreground = Brush("#B43A3A"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap
            };
            root.Children.Add(_status);

            HookTextEditor(_userText);
            HookTextEditor(_bottomText);
            HookTextEditor(_textHeight);
            HookImmediateCombo(_textStyle);
            _textColor.SelectedColorChanged += ColorChanged;
            _leaderColor.SelectedColorChanged += ColorChanged;
            HookImmediateCombo(_linetype);
            HookImmediateCombo(_lineWeight);
            HookImmediateCombo(_layer);
            _bottomEnabled.Checked += BottomEnabledChanged;
            _bottomEnabled.Unchecked += BottomEnabledChanged;
            PreviewKeyDown += WindowPreviewKeyDown;
            MouseEnter += delegate { SetHudHover(true); };
            MouseLeave += delegate { SetHudHover(false); };
            LocationChanged += delegate { WindowGeometryChanged(); };
            SizeChanged += delegate { WindowGeometryChanged(); };
        }

        public string AnnotationId { get { return _model == null ? string.Empty : _model.AnnotationId; } }

        public void ApplyAppearance(double normalOpacity, double hoverOpacity,
            bool glowEnabled, double glowIntensity)
        {
            _normalOpacity = Clamp(normalOpacity, 0.20, 1.0);
            _hoverOpacity = Clamp(hoverOpacity, 0.20, 1.0);
            _glowEffect.Opacity = glowEnabled ? Clamp(glowIntensity, 0.0, 1.0) : 0.0;
            Opacity = IsMouseOver ? _hoverOpacity : _normalOpacity;
        }

        private void SetHudHover(bool hovered)
        {
            Opacity = hovered ? _hoverOpacity : _normalOpacity;
            _animationRoot.BorderBrush = hovered ? _hoverBorderBrush : _normalBorderBrush;
            if (!_animationActive) _popupAnimation.SetHoverState(hovered);
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
            if (!IsVisible) return;
            BeginCloseAnimation(false);
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

        private void InitializePosition(Point origin, double? rememberedLeft, double? rememberedTop)
        {
            if (HasInitializedPosition) return;
            HasInitializedPosition = true;
            if (rememberedLeft.HasValue && rememberedTop.HasValue)
            {
                Left = rememberedLeft.Value;
                Top = rememberedTop.Value;
            }
            else
            {
                Left = origin.X + 20.0;
                Top = origin.Y + 20.0;
            }
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

        public void SetModel(PipeLengthAnnotationEditModel model)
        {
            _binding = true;
            try
            {
                _model = model;
                if (model == null) return;
                _userText.Text = model.UserText ?? string.Empty;
                _systemLength.Text = model.SystemLengthText ?? string.Empty;
                _systemLength.Visibility = model.IsBound && !string.IsNullOrWhiteSpace(model.SystemLengthText)
                    ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                _bottomText.Text = model.BottomText ?? string.Empty;
                _bottomEnabled.IsChecked = model.HasBottomAnnotation;
                _bottomRow.Visibility = model.HasBottomAnnotation ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                _textHeight.Text = model.TextHeight.ToString("0.###", CultureInfo.InvariantCulture);
                SetStringItems(_textStyle, model.TextStyleNames, model.TextStyleName);
                SetStringItems(_layer, model.LayerNames, model.LayerName);
                SetStringItems(_linetype, model.LinetypeNames, model.LinetypeName);
                _textColor.SelectedColor = model.TextColor ?? CDBoxColor.FromIndex(model.TextColorIndex);
                _leaderColor.SelectedColor = model.LeaderColor ?? CDBoxColor.FromIndex(model.LeaderColorIndex);
                SelectLineWeight(model.LineWeight);
                UpdateBindingInfo(model);
                SetStatus(string.Empty, false);
            }
            finally
            {
                _binding = false;
            }
        }

        public void SetStatus(string message, bool isError)
        {
            _status.Text = message ?? string.Empty;
            _status.Foreground = isError ? Brush("#B43A3A") : Brush("#3B6B4A");
            _status.Visibility = string.IsNullOrWhiteSpace(message) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        private void UpdateBindingInfo(PipeLengthAnnotationEditModel model)
        {
            if (!model.IsBound)
            {
                _bindingInfo.Text = "未绑定对象";
                _bindingInfo.Foreground = Brush("#7A5A30");
                _bindingAction.Content = "重新绑定";
                return;
            }
            string layer = string.IsNullOrWhiteSpace(model.SourceLayerName) ? "不可用" : model.SourceLayerName;
            _bindingInfo.Text = "绑定对象：" + layer;
            _bindingInfo.Foreground = model.BindingIsValid ? Brush("#53637A") : Brush("#B43A3A");
            _bindingAction.Content = "解除绑定";
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
                EventHandler moved = UserMoved;
                if (moved != null) moved(this, EventArgs.Empty);
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

        private void ToggleMore(object sender, RoutedEventArgs e)
        {
            bool expanded = _morePanel.Visibility != System.Windows.Visibility.Visible;
            _morePanel.Visibility = expanded ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            _moreToggle.Content = expanded ? "收起编辑  ▴" : "更多编辑  ▾";
        }

        private void BindingActionClick(object sender, RoutedEventArgs e)
        {
            if (_model == null || BindingHandler == null || _committing) return;
            try
            {
                _committing = true;
                PipeLengthAnnotationEditModel saved = BindingHandler(ReadModel());
                if (saved != null) SetModel(saved);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
            finally { _committing = false; }
        }

        private void HookTextEditor(TextBox textBox)
        {
            textBox.GotKeyboardFocus += EditorGotKeyboardFocus;
            textBox.LostKeyboardFocus += EditorLostKeyboardFocus;
            textBox.PreviewKeyDown += EditorPreviewKeyDown;
        }

        private void HookImmediateCombo(ComboBox combo)
        {
            combo.SelectionChanged += delegate
            {
                if (!_binding && !_committing && IsLoaded) CommitCurrentModel();
            };
        }

        private void ColorChanged(object sender, EventArgs e)
        {
            if (!_binding && !_committing && IsLoaded) CommitCurrentModel();
        }

        private void EditorGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _focusedEditor = sender as Control;
            _focusedValue = ReadEditorValue(_focusedEditor);
        }

        private void EditorLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_binding || _committing) return;
            Control editor = sender as Control;
            if (editor != null && !string.Equals(_focusedValue, ReadEditorValue(editor), StringComparison.Ordinal))
            {
                CommitCurrentModel();
            }
            _focusedEditor = null;
            _focusedValue = null;
        }

        private void EditorPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ReferenceEquals(sender, _bottomText)
                    && (Keyboard.Modifiers & ModifierKeys.Control) == 0)
                {
                    return;
                }
                e.Handled = true;
                CommitCurrentModel();
                Keyboard.ClearFocus();
            }
            else if (e.Key == Key.Escape)
            {
                Control editor = sender as Control;
                string current = ReadEditorValue(editor);
                if (_focusedValue != null && !string.Equals(current, _focusedValue, StringComparison.Ordinal))
                {
                    WriteEditorValue(editor, _focusedValue);
                    e.Handled = true;
                }
            }
        }

        private void WindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || e.Handled) return;
            e.Handled = true;
            Close();
        }

        private void BottomEnabledChanged(object sender, RoutedEventArgs e)
        {
            if (_binding) return;
            bool enabled = _bottomEnabled.IsChecked == true;
            _bottomRow.Visibility = enabled ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            if (enabled)
            {
                _bottomText.Focus();
                return;
            }
            _bottomText.Text = string.Empty;
            CommitCurrentModel();
        }

        private void CommitCurrentModel()
        {
            if (_binding || _committing || _model == null || CommitHandler == null) return;
            try
            {
                _committing = true;
                PipeLengthAnnotationEditModel saved = CommitHandler(ReadModel());
                if (saved != null) SetModel(saved);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
            finally { _committing = false; }
        }

        private PipeLengthAnnotationEditModel ReadModel()
        {
            double height;
            string rawHeight = (_textHeight.Text ?? string.Empty).Trim().Replace(',', '.');
            if (!double.TryParse(rawHeight, NumberStyles.Float, CultureInfo.InvariantCulture, out height) || height <= 0.0)
            {
                throw new InvalidOperationException("请输入有效的文字高度。 ");
            }
            CDBoxColor textColor = _textColor.SelectedColor;
            CDBoxColor leaderColor = _leaderColor.SelectedColor;
            var weight = _lineWeight.SelectedItem as LineWeightOption;
            bool hasBottom = _bottomEnabled.IsChecked == true;
            string userText = _userText.Text ?? string.Empty;
            return new PipeLengthAnnotationEditModel
            {
                SelectedObjectId = _model.SelectedObjectId,
                AnnotationId = _model.AnnotationId,
                UserText = userText,
                SystemLengthText = _model.IsBound ? _model.SystemLengthText : string.Empty,
                DetachedLengthToken = _model.DetachedLengthToken,
                TopText = _model.IsBound
                    ? PipeLengthAnnotationTextComposer.Compose(userText, _model.SystemLengthText)
                    : userText,
                BottomText = hasBottom ? _bottomText.Text ?? string.Empty : string.Empty,
                HasBottomAnnotation = hasBottom,
                TextStyleName = Convert.ToString(_textStyle.SelectedItem, CultureInfo.CurrentCulture) ?? string.Empty,
                TextHeight = height,
                LayerName = Convert.ToString(_layer.SelectedItem, CultureInfo.CurrentCulture) ?? string.Empty,
                TextColorIndex = CDBoxColorService.ToCompatibleColorIndex(textColor, _model.TextColorIndex),
                LeaderColorIndex = CDBoxColorService.ToCompatibleColorIndex(leaderColor, _model.LeaderColorIndex),
                TextColor = textColor,
                LeaderColor = leaderColor,
                LinetypeName = Convert.ToString(_linetype.SelectedItem, CultureInfo.CurrentCulture) ?? string.Empty,
                LineWeight = weight == null ? _model.LineWeight : weight.Value,
                SourceObjectId = _model.SourceObjectId,
                SourceHandle = _model.SourceHandle,
                SourceLayerName = _model.SourceLayerName,
                SourceObjectType = _model.SourceObjectType,
                BindingPoint = _model.BindingPoint,
                HasBindingPoint = _model.HasBindingPoint,
                IsBound = _model.IsBound,
                BindingIsValid = _model.BindingIsValid,
                TextStyleNames = _model.TextStyleNames,
                LayerNames = _model.LayerNames,
                LinetypeNames = _model.LinetypeNames
            };
        }

        private static string ReadEditorValue(Control control)
        {
            TextBox box = control as TextBox;
            return box == null ? string.Empty : box.Text ?? string.Empty;
        }

        private static void WriteEditorValue(Control control, string value)
        {
            TextBox box = control as TextBox;
            if (box != null) box.Text = value ?? string.Empty;
        }

        private static TextBox CreateTextBox()
        {
            return new TextBox
            {
                MinHeight = 28,
                Padding = new Thickness(7, 3, 7, 3),
                BorderBrush = Brush("#C7D4E5"),
                BorderThickness = new Thickness(1),
                Background = Brush("#FAFCFF"),
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private static TextBox AddTextBoxRow(Grid grid, int row, string label)
        {
            AddLabel(grid, row, label);
            TextBox box = CreateTextBox();
            box.Margin = new Thickness(0, 2, 0, 2);
            Grid.SetRow(box, row);
            Grid.SetColumn(box, 1);
            grid.Children.Add(box);
            return box;
        }

        private static ComboBox AddComboRow(Grid grid, int row, string label)
        {
            AddLabel(grid, row, label);
            var combo = new ComboBox
            {
                MinHeight = 28,
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 2, 0, 2),
                BorderBrush = Brush("#C7D4E5"),
                Background = Brush("#FAFCFF")
            };
            Grid.SetRow(combo, row);
            Grid.SetColumn(combo, 1);
            grid.Children.Add(combo);
            return combo;
        }

        private static CDBoxColorPicker AddColorPickerRow(Grid grid, int row, string label)
        {
            AddLabel(grid, row, label);
            var picker = new CDBoxColorPicker
            {
                Margin = new Thickness(0, 2, 0, 2),
                AllowByLayer = true,
                AllowByBlock = true,
                AllowTrueColor = true,
                AllowColorBook = true,
                AllowCDBoxStandard = true
            };
            Grid.SetRow(picker, row);
            Grid.SetColumn(picker, 1);
            grid.Children.Add(picker);
            return picker;
        }

        private static void AddLabel(Grid grid, int row, string text)
        {
            var label = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("#607089"),
                Margin = new Thickness(0, 2, 7, 2),
                FontSize = 11.5
            };
            Grid.SetRow(label, row);
            grid.Children.Add(label);
        }

        private static Button CreateLinkButton(string text)
        {
            return new Button
            {
                Content = text,
                MinHeight = 24,
                Padding = new Thickness(2, 1, 2, 1),
                Background = Brushes.Transparent,
                Foreground = Brush("#3D63A7"),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
        }

        private static void SetStringItems(ComboBox combo, IList<string> items, string selected)
        {
            combo.ItemsSource = items ?? new List<string>();
            combo.SelectedItem = selected;
            if (combo.SelectedIndex < 0 && combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private void SelectLineWeight(LineWeight value)
        {
            foreach (object item in _lineWeight.Items)
            {
                var option = item as LineWeightOption;
                if (option != null && option.Value == value) { _lineWeight.SelectedItem = item; return; }
            }
            _lineWeight.SelectedIndex = 0;
        }

        private static IList<LineWeightOption> BuildLineWeightOptions()
        {
            return new[]
            {
                new LineWeightOption("随层", LineWeight.ByLayer),
                new LineWeightOption("随块", LineWeight.ByBlock),
                new LineWeightOption("默认", LineWeight.ByLineWeightDefault),
                new LineWeightOption("0.00 mm", LineWeight.LineWeight000),
                new LineWeightOption("0.05 mm", LineWeight.LineWeight005),
                new LineWeightOption("0.09 mm", LineWeight.LineWeight009),
                new LineWeightOption("0.13 mm", LineWeight.LineWeight013),
                new LineWeightOption("0.15 mm", LineWeight.LineWeight015),
                new LineWeightOption("0.18 mm", LineWeight.LineWeight018),
                new LineWeightOption("0.20 mm", LineWeight.LineWeight020),
                new LineWeightOption("0.25 mm", LineWeight.LineWeight025),
                new LineWeightOption("0.30 mm", LineWeight.LineWeight030),
                new LineWeightOption("0.35 mm", LineWeight.LineWeight035),
                new LineWeightOption("0.40 mm", LineWeight.LineWeight040),
                new LineWeightOption("0.50 mm", LineWeight.LineWeight050),
                new LineWeightOption("0.53 mm", LineWeight.LineWeight053),
                new LineWeightOption("0.60 mm", LineWeight.LineWeight060),
                new LineWeightOption("0.70 mm", LineWeight.LineWeight070),
                new LineWeightOption("0.80 mm", LineWeight.LineWeight080),
                new LineWeightOption("0.90 mm", LineWeight.LineWeight090),
                new LineWeightOption("1.00 mm", LineWeight.LineWeight100),
                new LineWeightOption("1.06 mm", LineWeight.LineWeight106),
                new LineWeightOption("1.20 mm", LineWeight.LineWeight120),
                new LineWeightOption("1.40 mm", LineWeight.LineWeight140),
                new LineWeightOption("1.58 mm", LineWeight.LineWeight158),
                new LineWeightOption("2.00 mm", LineWeight.LineWeight200),
                new LineWeightOption("2.11 mm", LineWeight.LineWeight211)
            };
        }

        private static SolidColorBrush Brush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return minimum;
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private sealed class LineWeightOption
        {
            public string Name { get; private set; }
            public LineWeight Value { get; private set; }
            public LineWeightOption(string name, LineWeight value) { Name = name; Value = value; }
        }
    }
}
