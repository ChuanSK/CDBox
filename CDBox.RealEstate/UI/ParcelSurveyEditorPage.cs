using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using CDBox.RealEstate.Cad;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.Services;
using CDBox.RealEstate.Settings;
using CDBox.Shared.UI;

namespace CDBox.RealEstate.UI
{
    public static class ParcelSurveyEditorPage
    {
        public const string PageId = "realestate-parcel-survey-editor";
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        internal static CDBoxPageDefinition Create(ParcelSurveyStore store,
            ParcelBoundaryCadService cad, ParcelSurveyCadScopeService scopes,
            ParcelSurveyCadWorkflowService workflow)
        {
            if (store == null) throw new ArgumentNullException("store");
            if (cad == null) throw new ArgumentNullException("cad");
            if (scopes == null) throw new ArgumentNullException("scopes");
            if (workflow == null) throw new ArgumentNullException("workflow");
            return new CDBoxPageDefinition(PageId, "宗地调查数据编辑器",
                delegate
                {
                    ParcelSurveyScopeContext scope = scopes.CurrentContext(
                        store);
                    ParcelSurveyRecord record = store.GetRecord(
                        scope.RecordId) ?? new ParcelSurveyRecord
                        {
                            DocumentId = scope.DocumentId,
                            DocumentName = scope.DocumentName,
                            ScopeType = "whole"
                        };
                    return BuildHtml(record,
                        ParcelSurveyValidator.Validate(record), scope);
                },
                delegate(CDBoxPageRouteRequest request)
                {
                    return Route(request, store, cad, scopes, workflow);
                })
            {
                Width = 1380,
                Height = 900,
                MinimumWidth = 1040,
                MinimumHeight = 700
            };
        }

        public static string BuildHtml(ParcelSurveyRecord record,
            ParcelSurveyValidationResult validation)
        {
            return BuildHtml(record, validation,
                BuildFallbackScope(record));
        }

        public static string BuildHtml(ParcelSurveyRecord record,
            ParcelSurveyValidationResult validation,
            ParcelSurveyScopeContext scope)
        {
            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            validation = validation ?? ParcelSurveyValidator.Validate(record);
            var context = new PageContext
            {
                Record = record,
                Validation = validation,
                Scope = scope ?? BuildFallbackScope(record),
                Fields = ParcelSurveyFieldCatalog.Fields,
                BuildingFields = ParcelSurveyFieldCatalog.BuildingFields,
                Statuses = new List<StatusOption>
                {
                    new StatusOption(0, "自动"),
                    new StatusOption(1, "默认"),
                    new StatusOption(2, "导入"),
                    new StatusOption(3, "人工"),
                    new StatusOption(4, "不适用")
                },
                MarkerTypes = new[] { "钢钉", "水泥桩", "石灰桩", "喷涂", "木桩", "其他" },
                LineCategories = new[] { "围墙", "墙壁", "门墩", "道路", "田埂", "沟渠", "铁丝网", "界址线", "其他" },
                LinePositions = new[] { "内", "中", "外", "待确认" }
            };

            var html = new StringBuilder(70000);
            html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
                .Append("<title>宗地调查数据编辑器</title><style>")
                .Append(@"
:root{--bg:#f5f7fb;--panel:#fff;--panel2:#f8fafc;--text:#172033;--muted:#69768a;--line:#e1e7f0;--brand:#316fe8;--brand2:#7047eb;--ok:#087f5b;--danger:#b42318;--auto:#e8f2ff;--default:#eeeafe;--import:#e7f8ef;--manual:#fff4d8;--na:#edf0f4;--shadow:0 16px 38px rgba(30,41,59,.08)}
*{box-sizing:border-box}html,body{height:100%;margin:0;overflow:hidden;background:var(--bg);color:var(--text);font-family:'Microsoft YaHei UI','Segoe UI',sans-serif}button,input,select,textarea{font:inherit}.shell{height:100vh;display:grid;grid-template-columns:264px minmax(0,1fr);grid-template-rows:116px minmax(0,1fr);grid-template-areas:'top top' 'sidebar workspace';background:radial-gradient(circle at 78% -10%,rgba(49,111,232,.12),transparent 32%),var(--bg)}.top{grid-area:top;display:grid;grid-template-columns:minmax(286px,1fr) auto;gap:24px;padding:17px 26px 15px;border-bottom:1px solid var(--line);background:rgba(255,255,255,.96)}.top-left,.top-right{display:flex;min-width:0;flex-direction:column;justify-content:space-between}.editor-title{margin:0;font-size:21px;line-height:1.25}.parcel-picker{display:flex;align-items:center;gap:10px;min-width:0}.parcel-picker strong{flex:0 0 auto;font-size:12px}.parcel-picker select{width:min(236px,100%);height:34px;padding:0 10px;border:1px solid var(--line);border-radius:8px;background:#fff;color:var(--text);outline:0}.top-right{align-items:flex-end}.actions,.scope-actions{display:flex;align-items:center;justify-content:flex-end;gap:8px}.scope-actions{flex-wrap:wrap}.scope-actions .mini{height:32px;padding-inline:11px}.sidebar{grid-area:sidebar;display:flex;min-height:0;flex-direction:column;padding:18px 16px;border-right:1px solid var(--line);background:rgba(255,255,255,.92)}.nav{display:flex;flex:1;min-height:0;flex-direction:column;gap:7px;overflow:auto;padding:2px}.nav-item{width:100%;padding:12px;border:0;border-radius:13px;background:transparent;color:var(--muted);font-size:13px;font-weight:750;text-align:left;cursor:pointer;transition:.18s}.nav-item:hover{background:var(--panel2);color:var(--text);transform:translateX(2px)}.nav-item.active{color:var(--brand);background:linear-gradient(135deg,rgba(49,111,232,.12),rgba(112,71,235,.09));box-shadow:inset 0 0 0 1px rgba(49,111,232,.10)}.workspace{grid-area:workspace;display:flex;min-width:0;min-height:0;flex-direction:column}.btn{height:38px;padding:0 15px;border:1px solid var(--line);border-radius:10px;background:#fff;color:var(--text);font-weight:800;cursor:pointer}.btn:hover,.mini:hover{border-color:var(--brand);color:var(--brand)}.btn.primary{border:0;color:#fff;background:linear-gradient(135deg,var(--brand),var(--brand2))}.btn:disabled,.mini:disabled{opacity:.42;cursor:not-allowed}.export-menu{position:relative}.export-options{position:absolute;right:0;top:calc(100% + 8px);z-index:40;width:224px;padding:6px;border:1px solid var(--line);border-radius:12px;background:#fff;box-shadow:0 18px 44px rgba(15,23,42,.18)}.export-options[hidden]{display:none}.export-option{display:flex;width:100%;align-items:center;justify-content:space-between;gap:12px;padding:10px 11px;border:0;border-radius:8px;background:transparent;color:var(--text);font-weight:750;text-align:left;cursor:pointer}.export-option:hover{background:var(--panel2);color:var(--brand)}.export-option small{color:var(--muted);font-size:9px;font-weight:650}.content{flex:1;min-height:0;overflow:auto;padding:20px 24px 50px}.badge,.status-select{border:1px solid transparent;border-radius:999px;font-size:10px;font-weight:850}.badge{padding:4px 8px}.s0{background:var(--auto);color:#245da9}.s1{background:var(--default);color:#5b3cb0}.s2{background:var(--import);color:#087047}.s3{background:var(--manual);color:#8a5a00}.s4{background:var(--na);color:#596477}.group{max-width:1460px;margin:0 auto 16px;border:1px solid var(--line);border-radius:17px;background:var(--panel);box-shadow:var(--shadow);overflow:hidden}.group-head{display:flex;justify-content:space-between;align-items:center;padding:14px 16px;border-bottom:1px solid var(--line);background:linear-gradient(90deg,var(--panel2),#fff)}.group-head strong{font-size:14px}.field-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;padding:14px}.field-card{min-width:0;padding:10px;border:1px solid #e2e8f1;border-radius:11px;background:#fff}.field-card.wide{grid-column:span 2}.field-card.full{grid-column:1/-1}.field-card.conditional{border-left:3px solid #b9a7f5}.field-head{display:flex;align-items:center;justify-content:space-between;gap:8px;margin-bottom:7px}.field-label{font-size:11px;font-weight:850}.required{color:var(--danger);margin-left:3px}.status-select{width:66px;height:24px;padding:0 5px;outline:0}.control{width:100%;height:36px;border:1px solid var(--line);border-radius:8px;background:#fff;color:var(--text);padding:0 9px;outline:0}.control:focus{border-color:var(--brand);box-shadow:0 0 0 3px rgba(49,111,232,.09)}textarea.control{height:72px;padding:8px 9px;resize:vertical;line-height:1.55}select[multiple].control{height:66px;padding:4px}.check-control{height:36px;display:flex;align-items:center;gap:8px}.na-note{display:none;height:36px;align-items:center;color:#596477;font-size:11px}.field-card.na .control,.field-card.na .check-control{display:none}.field-card.na .na-note{display:flex}.table-wrap{overflow:auto}.data-table{width:100%;min-width:1040px;border-collapse:collapse}.data-table th,.data-table td{padding:7px 6px;border-bottom:1px solid #e4e9f1;text-align:left;vertical-align:top}.data-table th{position:sticky;top:0;background:#f6f8fc;color:#53627a;font-size:9px;white-space:nowrap}.data-table td{font-size:10px}.cell{width:100%;min-width:70px;height:31px;padding:0 7px;border:1px solid var(--line);border-radius:7px;background:#fff}.cell.small{min-width:48px}.cell.long{min-width:150px}.cell:read-only{background:#f0f3f7;color:#657287}.mini{height:29px;padding:0 9px;border:1px solid var(--line);border-radius:8px;background:#fff;color:var(--brand);cursor:pointer}.mini.danger{color:var(--danger)}.section-tools{display:flex;align-items:center;justify-content:flex-end;gap:8px;padding:11px 14px}.section-tools .closed{margin-right:auto}.closed{display:flex;align-items:center;gap:7px;font-size:11px;font-weight:750}.building{margin:14px;border:1px solid #dfe6f1;border-radius:12px;overflow:hidden}.building-head{display:flex;justify-content:space-between;align-items:center;padding:10px 12px;background:#f6f8fc}.building-head strong{font-size:12px}.empty{padding:42px;text-align:center;color:var(--muted);font-size:12px}.toast{position:fixed;right:22px;bottom:20px;z-index:50;max-width:440px;padding:11px 14px;border-radius:10px;background:#172033;color:#fff;box-shadow:0 16px 38px rgba(15,23,42,.28)}.toast.success{background:var(--ok)}.toast.error{background:var(--danger)}
.modal-mask{position:fixed;inset:0;z-index:45;display:grid;place-items:center;padding:24px;background:rgba(16,26,45,.48);backdrop-filter:blur(3px)}.modal{width:min(660px,100%);max-height:calc(100vh - 48px);overflow:auto;border:1px solid #d9e2ef;border-radius:16px;background:#fff;box-shadow:0 30px 80px rgba(15,23,42,.28)}.modal-head{display:flex;justify-content:space-between;align-items:flex-start;gap:16px;padding:16px 18px;border-bottom:1px solid var(--line);background:linear-gradient(90deg,#f4f7ff,#fff)}.modal-head h2{margin:0;font-size:17px}.modal-head p{margin:5px 0 0;color:var(--muted);font-size:10px;line-height:1.55}.modal-body{padding:16px 18px}.modal-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.modal-field{display:grid;gap:6px}.modal-field.full{grid-column:1/-1}.modal-field>span{font-size:11px;font-weight:850}.modal-foot{display:flex;justify-content:flex-end;gap:8px;padding:12px 18px;border-top:1px solid var(--line);background:#fafbfe}.choice-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:10px}.choice{display:flex;align-items:center;gap:9px;padding:14px;border:1px solid var(--line);border-radius:10px;cursor:pointer}.choice:has(input:checked){border-color:var(--brand);background:#edf4ff}.range-summary{margin-bottom:13px;padding:10px 12px;border:1px solid #cadcff;border-radius:10px;background:#edf4ff;color:#34547f;font-size:11px}.source-meta{display:flex;flex-wrap:wrap;gap:6px;margin-top:8px}.source-meta span{padding:4px 7px;border-radius:999px;background:#eef2f7;color:#5e6a7d;font-size:9px}
textarea.control{min-width:100%;resize:both}
@media(max-width:1180px){.shell{grid-template-columns:224px minmax(0,1fr);grid-template-rows:124px minmax(0,1fr)}.top{grid-template-columns:minmax(232px,1fr) auto;padding-inline:18px}.scope-actions{gap:6px}.scope-actions .mini{padding-inline:8px}.field-grid{grid-template-columns:repeat(2,minmax(0,1fr))}.content{padding-inline:14px}}@media(max-width:760px){.shell{grid-template-columns:190px minmax(0,1fr);grid-template-rows:176px minmax(0,1fr)}.top{grid-template-columns:1fr;gap:10px;padding:12px 14px}.top-right{align-items:flex-start;gap:8px}.actions,.scope-actions{justify-content:flex-start}.field-grid,.modal-grid{grid-template-columns:1fr}.field-card.wide,.modal-field.full{grid-column:auto}}
")
                .Append("</style></head><body><main class=\"shell page\" data-page=\"")
                .Append(PageId).Append("\"><header class=\"top\"><div class=\"top-left\"><h1 class=\"editor-title\">宗地调查数据编辑器</h1><label class=\"parcel-picker\"><strong>当前宗地</strong><select id=\"scopeRegion\"></select></label></div>")
                .Append("<div class=\"top-right\"><div class=\"actions\"><button id=\"saveParcel\" class=\"btn\" type=\"button\">保存宗地信息</button><button id=\"checkData\" class=\"btn\" type=\"button\">检查数据</button><div id=\"exportMenu\" class=\"export-menu\"><button id=\"exportSurvey\" class=\"btn primary\" type=\"button\" aria-haspopup=\"menu\" aria-expanded=\"false\">导出调查表 ▾</button><div id=\"exportOptions\" class=\"export-options\" role=\"menu\" hidden><button class=\"export-option\" data-export-format=\"word\" role=\"menuitem\" type=\"button\"><span>导出为 Word 文档</span></button><button class=\"export-option\" data-export-format=\"excel\" role=\"menuitem\" type=\"button\"><span>导出为 Excel 表格</span></button><button class=\"export-option\" data-export-format=\"checks\" role=\"menuitem\" type=\"button\"><span>导出四张检查表</span></button></div></div></div>")
                .Append("<div class=\"scope-actions\"><button class=\"mini\" data-scope-action=\"newparcel\" type=\"button\">权属线建宗</button><button class=\"mini\" data-scope-action=\"create\" type=\"button\">区域建宗</button><button class=\"mini\" data-scope-action=\"bind\" type=\"button\">选择区域建宗</button><button class=\"mini\" data-scope-bound-only=\"1\" data-scope-action=\"rename\" type=\"button\">重命名宗地</button><button class=\"mini danger\" data-scope-bound-only=\"1\" data-scope-action=\"delete\" type=\"button\">删除宗地信息</button></div></div></header>")
                .Append("<aside class=\"sidebar\"><nav id=\"tabs\" class=\"nav\"></nav></aside><section class=\"workspace\"><section id=\"content\" class=\"content\"></section></section></main>")
                .Append("<script>window.CDBoxParcelSurveyContext=")
                .Append(SafeJson(context)).Append(";</script><script>")
                .Append(BuildScript())
                .Append("</script></body></html>");
            return html.ToString();
        }

        private static CDBoxPageRouteResult Route(CDBoxPageRouteRequest request,
            ParcelSurveyStore store, ParcelBoundaryCadService cad,
            ParcelSurveyCadScopeService scopes,
            ParcelSurveyCadWorkflowService workflow)
        {
            var result = new CDBoxPageRouteResult { Handled = true };
            if (request == null) return result;
            string name = (request.Name ?? string.Empty).Trim().ToLowerInvariant();
            if (name == "ready") return result;
            try
            {
                if (name == "pollparcelscope")
                {
                    ParcelSurveyScopeActionRequest poll = Serializer
                        .Deserialize<ParcelSurveyScopeActionRequest>(
                            request.Argument ?? string.Empty)
                        ?? new ParcelSurveyScopeActionRequest();
                    ParcelSurveyScopeContext current = scopes.CurrentContext(
                        store);
                    string expected = (poll.ScopeToken ?? string.Empty).Trim();
                    string actual = ScopeToken(current);
                    if (!string.Equals(expected, actual,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (IsBound(poll.Record))
                            store.SavePreservingCurrentScope(poll.Record);
                        result.RefreshPage = true;
                    }
                    return result;
                }
                if (name == "switchparcelscope"
                    || name == "newparcel"
                    || name == "createparcelregion"
                    || name == "bindparcelregion"
                    || name == "renameparcelinfo"
                    || name == "deleteparcelinfo")
                    return RouteScopeAction(name, request, store, scopes,
                        workflow);

                ParcelSurveyRecord record = Serializer.Deserialize<ParcelSurveyRecord>(
                    request.Argument ?? string.Empty) ?? new ParcelSurveyRecord();
                record.Normalize();
                if (name == "cadrecognizeboundary")
                {
                    if (IsBound(record)) store.Save(record);
                    ParcelSurveyRecord selected = workflow.SelectParcel(
                        IsBound(record) ? record : null);
                    result.ExecuteScript =
                        "window.CDBoxParcelScopeInteractionFinished && window.CDBoxParcelScopeInteractionFinished();";
                    result.RefreshPage = selected != null;
                    return result;
                }
                if (name == "cadselectboundarysegment")
                {
                    IList<ParcelBoundarySegmentRecord> segments =
                        cad.SelectBoundarySegmentsContinuously(record);
                    result.ExecuteScript =
                        "window.CDBoxParcelCadInteractionFinished && window.CDBoxParcelCadInteractionFinished('segment',"
                        + SafeJson(segments) + ");";
                    return result;
                }
                if (name == "cadselectsignaturegroup")
                {
                    IList<ParcelBoundarySignatureGroupRecord> groups =
                        cad.SelectSignatureGroupsContinuously(record);
                    result.ExecuteScript =
                        "window.CDBoxParcelCadInteractionFinished && window.CDBoxParcelCadInteractionFinished('signature',"
                        + SafeJson(groups) + ");";
                    return result;
                }
                if (name == "generateboundarydescriptions"
                    || name == "autogenerateboundarydescriptions")
                {
                    bool overwriteManual = name ==
                        "generateboundarydescriptions";
                    ParcelBoundaryDescriptionGenerator.Apply(record,
                        overwriteManual);
                    result.ExecuteScript =
                        "window.CDBoxParcelDescriptionsGenerated && window.CDBoxParcelDescriptionsGenerated("
                        + SafeJson(record) + ");";
                    result.ToastKind = "success";
                    result.ToastMessage = overwriteManual
                        ? "已重新生成界址点位、界址线走向和宗地四至。"
                        : "已根据最新界址数据更新自动说明；人工文字保持不变。";
                    return result;
                }
                if (name == "saveparcel")
                {
                    if (!IsBound(record)) throw new InvalidOperationException(
                        "请先通过区域或权属线新建并选择宗地。");
                    store.Save(record);
                    result.RefreshPage = true;
                    result.ToastKind = "success";
                    result.ToastMessage = "当前宗地调查数据已保存。";
                    return result;
                }
                if (name == "checkparcel")
                {
                    if (!IsBound(record)) throw new InvalidOperationException(
                        "请先通过区域或权属线新建并选择宗地。");
                    store.Save(record);
                    ParcelSurveyValidationResult validation =
                        ParcelSurveyValidator.Validate(record);
                    result.RefreshPage = true;
                    result.ToastKind = validation.PassesDataChecks ? "success" : "error";
                    result.ToastMessage = validation.PassesDataChecks
                        ? "数据检查通过，已具备调查表导出条件。"
                        : "检查完成：缺失 " + validation.RequiredMissingCount
                            + " 项，待确认 " + validation.PendingConfirmationCount
                            + " 项，另有 " + validation.Issues.Count
                            + " 条检查信息；当前仍可导出，未填写位置将保持空白。";
                    return result;
                }
                if (name == "exportparcel"
                    || name == "exportparcelexcel"
                    || name == "exportparcelword"
                    || name == "exportparcelchecks")
                {
                    if (!IsBound(record)) throw new InvalidOperationException(
                        "请先通过区域或权属线新建并选择宗地。");
                    store.Save(record);
                    bool word = name == "exportparcelword";
                    bool checks = name == "exportparcelchecks";
                    string path = word
                        ? ParcelSurveyExportDialog.ExportWord(record)
                        : checks
                            ? ParcelSurveyExportDialog.ExportCheckForms(record)
                            : ParcelSurveyExportDialog.ExportExcel(record);
                    if (string.IsNullOrWhiteSpace(path)) return result;
                    result.ToastKind = "success";
                    result.ToastMessage = (word ? "地籍调查表 Word 文档"
                        : checks ? "四张检查表 Excel 表格"
                        : "权籍调查表 Excel 表格")
                        + "已导出并完成回读检查："
                        + path;
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "宗地调查数据无效：" + ex.Message;
                if (name.StartsWith("cad", StringComparison.Ordinal))
                    result.ExecuteScript =
                        "window.CDBoxParcelCadInteractionFinished && window.CDBoxParcelCadInteractionFinished('cancel',null);";
                else if (name == "createparcelregion"
                    || name == "newparcel"
                    || name == "bindparcelregion")
                    result.ExecuteScript =
                        "window.CDBoxParcelScopeInteractionFinished && window.CDBoxParcelScopeInteractionFinished();";
                return result;
            }
            result.Handled = false;
            return result;
        }

        private static CDBoxPageRouteResult RouteScopeAction(string name,
            CDBoxPageRouteRequest request, ParcelSurveyStore store,
            ParcelSurveyCadScopeService scopes,
            ParcelSurveyCadWorkflowService workflow)
        {
            var result = new CDBoxPageRouteResult { Handled = true };
            ParcelSurveyScopeActionRequest action = Serializer.Deserialize<
                ParcelSurveyScopeActionRequest>(request.Argument
                    ?? string.Empty) ?? new ParcelSurveyScopeActionRequest();
            if (IsBound(action.Record)) store.Save(action.Record);

            string documentId = action.Record == null
                ? action.DocumentId : action.Record.DocumentId;
            if (name == "switchparcelscope")
            {
                store.SelectRecord(documentId, action.RecordId);
                result.RefreshPage = true;
                return result;
            }
            if (name == "newparcel")
            {
                ParcelSurveyRecord parcel = workflow.SelectParcel();
                result.ExecuteScript =
                    "window.CDBoxParcelScopeInteractionFinished && window.CDBoxParcelScopeInteractionFinished();";
                result.RefreshPage = parcel != null;
                return result;
            }
            if (name == "createparcelregion"
                || name == "bindparcelregion")
            {
                ParcelSurveyRegionInfo region = name == "createparcelregion"
                    ? scopes.CreateRectangleRegion()
                    : scopes.BindExistingRegion();
                if (region != null)
                {
                    ParcelSurveyRecord parcel = store.Current(documentId,
                        action.Record == null ? string.Empty
                            : action.Record.DocumentName,
                        region.RegionId, region.RegionName);
                    parcel.ParcelName = region.RegionName;
                    store.Save(parcel);
                }
                result.ExecuteScript =
                    "window.CDBoxParcelScopeInteractionFinished && window.CDBoxParcelScopeInteractionFinished();";
                result.RefreshPage = region != null;
                return result;
            }
            if (name == "renameparcelinfo")
            {
                ParcelSurveyRecord target = store.GetRecord(action.RecordId);
                if (target == null) throw new InvalidOperationException(
                    "未找到需要重命名的宗地。");
                string parcelName = (action.ParcelName ?? string.Empty).Trim();
                if (parcelName.Length == 0) throw new InvalidOperationException(
                    "宗地名不能为空。");
                store.RenameParcelInfo(documentId, target.Id, parcelName);
                if (target.ScopeType == "region"
                    && !string.IsNullOrWhiteSpace(target.RegionId))
                    scopes.RenameRegion(target.RegionId, parcelName);
                result.RefreshPage = true;
                result.ToastKind = "success";
                result.ToastMessage = "宗地已重命名为“" + parcelName + "”。";
                return result;
            }
            if (name == "deleteparcelinfo")
            {
                ParcelSurveyRecord target = store.GetRecord(action.RecordId);
                if (target != null && target.ScopeType == "region"
                    && !string.IsNullOrWhiteSpace(target.RegionId))
                    scopes.DeleteRegion(target.RegionId);
                store.DeleteParcelInfo(documentId, action.RecordId);
                result.RefreshPage = true;
                result.ToastKind = "success";
                result.ToastMessage = "已删除宗地信息并解除图形绑定。";
                return result;
            }
            result.Handled = false;
            return result;
        }

        private static ParcelSurveyScopeContext BuildFallbackScope(
            ParcelSurveyRecord record)
        {
            record = record ?? new ParcelSurveyRecord();
            return new ParcelSurveyScopeContext
            {
                DocumentId = record.DocumentId ?? string.Empty,
                DocumentName = string.IsNullOrWhiteSpace(record.DocumentName)
                    ? "当前图纸" : record.DocumentName,
                ScopeType = record.ScopeType ?? "whole",
                RegionId = record.RegionId ?? string.Empty,
                RegionName = string.IsNullOrWhiteSpace(record.RegionName)
                    ? "整张图纸" : record.RegionName,
                ParcelId = record.ParcelId ?? string.Empty,
                ParcelName = record.ParcelName ?? string.Empty,
                RecordId = record.ScopeType == "parcel"
                    || record.ScopeType == "region"
                    ? record.Id ?? string.Empty : string.Empty,
                BindingValid = IsBound(record)
            };
        }

        private static string ScopeToken(ParcelSurveyScopeContext scope)
        {
            scope = scope ?? new ParcelSurveyScopeContext();
            return scope.DocumentId + "|" + scope.RecordId;
        }

        private static bool IsBound(ParcelSurveyRecord record)
        {
            if (record == null) return false;
            if (string.Equals(record.ScopeType, "region",
                StringComparison.OrdinalIgnoreCase))
                return !string.IsNullOrWhiteSpace(record.RegionId);
            return string.Equals(record.ScopeType, "parcel",
                    StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(
                    record.Boundary.SourceObjectHandle);
        }

        private static string SafeJson(object value)
        {
            return Serializer.Serialize(value).Replace("</", "<\\/")
                .Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
        }

        private static string BuildScript()
        {
            return @"
(function(){
var ctx=window.CDBoxParcelSurveyContext||{},draft=clone(ctx.Record||{}),scope=ctx.Scope||{},hasSelection=!!(ctx.Scope&&ctx.Scope.RecordId),canEdit=hasSelection&&ctx.Scope.BindingValid!==false,interactionBusy=false,viewKey='cdbox.parcel-editor.view.'+(ctx.Record&&ctx.Record.Id||'current'),view=loadView(),activeTab=Math.min(6,Math.max(1,Number(view.activeTab)||1));
var tabNames=['项目与人员','宗地基本信息','权利人与权属','土地用途与面积','界址调查','房屋调查'];
var fourBoundaryKeys=['parcel.northBoundary','parcel.eastBoundary','parcel.southBoundary','parcel.westBoundary'];
function clone(v){return JSON.parse(JSON.stringify(v||{}));}
function loadView(){try{var stored=localStorage.getItem(viewKey);if(stored)return JSON.parse(stored)||{};}catch(e){}try{var named=String(window.name||'');if(named.indexOf('cdbox-parcel-view:')===0){var saved=JSON.parse(named.slice(18));if(saved&&saved.key===viewKey)return saved.state||{};}}catch(e){}return {};}
function saveView(){view.activeTab=activeTab;var json=JSON.stringify(view);try{localStorage.setItem(viewKey,json);}catch(e){}try{window.name='cdbox-parcel-view:'+JSON.stringify({key:viewKey,state:view});}catch(e){}}
function captureView(){var scroller=document.getElementById('content');view.activeTab=activeTab;view.scrollY=Math.max(0,scroller?scroller.scrollTop:(window.scrollY||0));view.textareas=view.textareas||{};document.querySelectorAll('textarea[data-resize-key]').forEach(function(x){view.textareas[x.dataset.resizeKey]={width:Math.round(x.offsetWidth),height:Math.round(x.offsetHeight)};});saveView();}
function resizeAttrs(scope,key){var k=scope+'|'+key,s=view.textareas&&view.textareas[k];return ` data-resize-key='${attr(k)}'${s?` style='width:${Number(s.width)||0}px;height:${Number(s.height)||72}px'`:''}`;}
function restoreView(){document.querySelectorAll('textarea[data-resize-key]').forEach(function(x){var s=view.textareas&&view.textareas[x.dataset.resizeKey];if(!s)return;if(Number(s.width)>0)x.style.width=Number(s.width)+'px';if(Number(s.height)>0)x.style.height=Number(s.height)+'px';});requestAnimationFrame(function(){var scroller=document.getElementById('content'),y=Math.max(0,Number(view.scrollY)||0);if(scroller)scroller.scrollTo(0,y);else window.scrollTo(0,y);});}
function post(n,a){try{chrome.webview.postMessage('studio|'+n+'|'+encodeURIComponent(a||''));}catch(e){toast('无法连接页面宿主。','error');}}
function esc(v){return String(v==null?'':v).replace(/[&<>']/g,function(c){return c==='&'?'&amp;':c==='<'?'&lt;':c==='>'?'&gt;':'&#39;';});}
function attr(v){return esc(v).replace(/`/g,'&#96;');}
function ensure(){draft.Fields=draft.Fields||{};draft.Boundary=draft.Boundary||{};draft.Boundary.Points=draft.Boundary.Points||[];draft.Boundary.Segments=draft.Boundary.Segments||[];draft.Boundary.SignatureGroups=draft.Boundary.SignatureGroups||[];draft.Buildings=draft.Buildings||[];draft.LayoutDiagnostics=draft.LayoutDiagnostics||{};}
function value(key,fields){fields=fields||draft.Fields;return fields[key]||(fields[key]={TextValue:'',NumericValue:null,BooleanValue:false,Selections:[],Status:3,Confirmed:false});}
function def(key){return (ctx.Fields||[]).find(function(x){return x.Key===key;});}
function statusName(n){var s=(ctx.Statuses||[]).find(function(x){return Number(x.Value)===Number(n);});return s?s.Label:'人工';}
function statusOptions(d,v){return (ctx.Statuses||[]).filter(function(x){return Number(x.Value)!==4||d.AllowNotApplicable;}).map(function(x){return `<option value='${x.Value}'${Number(x.Value)===Number(v)?' selected':''}>${esc(x.Label)}</option>`;}).join('');}
function optionList(items,current,empty){var h=empty===false?'':`<option value=''>请选择</option>`;return h+(items||[]).map(function(x){return `<option value='${attr(x)}'${String(x)===String(current)?' selected':''}>${esc(x)}</option>`;}).join('');}
function scopePayload(extra){var data={Record:draft,RecordId:scope.RecordId||'',DocumentId:scope.DocumentId||'',ScopeType:scope.ScopeType||'whole',RegionId:scope.RegionId||'',RegionName:scope.RegionName||'',ParcelId:scope.ParcelId||'',ParcelName:scope.ParcelName||'',ScopeToken:(scope.DocumentId||'')+'|'+(scope.RecordId||'')};Object.keys(extra||{}).forEach(function(k){data[k]=extra[k];});return JSON.stringify(data);}
function renderScope(){var parcels=scope.Parcels||[],range=document.getElementById('scopeRegion');range.innerHTML=parcels.map(function(x){return `<option value='${attr(x.RecordId)}'${x.RecordId===scope.RecordId?' selected':''}>${esc(x.ParcelName||x.OwnerName||'未命名宗地')}${x.BoundaryValid?'':' · 绑定异常'}</option>`;}).join('');document.querySelectorAll('[data-scope-bound-only]').forEach(function(x){x.disabled=!hasSelection;});}
function conditionActive(d,fields){if(!d.ConditionKey)return true;var c=value(d.ConditionKey,fields),op=d.ConditionOperator||'';if(op==='true')return !!c.BooleanValue;if(op==='false')return !c.BooleanValue;if(op==='neq')return !!c.TextValue&&c.TextValue!==d.ConditionValue;return c.TextValue===d.ConditionValue;}
function renderTop(){['saveParcel','checkData','exportSurvey'].forEach(function(id){document.getElementById(id).disabled=!canEdit;});}
function showRenameDialog(){var existing=document.querySelector('[data-rename-dialog]');if(existing)existing.remove();var current=(scope.ParcelName||scope.RegionName||'').trim(),mask=document.createElement('div');mask.className='modal-mask';mask.dataset.renameDialog='1';mask.innerHTML=`<form class='modal' data-rename-form><div class='modal-head'><div><h2>重命名宗地</h2><p>区域或权属线只作为宗地的绑定与选择依据。</p></div></div><div class='modal-body'><label class='modal-field'><span>宗地名 *</span><input class='control' data-rename-input type='text' value='${attr(current)}' placeholder='例如 张三、北侧宗地' autocomplete='off'><small data-rename-error style='min-height:18px;color:var(--danger)'></small></label></div><div class='modal-foot'><button class='btn' data-rename-cancel type='button'>取消</button><button class='btn primary' type='submit'>确定</button></div></form>`;document.body.appendChild(mask);var form=mask.querySelector('[data-rename-form]'),input=mask.querySelector('[data-rename-input]'),error=mask.querySelector('[data-rename-error]');function close(){mask.remove();}mask.querySelector('[data-rename-cancel]').onclick=close;mask.onclick=function(e){if(e.target===mask)close();};input.onkeydown=function(e){if(e.key==='Escape'){e.preventDefault();close();}};form.onsubmit=function(e){e.preventDefault();var name=(input.value||'').trim();if(!name){error.textContent='宗地名不能为空。';input.focus();return;}captureView();close();post('renameParcelInfo',scopePayload({RecordId:scope.RecordId,ParcelName:name}));};requestAnimationFrame(function(){input.focus();input.select();});}
function renderTabs(){document.getElementById('tabs').innerHTML=tabNames.map(function(n,i){return `<button class='nav-item${activeTab===i+1?' active':''}' data-tab='${i+1}' type='button'>${i+1}. ${n}</button>`;}).join('');}
function fieldControl(d,v,scope){var id=scope+'-'+d.Key.replace(/[^a-zA-Z0-9]/g,'-'),disabled=d.ReadOnly?' readonly':'';if(d.Kind==='textarea')return `<textarea id='${id}' class='control' data-role='value'${resizeAttrs(scope,d.Key)}${disabled}>${esc(v.TextValue||'')}</textarea>`;if(d.Kind==='select')return `<select id='${id}' class='control' data-role='value'>${optionList(d.Options,v.TextValue)}</select>`;if(d.Kind==='multiselect')return `<select id='${id}' class='control' data-role='value' multiple>${(d.Options||[]).map(function(x){return `<option value='${attr(x)}'${(v.Selections||[]).indexOf(x)>=0?' selected':''}>${esc(x)}</option>`;}).join('')}</select>`;if(d.Kind==='checkbox')return `<label class='check-control'><input data-role='value' type='checkbox'${v.BooleanValue?' checked':''}> <span>${v.BooleanValue?'是':'否'}</span></label>`;var type=d.Kind==='number'?'number':d.Kind==='date'?'date':'text',step=d.Kind==='number'?' step=.01':'';return `<input id='${id}' class='control' data-role='value' type='${type}' value='${attr(d.Kind==='number'?(v.NumericValue==null?'':v.NumericValue):(v.TextValue||''))}'${step}${disabled}>`;}
function renderField(d,fields,scope,buildingId){var v=value(d.Key,fields),wide=d.Kind==='textarea'?' wide':'',conditional=d.ConditionKey?' conditional':'',hidden=!conditionActive(d,fields)?' style=display:none':'',na=Number(v.Status)===4?' na':'';return `<div class='field-card${wide}${conditional}${na}' data-key='${attr(d.Key)}' data-kind='${attr(d.Kind)}' data-scope='${scope}'${buildingId?` data-building='${buildingId}'`:''}${hidden}><div class='field-head'><span class='field-label'>${esc(d.Label)}${d.Required?`<i class='required'>*</i>`:''}${d.Unit?` <small>(${esc(d.Unit)})</small>`:''}</span><select class='status-select s${Number(v.Status)||0}' data-role='status'>${statusOptions(d,v.Status)}</select></div>${fieldControl(d,v,scope)}<div class='na-note'>明确输出 /</div></div>`;}
function groupsFor(fields,definitions,scope,buildingId,excludedKeys){var defs=(definitions||[]).filter(function(d){return d.Tab===activeTab&&(!excludedKeys||excludedKeys.indexOf(d.Key)<0);}),groups=[];defs.forEach(function(d){if(groups.indexOf(d.Group)<0)groups.push(d.Group);});return groups.map(function(g){return `<section class='group'><div class='group-head'><strong>${esc(g)}</strong></div><div class='field-grid'>${defs.filter(function(d){return d.Group===g;}).map(function(d){return renderField(d,fields,scope,buildingId);}).join('')}</div></section>`;}).join('');}
function renderContent(){if(!canEdit){document.getElementById('content').innerHTML=`<section class='group'><div class='empty'>请通过区域或权属线新建宗地后再编辑。</div></section>`;return;}var h='';if(activeTab===5)h+=renderBoundary();else if(activeTab===6)h+=linkedReferences()+groupsFor(draft.Fields,ctx.Fields,'parcel','')+renderBuildings();else h+=groupsFor(draft.Fields,ctx.Fields,'parcel','');document.getElementById('content').innerHTML=h;bindContent();}
function linkedReferences(){var items=[['项目名称','project.name'],['邮政编码','project.postalCode']];if(value('house.followParcelOwner').BooleanValue)items=items.concat([['沿用的宗地权利人','rights.ownerName'],['证件种类','rights.certificateType'],['证件号码','rights.certificateNumber'],['通讯地址','rights.contactAddress'],['联系电话','rights.contactPhone']]);return `<section class='group'><div class='group-head'><strong>跨表共用信息</strong></div><div class='field-grid'>${items.map(function(x){var d=def(x[1]),v=value(x[1]);return `<div class='field-card'><div class='field-head'><span class='field-label'>${esc(x[0])}</span><span class='badge s${v.Status}'>${statusName(v.Status)}</span></div><div class='control' style='display:flex;align-items:center;background:#f0f3f7'>${esc(d?v.DisplayValue||v.TextValue:v.TextValue)||'尚未填写'}</div></div>`;}).join('')}</div></section>`;}
function renderBoundary(){var b=draft.Boundary,source=b.SourceObjectHandle?`<div class='source-meta'><span>图元 ${esc(b.SourceObjectHandle)}</span><span>图层 ${esc(b.SourceLayerName||'未命名')}</span><span>${b.SourceClockwise?'顺时针':'逆时针'}</span><span>面积 ${esc(b.SourceArea==null?'--':b.SourceArea)} ㎡</span></div>`:'';return `<section class='group'><div class='group-head'><div><strong>CAD 宗地几何</strong>${source}</div></div><div class='section-tools'><label class='closed'><input id='boundaryClosed' type='checkbox'${b.ParcelBoundaryClosed?' checked':''}> 宗地权属线已闭合</label><button class='mini' data-cad='boundary' type='button'>选择宗地</button><button class='mini' data-add='point' type='button'>+ 界址点</button></div>${pointTable()}</section><section class='group'><div class='group-head'><strong>界址段列表</strong></div><div class='section-tools'><button class='mini' data-cad='segment' type='button'>填写界址段</button><button class='mini' data-add='segment' type='button'>+ 界址段</button></div>${segmentTable()}</section><section class='group'><div class='group-head'><strong>界址签章分组</strong></div><div class='section-tools'><button class='mini' data-cad='signature' type='button'>填写邻宗信息</button><button class='mini' data-auto-signature='1' type='button'>自动分组</button><button class='mini' data-add='signature' type='button'>+ 签章组</button></div>${signatureTable()}</section><section class='group'><div class='group-head'><strong>界址说明</strong></div><div class='section-tools'><button class='mini' data-generate-description='1' type='button'>重新生成说明与四至</button></div></section>${fourBoundaries()}`+groupsFor(draft.Fields,ctx.Fields,'parcel','',fourBoundaryKeys);}
function fourBoundaries(){var defs=fourBoundaryKeys.map(def).filter(Boolean);return `<section class='group'><div class='group-head'><strong>宗地四至</strong></div><div class='field-grid'>${defs.map(function(d){return renderField(d,draft.Fields,'parcel','');}).join('')}</div></section>`;}
function pointTable(){var rows=draft.Boundary.Points.map(function(p,i){return `<tr data-point='${i}'><td>${i+1}</td><td><input class='cell' data-p='PointNumber' value='${attr(p.PointNumber||'')}'></td><td><input class='cell' value='${attr(p.X==null?'':p.X)}' readonly></td><td><input class='cell' value='${attr(p.Y==null?'':p.Y)}' readonly></td><td><select class='cell' data-p='MarkerType'>${optionList(ctx.MarkerTypes,p.MarkerType||'喷涂')}</select></td><td><input class='cell long' data-p='Description' value='${attr(p.Description||'')}'></td><td><select class='cell' data-p='Status'>${statusOptions({AllowNotApplicable:false},p.Status)}</select></td><td><button class='mini danger' data-remove-point='${i}' type='button'>删除</button></td></tr>`;}).join('');return `<div class='table-wrap'><table class='data-table'><thead><tr><th>序号</th><th>界址点号</th><th>X 坐标</th><th>Y 坐标</th><th>界标种类</th><th>点位说明</th><th>状态</th><th></th></tr></thead><tbody>${rows||`<tr><td colspan='8' class='empty'>尚无界址点，请从 CAD 识别或添加记录。</td></tr>`}</tbody></table></div>`;}
function segmentTable(){var rows=draft.Boundary.Segments.map(function(s,i){return `<tr data-segment='${i}'><td>${Math.floor(i/26)+1}</td><td><input class='cell small' data-p='StartPointNumber' value='${attr(s.StartPointNumber||'')}'></td><td><input class='cell' data-p='MiddlePointNumbers' value='${attr(s.MiddlePointNumbers||'')}'></td><td><input class='cell small' data-p='EndPointNumber' value='${attr(s.EndPointNumber||'')}'></td><td><input class='cell small' data-p='Distance' type='number' step='0.01' value='${attr(s.Distance==null?'':s.Distance)}'></td><td><select class='cell' data-p='LineCategory'>${optionList(ctx.LineCategories,s.LineCategory)}</select></td><td><select class='cell' data-p='LinePosition'>${optionList(ctx.LinePositions,s.LinePosition)}</select></td><td><input class='cell' data-p='NeighborParcelCode' value='${attr(s.NeighborParcelCode||'')}'></td><td><input class='cell' data-p='NeighborOwner' value='${attr(s.NeighborOwner||'')}'></td><td><input class='cell small' data-p='Direction' value='${attr(s.Direction||'')}'></td><td><input class='cell long' data-p='Description' value='${attr(s.Description||'')}'></td><td><select class='cell' data-p='Status'>${statusOptions({AllowNotApplicable:false},s.Status)}</select></td><td><button class='mini danger' data-remove-segment='${i}' type='button'>删除</button></td></tr>`;}).join('');return `<div class='table-wrap'><table class='data-table'><thead><tr><th>页</th><th>起点号</th><th>中间点号</th><th>终点号</th><th>距离</th><th>线类别</th><th>线位置</th><th>相邻宗地代码</th><th>相邻权利人</th><th>方向</th><th>段说明</th><th>状态</th><th></th></tr></thead><tbody>${rows||`<tr><td colspan='13' class='empty'>尚无界址段。</td></tr>`}</tbody></table></div>`;}
function signatureTable(){var rows=draft.Boundary.SignatureGroups.map(function(g,i){return `<tr data-signature='${i}'><td>${Math.floor(i/13)+1}</td><td><input class='cell small' data-p='StartPointNumber' value='${attr(g.StartPointNumber||'')}'></td><td><input class='cell' data-p='MiddlePointNumbers' value='${attr(g.MiddlePointNumbers||'/')}'></td><td><input class='cell small' data-p='EndPointNumber' value='${attr(g.EndPointNumber||'')}'></td><td><input class='cell' data-p='NeighborOwner' value='${attr(g.NeighborOwner||'')}'></td><td><input class='cell' data-p='NeighborParcelCode' value='${attr(g.NeighborParcelCode||'')}'></td><td><input class='cell' data-p='NeighborRepresentative' value='${attr(g.NeighborRepresentative||'')}'></td><td><input class='cell' data-p='ParcelRepresentative' value='${attr(g.ParcelRepresentative||'')}'></td><td><input class='cell' data-p='ConfirmationDate' type='date' value='${attr(g.ConfirmationDate||'')}'></td><td><input class='cell' data-p='SignatureStatus' value='${attr(g.SignatureStatus||'')}'></td><td><select class='cell' data-p='Status'>${statusOptions({AllowNotApplicable:false},g.Status)}</select></td><td><button class='mini danger' data-remove-signature='${i}' type='button'>删除</button></td></tr>`;}).join('');return `<div class='table-wrap'><table class='data-table'><thead><tr><th>页</th><th>起点</th><th>中间点</th><th>终点</th><th>邻宗权利人</th><th>邻宗代码</th><th>邻宗指界人</th><th>本宗指界人</th><th>指界日期</th><th>签章状态</th><th>来源</th><th></th></tr></thead><tbody>${rows||`<tr><td colspan='12' class='empty'>尚无签章组，可按连续邻宗自动整理后添加。</td></tr>`}</tbody></table></div>`;}
function applyBoundaryRecognition(data,prefix,startNumber,clockwise){var vertices=data.Vertices||[],n=vertices.length,start=vertices.findIndex(function(v){return Number(v.SourceIndex)===Number(data.SelectedStartSourceIndex);});if(start<0||n<3){toast('权属线节点数据无效，请重新识别。','error');return;}var step=clockwise===!!data.NativeClockwise?1:-1,points=[];for(var i=0;i<n;i++){var raw=(start+step*i+n*4)%n,v=vertices[raw],next=(raw+step+n)%n,edge=step===1?v.DistanceToNext:vertices[next].DistanceToNext;points.push({Sequence:i+1,PointNumber:prefix+(startNumber+i),X:v.X,Y:v.Y,DistanceToNext:edge,MarkerType:'喷涂',Description:'',Confirmed:true,Status:0});}draft.Boundary.Points=points;draft.Boundary.Segments=[];draft.Boundary.SignatureGroups=[];draft.Boundary.ParcelBoundaryClosed=true;draft.Boundary.SourceObjectHandle=data.SourceObjectHandle||'';draft.Boundary.SourceLayerName=data.SourceLayerName||'';draft.Boundary.SourceClockwise=clockwise;draft.Boundary.SourceArea=data.Area;var area=value('land.parcelArea');area.NumericValue=data.Area;area.TextValue='';area.Status=0;area.Confirmed=false;renderContent();restoreView();toast('已自动填入 '+n+' 个界址点及坐标，面积同步为 '+data.Area+' ㎡。','success');post('autoGenerateBoundaryDescriptions',JSON.stringify(draft));}
window.CDBoxParcelCadInteractionFinished=function(kind,data){interactionBusy=false;post('restore','');setTimeout(function(){if(!data){restoreView();return;}if(kind==='boundary'){applyBoundaryRecognition(data,data.StartPointPrefix||'J',Number(data.StartPointNumber)||1,!!data.ConfiguredClockwise);return;}if(kind==='segment'){draft.Boundary.Segments=clone(data||[]);renderContent();restoreView();toast('界址段连续选择已结束，已保留本轮录入结果。','success');post('autoGenerateBoundaryDescriptions',JSON.stringify(draft));return;}if(kind==='signature'){draft.Boundary.SignatureGroups=clone(data||[]);renderContent();restoreView();toast('邻宗信息连续选择已结束，已保留本轮录入结果。','success');}},80);};
window.CDBoxParcelScopeInteractionFinished=function(){interactionBusy=false;post('restore','');restoreView();};
window.CDBoxParcelDescriptionsGenerated=function(record){draft=clone(record||draft);ensure();renderTop();renderContent();restoreView();};
function renderBuildings(){var rows=draft.Buildings.map(function(b,i){var title=value('building.number',b.Fields).TextValue||('房屋 '+(i+1));return `<article class='building'><div class='building-head'><strong>${esc(title)}</strong><button class='mini danger' data-remove-building='${attr(b.Id)}' type='button'>删除此幢</button></div><div class='field-grid'>${(ctx.BuildingFields||[]).map(function(d){return renderField(d,b.Fields,'building-'+b.Id,b.Id);}).join('')}</div></article>`;}).join('');return `<section class='group'><div class='group-head'><strong>每幢房屋</strong></div><div class='section-tools'><button class='mini' data-add='building' type='button'>+ 新增一幢</button></div>${rows||`<div class='empty'>尚未添加房屋。</div>`}</section>`;}
function syncFields(){document.querySelectorAll('.field-card[data-key]').forEach(function(card){var key=card.dataset.key,kind=card.dataset.kind,fields=draft.Fields;if(card.dataset.building){var b=draft.Buildings.find(function(x){return x.Id===card.dataset.building;});if(!b)return;fields=b.Fields;}var v=value(key,fields),s=card.querySelector('[data-role=status]'),c=card.querySelector('[data-role=value]'),old=Number(v.Status);v.Status=Number(s?s.value:v.Status);if(v.Status===4){v.TextValue='/';v.NumericValue=null;v.BooleanValue=false;v.Selections=[];v.Confirmed=true;return;}if(old===4){v.TextValue='';v.NumericValue=null;v.BooleanValue=false;v.Selections=[];v.Confirmed=false;return;}if(!c)return;if(kind==='number'){v.NumericValue=c.value===''?null:Number(c.value);v.TextValue='';}else if(kind==='checkbox'){v.BooleanValue=!!c.checked;v.TextValue='';}else if(kind==='multiselect'){v.Selections=Array.from(c.selectedOptions).map(function(o){return o.value;});v.TextValue='';}else{v.TextValue=c.value||'';v.NumericValue=null;}});}
function syncTables(){var closed=document.getElementById('boundaryClosed');if(closed)draft.Boundary.ParcelBoundaryClosed=closed.checked;document.querySelectorAll('tr[data-point]').forEach(function(row){var p=draft.Boundary.Points[Number(row.dataset.point)];row.querySelectorAll('[data-p]').forEach(function(c){var k=c.dataset.p;p[k]=c.type==='checkbox'?c.checked:c.value;});});document.querySelectorAll('tr[data-segment]').forEach(function(row){var s=draft.Boundary.Segments[Number(row.dataset.segment)];row.querySelectorAll('[data-p]').forEach(function(c){var k=c.dataset.p,val=c.type==='checkbox'?c.checked:c.value;s[k]=k==='Distance'?(val===''?null:Number(val)):k==='Status'?Number(val):val;});});document.querySelectorAll('tr[data-signature]').forEach(function(row){var g=draft.Boundary.SignatureGroups[Number(row.dataset.signature)];row.querySelectorAll('[data-p]').forEach(function(c){var k=c.dataset.p;g[k]=c.type==='checkbox'?c.checked:k==='Status'?Number(c.value):c.value;});});}
function syncAll(){syncFields();syncTables();ensure();}
function bindContent(){document.querySelectorAll('textarea[data-resize-key]').forEach(function(x){x.addEventListener('mouseup',captureView);x.addEventListener('pointerup',captureView);});document.querySelectorAll('[data-role=status]').forEach(function(s){s.onchange=function(){syncAll();captureView();renderContent();restoreView();};});document.querySelectorAll('.check-control input').forEach(function(c){c.onchange=function(){var span=c.parentElement.querySelector('span');if(span)span.textContent=c.checked?'是':'否';};});var conditionKeys=(ctx.Fields||[]).filter(function(d){return !!d.ConditionKey;}).map(function(d){return d.ConditionKey;});document.querySelectorAll('.field-card[data-key]').forEach(function(card){if(conditionKeys.indexOf(card.dataset.key)<0)return;var c=card.querySelector('[data-role=value]');if(c)c.addEventListener('change',function(){syncAll();captureView();renderContent();restoreView();});});document.querySelectorAll('[data-add]').forEach(function(b){b.onclick=function(){syncAll();captureView();add(b.dataset.add);renderContent();restoreView();};});document.querySelectorAll('[data-cad]').forEach(function(b){b.onclick=function(){syncAll();if(b.dataset.cad!=='boundary'&&draft.Boundary.Points.length<2){toast('请先从图纸识别权属线和界址点。','error');return;}captureView();interactionBusy=true;var route=b.dataset.cad==='boundary'?'cadRecognizeBoundary':b.dataset.cad==='segment'?'cadSelectBoundarySegment':'cadSelectSignatureGroup';post('minimize','');setTimeout(function(){post(route,JSON.stringify(draft));},100);};});document.querySelectorAll('[data-generate-description]').forEach(function(b){b.onclick=function(){syncAll();captureView();post('generateBoundaryDescriptions',JSON.stringify(draft));};});document.querySelectorAll('[data-auto-signature]').forEach(function(b){b.onclick=function(){syncAll();captureView();autoSignatureGroups();renderContent();restoreView();};});document.querySelectorAll('[data-remove-point]').forEach(function(b){b.onclick=function(){syncAll();captureView();draft.Boundary.Points.splice(Number(b.dataset.removePoint),1);renderContent();restoreView();};});document.querySelectorAll('[data-remove-segment]').forEach(function(b){b.onclick=function(){syncAll();captureView();draft.Boundary.Segments.splice(Number(b.dataset.removeSegment),1);renderContent();restoreView();};});document.querySelectorAll('[data-remove-signature]').forEach(function(b){b.onclick=function(){syncAll();captureView();draft.Boundary.SignatureGroups.splice(Number(b.dataset.removeSignature),1);renderContent();restoreView();};});document.querySelectorAll('[data-remove-building]').forEach(function(b){b.onclick=function(){syncAll();captureView();draft.Buildings=draft.Buildings.filter(function(x){return x.Id!==b.dataset.removeBuilding;});renderContent();restoreView();};});}
function signatureMiddle(numbers){numbers=(numbers||[]).filter(Boolean);if(!numbers.length)return '/';if(numbers.length>=3)return numbers[0]+'-'+numbers[numbers.length-1];return numbers.join('、');}
function autoSignatureGroups(){var groups=[],current=null;(draft.Boundary.Segments||[]).forEach(function(s){var key=(s.NeighborParcelCode||'')+'|'+(s.NeighborOwner||''),inside=String(s.MiddlePointNumbers||'').split(/[、,，;； ]+/).filter(Boolean);if(!current||current._key!==key){current={_key:key,StartPointNumber:s.StartPointNumber||'',MiddlePointNumbers:'/',EndPointNumber:s.EndPointNumber||'',NeighborOwner:s.NeighborOwner||'',NeighborParcelCode:s.NeighborParcelCode||'',NeighborRepresentative:'',ParcelRepresentative:'',ConfirmationDate:'',SignatureStatus:'待签章',PreservePaperSignatureBlank:true,Confirmed:true,Status:0,_middle:inside.slice()};groups.push(current);}else{current._middle.push(s.StartPointNumber||'');current._middle=current._middle.concat(inside);current.EndPointNumber=s.EndPointNumber||'';}});groups.forEach(function(g){g.MiddlePointNumbers=signatureMiddle(g._middle);delete g._middle;delete g._key;});draft.Boundary.SignatureGroups=groups;toast('已按连续相邻宗生成 '+groups.length+' 个签章组，请继续填写指界与签章信息。','success');}
function add(type){if(type==='point'){var n=draft.Boundary.Points.length+1;draft.Boundary.Points.push({Sequence:n,PointNumber:'J'+n,X:null,Y:null,DistanceToNext:null,MarkerType:'喷涂',Description:'',Confirmed:true,Status:3});}else if(type==='segment'){var a=draft.Boundary.Segments.length,b=draft.Boundary.Points;draft.Boundary.Segments.push({StartPointNumber:b[a]?b[a].PointNumber:'',MiddlePointNumbers:'',EndPointNumber:b[a+1]?b[a+1].PointNumber:(b.length&&a===b.length-1?b[0].PointNumber:''),Distance:null,LineCategory:'',LinePosition:'待确认',NeighborParcelCode:'',NeighborOwner:'',Direction:'',Description:'',Confirmed:true,NeighborHandled:true,Status:3});}else if(type==='signature'){draft.Boundary.SignatureGroups.push({StartPointNumber:'',MiddlePointNumbers:'/',EndPointNumber:'',NeighborOwner:'',NeighborParcelCode:'',NeighborRepresentative:'',ParcelRepresentative:'',ConfirmationDate:'',SignatureStatus:'',PreservePaperSignatureBlank:true,Confirmed:true,Status:3});}else if(type==='building'){var fs={};(ctx.BuildingFields||[]).forEach(function(d){fs[d.Key]={TextValue:'',NumericValue:null,BooleanValue:false,Selections:[],Status:d.DefaultStatus,Confirmed:false};});draft.Buildings.push({Id:'b'+Date.now()+Math.random().toString(16).slice(2),Fields:fs});}}
function toast(m,k){var old=document.querySelector('.toast');if(old)old.remove();var x=document.createElement('div');x.className='toast '+(k||'');x.textContent=m||'';document.body.appendChild(x);setTimeout(function(){x.remove();},3200);}
function init(){ensure();renderTop();renderScope();renderTabs();renderContent();restoreView();document.getElementById('tabs').onclick=function(e){var b=e.target.closest('[data-tab]');if(!b||!canEdit)return;syncAll();captureView();activeTab=Number(b.dataset.tab);view.scrollY=0;saveView();renderTabs();renderContent();document.getElementById('content').scrollTo(0,0);};document.getElementById('scopeRegion').onchange=function(){if(!this.value)return;if(canEdit)syncAll();captureView();post('switchParcelScope',scopePayload({RecordId:this.value}));};document.querySelector('.scope-actions').onclick=function(e){var b=e.target.closest('[data-scope-action]');if(!b||b.disabled)return;if(canEdit)syncAll();captureView();var action=b.dataset.scopeAction;if(action==='delete'){if(!confirm('确定删除当前宗地信息吗？\n插件生成的区域线将同时删除；已有区域线或权属线只解除绑定。'))return;post('deleteParcelInfo',scopePayload({RecordId:scope.RecordId}));return;}if(action==='rename'){showRenameDialog();return;}var routes={newparcel:'newParcel',create:'createParcelRegion',bind:'bindParcelRegion'},route=routes[action];if(!route)return;interactionBusy=true;post('minimize','');setTimeout(function(){post(route,scopePayload({RecordId:scope.RecordId}));},100);};document.getElementById('saveParcel').onclick=function(){if(!canEdit)return;syncAll();captureView();post('saveParcel',JSON.stringify(draft));};document.getElementById('checkData').onclick=function(){if(!canEdit)return;syncAll();captureView();post('checkParcel',JSON.stringify(draft));};var exportMenu=document.getElementById('exportMenu'),exportButton=document.getElementById('exportSurvey'),exportOptions=document.getElementById('exportOptions');function closeExportMenu(){exportOptions.hidden=true;exportButton.setAttribute('aria-expanded','false');}exportButton.onclick=function(e){e.stopPropagation();if(!canEdit)return;var open=exportOptions.hidden;exportOptions.hidden=!open;exportButton.setAttribute('aria-expanded',open?'true':'false');};exportOptions.onclick=function(e){var b=e.target.closest('[data-export-format]');if(!b)return;e.stopPropagation();closeExportMenu();syncAll();captureView();if(b.dataset.exportFormat==='word'){post('exportParcelWord',JSON.stringify(draft));return;}if(b.dataset.exportFormat==='excel'){post('exportParcelExcel',JSON.stringify(draft));return;}if(b.dataset.exportFormat==='checks'){post('exportParcelChecks',JSON.stringify(draft));}};document.addEventListener('click',function(e){if(!exportMenu.contains(e.target))closeExportMenu();});document.addEventListener('keydown',function(e){if(e.key==='Escape')closeExportMenu();});window.CDBoxStudioToast=toast;setTimeout(function(){post('ready','parcel-survey-editor');},50);setInterval(function(){if(interactionBusy||document.hidden)return;post('pollParcelScope',scopePayload());},1800);}
init();
})();
";
        }

        private sealed class PageContext
        {
            public ParcelSurveyRecord Record { get; set; }
            public ParcelSurveyValidationResult Validation { get; set; }
            public ParcelSurveyScopeContext Scope { get; set; }
            public IList<ParcelSurveyFieldDefinition> Fields { get; set; }
            public IList<ParcelSurveyFieldDefinition> BuildingFields { get; set; }
            public List<StatusOption> Statuses { get; set; }
            public string[] MarkerTypes { get; set; }
            public string[] LineCategories { get; set; }
            public string[] LinePositions { get; set; }
        }

        private sealed class StatusOption
        {
            public StatusOption(int value, string label)
            {
                Value = value;
                Label = label;
            }
            public int Value { get; set; }
            public string Label { get; set; }
        }
    }
}
