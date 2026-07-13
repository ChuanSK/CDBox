using System;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioPendingUpdate
    {
        public int SchemaVersion { get; set; }
        public string Channel { get; set; }
        public string CurrentVersion { get; set; }
        public string TargetVersion { get; set; }
        public int VersionCode { get; set; }
        public string PackagePath { get; set; }
        public string PackageFileName { get; set; }
        public string PackageSha256 { get; set; }
        public string TargetBundlePath { get; set; }
        public int AutoCadProcessId { get; set; }
        public string AutoCadProcessName { get; set; }
        public string AutoCadProcessStartTimeUtc { get; set; }
        public string CreatedAtUtc { get; set; }
        public string WorkDirectory { get; set; }
        public string UpdaterLogPath { get; set; }
        public string LastResultPath { get; set; }
        public string StudioLogPath { get; set; }
        public string ProtectedUserConfigDirectory { get; set; }
    }
}
