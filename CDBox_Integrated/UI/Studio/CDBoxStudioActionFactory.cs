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
                        module.Enabled ? BuildBadge(module.CommandName, string.Empty) : "????",
                        CDBoxStudioActionKind.Module,
                        module.Enabled,
                        true,
                        module.Run));
                }
            }

            AddCommand(actions, "cmd:PLDM", "??????", "??", string.Empty, "PLDM", string.Empty);
            AddCommand(actions, "cmd:ZDM", "?????", "??",
                "??????????????????????????",
                "ZDM", "ZDM");
            actions.Add(new CDBoxStudioAction(
                "module:longitudinal-profile-settings",
                "?????",
                "??",
                "????????????????????",
                "ZDMSZ",
                "ZDMSZ",
                CDBoxStudioActionKind.Module,
                true,
                true,
                delegate
                {
                    CDBoxStudioLongitudinalProfileSettingsWindow
                        .ShowWindow(new AcadMainWindow());
                }));
            AddCommand(actions, "cmd:SXMRB", "?????", "????", string.Empty, "SXMRB", string.Empty);
            AddCommand(actions, "cmd:SXQC", "????", "????", string.Empty, "SXQC", string.Empty);
            AddCommand(actions, "cmd:CDQBOARD", "?????", "???", string.Empty, "CDQBOARD", string.Empty);
            AddCommand(actions, "cmd:TCFRAMELAYOUT", "??????", "????", string.Empty, "TCFRAMELAYOUT", string.Empty);
            AddCommand(actions, "cmd:TCFRAMEPLACE", "??????", "????", string.Empty, "TCFRAMEPLACE", string.Empty);
            actions.Add(new CDBoxStudioAction(
                "module:frame-settings",
                "????",
                "????",
                "?????????????????????????????",
                "TCFRAMESET",
                "TCFRAMESET",
                CDBoxStudioActionKind.Module,
                true,
                true,
                delegate { CDBoxStudioFrameSettingsWindow.ShowWindow(new AcadMainWindow()); }));
            AddCommand(actions, "cmd:CDJMSB", "????", "????",
                "?????? CASS DAT/TXT/CSV ??????????",
                "CDJMSB", "CDJMSB");
            actions.Add(new CDBoxStudioAction(
                "module:short-code-settings",
                "??????",
                "????",
                "?????????????????????????",
                "CDJMSZ",
                "CDJMSZ",
                CDBoxStudioActionKind.Module,
                true,
                true,
                delegate
                {
                    CDBoxStudioShortCodeSettingsWindow.ShowWindow(
                        new AcadMainWindow());
                }));
            AddCommand(actions, "cmd:CDSET", "CDBox??", "??", string.Empty, "CDSET", string.Empty);

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
            if (string.IsNullOrWhiteSpace(moduleId)) return "??";

            switch (moduleId.ToLowerInvariant())
            {
                case "layer-manager":
                    return "?????";
                case "annotation-settings":
                case "surface-area-annotation":
                case "pipe-length-annotation":
                case "node-annotation":
                    return "??";
                case "section-drawing":
                    return "??";
                case "quantity-pipe-attributes":
                    return "????";
                case "quantity-calculation":
                    return "???";
                case "frame-template-add":
                case "frame-cut-layout":
                    return "????";
                case "excel-to-cad":
                    return "????";
                default:
                    return "??";
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
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), "????????", "CDBox Studio", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }

            doc.SendStringToExecute(commandName.Trim() + " ", true, false, false);
        }
    }
}
