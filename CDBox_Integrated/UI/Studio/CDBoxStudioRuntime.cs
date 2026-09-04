using System;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioRuntimeInfo
    {
        public bool Available { get; set; }
        public string Version { get; set; }
        public string ErrorMessage { get; set; }
    }

    internal static class CDBoxStudioRuntime
    {
        private static readonly object EnvironmentSyncRoot = new object();
        private static Task<CoreWebView2Environment> _environmentTask;

        public static CDBoxStudioRuntimeInfo Detect()
        {
            var info = new CDBoxStudioRuntimeInfo();

            try
            {
                string version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                info.Version = version ?? string.Empty;
                info.Available = !string.IsNullOrWhiteSpace(version);

                if (!info.Available)
                {
                    info.ErrorMessage = "未检测到 Microsoft Edge WebView2 Runtime。";
                }
            }
            catch (Exception ex)
            {
                info.Available = false;
                info.ErrorMessage = ex.Message;
                CDBoxStudioLogger.Error("WebView2 Runtime 检测失败。", ex);
            }

            return info;
        }

        public static Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            lock (EnvironmentSyncRoot)
            {
                if (_environmentTask == null || _environmentTask.IsCanceled
                    || _environmentTask.IsFaulted)
                    _environmentTask = CreateEnvironmentAsync();
                return _environmentTask;
            }
        }

        private static async Task<CoreWebView2Environment>
            CreateEnvironmentAsync()
        {
            string userDataFolder =
                CDBoxStudioWebViewProfile.EnsureWritableUserDataFolder();
            CDBoxStudioLogger.Info("WebView2 使用用户数据目录："
                + userDataFolder);
            try
            {
                return await CoreWebView2Environment.CreateAsync(null,
                    userDataFolder);
            }
            catch (Exception primaryException)
            {
                string recoveryFolder = CDBoxStudioWebViewProfile
                    .EnsureWritableRecoveryFolder();
                CDBoxStudioLogger.Error(
                    "WebView2 常用数据目录初始化失败，改用备用目录："
                    + recoveryFolder, primaryException);
                try
                {
                    return await CoreWebView2Environment.CreateAsync(null,
                        recoveryFolder);
                }
                catch (Exception recoveryException)
                {
                    throw new InvalidOperationException(
                        "WebView2 无法创建用户数据目录。常用目录："
                        + userDataFolder + "；备用目录：" + recoveryFolder
                        + "。请确认当前 Windows 用户对这些目录具有写入权限。",
                        recoveryException);
                }
            }
        }
    }
}
