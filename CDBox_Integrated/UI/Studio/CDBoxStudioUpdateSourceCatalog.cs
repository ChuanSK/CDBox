using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioUpdateSourceCatalog
    {
        public const string OfficialBaseUrl =
            "https://cdbox-release-cdbox-d9gsv9fvj6a1aed69.webapps.tcloudbase.com/";
        public const string StableManifestUrl = OfficialBaseUrl
            + "releases/stable/update.json";
        public const string PreviewManifestUrl = OfficialBaseUrl
            + "releases/preview/update.json";
        public const string LatestInstallerUrl = OfficialBaseUrl
            + "installer/latest/CDBoxInstaller.exe";

        public static string GetManifestUrl(string channel)
        {
            return string.Equals((channel ?? string.Empty).Trim(), "stable",
                StringComparison.OrdinalIgnoreCase)
                ? StableManifestUrl : PreviewManifestUrl;
        }

        public static IList<CDBoxStudioUpdateSource> CreateManifestSources(
            string channel = "preview")
        {
            return new List<CDBoxStudioUpdateSource>
            {
                new CDBoxStudioUpdateSource
                {
                    Name = "CDBox 官方发布源",
                    Url = GetManifestUrl(channel),
                    Enabled = true
                }
            };
        }

        public static string GetVersionedInstallerUrl(string version,
            string fileName)
        {
            return OfficialBaseUrl + "installer/"
                + Uri.EscapeDataString((version ?? string.Empty).Trim()) + "/"
                + Uri.EscapeDataString((fileName ?? string.Empty).Trim());
        }

        public static string GetReleaseNotesUrl(string version)
        {
            return OfficialBaseUrl + "releases/"
                + Uri.EscapeDataString((version ?? string.Empty).Trim());
        }
    }
}
