using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    public sealed class WastewaterQuantityAttributeEngine
        : IQuantityAttributeEngine
    {
        public void ApplySmartDefaults(QuantityPipeAttributes attrs,
            string sourceText, bool overwrite)
        {
            if (attrs == null || attrs.IsSpecialObject) return;
            string source = sourceText ?? string.Empty;

            string diameter = InferDiameter(source);
            if (!string.IsNullOrWhiteSpace(diameter) &&
                (overwrite || string.IsNullOrWhiteSpace(attrs.Diameter)))
                attrs.Diameter = diameter;

            string wellSpec = InferWellSpec(source);
            if (!string.IsNullOrWhiteSpace(wellSpec) &&
                (overwrite || string.IsNullOrWhiteSpace(attrs.WellSpec)
                    || QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind)))
                attrs.WellSpec = wellSpec;

            if ((overwrite || string.IsNullOrWhiteSpace(attrs.Material)) &&
                Contains(source, "HDPE", "高密度", "波纹"))
                attrs.Material =
                    "钢带增强高密度聚乙烯螺旋波纹管(HDPE)";
            else if ((overwrite || string.IsNullOrWhiteSpace(attrs.Material))
                && Contains(source, "PVC", "UPVC"))
                attrs.Material = "PVC管";

            string explicitBranchType = string.Empty;
            if (QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind))
            {
                explicitBranchType = InferExplicitBranchType(source);
                if (overwrite || string.IsNullOrWhiteSpace(
                        attrs.ExcavationType))
                    attrs.ExcavationType = "人工开挖";
                if (!string.IsNullOrWhiteSpace(explicitBranchType))
                    attrs.BranchType = explicitBranchType;
                else if (overwrite || string.IsNullOrWhiteSpace(
                        attrs.BranchType))
                    attrs.BranchType = InferBranchType(source);
                attrs.BranchIncludeInCalculation = ShouldIncludeBranch(
                    attrs.BranchType, source);
                if (overwrite || attrs.BranchDepth <= 0)
                    attrs.BranchDepth = attrs.BranchDepth > 0
                        ? attrs.BranchDepth : 0.6;
            }
            else if (QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind))
            {
                if (overwrite || string.IsNullOrWhiteSpace(attrs.WellType))
                    attrs.WellType = InferWellType(source);
                if (overwrite || string.IsNullOrWhiteSpace(
                        attrs.WellMaterialType))
                    attrs.WellMaterialType = InferWellMaterialType(source);
                if (overwrite || attrs.SiltWellDeductDepth500 <= 0)
                    attrs.SiltWellDeductDepth500 = 0.20;
                if (overwrite || attrs.SiltWellDeductDepth700 <= 0)
                    attrs.SiltWellDeductDepth700 = 0.50;
                if (overwrite || string.IsNullOrWhiteSpace(
                        attrs.WellCoverMaterial))
                    attrs.WellCoverMaterial = "铸铁井盖";
                if (overwrite || attrs.ExcavationLength <= 0 ||
                    attrs.ExcavationWidth <= 0)
                {
                    double size = Contains(attrs.WellSpec, "700")
                        ? 1.5 : 1.3;
                    attrs.ExcavationLength = size;
                    attrs.ExcavationWidth = size;
                }
                if (overwrite || string.IsNullOrWhiteSpace(attrs.CoverPlate))
                    attrs.CoverPlate = Contains(attrs.WellSpec, "700")
                        ? "1600承压盖板" : "1200承压盖板";
            }
            else if (overwrite || string.IsNullOrWhiteSpace(
                    attrs.ExcavationType))
                attrs.ExcavationType = "机械开挖";

            double outer = InferOuterDiameter(attrs.Diameter);
            if (outer > 0 && (overwrite || attrs.PipeOuterDiameter <= 0))
                attrs.PipeOuterDiameter = outer;
            if (overwrite || attrs.TrenchWidth <= 0)
                attrs.TrenchWidth = InferTrenchWidth(attrs.Diameter,
                    attrs.TrenchWidth);
            if (overwrite || attrs.RoadThickness < 0)
                attrs.RoadThickness = InferRoadThickness(source,
                    attrs.RoadThickness);
            else if (attrs.RoadThickness <= 0 &&
                !Contains(source, "绿化", "原土", "无路面"))
                attrs.RoadThickness = InferRoadThickness(source,
                    attrs.RoadThickness);

            if (overwrite || attrs.SandCushionThickness <= 0)
                attrs.SandCushionThickness =
                    QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind)
                        ? 0.10 : 0.15;
            if (overwrite || attrs.GravelCushionThickness <= 0)
                attrs.GravelCushionThickness = 0.10;
            if (overwrite || attrs.C25RestoreThickness <= 0)
                attrs.C25RestoreThickness =
                    QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind)
                        ? 0.30 : 0.25;

            if (QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind))
            {
                bool force = ShouldForceSpecialBranchStructure(
                    explicitBranchType);
                if (overwrite || string.IsNullOrWhiteSpace(
                        attrs.BackfillType) || force)
                    attrs.BackfillType = InferBranchBackfillType(
                        attrs.BranchType, source);
                if (overwrite || string.IsNullOrWhiteSpace(
                        attrs.BackfillStructure) || force)
                    attrs.BackfillStructure = BuildDefaultBackfillStructure(
                        attrs);
                ApplyNoStructureThicknessRules(attrs);
            }
            else
            {
                if (overwrite || string.IsNullOrWhiteSpace(
                        attrs.BackfillType))
                    attrs.BackfillType = Contains(source, "原土")
                        ? "原土回填" : "中粗砂回填";
                if (overwrite || string.IsNullOrWhiteSpace(
                        attrs.BackfillStructure))
                    attrs.BackfillStructure = BuildDefaultBackfillStructure(
                        attrs);
            }

            if (overwrite || !attrs.Enabled) attrs.Enabled = true;
            attrs.DeductPipeVolume =
                !QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind);
        }

        public QuantityDependencyResult NormalizeDraft(
            QuantityPipeAttributes draft,
            IEnumerable<QuantityStructureLayer> submittedLayers,
            QuantityPipeAttributes previous,
            QuantityPipeAttributes startWell,
            QuantityPipeAttributes endWell,
            QuantityPipeAttributes branchDefaults,
            string changedField)
        {
            QuantityPipeAttributes attrs = (draft ?? QuantityPipeAttributes.Default).Clone();
            QuantityPipeAttributes old = previous == null ? attrs.Clone() : previous.Clone();
            bool layersWereSubmitted = submittedLayers != null;
            List<QuantityStructureLayer> layers = CloneLayers(submittedLayers);
            List<QuantityStructureLayer> submittedLayerSnapshot = CloneLayers(layers);
            if (!layersWereSubmitted && !string.IsNullOrWhiteSpace(attrs.BackfillStructure))
            {
                layers = QuantityStructureLayer.Parse(attrs.BackfillStructure);
            }

            string changed = (changedField ?? string.Empty).Trim();
            bool nodeMode = QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind);
            bool branchMode = QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind);

            if (attrs.IsSpecialObject)
            {
                if (QuantityPipeAttributes.IsMainPipeKind(attrs.ObjectKind))
                {
                    attrs.AverageDepth = RoundForEditor(attrs.AverageDepth);
                    DistributeLayers(layers, attrs.AverageDepth, false);
                }
            }
            else if (QuantityPipeAttributes.IsMainPipeKind(attrs.ObjectKind))
            {
                NormalizeMainPipe(attrs, layers, old, startWell, endWell, changed);
            }
            else if (branchMode)
            {
                ApplyBranchRules(attrs, layers, branchDefaults, changed);
                NormalizeBranch(attrs, layers);
            }
            else if (nodeMode)
            {
                ApplyWellSizeDefaults(attrs, old, changed);
                DistributeLayers(layers, attrs.WellDepth, true);
            }

            SyncStructure(attrs, layers, nodeMode);
            if (!attrs.IsSpecialObject && !nodeMode && (attrs.PipeOuterDiameter <= 0 || string.Equals(changed, "Diameter", StringComparison.OrdinalIgnoreCase)))
            {
                attrs.PipeOuterDiameter = ParseDiameterMetres(attrs.Diameter);
            }

            var result = new QuantityDependencyResult
            {
                Attributes = attrs,
                Layers = layers,
                RealExcavationDepth = nodeMode ? RoundForEditor(attrs.WellDepth + SumBelowWellLayers(layers)) : 0.0
            };
            Validate(result, nodeMode, branchMode, attrs.IsSpecialObject);
            ValidateSubmittedPipeLayerHeights(result, submittedLayerSnapshot, nodeMode,
                branchMode, changed);
            return result;
        }

        private static void NormalizeMainPipe(
            QuantityPipeAttributes attrs,
            List<QuantityStructureLayer> layers,
            QuantityPipeAttributes previous,
            QuantityPipeAttributes startWell,
            QuantityPipeAttributes endWell,
            string changed)
        {
            bool manualDepthEdit = EqualsAny(changed, "StartDepth", "EndDepth");
            bool structureEdit = EqualsAny(changed, "BackfillStructure", "StructureLayers", "AutoStructure");
            double cushion = GetPipeCushion(attrs, layers);
            bool refreshEndpointDepths = !structureEdit && EqualsAny(changed, "Load", "Refresh", "ApplyDefaults", "BatchWrite", "ConnectedWell", "StartNode", "EndNode", "SwapEndpoints");

            if (!manualDepthEdit && refreshEndpointDepths)
            {
                if (startWell != null) attrs.StartDepth = CalculateEndpointDepth(startWell, cushion, attrs.StartDepth);
                if (endWell != null) attrs.EndDepth = CalculateEndpointDepth(endWell, cushion, attrs.EndDepth);
            }
            if (startWell != null)
                attrs.StartInvertElevation =
                    QuantityPipeAttributes.CalculateDesignInvertElevationByWell(
                        startWell, attrs.StartInvertElevation);
            if (endWell != null)
                attrs.EndInvertElevation =
                    QuantityPipeAttributes.CalculateDesignInvertElevationByWell(
                        endWell, attrs.EndInvertElevation);

            RecalculateAverageDepth(attrs);
            DistributeLayers(layers, attrs.AverageDepth, false);
        }

        private static void NormalizeBranch(QuantityPipeAttributes attrs, List<QuantityStructureLayer> layers)
        {
            if (Contains(attrs.BranchType, "原土"))
            {
                if (layers.Count == 0) layers.Add(new QuantityStructureLayer { Name = "原土回填", IsPipeLayer = true });
                layers[0].Name = "原土回填";
                layers[0].Height = attrs.BranchDepth;
                layers[0].Locked = false;
                layers[0].IsPipeLayer = true;
                layers[0].IsBelowWellLayer = false;
                if (layers.Count > 1) layers.RemoveRange(1, layers.Count - 1);
                return;
            }
            if (Contains(attrs.BranchType, "明管", "并埋")) return;
            DistributeLayers(layers, attrs.BranchDepth, false);
        }

        private static void ApplyWellSizeDefaults(QuantityPipeAttributes attrs,
            QuantityPipeAttributes previous, string changed)
        {
            if (attrs == null) return;

            bool specificationChanged = string.Equals(changed, "WellSpec",
                StringComparison.OrdinalIgnoreCase);
            double oldLength = previous == null ? 0.0 : previous.ExcavationLength;
            double oldWidth = previous == null ? 0.0 : previous.ExcavationWidth;

            if (!Contains(attrs.WellSpec, "700"))
            {
                if (specificationChanged && Contains(attrs.WellSpec, "500"))
                {
                    if (attrs.ExcavationLength <= 0.0 || NearlyEqual(attrs.ExcavationLength, 1.5))
                        attrs.ExcavationLength = 1.3;
                    if (attrs.ExcavationWidth <= 0.0 || NearlyEqual(attrs.ExcavationWidth, 1.5))
                        attrs.ExcavationWidth = 1.3;
                }
                return;
            }

            if (attrs.ExcavationLength <= 0.0
                || NearlyEqual(attrs.ExcavationLength, 1.3)
                || (specificationChanged && NearlyEqual(attrs.ExcavationLength, oldLength)
                    && NearlyEqual(oldLength, 1.3)))
                attrs.ExcavationLength = 1.5;

            if (attrs.ExcavationWidth <= 0.0
                || NearlyEqual(attrs.ExcavationWidth, 1.3)
                || (specificationChanged && NearlyEqual(attrs.ExcavationWidth, oldWidth)
                    && NearlyEqual(oldWidth, 1.3)))
                attrs.ExcavationWidth = 1.5;
        }

        private static void ApplyBranchRules(QuantityPipeAttributes attrs, List<QuantityStructureLayer> layers, QuantityPipeAttributes branchDefaults, string changed)
        {
            string type = attrs.BranchType ?? string.Empty;
            if (Contains(type, "并埋", "明管"))
            {
                attrs.BranchIncludeInCalculation = false;
                attrs.BackfillType = "无结构层";
                layers.Clear();
                return;
            }
            if (Contains(type, "原土"))
            {
                attrs.BranchIncludeInCalculation = true;
                attrs.BackfillType = "原土回填";
                return;
            }
            attrs.BranchIncludeInCalculation = true;
            if (Contains(type, "砼恢复") && string.Equals(changed, "BranchType", StringComparison.OrdinalIgnoreCase))
            {
                QuantityPipeAttributes defaults = branchDefaults ?? QuantityPipeAttributes.DefaultBranchPipe;
                attrs.BackfillType = defaults.BackfillType ?? "中粗砂回填";
                layers.Clear();
                layers.AddRange(QuantityStructureLayer.Parse(defaults.BackfillStructure));
            }
        }

        private static void DistributeLayers(List<QuantityStructureLayer> layers, double totalHeight, bool nodeMode)
        {
            if (layers == null || layers.Count == 0) return;
            totalHeight = RoundForEditor(totalHeight);
            double locked = 0.0;
            var unlocked = new List<QuantityStructureLayer>();
            foreach (QuantityStructureLayer layer in layers)
            {
                if (layer == null) continue;
                bool below = nodeMode && (layer.IsBelowWellLayer || layer.IsCushionLayer);
                if (below) continue;
                if (layer.Locked) locked += layer.Height;
                else unlocked.Add(layer);
            }
            if (unlocked.Count == 0) return;
            double remaining = totalHeight - locked;
            double value = RoundForEditor(remaining / unlocked.Count);
            for (int i = 0; i < unlocked.Count; i++)
            {
                unlocked[i].Height = i == unlocked.Count - 1
                    ? RoundForEditor(remaining - value * (unlocked.Count - 1))
                    : value;
            }
        }

        private static void RecalculateAverageDepth(QuantityPipeAttributes attrs)
        {
            attrs.AverageDepth = CalculateAverageDepthForEditorCore(
                attrs.StartDepth, attrs.EndDepth);
        }

        /// <summary>
        /// 属性编辑器按界面可见的两位端点深度计算平均值。
        /// 端点实际值仍原样保存；仅派生平均深度采用十进制四舍五入，
        /// 避免 0.80 与 0.75 一类值受二进制浮点尾差影响显示为 0.77。
        /// </summary>
        public double CalculateAverageDepthForEditor(double startDepth,
            double endDepth)
        {
            return CalculateAverageDepthForEditorCore(startDepth, endDepth);
        }

        private static double CalculateAverageDepthForEditorCore(
            double startDepth,
            double endDepth)
        {
            decimal start = RoundDecimalForEditor(startDepth);
            decimal end = RoundDecimalForEditor(endDepth);
            if (start > 0m && end > 0m)
                return (double)Math.Round((start + end) / 2m, 2,
                    MidpointRounding.AwayFromZero);
            if (start > 0m) return (double)start;
            if (end > 0m) return (double)end;
            return 0.0;
        }

        public double CalculatePipeRemainingBackfillHeight(double totalDepth,
            IEnumerable<QuantityStructureLayer> layers)
        {
            return WastewaterQuantityEngineeringMath
                .CalculatePipeRemainingBackfillHeight(totalDepth, layers);
        }

        public double CalculateEarthworkOut(double roadWaste,
            double excavation, double reusableOriginalSoil)
        {
            return WastewaterQuantityEngineeringMath.CalculateEarthworkOut(
                roadWaste, excavation, reusableOriginalSoil);
        }

        public string DefaultSettingsFilePath
        {
            get
            {
                return WastewaterQuantityAttributeDefaultStore
                    .SettingsFilePath;
            }
        }

        public QuantityAttributeDefaults LoadDefaults()
        {
            return WastewaterQuantityAttributeDefaultStore.Load();
        }

        public void SaveDefaults(QuantityAttributeDefaults defaults)
        {
            WastewaterQuantityAttributeDefaultStore.Save(defaults);
        }

        public QuantityPipeAttributes LoadDefaultForKind(string kind)
        {
            return WastewaterQuantityAttributeDefaultStore.LoadForKind(kind);
        }

        public void SaveDefaultForKind(string kind,
            QuantityPipeAttributes attrs)
        {
            WastewaterQuantityAttributeDefaultStore.SaveForKind(kind, attrs);
        }

        private static double CalculateEndpointDepth(QuantityPipeAttributes well, double pipeCushion, double fallback)
        {
            return QuantityPipeAttributes.CalculatePipeExcavationDepthByWell(well, pipeCushion, fallback);
        }

        private static void SyncStructure(QuantityPipeAttributes attrs, List<QuantityStructureLayer> layers, bool nodeMode)
        {
            attrs.BackfillStructure = QuantityStructureLayer.Serialize(layers, nodeMode);
            attrs.SandCushionThickness = QuantityStructureLayer.SumHeight(layers, QuantityStructureLayer.IsSandCushion);
            attrs.GravelCushionThickness = QuantityStructureLayer.SumHeight(layers, QuantityStructureLayer.IsGravel);
            attrs.C25RestoreThickness = QuantityStructureLayer.SumHeight(layers, QuantityStructureLayer.IsC25Restore);
        }

        private static double GetPipeCushion(QuantityPipeAttributes attrs, List<QuantityStructureLayer> layers)
        {
            List<QuantityStructureLayer> source = layers ?? QuantityStructureLayer.Parse(attrs == null ? string.Empty : attrs.BackfillStructure);
            return QuantityStructureLayer.ResolvePipeCushionHeight(
                source, attrs == null ? 0.0 : attrs.SandCushionThickness);
        }

        private static double SumBelowWellLayers(IEnumerable<QuantityStructureLayer> layers)
        {
            double total = 0.0;
            if (layers == null) return total;
            foreach (QuantityStructureLayer layer in layers)
            {
                if (layer != null && (layer.IsBelowWellLayer || layer.IsCushionLayer)) total += layer.Height;
            }
            return total;
        }

        private static void Validate(QuantityDependencyResult result, bool nodeMode, bool branchMode, bool specialObject)
        {
            QuantityPipeAttributes attrs = result.Attributes;
            foreach (QuantityStructureLayer layer in result.Layers)
            {
                if (layer == null) continue;
                if (layer.Height < 0) result.Warnings.Add((layer.Name ?? "结构层") + "计算结果为负值，请检查锁定层与总深度。");
                if (!nodeMode && !branchMode && layer.IsPipeLayer && attrs.PipeOuterDiameter > 0
                    && layer.Height + 0.0000001 < attrs.PipeOuterDiameter)
                {
                    result.Warnings.Add((layer.Name ?? "管线层") + "厚度 "
                        + RoundForEditor(layer.Height).ToString("0.00")
                        + " m 小于管道外径 "
                        + RoundForEditor(attrs.PipeOuterDiameter).ToString("0.00")
                        + " m，请调整结构层。");
                }
            }
            if (!specialObject && nodeMode && attrs.WellDepth <= 0) result.Warnings.Add("井深无效。");
            if (!specialObject && !nodeMode && !branchMode)
            {
                if (string.IsNullOrWhiteSpace(attrs.StartNode)) result.Warnings.Add("缺少起点井。");
                if (string.IsNullOrWhiteSpace(attrs.EndNode)) result.Warnings.Add("缺少终点井。");
                if (attrs.AverageDepth <= 0) result.Warnings.Add("平均深度无效。");
            }
        }

        private static void ValidateSubmittedPipeLayerHeights(QuantityDependencyResult result,
            IEnumerable<QuantityStructureLayer> submittedLayers, bool nodeMode, bool branchMode,
            string changedField)
        {
            if (result == null || result.Attributes == null || nodeMode || branchMode) return;
            if (!EqualsAny(changedField, "StructureLayers", "PipeOuterDiameter", "Diameter")) return;
            double outerDiameter = result.Attributes.PipeOuterDiameter;
            if (outerDiameter <= 0 || submittedLayers == null) return;

            foreach (QuantityStructureLayer layer in submittedLayers)
            {
                if (layer == null || !layer.IsPipeLayer
                    || layer.Height + 0.0000001 >= outerDiameter) continue;
                string warning = (layer.Name ?? "管线层") + "输入厚度 "
                    + RoundForEditor(layer.Height).ToString("0.00")
                    + " m 小于管道外径 "
                    + RoundForEditor(outerDiameter).ToString("0.00")
                    + " m；系统已按平均深度重新分配，请检查结果。";
                if (!result.Warnings.Contains(warning)) result.Warnings.Add(warning);
            }
        }

        private static List<QuantityStructureLayer> CloneLayers(IEnumerable<QuantityStructureLayer> layers)
        {
            var result = new List<QuantityStructureLayer>();
            if (layers == null) return result;
            foreach (QuantityStructureLayer layer in layers) if (layer != null) result.Add(layer.Clone());
            return result;
        }

        private static double ParseDiameterMetres(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0.0;
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(text, @"(?<n>\d{2,4})");
            double value;
            return match.Success && double.TryParse(match.Groups["n"].Value, out value) ? value / 1000.0 : 0.0;
        }

        private static string InferDiameter(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            Match match = Regex.Match(text,
                @"DN\s*(?<n>\d{2,4})", RegexOptions.IgnoreCase);
            if (match.Success) return "DN" + match.Groups["n"].Value;
            match = Regex.Match(text,
                @"(?<!\d)(?<n>\d{2,4})\s*(?:PVC|UPVC|HDPE|PE|管|波纹|砼管|钢管)",
                RegexOptions.IgnoreCase);
            if (match.Success) return "DN" + match.Groups["n"].Value;
            if (!Contains(text, "主管", "支管", "管线", "管径", "管道",
                    "PVC", "HDPE", "PE", "波纹"))
                return string.Empty;
            match = Regex.Match(text, @"(?<!\d)(?<n>\d{2,4})(?!\d)");
            return match.Success ? "DN" + match.Groups["n"].Value
                : string.Empty;
        }

        private static string InferWellSpec(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            Match match = Regex.Match(text, @"[φΦ]\s*(?<n>\d{3,4})");
            if (match.Success) return "φ" + match.Groups["n"].Value;
            match = Regex.Match(text,
                @"(?<!\d)(?<n>500|700|800|1000|1200|1500)(?!\d).{0,8}?(?:井|井盖|检查|沉泥|跌水)",
                RegexOptions.IgnoreCase);
            if (match.Success) return "φ" + match.Groups["n"].Value;
            match = Regex.Match(text,
                @"(?:井|井盖|检查|沉泥|跌水).{0,8}?(?<n>500|700|800|1000|1200|1500)(?!\d)",
                RegexOptions.IgnoreCase);
            if (match.Success) return "φ" + match.Groups["n"].Value;
            if (!Contains(text, "井", "检查", "沉泥", "跌水", "井盖"))
                return string.Empty;
            match = Regex.Match(text,
                @"(?<!\d)(?<n>500|700|800|1000|1200|1500)(?!\d)");
            return match.Success ? "φ" + match.Groups["n"].Value
                : string.Empty;
        }

        private static double InferOuterDiameter(string diameter)
        {
            Match match = Regex.Match(diameter ?? string.Empty,
                @"(?<n>\d{2,4})");
            double value;
            return match.Success && double.TryParse(match.Groups["n"].Value,
                NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value / 1000.0 : 0.0;
        }

        private static double InferTrenchWidth(string diameter,
            double fallback)
        {
            double outer = InferOuterDiameter(diameter);
            if (outer <= 0) return fallback > 0 ? fallback : 0.6;
            if (outer <= 0.12) return 0.4;
            if (outer <= 0.2) return 0.6;
            if (outer <= 0.3) return 0.8;
            return Math.Max(outer + 0.5, fallback > 0 ? fallback : 0.8);
        }

        private static double InferRoadThickness(string text,
            double fallback)
        {
            if (Contains(text, "绿化", "原土", "无路面")) return 0.0;
            if (Contains(text, "支路", "庭院")) return 0.12;
            return fallback > 0 ? fallback : 0.20;
        }

        private static string InferExplicitBranchType(string text)
        {
            if (Contains(text, "原土回填", "原土")) return "原土回填";
            if (Contains(text, "并埋")) return "并埋";
            if (Contains(text, "明管")) return "明管";
            if (Contains(text, "砼恢复", "混凝土恢复")) return "砼恢复";
            return string.Empty;
        }

        private static string InferBranchType(string text)
        {
            string explicitType = InferExplicitBranchType(text);
            if (!string.IsNullOrWhiteSpace(explicitType)) return explicitType;
            if (Contains(text, "砼")) return "砼恢复";
            if (Contains(text, "雨水")) return "雨水";
            return "砼恢复";
        }

        private static bool ShouldIncludeBranch(string branchType,
            string sourceText)
        {
            string text = (branchType ?? string.Empty) + " " +
                (sourceText ?? string.Empty);
            return !Contains(text, "不计算", "不统计", "明管", "并埋");
        }

        private static bool ShouldForceSpecialBranchStructure(string type)
        {
            return Contains(type, "并埋", "明管", "原土回填", "原土");
        }

        private static string InferBranchBackfillType(string branchType,
            string sourceText)
        {
            string text = (branchType ?? string.Empty) + " " +
                (sourceText ?? string.Empty);
            if (Contains(text, "原土回填", "原土")) return "原土回填";
            if (Contains(text, "明管", "并埋")) return "无结构层";
            return "中粗砂回填";
        }

        private static void ApplyNoStructureThicknessRules(
            QuantityPipeAttributes attrs)
        {
            if (attrs == null ||
                !QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind))
                return;
            if (!Contains(attrs.BranchType, "明管", "并埋", "原土回填",
                    "原土")) return;
            attrs.SandCushionThickness = 0.0;
            attrs.GravelCushionThickness = 0.0;
            attrs.C25RestoreThickness = 0.0;
            attrs.BranchIncludeInCalculation =
                Contains(attrs.BranchType, "原土回填", "原土");
        }

        private static string InferWellMaterialType(string text)
        {
            if (Contains(text, "砖砌", "砖井")) return "砖砌井";
            if (Contains(text, "现浇", "混凝土", "砼井"))
                return "现浇混凝土井";
            return "成品塑料井";
        }

        private static string InferWellType(string text)
        {
            string normalized = NormalizeMetadataText(text);
            if (normalized.IndexOf("检查",
                    StringComparison.CurrentCultureIgnoreCase) >= 0 &&
                normalized.IndexOf("沉泥",
                    StringComparison.CurrentCultureIgnoreCase) >= 0)
                return "检查井";
            string cleaned = Regex.Replace(text ?? string.Empty,
                @"检查\s*井?\s*[、,，/\\;；|_\-]*\s*沉泥井", "检查井",
                RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"检查\s*沉泥井", "检查井",
                RegexOptions.IgnoreCase);
            if (Contains(cleaned, "沉泥井", "沉泥")) return "沉泥井";
            if (Contains(cleaned, "跌水井", "跌水")) return "跌水井";
            return "检查井";
        }

        private static string NormalizeMetadataText(string text)
        {
            return (text ?? string.Empty).Replace(" ", string.Empty)
                .Replace("　", string.Empty).Replace("/", string.Empty)
                .Replace("、", string.Empty).Replace(",", string.Empty)
                .Replace("，", string.Empty).Replace("\\", string.Empty)
                .Replace("-", string.Empty).Replace("_", string.Empty)
                .Trim();
        }

        private static string BuildDefaultBackfillStructure(
            QuantityPipeAttributes attrs)
        {
            if (attrs == null) return string.Empty;
            if (QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind))
                return "承压盖板C25基础 " + Format(attrs.C25RestoreThickness)
                    + " 锁定" + Environment.NewLine + "承压盖板碎石垫层 "
                    + Format(attrs.GravelCushionThickness) + " 锁定"
                    + Environment.NewLine + "中粗砂回填 0.80"
                    + Environment.NewLine + "中粗砂垫层 "
                    + Format(attrs.SandCushionThickness) + " 锁定 垫层";
            if (QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind))
            {
                if (Contains(attrs.BranchType, "明管", "并埋"))
                    return string.Empty;
                if (Contains(attrs.BranchType, "原土回填", "原土"))
                    return "原土回填 " + Format(attrs.BranchDepth > 0
                        ? attrs.BranchDepth : 0.6) + " 管线层";
                return "C25砼恢复 " + Format(attrs.C25RestoreThickness > 0
                        ? attrs.C25RestoreThickness : 0.25) + " 锁定"
                    + Environment.NewLine + "中粗砂回填 0.25 管线层"
                    + Environment.NewLine + "中粗砂垫层 "
                    + Format(attrs.SandCushionThickness > 0
                        ? attrs.SandCushionThickness : 0.10) + " 锁定 垫层";
            }
            return "C25砼恢复 " + Format(attrs.C25RestoreThickness) + " 锁定"
                + Environment.NewLine + "碎石垫层 "
                + Format(attrs.GravelCushionThickness) + " 锁定"
                + Environment.NewLine + "中粗砂回填 0.80 管线层"
                + Environment.NewLine + "中粗砂垫层 "
                + Format(attrs.SandCushionThickness) + " 锁定 垫层";
        }

        private static string Format(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static bool EqualsAny(string value, params string[] candidates)
        {
            foreach (string candidate in candidates) if (string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool Contains(string text, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (string value in values) if (!string.IsNullOrWhiteSpace(value) && text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool NearlyEqual(double left, double right)
        {
            return Math.Abs(left - right) <= 0.0000001;
        }

        private static double RoundForEditor(double value)
        {
            return (double)RoundDecimalForEditor(value);
        }

        private static decimal RoundDecimalForEditor(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0m;
            return Math.Round(Convert.ToDecimal(value), 2,
                MidpointRounding.AwayFromZero);
        }
    }
}
