using System;

namespace CDBoxUpdater
{
    internal sealed class UpdateResultRecord
    {
        public int SchemaVersion { get; set; }
        public bool Success { get; set; }
        public bool RolledBack { get; set; }
        public bool ElevationRequested { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public string CurrentVersion { get; set; }
        public string TargetVersion { get; set; }
        public int VersionCode { get; set; }
        public string PackagePath { get; set; }
        public string PackageSha256Expected { get; set; }
        public string PackageSha256Actual { get; set; }
        public string TargetBundlePath { get; set; }
        public string BackupBundlePath { get; set; }
        public string PendingUpdatePath { get; set; }
        public string UpdaterLogPath { get; set; }
        public string StartedAtUtc { get; set; }
        public string FinishedAtUtc { get; set; }

        public UpdateResultRecord()
        {
            SchemaVersion = 1;
            Status = "pending";
            Message = string.Empty;
            ErrorMessage = string.Empty;
            CurrentVersion = string.Empty;
            TargetVersion = string.Empty;
            PackagePath = string.Empty;
            PackageSha256Expected = string.Empty;
            PackageSha256Actual = string.Empty;
            TargetBundlePath = string.Empty;
            BackupBundlePath = string.Empty;
            PendingUpdatePath = string.Empty;
            UpdaterLogPath = string.Empty;
            StartedAtUtc = DateTime.UtcNow.ToString("o");
            FinishedAtUtc = string.Empty;
        }
    }
}
