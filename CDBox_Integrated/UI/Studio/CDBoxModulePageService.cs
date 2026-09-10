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
        private readonly string _category;
        private readonly Dictionary<string, CDBoxStudioWebPageForm> _pages =
            new Dictionary<string, CDBoxStudioWebPageForm>(
                StringComparer.OrdinalIgnoreCase);

        public CDBoxModulePageService(string category = null)
        {
            _category = string.IsNullOrWhiteSpace(category)
                ? "业务模块" : category.Trim();
        }

        public void Show(CDBoxPageDefinition page)
        {
            if (page == null) throw new ArgumentNullException("page");

            CDBoxStudioWebPageForm existing;
            if (_pages.TryGetValue(page.Id, out existing)
                && existing != null && !existing.IsDisposed)
            {
                if (page.ReplaceExistingPage) existing.Close();
                else
                {
                    existing.Show();
                    existing.WindowState = FormWindowState.Normal;
                    existing.Activate();
                    existing.RefreshPage();
                    return;
                }
            }

            CDBoxStudioWebPageForm form = null;
            form = new CDBoxStudioWebPageForm(
                page.Title,
                page.HtmlFactory,
                delegate(CDBoxStudioRouteRequest request)
                {
                    return Route(page, request, form, _category);
                },
                page.Id);
            form.Width = PositiveOrDefault(page.Width, 1180);
            form.Height = PositiveOrDefault(page.Height, 760);
            form.MinimumSize = new Size(
                PositiveOrDefault(page.MinimumWidth, 900),
                PositiveOrDefault(page.MinimumHeight, 620));
            form.ConfigureTitleBarAction(page.TitleBarActionText,
                page.TitleBarActionToolTip, page.TitleBarActionScript,
                page.TitleBarActionHoverScript,
                page.TitleBarActionLeaveScript);
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

        public bool TryExecuteScript(string pageId, string script)
        {
            if (string.IsNullOrWhiteSpace(pageId)
                || string.IsNullOrWhiteSpace(script)) return false;
            CDBoxStudioWebPageForm page;
            if (!_pages.TryGetValue(pageId.Trim(), out page)
                || page == null || page.IsDisposed) return false;
            page.TryExecutePageScript(script);
            return true;
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
            CDBoxStudioRouteRequest request,
            CDBoxStudioWebPageForm form,
            string category)
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

                var mapped = new CDBoxStudioRouteResult();
                CopyResult(result, mapped);
                Action actionToRun = result.ActionToRun;
                Func<CDBoxPageRouteResult> interaction =
                    result.InteractionToRun;
                bool restorePageAfterAction = interaction != null
                    || result.RestorePageAfterAction;
                CDBoxStudioAction action = actionToRun == null
                    && interaction == null
                    ? null
                    : new CDBoxStudioAction(
                        "module-page:" + page.Id,
                        page.Title,
                        category,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        CDBoxStudioActionKind.Module,
                        true,
                        true,
                        delegate
                        {
                            bool visible = form != null && !form.IsDisposed
                                && form.Visible;
                            if (visible) form.Hide();
                            try
                            {
                                if (interaction != null)
                                {
                                    CDBoxPageRouteResult completed =
                                        interaction();
                                    if (completed != null)
                                        CopyResult(completed, mapped);
                                }
                                else actionToRun();
                            }
                            finally
                            {
                                if (restorePageAfterAction && visible
                                    && form != null
                                    && !form.IsDisposed)
                                {
                                    form.Show();
                                    form.WindowState =
                                        FormWindowState.Normal;
                                    form.Activate();
                                }
                            }
                        });

                mapped.ActionToRun = action;
                return mapped;
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

        private static void CopyResult(CDBoxPageRouteResult source,
            CDBoxStudioRouteResult target)
        {
            if (source == null || target == null) return;
            target.Handled = source.Handled;
            target.RefreshPage = source.RefreshPage;
            target.ToastMessage = source.ToastMessage;
            target.ToastKind = source.ToastKind;
            target.WindowTitleSuffix = source.WindowTitleSuffix;
            target.ExecuteScript = source.ExecuteScript;
        }

        private static int PositiveOrDefault(int value, int fallback)
        {
            return value > 0 ? value : fallback;
        }
    }
}
