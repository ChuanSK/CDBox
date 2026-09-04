using System;
using System.Collections.Generic;

namespace CDBox.Setup
{
    internal sealed class CadVersionDefinition
    {
        public CadVersionDefinition(int year, string releaseKey,
            string displayName = null)
        {
            Year = year;
            ReleaseKey = releaseKey ?? throw new ArgumentNullException(nameof(releaseKey));
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? (year > 0 ? "AutoCAD " + year
                    : "AutoCAD " + ReleaseKey)
                : displayName.Trim();
        }

        public int Year { get; }
        public string ReleaseKey { get; }
        public string DisplayName { get; }
    }

    internal sealed class PrerequisiteStatus
    {
        public bool DotNetFramework48Installed { get; set; }
        public int DotNetFrameworkRelease { get; set; }
        public bool WebView2Installed { get; set; }
        public string WebView2Version { get; set; } = string.Empty;
    }

    internal sealed class CadInstallation
    {
        public CadInstallation(CadVersionDefinition version, string installDirectory, string productKey)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
            InstallDirectory = installDirectory ?? string.Empty;
            ProductKey = productKey ?? string.Empty;
        }

        public CadVersionDefinition Version { get; }
        public string InstallDirectory { get; }
        public string ProductKey { get; }
        public bool IsDetected => !string.IsNullOrWhiteSpace(InstallDirectory) && !string.IsNullOrWhiteSpace(ProductKey);
        public string AcadExecutablePath => System.IO.Path.Combine(InstallDirectory, "acad.exe");
        public string CurrentUserProductRegistryPath =>
            @"Software\Autodesk\AutoCAD\" + Version.ReleaseKey + @"\" + ProductKey;
    }

    internal sealed class InstallResult
    {
        public InstallResult(CadInstallation installation, bool succeeded, string message)
        {
            Installation = installation;
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public CadInstallation Installation { get; }
        public bool Succeeded { get; }
        public string Message { get; }
    }

    internal sealed class InstallerLaunchOptions
    {
        public bool UpdateMode { get; set; }
        public string TargetCadExecutablePath { get; set; } = string.Empty;
        public string[] SelectedComponentIds { get; set; } =
            new string[0];
    }
}
