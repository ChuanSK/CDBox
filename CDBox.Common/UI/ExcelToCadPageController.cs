using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using TCPipeAutoDraw.Modules.ExcelToCad;
using TCPipeAutoDraw.UI;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CDBox.Common.UI
{
    internal sealed class ExcelToCadPageController : IDisposable
    {
        public const string PageId = "common-excel-to-cad";
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        private readonly ICDBoxLogger _logger;
        private readonly ExcelSelectionBridge _selectionBridge;
        private Document _document;
        private double _defaultTextHeight;
        private ExcelToCadDialogSettings _settings;
        private ExcelWorkbookInfo _workbookInfo;
        private ExcelDialogPayload _pendingFallback;

        public ExcelToCadPageController(ICDBoxLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException("logger");
            _selectionBridge = new ExcelSelectionBridge();
            // 模块初始化阶段不得触碰 AutoCAD 宿主对象；只有用户真正打开
            // Excel 转 CAD 页面时才读取当前图纸和 TEXTSIZE。
            _defaultTextHeight = 2.5;
            _settings = ExcelToCadSettingsStore.Load(_defaultTextHeight);
        }

        public void RefreshDocument()
        {
            _document = AcadApp.DocumentManager.MdiActiveDocument;
            _defaultTextHeight = ResolveDefaultTextHeight();
            _settings = ExcelToCadSettingsStore.Load(_defaultTextHeight);
        }

        public CDBoxPageDefinition CreatePage()
        {
            return new CDBoxPageDefinition(PageId, "Excel 转 CAD 表格",
                BuildDocument, Route)
            {
                Width = 1060,
                Height = 760,
                MinimumWidth = 820,
                MinimumHeight = 650
            };
        }

        private string BuildDocument()
        {
            RefreshDocument();
            return CDBoxStudioExcelToCadPage.BuildStandaloneDocument(
                new CDBoxStudioSettings(), _defaultTextHeight, _settings,
                LoadLayerNames(_document));
        }

        private CDBoxPageRouteResult Route(CDBoxPageRouteRequest request)
        {
            var result = new CDBoxPageRouteResult { Handled = true };
            if (request == null) return result;
            string name = (request.Name ?? string.Empty).Trim()
                .ToLowerInvariant();
            switch (name)
            {
                case "ready":
                    return result;
                case "browseexcelworkbook":
                    return BrowseWorkbook();
                case "openexcelselection":
                    return OpenExcelSelection(request.Argument);
                case "pollexcelselection":
                    return PollSelection();
                case "confirmexceltocad":
                    return Confirm(request.Argument);
                case "saveexceltocadpreferences":
                    return SavePreferences(request.Argument);
                case "confirmsavedexcelfallback":
                    return ConfirmSavedFallback();
                case "cancelexceltocad":
                    result.ExecuteScript =
                        "try{chrome.webview.postMessage('studio|close|');}catch(ex){}";
                    return result;
                default:
                    result.Handled = false;
                    return result;
            }
        }

        private CDBoxPageRouteResult BrowseWorkbook()
        {
            var result = Handled();
            try
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = "选择要转换的 Excel 工作簿";
                    dialog.Filter =
                        "Excel 工作簿 (*.xlsx;*.xls;*.xlsm)|*.xlsx;*.xls;*.xlsm|所有文件 (*.*)|*.*";
                    dialog.CheckFileExists = true;
                    dialog.Multiselect = false;
                    if (dialog.ShowDialog(new AcadMainWindow())
                        != DialogResult.OK)
                    {
                        result.ExecuteScript = ErrorScript(
                            "已取消选择 Excel 文件");
                        return result;
                    }
                    _workbookInfo = ExcelTableReader.Inspect(dialog.FileName);
                    result.ExecuteScript = BuildWorkbookScript(
                        _workbookInfo, null);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Excel 文件读取失败。", ex);
                result.ExecuteScript = ErrorScript(
                    "Excel 文件读取失败：" + ex.Message);
            }
            return result;
        }

        private CDBoxPageRouteResult OpenExcelSelection(string filePath)
        {
            var result = Handled();
            try
            {
                string path = (filePath ?? string.Empty).Trim();
                if (path.Length == 0)
                {
                    using (var dialog = new OpenFileDialog())
                    {
                        dialog.Title = "选择并打开 Excel 工作簿";
                        dialog.Filter =
                            "Excel 工作簿 (*.xlsx;*.xls;*.xlsm)|*.xlsx;*.xls;*.xlsm|所有文件 (*.*)|*.*";
                        dialog.CheckFileExists = true;
                        if (dialog.ShowDialog(new AcadMainWindow())
                            != DialogResult.OK)
                        {
                            result.ExecuteScript = ErrorScript(
                                "已取消打开 Excel");
                            return result;
                        }
                        path = dialog.FileName;
                    }
                }
                _workbookInfo = ExcelTableReader.Inspect(path);
                _selectionBridge.OpenOrAttach(path);
                result.ExecuteScript = BuildWorkbookScript(
                    _workbookInfo, null)
                    + "window.CDBoxExcelSelectionConnected && window.CDBoxExcelSelectionConnected();";
            }
            catch (Exception ex)
            {
                _logger.Error("连接 Excel 当前选择失败。", ex);
                result.ExecuteScript = ErrorScript(ex.Message
                    + " 可直接在范围框中输入单元格范围作为备用。");
            }
            return result;
        }

        private CDBoxPageRouteResult PollSelection()
        {
            var result = Handled();
            if (!_selectionBridge.IsConnected) return result;
            ExcelLiveSelection selection;
            string error;
            if (!_selectionBridge.TryReadSelection(out selection, out error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                    result.ExecuteScript =
                        "window.CDBoxExcelSelectionStatus && window.CDBoxExcelSelectionStatus("
                        + ToJsString(error) + ",'warn');";
                return result;
            }
            try
            {
                if (_workbookInfo == null || !string.Equals(
                    _workbookInfo.FilePath, selection.FilePath,
                    StringComparison.OrdinalIgnoreCase))
                    _workbookInfo = ExcelTableReader.Inspect(
                        selection.FilePath);
                result.ExecuteScript =
                    "window.CDBoxExcelSelectionChanged && window.CDBoxExcelSelectionChanged("
                    + Serializer.Serialize(new ExcelSelectionContext
                    {
                        workbook = BuildWorkbookContext(_workbookInfo,
                            selection.SheetName),
                        sheetName = selection.SheetName,
                        rangeAddress = selection.RangeAddress
                    }) + ");";
            }
            catch (Exception ex)
            {
                _logger.Error("同步 Excel 当前选择失败。", ex);
                result.ExecuteScript = ErrorScript(
                    "同步 Excel 当前选择失败：" + ex.Message);
            }
            return result;
        }

        private CDBoxPageRouteResult Confirm(string payload)
        {
            var result = Handled();
            try
            {
                ExcelDialogPayload value = DeserializePayload(payload);
                ExcelWorksheetInfo sheet;
                string error;
                if (!TryValidate(value, out sheet, out error))
                {
                    result.ExecuteScript = ErrorScript(error);
                    return result;
                }

                string snapshot = string.Empty;
                if (string.Equals(value.rangeMode, "live",
                    StringComparison.OrdinalIgnoreCase)
                    && _selectionBridge.IsConnected)
                {
                    ExcelLiveSelection live;
                    string readError;
                    if (_selectionBridge.TryReadSelection(out live,
                        out readError))
                    {
                        value.filePath = live.FilePath;
                        value.sheetName = live.SheetName;
                        value.selectedRange = live.RangeAddress;
                        _workbookInfo = ExcelTableReader.Inspect(
                            live.FilePath);
                        if (!TryValidate(value, out sheet, out error))
                        {
                            result.ExecuteScript = ErrorScript(error);
                            return result;
                        }
                    }
                    try
                    {
                        snapshot = _selectionBridge
                            .CreateWorkbookSnapshot();
                    }
                    catch (Exception ex)
                    {
                        _pendingFallback = value;
                        result.ExecuteScript =
                            "window.CDBoxExcelSnapshotFallback && window.CDBoxExcelSnapshotFallback("
                            + ToJsString(ex.Message) + ");";
                        return result;
                    }
                }
                return PrepareInsertion(value, sheet, snapshot);
            }
            catch (Exception ex)
            {
                _logger.Error("Excel 表格设置或读取失败。", ex);
                result.ExecuteScript = ErrorScript(
                    "Excel 表格读取失败：" + ex.Message);
                return result;
            }
        }

        private CDBoxPageRouteResult ConfirmSavedFallback()
        {
            ExcelDialogPayload value = _pendingFallback;
            _pendingFallback = null;
            if (value == null)
                return ScriptResult(ErrorScript(
                    "没有可继续处理的 Excel 设置。"));
            ExcelWorksheetInfo sheet;
            string error;
            if (!TryValidate(value, out sheet, out error))
                return ScriptResult(ErrorScript(error));
            try { return PrepareInsertion(value, sheet, string.Empty); }
            catch (Exception ex)
            {
                _logger.Error("Excel 最近保存版本读取失败。", ex);
                return ScriptResult(ErrorScript(
                    "Excel 表格读取失败：" + ex.Message));
            }
        }

        private CDBoxPageRouteResult PrepareInsertion(
            ExcelDialogPayload value, ExcelWorksheetInfo sheet,
            string snapshot)
        {
            ApplyPreferences(value);
            ExcelToCadOptions options = BuildOptions(value, sheet, snapshot);
            ExcelTableModel model;
            try { model = ExcelTableReader.Read(options); }
            catch
            {
                ExcelSelectionBridge.DeleteSnapshot(
                    options.TemporarySourcePath);
                options.TemporarySourcePath = string.Empty;
                throw;
            }

            var result = new CDBoxPageRouteResult
            {
                Handled = true,
                RefreshPage = true
            };
            result.ActionToRun = delegate
            {
                InsertConfigured(options, model);
            };
            return result;
        }

        private void InsertConfigured(ExcelToCadOptions options,
            ExcelTableModel model)
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument
                ?? _document;
            if (document == null)
                throw new InvalidOperationException("未找到当前图纸。");
            ExcelSelectionBridge.DeleteSnapshot(options.TemporarySourcePath);
            options.TemporarySourcePath = string.Empty;
            string summary = model.SheetName + "!" + model.SourceRange
                + "（" + model.RowCount + " 行 × " + model.ColumnCount
                + " 列，合并区域 " + model.MergedRanges.Count + " 个）";
            PromptPointResult point = document.Editor.GetPoint(
                new PromptPointOptions(
                    "\n指定 Excel 表格左上角插入点 " + summary + "："));
            if (point.Status != PromptStatus.OK)
            {
                document.Editor.WriteMessage(
                    "\n[Excel 转 CAD 表格] 已取消指定插入点。");
                return;
            }
            ExcelToCadInsertResult inserted = ExcelToCadService.Insert(
                document, model, options, point.Value);
            document.Editor.WriteMessage("\n[Excel 转 CAD 表格] "
                + inserted.Message + " 来源：" + summary + "。");
            if (!inserted.ObjectId.IsNull)
            {
                try
                {
                    document.Editor.SetImpliedSelection(
                        new[] { inserted.ObjectId });
                }
                catch { }
            }
            _logger.Info("Excel 转 CAD 完成：" + summary);
        }

        private CDBoxPageRouteResult SavePreferences(string payload)
        {
            var result = Handled();
            try { ApplyPreferences(DeserializePayload(payload)); }
            catch (Exception ex)
            {
                _logger.Error("Excel 转 CAD 设置保存失败。", ex);
            }
            return result;
        }

        private bool TryValidate(ExcelDialogPayload value,
            out ExcelWorksheetInfo sheet, out string error)
        {
            sheet = null;
            error = string.Empty;
            value.filePath = (value.filePath ?? string.Empty).Trim();
            value.sheetName = (value.sheetName ?? string.Empty).Trim();
            value.selectedRange =
                (value.selectedRange ?? string.Empty).Trim();
            if (value.filePath.Length == 0 || !File.Exists(value.filePath))
            {
                error = "请先选择有效的 Excel 文件。";
                return false;
            }
            if (_workbookInfo == null || !string.Equals(
                _workbookInfo.FilePath, value.filePath,
                StringComparison.OrdinalIgnoreCase))
                _workbookInfo = ExcelTableReader.Inspect(value.filePath);
            foreach (ExcelWorksheetInfo item in _workbookInfo.Sheets)
            {
                if (item.Index == value.sheetIndex || string.Equals(
                    item.Name, value.sheetName,
                    StringComparison.CurrentCultureIgnoreCase))
                {
                    sheet = item;
                    break;
                }
            }
            if (sheet == null)
            {
                error = "请选择有效的 Excel 工作表。";
                return false;
            }
            if (string.Equals(value.rangeMode, "live",
                StringComparison.OrdinalIgnoreCase))
            {
                if (value.selectedRange.Length == 0)
                {
                    error = "请在 Excel 中选择一个连续区域，或手动输入单元格范围。";
                    return false;
                }
                try
                {
                    value.selectedRange = ExcelTableReader
                        .NormalizeRangeAddress(value.selectedRange);
                }
                catch (Exception ex)
                {
                    error = "单元格范围无效：" + ex.Message;
                    return false;
                }
            }
            if (string.Equals(value.rangeMode, "print",
                StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(sheet.PrintArea))
            {
                error = "当前工作表未设置打印区域。";
                return false;
            }
            if (double.IsNaN(value.textHeight)
                || double.IsInfinity(value.textHeight)
                || value.textHeight < 0.1 || value.textHeight > 10000.0)
            {
                error = "基准文字高度应在 0.1 至 10000 之间。";
                return false;
            }
            value.sheetIndex = sheet.Index;
            value.sheetName = sheet.Name;
            return true;
        }

        private ExcelToCadOptions BuildOptions(ExcelDialogPayload value,
            ExcelWorksheetInfo sheet, string snapshot)
        {
            var options = new ExcelToCadOptions
            {
                FilePath = value.filePath,
                TemporarySourcePath = snapshot ?? string.Empty,
                SheetIndex = sheet.Index,
                SheetName = sheet.Name,
                RangeMode = string.Equals(value.rangeMode, "live",
                    StringComparison.OrdinalIgnoreCase)
                        ? ExcelTableRangeMode.LiveSelection
                        : (string.Equals(value.rangeMode, "print",
                            StringComparison.OrdinalIgnoreCase)
                            ? ExcelTableRangeMode.PrintArea
                            : ExcelTableRangeMode.UsedRange),
                SelectedRange = value.selectedRange,
                OutputType = string.Equals(value.outputType, "table",
                    StringComparison.OrdinalIgnoreCase)
                        ? CadExcelTableOutputType.NativeTable
                        : (string.Equals(value.outputType, "block",
                            StringComparison.OrdinalIgnoreCase)
                            ? CadExcelTableOutputType.Block
                            : CadExcelTableOutputType.ExplodedEntities),
                TextHeight = value.textHeight,
                PreserveBackgroundColors = value.preserveBackgroundColors,
                PreserveTextColors = value.preserveTextColors,
                PreserveMergedCells = value.preserveMergedCells,
                DrawGridLines = value.drawGridLines,
                GridLayerName = value.gridLayerName,
                ContentLayerName = value.contentLayerName,
                EntityColorMode = string.Equals(value.entityColorMode,
                    "layer", StringComparison.OrdinalIgnoreCase)
                        ? ExcelCadEntityColorMode.ByLayer
                        : ExcelCadEntityColorMode.ByBlock
            };
            options.Normalize();
            return options;
        }

        private void ApplyPreferences(ExcelDialogPayload value)
        {
            if (value == null) return;
            _settings.RangeMode = string.Equals(value.rangeMode, "used",
                StringComparison.OrdinalIgnoreCase)
                    ? ExcelTableRangeMode.UsedRange
                    : (string.Equals(value.rangeMode, "print",
                        StringComparison.OrdinalIgnoreCase)
                        ? ExcelTableRangeMode.PrintArea
                        : ExcelTableRangeMode.LiveSelection);
            _settings.OutputType = string.Equals(value.outputType, "table",
                StringComparison.OrdinalIgnoreCase)
                    ? CadExcelTableOutputType.NativeTable
                    : (string.Equals(value.outputType, "block",
                        StringComparison.OrdinalIgnoreCase)
                        ? CadExcelTableOutputType.Block
                        : CadExcelTableOutputType.ExplodedEntities);
            _settings.TextHeight = value.textHeight;
            _settings.PreserveBackgroundColors = value.preserveBackgroundColors;
            _settings.PreserveTextColors = value.preserveTextColors;
            _settings.PreserveMergedCells = value.preserveMergedCells;
            _settings.DrawGridLines = value.drawGridLines;
            _settings.GridLayerName = value.gridLayerName;
            _settings.ContentLayerName = value.contentLayerName;
            _settings.EntityColorMode = string.Equals(value.entityColorMode,
                "layer", StringComparison.OrdinalIgnoreCase)
                    ? ExcelCadEntityColorMode.ByLayer
                    : ExcelCadEntityColorMode.ByBlock;
            _settings.Normalize(_defaultTextHeight);
            ExcelToCadSettingsStore.Save(_settings);
        }

        private static List<string> LoadLayerNames(Document document)
        {
            var result = new List<string> { "0" };
            if (document == null || document.Database == null) return result;
            try
            {
                using (Transaction transaction = document.Database
                    .TransactionManager.StartOpenCloseTransaction())
                {
                    LayerTable table = transaction.GetObject(
                        document.Database.LayerTableId, OpenMode.ForRead)
                        as LayerTable;
                    if (table != null)
                    {
                        foreach (ObjectId id in table)
                        {
                            LayerTableRecord layer = transaction.GetObject(
                                id, OpenMode.ForRead) as LayerTableRecord;
                            string name = layer == null
                                ? string.Empty : layer.Name;
                            if (name.Length == 0 || result.Exists(x =>
                                string.Equals(x, name,
                                    StringComparison.OrdinalIgnoreCase)))
                                continue;
                            result.Add(name);
                        }
                    }
                    transaction.Commit();
                }
            }
            catch { }
            result.Sort(delegate(string left, string right)
            {
                if (string.Equals(left, "0",
                    StringComparison.OrdinalIgnoreCase)) return -1;
                if (string.Equals(right, "0",
                    StringComparison.OrdinalIgnoreCase)) return 1;
                return string.Compare(left, right,
                    StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }

        private static double ResolveDefaultTextHeight()
        {
            try
            {
                double height = Convert.ToDouble(
                    AcadApp.GetSystemVariable("TEXTSIZE"));
                if (height > 0.01) return height;
            }
            catch { }
            return 2.5;
        }

        private static ExcelDialogPayload DeserializePayload(string payload)
        {
            return Serializer.Deserialize<ExcelDialogPayload>(
                payload ?? string.Empty) ?? new ExcelDialogPayload();
        }

        private static string BuildWorkbookScript(ExcelWorkbookInfo info,
            string selectedSheet)
        {
            return "window.CDBoxExcelWorkbookLoaded && window.CDBoxExcelWorkbookLoaded("
                + Serializer.Serialize(BuildWorkbookContext(info,
                    selectedSheet)) + ");";
        }

        private static ExcelWorkbookContext BuildWorkbookContext(
            ExcelWorkbookInfo info, string selectedSheet)
        {
            var result = new ExcelWorkbookContext
            {
                filePath = info == null ? string.Empty : info.FilePath,
                activeSheetIndex = info == null ? 0 : info.ActiveSheetIndex,
                selectedSheetName = selectedSheet ?? string.Empty
            };
            if (info != null)
            {
                foreach (ExcelWorksheetInfo sheet in info.Sheets)
                    result.sheets.Add(new ExcelSheetContext
                    {
                        index = sheet.Index,
                        name = sheet.Name,
                        usedRange = sheet.UsedRange,
                        printArea = sheet.PrintArea,
                        hidden = sheet.Hidden
                    });
            }
            return result;
        }

        private static CDBoxPageRouteResult Handled()
        {
            return new CDBoxPageRouteResult { Handled = true };
        }

        private static CDBoxPageRouteResult ScriptResult(string script)
        {
            return new CDBoxPageRouteResult
                { Handled = true, ExecuteScript = script };
        }

        private static string ErrorScript(string message)
        {
            return "window.CDBoxExcelError && window.CDBoxExcelError("
                + ToJsString(message) + ");";
        }

        private static string ToJsString(string value)
        {
            if (value == null) return "''";
            return "'" + value.Replace("\\", "\\\\")
                .Replace("'", "\\'").Replace("\r", "\\r")
                .Replace("\n", "\\n") + "'";
        }

        public void Dispose()
        {
            _selectionBridge.Dispose();
        }

        private sealed class ExcelDialogPayload
        {
            public string filePath { get; set; }
            public int sheetIndex { get; set; }
            public string sheetName { get; set; }
            public string rangeMode { get; set; }
            public string selectedRange { get; set; }
            public string outputType { get; set; }
            public double textHeight { get; set; }
            public bool preserveBackgroundColors { get; set; }
            public bool preserveTextColors { get; set; }
            public bool preserveMergedCells { get; set; }
            public bool drawGridLines { get; set; }
            public string gridLayerName { get; set; }
            public string contentLayerName { get; set; }
            public string entityColorMode { get; set; }
        }

        private sealed class ExcelWorkbookContext
        {
            public ExcelWorkbookContext()
            {
                filePath = string.Empty;
                selectedSheetName = string.Empty;
                sheets = new List<ExcelSheetContext>();
            }
            public string filePath { get; set; }
            public int activeSheetIndex { get; set; }
            public string selectedSheetName { get; set; }
            public List<ExcelSheetContext> sheets { get; set; }
        }

        private sealed class ExcelSheetContext
        {
            public int index { get; set; }
            public string name { get; set; }
            public string usedRange { get; set; }
            public string printArea { get; set; }
            public bool hidden { get; set; }
        }

        private sealed class ExcelSelectionContext
        {
            public ExcelWorkbookContext workbook { get; set; }
            public string sheetName { get; set; }
            public string rangeAddress { get; set; }
        }
    }
}
