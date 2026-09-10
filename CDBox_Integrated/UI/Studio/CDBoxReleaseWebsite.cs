using System;
using System.Diagnostics;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxReleaseWebsite
    {
        public static string Url { get { return CDBoxStudioUpdateSourceCatalog.OfficialBaseUrl; } }

        public static void Open()
        {
            // 仅打开产品固定发布网站，不接受网络响应中的任意链接或命令。
            using (var process = Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true }))
            {
                if (process == null) throw new InvalidOperationException("无法打开发布网站，请使用浏览器访问：" + Url);
            }
        }
    }
}
