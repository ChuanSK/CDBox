[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$AutoCADDirectory = 'C:\Program Files\Autodesk\AutoCAD 2023'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$acadPath = Join-Path $AutoCADDirectory 'acad.exe'
$sampleDrawing = Join-Path $AutoCADDirectory 'Express\brkline.dwg'
$pluginPath = Join-Path $repoRoot "bin\$Configuration\net48\CDBox.dll"
$outputRoot = Join-Path $repoRoot 'artifacts\host-interactive'

foreach ($required in @($acadPath, $sampleDrawing, $pluginPath)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required interactive-test file was not found: $required"
    }
}

if (Get-Process acad -ErrorAction SilentlyContinue) {
    throw 'An AutoCAD process is already running. Close it before starting the isolated interactive smoke test.'
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$drawingPath = Join-Path $outputRoot 'CDBoxInteractiveSmoke.dwg'
$scriptPath = Join-Path $outputRoot 'CDBoxInteractiveSmoke.scr'
Copy-Item -LiteralPath $sampleDrawing -Destination $drawingPath -Force

$scriptLines = @(
    '_.FILEDIA',
    '0',
    '_.SECURELOAD',
    '0',
    '_.NETLOAD',
    ('"' + $pluginPath.Replace('\', '/') + '"'),
    'CDSELFTEST',
    'CDSTUDIO',
    '_.SECURELOAD',
    '1',
    '_.FILEDIA',
    '1'
)
[IO.File]::WriteAllLines($scriptPath, $scriptLines, [Text.Encoding]::ASCII)

$previousHeadlessValue = $env:CDBOX_HEADLESS_SELFTEST
$env:CDBOX_HEADLESS_SELFTEST = '1'
try {
    $arguments = @(
        ('"' + $drawingPath + '"'),
        '/b',
        ('"' + $scriptPath + '"'),
        '/nologo'
    )
    $process = Start-Process -FilePath $acadPath -ArgumentList $arguments -WorkingDirectory $outputRoot -PassThru
}
finally {
    $env:CDBOX_HEADLESS_SELFTEST = $previousHeadlessValue
}

[pscustomobject]@{
    ProcessId = $process.Id
    AutoCAD = $acadPath
    Drawing = $drawingPath
    Script = $scriptPath
    ExpectedWindow = 'CDBox Studio'
}
