using System;
using System.Threading;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace TCPipeAutoDraw.UI
{
    /// <summary>
    /// Gives WPF's render queue a short, throttled opportunity to paint while
    /// an AutoCAD command must continue to use the CAD main thread.
    ///
    /// Only render-or-higher dispatcher work is processed.  CAD input is not
    /// pumped here, which avoids re-entering commands while a transaction is
    /// active.
    /// </summary>
    internal static class CDBoxUiResponsiveness
    {
        private const int MinimumRenderIntervalMilliseconds = 45;
        private static Dispatcher _dispatcher;
        private static int _lastRenderTick;

        [ThreadStatic]
        private static bool _isRendering;

        public static void Initialize(Dispatcher dispatcher)
        {
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;
            Interlocked.CompareExchange(ref _dispatcher, dispatcher, null);
        }

        public static void YieldToRender(Document document = null,
            bool force = false)
        {
            Dispatcher dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;

            if (!dispatcher.CheckAccess())
            {
                try
                {
                    dispatcher.BeginInvoke(new Action(delegate
                    {
                        YieldToRender(document, force);
                    }), DispatcherPriority.Render);
                }
                catch { }
                return;
            }

            if (_isRendering) return;
            int now = Environment.TickCount;
            int previous = Volatile.Read(ref _lastRenderTick);
            if (!force && (uint)(now - previous) <
                MinimumRenderIntervalMilliseconds) return;
            Interlocked.Exchange(ref _lastRenderTick, now);

            _isRendering = true;
            try
            {
                // Invoking at Render priority drains already queued layout,
                // data-binding and presentation work without processing the
                // lower-priority WPF input queue.
                dispatcher.Invoke(DispatcherPriority.Render,
                    new Action(delegate { }));
                try { AcadApp.UpdateScreen(); }
                catch { }
            }
            catch
            {
                // Progress feedback must never interrupt the CAD operation.
            }
            finally
            {
                _isRendering = false;
            }
        }
    }
}
