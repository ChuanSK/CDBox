using System.Collections.Generic;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioUpdateSourceCatalog
    {
        public const string GiteeManifestUrl = "https://gitee.com/chaun-skye/cdbox-studio-preview/raw/master/update.json";
        public const string GitCodeManifestUrl = "https://raw.gitcode.com/ChuanSK/CDBox_Studio_Preview/raw/main%2FREADME.md/update.json";
        public const string GitHubManifestUrl = "https://raw.githubusercontent.com/ChuanSK/CDBox/CDBox-Studio-Preview/update.json";

        public static IList<CDBoxStudioUpdateSource> CreateManifestSources()
        {
            return new List<CDBoxStudioUpdateSource>
            {
                new CDBoxStudioUpdateSource { Name = "Gitee", Url = GiteeManifestUrl, Enabled = true },
                new CDBoxStudioUpdateSource { Name = "GitCode", Url = GitCodeManifestUrl, Enabled = true },
                new CDBoxStudioUpdateSource { Name = "GitHub", Url = GitHubManifestUrl, Enabled = true }
            };
        }
    }
}
