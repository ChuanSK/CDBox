[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$AutoCADDirectory = 'C:\Program Files\Autodesk\AutoCAD 2023'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$consolePath = Join-Path $AutoCADDirectory 'accoreconsole.exe'
$sampleDrawing = Join-Path $AutoCADDirectory 'Express\brkline.dwg'
$pluginPath = Join-Path $repoRoot "bin\$Configuration\net48\CDBox.dll"
$outputRoot = Join-Path $repoRoot 'artifacts\host-selftest'

foreach ($required in @($consolePath, $sampleDrawing, $pluginPath)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required host-test file was not found: $required"
    }
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$drawingPath = Join-Path $outputRoot 'CDBoxSelfTest.dwg'
$scriptPath = Join-Path $outputRoot 'CDBoxSelfTest.scr'
$logPath = Join-Path $outputRoot 'CDBoxSelfTest.log'
Copy-Item -LiteralPath $sampleDrawing -Destination $drawingPath -Force

$scriptLines = @(
    '_.FILEDIA',
    '0',
    '_.SECURELOAD',
    '0',
    '_.NETLOAD',
    ('"' + $pluginPath.Replace('\', '/') + '"'),
    'CDSELFTEST',
    '_.QUIT',
    '_N'
)
[IO.File]::WriteAllLines($scriptPath, $scriptLines, [Text.Encoding]::ASCII)

$previousHeadlessValue = $env:CDBOX_HEADLESS_SELFTEST
$env:CDBOX_HEADLESS_SELFTEST = '1'
try {
    $output = (& $consolePath /i $drawingPath /s $scriptPath 2>&1 | Out-String)
    $output = $output.Replace("`0", '')
    [IO.File]::WriteAllText($logPath, $output, [Text.UTF8Encoding]::new($false))
    Write-Output $output

    if ($output -notmatch 'CDBOX_SELFTEST_RESULT=PASS') {
        throw "CDBox Core Console self-test did not pass. See $logPath"
    }
}
finally {
    $env:CDBOX_HEADLESS_SELFTEST = $previousHeadlessValue
}

[pscustomobject]@{
    Result = 'PASS'
    Host = $consolePath
    Plugin = $pluginPath
    Log = $logPath
}
