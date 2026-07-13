using System;

namespace CDBoxUpdater
{
    internal sealed class BundleInstallException : Exception
    {
        public BundleInstallException(string message, Exception innerException, bool rolledBack, string backupBundlePath, string packageSha256Actual)
            : base(message, innerException)
        {
            RolledBack = rolledBack;
            BackupBundlePath = backupBundlePath ?? string.Empty;
            PackageSha256Actual = packageSha256Actual ?? string.Empty;
        }

        public bool RolledBack { get; private set; }
        public string BackupBundlePath { get; private set; }
        public string PackageSha256Actual { get; private set; }
    }
}
