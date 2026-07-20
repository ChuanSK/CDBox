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
                        module.Enabled ? BuildBadge(module.CommandName, string.Empty) : "暂未启用",
                        CDBoxStudioActionKind.Module,
                        module.Enabled,
                        true,
                        module.Run));
                }
            }

            AddCommand(actions, "cmd:PLDM", "批量生成断面", "断面", string.Empty, "PLDM", string.Empty);
            AddCommand(actions, "cmd:SXMRB", "属性默认表", "管线属性", string.Empty, "SXMRB", string.Empty);
            AddCommand(actions, "cmd:SXQC", "属性清除", "管线属性", string.Empty, "SXQC", string.Empty);
            AddCommand(actions, "cmd:CDQBOARD", "工程量看板", "工程量", string.Empty, "CDQBOARD", string.Empty);
            AddCommand(actions, "cmd:CDSET", "CDBox设置", "系统", string.Empty, "CDSET", string.Empty);

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
                    return "图层管理器";
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
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), "未找到当前图纸。", "CDBox Studio", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }

            doc.SendStringToExecute(commandName.Trim() + " ", true, false, false);
        }
    }
}
