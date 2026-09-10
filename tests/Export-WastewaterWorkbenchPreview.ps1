param(
    [string]$BuildDirectory = 'D:/CDBox Studio Preview/bin/Release515/net48',
    [string]$OutputDirectory = 'D:/CDBox Studio Preview/artifacts/5.1.5/preview'
)
$ErrorActionPreference='Stop'
$flags=[Reflection.BindingFlags]'Public,NonPublic,Static'
foreach($name in @('AcDbMgd','AcMgd','AcCoreMgd')){[void][Reflection.Assembly]::LoadFrom("D:/Program Files/Autodesk/AutoCAD 2023/$name.dll")}
foreach($name in @('CDBox.Shared','CDBox.Common','CDBox.Wastewater','CDBox.RealEstate','CDBox')){[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory ($name+'.dll')))}
$core=[Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.dll'))
$shared=[Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.Shared.dll'))
$settings=[Activator]::CreateInstance($core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxStudioSettings'),$true)
$appearance=[Activator]::CreateInstance($shared.GetType('CDBox.Shared.UI.CDBoxPageAppearance'),$true)
function Build-Page($type,$arguments){$core.GetType($type,$true).GetMethod('BuildStandaloneDocument',$flags).Invoke($null,$arguments)}
$pages=@{}
$pages.dashboard=Build-Page 'CDBox.Wastewater.UI.WastewaterQuantityDashboardPage' @('light',$true,'')
$pages.attributes=Build-Page 'CDBox.Wastewater.UI.WastewaterQuantityAttributeEditorPage' @($appearance,'preview','A1')
$pages.annotations=Build-Page 'CDBox.Wastewater.UI.WastewaterAnnotationSettingsPage' @($appearance,$null,$null,$null)
$pages.section=Build-Page 'TCPipeAutoDraw.UI.Studio.CDBoxStudioSectionDrawingPage' @($settings,'')
$pages.longitudinal=Build-Page 'TCPipeAutoDraw.UI.Studio.CDBoxStudioLongitudinalProfileSettingsPage' @($settings)
$profiles='{"profiles":[{"id":"main","kind":"主管","title":"主管","fields":[{"key":"Material","label":"管材","type":"text","value":"HDPE"},{"key":"Diameter","label":"管径","type":"text","value":"DN300"},{"key":"TrenchWidth","label":"开挖宽度 m","type":"number","value":"0.8"},{"key":"Enabled","label":"参与工程量统计","type":"bool","value":"1"},{"key":"BackfillStructure","label":"回填结构层","type":"textarea","value":"原土回填 1.2\n砂垫层 0.2 锁定 垫层"}]},{"id":"branch","kind":"支管","title":"支管","fields":[{"key":"Material","label":"管材","type":"text","value":"UPVC"}]},{"id":"node","kind":"检查井","title":"检查井","fields":[{"key":"WellSpec","label":"井规格","type":"text","value":"1000"}]}]}'
$pages.defaults=$core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxStudioQuantityDefaultsPage').GetMethod('BuildDocument',$flags).Invoke($null,@($settings,'',$profiles,$profiles))
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$theme=$core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxWorkbenchAppearance').GetMethod('BuildThemeStyles',$flags,$null,@($settings.GetType()),$null).Invoke($null,@($settings))
foreach($name in $pages.Keys){
    $html=$pages[$name].Replace('</head>',"<style>$theme</style></head>")
    $html=$html.Insert($html.IndexOf('<script>'),'<script src="fixture.js"></script>')
    [IO.File]::WriteAllText((Join-Path $OutputDirectory ($name+'.html')),$html,(New-Object Text.UTF8Encoding($false)))
}
Copy-Item (Join-Path $PSScriptRoot 'WastewaterWorkbenchFixture.js') (Join-Path $OutputDirectory 'fixture.js')
Copy-Item (Join-Path $PSScriptRoot 'WastewaterWorkbenchPreview.html') (Join-Path $OutputDirectory 'index.html')
Write-Output "Generated six wastewater pages: $OutputDirectory"
