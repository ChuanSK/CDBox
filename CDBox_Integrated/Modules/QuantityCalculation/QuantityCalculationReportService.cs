using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    public static class QuantityCalculationReportService
    {
        public static QuantityCalculationReport BuildReport(Document doc)
        {
            return BuildReport(doc, null, null);
        }

        public static QuantityCalculationReport BuildReport(Document doc, IEnumerable<ObjectId> scopeObjectIds)
        {
            return BuildReport(doc, scopeObjectIds, null);
        }

        public static QuantityCalculationReport BuildReport(Document doc, IEnumerable<ObjectId> scopeObjectIds, Action<int, int, string> progress)
        {
            if (doc == null) throw new ArgumentNullException("doc");

            QuantityCalculationReport report = new QuantityCalculationReport();
            List<ObjectId> ids = BuildScopedObjectIdList(doc, scopeObjectIds);
            int mainIndex = 1;
            int branchIndex = 1;
            int wellIndex = 1;

            List<QuantityPipeSelectionInfo> infos = new List<QuantityPipeSelectionInfo>();
            Dictionary<string, QuantityPipeAttributes> wellLookup = new Dictionary<string, QuantityPipeAttributes>(StringComparer.CurrentCultureIgnoreCase);

            int totalIds = Math.Max(ids.Count, 1);
            ReportProgress(progress, 0, totalIds, "正在读取工程量属性...");
            for (int idIndex = 0; idIndex < ids.Count; idIndex++)
            {
                ObjectId id = ids[idIndex];
                QuantityPipeSelectionInfo info = null;
                try
                {
                    info = QuantityPipeAttributeService.ReadPipe(doc, id);
                }
                catch (Exception ex)
                {
                    report.Warnings.Add("读取对象属性失败：" + id.ToString() + "，" + ex.Message);
                    ReportProgress(progress, idIndex + 1, totalIds, "正在读取工程量属性：" + (idIndex + 1) + "/" + totalIds);
                    continue;
                }

                ReportProgress(progress, idIndex + 1, totalIds, "正在读取工程量属性：" + (idIndex + 1) + "/" + totalIds);

                if (info == null || !info.HasSavedAttributes || info.Attributes == null) continue;
                if (!info.Attributes.Enabled) continue;
                infos.Add(info);

                if (QuantityPipeAttributes.IsNodeKind(info.Attributes.ObjectKind))
                {
                    QuantityPipeAttributes wellAttrs = info.Attributes;
                    if (!string.IsNullOrWhiteSpace(wellAttrs.NodeNo))
                    {
                        string nodeKey = wellAttrs.NodeNo.Trim();
                        if (!wellLookup.ContainsKey(nodeKey)) wellLookup.Add(nodeKey, wellAttrs.Clone());
                    }
                }
            }

            int totalInfos = Math.Max(infos.Count, 1);
            ReportProgress(progress, 0, totalInfos, "正在汇总节点/检查井工程量...");
            for (int wellInfoIndex = 0; wellInfoIndex < infos.Count; wellInfoIndex++)
            {
                QuantityPipeSelectionInfo info = infos[wellInfoIndex];
                QuantityPipeAttributes attrs = info.Attributes;
                if (QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind))
                {
                    QuantityDependencyResult normalized = QuantityDependencyService.NormalizeDraft(
                        attrs, QuantityStructureLayer.Parse(attrs.BackfillStructure), attrs, null, null, null, "Report");
                    attrs = normalized.Attributes;
                    QuantityWellCalculationRow row = BuildWellRow(info, attrs, wellIndex);
                    report.Wells.Add(row);
                    wellIndex++;
                }
                ReportProgress(progress, wellInfoIndex + 1, totalInfos, "正在汇总节点/检查井工程量：" + (wellInfoIndex + 1) + "/" + totalInfos);
            }

            ReportProgress(progress, 0, totalInfos, "正在汇总主管工程量...");
            for (int mainInfoIndex = 0; mainInfoIndex < infos.Count; mainInfoIndex++)
            {
                QuantityPipeSelectionInfo info = infos[mainInfoIndex];
                QuantityPipeAttributes attrs = info.Attributes;
                if (QuantityPipeAttributes.IsMainPipeKind(attrs.ObjectKind))
                {
                    attrs = NormalizeForReport(attrs, wellLookup, false);
                    QuantityMainPipeCalculationRow row = BuildMainPipeRow(info, attrs, mainIndex, wellLookup);
                    report.MainPipes.Add(row);
                    mainIndex++;
                }
                ReportProgress(progress, mainInfoIndex + 1, totalInfos, "正在汇总主管工程量：" + (mainInfoIndex + 1) + "/" + totalInfos);
            }

            ReportProgress(progress, 0, totalInfos, "正在汇总支管工程量...");
            for (int branchInfoIndex = 0; branchInfoIndex < infos.Count; branchInfoIndex++)
            {
                QuantityPipeSelectionInfo info = infos[branchInfoIndex];
                QuantityPipeAttributes attrs = info.Attributes;
                if (QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind))
                {
                    attrs = NormalizeForReport(attrs, wellLookup, true);
                    QuantityMainPipeCalculationRow row = BuildBranchPipeRow(info, attrs, branchIndex, wellLookup);
                    report.BranchPipes.Add(row);
                    branchIndex++;
                }
                ReportProgress(progress, branchInfoIndex + 1, totalInfos, "正在汇总支管工程量：" + (branchInfoIndex + 1) + "/" + totalInfos);
            }

            ReportProgress(progress, 1, 1, "工程量数据汇总完成。");
            if (report.MainPipes.Count == 0) report.Warnings.Add("未找到已写入属性且启用统计的主管对象。");
            if (report.BranchPipes.Count == 0) report.Warnings.Add("未找到已写入属性且启用统计的支管对象。");
            if (report.Wells.Count == 0) report.Warnings.Add("未找到已写入属性且启用统计的节点/检查井对象。");
            return report;
        }

        private static QuantityPipeAttributes NormalizeForReport(QuantityPipeAttributes attrs, IDictionary<string, QuantityPipeAttributes> wellLookup, bool branch)
        {
            QuantityPipeAttributes startWell = null;
            QuantityPipeAttributes endWell = null;
            if (!branch && wellLookup != null)
            {
                if (!string.IsNullOrWhiteSpace(attrs.StartNode)) wellLookup.TryGetValue(attrs.StartNode.Trim(), out startWell);
                if (!string.IsNullOrWhiteSpace(attrs.EndNode)) wellLookup.TryGetValue(attrs.EndNode.Trim(), out endWell);
            }
            QuantityPipeAttributes defaults = branch ? QuantityAttributeDefaultStore.LoadForKind(QuantityPipeAttributes.KindBranchPipe) : null;
            return QuantityDependencyService.NormalizeDraft(
                attrs, QuantityStructureLayer.Parse(attrs.BackfillStructure), attrs, startWell, endWell, defaults, "Report").Attributes;
        }

        private static void ReportProgress(Action<int, int, string> progress, int current, int total, string message)
        {
            if (progress == null) return;
            progress(current, total, message);
        }

        private static List<ObjectId> BuildScopedObjectIdList(Document doc, IEnumerable<ObjectId> scopeObjectIds)
        {
            if (scopeObjectIds == null) return QuantityPipeAttributeService.FindObjectsWithSavedAttributes(doc);

            List<ObjectId> ids = new List<ObjectId>();
            foreach (ObjectId id in scopeObjectIds)
            {
                if (id.IsNull) continue;
                if (!ids.Contains(id)) ids.Add(id);
            }
            return ids;
        }

        private static QuantityMainPipeCalculationRow BuildMainPipeRow(QuantityPipeSelectionInfo info, QuantityPipeAttributes attrs, int index, IDictionary<string, QuantityPipeAttributes> wellLookup)
        {
            return BuildPipeRow(info, attrs, index, wellLookup, false);
        }

        private static QuantityMainPipeCalculationRow BuildBranchPipeRow(QuantityPipeSelectionInfo info, QuantityPipeAttributes attrs, int index, IDictionary<string, QuantityPipeAttributes> wellLookup)
        {
            return BuildPipeRow(info, attrs, index, wellLookup, true);
        }

        internal static QuantityMainPipeCalculationRow BuildDashboardPipeRow(QuantityPipeSelectionInfo info, QuantityPipeAttributes attrs, int index, IDictionary<string, QuantityPipeAttributes> wellLookup, bool isBranch)
        {
            return BuildPipeRow(info, attrs, index, wellLookup, isBranch);
        }

        private static QuantityMainPipeCalculationRow BuildPipeRow(QuantityPipeSelectionInfo info, QuantityPipeAttributes attrs, int index, IDictionary<string, QuantityPipeAttributes> wellLookup, bool isBranch)
        {
            List<QuantityStructureLayer> layers = QuantityStructureLayer.Parse(attrs.BackfillStructure);
            double length = Round(attrs.EffectiveLength(info == null ? 0.0 : info.CadLength));
            double width = attrs.TrenchWidth;
            double pipeCushionHeightForDepth = SumLayerHeight(layers, QuantityStructureLayer.IsSandCushion, attrs.SandCushionThickness);
            double startDepth;
            double endDepth;
            double avgDepth;
            if (isBranch)
            {
                avgDepth = attrs.BranchDepth > 0 ? attrs.BranchDepth : attrs.AverageDepth;
                startDepth = avgDepth;
                endDepth = avgDepth;
            }
            else
            {
                startDepth = ResolvePipeEndpointDepth(attrs.StartNode, attrs.StartDepth, wellLookup, pipeCushionHeightForDepth);
                endDepth = ResolvePipeEndpointDepth(attrs.EndNode, attrs.EndDepth, wellLookup, pipeCushionHeightForDepth);
                avgDepth = 0.0;
                if (startDepth > 0 && endDepth > 0) avgDepth = (startDepth + endDepth) / 2.0;
                else if (startDepth > 0) avgDepth = startDepth;
                else if (endDepth > 0) avgDepth = endDepth;
                else avgDepth = 0.0;
            }

            bool exposedBranch = isBranch && ContainsAny(attrs.BranchType, "明管");
            bool coBuried = ContainsAny(attrs.BranchType, "并埋");
            bool hasExplicitEarthwork = avgDepth > 0 && width > 0 && (!string.IsNullOrWhiteSpace(attrs.BackfillStructure) || attrs.RoadThickness > 0);
            bool calculateEarthwork = !coBuried && (!isBranch || (attrs.BranchIncludeInCalculation && (!exposedBranch || hasExplicitEarthwork)));
            double pipeVolume = calculateEarthwork && attrs.DeductPipeVolume ? PipeVolume(length, attrs.PipeOuterDiameter) : 0.0;
            double c25Height = SumPipeC25Height(layers, attrs);
            double gravelHeight = SumLayerHeight(layers, QuantityStructureLayer.IsGravel, attrs.GravelCushionThickness);
            double sandCushionHeight = SumLayerHeight(layers, QuantityStructureLayer.IsSandCushion, attrs.SandCushionThickness);
            double sandBackfillHeight = QuantityStructureLayer.SumHeight(layers, QuantityStructureLayer.IsSandBackfill);
            double soilBackfillHeight = QuantityStructureLayer.SumHeight(layers, QuantityStructureLayer.IsOriginalSoilBackfill);
            double encasementHeight = QuantityStructureLayer.SumHeight(layers, IsPipeEncasementLayer);

            if (!HasLayer(layers, QuantityStructureLayer.IsSandBackfill) && !HasLayer(layers, QuantityStructureLayer.IsOriginalSoilBackfill))
            {
                double remain = QuantityEngineeringMath.CalculatePipeRemainingBackfillHeight(avgDepth, layers);
                if (ContainsAny(attrs.BackfillType, "原土")) soilBackfillHeight = remain;
                else sandBackfillHeight = remain;
            }

            if (!calculateEarthwork)
            {
                c25Height = 0.0;
                gravelHeight = 0.0;
                sandCushionHeight = 0.0;
                sandBackfillHeight = 0.0;
                soilBackfillHeight = 0.0;
                encasementHeight = 0.0;
            }

            double roadCutting = calculateEarthwork && attrs.RoadThickness > 0 ? length * 2.0 : 0.0;
            double roadBreaking = calculateEarthwork && attrs.RoadThickness > 0 ? length * width : 0.0;
            double roadWaste = calculateEarthwork && attrs.RoadThickness > 0 ? length * width * attrs.RoadThickness : 0.0;
            double excavation = calculateEarthwork ? length * width * Math.Max(avgDepth - attrs.RoadThickness, 0.0) : 0.0;
            double mechanical = IsManualExcavation(attrs.ExcavationType) ? 0.0 : excavation;
            double manual = IsManualExcavation(attrs.ExcavationType) ? excavation : 0.0;

            string pipeDeductionTarget = ResolvePipeDeductionTarget(layers, sandBackfillHeight, soilBackfillHeight, encasementHeight);
            double sandCushion = length * width * sandCushionHeight;
            double sandBackfill = Math.Max(0.0, length * width * sandBackfillHeight - (pipeDeductionTarget == "sand" ? pipeVolume : 0.0));
            double gravel = length * width * gravelHeight;
            double c25Restore = length * width * c25Height;
            double originalSoil = Math.Max(0.0, length * width * soilBackfillHeight - (pipeDeductionTarget == "soil" ? pipeVolume : 0.0));
            double encasement = Math.Max(0.0, length * width * encasementHeight - (pipeDeductionTarget == "encasement" ? pipeVolume : 0.0));
            double earthOut = QuantityEngineeringMath.CalculateEarthworkOut(roadWaste, excavation, originalSoil);

            QuantityMainPipeCalculationRow row = new QuantityMainPipeCalculationRow();
            row.Index = index;
            row.HandleText = info == null ? string.Empty : info.HandleText;
            row.LayerName = info == null ? string.Empty : info.LayerName;
            row.StartNode = attrs.StartNode ?? string.Empty;
            row.EndNode = attrs.EndNode ?? string.Empty;
            row.Material = attrs.Material ?? string.Empty;
            row.Diameter = attrs.Diameter ?? string.Empty;
            row.Length = Round(length);
            row.StartDepth = Round(startDepth);
            row.EndDepth = Round(endDepth);
            row.AverageDepth = Round(avgDepth);
            row.TrenchWidth = Round(width);
            row.RoadThickness = Round(attrs.RoadThickness);
            row.ExcavationType = attrs.ExcavationType ?? string.Empty;
            row.BackfillType = attrs.BackfillType ?? string.Empty;
            row.BackfillStructure = attrs.BackfillStructure ?? string.Empty;
            row.PipeOuterDiameter = Round(attrs.PipeOuterDiameter);
            row.PipeDeductionVolume = Round(pipeVolume);
            row.PipeDeductionTarget = pipeDeductionTarget;
            row.RoadCutting = Round(roadCutting);
            row.RoadBreaking = Round(roadBreaking);
            row.RoadWaste = Round(roadWaste);
            row.MechanicalExcavation = Round(mechanical);
            row.ManualExcavation = Round(manual);
            row.SandCushion = Round(sandCushion);
            row.SandBackfill = Round(sandBackfill);
            row.GravelCushion = Round(gravel);
            row.C25Restore = Round(c25Restore);
            row.OriginalSoilBackfill = Round(originalSoil);
            row.C25PipeEncasement = Round(encasement);
            row.EarthworkOut = Round(earthOut);
            row.Remark = string.Empty;
            row.ObjectKind = isBranch ? QuantityPipeAttributes.KindBranchPipe : QuantityPipeAttributes.KindMainPipe;
            row.BranchType = attrs.BranchType ?? string.Empty;
            row.CalculationSource = QuantityDashboardSources.Property;
            row.DataStatus = coBuried ? "仅统计长度（并埋）" : (calculateEarthwork || !isBranch ? "正常" : "仅统计长度");
            row.FormulaText = BuildMainFormulaText(row, c25Height, gravelHeight, sandCushionHeight, sandBackfillHeight, soilBackfillHeight, pipeDeductionTarget);
            return row;
        }

        private static double ResolvePipeEndpointDepth(string nodeNo, double fallback, IDictionary<string, QuantityPipeAttributes> wellLookup, double pipeCushionHeight)
        {
            if (string.IsNullOrWhiteSpace(nodeNo) || wellLookup == null) return fallback;
            QuantityPipeAttributes wellAttrs;
            if (!wellLookup.TryGetValue(nodeNo.Trim(), out wellAttrs) || wellAttrs == null) return fallback;
            return QuantityPipeAttributes.CalculatePipeExcavationDepthByWell(wellAttrs, pipeCushionHeight, fallback);
        }

        internal static QuantityWellCalculationRow BuildDashboardWellRow(QuantityPipeSelectionInfo info, QuantityPipeAttributes attrs, int index)
        {
            return BuildWellRow(info, attrs, index);
        }

        private static QuantityWellCalculationRow BuildWellRow(QuantityPipeSelectionInfo info, QuantityPipeAttributes attrs, int index)
        {
            List<QuantityStructureLayer> layers = QuantityStructureLayer.Parse(attrs.BackfillStructure);
            double excavationLength = attrs.ExcavationLength;
            double excavationWidth = attrs.ExcavationWidth;
            double area = excavationLength * excavationWidth;
            double perimeter = 2.0 * (excavationLength + excavationWidth);
            double radius = InferWellRadius(attrs.WellSpec);
            double wellArea = radius > 0 ? Math.PI * radius * radius : 0.0;
            double effectiveBackfillArea = Math.Max(0.0, area - wellArea);

            double sandCushionHeight = QuantityStructureLayer.SumHeight(layers, QuantityStructureLayer.IsSandCushion);
            if (layers.Count == 0) sandCushionHeight = attrs.SandCushionThickness;
            double wellBottomCushionHeight = SumWellBottomLayerHeight(layers, attrs);
            double c25CushionHeight = QuantityStructureLayer.SumHeight(layers, IsWellC25CushionLayer);
            double coverGravelHeight = QuantityStructureLayer.SumHeight(layers, IsCoverPlateGravelLayer);
            double coverC25Height = QuantityStructureLayer.SumHeight(layers, IsCoverPlateC25Layer);
            double sandBackfillHeight = QuantityStructureLayer.SumHeight(layers, QuantityStructureLayer.IsSandBackfill);

            if (!HasLayer(layers, QuantityStructureLayer.IsSandBackfill))
            {
                // 用户填写的井深只到井下方垫层上方；井下层在井深之下，不参与井内回填层扣减。
                double known = QuantityStructureLayer.SumHeight(layers, IsWellUpperNonBackfillLayer);
                sandBackfillHeight = Math.Max(attrs.WellDepth - known, 0.0);
            }

            double roadCutting = attrs.RoadThickness > 0 ? perimeter : 0.0;
            double roadBreaking = attrs.RoadThickness > 0 ? area : 0.0;
            double roadWaste = attrs.RoadThickness > 0 ? area * attrs.RoadThickness : 0.0;
            double excavation = area * Math.Max(attrs.WellDepth - attrs.RoadThickness + wellBottomCushionHeight, 0.0);
            double mechanical = IsManualExcavation(attrs.ExcavationType) ? 0.0 : excavation;
            double manual = IsManualExcavation(attrs.ExcavationType) ? excavation : 0.0;
            double sandCushion = area * sandCushionHeight;
            double c25Cushion = area * c25CushionHeight;
            double coverGravel = effectiveBackfillArea * coverGravelHeight;
            double coverC25 = effectiveBackfillArea * coverC25Height;
            double sandBackfill = effectiveBackfillArea * sandBackfillHeight;
            // 中粗砂属于新购回填材料，不能抵扣开挖土方外运；只有可回用原土才能抵扣。
            double earthOut = QuantityEngineeringMath.CalculateEarthworkOut(roadWaste, excavation, 0.0);

            QuantityWellCalculationRow row = new QuantityWellCalculationRow();
            row.Index = index;
            row.HandleText = info == null ? string.Empty : info.HandleText;
            row.LayerName = info == null ? string.Empty : info.LayerName;
            row.NodeNo = attrs.NodeNo ?? string.Empty;
            row.WellSpec = attrs.WellSpec ?? string.Empty;
            row.WellType = attrs.WellType ?? string.Empty;
            row.WellMaterialType = attrs.WellMaterialType ?? string.Empty;
            row.WellCoverMaterial = attrs.WellCoverMaterial ?? string.Empty;
            row.GroundElevation = Round(attrs.GroundElevation);
            row.WellDepth = Round(attrs.WellDepth);
            row.ShaftLength = Round(attrs.ShaftLength);
            row.ExcavationLength = Round(excavationLength);
            row.ExcavationWidth = Round(excavationWidth);
            row.RoadThickness = Round(attrs.RoadThickness);
            row.ExcavationType = attrs.ExcavationType ?? string.Empty;
            row.BackfillType = attrs.BackfillType ?? string.Empty;
            row.BackfillStructure = attrs.BackfillStructure ?? string.Empty;
            row.CoverPlate = attrs.CoverPlate ?? string.Empty;
            row.WellRadius = Round(radius);
            row.WellArea = Round(wellArea);
            row.RoadCutting = Round(roadCutting);
            row.RoadBreaking = Round(roadBreaking);
            row.RoadWaste = Round(roadWaste);
            row.MechanicalExcavation = Round(mechanical);
            row.ManualExcavation = Round(manual);
            row.SandCushion = Round(sandCushion);
            row.C25Cushion = Round(c25Cushion);
            row.CoverPlateGravelCushion = Round(coverGravel);
            row.CoverPlateC25Foundation = Round(coverC25);
            row.SandBackfill = Round(sandBackfill);
            row.EarthworkOut = Round(earthOut);
            row.CoverPlateCount = string.IsNullOrWhiteSpace(attrs.CoverPlate) || ContainsAny(attrs.CoverPlate, "无", "不设") ? 0 : 1;
            row.WellCoverCount = 1;
            row.Remark = string.Empty;
            row.CalculationSource = QuantityDashboardSources.Property;
            row.DataStatus = attrs.WellDepth > 0 && excavationLength > 0 && excavationWidth > 0 ? "正常" : "关键属性不完整";
            row.FormulaText = BuildWellFormulaText(row, sandCushionHeight, wellBottomCushionHeight, c25CushionHeight, coverGravelHeight, coverC25Height, sandBackfillHeight);
            return row;
        }

        private static string BuildMainFormulaText(QuantityMainPipeCalculationRow row, double c25Height, double gravelHeight, double sandCushionHeight, double sandBackfillHeight, double soilBackfillHeight, string pipeDeductionTarget)
        {
            List<string> parts = new List<string>();
            parts.Add("路面切缝=L×2");
            parts.Add("路面破碎=L×W");
            parts.Add("开挖=L×W×(H-路面)");
            if (sandCushionHeight > 0) parts.Add("砂垫层=L×W×" + Format(sandCushionHeight));
            if (sandBackfillHeight > 0) parts.Add("砂回填=L×W×" + Format(sandBackfillHeight) + (pipeDeductionTarget == "sand" ? "-管身体积" : string.Empty));
            if (gravelHeight > 0) parts.Add("碎石=L×W×" + Format(gravelHeight));
            if (c25Height > 0) parts.Add("C25=L×W×" + Format(c25Height));
            if (soilBackfillHeight > 0) parts.Add("原土回填=L×W×" + Format(soilBackfillHeight) + (pipeDeductionTarget == "soil" ? "-管身体积" : string.Empty));
            return string.Join("；", parts.ToArray());
        }

        private static string ResolvePipeDeductionTarget(List<QuantityStructureLayer> layers, double sandHeight, double soilHeight, double encasementHeight)
        {
            if (layers != null)
            {
                foreach (QuantityStructureLayer layer in layers)
                {
                    if (layer == null || !layer.IsPipeLayer) continue;
                    if (QuantityStructureLayer.IsSandBackfill(layer)) return "sand";
                    if (QuantityStructureLayer.IsOriginalSoilBackfill(layer)) return "soil";
                    if (IsPipeEncasementLayer(layer)) return "encasement";
                }
            }
            if (sandHeight > 0) return "sand";
            if (soilHeight > 0) return "soil";
            if (encasementHeight > 0) return "encasement";
            return string.Empty;
        }

        private static string BuildWellFormulaText(QuantityWellCalculationRow row, double sandCushionHeight, double wellBottomCushionHeight, double c25CushionHeight, double coverGravelHeight, double coverC25Height, double sandBackfillHeight)
        {
            List<string> parts = new List<string>();
            parts.Add("切缝=2×(长+宽)");
            parts.Add("破碎=长×宽");
            parts.Add(wellBottomCushionHeight > 0 ? "开挖=长×宽×(井深-路面+井下垫层)" : "开挖=长×宽×(井深-路面)");
            if (sandCushionHeight > 0) parts.Add("砂垫层=长×宽×" + Format(sandCushionHeight));
            if (c25CushionHeight > 0) parts.Add("C25垫层=长×宽×" + Format(c25CushionHeight));
            if (coverGravelHeight > 0) parts.Add("盖板碎石=长×宽×" + Format(coverGravelHeight));
            if (coverC25Height > 0) parts.Add("盖板C25=长×宽×" + Format(coverC25Height));
            if (sandBackfillHeight > 0) parts.Add("砂回填=(长×宽-井面积)×" + Format(sandBackfillHeight));
            return string.Join("；", parts.ToArray());
        }

        private static double SumLayerHeight(List<QuantityStructureLayer> layers, Predicate<QuantityStructureLayer> predicate, double fallback)
        {
            double sum = QuantityStructureLayer.SumHeight(layers, predicate);
            return layers != null && layers.Count > 0 ? sum : fallback;
        }

        private static double SumPipeC25Height(List<QuantityStructureLayer> layers, QuantityPipeAttributes attrs)
        {
            double sum = QuantityStructureLayer.SumHeight(layers, delegate(QuantityStructureLayer layer)
            {
                if (layer == null) return false;
                return QuantityStructureLayer.IsC25Restore(layer);
            });
            return layers != null && layers.Count > 0 ? sum : attrs.C25RestoreThickness;
        }

        private static bool IsPipeEncasementLayer(QuantityStructureLayer layer)
        {
            if (layer == null) return false;
            return QuantityStructureLayer.IsConcretePipeEncasement(layer);
        }

        private static bool HasLayer(IEnumerable<QuantityStructureLayer> layers, Predicate<QuantityStructureLayer> predicate)
        {
            if (layers == null || predicate == null) return false;
            foreach (QuantityStructureLayer layer in layers)
            {
                if (layer != null && predicate(layer)) return true;
            }
            return false;
        }

        private static double SumWellBottomLayerHeight(List<QuantityStructureLayer> layers, QuantityPipeAttributes attrs)
        {
            double height = 0.0;
            if (layers != null)
            {
                foreach (QuantityStructureLayer layer in layers)
                {
                    if (layer == null) continue;
                    if (layer.IsPipeLayer) height += layer.Height;
                }
            }

            if ((layers == null || layers.Count == 0) && attrs != null) height = attrs.SandCushionThickness;
            return height < 0 ? 0.0 : height;
        }

        private static bool IsWellUpperNonBackfillLayer(QuantityStructureLayer layer)
        {
            if (layer == null) return false;
            if (layer.IsPipeLayer) return false;
            if (QuantityStructureLayer.IsSandBackfill(layer)) return false;
            if (QuantityStructureLayer.IsOriginalSoilBackfill(layer)) return false;
            return IsWellC25CushionLayer(layer) || IsCoverPlateGravelLayer(layer) || IsCoverPlateC25Layer(layer);
        }

        private static bool IsWellC25CushionLayer(QuantityStructureLayer layer)
        {
            if (layer == null) return false;
            string text = (layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty);
            if (!QuantityStructureLayer.IsC25(layer)) return false;
            if (QuantityStructureLayer.IsCoverPlateLayer(layer)) return false;
            return QuantityStructureLayer.ContainsAny(text, "垫层", "基础");
        }

        private static bool IsCoverPlateGravelLayer(QuantityStructureLayer layer)
        {
            return QuantityStructureLayer.IsCoverPlateLayer(layer) && QuantityStructureLayer.IsGravel(layer);
        }

        private static bool IsCoverPlateC25Layer(QuantityStructureLayer layer)
        {
            return QuantityStructureLayer.IsCoverPlateLayer(layer) && QuantityStructureLayer.IsC25(layer);
        }

        private static double PipeVolume(double length, double outerDiameter)
        {
            if (length <= 0 || outerDiameter <= 0) return 0.0;
            double radius = outerDiameter / 2.0;
            return Math.PI * radius * radius * length;
        }

        private static double InferWellRadius(string wellSpec)
        {
            if (string.IsNullOrWhiteSpace(wellSpec)) return 0.0;
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(wellSpec, @"(?<n>\d{3,4})");
            if (!m.Success) return 0.0;
            double value;
            if (!double.TryParse(m.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return 0.0;
            return value / 2000.0;
        }

        private static bool IsManualExcavation(string excavationType)
        {
            return ContainsAny(excavationType, "人工", "手挖");
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(text) || values == null) return false;
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static double Round(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0.0;
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static string Format(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
