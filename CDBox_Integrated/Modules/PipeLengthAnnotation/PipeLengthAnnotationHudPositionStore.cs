using System;
using System.Globalization;
using System.IO;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    /// <summary>
    /// Persists the HUD's last user-selected screen position independently from DWG data.
    /// </summary>
    internal static class PipeLengthAnnotationHudPositionStore
    {
        private const string FolderName = "CDBox";
        private const string FileName = "PipeLengthAnnotationHud.position";

        public static bool TryLoad(out double left, out double top)
        {
            left = 0.0;
            top = 0.0;
            try
            {
                string path = GetPath();
                if (!File.Exists(path)) return false;
                string[] lines = File.ReadAllLines(path);
                if (lines.Length < 2) return false;
                return double.TryParse(lines[0], NumberStyles.Float, CultureInfo.InvariantCulture, out left)
                    && double.TryParse(lines[1], NumberStyles.Float, CultureInfo.InvariantCulture, out top)
                    && !double.IsNaN(left) && !double.IsInfinity(left)
                    && !double.IsNaN(top) && !double.IsInfinity(top);
            }
            catch { return false; }
        }

        public static void Save(double left, double top)
        {
            if (double.IsNaN(left) || double.IsInfinity(left)
                || double.IsNaN(top) || double.IsInfinity(top)) return;
            try
            {
                string path = GetPath();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllLines(path, new[]
                {
                    left.ToString("R", CultureInfo.InvariantCulture),
                    top.ToString("R", CultureInfo.InvariantCulture)
                });
            }
            catch { }
        }

        private static string GetPath()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
            return Path.Combine(Path.Combine(root, FolderName), FileName);
        }
    }
}
