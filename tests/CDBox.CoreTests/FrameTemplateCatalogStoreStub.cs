using System;
using System.IO;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    internal static class FrameTemplateCatalogStore
    {
        public static string DataDirectory
        {
            get
            {
                return Path.Combine(Path.GetTempPath(),
                    "CDBox.CoreTests", "FrameLayout");
            }
        }
    }
}
