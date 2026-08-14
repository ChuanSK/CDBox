[CmdletBinding()]
param(
    [string]$LatestVersion = '',
    [int]$VersionCode = 0,
    [string]$Title = '',
    [string]$AppVersion = '',
    [string]$PackageFileName = '',

    [string[]]$Notes = @(),
    [bool]$Mandatory = $false,
    [string]$Configuration = 'Release',
    [string]$AutoCADManagedDir = '',
    [string]$OutputDirectory = '',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

[xml]$project = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $repoRoot 'CDBox.csproj')
function Get-CDBoxProjectProperty([string]$name) {
    foreach ($group in @($project.Project.PropertyGroup)) {
        $node = $group.SelectSingleNode($name)
        if ($null -ne $node -and -not [string]::IsNullOrWhiteSpace($node.InnerText)) {
            return $node.InnerText.Trim()
        }
    }
    return ''
}

if ([string]::IsNullOrWhiteSpace($LatestVersion)) { $LatestVersion = Get-CDBoxProjectProperty 'CDBoxReleaseIdentity' }
if ($VersionCode -le 0) { $VersionCode = [int](Get-CDBoxProjectProperty 'CDBoxVersionCode') }
if ([string]::IsNullOrWhiteSpace($Title)) { $Title = Get-CDBoxProjectProperty 'CDBoxReleaseTitle' }
if ([string]::IsNullOrWhiteSpace($AppVersion)) { $AppVersion = Get-CDBoxProjectProperty 'CDBoxAppVersion' }
if ([string]::IsNullOrWhiteSpace($PackageFileName)) { $PackageFileName = Get-CDBoxProjectProperty 'CDBoxPackageFileName' }

if ([string]::IsNullOrWhiteSpace($LatestVersion) -or $VersionCode -le 0 -or [string]::IsNullOrWhiteSpace($Title) -or [string]::IsNullOrWhiteSpace($AppVersion)) {
    throw 'Release metadata is incomplete in CDBox.csproj.'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

if ([IO.Path]::GetExtension($PackageFileName) -ne '.zip' -or [IO.Path]::GetFileName($PackageFileName) -ne $PackageFileName) {
    throw 'PackageFileName must be a .zip file name without a directory.'
}

if (-not $SkipBuild) {
    $buildArguments = @('build', (Join-Path $repoRoot 'CDBox.sln'), '--configuration', $Configuration, '--no-restore', '--verbosity:minimal')
    if (-not [string]::IsNullOrWhiteSpace($AutoCADManagedDir)) {
        $buildArguments += "-p:AutoCADManagedDir=$AutoCADManagedDir"
    }
    & dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE." }
}

$buildOutput = Join-Path $repoRoot "bin\$Configuration\net48"
$mainAssembly = Join-Path $buildOutput 'CDBox.dll'
if (-not (Test-Path -LiteralPath $mainAssembly)) {
    throw "The main Release assembly was not found: $mainAssembly"
}

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("CDBox.Release." + [Guid]::NewGuid().ToString('N'))
$bundleRoot = Join-Path $temporaryRoot 'CDBox.bundle'
$contentsRoot = Join-Path $bundleRoot 'Contents'

try {
    New-Item -ItemType Directory -Path $contentsRoot -Force | Out-Null

    $manifestSource = Join-Path $repoRoot 'CDBox_Integrated\Install\PackageContents.xml'
    [xml]$packageContents = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestSource
    $packageContents.ApplicationPackage.AppVersion = $AppVersion
    $packageContents.Save((Join-Path $bundleRoot 'PackageContents.xml'))

    $rootExtensions = @('.dll', '.exe', '.pdb', '.config', '.json', '.xml')
    Get-ChildItem -LiteralPath $buildOutput -File | Where-Object { $rootExtensions -contains $_.Extension.ToLowerInvariant() } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $contentsRoot $_.Name)
    }

    foreach ($relativeDirectory in @('Templates', 'Updater', 'runtimes\win-x64')) {
        $source = Join-Path $buildOutput $relativeDirectory
        if (Test-Path -LiteralPath $source) {
            $destination = Join-Path $contentsRoot $relativeDirectory
            New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination -Recurse
        }
    }

    $requiredFiles = @(
        'PackageContents.xml',
        'Contents\CDBox.dll',
        'Contents\CDBox.Shared.dll',
        'Contents\CDBox.RealEstate.dll',
        'Contents\Microsoft.Web.WebView2.Core.dll',
        'Contents\Microsoft.Web.WebView2.WinForms.dll',
        'Contents\runtimes\win-x64\native\WebView2Loader.dll',
        'Contents\Updater\CDBoxUpdater.exe'
    )
    foreach ($relativePath in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $bundleRoot $relativePath))) {
            throw "The staged release is missing a required file: $relativePath"
        }
    }

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $packagePath = Join-Path $OutputDirectory $PackageFileName
    if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath -Force }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archiveStream = [IO.File]::Open($packagePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $archive = New-Object IO.Compression.ZipArchive($archiveStream, [IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        $entryRoot = $temporaryRoot.TrimEnd('\') + '\'
        Get-ChildItem -LiteralPath $bundleRoot -File -Recurse | ForEach-Object {
            $entryName = $_.FullName.Substring($entryRoot.Length).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $_.FullName,
                $entryName,
                [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
        $archiveStream.Dispose()
    }

    $manifestPath = Join-Path $OutputDirectory 'update.json'
    & (Join-Path $PSScriptRoot 'New-CDBoxUpdateManifest.ps1') `
        -PackagePath $packagePath `
        -LatestVersion $LatestVersion `
        -VersionCode $VersionCode `
        -Title $Title `
        -Notes $Notes `
        -Mandatory $Mandatory `
        -OutputPath $manifestPath
}
finally {
    $fullTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $fullStage = [IO.Path]::GetFullPath($temporaryRoot).TrimEnd('\') + '\'
    if ($fullStage.StartsWith($fullTemp, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $temporaryRoot -Leaf).StartsWith('CDBox.Release.')) {
        if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
    }
}
