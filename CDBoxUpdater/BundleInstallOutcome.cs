namespace CDBoxUpdater
{
    internal sealed class BundleInstallOutcome
    {
        public bool Success { get; set; }
        public bool RolledBack { get; set; }
        public string BackupBundlePath { get; set; }
        public string PackageSha256Actual { get; set; }
        public string Message { get; set; }
    }
}
