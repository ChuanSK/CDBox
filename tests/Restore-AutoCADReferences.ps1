[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$packageRoot = Join-Path $outputRoot 'packages'
$referenceRoot = Join-Path $outputRoot 'references'
$projectPath = Join-Path $PSScriptRoot 'AutoCADReferences\AutoCADReferences.csproj'
& dotnet restore $projectPath --packages $packageRoot `
    --source 'https://api.nuget.org/v3/index.json' --verbosity minimal | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "AutoCAD reference package restore failed: $LASTEXITCODE"
}

New-Item -ItemType Directory -Path $referenceRoot -Force | Out-Null
$packages = @{
    'AcMgd.dll' = 'autocad.net'
    'AcCoreMgd.dll' = 'autocad.net.core'
    'AcDbMgd.dll' = 'autocad.net.model'
}
foreach ($fileName in $packages.Keys) {
    $packageDirectory = Join-Path $packageRoot ($packages[$fileName] + '\24.2.0')
    $matches = @(Get-ChildItem -LiteralPath $packageDirectory -Recurse -File `
        -Filter $fileName)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one Autodesk reference for $fileName; found $($matches.Count)."
    }
    Copy-Item -LiteralPath $matches[0].FullName `
        -Destination (Join-Path $referenceRoot $fileName) -Force
}

# Reference assemblies are for compilation only; never package them with CDBox.
Write-Output $referenceRoot
