using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using TCPipeAutoDraw.Modules.LayerManager;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    public static class QuantityDashboardService
    {
        public static QuantityDashboardContext BuildContext(QuantityDashboardRequest request)
        {
            request = NormalizeRequest(request);
            Document document = ResolveDocument(request.documentId);
            if (string.IsNullOrWhiteSpace(request.documentId) && document != null) request.documentId = GetDocumentId(document);
            var context = new QuantityDashboardContext { request = request };
            context.documents = GetOpenedDocuments();
            if (document == null) context.regions = new List<QuantityDashboardRegionInfo>();
            else
            {
                using (document.LockDocument()) context.regions = QuantityDashboardRegionService.GetRegions(document);
            }
            context.cachedSnapshot = QuantityDashboardCache.Load(GetCacheKey(request));
            if (context.cachedSnapshot != null)
            {
                context.cachedSnapshot.status.fromCache = true;
                context.cachedSnapshot.status.message = "上次统计结果，正在重新计算……";
            }
            return context;
        }

        public static QuantityDashboardSnapshot BuildSnapshot(QuantityDashboardRequest request, Action<int, int, string> progress)
        {
            request = NormalizeRequest(request);
            Document doc = ResolveDocument(request.documentId);
            if (doc == null) throw new InvalidOperationException("未找到需要统计的已打开图纸。");
            request.documentId = GetDocumentId(doc);

            using (doc.LockDocument())
            {
            QuantityDashboardRegionInfo regionInfo = null;
            ObjectId regionId = ObjectId.Null;
            if (string.Equals(request.scopeType, "region", StringComparison.OrdinalIgnoreCase))
            {
                regionId = QuantityDashboardRegionService.FindRegionObjectId(doc, request.regionId);
                if (regionId.IsNull) throw new InvalidOperationException("固定统计区域不存在或已被删除。");
                regionInfo = QuantityDashboardRegionService.ReadRegion(doc, request.regionId, regionId);
                if (regionInfo == null || !regionInfo.boundaryValid) throw new InvalidOperationException("统计区域边界无效，请检查闭合多段线。");
            }

            DateTime started = DateTime.Now;
            var snapshot = new QuantityDashboardSnapshot();
            snapshot.cacheKey = GetCacheKey(request);
            snapshot.document = BuildDocumentInfo(doc);
            snapshot.scope = new QuantityDashboardScopeInfo
            {
                type = regionInfo == null ? "whole" : "region",
                regionId = regionInfo == null ? string.Empty : regionInfo.regionId,
                regionName = regionInfo == null ? "整张图纸" : regionInfo.regionName,
                pipeRule = "相交管线按整条计入",
                pointRule = "点状对象按中心点计入"
            };
            snapshot.status.isLive = request.liveMode;
            snapshot.status.isCalculating = true;
            snapshot.status.message = "正在统计当前工程量……";

            Report(progress, 0, 6, "正在读取工程量属性……");
            List<ObjectId> allSavedIds = QuantityPipeAttributeService.FindObjectsWithSavedAttributes(doc);
            HashSet<string> scopedHandles;
            List<ObjectId> scopedIds = BuildScopedObjectIdList(doc, allSavedIds, regionId, out scopedHandles);
            Dictionary<string, QuantityPipeSelectionInfo> infoByHandle = ReadScopedInfos(doc, scopedIds, scopedHandles, snapshot);

            Report(progress, 1, 6, "正在复用正式工程量计算逻辑……");
            QuantityCalculationReport report = QuantityCalculationReportService.BuildReport(doc, scopedIds, null);
            foreach (string warning in report.Warnings) snapshot.warnings.Add(warning);

            Report(progress, 2, 6, "正在汇总井类、主管和支管……");
            AddWellRows(snapshot, report.Wells.Where(x => scopedHandles.Contains(x.HandleText)), infoByHandle);
            AddPipeRows(snapshot, report.MainPipes.Where(x => scopedHandles.Contains(x.HandleText)), infoByHandle, false);
            AddPipeRows(snapshot, report.BranchPipes.Where(x => scopedHandles.Contains(x.HandleText)), infoByHandle, true);

            Report(progress, 3, 6, "正在统计 315井、化粪池和其他设施……");
            AddOtherObjects(snapshot, doc, regionId, scopedHandles);

            Report(progress, 4, 6, "正在检查数据完整度……");
            BuildQualityIssues(snapshot, infoByHandle);
            BuildSummary(snapshot);
            BuildReferenceItems(snapshot);
            BuildCharts(snapshot);
            BuildSourceSummary(snapshot);

            Report(progress, 5, 6, "正在保存统计快照……");
            snapshot.status.isCalculating = false;
            snapshot.status.fromCache = false;
            snapshot.status.latestFailed = false;
            snapshot.status.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            snapshot.status.totalObjectCount = snapshot.details.Count;
            snapshot.status.calculatedObjectCount = snapshot.details.Count(x => !string.Equals(x.source, QuantityDashboardSources.Failed, StringComparison.Ordinal));
            snapshot.status.failedObjectCount = snapshot.details.Count(x => string.Equals(x.source, QuantityDashboardSources.Failed, StringComparison.Ordinal));
            snapshot.status.issueCount = snapshot.qualityIssues.Sum(x => x.count);
            snapshot.status.dataCompleteness = CalculateCompleteness(snapshot);
            snapshot.status.message = "已更新 · 用时 " + Math.Max(0.01, (DateTime.Now - started).TotalSeconds).ToString("0.00", CultureInfo.InvariantCulture) + " 秒";
            QuantityDashboardCache.Save(snapshot);
            Report(progress, 6, 6, "工程量统计完成。");
            return snapshot;
            }
        }

        public static List<QuantityDashboardDocumentInfo> GetOpenedDocuments()
        {
            var result = new List<QuantityDashboardDocumentInfo>();
            Document active = AcadApp.DocumentManager.MdiActiveDocument;
            foreach (Document doc in AcadApp.DocumentManager)
            {
                if (doc == null) continue;
                QuantityDashboardDocumentInfo info = BuildDocumentInfo(doc);
                info.isActive = active != null && string.Equals(GetDocumentId(active), info.id, StringComparison.OrdinalIgnoreCase);
                result.Add(info);
            }
            return result;
        }

        public static Document ResolveDocument(string documentId)
        {
            string id = (documentId ?? string.Empty).Trim();
            Document active = AcadApp.DocumentManager.MdiActiveDocument;
            if (id.Length == 0) return active;
            foreach (Document doc in AcadApp.DocumentManager)
            {
                if (doc != null && string.Equals(GetDocumentId(doc), id, StringComparison.OrdinalIgnoreCase)) return doc;
            }
            return active != null && string.Equals(GetDocumentId(active), id, StringComparison.OrdinalIgnoreCase) ? active : null;
        }

        public static string GetDocumentId(Document doc)
        {
            if (doc == null) return string.Empty;
            return (doc.Name ?? string.Empty).Trim();
        }

        public static string GetCacheKey(QuantityDashboardRequest request)
        {
            request = NormalizeRequest(request);
            return (request.documentId ?? string.Empty).Trim().ToLowerInvariant() + "|" + request.scopeType + "|" + (request.regionId ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static QuantityDashboardRequest NormalizeRequest(QuantityDashboardRequest request)
        {
            request = request ?? new QuantityDashboardRequest();
            if (string.IsNullOrWhiteSpace(request.documentId))
            {
                Document active = AcadApp.DocumentManager.MdiActiveDocument;
                request.documentId = GetDocumentId(active);
            }
            request.scopeType = string.Equals(request.scopeType, "region", StringComparison.OrdinalIgnoreCase) ? "region" : "whole";
            if (request.scopeType == "whole") request.regionId = string.Empty;
            return request;
        }

        private static QuantityDashboardDocumentInfo BuildDocumentInfo(Document doc)
        {
            string path = doc == null ? string.Empty : (doc.Name ?? string.Empty);
            string name = path;
            try { name = System.IO.Path.GetFileName(path); } catch { }
            return new QuantityDashboardDocumentInfo
            {
                id = GetDocumentId(doc),
                name = string.IsNullOrWhiteSpace(name) ? "未命名图纸" : name,
                path = path,
                isActive = doc != null && AcadApp.DocumentManager.MdiActiveDocument == doc
            };
        }

        private static List<ObjectId> BuildScopedObjectIdList(Document doc, IEnumerable<ObjectId> ids, ObjectId regionId, out HashSet<string> handles)
        {
            var result = new List<ObjectId>();
            handles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Polyline region = regionId.IsNull ? null : tr.GetObject(regionId, OpenMode.ForRead, false) as Polyline;
                foreach (ObjectId id in ids ?? Enumerable.Empty<ObjectId>())
                {
                    try
                    {
                        Entity entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                        if (entity == null) continue;
                        if (region != null && !QuantityDashboardRegionService.IsEntityIncluded(entity, region)) continue;
                        result.Add(id);
                        handles.Add(entity.Handle.ToString());
                    }
                    catch
                    {
                    }
                }
                tr.Commit();
            }
            return result;
        }

        private static Dictionary<string, QuantityPipeSelectionInfo> ReadScopedInfos(Document doc, IEnumerable<ObjectId> ids, HashSet<string> scopedHandles, QuantityDashboardSnapshot snapshot)
        {
            var result = new Dictionary<string, QuantityPipeSelectionInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId id in ids ?? Enumerable.Empty<ObjectId>())
            {
                try
                {
                    QuantityPipeSelectionInfo info = QuantityPipeAttributeService.ReadPipe(doc, id);
                    if (info == null || !scopedHandles.Contains(info.HandleText)) continue;
                    result[info.HandleText] = info;
                }
                catch (Exception ex)
                {
                    snapshot.warnings.Add("读取工程量对象失败：" + id + "，" + ex.Message);
                }
            }
            return result;
        }

        private static void AddWellRows(QuantityDashboardSnapshot snapshot, IEnumerable<QuantityWellCalculationRow> rows, IDictionary<string, QuantityPipeSelectionInfo> infos)
        {
            int count = 0;
            foreach (QuantityWellCalculationRow row in rows)
            {
                if (row == null) continue;
                count++;
                double excavation = row.MechanicalExcavation + row.ManualExcavation;
                double bedding = row.SandCushion + row.C25Cushion + row.CoverPlateGravelCushion + row.CoverPlateC25Foundation;
                string type = string.IsNullOrWhiteSpace(row.WellType) ? "检查井" : row.WellType;
                Add(snapshot.wells.byType, type, 1);
                Add(snapshot.wells.bySpecification, EmptyAs(row.WellSpec, "未设置"), 1);
                Add(snapshot.wells.byMaterial, EmptyAs(row.WellMaterialType, "未设置"), 1);
                snapshot.wells.count++;
                snapshot.wells.cumulativeDepth += row.WellDepth;
                snapshot.wells.excavationVolume += excavation;
                snapshot.wells.backfillVolume += row.SandBackfill;
                snapshot.wells.beddingVolume += bedding;
                snapshot.wells.roadCuttingLength += row.RoadCutting;
                snapshot.wells.roadBreakingArea += row.RoadBreaking;
                snapshot.wells.roadWasteVolume += row.RoadWaste;
                snapshot.wells.mechanicalExcavationVolume += row.MechanicalExcavation;
                snapshot.wells.manualExcavationVolume += row.ManualExcavation;
                snapshot.wells.earthworkOutVolume += row.EarthworkOut;
                snapshot.wells.concreteVolume += row.C25Cushion + row.CoverPlateC25Foundation;
                snapshot.wells.sandVolume += row.SandCushion + row.SandBackfill;
                snapshot.wells.gravelVolume += row.CoverPlateGravelCushion;
                snapshot.wells.restorationArea += row.RoadBreaking;
                snapshot.wells.coverCount += row.WellCoverCount;
                Add(snapshot.wells.byBudgetClassification, BuildWellBudgetName(row), 1);
                if (row.WellCoverCount > 0) Add(snapshot.wells.byCoverMaterial, EmptyAs(row.WellCoverMaterial, "未设置井盖材料"), row.WellCoverCount);
                if (row.CoverPlateCount > 0) Add(snapshot.wells.byCoverMaterial, EmptyAs(row.CoverPlate, "承压盖板"), row.CoverPlateCount);
                AddWellLayerMaterials(snapshot.wells, row);
                if (row.RoadBreaking > 0) Add(snapshot.wells.byRestorationType, ResolveRestorationType(row.BackfillStructure, string.Empty, row.BackfillType), row.RoadBreaking);
                snapshot.details.Add(new QuantityDashboardDetailRow
                {
                    handle = row.HandleText,
                    category = "井类",
                    subcategory = type,
                    name = EmptyAs(row.NodeNo, type),
                    layerName = row.LayerName,
                    material = row.WellMaterialType,
                    specification = row.WellSpec,
                    depth = row.WellDepth,
                    width = Math.Max(row.ExcavationLength, row.ExcavationWidth),
                    excavationVolume = Round(excavation),
                    backfillVolume = Round(row.SandBackfill),
                    beddingVolume = Round(bedding),
                    restorationArea = Round(row.RoadBreaking),
                    concreteVolume = Round(row.C25Cushion + row.CoverPlateC25Foundation),
                    count = 1,
                    source = QuantityDashboardSources.Property,
                    status = row.DataStatus
                });
            }
            snapshot.wells.averageDepth = count == 0 ? 0.0 : snapshot.wells.cumulativeDepth / count;
            RoundCategory(snapshot.wells);
        }

        private static void AddPipeRows(QuantityDashboardSnapshot snapshot, IEnumerable<QuantityMainPipeCalculationRow> rows, IDictionary<string, QuantityPipeSelectionInfo> infos, bool branch)
        {
            QuantityDashboardCategorySummary category = branch ? snapshot.branchPipes : snapshot.mainPipes;
            double depthSum = 0.0;
            foreach (QuantityMainPipeCalculationRow row in rows)
            {
                if (row == null) continue;
                double excavation = row.MechanicalExcavation + row.ManualExcavation;
                double backfill = row.SandBackfill;
                double bedding = row.SandCushion + row.GravelCushion;
                double pipeDeduction = row.PipeOuterDiameter > 0 ? Math.PI * Math.Pow(row.PipeOuterDiameter / 2.0, 2.0) * row.Length : 0.0;
                category.count++;
                category.length += row.Length;
                depthSum += row.AverageDepth;
                category.excavationVolume += excavation;
                category.backfillVolume += backfill;
                category.pipeDeductionVolume += pipeDeduction;
                category.beddingVolume += bedding;
                category.restorationArea += row.RoadBreaking;
                category.roadCuttingLength += row.RoadCutting;
                category.roadBreakingArea += row.RoadBreaking;
                category.roadWasteVolume += row.RoadWaste;
                category.mechanicalExcavationVolume += row.MechanicalExcavation;
                category.manualExcavationVolume += row.ManualExcavation;
                category.earthworkOutVolume += row.EarthworkOut;
                category.concreteVolume += row.C25Restore + row.C25PipeEncasement;
                category.sandVolume += row.SandCushion + row.SandBackfill;
                category.gravelVolume += row.GravelCushion;
                category.originalSoilVolume += row.OriginalSoilBackfill;
                Add(category.byMaterial, EmptyAs(row.Material, "未设置"), row.Length);
                Add(category.bySpecification, EmptyAs(row.Diameter, "未设置"), row.Length);
                Add(category.byType, branch ? EmptyAs(row.BranchType, "未设置") : "主管", row.Length);
                Add(category.byBudgetClassification, BuildPipeBudgetName(row), row.Length);
                AddPipeLayerMaterials(category, row);
                if (row.RoadBreaking > 0) Add(category.byRestorationType, ResolveRestorationType(row.BackfillStructure, row.BranchType, row.BackfillType), row.RoadBreaking);
                if (branch && ContainsAny(row.BranchType, "明管")) category.exposedPipeLength += row.Length;
                if (ContainsAny(row.BranchType, "并埋")) category.coBuriedLength += row.Length;
                snapshot.details.Add(new QuantityDashboardDetailRow
                {
                    handle = row.HandleText,
                    category = branch ? "支管" : "主管",
                    subcategory = branch ? EmptyAs(row.BranchType, "支管") : "主管",
                    name = row.LayerName,
                    layerName = row.LayerName,
                    material = row.Material,
                    specification = row.Diameter,
                    startNode = row.StartNode,
                    endNode = row.EndNode,
                    length = row.Length,
                    depth = row.AverageDepth,
                    width = row.TrenchWidth,
                    excavationVolume = Round(excavation),
                    backfillVolume = Round(backfill),
                    beddingVolume = Round(bedding),
                    restorationArea = Round(row.RoadBreaking),
                    concreteVolume = Round(row.C25Restore + row.C25PipeEncasement),
                    count = 1,
                    source = QuantityDashboardSources.Property,
                    status = row.DataStatus
                });
            }
            category.averageDepth = category.count == 0 ? 0.0 : depthSum / category.count;
            RoundCategory(category);
        }

        private static void AddOtherObjects(QuantityDashboardSnapshot snapshot, Document doc, ObjectId regionId, HashSet<string> attributedHandles)
        {
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Polyline region = regionId.IsNull ? null : tr.GetObject(regionId, OpenMode.ForRead, false) as Polyline;
                BlockTableRecord space = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead, false) as BlockTableRecord;
                if (space == null) return;
                foreach (ObjectId id in space)
                {
                    Entity entity = null;
                    try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; } catch { }
                    if (entity == null || attributedHandles.Contains(entity.Handle.ToString())) continue;
                    if (!regionId.IsNull && id == regionId) continue;
                    if (region != null && !QuantityDashboardRegionService.IsEntityIncluded(entity, region)) continue;
                    LayerMetadata metadata = LayerManagerService.GetLayerMetadata(db, tr, entity.Layer);
                    QuantityDashboardDetailRow detail;
                    if (!QuantityDashboardOtherProviderRegistry.TryCreate(entity, metadata, out detail)) continue;
                    snapshot.details.Add(detail);
                    snapshot.others.count++;
                    snapshot.others.excavationVolume += detail.excavationVolume;
                    snapshot.others.backfillVolume += detail.backfillVolume;
                    snapshot.others.beddingVolume += detail.beddingVolume;
                    snapshot.others.concreteVolume += detail.concreteVolume;
                    Add(snapshot.others.byType, detail.subcategory, 1);
                    Add(snapshot.others.bySpecification, EmptyAs(detail.specification, "未设置"), 1);
                }
                tr.Commit();
            }
            RoundCategory(snapshot.others);
        }

        private static void BuildQualityIssues(QuantityDashboardSnapshot snapshot, IDictionary<string, QuantityPipeSelectionInfo> infos)
        {
            foreach (QuantityDashboardDetailRow detail in snapshot.details)
            {
                QuantityPipeSelectionInfo info;
                infos.TryGetValue(detail.handle ?? string.Empty, out info);
                QuantityPipeAttributes attrs = info == null ? null : info.Attributes;
                if (attrs != null)
                {
                    Check(detail, snapshot, "missingParent", "缺少父属性", string.IsNullOrWhiteSpace(attrs.LayerParentGroup), "warning");
                    Check(detail, snapshot, "missingCategory", "缺少分类", detail.category != "主管" && string.IsNullOrWhiteSpace(attrs.LayerParentClass), "warning");
                    Check(detail, snapshot, "missingTags", "缺少标签", string.IsNullOrWhiteSpace(attrs.LayerTags), "info");
                    if (detail.category == "主管" || detail.category == "支管")
                    {
                        Check(detail, snapshot, "missingDiameter", "缺少管径", string.IsNullOrWhiteSpace(attrs.Diameter), "warning");
                        bool lengthOnly = ContainsAny(attrs.BranchType, "明管", "并埋");
                        Check(detail, snapshot, "missingDepth", "缺少平均深度", detail.depth <= 0 && !lengthOnly, "warning");
                        Check(detail, snapshot, "missingWidth", "开挖宽度无法确定", detail.width <= 0 && !lengthOnly, "warning");
                        Check(detail, snapshot, "missingStructure", "缺少结构层", string.IsNullOrWhiteSpace(attrs.BackfillStructure) && !lengthOnly, "warning");
                        if (detail.category == "主管")
                        {
                            Check(detail, snapshot, "missingStartNode", "缺少起点井", string.IsNullOrWhiteSpace(attrs.StartNode), "warning");
                            Check(detail, snapshot, "missingEndNode", "缺少终点井", string.IsNullOrWhiteSpace(attrs.EndNode), "warning");
                        }
                    }
                    else if (detail.category == "井类")
                    {
                        Check(detail, snapshot, "missingWellSpec", "缺少井规格", string.IsNullOrWhiteSpace(attrs.WellSpec), "warning");
                        Check(detail, snapshot, "missingWellDepth", "缺少井深", attrs.WellDepth <= 0, "error");
                    }
                }
                if (string.Equals(detail.source, QuantityDashboardSources.DefaultEstimate, StringComparison.Ordinal))
                    Check(detail, snapshot, "defaultEstimate", "使用默认参数估算", true, "info");
                if (string.Equals(detail.source, QuantityDashboardSources.CountOnly, StringComparison.Ordinal))
                    Check(detail, snapshot, "countOnly", "无法计算详细工程量", true, "warning");
                if (string.Equals(detail.source, QuantityDashboardSources.Failed, StringComparison.Ordinal))
                    Check(detail, snapshot, "calculationFailed", "工程量计算失败", true, "error");
            }
        }

        private static void Check(QuantityDashboardDetailRow detail, QuantityDashboardSnapshot snapshot, string code, string title, bool condition, string severity)
        {
            if (!condition) return;
            detail.issueCodes.Add(code);
            if (detail.status == "正常") detail.status = title;
            QuantityDashboardQualityIssue issue = snapshot.qualityIssues.FirstOrDefault(x => string.Equals(x.code, code, StringComparison.OrdinalIgnoreCase));
            if (issue == null)
            {
                issue = new QuantityDashboardQualityIssue { code = code, title = title, severity = severity, category = detail.category, message = title };
                snapshot.qualityIssues.Add(issue);
            }
            issue.count++;
            if (!string.IsNullOrWhiteSpace(detail.handle) && issue.handles.Count < 200) issue.handles.Add(detail.handle);
        }

        private static void BuildSummary(QuantityDashboardSnapshot snapshot)
        {
            snapshot.summary.mainPipeLength = snapshot.mainPipes.length;
            snapshot.summary.branchPipeLength = snapshot.branchPipes.length;
            snapshot.summary.totalPipeLength = snapshot.mainPipes.length + snapshot.branchPipes.length;
            snapshot.summary.excavationVolume = snapshot.wells.excavationVolume + snapshot.mainPipes.excavationVolume + snapshot.branchPipes.excavationVolume + snapshot.others.excavationVolume;
            snapshot.summary.backfillVolume = snapshot.wells.backfillVolume + snapshot.mainPipes.backfillVolume + snapshot.branchPipes.backfillVolume + snapshot.others.backfillVolume;
            snapshot.summary.beddingVolume = snapshot.wells.beddingVolume + snapshot.mainPipes.beddingVolume + snapshot.branchPipes.beddingVolume + snapshot.others.beddingVolume;
            snapshot.summary.restorationArea = snapshot.wells.restorationArea + snapshot.mainPipes.restorationArea + snapshot.branchPipes.restorationArea + snapshot.others.restorationArea;
            snapshot.summary.roadCuttingLength = snapshot.wells.roadCuttingLength + snapshot.mainPipes.roadCuttingLength + snapshot.branchPipes.roadCuttingLength + snapshot.others.roadCuttingLength;
            snapshot.summary.roadBreakingArea = snapshot.wells.roadBreakingArea + snapshot.mainPipes.roadBreakingArea + snapshot.branchPipes.roadBreakingArea + snapshot.others.roadBreakingArea;
            snapshot.summary.roadWasteVolume = snapshot.wells.roadWasteVolume + snapshot.mainPipes.roadWasteVolume + snapshot.branchPipes.roadWasteVolume + snapshot.others.roadWasteVolume;
            snapshot.summary.mechanicalExcavationVolume = snapshot.wells.mechanicalExcavationVolume + snapshot.mainPipes.mechanicalExcavationVolume + snapshot.branchPipes.mechanicalExcavationVolume + snapshot.others.mechanicalExcavationVolume;
            snapshot.summary.manualExcavationVolume = snapshot.wells.manualExcavationVolume + snapshot.mainPipes.manualExcavationVolume + snapshot.branchPipes.manualExcavationVolume + snapshot.others.manualExcavationVolume;
            snapshot.summary.earthworkOutVolume = snapshot.wells.earthworkOutVolume + snapshot.mainPipes.earthworkOutVolume + snapshot.branchPipes.earthworkOutVolume + snapshot.others.earthworkOutVolume;
            snapshot.summary.concreteVolume = snapshot.wells.concreteVolume + snapshot.mainPipes.concreteVolume + snapshot.branchPipes.concreteVolume + snapshot.others.concreteVolume;
            snapshot.summary.sandVolume = snapshot.wells.sandVolume + snapshot.mainPipes.sandVolume + snapshot.branchPipes.sandVolume;
            snapshot.summary.gravelVolume = snapshot.wells.gravelVolume + snapshot.mainPipes.gravelVolume + snapshot.branchPipes.gravelVolume;
            snapshot.summary.wellCount = snapshot.wells.count;
            snapshot.summary.otherFacilityCount = snapshot.others.count;
            snapshot.summary.facilityCount = snapshot.wells.count + snapshot.others.count;
            RoundSummary(snapshot.summary);
        }

        private static void BuildReferenceItems(QuantityDashboardSnapshot snapshot)
        {
            Dictionary<string, double> pipeTypes = new Dictionary<string, double>(StringComparer.CurrentCultureIgnoreCase);
            Merge(pipeTypes, snapshot.mainPipes.byBudgetClassification);
            Merge(pipeTypes, snapshot.branchPipes.byBudgetClassification);
            AddBudgetItems(snapshot.referenceItems, pipeTypes, "m", string.Empty, QuantityDashboardSources.Property);
            AddCountItems(snapshot.referenceItems, snapshot.wells.byBudgetClassification, "座", QuantityDashboardSources.Property);
            AddCountItems(snapshot.referenceItems, snapshot.others.byType, "个", "混合来源");
            snapshot.referenceItems.Add(Item("路面机械切缝", snapshot.summary.roadCuttingLength, "m", "按沟槽及构筑物边界", "混合来源"));
            snapshot.referenceItems.Add(Item("路面破碎", snapshot.summary.roadBreakingArea, "㎡", "按实际恢复范围", "混合来源"));
            snapshot.referenceItems.Add(Item("拆除路面弃置", snapshot.summary.roadWasteVolume, "m³", "路面破碎面积×结构层厚度", "混合来源"));
            snapshot.referenceItems.Add(Item("开挖基坑/沟槽土方（机械）", snapshot.summary.mechanicalExcavationVolume, "m³", "并埋不计开挖", "混合来源"));
            snapshot.referenceItems.Add(Item("开挖基坑/沟槽土方（人工）", snapshot.summary.manualExcavationVolume, "m³", "并埋不计开挖", "混合来源"));
            Dictionary<string, double> materials = new Dictionary<string, double>(StringComparer.CurrentCultureIgnoreCase);
            Merge(materials, snapshot.mainPipes.byLayerMaterial);
            Merge(materials, snapshot.branchPipes.byLayerMaterial);
            Merge(materials, snapshot.wells.byLayerMaterial);
            Merge(materials, snapshot.others.byLayerMaterial);
            foreach (KeyValuePair<string, double> pair in materials.OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
                snapshot.referenceItems.Add(Item(pair.Key, pair.Value, "m³", "按结构层材料/强度分列", "混合来源"));
            Dictionary<string, double> restorations = new Dictionary<string, double>(StringComparer.CurrentCultureIgnoreCase);
            Merge(restorations, snapshot.mainPipes.byRestorationType);
            Merge(restorations, snapshot.branchPipes.byRestorationType);
            Merge(restorations, snapshot.wells.byRestorationType);
            foreach (KeyValuePair<string, double> pair in restorations.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
                snapshot.referenceItems.Add(Item(pair.Key + "（路面恢复）", pair.Value, "㎡", "不同恢复材料单独计价", "混合来源"));
            AddCountItems(snapshot.referenceItems, snapshot.wells.byCoverMaterial, "个", QuantityDashboardSources.Property);
            snapshot.referenceItems.Add(Item("余土及道渣弃置（外运）", snapshot.summary.earthworkOutVolume, "m³", "开挖及拆除量扣除可利用原土", "混合来源"));
        }

        private static void AddBudgetItems(List<QuantityDashboardReferenceItem> items, Dictionary<string, double> values, string unit, string remark, string source)
        {
            foreach (KeyValuePair<string, double> pair in values.OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
                items.Add(Item(pair.Key, pair.Value, unit, remark, source));
        }

        private static void AddCountItems(List<QuantityDashboardReferenceItem> items, Dictionary<string, double> values, string unit, string source)
        {
            foreach (KeyValuePair<string, double> pair in values.OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                items.Add(Item(pair.Key, pair.Value, unit, string.Empty, source));
            }
        }

        private static QuantityDashboardReferenceItem Item(string name, double value, string unit, string remark, string source)
        {
            return new QuantityDashboardReferenceItem { item = name, quantity = Round(value), unit = unit, remark = remark, source = source };
        }

        private static void BuildCharts(QuantityDashboardSnapshot snapshot)
        {
            foreach (KeyValuePair<string, double> pair in snapshot.wells.byType) snapshot.charts.facilities.Add(Chart(pair.Key, pair.Value, "座", "井类|" + pair.Key));
            foreach (KeyValuePair<string, double> pair in snapshot.others.byType) snapshot.charts.facilities.Add(Chart(pair.Key, pair.Value, "个", "其他|" + pair.Key));
            snapshot.charts.pipeLengths.Add(Chart("主管", snapshot.mainPipes.length, "m", "主管"));
            snapshot.charts.pipeLengths.Add(Chart("支管", snapshot.branchPipes.length, "m", "支管"));
            snapshot.charts.diametersMain = snapshot.mainPipes.bySpecification.Select(x => Chart(x.Key, x.Value, "m", "主管|" + x.Key)).OrderByDescending(x => x.value).ToList();
            snapshot.charts.diametersBranch = snapshot.branchPipes.bySpecification.Select(x => Chart(x.Key, x.Value, "m", "支管|" + x.Key)).OrderByDescending(x => x.value).ToList();
            snapshot.charts.excavation.Add(Chart("主管", snapshot.mainPipes.excavationVolume, "m³", "主管"));
            snapshot.charts.excavation.Add(Chart("支管", snapshot.branchPipes.excavationVolume, "m³", "支管"));
            snapshot.charts.excavation.Add(Chart("井类", snapshot.wells.excavationVolume, "m³", "井类"));
            snapshot.charts.excavation.Add(Chart("其他", snapshot.others.excavationVolume, "m³", "其他"));
            snapshot.charts.backfill.Add(Chart("主管", snapshot.mainPipes.backfillVolume, "m³", "主管"));
            snapshot.charts.backfill.Add(Chart("支管", snapshot.branchPipes.backfillVolume, "m³", "支管"));
            snapshot.charts.backfill.Add(Chart("井类", snapshot.wells.backfillVolume, "m³", "井类"));
            snapshot.charts.backfill.Add(Chart("其他", snapshot.others.backfillVolume, "m³", "其他"));
            Dictionary<string, double> materials = new Dictionary<string, double>(StringComparer.CurrentCultureIgnoreCase);
            Merge(materials, snapshot.mainPipes.byLayerMaterial); Merge(materials, snapshot.branchPipes.byLayerMaterial);
            Add(materials, "井类垫层", snapshot.wells.beddingVolume); Add(materials, "其他设施垫层", snapshot.others.beddingVolume);
            snapshot.charts.layerMaterials = materials.Select(x => Chart(x.Key, x.Value, "m³", x.Key)).Where(x => x.value > 0).OrderByDescending(x => x.value).ToList();
            snapshot.charts.restorations.Add(Chart("主管恢复", snapshot.mainPipes.restorationArea, "㎡", "主管"));
            snapshot.charts.restorations.Add(Chart("支管恢复", snapshot.branchPipes.restorationArea, "㎡", "支管"));
            snapshot.charts.restorations.Add(Chart("井类恢复", snapshot.wells.restorationArea, "㎡", "井类"));
        }

        private static QuantityDashboardChartItem Chart(string name, double value, string unit, string filter)
        {
            return new QuantityDashboardChartItem { name = name, value = Round(value), unit = unit, filter = filter };
        }

        private static void BuildSourceSummary(QuantityDashboardSnapshot snapshot)
        {
            foreach (QuantityDashboardDetailRow detail in snapshot.details)
            {
                if (detail.source == QuantityDashboardSources.ExactGeometry || detail.source == QuantityDashboardSources.Property) snapshot.sourceSummary.propertyOrGeometryCount++;
                else if (detail.source == QuantityDashboardSources.DefaultEstimate) snapshot.sourceSummary.defaultEstimateCount++;
                else if (detail.source == QuantityDashboardSources.CountOnly) snapshot.sourceSummary.countOnlyCount++;
                else snapshot.sourceSummary.failedCount++;
            }
            int total = Math.Max(1, snapshot.details.Count);
            snapshot.sourceSummary.propertyOrGeometryPercent = Round(snapshot.sourceSummary.propertyOrGeometryCount * 100.0 / total);
            snapshot.sourceSummary.defaultEstimatePercent = Round(snapshot.sourceSummary.defaultEstimateCount * 100.0 / total);
            snapshot.sourceSummary.countOnlyPercent = Round(snapshot.sourceSummary.countOnlyCount * 100.0 / total);
            snapshot.sourceSummary.failedPercent = Round(snapshot.sourceSummary.failedCount * 100.0 / total);
        }

        private static double CalculateCompleteness(QuantityDashboardSnapshot snapshot)
        {
            int total = snapshot.details.Count;
            if (total == 0) return 0.0;
            double weighted = snapshot.sourceSummary.propertyOrGeometryCount + snapshot.sourceSummary.defaultEstimateCount * 0.72 + snapshot.sourceSummary.countOnlyCount * 0.35;
            double issuePenalty = Math.Min(20.0, snapshot.qualityIssues.Where(x => x.severity == "error").Sum(x => x.count) * 3.0 + snapshot.qualityIssues.Where(x => x.severity == "warning").Sum(x => x.count) * 0.45);
            return Math.Max(0.0, Math.Min(100.0, weighted * 100.0 / total - issuePenalty));
        }

        private static string BuildPipeBudgetName(QuantityMainPipeCalculationRow row)
        {
            return QuantityDashboardClassification.BuildPipeType(row.Diameter, row.Material);
        }

        private static string BuildWellBudgetName(QuantityWellCalculationRow row)
        {
            var parts = new List<string>();
            parts.Add(EmptyAs(row.WellSpec, "未设置井规格"));
            parts.Add(EmptyAs(row.WellMaterialType, "未设置井材料"));
            parts.Add(EmptyAs(row.WellType, "检查井"));
            return string.Join(" · ", parts.ToArray());
        }

        private static void AddPipeLayerMaterials(QuantityDashboardCategorySummary category, QuantityMainPipeCalculationRow row)
        {
            List<QuantityStructureLayer> layers = QuantityStructureLayer.Parse(row.BackfillStructure);
            AddPositive(category.byLayerMaterial, FirstLayerName(layers, QuantityStructureLayer.IsSandCushion, "中粗砂垫层"), row.SandCushion);
            AddPositive(category.byLayerMaterial, FirstLayerName(layers, QuantityStructureLayer.IsSandBackfill, "中粗砂回填（包管）"), row.SandBackfill);
            AddPositive(category.byLayerMaterial, FirstLayerName(layers, QuantityStructureLayer.IsGravel, "碎石垫层"), row.GravelCushion);
            AddPositive(category.byLayerMaterial, FirstLayerName(layers, delegate(QuantityStructureLayer layer)
            {
                return QuantityStructureLayer.IsC25(layer) && !ContainsAny((layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty), "包管", "包封");
            }, "C25混凝土恢复"), row.C25Restore);
            AddPositive(category.byLayerMaterial, FirstLayerName(layers, delegate(QuantityStructureLayer layer)
            {
                return ContainsAny((layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty), "包管", "包封");
            }, "混凝土包管"), row.C25PipeEncasement);
        }

        private static void AddWellLayerMaterials(QuantityDashboardCategorySummary category, QuantityWellCalculationRow row)
        {
            List<QuantityStructureLayer> layers = QuantityStructureLayer.Parse(row.BackfillStructure);
            AddPositive(category.byLayerMaterial, FirstLayerName(layers, QuantityStructureLayer.IsSandCushion, "中粗砂垫层"), row.SandCushion);
            AddPositive(category.byLayerMaterial, FirstLayerName(layers, delegate(QuantityStructureLayer layer)
            {
                return QuantityStructureLayer.IsC25(layer) && !QuantityStructureLayer.IsCoverPlateLayer(layer);
            }, "C25混凝土垫层"), row.C25Cushion);
            AddPositive(category.byLayerMaterial, FirstLayerName(layers, delegate(QuantityStructureLayer layer)
            {
                return QuantityStructureLayer.IsCoverPlateLayer(layer) && QuantityStructureLayer.IsGravel(layer);
            }, "承压盖板碎石垫层"), row.CoverPlateGravelCushion);
            AddPositive(category.byLayerMaterial, FirstLayerName(layers, delegate(QuantityStructureLayer layer)
            {
                return QuantityStructureLayer.IsCoverPlateLayer(layer) && QuantityStructureLayer.IsC25(layer);
            }, "承压盖板C25混凝土基础"), row.CoverPlateC25Foundation);
            AddPositive(category.byLayerMaterial, FirstLayerName(layers, QuantityStructureLayer.IsSandBackfill, "中粗砂回填"), row.SandBackfill);
        }

        private static string FirstLayerName(IEnumerable<QuantityStructureLayer> layers, Predicate<QuantityStructureLayer> predicate, string fallback)
        {
            if (layers != null && predicate != null)
            {
                foreach (QuantityStructureLayer layer in layers)
                {
                    if (layer != null && predicate(layer) && !string.IsNullOrWhiteSpace(layer.Name)) return layer.Name.Trim();
                }
            }
            return fallback;
        }

        private static string ResolveRestorationType(string structureText, string branchType, string backfillType)
        {
            foreach (QuantityStructureLayer layer in QuantityStructureLayer.Parse(structureText))
            {
                string text = (layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty);
                if (ContainsAny(text, "恢复") && !string.IsNullOrWhiteSpace(layer.Name)) return layer.Name.Trim();
            }
            if (ContainsAny(branchType, "恢复")) return branchType.Trim();
            if (ContainsAny(backfillType, "恢复")) return backfillType.Trim();
            return "未设置恢复材料";
        }

        private static void AddPositive(Dictionary<string, double> values, string key, double amount)
        {
            if (amount > 0) Add(values, key, amount);
        }

        private static void RoundSummary(QuantityDashboardSummary s)
        {
            s.totalPipeLength = Round(s.totalPipeLength); s.mainPipeLength = Round(s.mainPipeLength); s.branchPipeLength = Round(s.branchPipeLength);
            s.excavationVolume = Round(s.excavationVolume); s.backfillVolume = Round(s.backfillVolume); s.beddingVolume = Round(s.beddingVolume);
            s.restorationArea = Round(s.restorationArea); s.concreteVolume = Round(s.concreteVolume); s.sandVolume = Round(s.sandVolume); s.gravelVolume = Round(s.gravelVolume);
            s.roadCuttingLength = Round(s.roadCuttingLength); s.roadBreakingArea = Round(s.roadBreakingArea); s.roadWasteVolume = Round(s.roadWasteVolume);
            s.mechanicalExcavationVolume = Round(s.mechanicalExcavationVolume); s.manualExcavationVolume = Round(s.manualExcavationVolume); s.earthworkOutVolume = Round(s.earthworkOutVolume);
        }

        private static void RoundCategory(QuantityDashboardCategorySummary c)
        {
            c.length = Round(c.length); c.averageDepth = Round(c.averageDepth); c.excavationVolume = Round(c.excavationVolume); c.backfillVolume = Round(c.backfillVolume);
            c.pipeDeductionVolume = Round(c.pipeDeductionVolume); c.beddingVolume = Round(c.beddingVolume); c.restorationArea = Round(c.restorationArea);
            c.concreteVolume = Round(c.concreteVolume); c.sandVolume = Round(c.sandVolume); c.gravelVolume = Round(c.gravelVolume); c.originalSoilVolume = Round(c.originalSoilVolume);
            c.cumulativeDepth = Round(c.cumulativeDepth); c.exposedPipeLength = Round(c.exposedPipeLength); c.coBuriedLength = Round(c.coBuriedLength);
            c.roadCuttingLength = Round(c.roadCuttingLength); c.roadBreakingArea = Round(c.roadBreakingArea); c.roadWasteVolume = Round(c.roadWasteVolume);
            c.mechanicalExcavationVolume = Round(c.mechanicalExcavationVolume); c.manualExcavationVolume = Round(c.manualExcavationVolume); c.earthworkOutVolume = Round(c.earthworkOutVolume);
            RoundDictionary(c.byMaterial); RoundDictionary(c.bySpecification); RoundDictionary(c.byType); RoundDictionary(c.byLayerMaterial);
            RoundDictionary(c.byBudgetClassification); RoundDictionary(c.byRestorationType); RoundDictionary(c.byCoverMaterial);
        }

        private static void RoundDictionary(Dictionary<string, double> values)
        {
            foreach (string key in values.Keys.ToList()) values[key] = Round(values[key]);
        }

        private static void Add(Dictionary<string, double> values, string key, double amount)
        {
            key = EmptyAs(key, "未设置");
            double current;
            values.TryGetValue(key, out current);
            values[key] = current + amount;
        }

        private static void Merge(Dictionary<string, double> target, Dictionary<string, double> source)
        {
            foreach (KeyValuePair<string, double> item in source) Add(target, item.Key, item.Value);
        }

        private static string EmptyAs(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (string value in values) if (!string.IsNullOrWhiteSpace(value) && text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            return false;
        }

        private static double Round(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0.0;
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static void Report(Action<int, int, string> progress, int current, int total, string message)
        {
            if (progress != null) progress(current, total, message);
        }
    }
}
