using CDBox.Shared.Services;
using CDBox.Shared.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    // 页面脚本测试不执行 CAD 路由；生产路由由 CDBox.dll 编译验证。
    internal sealed class LayerManagerPageRouter
    {
        public LayerManagerPageRouter(ICDBoxLogger logger,
            ICDBoxPageService pages)
        {
        }

        public CDBoxPageRouteResult Route(CDBoxPageRouteRequest request)
        {
            return new CDBoxPageRouteResult
            {
                Handled = request != null && string.Equals(request.Name,
                    "ready", System.StringComparison.OrdinalIgnoreCase)
            };
        }
    }
}
