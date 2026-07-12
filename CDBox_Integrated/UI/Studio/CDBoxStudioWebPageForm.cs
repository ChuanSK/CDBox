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
        private const int RoundRadius = 12;
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
        private readonly Color _chromeBackColor;
        private readonly List<Control> _resizeGrips = new List<Control>();
        private Panel _rootPanel;
        private Panel _titleBar;
        private Panel _contentPanel;
        private Label _titleLabel;
        private WebView2 _webView;
        private bool _webViewReady;

        public CDBoxStudioWebPageForm(string title, Func<string> htmlFactory, Func<CDBoxStudioRouteRequest, CDBoxStudioRouteResult> routeHandler)
            : this(title, htmlFactory, routeHandler, DefaultChromeBackColor)
        {
        }

        public CDBoxStudioWebPageForm(string title, Func<string> htmlFactory, Func<CDBoxStudioRouteRequest, CDBoxStudioRouteResult> routeHandler, Color chromeBackColor)
        {
            _baseTitle = string.IsNullOrWhiteSpace(title) ? "CDBox Studio" : title.Trim();
            _htmlFactory = htmlFactory ?? throw new ArgumentNullException("htmlFactory");
            _routeHandler = routeHandler;
            _chromeBackColor = chromeBackColor.IsEmpty ? DefaultChromeBackColor : chromeBackColor;

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
            base.OnShown(e);
            ApplyRoundedRegion();
            UpdateResizeGrips();
            await InitializeWebViewAsync();
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
            using (Pen pen = new Pen(Color.FromArgb(216, 231, 245), 1F))
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
            _contentPanel.Controls.Add(_webView);

            BuildTitleBarControls();
            _rootPanel.Controls.Add(_contentPanel);
            _rootPanel.Controls.Add(_titleBar);

            BuildNativeResizeGrips();
            UpdateResizeGrips();
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
            _titleLabel.ForeColor = Color.FromArgb(22, 32, 51);
            _titleLabel.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            _titleLabel.MouseDown += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button == MouseButtons.Left) BeginWindowDrag();
            };

            _titleBar.Controls.Add(_titleLabel);
            _titleBar.Controls.Add(minimize);
            _titleBar.Controls.Add(close);
        }

        private Button CreateTitleButton(string text, bool isClose)
        {
            var button = new Button();
            button.Dock = DockStyle.Right;
            button.Width = 48;
            button.Height = 42;
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = isClose ? Color.FromArgb(239, 68, 68) : Color.FromArgb(226, 235, 248);
            button.FlatAppearance.MouseDownBackColor = isClose ? Color.FromArgb(220, 38, 38) : Color.FromArgb(211, 224, 244);
            button.BackColor = _chromeBackColor;
            button.ForeColor = isClose ? Color.FromArgb(153, 27, 27) : Color.FromArgb(61, 75, 100);
            button.Cursor = Cursors.Default;
            button.TabStop = false;
            button.Font = new Font(Font.FontFamily, isClose ? 12F : 10F, FontStyle.Regular);
            button.MouseEnter += delegate
            {
                if (isClose) button.ForeColor = Color.White;
            };
            button.MouseLeave += delegate
            {
                button.ForeColor = isClose ? Color.FromArgb(153, 27, 27) : Color.FromArgb(61, 75, 100);
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
                    string reason = runtime == null ? "未知错误" : runtime.ErrorMessage;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(reason) ? "未检测到 Microsoft Edge WebView2 Runtime。" : reason);
                }

                CDBoxStudioLogger.Info("独立页面 WebView2 Runtime 可用。版本：" + runtime.Version + "，页面：" + _baseTitle);

                await _webView.EnsureCoreWebView2Async(null);
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
                _webView.NavigateToString(html);
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("独立 WebView2 页面刷新失败：" + _baseTitle, ex);
                NavigateToErrorPage("页面加载失败", ex.Message, ex.ToString());
            }
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

            if (string.Equals(request.Name, "close", StringComparison.OrdinalIgnoreCase))
            {
                Close();
                return;
            }

            if (string.Equals(request.Name, "minimize", StringComparison.OrdinalIgnoreCase))
            {
                WindowState = FormWindowState.Minimized;
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
            if (string.IsNullOrWhiteSpace(script) || !_webViewReady || _webView == null || _webView.IsDisposed || _webView.CoreWebView2 == null) return false;

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
                + ".wrap{height:100%;display:grid;place-items:center;padding:28px;box-sizing:border-box}.card{max-width:760px;background:#fff;border:1px solid #dce8f6;border-radius:18px;box-shadow:0 12px 28px rgba(30,41,59,.08);padding:22px}.k{color:#64748b;line-height:1.7}.detail{white-space:pre-wrap;word-break:break-word;background:#f8fbff;border:1px solid #e8eef8;border-radius:12px;padding:12px;color:#64748b}.danger{border:1px solid #fecaca;background:#fff;color:#b91c1c;border-radius:12px;padding:9px 13px;cursor:pointer;font-weight:700}"
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
