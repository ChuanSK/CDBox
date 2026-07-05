namespace TCPipeAutoDraw.Core.Startup
{
    internal sealed class CDBoxInstallResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string InstallRoot { get; set; }
        public bool DeferredDeleteStarted { get; set; }
        public string SelfCheckReport { get; set; }
        public string LogFilePath { get; set; }

        public static CDBoxInstallResult Ok(string message, string installRoot)
        {
            return new CDBoxInstallResult
            {
                Success = true,
                Message = message,
                InstallRoot = installRoot,
                SelfCheckReport = string.Empty,
                LogFilePath = CDBoxInstallLogger.LogFilePath
            };
        }

        public static CDBoxInstallResult Ok(string message, string installRoot, string selfCheckReport)
        {
            return new CDBoxInstallResult
            {
                Success = true,
                Message = message,
                InstallRoot = installRoot,
                SelfCheckReport = selfCheckReport ?? string.Empty,
                LogFilePath = CDBoxInstallLogger.LogFilePath
            };
        }

        public static CDBoxInstallResult Fail(string message, string installRoot = null)
        {
            return new CDBoxInstallResult
            {
                Success = false,
                Message = message,
                InstallRoot = installRoot ?? string.Empty,
                SelfCheckReport = string.Empty,
                LogFilePath = CDBoxInstallLogger.LogFilePath
            };
        }
    }
}
