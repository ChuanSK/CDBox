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
        public static IList<ITCModule> CreateDefaultModules(Action drawPipeAction, Action layerManagerAction, Action annotationSettingsAction, Action surfaceAreaAnnotationAction, Action pipeLengthAnnotationAction, Action nodeAnnotationAction, Action sectionDrawingAction, Action frameTemplateAction, Action frameCutLayoutAction, Action quantityPipeAttributeAction, Action quantityCalculationAction, Action excelToCadAction)
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
                "annotation-settings",
                "标注设置",
                "统一管理表面积、管线长度、节点三类标注设置，并可直接启动对应标注。",
                "BZSZ",
                true,
                annotationSettingsAction));

            modules.Add(new TCModuleDescriptor(
                "surface-area-annotation",
                "表面积标注",
                "选择闭合区域，根据图上高程点估算表面积，并按图层自动生成注记。",
                "BMJ",
                true,
                surfaceAreaAnnotationAction));

            modules.Add(new TCModuleDescriptor(
                "pipe-length-annotation",
                "管线长度标注",
                "选择指定多段线，按实际长度生成文字、横线和引线注记。",
                "GCBZ",
                true,
                pipeLengthAnnotationAction));

            modules.Add(new TCModuleDescriptor(
                "node-annotation",
                "节点标注",
                "自动吸附最近节点/检查井，生成节点编号、井深、井筒和沉泥井文字标注。",
                "JDBZ",
                true,
                nodeAnnotationAction));

            modules.Add(new TCModuleDescriptor(
                "section-drawing",
                "断面图生成",
                "自定义层级、填充、管道圆、线性标注，并按 1:1 生成管线断面图。",
                "DM",
                true,
                sectionDrawingAction));

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
                "quantity-pipe-attributes",
                "属性编辑器",
                "点选主管、节点/检查井、支管后自动识别并打开对应的小型属性界面。默认表请使用 SXMRB。",
                "SX",
                true,
                quantityPipeAttributeAction));

            modules.Add(new TCModuleDescriptor(
                "quantity-calculation",
                "工程量表格生成",
                "读取已写入的主管和节点/检查井属性，生成工程量计算表。支管暂不统计。",
                "GCL",
                true,
                quantityCalculationAction));

            modules.Add(new TCModuleDescriptor(
                "excel-to-cad",
                "Excel 转 CAD 表格",
                "读取 Excel 使用区域、打印区域或当前选择区域，按分解线文字、原生 TABLE 或块形式生成 CAD 表格。",
                "CDEXCEL",
                true,
                excelToCadAction));

            modules.Add(new TCModuleDescriptor(
                "pipe-draw",
                "展点绘制管线(暂时放弃)",
                "读取 TXT/DAT/CSV 测点代码，自动生成管线多段线和基础注记。",
                "CDDRAWNET",
                false,
                drawPipeAction));

            return modules;
        }
    }
}
