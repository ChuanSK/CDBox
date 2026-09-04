using System;
using CDBox.Shared.Modules;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace TCPipeAutoDraw.Core.Modules
{
    internal sealed class QuantityDashboardMonitorAdapter
        : IQuantityDashboardMonitorService
    {
        public void Configure(Action<string> scriptSink)
        {
            QuantityDashboardLiveMonitor.Configure(scriptSink);
        }

        public void SetLiveEnabled(bool enabled)
        {
            QuantityDashboardLiveMonitor.SetLiveEnabled(enabled);
        }

        public void MarkDirty(string reason)
        {
            QuantityDashboardLiveMonitor.MarkDirty(reason);
        }
    }
}
