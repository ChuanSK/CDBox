param(
    [string]$BuildDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'bin\ReleaseCommon\net48'),
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\common-workbench\preview')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Web.Extensions
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.Shared.dll'))
$common = [Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.Common.dll'))
$core = [Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.dll'))
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'
$appearance = [Activator]::CreateInstance($core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxStudioSettings'), $true)
$catalog = $core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxThemeCatalog')
$appearance.DarkTheme = $catalog.GetMethod('Create',$flags).Invoke($null,@('github','dark'))
$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$context = $serializer.DeserializeObject(@'
{"templates":[{"Id":"a","TemplateName":"标准 A3 横向图框","PaperSize":"A3","IsDefault":true,"FrameWidth":420,"FrameHeight":297,"FrameMinX":0,"FrameMinY":0,"FrameMaxX":420,"FrameMaxY":297,"MarginTop":10,"MarginRight":10,"MarginBottom":20,"MarginLeft":10,"PreviewSegments":[{"X1":10,"Y1":10,"X2":410,"Y2":10},{"X1":410,"Y1":10,"X2":410,"Y2":287},{"X1":410,"Y1":287,"X2":10,"Y2":287},{"X1":10,"Y1":287,"X2":10,"Y2":10}]},{"Id":"b","TemplateName":"项目图框（含标题栏）","PaperSize":"A3","FrameWidth":420,"FrameHeight":297,"MarginTop":5,"MarginRight":5,"MarginBottom":30,"MarginLeft":5}],"papers":[{"Name":"A3"},{"Name":"A4"}],"settings":{"DrawNorthArrow":true,"NorthDirectionMode":"Auto","NorthReferencePosition":"TopRight","NorthOffsetX":10,"NorthOffsetY":10,"NorthSize":15,"DrawScaleLabel":true,"ScaleText":"1:500","ScaleTextHeight":3.5,"ScaleColorIndex":7,"ScaleTextStyle":"Standard","ScaleReferencePosition":"TopRight","ScaleOffsetX":10,"ScaleOffsetY":25,"FramesPerRow":3,"HorizontalGap":20,"VerticalGap":20,"TemplateViewMode":"Single"},"textStyles":["Standard","宋体"]}
'@)
$shortValues = [Activator]::CreateInstance($common.GetType('CDBox.Common.Features.ShortCodeRecognition.ShortCodeRecognitionSettings'))
$bridge = @'
<script>window.previewMessages=[];window.previewErrors=[];window.addEventListener('error',e=>previewErrors.push(e.message));window.chrome={webview:{postMessage:raw=>previewMessages.push(raw)}};</script>
'@
[void][IO.Directory]::CreateDirectory($OutputDirectory)
foreach ($mode in @('light','dark')) {
    $appearance.Theme = $mode
    $pages = @{}
    $pages.home = $core.GetType('CDBox.Common.UI.CommonHomePage').GetMethod('BuildDocument',$flags).Invoke($null,@('0.5.0',$appearance))
    $pages.short = ($core.GetType('CDBox.Common.UI.ShortCodeRecognitionSettingsPage').GetMethods($flags) | Where-Object { $_.Name -eq 'BuildDocument' -and $_.GetParameters().Count -eq 2 }).Invoke($null,@($shortValues,$appearance))
    $pages.frame = ($core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxStudioFrameSettingsPage').GetMethods($flags) | Where-Object { $_.Name -eq 'BuildDocument' -and $_.GetParameters().Count -eq 3 }).Invoke($null,@($appearance,$false,$context))
    $pages.excel = ($core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxStudioExcelToCadPage').GetMethods($flags) | Where-Object { $_.Name -eq 'BuildStandaloneDocument' -and $_.GetParameters().Count -eq 4 }).Invoke($null,@($appearance,[double]2.5,$null,$null))
    foreach ($name in $pages.Keys) {
        # Only install the mock bridge before the first script, preserving message history.
        $html = $pages[$name].Insert($pages[$name].IndexOf('<script>'),$bridge)
        [IO.File]::WriteAllText((Join-Path $OutputDirectory "$name-$mode.html"),$html,(New-Object Text.UTF8Encoding($false)))
    }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CommonWorkbenchPreview.html') -Destination (Join-Path $OutputDirectory 'index.html')
Write-Output "Common preview: $OutputDirectory"
