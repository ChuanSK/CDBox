using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioForm : Form
    {
        private readonly Dictionary<string, CDBoxStudioAction> _actionsById = new Dictionary<string, CDBoxStudioAction>(StringComparer.OrdinalIgnoreCase);
        private readonly CDBoxStudioState _state;
        private readonly CDBoxStudioSettings _settings;
        private CDBoxStudioCommandRouter _router;
        private WebView2 _webView;
        private bool _webViewReady;
        private string _runtimeVersion;
        private bool _windowStateRestored;

        public CDBoxStudioForm(IEnumerable<CDBoxStudioAction> actions)
        {
            Text = "CDBox Studio（WebView2 实验工作台）";
            Width = 1220;
            Height = 780;
            MinimumSize = new Size(1000, 660);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(246, 248, 252);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            _state = CDBoxStudioStateStore.Load();
            _settings = CDBoxStudioSettingsStore.Load();
            SetActions(actions);
            BuildUi();
            CDBoxStudioLogger.Info("Studio 窗口构造完成。日志路径：" + CDBoxStudioLogger.LogFilePath);
        }

        public void SetActions(IEnumerable<CDBoxStudioAction> actions)
        {
            _actionsById.Clear();
            if (actions != null)
            {
                foreach (CDBoxStudioAction action in actions)
                {
                    if (action == null || string.IsNullOrWhiteSpace(action.Id)) continue;
                    _actionsById[action.Id] = action;
                }
            }

            _state.RemoveMissingActions(_actionsById.Keys);
            CDBoxStudioStateStore.Save(_state);
            _router = new CDBoxStudioCommandRouter(_actionsById, _state, _settings, PostScriptFromRouter);
            RefreshPage();
        }

        protected override async void OnShown(EventArgs e)
        {
            if (!_windowStateRestored)
            {
                CDBoxWindowStateStore.Restore(this, "studio-main");
                _windowStateRestored = true;
            }
            base.OnShown(e);
            await InitializeWebViewAsync();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            CDBoxWindowStateStore.Save(this, "studio-main");
            base.OnFormClosed(e);
        }

        private void BuildUi()
        {
            _webView = new WebView2();
            _webView.Dock = DockStyle.Fill;
            _webView.DefaultBackgroundColor = Color.FromArgb(246, 248, 252);
            Controls.Add(_webView);
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

                _runtimeVersion = runtime.Version;
                CDBoxStudioLogger.Info("WebView2 Runtime 可用。版本：" + _runtimeVersion);

                await _webView.EnsureCoreWebView2Async(null);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                _webViewReady = true;
                RefreshPage();
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("WebView2 初始化失败。", ex);
                ShowFallback(ex);
            }
        }

        private void RefreshPage()
        {
            if (!_webViewReady || _webView == null || _webView.IsDisposed || _webView.CoreWebView2 == null) return;
            _webView.NavigateToString(CDBoxStudioHtml.Build(_actionsById.Values, _state, _settings, _runtimeVersion, CDBoxStudioLogger.LogFilePath, CDBoxStudioSettingsStore.SettingsFilePath));
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
                CDBoxStudioLogger.Error("读取前端消息失败。", ex);
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
            CDBoxStudioRouteResult result = _router == null ? null : _router.Route(request);
            if (result == null || !result.Handled) return;

            if (!string.IsNullOrWhiteSpace(result.WindowTitleSuffix))
            {
                Text = "CDBox Studio（" + result.WindowTitleSuffix.Trim() + "）";
            }

            if (result.ActionToRun != null)
            {
                RunAction(result.ActionToRun, result.RefreshPage);
                return;
            }

            if (result.RefreshPage) RefreshPage();
            if (!string.IsNullOrWhiteSpace(result.ExecuteScript)) ExecuteScript(result.ExecuteScript);
            if (!string.IsNullOrWhiteSpace(result.ToastMessage)) ShowToast(result.ToastMessage, result.ToastKind);
        }

        private void RunAction(CDBoxStudioAction action, bool refreshAfterRun)
        {
            if (action == null || !action.Enabled) return;

            bool shouldRestore = action.RestoreStudioAfterRun;
            bool wasVisible = Visible;

            try
            {
                ShowToast("正在打开：" + action.Title, "info");
                Hide();
                action.Run();
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error(action.Title + " 运行失败。", ex);
                MessageBox.Show(new AcadMainWindow(), ex.Message, action.Title + "运行失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                shouldRestore = true;
            }
            finally
            {
                if (shouldRestore && wasVisible && !IsDisposed)
                {
                    Show(new AcadMainWindow());
                    WindowState = FormWindowState.Normal;
                    Activate();
                    if (refreshAfterRun) RefreshPage();
                    ShowToast("已返回 Studio", "success");
                }
            }
        }


        private void PostScriptFromRouter(string script)
        {
            if (string.IsNullOrWhiteSpace(script) || IsDisposed) return;

            try
            {
                if (InvokeRequired) BeginInvoke(new Action(delegate { ExecuteScript(script); }));
                else ExecuteScript(script);
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("执行 Studio 后台路由脚本失败。", ex);
            }
        }

        private void ShowToast(string message, string kind)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            ExecuteScript("window.CDBoxStudioToast && window.CDBoxStudioToast(" + ToJsString(message) + "," + ToJsString(kind ?? "info") + ");");
        }

        private void ExecuteScript(string script)
        {
            if (string.IsNullOrWhiteSpace(script) || !_webViewReady || _webView == null || _webView.IsDisposed || _webView.CoreWebView2 == null) return;

            try
            {
                _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("执行 Studio 前端脚本失败。", ex);
            }
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
                    Controls.Remove(_webView);
                    _webView.Dispose();
                    _webView = null;
                }
            }
            catch
            {
            }

            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.FromArgb(246, 248, 252);
            panel.Padding = new Padding(38);

            var title = new Label();
            title.Dock = DockStyle.Top;
            title.Height = 42;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Font = new Font(Font.FontFamily, 15F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(22, 32, 51);
            title.Text = "CDBox Studio 无法启动 WebView2";

            var body = new Label();
            body.Dock = DockStyle.Fill;
            body.AutoSize = false;
            body.TextAlign = ContentAlignment.TopLeft;
            body.Font = new Font(Font.FontFamily, 10F, FontStyle.Regular);
            body.ForeColor = Color.FromArgb(70, 80, 100);
            body.Padding = new Padding(0, 18, 0, 0);
            body.Text = "CDBox Studio 需要 Microsoft Edge WebView2 Runtime。\r\n\r\n"
                + "当前 WebView2 初始化失败，经典 CDBOX、侧边栏和所有旧功能不受影响。\r\n\r\n"
                + "建议：安装或修复 Microsoft Edge WebView2 Runtime 后，重新打开 CDSTUDIO。\r\n\r\n"
                + "错误信息：" + (ex == null ? "未知错误" : ex.Message) + "\r\n\r\n"
                + "Studio 日志：" + CDBoxStudioLogger.LogFilePath;

            panel.Controls.Add(body);
            panel.Controls.Add(title);
            Controls.Add(panel);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_router != null)
                {
                    _router.Dispose();
                    _router = null;
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
    }
}
