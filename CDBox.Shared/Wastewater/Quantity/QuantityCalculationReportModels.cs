using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    // 污水工程量的共享报表数据契约。
    public sealed class QuantityCalculationReport
    {
        public List<QuantityMainPipeCalculationRow> MainPipes { get; private set; }
        public List<QuantityMainPipeCalculationRow> BranchPipes { get; private set; }
        public List<QuantityWellCalculationRow> Wells { get; private set; }
        public List<string> Warnings { get; private set; }

        public QuantityCalculationReport()
        {
            MainPipes = new List<QuantityMainPipeCalculationRow>();
            BranchPipes = new List<QuantityMainPipeCalculationRow>();
            Wells = new List<QuantityWellCalculationRow>();
            Warnings = new List<string>();
        }
    }

    public sealed class QuantityCalculationStep
    {
        public string ItemName { get; set; }
        public string Formula { get; set; }
        public string Substitution { get; set; }
        public double Result { get; set; }
        public string Unit { get; set; }
        public string Explanation { get; set; }

        public QuantityCalculationStep()
        {
            ItemName = string.Empty;
            Formula = string.Empty;
            Substitution = string.Empty;
            Unit = string.Empty;
            Explanation = string.Empty;
        }
    }

    public sealed class QuantityMainPipeCalculationRow
    {
        public int Index { get; set; }
        public string HandleText { get; set; }
        public string LayerName { get; set; }
        public string StartNode { get; set; }
        public string EndNode { get; set; }
        public string Material { get; set; }
        public string Diameter { get; set; }
        public double Length { get; set; }
        public double StartDepth { get; set; }
        public double EndDepth { get; set; }
        public double AverageDepth { get; set; }
        public double TrenchWidth { get; set; }
        public double RoadThickness { get; set; }
        public string ExcavationType { get; set; }
        public string BackfillType { get; set; }
        public string BackfillStructure { get; set; }
        public double PipeOuterDiameter { get; set; }
        public double PipeDeductionVolume { get; set; }
        public string PipeDeductionTarget { get; set; }
        public double RoadCutting { get; set; }
        public double RoadBreaking { get; set; }
        public double RoadWaste { get; set; }
        public double MechanicalExcavation { get; set; }
        public double ManualExcavation { get; set; }
        public double SandCushion { get; set; }
        public double SandBackfill { get; set; }
        public double GravelCushion { get; set; }
        public double C25Restore { get; set; }
        public double OriginalSoilBackfill { get; set; }
        public double C25PipeEncasement { get; set; }
        public double EarthworkOut { get; set; }
        public string FormulaText { get; set; }
        public string Remark { get; set; }
        public string ObjectKind { get; set; }
        public string BranchType { get; set; }
        public string CalculationSource { get; set; }
        public string DataStatus { get; set; }
        public List<QuantityCalculationStep> CalculationSteps { get; set; }

        public QuantityMainPipeCalculationRow()
        {
            HandleText = string.Empty;
            LayerName = string.Empty;
            StartNode = string.Empty;
            EndNode = string.Empty;
            Material = string.Empty;
            Diameter = string.Empty;
            ExcavationType = string.Empty;
            BackfillType = string.Empty;
            BackfillStructure = string.Empty;
            PipeDeductionTarget = string.Empty;
            FormulaText = string.Empty;
            Remark = string.Empty;
            ObjectKind = string.Empty;
            BranchType = string.Empty;
            CalculationSource = string.Empty;
            DataStatus = string.Empty;
            CalculationSteps = new List<QuantityCalculationStep>();
        }
    }

    public sealed class QuantityWellCalculationRow
    {
        public int Index { get; set; }
        public string HandleText { get; set; }
        public string LayerName { get; set; }
        public string NodeNo { get; set; }
        public string WellSpec { get; set; }
        public string WellType { get; set; }
        public string WellMaterialType { get; set; }
        public string WellCoverMaterial { get; set; }
        public double GroundElevation { get; set; }
        public double WellDepth { get; set; }
        public double ShaftLength { get; set; }
        public double ExcavationLength { get; set; }
        public double ExcavationWidth { get; set; }
        public double RoadThickness { get; set; }
        public string ExcavationType { get; set; }
        public string BackfillType { get; set; }
        public string BackfillStructure { get; set; }
        public string CoverPlate { get; set; }
        public double WellRadius { get; set; }
        public double WellArea { get; set; }
        public double RoadCutting { get; set; }
        public double RoadBreaking { get; set; }
        public double RoadWaste { get; set; }
        public double MechanicalExcavation { get; set; }
        public double ManualExcavation { get; set; }
        public double SandCushion { get; set; }
        public double C25Cushion { get; set; }
        public double CoverPlateGravelCushion { get; set; }
        public double CoverPlateC25Foundation { get; set; }
        public double SandBackfill { get; set; }
        public double EarthworkOut { get; set; }
        public int CoverPlateCount { get; set; }
        public int WellCoverCount { get; set; }
        public string FormulaText { get; set; }
        public string Remark { get; set; }
        public string CalculationSource { get; set; }
        public string DataStatus { get; set; }
        public List<QuantityCalculationStep> CalculationSteps { get; set; }

        public QuantityWellCalculationRow()
        {
            HandleText = string.Empty;
            LayerName = string.Empty;
            NodeNo = string.Empty;
            WellSpec = string.Empty;
            WellType = string.Empty;
            WellMaterialType = string.Empty;
            WellCoverMaterial = string.Empty;
            ExcavationType = string.Empty;
            BackfillType = string.Empty;
            BackfillStructure = string.Empty;
            CoverPlate = string.Empty;
            FormulaText = string.Empty;
            Remark = string.Empty;
            CalculationSource = string.Empty;
            DataStatus = string.Empty;
            CoverPlateCount = 1;
            WellCoverCount = 1;
            CalculationSteps = new List<QuantityCalculationStep>();
        }
    }

    public sealed class QuantityStructureLayer
    {
        public string Name { get; set; }
        public double Height { get; set; }
        public bool Locked { get; set; }
        public bool IsPipeLayer { get; set; }
        public bool IsBelowWellLayer { get; set; }
        public bool IsCushionLayer { get; set; }
        public string RawText { get; set; }

        public QuantityStructureLayer()
        {
            Name = string.Empty;
            RawText = string.Empty;
        }

        public static List<QuantityStructureLayer> Parse(string text)
        {
            List<QuantityStructureLayer> layers = new List<QuantityStructureLayer>();
            if (string.IsNullOrWhiteSpace(text)) return layers;

            string normalized = text.Replace("；", "\n").Replace(";", "\n").Replace("，", "\n").Replace(",", "\n").Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in lines)
            {
                string line = raw == null ? string.Empty : raw.Trim();
                if (line.Length == 0) continue;

                QuantityStructureLayer layer = new QuantityStructureLayer();
                layer.RawText = line;
                MatchCollection matches = Regex.Matches(line, @"[-+]?\d+(?:\.\d+)?");
                if (matches.Count > 0)
                {
                    Match m = matches[matches.Count - 1];
                    layer.Height = ParseDouble(m.Value, 0.0);
                    string before = line.Substring(0, m.Index).Trim();
                    string suffix = line.Substring(m.Index + m.Length).Trim();
                    layer.Locked = ContainsAny(suffix, "锁定", "固定");
                    layer.IsBelowWellLayer = ContainsAny(suffix, "井下层", "井下方垫层");
                    layer.IsPipeLayer = ContainsAny(suffix, "管线层", "管道层", "管层");
                    layer.IsCushionLayer = layer.IsBelowWellLayer || ContainsAny(suffix, "垫层");
                    layer.Name = CleanName(before.Length > 0 ? before : line);
                    // 兼容旧数据：管线下方的砂垫层过去被标成“管线层”，
                    // 新模型中应统一迁移为“垫层”。未显式标记的砂垫层也按垫层处理。
                    if (IsSandCushion(layer)
                        || (layer.IsPipeLayer && ContainsAny(layer.Name, "垫层")))
                    {
                        layer.IsCushionLayer = true;
                        layer.IsPipeLayer = false;
                    }
                }
                else
                {
                    layer.Locked = ContainsAny(line, "锁定", "固定");
                    layer.IsBelowWellLayer = ContainsAny(line, "井下层", "井下方垫层");
                    layer.IsPipeLayer = ContainsAny(line, "管线层", "管道层", "管层");
                    layer.IsCushionLayer = layer.IsBelowWellLayer;
                    layer.Height = 0.0;
                    layer.Name = CleanName(line);
                }

                layers.Add(layer);
            }
            return layers;
        }

        public QuantityStructureLayer Clone()
        {
            return new QuantityStructureLayer
            {
                Name = Name ?? string.Empty,
                Height = Height,
                Locked = Locked,
                IsPipeLayer = IsPipeLayer,
                IsBelowWellLayer = IsBelowWellLayer,
                IsCushionLayer = IsCushionLayer,
                RawText = RawText ?? string.Empty
            };
        }

        public static string Serialize(IEnumerable<QuantityStructureLayer> layers, bool nodeWellMode)
        {
            if (layers == null) return string.Empty;
            var lines = new List<string>();
            foreach (QuantityStructureLayer layer in layers)
            {
                if (layer == null) continue;
                string name = string.IsNullOrWhiteSpace(layer.Name) ? "结构层" : layer.Name.Trim();
                string line = name + " " + layer.Height.ToString("0.########", CultureInfo.InvariantCulture);
                if (layer.Locked) line += " 锁定";
                if (nodeWellMode)
                {
                    if (layer.IsBelowWellLayer || layer.IsCushionLayer) line += " 垫层";
                    else if (layer.IsPipeLayer) line += " 管线层";
                }
                else if (layer.IsPipeLayer)
                {
                    line += " 管线层";
                }
                else if (layer.IsCushionLayer)
                {
                    line += " 垫层";
                }
                lines.Add(line);
            }
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        public static double SumHeight(IEnumerable<QuantityStructureLayer> layers, Predicate<QuantityStructureLayer> predicate)
        {
            if (layers == null || predicate == null) return 0.0;
            double sum = 0.0;
            foreach (QuantityStructureLayer layer in layers)
            {
                if (layer == null) continue;
                if (predicate(layer)) sum += layer.Height;
            }
            return sum;
        }

        public static bool ContainsAny(string text, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(text) || values == null) return false;
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            }
            return false;
        }

        public static bool IsSandCushion(QuantityStructureLayer layer)
        {
            if (layer == null) return false;
            string text = (layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty);
            if (ContainsAny(text, "回填")) return false;
            return ContainsAny(text, "中粗砂垫层", "粗砂垫层", "砂垫层") || (ContainsAny(text, "砂") && ContainsAny(text, "垫层"));
        }

        public static bool IsMarkedCushion(QuantityStructureLayer layer)
        {
            return layer != null && (layer.IsCushionLayer || layer.IsBelowWellLayer);
        }

        public static double ResolvePipeCushionHeight(IEnumerable<QuantityStructureLayer> layers, double fallback)
        {
            if (layers != null)
            {
                var snapshot = new List<QuantityStructureLayer>();
                foreach (QuantityStructureLayer layer in layers) if (layer != null) snapshot.Add(layer);
                double marked = SumHeight(snapshot, IsMarkedCushion);
                if (marked > 0) return marked;
                double inferred = SumHeight(snapshot, IsSandCushion);
                if (inferred > 0) return inferred;
            }
            return fallback > 0 ? fallback : 0.0;
        }

        public static bool IsSandBackfill(QuantityStructureLayer layer)
        {
            if (layer == null) return false;
            string text = (layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty);
            if (IsSandCushion(layer)) return false;
            if (!ContainsAny(text, "中粗砂", "粗砂", "砂")) return false;
            return ContainsAny(text, "回填", "包管", "包封", "管周", "管顶");
        }

        public static bool IsOriginalSoilBackfill(QuantityStructureLayer layer)
        {
            if (layer == null) return false;
            string text = (layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty);
            return ContainsAny(text, "原土") && ContainsAny(text, "回填");
        }

        public static bool IsGravel(QuantityStructureLayer layer)
        {
            if (layer == null) return false;
            string text = (layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty);
            return ContainsAny(text, "碎石");
        }

        public static bool IsC25(QuantityStructureLayer layer)
        {
            if (layer == null) return false;
            string text = (layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty);
            return ContainsAny(text, "C25", "砼", "混凝土");
        }

        public static bool IsConcretePipeEncasement(QuantityStructureLayer layer)
        {
            if (layer == null || !IsC25(layer)) return false;
            string text = (layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty);
            return ContainsAny(text, "包管", "包封");
        }

        public static bool IsC25Restore(QuantityStructureLayer layer)
        {
            return IsC25(layer) && !IsConcretePipeEncasement(layer);
        }

        public static bool IsCoverPlateLayer(QuantityStructureLayer layer)
        {
            if (layer == null) return false;
            string text = (layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty);
            return ContainsAny(text, "承压盖板", "盖板");
        }

        private static string CleanName(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            // Parse 已经使用最后一个数值作为层高并截去该数值；这里不能再次删除层名中的数字，
            // 否则 C25、C30、3:7 灰土等材料名称会被破坏。
            string name = text;
            name = name.Replace("锁定", " ").Replace("固定", " ").Replace("管线层", " ").Replace("管道层", " ").Replace("管层", " ").Replace("井下层", " ").Replace("井下方垫层", " ");
            // 修复旧解析器已经写回 DWG 的“C 砼恢复 / C混凝土”历史文本。
            name = Regex.Replace(name, @"^C\s*(?=砼|混凝土)", "C25", RegexOptions.IgnoreCase);
            return Regex.Replace(name, @"\s+", " ").Trim();
        }

        private static double ParseDouble(string text, double fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            double value;
            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value;
            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return value;
            return fallback;
        }
    }
}
