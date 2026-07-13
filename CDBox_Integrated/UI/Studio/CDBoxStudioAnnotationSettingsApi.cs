using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using TCPipeAutoDraw.Modules.AnnotationSettings;
using TCPipeAutoDraw.Modules.NodeAnnotation;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;
using TCPipeAutoDraw.Modules.SurfaceAreaAnnotation;
using TCPipeAutoDraw.UI;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioAnnotationSettingsApi
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static string BuildEnvelopeJson()
        {
            SurfaceAreaAnnotationOptions surface = SurfaceAreaAnnotationSettingsStore.Load();
            PipeLengthAnnotationOptions pipe = PipeLengthAnnotationSettingsStore.Load();
            NodeAnnotationOptions node = NodeAnnotationSettingsStore.Load();

            var envelope = new CDBoxStudioAnnotationSettingsEnvelope
            {
                current = BuildValues(surface, pipe, node),
                defaults = BuildValues(SurfaceAreaAnnotationOptions.Default, PipeLengthAnnotationOptions.Default, NodeAnnotationOptions.Default),
                textStyles = LoadTextStyleNames(surface.AnnotationFontName, pipe.AnnotationFontName, node.AnnotationFontName),
                layerLinkModes = BuildLayerLinkModes(),
                colors = BuildColorOptions()
            };

            return Serializer.Serialize(envelope);
        }

        public static string BuildDefaultSectionJson(string section)
        {
            string normalized = NormalizeSection(section);
            CDBoxStudioAnnotationSettingsValues values = BuildValues(
                SurfaceAreaAnnotationOptions.Default,
                PipeLengthAnnotationOptions.Default,
                NodeAnnotationOptions.Default);

            object value;
            switch (normalized)
            {
                case "pipelength":
                    value = values.pipeLength;
                    break;
                case "node":
                    value = values.node;
                    break;
                case "all":
                    value = values;
                    break;
                default:
                    normalized = "surface";
                    value = values.surface;
                    break;
            }

            return Serializer.Serialize(new CDBoxStudioAnnotationDefaultResult { section = normalized, value = value });
        }

        public static void SavePayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) throw new InvalidOperationException("标注设置保存数据为空。");

            CDBoxStudioAnnotationSettingsValues submitted;
            try
            {
                submitted = Serializer.Deserialize<CDBoxStudioAnnotationSettingsValues>(payload);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("标注设置数据格式无效。", ex);
            }

            if (submitted == null || submitted.surface == null || submitted.pipeLength == null || submitted.node == null)
            {
                throw new InvalidOperationException("标注设置数据不完整。");
            }

            SurfaceAreaAnnotationOptions loadedSurface = SurfaceAreaAnnotationSettingsStore.Load();
            PipeLengthAnnotationOptions loadedPipe = PipeLengthAnnotationSettingsStore.Load();
            NodeAnnotationOptions loadedNode = NodeAnnotationSettingsStore.Load();

            SurfaceAreaAnnotationOptions oldSurface = CloneSurface(loadedSurface);
            PipeLengthAnnotationOptions oldPipe = ClonePipe(loadedPipe);
            NodeAnnotationOptions oldNode = CloneNode(loadedNode);

            SurfaceAreaAnnotationOptions surface = MergeSurface(CloneSurface(loadedSurface), submitted.surface);
            PipeLengthAnnotationOptions pipe = MergePipe(ClonePipe(loadedPipe), submitted.pipeLength);
            NodeAnnotationOptions node = MergeNode(CloneNode(loadedNode), submitted.node);

            try
            {
                SurfaceAreaAnnotationSettingsStore.SaveStrict(surface);
                PipeLengthAnnotationSettingsStore.SaveStrict(pipe);
                NodeAnnotationSettingsStore.SaveStrict(node);
            }
            catch
            {
                TryRollback(oldSurface, oldPipe, oldNode);
                throw;
            }
        }

        public static void OpenLegacyWindow()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("未找到当前 AutoCAD 图纸。");

            using (var form = new AnnotationSettingsForm(doc))
            {
                form.ShowDialog(new AcadMainWindow());
            }
        }

        private static CDBoxStudioAnnotationSettingsValues BuildValues(
            SurfaceAreaAnnotationOptions surface,
            PipeLengthAnnotationOptions pipe,
            NodeAnnotationOptions node)
        {
            surface = surface ?? SurfaceAreaAnnotationOptions.Default;
            pipe = pipe ?? PipeLengthAnnotationOptions.Default;
            node = node ?? NodeAnnotationOptions.Default;

            return new CDBoxStudioAnnotationSettingsValues
            {
                surface = new CDBoxStudioSurfaceAnnotationSettings
                {
                    boundaryInterval = surface.BoundaryInterval,
                    calculationMode = "调用 CASS surfacearea 计算（固定）",
                    keepCassGeneratedObjects = !surface.DeleteCassGeneratedObjects,
                    textHeight = surface.TextHeight,
                    decimalPlaces = surface.DecimalPlaces,
                    annotationFontName = surface.AnnotationFontName ?? string.Empty,
                    annotationTemplate = surface.AnnotationTemplate ?? string.Empty
                },
                pipeLength = new CDBoxStudioPipeLengthAnnotationSettings
                {
                    textHeight = pipe.TextHeight,
                    decimalPlaces = pipe.DecimalPlaces,
                    annotationFontName = pipe.AnnotationFontName ?? string.Empty,
                    annotationTemplate = pipe.AnnotationTemplate ?? string.Empty,
                    enableSourceMetadataLayerLink = pipe.EnableSourceMetadataLayerLink,
                    layerLinkMode = pipe.LayerLinkMode.ToString(),
                    autoAnnotationLayerSuffix = pipe.AutoAnnotationLayerSuffix ?? string.Empty,
                    fallbackAnnotationLayerName = pipe.FallbackAnnotationLayerName ?? string.Empty,
                    writeAutoAnnotationLayerMetadata = pipe.WriteAutoAnnotationLayerMetadata,
                    annotationSplitTagText = pipe.AnnotationSplitTagText ?? string.Empty,
                    drawBottomAnnotation = pipe.DrawBottomAnnotation,
                    excavationWidth = pipe.ExcavationWidth,
                    excavationHeight = pipe.ExcavationHeight,
                    excavationDepth = pipe.ExcavationDepth,
                    bottomAnnotationTemplate = pipe.BottomAnnotationTemplate ?? string.Empty
                },
                node = new CDBoxStudioNodeAnnotationSettings
                {
                    textHeight = node.TextHeight,
                    decimalPlaces = node.DecimalPlaces,
                    annotationFontName = node.AnnotationFontName ?? string.Empty,
                    lineSpacingFactor = node.LineSpacingFactor,
                    nodeNoColorIndex = node.NodeNoColorIndex,
                    textColorIndex = node.TextColorIndex,
                    previewLeaderColorIndex = node.PreviewLeaderColorIndex
                }
            };
        }

        private static SurfaceAreaAnnotationOptions MergeSurface(SurfaceAreaAnnotationOptions current, CDBoxStudioSurfaceAnnotationSettings value)
        {
            current = current ?? SurfaceAreaAnnotationOptions.Default;
            current.BoundaryInterval = Clamp(value.boundaryInterval, 0.1, 1000.0, SurfaceAreaAnnotationOptions.Default.BoundaryInterval);
            current.TextHeight = Clamp(value.textHeight, 0.1, 1000.0, SurfaceAreaAnnotationOptions.Default.TextHeight);
            current.DecimalPlaces = Clamp(value.decimalPlaces, 0, 6);
            current.AnnotationFontName = NonEmpty(value.annotationFontName, current.AnnotationFontName, SurfaceAreaAnnotationOptions.Default.AnnotationFontName);
            current.AnnotationTemplate = NonEmpty(value.annotationTemplate, current.AnnotationTemplate, SurfaceAreaAnnotationOptions.Default.AnnotationTemplate);
            current.DeleteCassGeneratedObjects = !value.keepCassGeneratedObjects;
            current.CalculationMode = SurfaceAreaCalculationMode.CassCommand;
            return current;
        }

        private static PipeLengthAnnotationOptions MergePipe(PipeLengthAnnotationOptions current, CDBoxStudioPipeLengthAnnotationSettings value)
        {
            current = current ?? PipeLengthAnnotationOptions.Default;
            current.TextHeight = Clamp(value.textHeight, 0.1, 1000.0, PipeLengthAnnotationOptions.Default.TextHeight);
            current.DecimalPlaces = Clamp(value.decimalPlaces, 0, 6);
            current.AnnotationFontName = NonEmpty(value.annotationFontName, current.AnnotationFontName, PipeLengthAnnotationOptions.Default.AnnotationFontName);
            current.AnnotationTemplate = NonEmpty(value.annotationTemplate, current.AnnotationTemplate, PipeLengthAnnotationOptions.Default.AnnotationTemplate);
            current.EnableSourceMetadataLayerLink = value.enableSourceMetadataLayerLink;
            current.LayerLinkMode = ParseLayerLinkMode(value.layerLinkMode, current.LayerLinkMode);
            current.AutoAnnotationLayerSuffix = NonEmpty(value.autoAnnotationLayerSuffix, current.AutoAnnotationLayerSuffix, PipeLengthAnnotationOptions.Default.AutoAnnotationLayerSuffix);
            current.FallbackAnnotationLayerName = NonEmpty(value.fallbackAnnotationLayerName, current.FallbackAnnotationLayerName, PipeLengthAnnotationOptions.Default.FallbackAnnotationLayerName);
            current.WriteAutoAnnotationLayerMetadata = value.writeAutoAnnotationLayerMetadata;
            current.AnnotationSplitTagText = value.annotationSplitTagText ?? string.Empty;
            current.DrawBottomAnnotation = value.drawBottomAnnotation;
            current.ExcavationWidth = Clamp(value.excavationWidth, 0.0, 100000.0, 0.0);
            current.ExcavationHeight = Clamp(value.excavationHeight, 0.0, 100000.0, 0.0);
            current.ExcavationDepth = Clamp(value.excavationDepth, 0.0, 100000.0, 0.0);
            current.BottomAnnotationTemplate = NonEmpty(value.bottomAnnotationTemplate, current.BottomAnnotationTemplate, PipeLengthAnnotationOptions.Default.BottomAnnotationTemplate);
            return current;
        }

        private static NodeAnnotationOptions MergeNode(NodeAnnotationOptions current, CDBoxStudioNodeAnnotationSettings value)
        {
            current = current ?? NodeAnnotationOptions.Default;
            current.TextHeight = Clamp(value.textHeight, 0.1, 1000.0, NodeAnnotationOptions.Default.TextHeight);
            current.DecimalPlaces = Clamp(value.decimalPlaces, 0, 6);
            current.AnnotationFontName = NonEmpty(value.annotationFontName, current.AnnotationFontName, NodeAnnotationOptions.Default.AnnotationFontName);
            current.LineSpacingFactor = Clamp(value.lineSpacingFactor, 0.5, 5.0, NodeAnnotationOptions.Default.LineSpacingFactor);
            current.NodeNoColorIndex = NormalizeColor(value.nodeNoColorIndex, current.NodeNoColorIndex);
            current.TextColorIndex = NormalizeColor(value.textColorIndex, current.TextColorIndex);
            current.PreviewLeaderColorIndex = NormalizeColor(value.previewLeaderColorIndex, current.PreviewLeaderColorIndex);
            return current;
        }

        private static List<string> LoadTextStyleNames(params string[] configuredNames)
        {
            var names = new List<string>();
            string currentStyle = string.Empty;

            try
            {
                Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                Database db = doc == null ? null : doc.Database;
                if (db != null)
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        TextStyleTable table = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                        foreach (ObjectId id in table)
                        {
                            TextStyleTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as TextStyleTableRecord;
                            if (record == null || record.IsErased || string.IsNullOrWhiteSpace(record.Name)) continue;
                            AddIfMissing(names, record.Name);
                        }

                        if (!db.Textstyle.IsNull)
                        {
                            TextStyleTableRecord record = tr.GetObject(db.Textstyle, OpenMode.ForRead, false) as TextStyleTableRecord;
                            if (record != null) currentStyle = record.Name;
                        }
                        tr.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Warn("读取 CAD 文字样式失败，将使用兼容列表：" + ex.Message);
            }

            if (configuredNames != null)
            {
                foreach (string name in configuredNames) AddIfMissing(names, name);
            }
            AddIfMissing(names, "宋体");
            AddIfMissing(names, "STANDARD");
            names.Sort(StringComparer.CurrentCultureIgnoreCase);

            if (Contains(names, "宋体")) MoveToTop(names, "宋体");
            else if (!string.IsNullOrWhiteSpace(currentStyle)) MoveToTop(names, currentStyle);
            return names;
        }

        private static List<CDBoxStudioAnnotationSelectOption> BuildLayerLinkModes()
        {
            return new List<CDBoxStudioAnnotationSelectOption>
            {
                new CDBoxStudioAnnotationSelectOption { value = AnnotationLayerLinkMode.ParentGroup.ToString(), label = "按父属性：支管 → 支管注记" },
                new CDBoxStudioAnnotationSelectOption { value = AnnotationLayerLinkMode.ParentClass.ToString(), label = "按分类：110PVC管 → 110PVC管注记" },
                new CDBoxStudioAnnotationSelectOption { value = AnnotationLayerLinkMode.FirstMatchedTag.ToString(), label = "按标签：明管 → 明管注记" },
                new CDBoxStudioAnnotationSelectOption { value = AnnotationLayerLinkMode.ParentGroupAndTag.ToString(), label = "按父属性+标签：支管-明管注记" }
            };
        }

        private static List<CDBoxStudioAnnotationColorOption> BuildColorOptions()
        {
            var result = new List<CDBoxStudioAnnotationColorOption>(255);
            for (short i = 1; i <= 255; i++)
            {
                result.Add(new CDBoxStudioAnnotationColorOption
                {
                    index = i,
                    name = GetColorName(i),
                    cssColor = GetColorCss(i)
                });
            }
            return result;
        }

        private static string GetColorName(short index)
        {
            switch (index)
            {
                case 1: return "红色";
                case 2: return "黄色";
                case 3: return "绿色";
                case 4: return "青色";
                case 5: return "蓝色";
                case 6: return "洋红";
                case 7: return "白色/黑色";
                case 8: return "深灰色";
                case 9: return "浅灰色";
                case 250: return "灰色 250";
                case 251: return "灰色 251";
                case 252: return "灰色 252";
                case 253: return "灰色 253";
                case 254: return "灰色 254";
                case 255: return "灰色 255";
                default: return "ACI " + index;
            }
        }

        private static string GetColorCss(short index)
        {
            switch (index)
            {
                case 1: return "#ef4444";
                case 2: return "#facc15";
                case 3: return "#22c55e";
                case 4: return "#06b6d4";
                case 5: return "#3b82f6";
                case 6: return "#d946ef";
                case 7: return "#e5e7eb";
                case 8: return "#64748b";
                case 9: return "#cbd5e1";
            }

            if (index >= 250)
            {
                int gray = 45 + (index - 250) * 34;
                if (gray > 230) gray = 230;
                return "rgb(" + gray + "," + gray + "," + gray + ")";
            }

            int offset = index - 10;
            int hueBand = Math.Max(0, offset / 10);
            int shade = Math.Max(0, offset % 10);
            int hue = (hueBand * 15) % 360;
            int saturation = (shade % 2 == 0) ? 82 : 55;
            int[] lightness = { 50, 68, 42, 58, 34, 52, 26, 44, 20, 36 };
            return "hsl(" + hue + "," + saturation + "%," + lightness[shade] + "%)";
        }

        private static AnnotationLayerLinkMode ParseLayerLinkMode(string text, AnnotationLayerLinkMode fallback)
        {
            AnnotationLayerLinkMode mode;
            return Enum.TryParse(text ?? string.Empty, true, out mode) ? mode : fallback;
        }

        private static SurfaceAreaAnnotationOptions CloneSurface(SurfaceAreaAnnotationOptions value)
        {
            value = value ?? SurfaceAreaAnnotationOptions.Default;
            return new SurfaceAreaAnnotationOptions
            {
                BoundaryInterval = value.BoundaryInterval,
                TextHeight = value.TextHeight,
                DecimalPlaces = value.DecimalPlaces,
                AnnotationTemplate = value.AnnotationTemplate,
                CalculationMode = value.CalculationMode,
                CassSurfaceLogPath = value.CassSurfaceLogPath,
                DeleteCassGeneratedObjects = value.DeleteCassGeneratedObjects,
                AnnotationFontName = value.AnnotationFontName,
                LayerMode = value.LayerMode,
                SelectedLayerName = value.SelectedLayerName,
                AnnotationLayerName = value.AnnotationLayerName,
                UseBoundaryLayerForAnnotation = value.UseBoundaryLayerForAnnotation,
                DrawLeader = value.DrawLeader
            };
        }

        private static PipeLengthAnnotationOptions ClonePipe(PipeLengthAnnotationOptions value)
        {
            value = value ?? PipeLengthAnnotationOptions.Default;
            return new PipeLengthAnnotationOptions
            {
                TextHeight = value.TextHeight,
                DecimalPlaces = value.DecimalPlaces,
                AnnotationTemplate = value.AnnotationTemplate,
                AnnotationFontName = value.AnnotationFontName,
                LayerMode = value.LayerMode,
                SelectedLayerName = value.SelectedLayerName,
                AnnotationLayerName = value.AnnotationLayerName,
                DrawLeader = value.DrawLeader,
                EnableSourceMetadataLayerLink = value.EnableSourceMetadataLayerLink,
                LayerLinkMode = value.LayerLinkMode,
                AutoAnnotationLayerSuffix = value.AutoAnnotationLayerSuffix,
                FallbackAnnotationLayerName = value.FallbackAnnotationLayerName,
                WriteAutoAnnotationLayerMetadata = value.WriteAutoAnnotationLayerMetadata,
                AnnotationSplitTagText = value.AnnotationSplitTagText,
                DrawBottomAnnotation = value.DrawBottomAnnotation,
                BottomAnnotationTemplate = value.BottomAnnotationTemplate,
                ExcavationWidth = value.ExcavationWidth,
                ExcavationHeight = value.ExcavationHeight,
                ExcavationDepth = value.ExcavationDepth
            };
        }

        private static NodeAnnotationOptions CloneNode(NodeAnnotationOptions value)
        {
            value = value ?? NodeAnnotationOptions.Default;
            return new NodeAnnotationOptions
            {
                TextHeight = value.TextHeight,
                DecimalPlaces = value.DecimalPlaces,
                AnnotationFontName = value.AnnotationFontName,
                AnnotationLayerName = value.AnnotationLayerName,
                LineSpacingFactor = value.LineSpacingFactor,
                NodeNoColorIndex = value.NodeNoColorIndex,
                TextColorIndex = value.TextColorIndex,
                PreviewLeaderColorIndex = value.PreviewLeaderColorIndex
            };
        }

        private static void TryRollback(SurfaceAreaAnnotationOptions surface, PipeLengthAnnotationOptions pipe, NodeAnnotationOptions node)
        {
            try { SurfaceAreaAnnotationSettingsStore.SaveStrict(surface); } catch { }
            try { PipeLengthAnnotationSettingsStore.SaveStrict(pipe); } catch { }
            try { NodeAnnotationSettingsStore.SaveStrict(node); } catch { }
        }

        private static string NormalizeSection(string section)
        {
            string value = (section ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "pipe" || value == "pipe-length" || value == "pipelength") return "pipelength";
            if (value == "node") return "node";
            if (value == "all") return "all";
            return "surface";
        }

        private static double Clamp(double value, double min, double max, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return fallback;
            return Math.Max(min, Math.Min(max, value));
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static short NormalizeColor(short value, short fallback)
        {
            return value >= 1 && value <= 255 ? value : fallback;
        }

        private static string NonEmpty(string value, string current, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            if (!string.IsNullOrWhiteSpace(current)) return current.Trim();
            return fallback ?? string.Empty;
        }

        private static void AddIfMissing(List<string> names, string value)
        {
            if (names == null || string.IsNullOrWhiteSpace(value) || Contains(names, value)) return;
            names.Add(value.Trim());
        }

        private static bool Contains(List<string> names, string value)
        {
            return names != null && names.Any(x => string.Equals(x, value, StringComparison.CurrentCultureIgnoreCase));
        }

        private static void MoveToTop(List<string> names, string value)
        {
            int index = names.FindIndex(x => string.Equals(x, value, StringComparison.CurrentCultureIgnoreCase));
            if (index <= 0) return;
            string item = names[index];
            names.RemoveAt(index);
            names.Insert(0, item);
        }
    }
}
