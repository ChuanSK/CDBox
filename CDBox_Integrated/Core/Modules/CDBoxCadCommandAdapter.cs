using System;
using Autodesk.AutoCAD.ApplicationServices;
using CDBox.Shared.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Core.Modules
{
    internal sealed class CDBoxCadCommandAdapter : ICDBoxCadCommandService
    {
        public void Execute(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName)) return;
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null)
                throw new InvalidOperationException("未找到当前图纸。");
            document.SendStringToExecute(commandName.Trim() + " ",
                true, false, false);
        }
    }
}
