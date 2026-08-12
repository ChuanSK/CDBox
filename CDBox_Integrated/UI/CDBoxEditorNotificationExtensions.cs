using System;
using System.Globalization;
using System.Text.RegularExpressions;
using TCPipeAutoDraw.UI;

namespace Autodesk.AutoCAD.EditorInput
{
    internal static class CDBoxEditorNotificationExtensions
    {
        private static readonly Regex TitledMessage = new Regex(
            @"^\s*\[(?<title>[^\]]+)\]\s*(?<body>[\s\S]*)$",
            RegexOptions.Compiled);

        public static void WriteHudMessage(this Editor editor,
            string message, params object[] args)
        {
            string value = Format(message, args);
            if (string.IsNullOrWhiteSpace(value)) return;
            if (IsHeadlessSelfTest())
            {
                if (editor != null) editor.WriteMessage(value);
                return;
            }
            if (IsSelfTestDiagnostic(value))
            {
                if (editor != null) editor.WriteMessage(value);
                if (value.IndexOf("CDBOX_SELFTEST_RESULT",
                        StringComparison.OrdinalIgnoreCase) < 0)
                    return;
            }
            if (LooksLikeStartupBanner(value))
            {
                return;
            }

            string normalized = value.Replace("\r\n", "\n")
                .Replace('\r', '\n').Trim();
            CDBoxNotificationKind kind = Classify(normalized);
            if (IsSuppressedAnnotationStatus(normalized, kind)) return;
            string title = "CDBox";
            string body = normalized;
            Match match = TitledMessage.Match(normalized);
            if (match.Success)
            {
                title = match.Groups["title"].Value.Trim();
                body = match.Groups["body"].Value.Trim();
            }
            if (string.IsNullOrWhiteSpace(body)) body = normalized;
            CDBoxNotificationService.Notify(title, body, kind);
        }

        public static PromptPointResult GetHudPoint(this Editor editor,
            string message)
        {
            using (CDBoxNotificationService.BeginCommandPrompt(
                "操作提示", message))
                return editor.GetPoint("\n ");
        }

        public static PromptPointResult GetHudPoint(this Editor editor,
            PromptPointOptions options)
        {
            if (options == null) return editor.GetPoint(options);
            string message = options.Message;
            options.Message = "\n ";
            try
            {
                using (CDBoxNotificationService.BeginCommandPrompt(
                    "操作提示", message))
                    return editor.GetPoint(options);
            }
            finally
            {
                options.Message = message;
            }
        }

        public static PromptPointResult GetHudCorner(this Editor editor,
            PromptCornerOptions options)
        {
            if (options == null) return editor.GetCorner(options);
            string message = options.Message;
            options.Message = "\n ";
            try
            {
                using (CDBoxNotificationService.BeginCommandPrompt(
                    "操作提示", message))
                    return editor.GetCorner(options);
            }
            finally
            {
                options.Message = message;
            }
        }

        public static PromptSelectionResult GetHudSelection(
            this Editor editor, PromptSelectionOptions options)
        {
            if (options == null) return editor.GetSelection(options);
            string adding = options.MessageForAdding;
            string removing = options.MessageForRemoval;
            options.MessageForAdding = "\n ";
            options.MessageForRemoval = "\n ";
            try
            {
                using (CDBoxNotificationService.BeginCommandPrompt(
                    "选择对象", adding))
                    return editor.GetSelection(options);
            }
            finally
            {
                options.MessageForAdding = adding;
                options.MessageForRemoval = removing;
            }
        }

        public static PromptEntityResult GetHudEntity(this Editor editor,
            PromptEntityOptions options)
        {
            if (options == null) return editor.GetEntity(options);
            string message = options.Message;
            options.Message = "\n ";
            try
            {
                using (CDBoxNotificationService.BeginCommandPrompt(
                    "选择对象", message))
                    return editor.GetEntity(options);
            }
            finally
            {
                options.Message = message;
            }
        }

        public static PromptIntegerResult GetHudInteger(this Editor editor,
            PromptIntegerOptions options)
        {
            if (options == null) return editor.GetInteger(options);
            string message = options.Message;
            options.Message = "\n ";
            try
            {
                using (CDBoxNotificationService.BeginCommandPrompt(
                    "输入参数", message))
                    return editor.GetInteger(options);
            }
            finally
            {
                options.Message = message;
            }
        }

        public static PromptResult GetHudString(this Editor editor,
            PromptStringOptions options)
        {
            if (options == null) return editor.GetString(options);
            string message = options.Message;
            options.Message = "\n ";
            try
            {
                using (CDBoxNotificationService.BeginCommandPrompt(
                    "输入参数", message))
                    return editor.GetString(options);
            }
            finally
            {
                options.Message = message;
            }
        }

        public static PromptResult DragWithHud(this Editor editor,
            DrawJig jig, string message)
        {
            using (CDBoxNotificationService.BeginCommandPrompt(
                ResolvePromptTitle(message, "操作提示"), message))
                return editor.Drag(jig);
        }

        private static string ResolvePromptTitle(string message,
            string fallback)
        {
            string value = message ?? string.Empty;
            if (ContainsAny(value, "落图位置", "插入点"))
                return "选择落图位置";
            return fallback;
        }

        private static string Format(string message, object[] args)
        {
            string source = message ?? string.Empty;
            if (args == null || args.Length == 0) return source;
            try
            {
                return string.Format(CultureInfo.CurrentCulture,
                    source, args);
            }
            catch
            {
                return source;
            }
        }

        private static CDBoxNotificationKind Classify(string value)
        {
            string text = value ?? string.Empty;
            if (ContainsAny(text, "失败", "错误", "异常", "无法",
                    "未找到", "无效", "致命"))
                return CDBoxNotificationKind.Error;
            if (ContainsAny(text, "警告", "取消", "跳过", "未选择",
                    "至少", "请先", "不支持"))
                return CDBoxNotificationKind.Warning;
            if (ContainsAny(text, "完成", "成功", "已生成", "已保存",
                    "已更新", "已创建", "已设置", "已布置"))
                return CDBoxNotificationKind.Success;
            return CDBoxNotificationKind.Information;
        }

        private static bool IsSuppressedAnnotationStatus(string value,
            CDBoxNotificationKind kind)
        {
            string text = value ?? string.Empty;
            bool annotationMessage = ContainsAny(text,
                "[管线长度标注]", "[节点标注]", "[表面积标注]");
            if (!annotationMessage) return false;
            if (kind == CDBoxNotificationKind.Success) return true;
            if (ContainsAny(text, "已退出", "已取消", "取消选择"))
                return true;
            return kind == CDBoxNotificationKind.Information
                && ContainsAny(text, "已启动", "正在通过", "完成后将");
        }

        private static bool ContainsAny(string value,
            params string[] terms)
        {
            foreach (string term in terms)
            {
                if (value.IndexOf(term,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static bool IsHeadlessSelfTest()
        {
            string value = Environment.GetEnvironmentVariable(
                "CDBOX_HEADLESS_SELFTEST");
            return string.Equals(value, "1",
                       StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSelfTestDiagnostic(string value)
        {
            return value.IndexOf("CDBox Self Test",
                       StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("CDBOX_SELFTEST_RESULT",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || value.TrimStart().StartsWith("[PASS]",
                    StringComparison.OrdinalIgnoreCase)
                || value.TrimStart().StartsWith("[FAIL]",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeStartupBanner(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 160)
                return false;
            int lines = value.CountCharacter('\n');
            return lines >= 5 && value.IndexOf("CDBox",
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountCharacter(this string value, char target)
        {
            int count = 0;
            foreach (char current in value)
                if (current == target) count++;
            return count;
        }
    }
}
