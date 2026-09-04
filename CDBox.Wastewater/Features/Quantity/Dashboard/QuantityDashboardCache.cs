using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    public static class QuantityDashboardCache
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 128 };

        public static string CacheDirectory
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CDBox", "Studio", "QuantityDashboard", "Cache");
            }
        }

        public static string SnapshotDirectory
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CDBox", "Studio", "QuantityDashboard", "Snapshots");
            }
        }

        public static QuantityDashboardSnapshot Load(string cacheKey)
        {
            try
            {
                string path = GetCacheFile(cacheKey);
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path, Encoding.UTF8);
                QuantityDashboardSnapshot snapshot = Serializer.Deserialize<QuantityDashboardSnapshot>(json);
                if (snapshot == null || !string.Equals(snapshot.cacheKey, cacheKey, StringComparison.OrdinalIgnoreCase)) return null;
                return snapshot;
            }
            catch
            {
                return null;
            }
        }

        public static void Save(QuantityDashboardSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.cacheKey)) return;
            Directory.CreateDirectory(CacheDirectory);
            string path = GetCacheFile(snapshot.cacheKey);
            string temp = path + ".tmp";
            File.WriteAllText(temp, Serializer.Serialize(snapshot), new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        public static string SaveNamedSnapshot(QuantityDashboardSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            Directory.CreateDirectory(SnapshotDirectory);
            string drawing = SanitizeFileName(snapshot.document == null ? "图纸" : snapshot.document.name);
            string scope = SanitizeFileName(snapshot.scope == null ? "整张图纸" : snapshot.scope.regionName);
            string fileName = drawing + "_" + scope + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";
            string path = Path.Combine(SnapshotDirectory, fileName);
            File.WriteAllText(path, Serializer.Serialize(snapshot), new UTF8Encoding(false));
            return path;
        }

        private static string GetCacheFile(string cacheKey)
        {
            Directory.CreateDirectory(CacheDirectory);
            return Path.Combine(CacheDirectory, Hash(cacheKey ?? string.Empty) + ".json");
        }

        private static string Hash(string text)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        private static string SanitizeFileName(string value)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "工程量" : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) text = text.Replace(c, '_');
            return text;
        }
    }
}
