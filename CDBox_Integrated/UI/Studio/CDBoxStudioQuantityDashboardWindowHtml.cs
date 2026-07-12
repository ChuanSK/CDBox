namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioQuantityDashboardWindowHtml
    {
        public static string Build(CDBoxStudioSettings settings, string logFilePath)
        {
            settings = settings ?? new CDBoxStudioSettings();
            settings.Normalize();
            return CDBoxStudioQuantityDashboardPage.BuildStandaloneDocument(settings.Theme, settings.AnimationsEnabled, logFilePath);
        }
    }
}
