using System;
using System.IO;
using System.Text;

namespace CDBoxUpdater
{
    internal static class UpdaterLogger
    {
        private static readonly object SyncRoot = new object();
        private static string _logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CDBox",
            "Studio",
            "updater.log");

        public static string LogPath { get { return _logPath; } }

        public static void Configure(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            _logPath = Path.GetFullPath(path);
        }

        public static void Info(string message) { Write("INFO", message, null); }
        public static void Warn(string message) { Write("WARN", message, null); }
        public static void Error(string message, Exception ex) { Write("ERROR", message, ex); }

        private static void Write(string level, string message, Exception ex)
        {
            try
            {
                lock (SyncRoot)
                {
                    string directory = Path.GetDirectoryName(_logPath);
                    if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

                    var sb = new StringBuilder();
                    sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    sb.Append(" [").Append(level).Append("] ");
                    sb.Append(message ?? string.Empty);
                    if (ex != null)
                    {
                        sb.AppendLine();
                        sb.Append(ex);
                    }
                    sb.AppendLine();
                    File.AppendAllText(_logPath, sb.ToString(), new UTF8Encoding(false));
                }
            }
            catch
            {
            }
        }
    }
}
