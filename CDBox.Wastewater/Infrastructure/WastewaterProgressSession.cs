using System;
using Autodesk.AutoCAD.ApplicationServices;
using CDBox.Wastewater.Module;
namespace CDBox.Wastewater.Infrastructure
{
    public sealed class CDBoxProgressSession : IDisposable
    {
        private readonly IDisposable _prompt;
        private readonly Document _document;

        private CDBoxProgressSession(Document document, IDisposable prompt)
        {
            _document = document;
            _prompt = prompt;
        }

        public static CDBoxProgressSession Start(Document document,
            string title, string message, string source = null,
            string mergeKey = null, bool indeterminate = true)
        {
            IDisposable prompt = WastewaterRuntimeServices.Prompts == null
                ? null : WastewaterRuntimeServices.Prompts.Begin(title,
                    message);
            return new CDBoxProgressSession(document, prompt);
        }

        public void Report(int current, int total, string message)
        {
            if (current == 0 || current == total) Write(message);
        }

        public void ReportMarquee(string message) { Write(message); }
        public void Complete(string message) { Write(message); }
        public void Fail(string message) { Write(message); }
        public void Cancel(string message) { Write(message); }

        private void Write(string message)
        {
            try
            {
                if (_document != null && _document.Editor != null
                    && !string.IsNullOrWhiteSpace(message))
                    _document.Editor.WriteMessage("\n[CDBox] " + message);
            }
            catch { }
        }

        public void Dispose()
        {
            if (_prompt != null) _prompt.Dispose();
        }
    }
}
