namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioInstallerLaunchResult
    {
        public bool Started { get; set; }
        public string InstallerPath { get; set; }
        public string CommandLine { get; set; }
        public string ErrorMessage { get; set; }

        public CDBoxStudioInstallerLaunchResult()
        {
            InstallerPath = string.Empty;
            CommandLine = string.Empty;
            ErrorMessage = string.Empty;
        }
    }
}
