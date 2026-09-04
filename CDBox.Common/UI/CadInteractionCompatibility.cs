using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.GraphicsInterface;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.UI
{
    internal sealed class AcadMainWindow : IWin32Window
    {
        public IntPtr Handle
        {
            get
            {
                try
                {
                    return AcadApp.MainWindow == null
                        ? IntPtr.Zero : AcadApp.MainWindow.Handle;
                }
                catch { return IntPtr.Zero; }
            }
        }
    }

    internal static class CommonEditorInteractionExtensions
    {
        public static PromptPointResult GetHudPoint(this Editor editor,
            string message)
        {
            return editor.GetPoint(message);
        }

        public static PromptPointResult GetHudPoint(this Editor editor,
            PromptPointOptions options)
        {
            return editor.GetPoint(options);
        }

        public static PromptPointResult GetHudCorner(this Editor editor,
            PromptCornerOptions options)
        {
            return editor.GetCorner(options);
        }

        public static PromptSelectionResult GetHudSelection(
            this Editor editor, PromptSelectionOptions options)
        {
            return editor.GetSelection(options);
        }

        public static PromptResult DragWithHud(this Editor editor,
            DrawJig jig, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                editor.WriteMessage("\n" + message.Trim());
            return editor.Drag(jig);
        }

        public static void WriteHudMessage(this Editor editor,
            string message, params object[] arguments)
        {
            if (editor == null) return;
            editor.WriteMessage(message ?? string.Empty,
                arguments ?? new object[0]);
        }
    }

    internal sealed class CDBoxProgressSession : IDisposable
    {
        private readonly Document _document;
        private readonly string _title;

        private CDBoxProgressSession(Document document, string title,
            string message)
        {
            _document = document;
            _title = string.IsNullOrWhiteSpace(title) ? "正在处理" : title;
            Write(message);
        }

        public static CDBoxProgressSession Start(Document document,
            string title, string message, string source = null,
            string mergeKey = null, bool indeterminate = true)
        {
            return new CDBoxProgressSession(document, title, message);
        }

        public void Report(int current, int total, string message)
        {
            if (total > 0 && (current == 0 || current == total))
                Write(message);
        }

        public void Complete(string message) { Write(message); }
        public void Fail(string message) { Write(message); }
        public void Dispose() { }

        private void Write(string message)
        {
            try
            {
                if (_document != null && _document.Editor != null)
                    _document.Editor.WriteMessage("\n[" + _title + "] "
                        + (message ?? string.Empty));
            }
            catch { }
        }
    }
}

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioFrameChoiceItem
    {
        public string Value { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public string Badge { get; set; }
    }

    internal static class CDBoxStudioFrameChoiceWindow
    {
        public static bool TryChoose(string title, string subtitle,
            IList<CDBoxStudioFrameChoiceItem> items, string defaultValue,
            out string selectedValue, IWin32Window owner = null)
        {
            selectedValue = string.Empty;
            List<CDBoxStudioFrameChoiceItem> choices =
                (items ?? new List<CDBoxStudioFrameChoiceItem>())
                    .Where(x => x != null
                        && !string.IsNullOrWhiteSpace(x.Value)).ToList();
            if (choices.Count == 0) return false;

            using (var form = new Form())
            using (var list = new ListBox())
            using (var ok = new Button())
            using (var cancel = new Button())
            using (var hint = new Label())
            {
                form.Text = string.IsNullOrWhiteSpace(title)
                    ? "请选择" : title.Trim();
                form.Font = new Font("Microsoft YaHei UI", 9F);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(470,
                    Math.Min(520, 160 + choices.Count * 54));

                hint.Text = subtitle ?? string.Empty;
                hint.AutoEllipsis = true;
                hint.SetBounds(16, 14, 438, 34);
                list.SetBounds(16, 52, 438,
                    form.ClientSize.Height - 106);
                list.DisplayMember = "Title";
                // Do not use DataSource here.  Under older AutoCAD/WinForms
                // hosts (notably AutoCAD 2016), currency-manager binding can
                // be deferred until the form receives a BindingContext.  At
                // this point Items may therefore still be empty, making the
                // otherwise valid SelectedIndex = 0 throw an
                // ArgumentOutOfRangeException.  The choices are a fixed
                // snapshot, so populate them synchronously instead.
                list.Items.AddRange(choices.Cast<object>().ToArray());
                list.IntegralHeight = false;

                int selected = choices.FindIndex(x => string.Equals(
                    x.Value, defaultValue,
                    StringComparison.OrdinalIgnoreCase));
                int targetIndex = selected >= 0
                    && selected < list.Items.Count ? selected
                    : (list.Items.Count > 0 ? 0 : -1);
                if (targetIndex >= 0) list.SelectedIndex = targetIndex;

                ok.Text = "确定";
                ok.DialogResult = DialogResult.OK;
                ok.SetBounds(286, form.ClientSize.Height - 42, 80, 28);
                cancel.Text = "取消";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(374, form.ClientSize.Height - 42, 80, 28);
                form.Controls.AddRange(new Control[]
                    { hint, list, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                if (form.ShowDialog(owner) != DialogResult.OK
                    || list.SelectedItem == null) return false;

                selectedValue = ((CDBoxStudioFrameChoiceItem)
                    list.SelectedItem).Value ?? string.Empty;
                return selectedValue.Length > 0;
            }
        }
    }

    internal static class CDBoxStudioFrameNoticeWindow
    {
        public static void ShowNotice(string title, IList<string> messages,
            IWin32Window owner = null)
        {
            string message = string.Join(Environment.NewLine,
                (messages ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => "• " + x.Trim()));
            if (message.Length == 0) return;
            MessageBox.Show(owner, message,
                string.IsNullOrWhiteSpace(title) ? "图框提示" : title,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    internal static class CDBoxStudioFrameSettingsWindow
    {
        internal static Action ShowAction { get; set; }

        public static void ShowWindow(IWin32Window owner)
        {
            Action action = ShowAction;
            if (action != null) action();
        }
    }
}
