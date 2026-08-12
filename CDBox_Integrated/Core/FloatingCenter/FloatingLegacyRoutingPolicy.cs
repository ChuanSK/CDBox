using System;

namespace TCPipeAutoDraw.Core.FloatingCenter
{
    public static class FloatingLegacyRoutingPolicy
    {
        public static bool RequiresSynchronousDecision(string buttonSet)
        {
            return !string.Equals((buttonSet ?? string.Empty).Trim(), "OK",
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool AllowsIndependentProgressWindow
        {
            get { return false; }
        }
    }
}
