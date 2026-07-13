namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioDefaultProfilesWindowHtml
    {
        public static string Build(CDBoxStudioSettings settings, string logFilePath)
        {
            return CDBoxStudioQuantityDefaultsPage.BuildStandaloneDocument(settings, logFilePath);
        }
    }
}
