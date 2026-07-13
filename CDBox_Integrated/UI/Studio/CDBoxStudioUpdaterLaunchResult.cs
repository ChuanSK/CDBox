namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioUpdaterLaunchResult
    {
        public bool Prepared { get; set; }
        public bool Started { get; set; }
        public string PendingUpdatePath { get; set; }
        public string UpdaterPath { get; set; }
        public string TargetBundlePath { get; set; }
        public string UpdaterLogPath { get; set; }
        public string LastResultPath { get; set; }
        public string CommandLine { get; set; }
        public string ErrorMessage { get; set; }
    }
}
