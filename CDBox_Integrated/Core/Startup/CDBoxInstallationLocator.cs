using System;
using System.Diagnostics;
using System.IO;

namespace TCPipeAutoDraw.Core.Startup
{
    internal static class CDBoxInstallationLocator
    {
        private const string BundleFolderName = "CDBox.bundle";

        public static string GetCadDirectory()
        {
            try
            {
                string executable = Process.GetCurrentProcess()
                    .MainModule.FileName;
                string directory = Path.GetDirectoryName(executable);
                if (!string.IsNullOrWhiteSpace(directory))
                    return directory;
            }
            catch { }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string GetCadExecutablePath()
        {
            return Path.Combine(GetCadDirectory(), "acad.exe");
        }

        public static string GetInstallRoot()
        {
            return Path.Combine(GetCadDirectory(), BundleFolderName);
        }

        public static bool IsRunningFromInstallFolder()
        {
            try
            {
                string assemblyPath = typeof(CDBoxInstallationLocator)
                    .Assembly.Location;
                string root = Path.GetFullPath(GetInstallRoot())
                    .TrimEnd(Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                return Path.GetFullPath(assemblyPath).StartsWith(root,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

    }
}
