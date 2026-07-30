using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using TCPipeAutoDraw.Modules.ExcelToCad;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioExcelToCadWindow : IDisposable
    {
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        private readonly double _defaultTextHeight;
        private readonly ExcelSelectionBridge _selectionBridge;
        private readonly ExcelToCadDialogSettings _dialogSettings;
        private readonly List<string> _layerNames;
        private readonly bool _embedded;
        private Document _document;
        private CDBoxStudioWebPageForm _window;
        private ExcelWorkbookInfo _workbookInfo;
        private ExcelDialogPayload _pendingFallback;
        private ExcelToCadOptions _options;
        private ExcelTableModel _model;

        private CDBoxStudioExcelToCadWindow(Document document,
            double defaultTextHeight, bool embedded)
        {
            _document = document;
            _defaultTextHeight = defaultTextHeight;
            _embedded = embedded;
            _selectionBridge = new ExcelSelectionBridge();
            _dialogSettings = ExcelToCadSettingsStore.Load(defaultTextHeight);
            _layerNames = LoadLayerNames(document);
        }

        internal static CDBoxStudioExcelToCadWindow CreateEmbedded(
            Document document, double defaultTextHeight)
        {
            return new CDBoxStudioExcelToCadWindow(document, defaultTextHeight,
                true);
        }

        public static bool TryConfigure(Document document, double defaultTextHeight,
            IWin32Window owner, out ExcelToCadOptions options,
            out ExcelTableModel model)
        {
            using (var controller = new CDBoxStudioExcelToCadWindow(document,
                defaultTextHeight, false))
            {
                controller.Show(owner ?? new AcadMainWindow());
                options = controller._options == null
                    ? null : controller._options.Clone();
                model = controller._model;
                return options != null && model != null;
            }
        }

        private void Show(IWin32Window owner)
        {
            _window = new CDBoxStudioWebPageForm(
                "Excel 转 CAD 表格",
                delegate
                {
                    return CDBoxStudioExcelToCadPage.BuildStandaloneDocument(
                        CDBoxStudioSettingsStore.Load(), _defaultTextHeight,
                        _dialogSettings, _layerNames);
                },
                Route,
                "excel-to-cad");
            _window.Width = 1060;
            _window.Height = 760;
            _window.MinimumSize = new Size(820, 650);
            _window.ShowDialog(owner);
        }

        internal bool TryRoute(CDBoxStudioRouteRequest request,
            Document activeDocument, out CDBoxStudioRouteResult result)
        {
            if (activeDocument != null) _document = activeDocument;
            if (_embedded && request != null
                && string.Equals((request.Name ?? string.Empty).Trim(), "ready",
                    StringComparison.OrdinalIgnoreCase))
            {
                result = null;
                return false;
            }
            result = Route(request);
            return result != null && result.Handled;
        }

        private CDBoxStudioRouteResult Route(CDBoxStudioRouteRequest request)
        {
            var result = new CDBoxStudioRouteResult { Handled = true };
            if (request == null) return result;
            switch ((request.Name ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "ready":
                case "exceltocadembeddedready":
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
                    if (_embedded)
                    {
                        result.ExecuteScript =
                            "window.CDBoxStudioCloseExcelToCad && window.CDBoxStudioCloseExcelToCad();";
                    }
                    else Close();
                    return result;
                default:
                    result.Handled = false;
                    return result;
            }
        }

        private CDBoxStudioRouteResult BrowseWorkbook()
        {
            var result = new CDBoxStudioRouteResult { Handled = true };
            try
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = "选择要转换的 Excel 工作簿";
                    dialog.Filter =
                        "Excel 工作簿 (*.xlsx;*.xls;*.xlsm)|*.xlsx;*.xls;*.xlsm|所有文件 (*.*)|*.*";
                    dialog.CheckFileExists = true;
                    dialog.Multiselect = false;
                    if (dialog.ShowDialog(GetDialogOwner()) != DialogResult.OK)
                    {
                        result.ExecuteScript =
                            "window.CDBoxExcelError && window.CDBoxExcelError('已取消选择 Excel 文件');";
                        return result;
                    }
                    _workbookInfo = ExcelTableReader.Inspect(dialog.FileName);
                    result.ExecuteScript = BuildWorkbookScript(_workbookInfo, null);
                }
            }
            catch (Exception ex)
            {
                result.ExecuteScript = ErrorScript("Excel 文件读取失败：" + ex.Message);
            }
            return result;
        }

        private CDBoxStudioRouteResult OpenExcelSelection(string filePath)
        {
            var result = new CDBoxStudioRouteResult { Handled = true };
            try
            {
                string path = (filePath ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(path))
                {
                    using (var dialog = new OpenFileDialog())
                    {
                        dialog.Title = "选择并打开 Excel 工作簿";
                        dialog.Filter =
                            "Excel 工作簿 (*.xlsx;*.xls;*.xlsm)|*.xlsx;*.xls;*.xlsm|所有文件 (*.*)|*.*";
                        dialog.CheckFileExists = true;
                        if (dialog.ShowDialog(GetDialogOwner()) != DialogResult.OK)
                        {
                            result.ExecuteScript =
                                "window.CDBoxExcelError && window.CDBoxExcelError('已取消打开 Excel');";
                            return result;
                        }
                        path = dialog.FileName;
                    }
                }
                _workbookInfo = ExcelTableReader.Inspect(path);
                _selectionBridge.OpenOrAttach(path);
                result.ExecuteScript = BuildWorkbookScript(_workbookInfo, null)
                    + "window.CDBoxExcelSelectionConnected && window.CDBoxExcelSelectionConnected();";
            }
            catch (Exception ex)
            {
                result.ExecuteScript = ErrorScript(ex.Message
                    + " 可直接在范围框中输入单元格范围作为备用。");
            }
            return result;
        }

        private CDBoxStudioRouteResult PollSelection()
        {
            var result = new CDBoxStudioRouteResult { Handled = true };
            if (!_selectionBridge.IsConnected) return result;

            ExcelLiveSelection selection;
            string error;
            if (!_selectionBridge.TryReadSelection(out selection, out error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    result.ExecuteScript =
                        "window.CDBoxExcelSelectionStatus && window.CDBoxExcelSelectionStatus("
                        + ToJsString(error) + ",'warn');";
                }
                return result;
            }

            try
            {
                if (_workbookInfo == null
                    || !string.Equals(_workbookInfo.FilePath, selection.FilePath,
                        StringComparison.OrdinalIgnoreCase))
                    _workbookInfo = ExcelTableReader.Inspect(selection.FilePath);
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
                result.ExecuteScript = ErrorScript("同步 Excel 当前选择失败：" + ex.Message);
            }
            return result;
        }

        private CDBoxStudioRouteResult Confirm(string payload)
        {
            var result = new CDBoxStudioRouteResult { Handled = true };
            try
            {
                ExcelDialogPayload value =
                    Serializer.Deserialize<ExcelDialogPayload>(payload ?? string.Empty)
                    ?? new ExcelDialogPayload();
                string validationError;
                ExcelWorksheetInfo sheet;
                if (!TryValidate(value, out sheet, out validationError))
                {
                    result.ExecuteScript = ErrorScript(validationError);
                    return result;
                }

                if (string.Equals(value.rangeMode, "live",
                    StringComparison.OrdinalIgnoreCase)
                    && _selectionBridge.IsConnected)
                {
                    ExcelLiveSelection live;
                    string readError;
                    if (_selectionBridge.TryReadSelection(out live, out readError))
                    {
                        value.filePath = live.FilePath;
                        value.sheetName = live.SheetName;
                        value.selectedRange = live.RangeAddress;
                        _workbookInfo = ExcelTableReader.Inspect(live.FilePath);
                        if (!TryValidate(value, out sheet, out validationError))
                        {
                            result.ExecuteScript = ErrorScript(validationError);
                            return result;
                        }
                    }

                    try
                    {
                        string snapshot = _selectionBridge.CreateWorkbookSnapshot();
                        Complete(value, sheet, snapshot);
                        if (_embedded) CompleteEmbeddedInsertion(result);
                        else Close();
                        return result;
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

                Complete(value, sheet, string.Empty);
                if (_embedded) CompleteEmbeddedInsertion(result);
                else Close();
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("Excel 转 CAD 表格设置或读取失败。", ex);
                result.ExecuteScript = ErrorScript("Excel 表格读取失败：" + ex.Message);
            }
            return result;
        }

        private CDBoxStudioRouteResult SavePreferences(string payload)
        {
            var result = new CDBoxStudioRouteResult { Handled = true };
            try
            {
                ExcelDialogPayload value =
                    Serializer.Deserialize<ExcelDialogPayload>(payload ?? string.Empty)
                    ?? new ExcelDialogPayload();
                ApplyPreferences(value);
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("Excel 转 CAD 表格设置保存失败。", ex);
            }
            return result;
        }

        private CDBoxStudioRouteResult ConfirmSavedFallback()
        {
            var result = new CDBoxStudioRouteResult { Handled = true };
            ExcelDialogPayload value = _pendingFallback;
            _pendingFallback = null;
            if (value == null)
            {
                result.ExecuteScript = ErrorScript("没有可继续处理的 Excel 设置。");
                return result;
            }
            ExcelWorksheetInfo sheet;
            string error;
            if (!TryValidate(value, out sheet, out error))
            {
                result.ExecuteScript = ErrorScript(error);
                return result;
            }
            try
            {
                Complete(value, sheet, string.Empty);
                if (_embedded) CompleteEmbeddedInsertion(result);
                else Close();
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("Excel 转 CAD 读取最近保存版本失败。", ex);
                result.ExecuteScript = ErrorScript("Excel 表格读取失败：" + ex.Message);
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
            value.selectedRange = (value.selectedRange ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value.filePath)
                || !File.Exists(value.filePath))
            {
                error = "请先选择有效的 Excel 文件。";
                return false;
            }
            if (_workbookInfo == null
                || !string.Equals(_workbookInfo.FilePath, value.filePath,
                    StringComparison.OrdinalIgnoreCase))
                _workbookInfo = ExcelTableReader.Inspect(value.filePath);

            for (int i = 0; i < _workbookInfo.Sheets.Count; i++)
            {
                ExcelWorksheetInfo item = _workbookInfo.Sheets[i];
                if (item.Index == value.sheetIndex
                    || string.Equals(item.Name, value.sheetName,
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
                if (string.IsNullOrWhiteSpace(value.selectedRange))
                {
                    error = "请在 Excel 中选择一个连续区域，或手动输入单元格范围。";
                    return false;
                }
                try
                {
                    value.selectedRange =
                        ExcelTableReader.NormalizeRangeAddress(value.selectedRange);
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
            if (double.IsNaN(value.textHeight) || double.IsInfinity(value.textHeight)
                || value.textHeight < 0.1 || value.textHeight > 10000.0)
            {
                error = "基准文字高度应在 0.1 至 10000 之间。";
                return false;
            }
            value.sheetIndex = sheet.Index;
            value.sheetName = sheet.Name;
            return true;
        }

        private void Complete(ExcelDialogPayload value,
            ExcelWorksheetInfo sheet, string snapshot)
        {
            ApplyPreferences(value);
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
                EntityColorMode = string.Equals(value.entityColorMode, "layer",
                    StringComparison.OrdinalIgnoreCase)
                    ? ExcelCadEntityColorMode.ByLayer
                    : ExcelCadEntityColorMode.ByBlock
            };
            options.Normalize();
            try
            {
                ExcelTableModel model = ExcelTableReader.Read(options);
                _options = options;
                _model = model;
            }
            catch
            {
                ExcelSelectionBridge.DeleteSnapshot(options.TemporarySourcePath);
                options.TemporarySourcePath = string.Empty;
                _options = null;
                _model = null;
                throw;
            }
        }

        private void ApplyPreferences(ExcelDialogPayload value)
        {
            if (value == null) return;
            _dialogSettings.RangeMode = string.Equals(value.rangeMode, "used",
                StringComparison.OrdinalIgnoreCase)
                ? ExcelTableRangeMode.UsedRange
                : (string.Equals(value.rangeMode, "print",
                    StringComparison.OrdinalIgnoreCase)
                    ? ExcelTableRangeMode.PrintArea
                    : ExcelTableRangeMode.LiveSelection);
            _dialogSettings.OutputType = string.Equals(value.outputType, "table",
                StringComparison.OrdinalIgnoreCase)
                ? CadExcelTableOutputType.NativeTable
                : (string.Equals(value.outputType, "block",
                    StringComparison.OrdinalIgnoreCase)
                    ? CadExcelTableOutputType.Block
                    : CadExcelTableOutputType.ExplodedEntities);
            _dialogSettings.TextHeight = value.textHeight;
            _dialogSettings.PreserveBackgroundColors =
                value.preserveBackgroundColors;
            _dialogSettings.PreserveTextColors = value.preserveTextColors;
            _dialogSettings.PreserveMergedCells = value.preserveMergedCells;
            _dialogSettings.DrawGridLines = value.drawGridLines;
            _dialogSettings.GridLayerName = value.gridLayerName;
            _dialogSettings.ContentLayerName = value.contentLayerName;
            _dialogSettings.EntityColorMode = string.Equals(value.entityColorMode,
                "layer", StringComparison.OrdinalIgnoreCase)
                ? ExcelCadEntityColorMode.ByLayer
                : ExcelCadEntityColorMode.ByBlock;
            _dialogSettings.Normalize(_defaultTextHeight);
            ExcelToCadSettingsStore.Save(_dialogSettings);
        }

        internal static List<string> LoadLayerNames(Document document)
        {
            var result = new List<string> { "0" };
            if (document == null || document.Database == null) return result;
            try
            {
                using (Transaction transaction =
                    document.Database.TransactionManager.StartOpenCloseTransaction())
                {
                    LayerTable table = transaction.GetObject(
                        document.Database.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (table != null)
                    {
                        foreach (ObjectId id in table)
                        {
                            LayerTableRecord layer = transaction.GetObject(id,
                                OpenMode.ForRead) as LayerTableRecord;
                            string name = layer == null ? string.Empty : layer.Name;
                            if (string.IsNullOrWhiteSpace(name)
                                || result.Exists(x => string.Equals(x, name,
                                    StringComparison.OrdinalIgnoreCase))) continue;
                            result.Add(name);
                        }
                    }
                    transaction.Commit();
                }
            }
            catch { }
            result.Sort(delegate(string left, string right)
            {
                if (string.Equals(left, "0", StringComparison.OrdinalIgnoreCase))
                    return -1;
                if (string.Equals(right, "0", StringComparison.OrdinalIgnoreCase))
                    return 1;
                return string.Compare(left, right,
                    StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }

        private static string BuildWorkbookScript(ExcelWorkbookInfo info,
            string selectedSheet)
        {
            return "window.CDBoxExcelWorkbookLoaded && window.CDBoxExcelWorkbookLoaded("
                + Serializer.Serialize(BuildWorkbookContext(info, selectedSheet)) + ");";
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
                for (int i = 0; i < info.Sheets.Count; i++)
                {
                    ExcelWorksheetInfo sheet = info.Sheets[i];
                    result.sheets.Add(new ExcelSheetContext
                    {
                        index = sheet.Index,
                        name = sheet.Name,
                        usedRange = sheet.UsedRange,
                        printArea = sheet.PrintArea,
                        hidden = sheet.Hidden
                    });
                }
            }
            return result;
        }

        private static string ErrorScript(string message)
        {
            return "window.CDBoxExcelError && window.CDBoxExcelError("
                + ToJsString(message) + ");";
        }

        private static string ToJsString(string value)
        {
            if (value == null) return "''";
            return "'" + value.Replace("\\", "\\\\").Replace("'", "\\'")
                .Replace("\r", "\\r").Replace("\n", "\\n") + "'";
        }

        private void Close()
        {
            if (_window != null && !_window.IsDisposed) _window.Close();
        }

        private IWin32Window GetDialogOwner()
        {
            return _window != null && !_window.IsDisposed
                ? (IWin32Window)_window
                : new AcadMainWindow();
        }

        private void CompleteEmbeddedInsertion(CDBoxStudioRouteResult result)
        {
            ExcelToCadOptions options = _options == null ? null : _options.Clone();
            ExcelTableModel model = _model;
            _options = null;
            _model = null;

            string message;
            bool success;
            try
            {
                success = ExcelToCadCommandService.InsertConfigured(_document,
                    options, model, GetDialogOwner(), out message);
            }
            catch (Exception ex)
            {
                ExcelSelectionBridge.DeleteSnapshot(options == null
                    ? string.Empty : options.TemporarySourcePath);
                message = "无法进入 CAD 插入点选择：" + ex.Message;
                success = false;
                CDBoxStudioLogger.Error("工作台内嵌 Excel 转 CAD 无法进入插入流程。",
                    ex);
            }
            result.ToastKind = success ? "success" : "info";
            result.ToastMessage = message;
            result.ExecuteScript =
                "window.CDBoxExcelOperationCompleted && window.CDBoxExcelOperationCompleted("
                + (success ? "true" : "false") + "," + ToJsString(message) + ");";
        }

        public void Dispose()
        {
            _selectionBridge.Dispose();
            if (_window != null)
            {
                if (!_window.IsDisposed) _window.Dispose();
                _window = null;
            }
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
