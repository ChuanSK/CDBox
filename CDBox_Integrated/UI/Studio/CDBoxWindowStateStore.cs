using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxWindowStateStore
    {
        private static readonly object SyncRoot = new object();

        public static string StateFilePath
        {
            get { return Path.Combine(CDBoxStudioLogger.LogDirectory, "window-states.xml"); }
        }

        public static void Restore(Form form, string key)
        {
            if (form == null || string.IsNullOrWhiteSpace(key)) return;
            try
            {
                WindowStateEntry entry;
                lock (SyncRoot)
                {
                    Dictionary<string, WindowStateEntry> entries = LoadEntries();
                    if (!entries.TryGetValue(key.Trim(), out entry)) return;
                }

                Rectangle bounds = new Rectangle(entry.X, entry.Y, entry.Width, entry.Height);
                Rectangle workingArea = FindWorkingArea(bounds);
                if (workingArea == Rectangle.Empty) return;

                int width = Math.Min(Math.Max(bounds.Width, form.MinimumSize.Width), workingArea.Width);
                int height = Math.Min(Math.Max(bounds.Height, form.MinimumSize.Height), workingArea.Height);
                int x = Math.Max(workingArea.Left, Math.Min(bounds.X, workingArea.Right - width));
                int y = Math.Max(workingArea.Top, Math.Min(bounds.Y, workingArea.Bottom - height));
                form.StartPosition = FormStartPosition.Manual;
                form.Bounds = new Rectangle(x, y, width, height);
                if (entry.Maximized) form.WindowState = FormWindowState.Maximized;
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Warn("窗口状态读取失败：" + ex.Message);
            }
        }

        public static void Save(Form form, string key)
        {
            if (form == null || string.IsNullOrWhiteSpace(key)) return;
            try
            {
                Rectangle bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
                if (bounds.Width <= 0 || bounds.Height <= 0) return;

                lock (SyncRoot)
                {
                    Dictionary<string, WindowStateEntry> entries = LoadEntries();
                    entries[key.Trim()] = new WindowStateEntry
                    {
                        X = bounds.X,
                        Y = bounds.Y,
                        Width = bounds.Width,
                        Height = bounds.Height,
                        Maximized = form.WindowState == FormWindowState.Maximized
                    };
                    SaveEntries(entries);
                }
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Warn("窗口状态保存失败：" + ex.Message);
            }
        }

        private static Rectangle FindWorkingArea(Rectangle bounds)
        {
            Rectangle best = Rectangle.Empty;
            int bestArea = 0;
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle intersection = Rectangle.Intersect(bounds, screen.WorkingArea);
                int area = Math.Max(0, intersection.Width) * Math.Max(0, intersection.Height);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = screen.WorkingArea;
                }
            }
            return bestArea > 0 ? best : (Screen.PrimaryScreen == null ? Rectangle.Empty : Screen.PrimaryScreen.WorkingArea);
        }

        private static Dictionary<string, WindowStateEntry> LoadEntries()
        {
            var entries = new Dictionary<string, WindowStateEntry>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(StateFilePath)) return entries;

            XDocument document = XDocument.Load(StateFilePath);
            XElement root = document.Root;
            if (root == null) return entries;
            foreach (XElement item in root.Elements("Window"))
            {
                string key = (string)item.Attribute("Key");
                if (string.IsNullOrWhiteSpace(key)) continue;
                int x, y, width, height;
                bool maximized;
                if (!int.TryParse((string)item.Attribute("X"), out x)
                    || !int.TryParse((string)item.Attribute("Y"), out y)
                    || !int.TryParse((string)item.Attribute("Width"), out width)
                    || !int.TryParse((string)item.Attribute("Height"), out height)) continue;
                bool.TryParse((string)item.Attribute("Maximized"), out maximized);
                entries[key.Trim()] = new WindowStateEntry { X = x, Y = y, Width = width, Height = height, Maximized = maximized };
            }
            return entries;
        }

        private static void SaveEntries(Dictionary<string, WindowStateEntry> entries)
        {
            CDBoxStudioLogger.EnsureLogDirectory();
            var root = new XElement("CDBoxWindowStates", new XAttribute("Version", "1"),
                entries.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(pair =>
                    new XElement("Window",
                        new XAttribute("Key", pair.Key),
                        new XAttribute("X", pair.Value.X),
                        new XAttribute("Y", pair.Value.Y),
                        new XAttribute("Width", pair.Value.Width),
                        new XAttribute("Height", pair.Value.Height),
                        new XAttribute("Maximized", pair.Value.Maximized))));
            string tempPath = StateFilePath + ".tmp";
            new XDocument(root).Save(tempPath);
            if (File.Exists(StateFilePath))
            {
                string backupPath = StateFilePath + ".bak";
                File.Replace(tempPath, StateFilePath, backupPath, true);
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }
            else
            {
                File.Move(tempPath, StateFilePath);
            }
        }

        private sealed class WindowStateEntry
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public bool Maximized;
        }
    }
}
