using System;
using System.IO;
using System.Xml.Linq;

namespace TCPipeAutoDraw.Modules.ShortCodeRecognition
{
    public sealed class ShortCodeRecognitionSettings
    {
        public ShortCodeRecognitionSettings()
        {
            RecognitionSymbol = "+";
            ConnectPreviousPoint = true;
            ConnectNextPoint = false;
            AutoClose = false;
        }

        public string RecognitionSymbol { get; set; }
        public bool ConnectPreviousPoint { get; set; }
        public bool ConnectNextPoint { get; set; }
        public bool AutoClose { get; set; }

        public void Normalize()
        {
            string symbol = (RecognitionSymbol ?? string.Empty)
                .Replace("\0", string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Trim();
            if (symbol.Length == 0) symbol = "+";
            if (symbol.Length > 16)
            {
                symbol = symbol.Substring(0, 16);
                if (symbol.Length > 0
                    && char.IsHighSurrogate(symbol[symbol.Length - 1]))
                    symbol = symbol.Substring(0, symbol.Length - 1);
            }
            RecognitionSymbol = symbol;
        }
    }

    public static class ShortCodeRecognitionSettingsStore
    {
        private static readonly object Gate = new object();

        public static string SettingsPath
        {
            get
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "CDBox");
                return Path.Combine(directory,
                    "ShortCodeRecognitionSettings.xml");
            }
        }

        public static ShortCodeRecognitionSettings Load()
        {
            lock (Gate)
            {
                ShortCodeRecognitionSettings settings =
                    new ShortCodeRecognitionSettings();
                try
                {
                    if (!File.Exists(SettingsPath)) return settings;
                    XElement root = XDocument.Load(SettingsPath).Root;
                    if (root == null) return settings;
                    settings.RecognitionSymbol = Text(root,
                        "RecognitionSymbol", settings.RecognitionSymbol);
                    bool legacyAdjacent = Bool(root,
                        "ConnectAdjacentPoints", false);
                    settings.ConnectPreviousPoint = Bool(root,
                        "ConnectPreviousPoint",
                        legacyAdjacent || settings.ConnectPreviousPoint);
                    settings.ConnectNextPoint = Bool(root,
                        "ConnectNextPoint", legacyAdjacent);
                    settings.AutoClose = Bool(root, "AutoClose",
                        settings.AutoClose);
                }
                catch
                {
                    // 配置损坏时回到安全默认值，不影响命令加载。
                }
                settings.Normalize();
                return settings;
            }
        }

        public static void Save(ShortCodeRecognitionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            lock (Gate)
            {
                settings.Normalize();
                string directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                XElement root = new XElement("ShortCodeRecognitionSettings",
                    new XAttribute("Version", "1"),
                    new XElement("RecognitionSymbol",
                        settings.RecognitionSymbol),
                    new XElement("ConnectPreviousPoint",
                        settings.ConnectPreviousPoint),
                    new XElement("ConnectNextPoint",
                        settings.ConnectNextPoint),
                    new XElement("AutoClose", settings.AutoClose));
                new XDocument(root).Save(SettingsPath);
            }
        }

        private static string Text(XElement root, string name,
            string fallback)
        {
            XElement element = root.Element(name);
            return element == null ? fallback : (element.Value ?? fallback);
        }

        private static bool Bool(XElement root, string name, bool fallback)
        {
            XElement element = root.Element(name);
            bool value;
            return element != null
                && bool.TryParse(element.Value, out value)
                    ? value : fallback;
        }
    }
}
