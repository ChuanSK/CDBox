using System;
using System.IO;

namespace TCPipeAutoDraw.Core.Business
{
    internal enum CDBoxBusinessMode
    {
        Wastewater,
        RealEstate
    }

    internal sealed class CDBoxBusinessModeChangedEventArgs : EventArgs
    {
        public CDBoxBusinessModeChangedEventArgs(CDBoxBusinessMode mode)
        {
            Mode = mode;
        }

        public CDBoxBusinessMode Mode { get; private set; }
    }

    internal static class CDBoxBusinessModeService
    {
        private static readonly object Gate = new object();
        private static CDBoxBusinessMode _mode = CDBoxBusinessMode.Wastewater;
        private static bool _initialized;

        public static event EventHandler<CDBoxBusinessModeChangedEventArgs>
            Changed;

        public static CDBoxBusinessMode Current
        {
            get { lock (Gate) return _mode; }
        }

        public static bool IsWastewater
        {
            get { return Current == CDBoxBusinessMode.Wastewater; }
        }

        public static void Initialize()
        {
            lock (Gate)
            {
                if (_initialized) return;
                _mode = Parse(ReadPersisted());
                _initialized = true;
            }
        }

        public static void SetMode(CDBoxBusinessMode mode)
        {
            if (!Enum.IsDefined(typeof(CDBoxBusinessMode), mode))
                mode = CDBoxBusinessMode.Wastewater;
            bool changed;
            lock (Gate)
            {
                changed = _mode != mode;
                _mode = mode;
                _initialized = true;
            }
            Persist(mode);
            if (!changed) return;
            EventHandler<CDBoxBusinessModeChangedEventArgs> handler = Changed;
            if (handler == null) return;
            var args = new CDBoxBusinessModeChangedEventArgs(mode);
            foreach (Delegate callback in handler.GetInvocationList())
                try
                {
                    ((EventHandler<CDBoxBusinessModeChangedEventArgs>)callback)(
                        null, args);
                }
                catch { }
        }

        public static void Toggle()
        {
            SetMode(IsWastewater ? CDBoxBusinessMode.RealEstate
                : CDBoxBusinessMode.Wastewater);
        }

        public static string DisplayName(CDBoxBusinessMode mode)
        {
            return mode == CDBoxBusinessMode.RealEstate
                ? "不动产" : "污水管线";
        }

        internal static CDBoxBusinessMode Parse(string value)
        {
            return string.Equals((value ?? string.Empty).Trim(),
                "RealEstate", StringComparison.OrdinalIgnoreCase)
                || string.Equals((value ?? string.Empty).Trim(),
                    "不动产", StringComparison.OrdinalIgnoreCase)
                ? CDBoxBusinessMode.RealEstate
                : CDBoxBusinessMode.Wastewater;
        }

        private static string ReadPersisted()
        {
            try
            {
                string path = SettingsPath();
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
            catch { return string.Empty; }
        }

        private static void Persist(CDBoxBusinessMode mode)
        {
            try
            {
                string path = SettingsPath();
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, mode.ToString());
            }
            catch { }
        }

        private static string SettingsPath()
        {
            return Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), "CDBox",
                "business-mode.txt");
        }
    }
}
