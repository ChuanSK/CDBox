param([string]$BuildDirectory = 'D:/CDBox Studio Preview/bin/Release513/net48',
      [string]$OutputDirectory = 'D:/CDBox Studio Preview/artifacts/5.1.3/preview/accent')
$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.Shared.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.RealEstate.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.Wastewater.dll'))
$core=[Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.dll'))
$flags=[Reflection.BindingFlags]'Static,Public,NonPublic'
$settings=[Activator]::CreateInstance($core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxStudioSettings'),$true)
$attach=$core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxAccentAppearance').GetMethod('Attach',$flags)
$pages=@{}
$pages.building=$core.GetType('CDBox.RealEstate.UI.BuildingLengthAnnotationSettingsPage').GetMethod('BuildHtml',$flags).Invoke($null,@($null,$null))
$pages.quantity=$core.GetType('CDBox.Wastewater.UI.WastewaterQuantityDashboardPage').GetMethod('BuildStandaloneDocument',$flags).Invoke($null,@('light',$true,''))
$pages.section=$core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxStudioSectionDrawingPage').GetMethod('BuildStandaloneDocument',$flags).Invoke($null,@($settings,''))
[void][IO.Directory]::CreateDirectory($OutputDirectory)
foreach($name in $pages.Keys){
    $html=$attach.Invoke($null,@($pages[$name],$settings))
    $bridge='<script>window.previewErrors=[];window.addEventListener("error",e=>previewErrors.push(e.message));window.chrome={webview:{postMessage:raw=>{if(raw.startsWith("studio|getSectionDrawingOptions|"))setTimeout(()=>window.CDBoxSectionDrawingLoad({options:{Width:1,Layers:[{Height:0.2,LeftLabel:"示例结构层",DrawLayer:true,Pipes:[]}]}}),10);}}};</script>'
    $html=$html.Insert($html.IndexOf('<script>'),$bridge)
    [IO.File]::WriteAllText((Join-Path $OutputDirectory ($name+'.html')),$html,(New-Object Text.UTF8Encoding($false)))
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AccentPreview.html') -Destination (Join-Path $OutputDirectory 'index.html')
Write-Output "Accent preview: $OutputDirectory"
