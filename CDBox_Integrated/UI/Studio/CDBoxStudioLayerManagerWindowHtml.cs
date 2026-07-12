namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioLayerManagerWindowHtml
    {
        public static string Build(CDBoxStudioSettings settings, string logFilePath)
        {
            return CDBoxStudioLayerManagerPage.BuildStandaloneDocument(settings, logFilePath);
        }
    }
}
