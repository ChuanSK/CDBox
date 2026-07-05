using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using TCPipeAutoDraw.Core.Modules;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioActionFactory
    {
        public static IList<CDBoxStudioAction> Create(IEnumerable<ITCModule> modules)
        {
            var actions = new List<CDBoxStudioAction>();

            if (modules != null)
            {
                foreach (ITCModule module in modules)
                {
                    if (module == null) continue;

                    actions.Add(new CDBoxStudioAction(
                        "module:" + module.Id,
                        module.Name,
                        GetCategory(module.Id),
                        module.Description,
                        module.CommandName,
                        module.Enabled ? BuildBadge(module.CommandName, "原有窗口") : "暂未启用",
                        CDBoxStudioActionKind.Module,
                        module.Enabled,
                        true,
                        module.Run));
                }
            }

            AddCommand(actions, "cmd:PLDM", "批量生成断面", "断面", "调用原有 PLDM 命令，按已写入属性批量绘制断面图。", "PLDM", "原有命令");
            AddCommand(actions, "cmd:SXMRB", "属性默认表", "管线属性", "打开原有属性默认表编辑器，维护主管、支管、井等默认识别与填充参数。", "SXMRB", "原有窗口");
            AddCommand(actions, "cmd:SXQC", "属性清除", "管线属性", "调用原有属性清除命令，清理所选对象上的工程量属性记录。", "SXQC", "原有命令");
            AddCommand(actions, "cmd:CDBOX", "经典合集窗口", "系统", "保留并打开原有 CDBOX 经典合集窗口，不替换旧界面。", "CDBOX", "旧窗口");
            AddCommand(actions, "cmd:CDCBL", "显示侧边栏", "系统", "调用原有 CDCBL 侧边栏入口，可停靠在 CAD 左侧或右侧。", "CDCBL", "旧入口");
            AddCommand(actions, "cmd:CDSET", "工具箱设置", "系统", "打开原有 CDBox 设置窗口。", "CDSET", "原有窗口");

            return actions;
        }

        private static void AddCommand(IList<CDBoxStudioAction> actions, string id, string title, string category, string description, string commandName, string badgeText)
        {
            actions.Add(new CDBoxStudioAction(
                id,
                title,
                category,
                description,
                commandName,
                badgeText,
                CDBoxStudioActionKind.Command,
                true,
                false,
                delegate { SendAcadCommand(commandName); }));
        }

        private static string GetCategory(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) return "其他";

            switch (moduleId.ToLowerInvariant())
            {
                case "layer-manager":
                    return "常用";
                case "annotation-settings":
                case "surface-area-annotation":
                case "pipe-length-annotation":
                case "node-annotation":
                    return "标注";
                case "section-drawing":
                    return "断面";
                case "quantity-pipe-attributes":
                    return "管线属性";
                case "quantity-calculation":
                    return "工程量";
                case "frame-template-add":
                case "frame-cut-layout":
                    return "图框工具";
                default:
                    return "其他";
            }
        }

        private static string BuildBadge(string commandName, string fallback)
        {
            return string.IsNullOrWhiteSpace(commandName) ? fallback : commandName.Trim();
        }

        private static void SendAcadCommand(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName)) return;

            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                System.Windows.Forms.MessageBox.Show(new AcadMainWindow(), "未找到当前图纸。", "CDBox Studio", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }

            doc.SendStringToExecute(commandName.Trim() + " ", true, false, false);
        }
    }
}
