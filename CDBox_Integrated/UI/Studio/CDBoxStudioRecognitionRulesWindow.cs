using System;
using System.Windows.Forms;
using TCPipeAutoDraw.Modules.LayerManager;
using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioRecognitionRulesWindow
    {
        private static CDBoxStudioWebPageForm _current;

        public static void ShowWindow(IWin32Window owner)
        {
            if (_current != null && !_current.IsDisposed)
            {
                _current.WindowState = FormWindowState.Normal;
                _current.Activate();
                return;
            }

            _current = new CDBoxStudioWebPageForm(
                "属性识别表",
                delegate { return CDBoxStudioRecognitionRulesWindowHtml.Build(CDBoxStudioSettingsStore.Load(), CDBoxStudioLogger.LogFilePath); },
                Route);
            _current.FormClosed += delegate { _current = null; };
            _current.Width = 1520;
            _current.Height = 855;
            _current.MinimumSize = new System.Drawing.Size(1120, 680);
            _current.Show(owner ?? new AcadMainWindow());
            CDBoxStudioLogger.Info("已打开属性识别表独立 WebView2 窗口。路径：" + CDBoxStudioRecognitionRules.RulesFilePath);
        }

        private static CDBoxStudioRouteResult Route(CDBoxStudioRouteRequest request)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "info" };
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                result.Handled = false;
                return result;
            }

            string name = request.Name.Trim().ToLowerInvariant();
            switch (name)
            {
                case "ready":
                    result.ToastKind = "success";
                    result.ToastMessage = "属性识别表独立窗口已就绪";
                    return result;

                case "saverecognitionrules":
                    return SaveRecognitionRules(request.Argument);

                case "openlegacyrecognition":
                    return OpenLegacyRecognitionRules();

                case "openlogs":
                    return OpenLogs();

                default:
                    result.Handled = false;
                    CDBoxStudioLogger.Warn("属性识别表独立窗口收到未知路由消息：" + request.Name);
                    return result;
            }
        }

        private static CDBoxStudioRouteResult SaveRecognitionRules(string payload)
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = false,
                ToastKind = "success"
            };

            try
            {
                int count = CDBoxStudioRecognitionRules.SavePayload(payload);
                result.ToastMessage = "属性识别表已保存：" + count + " 条规则";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "属性识别表保存失败：" + ex.Message;
                CDBoxStudioLogger.Error("独立窗口保存属性识别表失败。", ex);
            }

            return result;
        }

        private static CDBoxStudioRouteResult OpenLegacyRecognitionRules()
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = true,
                ToastKind = "success"
            };

            try
            {
                using (var form = new LayerRecognitionRulesForm(LayerManagerService.LoadRecognitionRules(), LayerManagerService.GetRecognitionRulesFilePath()))
                {
                    DialogResult dialogResult = form.ShowDialog(new AcadMainWindow());
                    if (dialogResult == DialogResult.OK)
                    {
                        LayerManagerService.SaveRecognitionRules(form.Rules);
                        result.ToastMessage = "旧版属性识别表已保存，独立窗口已刷新";
                        CDBoxStudioLogger.Info("通过旧版窗口保存属性识别表。路径：" + LayerManagerService.GetRecognitionRulesFilePath());
                    }
                    else
                    {
                        result.RefreshPage = false;
                        result.ToastKind = "info";
                        result.ToastMessage = "已关闭旧版属性识别表";
                    }
                }
            }
            catch (Exception ex)
            {
                result.RefreshPage = false;
                result.ToastKind = "error";
                result.ToastMessage = "旧版属性识别表打开失败：" + ex.Message;
                CDBoxStudioLogger.Error("独立窗口打开旧版属性识别表失败。", ex);
            }

            return result;
        }

        private static CDBoxStudioRouteResult OpenLogs()
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };

            try
            {
                CDBoxStudioLogger.OpenLogFolder();
                result.ToastMessage = "已打开 Studio 日志目录";
            }
            catch (Exception ex)
            {
                result.ToastMessage = "日志目录打开失败：" + ex.Message;
                result.ToastKind = "error";
                CDBoxStudioLogger.Error("独立窗口打开 Studio 日志目录失败。", ex);
            }

            return result;
        }
    }
}
