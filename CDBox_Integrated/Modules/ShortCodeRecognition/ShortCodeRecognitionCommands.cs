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
                    "\n选择带简码的 CASS DAT/TXT/CSV 坐标文件");
                options.Filter =
                    "坐标数据 (*.dat;*.txt;*.csv)|*.dat;*.txt;*.csv|所有文件 (*.*)|*.*";
                PromptFileNameResult selected =
                    editor.GetFileNameForOpen(options);
                if (selected.Status != PromptStatus.OK
                    || string.IsNullOrWhiteSpace(selected.StringResult))
                    return;

                ShortCodeRecognitionResult result =
                    new ShortCodeRecognitionModule().Run(document,
                        selected.StringResult,
                        ShortCodeRecognitionSettingsStore.Load());
                editor.WriteMessage(result.ToEditorMessage());
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(
                    "\n[简码识别] 识别失败，图纸未写入不完整结果："
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
                    "简码识别设置", System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }
    }
}
