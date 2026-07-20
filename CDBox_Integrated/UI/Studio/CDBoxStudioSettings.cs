using System;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioSettings
    {
        public const string ThemeLight = "light";
        public const string ThemeFresh = "fresh";
        public const string ThemeDark = "dark";

        public CDBoxStudioSettings()
        {
            Theme = ThemeLight;
            AnimationsEnabled = true;
            UpdateChannel = CDBoxStudioUpdateService.DefaultChannel;
            UpdateSourceUrl = CDBoxStudioUpdateService.DefaultUpdateSourceUrl;
        }

        public string Theme { get; set; }
        public bool AnimationsEnabled { get; set; }
        public string UpdateChannel { get; set; }
        public string UpdateSourceUrl { get; set; }

        public void Normalize()
        {
            if (!IsValidTheme(Theme)) Theme = ThemeLight;
            if (string.IsNullOrWhiteSpace(UpdateChannel)) UpdateChannel = CDBoxStudioUpdateService.DefaultChannel;
            else UpdateChannel = UpdateChannel.Trim().ToLowerInvariant();
            if (!string.Equals(UpdateChannel, CDBoxStudioUpdateService.DefaultChannel, StringComparison.OrdinalIgnoreCase)) UpdateChannel = CDBoxStudioUpdateService.DefaultChannel;
            if (string.IsNullOrWhiteSpace(UpdateSourceUrl)) UpdateSourceUrl = CDBoxStudioUpdateService.DefaultUpdateSourceUrl;
            else UpdateSourceUrl = UpdateSourceUrl.Trim();
        }

        public static bool IsValidTheme(string theme)
        {
            return string.Equals(theme, ThemeLight, StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme, ThemeFresh, StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme, ThemeDark, StringComparison.OrdinalIgnoreCase);
        }
    }
}
