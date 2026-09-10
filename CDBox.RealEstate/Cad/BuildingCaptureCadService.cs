using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CDBox.RealEstate.Geometry;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.Services;
using CDBox.RealEstate.Settings;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace CDBox.RealEstate.Cad
{
    internal sealed class BuildingCaptureCadService
    {
        private readonly ICDBoxPromptService _prompts;
        private readonly ICDBoxNotificationService _notifications;
        private readonly ICDBoxLogger _logger;
        private readonly ICDBoxPageService _pages;
        private readonly Action _openSurvey;
        private readonly ParcelSurveyStore _store = new ParcelSurveyStore();

        public BuildingCaptureCadService(ICDBoxPromptService prompts, ICDBoxNotificationService notifications,
            ICDBoxLogger logger, ICDBoxPageService pages, Action openSurvey)
        { _prompts = prompts; _notifications = notifications; _logger = logger; _pages = pages; _openSurvey = openSurvey; }

        public void Execute()
        {
            try
            {
                Document document = AcadApp.DocumentManager.MdiActiveDocument;
                if (document == null) throw new InvalidOperationException("当前没有可用的 CAD 图纸。");
                string documentId = ParcelSurveyCadScopeService.GetDocumentId(document);
                var scope = new ParcelSurveyCadScopeService(_notifications).CurrentContext(_store);
                if (scope.Parcels.Count == 0)
                    throw new InvalidOperationException("当前图纸尚无宗地，请先添加宗地后再添加房屋。");
                PromptSelectionResult selection;
                using (_prompts.Begin("添加房屋", "逐个选择或框选本次层次的房屋、中空与半封图形，Enter 完成；Esc 取消。"))
                    selection=document.Editor.GetSelection(new PromptSelectionOptions {
                        MessageForAdding="\n选择房屋层次图形（可点选或框选，Enter 完成）：", AllowDuplicates=false },
                        new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE,CIRCLE") }));
                if(selection.Status==PromptStatus.Cancel)return;
                if (selection.Status != PromptStatus.OK || selection.Value.Count == 0)
                    throw new InvalidOperationException("没有选中房屋闭合多段线或圆。");
                var settings = BuildingLengthAnnotationSettingsStore.Load();
                List<Source> sources;
                ObjectId spaceId = document.Database.CurrentSpaceId;
                using (document.LockDocument())
                    sources = selection.Value.GetObjectIds().Select(id => ReadSource(document.Database, id, settings)).ToList();
                var calculation = BuildingAreaService.Calculate(documentId,
                    sources.Select(s => BuildingAreaService.Component(s.Handle, s.Layer, s.Plan)));
                var context = new BuildingCaptureContext { DocumentName = scope.DocumentName,
                    SelectedRecordId = scope.RecordId, Parcels = scope.Parcels, Calculation = calculation,
                    Buildings=_store.GetBoundRecords(documentId).SelectMany(r=>r.Buildings.Select(b=>new BuildingCaptureTarget {
                        RecordId=r.Id,BuildingId=b.Id,BuildingNumber=b.Fields["building.number"].TextValue})).ToList() };
                bool committed = false, annotated = false;
                string addedRecordId = null;
                Action validate=()=>
                {
                    if (AcadApp.DocumentManager.MdiActiveDocument != document
                        || ParcelSurveyCadScopeService.GetDocumentId(document) != documentId)
                        throw new InvalidOperationException("当前图纸已切换或另存，请回到原图纸重新框选房屋。");
                    if (document.Database.CurrentSpaceId != spaceId)
                        throw new InvalidOperationException("当前绘图空间已切换，请重新框选房屋。");
                    foreach (var source in sources)
                    {
                        var latest=ReadSource(document.Database,source.Id,settings);
                        if(latest.Fingerprint!=source.Fingerprint)throw new InvalidOperationException("所选图形已修改，请重新选择，避免使用过期面积。");
                    }
                };
                Action annotate=()=>
                {
                    if(annotated)throw new InvalidOperationException("本次选择已生成注记。");
                    using(document.LockDocument())
                    {
                        validate();
                        using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                        {
                            foreach (var source in sources.Where(s=>!calculation.ExcludedHollowHandles.Contains(s.Handle)))
                                BuildingLengthAnnotationCadService.AppendPlan(document.Database, transaction,
                                    source.Plan, source.Elevation, settings);
                            transaction.Commit();
                        }
                    }
                    annotated=true;
                };
                Func<BuildingCaptureInput, ParcelSurveyRecord> commit = input =>
                {
                    if (committed) throw new InvalidOperationException("本次层次已添加，请重新选择下一条。");
                    var building = BuildingAreaService.CreateBuilding(calculation, input);
                    ParcelSurveyRecord saved;
                    using (document.LockDocument())
                    {
                        validate();
                        saved=_store.AddCapturedBuilding(documentId,input.RecordId,building,null,input.TargetBuildingId);
                    }
                    committed = true;
                    addedRecordId = saved.Id;
                    // Deliver only additive building changes; keep other in-progress survey fields intact.
                    try
                    {
                        string json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(saved)
                            .Replace("<", "\\u003c").Replace(">", "\\u003e");
                        _pages.TryExecuteScript("realestate-parcel-survey-editor",
                            "window.CDBoxParcelBuildingsChanged && window.CDBoxParcelBuildingsChanged(" + json + ");");
                    }
                    catch (Exception ex)
                    {
                        // Persistence and CAD have committed; a closed WebView must not turn success into failure.
                        _logger.Error("房屋已添加，编辑器即时刷新失败；重新打开时将读取已保存记录。", ex);
                    }
                    return saved;
                };
                _pages.Show(CDBoxUiGateway.Call<CDBoxPageDefinition>("realestate.building-capture", "Create",
                    context, commit, annotate, (Action)Execute, (Action)(() => {
                        if (!string.IsNullOrWhiteSpace(addedRecordId)) _store.SelectRecord(documentId, addedRecordId);
                        _openSurvey();
                    })));
            }
            catch (Exception ex)
            {
                _logger.Error("添加房屋失败。", ex);
                _notifications.Show("添加房屋", "添加房屋失败：" + ex.Message, CDBoxNotificationLevel.Error);
            }
        }

        private static Source ReadSource(Database database, ObjectId id, BuildingLengthAnnotationSettings settings)
        {
            BuildingAnnotationPlan plan; double elevation;
            BuildingLengthAnnotationCadService.ReadPlan(database, id, settings, out plan, out elevation);
            using (var tr = database.TransactionManager.StartOpenCloseTransaction())
            {
                var polyline = (Entity)tr.GetObject(id, OpenMode.ForRead);
                string points = string.Join(";", plan.Boundary.Select(s => s.Start.X.ToString("R", CultureInfo.InvariantCulture)
                    + "," + s.Start.Y.ToString("R", CultureInfo.InvariantCulture)+","+s.Bulge.ToString("R",CultureInfo.InvariantCulture)));
                return new Source { Id = id, Handle = id.Handle.ToString(), Layer = polyline.Layer,
                    Plan = plan, Elevation = elevation, Fingerprint = polyline.Layer + "|" + elevation.ToString("R", CultureInfo.InvariantCulture) + "|" + points };
            }
        }
        private sealed class Source
        {
            public ObjectId Id;
            public string Handle, Layer, Fingerprint;
            public double Elevation;
            public BuildingAnnotationPlan Plan;
        }
    }
}
