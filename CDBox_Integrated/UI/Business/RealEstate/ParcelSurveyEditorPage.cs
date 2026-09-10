using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using CDBox.RealEstate.Cad;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.Services;
using CDBox.RealEstate.Settings;
using CDBox.Shared.UI;
using TCPipeAutoDraw.UI.Studio;

namespace CDBox.RealEstate.UI
{
    public static class ParcelSurveyEditorPage
    {
        public const string PageId = "realestate-parcel-survey-editor";
        public const int SidebarWidth = 184;
        public const int LegacyMinimumWidth = 1040;
        public const int MinimumEditorWidth = 776;
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        internal static CDBoxPageDefinition Create(ParcelSurveyStore store,
            ParcelBoundaryCadService cad, ParcelSurveyCadScopeService scopes,
            ParcelSurveyCadWorkflowService workflow, Action addBuilding)
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
                    return BuildHtml(record, scope);
                },
                delegate(CDBoxPageRouteRequest request)
                {
                    return Route(request, store, cad, scopes, workflow, addBuilding);
                })
            {
                Width = 1380,
                Height = 900,
                MinimumWidth = MinimumEditorWidth,
                MinimumHeight = 700,
                TitleBarActionText = "≡",
                TitleBarActionToolTip = "切换侧边栏",
                TitleBarActionScript =
                    "window.CDBoxToggleParcelSidebar && window.CDBoxToggleParcelSidebar();",
                TitleBarActionHoverScript =
                    "window.CDBoxPreviewParcelSidebar && window.CDBoxPreviewParcelSidebar(true);",
                TitleBarActionLeaveScript =
                    "window.CDBoxPreviewParcelSidebar && window.CDBoxPreviewParcelSidebar(false);"
            };
        }

        public static string BuildHtml(ParcelSurveyRecord record,
            ParcelSurveyValidationResult validation)
        {
            return BuildHtml(record, BuildFallbackScope(record));
        }

        public static string BuildHtml(ParcelSurveyRecord record,
            ParcelSurveyValidationResult validation,
            ParcelSurveyScopeContext scope)
        {
            return BuildHtml(record, scope);
        }

        private static string BuildHtml(ParcelSurveyRecord record,
            ParcelSurveyScopeContext scope)
        {
            return BuildWithAppearance(record, scope, CDBoxStudioSettingsStore.Load());
        }

        internal static string BuildWithAppearance(ParcelSurveyRecord record, ParcelSurveyScopeContext scope, CDBoxStudioSettings appearance)
        {
            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            BuildingAreaService.Synchronize(record);
            var context = new PageContext
            {
                Record = record,
                SidebarExpandedMinimumWidth = MinimumEditorWidth + SidebarWidth,
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

            appearance = appearance ?? new CDBoxStudioSettings();
            appearance.Normalize();
            var html = new StringBuilder(70000);
            html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
                .Append("<title>宗地调查数据编辑器</title><style>")
                .Append(CDBoxWorkbenchAppearance.BuildThemeStyles(appearance))
                .Append(ParcelSurveyEditorStyles.Build())
                .Append("</style></head><body data-appearance=\"workbench\" data-theme=\"")
                .Append(CDBoxWorkbenchAppearance.ResolveTheme(appearance.Theme))
                .Append("\" class=\"").Append(appearance.AnimationsEnabled ? "" : "no-animations")
                .Append("\"><main class=\"shell page\" data-page=\"")
                .Append(PageId).Append("\"><header class=\"top\"><div class=\"toolbar\"><label class=\"parcel-picker\"><select id=\"scopeRegion\" aria-label=\"宗地选择\"></select></label><div id=\"parcelActions\" class=\"parcel-actions\"><button id=\"parcelMore\" class=\"toolbar-btn more-button\" type=\"button\" aria-label=\"更多宗地操作\" aria-expanded=\"false\" aria-controls=\"parcelActionItems\">···</button><div id=\"parcelActionItems\" class=\"parcel-action-items\" inert><button class=\"toolbar-btn\" data-scope-bound-only=\"1\" data-scope-action=\"rename\" type=\"button\">重命名宗地</button><button class=\"toolbar-btn danger\" data-scope-bound-only=\"1\" data-scope-action=\"delete\" type=\"button\">删除宗地信息</button></div></div><div class=\"auto-save\"><span id=\"autoSaveStatus\" role=\"status\" aria-live=\"polite\"></span><button id=\"retryAutoSave\" class=\"mini\" type=\"button\" hidden>重试</button></div><div id=\"exportMenu\" class=\"export-menu\"><button id=\"exportSurvey\" class=\"toolbar-btn primary\" type=\"button\" aria-haspopup=\"menu\" aria-expanded=\"false\">导出 ▾</button><div id=\"exportOptions\" class=\"export-options\" role=\"menu\" hidden><button class=\"export-option\" data-export-format=\"property\" role=\"menuitem\" type=\"button\"><span>导出房产文档</span></button><button class=\"export-option\" data-export-format=\"word\" role=\"menuitem\" type=\"button\"><span>导出为 Word 文档</span></button><button class=\"export-option\" data-export-format=\"excel\" role=\"menuitem\" type=\"button\"><span>导出为 Excel 表格</span></button><button class=\"export-option\" data-export-format=\"checks\" role=\"menuitem\" type=\"button\"><span>导出四张检查表</span></button></div></div></div></header>")
                .Append("<aside id=\"sidebar\" class=\"sidebar\"><nav id=\"tabs\" class=\"nav\"></nav></aside><div id=\"sidebarEdge\" class=\"sidebar-edge\" aria-hidden=\"true\"></div><section class=\"workspace\"><section id=\"content\" class=\"content\" tabindex=\"-1\"></section></section></main>")
                .Append("<script>window.CDBoxParcelSurveyContext=")
                .Append(SafeJson(context)).Append(";</script><script>")
                .Append(CDBoxWorkbenchThemeScript.Build())
                .Append(BuildScript())
                .Append("</script></body></html>");
            return CDBoxAccentAppearance.Attach(html.ToString(), appearance);
        }

        private static CDBoxPageRouteResult Route(CDBoxPageRouteRequest request,
            ParcelSurveyStore store, ParcelBoundaryCadService cad,
            ParcelSurveyCadScopeService scopes,
            ParcelSurveyCadWorkflowService workflow, Action addBuilding)
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
                    else if (IsBound(poll.Record))
                    {
                        var saved = store.GetRecord(poll.Record.Id);
                        if (saved != null && saved.BuildingsRevision > poll.Record.BuildingsRevision)
                        {
                            saved = store.SavePreservingCurrentScope(poll.Record);
                            result.ExecuteScript = BuildingsChangedScript(saved);
                        }
                    }
                    return result;
                }
                if (name == "switchparcelscope"
                    || name == "renameparcelinfo"
                    || name == "deleteparcelinfo")
                    return RouteScopeAction(name, request, store, scopes);

                if (name == "autosaveparcel")
                {
                    var save = Serializer.Deserialize<ParcelSurveyAutoSaveRequest>(
                        request.Argument ?? string.Empty) ?? new ParcelSurveyAutoSaveRequest();
                    try
                    {
                        if (!IsBound(save.Record)) throw new InvalidOperationException("宗地绑定无效");
                        var saved = store.SavePreservingCurrentScope(save.Record);
                        if (saved == null)
                            throw new InvalidOperationException("宗地已删除，请重新选择");
                        result.ExecuteScript = BuildingsChangedScript(saved)
                            + "window.CDBoxParcelAutoSaved && window.CDBoxParcelAutoSaved("
                            + save.RequestId + ",true,'');";
                    }
                    catch (Exception ex)
                    {
                        result.ExecuteScript = "window.CDBoxParcelAutoSaved && window.CDBoxParcelAutoSaved("
                            + save.RequestId + ",false," + SafeJson("自动保存失败：" + ex.Message) + ");";
                    }
                    return result;
                }

                if (name == "saveparcelview")
                {
                    ParcelSurveyViewStateRequest view = Serializer
                        .Deserialize<ParcelSurveyViewStateRequest>(
                            request.Argument ?? string.Empty)
                        ?? new ParcelSurveyViewStateRequest();
                    ParcelSurveyRecord target = store.GetRecord(
                        view.RecordId);
                    if (IsBound(target))
                    {
                        target.LayoutDiagnostics.TextAreaHeights =
                            view.TextAreaHeights;
                        target.LayoutDiagnostics.Normalize();
                        store.SavePreservingCurrentScope(target);
                    }
                    return result;
                }

                ParcelSurveyRecord record = Serializer.Deserialize<ParcelSurveyRecord>(
                    request.Argument ?? string.Empty) ?? new ParcelSurveyRecord();
                record.Normalize();
                if (name == "addbuildingfromcad")
                {
                    if (!IsBound(record) || addBuilding == null)
                        throw new InvalidOperationException("请先选择有效宗地。");
                    store.Save(record);
                    result.ActionToRun = addBuilding;
                    return result;
                }
                if (name == "cadrecognizemapsheet")
                {
                    if (!IsBound(record)) throw new InvalidOperationException(
                        "请先通过不动产菜单选择宗地。");
                    store.Save(record);
                    bool started = workflow.RecognizeMapSheet(record, true);
                    if (!started)
                        result.ExecuteScript =
                            "window.CDBoxParcelCadInteractionFinished && window.CDBoxParcelCadInteractionFinished('cancel',null);";
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
                        "请先通过不动产菜单选择宗地。");
                    store.Save(record);
                    result.RefreshPage = true;
                    result.ToastKind = "success";
                    result.ToastMessage = "当前宗地调查数据已保存。";
                    return result;
                }
                if (name == "exportparcel"
                    || name == "exportparcelexcel"
                    || name == "exportparcelword"
                    || name == "exportparcelchecks"
                    || name == "exportparcelproperty")
                {
                    if (!IsBound(record)) throw new InvalidOperationException(
                        "请先通过不动产菜单选择宗地。");
                    store.Save(record);
                    bool word = name == "exportparcelword";
                    bool checks = name == "exportparcelchecks";
                    bool property = name == "exportparcelproperty";
                    string path = property ? ParcelSurveyExportDialog.ExportProperty(record) : word
                        ? ParcelSurveyExportDialog.ExportWord(record)
                        : checks
                            ? ParcelSurveyExportDialog.ExportCheckForms(record)
                            : ParcelSurveyExportDialog.ExportExcel(record);
                    if (string.IsNullOrWhiteSpace(path)) return result;
                    result.ToastKind = "success";
                    result.ToastMessage = (property ? "房产文档" : word ? "地籍调查表 Word 文档"
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
                if (name == "switchparcelscope" || name == "renameparcelinfo" || name == "deleteparcelinfo")
                    result.ExecuteScript = "window.CDBoxParcelScopeActionFailed && window.CDBoxParcelScopeActionFailed();";
                if (name.StartsWith("cad", StringComparison.Ordinal))
                    result.ExecuteScript =
                        "window.CDBoxParcelCadInteractionFinished && window.CDBoxParcelCadInteractionFinished('cancel',null);";
                return result;
            }
            result.Handled = false;
            return result;
        }

        private static string BuildingsChangedScript(ParcelSurveyRecord record)
        {
            return record == null ? string.Empty : "window.CDBoxParcelBuildingsChanged && window.CDBoxParcelBuildingsChanged("
                + SafeJson(record) + ");";
        }

        private static CDBoxPageRouteResult RouteScopeAction(string name,
            CDBoxPageRouteRequest request, ParcelSurveyStore store,
            ParcelSurveyCadScopeService scopes)
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
var sidebarPreferenceKey='cdbox.parcel-editor.sidebar-visible',sidebarPreviewTimer=0,sidebarPreferredVisible=loadSidebarPreference(),titleActionHovered=false;
function loadSidebarPreference(){try{var saved=localStorage.getItem(sidebarPreferenceKey);return saved===null?true:saved==='1';}catch(e){return true;}}
function saveSidebarPreference(){try{localStorage.setItem(sidebarPreferenceKey,sidebarPreferredVisible?'1':'0');}catch(e){}}
function sidebarForcedCompact(){return window.innerWidth<(Number(ctx.SidebarExpandedMinimumWidth)||960);}
function applySidebarState(){var shell=document.querySelector('.shell'),collapsed=!sidebarPreferredVisible||sidebarForcedCompact();if(!shell)return;shell.classList.toggle('sidebar-collapsed',collapsed);if(!collapsed)shell.classList.remove('sidebar-preview');document.getElementById('sidebar').inert=collapsed&&!shell.classList.contains('sidebar-preview');}
function previewSidebar(show){clearTimeout(sidebarPreviewTimer);var shell=document.querySelector('.shell');if(!shell||!shell.classList.contains('sidebar-collapsed'))return;if(show){shell.classList.add('sidebar-preview');document.getElementById('sidebar').inert=false;return;}sidebarPreviewTimer=setTimeout(function(){var sidebar=document.getElementById('sidebar');if(!sidebar.matches(':hover')&&!titleActionHovered){if(sidebar.contains(document.activeElement))document.getElementById('content').focus({preventScroll:true});shell.classList.remove('sidebar-preview');sidebar.inert=true;}},200);}
window.CDBoxToggleParcelSidebar=function(){sidebarPreferredVisible=!sidebarPreferredVisible;saveSidebarPreference();applySidebarState();if(!sidebarPreferredVisible)previewSidebar(false);};
window.CDBoxPreviewParcelSidebar=function(show){titleActionHovered=!!show;previewSidebar(!!show);};
/* WORKBENCH_HELPERS */
function clone(v){return JSON.parse(JSON.stringify(v||{}));}
function loadView(){try{var stored=localStorage.getItem(viewKey);if(stored)return JSON.parse(stored)||{};}catch(e){}try{var named=String(window.name||'');if(named.indexOf('cdbox-parcel-view:')===0){var saved=JSON.parse(named.slice(18));if(saved&&saved.key===viewKey)return saved.state||{};}}catch(e){}return {};}
function saveView(){view.activeTab=activeTab;var json=JSON.stringify(view);try{localStorage.setItem(viewKey,json);}catch(e){}try{window.name='cdbox-parcel-view:'+JSON.stringify({key:viewKey,state:view});}catch(e){}}
function captureView(){var scroller=document.getElementById('content');view.activeTab=activeTab;view.scrollY=Math.max(0,scroller?scroller.scrollTop:(window.scrollY||0));view.textareas=view.textareas||{};draft.LayoutDiagnostics=draft.LayoutDiagnostics||{};draft.LayoutDiagnostics.TextAreaHeights=draft.LayoutDiagnostics.TextAreaHeights||{};document.querySelectorAll('textarea[data-resize-key]').forEach(function(x){if(!x.offsetHeight)return;var h=Math.max(56,Math.min(1200,Math.round(x.offsetHeight))),k=x.dataset.resizeKey;view.textareas[k]={height:h};draft.LayoutDiagnostics.TextAreaHeights[k]=h;});saveView();}
function textareaHeight(k){var stored=draft.LayoutDiagnostics&&draft.LayoutDiagnostics.TextAreaHeights&&draft.LayoutDiagnostics.TextAreaHeights[k],local=view.textareas&&view.textareas[k]&&view.textareas[k].height,h=Number(stored)||Number(local)||72;return Math.max(56,Math.min(1200,h));}
function resizeAttrs(scope,key){var k=scope+'|'+key;return ` data-resize-key='${attr(k)}' style='height:${textareaHeight(k)}px'`;}
function fitTextarea(x){if(!x||!x.offsetWidth||x.closest('.is-collapsed')||x.closest('.read-mode:not(.is-editing)'))return;var minimum=textareaHeight(x.dataset.resizeKey);x.style.height='0px';var contentHeight=Math.max(56,Math.min(1200,x.scrollHeight+2));x.style.height=Math.max(minimum,contentHeight)+'px';}
function restoreView(){document.querySelectorAll('textarea[data-resize-key]').forEach(function(x){x.style.width='100%';});requestAnimationFrame(function(){document.querySelectorAll('textarea[data-resize-key]').forEach(fitTextarea);var scroller=document.getElementById('content'),y=Math.max(0,Number(view.scrollY)||0);if(scroller)scroller.scrollTo(0,y);else window.scrollTo(0,y);});}
var textareaSaveTimer=0;function persistTextareaLayout(){captureView();if(!canEdit)return;clearTimeout(textareaSaveTimer);textareaSaveTimer=setTimeout(function(){post('saveParcelView',JSON.stringify({RecordId:draft.Id||scope.RecordId||'',TextAreaHeights:draft.LayoutDiagnostics.TextAreaHeights||{}}));},180);}
function post(n,a){try{chrome.webview.postMessage('studio|'+n+'|'+encodeURIComponent(a||''));}catch(e){toast('无法连接页面宿主。','error');}}
function esc(v){return String(v==null?'':v).replace(/[&<>']/g,function(c){return c==='&'?'&amp;':c==='<'?'&lt;':c==='>'?'&gt;':'&#39;';});}
function attr(v){return esc(v).replace(/`/g,'&#96;');}
function ensure(){draft.Fields=draft.Fields||{};draft.Boundary=draft.Boundary||{};draft.Boundary.Points=draft.Boundary.Points||[];draft.Boundary.Segments=draft.Boundary.Segments||[];draft.Boundary.SignatureGroups=draft.Boundary.SignatureGroups||[];draft.Buildings=draft.Buildings||[];draft.LayoutDiagnostics=draft.LayoutDiagnostics||{};draft.LayoutDiagnostics.TextAreaHeights=draft.LayoutDiagnostics.TextAreaHeights||{};if(draft.Boundary.SignatureGroups.length)syncSegmentNeighbors(false);}
function value(key,fields){fields=fields||draft.Fields;return fields[key]||(fields[key]={TextValue:'',NumericValue:null,BooleanValue:false,Selections:[],Status:3,Confirmed:false});}
function def(key){return (ctx.Fields||[]).find(function(x){return x.Key===key;});}
function statusName(n){var s=(ctx.Statuses||[]).find(function(x){return Number(x.Value)===Number(n);});return s?s.Label:'人工';}
function statusOptions(d,v){return (ctx.Statuses||[]).filter(function(x){return Number(x.Value)!==4||d.AllowNotApplicable;}).map(function(x){return `<option value='${x.Value}'${Number(x.Value)===Number(v)?' selected':''}>${esc(x.Label)}</option>`;}).join('');}
function optionList(items,current,empty){var h=empty===false?'':`<option value=''>请选择</option>`;return h+(items||[]).map(function(x){return `<option value='${attr(x)}'${String(x)===String(current)?' selected':''}>${esc(x)}</option>`;}).join('');}
function scopePayload(extra){var data={Record:draft,RecordId:scope.RecordId||'',DocumentId:scope.DocumentId||'',ScopeType:scope.ScopeType||'whole',RegionId:scope.RegionId||'',RegionName:scope.RegionName||'',ParcelId:scope.ParcelId||'',ParcelName:scope.ParcelName||'',ScopeToken:(scope.DocumentId||'')+'|'+(scope.RecordId||'')};Object.keys(extra||{}).forEach(function(k){data[k]=extra[k];});return JSON.stringify(data);}
function renderScope(){var parcels=scope.Parcels||[],range=document.getElementById('scopeRegion');range.innerHTML=parcels.map(function(x){return `<option value='${attr(x.RecordId)}'${x.RecordId===scope.RecordId?' selected':''}>${esc(x.ParcelName||x.OwnerName||'未命名宗地')}${x.BoundaryValid?'':' · 绑定异常'}</option>`;}).join('');document.querySelectorAll('[data-scope-bound-only]').forEach(function(x){x.disabled=!hasSelection;});}
function conditionActive(d,fields){if(!d.ConditionKey)return true;var c=value(d.ConditionKey,fields),op=d.ConditionOperator||'';if(op==='true')return !!c.BooleanValue;if(op==='false')return !c.BooleanValue;if(op==='neq')return !!c.TextValue&&c.TextValue!==d.ConditionValue;return c.TextValue===d.ConditionValue;}
function renderTop(){document.getElementById('exportSurvey').disabled=!canEdit;document.getElementById('parcelMore').disabled=!hasSelection;}
function showRenameDialog(){var existing=document.querySelector('[data-rename-dialog]');if(existing)existing.remove();var current=(scope.ParcelName||scope.RegionName||'').trim(),mask=document.createElement('div');mask.className='modal-mask';mask.dataset.renameDialog='1';mask.innerHTML=`<form class='modal' data-rename-form><div class='modal-head'><div><h2>重命名宗地</h2><p>区域或权属线只作为宗地的绑定与选择依据。</p></div></div><div class='modal-body'><label class='modal-field'><span>宗地名 *</span><input class='control' data-rename-input type='text' value='${attr(current)}' placeholder='例如 张三、北侧宗地' autocomplete='off'><small data-rename-error style='min-height:18px;color:var(--danger)'></small></label></div><div class='modal-foot'><button class='btn' data-rename-cancel type='button'>取消</button><button class='btn primary' type='submit'>确定</button></div></form>`;document.body.appendChild(mask);var form=mask.querySelector('[data-rename-form]'),input=mask.querySelector('[data-rename-input]'),error=mask.querySelector('[data-rename-error]');function close(){mask.remove();}mask.querySelector('[data-rename-cancel]').onclick=close;mask.onclick=function(e){if(e.target===mask)close();};input.onkeydown=function(e){if(e.key==='Escape'){e.preventDefault();close();}};form.onsubmit=function(e){e.preventDefault();var name=(input.value||'').trim();if(!name){error.textContent='宗地名不能为空。';input.focus();return;}captureView();close();leaveParcel(function(){post('renameParcelInfo',scopePayload({RecordId:scope.RecordId,ParcelName:name}));});};requestAnimationFrame(function(){input.focus();input.select();});}
function executeExport(format){syncAll();captureView();if(format==='property'){post('exportParcelProperty',JSON.stringify(draft));return;}if(format==='word'){post('exportParcelWord',JSON.stringify(draft));return;}if(format==='excel'){post('exportParcelExcel',JSON.stringify(draft));return;}if(format==='checks'){post('exportParcelChecks',JSON.stringify(draft));}}
function showExportWarning(format){if(format==='property'){afterAutoSave(function(){executeExport(format);});return;}var existing=document.querySelector('[data-export-warning]');if(existing)existing.remove();var mask=document.createElement('div');mask.className='modal-mask';mask.dataset.exportWarning='1';mask.innerHTML=`<form class='modal' data-export-warning-form role='alertdialog' aria-modal='true' aria-labelledby='exportWarningTitle'><div class='modal-head'><div><h2 id='exportWarningTitle'>导出前提示</h2></div></div><div class='modal-body'><p style='margin:0;color:var(--danger);font-size:13px;font-weight:800;line-height:1.8'>自动生成界址说明与宗地四至尚不成熟，导出后请务必进行核实校验！！！</p></div><div class='modal-foot'><button class='btn' data-export-warning-cancel type='button'>取消</button><button class='btn primary' type='submit'>我已知晓</button></div></form>`;document.body.appendChild(mask);var form=mask.querySelector('[data-export-warning-form]'),ack=form.querySelector('button[type=submit]');function close(){mask.remove();}mask.querySelector('[data-export-warning-cancel]').onclick=close;mask.onclick=function(e){if(e.target===mask)close();};form.onkeydown=function(e){if(e.key==='Escape'){e.preventDefault();close();}};form.onsubmit=function(e){e.preventDefault();close();executeExport(format);};requestAnimationFrame(function(){ack.focus();});}
function renderTabs(){document.getElementById('tabs').innerHTML=tabNames.map(function(n,i){var count=tabPending(i+1);return `<button aria-current='${activeTab===i+1?'page':'false'}' class='nav-item${activeTab===i+1?' active':''}' data-tab='${i+1}' type='button' title='${n}${count?' · '+count+' 项待处理':' · 无待处理字段'}'><span class='nav-label'>${n}</span><span class='nav-count'${count?'':' hidden'}>${count}</span></button>`;}).join('');}
function fieldControl(d,v,scope){var id=scope+'-'+d.Key.replace(/[^a-zA-Z0-9\u4e00-\u9fff]/g,'-'),disabled=d.ReadOnly&&Number(v.Status)!==3?' readonly':'';if(d.Kind==='textarea')return `<textarea id='${id}' class='control' data-role='value'${resizeAttrs(scope,d.Key)}${disabled}>${esc(v.TextValue||'')}</textarea>`;if(d.Kind==='select')return `<select id='${id}' class='control' data-role='value'>${optionList(d.Options,v.TextValue)}</select>`;if(d.Kind==='multiselect')return `<div id='${id}' class='multi-check-grid' data-role='value'>${(d.Options||[]).map(function(x){return `<label class='multi-check'><input type='checkbox' value='${attr(x)}'${(v.Selections||[]).indexOf(x)>=0?' checked':''}><span>${esc(x)}</span></label>`;}).join('')}</div>`;if(d.Kind==='checkbox')return `<label class='check-control'><input id='${id}' data-role='value' type='checkbox'${v.BooleanValue?' checked':''}> <span>${v.BooleanValue?'是':'否'}</span></label>`;var type=d.Kind==='number'?'number':d.Kind==='date'?'date':'text',step=d.Kind==='number'?' step=.01':'';var input=`<input id='${id}' class='control' data-role='value' type='${type}' value='${attr(d.Kind==='number'?(v.NumericValue==null?'':v.NumericValue):(v.TextValue||''))}'${step}${disabled}>`;return d.Key==='parcel.mapSheetNumber'?`<div class='control-action'>${input}<button class='mini' data-map-sheet='1' type='button'>自动识别</button></div>`:input;}
function renderField(d,fields,scope,buildingId){
 var measured=buildingId&&draft.Buildings.find(function(b){return b.Id===buildingId&&b.HasFloorAreas;});
 if(d.Key==='land.buildingAreaTotal'||d.Key==='land.buildingFootprintTotal'||d.Key==='building.footprintArea'||(d.Key==='building.area'&&measured)){var derived=value(d.Key,fields);return `<div class='field-card read-mode' data-key='${attr(d.Key)}' data-kind='number' data-derived-area='1'${buildingId?` data-building='${attr(buildingId)}'`:''}><div class='field-head'><span class='field-label'>${esc(d.Label)} <small>(㎡)</small></span><span class='status-select s0'>自动</span></div><div class='field-display'>${derived.NumericValue==null?'等待房屋面积完整后汇总':Number(derived.NumericValue).toFixed(2)}</div></div>`;}
 if(d.Key==='building.floor'&&measured)return `<div class='field-card read-mode'><div class='field-head'><span class='field-label'>${esc(d.Label)}</span><span class='status-select s0'>自动</span></div><div class='field-display' data-derived-floor>${esc(value(d.Key,fields).TextValue||'由层次明细汇总')}</div></div>`;
 var v=value(d.Key,fields),wide=d.Kind==='textarea'?' full':'',toggle=d.Kind==='checkbox'?' toggle-field full':'',conditional=d.ConditionKey?' conditional':'',hidden=!conditionActive(d,fields)?' style=display:none':'',na=Number(v.Status)===4?' na':'',read=Number(v.Status)===0||Number(v.Status)===1,id=scope+'-'+d.Key.replace(/[^a-zA-Z0-9\u4e00-\u9fff]/g,'-');
 return `<div class='field-card${wide}${toggle}${conditional}${na}${read?' read-mode':''}' data-key='${attr(d.Key)}' data-kind='${attr(d.Kind)}' data-scope='${scope}'${buildingId?` data-building='${buildingId}'`:''}${hidden}><div class='field-head'><label class='field-label' for='${id}'>${esc(d.Label)}${d.Required?`<i class='required'>*</i>`:''}${d.Unit?` <small>(${esc(d.Unit)})</small>`:''}</label><select aria-label='${attr(d.Label)} · 数据来源' class='status-select s${Number(v.Status)||0}' data-role='status'>${statusOptions(d,v.Status)}</select></div><button class='field-display' type='button' data-edit-field='1' aria-label='编辑${attr(d.Label)}' title='点击编辑；修改后转为人工'>${esc(displayFieldValue(d,v))}</button><div class='field-editor'>${fieldControl(d,v,scope)}</div><div class='na-note'>明确输出 /</div></div>`;
}
function groupsFor(fields,definitions,scope,buildingId,excludedKeys){
 var defs=(definitions||[]).filter(function(d){return d.Tab===activeTab&&(!excludedKeys||excludedKeys.indexOf(d.Key)<0);}),groups=[];
 defs.forEach(function(d){if(groups.indexOf(d.Group)<0)groups.push(d.Group);});
 return groups.map(function(g){var items=defs.filter(function(d){return d.Group===g;});if(!items.some(function(d){return conditionActive(d,fields);}))return '';
 var body=items.map(function(d){return renderField(d,fields,scope,buildingId);}).join('');
 if(g==='房屋权利人'&&value('house.followParcelOwner').BooleanValue)body+=inheritedOwner();
 var tools=g==='界址说明'?`<button class='mini' data-generate-description='1' type='button'>刷新界址说明</button>`:'';
 return renderGroup(scope+'|'+g,g,`<div class='field-grid'>${body}</div>`,function(){return fieldProgress(items,fields);},tools);
 }).join('');
}
function renderContent(){synchronizeBuildingAreas();var content=document.getElementById('content');groupCatalog={};if(!canEdit){content.innerHTML=`<section class='group'><div class='empty'>请先从不动产菜单选择宗地后再编辑。</div></section>`;animateContent(content);return;}var h='';if(activeTab===5)h+=renderBoundary();else if(activeTab===6)h+=renderBuildings()+linkedReferences()+groupsFor(draft.Fields,ctx.Fields,'parcel','');else h+=groupsFor(draft.Fields,ctx.Fields,'parcel','');content.innerHTML=h;bindContent();refreshProgress(false);animateContent(content);}
function animateContent(content){if(!content)return;content.classList.remove('switching');void content.offsetWidth;content.classList.add('switching');setTimeout(function(){content.classList.remove('switching');},240);}
function linkedReferences(){var keys=['project.name','project.postalCode'],items=keys.map(def).filter(Boolean);return renderGroup('references','跨表共用信息',`<div class='field-grid'>${items.map(function(d){return `<div class='field-card'><div class='field-head'><span class='field-label'>${esc(d.Label)}</span><span class='badge s${value(d.Key).Status}'>${statusName(value(d.Key).Status)}</span></div><div class='reference-value'>${esc(displayFieldValue(d,value(d.Key)))}</div></div>`;}).join('')}</div>`,function(){return fieldProgress(items,draft.Fields);});}
function inheritedOwner(){var items=[['沿用的宗地权利人','rights.ownerName'],['证件种类','rights.certificateType'],['证件号码','rights.certificateNumber'],['通讯地址','rights.contactAddress'],['联系电话','rights.contactPhone']];return `<div class='inherited-owner field-grid'>${items.map(function(x){return `<div class='field-card'><div class='field-label'>${esc(x[0])}</div><div class='reference-value'>${esc(value(x[1]).TextValue||'宗地信息尚未填写')}</div></div>`;}).join('')}</div>`;}
function renderBoundary(){
 var b=draft.Boundary,source=b.SourceObjectHandle?`<div class='source-meta'><span>图元 ${esc(b.SourceObjectHandle)}</span><span>图层 ${esc(b.SourceLayerName||'未记录')}</span><span>${b.SourceClockwise?'顺时针':'逆时针'}</span><span>面积 ${esc(b.SourceArea==null?'--':b.SourceArea)} ㎡</span></div>`:'';
 var points=renderGroup('boundary|points','界址点列表',source+pointTable(),function(){return boundaryProgress('points');},`<label class='closed'><input id='boundaryClosed' type='checkbox'${b.ParcelBoundaryClosed?' checked':''}> 宗地权属线已闭合</label><button class='mini' data-add='point' type='button'>+ 界址点</button>`);
 var segments=renderGroup('boundary|segments','界址段列表',segmentTable(),function(){return boundaryProgress('segments');},`<button class='mini' data-cad='segment' type='button'>填写界址段</button><button class='mini' data-add='segment' type='button'>+ 界址段</button>`);
 var signatures=renderGroup('boundary|signatures','界址签章分组',signatureTable(),function(){return boundaryProgress('signatures');},`<button class='mini' data-cad='signature' type='button'>填写邻宗信息</button><button class='mini' data-auto-signature='1' type='button'>自动分组</button><button class='mini' data-add='signature' type='button'>+ 签章组</button>`);
 return points+segments+signatures+groupsFor(draft.Fields,ctx.Fields,'parcel','',fourBoundaryKeys)+fourBoundaries();
}
function fourBoundaries(){var defs=fourBoundaryKeys.map(def).filter(Boolean);return renderGroup('parcel|宗地四至','宗地四至',`<div class='field-grid'>${defs.map(function(d){return renderField(d,draft.Fields,'parcel','');}).join('')}</div>`,function(){return fieldProgress(defs,draft.Fields);});}
function pointTable(){var rows=draft.Boundary.Points.map(function(p,i){return `<tr data-point='${i}'><td class='point-number-data'><input class='cell point-number' data-p='PointNumber' value='${attr(p.PointNumber||'')}'></td><td class='col-point-coordinate'><input class='cell' value='${attr(p.X==null?'':p.X)}' readonly></td><td class='col-point-coordinate'><input class='cell' value='${attr(p.Y==null?'':p.Y)}' readonly></td><td class='col-marker'><select class='cell' data-p='MarkerType'>${optionList(ctx.MarkerTypes,p.MarkerType||'喷涂')}</select></td><td><input class='cell long' data-p='Description' value='${attr(p.Description||'')}'></td><td class='delete-data'><button class='icon-btn danger' data-remove-point='${i}' type='button' title='删除' aria-label='删除界址点'>×</button></td></tr>`;}).join('');return `<div class='table-wrap'><table class='data-table point-table'><thead><tr><th class='point-number-head'>界址点号</th><th class='col-point-coordinate'>X 坐标</th><th class='col-point-coordinate'>Y 坐标</th><th class='col-marker'>界标种类</th><th>点位说明</th><th class='delete-head'></th></tr></thead><tbody>${rows||`<tr><td colspan='6' class='empty'>尚无界址点，请从 CAD 识别或添加记录。</td></tr>`}</tbody></table></div>`;}
function segmentTable(){var rows=draft.Boundary.Segments.map(function(s,i){return `<tr data-segment='${i}'><td class='col-segment-point'><input class='cell small' data-p='StartPointNumber' value='${attr(s.StartPointNumber||'')}'></td><td class='col-segment-middle'><input class='cell' data-p='MiddlePointNumbers' value='${attr(formatMiddle(s.MiddlePointNumbers,''))}'></td><td class='col-segment-point'><input class='cell small' data-p='EndPointNumber' value='${attr(s.EndPointNumber||'')}'></td><td class='col-segment-distance'><input class='cell small' data-p='Distance' type='number' step='0.01' value='${attr(s.Distance==null?'':s.Distance)}'></td><td class='col-segment-category'><select class='cell' data-p='LineCategory'>${optionList(ctx.LineCategories,s.LineCategory)}</select></td><td class='col-segment-position'><select class='cell' data-p='LinePosition'>${optionList(ctx.LinePositions,s.LinePosition)}</select></td><td class='col-segment-owner'><input class='cell' value='${attr(s.NeighborOwner||'')}' readonly title='根据界址签章分组自动填入'></td><td class='col-segment-direction'><input class='cell small' data-p='Direction' value='${attr(s.Direction||'')}'></td><td><input class='cell long' data-p='Description' value='${attr(s.Description||'')}'></td><td class='delete-data'><button class='icon-btn danger' data-remove-segment='${i}' type='button' title='删除' aria-label='删除界址段'>×</button></td></tr>`;}).join('');return `<div class='table-wrap'><table class='data-table segment-table'><thead><tr><th class='col-segment-point'>起点号</th><th class='col-segment-middle'>中间点号</th><th class='col-segment-point'>终点号</th><th class='col-segment-distance'>距离</th><th class='col-segment-category'>线类别</th><th class='col-segment-position'>线位置</th><th class='col-segment-owner'>相邻权利人</th><th class='col-segment-direction'>方向</th><th>段说明</th><th class='delete-head'></th></tr></thead><tbody>${rows||`<tr><td colspan='10' class='empty'>尚无界址段。</td></tr>`}</tbody></table></div>`;}
function signatureTable(){var rows=draft.Boundary.SignatureGroups.map(function(g,i){return `<tr data-signature='${i}'><td class='col-signature-point'><input class='cell small' data-p='StartPointNumber' value='${attr(g.StartPointNumber||'')}'></td><td class='col-signature-middle'><input class='cell' data-p='MiddlePointNumbers' value='${attr(formatMiddle(g.MiddlePointNumbers,'/'))}'></td><td class='col-signature-point'><input class='cell small' data-p='EndPointNumber' value='${attr(g.EndPointNumber||'')}'></td><td class='col-signature-owner'><input class='cell' data-p='NeighborOwner' value='${attr(g.NeighborOwner||'')}'></td><td class='col-signature-person col-signature-neighbor'><input class='cell' data-p='NeighborRepresentative' value='${attr(g.NeighborRepresentative||'')}'></td><td class='col-signature-person col-signature-self'><input class='cell' data-p='ParcelRepresentative' value='${attr(g.ParcelRepresentative||'')}'></td><td class='col-signature-date'><input class='cell' data-p='ConfirmationDate' type='date' value='${attr(g.ConfirmationDate||'')}'></td><td class='delete-data'><button class='icon-btn danger' data-remove-signature='${i}' type='button' title='删除' aria-label='删除签章组'>×</button></td></tr>`;}).join('');return `<div class='table-wrap'><table class='data-table signature-table'><thead><tr><th class='col-signature-point'>起点</th><th class='col-signature-middle'>中间点</th><th class='col-signature-point'>终点</th><th class='col-signature-owner'>邻宗权利人</th><th class='col-signature-person col-signature-neighbor'>邻宗指界人</th><th class='col-signature-person col-signature-self'>本宗指界人</th><th class='col-signature-date'>指界日期</th><th class='delete-head'></th></tr></thead><tbody>${rows||`<tr><td colspan='8' class='empty'>尚无签章组，可按连续邻宗自动整理后添加。</td></tr>`}</tbody></table></div>`;}
window.CDBoxParcelCadInteractionFinished=function(kind,data){interactionBusy=false;post('restore','');setTimeout(function(){if(!data){restoreView();return;}if(kind==='segment'){draft.Boundary.Segments=clone(data||[]);syncSegmentNeighbors(false);renderContent();restoreView();toast('界址段连续选择已结束，已保留本轮录入结果。','success');post('autoGenerateBoundaryDescriptions',JSON.stringify(draft));return;}if(kind==='signature'){draft.Boundary.SignatureGroups=clone(data||[]);syncSegmentNeighbors(true);renderContent();restoreView();toast('邻宗信息连续选择已结束，已同步界址段和宗地四至。','success');post('autoGenerateBoundaryDescriptions',JSON.stringify(draft));}},80);};
window.CDBoxParcelDescriptionsGenerated=function(record){draft=clone(record||draft);ensure();renderTop();renderContent();restoreView();scheduleAutoSave();};
function syncFields(){document.querySelectorAll('.field-card[data-key]').forEach(function(card){var key=card.dataset.key,kind=card.dataset.kind,fields=draft.Fields;if(card.dataset.building){var b=draft.Buildings.find(function(x){return x.Id===card.dataset.building;});if(!b)return;fields=b.Fields;}var v=value(key,fields),s=card.querySelector('[data-role=status]'),c=card.querySelector('[data-role=value]'),old=Number(v.Status);v.Status=Number(s?s.value:v.Status);if(v.Status===4){v.TextValue='/';v.NumericValue=null;v.BooleanValue=false;v.Selections=[];v.Confirmed=true;return;}if(old===4){v.TextValue='';v.NumericValue=null;v.BooleanValue=false;v.Selections=[];v.Confirmed=false;return;}if(!c)return;if(kind==='number'){v.NumericValue=c.value===''?null:Number(c.value);v.TextValue='';}else if(kind==='checkbox'){v.BooleanValue=!!c.checked;v.TextValue='';}else if(kind==='multiselect'){v.Selections=Array.from(c.querySelectorAll('input[type=checkbox]:checked')).map(function(o){return o.value;});v.TextValue='';}else{v.TextValue=c.value||'';v.NumericValue=null;}});}
function syncTables(){var closed=document.getElementById('boundaryClosed');if(closed)draft.Boundary.ParcelBoundaryClosed=closed.checked;document.querySelectorAll('tr[data-point]').forEach(function(row){var p=draft.Boundary.Points[Number(row.dataset.point)];row.querySelectorAll('[data-p]').forEach(function(c){var k=c.dataset.p;p[k]=c.type==='checkbox'?c.checked:c.value;});});document.querySelectorAll('tr[data-segment]').forEach(function(row){var s=draft.Boundary.Segments[Number(row.dataset.segment)];row.querySelectorAll('[data-p]').forEach(function(c){var k=c.dataset.p,val=c.type==='checkbox'?c.checked:c.value;s[k]=k==='Distance'?(val===''?null:Number(val)):k==='Status'?Number(val):val;});});document.querySelectorAll('tr[data-signature]').forEach(function(row){var g=draft.Boundary.SignatureGroups[Number(row.dataset.signature)];row.querySelectorAll('[data-p]').forEach(function(c){var k=c.dataset.p;g[k]=c.type==='checkbox'?c.checked:k==='Status'?Number(c.value):c.value;});});}
function syncAll(){syncFields();syncTables();ensure();synchronizeBuildingAreas();}
function bindContent(){
bindBuildings();
document.querySelectorAll('textarea[data-resize-key]').forEach(function(x){x.addEventListener('input',function(){fitTextarea(x);persistTextareaLayout();});x.addEventListener('mouseup',persistTextareaLayout);x.addEventListener('pointerup',persistTextareaLayout);});
document.querySelectorAll('[data-role=status]').forEach(function(s){s.onchange=function(){syncAll();captureView();renderContent();restoreView();};});
document.querySelectorAll('.check-control input').forEach(function(c){c.onchange=function(){var span=c.parentElement.querySelector('span');if(span)span.textContent=c.checked?'是':'否';};});
var conditionKeys=(ctx.Fields||[]).filter(function(d){return !!d.ConditionKey;}).map(function(d){return d.ConditionKey;});
document.querySelectorAll('.field-card[data-key]').forEach(function(card){if(conditionKeys.indexOf(card.dataset.key)<0)return;var c=card.querySelector('[data-role=value]');if(c)c.addEventListener('change',function(){syncAll();captureView();renderContent();restoreView();});});
document.querySelectorAll('[data-add]').forEach(function(b){b.onclick=function(){syncAll();captureView();add(b.dataset.add);renderContent();restoreView();};});
document.querySelectorAll('[data-map-sheet]').forEach(function(b){b.onclick=function(){syncAll();if((draft.Boundary.Points||[]).length<3){toast('请先选择宗地并识别权属线。','error');return;}captureView();interactionBusy=true;post('minimize','');setTimeout(function(){post('cadRecognizeMapSheet',JSON.stringify(draft));},100);};});
document.querySelectorAll('[data-cad]').forEach(function(b){b.onclick=function(){syncAll();if(draft.Boundary.Points.length<2){toast('请先从图纸识别权属线和界址点。','error');return;}captureView();interactionBusy=true;var route=b.dataset.cad==='segment'?'cadSelectBoundarySegment':'cadSelectSignatureGroup';post('minimize','');setTimeout(function(){post(route,JSON.stringify(draft));},100);};});
document.querySelectorAll('[data-generate-description]').forEach(function(b){b.onclick=function(){syncAll();captureView();post('generateBoundaryDescriptions',JSON.stringify(draft));};});
document.querySelectorAll('[data-auto-signature]').forEach(function(b){b.onclick=function(){syncAll();captureView();autoSignatureGroups();renderContent();restoreView();post('autoGenerateBoundaryDescriptions',JSON.stringify(draft));};});
document.querySelectorAll('tr[data-signature] [data-p=StartPointNumber],tr[data-signature] [data-p=EndPointNumber],tr[data-signature] [data-p=NeighborOwner]').forEach(function(c){c.onchange=function(){syncAll();syncSegmentNeighbors(true);captureView();renderContent();restoreView();post('autoGenerateBoundaryDescriptions',JSON.stringify(draft));};});
document.querySelectorAll('[data-remove-point]').forEach(function(b){b.onclick=function(){syncAll();captureView();draft.Boundary.Points.splice(Number(b.dataset.removePoint),1);renderContent();restoreView();};});
document.querySelectorAll('[data-remove-segment]').forEach(function(b){b.onclick=function(){syncAll();captureView();draft.Boundary.Segments.splice(Number(b.dataset.removeSegment),1);renderContent();restoreView();post('autoGenerateBoundaryDescriptions',JSON.stringify(draft));};});
document.querySelectorAll('[data-remove-signature]').forEach(function(b){b.onclick=function(){syncAll();captureView();draft.Boundary.SignatureGroups.splice(Number(b.dataset.removeSignature),1);syncSegmentNeighbors(true);renderContent();restoreView();post('autoGenerateBoundaryDescriptions',JSON.stringify(draft));};});
document.querySelectorAll('[data-remove-building]').forEach(function(b){b.onclick=function(){syncAll();captureView();var index=draft.Buildings.findIndex(function(x){return x.Id===b.dataset.removeBuilding;});draft.Buildings.splice(index,1);var next=draft.Buildings[Math.min(index,draft.Buildings.length-1)];view.buildingId=next?next.Id:'';view.buildingEditing=false;saveView();renderContent();restoreView();};});
}
function formatMiddle(value,emptyValue){var numbers=String(value||'').split(/[、,，;； ]+/).filter(function(x){return x&&x!=='/';});if(!numbers.length)return emptyValue||'';if(numbers.length>=3)return numbers[0]+'-'+numbers[numbers.length-1];return numbers.join('、');}
function signatureMiddle(numbers){return formatMiddle((numbers||[]).filter(Boolean).join('、'),'/');}
function boundaryEdges(start,end){var points=draft.Boundary.Points||[],startIndex=points.findIndex(function(p){return String(p.PointNumber||'').toLowerCase()===String(start||'').trim().toLowerCase();}),endIndex=points.findIndex(function(p){return String(p.PointNumber||'').toLowerCase()===String(end||'').trim().toLowerCase();}),edges=[],current=startIndex,guard=0;if(startIndex<0||endIndex<0||startIndex===endIndex)return edges;while(current!==endIndex&&guard++<points.length){var next=(current+1)%points.length;edges.push(String(points[current].PointNumber||'').toLowerCase()+'>'+String(points[next].PointNumber||'').toLowerCase());current=next;}return current===endIndex?edges:[];}
function signatureForSegment(segment){var edges=boundaryEdges(segment.StartPointNumber,segment.EndPointNumber);if(!edges.length)return null;return (draft.Boundary.SignatureGroups||[]).map(function(g){return {group:g,edges:boundaryEdges(g.StartPointNumber,g.EndPointNumber)};}).filter(function(x){return x.edges.length&&edges.every(function(edge){return x.edges.indexOf(edge)>=0;});}).sort(function(a,b){return a.edges.length-b.edges.length;}).map(function(x){return x.group;})[0]||null;}
function syncSegmentNeighbors(clearWithoutGroups){var groups=draft.Boundary.SignatureGroups||[];(draft.Boundary.Segments||[]).forEach(function(s){var g=signatureForSegment(s);if(g){s.NeighborOwner=g.NeighborOwner||'';s.NeighborParcelCode=g.NeighborParcelCode||'';}else if(groups.length||clearWithoutGroups){s.NeighborOwner='';s.NeighborParcelCode='';}});}
function autoSignatureGroups(){var groups=[],current=null;(draft.Boundary.Segments||[]).forEach(function(s){var key=(s.NeighborParcelCode||'')+'|'+(s.NeighborOwner||''),inside=String(s.MiddlePointNumbers||'').split(/[、,，;； ]+/).filter(Boolean);if(!current||current._key!==key){current={_key:key,StartPointNumber:s.StartPointNumber||'',MiddlePointNumbers:'/',EndPointNumber:s.EndPointNumber||'',NeighborOwner:s.NeighborOwner||'',NeighborParcelCode:s.NeighborParcelCode||'',NeighborRepresentative:'',ParcelRepresentative:'',ConfirmationDate:'',SignatureStatus:'待签章',PreservePaperSignatureBlank:true,Confirmed:true,Status:0,_middle:inside.slice()};groups.push(current);}else{current._middle.push(s.StartPointNumber||'');current._middle=current._middle.concat(inside);current.EndPointNumber=s.EndPointNumber||'';}});groups.forEach(function(g){g.MiddlePointNumbers=signatureMiddle(g._middle);delete g._middle;delete g._key;});draft.Boundary.SignatureGroups=groups;syncSegmentNeighbors(true);toast('已按连续相邻宗生成 '+groups.length+' 个签章组，请继续填写指界与签章信息。','success');}
function add(type){if(type==='point'){var n=draft.Boundary.Points.length+1;draft.Boundary.Points.push({Sequence:n,PointNumber:'J'+n,X:null,Y:null,DistanceToNext:null,MarkerType:'喷涂',Description:'',Confirmed:true,Status:3});}else if(type==='segment'){var a=draft.Boundary.Segments.length,b=draft.Boundary.Points;draft.Boundary.Segments.push({StartPointNumber:b[a]?b[a].PointNumber:'',MiddlePointNumbers:'',EndPointNumber:b[a+1]?b[a+1].PointNumber:(b.length&&a===b.length-1?b[0].PointNumber:''),Distance:null,LineCategory:'',LinePosition:'待确认',NeighborParcelCode:'',NeighborOwner:'',Direction:'',Description:'',Confirmed:true,NeighborHandled:true,Status:3});}else if(type==='signature'){draft.Boundary.SignatureGroups.push({StartPointNumber:'',MiddlePointNumbers:'/',EndPointNumber:'',NeighborOwner:'',NeighborParcelCode:'',NeighborRepresentative:'',ParcelRepresentative:'',ConfirmationDate:'',SignatureStatus:'',PreservePaperSignatureBlank:true,Confirmed:true,Status:3});}else if(type==='building'){var fs={};(ctx.BuildingFields||[]).forEach(function(d){fs[d.Key]={TextValue:'',NumericValue:null,BooleanValue:false,Selections:[],Status:d.DefaultStatus,Confirmed:false};});var id='b'+Date.now()+Math.random().toString(16).slice(2);draft.Buildings.push({Id:id,Fields:fs});view.buildingId=id;view.buildingEditing=true;view.buildingCategory=0;saveView();}}
function toast(m,k){var old=document.querySelector('.toast');if(old)old.remove();var x=document.createElement('div');x.className='toast '+(k||'');x.textContent=m||'';document.body.appendChild(x);setTimeout(function(){x.remove();},3200);}
function init(){
ensure();renderTop();renderScope();renderTabs();renderContent();restoreView();applySidebarState();bindWorkbench();bindAutoSave();
var sidebar=document.getElementById('sidebar'),sidebarEdge=document.getElementById('sidebarEdge');
sidebarEdge.addEventListener('mouseenter',function(){previewSidebar(true);});
sidebarEdge.addEventListener('mouseleave',function(){previewSidebar(false);});
sidebar.addEventListener('mouseenter',function(){previewSidebar(true);});
sidebar.addEventListener('mouseleave',function(){previewSidebar(false);});
sidebar.addEventListener('focusout',function(){previewSidebar(false);});
window.addEventListener('blur',function(){titleActionHovered=false;previewSidebar(false);});
window.addEventListener('resize',applySidebarState);
document.getElementById('tabs').onclick=function(e){var b=e.target.closest('[data-tab]');if(!b||!canEdit)return;syncAll();captureView();activeTab=Number(b.dataset.tab);view.scrollY=0;saveView();renderTabs();renderContent();document.getElementById('content').scrollTo(0,0);};
document.getElementById('scopeRegion').onchange=function(){if(!this.value)return;if(canEdit)syncAll();captureView();var selected=this.value;leaveParcel(function(){post('switchParcelScope',scopePayload({RecordId:selected}));});};
document.querySelector('.toolbar').onclick=function(e){var b=e.target.closest('[data-scope-action]');if(!b||b.disabled)return;if(canEdit)syncAll();captureView();var action=b.dataset.scopeAction;if(action==='delete'){if(!confirm('确定删除当前宗地信息吗？\n插件生成的区域线将同时删除；已有区域线或权属线只解除绑定。'))return;leaveParcel(function(){post('deleteParcelInfo',scopePayload({RecordId:scope.RecordId}));});return;}if(action==='rename'){showRenameDialog();return;}};
var exportMenu=document.getElementById('exportMenu'),exportButton=document.getElementById('exportSurvey'),exportOptions=document.getElementById('exportOptions');
function closeExportMenu(){exportOptions.hidden=true;exportButton.setAttribute('aria-expanded','false');}
exportButton.onclick=function(e){e.stopPropagation();if(!canEdit)return;var open=exportOptions.hidden;exportOptions.hidden=!open;exportButton.setAttribute('aria-expanded',open?'true':'false');};
exportOptions.onclick=function(e){var b=e.target.closest('[data-export-format]');if(!b)return;e.stopPropagation();closeExportMenu();showExportWarning(b.dataset.exportFormat);};
document.addEventListener('click',function(e){if(!exportMenu.contains(e.target))closeExportMenu();});
document.addEventListener('keydown',function(e){if(e.key==='Escape'){closeExportMenu();previewSidebar(false);}});
window.CDBoxStudioToast=toast;setTimeout(function(){post('ready','parcel-survey-editor');},50);setInterval(function(){if(interactionBusy||document.hidden||autoSaveSuspended||autoSaveFlight||autoSaveContinuation)return;syncAll();post('pollParcelScope',scopePayload());},1800);
}
init();
})();
".Replace("/* WORKBENCH_HELPERS */", ParcelSurveyEditorInteractions.Build() + ParcelSurveyEditorBuildings.Build() + ParcelSurveyEditorAutoSave.Build());
        }

        public sealed class ParcelSurveyAutoSaveRequest
        {
            public int RequestId { get; set; }
            public ParcelSurveyRecord Record { get; set; }
        }

        public sealed class ParcelSurveyViewStateRequest
        {
            public string RecordId { get; set; }
            public Dictionary<string, int> TextAreaHeights { get; set; }

            public ParcelSurveyViewStateRequest()
            {
                RecordId = string.Empty;
                TextAreaHeights = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        private sealed class PageContext
        {
            public ParcelSurveyRecord Record { get; set; }
            public int SidebarExpandedMinimumWidth { get; set; }
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
