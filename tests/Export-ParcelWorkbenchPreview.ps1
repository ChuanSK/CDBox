param(
    [string]$BuildDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\parcel-workbench\build'),
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\parcel-workbench\preview')
)
# Run with Windows PowerShell (the production assemblies target .NET Framework 4.8).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Web.Extensions
[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.Shared.dll'))
$business = [Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.RealEstate.dll'))
$core = [Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.dll'))
$serializer = New-Object Web.Script.Serialization.JavaScriptSerializer
$recordType = $business.GetType('CDBox.RealEstate.Models.ParcelSurveyRecord', $true)
$scopeType = $business.GetType('CDBox.RealEstate.Models.ParcelSurveyScopeContext', $true)
$validationType = $business.GetType('CDBox.RealEstate.Models.ParcelSurveyValidationResult', $true)
$record = $serializer.Deserialize(@'
{"Id":"workbench-preview","DocumentId":"preview-drawing","DocumentName":"宗地调查示例.dwg","ScopeType":"parcel","ParcelName":"示例宗地 01","Fields":{
"project.name":{"TextValue":"不动产权籍调查项目 · 示例","Status":2},
"project.organization":{"TextValue":"示例测绘院","Status":1},
"project.surveyDate":{"TextValue":"2026-09-07","Status":3},
"project.formFiller":{"TextValue":"调查员甲","Status":3},
"project.formDate":{"TextValue":"2026-09-07","Status":3},
"project.rightsSurveyor":{"TextValue":"调查员乙","Status":3},
"project.rightsSurveyDate":{"TextValue":"2026-09-06","Status":2},
"project.surveyor":{"TextValue":"测量员甲","Status":3},
"project.measureDate":{"TextValue":"2026-09-06","Status":2},
"project.reviewer":{"TextValue":"审核员甲","Status":3},
"project.reviewDate":{"TextValue":"2026-09-07","Status":3},
"parcel.location":{"TextValue":"示例村一组（预览数据）","Status":3},
"parcel.mapScale":{"TextValue":"1:500","Status":1}},
"Boundary":{"ParcelBoundaryClosed":true,"Points":[
{"PointNumber":"J1","X":3500000.12,"Y":500000.34,"MarkerType":"钢钉"},
{"PointNumber":"J2","X":3500020.12,"Y":500000.34,"MarkerType":"钢钉"},
{"PointNumber":"J3","X":3500020.12,"Y":500020.34,"MarkerType":"喷涂"},
{"PointNumber":"J4","X":3500000.12,"Y":500020.34,"MarkerType":"喷涂"}],
"Segments":[{"StartPointNumber":"J1","EndPointNumber":"J2","Distance":20,"LineCategory":"围墙","LinePosition":"中","Direction":"北","Description":"沿现状围墙中心线"}],
"SignatureGroups":[{"StartPointNumber":"J1","EndPointNumber":"J2","NeighborOwner":"示例邻宗","NeighborRepresentative":"指界员甲","ParcelRepresentative":"指界员乙","ConfirmationDate":"2026-09-07"}]},
"Buildings":[
{"Id":"preview-f1","Fields":{"building.number":{"TextValue":"F0001","Status":3},"building.householdNumber":{"TextValue":"0001","Status":3},"building.totalFloors":{"NumericValue":1,"Status":3},"building.floor":{"TextValue":"1","Status":3},"building.structure":{"TextValue":"砖混","Status":3},"building.area":{"NumericValue":60.10,"Status":0}}},
{"Id":"preview-f2","Fields":{"building.number":{"TextValue":"F0002","Status":3},"building.area":{"NumericValue":84.26,"Status":0}}},
{"Id":"preview-f3","Fields":{"building.number":{"TextValue":"F0003","Status":3},"building.area":{"NumericValue":112.30,"Status":0}}}]}
'@, $recordType)
$scope = $serializer.Deserialize(@'
{"DocumentId":"preview-drawing","ScopeType":"parcel","RecordId":"workbench-preview","ParcelName":"示例宗地 01","BindingValid":true,"Parcels":[{"RecordId":"workbench-preview","ParcelName":"示例宗地 01","BoundaryValid":true},{"RecordId":"workbench-preview-02","ParcelName":"示例宗地 02","BoundaryValid":true}]}
'@, $scopeType)
$pageType = $core.GetType('CDBox.RealEstate.UI.ParcelSurveyEditorPage', $true)
$method = $pageType.GetMethod('BuildWithAppearance', [Reflection.BindingFlags]'Static,NonPublic')
$appearance = [Activator]::CreateInstance($core.GetType('TCPipeAutoDraw.UI.Studio.CDBoxStudioSettings'),$true)
$html = $method.Invoke($null, @($record, $scope, $appearance))
# The preview captures messages; it cannot call CAD, save business data or export files.
$bridge = @'
<script>
window.previewMessages=[];window.previewErrors=[];window.previewSaveDelay=30;window.previewSaveFails=false;window.previewSavedRecords={};
window.addEventListener('error',function(e){window.previewErrors.push(e.message);});
window.chrome=window.chrome||{};window.chrome.webview={postMessage:function(message){window.previewMessages.push(message);if(message.indexOf('studio|autoSaveParcel|')===0){var payload=JSON.parse(decodeURIComponent(message.split('|')[2])),failed=window.previewSaveFails;setTimeout(function(){if(!failed)window.previewSavedRecords[payload.Record.Id]=payload.Record;window.CDBoxParcelAutoSaved(payload.RequestId,!failed,failed?'模拟保存失败':'');},window.previewSaveDelay);}}};
try{localStorage.removeItem('cdbox.parcel-editor.view.workbench-preview');localStorage.setItem('cdbox.parcel-editor.sidebar-visible','1');}catch(e){}
</script>
'@
$html = $html.Replace('<script>window.CDBoxParcelSurveyContext=', $bridge + '<script>window.CDBoxParcelSurveyContext=')
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$utf8 = New-Object Text.UTF8Encoding($false)
foreach ($theme in @('light', 'dark')) {
    $themed = $html -replace 'data-theme="(light|dark)"', ('data-theme="' + $theme + '"')
    [IO.File]::WriteAllText((Join-Path $OutputDirectory ($theme + '.html')), $themed, $utf8)
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ParcelWorkbenchPreview.html') -Destination (Join-Path $OutputDirectory 'index.html')
Write-Output "Preview generated from built CDBox.dll: $OutputDirectory"
