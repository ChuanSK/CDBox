param(
    [string]$BuildDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'bin\Release\net48'),
    [string]$Folder,
    [switch]$Child
)
$ErrorActionPreference = 'Stop'
if ($Child) {
    $shared = [Reflection.Assembly]::LoadFrom((Join-Path $Folder 'CDBox.Shared.dll'))
    $core = [Reflection.Assembly]::LoadFrom((Join-Path $Folder 'CDBox.dll'))
    $flags = [Reflection.BindingFlags]'Static,Public,NonPublic'
    $dispatcher = $core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxUiDispatcher', $true)
    $dispatcher.GetMethod('Initialize', $flags).Invoke($null, @()) | Out-Null
    $settingsType = $core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxStudioSettings', $true)
    $settings = [Activator]::CreateInstance($settingsType, $true)
    if ([string]::IsNullOrWhiteSpace($settings.Theme)) { throw 'Base theme failed to load' }
    foreach ($module in @('Common','Wastewater','RealEstate')) {
        $path = Join-Path $Folder "CDBox.$module.dll"
        if (-not (Test-Path -LiteralPath $path)) { continue }
        $business = [Reflection.Assembly]::LoadFrom($path)
        switch ($module) {
            'Common' {
                $pageType = $core.GetType('CDBox.Common.UI.CommonHomePage', $true)
                $page = $pageType.GetMethod('Create', $flags).Invoke($null, @('isolation-test'))
                $html = $page.HtmlFactory.Invoke()
            }
            'Wastewater' {
                $pageType = $core.GetType('CDBox.Wastewater.UI.WastewaterQuantityDashboardPage', $true)
                $html = $pageType.GetMethod('BuildStandaloneDocument', $flags).Invoke($null, @('light', $true, ''))
            }
            'RealEstate' {
                $recordType = $business.GetType('CDBox.RealEstate.Models.ParcelSurveyRecord', $true)
                $validationType = $business.GetType('CDBox.RealEstate.Models.ParcelSurveyValidationResult', $true)
                $pageType = $core.GetType('CDBox.RealEstate.UI.ParcelSurveyEditorPage', $true)
                $method = $pageType.GetMethod('BuildHtml', [Type[]]@($recordType, $validationType))
                $html = $method.Invoke($null, @([Activator]::CreateInstance($recordType), [Activator]::CreateInstance($validationType)))
            }
        }
        if ($html -notmatch '<html') { throw "$module page failed to render" }
    }
    foreach ($name in @('CDBox.Common','CDBox.Wastewater','CDBox.RealEstate')) {
        if (-not (Test-Path -LiteralPath (Join-Path $Folder "$name.dll")) -and
            ([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq $name })) {
            throw "Unexpected unloaded module dependency: $name"
        }
    }
    Write-Output 'PASS'
    exit 0
}

$runRoot = Join-Path ([IO.Path]::GetTempPath()) ('CDBox.UiIsolation.' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($runRoot)
$powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
for ($mask = 0; $mask -lt 8; $mask++) {
    $stage = Join-Path $runRoot ([string]$mask)
    [void][IO.Directory]::CreateDirectory($stage)
    $files = @('CDBox.dll','CDBox.Shared.dll')
    $modules = @('Common','Wastewater','RealEstate')
    for ($index = 0; $index -lt 3; $index++) {
        if ($mask -band (1 -shl $index)) { $files += "CDBox.$($modules[$index]).dll" }
    }
    foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $BuildDirectory $file) -Destination (Join-Path $stage $file) }
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $powershell
    $info.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '" -Child -Folder "' + $stage + '"'
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($info)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    $exitCode = $process.ExitCode
    $process.Dispose()
    if ($exitCode -ne 0 -or $stdout -notmatch 'PASS') { throw "Combination $mask failed: $stderr $stdout" }
    Write-Output "PASS: Base + $((($files | Where-Object { $_ -notin @('CDBox.dll','CDBox.Shared.dll') }) -join ', '))"
}
Write-Output "All 8 module combinations passed. Isolation directory: $runRoot"
