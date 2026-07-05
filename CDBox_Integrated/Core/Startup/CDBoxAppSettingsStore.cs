using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TCPipeAutoDraw.Core.Startup
{
    internal static class CDBoxAppSettingsStore
    {
        private const string FolderName = "CDBox";
        private const string FileName = "CDBoxApp.settings";

        public static CDBoxAppSettings Load()
        {
            CDBoxAppSettings settings = CDBoxAppSettings.Default;

            try
            {
                string path = GetSettingsPath();
                if (!File.Exists(path)) return settings;

                Dictionary<string, string> values = ReadKeyValues(path);
                bool boolValue;
                string text;

                if (TryGetBool(values, "PromptInstallOnLoad", out boolValue)) settings.PromptInstallOnLoad = boolValue;
                if (TryGetBool(values, "PromptSidebarOnLoad", out boolValue)) settings.PromptSidebarOnLoad = boolValue;
                if (TryGetBool(values, "AutoShowSidebarOnLoad", out boolValue)) settings.AutoShowSidebarOnLoad = boolValue;
                if (TryGetString(values, "InstalledPath", out text)) settings.InstalledPath = text ?? string.Empty;
            }
            catch
            {
                // 设置读取失败时保持默认值，不能影响插件加载。
            }

            return settings;
        }

        public static void Save(CDBoxAppSettings settings)
        {
            if (settings == null) return;

            try
            {
                string path = GetSettingsPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var lines = new List<string>();
                Add(lines, "PromptInstallOnLoad", settings.PromptInstallOnLoad);
                Add(lines, "PromptSidebarOnLoad", settings.PromptSidebarOnLoad);
                Add(lines, "AutoShowSidebarOnLoad", settings.AutoShowSidebarOnLoad);
                Add(lines, "InstalledPath", settings.InstalledPath ?? string.Empty);

                File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(true));
            }
            catch
            {
                // 设置保存失败不能影响当前命令。
            }
        }

        public static string GetSettingsPath()
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(baseDir)) baseDir = Path.GetTempPath();
            return Path.Combine(Path.Combine(baseDir, FolderName), FileName);
        }

        private static void Add(List<string> lines, string key, bool value)
        {
            Add(lines, key, value ? "true" : "false");
        }

        private static void Add(List<string> lines, string key, string value)
        {
            lines.Add(key + "=" + Uri.EscapeDataString(value ?? string.Empty));
        }

        private static Dictionary<string, string> ReadKeyValues(string path)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            foreach (string raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                int index = raw.IndexOf('=');
                if (index <= 0) continue;

                string key = raw.Substring(0, index).Trim();
                string value = raw.Substring(index + 1);
                if (string.IsNullOrWhiteSpace(key)) continue;

                try { dict[key] = Uri.UnescapeDataString(value ?? string.Empty); }
                catch { dict[key] = value ?? string.Empty; }
            }
            return dict;
        }

        private static bool TryGetString(Dictionary<string, string> values, string key, out string value)
        {
            value = string.Empty;
            if (values == null || string.IsNullOrWhiteSpace(key)) return false;
            return values.TryGetValue(key, out value);
        }

        private static bool TryGetBool(Dictionary<string, string> values, string key, out bool value)
        {
            value = false;
            string text;
            return TryGetString(values, key, out text) && bool.TryParse(text, out value);
        }
    }
}
