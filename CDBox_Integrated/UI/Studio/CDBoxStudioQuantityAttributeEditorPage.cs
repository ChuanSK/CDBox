namespace TCPipeAutoDraw.UI.Studio
{
    /// <summary>
    /// 旧综合 Studio 的嵌入占位壳。完整属性编辑器页面已迁入
    /// CDBox.Wastewater，旧工作台只负责转到独立模块页面。
    /// </summary>
    internal static class CDBoxStudioQuantityAttributeEditorPage
    {
        public static string BuildEmbeddedSection()
        {
            return "<section id=\"quantityAttributeEditorPage\" "
                + "style=\"display:none\"></section>";
        }

        public static string BuildStyles(bool standalone)
        {
            return string.Empty;
        }

        public static string BuildComponentScript()
        {
            return "(function(w){w.CDBoxQuantityAttributeEditorPage="
                + "{create:function(o){var r=document.getElementById("
                + "(o||{}).rootId||'quantityAttributeEditorPage');"
                + "if(r)r.innerHTML='<button id=\"openWastewaterAttributeEditor\">"
                + "打开污水属性编辑器</button>';var b=document.getElementById("
                + "'openWastewaterAttributeEditor');if(b)b.onclick=function(){"
                + "if(o&&o.post)o.post('openQuantityAttributeEditorWindow','{}');};"
                + "return{open:function(){if(o&&o.post)o.post("
                + "'openQuantityAttributeEditorWindow','{}');},"
                + "suspend:function(){}};}};})(window);";
        }
    }
}
