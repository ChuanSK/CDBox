using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioUpdateDownloadResult
    {
        public CDBoxStudioUpdateDownloadResult()
        {
            Success = false;
            Verified = false;
            CurrentVersion = CDBoxStudioUpdateService.CurrentVersion;
            CurrentVersionCode = CDBoxStudioUpdateService.CurrentVersionCode;
            LatestVersion = string.Empty;
            VersionCode = 0;
            Channel = string.Empty;
            InstallerFileName = string.Empty;
            InstallerSizeBytes = 0;
            Sha256Expected = string.Empty;
            Sha256Actual = string.Empty;
            FilePath = string.Empty;
            SourceName = string.Empty;
            SourceUrl = string.Empty;
            ErrorMessage = string.Empty;
            InstallerCommand = string.Empty;
            InstallerPrepared = false;
            InstallerStarted = false;
            InstallerErrorMessage = string.Empty;
            StartedAt = DateTime.Now;
            FinishedAt = DateTime.MinValue;
            Attempts = new List<CDBoxStudioUpdateDownloadAttempt>();
            InstalledComponentIds = new string[0];
            InstalledComponentNames = new string[0];
            ComponentPlanText = string.Empty;
            ComponentStateWarning = string.Empty;
        }

        public bool Success { get; set; }
        public bool Verified { get; set; }
        public string CurrentVersion { get; set; }
        public int CurrentVersionCode { get; set; }
        public string LatestVersion { get; set; }
        public int VersionCode { get; set; }
        public string Channel { get; set; }
        public string InstallerFileName { get; set; }
        public long InstallerSizeBytes { get; set; }
        public string Sha256Expected { get; set; }
        public string Sha256Actual { get; set; }
        public string FilePath { get; set; }
        public string SourceName { get; set; }
        public string SourceUrl { get; set; }
        public string ErrorMessage { get; set; }
        public string InstallerCommand { get; set; }
        public bool InstallerPrepared { get; set; }
        public bool InstallerStarted { get; set; }
        public string InstallerErrorMessage { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }
        public List<CDBoxStudioUpdateDownloadAttempt> Attempts { get; private set; }
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
            sb.Append('{');
            Append(sb, "success", Success); sb.Append(',');
            Append(sb, "verified", Verified); sb.Append(',');
            Append(sb, "currentVersion", CurrentVersion); sb.Append(',');
            Append(sb, "currentVersionCode", CurrentVersionCode); sb.Append(',');
            Append(sb, "latestVersion", LatestVersion); sb.Append(',');
            Append(sb, "versionCode", VersionCode); sb.Append(',');
            Append(sb, "channel", Channel); sb.Append(',');
            Append(sb, "installerFileName", InstallerFileName); sb.Append(',');
            Append(sb, "installerSizeBytes", InstallerSizeBytes); sb.Append(',');
            Append(sb, "installerSizeText", InstallerSizeText); sb.Append(',');
            Append(sb, "sha256Expected", Sha256Expected); sb.Append(',');
            Append(sb, "sha256Actual", Sha256Actual); sb.Append(',');
            Append(sb, "filePath", FilePath); sb.Append(',');
            Append(sb, "sourceName", SourceName); sb.Append(',');
            Append(sb, "sourceUrl", SourceUrl); sb.Append(',');
            Append(sb, "errorMessage", ErrorMessage); sb.Append(',');
            Append(sb, "installerCommand", InstallerCommand); sb.Append(',');
            Append(sb, "installerPrepared", InstallerPrepared); sb.Append(',');
            Append(sb, "installerStarted", InstallerStarted); sb.Append(',');
            Append(sb, "installerErrorMessage", InstallerErrorMessage); sb.Append(',');
            Append(sb, "startedAt", StartedAt.ToString("yyyy-MM-dd HH:mm:ss")); sb.Append(',');
            Append(sb, "finishedAt", FinishedAt == DateTime.MinValue ? string.Empty : FinishedAt.ToString("yyyy-MM-dd HH:mm:ss")); sb.Append(',');
            Append(sb, "componentPlanText", ComponentPlanText); sb.Append(',');
            Append(sb, "componentStateWarning", ComponentStateWarning); sb.Append(',');
            AppendArray(sb, "installedComponentIds", InstalledComponentIds); sb.Append(',');
            AppendArray(sb, "installedComponentNames", InstalledComponentNames); sb.Append(',');
            sb.Append("\"attempts\":[");
            for (int i = 0; i < Attempts.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Attempts[i] == null ? "{}" : Attempts[i].ToJson());
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
