using System;
using System.Globalization;
using System.Text;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioUpdateDownloadAttempt
    {
        public CDBoxStudioUpdateDownloadAttempt()
        {
            SourceName = string.Empty;
            SourceUrl = string.Empty;
            ErrorMessage = string.Empty;
            InstallerPath = string.Empty;
            Sha256Actual = string.Empty;
            StartedAt = DateTime.Now;
            FinishedAt = DateTime.MinValue;
        }

        public string SourceName { get; set; }
        public string SourceUrl { get; set; }
        public bool Success { get; set; }
        public bool Verified { get; set; }
        public string ErrorMessage { get; set; }
        public long BytesReceived { get; set; }
        public long BytesTotal { get; set; }
        public string InstallerPath { get; set; }
        public string Sha256Actual { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append('{');
            Append(sb, "sourceName", SourceName); sb.Append(',');
            Append(sb, "sourceUrl", SourceUrl); sb.Append(',');
            Append(sb, "success", Success); sb.Append(',');
            Append(sb, "verified", Verified); sb.Append(',');
            Append(sb, "errorMessage", ErrorMessage); sb.Append(',');
            Append(sb, "bytesReceived", BytesReceived); sb.Append(',');
            Append(sb, "bytesTotal", BytesTotal); sb.Append(',');
            Append(sb, "installerPath", InstallerPath); sb.Append(',');
            Append(sb, "sha256Actual", Sha256Actual); sb.Append(',');
            Append(sb, "startedAt", StartedAt.ToString("yyyy-MM-dd HH:mm:ss")); sb.Append(',');
            Append(sb, "finishedAt", FinishedAt == DateTime.MinValue ? string.Empty : FinishedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.Append('}');
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

        private static void Append(StringBuilder sb, string name, long value)
        {
            sb.Append('"').Append(Escape(name)).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
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
