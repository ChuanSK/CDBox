using System;

namespace CDBox.Shared.UI
{
    public interface ICDBoxPageService
    {
        void Show(CDBoxPageDefinition page);
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
        public bool Handled { get; set; }
        public bool RefreshPage { get; set; }
        public string ToastMessage { get; set; }
        public string ToastKind { get; set; }
        public string WindowTitleSuffix { get; set; }
        public string ExecuteScript { get; set; }
    }
}
