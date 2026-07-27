using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace TCPipeAutoDraw.Modules.SectionDrawing
{
    /// <summary>
    /// 断面图批量生成服务。
    /// 根据工程量管线属性生成断面图，按结构层一致性合并重复断面。
    /// </summary>
    public static class SectionBatchDrawingService
    {
        private const int ColumnsPerRow = 10;
        private const double HorizontalGap = 0.25;
        private const double VerticalGap = 0.40;
        private const double CategoryVerticalGap = 0.55;

        public static SectionBatchDrawingResult Run(Document doc)
        {
            return Run(doc, null);
        }

        public static SectionBatchDrawingResult Run(Document doc, Action<int, int, string> progress)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            Editor ed = doc.Editor;

            List<ObjectId> ids = PromptSelectRegion(doc);
            if (ids == null || ids.Count == 0)
            {
                return new SectionBatchDrawingResult
                {
                    Success = false,
                    Message = "未选择任何对象。"
                };
            }

            List<BatchPipeSectionSource> sources = BuildSources(doc, ids);
            if (sources.Count == 0)
            {
                return new SectionBatchDrawingResult
                {
                    Success = false,
                    Message = "所选区域内未找到可生成断面图的主管或支管属性。请确认对象已通过 属性编辑器 写入管线属性，且回填结构层不为空。",
                    SelectedCount = ids.Count
                };
            }

            List<BatchSectionGroup> groups = GroupSources(sources);
            if (groups.Count == 0)
            {
                return new SectionBatchDrawingResult
                {
                    Success = false,
                    Message = "没有可绘制的断面图。",
                    SelectedCount = ids.Count,
                    PipeCount = sources.Count
                };
            }

            var pointOpt = new PromptPointOptions("\n请选择生成断面图左下角插入点：");
            PromptPointResult pointRes = ed.GetPoint(pointOpt);
            if (pointRes.Status != PromptStatus.OK)
            {
                return new SectionBatchDrawingResult
                {
                    Success = false,
                    Message = "已取消绘制。",
                    SelectedCount = ids.Count,
                    PipeCount = sources.Count,
                    SectionCount = groups.Count
                };
            }

            SectionDrawingOptions baseOptions = SectionDrawingSettingsStore.Load();
            int entityCount = 0;
            int hatchFailureCount = 0;
            int successCount = 0;
            int failCount = 0;

            var mainGroups = new List<BatchSectionGroup>();
            var branchGroups = new List<BatchSectionGroup>();
            for (int i = 0; i < groups.Count; i++)
            {
                BatchSectionGroup group = groups[i];
                if (IsBranchGroup(group)) branchGroups.Add(group);
                else mainGroups.Add(group);
            }

            double nextRowY = pointRes.Value.Y;
            Point3d startPoint = pointRes.Value;
            int progressIndex = 0;
            int progressTotal = Math.Max(groups.Count, 1);
            ReportProgress(progress, 0, progressTotal, "正在生成批量断面图...");

            DrawGroupRows(doc, mainGroups, baseOptions, startPoint, ref nextRowY, ref successCount, ref failCount, ref entityCount, ref hatchFailureCount, progress, progressTotal, ref progressIndex);

            if (mainGroups.Count > 0 && branchGroups.Count > 0)
            {
                nextRowY -= CategoryVerticalGap;
            }

            DrawGroupRows(doc, branchGroups, baseOptions, new Point3d(startPoint.X, nextRowY, startPoint.Z), ref nextRowY, ref successCount, ref failCount, ref entityCount, ref hatchFailureCount, progress, progressTotal, ref progressIndex);
            ReportProgress(progress, progressTotal, progressTotal, "批量断面图生成完成。");

            return new SectionBatchDrawingResult
            {
                Success = successCount > 0,
                Message = successCount > 0 ? "批量断面图已生成。" : "批量断面图生成失败。",
                SelectedCount = ids.Count,
                PipeCount = sources.Count,
                SectionCount = groups.Count,
                SuccessSectionCount = successCount,
                FailSectionCount = failCount,
                EntityCount = entityCount,
                HatchFailureCount = hatchFailureCount,
                MergedPipeCount = Math.Max(0, sources.Count - groups.Count)
            };
        }

        private static void DrawGroupRows(
            Document doc,
            IList<BatchSectionGroup> groups,
            SectionDrawingOptions baseOptions,
            Point3d startPoint,
            ref double nextRowY,
            ref int successCount,
            ref int failCount,
            ref int entityCount,
            ref int hatchFailureCount,
            Action<int, int, string> progress,
            int progressTotal,
            ref int progressIndex)
        {
            if (groups == null || groups.Count == 0)
            {
                nextRowY = startPoint.Y;
                return;
            }

            double currentX = startPoint.X;
            double currentY = startPoint.Y;
            double rowHeight = 0.0;
            int column = 0;

            for (int i = 0; i < groups.Count; i++)
            {
                BatchSectionGroup group = groups[i];
                if (column >= ColumnsPerRow)
                {
                    currentX = startPoint.X;
                    currentY -= rowHeight + VerticalGap;
                    rowHeight = 0.0;
                    column = 0;
                }

                SectionDrawingOptions options = group.BuildOptions(baseOptions);
                SectionLayoutCalculator.Normalize(options);
                SectionDrawingOptions drawingOptions = SectionDrawingScaleService.CreateScaledOptions(options);
                SectionLayout layout = SectionLayoutCalculator.Calculate(drawingOptions);

                double tileWidth = Math.Max(0.20, layout.ExtentMaxX - layout.ExtentMinX);
                double tileHeight = Math.Max(0.20, layout.ExtentMaxY - layout.ExtentMinY);

                Point3d bodyOrigin = new Point3d(currentX + drawingOptions.LeftLabelWidth, currentY, startPoint.Z);
                SectionDrawingResult drawResult = SectionDrawingService.Draw(doc, options, bodyOrigin, group.GetSourceIds());
                if (drawResult != null && drawResult.Success)
                {
                    successCount++;
                    entityCount += drawResult.EntityCount;
                    hatchFailureCount += drawResult.HatchFailureCount;
                }
                else
                {
                    failCount++;
                }

                currentX += tileWidth + HorizontalGap;
                if (tileHeight > rowHeight) rowHeight = tileHeight;
                column++;
                progressIndex++;
                ReportProgress(progress, progressIndex, progressTotal, "正在生成批量断面图：" + progressIndex + "/" + progressTotal);
            }

            nextRowY = currentY - rowHeight;
        }

        private static void ReportProgress(Action<int, int, string> progress, int current, int total, string message)
        {
            if (progress == null) return;
            progress(current, total, message);
        }

        private static bool IsBranchGroup(BatchSectionGroup group)
        {
            if (group == null || group.Template == null || group.Template.Attributes == null) return false;
            return QuantityPipeAttributes.IsBranchKind(group.Template.Attributes.ObjectKind);
        }

        private static List<ObjectId> PromptSelectRegion(Document doc)
        {
            Editor ed = doc.Editor;
            var ids = new List<ObjectId>();

            try
            {
                PromptSelectionResult implied = ed.SelectImplied();
                if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
                {
                    foreach (SelectedObject selected in implied.Value)
                    {
                        if (selected != null && !selected.ObjectId.IsNull) ids.Add(selected.ObjectId);
                    }
                    ed.SetImpliedSelection(new ObjectId[0]);
                    if (ids.Count > 0) return ids;
                }
            }
            catch
            {
            }

            var selOpt = new PromptSelectionOptions();
            selOpt.MessageForAdding = "\n请选择需要批量生成断面图的管线区域对象：";
            selOpt.MessageForRemoval = "\n移除对象：";
            selOpt.AllowDuplicates = false;
            PromptSelectionResult res = ed.GetSelection(selOpt);
            if (res.Status != PromptStatus.OK || res.Value == null) return ids;

            foreach (SelectedObject selected in res.Value)
            {
                if (selected != null && !selected.ObjectId.IsNull) ids.Add(selected.ObjectId);
            }
            return ids;
        }

        private static List<BatchPipeSectionSource> BuildSources(Document doc, IList<ObjectId> ids)
        {
            var sources = new List<BatchPipeSectionSource>();
            if (doc == null || ids == null) return sources;

            int order = 0;
            foreach (ObjectId id in ids)
            {
                order++;
                if (id.IsNull) continue;

                QuantityPipeSelectionInfo info;
                try
                {
                    info = QuantityPipeAttributeService.ReadPipe(doc, id);
                }
                catch
                {
                    continue;
                }

                if (info == null || info.Attributes == null) continue;
                if (!info.HasSavedAttributes) continue;

                QuantityPipeAttributes attrs = info.Attributes;
                if (!attrs.Enabled) continue;
                if (QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind)) continue;
                if (!QuantityPipeAttributes.IsMainPipeKind(attrs.ObjectKind) && !QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind)) continue;
                if (QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind) && !attrs.BranchIncludeInCalculation && string.IsNullOrWhiteSpace(attrs.BackfillStructure)) continue;

                List<QuantityStructureLayer> qLayers = QuantityStructureLayer.Parse(attrs.BackfillStructure);
                if (qLayers.Count == 0) continue;

                List<SectionLayerOptions> sectionLayers = ConvertLayers(qLayers, attrs);
                if (sectionLayers.Count == 0) continue;

                double width = attrs.TrenchWidth > 0 ? attrs.TrenchWidth : SectionDrawingOptions.Default.Width;
                if (width <= 0) width = 1.0;

                double pipeDiameter = attrs.PipeOuterDiameter > 0 ? attrs.PipeOuterDiameter : InferPipeDiameter(attrs.Diameter);
                if (pipeDiameter > 0)
                {
                    int pipeLayerIndex = ResolvePipeLayerIndex(qLayers, sectionLayers);
                    if (pipeLayerIndex >= 0 && pipeLayerIndex < sectionLayers.Count)
                    {
                        SectionPipeVerticalMode verticalMode = HasCushionLayerBelow(qLayers, pipeLayerIndex)
                            ? SectionPipeVerticalMode.LayerBottom
                            : SectionPipeVerticalMode.LayerCenter;

                        sectionLayers[pipeLayerIndex].Pipes.Add(new SectionPipeOptions
                        {
                            Diameter = pipeDiameter,
                            PipeText = SectionPipeOptions.BuildPipeText(pipeDiameter),
                            HostLayerIndex = pipeLayerIndex,
                            VerticalMode = verticalMode
                        });
                    }
                }

                string title = BuildSegmentTitle(attrs);
                string key = BuildGroupKey(attrs, width, sectionLayers);

                sources.Add(new BatchPipeSectionSource
                {
                    Order = order,
                    Info = info,
                    Attributes = attrs,
                    Width = width,
                    Layers = sectionLayers,
                    SegmentTitle = title,
                    GroupKey = key
                });
            }

            return sources;
        }

        private static List<SectionLayerOptions> ConvertLayers(IList<QuantityStructureLayer> qLayers, QuantityPipeAttributes attrs)
        {
            var layers = new List<SectionLayerOptions>();
            if (qLayers == null) return layers;

            for (int i = 0; i < qLayers.Count; i++)
            {
                QuantityStructureLayer q = qLayers[i];
                if (q == null) continue;
                if (q.Height <= 0) continue;

                PatternChoice pattern = ResolveDefaultPattern(q.Name, q.RawText);
                layers.Add(new SectionLayerOptions
                {
                    DrawLayer = true,
                    // 注记文字直接取属性表原始层名，仅去掉末尾高度/控制词；
                    // 不使用 QuantityStructureLayer.Name，因为其清理逻辑会把 C25 中的 25 一并去掉。
                    LeftLabel = BuildLayerDisplayName(q),
                    Height = RoundForSection(q.Height),
                    HeightLocked = q.Locked,
                    HatchPatternName = pattern.Name,
                    HatchScale = pattern.Scale,
                    HatchAngle = 0.0,
                    Pipes = new List<SectionPipeOptions>()
                });
            }

            return layers;
        }


        private static string BuildLayerDisplayName(QuantityStructureLayer layer)
        {
            if (layer == null) return string.Empty;

            string text = layer.RawText == null ? string.Empty : layer.RawText.Trim();
            if (string.IsNullOrWhiteSpace(text)) text = layer.Name == null ? string.Empty : layer.Name.Trim();
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // 去掉控制词，但保留材料名称中的数字，例如 C25、C30。
            text = Regex.Replace(text, @"\s+垫层\s*$", string.Empty);
            text = Regex.Replace(text, "锁定|固定|管线层|管道层|管层|井下层|井下方垫层", " ");

            // 只移除与层高相同的最后一个数字，避免把 C25 / C30 中的数字删掉。
            MatchCollection matches = Regex.Matches(text, @"[-+]?\d+(?:\.\d+)?");
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                double value;
                if (!double.TryParse(matches[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    && !double.TryParse(matches[i].Value, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) continue;

                if (Math.Abs(value - layer.Height) <= 0.000001)
                {
                    text = text.Remove(matches[i].Index, matches[i].Length);
                    break;
                }
            }

            text = Regex.Replace(text, @"[：:，,；;、/\\|]+", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            if (!string.IsNullOrWhiteSpace(text)) return text;

            return layer.Name == null ? string.Empty : layer.Name.Trim();
        }

        private static int ResolvePipeLayerIndex(IList<QuantityStructureLayer> qLayers, IList<SectionLayerOptions> sectionLayers)
        {
            if (sectionLayers == null || sectionLayers.Count == 0) return -1;
            if (qLayers != null)
            {
                int sectionIndex = 0;
                for (int i = 0; i < qLayers.Count; i++)
                {
                    QuantityStructureLayer layer = qLayers[i];
                    if (layer == null || layer.Height <= 0) continue;
                    if (layer.IsPipeLayer) return sectionIndex;
                    sectionIndex++;
                }

                sectionIndex = 0;
                for (int i = 0; i < qLayers.Count; i++)
                {
                    QuantityStructureLayer layer = qLayers[i];
                    if (layer == null || layer.Height <= 0) continue;
                    string text = (layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty);
                    if (ContainsAny(text, "包管", "管顶", "管周")) return sectionIndex;
                    sectionIndex++;
                }
            }

            return Math.Max(0, sectionLayers.Count / 2);
        }

        private static bool HasCushionLayerBelow(IList<QuantityStructureLayer> qLayers, int pipeLayerSectionIndex)
        {
            if (qLayers == null || pipeLayerSectionIndex < 0) return false;

            int sectionIndex = 0;
            bool passedPipeLayer = false;
            for (int i = 0; i < qLayers.Count; i++)
            {
                QuantityStructureLayer layer = qLayers[i];
                if (layer == null || layer.Height <= 0) continue;

                if (!passedPipeLayer)
                {
                    if (sectionIndex == pipeLayerSectionIndex)
                    {
                        passedPipeLayer = true;
                    }
                    sectionIndex++;
                    continue;
                }

                string text = (layer.Name ?? string.Empty) + " " + (layer.RawText ?? string.Empty);
                if (layer.IsCushionLayer || layer.IsBelowWellLayer || ContainsAny(text, "垫层")) return true;
                sectionIndex++;
            }

            return false;
        }


        private static PatternChoice ResolveDefaultPattern(string name, string rawText)
        {
            string text = ((name ?? string.Empty) + " " + (rawText ?? string.Empty)).Trim();

            if (ContainsAny(text, "碎石")) return new PatternChoice("HEX", 0.1);
            if (ContainsAny(text, "原土") && ContainsAny(text, "回填")) return new PatternChoice("EARTH", 0.1);
            if (ContainsAny(text, "C25", "C30", "C20", "C15", "砼", "混凝土", "路面恢复", "恢复路面", "砼恢复", "混凝土恢复")) return new PatternChoice("AR-CONC", 0.01);
            if (ContainsAny(text, "中粗砂", "粗砂", "砂包管", "砂垫层", "砂回填", "包管") || (ContainsAny(text, "砂") && !ContainsAny(text, "砂浆"))) return new PatternChoice("1064", 0.01);

            return new PatternChoice(string.Empty, 1.0);
        }

        private static double InferPipeDiameter(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0.0;
            Match m = Regex.Match(text, @"(?i)DN\s*(\d+(?:\.\d+)?)");
            if (!m.Success) m = Regex.Match(text, @"[Φφ]\s*(\d+(?:\.\d+)?)");
            if (!m.Success) m = Regex.Match(text, @"(\d+(?:\.\d+)?)");
            if (!m.Success) return 0.0;

            double value;
            if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return 0.0;

            if (value <= 0) return 0.0;
            return value > 10.0 ? value / 1000.0 : value;
        }

        private static string BuildSegmentTitle(QuantityPipeAttributes attrs)
        {
            if (attrs == null) return string.Empty;
            string start = attrs.StartNode == null ? string.Empty : attrs.StartNode.Trim();
            string end = attrs.EndNode == null ? string.Empty : attrs.EndNode.Trim();
            if (!string.IsNullOrWhiteSpace(start) && !string.IsNullOrWhiteSpace(end)) return start + "至" + end;
            return string.Empty;
        }

        private static string BuildGroupKey(QuantityPipeAttributes attrs, double width, IList<SectionLayerOptions> layers)
        {
            var parts = new List<string>();
            string kindKey = attrs != null && QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind) ? "BRANCH" : "MAIN";
            parts.Add("K=" + kindKey);
            parts.Add("W=" + width.ToString("0.###", CultureInfo.InvariantCulture));
            if (layers != null)
            {
                for (int i = 0; i < layers.Count; i++)
                {
                    SectionLayerOptions layer = layers[i];
                    if (layer == null || !layer.DrawLayer) continue;
                    parts.Add("L=" + NormalizeKeyText(layer.LeftLabel) + ":" + layer.Height.ToString("0.########", CultureInfo.InvariantCulture) + ":" + NormalizeKeyText(layer.HatchPatternName) + ":" + layer.HatchScale.ToString("0.########", CultureInfo.InvariantCulture));
                    if (layer.Pipes != null)
                    {
                        for (int p = 0; p < layer.Pipes.Count; p++)
                        {
                            SectionPipeOptions pipe = layer.Pipes[p];
                            if (pipe == null || pipe.Diameter <= 0) continue;
                            parts.Add("P=" + pipe.Diameter.ToString("0.###", CultureInfo.InvariantCulture) + ":" + pipe.VerticalMode.ToString());
                        }
                    }
                }
            }
            return string.Join("|", parts.ToArray());
        }

        private static string NormalizeKeyText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return Regex.Replace(text.Trim(), @"\s+", string.Empty).ToUpperInvariant();
        }

        private static List<BatchSectionGroup> GroupSources(List<BatchPipeSectionSource> sources)
        {
            var groups = new List<BatchSectionGroup>();
            var lookup = new Dictionary<string, BatchSectionGroup>(StringComparer.OrdinalIgnoreCase);

            foreach (BatchPipeSectionSource source in sources)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.GroupKey)) continue;
                BatchSectionGroup group;
                if (!lookup.TryGetValue(source.GroupKey, out group))
                {
                    group = new BatchSectionGroup
                    {
                        Key = source.GroupKey,
                        Template = source
                    };
                    lookup[source.GroupKey] = group;
                    groups.Add(group);
                }
                group.Sources.Add(source);
            }

            return groups;
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

        private static double RoundForSection(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0.0;
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
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

        private sealed class BatchPipeSectionSource
        {
            public int Order;
            public QuantityPipeSelectionInfo Info;
            public QuantityPipeAttributes Attributes;
            public double Width;
            public List<SectionLayerOptions> Layers;
            public string SegmentTitle;
            public string GroupKey;
        }

        private sealed class BatchSectionGroup
        {
            public string Key;
            public BatchPipeSectionSource Template;
            public List<BatchPipeSectionSource> Sources = new List<BatchPipeSectionSource>();

            public IEnumerable<ObjectId> GetSourceIds()
            {
                for (int i = 0; i < Sources.Count; i++)
                {
                    BatchPipeSectionSource source = Sources[i];
                    if (source != null && source.Info != null && !source.Info.ObjectId.IsNull) yield return source.Info.ObjectId;
                }
            }

            public SectionDrawingOptions BuildOptions(SectionDrawingOptions baseOptions)
            {
                SectionDrawingOptions options = baseOptions == null ? SectionDrawingOptions.Default.Clone() : baseOptions.Clone();
                options.Width = Template == null ? options.Width : Template.Width;
                options.LockTotalHeight = false;
                options.Layers = new List<SectionLayerOptions>();
                if (Template != null && Template.Layers != null)
                {
                    for (int i = 0; i < Template.Layers.Count; i++)
                    {
                        options.Layers.Add(Template.Layers[i] == null ? new SectionLayerOptions() : Template.Layers[i].Clone());
                    }
                }
                options.SectionTitle = BuildMergedTitle();
                options.DrawTitle = !string.IsNullOrWhiteSpace(options.SectionTitle);
                options.DrawPipeCircle = true;
                SectionLayoutCalculator.Normalize(options);
                options.TotalHeight = RoundForSection(options.TotalHeight);
                return options;
            }

            private string BuildMergedTitle()
            {
                var segments = new List<PipelineSegment>();
                for (int i = 0; i < Sources.Count; i++)
                {
                    BatchPipeSectionSource source = Sources[i];
                    if (source == null || string.IsNullOrWhiteSpace(source.SegmentTitle)) continue;
                    PipelineSegment seg;
                    if (PipelineSegment.TryParse(source.SegmentTitle, out seg)) segments.Add(seg);
                    else segments.Add(PipelineSegment.FromRaw(source.SegmentTitle, source.Order));
                }
                if (segments.Count == 0) return string.Empty;

                segments.Sort(PipelineSegment.Compare);
                var lines = new List<string>();
                PipelineSegment current = null;
                for (int i = 0; i < segments.Count; i++)
                {
                    PipelineSegment seg = segments[i];
                    if (current == null)
                    {
                        current = seg.Clone();
                        continue;
                    }

                    if (current.CanMerge(seg))
                    {
                        current.EndText = seg.EndText;
                        current.EndPrefix = seg.EndPrefix;
                        current.EndNumber = seg.EndNumber;
                        current.RawText = current.StartText + "至" + current.EndText;
                    }
                    else
                    {
                        lines.Add(current.ToText());
                        current = seg.Clone();
                    }
                }
                if (current != null) lines.Add(current.ToText());
                return string.Join(Environment.NewLine, lines.ToArray());
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
                    EndText = string.Empty,
                    Order = order,
                    Parsed = false
                };
            }

            public static bool TryParse(string text, out PipelineSegment segment)
            {
                segment = null;
                if (string.IsNullOrWhiteSpace(text)) return false;
                string[] parts = text.Split(new[] { "至" }, StringSplitOptions.None);
                if (parts.Length != 2) return false;

                NodeCode start;
                NodeCode end;
                if (!NodeCode.TryParse(parts[0].Trim(), out start)) return false;
                if (!NodeCode.TryParse(parts[1].Trim(), out end)) return false;

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
                return new PipelineSegment
                {
                    RawText = RawText,
                    StartText = StartText,
                    EndText = EndText,
                    StartPrefix = StartPrefix,
                    EndPrefix = EndPrefix,
                    StartNumber = StartNumber,
                    EndNumber = EndNumber,
                    Order = Order,
                    Parsed = Parsed
                };
            }

            public bool CanMerge(PipelineSegment next)
            {
                if (next == null || !Parsed || !next.Parsed) return false;
                if (!string.Equals(StartPrefix, next.StartPrefix, StringComparison.CurrentCultureIgnoreCase)) return false;
                if (!string.Equals(EndPrefix, next.EndPrefix, StringComparison.CurrentCultureIgnoreCase)) return false;
                return EndNumber == next.StartNumber;
            }

            public string ToText()
            {
                if (!Parsed) return RawText ?? string.Empty;
                return StartText + "至" + EndText;
            }

            public static int Compare(PipelineSegment a, PipelineSegment b)
            {
                if (a == null && b == null) return 0;
                if (a == null) return -1;
                if (b == null) return 1;
                if (a.Parsed && b.Parsed)
                {
                    int c = string.Compare(a.StartPrefix, b.StartPrefix, StringComparison.CurrentCultureIgnoreCase);
                    if (c != 0) return c;
                    c = a.StartNumber.CompareTo(b.StartNumber);
                    if (c != 0) return c;
                    return a.EndNumber.CompareTo(b.EndNumber);
                }
                if (a.Parsed) return -1;
                if (b.Parsed) return 1;
                return a.Order.CompareTo(b.Order);
            }
        }

        private struct NodeCode
        {
            public string Prefix;
            public int Number;

            public static bool TryParse(string text, out NodeCode code)
            {
                code = new NodeCode();
                if (string.IsNullOrWhiteSpace(text)) return false;
                Match m = Regex.Match(text.Trim(), @"^(?<prefix>[^0-9]*?)(?<number>\d+)$");
                if (!m.Success) return false;
                int number;
                if (!int.TryParse(m.Groups["number"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return false;
                code.Prefix = m.Groups["prefix"].Value;
                code.Number = number;
                return true;
            }
        }
    }

    public sealed class SectionBatchDrawingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int SelectedCount { get; set; }
        public int PipeCount { get; set; }
        public int SectionCount { get; set; }
        public int SuccessSectionCount { get; set; }
        public int FailSectionCount { get; set; }
        public int EntityCount { get; set; }
        public int HatchFailureCount { get; set; }
        public int MergedPipeCount { get; set; }

        public SectionBatchDrawingResult()
        {
            Message = string.Empty;
        }

        public string ToEditorMessage()
        {
            if (!Success) return "\n[批量断面图] " + Message;
            string text = "\n[批量断面图] 完成。选中对象 " + SelectedCount + " 个，识别管线 " + PipeCount + " 条，生成断面 " + SuccessSectionCount + " 张";
            if (MergedPipeCount > 0) text += "，合并重复 " + MergedPipeCount + " 条";
            text += "，生成对象 " + EntityCount + " 个。";
            if (FailSectionCount > 0) text += "失败断面 " + FailSectionCount + " 张。";
            if (HatchFailureCount > 0) text += "有 " + HatchFailureCount + " 个填充图案未能生成，请检查填充名称是否存在。";
            return text;
        }
    }
}
