using System;
using System.Threading;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioUpdateRoutes
    {
        private readonly CDBoxStudioSettings _settings;
        private readonly Action<string> _scriptSink;
        private readonly Action<string> _applySettingsArgument;
        private int _checkRunning;
        private int _downloadRunning;

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
            if (name == "checkupdate")
            {
                result = RouteCheckUpdate(request.Argument);
                return true;
            }
            if (name == "downloadupdate")
            {
                result = RouteDownloadUpdate(request.Argument);
                return true;
            }
            if (name == "openinstaller")
            {
                result = RouteOpenInstaller(request.Argument);
                return true;
            }
            return false;
        }

        private CDBoxStudioRouteResult RouteOpenInstaller(string argument)
        {
            ApplyAndSaveSettings(argument);
            if (!CDBoxStudioInstallerLauncher.ManagementInstallerExists)
            {
                CDBoxStudioRouteResult download = RouteDownloadUpdate(
                    argument);
                download.ToastMessage = "未找到本地安装器，正在下载官方安装器";
                return download;
            }
            CDBoxStudioInstallerLaunchResult launch =
                CDBoxStudioInstallerLauncher
                    .PrepareAndLaunchManagementInstaller();
            return new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = false,
                ToastKind = launch.Started ? "success" : "error",
                ToastMessage = launch.Started
                    ? "安装器已打开，可增减模块或卸载插件"
                    : "安装器打开失败：" + launch.ErrorMessage
            };
        }

        private CDBoxStudioRouteResult RouteCheckUpdate(string argument)
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = false,
                ToastKind = "info",
                ToastMessage = "正在后台检查更新",
                ExecuteScript = "window.CDBoxStudioUpdateProgress && window.CDBoxStudioUpdateProgress({percent:8,message:'正在读取 update.json',kind:'running'});"
            };

            try
            {
                ApplyAndSaveSettings(argument);
                if (Interlocked.CompareExchange(ref _checkRunning, 1, 0) != 0)
                {
                    result.ToastKind = "warning";
                    result.ToastMessage = "更新检查正在进行，请稍候";
                    return result;
                }

                CDBoxStudioSettings settingsSnapshot = CloneSettings(_settings);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        CDBoxStudioUpdateResult update = CDBoxStudioUpdateService.Check(settingsSnapshot);
                        PushScript("window.CDBoxStudioUpdateResult && window.CDBoxStudioUpdateResult(" + update.ToJson() + ");");
                        if (update.Success && update.UpdateAvailable)
                            PushScript("window.CDBoxStudioToast && window.CDBoxStudioToast(" + ToJsString("发现新版本：" + update.LatestVersion) + ",'success');");
                        else if (update.Success)
                            PushScript("window.CDBoxStudioToast && window.CDBoxStudioToast('当前已是最新版本','info');");
                        else
                            PushScript("window.CDBoxStudioToast && window.CDBoxStudioToast('检查更新失败，请查看更新信息或 Studio 日志','warning');");
                    }
                    catch (Exception ex)
                    {
                        var update = NewFailedCheckResult(settingsSnapshot, ex);
                        PushScript("window.CDBoxStudioUpdateResult && window.CDBoxStudioUpdateResult(" + update.ToJson() + ");");
                        CDBoxStudioLogger.Error("Studio 后台检查更新失败。", ex);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _checkRunning, 0);
                    }
                });
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _checkRunning, 0);
                CDBoxStudioUpdateResult update = NewFailedCheckResult(_settings, ex);
                result.ExecuteScript = "window.CDBoxStudioUpdateResult && window.CDBoxStudioUpdateResult(" + update.ToJson() + ");";
                result.ToastKind = "error";
                result.ToastMessage = "检查更新失败：" + ex.Message;
                CDBoxStudioLogger.Error("Studio 检查更新路由失败。", ex);
            }
            return result;
        }

        private CDBoxStudioRouteResult RouteDownloadUpdate(string argument)
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = false,
                ToastKind = "info",
                ToastMessage = "已开始下载、校验并准备安装更新",
                ExecuteScript = "window.CDBoxStudioUpdateProgress && window.CDBoxStudioUpdateProgress({percent:0,message:'准备下载安装器',kind:'running'});"
            };

            try
            {
                ApplyAndSaveSettings(argument);
                if (Interlocked.CompareExchange(ref _downloadRunning, 1, 0) != 0)
                {
                    result.ToastKind = "warning";
                    result.ToastMessage = "安装器下载正在进行，请稍候";
                    return result;
                }

                CDBoxStudioSettings settingsSnapshot = CloneSettings(_settings);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        CDBoxStudioUpdateDownloadResult download = CDBoxStudioUpdateService.DownloadAndVerify(settingsSnapshot, delegate(int percent, string message)
                        {
                            PushUpdateProgress(percent, message, "progress");
                        });
                        PushScript("window.CDBoxStudioDownloadResult && window.CDBoxStudioDownloadResult(" + download.ToJson() + ");");
                        if (download.Success && download.Verified && download.InstallerStarted)
                            PushScript("window.CDBoxStudioToast && window.CDBoxStudioToast('更新器已启动，请正常关闭 AutoCAD 以完成安装','success');");
                        else if (download.Success && download.Verified)
                            PushScript("window.CDBoxStudioToast && window.CDBoxStudioToast('安装器校验通过，但未能启动，请查看 Studio 日志','warning');");
                    }
                    catch (Exception ex)
                    {
                        CDBoxStudioUpdateDownloadResult download = NewFailedDownloadResult(settingsSnapshot, ex);
                        PushScript("window.CDBoxStudioDownloadResult && window.CDBoxStudioDownloadResult(" + download.ToJson() + ");");
                        CDBoxStudioLogger.Error("Studio 后台下载更新失败。", ex);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _downloadRunning, 0);
                    }
                });
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _downloadRunning, 0);
                CDBoxStudioUpdateDownloadResult download = NewFailedDownloadResult(_settings, ex);
                result.ExecuteScript = "window.CDBoxStudioDownloadResult && window.CDBoxStudioDownloadResult(" + download.ToJson() + ");";
                result.ToastKind = "error";
                result.ToastMessage = "安装器下载失败：" + ex.Message;
                CDBoxStudioLogger.Error("Studio 下载更新路由失败。", ex);
            }
            return result;
        }

        private void ApplyAndSaveSettings(string argument)
        {
            if (_applySettingsArgument != null) _applySettingsArgument(argument);
            CDBoxStudioSettingsStore.Save(_settings);
        }

        private static CDBoxStudioSettings CloneSettings(CDBoxStudioSettings source)
        {
            var settings = new CDBoxStudioSettings();
            if (source != null)
            {
                settings.Theme = source.Theme;
                settings.AnimationsEnabled = source.AnimationsEnabled;
                settings.AnnotationHudNormalOpacity = source.AnnotationHudNormalOpacity;
                settings.AnnotationHudHoverOpacity = source.AnnotationHudHoverOpacity;
                settings.AnnotationHudGlowEnabled = source.AnnotationHudGlowEnabled;
                settings.AnnotationHudGlowIntensity = source.AnnotationHudGlowIntensity;
                settings.DoubleClickOpenEnabled = source.DoubleClickOpenEnabled;
                settings.ColorOutputMode = source.ColorOutputMode;
                settings.UpdateChannel = source.UpdateChannel;
            }
            settings.Normalize();
            return settings;
        }

        private static CDBoxStudioUpdateResult NewFailedCheckResult(CDBoxStudioSettings settings, Exception ex)
        {
            CDBoxStudioComponentUpdatePlan plan =
                CDBoxStudioComponentUpdatePlan.Capture();
            return new CDBoxStudioUpdateResult
            {
                Success = false,
                ErrorMessage = ex == null ? string.Empty : ex.Message,
                CurrentVersion = CDBoxStudioUpdateService.CurrentVersion,
                CurrentVersionCode = CDBoxStudioUpdateService.CurrentVersionCode,
                LatestVersion = string.Empty,
                Channel = settings == null ? CDBoxStudioUpdateService.DefaultChannel : settings.UpdateChannel,
                SourceName = CDBoxStudioUpdateService.DefaultUpdateSourceName,
                SourceUrl = CDBoxStudioUpdateService.DefaultUpdateSourceUrl,
                InstalledComponentIds = plan.SelectedComponentIds,
                InstalledComponentNames = plan.SelectedComponentNames,
                ComponentPlanText = plan.Summary,
                ComponentStateWarning = plan.Warning
            };
        }

        private static CDBoxStudioUpdateDownloadResult NewFailedDownloadResult(CDBoxStudioSettings settings, Exception ex)
        {
            CDBoxStudioComponentUpdatePlan plan =
                CDBoxStudioComponentUpdatePlan.Capture();
            return new CDBoxStudioUpdateDownloadResult
            {
                Success = false,
                Verified = false,
                ErrorMessage = ex == null ? string.Empty : ex.Message,
                CurrentVersion = CDBoxStudioUpdateService.CurrentVersion,
                CurrentVersionCode = CDBoxStudioUpdateService.CurrentVersionCode,
                Channel = settings == null ? CDBoxStudioUpdateService.DefaultChannel : settings.UpdateChannel,
                InstalledComponentIds = plan.SelectedComponentIds,
                InstalledComponentNames = plan.SelectedComponentNames,
                ComponentPlanText = plan.Summary,
                ComponentStateWarning = plan.Warning
            };
        }

        private void PushUpdateProgress(int percent, string message, string kind)
        {
            int safePercent = Math.Max(0, Math.Min(100, percent));
            PushScript("window.CDBoxStudioUpdateProgress && window.CDBoxStudioUpdateProgress({percent:"
                + safePercent + ",message:" + ToJsString(message ?? string.Empty)
                + ",kind:" + ToJsString(kind ?? "progress") + "});");
        }

        private void PushScript(string script)
        {
            if (_scriptSink == null || string.IsNullOrWhiteSpace(script)) return;
            try { _scriptSink(script); }
            catch (Exception ex) { CDBoxStudioLogger.Error("向 Studio 前端推送脚本失败。", ex); }
        }

        private static string ToJsString(string value)
        {
            if (value == null) return "''";
            return "'" + value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n") + "'";
        }
    }
}
