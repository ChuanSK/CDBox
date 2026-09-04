[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ('CDBox.ManifestTest.' + [Guid]::NewGuid().ToString('N'))

function Assert-Manifest([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
    $installerPath = Join-Path $testRoot 'CDBoxInstaller-5.1.0.exe'
    [IO.File]::WriteAllBytes($installerPath,
        [Text.Encoding]::UTF8.GetBytes('CDBox manifest contract test'))
    $manifestPath = Join-Path $testRoot 'update.json'

    $result = & (Join-Path $repoRoot 'scripts\New-CDBoxUpdateManifest.ps1') `
        -InstallerPath $installerPath `
        -ReleaseVersion '5.1.0' `
        -InstallerVersion '5.1.0' `
        -Title 'CDBox Studio Preview 5.1.0' `
        -Summary 'Release Manifest V2 contract test.' `
        -Channel 'preview' `
        -PublishedAt '2026-09-03T00:00:00+08:00' `
        -OutputPath $manifestPath

    Assert-Manifest ($result.ValidationStatus -eq 'READY TO PUBLISH') `
        'Generator did not return READY TO PUBLISH.'
    Assert-Manifest (Test-Path -LiteralPath $manifestPath) `
        'Generator did not create update.json.'

    $bytes = [IO.File]::ReadAllBytes($manifestPath)
    Assert-Manifest (-not ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF `
            -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) `
        'update.json must use UTF-8 without BOM.'

    $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath |
        ConvertFrom-Json
    Assert-Manifest ($manifest.schemaVersion -eq 2) `
        'schemaVersion must be 2.'
    Assert-Manifest ($manifest.channel -eq 'preview') `
        'channel must be preview.'
    Assert-Manifest ($manifest.release.version -eq '5.1.0') `
        'release.version mismatch.'
    Assert-Manifest ($manifest.installer.package.fileName -eq `
            'CDBoxInstaller-5.1.0.exe') 'Installer file name mismatch.'
    Assert-Manifest ($manifest.installer.package.sizeBytes -eq `
            (Get-Item -LiteralPath $installerPath).Length) `
        'Installer size mismatch.'
    Assert-Manifest ($manifest.installer.package.sha256 -eq `
            (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash) `
        'Installer SHA-256 mismatch.'
    Assert-Manifest ($manifest.installer.downloadUrl -eq `
            'https://cdbox-release-cdbox-d9gsv9fvj6a1aed69.webapps.tcloudbase.com/installer/latest/CDBoxInstaller.exe') `
        'Latest installer URL mismatch.'
    Assert-Manifest ($manifest.installer.package.versionedUrl -eq `
            'https://cdbox-release-cdbox-d9gsv9fvj6a1aed69.webapps.tcloudbase.com/installer/5.1.0/CDBoxInstaller-5.1.0.exe') `
        'Versioned installer URL mismatch.'

    $toolAssembly = Join-Path $repoRoot `
        'CDBox.ReleaseTool\bin\Release\net8.0\CDBox.ReleaseTool.dll'
    & dotnet $toolAssembly validate `
        --manifest $manifestPath `
        --config (Join-Path $repoRoot 'scripts\release-config.json') `
        --schema (Join-Path $repoRoot 'scripts\update.schema.json')
    if ($LASTEXITCODE -ne 0) {
        throw "Manifest validation failed with exit code $LASTEXITCODE."
    }

    Write-Host 'Release Manifest V2 contract test passed.'
}
finally {
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $fullTestRoot = [IO.Path]::GetFullPath($testRoot).TrimEnd('\') + '\'
    if ($fullTestRoot.StartsWith($tempRoot,
            [StringComparison]::OrdinalIgnoreCase) `
        -and (Split-Path $testRoot -Leaf).StartsWith('CDBox.ManifestTest.')) {
        if (Test-Path -LiteralPath $testRoot) {
            Remove-Item -LiteralPath $testRoot -Recurse -Force
        }
    }
}
