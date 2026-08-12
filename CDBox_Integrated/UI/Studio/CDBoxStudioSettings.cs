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
            AnnotationHudNormalOpacity = 0.68;
            AnnotationHudHoverOpacity = 1.0;
            AnnotationHudGlowEnabled = true;
            AnnotationHudGlowIntensity = 0.28;
            FloatingCenterEnabled = true;
            FloatingCenterShowOnStartup = true;
            FloatingCenterSnapToEdges = true;
            FloatingCenterAutoCloseSeconds = 5;
            FloatingCenterAutoCheckEnabled = false;
            FloatingCenterSafeAutoSyncEnabled = false;
            DoubleClickOpenEnabled = true;
            ColorOutputMode = TCPipeAutoDraw.Core.Colors.CDBoxColorOutputMode.PreserveOriginalType;
            UpdateChannel = CDBoxStudioUpdateService.DefaultChannel;
        }

        public string Theme { get; set; }
        public bool AnimationsEnabled { get; set; }
        public double AnnotationHudNormalOpacity { get; set; }
        public double AnnotationHudHoverOpacity { get; set; }
        public bool AnnotationHudGlowEnabled { get; set; }
        public double AnnotationHudGlowIntensity { get; set; }
        public bool FloatingCenterEnabled { get; set; }
        public bool FloatingCenterShowOnStartup { get; set; }
        public bool FloatingCenterSnapToEdges { get; set; }
        public int FloatingCenterAutoCloseSeconds { get; set; }
        public bool FloatingCenterAutoCheckEnabled { get; set; }
        public bool FloatingCenterSafeAutoSyncEnabled { get; set; }
        public bool DoubleClickOpenEnabled { get; set; }
        public TCPipeAutoDraw.Core.Colors.CDBoxColorOutputMode ColorOutputMode { get; set; }
        public string UpdateChannel { get; set; }

        public void Normalize()
        {
            if (!IsValidTheme(Theme)) Theme = ThemeLight;
            AnnotationHudNormalOpacity = Clamp(AnnotationHudNormalOpacity, 0.20, 1.0, 0.68);
            AnnotationHudHoverOpacity = Clamp(AnnotationHudHoverOpacity, 0.20, 1.0, 1.0);
            AnnotationHudGlowIntensity = Clamp(AnnotationHudGlowIntensity, 0.0, 1.0, 0.28);
            FloatingCenterAutoCloseSeconds = Math.Max(1,
                Math.Min(30, FloatingCenterAutoCloseSeconds));
            if (!Enum.IsDefined(typeof(TCPipeAutoDraw.Core.Colors.CDBoxColorOutputMode), ColorOutputMode))
                ColorOutputMode = TCPipeAutoDraw.Core.Colors.CDBoxColorOutputMode.PreserveOriginalType;
            if (string.IsNullOrWhiteSpace(UpdateChannel)) UpdateChannel = CDBoxStudioUpdateService.DefaultChannel;
            else UpdateChannel = UpdateChannel.Trim().ToLowerInvariant();
            if (!string.Equals(UpdateChannel, CDBoxStudioUpdateService.DefaultChannel, StringComparison.OrdinalIgnoreCase)) UpdateChannel = CDBoxStudioUpdateService.DefaultChannel;
        }

        public static bool IsValidTheme(string theme)
        {
            return string.Equals(theme, ThemeLight, StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme, ThemeFresh, StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme, ThemeDark, StringComparison.OrdinalIgnoreCase);
        }

        private static double Clamp(double value, double minimum, double maximum, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return fallback;
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
