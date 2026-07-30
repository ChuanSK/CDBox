using System;
using System.Globalization;
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
                if (!File.Exists(SettingsFilePath))
                {
                    TCPipeAutoDraw.Core.Colors.CDBoxColorService.OutputMode = settings.ColorOutputMode;
                    return settings;
                }

                XDocument doc = XDocument.Load(SettingsFilePath);
                XElement root = doc.Root;
                if (root == null)
                {
                    TCPipeAutoDraw.Core.Colors.CDBoxColorService.OutputMode = settings.ColorOutputMode;
                    return settings;
                }

                settings.Theme = ((string)root.Element("Theme") ?? settings.Theme).Trim();
                settings.UpdateChannel = ((string)root.Element("UpdateChannel") ?? settings.UpdateChannel).Trim();
                TCPipeAutoDraw.Core.Colors.CDBoxColorOutputMode colorOutputMode;
                if (Enum.TryParse((string)root.Element("ColorOutputMode"), true, out colorOutputMode))
                    settings.ColorOutputMode = colorOutputMode;

                bool animations;
                if (bool.TryParse((string)root.Element("AnimationsEnabled"), out animations)) settings.AnimationsEnabled = animations;
                settings.AnnotationHudNormalOpacity = ReadDouble(root,
                    "AnnotationHudNormalOpacity", settings.AnnotationHudNormalOpacity);
                settings.AnnotationHudHoverOpacity = ReadDouble(root,
                    "AnnotationHudHoverOpacity", settings.AnnotationHudHoverOpacity);
                settings.AnnotationHudGlowIntensity = ReadDouble(root,
                    "AnnotationHudGlowIntensity", settings.AnnotationHudGlowIntensity);
                bool glowEnabled;
                if (bool.TryParse((string)root.Element("AnnotationHudGlowEnabled"), out glowEnabled))
                    settings.AnnotationHudGlowEnabled = glowEnabled;
                bool doubleClickOpenEnabled;
                if (bool.TryParse((string)root.Element("DoubleClickOpenEnabled"),
                    out doubleClickOpenEnabled))
                    settings.DoubleClickOpenEnabled = doubleClickOpenEnabled;

                settings.Normalize();
                TCPipeAutoDraw.Core.Colors.CDBoxColorService.OutputMode = settings.ColorOutputMode;
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("Studio 设置读取失败，已使用默认设置。", ex);
                var fallback = new CDBoxStudioSettings();
                TCPipeAutoDraw.Core.Colors.CDBoxColorService.OutputMode = fallback.ColorOutputMode;
                return fallback;
            }

            return settings;
        }

        public static void Save(CDBoxStudioSettings settings)
        {
            if (settings == null) return;

            try
            {
                settings.Normalize();
                TCPipeAutoDraw.Core.Colors.CDBoxColorService.OutputMode = settings.ColorOutputMode;
                CDBoxStudioLogger.EnsureLogDirectory();

                var root = new XElement("CDBoxStudioSettings",
                    new XAttribute("Version", "5"),
                    new XElement("Theme", settings.Theme),
                    new XElement("AnimationsEnabled", settings.AnimationsEnabled),
                    new XElement("AnnotationHudNormalOpacity",
                        settings.AnnotationHudNormalOpacity.ToString("0.##", CultureInfo.InvariantCulture)),
                    new XElement("AnnotationHudHoverOpacity",
                        settings.AnnotationHudHoverOpacity.ToString("0.##", CultureInfo.InvariantCulture)),
                    new XElement("AnnotationHudGlowEnabled", settings.AnnotationHudGlowEnabled),
                    new XElement("AnnotationHudGlowIntensity",
                        settings.AnnotationHudGlowIntensity.ToString("0.##", CultureInfo.InvariantCulture)),
                    new XElement("DoubleClickOpenEnabled", settings.DoubleClickOpenEnabled),
                    new XElement("ColorOutputMode", settings.ColorOutputMode.ToString()),
                    new XElement("UpdateChannel", settings.UpdateChannel));

                new XDocument(root).Save(SettingsFilePath);
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("Studio 设置保存失败。", ex);
            }
        }

        private static double ReadDouble(XElement root, string name, double fallback)
        {
            double value;
            return double.TryParse((string)root.Element(name), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) ? value : fallback;
        }
    }
}
