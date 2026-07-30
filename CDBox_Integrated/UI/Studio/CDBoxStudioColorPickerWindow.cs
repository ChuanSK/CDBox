using System;
using System.Drawing;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using TCPipeAutoDraw.Core.Colors;
using AcadColorDialog = Autodesk.AutoCAD.Windows.ColorDialog;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioColorPickerWindow : IDisposable
    {
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        private readonly CDBoxColor _initial;
        private readonly CDBoxStudioColorPickerOptions _options;
        private CDBoxStudioWebPageForm _window;
        private CDBoxColor _selected;

        private CDBoxStudioColorPickerWindow(CDBoxColor initial,
            CDBoxStudioColorPickerOptions options)
        {
            _initial = (initial ?? CDBoxColor.FromIndex(7)).Clone();
            _options = options;
        }

        public static bool TryPick(CDBoxColor initial, out CDBoxColor selected,
            bool allowByLayer = true, bool allowByBlock = true,
            bool allowTrueColor = true, bool allowColorBook = true,
            bool allowStandard = true, IWin32Window owner = null)
        {
            var options = new CDBoxStudioColorPickerOptions
            {
                AllowByLayer = allowByLayer,
                AllowByBlock = allowByBlock,
                AllowTrueColor = allowTrueColor,
                AllowColorBook = allowColorBook,
                AllowStandard = allowStandard
            };
            using (var controller = new CDBoxStudioColorPickerWindow(initial, options))
            {
                controller.Show(owner ?? new AcadMainWindow());
                selected = controller._selected == null
                    ? null : controller._selected.Clone();
                return selected != null;
            }
        }

        private void Show(IWin32Window owner)
        {
            _window = new CDBoxStudioWebPageForm(
                "CDBox 颜色选择器",
                delegate
                {
                    return CDBoxStudioColorPickerPage.BuildStandaloneDocument(
                        CDBoxStudioSettingsStore.Load(), _initial, _options);
                },
                Route,
                "color-picker");
            _window.Width = 900;
            _window.Height = 690;
            _window.MinimumSize = new Size(700, 540);
            _window.ShowDialog(owner);
        }

        private CDBoxStudioRouteResult Route(CDBoxStudioRouteRequest request)
        {
            var result = new CDBoxStudioRouteResult { Handled = true };
            if (request == null) return result;
            switch ((request.Name ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "ready":
                    return result;
                case "cancelcolorpicker":
                    Close();
                    return result;
                case "confirmcolorpicker":
                    return Confirm(request.Argument);
                case "browsecadcolorbook":
                    return BrowseColorBook(request.Argument);
                default:
                    result.Handled = false;
                    return result;
            }
        }

        private CDBoxStudioRouteResult Confirm(string payload)
        {
            var result = new CDBoxStudioRouteResult { Handled = true };
            try
            {
                CDBoxColor value = Serializer.Deserialize<CDBoxColor>(payload ?? string.Empty);
                string error;
                value = Normalize(value, out error);
                if (value == null)
                {
                    result.ToastKind = "error";
                    result.ToastMessage = error;
                    return result;
                }
                _selected = value;
                Close();
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "颜色数据无效：" + ex.Message;
            }
            return result;
        }

        private CDBoxStudioRouteResult BrowseColorBook(string payload)
        {
            var result = new CDBoxStudioRouteResult { Handled = true };
            if (!_options.AllowColorBook)
            {
                result.ToastKind = "error";
                result.ToastMessage = "当前功能不支持配色系统颜色。";
                return result;
            }
            try
            {
                CDBoxColor current = string.IsNullOrWhiteSpace(payload)
                    ? _initial : Serializer.Deserialize<CDBoxColor>(payload);
                var dialog = new AcadColorDialog { IncludeByBlockByLayer = false };
                dialog.Color = CDBoxColorService.ToCadColor(current);
                dialog.SetDialogTabs(AcadColorDialog.ColorTabs.ColorBookTab);
                if (dialog.ShowModal() != true) return result;
                CDBoxColor selected = CDBoxColorService.FromCadColor(dialog.Color);
                if (selected.Type != CDBoxColorType.ColorBook)
                {
                    result.ToastKind = "warning";
                    result.ToastMessage = "请在 AutoCAD 配色系统页中选择颜色。";
                    return result;
                }
                result.ExecuteScript =
                    "window.CDBoxColorPickerFromCad && window.CDBoxColorPickerFromCad("
                    + Serializer.Serialize(selected) + ");";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "无法读取 AutoCAD 配色系统：" + ex.Message;
            }
            return result;
        }

        private CDBoxColor Normalize(CDBoxColor value, out string error)
        {
            error = string.Empty;
            value = value ?? CDBoxColor.FromIndex(7);
            switch (value.Type)
            {
                case CDBoxColorType.ByLayer:
                    if (_options.AllowByLayer) return CDBoxColor.ByLayer();
                    break;
                case CDBoxColorType.ByBlock:
                    if (_options.AllowByBlock) return CDBoxColor.ByBlock();
                    break;
                case CDBoxColorType.IndexColor:
                    return CDBoxColor.FromIndex(value.Index);
                case CDBoxColorType.TrueColor:
                    if (_options.AllowTrueColor) return CDBoxColor.FromRgb(value.R, value.G, value.B);
                    break;
                case CDBoxColorType.ColorBook:
                    if (_options.AllowColorBook
                        && !string.IsNullOrWhiteSpace(value.BookName)
                        && !string.IsNullOrWhiteSpace(value.ColorName))
                    {
                        value = value.Clone();
                        value.BookName = value.BookName.Trim();
                        value.ColorName = value.ColorName.Trim();
                        value.DisplayName = value.BookName + " · " + value.ColorName;
                        return value;
                    }
                    error = "请同时填写配色系统名称和颜色名称。";
                    return null;
                case CDBoxColorType.CDBoxStandard:
                    if (_options.AllowStandard) return value.Clone();
                    break;
            }
            error = "当前功能不支持所选颜色类型。";
            return null;
        }

        private void Close()
        {
            if (_window == null || _window.IsDisposed) return;
            _window.Close();
        }

        public void Dispose()
        {
            if (_window != null)
            {
                if (!_window.IsDisposed) _window.Dispose();
                _window = null;
            }
        }
    }
}
