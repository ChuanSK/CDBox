using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Modules.WastewaterResultTable
{
    public sealed class WastewaterResultTableCommands
    {
        [CommandMethod("WSGCGB", CommandFlags.Modal
            | CommandFlags.UsePickSet)]
        [CommandMethod("CDWELLTABLE", CommandFlags.Modal
            | CommandFlags.UsePickSet)]
        public void DrawWastewaterResultTable()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            try
            {
                WastewaterResultTableDrawingService.Run(document);
            }
            catch (System.Exception exception)
            {
                document.Editor.WriteHudMessage(
                    "\n[污水管成果表] 生成失败：" + exception.Message);
            }
        }
    }
}
