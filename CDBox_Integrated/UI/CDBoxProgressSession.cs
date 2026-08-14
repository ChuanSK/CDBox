using System;
using Autodesk.AutoCAD.ApplicationServices;
using TCPipeAutoDraw.Core.FloatingCenter;
using TCPipeAutoDraw.UI.FloatingCenter;
using FloatingCenterHub = TCPipeAutoDraw.Core.FloatingCenter.FloatingCenter;

namespace TCPipeAutoDraw.UI
{
    /// <summary>
    /// 统一的长任务进度会话。它只发布结构化状态，不创建独立窗口。
    /// </summary>
    public sealed class CDBoxProgressSession : IDisposable
    {
        private readonly IProgressHandle _handle;
        private readonly Document _document;
        private readonly string _title;
        private readonly bool _writeFallback;
        private int _lastFallbackBucket = -1;
        private int _lastPublishedTick;
        private double _lastPublishedPercent = -1.0;
        private bool _finished;

        private CDBoxProgressSession(IProgressHandle handle,
            Document document, string title, bool writeFallback)
        {
            _handle = handle;
            _document = document;
            _title = title;
            _writeFallback = writeFallback;
        }

        public static CDBoxProgressSession Start(Document document,
            string title, string message, string source = null,
            string mergeKey = null, bool indeterminate = true)
        {
            string resolvedTitle = string.IsNullOrWhiteSpace(title)
                ? "正在处理" : title.Trim();
            IProgressHandle handle = FloatingCenterHub.Current.BeginProgress(
                new FloatingProgressSpec
                {
                    DocumentId = document == null ? string.Empty :
                        CadFloatingDocumentIdentity.GetDocumentId(document),
                    Source = string.IsNullOrWhiteSpace(source)
                        ? "CDBoxProgress" : source.Trim(),
                    Title = resolvedTitle,
                    Message = string.IsNullOrWhiteSpace(message)
                        ? "正在处理，请稍候…" : message.Trim(),
                    MergeKey = string.IsNullOrWhiteSpace(mergeKey)
                        ? "progress:" + resolvedTitle : mergeKey.Trim(),
                    IsIndeterminate = indeterminate
                });
            bool fallback = !FloatingCenterController.CanPresent;
            var session = new CDBoxProgressSession(handle, document,
                resolvedTitle, fallback);
            if (fallback) session.WriteFallback(message);
            CDBoxUiResponsiveness.YieldToRender(document, true);
            return session;
        }

        public void Report(int current, int total, string message)
        {
            if (_finished || _handle == null) return;
            if (total <= 0) total = 1;
            current = Math.Max(0, Math.Min(current, total));
            double percent = current * 100.0 / total;
            int now = Environment.TickCount;
            bool publish = current == 0 || current == total
                || _lastPublishedPercent < 0.0
                || percent - _lastPublishedPercent >= 1.0
                || (uint)(now - _lastPublishedTick) >= 80U;
            if (publish)
            {
                _lastPublishedTick = now;
                _lastPublishedPercent = percent;
                _handle.Report(percent, message);
                CDBoxUiResponsiveness.YieldToRender(_document);
            }
            if (_writeFallback)
            {
                int bucket = (int)Math.Floor(current * 10.0 / total);
                if (bucket != _lastFallbackBucket)
                {
                    _lastFallbackBucket = bucket;
                    WriteFallback(message);
                }
            }
        }

        public void ReportMarquee(string message)
        {
            if (_finished || _handle == null) return;
            _handle.Report(null, message);
            _lastPublishedTick = Environment.TickCount;
            _lastPublishedPercent = -1.0;
            CDBoxUiResponsiveness.YieldToRender(_document, true);
            if (_writeFallback) WriteFallback(message);
        }

        public void Complete(string message)
        {
            if (_finished || _handle == null) return;
            _finished = true;
            _handle.Complete(message);
            CDBoxUiResponsiveness.YieldToRender(_document, true);
            if (_writeFallback) WriteFallback(message);
        }

        public void Fail(string message)
        {
            if (_finished || _handle == null) return;
            _finished = true;
            _handle.Fail(message);
            CDBoxUiResponsiveness.YieldToRender(_document, true);
            if (_writeFallback) WriteFallback(message);
        }

        public void Cancel(string message)
        {
            if (_finished || _handle == null) return;
            _finished = true;
            _handle.Cancel(message);
            CDBoxUiResponsiveness.YieldToRender(_document, true);
            if (_writeFallback) WriteFallback(message);
        }

        public void Dispose()
        {
            if (_finished || _handle == null) return;
            _finished = true;
            _handle.Dispose();
        }

        private void WriteFallback(string message)
        {
            try
            {
                if (_document == null || _document.Editor == null) return;
                _document.Editor.WriteMessage("\n[" + _title + "] "
                    + (string.IsNullOrWhiteSpace(message)
                        ? "正在处理…" : message.Trim()));
            }
            catch { }
        }
    }
}
