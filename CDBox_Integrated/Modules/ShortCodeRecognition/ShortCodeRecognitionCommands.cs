using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using TCPipeAutoDraw.UI;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Modules.ShortCodeRecognition
{
    public sealed class ShortCodeRecognitionCommands
    {
        [CommandMethod("CDSHORTCODE", CommandFlags.Modal)]
        [CommandMethod("CDJMSB", CommandFlags.Modal)]
        public void Recognize()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            try
            {
                PromptOpenFileOptions options = new PromptOpenFileOptions(
                    "\n?????? CASS DAT/TXT/CSV ????");
                options.Filter =
                    "???? (*.dat;*.txt;*.csv)|*.dat;*.txt;*.csv|???? (*.*)|*.*";
                PromptFileNameResult selected =
                    editor.GetFileNameForOpen(options);
                if (selected.Status != PromptStatus.OK
                    || string.IsNullOrWhiteSpace(selected.StringResult))
                    return;

                ShortCodeRecognitionResult result =
                    new ShortCodeRecognitionModule().Run(document,
                        selected.StringResult,
                        ShortCodeRecognitionSettingsStore.Load());
                editor.WriteHudMessage(result.ToEditorMessage());
            }
            catch (System.Exception ex)
            {
                editor.WriteHudMessage(
                    "\n[????] ????????????????"
                    + ex.Message);
            }
        }

        [CommandMethod("CDSHORTCODESET", CommandFlags.Modal)]
        [CommandMethod("CDJMSZ", CommandFlags.Modal)]
        public void OpenSettings()
        {
            try
            {
                CDBoxStudioShortCodeSettingsWindow.ShowWindow(
                    new AcadMainWindow());
            }
            catch (System.Exception ex)
            {
                CDBoxMessageBox.Show(new AcadMainWindow(), ex.Message,
                    "??????", System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }
    }
}
