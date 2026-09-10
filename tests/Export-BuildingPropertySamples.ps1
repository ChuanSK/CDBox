param([string]$BuildDirectory='D:/CDBox Studio Preview/bin/Release518/net48',[string]$OutputDirectory='D:/CDBox Studio Preview/artifacts/5.1.8/documents')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions
foreach($name in @('AcDbMgd','AcMgd','AcCoreMgd')){[void][Reflection.Assembly]::LoadFrom("D:/Program Files/Autodesk/AutoCAD 2023/$name.dll")}
foreach($name in @('CDBox.Shared','CDBox.RealEstate')){[void][Reflection.Assembly]::LoadFrom((Join-Path $BuildDirectory ($name+'.dll')))}
$test=[Reflection.Assembly]::LoadFrom('D:/CDBox Studio Preview/artifacts/5.1.8/tests/CDBox.CoreTests.exe')
$serializer=[Web.Script.Serialization.JavaScriptSerializer]::new();$serializer.MaxJsonLength=16777216
$example=$test.GetType('CDBox.CoreTests.BuildingPropertyFeatureTests').GetMethod('Example').Invoke($null,@())
$record=$serializer.Deserialize($serializer.Serialize($example),[CDBox.RealEstate.Models.ParcelSurveyRecord])
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$record.Field('project.rightsSurveyor').TextValue='调查员示例'
$record.Field('project.rightsSurveyDate').TextValue='2026-09-09'
$record.Field('project.surveyDate').TextValue='2026-09-09'
[CDBox.RealEstate.Services.ParcelSurveyWordExporter]::Export((Join-Path $BuildDirectory 'Templates/地籍调查表.docx'),(Join-Path $OutputDirectory '一幢调查表.docx'),$record)
[CDBox.RealEstate.Services.BuildingPropertyWordExporter]::Export((Join-Path $OutputDirectory '李四房产.docx'),$record)
$arc=[CDBox.RealEstate.Geometry.BuildingCurvedBoundaryPlanner]::Create([CDBox.RealEstate.Geometry.BuildingPoint2[]]@([CDBox.RealEstate.Geometry.BuildingPoint2]::new(-10,0),[CDBox.RealEstate.Geometry.BuildingPoint2]::new(10,0)),[double[]]@(1,1),.3,$false)
$curve=[CDBox.RealEstate.Services.BuildingAreaService]::Calculate('qa-doc',[CDBox.RealEstate.Models.BuildingAreaComponent[]]@([CDBox.RealEstate.Services.BuildingAreaService]::Component('arc','房屋',$arc)))
$curved=[CDBox.RealEstate.Models.ParcelSurveyRecord]::new();$curved.ScopeType='parcel';$curved.ParcelId='arc-parcel';$curved.ParcelName='圆弧示例';$curved.Normalize();$curved.Field('rights.ownerName').TextValue='圆弧示例';$curved.Field('house.followParcelOwner').BooleanValue=$true
$input=[CDBox.RealEstate.Models.BuildingCaptureInput]::new();$input.BuildingNumber='1幢';$input.Floor='一层';$input.Structure='砖混'
$curved.Buildings.Add([CDBox.RealEstate.Services.BuildingAreaService]::CreateBuilding($curve,$input))
[CDBox.RealEstate.Services.BuildingPropertyWordExporter]::Export((Join-Path $OutputDirectory '圆弧示例房产.docx'),$curved)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'arc-geometry.json'),$serializer.Serialize($arc),[Text.UTF8Encoding]::new($false))
$points=[CDBox.RealEstate.Geometry.BuildingPoint2[]]@([CDBox.RealEstate.Geometry.BuildingPoint2]::new(0,0),[CDBox.RealEstate.Geometry.BuildingPoint2]::new(6,0),[CDBox.RealEstate.Geometry.BuildingPoint2]::new(6,1),[CDBox.RealEstate.Geometry.BuildingPoint2]::new(2,1),[CDBox.RealEstate.Geometry.BuildingPoint2]::new(2,5),[CDBox.RealEstate.Geometry.BuildingPoint2]::new(0,5))
$plan=[CDBox.RealEstate.Geometry.BuildingLengthAnnotationPlanner]::Create($points,.15,$false)
$calc=[CDBox.RealEstate.Services.BuildingAreaService]::Calculate('qa-doc',[CDBox.RealEstate.Models.BuildingAreaComponent[]]@([CDBox.RealEstate.Services.BuildingAreaService]::Component('L','房屋',$plan)))
for($i=2;$i -le 4;$i++){
 $input.BuildingNumber="$i`幢";$input.Floor='一层';$building=[CDBox.RealEstate.Services.BuildingAreaService]::CreateBuilding($calc,$input)
 $building.Fields['building.notes'].TextValue="第 $i 幢调查说明";$building.Fields['building.reviewOpinion'].TextValue="第 $i 幢审核意见"
 $record.Buildings.Add($building)
}
$record.Field('project.rightsSurveyor').TextValue='调查员示例'
$record.Field('project.surveyDate').TextValue='2026-09-09'
$record.Field('project.rightsSurveyDate').TextValue='2026-09-09'
[CDBox.RealEstate.Services.BuildingAreaService]::Synchronize($record)
[CDBox.RealEstate.Services.ParcelSurveyWordExporter]::Export((Join-Path $BuildDirectory 'Templates/地籍调查表.docx'),(Join-Path $OutputDirectory '四幢调查表.docx'),$record)
for($i=0;$i -lt 12;$i++){
 $floor=[CDBox.RealEstate.Models.BuildingFloorArea]::new();$floor.Name='二至三层通道';$floor.Count=2;$floor.Calculation=$calc;$record.Buildings[1].FloorAreas.Add($floor)
}
$record.ParcelName='多层次续页示例'
[CDBox.RealEstate.Services.BuildingPropertyWordExporter]::Export((Join-Path $OutputDirectory '多层次续页示例房产.docx'),$record)
$many=$serializer.Deserialize($serializer.Serialize($record),[CDBox.RealEstate.Models.ParcelSurveyRecord])
$many.Buildings.Clear()
for($i=1;$i -le 20;$i++){
 $input.BuildingNumber="$i`幢";$input.Floor='一层';$many.Buildings.Add([CDBox.RealEstate.Services.BuildingAreaService]::CreateBuilding($calc,$input))
}
[CDBox.RealEstate.Services.ParcelSurveyWordExporter]::Export((Join-Path $BuildDirectory 'Templates/地籍调查表.docx'),(Join-Path $OutputDirectory '二十幢调查表.docx'),$many)
$many.Buildings.Clear();$many.Field('project.rightsSurveyDate').TextValue='';$many.Field('project.rightsSurveyor').TextValue=''
[CDBox.RealEstate.Services.ParcelSurveyWordExporter]::Export((Join-Path $BuildDirectory 'Templates/地籍调查表.docx'),(Join-Path $OutputDirectory '无房屋空日期调查表.docx'),$many)
$uPoints=[CDBox.RealEstate.Geometry.BuildingPoint2[]]@(@(0,0),@(21.79,0),@(21.79,13.21),@(14.78,13.21),@(14.78,8.45),@(6.79,8.45),@(6.79,13.21),@(0,13.21) | ForEach-Object {[CDBox.RealEstate.Geometry.BuildingPoint2]::new($_[0],$_[1])})
$uPlan=[CDBox.RealEstate.Geometry.BuildingLengthAnnotationPlanner]::Create($uPoints,.35,$false)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'rectangle-geometry.json'),$serializer.Serialize($uPlan),[Text.UTF8Encoding]::new($false))
$uCalc=[CDBox.RealEstate.Services.BuildingAreaService]::Calculate('qa-doc',[CDBox.RealEstate.Models.BuildingAreaComponent[]]@([CDBox.RealEstate.Services.BuildingAreaService]::Component('U','房屋',$uPlan)))
$many.ParcelName='矩形分割示例';$input.BuildingNumber='1幢';$many.Buildings.Add([CDBox.RealEstate.Services.BuildingAreaService]::CreateBuilding($uCalc,$input))
[CDBox.RealEstate.Services.BuildingPropertyWordExporter]::Export((Join-Path $OutputDirectory '矩形分割示例房产.docx'),$many)
Write-Output "Exported survey and geometry samples through the built business assembly: $OutputDirectory"
