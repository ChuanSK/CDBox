using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.Core.Modules
{
    /// <summary>
    /// CDBOX 模块注册器。
    /// 后续新增模块时，优先在这里注册，避免继续膨胀命令入口和主界面代码。
    /// </summary>
    public static class TCModuleRegistry
    {
        public static IList<ITCModule> CreateDefaultModules(
            Action layerManagerAction, Action frameTemplateAction,
            Action frameCutLayoutAction,
            Action excelToCadAction)
        {
            var modules = new List<ITCModule>();

            modules.Add(new TCModuleDescriptor(
                "layer-manager",
                "图层管理器",
                "图层选择、批量删除、锁定/解锁、开关/冻结等管理工具。",
                "TCGL",
                true,
                layerManagerAction));

            modules.Add(new TCModuleDescriptor(
                "frame-template-add",
                "添加图框模板",
                "从当前图框或外部 DWG 模板添加图框，并框选有效绘制区域作为裁图区域。",
                "TCFRAMEADD",
                true,
                frameTemplateAction));

            modules.Add(new TCModuleDescriptor(
                "frame-cut-layout",
                "布置裁图区域",
                "选择图幅与裁图方向，布置对应模板的红色矩形裁图框，或将闭合曲线设为裁图区域。",
                "TCFRAMECUT",
                true,
                frameCutLayoutAction));

            modules.Add(new TCModuleDescriptor(
                "excel-to-cad",
                "Excel 转 CAD 表格",
                "读取 Excel 使用区域、打印区域或当前选择区域，按分解线文字、原生 TABLE 或块形式生成 CAD 表格。",
                "CDEXCEL",
                true,
                excelToCadAction));

            return modules;
        }
    }
}
