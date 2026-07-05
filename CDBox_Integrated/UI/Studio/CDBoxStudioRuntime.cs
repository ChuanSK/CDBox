using System;
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
    }
}
