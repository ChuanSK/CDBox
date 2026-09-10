using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioWebPageForm : Form
    {
        private const int NativeResizeGrip = 6;
        private const int RoundRadius = 8;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCAPTION = 2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int CS_DROPSHADOW = 0x00020000;

        private static readonly Color DefaultChromeBackColor = Color.FromArgb(247, 249, 254);

        private readonly Func<string> _htmlFactory;
        private readonly Func<CDBoxStudioRouteRequest, CDBoxStudioRouteResult> _routeHandler;
        private readonly string _baseTitle;
        private readonly string _windowStateKey;
        private Color _chromeBackColor;
        private readonly bool _workbenchAppearance;
        private string _workbenchTheme;
        private readonly List<Control> _resizeGrips = new List<Control>();
        private Panel _rootPanel;
        private Panel _titleBar;
        private Panel _contentPanel;
        private Label _titleLabel;
        private Button _titleActionButton;
        private ToolTip _titleActionToolTip;
        private WebView2 _webView;
        private bool _webViewReady;
        private bool _parcelSaveAllowsClose;
        private bool _windowStateRestored;
        private FormWindowState _windowStateBeforeCadMinimize =
            FormWindowState.Normal;
        private bool _hudMode;
        private bool _hudAnimationsEnabled;
        private bool _hudGlowEnabled;
        private bool _hudHovered;
        private bool _hudAllowImmediateClose;
        private double _hudNormalOpacity = 0.68;
        private double _hudHoverOpacity = 1.0;
        private double _hudGlowIntensity = 0.28;
        private Timer _hudAnimationTimer;
        private DateTime _hudAnimationStartedUtc;
        private Rectangle _hudAnimationFrom;
        private Rectangle _hudAnimationTo;
        private Rectangle _hudTargetBounds;
        private Size _hudMinimumSize;
        private bool _hudClosingAnimation;

        public CDBoxStudioWebPageForm(string title, Func<string> htmlFactory, Func<CDBoxStudioRouteRequest, CDBoxStudioRouteResult> routeHandler)
            : this(title, htmlFactory, routeHandler, DefaultChromeBackColor, title)
        {
        }

        public CDBoxStudioWebPageForm(string title, Func<string> htmlFactory, Func<CDBoxStudioRouteRequest, CDBoxStudioRouteResult> routeHandler, Color chromeBackColor)
            : this(title, htmlFactory, routeHandler, chromeBackColor, title)
        {
        }

        public CDBoxStudioWebPageForm(string title, Func<string> htmlFactory, Func<CDBoxStudioRouteRequest, CDBoxStudioRouteResult> routeHandler, string windowStateKey)
            : this(title, htmlFactory, routeHandler, DefaultChromeBackColor, windowStateKey)
        {
        }

        public CDBoxStudioWebPageForm(string title, Func<string> htmlFactory, Func<CDBoxStudioRouteRequest, CDBoxStudioRouteResult> routeHandler, Color chromeBackColor, string windowStateKey)
        {
            _baseTitle = string.IsNullOrWhiteSpace(title) ? "CDBox Studio" : title.Trim();
            _windowStateKey = "page:" + (string.IsNullOrWhiteSpace(windowStateKey) ? _baseTitle : windowStateKey.Trim());
            _htmlFactory = htmlFactory ?? throw new ArgumentNullException("htmlFactory");
            _routeHandler = routeHandler;
            _workbenchAppearance = CDBoxWorkbenchAppearance.IsEnabledFor(windowStateKey);
            _workbenchTheme = _workbenchAppearance ? CDBoxStudioSettingsStore.Load().Theme : null;
            _chromeBackColor = _workbenchAppearance
                ? CDBoxWorkbenchAppearance.ChromeBackground(_workbenchTheme)
                : chromeBackColor.IsEmpty ? DefaultChromeBackColor : chromeBackColor;

            Text = _baseTitle;
            Width = 1320;
            Height = 820;
            MinimumSize = new Size(1120, 680);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = _chromeBackColor;
            Padding = new Padding(NativeResizeGrip);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            DoubleBuffered = true;

            BuildUi();
            CDBoxStudioLogger.Info("独立 WebView2 页面宿主已创建：" + _baseTitle);
        }

        public void EnableHudPresentation(CDBoxStudioSettings settings)
        {
            settings = settings ?? new CDBoxStudioSettings();
            settings.Normalize();
            _hudMode = true;
            _hudAnimationsEnabled = settings.AnimationsEnabled;
            _hudGlowEnabled = settings.AnnotationHudGlowEnabled;
            _hudNormalOpacity = settings.AnnotationHudNormalOpacity;
            _hudHoverOpacity = settings.AnnotationHudHoverOpacity;
            _hudGlowIntensity = settings.AnnotationHudGlowIntensity;
            MaximizeBox = false;
            MinimizeBox = false;
            Opacity = _hudNormalOpacity;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        protected override async void OnShown(EventArgs e)
        {
            if (!_windowStateRestored)
            {
                CDBoxWindowStateStore.Restore(this, _windowStateKey);
                _windowStateRestored = true;
            }
            if (_hudMode) PrepareHudOpenAnimation();
            base.OnShown(e);
            ApplyRoundedRegion();
            UpdateResizeGrips();
            if (_hudMode) BeginHudOpenAnimation();
            await InitializeWebViewAsync();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_windowStateKey == "page:realestate-parcel-survey-editor"
                && _webViewReady && !_parcelSaveAllowsClose
                && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                // The editor acknowledges its final draft before asking the host to close.
                ExecuteScript("if(window.CDBoxRequestParcelClose){window.CDBoxRequestParcelClose();}else{chrome.webview.postMessage('studio|closeParcelSaved|');}");
                return;
            }
            if (_hudMode && _hudAnimationsEnabled && !_hudAllowImmediateClose
                && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                BeginHudCloseAnimation();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopHudAnimation();
            CDBoxWindowStateStore.Save(this, _windowStateKey);
            base.OnFormClosed(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            ApplyRoundedRegion();
            UpdateResizeGrips();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int glowAlpha = _hudGlowEnabled
                ? Math.Max(40, Math.Min(255,
                    Convert.ToInt32(80 + 175 * _hudGlowIntensity)))
                : 216;
            Color borderColor = _hudMode && _hudHovered
                ? Color.FromArgb(glowAlpha, 115, 169, 245)
                : _workbenchAppearance ? CDBoxWorkbenchAppearance.HoverBackground(_workbenchTheme)
                : Color.FromArgb(216, 231, 245);
            using (Pen pen = new Pen(borderColor, _hudMode && _hudHovered ? 1.5F : 1F))
            {
                Rectangle rect = ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = CreateRoundedPath(rect, RoundRadius))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        private void BuildUi()
        {
            _rootPanel = new Panel();
            _rootPanel.Dock = DockStyle.Fill;
            _rootPanel.BackColor = _chromeBackColor;
            _rootPanel.Padding = new Padding(0);
            Controls.Add(_rootPanel);

            _titleBar = new Panel();
            _titleBar.Dock = DockStyle.Top;
            _titleBar.Height = 42;
            _titleBar.BackColor = _chromeBackColor;
            _titleBar.MouseDown += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button == MouseButtons.Left) BeginWindowDrag();
            };

            _contentPanel = new Panel();
            _contentPanel.Dock = DockStyle.Fill;
            _contentPanel.BackColor = _chromeBackColor;

            _webView = new WebView2();
            _webView.Dock = DockStyle.Fill;
            _webView.DefaultBackgroundColor = _chromeBackColor;
            _webView.MouseEnter += delegate { SetHudHover(true); };
            _webView.MouseLeave += delegate { SetHudHover(false); };
            _contentPanel.Controls.Add(_webView);

            BuildTitleBarControls();
            _rootPanel.Controls.Add(_contentPanel);
            _rootPanel.Controls.Add(_titleBar);

            BuildNativeResizeGrips();
            UpdateResizeGrips();
            _rootPanel.MouseEnter += delegate { SetHudHover(true); };
            _rootPanel.MouseLeave += delegate { SetHudHover(false); };
            _titleBar.MouseEnter += delegate { SetHudHover(true); };
            _titleBar.MouseLeave += delegate { SetHudHover(false); };
        }

        private void SetHudHover(bool hovered)
        {
            if (!_hudMode || _hudClosingAnimation) return;
            _hudHovered = hovered;
            if (_hudAnimationTimer == null)
                Opacity = hovered ? _hudHoverOpacity : _hudNormalOpacity;
            Invalidate();
        }

        private void PrepareHudOpenAnimation()
        {
            _hudTargetBounds = Bounds;
            if (!_hudAnimationsEnabled)
            {
                Opacity = _hudNormalOpacity;
                return;
            }
            _hudMinimumSize = MinimumSize;
            MinimumSize = Size.Empty;
            Point cursor = Cursor.Position;
            int startWidth = Math.Max(36, Convert.ToInt32(
                _hudTargetBounds.Width * 0.08));
            int startHeight = Math.Max(28, Convert.ToInt32(
                _hudTargetBounds.Height * 0.08));
            _hudAnimationFrom = new Rectangle(
                cursor.X - startWidth / 2, cursor.Y - startHeight / 2,
                startWidth, startHeight);
            _hudAnimationTo = _hudTargetBounds;
            Bounds = _hudAnimationFrom;
            Opacity = 0.05;
        }

        private void BeginHudOpenAnimation()
        {
            if (!_hudAnimationsEnabled) return;
            StartHudAnimation(false);
        }

        private void BeginHudCloseAnimation()
        {
            if (_hudClosingAnimation) return;
            if (_hudAnimationTimer != null)
            {
                StopHudAnimation();
                Bounds = _hudTargetBounds;
                MinimumSize = _hudMinimumSize;
                Opacity = _hudHovered ? _hudHoverOpacity : _hudNormalOpacity;
            }
            _hudClosingAnimation = true;
            _hudTargetBounds = Bounds;
            _hudMinimumSize = MinimumSize;
            MinimumSize = Size.Empty;
            Point cursor = Cursor.Position;
            int endWidth = Math.Max(36, Convert.ToInt32(Bounds.Width * 0.08));
            int endHeight = Math.Max(28, Convert.ToInt32(Bounds.Height * 0.08));
            _hudAnimationFrom = Bounds;
            _hudAnimationTo = new Rectangle(
                cursor.X - endWidth / 2, cursor.Y - endHeight / 2,
                endWidth, endHeight);
            StartHudAnimation(true);
        }

        private void StartHudAnimation(bool closing)
        {
            StopHudAnimation();
            _hudClosingAnimation = closing;
            _hudAnimationStartedUtc = DateTime.UtcNow;
            _hudAnimationTimer = new Timer { Interval = 15 };
            _hudAnimationTimer.Tick += HudAnimationTick;
            _hudAnimationTimer.Start();
        }

        private void HudAnimationTick(object sender, EventArgs e)
        {
            const double duration = 180.0;
            double raw = (DateTime.UtcNow - _hudAnimationStartedUtc)
                .TotalMilliseconds / duration;
            double progress = Math.Max(0.0, Math.Min(1.0, raw));
            double eased = 1.0 - Math.Pow(1.0 - progress, 3.0);
            Bounds = Interpolate(_hudAnimationFrom, _hudAnimationTo, eased);
            double targetOpacity = _hudClosingAnimation ? 0.05
                : (_hudHovered ? _hudHoverOpacity : _hudNormalOpacity);
            Opacity = _hudClosingAnimation
                ? _hudNormalOpacity + (targetOpacity - _hudNormalOpacity) * eased
                : 0.05 + (targetOpacity - 0.05) * eased;
            if (progress < 1.0) return;

            bool closing = _hudClosingAnimation;
            StopHudAnimation();
            MinimumSize = _hudMinimumSize;
            if (closing)
            {
                _hudAllowImmediateClose = true;
                Bounds = _hudTargetBounds;
                Close();
                return;
            }
            Bounds = _hudTargetBounds;
            Opacity = _hudHovered ? _hudHoverOpacity : _hudNormalOpacity;
            Invalidate();
        }

        private void StopHudAnimation()
        {
            if (_hudAnimationTimer == null) return;
            _hudAnimationTimer.Stop();
            _hudAnimationTimer.Tick -= HudAnimationTick;
            _hudAnimationTimer.Dispose();
            _hudAnimationTimer = null;
        }

        private static Rectangle Interpolate(Rectangle from, Rectangle to,
            double progress)
        {
            return new Rectangle(
                Convert.ToInt32(from.X + (to.X - from.X) * progress),
                Convert.ToInt32(from.Y + (to.Y - from.Y) * progress),
                Math.Max(1, Convert.ToInt32(
                    from.Width + (to.Width - from.Width) * progress)),
                Math.Max(1, Convert.ToInt32(
                    from.Height + (to.Height - from.Height) * progress)));
        }

        private void BuildTitleBarControls()
        {
            var close = CreateTitleButton("×", true);
            close.Click += delegate { Close(); };

            var minimize = CreateTitleButton("—", false);
            minimize.Click += delegate { WindowState = FormWindowState.Minimized; };

            _titleLabel = new Label();
            _titleLabel.Dock = DockStyle.Fill;
            _titleLabel.Text = _baseTitle;
            _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            _titleLabel.AutoEllipsis = true;
            _titleLabel.Padding = new Padding(16, 0, 0, 0);
            _titleLabel.ForeColor = _workbenchAppearance ? CDBoxWorkbenchAppearance.Foreground(_workbenchTheme) : Color.FromArgb(22, 32, 51);
            _titleLabel.Font = new Font(Font.FontFamily, 10F, _workbenchAppearance ? FontStyle.Regular : FontStyle.Bold);
            _titleLabel.MouseDown += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button == MouseButtons.Left) BeginWindowDrag();
            };

            _titleBar.Controls.Add(_titleLabel);
            _titleBar.Controls.Add(minimize);
            _titleBar.Controls.Add(close);
        }

        internal void ConfigureTitleBarAction(string text, string toolTip,
            string clickScript, string hoverScript, string leaveScript)
        {
            if (string.IsNullOrWhiteSpace(text) || _titleBar == null) return;
            if (_titleActionButton != null)
            {
                _titleBar.Controls.Remove(_titleActionButton);
                _titleActionButton.Dispose();
            }

            var button = new Button
            {
                Text = text.Trim(),
                Width = 28,
                Height = 28,
                Location = new Point(7, 7),
                FlatStyle = FlatStyle.Flat,
                BackColor = _chromeBackColor,
                ForeColor = _workbenchAppearance ? CDBoxWorkbenchAppearance.Foreground(_workbenchTheme) : Color.FromArgb(61, 75, 100),
                Cursor = Cursors.Hand,
                TabStop = false,
                UseVisualStyleBackColor = false,
                TextAlign = ContentAlignment.MiddleCenter,
                AccessibleName = string.IsNullOrWhiteSpace(toolTip)
                    ? text.Trim() : toolTip.Trim(),
                Font = new Font(Font.FontFamily, 11F, FontStyle.Regular)
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.BorderColor = _chromeBackColor;
            button.FlatAppearance.MouseOverBackColor = _workbenchAppearance
                ? CDBoxWorkbenchAppearance.HoverBackground(_workbenchTheme) : Color.FromArgb(226, 235, 248);
            button.FlatAppearance.MouseDownBackColor = _workbenchAppearance
                ? CDBoxWorkbenchAppearance.HoverBackground(_workbenchTheme) : Color.FromArgb(211, 224, 244);
            button.Click += delegate { TryExecutePageScript(clickScript); };
            button.MouseEnter += delegate
            {
                TryExecutePageScript(hoverScript);
            };
            button.MouseLeave += delegate
            {
                TryExecutePageScript(leaveScript);
            };
            _titleActionButton = button;
            _titleLabel.Padding = new Padding(43, 0, 0, 0);
            _titleBar.Controls.Add(button);
            button.BringToFront();

            if (!string.IsNullOrWhiteSpace(toolTip))
            {
                if (_titleActionToolTip == null)
                    _titleActionToolTip = new ToolTip();
                _titleActionToolTip.SetToolTip(button, toolTip.Trim());
            }
        }

        private Button CreateTitleButton(string text, bool isClose)
        {
            var button = new Button();
            button.Dock = DockStyle.Right;
            button.Width = 48;
            button.Height = 42;
            button.Text = text;
            button.Tag = isClose ? "close" : "window-action";
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = isClose ? Color.FromArgb(239, 68, 68) : _workbenchAppearance ? CDBoxWorkbenchAppearance.HoverBackground(_workbenchTheme) : Color.FromArgb(226, 235, 248);
            button.FlatAppearance.MouseDownBackColor = isClose ? Color.FromArgb(220, 38, 38) : _workbenchAppearance ? CDBoxWorkbenchAppearance.HoverBackground(_workbenchTheme) : Color.FromArgb(211, 224, 244);
            button.BackColor = _chromeBackColor;
            button.ForeColor = _workbenchAppearance ? CDBoxWorkbenchAppearance.Foreground(_workbenchTheme) : isClose ? Color.FromArgb(153, 27, 27) : Color.FromArgb(61, 75, 100);
            button.Cursor = Cursors.Default;
            button.TabStop = false;
            button.Font = new Font(Font.FontFamily, isClose ? 12F : 10F, FontStyle.Regular);
            button.MouseEnter += delegate
            {
                if (isClose) button.ForeColor = Color.White;
            };
            button.MouseLeave += delegate
            {
                button.ForeColor = _workbenchAppearance ? CDBoxWorkbenchAppearance.Foreground(_workbenchTheme) : isClose ? Color.FromArgb(153, 27, 27) : Color.FromArgb(61, 75, 100);
            };
            return button;
        }

        private void BuildNativeResizeGrips()
        {
            _resizeGrips.Clear();
            CreateResizeGrip("GripLeft", HTLEFT, Cursors.SizeWE);
            CreateResizeGrip("GripRight", HTRIGHT, Cursors.SizeWE);
            CreateResizeGrip("GripTop", HTTOP, Cursors.SizeNS);
            CreateResizeGrip("GripBottom", HTBOTTOM, Cursors.SizeNS);
            CreateResizeGrip("GripTopLeft", HTTOPLEFT, Cursors.SizeNWSE);
            CreateResizeGrip("GripTopRight", HTTOPRIGHT, Cursors.SizeNESW);
            CreateResizeGrip("GripBottomLeft", HTBOTTOMLEFT, Cursors.SizeNESW);
            CreateResizeGrip("GripBottomRight", HTBOTTOMRIGHT, Cursors.SizeNWSE);
        }

        private void CreateResizeGrip(string name, int hitTest, Cursor cursor)
        {
            var grip = new Panel();
            grip.Name = name;
            grip.BackColor = Color.Transparent;
            grip.Cursor = cursor;
            grip.TabStop = false;
            grip.Tag = hitTest;
            grip.MouseDown += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button != MouseButtons.Left) return;
                Control control = sender as Control;
                int gripHitTest = 0;
                if (control != null && control.Tag != null) gripHitTest = Convert.ToInt32(control.Tag);
                BeginWindowResize(gripHitTest);
            };
            Controls.Add(grip);
            grip.BringToFront();
            _resizeGrips.Add(grip);
        }

        private void UpdateResizeGrips()
        {
            if (IsDisposed || _resizeGrips.Count == 0) return;

            int w = ClientSize.Width;
            int h = ClientSize.Height;
            int g = NativeResizeGrip;
            bool enabled = WindowState != FormWindowState.Maximized && w > g * 2 && h > g * 2;

            foreach (Control grip in _resizeGrips)
            {
                grip.Visible = enabled;
                grip.Enabled = enabled;
            }

            if (!enabled) return;

            SetGripBounds("GripTopLeft", 0, 0, g, g);
            SetGripBounds("GripTopRight", w - g, 0, g, g);
            SetGripBounds("GripBottomLeft", 0, h - g, g, g);
            SetGripBounds("GripBottomRight", w - g, h - g, g, g);
            SetGripBounds("GripLeft", 0, g, g, h - g * 2);
            SetGripBounds("GripRight", w - g, g, g, h - g * 2);
            SetGripBounds("GripTop", g, 0, w - g * 2, g);
            SetGripBounds("GripBottom", g, h - g, w - g * 2, g);
            BringResizeGripsToFront();
        }

        private void SetGripBounds(string name, int x, int y, int width, int height)
        {
            foreach (Control grip in _resizeGrips)
            {
                if (!string.Equals(grip.Name, name, StringComparison.Ordinal)) continue;
                grip.Bounds = new Rectangle(x, y, Math.Max(1, width), Math.Max(1, height));
                return;
            }
        }

        private void BringResizeGripsToFront()
        {
            foreach (Control grip in _resizeGrips)
            {
                grip.BringToFront();
            }
        }

        private async System.Threading.Tasks.Task InitializeWebViewAsync()
        {
            if (_webViewReady || _webView == null || _webView.IsDisposed) return;

            try
            {
                CDBoxStudioRuntimeInfo runtime = CDBoxStudioRuntime.Detect();
                if (runtime == null || !runtime.Available)
                {
                    runtime = await CDBoxStudioRuntimeInstaller
                        .EnsureAvailableWithPromptAsync(this, runtime);
                }
                if (runtime == null || !runtime.Available)
                {
                    string reason = runtime == null ? "未知错误" : runtime.ErrorMessage;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(reason) ? "未检测到 Microsoft Edge WebView2 Runtime。" : reason);
                }

                CDBoxStudioLogger.Info("独立页面 WebView2 Runtime 可用。版本：" + runtime.Version + "，页面：" + _baseTitle);

                CoreWebView2Environment environment = await
                    CDBoxStudioRuntime.GetEnvironmentAsync();
                await _webView.EnsureCoreWebView2Async(environment);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                _webViewReady = true;
                RefreshPage();
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("独立 WebView2 页面初始化失败：" + _baseTitle, ex);
                ShowFallback(ex);
            }
        }

        public void RefreshPage()
        {
            if (!_webViewReady || _webView == null || _webView.IsDisposed || _webView.CoreWebView2 == null) return;

            try
            {
                string html = _htmlFactory == null ? string.Empty : _htmlFactory();
                if (string.IsNullOrWhiteSpace(html)) throw new InvalidOperationException("页面 HTML 为空，无法加载。");
                if (_workbenchAppearance)
                    RefreshWorkbenchChrome(CDBoxStudioSettingsStore.Load().Theme);
                _webView.NavigateToString(CDBoxAccentAppearance.Attach(html));
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("独立 WebView2 页面刷新失败：" + _baseTitle, ex);
                NavigateToErrorPage("页面加载失败", ex.Message, ex.ToString());
            }
        }

        internal static void RefreshWorkbenchWindows(CDBoxStudioSettings settings)
        {
            CDBoxAccentAppearance.RefreshNative(settings);
            WastewaterNativeAppearance.Refresh(settings);
            foreach (Form form in Application.OpenForms)
            {
                var page = form as CDBoxStudioWebPageForm;
                if (page == null)
                {
                    var studio = form as CDBoxStudioForm;
                    if (studio != null) studio.RefreshAccent(settings);
                    else if (form.GetType().Assembly == typeof(CDBoxAccentAppearance).Assembly) CDBoxAccentAppearance.ApplyNative(form);
                    continue;
                }
                if (page._workbenchAppearance) page.RefreshWorkbenchChrome(settings.Theme);
                page.ExecuteScript((page._workbenchAppearance ? "window.CDBoxApplyWorkbenchTheme && window.CDBoxApplyWorkbenchTheme(" : "window.CDBoxApplyAccentTheme && window.CDBoxApplyAccentTheme(")
                    + CDBoxThemeCatalog.Json(settings) + (page._workbenchAppearance ? ");" : ",true);"));
            }
        }

        private void RefreshWorkbenchChrome(string theme)
        {
            _workbenchTheme = theme;
            _chromeBackColor = CDBoxWorkbenchAppearance.ChromeBackground(theme);
            Color foreground = CDBoxWorkbenchAppearance.Foreground(theme);
            Color hover = CDBoxWorkbenchAppearance.HoverBackground(theme);
            BackColor = _chromeBackColor;
            _rootPanel.BackColor = _titleBar.BackColor = _contentPanel.BackColor = _chromeBackColor;
            _webView.DefaultBackgroundColor = _chromeBackColor;
            _titleLabel.ForeColor = foreground;
            foreach (Control control in _titleBar.Controls)
            {
                Button button = control as Button;
                if (button == null) continue;
                button.BackColor = _chromeBackColor;
                button.ForeColor = foreground;
                button.FlatAppearance.BorderColor = _chromeBackColor;
                if (string.Equals(button.Tag as string, "close", StringComparison.Ordinal)) continue;
                button.FlatAppearance.MouseOverBackColor = hover;
                button.FlatAppearance.MouseDownBackColor = hover;
            }
            foreach (Control grip in _resizeGrips) grip.BackColor = _chromeBackColor;
            Invalidate();
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string raw;
            try
            {
                raw = e.TryGetWebMessageAsString();
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("读取独立页面前端消息失败：" + _baseTitle, ex);
                return;
            }

            if (string.IsNullOrWhiteSpace(raw)) return;

            BeginInvoke(new Action(delegate
            {
                RouteMessage(raw);
            }));
        }

        private void RouteMessage(string raw)
        {
            CDBoxStudioRouteRequest request = CDBoxStudioRouteRequest.Parse(raw);
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                CDBoxStudioLogger.Warn("独立页面收到无法识别的路由消息：" + raw + "，页面：" + _baseTitle);
                NavigateToErrorPage("页面路由格式异常", "前端发送的路由消息无法识别。", "原始消息：" + raw);
                return;
            }

            if (_windowStateKey == "page:realestate-parcel-survey-editor"
                && string.Equals(request.Name, "closeParcelSaved", StringComparison.OrdinalIgnoreCase))
            {
                _parcelSaveAllowsClose = true;
                Close();
                return;
            }

            if (string.Equals(request.Name, "close", StringComparison.OrdinalIgnoreCase))
            {
                Close();
                return;
            }

            if (string.Equals(request.Name, "minimize", StringComparison.OrdinalIgnoreCase))
            {
                if (WindowState != FormWindowState.Minimized)
                    _windowStateBeforeCadMinimize = WindowState;
                WindowState = FormWindowState.Minimized;
                return;
            }

            if (string.Equals(request.Name, "restore", StringComparison.OrdinalIgnoreCase))
            {
                if (WindowState == FormWindowState.Minimized)
                    WindowState = _windowStateBeforeCadMinimize ==
                        FormWindowState.Minimized ? FormWindowState.Normal
                        : _windowStateBeforeCadMinimize;
                Show();
                Activate();
                BringToFront();
                return;
            }

            if (string.Equals(request.Name, "beginDrag", StringComparison.OrdinalIgnoreCase))
            {
                BeginWindowDrag();
                return;
            }

            CDBoxStudioRouteResult result = _routeHandler == null ? null : _routeHandler(request);
            if (result == null || !result.Handled)
            {
                string message = "独立页面路由未匹配：" + request.Name;
                CDBoxStudioLogger.Warn(message + "，页面：" + _baseTitle);
                NavigateToErrorPage("页面路由未匹配", message, "原始消息：" + raw);
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.WindowTitleSuffix))
            {
                Text = _baseTitle + "（" + result.WindowTitleSuffix.Trim() + "）";
                if (_titleLabel != null) _titleLabel.Text = Text;
            }

            if (result.ActionToRun != null)
            {
                try
                {
                    result.ActionToRun.Run();
                }
                catch (Exception ex)
                {
                    CDBoxStudioLogger.Error("独立页面动作运行失败：" + result.ActionToRun.Title, ex);
                    ShowToast(result.ActionToRun.Title + "运行失败：" + ex.Message, "error");
                }
            }

            if (!string.IsNullOrWhiteSpace(result.ExecuteScript)) ExecuteScript(result.ExecuteScript);
            if (!string.IsNullOrWhiteSpace(result.ToastMessage)) ShowToast(result.ToastMessage, result.ToastKind);
            if (result.RefreshPage) RefreshPage();
        }

        private void BeginWindowDrag()
        {
            try
            {
                if (WindowState == FormWindowState.Maximized) return;
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("独立页面窗口拖动失败：" + _baseTitle, ex);
            }
        }

        private void BeginWindowResize(int hitTest)
        {
            try
            {
                if (WindowState == FormWindowState.Maximized) return;
                if (hitTest == HTCAPTION || hitTest == 0) return;
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(hitTest), IntPtr.Zero);
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("独立页面窗口缩放失败：" + _baseTitle, ex);
            }
        }

        private void ShowToast(string message, string kind)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            ExecuteScript("window.CDBoxStudioToast && window.CDBoxStudioToast(" + ToJsString(message) + "," + ToJsString(kind ?? "info") + ");");
        }

        internal bool TryExecutePageScript(string script)
        {
            if (string.IsNullOrWhiteSpace(script) || IsDisposed || Disposing) return false;

            if (InvokeRequired)
            {
                try
                {
                    if (!IsHandleCreated) return false;
                    BeginInvoke(new Action(delegate { TryExecutePageScript(script); }));
                    return true;
                }
                catch (Exception ex)
                {
                    CDBoxStudioLogger.Error("独立页面调度脚本失败：" + _baseTitle, ex);
                    return false;
                }
            }

            if (!_webViewReady || _webView == null || _webView.IsDisposed
                || _webView.CoreWebView2 == null) return false;

            try
            {
                _webView.CoreWebView2.ExecuteScriptAsync(script);
                return true;
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("独立页面执行脚本失败：" + _baseTitle, ex);
                return false;
            }
        }

        private void ExecuteScript(string script)
        {
            TryExecutePageScript(script);
        }

        private static string ToJsString(string value)
        {
            if (value == null) return "''";
            return "'" + value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n") + "'";
        }

        private void ShowFallback(Exception ex)
        {
            try
            {
                if (_webView != null)
                {
                    if (_contentPanel != null) _contentPanel.Controls.Remove(_webView);
                    else Controls.Remove(_webView);
                    _webView.Dispose();
                    _webView = null;
                }
                if (_titleActionToolTip != null)
                {
                    _titleActionToolTip.Dispose();
                    _titleActionToolTip = null;
                }
            }
            catch
            {
            }

            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = _chromeBackColor;
            panel.Padding = new Padding(22);

            var body = new Label();
            body.Dock = DockStyle.Fill;
            body.AutoSize = false;
            body.TextAlign = ContentAlignment.TopLeft;
            body.Font = new Font(Font.FontFamily, 10F, FontStyle.Regular);
            body.ForeColor = Color.FromArgb(70, 80, 100);
            body.Padding = new Padding(0, 18, 0, 0);
            body.Text = "该独立页面需要 Microsoft Edge WebView2 Runtime。\r\n\r\n"
                + "当前 WebView2 初始化失败，旧版 WinForms 页面和其他旧功能不受影响。\r\n\r\n"
                + "宿主模板仍可拖动、最小化、关闭和边缘缩放。\r\n\r\n"
                + "CDBox 用户数据目录：\r\n"
                + CDBoxStudioWebViewProfile.PreferredUserDataFolder
                + "\r\n\r\n"
                + "错误信息：" + (ex == null ? "未知错误" : ex.Message) + "\r\n\r\n"
                + "Studio 日志：" + CDBoxStudioLogger.LogFilePath;

            panel.Controls.Add(body);
            if (_contentPanel != null) _contentPanel.Controls.Add(panel);
            else Controls.Add(panel);
            BringResizeGripsToFront();
        }

        private void NavigateToErrorPage(string title, string message, string detail)
        {
            try
            {
                if (_webViewReady && _webView != null && !_webView.IsDisposed && _webView.CoreWebView2 != null)
                {
                    _webView.NavigateToString(BuildErrorHtml(title, message, detail));
                }
                else
                {
                    ShowFallback(new InvalidOperationException(message ?? title ?? "页面错误"));
                }
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("独立页面错误页显示失败：" + _baseTitle, ex);
            }
        }

        private string BuildErrorHtml(string title, string message, string detail)
        {
            return "<!doctype html><html><head><meta charset=\"utf-8\"><style>"
                + "html,body{height:100%;margin:0;font-family:'Microsoft YaHei UI','Segoe UI',sans-serif;background:#f7f9fe;color:#162033;overflow:hidden}"
                + ".wrap{height:100%;display:grid;place-items:center;padding:28px;box-sizing:border-box}.card{max-width:760px;background:#fff;border:1px solid #dce8f6;border-radius:12px;box-shadow:0 12px 28px rgba(30,41,59,.08);padding:22px}.k{color:#64748b;line-height:1.7}.detail{white-space:pre-wrap;word-break:break-word;background:#f8fbff;border:1px solid #e8eef8;border-radius:9px;padding:12px;color:#64748b}.danger{border:1px solid #fecaca;background:#fff;color:#b91c1c;border-radius:9px;padding:9px 13px;cursor:pointer;font-weight:700}"
                + "</style></head><body><div class=\"wrap\"><div class=\"card\"><h2>" + Html(title) + "</h2><p class=\"k\">" + Html(message) + "</p><pre class=\"detail\">" + Html(detail) + "</pre><button class=\"danger\" onclick=\"chrome&&chrome.webview&&chrome.webview.postMessage('studio|close|')\">关闭</button></div></div></body></html>";
        }

        private static string Html(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        private void ApplyRoundedRegion()
        {
            if (IsDisposed) return;

            if (WindowState == FormWindowState.Maximized)
            {
                Region = null;
                return;
            }

            Rectangle rect = new Rectangle(0, 0, Width, Height);
            using (GraphicsPath path = CreateRoundedPath(rect, RoundRadius))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(1, radius * 2);
            Rectangle arc = new Rectangle(rect.Left, rect.Top, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_titleActionToolTip != null)
                {
                    _titleActionToolTip.Dispose();
                    _titleActionToolTip = null;
                }
                if (_webView != null)
                {
                    try
                    {
                        if (_webView.CoreWebView2 != null) _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                    }
                    catch
                    {
                    }
                    _webView.Dispose();
                    _webView = null;
                }
            }

            base.Dispose(disposing);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
