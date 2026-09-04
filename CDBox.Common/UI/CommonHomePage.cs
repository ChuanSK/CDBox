using System;
using System.Net;
using CDBox.Shared.UI;

namespace CDBox.Common.UI
{
    internal static class CommonHomePage
    {
        public static CDBoxPageDefinition Create(string version)
        {
            return new CDBoxPageDefinition(
                "common-home", "CDBox 公共业务",
                delegate
                {
                    return "<!doctype html><html lang='zh-CN'><head>"
                        + "<meta charset='utf-8'><style>body{margin:0;padding:42px;"
                        + "font-family:'Microsoft YaHei UI',sans-serif;background:#f7f9fe;"
                        + "color:#172033}.card{max-width:760px;margin:auto;padding:32px;"
                        + "border:1px solid #d8e3f2;border-radius:22px;background:#fff}"
                        + "h1{margin-top:0}p{color:#60708b;line-height:1.8}</style></head>"
                        + "<body><section class='card'><h1>CDBox 公共业务</h1>"
                        + "<p>简码识别、图框工具和 Excel 转 CAD 表格由独立公共业务组件承载；图层管理器作为各业务的共同基础已置于必装基础组件。</p><small>v"
                        + WebUtility.HtmlEncode(version ?? string.Empty)
                        + "</small></section></body></html>";
                },
                delegate(CDBoxPageRouteRequest request)
                {
                    return new CDBoxPageRouteResult
                    {
                        Handled = request != null && string.Equals(
                            request.Name, "ready",
                            StringComparison.OrdinalIgnoreCase)
                    };
                });
        }
    }
}
