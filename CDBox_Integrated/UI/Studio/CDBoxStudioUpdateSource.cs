using System;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioUpdateSource
    {
        public CDBoxStudioUpdateSource()
        {
            Name = string.Empty;
            Url = string.Empty;
            Sha256 = string.Empty;
            Enabled = true;
        }

        public string Name { get; set; }
        public string Url { get; set; }
        public string Sha256 { get; set; }
        public bool Enabled { get; set; }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Name)) return Name.Trim();
                if (!string.IsNullOrWhiteSpace(Url)) return Url.Trim();
                return "未命名下载源";
            }
        }
    }
}
