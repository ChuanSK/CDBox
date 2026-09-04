namespace TCPipeAutoDraw.UI.Studio
{
    /// <summary>
    /// 旧综合 Studio 的轻量占位组件。完整页面由
    /// CDBox.Wastewater.dll 提供。
    /// </summary>
    internal static class CDBoxStudioQuantityDashboardPage
    {
        public static string BuildEmbeddedSection()
        {
            return "<section id='quantityDashboardPage' class='qd-module-placeholder'>"
                + "<h2>工程量看板</h2><p>该页面已迁移到污水业务模块。</p>"
                + "<button onclick=\"post('openQuantityDashboardWindow','')\">打开工程量看板</button></section>";
        }

        public static string BuildStyles(bool standalone)
        {
            return ".qd-module-placeholder{padding:28px}.qd-module-placeholder button{padding:9px 16px}";
        }

        public static string BuildComponentScript()
        {
            return "window.CDBoxQuantityDashboardPage={create:function(o){return{open:function(){if(o&&o.post)o.post('openQuantityDashboardWindow','');}}}};";
        }

        public static string BuildStandaloneDocument(string theme,
            bool animationsEnabled, string logFilePath)
        {
            return "<!doctype html><html><body>工程量看板已迁移到污水业务模块。</body></html>";
        }
    }
}
