[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,
    [Parameter(Mandatory = $true)]
    [string]$ReleaseVersion,
    [string]$InstallerVersion = '',
    [Parameter(Mandatory = $true)]
    [string]$Title,
    [Parameter(Mandatory = $true)]
    [string]$Summary,
    [ValidateSet('stable', 'preview')]
    [string]$Channel = 'preview',
    [string]$PublishedAt = '',
    [string]$ReleaseConfigPath = '',
    [string]$SchemaPath = '',
    [string]$OutputPath = '',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($InstallerVersion)) {
    $InstallerVersion = $ReleaseVersion
}
if ([string]::IsNullOrWhiteSpace($PublishedAt)) {
    $PublishedAt = [DateTimeOffset]::Now.ToString('yyyy-MM-ddTHH:mm:sszzz')
}
if ([string]::IsNullOrWhiteSpace($ReleaseConfigPath)) {
    $ReleaseConfigPath = Join-Path $PSScriptRoot 'release-config.json'
}
if ([string]::IsNullOrWhiteSpace($SchemaPath)) {
    $SchemaPath = Join-Path $PSScriptRoot 'update.schema.json'
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "artifacts\releases\$Channel\update.json"
}

$installer = Get-Item -LiteralPath $InstallerPath
if ($installer.Extension -ne '.exe' -or $installer.Length -le 0) {
    throw "The release artifact must be a non-empty EXE installer: $($installer.FullName)"
}

$toolProject = Join-Path $repoRoot 'CDBox.ReleaseTool\CDBox.ReleaseTool.csproj'
& dotnet build $toolProject --configuration $Configuration --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    throw "Release Manifest tool build failed with exit code $LASTEXITCODE."
}
$toolAssembly = Join-Path $repoRoot "CDBox.ReleaseTool\bin\$Configuration\net8.0\CDBox.ReleaseTool.dll"
if (-not (Test-Path -LiteralPath $toolAssembly)) {
    throw "Release Manifest tool was not found: $toolAssembly"
}

$toolOutput = & dotnet $toolAssembly generate `
    --installer $installer.FullName `
    --release-version $ReleaseVersion `
    --installer-version $InstallerVersion `
    --title $Title `
    --summary $Summary `
    --channel $Channel `
    --published-at $PublishedAt `
    --config ([IO.Path]::GetFullPath($ReleaseConfigPath)) `
    --schema ([IO.Path]::GetFullPath($SchemaPath)) `
    --output ([IO.Path]::GetFullPath($OutputPath))
if ($LASTEXITCODE -ne 0) {
    throw "Release Manifest generation failed with exit code $LASTEXITCODE."
}

$resultLine = @($toolOutput) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Last 1
if ([string]::IsNullOrWhiteSpace($resultLine)) {
    throw 'Release Manifest tool returned no result.'
}
$result = $resultLine | ConvertFrom-Json

[pscustomobject]@{
    ManifestPath = [string]$result.manifestPath
    ManifestUrl = [string]$result.manifestUrl
    InstallerPath = [string]$result.installerPath
    InstallerSize = [long]$result.installerSize
    InstallerSha256 = [string]$result.installerSha256
    LatestUrl = [string]$result.latestUrl
    VersionedUrl = [string]$result.versionedUrl
    Channel = [string]$result.channel
    ValidationStatus = [string]$result.validationStatus
}
