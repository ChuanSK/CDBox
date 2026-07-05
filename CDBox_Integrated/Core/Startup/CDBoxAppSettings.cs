using System;

namespace TCPipeAutoDraw.Core.Startup
{
    /// <summary>
    /// CDBOX 启动、侧边栏和安装相关设置。
    /// 保存到 %AppData%\CDBox\CDBoxApp.settings，避免写入图纸或 CAD 系统配置。
    /// </summary>
    internal sealed class CDBoxAppSettings
    {
        public bool PromptInstallOnLoad { get; set; }
        public bool PromptSidebarOnLoad { get; set; }
        public bool AutoShowSidebarOnLoad { get; set; }
        public string InstalledPath { get; set; }

        public static CDBoxAppSettings Default
        {
            get
            {
                return new CDBoxAppSettings
                {
                    PromptInstallOnLoad = true,
                    PromptSidebarOnLoad = true,
                    AutoShowSidebarOnLoad = true,
                    InstalledPath = string.Empty
                };
            }
        }
    }
}
