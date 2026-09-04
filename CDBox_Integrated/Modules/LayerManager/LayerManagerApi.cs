using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using TCPipeAutoDraw.Core.Colors;
using TCPipeAutoDraw.Modules.LayerManager;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Modules.LayerManager
{
    internal static class LayerManagerApi
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private static readonly string[] PriorityParents =
        {
            "主管", "支管", "井", "结构层", "构筑物", "注记", "测点", "辅助"
        };

        public static string BuildEnvelopeJson(bool countObjects)
        {
            return Serialize(BuildEnvelope(countObjects));
        }

        public static CDBoxStudioLayerManagerEnvelope BuildEnvelope(bool countObjects)
        {
            Document doc = GetActiveDocument();
            List<LayerInfo> source = LayerManagerService.GetLayers(doc, countObjects);
            var envelope = new CDBoxStudioLayerManagerEnvelope();

            foreach (LayerInfo item in source)
            {
                envelope.layers.Add(new CDBoxStudioLayerRow
                {
                    name = item.Name ?? string.Empty,
                    parent = (item.ParentGroup ?? string.Empty).Trim(),
                    category = (item.ParentClass ?? string.Empty).Trim(),
                    tags = LayerMetadata.ParseTags(item.TagText),
                    isCurrent = item.IsCurrent,
                    isLocked = item.IsLocked,
                    isFrozen = item.IsFrozen,
                    isOff = item.IsOff,
                    isDependent = item.IsDependent,
                    isPlottable = item.IsPlottable,
                    objectCount = countObjects ? (int?)item.ObjectCount : null,
                    colorIndex = item.ColorIndex,
                    colorName = (item.Color ?? CDBoxColor.FromIndex(item.ColorIndex)).ToString(),
                    colorHex = (item.Color ?? CDBoxColor.FromIndex(item.ColorIndex)).Hex,
                    colorType = (item.Color ?? CDBoxColor.FromIndex(item.ColorIndex)).Type.ToString(),
                    colorRgb = (item.Color ?? CDBoxColor.FromIndex(item.ColorIndex)).RgbText,
                    linetype = item.Linetype ?? string.Empty,
                    normalizedName = item.NormalizedName ?? string.Empty,
                    suggestedName = item.SuggestedName ?? string.Empty,
                    recognitionStatus = item.RecognitionStatus ?? string.Empty,
                    confidencePercent = (int)Math.Round(Math.Max(0d, Math.Min(1d, item.RecognitionConfidence)) * 100d),
                    recognitionSource = item.RecognitionSource ?? string.Empty,
                    recognitionExplanation = item.RecognitionExplanation ?? string.Empty,
                    recognitionConflicts = item.RecognitionConflicts ?? string.Empty,
                    recognitionMissingFields = item.RecognitionMissingFields ?? string.Empty,
                    recognizedParent = item.RecognitionParentGroup ?? string.Empty,
                    recognizedCategory = item.RecognitionParentClass ?? string.Empty,
                    objectType = item.ObjectType ?? string.Empty,
                    specification = item.Specification ?? string.Empty,
                    material = item.Material ?? string.Empty,
                    constructionType = item.ConstructionType ?? string.Empty,
                    nodeType = item.NodeType ?? string.Empty,
                    structureType = item.StructureType ?? string.Empty,
                    purpose = item.Purpose ?? string.Empty
                });
            }

            var parentSet = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (string value in PriorityParents) parentSet.Add(value);
            foreach (CDBoxStudioLayerRow row in envelope.layers)
            {
                if (!string.IsNullOrWhiteSpace(row.parent)) parentSet.Add(row.parent.Trim());
                if (!string.IsNullOrWhiteSpace(row.recognizedParent)) parentSet.Add(row.recognizedParent.Trim());
            }
            envelope.parentOptions = PriorityParents
                .Concat(parentSet.Where(x => !PriorityParents.Any(p => string.Equals(p, x, StringComparison.CurrentCultureIgnoreCase)))
                    .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
                .ToList();

            envelope.categoriesByParent = envelope.layers
                .Where(x => !string.IsNullOrWhiteSpace(x.parent) && !string.IsNullOrWhiteSpace(x.category))
                .GroupBy(x => x.parent.Trim(), StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.category.Trim()).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                    StringComparer.CurrentCultureIgnoreCase);

            envelope.tagOptions = envelope.layers
                .SelectMany(x => x.tags ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            envelope.presets = LayerPresetStore.LoadAll()
                .Select(ToStudioPreset)
                .ToList();

            return envelope;
        }

        public static string BuildObjectCountsJson()
        {
            CDBoxStudioLayerManagerEnvelope envelope = BuildEnvelope(true);
            var counts = envelope.layers.ToDictionary(x => x.name, x => x.objectCount ?? 0, StringComparer.CurrentCultureIgnoreCase);
            return Serialize(counts);
        }

        public static CDBoxStudioLayerSaveResult SaveMetadata(string payload)
        {
            CDBoxStudioLayerMetadataSaveRequest request = Deserialize<CDBoxStudioLayerMetadataSaveRequest>(payload) ?? new CDBoxStudioLayerMetadataSaveRequest();
            var result = new CDBoxStudioLayerSaveResult();
            List<CDBoxStudioLayerMetadataChange> changes = (request.changes ?? new List<CDBoxStudioLayerMetadataChange>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.name))
                .GroupBy(x => x.name.Trim(), StringComparer.CurrentCultureIgnoreCase)
                .Select(g => g.Last())
                .ToList();

            if (changes.Count == 0)
            {
                result.success = true;
                return result;
            }

            Document doc = GetActiveDocument();
            Database db = doc.Database;
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (CDBoxStudioLayerMetadataChange change in changes)
                {
                    string layerName = (change.name ?? string.Empty).Trim();
                    try
                    {
                        if (!lt.Has(layerName))
                        {
                            AddFailure(result, layerName, "图层不存在");
                            continue;
                        }

                        LayerTableRecord layer = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead);
                        if (layer.IsDependent)
                        {
                            AddFailure(result, layerName, "外部参照依赖图层不可写入");
                            continue;
                        }

                        LayerMetadata metadata = LayerManagerService.GetLayerMetadata(db, tr, layerName).Clone();
                        metadata.ParentGroup = (change.parent ?? string.Empty).Trim();
                        metadata.ParentClass = (change.category ?? string.Empty).Trim();
                        metadata.Tags = NormalizeTags(change.tags);
                        metadata.RecognitionSource = "人工确认";
                        metadata.RecognitionConfidence = 1d;

                        LayerManagerService.SetLayerMetadata(db, tr, layerName, metadata);
                        LayerMetadata saved = LayerManagerService.GetLayerMetadata(db, tr, layerName);
                        if (!MetadataEquals(metadata, saved))
                        {
                            AddFailure(result, layerName, "写入后校验失败");
                            continue;
                        }

                        result.successCount++;
                        result.savedLayers.Add(layerName);
                    }
                    catch (Exception ex)
                    {
                        AddFailure(result, layerName, ex.Message);
                    }
                }
                tr.Commit();
            }

            result.failCount = result.failures.Count;
            result.success = result.failCount == 0;
            return result;
        }

        public static CDBoxStudioLayerActionResult RunAction(string payload)
        {
            CDBoxStudioLayerActionRequest request = Deserialize<CDBoxStudioLayerActionRequest>(payload) ?? new CDBoxStudioLayerActionRequest();
            string action = (request.action ?? string.Empty).Trim().ToLowerInvariant();
            List<string> layers = NormalizeLayerNames(request.layers);
            if (layers.Count == 0) throw new InvalidOperationException("未选择有效图层。");

            Document doc = GetActiveDocument();
            LayerOperationResult operation;
            switch (action)
            {
                case "selectobjects":
                    operation = LayerManagerService.SelectObjectsByLayers(doc, layers);
                    break;
                case "deleteobjects":
                    operation = LayerManagerService.DeleteObjectsByLayers(doc, layers, request.forceUnlock);
                    break;
                case "lock":
                    operation = LayerManagerService.SetLayerState(doc, layers, LayerStateAction.Lock);
                    break;
                case "unlock":
                    operation = LayerManagerService.SetLayerState(doc, layers, LayerStateAction.Unlock);
                    break;
                case "hide":
                    operation = LayerManagerService.SetLayerState(doc, layers, LayerStateAction.TurnOff);
                    break;
                case "showandthaw":
                    operation = Merge(
                        LayerManagerService.SetLayerState(doc, layers, LayerStateAction.TurnOn),
                        LayerManagerService.SetLayerState(doc, layers, LayerStateAction.Thaw),
                        "已显示并解冻所选图层。");
                    break;
                case "freeze":
                    operation = LayerManagerService.SetLayerState(doc, layers, LayerStateAction.Freeze);
                    break;
                case "thaw":
                    operation = LayerManagerService.SetLayerState(doc, layers, LayerStateAction.Thaw);
                    break;
                default:
                    throw new InvalidOperationException("不支持的图层操作：" + action);
            }

            return FromOperation(action, layers, operation);
        }

        public static CDBoxStudioLayerActionResult SetCurrentLayer(string layerName)
        {
            string name = (layerName ?? string.Empty).Trim();
            if (name.Length == 0) throw new InvalidOperationException("未指定图层名。");
            LayerOperationResult operation = LayerManagerService.SetCurrentLayer(GetActiveDocument(), name);
            return FromOperation("setCurrent", new List<string> { name }, operation);
        }

        public static CDBoxColor GetLayerColor(string layerName)
        {
            return LayerManagerService.GetLayerColor(GetActiveDocument(), (layerName ?? string.Empty).Trim());
        }

        public static CDBoxStudioLayerActionResult SetLayerColor(string layerName, CDBoxColor color)
        {
            string name = (layerName ?? string.Empty).Trim();
            LayerOperationResult operation = LayerManagerService.SetLayerColor(GetActiveDocument(), name, color);
            return FromOperation("setLayerColor", new List<string> { name }, operation);
        }

        public static CDBoxStudioLayerActionResult AutoRecognize(string payload)
        {
            CDBoxStudioLayerRecognitionRequest request = Deserialize<CDBoxStudioLayerRecognitionRequest>(payload) ?? new CDBoxStudioLayerRecognitionRequest();
            List<string> layers = NormalizeLayerNames(request.layers);
            if (layers.Count == 0) throw new InvalidOperationException("没有可识别的图层。");
            LayerOperationResult operation = LayerManagerService.AutoInferLayerMetadata(GetActiveDocument(), layers, request.overwriteExisting);
            return FromOperation("autoRecognize", layers, operation);
        }

        public static CDBoxStudioLayerActionResult CreateDefaultPipeLayers(string payload)
        {
            CDBoxStudioLayerPresetRequest request = Deserialize<CDBoxStudioLayerPresetRequest>(payload)
                ?? new CDBoxStudioLayerPresetRequest();
            LayerPresetDefinition preset = LayerPresetStore.Find(request.id);
            if (preset == null) throw new InvalidOperationException("所选图层预设不存在或已被删除。");
            LayerOperationResult operation = LayerManagerService.CreatePipeLayers(
                GetActiveDocument(), preset.Layers);
            operation.Message = "已应用预设“" + preset.Name + "”。\n" + operation.Message;
            return FromOperation("createDefaultPipeLayers", preset.Layers, operation);
        }

        public static CDBoxStudioLayerActionResult SaveLayerPreset(string payload)
        {
            CDBoxStudioLayerPresetRequest request = Deserialize<CDBoxStudioLayerPresetRequest>(payload)
                ?? new CDBoxStudioLayerPresetRequest();
            LayerPresetDefinition preset = LayerPresetStore.SaveCustom(
                request.name, NormalizeLayerNames(request.layers));
            return new CDBoxStudioLayerActionResult
            {
                success = true,
                action = "saveLayerPreset",
                successCount = 1,
                layers = new List<string>(preset.Layers),
                message = "图层预设“" + preset.Name + "”已保存，共 "
                    + preset.Layers.Count + " 个图层。"
            };
        }

        public static CDBoxStudioLayerActionResult DeleteLayerPreset(string payload)
        {
            CDBoxStudioLayerPresetRequest request = Deserialize<CDBoxStudioLayerPresetRequest>(payload)
                ?? new CDBoxStudioLayerPresetRequest();
            bool deleted = LayerPresetStore.DeleteCustom(request.id);
            return new CDBoxStudioLayerActionResult
            {
                success = deleted,
                action = "deleteLayerPreset",
                successCount = deleted ? 1 : 0,
                skipCount = deleted ? 0 : 1,
                message = deleted ? "自定义图层预设已删除。" : "未找到可删除的自定义预设。"
            };
        }

        public static string Serialize(object value)
        {
            return Serializer.Serialize(value);
        }

        private static T Deserialize<T>(string payload) where T : class
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;
            return Serializer.Deserialize<T>(payload);
        }

        private static Document GetActiveDocument()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("未找到当前图纸。");
            return doc;
        }

        private static List<string> NormalizeLayerNames(IEnumerable<string> names)
        {
            return (names ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static List<string> NormalizeTags(IEnumerable<string> tags)
        {
            string text = string.Join("、", tags ?? Enumerable.Empty<string>());
            return LayerMetadata.ParseTags(text);
        }

        private static CDBoxStudioLayerPreset ToStudioPreset(LayerPresetDefinition preset)
        {
            preset = preset ?? new LayerPresetDefinition();
            return new CDBoxStudioLayerPreset
            {
                id = preset.Id ?? string.Empty,
                name = preset.Name ?? string.Empty,
                description = preset.Description ?? string.Empty,
                isBuiltIn = preset.IsBuiltIn,
                layers = new List<string>(preset.Layers ?? new List<string>())
            };
        }

        private static bool MetadataEquals(LayerMetadata a, LayerMetadata b)
        {
            a = a ?? new LayerMetadata();
            b = b ?? new LayerMetadata();
            if (!string.Equals((a.ParentGroup ?? string.Empty).Trim(), (b.ParentGroup ?? string.Empty).Trim(), StringComparison.CurrentCulture)) return false;
            if (!string.Equals((a.ParentClass ?? string.Empty).Trim(), (b.ParentClass ?? string.Empty).Trim(), StringComparison.CurrentCulture)) return false;
            List<string> at = NormalizeTags(a.Tags);
            List<string> bt = NormalizeTags(b.Tags);
            return at.SequenceEqual(bt, StringComparer.CurrentCultureIgnoreCase);
        }

        private static void AddFailure(CDBoxStudioLayerSaveResult result, string layerName, string reason)
        {
            result.failures.Add(new CDBoxStudioLayerFailure { layerName = layerName ?? string.Empty, reason = string.IsNullOrWhiteSpace(reason) ? "未知错误" : reason });
        }

        private static LayerOperationResult Merge(LayerOperationResult a, LayerOperationResult b, string message)
        {
            a = a ?? new LayerOperationResult();
            b = b ?? new LayerOperationResult();
            return new LayerOperationResult
            {
                SuccessCount = Math.Max(a.SuccessCount, b.SuccessCount),
                FailCount = a.FailCount + b.FailCount,
                SkipCount = a.SkipCount + b.SkipCount,
                ObjectCount = a.ObjectCount + b.ObjectCount,
                Message = message
            };
        }

        private static CDBoxStudioLayerActionResult FromOperation(string action, List<string> layers, LayerOperationResult operation)
        {
            operation = operation ?? new LayerOperationResult();
            return new CDBoxStudioLayerActionResult
            {
                success = operation.FailCount == 0,
                action = action ?? string.Empty,
                message = operation.Message ?? string.Empty,
                successCount = operation.SuccessCount,
                failCount = operation.FailCount,
                skipCount = operation.SkipCount,
                objectCount = operation.ObjectCount,
                layers = layers ?? new List<string>()
            };
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
                case 8: return "灰色";
                case 9: return "浅灰";
                default: return "ACI " + index;
            }
        }

        private static string GetColorHex(short index)
        {
            switch (index)
            {
                case 1: return "#ef4444";
                case 2: return "#eab308";
                case 3: return "#22c55e";
                case 4: return "#06b6d4";
                case 5: return "#3b82f6";
                case 6: return "#d946ef";
                case 7: return "#64748b";
                case 8: return "#6b7280";
                case 9: return "#9ca3af";
                default: return "#94a3b8";
            }
        }
    }
}
