using System;
using System.Web;

namespace TCPipeAutoDraw.UI.Studio
{
    /// <summary>仅定义公开版本服务的位置，不包含存储路径或安装器入口。</summary>
    internal static class CDBoxStudioUpdateSourceCatalog
    {
        public const string OfficialBaseUrl =
            "https://cdbox-release-cdbox-d9gsv9fvj6a1aed69.webapps.tcloudbase.com/";
        // Product-owned endpoint; never read an override from user settings.
        public const string DefaultReleaseServiceUrl =
            "https://cdbox-d9gsv9fvj6a1aed69-1472504232.ap-shanghai.app.tcloudbase.com/api/releases/latest";

        public static string BuildServiceUrl(string serviceUrl, string channel)
        {
            Uri uri;
            if (string.IsNullOrWhiteSpace(serviceUrl))
                throw new InvalidOperationException("尚未配置版本服务地址，请前往发布网站查看最新版本。");
            if (!Uri.TryCreate(serviceUrl.Trim(), UriKind.Absolute, out uri)
                || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidOperationException("版本服务地址必须是无账号信息、无片段的 HTTPS 地址。");
            string selected = (channel ?? "").Trim().ToLowerInvariant();
            if (selected != "stable" && selected != "preview")
                throw new InvalidOperationException("请选择 Stable 或 Preview 更新通道。");
            var query = HttpUtility.ParseQueryString(uri.Query);
            query["channel"] = selected;
            var builder = new UriBuilder(uri) { Query = query.ToString() };
            return builder.Uri.AbsoluteUri;
        }
    }
}
