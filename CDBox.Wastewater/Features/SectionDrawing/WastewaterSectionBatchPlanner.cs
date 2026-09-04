using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CDBox.Shared.Wastewater.Drafting;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.Modules.SectionDrawing;

namespace CDBox.Wastewater.Features.SectionDrawing
{
    internal static class WastewaterSectionBatchPlanner
    {
        public static List<SectionBatchPlanGroup> Plan(
            IEnumerable<SectionBatchSourceData> values,
            SectionDrawingOptions baseOptions)
        {
            var sources = new List<PlannerSource>();
            foreach (SectionBatchSourceData value in values ??
                Enumerable.Empty<SectionBatchSourceData>())
            {
                PlannerSource source = BuildSource(value);
                if (source != null) sources.Add(source);
            }

            var groups = new List<PlannerGroup>();
            var lookup = new Dictionary<string, PlannerGroup>(
                StringComparer.OrdinalIgnoreCase);
            foreach (PlannerSource source in sources)
            {
                PlannerGroup group;
                if (!lookup.TryGetValue(source.GroupKey, out group))
                {
                    group = new PlannerGroup { Template = source };
                    lookup[source.GroupKey] = group;
                    groups.Add(group);
                }
                group.Sources.Add(source);
            }

            var result = new List<SectionBatchPlanGroup>();
            foreach (PlannerGroup group in groups)
            {
                SectionDrawingOptions options = baseOptions == null
                    ? SectionDrawingOptions.Default.Clone()
                    : baseOptions.Clone();
                options.Width = group.Template.Width;
                options.LockTotalHeight = false;
                options.Layers = group.Template.Layers
                    .Select(x => x == null ? new SectionLayerOptions()
                        : x.Clone()).ToList();
                options.SectionTitle = BuildMergedTitle(group.Sources);
                options.DrawTitle = !string.IsNullOrWhiteSpace(
                    options.SectionTitle);
                options.DrawPipeCircle = true;
                WastewaterSectionDrawingEngine.NormalizeCore(options);
                options.TotalHeight = Round(options.TotalHeight);
                result.Add(new SectionBatchPlanGroup
                {
                    IsBranch = QuantityPipeAttributes.IsBranchKind(
                        group.Template.Attributes.ObjectKind),
                    Options = options,
                    SourceIds = group.Sources.Select(x => x.SourceId)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                });
            }
            return result;
        }

        private static PlannerSource BuildSource(SectionBatchSourceData input)
        {
            if (input == null || input.Attributes == null) return null;
            QuantityPipeAttributes attrs = input.Attributes;
            if (!attrs.Enabled || QuantityPipeAttributes.IsNodeKind(
                    attrs.ObjectKind)) return null;
            if (!QuantityPipeAttributes.IsMainPipeKind(attrs.ObjectKind) &&
                !QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind))
                return null;
            if (QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind) &&
                !attrs.BranchIncludeInCalculation &&
                string.IsNullOrWhiteSpace(attrs.BackfillStructure))
                return null;

            List<QuantityStructureLayer> quantityLayers =
                QuantityStructureLayer.Parse(attrs.BackfillStructure);
            List<SectionLayerOptions> layers = ConvertLayers(quantityLayers);
            if (layers.Count == 0) return null;
            double width = attrs.TrenchWidth > 0 ? attrs.TrenchWidth
                : SectionDrawingOptions.Default.Width;
            if (width <= 0) width = 1.0;
            double pipeDiameter = attrs.PipeOuterDiameter > 0
                ? attrs.PipeOuterDiameter : InferPipeDiameter(attrs.Diameter);
            if (pipeDiameter > 0)
            {
                int index = ResolvePipeLayerIndex(quantityLayers, layers);
                if (index >= 0 && index < layers.Count)
                    layers[index].Pipes.Add(new SectionPipeOptions
                    {
                        Diameter = pipeDiameter,
                        PipeText = SectionPipeOptions.BuildPipeText(
                            pipeDiameter),
                        HostLayerIndex = index,
                        VerticalMode = HasCushionLayerBelow(quantityLayers,
                            index) ? SectionPipeVerticalMode.LayerBottom
                            : SectionPipeVerticalMode.LayerCenter
                    });
            }
            return new PlannerSource
            {
                SourceId = input.SourceId ?? string.Empty,
                Order = input.Order,
                Attributes = attrs,
                Width = width,
                Layers = layers,
                SegmentTitle = BuildSegmentTitle(attrs),
                GroupKey = BuildGroupKey(attrs, width, layers)
            };
        }

        private static List<SectionLayerOptions> ConvertLayers(
            IList<QuantityStructureLayer> source)
        {
            var result = new List<SectionLayerOptions>();
            foreach (QuantityStructureLayer layer in source ??
                new List<QuantityStructureLayer>())
            {
                if (layer == null || layer.Height <= 0) continue;
                PatternChoice pattern = ResolvePattern(layer.Name,
                    layer.RawText);
                result.Add(new SectionLayerOptions
                {
                    DrawLayer = true,
                    LeftLabel = BuildLayerDisplayName(layer),
                    Height = Round(layer.Height),
                    HeightLocked = layer.Locked,
                    HatchPatternName = pattern.Name,
                    HatchScale = pattern.Scale,
                    HatchAngle = 0.0,
                    Pipes = new List<SectionPipeOptions>()
                });
            }
            return result;
        }

        private static string BuildLayerDisplayName(
            QuantityStructureLayer layer)
        {
            string text = string.IsNullOrWhiteSpace(layer.RawText)
                ? layer.Name ?? string.Empty : layer.RawText.Trim();
            text = Regex.Replace(text, @"\s+垫层\s*$", string.Empty);
            text = Regex.Replace(text,
                "锁定|固定|管线层|管道层|管层|井下层|井下方垫层", " ");
            MatchCollection matches = Regex.Matches(text,
                @"[-+]?\d+(?:\.\d+)?");
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                double value;
                if (!double.TryParse(matches[i].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out value) &&
                    !double.TryParse(matches[i].Value, NumberStyles.Float,
                        CultureInfo.CurrentCulture, out value)) continue;
                if (Math.Abs(value - layer.Height) > 0.000001) continue;
                text = text.Remove(matches[i].Index, matches[i].Length);
                break;
            }
            text = Regex.Replace(text, @"[：:，,；;、/\\|]+", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return string.IsNullOrWhiteSpace(text)
                ? layer.Name ?? string.Empty : text;
        }

        private static int ResolvePipeLayerIndex(
            IList<QuantityStructureLayer> source,
            IList<SectionLayerOptions> layers)
        {
            if (layers == null || layers.Count == 0) return -1;
            int sectionIndex = 0;
            foreach (QuantityStructureLayer layer in source ??
                new List<QuantityStructureLayer>())
            {
                if (layer == null || layer.Height <= 0) continue;
                if (layer.IsPipeLayer) return sectionIndex;
                sectionIndex++;
            }
            sectionIndex = 0;
            foreach (QuantityStructureLayer layer in source ??
                new List<QuantityStructureLayer>())
            {
                if (layer == null || layer.Height <= 0) continue;
                string text = (layer.Name ?? string.Empty) + " " +
                    (layer.RawText ?? string.Empty);
                if (Contains(text, "包管", "管顶", "管周"))
                    return sectionIndex;
                sectionIndex++;
            }
            return Math.Max(0, layers.Count / 2);
        }

        private static bool HasCushionLayerBelow(
            IList<QuantityStructureLayer> source, int pipeIndex)
        {
            int sectionIndex = 0;
            bool passed = false;
            foreach (QuantityStructureLayer layer in source ??
                new List<QuantityStructureLayer>())
            {
                if (layer == null || layer.Height <= 0) continue;
                if (!passed)
                {
                    if (sectionIndex == pipeIndex) passed = true;
                    sectionIndex++;
                    continue;
                }
                string text = (layer.Name ?? string.Empty) + " " +
                    (layer.RawText ?? string.Empty);
                if (layer.IsCushionLayer || layer.IsBelowWellLayer ||
                    Contains(text, "垫层")) return true;
            }
            return false;
        }

        private static PatternChoice ResolvePattern(string name,
            string rawText)
        {
            string text = (name ?? string.Empty) + " " +
                (rawText ?? string.Empty);
            if (Contains(text, "碎石")) return new PatternChoice("HEX", 0.1);
            if (Contains(text, "原土") && Contains(text, "回填"))
                return new PatternChoice("EARTH", 0.1);
            if (Contains(text, "C25", "C30", "C20", "C15", "砼",
                    "混凝土", "路面恢复", "恢复路面", "砼恢复",
                    "混凝土恢复"))
                return new PatternChoice("AR-CONC", 0.01);
            if (Contains(text, "中粗砂", "粗砂", "砂包管", "砂垫层",
                    "砂回填", "包管") ||
                (Contains(text, "砂") && !Contains(text, "砂浆")))
                return new PatternChoice("1064", 0.01);
            return new PatternChoice(string.Empty, 1.0);
        }

        private static double InferPipeDiameter(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0.0;
            Match match = Regex.Match(text, @"(?i)DN\s*(\d+(?:\.\d+)?)");
            if (!match.Success)
                match = Regex.Match(text, @"[Φφ]\s*(\d+(?:\.\d+)?)");
            if (!match.Success)
                match = Regex.Match(text, @"(\d+(?:\.\d+)?)");
            double value;
            if (!match.Success || !double.TryParse(match.Groups[1].Value,
                    NumberStyles.Float, CultureInfo.InvariantCulture,
                    out value)) return 0.0;
            return value > 10.0 ? value / 1000.0 : Math.Max(0.0, value);
        }

        private static string BuildSegmentTitle(QuantityPipeAttributes attrs)
        {
            string start = (attrs.StartNode ?? string.Empty).Trim();
            string end = (attrs.EndNode ?? string.Empty).Trim();
            return start.Length > 0 && end.Length > 0
                ? start + "至" + end : string.Empty;
        }

        private static string BuildGroupKey(QuantityPipeAttributes attrs,
            double width, IEnumerable<SectionLayerOptions> layers)
        {
            var parts = new List<string>
            {
                "K=" + (QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind)
                    ? "BRANCH" : "MAIN"),
                "W=" + width.ToString("0.###", CultureInfo.InvariantCulture)
            };
            foreach (SectionLayerOptions layer in layers)
            {
                if (layer == null || !layer.DrawLayer) continue;
                parts.Add("L=" + Key(layer.LeftLabel) + ":" +
                    layer.Height.ToString("0.########",
                        CultureInfo.InvariantCulture) + ":" +
                    Key(layer.HatchPatternName) + ":" +
                    layer.HatchScale.ToString("0.########",
                        CultureInfo.InvariantCulture));
                foreach (SectionPipeOptions pipe in layer.Pipes ??
                    new List<SectionPipeOptions>())
                    if (pipe != null && pipe.Diameter > 0)
                        parts.Add("P=" + pipe.Diameter.ToString("0.###",
                            CultureInfo.InvariantCulture) + ":" +
                            pipe.VerticalMode);
            }
            return string.Join("|", parts.ToArray());
        }

        private static string BuildMergedTitle(List<PlannerSource> sources)
        {
            var segments = new List<PipelineSegment>();
            foreach (PlannerSource source in sources)
            {
                if (string.IsNullOrWhiteSpace(source.SegmentTitle)) continue;
                PipelineSegment segment;
                segments.Add(PipelineSegment.TryParse(source.SegmentTitle,
                    out segment) ? segment : PipelineSegment.FromRaw(
                        source.SegmentTitle, source.Order));
            }
            segments.Sort(PipelineSegment.Compare);
            var lines = new List<string>();
            PipelineSegment current = null;
            foreach (PipelineSegment segment in segments)
            {
                if (current == null) { current = segment.Clone(); continue; }
                if (current.CanMerge(segment))
                {
                    current.EndText = segment.EndText;
                    current.EndNumber = segment.EndNumber;
                }
                else { lines.Add(current.ToText()); current = segment.Clone(); }
            }
            if (current != null) lines.Add(current.ToText());
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static string Key(string text)
        {
            return Regex.Replace((text ?? string.Empty).Trim(), @"\s+",
                string.Empty).ToUpperInvariant();
        }

        private static bool Contains(string text, params string[] values)
        {
            foreach (string value in values ?? new string[0])
                if (!string.IsNullOrWhiteSpace(value) &&
                    (text ?? string.Empty).IndexOf(value,
                        StringComparison.CurrentCultureIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private static double Round(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? 0.0
                : Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private sealed class PlannerSource
        {
            public string SourceId;
            public int Order;
            public QuantityPipeAttributes Attributes;
            public double Width;
            public List<SectionLayerOptions> Layers;
            public string SegmentTitle;
            public string GroupKey;
        }

        private sealed class PlannerGroup
        {
            public PlannerSource Template;
            public List<PlannerSource> Sources = new List<PlannerSource>();
        }

        private struct PatternChoice
        {
            public string Name;
            public double Scale;
            public PatternChoice(string name, double scale)
            {
                Name = name ?? string.Empty;
                Scale = scale;
            }
        }

        private sealed class PipelineSegment
        {
            public string RawText;
            public string StartText;
            public string EndText;
            public string StartPrefix;
            public string EndPrefix;
            public int StartNumber;
            public int EndNumber;
            public int Order;
            public bool Parsed;

            public static PipelineSegment FromRaw(string text, int order)
            {
                return new PipelineSegment
                {
                    RawText = text ?? string.Empty,
                    StartText = text ?? string.Empty,
                    Order = order
                };
            }

            public static bool TryParse(string text,
                out PipelineSegment segment)
            {
                segment = null;
                string[] parts = (text ?? string.Empty).Split(
                    new[] { "至" }, StringSplitOptions.None);
                NodeCode start;
                NodeCode end;
                if (parts.Length != 2 || !NodeCode.TryParse(parts[0],
                        out start) || !NodeCode.TryParse(parts[1], out end))
                    return false;
                segment = new PipelineSegment
                {
                    RawText = text.Trim(),
                    StartText = parts[0].Trim(),
                    EndText = parts[1].Trim(),
                    StartPrefix = start.Prefix,
                    EndPrefix = end.Prefix,
                    StartNumber = start.Number,
                    EndNumber = end.Number,
                    Parsed = true
                };
                return true;
            }

            public PipelineSegment Clone()
            {
                return (PipelineSegment)MemberwiseClone();
            }

            public bool CanMerge(PipelineSegment next)
            {
                return next != null && Parsed && next.Parsed &&
                    string.Equals(StartPrefix, next.StartPrefix,
                        StringComparison.CurrentCultureIgnoreCase) &&
                    string.Equals(EndPrefix, next.EndPrefix,
                        StringComparison.CurrentCultureIgnoreCase) &&
                    EndNumber == next.StartNumber;
            }

            public string ToText()
            {
                return Parsed ? StartText + "至" + EndText
                    : RawText ?? string.Empty;
            }

            public static int Compare(PipelineSegment left,
                PipelineSegment right)
            {
                if (left == null) return right == null ? 0 : -1;
                if (right == null) return 1;
                if (left.Parsed && right.Parsed)
                {
                    int result = string.Compare(left.StartPrefix,
                        right.StartPrefix,
                        StringComparison.CurrentCultureIgnoreCase);
                    if (result != 0) return result;
                    result = left.StartNumber.CompareTo(right.StartNumber);
                    return result != 0 ? result
                        : left.EndNumber.CompareTo(right.EndNumber);
                }
                if (left.Parsed) return -1;
                if (right.Parsed) return 1;
                return left.Order.CompareTo(right.Order);
            }
        }

        private struct NodeCode
        {
            public string Prefix;
            public int Number;
            public static bool TryParse(string text, out NodeCode code)
            {
                code = new NodeCode();
                Match match = Regex.Match((text ?? string.Empty).Trim(),
                    @"^(?<prefix>[^0-9]*?)(?<number>\d+)$");
                int number;
                if (!match.Success || !int.TryParse(
                        match.Groups["number"].Value, out number))
                    return false;
                code.Prefix = match.Groups["prefix"].Value;
                code.Number = number;
                return true;
            }
        }
    }
}
