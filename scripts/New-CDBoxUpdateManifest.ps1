[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$LatestVersion,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$VersionCode,

    [Parameter(Mandatory = $true)]
    [string]$Title,

    [string]$ReleaseDate = (Get-Date -Format 'yyyy-MM-dd'),
    [string[]]$Notes = @(),
    [bool]$Mandatory = $false,
    [string]$Channel = 'studio-preview',
    [string]$SourceConfigPath = '',
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($SourceConfigPath)) {
    $SourceConfigPath = Join-Path $PSScriptRoot 'update-sources.json'
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'update.json'
}

$package = Get-Item -LiteralPath $PackagePath
if ($package.Extension -ne '.zip') {
    throw "The update package must be a ZIP file: $($package.FullName)"
}

if (-not (Test-Path -LiteralPath $SourceConfigPath)) {
    throw "The update source configuration does not exist: $SourceConfigPath"
}

$sourceTemplates = Get-Content -Raw -Encoding UTF8 -LiteralPath $SourceConfigPath | ConvertFrom-Json
if ($null -eq $sourceTemplates -or @($sourceTemplates).Count -eq 0) {
    throw "The update source configuration cannot be empty: $SourceConfigPath"
}

$urls = foreach ($source in @($sourceTemplates)) {
    $name = [string]$source.name
    $template = [string]$source.urlTemplate
    if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($template)) {
        throw 'Each update source must contain name and urlTemplate.'
    }

    $url = $template.Replace('{latestVersion}', $LatestVersion).Replace('{fileName}', $package.Name)
    $uri = $null
    if (-not [Uri]::TryCreate($url, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne 'https') {
        throw "An update source must be a valid HTTPS URL: $url"
    }

    $urlFileName = [Uri]::UnescapeDataString([IO.Path]::GetFileName($uri.AbsolutePath))
    if (-not [string]::Equals($urlFileName, $package.Name, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The URL file name does not match the package: URL=$urlFileName, Package=$($package.Name)"
    }

    [ordered]@{ name = $name; url = $url }
}

$hash = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
$manifest = [ordered]@{
    channel = $Channel
    latestVersion = $LatestVersion
    versionCode = $VersionCode
    title = $Title
    releaseDate = $ReleaseDate
    mandatory = $Mandatory
    package = [ordered]@{
        fileName = $package.Name
        size = $package.Length
        sha256 = $hash
        urls = @($urls)
    }
    notes = @($Notes)
}

$fullOutputPath = [IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path $fullOutputPath -Parent
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$json = $manifest | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText($fullOutputPath, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    ManifestPath = $fullOutputPath
    PackagePath = $package.FullName
    PackageSize = $package.Length
    Sha256 = $hash
    SourceCount = @($urls).Count
}
