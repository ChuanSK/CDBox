using System;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.GraphicsInterface;
using CDBox.Wastewater.Module;

namespace Autodesk.AutoCAD.EditorInput
{
    internal static class WastewaterEditorExtensions
    {
        public static void WriteHudMessage(this Editor editor,
            string message, params object[] args)
        {
            string value = message ?? string.Empty;
            if (args != null && args.Length > 0)
                try { value = string.Format(value, args); } catch { }
            if (editor != null && value.Length > 0)
                editor.WriteMessage(value);
        }

        public static PromptPointResult GetHudPoint(this Editor editor,
            PromptPointOptions options)
        {
            string message = options == null ? string.Empty : options.Message;
            using (Begin("操作提示", message))
                return editor.GetPoint(options);
        }

        public static PromptEntityResult GetHudEntity(this Editor editor,
            PromptEntityOptions options)
        {
            string message = options == null ? string.Empty : options.Message;
            using (Begin("选择对象", message))
                return editor.GetEntity(options);
        }

        public static PromptSelectionResult GetHudSelection(
            this Editor editor, PromptSelectionOptions options)
        {
            string message = options == null ? string.Empty
                : options.MessageForAdding;
            using (Begin("选择对象", message))
                return editor.GetSelection(options);
        }

        public static PromptResult DragWithHud(this Editor editor,
            DrawJig jig, string message)
        {
            using (Begin("选择落图位置", message))
                return editor.Drag(jig);
        }

        private static IDisposable Begin(string title, string message)
        {
            return WastewaterRuntimeServices.Prompts == null
                ? EmptyDisposable.Instance
                : WastewaterRuntimeServices.Prompts.Begin(title, message);
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance =
                new EmptyDisposable();
            public void Dispose() { }
        }
    }
}
