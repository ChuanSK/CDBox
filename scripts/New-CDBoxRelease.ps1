[CmdletBinding()]
param(
    [string]$ReleaseVersion = '',
    [string]$InstallerVersion = '',
    [string]$Title = '',
    [string]$AppVersion = '',
    [string]$InstallerFileName = '',
    [string]$Summary = '',
    [string[]]$Notes = @(),
    [ValidateSet('', 'stable', 'preview')]
    [string]$Channel = '',
    [string]$PublishedAt = '',
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

if ([string]::IsNullOrWhiteSpace($Title)) { $Title = Get-CDBoxProjectProperty 'CDBoxReleaseTitle' }
if ([string]::IsNullOrWhiteSpace($AppVersion)) { $AppVersion = Get-CDBoxProjectProperty 'CDBoxAppVersion' }
if ([string]::IsNullOrWhiteSpace($InstallerFileName)) { $InstallerFileName = Get-CDBoxProjectProperty 'CDBoxInstallerFileName' }
if ([string]::IsNullOrWhiteSpace($ReleaseVersion)) { $ReleaseVersion = $AppVersion }
if ([string]::IsNullOrWhiteSpace($InstallerVersion)) { $InstallerVersion = $ReleaseVersion }
if ([string]::IsNullOrWhiteSpace($Channel)) { $Channel = (Get-CDBoxProjectProperty 'CDBoxUpdateChannel').ToLowerInvariant() }
if ([string]::IsNullOrWhiteSpace($Summary)) {
    $Summary = if (@($Notes).Count -gt 0) { @($Notes) -join '；' } else { "CDBox $ReleaseVersion 版本更新。" }
}
if ([string]::IsNullOrWhiteSpace($PublishedAt)) { $PublishedAt = [DateTimeOffset]::Now.ToString('yyyy-MM-ddTHH:mm:sszzz') }

if ([string]::IsNullOrWhiteSpace($ReleaseVersion) -or [string]::IsNullOrWhiteSpace($InstallerVersion) -or [string]::IsNullOrWhiteSpace($Title) -or [string]::IsNullOrWhiteSpace($AppVersion)) {
    throw 'Release metadata is incomplete in CDBox.csproj.'
}
if ($Channel -notin @('stable', 'preview')) { throw 'CDBoxUpdateChannel must be stable or preview.' }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ([IO.Path]::GetExtension($InstallerFileName) -ne '.exe' -or [IO.Path]::GetFileName($InstallerFileName) -ne $InstallerFileName) {
    throw 'InstallerFileName must be an .exe file name without a directory.'
}

if (-not $SkipBuild) {
    $buildArguments = @('build', (Join-Path $repoRoot 'CDBox.sln'), '--configuration', $Configuration, '--no-restore', '--verbosity:minimal', "-p:CDBoxUpdateChannel=$Channel")
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
$builtVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($mainAssembly).ProductVersion
if ($builtVersion -ne $AppVersion) {
    throw "Built plugin version ($builtVersion) does not match AppVersion ($AppVersion). Rebuild before publishing."
}
$assemblyMetadata = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($mainAssembly)).GetCustomAttributesData()
$channelAttribute = $assemblyMetadata | Where-Object {
    $_.AttributeType.FullName -eq 'System.Reflection.AssemblyMetadataAttribute' `
        -and $_.ConstructorArguments[0].Value -eq 'CDBoxUpdateChannel'
} | Select-Object -First 1
if ($null -eq $channelAttribute -or $channelAttribute.ConstructorArguments[1].Value -ne $Channel) {
    throw "Built plugin channel does not match requested channel ($Channel). Rebuild without -SkipBuild."
}

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("CDBox.Release." + [Guid]::NewGuid().ToString('N'))
$bundleRoot = Join-Path $temporaryRoot 'CDBox.bundle'
$contentsRoot = Join-Path $bundleRoot 'Contents'
$payloadPath = Join-Path $temporaryRoot 'InstallerPayload.zip'

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

    $componentManifestPath = Join-Path $contentsRoot 'components.json'
    if (-not (Test-Path -LiteralPath $componentManifestPath)) {
        throw "The staged release is missing the component manifest: $componentManifestPath"
    }
    $componentManifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $componentManifestPath | ConvertFrom-Json
    $componentManifest.ProductVersion = $AppVersion
    $componentManifest | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 -LiteralPath $componentManifestPath

    foreach ($relativeDirectory in @('Templates', 'runtimes\win-x64')) {
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
        'Contents\CDBox.Common.dll',
        'Contents\CDBox.Wastewater.dll',
        'Contents\CDBox.RealEstate.dll',
        'Contents\components.json',
        'Contents\Microsoft.Web.WebView2.Core.dll',
        'Contents\Microsoft.Web.WebView2.WinForms.dll',
        'Contents\runtimes\win-x64\native\WebView2Loader.dll'
    )
    foreach ($relativePath in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $bundleRoot $relativePath))) {
            throw "The staged release is missing a required file: $relativePath"
        }
    }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archiveStream = [IO.File]::Open($payloadPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $archive = New-Object IO.Compression.ZipArchive($archiveStream, [IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        $entryRoot = $temporaryRoot.TrimEnd('\') + '\'
        Get-ChildItem -LiteralPath $bundleRoot -File -Recurse | ForEach-Object {
            $entryName = $_.FullName.Substring($entryRoot.Length).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entryName, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
        $archiveStream.Dispose()
    }

    $installerProject = Join-Path $repoRoot 'CDBoxInstaller\CDBoxInstaller.csproj'
    $installerBuildDirectory = Join-Path $temporaryRoot 'InstallerBuild'
    $installerBuildArguments = @(
        'build', $installerProject,
        '--configuration', $Configuration,
        '--verbosity:minimal',
        "-p:CDBoxInstallerPayload=$payloadPath",
        "-p:OutputPath=$installerBuildDirectory"
    )
    & dotnet @installerBuildArguments
    if ($LASTEXITCODE -ne 0) { throw "Installer build failed with exit code $LASTEXITCODE." }

    $builtInstaller = Join-Path $installerBuildDirectory 'CDBox安装器.exe'
    if (-not (Test-Path -LiteralPath $builtInstaller)) {
        throw "The installer executable was not found: $builtInstaller"
    }

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $versionedDirectory = Join-Path $OutputDirectory ("installer\" + $InstallerVersion)
    $latestDirectory = Join-Path $OutputDirectory 'installer\latest'
    New-Item -ItemType Directory -Path $versionedDirectory, $latestDirectory -Force | Out-Null
    $installerPath = Join-Path $versionedDirectory $InstallerFileName
    if (Test-Path -LiteralPath $installerPath) {
        $existingHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
        $newHash = (Get-FileHash -LiteralPath $builtInstaller -Algorithm SHA256).Hash
        if (-not [string]::Equals($existingHash, $newHash, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Versioned installer already exists with different content and cannot be overwritten: $installerPath"
        }
    }
    else {
        Copy-Item -LiteralPath $builtInstaller -Destination $installerPath
    }
    $verifyProcess = Start-Process -FilePath $installerPath -ArgumentList '--verify-payload' -WindowStyle Hidden -Wait -PassThru
    if ($verifyProcess.ExitCode -ne 0) { throw "Installer payload validation failed with exit code $($verifyProcess.ExitCode)." }
    $latestInstallerPath = Join-Path $latestDirectory 'CDBoxInstaller.exe'
    Copy-Item -LiteralPath $builtInstaller -Destination $latestInstallerPath -Force

    $schemaPublishDirectory = Join-Path $OutputDirectory 'schemas'
    New-Item -ItemType Directory -Path $schemaPublishDirectory -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'update.schema.json') -Destination (Join-Path $schemaPublishDirectory 'update.schema.json') -Force

    $manifestPath = Join-Path $OutputDirectory ("releases\$Channel\update.json")
    $manifestResult = & (Join-Path $PSScriptRoot 'New-CDBoxUpdateManifest.ps1') `
        -InstallerPath $installerPath `
        -ReleaseVersion $ReleaseVersion `
        -InstallerVersion $InstallerVersion `
        -Title $Title `
        -Summary $Summary `
        -Channel $Channel `
        -PublishedAt $PublishedAt `
        -OutputPath $manifestPath

    [PSCustomObject]@{
        ManifestPath = $manifestResult.ManifestPath
        ManifestUrl = $manifestResult.ManifestUrl
        InstallerPath = $manifestResult.InstallerPath
        LatestInstallerPath = $latestInstallerPath
        InstallerSize = $manifestResult.InstallerSize
        InstallerSha256 = $manifestResult.InstallerSha256
        LatestUrl = $manifestResult.LatestUrl
        VersionedUrl = $manifestResult.VersionedUrl
        Channel = $manifestResult.Channel
        ValidationStatus = $manifestResult.ValidationStatus
    }
}
finally {
    $fullTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $fullStage = [IO.Path]::GetFullPath($temporaryRoot).TrimEnd('\') + '\'
    if ($fullStage.StartsWith($fullTemp, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $temporaryRoot -Leaf).StartsWith('CDBox.Release.')) {
        if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
    }
}
