param(
    [string]$BuildDirectory='D:/CDBox Studio Preview/bin/Release519/net48',
    [string]$OutputDirectory='D:/CDBox Studio Preview/artifacts/5.1.9/preview'
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions
foreach($name in @('AcDbMgd','AcMgd','AcCoreMgd')){[void][Reflection.Assembly]::LoadFrom("D:/Program Files/Autodesk/AutoCAD 2023/$name.dll")}
foreach($name in @('CDBox.Shared','CDBox.RealEstate','CDBox')){[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory ($name+'.dll')))}
$core=[Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.dll'))
$flags=[Reflection.BindingFlags]'Static,Public,NonPublic'
$appearance=[Activator]::CreateInstance($core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxStudioSettings'),$true)
$catalog=$core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxThemeCatalog')
$appearance.DarkTheme=$catalog.GetMethod('Create',$flags).Invoke($null,@('github','dark'))
$settings=[CDBox.RealEstate.Settings.BuildingLengthAnnotationSettings]::new()
$cad=[CDBox.RealEstate.Settings.BuildingAnnotationCadCatalog]::new()
$cad.TextStyles.Add('Standard');$cad.TextStyles.Add('宋体');$cad.Linetypes.Add('Continuous');$cad.Linetypes.Add('DASHED')
$pages=@{}
$pages.layers=$core.GetType('TCPipeAutoDraw.UI.Studio.LayerManagerPage').GetMethod('BuildDocument',$flags).Invoke($null,@())
$pages.rules=$core.GetType('TCPipeAutoDraw.UI.Studio.LayerRecognitionRulesPage').GetMethod('BuildDocument',$flags).Invoke($null,@())
$pages.building=$core.GetType('CDBox.RealEstate.UI.BuildingLengthAnnotationSettingsPage').GetMethod('BuildHtml',$flags).Invoke($null,@($settings,$cad))
$bridge=@'
<script>window.previewMessages=[];window.previewErrors=[];window.addEventListener('error',e=>previewErrors.push(e.message));window.chrome={webview:{postMessage:m=>previewMessages.push(m)}};</script>
'@
[void][IO.Directory]::CreateDirectory($OutputDirectory)
foreach($mode in @('light','dark')){
    $appearance.Theme=$mode
    $json=$catalog.GetMethod('Json',$flags).Invoke($null,@($appearance))
    foreach($name in $pages.Keys){
        $html=$pages[$name].Insert($pages[$name].IndexOf('<script>'),$bridge)
        # Exercise the same live theme entry point used by the native host, without writing user preferences.
        $html=$html.Replace('</body>',"<script>window.previewAppearance=$json;window.CDBoxApplyWorkbenchTheme(window.previewAppearance);</script></body>")
        [IO.File]::WriteAllText((Join-Path $OutputDirectory "$name-$mode.html"),$html,[Text.UTF8Encoding]::new($false))
    }
}
Write-Output "Utility workbench previews: $OutputDirectory"
