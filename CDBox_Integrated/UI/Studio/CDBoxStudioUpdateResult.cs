using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioUpdateResult
    {
        public CDBoxStudioUpdateResult()
        {
            Success = false;
            UpdateAvailable = false;
            CurrentVersion = CDBoxStudioUpdateService.CurrentVersion;
            CurrentVersionCode = CDBoxStudioUpdateService.CurrentVersionCode;
            LatestVersion = string.Empty;
            VersionCode = 0;
            Channel = string.Empty;
            SourceName = string.Empty;
            SourceUrl = string.Empty;
            DownloadUrl = string.Empty;
            InstallerFileName = string.Empty;
            InstallerSizeBytes = 0;
            Title = string.Empty;
            Notes = string.Empty;
            ReleaseDate = string.Empty;
            Mandatory = false;
            Sha256 = string.Empty;
            ErrorMessage = string.Empty;
            CheckedAt = DateTime.Now;
            Sources = new List<CDBoxStudioUpdateSource>();
            InstalledComponentIds = new string[0];
            InstalledComponentNames = new string[0];
            ComponentPlanText = string.Empty;
            ComponentStateWarning = string.Empty;
        }

        public bool Success { get; set; }
        public bool UpdateAvailable { get; set; }
        public string CurrentVersion { get; set; }
        public int CurrentVersionCode { get; set; }
        public string LatestVersion { get; set; }
        public int VersionCode { get; set; }
        public string Channel { get; set; }
        public string SourceName { get; set; }
        public string SourceUrl { get; set; }
        public string DownloadUrl { get; set; }
        public string InstallerFileName { get; set; }
        public long InstallerSizeBytes { get; set; }
        public string Title { get; set; }
        public string Notes { get; set; }
        public string ReleaseDate { get; set; }
        public bool Mandatory { get; set; }
        public string Sha256 { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime CheckedAt { get; set; }
        public List<CDBoxStudioUpdateSource> Sources { get; private set; }
        public string[] InstalledComponentIds { get; set; }
        public string[] InstalledComponentNames { get; set; }
        public string ComponentPlanText { get; set; }
        public string ComponentStateWarning { get; set; }

        public string InstallerSizeText
        {
            get
            {
                if (InstallerSizeBytes <= 0) return string.Empty;
                double mb = InstallerSizeBytes / 1024d / 1024d;
                return mb.ToString("0.##", CultureInfo.InvariantCulture) + " MB";
            }
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            Append(sb, "success", Success); sb.Append(',');
            Append(sb, "updateAvailable", UpdateAvailable); sb.Append(',');
            Append(sb, "currentVersion", CurrentVersion); sb.Append(',');
            Append(sb, "currentVersionCode", CurrentVersionCode); sb.Append(',');
            Append(sb, "latestVersion", LatestVersion); sb.Append(',');
            Append(sb, "versionCode", VersionCode); sb.Append(',');
            Append(sb, "channel", Channel); sb.Append(',');
            Append(sb, "sourceName", SourceName); sb.Append(',');
            Append(sb, "sourceUrl", SourceUrl); sb.Append(',');
            Append(sb, "downloadUrl", DownloadUrl); sb.Append(',');
            Append(sb, "installerFileName", InstallerFileName); sb.Append(',');
            Append(sb, "installerSizeText", InstallerSizeText); sb.Append(',');
            Append(sb, "installerSizeBytes", InstallerSizeBytes); sb.Append(',');
            Append(sb, "title", Title); sb.Append(',');
            Append(sb, "notes", Notes); sb.Append(',');
            Append(sb, "releaseDate", ReleaseDate); sb.Append(',');
            Append(sb, "mandatory", Mandatory); sb.Append(',');
            Append(sb, "sha256", Sha256); sb.Append(',');
            Append(sb, "errorMessage", ErrorMessage); sb.Append(',');
            Append(sb, "checkedAt", CheckedAt.ToString("yyyy-MM-dd HH:mm:ss")); sb.Append(',');
            Append(sb, "componentPlanText", ComponentPlanText); sb.Append(',');
            Append(sb, "componentStateWarning", ComponentStateWarning); sb.Append(',');
            AppendArray(sb, "installedComponentIds", InstalledComponentIds); sb.Append(',');
            AppendArray(sb, "installedComponentNames", InstalledComponentNames); sb.Append(',');
            sb.Append("\"sources\":[");
            for (int i = 0; i < Sources.Count; i++)
            {
                CDBoxStudioUpdateSource source = Sources[i];
                if (i > 0) sb.Append(',');
                sb.Append('{');
                Append(sb, "name", source == null ? string.Empty : source.Name); sb.Append(',');
                Append(sb, "url", source == null ? string.Empty : source.Url); sb.Append(',');
                Append(sb, "sha256", source == null ? string.Empty : source.Sha256); sb.Append(',');
                Append(sb, "enabled", source == null || source.Enabled);
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, string name, string value)
        {
            sb.Append('"').Append(Escape(name)).Append("\":\"").Append(Escape(value ?? string.Empty)).Append('"');
        }

        private static void Append(StringBuilder sb, string name, bool value)
        {
            sb.Append('"').Append(Escape(name)).Append("\":").Append(value ? "true" : "false");
        }

        private static void Append(StringBuilder sb, string name, int value)
        {
            sb.Append('"').Append(Escape(name)).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Append(StringBuilder sb, string name, long value)
        {
            sb.Append('"').Append(Escape(name)).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendArray(StringBuilder sb, string name,
            IEnumerable<string> values)
        {
            sb.Append('"').Append(Escape(name)).Append("\":[");
            bool first = true;
            foreach (string value in values ?? new string[0])
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(Escape(value ?? string.Empty)).Append('"');
            }
            sb.Append(']');
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var sb = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
