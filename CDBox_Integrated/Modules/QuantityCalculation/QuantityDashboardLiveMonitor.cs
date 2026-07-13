using System;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    public static class QuantityDashboardLiveMonitor
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();
        private static Action<string> _scriptSink;
        private static Document _attachedDocument;
        private static Timer _timer;
        private static bool _started;
        private static bool _dirty;
        private static bool _liveEnabled = true;

        public static void Configure(Action<string> scriptSink)
        {
            _scriptSink = scriptSink;
            EnsureStarted();
        }

        public static void SetLiveEnabled(bool enabled)
        {
            _liveEnabled = enabled;
            Push(new { type = "liveMode", enabled = enabled, documentId = QuantityDashboardService.GetDocumentId(AcadApp.DocumentManager.MdiActiveDocument) });
        }

        public static void MarkDirty(string reason)
        {
            _dirty = true;
            if (!_liveEnabled) return;
            EnsureTimer();
            _timer.Stop();
            _timer.Interval = 650;
            _timer.Start();
            Push(new { type = "dirty", reason = reason ?? string.Empty, documentId = QuantityDashboardService.GetDocumentId(AcadApp.DocumentManager.MdiActiveDocument) });
        }

        private static void EnsureStarted()
        {
            if (_started) return;
            _started = true;
            try
            {
                AcadApp.DocumentManager.DocumentActivated += OnDocumentActivated;
            }
            catch
            {
            }
            Attach(AcadApp.DocumentManager.MdiActiveDocument);
            EnsureTimer();
        }

        private static void EnsureTimer()
        {
            if (_timer != null) return;
            _timer = new Timer { Interval = 650 };
            _timer.Tick += delegate
            {
                _timer.Stop();
                if (!_dirty || !_liveEnabled) return;
                _dirty = false;
                Push(new { type = "refreshRequested", documentId = QuantityDashboardService.GetDocumentId(AcadApp.DocumentManager.MdiActiveDocument) });
            };
        }

        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            Attach(e == null ? AcadApp.DocumentManager.MdiActiveDocument : e.Document);
            Push(new { type = "documentActivated", documentId = QuantityDashboardService.GetDocumentId(_attachedDocument) });
        }

        private static void Attach(Document doc)
        {
            if (_attachedDocument == doc) return;
            Detach();
            _attachedDocument = doc;
            if (doc == null) return;
            try { doc.CommandEnded += OnCommandEnded; } catch { }
            try { doc.Database.ObjectAppended += OnObjectChanged; } catch { }
            try { doc.Database.ObjectModified += OnObjectChanged; } catch { }
            try { doc.Database.ObjectErased += OnObjectErased; } catch { }
        }

        private static void Detach()
        {
            Document doc = _attachedDocument;
            _attachedDocument = null;
            if (doc == null) return;
            try { doc.CommandEnded -= OnCommandEnded; } catch { }
            try { doc.Database.ObjectAppended -= OnObjectChanged; } catch { }
            try { doc.Database.ObjectModified -= OnObjectChanged; } catch { }
            try { doc.Database.ObjectErased -= OnObjectErased; } catch { }
        }

        private static void OnObjectChanged(object sender, ObjectEventArgs e)
        {
            _dirty = true;
        }

        private static void OnObjectErased(object sender, ObjectErasedEventArgs e)
        {
            _dirty = true;
        }

        private static void OnCommandEnded(object sender, CommandEventArgs e)
        {
            if (_dirty) MarkDirty(e == null ? "commandEnded" : e.GlobalCommandName);
        }

        private static void Push(object data)
        {
            Action<string> sink = _scriptSink;
            if (sink == null) return;
            try
            {
                string json = Serializer.Serialize(data);
                sink("window.CDBoxQuantityDashboardEvent && window.CDBoxQuantityDashboardEvent(" + json + ");");
            }
            catch
            {
            }
        }
    }
}
