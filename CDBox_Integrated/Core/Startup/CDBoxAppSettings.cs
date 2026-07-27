using System;

namespace TCPipeAutoDraw.Core.Startup
{
    /// <summary>
    /// CDBOX 启动和安装相关设置。
    /// 保存到 %AppData%\CDBox\CDBoxApp.settings，避免写入图纸或 CAD 系统配置。
    /// </summary>
    internal sealed class CDBoxAppSettings
    {
        public bool PromptInstallOnLoad { get; set; }
        public string InstalledPath { get; set; }
        public string LastInstallPromptIdentity { get; set; }

        public bool ShouldPromptForInstall(bool installed, string currentIdentity)
        {
            if (installed) return false;
            if (PromptInstallOnLoad) return true;
            return !string.Equals(
                LastInstallPromptIdentity ?? string.Empty,
                currentIdentity ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        public static CDBoxAppSettings Default
        {
            get
            {
                return new CDBoxAppSettings
                {
                    PromptInstallOnLoad = true,
                    InstalledPath = string.Empty,
                    LastInstallPromptIdentity = string.Empty
                };
            }
        }
    }
}
