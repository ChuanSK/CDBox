namespace TCPipeAutoDraw.UI.Studio
{
    /// <summary>
    /// 迁移后的公共业务独立页面只保留与统一界面外观有关的最小设置，
    /// 不再依赖主程序集的工作台设置模型。
    /// </summary>
    internal sealed class CDBoxStudioSettings
    {
        public CDBoxStudioSettings()
        {
            Theme = "fresh";
            AnimationsEnabled = true;
        }

        public string Theme { get; set; }
        public bool AnimationsEnabled { get; set; }

        public void Normalize()
        {
            string value = (Theme ?? string.Empty).Trim().ToLowerInvariant();
            Theme = value == "dark" || value == "classic"
                ? value : "fresh";
        }
    }
}
