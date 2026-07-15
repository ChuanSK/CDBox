using System;
using System.Windows.Forms;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioDefaultProfilesWindow
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
                "属性默认表",
                delegate { return CDBoxStudioDefaultProfilesWindowHtml.Build(CDBoxStudioSettingsStore.Load(), CDBoxStudioLogger.LogFilePath); },
                Route,
                "default-profiles");
            _current.FormClosed += delegate { _current = null; };
            _current.Width = 1360;
            _current.Height = 840;
            _current.MinimumSize = new System.Drawing.Size(1160, 700);
            _current.Show(owner ?? new AcadMainWindow());
            CDBoxStudioLogger.Info("已打开属性默认表独立 WebView2 窗口。路径：" + CDBoxStudioDefaultProfiles.DefaultsFilePath);
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
                    result.ToastMessage = "属性默认表独立窗口已就绪";
                    return result;

                case "savedefaultprofiles":
                    return SaveDefaultProfiles(request.Argument);

                case "openlegacydefaultprofiles":
                    return OpenLegacyDefaultProfiles();

                case "openlogs":
                    return OpenLogs();

                case "pageerror":
                    CDBoxStudioLogger.Warn("属性默认表独立页面前端初始化失败：" + (request.Argument ?? string.Empty));
                    result.ToastKind = "error";
                    result.ToastMessage = "属性默认表页面初始化失败，已写入 Studio 日志";
                    return result;

                default:
                    result.Handled = false;
                    CDBoxStudioLogger.Warn("属性默认表独立窗口收到未知路由消息：" + request.Name);
                    return result;
            }
        }

        private static CDBoxStudioRouteResult SaveDefaultProfiles(string payload)
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = false,
                ToastKind = "success"
            };

            try
            {
                int count = CDBoxStudioDefaultProfiles.SavePayload(payload);
                result.ToastMessage = "属性默认表已保存：" + count + " 个字段";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "属性默认表保存失败：" + ex.Message;
                CDBoxStudioLogger.Error("独立窗口保存属性默认表失败。", ex);
            }

            return result;
        }

        private static CDBoxStudioRouteResult OpenLegacyDefaultProfiles()
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = true,
                ToastKind = "success"
            };

            try
            {
                using (var form = new QuantityDefaultProfileForm())
                {
                    form.ShowDialog(new AcadMainWindow());
                }

                result.ToastMessage = "旧版属性默认表已关闭，独立窗口已刷新";
                CDBoxStudioLogger.Info("已从独立窗口打开旧版属性默认表。路径：" + CDBoxStudioDefaultProfiles.DefaultsFilePath);
            }
            catch (Exception ex)
            {
                result.RefreshPage = false;
                result.ToastKind = "error";
                result.ToastMessage = "旧版属性默认表打开失败：" + ex.Message;
                CDBoxStudioLogger.Error("独立窗口打开旧版属性默认表失败。", ex);
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
                CDBoxStudioLogger.Error("属性默认表独立窗口打开 Studio 日志目录失败。", ex);
            }

            return result;
        }
    }
}
