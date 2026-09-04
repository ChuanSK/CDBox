namespace CDBox.ReleaseTool;

internal sealed class ReleaseManifest
{
    public int SchemaVersion { get; set; } = 2;
    public string Channel { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public ReleaseInformation Release { get; set; } = new();
    public InstallerInformation Installer { get; set; } = new();
}

internal sealed class ReleaseInformation
{
    public string Version { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string ReleaseNotesUrl { get; set; } = string.Empty;
}

internal sealed class InstallerInformation
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public InstallerPackageInformation Package { get; set; } = new();
}

internal sealed class InstallerPackageInformation
{
    public string FileName { get; set; } = string.Empty;
    public string VersionedUrl { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

internal sealed class ReleaseConfiguration
{
    public Dictionary<string, ReleaseChannelConfiguration> Channels { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class ReleaseChannelConfiguration
{
    public string Channel { get; set; } = string.Empty;
    public string UpdateBaseUrl { get; set; } = string.Empty;
    public string DownloadBaseUrl { get; set; } = string.Empty;
    public string WebsiteBaseUrl { get; set; } = string.Empty;
}

internal sealed class ReleaseGenerationResult
{
    public string ManifestPath { get; set; } = string.Empty;
    public string ManifestUrl { get; set; } = string.Empty;
    public string InstallerPath { get; set; } = string.Empty;
    public long InstallerSize { get; set; }
    public string InstallerSha256 { get; set; } = string.Empty;
    public string LatestUrl { get; set; } = string.Empty;
    public string VersionedUrl { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string ValidationStatus { get; set; } = "READY TO PUBLISH";
}
