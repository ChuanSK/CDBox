using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioStateStore
    {
        public static string StateFilePath
        {
            get { return Path.Combine(CDBoxStudioLogger.LogDirectory, "studio-state.xml"); }
        }

        public static CDBoxStudioState Load()
        {
            var state = new CDBoxStudioState();

            try
            {
                if (!File.Exists(StateFilePath)) return state;

                XDocument doc = XDocument.Load(StateFilePath);
                XElement root = doc.Root;
                if (root == null) return state;

                XElement favorites = root.Element("Favorites");
                if (favorites != null)
                {
                    foreach (XElement item in favorites.Elements("Item"))
                    {
                        string id = (string)item.Attribute("Id");
                        if (!string.IsNullOrWhiteSpace(id)) state.FavoriteIds.Add(id.Trim());
                    }
                }

                XElement recent = root.Element("Recent");
                if (recent != null)
                {
                    foreach (XElement item in recent.Elements("Item"))
                    {
                        string id = (string)item.Attribute("Id");
                        if (string.IsNullOrWhiteSpace(id)) continue;

                        int useCount = 0;
                        DateTime lastUsedUtc = DateTime.MinValue;
                        int.TryParse((string)item.Attribute("UseCount"), out useCount);
                        DateTime.TryParse((string)item.Attribute("LastUsedUtc"), out lastUsedUtc);

                        state.RecentItems.Add(new CDBoxStudioRecentItem
                        {
                            Id = id.Trim(),
                            UseCount = Math.Max(1, useCount),
                            LastUsedUtc = lastUsedUtc == DateTime.MinValue ? DateTime.UtcNow : lastUsedUtc.ToUniversalTime()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("Studio 状态读取失败，已使用空状态。", ex);
                return new CDBoxStudioState();
            }

            return state;
        }

        public static void Save(CDBoxStudioState state)
        {
            if (state == null) return;

            try
            {
                CDBoxStudioLogger.EnsureLogDirectory();

                var root = new XElement("CDBoxStudioState",
                    new XAttribute("Version", "1"),
                    new XElement("Favorites",
                        state.FavoriteIds
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                            .Select(x => new XElement("Item", new XAttribute("Id", x)))),
                    new XElement("Recent",
                        state.RecentItems
                            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
                            .OrderByDescending(x => x.LastUsedUtc)
                            .Select(x => new XElement("Item",
                                new XAttribute("Id", x.Id),
                                new XAttribute("UseCount", x.UseCount),
                                new XAttribute("LastUsedUtc", x.LastUsedUtc.ToUniversalTime().ToString("o"))))));

                new XDocument(root).Save(StateFilePath);
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("Studio 状态保存失败。", ex);
            }
        }
    }
}
