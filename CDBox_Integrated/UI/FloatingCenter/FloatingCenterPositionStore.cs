using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using TCPipeAutoDraw.Core.FloatingCenter;

namespace TCPipeAutoDraw.UI.FloatingCenter
{
    internal static class FloatingCenterPositionStore
    {
        private static readonly object Gate = new object();

        private static string FilePath
        {
            get
            {
                string root = Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
                return Path.Combine(root, "CDBox", "FloatingCenter.position.xml");
            }
        }

        public static FloatingPositionState Load()
        {
            lock (Gate)
            {
                try
                {
                    if (!File.Exists(FilePath)) return null;
                    XElement root = XDocument.Load(FilePath).Root;
                    if (root == null) return null;
                    FloatingSnapEdge edge;
                    if (!Enum.TryParse((string)root.Element("SnapEdge"), true,
                        out edge)) edge = FloatingSnapEdge.None;
                    return new FloatingPositionState
                    {
                        MonitorDeviceName = ((string)root.Element("Monitor") ??
                            string.Empty).Trim(),
                        SnapEdge = edge,
                        EdgeOffset = Read(root, "EdgeOffset"),
                        Left = Read(root, "Left"),
                        Top = Read(root, "Top")
                    };
                }
                catch { return null; }
            }
        }

        public static void Save(FloatingPositionState state)
        {
            if (state == null || !IsFinite(state.Left) || !IsFinite(state.Top)
                || !IsFinite(state.EdgeOffset)) return;
            lock (Gate)
            {
                try
                {
                    string directory = Path.GetDirectoryName(FilePath);
                    if (!string.IsNullOrWhiteSpace(directory))
                        Directory.CreateDirectory(directory);
                    new XDocument(new XElement("FloatingCenterPosition",
                        new XAttribute("Version", "1"),
                        new XElement("Monitor", state.MonitorDeviceName ?? string.Empty),
                        new XElement("SnapEdge", state.SnapEdge),
                        new XElement("EdgeOffset", R(state.EdgeOffset)),
                        new XElement("Left", R(state.Left)),
                        new XElement("Top", R(state.Top)))).Save(FilePath);
                }
                catch { }
            }
        }

        public static void Reset()
        {
            lock (Gate)
            {
                try { if (File.Exists(FilePath)) File.Delete(FilePath); }
                catch { }
            }
        }

        private static double Read(XElement root, string name)
        {
            double value;
            return double.TryParse((string)root.Element(name), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) && IsFinite(value)
                ? value : 0.0;
        }

        private static string R(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
