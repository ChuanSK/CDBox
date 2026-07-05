using System;
using System.IO;

namespace TCPipeAutoDraw.Core.Startup
{
    internal static class CDBoxInstallLogger
    {
        private static readonly object SyncRoot = new object();

        public static string LogDirectory
        {
            get { return Path.Combine(GetAppDataRoot(), "Studio"); }
        }

        public static string LogFilePath
        {
            get { return Path.Combine(LogDirectory, "studio.log"); }
        }

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Warn(string message)
        {
            Write("WARN", message, null);
        }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR", message, ex);
        }

        private static void Write(string level, string message, Exception ex)
        {
            try
            {
                lock (SyncRoot)
                {
                    Directory.CreateDirectory(LogDirectory);
                    using (var writer = new StreamWriter(LogFilePath, true, System.Text.Encoding.UTF8))
                    {
                        writer.Write(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                        writer.Write(" [INSTALL-");
                        writer.Write(level ?? "INFO");
                        writer.Write("] ");
                        writer.WriteLine(message ?? string.Empty);

                        if (ex != null)
                        {
                            writer.WriteLine(ex.GetType().FullName + ": " + ex.Message);
                            writer.WriteLine(ex.StackTrace ?? string.Empty);
                        }
                    }
                }
            }
            catch
            {
                // 安装日志不能影响安装/更新主流程。
            }
        }

        private static string GetAppDataRoot()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData)) appData = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appData, "CDBox");
        }
    }
}
