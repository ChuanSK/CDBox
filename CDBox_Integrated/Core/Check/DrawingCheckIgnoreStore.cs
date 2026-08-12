using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TCPipeAutoDraw.Core.Check
{
    public static class DrawingCheckIgnoreStore
    {
        private static readonly object Gate = new object();

        public static HashSet<string> LoadKeys(string documentKey)
        {
            return new HashSet<string>(LoadIssues(documentKey, string.Empty)
                .Select(x => x.IgnoreKey).Where(x =>
                    !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);
        }

        public static List<DrawingCheckIssue> LoadIssues(string documentKey,
            string documentId)
        {
            string key = Normalize(documentKey);
            lock (Gate)
                return LoadAll().Where(x => Same(x.DocumentKey, key))
                    .Select(x => new DrawingCheckIssue
                    {
                        Id = x.IgnoreKey,
                        IgnoreKey = x.IgnoreKey,
                        DocumentId = documentId ?? string.Empty,
                        RuleId = x.RuleId,
                        Category = x.Category,
                        Severity = x.Severity,
                        Title = x.Title,
                        Message = x.Message,
                        Description = x.Message,
                        ObjectHandles = x.ObjectHandles == null
                            ? new List<string>() : new List<string>(
                                x.ObjectHandles),
                        CreatedAt = x.IgnoredAt,
                        UpdatedAt = x.IgnoredAt,
                        Status = DrawingCheckIssueStatus.Ignored
                    }).ToList();
        }

        public static void Add(string documentKey,
            IEnumerable<DrawingCheckIssue> issues)
        {
            string key = Normalize(documentKey);
            if (key.Length == 0) return;
            lock (Gate)
            {
                List<IgnoredIssueRecord> all = LoadAll();
                foreach (DrawingCheckIssue issue in issues ??
                    Enumerable.Empty<DrawingCheckIssue>())
                {
                    if (issue == null ||
                        string.IsNullOrWhiteSpace(issue.IgnoreKey)) continue;
                    all.RemoveAll(x => Same(x.DocumentKey, key) &&
                        Same(x.IgnoreKey, issue.IgnoreKey));
                    all.Add(new IgnoredIssueRecord
                    {
                        DocumentKey = key,
                        IgnoreKey = issue.IgnoreKey,
                        RuleId = issue.RuleId,
                        Category = issue.Category,
                        Severity = issue.Severity,
                        Title = issue.Title,
                        Message = issue.Message,
                        ObjectHandles = issue.ObjectHandles == null
                            ? new List<string>() : new List<string>(
                                issue.ObjectHandles),
                        IgnoredAt = DateTime.UtcNow
                    });
                }
                SaveAll(all);
            }
        }

        public static void Remove(string documentKey,
            IEnumerable<DrawingCheckIssue> issues)
        {
            string key = Normalize(documentKey);
            var remove = new HashSet<string>((issues ??
                Enumerable.Empty<DrawingCheckIssue>()).Where(x => x != null)
                    .Select(x => x.IgnoreKey).Where(x =>
                        !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);
            if (key.Length == 0 || remove.Count == 0) return;
            lock (Gate)
            {
                List<IgnoredIssueRecord> all = LoadAll();
                all.RemoveAll(x => Same(x.DocumentKey, key) &&
                    remove.Contains(x.IgnoreKey ?? string.Empty));
                SaveAll(all);
            }
        }

        public static void RemoveKeys(string documentKey,
            IEnumerable<string> ignoreKeys)
        {
            string key = Normalize(documentKey);
            var remove = new HashSet<string>((ignoreKeys ??
                Enumerable.Empty<string>()).Where(x =>
                    !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);
            if (key.Length == 0 || remove.Count == 0) return;
            lock (Gate)
            {
                List<IgnoredIssueRecord> all = LoadAll();
                all.RemoveAll(x => Same(x.DocumentKey, key) &&
                    remove.Contains(x.IgnoreKey ?? string.Empty));
                SaveAll(all);
            }
        }

        private static List<IgnoredIssueRecord> LoadAll()
        {
            var result = new List<IgnoredIssueRecord>();
            string path = SettingsFilePath;
            if (!File.Exists(path)) return result;
            try
            {
                XDocument document = XDocument.Load(path);
                foreach (XElement item in document.Root == null
                    ? Enumerable.Empty<XElement>() :
                    document.Root.Elements("Issue"))
                {
                    DrawingCheckSeverity severity;
                    if (!Enum.TryParse((string)item.Attribute("severity"),
                        true, out severity)) severity =
                            DrawingCheckSeverity.Warning;
                    DateTime ignoredAt;
                    if (!DateTime.TryParse((string)item.Attribute("ignoredAt"),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out ignoredAt))
                        ignoredAt = DateTime.UtcNow;
                    result.Add(new IgnoredIssueRecord
                    {
                        DocumentKey = (string)item.Attribute("document") ??
                            string.Empty,
                        IgnoreKey = (string)item.Attribute("key") ?? string.Empty,
                        RuleId = (string)item.Attribute("rule") ?? string.Empty,
                        Category = (string)item.Attribute("category") ??
                            string.Empty,
                        Severity = severity,
                        Title = (string)item.Element("Title") ?? string.Empty,
                        Message = (string)item.Element("Message") ?? string.Empty,
                        ObjectHandles = ((string)item.Element("Handles") ??
                            string.Empty).Split(new[] { ';' },
                                StringSplitOptions.RemoveEmptyEntries).ToList(),
                        IgnoredAt = ignoredAt
                    });
                }
            }
            catch { }
            return result;
        }

        private static void SaveAll(IEnumerable<IgnoredIssueRecord> records)
        {
            try
            {
                string path = SettingsFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var root = new XElement("DrawingCheckIgnored",
                    new XAttribute("version", "1"));
                foreach (IgnoredIssueRecord record in (records ??
                    Enumerable.Empty<IgnoredIssueRecord>())
                    .Where(x => x != null &&
                        !string.IsNullOrWhiteSpace(x.DocumentKey) &&
                        !string.IsNullOrWhiteSpace(x.IgnoreKey))
                    .OrderBy(x => x.DocumentKey,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(x => x.IgnoredAt))
                    root.Add(new XElement("Issue",
                        new XAttribute("document", record.DocumentKey),
                        new XAttribute("key", record.IgnoreKey),
                        new XAttribute("rule", record.RuleId ?? string.Empty),
                        new XAttribute("category", record.Category ?? string.Empty),
                        new XAttribute("severity", record.Severity),
                        new XAttribute("ignoredAt", record.IgnoredAt.ToString(
                            "o", CultureInfo.InvariantCulture)),
                        new XElement("Title", record.Title ?? string.Empty),
                        new XElement("Message", record.Message ?? string.Empty),
                        new XElement("Handles", string.Join(";",
                            (record.ObjectHandles ?? new List<string>())
                            .ToArray()))));
                new XDocument(root).Save(path);
            }
            catch { }
        }

        private static string SettingsFilePath
        {
            get
            {
                string root = Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);
                return Path.Combine(root, "CDBox", "DrawingCheckIgnored.xml");
            }
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        private sealed class IgnoredIssueRecord
        {
            public string DocumentKey;
            public string IgnoreKey;
            public string RuleId;
            public string Category;
            public DrawingCheckSeverity Severity;
            public string Title;
            public string Message;
            public List<string> ObjectHandles;
            public DateTime IgnoredAt;
        }
    }
}
