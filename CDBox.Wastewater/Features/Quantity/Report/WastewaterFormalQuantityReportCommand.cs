using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CDBox.Shared.Services;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    internal static class WastewaterFormalQuantityReportCommand
    {
        public static void Run(Document document, ICDBoxLogger logger,
            ICDBoxPromptService prompts)
        {
            if (document == null) return;
            Editor editor = document.Editor;
            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\n选择工程量统计范围内的主管和井对象（可框选/窗选）：",
                MessageForRemoval = "\n移除对象："
            };
            PromptSelectionResult selected;
            using (prompts == null ? null : prompts.Begin(
                "工程量表格生成", "请选择工程量统计范围；按 Esc 取消。"))
                selected = editor.GetSelection(options);
            if (selected.Status != PromptStatus.OK || selected.Value == null
                || selected.Value.Count == 0)
            {
                editor.WriteMessage("\n[GCL] 未选择统计范围，已取消。");
                return;
            }

            string drawingName;
            try
            {
                drawingName = Path.GetFileNameWithoutExtension(document.Name);
            }
            catch { drawingName = "当前图纸"; }
            if (string.IsNullOrWhiteSpace(drawingName))
                drawingName = "当前图纸";

            using (var dialog = new SaveFileDialog
            {
                Title = "保存工程量计算表",
                Filter = "Excel 97-2003 工作簿 (*.xls)|*.xls|所有文件 (*.*)|*.*",
                FileName = SanitizeFileName(drawingName
                    + "工程量计算表") + ".xls",
                AddExtension = true,
                DefaultExt = "xls"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                ObjectId[] ids = selected.Value.GetObjectIds()
                    .Where(x => !x.IsNull).Distinct().ToArray();
                editor.WriteMessage("\n[GCL] 正在计算工程量……");
                QuantityCalculationReport report =
                    WastewaterQuantityCalculationReportService.BuildReport(
                        document, ids, delegate(int current, int total,
                            string message)
                        {
                            if (!string.IsNullOrWhiteSpace(message))
                                editor.WriteMessage("\n[GCL] " + message);
                        });
                WastewaterQuantityExcelExporter.Export(dialog.FileName,
                    report);
                string message = "工程量表格已生成：主管 "
                    + report.MainPipes.Count + " 条，节点/检查井 "
                    + report.Wells.Count + " 个。";
                if (report.Warnings.Count > 0)
                    message += " 提示 " + report.Warnings.Count
                        + " 条，请查看命令行信息。";
                editor.WriteMessage("\n[GCL] " + message + "\n"
                    + dialog.FileName);
                if (logger != null)
                    logger.Info(message + " 文件：" + dialog.FileName);
                MessageBox.Show(message + "\r\n\r\n" + dialog.FileName,
                    "工程量表格生成", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private static string SanitizeFileName(string value)
        {
            string text = value ?? "当前图纸工程量计算表";
            foreach (char valueChar in Path.GetInvalidFileNameChars())
                text = text.Replace(valueChar, '_');
            return text;
        }
    }
}
