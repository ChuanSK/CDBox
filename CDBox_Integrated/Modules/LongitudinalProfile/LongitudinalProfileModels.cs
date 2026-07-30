using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TCPipeAutoDraw.Modules.LongitudinalProfile
{
    public sealed class LongitudinalProfilePipeData
    {
        public string SourceId { get; set; }
        public string StartNode { get; set; }
        public string EndNode { get; set; }
        public string Diameter { get; set; }
        public double OuterDiameter { get; set; }
        public double PlanLength { get; set; }
        public int SelectionOrder { get; set; }
    }

    public sealed class LongitudinalProfileWellData
    {
        public string SourceId { get; set; }
        public string NodeNo { get; set; }
        public string WellSpec { get; set; }
        public string WellType { get; set; }
        public double GroundElevation { get; set; }
        public double WellDepth { get; set; }
        public double SiltWellDeductDepth500 { get; set; }
        public double SiltWellDeductDepth700 { get; set; }
    }

    public sealed class LongitudinalProfileNodeData
    {
        public string SourceId { get; set; }
        public string NodeNo { get; set; }
        public double GroundElevation { get; set; }
        public double DesignInvertElevation { get; set; }
        public double PipeBottomDepth { get; set; }
        public double WellDepth { get; set; }
        public double SiltWellAdjustment { get; set; }
        public double CumulativeDistance { get; set; }
    }

    public sealed class LongitudinalProfileSpanData
    {
        public string SourceId { get; set; }
        public string StartNode { get; set; }
        public string EndNode { get; set; }
        public string Diameter { get; set; }
        public double OuterDiameter { get; set; }
        public double PlanLength { get; set; }
        public double SlopePermille { get; set; }
    }

    public sealed class LongitudinalProfileData
    {
        public List<LongitudinalProfileNodeData> Nodes { get; set; }
        public List<LongitudinalProfileSpanData> Spans { get; set; }

        public LongitudinalProfileData()
        {
            Nodes = new List<LongitudinalProfileNodeData>();
            Spans = new List<LongitudinalProfileSpanData>();
        }
    }

    public sealed class LongitudinalProfileBuildResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public LongitudinalProfileData Profile { get; set; }

        public LongitudinalProfileBuildResult()
        {
            Message = string.Empty;
            Profile = new LongitudinalProfileData();
        }
    }

    /// <summary>
    /// 纵断面拓扑及高程计算。该类不依赖 AutoCAD，便于用固定数据核对坡度。
    /// </summary>
    public static class LongitudinalProfileCalculator
    {
        private static readonly StringComparer NodeComparer =
            StringComparer.CurrentCultureIgnoreCase;

        public static LongitudinalProfileBuildResult Build(
            IEnumerable<LongitudinalProfilePipeData> selectedPipes,
            IEnumerable<LongitudinalProfileWellData> wells)
        {
            List<LongitudinalProfilePipeData> pipes =
                (selectedPipes ?? Enumerable.Empty<LongitudinalProfilePipeData>())
                    .Where(p => p != null).OrderBy(p => p.SelectionOrder).ToList();
            if (pipes.Count == 0)
                return Fail("请选择至少一条具有工程量属性的主管管线。");

            for (int i = 0; i < pipes.Count; i++)
            {
                LongitudinalProfilePipeData pipe = pipes[i];
                pipe.StartNode = Clean(pipe.StartNode);
                pipe.EndNode = Clean(pipe.EndNode);
                if (pipe.StartNode.Length == 0 || pipe.EndNode.Length == 0)
                    return Fail("所选管线存在未识别起点井或终点井的对象，请先在属性编辑器中补全。");
                if (NodeComparer.Equals(pipe.StartNode, pipe.EndNode))
                    return Fail("管线“" + DisplayId(pipe.SourceId)
                        + "”的起点井与终点井相同，无法生成纵断面。");
                if (pipe.PlanLength <= 1e-8)
                    return Fail("管线“" + DisplayId(pipe.SourceId)
                        + "”没有有效长度。");
            }

            var adjacency =
                new Dictionary<string, List<int>>(NodeComparer);
            for (int i = 0; i < pipes.Count; i++)
            {
                AddEdge(adjacency, pipes[i].StartNode, i);
                AddEdge(adjacency, pipes[i].EndNode, i);
            }
            foreach (KeyValuePair<string, List<int>> item in adjacency)
            {
                if (item.Value.Count > 2)
                    return Fail("井“" + item.Key
                        + "”连接了三条及以上所选管线。第一阶段仅支持无分支的连续路径。");
            }

            if (!AllEdgesConnected(pipes, adjacency))
                return Fail("所选管线没有通过井节点连成一条连续路径。");

            List<string> ends = adjacency
                .Where(x => x.Value.Count == 1)
                .Select(x => x.Key).ToList();
            if (ends.Count != 2)
                return Fail("所选管线形成闭环或拓扑不完整，第一阶段仅支持具有明确起点和终点的路径。");

            LongitudinalProfilePipeData first = pipes[0];
            string start = ends.FirstOrDefault(x =>
                NodeComparer.Equals(x, first.StartNode))
                ?? ends.FirstOrDefault(x =>
                    NodeComparer.Equals(x, first.EndNode))
                ?? ends.OrderBy(x => x, NodeComparer).First();

            var orderedPipes = new List<LongitudinalProfilePipeData>();
            var orderedNodes = new List<string> { start };
            var used = new HashSet<int>();
            string current = start;
            while (orderedPipes.Count < pipes.Count)
            {
                List<int> incident;
                if (!adjacency.TryGetValue(current, out incident))
                    return Fail("管线路径在井“" + current + "”处中断。");
                int edge = incident.FirstOrDefault(index => !used.Contains(index));
                if (used.Contains(edge))
                    return Fail("管线路径在井“" + current + "”处中断。");
                used.Add(edge);
                LongitudinalProfilePipeData pipe = pipes[edge];
                orderedPipes.Add(pipe);
                current = NodeComparer.Equals(pipe.StartNode, current)
                    ? pipe.EndNode : pipe.StartNode;
                orderedNodes.Add(current);
            }

            var wellLookup =
                new Dictionary<string, LongitudinalProfileWellData>(NodeComparer);
            foreach (LongitudinalProfileWellData well in
                wells ?? Enumerable.Empty<LongitudinalProfileWellData>())
            {
                if (well == null) continue;
                string nodeNo = Clean(well.NodeNo);
                if (nodeNo.Length > 0 && !wellLookup.ContainsKey(nodeNo))
                    wellLookup[nodeNo] = well;
            }

            var profile = new LongitudinalProfileData();
            double distance = 0.0;
            for (int i = 0; i < orderedNodes.Count; i++)
            {
                string nodeNo = orderedNodes[i];
                LongitudinalProfileWellData well;
                if (!wellLookup.TryGetValue(nodeNo, out well))
                    return Fail("未找到井“" + nodeNo
                        + "”的属性对象，请确认井编号与管线起终点井一致。");
                if (!IsFinite(well.GroundElevation) || !IsFinite(well.WellDepth)
                    || well.WellDepth <= 0.0)
                    return Fail("井“" + nodeNo
                        + "”缺少有效的自然地面标高或井深。");

                double adjustment = ResolveSiltAdjustment(well);
                profile.Nodes.Add(new LongitudinalProfileNodeData
                {
                    SourceId = well.SourceId ?? string.Empty,
                    NodeNo = nodeNo,
                    GroundElevation = well.GroundElevation,
                    DesignInvertElevation =
                        well.GroundElevation - well.WellDepth + adjustment,
                    PipeBottomDepth = well.WellDepth + adjustment,
                    WellDepth = well.WellDepth,
                    SiltWellAdjustment = adjustment,
                    CumulativeDistance = distance
                });
                if (i < orderedPipes.Count)
                    distance += orderedPipes[i].PlanLength;
            }

            for (int i = 0; i < orderedPipes.Count; i++)
            {
                LongitudinalProfilePipeData pipe = orderedPipes[i];
                LongitudinalProfileNodeData from = profile.Nodes[i];
                LongitudinalProfileNodeData to = profile.Nodes[i + 1];
                profile.Spans.Add(new LongitudinalProfileSpanData
                {
                    SourceId = pipe.SourceId ?? string.Empty,
                    StartNode = from.NodeNo,
                    EndNode = to.NodeNo,
                    Diameter = Clean(pipe.Diameter),
                    OuterDiameter = ResolveOuterDiameter(pipe),
                    PlanLength = pipe.PlanLength,
                    SlopePermille =
                        (from.DesignInvertElevation
                            - to.DesignInvertElevation)
                        / pipe.PlanLength * 1000.0
                });
            }

            return new LongitudinalProfileBuildResult
            {
                Success = true,
                Message = "纵断面路径已识别。",
                Profile = profile
            };
        }

        public static double ResolveSiltAdjustment(
            LongitudinalProfileWellData well)
        {
            if (well == null
                || (well.WellType ?? string.Empty).IndexOf("沉泥",
                    StringComparison.CurrentCultureIgnoreCase) < 0)
                return 0.0;
            bool is700 = (well.WellSpec ?? string.Empty).IndexOf("700",
                StringComparison.OrdinalIgnoreCase) >= 0;
            double value = is700
                ? well.SiltWellDeductDepth700
                : well.SiltWellDeductDepth500;
            if (value <= 0.0) value = is700 ? 0.50 : 0.20;
            // 兼容早期图纸把 20/50 直接按厘米保存的情况。
            if (value > 5.0) value /= 100.0;
            return value;
        }

        public static double ParseNominalDiameterMetres(string diameter)
        {
            Match match = Regex.Match(diameter ?? string.Empty,
                @"(?:DN|DE|Φ|φ)?\s*(\d+(?:\.\d+)?)",
                RegexOptions.IgnoreCase);
            double millimetres;
            return match.Success
                && double.TryParse(match.Groups[1].Value,
                    NumberStyles.Float, CultureInfo.InvariantCulture,
                    out millimetres)
                && millimetres > 0
                    ? millimetres / 1000.0
                    : 0.0;
        }

        private static double ResolveOuterDiameter(
            LongitudinalProfilePipeData pipe)
        {
            if (pipe != null && pipe.OuterDiameter > 0)
                return pipe.OuterDiameter;
            double parsed = ParseNominalDiameterMetres(
                pipe == null ? string.Empty : pipe.Diameter);
            return parsed > 0 ? parsed : 0.20;
        }

        private static bool AllEdgesConnected(
            IList<LongitudinalProfilePipeData> pipes,
            IDictionary<string, List<int>> adjacency)
        {
            var visitedEdges = new HashSet<int>();
            var pending = new Queue<int>();
            pending.Enqueue(0);
            while (pending.Count > 0)
            {
                int edge = pending.Dequeue();
                if (!visitedEdges.Add(edge)) continue;
                LongitudinalProfilePipeData pipe = pipes[edge];
                foreach (string node in new[] { pipe.StartNode, pipe.EndNode })
                {
                    List<int> edges;
                    if (!adjacency.TryGetValue(node, out edges)) continue;
                    foreach (int next in edges)
                        if (!visitedEdges.Contains(next)) pending.Enqueue(next);
                }
            }
            return visitedEdges.Count == pipes.Count;
        }

        private static void AddEdge(
            IDictionary<string, List<int>> adjacency,
            string node, int index)
        {
            List<int> edges;
            if (!adjacency.TryGetValue(node, out edges))
            {
                edges = new List<int>();
                adjacency[node] = edges;
            }
            edges.Add(index);
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string DisplayId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未知" : value.Trim();
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static LongitudinalProfileBuildResult Fail(string message)
        {
            return new LongitudinalProfileBuildResult
            {
                Success = false,
                Message = message ?? string.Empty
            };
        }
    }
}
