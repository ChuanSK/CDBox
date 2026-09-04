using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.UI
{
    public sealed class RealEstateMenuItem
    {
        private RealEstateMenuItem(string label, string commandName, bool isSeparator)
        {
            Label = label ?? string.Empty;
            CommandName = commandName ?? string.Empty;
            IsSeparator = isSeparator;
        }

        public string Label { get; }

        public string CommandName { get; }

        public bool IsSeparator { get; }

        public static RealEstateMenuItem Command(string label, string commandName)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("菜单名称不能为空。", nameof(label));
            if (string.IsNullOrWhiteSpace(commandName))
                throw new ArgumentException("菜单命令不能为空。", nameof(commandName));

            return new RealEstateMenuItem(label.Trim(), commandName.Trim(), false);
        }

        public static RealEstateMenuItem Separator()
        {
            return new RealEstateMenuItem(string.Empty, string.Empty, true);
        }
    }

    public static class RealEstateMenuLayout
    {
        private static readonly IReadOnlyList<RealEstateMenuItem> MenuItems =
            Array.AsReadOnly(new[]
            {
                RealEstateMenuItem.Command("▣ 选择宗地", "CDRESELECTPARCEL"),
                RealEstateMenuItem.Command("✎ 填写界址段", "CDREFILLSEGMENT"),
                RealEstateMenuItem.Command("◇ 填写邻宗信息", "CDREFILLNEIGHBOR"),
                RealEstateMenuItem.Separator(),
                RealEstateMenuItem.Command("▤ 宗地调查数据编辑器", "CDREDJ"),
                RealEstateMenuItem.Separator(),
                RealEstateMenuItem.Command("▱ 注记建筑边长", "CDREBL"),
                RealEstateMenuItem.Command("⚙ 建筑边长注记设置", "CDREBLSZ"),
                RealEstateMenuItem.Separator(),
                RealEstateMenuItem.Command("⚙ CDBox设置", "CDSET"),
                RealEstateMenuItem.Separator(),
                RealEstateMenuItem.Command("ⓘ 关于超重氢工具箱", "CDABOUT")
            });

        public static IReadOnlyList<RealEstateMenuItem> Items => MenuItems;
    }
}
