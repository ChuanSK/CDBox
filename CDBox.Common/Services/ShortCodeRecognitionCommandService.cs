using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using CDBox.Common.Features.ShortCodeRecognition;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CDBox.Common.Services
{
    internal sealed class ShortCodeRecognitionCommandService
    {
        private readonly ICDBoxPageService _pages;
        private readonly ICDBoxLogger _logger;

        public ShortCodeRecognitionCommandService(ICDBoxPageService pages,
            ICDBoxLogger logger)
        {
            _pages = pages ?? throw new ArgumentNullException("pages");
            _logger = logger ?? throw new ArgumentNullException("logger");
        }

        public void Recognize()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            try
            {
                var options = new PromptOpenFileOptions(
                    "\n选择带简码的 CASS DAT/TXT/CSV 坐标文件")
                {
                    Filter = "坐标数据 (*.dat;*.txt;*.csv)|*.dat;*.txt;*.csv|所有文件 (*.*)|*.*"
                };
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
                _logger.Info("简码识别完成：" + selected.StringResult);
            }
            catch (Exception ex)
            {
                _logger.Error("简码识别失败。", ex);
                editor.WriteMessage(
                    "\n[简码识别] 识别失败，图纸未写入不完整结果："
                    + ex.Message);
            }
        }

        public void OpenSettings()
        {
            _pages.Show(CDBoxUiGateway.Call<CDBoxPageDefinition>("common.short-code", "Create"));
        }
    }
}
