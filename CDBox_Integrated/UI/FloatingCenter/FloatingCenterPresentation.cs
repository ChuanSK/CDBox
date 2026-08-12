using System;
using System.Collections.Generic;
using System.Linq;
using TCPipeAutoDraw.Core.FloatingCenter;

namespace TCPipeAutoDraw.UI.FloatingCenter
{
    internal sealed class FloatingCenterActionEventArgs : EventArgs
    {
        public FloatingCenterActionEventArgs(string actionId)
        {
            ActionId = actionId ?? string.Empty;
        }

        public string ActionId { get; private set; }
    }

    internal sealed class FloatingCenterViewSnapshot
    {
        public string DocumentId { get; set; }
        public string DocumentName { get; set; }
        public FloatingStatusSnapshot Status { get; set; }
        public IList<FloatingMessage> Active { get; set; }
        public IList<FloatingMessage> History { get; set; }
        public IList<FloatingMessage> Ignored { get; set; }
    }

    internal static class FloatingCenterPresentation
    {
        public static string Badge(int taskGroupCount)
        {
            if (taskGroupCount <= 0) return string.Empty;
            return taskGroupCount > 9 ? "9+" : taskGroupCount.ToString();
        }

        public static string HealthText(FloatingHealthState value)
        {
            switch (value)
            {
                case FloatingHealthState.Critical: return "存在严重问题";
                case FloatingHealthState.Warning: return "存在警告";
                case FloatingHealthState.Pending: return "存在待处理任务";
                default: return "当前图纸正常";
            }
        }

        public static string ActivityText(FloatingActivityState value)
        {
            switch (value)
            {
                case FloatingActivityState.Working: return "正在处理";
                case FloatingActivityState.WaitingForCadInput: return "等待 CAD 输入";
                case FloatingActivityState.WaitingForUserDecision: return "等待确认";
                case FloatingActivityState.Paused: return "已暂停";
                default: return "空闲";
            }
        }

        public static string HealthColor(FloatingHealthState value)
        {
            switch (value)
            {
                case FloatingHealthState.Critical: return "#EF4444";
                case FloatingHealthState.Warning: return "#F59E0B";
                case FloatingHealthState.Pending: return "#7C5CFC";
                default: return "#3B82F6";
            }
        }

        public static double? OverallProgress(IEnumerable<FloatingMessage> messages)
        {
            List<FloatingMessage> progress = (messages ??
                Enumerable.Empty<FloatingMessage>()).Where(x => x != null &&
                    x.Kind == FloatingMessageKind.Progress).ToList();
            if (progress.Count == 0 || progress.Any(x => x.IsIndeterminate ||
                !x.Progress.HasValue)) return null;
            return progress.Average(x => Math.Max(0.0,
                Math.Min(100.0, x.Progress.Value)));
        }

        public static string KindGlyph(FloatingMessageKind kind)
        {
            switch (kind)
            {
                case FloatingMessageKind.Success: return "✓";
                case FloatingMessageKind.Warning: return "!";
                case FloatingMessageKind.Error: return "×";
                case FloatingMessageKind.Progress: return "↻";
                case FloatingMessageKind.Prompt: return "…";
                case FloatingMessageKind.Decision: return "?";
                case FloatingMessageKind.Sync: return "⇄";
                case FloatingMessageKind.Check: return "⌕";
                default: return "i";
            }
        }
    }
}
