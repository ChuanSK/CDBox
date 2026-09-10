using System;
using System.Threading;
using System.Web.Script.Serialization;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioUpdateRoutes
    {
        private readonly CDBoxStudioSettings _settings;
        private readonly Action<string> _scriptSink;
        private readonly Action<string> _applySettingsArgument;
        private int _checkRunning;

        public CDBoxStudioUpdateRoutes(CDBoxStudioSettings settings, Action<string> scriptSink, Action<string> applySettingsArgument)
        {
            _settings = settings ?? new CDBoxStudioSettings();
            _scriptSink = scriptSink;
            _applySettingsArgument = applySettingsArgument;
        }

        public bool TryRoute(CDBoxStudioRouteRequest request, out CDBoxStudioRouteResult result)
        {
            result = null;
            if (request == null || string.IsNullOrWhiteSpace(request.Name)) return false;
            string name = request.Name.Trim().ToLowerInvariant();
            if (name == "checkupdate") result = Check(request.Argument);
            // 旧页面的消息只引导官网，不再存在安装包网络下载执行路径。
            else if (name == "openreleasewebsite" || name == "downloadupdate") result = OpenWebsite();
            else if (name == "openinstaller") result = OpenInstaller();
            else return false;
            return true;
        }

        private CDBoxStudioRouteResult OpenInstaller()
        {
            if (!CDBoxStudioInstallerLauncher.ManagementInstallerExists)
            {
                var result = OpenWebsite();
                if (result.ToastKind == "success")
                    result.ToastMessage = "未找到本地组件管理器，请从发布网站获取安装器";
                return result;
            }
            var launch = CDBoxStudioInstallerLauncher.PrepareAndLaunchManagementInstaller();
            return Message(launch.Started ? "success" : "error",
                launch.Started ? "组件管理器已打开，可增减模块或卸载插件" : "组件管理器打开失败：" + launch.ErrorMessage);
        }

        private static CDBoxStudioRouteResult OpenWebsite()
        {
            try
            {
                CDBoxReleaseWebsite.Open();
                return Message("success", "已打开 CDBox 发布网站");
            }
            catch (Exception ex)
            {
                return Message("error", ex.Message);
            }
        }

        private CDBoxStudioRouteResult Check(string argument)
        {
            if (Interlocked.CompareExchange(ref _checkRunning, 1, 0) != 0)
                return Message("warning", "更新检查正在进行，请稍候");
            try
            {
                if (_applySettingsArgument != null) _applySettingsArgument(argument);
                CDBoxStudioSettingsStore.Save(_settings);
                var snapshot = new CDBoxStudioSettings
                {
                    UpdateChannel = _settings.UpdateChannel,
                    ReleaseServiceUrl = _settings.ReleaseServiceUrl
                };
                snapshot.Normalize();
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        var update = CDBoxStudioUpdateService.Check(snapshot);
                        PushScript("window.CDBoxStudioUpdateResult && window.CDBoxStudioUpdateResult(" + update.ToJson() + ");");
                    }
                    catch (Exception ex)
                    {
                        PushFailure(snapshot.UpdateChannel, ex);
                    }
                    finally { Interlocked.Exchange(ref _checkRunning, 0); }
                });
                var result = Message("info", "正在后台查询版本服务");
                result.ExecuteScript = "window.CDBoxStudioUpdateProgress && window.CDBoxStudioUpdateProgress({percent:8,message:'正在查询版本服务',kind:'running'});";
                return result;
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _checkRunning, 0);
                var result = Message("error", "检查更新失败：" + ex.Message);
                result.ExecuteScript = "window.CDBoxStudioUpdateResult && window.CDBoxStudioUpdateResult("
                    + new CDBoxStudioUpdateResult { Channel = _settings.UpdateChannel, ErrorMessage = ex.Message }.ToJson() + ");";
                return result;
            }
        }

        private void PushFailure(string channel, Exception ex)
        {
            var update = new CDBoxStudioUpdateResult { Channel = channel, ErrorMessage = ex.Message };
            PushScript("window.CDBoxStudioUpdateResult && window.CDBoxStudioUpdateResult(" + update.ToJson() + ");");
            CDBoxStudioLogger.Error("后台检查版本服务失败。", ex);
        }

        private static CDBoxStudioRouteResult Message(string kind, string message)
        {
            return new CDBoxStudioRouteResult
            {
                Handled = true, RefreshPage = false, ToastKind = kind, ToastMessage = message
            };
        }

        private void PushScript(string script)
        {
            if (_scriptSink == null) return;
            try { _scriptSink(script); }
            catch (Exception ex) { CDBoxStudioLogger.Error("更新结果推送失败。", ex); }
        }
    }
}
