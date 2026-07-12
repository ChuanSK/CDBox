namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioAnnotationSettingsWindowHtml
    {
        public static string Build(CDBoxStudioSettings settings, string logFilePath, string initialSection)
        {
            return CDBoxStudioAnnotationSettingsPage.BuildStandaloneDocument(settings, logFilePath, initialSection);
        }
    }
}
