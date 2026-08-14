using System;
using System.Net;
using System.Text;
using CDBox.RealEstate.Models;
using CDBox.Shared.UI;

namespace CDBox.RealEstate.UI
{
    internal static class RealEstateHomePage
    {
        public const string PageId = "realestate-home";

        public static CDBoxPageDefinition Create(
            RealEstateWorkspaceState state)
        {
            if (state == null) throw new ArgumentNullException("state");

            return new CDBoxPageDefinition(
                PageId,
                "CDBox 不动产",
                delegate { return BuildHtml(state); },
                Route)
            {
                Width = 1120,
                Height = 720,
                MinimumWidth = 860,
                MinimumHeight = 580
            };
        }

        private static CDBoxPageRouteResult Route(
            CDBoxPageRouteRequest request)
        {
            bool ready = request != null
                && string.Equals(request.Name, "ready",
                    StringComparison.OrdinalIgnoreCase);
            return new CDBoxPageRouteResult { Handled = ready };
        }

        private static string BuildHtml(RealEstateWorkspaceState state)
        {
            var html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">");
            html.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            html.Append("<style>");
            html.Append("*{box-sizing:border-box}body{margin:0;font-family:'Microsoft YaHei UI',sans-serif;background:#f7f9fe;color:#172033}");
            html.Append("main{min-height:100vh;padding:48px;display:grid;place-items:center}.panel{width:min(860px,100%);background:#fff;border:1px solid #d8e3f2;border-radius:26px;padding:38px;box-shadow:0 24px 70px rgba(30,64,175,.10)}");
            html.Append(".kicker{color:#2563eb;font-size:12px;font-weight:800;letter-spacing:.14em;text-transform:uppercase}h1{font-size:30px;margin:12px 0 10px}p{color:#60708b;line-height:1.8;margin:0}.status{margin-top:28px;padding:18px;border-radius:18px;background:linear-gradient(135deg,#eff6ff,#f5f3ff);display:flex;align-items:center;justify-content:space-between;gap:18px}.status strong{color:#1d4ed8}.version{font:12px Consolas,monospace;color:#64748b;border:1px solid #d8e3f2;background:#fff;border-radius:999px;padding:6px 10px}");
            html.Append("@media(prefers-color-scheme:dark){body{background:#101827;color:#e8eef9}.panel{background:#172033;border-color:#2a3a55;box-shadow:none}p{color:#9cafca}.status{background:linear-gradient(135deg,#182b4b,#282249)}.version{background:#101827;border-color:#2a3a55;color:#9cafca}}@media(max-width:720px){main{padding:24px}.panel{padding:28px}.status{align-items:flex-start;flex-direction:column}}</style></head><body>");
            html.Append("<main><section class=\"panel\"><span class=\"kicker\">CDBox RealEstate</span><h1>CDBox 不动产</h1><p>");
            html.Append(H(state.Message));
            html.Append("</p><div class=\"status\"><strong>");
            html.Append(H(state.StatusLabel));
            html.Append("</strong><span class=\"version\">v");
            html.Append(H(state.ModuleVersion));
            html.Append("</span></div></section></main><script>if(window.chrome&&chrome.webview){chrome.webview.postMessage('studio|ready|realestate');}</script></body></html>");
            return html.ToString();
        }

        private static string H(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
