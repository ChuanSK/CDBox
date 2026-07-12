[CmdletBinding()]
param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot '..\bin\Release\net48'),
    [string]$InstallRoot = 'C:\Program Files\Autodesk\AutoCAD 2023\CDBox.bundle'
)

$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($SourceDirectory)
$install = [IO.Path]::GetFullPath($InstallRoot)
$contents = Join-Path $install 'Contents'
$required = @(
    'CDBox.dll',
    'Microsoft.Web.WebView2.Core.dll',
    'Microsoft.Web.WebView2.WinForms.dll',
    'runtimes\win-x64\native\WebView2Loader.dll',
    'Updater\CDBoxUpdater.exe'
)

if (Get-Process acad, accoreconsole -ErrorAction SilentlyContinue) {
    throw 'AutoCAD or Core Console is still running. Close all AutoCAD processes before repair.'
}

foreach ($relative in $required) {
    $path = Join-Path $source $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Release output is incomplete. Missing: $path"
    }
}

if (-not (Test-Path -LiteralPath $install -PathType Container)) {
    throw "Existing CDBox.bundle was not found: $install"
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = "$install.repair-backup-$stamp"
$staging = Join-Path ([IO.Path]::GetTempPath()) "CDBox-legacy-repair-$stamp"

try {
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    Copy-Item -Path (Join-Path $source '*') -Destination $staging -Recurse -Force
    foreach ($relative in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $staging $relative) -PathType Leaf)) {
            throw "Staging validation failed. Missing: $relative"
        }
    }

    Copy-Item -LiteralPath $install -Destination $backup -Recurse -Force
    if (Test-Path -LiteralPath $contents) { Remove-Item -LiteralPath $contents -Recurse -Force }
    New-Item -ItemType Directory -Path $contents -Force | Out-Null
    Copy-Item -Path (Join-Path $staging '*') -Destination $contents -Recurse -Force

    foreach ($relative in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $contents $relative) -PathType Leaf)) {
            throw "Post-install validation failed. Missing: $relative"
        }
    }

    [pscustomobject]@{
        Result = 'PASS'
        InstallRoot = $install
        Backup = $backup
        Source = $source
    }
}
catch {
    if (Test-Path -LiteralPath $backup) {
        if (Test-Path -LiteralPath $install) { Remove-Item -LiteralPath $install -Recurse -Force }
        Copy-Item -LiteralPath $backup -Destination $install -Recurse -Force
    }
    throw
}
finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
