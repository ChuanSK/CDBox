using System;
using System.Collections.Generic;
using System.Linq;
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
        private static int _changeTrackingSuppressionDepth;
        private static readonly HashSet<string> DirtyHandles =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static event EventHandler<QuantityDashboardDirtyEventArgs> DirtyMarked;

        /// <summary>
        /// 临时忽略只生成辅助图形、不会改变工程量的数据写入。
        /// 仅由明确知道其输出不参与污水工程量的业务命令使用。
        /// </summary>
        public static IDisposable SuspendChangeTracking()
        {
            _changeTrackingSuppressionDepth++;
            return new ChangeTrackingSuppression();
        }

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
            MarkDirty(AcadApp.DocumentManager.MdiActiveDocument, reason);
        }

        public static void MarkDirty(Document document, string reason)
        {
            List<string> handles = DirtyHandles.ToList();
            DirtyHandles.Clear();
            _dirty = true;
            RaiseDirtyMarked(document, reason, handles);
            if (!_liveEnabled) return;
            EnsureTimer();
            _timer.Stop();
            _timer.Interval = 650;
            _timer.Start();
            Push(new { type = "dirty", reason = reason ?? string.Empty, documentId = QuantityDashboardService.GetDocumentId(document) });
        }

        private static void RaiseDirtyMarked(Document document, string reason,
            IEnumerable<string> handles)
        {
            EventHandler<QuantityDashboardDirtyEventArgs> handler = DirtyMarked;
            if (handler == null) return;
            var args = new QuantityDashboardDirtyEventArgs(document,
                reason ?? string.Empty, handles);
            foreach (Delegate callback in handler.GetInvocationList())
                try
                {
                    ((EventHandler<QuantityDashboardDirtyEventArgs>)callback)(
                        null, args);
                }
                catch { }
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
            DirtyHandles.Clear();
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
            if (_changeTrackingSuppressionDepth > 0) return;
            if (RememberHandle(e == null ? null : e.DBObject)) _dirty = true;
        }

        private static void OnObjectErased(object sender, ObjectErasedEventArgs e)
        {
            if (_changeTrackingSuppressionDepth > 0) return;
            if (RememberHandle(e == null ? null : e.DBObject)) _dirty = true;
        }

        private sealed class ChangeTrackingSuppression : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_changeTrackingSuppressionDepth > 0)
                    _changeTrackingSuppressionDepth--;
            }
        }

        private static bool RememberHandle(DBObject value)
        {
            // QSAVE/SAVE updates dictionaries, symbol tables and other database
            // records even when drawing entities did not change. Quantity and
            // sync state only depends on graphical entities, so those records
            // must not mark the drawing dirty.
            if (!(value is Entity)) return false;
            try
            {
                string handle = value.Handle.ToString();
                if (!string.IsNullOrWhiteSpace(handle))
                {
                    DirtyHandles.Add(handle.Trim());
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static void OnCommandEnded(object sender, CommandEventArgs e)
        {
            if (!_dirty) return;
            string commandName = e == null ? string.Empty :
                (e.GlobalCommandName ?? string.Empty).Trim();
            if (IsSaveCommand(commandName))
            {
                // A save can report entities as modified because their filing
                // state changed. The actual edit command, if any, has already
                // emitted its own dirty event before QSAVE begins.
                _dirty = false;
                DirtyHandles.Clear();
                return;
            }
            if (DirtyHandles.Count == 0)
            {
                _dirty = false;
                return;
            }
            MarkDirty(string.IsNullOrWhiteSpace(commandName)
                ? "commandEnded" : commandName);
        }

        private static bool IsSaveCommand(string commandName)
        {
            string value = (commandName ?? string.Empty).Trim()
                .TrimStart('_', '.').ToUpperInvariant();
            return value == "SAVE" || value == "QSAVE" ||
                value == "SAVEAS" || value == "SAVEALL";
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

    public sealed class QuantityDashboardDirtyEventArgs : EventArgs
    {
        public QuantityDashboardDirtyEventArgs(Document document,
            string reason, IEnumerable<string> objectHandles)
        {
            Document = document;
            Reason = reason ?? string.Empty;
            ObjectHandles = (objectHandles ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()).Distinct(
                    StringComparer.OrdinalIgnoreCase).ToList();
        }

        public Document Document { get; private set; }
        public string Reason { get; private set; }
        public List<string> ObjectHandles { get; private set; }
    }
}
