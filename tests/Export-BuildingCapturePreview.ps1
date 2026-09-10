param(
    [string]$BuildDirectory='D:/CDBox Studio Preview/bin/Release517/net48',
    [string]$OutputDirectory='D:/CDBox Studio Preview/artifacts/5.1.7/preview'
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions
foreach($name in @('AcDbMgd','AcMgd','AcCoreMgd')){[void][Reflection.Assembly]::LoadFrom("D:/Program Files/Autodesk/AutoCAD 2023/$name.dll")}
foreach($name in @('CDBox.Shared','CDBox.RealEstate','CDBox')){[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory ($name+'.dll')))}
$business=[Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.RealEstate.dll'))
$core=[Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory 'CDBox.dll'))
$serializer=New-Object Web.Script.Serialization.JavaScriptSerializer
function Plan-Rectangle([double]$width,[double]$height){
    $points=[CDBox.RealEstate.Geometry.BuildingPoint2[]]@(
        [CDBox.RealEstate.Geometry.BuildingPoint2]::new(0,0),
        [CDBox.RealEstate.Geometry.BuildingPoint2]::new($width,0),
        [CDBox.RealEstate.Geometry.BuildingPoint2]::new($width,$height),
        [CDBox.RealEstate.Geometry.BuildingPoint2]::new(0,$height))
    return [CDBox.RealEstate.Geometry.BuildingLengthAnnotationPlanner]::Create($points,.5,$true)
}
$components=[CDBox.RealEstate.Models.BuildingAreaComponent[]]@(
    [CDBox.RealEstate.Services.BuildingAreaService]::Component('A1','房屋',(Plan-Rectangle 10 10)),
    [CDBox.RealEstate.Services.BuildingAreaService]::Component('A2','中空',(Plan-Rectangle 2 5)),
    [CDBox.RealEstate.Services.BuildingAreaService]::Component('A3','半封',(Plan-Rectangle 4 2)))
$calculation=[CDBox.RealEstate.Services.BuildingAreaService]::Calculate('preview-drawing',$components)
$context=$serializer.Deserialize('{"DocumentName":"房屋面积示例.dwg","SelectedRecordId":"workbench-preview","Parcels":[{"RecordId":"workbench-preview","ParcelName":"示例宗地 01"},{"RecordId":"workbench-preview-02","ParcelName":"示例宗地 02"}]}',$business.GetType('CDBox.RealEstate.Models.BuildingCaptureContext'))
$context.Calculation=$calculation
$target=[CDBox.RealEstate.Models.BuildingCaptureTarget]::new();$target.RecordId='workbench-preview';$target.BuildingId='preview-f1';$target.BuildingNumber='F0001';$context.Buildings.Add($target)
$html=$core.GetType('CDBox.RealEstate.UI.BuildingCapturePage').GetMethod('BuildHtml').Invoke($null,@($context))
$bridge=@'
<script>window.previewMessages=[];window.previewErrors=[];window.addEventListener('error',e=>previewErrors.push(e.message));window.chrome={webview:{postMessage:m=>previewMessages.push(m)}};</script>
'@
$html=$html.Insert($html.IndexOf('<script>'),$bridge)
[void][IO.Directory]::CreateDirectory($OutputDirectory)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'capture.html'),$html,[Text.UTF8Encoding]::new($false))
$input=[CDBox.RealEstate.Models.BuildingCaptureInput]::new()
$input.Floor='一层';$building=[CDBox.RealEstate.Services.BuildingAreaService]::CreateBuilding($calculation,$input)
$building.Id='captured-1';$building.AddedRevision=1;$building.FloorAreas[0].Id='captured-floor-1';$building.FloorAreas[0].AddedRevision=1
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'building.json'),$serializer.Serialize($building),[Text.UTF8Encoding]::new($false))
$points=[CDBox.RealEstate.Geometry.BuildingPoint2[]]@(
    [CDBox.RealEstate.Geometry.BuildingPoint2]::new(0,0),[CDBox.RealEstate.Geometry.BuildingPoint2]::new(6,0),
    [CDBox.RealEstate.Geometry.BuildingPoint2]::new(6,1),[CDBox.RealEstate.Geometry.BuildingPoint2]::new(2,1),
    [CDBox.RealEstate.Geometry.BuildingPoint2]::new(2,5),[CDBox.RealEstate.Geometry.BuildingPoint2]::new(0,5))
$plan=[CDBox.RealEstate.Geometry.BuildingLengthAnnotationPlanner]::Create($points,.15,$false)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'geometry.json'),$serializer.Serialize($plan),[Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Export-ParcelWorkbenchPreview.ps1') -BuildDirectory $BuildDirectory -OutputDirectory (Join-Path $OutputDirectory 'parcel')
Write-Output "Generated capture and parcel previews in $OutputDirectory"
