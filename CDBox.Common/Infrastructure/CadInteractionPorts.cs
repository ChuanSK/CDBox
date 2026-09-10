using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.GraphicsInterface;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CDBox.Common.Infrastructure
{
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

    internal static class FrameChoiceInput
    {
        public static bool TryChoose(string title, string subtitle,
            IList<CDBoxStudioFrameChoiceItem> items, string defaultValue,
            out string selectedValue)
        {
            object[] args = { title, subtitle, items, defaultValue, null, null };
            bool accepted = CDBox.Shared.UI.CDBoxUiGateway.Call<bool>("common.choice", "TryChoose", args);
            selectedValue = (string)args[4];
            return accepted;
        }
    }

    internal static class FrameNoticeInput
    {
        public static void ShowNotice(string title, IList<string> messages)
        {
            CDBox.Shared.UI.CDBoxUiGateway.Call("common.notice", "ShowNotice", title, messages, null);
        }
    }

    internal static class CDBoxStudioFrameSettingsWindow
    {
        internal static Action ShowAction { get; set; }

        public static void ShowWindow()
        {
            Action action = ShowAction;
            if (action != null) action();
        }
    }
}
