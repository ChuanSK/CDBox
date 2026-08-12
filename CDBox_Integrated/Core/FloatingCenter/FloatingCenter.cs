using System;

namespace TCPipeAutoDraw.Core.FloatingCenter
{
    public static class FloatingCenter
    {
        private static readonly object Gate = new object();
        private static IFloatingCenter _current =
            new FloatingCenterService();

        public static IFloatingCenter Current
        {
            get
            {
                lock (Gate) return _current;
            }
        }

        public static void Configure(IFloatingCenter service)
        {
            if (service == null) throw new ArgumentNullException("service");
            lock (Gate) _current = service;
        }

        public static void Reset()
        {
            lock (Gate) _current = new FloatingCenterService();
        }
    }
}
