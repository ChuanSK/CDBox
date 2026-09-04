using System;
using System.Collections.Generic;
using System.Linq;
using CDBox.Shared.Components;

namespace CDBox.Setup
{
    internal sealed class InstallerFeatureGroup
    {
        public string Name { get; set; } = string.Empty;
        public string[] Features { get; set; } = new string[0];
    }

    internal sealed class InstallerComponentPresentation
    {
        public string ComponentId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public InstallerFeatureGroup[] Groups { get; set; } =
            new InstallerFeatureGroup[0];
    }

    internal static class InstallerPresentationCatalog
    {
        private static readonly IReadOnlyList<InstallerComponentPresentation>
            Items = new[]
            {
                Item(CDBoxComponentIds.Base,
                    "CDBox 的必需运行、界面、配置、更新与图层基础能力。",
                    Group("基础能力", "核心运行组件", "CDBox UI / WebView2",
                        "配置与数据组件", "更新服务", "LSP 兼容组件",
                        "图层管理器")),
                Item(CDBoxComponentIds.Common,
                    "供不同业务共同使用的 CAD 效率工具与数据处理组件。",
                    Group("图层与属性工具", "父类 / 标签管理", "图层识别",
                        "属性识别表", "属性默认表", "批量属性处理"),
                    Group("CAD 工具", "文本提取", "表格导入",
                        "Excel 数据导入", "图框与通用成果工具")),
                Item(CDBoxComponentIds.Wastewater,
                    "排水管网设计、属性管理、工程量统计、成果输出与图纸检查。",
                    Group("对象管理", "管线属性编辑", "节点属性编辑",
                        "起终点识别"),
                    Group("结构层", "管线结构层", "井结构层",
                        "开挖深度计算"),
                    Group("标注", "节点标注", "管线标注", "标注设置",
                        "纵断面与批量断面"),
                    Group("工程量", "工程量统计", "固定区域统计",
                        "Excel 成果输出"),
                    Group("图纸检查", "属性检查", "联动检查", "同步中心")),
                Item(CDBoxComponentIds.RealEstate,
                    "宗地、界址、建筑物调查与不动产成果导出组件。",
                    Group("宗地调查", "宗地选择与数据编辑", "权利人与权属",
                        "土地用途与面积"),
                    Group("界址调查", "界址点识别", "界址段与邻宗信息",
                        "界址说明与宗地四至"),
                    Group("建筑与成果", "建筑边长注记", "房屋调查",
                        "Word / Excel 调查表", "四张检查表"))
            };

        public static InstallerComponentPresentation Find(string componentId)
        {
            return Items.FirstOrDefault(x => string.Equals(x.ComponentId,
                       componentId, StringComparison.OrdinalIgnoreCase))
                ?? new InstallerComponentPresentation
                {
                    ComponentId = componentId ?? string.Empty,
                    Description = "CDBox 业务组件。"
                };
        }

        private static InstallerComponentPresentation Item(string id,
            string description, params InstallerFeatureGroup[] groups)
        {
            return new InstallerComponentPresentation
            {
                ComponentId = id,
                Description = description,
                Groups = groups ?? new InstallerFeatureGroup[0]
            };
        }

        private static InstallerFeatureGroup Group(string name,
            params string[] features)
        {
            return new InstallerFeatureGroup
            {
                Name = name,
                Features = features ?? new string[0]
            };
        }
    }
}
