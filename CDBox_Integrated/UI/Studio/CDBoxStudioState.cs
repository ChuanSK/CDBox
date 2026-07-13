using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioRecentItem
    {
        public string Id { get; set; }
        public int UseCount { get; set; }
        public DateTime LastUsedUtc { get; set; }
    }

    internal sealed class CDBoxStudioState
    {
        private const int MaxRecentCount = 24;

        public CDBoxStudioState()
        {
            FavoriteIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RecentItems = new List<CDBoxStudioRecentItem>();
        }

        public HashSet<string> FavoriteIds { get; private set; }
        public List<CDBoxStudioRecentItem> RecentItems { get; private set; }

        public bool IsFavorite(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && FavoriteIds.Contains(id);
        }

        public bool HasRecent(string id)
        {
            return GetRecent(id) != null;
        }

        public CDBoxStudioRecentItem GetRecent(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return RecentItems.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public int GetRecentRank(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return 9999;

            List<CDBoxStudioRecentItem> ordered = RecentItems
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .OrderByDescending(x => x.LastUsedUtc)
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                if (string.Equals(ordered[i].Id, id, StringComparison.OrdinalIgnoreCase)) return i + 1;
            }

            return 9999;
        }

        public bool ToggleFavorite(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            if (FavoriteIds.Contains(id))
            {
                FavoriteIds.Remove(id);
                return false;
            }

            FavoriteIds.Add(id);
            return true;
        }

        public bool RemoveFavorite(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && FavoriteIds.Remove(id);
        }

        public void ClearRecent()
        {
            RecentItems.Clear();
        }

        public void MarkRecent(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            CDBoxStudioRecentItem item = GetRecent(id);
            if (item == null)
            {
                item = new CDBoxStudioRecentItem { Id = id.Trim(), UseCount = 0 };
                RecentItems.Add(item);
            }

            item.UseCount++;
            item.LastUsedUtc = DateTime.UtcNow;
            TrimRecent();
        }

        public void RemoveMissingActions(IEnumerable<string> currentIds)
        {
            if (currentIds == null) return;

            var idSet = new HashSet<string>(currentIds.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
            FavoriteIds.RemoveWhere(x => !idSet.Contains(x));
            RecentItems.RemoveAll(x => x == null || string.IsNullOrWhiteSpace(x.Id) || !idSet.Contains(x.Id));
        }

        private void TrimRecent()
        {
            RecentItems = RecentItems
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
                .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.LastUsedUtc).First())
                .OrderByDescending(x => x.LastUsedUtc)
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .Take(MaxRecentCount)
                .ToList();
        }
    }
}
