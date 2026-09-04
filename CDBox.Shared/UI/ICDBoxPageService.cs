using System;

namespace CDBox.Shared.UI
{
    public interface ICDBoxPageService
    {
        void Show(CDBoxPageDefinition page);
        bool TryExecuteScript(string pageId, string script);
    }

    public sealed class CDBoxPageDefinition
    {
        public CDBoxPageDefinition(
            string id,
            string title,
            Func<string> htmlFactory,
            Func<CDBoxPageRouteRequest, CDBoxPageRouteResult> routeHandler)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("页面 Id 不能为空。", "id");
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("页面标题不能为空。", "title");

            Id = id.Trim();
            Title = title.Trim();
            HtmlFactory = htmlFactory
                ?? throw new ArgumentNullException("htmlFactory");
            RouteHandler = routeHandler;
            Width = 1180;
            Height = 760;
            MinimumWidth = 900;
            MinimumHeight = 620;
            TitleBarActionText = string.Empty;
            TitleBarActionToolTip = string.Empty;
            TitleBarActionScript = string.Empty;
            TitleBarActionHoverScript = string.Empty;
            TitleBarActionLeaveScript = string.Empty;
        }

        public string Id { get; private set; }
        public string Title { get; private set; }
        public Func<string> HtmlFactory { get; private set; }
        public Func<CDBoxPageRouteRequest, CDBoxPageRouteResult> RouteHandler
        {
            get;
            private set;
        }

        public int Width { get; set; }
        public int Height { get; set; }
        public int MinimumWidth { get; set; }
        public int MinimumHeight { get; set; }
        public string TitleBarActionText { get; set; }
        public string TitleBarActionToolTip { get; set; }
        public string TitleBarActionScript { get; set; }
        public string TitleBarActionHoverScript { get; set; }
        public string TitleBarActionLeaveScript { get; set; }
    }

    public sealed class CDBoxPageRouteRequest
    {
        public CDBoxPageRouteRequest(string name, string argument)
        {
            Name = name ?? string.Empty;
            Argument = argument ?? string.Empty;
        }

        public string Name { get; private set; }
        public string Argument { get; private set; }
    }

    public sealed class CDBoxPageRouteResult
    {
        public CDBoxPageRouteResult()
        {
            RestorePageAfterAction = true;
        }

        /// <summary>
        /// 页面需要暂时让出 CAD 交互焦点时执行的动作。宿主负责在动作期间
        /// 隐藏页面，并在动作结束后恢复页面和刷新内容。
        /// </summary>
        public Action ActionToRun { get; set; }
        /// <summary>
        /// 需要 CAD 同步取点、选择对象等交互并在结束后把结果返回页面时使用。
        /// 宿主会隐藏页面，执行此回调，再恢复页面并应用回调返回的脚本与提示。
        /// </summary>
        public Func<CDBoxPageRouteResult> InteractionToRun { get; set; }
        public bool RestorePageAfterAction { get; set; }
        public bool Handled { get; set; }
        public bool RefreshPage { get; set; }
        public string ToastMessage { get; set; }
        public string ToastKind { get; set; }
        public string WindowTitleSuffix { get; set; }
        public string ExecuteScript { get; set; }
    }

    public sealed class CDBoxPageAppearance
    {
        public string Theme { get; set; }
        public bool AnimationsEnabled { get; set; }
        public string LogFilePath { get; set; }

        public CDBoxPageAppearance()
        {
            Theme = "light";
            AnimationsEnabled = true;
            LogFilePath = string.Empty;
        }
    }

    public interface ICDBoxPageAppearanceService
    {
        CDBoxPageAppearance GetAppearance();
    }
}
