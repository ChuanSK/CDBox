using System;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using TCPipeAutoDraw.UI;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Modules.ExcelToCad
{
    public static class ExcelToCadCommandService
    {
        public static void Run(Document document, IWin32Window owner)
        {
            if (document == null)
            {
                CDBoxMessageBox.Show(owner, "未找到当前图纸。", "Excel 转 CAD 表格",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ExcelToCadOptions options;
            ExcelTableModel model;
            if (!CDBoxStudioExcelToCadWindow.TryConfigure(
                document, ResolveDefaultTextHeight(), owner, out options,
                out model)) return;
            string message;
            InsertConfigured(document, options, model, owner, out message);
        }

        internal static bool InsertConfigured(Document document,
            ExcelToCadOptions options, ExcelTableModel model, IWin32Window owner,
            out string message)
        {
            message = string.Empty;
            if (document == null || options == null || model == null)
            {
                message = "Excel 表格数据尚未准备完成。";
                return false;
            }

            ExcelSelectionBridge.DeleteSnapshot(options.TemporarySourcePath);
            options.TemporarySourcePath = string.Empty;

            string summary = model.SheetName + "!" + model.SourceRange + "（"
                + model.RowCount + " 行 × " + model.ColumnCount + " 列，合并区域 "
                + model.MergedRanges.Count + " 个）";
            var pointOptions = new PromptPointOptions(
                "\n指定 Excel 表格左上角插入点 " + summary + "：");
            PromptPointResult point = document.Editor.GetHudPoint(pointOptions);
            if (point.Status != PromptStatus.OK)
            {
                message = "已取消指定表格插入点。";
                return false;
            }

            try
            {
                ExcelToCadInsertResult result = ExcelToCadService.Insert(document, model,
                    options, point.Value);
                message = result.Message;
                document.Editor.WriteHudMessage("\n[Excel 转 CAD 表格] " + result.Message
                    + " 来源：" + summary + "。");
                if (!result.ObjectId.IsNull)
                {
                    try { document.Editor.SetImpliedSelection(new[] { result.ObjectId }); }
                    catch { }
                }
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                CDBoxStudioLogger.Error("Excel 转 CAD 表格生成失败。来源："
                    + summary, ex);
                CDBoxMessageBox.Show(owner, ex.Message, "Excel 转 CAD 表格失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        internal static double ResolveDefaultTextHeight()
        {
            try
            {
                object value = AcadApp.GetSystemVariable("TEXTSIZE");
                double height = Convert.ToDouble(value);
                if (height > 0.01) return height;
            }
            catch { }
            return 2.5;
        }
    }
}
