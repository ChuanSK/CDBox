using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    public sealed class QuantityDependencyResult
    {
        public QuantityPipeAttributes Attributes { get; set; }
        public List<QuantityStructureLayer> Layers { get; set; }
        public List<string> Warnings { get; set; }
        public double RealExcavationDepth { get; set; }

        public QuantityDependencyResult()
        {
            Attributes = QuantityPipeAttributes.Default;
            Layers = new List<QuantityStructureLayer>();
            Warnings = new List<string>();
        }
    }

    public static class QuantityDependencyService
    {
        public static QuantityDependencyResult NormalizeDraft(
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
            if (!layersWereSubmitted && !string.IsNullOrWhiteSpace(attrs.BackfillStructure))
            {
                layers = QuantityStructureLayer.Parse(attrs.BackfillStructure);
            }

            string changed = (changedField ?? string.Empty).Trim();
            bool nodeMode = QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind);
            bool branchMode = QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind);

            if (branchMode) ApplyBranchRules(attrs, layers, branchDefaults, changed);

            if (QuantityPipeAttributes.IsMainPipeKind(attrs.ObjectKind))
            {
                NormalizeMainPipe(attrs, layers, old, startWell, endWell, changed);
            }
            else if (branchMode)
            {
                NormalizeBranch(attrs, layers);
            }
            else if (nodeMode)
            {
                DistributeLayers(layers, attrs.WellDepth, true);
            }

            SyncStructure(attrs, layers, nodeMode);
            if (!nodeMode && (attrs.PipeOuterDiameter <= 0 || string.Equals(changed, "Diameter", StringComparison.OrdinalIgnoreCase)))
            {
                attrs.PipeOuterDiameter = ParseDiameterMetres(attrs.Diameter);
            }

            var result = new QuantityDependencyResult
            {
                Attributes = attrs,
                Layers = layers,
                RealExcavationDepth = nodeMode ? attrs.WellDepth + SumBelowWellLayers(layers) : 0.0
            };
            Validate(result, nodeMode, branchMode);
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
            double oldCushion = GetPipeCushion(previous, null);
            double cushion = GetPipeCushion(attrs, layers);

            if (!manualDepthEdit)
            {
                if (startWell != null) attrs.StartDepth = CalculateEndpointDepth(startWell, cushion, attrs.StartDepth);
                else if (structureEdit && attrs.StartDepth > 0) attrs.StartDepth += cushion - oldCushion;
                if (endWell != null) attrs.EndDepth = CalculateEndpointDepth(endWell, cushion, attrs.EndDepth);
                else if (structureEdit && attrs.EndDepth > 0) attrs.EndDepth += cushion - oldCushion;
            }

            RecalculateAverageDepth(attrs);
            DistributeLayers(layers, attrs.AverageDepth, false);

            double redistributedCushion = GetPipeCushion(attrs, layers);
            if (!manualDepthEdit && Math.Abs(redistributedCushion - cushion) > 0.0000001)
            {
                if (startWell != null) attrs.StartDepth = CalculateEndpointDepth(startWell, redistributedCushion, attrs.StartDepth);
                else if (structureEdit && attrs.StartDepth > 0) attrs.StartDepth += redistributedCushion - cushion;
                if (endWell != null) attrs.EndDepth = CalculateEndpointDepth(endWell, redistributedCushion, attrs.EndDepth);
                else if (structureEdit && attrs.EndDepth > 0) attrs.EndDepth += redistributedCushion - cushion;
                RecalculateAverageDepth(attrs);
                DistributeLayers(layers, attrs.AverageDepth, false);
            }
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
            double locked = 0.0;
            var unlocked = new List<QuantityStructureLayer>();
            foreach (QuantityStructureLayer layer in layers)
            {
                if (layer == null) continue;
                bool below = nodeMode && (layer.IsBelowWellLayer || layer.IsPipeLayer);
                if (below) continue;
                if (layer.Locked) locked += layer.Height;
                else unlocked.Add(layer);
            }
            if (unlocked.Count == 0) return;
            double value = (totalHeight - locked) / unlocked.Count;
            foreach (QuantityStructureLayer layer in unlocked) layer.Height = value;
        }

        private static void RecalculateAverageDepth(QuantityPipeAttributes attrs)
        {
            double start = attrs.StartDepth > 0 ? attrs.StartDepth : 0.0;
            double end = attrs.EndDepth > 0 ? attrs.EndDepth : 0.0;
            if (start > 0 && end > 0) attrs.AverageDepth = (start + end) / 2.0;
            else if (start > 0) attrs.AverageDepth = start;
            else if (end > 0) attrs.AverageDepth = end;
            else attrs.AverageDepth = 0.0;
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
            double height = QuantityStructureLayer.SumHeight(source, QuantityStructureLayer.IsSandCushion);
            if (source.Count == 0 && attrs != null) height = attrs.SandCushionThickness;
            return height;
        }

        private static double SumBelowWellLayers(IEnumerable<QuantityStructureLayer> layers)
        {
            double total = 0.0;
            if (layers == null) return total;
            foreach (QuantityStructureLayer layer in layers)
            {
                if (layer != null && (layer.IsBelowWellLayer || layer.IsPipeLayer)) total += layer.Height;
            }
            return total;
        }

        private static void Validate(QuantityDependencyResult result, bool nodeMode, bool branchMode)
        {
            QuantityPipeAttributes attrs = result.Attributes;
            foreach (QuantityStructureLayer layer in result.Layers)
            {
                if (layer == null) continue;
                if (layer.Height < 0) result.Warnings.Add((layer.Name ?? "结构层") + "计算结果为负值，请检查锁定层与总深度。");
                if (!nodeMode && layer.IsPipeLayer && attrs.PipeOuterDiameter > 0 && layer.Height <= attrs.PipeOuterDiameter)
                    result.Warnings.Add((layer.Name ?? "管线层") + "高度不大于管道外径。");
            }
            if (nodeMode && attrs.WellDepth <= 0) result.Warnings.Add("井深无效。");
            if (!nodeMode && !branchMode)
            {
                if (string.IsNullOrWhiteSpace(attrs.StartNode)) result.Warnings.Add("缺少起点井。");
                if (string.IsNullOrWhiteSpace(attrs.EndNode)) result.Warnings.Add("缺少终点井。");
                if (attrs.AverageDepth <= 0) result.Warnings.Add("平均深度无效。");
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
    }
}
