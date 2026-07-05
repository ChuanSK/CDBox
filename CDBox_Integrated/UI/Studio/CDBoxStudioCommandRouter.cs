using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioCommandRouter
    {
        private readonly Dictionary<string, CDBoxStudioAction> _actionsById;
        private readonly CDBoxStudioState _state;

        public CDBoxStudioCommandRouter(Dictionary<string, CDBoxStudioAction> actionsById, CDBoxStudioState state)
        {
            if (actionsById == null) throw new ArgumentNullException("actionsById");
            _actionsById = actionsById;
            _state = state ?? new CDBoxStudioState();
        }

        public CDBoxStudioRouteResult Route(CDBoxStudioRouteRequest request)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "info" };
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                result.Handled = false;
                return result;
            }

            string name = request.Name.Trim().ToLowerInvariant();
            switch (name)
            {
                case "ready":
                    CDBoxStudioLogger.Info("Studio 前端已就绪。动作数量：" + _actionsById.Count);
                    result.ToastMessage = "CDBox Studio 已就绪";
                    result.ToastKind = "success";
                    return result;

                case "run":
                    return RouteRun(request.Argument);

                case "favorite":
                    return RouteFavorite(request.Argument);

                case "openlogs":
                    return RouteOpenLogs();

                case "filter":
                    result.WindowTitleSuffix = request.Argument;
                    return result;

                default:
                    result.Handled = false;
                    CDBoxStudioLogger.Warn("收到未知 Studio 路由消息：" + request.Name);
                    return result;
            }
        }

        private CDBoxStudioRouteResult RouteRun(string id)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "info" };

            CDBoxStudioAction action;
            if (string.IsNullOrWhiteSpace(id) || !_actionsById.TryGetValue(id.Trim(), out action) || action == null)
            {
                result.ToastMessage = "未找到该功能入口";
                result.ToastKind = "warning";
                CDBoxStudioLogger.Warn("运行入口失败，未找到动作：" + (id ?? string.Empty));
                return result;
            }

            if (!action.Enabled)
            {
                result.ToastMessage = action.Title + " 暂未启用";
                result.ToastKind = "warning";
                return result;
            }

            _state.MarkRecent(action.Id);
            SaveState();
            CDBoxStudioLogger.Info("运行入口：" + action.Title + " [" + action.Id + "]");
            result.ActionToRun = action;
            result.RefreshPage = true;
            return result;
        }

        private CDBoxStudioRouteResult RouteFavorite(string id)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, RefreshPage = true, ToastKind = "success" };

            CDBoxStudioAction action;
            if (string.IsNullOrWhiteSpace(id) || !_actionsById.TryGetValue(id.Trim(), out action) || action == null)
            {
                result.ToastMessage = "未找到该功能入口";
                result.ToastKind = "warning";
                return result;
            }

            bool added = _state.ToggleFavorite(action.Id);
            SaveState();
            CDBoxStudioLogger.Info((added ? "收藏入口：" : "取消收藏入口：") + action.Title + " [" + action.Id + "]");
            result.ToastMessage = added ? "已收藏：" + action.Title : "已取消收藏：" + action.Title;
            return result;
        }

        private CDBoxStudioRouteResult RouteOpenLogs()
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };

            try
            {
                CDBoxStudioLogger.OpenLogFolder();
                result.ToastMessage = "已打开 Studio 日志目录";
                CDBoxStudioLogger.Info("打开 Studio 日志目录。路径：" + CDBoxStudioLogger.LogDirectory);
            }
            catch (Exception ex)
            {
                result.ToastMessage = "日志目录打开失败：" + ex.Message;
                result.ToastKind = "error";
                CDBoxStudioLogger.Error("打开 Studio 日志目录失败。", ex);
            }

            return result;
        }

        private void SaveState()
        {
            _state.RemoveMissingActions(_actionsById.Keys.ToList());
            CDBoxStudioStateStore.Save(_state);
        }
    }
}
