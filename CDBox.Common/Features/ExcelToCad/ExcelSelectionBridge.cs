// Canonical implementation owned by CDBox.Common.
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TCPipeAutoDraw.Modules.ExcelToCad
{
    /// <summary>
    /// Late-bound bridge to desktop Excel/WPS. The plugin never closes the user's
    /// workbook; it only opens/activates it and polls the current continuous range.
    /// </summary>
    internal sealed class ExcelSelectionBridge : IDisposable
    {
        private object _application;
        private object _workbook;

        public bool IsConnected { get { return _application != null; } }

        public void OpenOrAttach(string filePath)
        {
            EnsureApplication();
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                OpenWorkbook(Path.GetFullPath(filePath));
            }
            else
            {
                ExcelLiveSelection current;
                string error;
                if (!TryReadSelection(out current, out error))
                    throw new InvalidOperationException("未检测到当前 Excel 工作簿。请先选择文件。"
                        + (string.IsNullOrWhiteSpace(error) ? string.Empty : "\r\n" + error));
            }

            SetProperty(_application, "Visible", true);
            if (_workbook != null)
            {
                try { InvokeMethod(_workbook, "Activate"); }
                catch { }
            }
        }

        public bool TryReadSelection(out ExcelLiveSelection selection, out string error)
        {
            selection = null;
            error = string.Empty;
            if (_application == null)
            {
                error = "尚未连接 Excel。";
                return false;
            }

            object selected = null;
            object areas = null;
            object worksheet = null;
            object workbook = null;
            try
            {
                selected = GetProperty(_application, "Selection");
                if (selected == null)
                {
                    error = "Excel 中没有活动选择区域。";
                    return false;
                }
                areas = GetProperty(selected, "Areas");
                int areaCount = Convert.ToInt32(GetProperty(areas, "Count"),
                    CultureInfo.InvariantCulture);
                if (areaCount != 1)
                {
                    error = "请选择一个连续的单元格区域，暂不支持多区域选择。";
                    return false;
                }

                string address = Convert.ToString(GetProperty(selected, "Address"),
                    CultureInfo.InvariantCulture);
                worksheet = GetProperty(selected, "Worksheet")
                    ?? GetProperty(selected, "Parent");
                workbook = worksheet == null ? null : GetProperty(worksheet, "Parent");
                string sheetName = Convert.ToString(GetProperty(worksheet, "Name"),
                    CultureInfo.CurrentCulture);
                string fullName = Convert.ToString(GetProperty(workbook, "FullName"),
                    CultureInfo.CurrentCulture);
                if (string.IsNullOrWhiteSpace(address)
                    || string.IsNullOrWhiteSpace(sheetName)
                    || string.IsNullOrWhiteSpace(fullName))
                {
                    error = "当前选择不是有效的 Excel 单元格区域。";
                    return false;
                }
                string fullPath = Path.GetFullPath(fullName);
                if (!File.Exists(fullPath))
                {
                    error = "当前工作簿尚未保存。请先保存工作簿，再选择需要转换的区域。";
                    return false;
                }

                selection = new ExcelLiveSelection
                {
                    FilePath = fullPath,
                    SheetName = sheetName,
                    RangeAddress = ExcelTableReader.NormalizeRangeAddress(address)
                };
                return true;
            }
            catch (COMException)
            {
                error = "Excel 正忙或当前选择无法读取，请完成单元格选择后重试。";
                return false;
            }
            catch (Exception ex)
            {
                error = "读取 Excel 当前选择失败：" + ex.Message;
                return false;
            }
            finally
            {
                ReleaseComObject(workbook);
                ReleaseComObject(worksheet);
                ReleaseComObject(areas);
                ReleaseComObject(selected);
            }
        }

        public string CreateWorkbookSnapshot()
        {
            if (_application == null)
                throw new InvalidOperationException("尚未连接 Excel。");

            object workbook = null;
            string snapshotPath = string.Empty;
            try
            {
                workbook = GetProperty(_application, "ActiveWorkbook");
                if (workbook == null)
                    throw new InvalidOperationException("Excel 中没有活动工作簿。");

                string fullName = Convert.ToString(GetProperty(workbook, "FullName"),
                    CultureInfo.CurrentCulture);
                string extension = Path.GetExtension(fullName);
                if (!string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".xlsx",
                        StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".xlsm",
                        StringComparison.OrdinalIgnoreCase))
                    extension = ".xlsx";

                string directory = SnapshotDirectory;
                Directory.CreateDirectory(directory);
                snapshotPath = Path.Combine(directory, "selection-"
                    + Guid.NewGuid().ToString("N") + extension.ToLowerInvariant());
                InvokeMethod(workbook, "SaveCopyAs", snapshotPath);
                if (!File.Exists(snapshotPath))
                    throw new IOException("Excel 未能生成当前工作簿的临时副本。");
                return snapshotPath;
            }
            catch
            {
                DeleteSnapshot(snapshotPath);
                throw;
            }
            finally
            {
                ReleaseComObject(workbook);
            }
        }

        public static void DeleteSnapshot(string snapshotPath)
        {
            if (string.IsNullOrWhiteSpace(snapshotPath)) return;
            try
            {
                string root = Path.GetFullPath(SnapshotDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                string fullPath = Path.GetFullPath(snapshotPath);
                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return;
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
            catch { }
        }

        public void Dispose()
        {
            ReleaseComObject(_workbook);
            _workbook = null;
            ReleaseComObject(_application);
            _application = null;
        }

        private void EnsureApplication()
        {
            if (_application != null) return;
            string[] programIds = { "Excel.Application", "ket.Application" };
            for (int i = 0; i < programIds.Length && _application == null; i++)
            {
                try { _application = Marshal.GetActiveObject(programIds[i]); }
                catch { }
            }
            for (int i = 0; i < programIds.Length && _application == null; i++)
            {
                try
                {
                    Type type = Type.GetTypeFromProgID(programIds[i], false);
                    if (type != null) _application = Activator.CreateInstance(type);
                }
                catch { }
            }
            if (_application == null)
                throw new InvalidOperationException(
                    "无法启动 Excel/WPS。请确认已安装桌面版 Excel 或 WPS 表格。");
        }

        private void OpenWorkbook(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Excel 文件不存在。", filePath);

            object workbooks = null;
            object opened = null;
            try
            {
                workbooks = GetProperty(_application, "Workbooks");
                int count = Convert.ToInt32(GetProperty(workbooks, "Count"),
                    CultureInfo.InvariantCulture);
                for (int i = 1; i <= count; i++)
                {
                    object candidate = null;
                    try
                    {
                        candidate = GetIndexedProperty(workbooks, "Item", i);
                        string candidatePath = Convert.ToString(
                            GetProperty(candidate, "FullName"), CultureInfo.CurrentCulture);
                        if (!string.Equals(Path.GetFullPath(candidatePath), filePath,
                            StringComparison.OrdinalIgnoreCase)) continue;
                        opened = candidate;
                        candidate = null;
                        break;
                    }
                    catch { }
                    finally { ReleaseComObject(candidate); }
                }
                if (opened == null)
                    opened = InvokeMethod(workbooks, "Open", filePath);
                ReleaseComObject(_workbook);
                _workbook = opened;
                opened = null;
            }
            finally
            {
                ReleaseComObject(opened);
                ReleaseComObject(workbooks);
            }
        }

        private static object GetProperty(object target, string name)
        {
            if (target == null) return null;
            return target.GetType().InvokeMember(name,
                BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                null, target, null, CultureInfo.CurrentCulture);
        }

        private static object GetIndexedProperty(object target, string name, object index)
        {
            if (target == null) return null;
            return target.GetType().InvokeMember(name,
                BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                null, target, new[] { index }, CultureInfo.CurrentCulture);
        }

        private static void SetProperty(object target, string name, object value)
        {
            if (target == null) return;
            target.GetType().InvokeMember(name,
                BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.Instance,
                null, target, new[] { value }, CultureInfo.CurrentCulture);
        }

        private static object InvokeMethod(object target, string name,
            params object[] arguments)
        {
            if (target == null) return null;
            return target.GetType().InvokeMember(name,
                BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                null, target, arguments, CultureInfo.CurrentCulture);
        }

        private static void ReleaseComObject(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.ReleaseComObject(value); }
            catch { }
        }

        private static string SnapshotDirectory
        {
            get
            {
                return Path.Combine(Path.GetTempPath(), "CDBox", "ExcelToCad");
            }
        }
    }

    internal sealed class ExcelLiveSelection
    {
        public ExcelLiveSelection()
        {
            FilePath = string.Empty;
            SheetName = string.Empty;
            RangeAddress = string.Empty;
        }

        public string FilePath { get; set; }
        public string SheetName { get; set; }
        public string RangeAddress { get; set; }
    }
}
