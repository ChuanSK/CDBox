using System;
using System.IO;
using System.Xml.Linq;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioSettingsStore
    {
        public static string SettingsFilePath
        {
            get { return Path.Combine(CDBoxStudioLogger.LogDirectory, "studio-settings.xml"); }
        }

        public static CDBoxStudioSettings Load()
        {
            var settings = new CDBoxStudioSettings();

            try
            {
                if (!File.Exists(SettingsFilePath)) return settings;

                XDocument doc = XDocument.Load(SettingsFilePath);
                XElement root = doc.Root;
                if (root == null) return settings;

                settings.Theme = ((string)root.Element("Theme") ?? settings.Theme).Trim();
                settings.UpdateChannel = ((string)root.Element("UpdateChannel") ?? settings.UpdateChannel).Trim();
                settings.UpdateSourceUrl = ((string)root.Element("UpdateSourceUrl") ?? settings.UpdateSourceUrl).Trim();

                bool animations;
                if (bool.TryParse((string)root.Element("AnimationsEnabled"), out animations)) settings.AnimationsEnabled = animations;


                settings.Normalize();
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("Studio 设置读取失败，已使用默认设置。", ex);
                return new CDBoxStudioSettings();
            }

            return settings;
        }

        public static void Save(CDBoxStudioSettings settings)
        {
            if (settings == null) return;

            try
            {
                settings.Normalize();
                CDBoxStudioLogger.EnsureLogDirectory();

                var root = new XElement("CDBoxStudioSettings",
                    new XAttribute("Version", "1"),
                    new XElement("Theme", settings.Theme),
                    new XElement("AnimationsEnabled", settings.AnimationsEnabled),
                    new XElement("UpdateChannel", settings.UpdateChannel),
                    new XElement("UpdateSourceUrl", settings.UpdateSourceUrl));

                new XDocument(root).Save(SettingsFilePath);
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("Studio 设置保存失败。", ex);
            }
        }
    }
}
