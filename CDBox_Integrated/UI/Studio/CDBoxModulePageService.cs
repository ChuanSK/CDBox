using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CDBox.Shared.UI;
using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    /// <summary>
    /// 把业务模块页面接入现有 CDBox WebView2 窗口宿主。
    /// </summary>
    internal sealed class CDBoxModulePageService : ICDBoxPageService
    {
        private readonly Dictionary<string, CDBoxStudioWebPageForm> _pages =
            new Dictionary<string, CDBoxStudioWebPageForm>(
                StringComparer.OrdinalIgnoreCase);

        public void Show(CDBoxPageDefinition page)
        {
            if (page == null) throw new ArgumentNullException("page");

            CDBoxStudioWebPageForm existing;
            if (_pages.TryGetValue(page.Id, out existing)
                && existing != null && !existing.IsDisposed)
            {
                existing.WindowState = FormWindowState.Normal;
                existing.Activate();
                existing.RefreshPage();
                return;
            }

            var form = new CDBoxStudioWebPageForm(
                page.Title,
                page.HtmlFactory,
                delegate(CDBoxStudioRouteRequest request)
                {
                    return Route(page, request);
                },
                page.Id);
            form.Width = PositiveOrDefault(page.Width, 1180);
            form.Height = PositiveOrDefault(page.Height, 760);
            form.MinimumSize = new Size(
                PositiveOrDefault(page.MinimumWidth, 900),
                PositiveOrDefault(page.MinimumHeight, 620));
            _pages[page.Id] = form;
            form.FormClosed += delegate
            {
                CDBoxStudioWebPageForm current;
                if (_pages.TryGetValue(page.Id, out current)
                    && ReferenceEquals(current, form))
                    _pages.Remove(page.Id);
            };
            form.Show(new AcadMainWindow());
        }

        public void CloseAll()
        {
            var pages = new List<CDBoxStudioWebPageForm>(_pages.Values);
            _pages.Clear();
            foreach (CDBoxStudioWebPageForm page in pages)
            {
                try
                {
                    if (page != null && !page.IsDisposed) page.Close();
                }
                catch (Exception ex)
                {
                    CDBoxStudioLogger.Error(
                        "关闭业务模块页面失败。", ex);
                }
            }
        }

        private static CDBoxStudioRouteResult Route(
            CDBoxPageDefinition page,
            CDBoxStudioRouteRequest request)
        {
            try
            {
                CDBoxPageRouteResult result = page.RouteHandler == null
                    ? null
                    : page.RouteHandler(new CDBoxPageRouteRequest(
                        request == null ? string.Empty : request.Name,
                        request == null ? string.Empty : request.Argument));
                if (result == null)
                    return new CDBoxStudioRouteResult { Handled = false };

                return new CDBoxStudioRouteResult
                {
                    Handled = result.Handled,
                    RefreshPage = result.RefreshPage,
                    ToastMessage = result.ToastMessage,
                    ToastKind = result.ToastKind,
                    WindowTitleSuffix = result.WindowTitleSuffix,
                    ExecuteScript = result.ExecuteScript
                };
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error(
                    "业务模块页面路由失败：" + page.Id, ex);
                return new CDBoxStudioRouteResult
                {
                    Handled = true,
                    ToastKind = "error",
                    ToastMessage = "页面操作失败：" + ex.Message
                };
            }
        }

        private static int PositiveOrDefault(int value, int fallback)
        {
            return value > 0 ? value : fallback;
        }
    }
}
