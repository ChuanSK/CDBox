using System;
using CDBox.Shared.Wastewater.Drafting;
using CDBox.Wastewater.Module;

namespace TCPipeAutoDraw.Modules.SectionDrawing
{
    public static class SectionDrawingScaleService
    {
        public static SectionDrawingOptions CreateScaledOptions(
            SectionDrawingOptions source)
        {
            return WastewaterSectionDrawingRegistry.GetRequired()
                .CreateScaledOptions(source);
        }
    }
}

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    internal static class QuantityDashboardLiveMonitor
    {
        public static IDisposable SuspendChangeTracking()
        {
            return EmptyDisposable.Instance;
        }

        public static void MarkDirty(object document, string reason)
        {
            if (WastewaterRuntimeServices.DashboardMonitor != null)
                WastewaterRuntimeServices.DashboardMonitor.MarkDirty(reason);
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance =
                new EmptyDisposable();
            public void Dispose() { }
        }
    }
}
