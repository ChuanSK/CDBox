using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace TCPipeAutoDraw.Modules.LayerManager
{
    public static class LayerManagerService
    {
        private const string LayerMetadataXrecordName = "CDBoxLayerMetadata";
        private const string LayerMetadataIndexDictionaryName = "CDBoxLayerMetadataIndex";
        public const string MatchModeExact = "精确";
        public const string MatchModeContains = "包含";
        public const string MatchModeWildcard = "通配符";
        public const string MatchModeRegex = "正则";


        public static readonly string[] DefaultPipeLayers = new[]
        {
            "75PVC管(明管)",
            "75PVC管(砼恢复)",
            "110PVC管(雨水管)",
            "110PVC管(砼恢复)",
            "110PVC管(明管)",
            "110PVC管(并埋)",
            "110PVC管(原土回填)",
            "200波纹管(砼恢复)",
            "300波纹管(砼恢复)",
            "500铸铁井盖",
            "700铸铁井盖",
            "315井(混凝土)",
            "315井(铸铁)",
            "主管注记",
            "支管注记"
        };

        public static List<LayerInfo> GetLayers(Document doc, bool countObjects)
        {
            if (doc == null) throw new ArgumentNullException("doc");

            var result = new List<LayerInfo>();
            Database db = doc.Database;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Dictionary<string, int> counts = countObjects
                    ? CountObjectsByLayer(db, tr)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                ObjectId currentLayerId = db.Clayer;

                foreach (ObjectId layerId in layerTable)
                {
                    LayerTableRecord layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                    string lineTypeName = GetLineTypeName(layer, tr);
                    int count;
                    counts.TryGetValue(layer.Name, out count);

                    LayerMetadata metadata = ReadLayerMetadata(layer, tr);
                    var item = new LayerInfo
                    {
                        Selected = false,
                        Name = layer.Name,
                        IsCurrent = layerId == currentLayerId,
                        IsOff = layer.IsOff,
                        IsFrozen = layer.IsFrozen,
                        IsLocked = layer.IsLocked,
                        IsDependent = layer.IsDependent,
                        IsPlottable = layer.IsPlottable,
                        ColorIndex = layer.Color == null ? (short)7 : layer.Color.ColorIndex,
                        Linetype = lineTypeName,
                        ObjectCount = count,
                        ParentGroup = metadata.ParentGroup,
                        ParentClass = metadata.ParentClass,
                        TagText = metadata.TagText
                    };
                    item.StatusText = BuildStatusText(item);
                    result.Add(item);
                }

                tr.Commit();
            }

            result.Sort(delegate (LayerInfo a, LayerInfo b)
            {
                if (a.IsCurrent && !b.IsCurrent) return -1;
                if (!a.IsCurrent && b.IsCurrent) return 1;
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }

        public static LayerOperationResult SelectObjectsByLayers(Document doc, IEnumerable<string> layerNames)
        {
            var output = new LayerOperationResult();
            List<string> layers = NormalizeLayerNames(layerNames);
            if (layers.Count == 0)
            {
                output.Message = "未选择有效图层。";
                return output;
            }

            PromptSelectionResult psr = doc.Editor.SelectAll(BuildLayerSelectionFilter(layers));
            if (psr.Status != PromptStatus.OK || psr.Value == null)
            {
                doc.Editor.SetImpliedSelection(new ObjectId[0]);
                output.Message = "所选图层中没有可选对象。";
                return output;
            }

            ObjectId[] ids = psr.Value.GetObjectIds();
            doc.Editor.SetImpliedSelection(ids);
            output.ObjectCount = ids.Length;
            output.SuccessCount = layers.Count;
            output.Message = "已选中 " + ids.Length + " 个对象，涉及 " + layers.Count + " 个图层。";
            return output;
        }

        public static LayerOperationResult DeleteObjectsByLayers(Document doc, IEnumerable<string> layerNames, bool forceUnlock)
        {
            var output = new LayerOperationResult();
            List<string> layers = NormalizeLayerNames(layerNames);
            if (layers.Count == 0)
            {
                output.Message = "未选择有效图层。";
                return output;
            }

            Database db = doc.Database;
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                var originalLock = new Dictionary<ObjectId, bool>();
                var targetLayerIds = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);

                foreach (string layerName in layers)
                {
                    if (!lt.Has(layerName))
                    {
                        output.FailCount++;
                        continue;
                    }

                    ObjectId layerId = lt[layerName];
                    targetLayerIds[layerName] = layerId;
                    LayerTableRecord layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                    originalLock[layerId] = layer.IsLocked;

                    if (forceUnlock && layer.IsLocked && !layer.IsDependent)
                    {
                        layer.UpgradeOpen();
                        layer.IsLocked = false;
                    }
                }

                List<ObjectId> ids = GetEntityIdsByLayers(db, tr, layers);
                foreach (ObjectId id in ids)
                {
                    try
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                        if (ent == null)
                        {
                            output.SkipCount++;
                            continue;
                        }

                        ObjectId layerId = ResolveLayerId(lt, ent.Layer);
                        LayerTableRecord layer = ObjectId.Null == layerId
                            ? null
                            : tr.GetObject(layerId, OpenMode.ForRead) as LayerTableRecord;

                        if (layer != null && layer.IsLocked && !forceUnlock)
                        {
                            output.SkipCount++;
                            continue;
                        }

                        ent.UpgradeOpen();
                        ent.Erase();
                        output.ObjectCount++;
                    }
                    catch
                    {
                        output.FailCount++;
                    }
                }

                foreach (KeyValuePair<ObjectId, bool> pair in originalLock)
                {
                    try
                    {
                        LayerTableRecord layer = (LayerTableRecord)tr.GetObject(pair.Key, OpenMode.ForWrite, false);
                        if (layer != null && !layer.IsDependent)
                        {
                            layer.IsLocked = pair.Value;
                        }
                    }
                    catch
                    {
                        // 锁定状态恢复失败不影响对象删除结果。
                    }
                }

                tr.Commit();
            }

            doc.Editor.SetImpliedSelection(new ObjectId[0]);
            output.SuccessCount = output.ObjectCount;
            output.Message = "已删除 " + output.ObjectCount + " 个对象。";
            if (output.SkipCount > 0) output.Message += " 跳过 " + output.SkipCount + " 个对象，可能位于锁定图层。";
            if (output.FailCount > 0) output.Message += " 失败 " + output.FailCount + " 个。";
            return output;
        }

        public static LayerOperationResult SetLayerState(Document doc, IEnumerable<string> layerNames, LayerStateAction action)
        {
            var output = new LayerOperationResult();
            List<string> layers = NormalizeLayerNames(layerNames);
            if (layers.Count == 0)
            {
                output.Message = "未选择有效图层。";
                return output;
            }

            Database db = doc.Database;
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                string currentLayerName = GetCurrentLayerName(db, tr);

                foreach (string layerName in layers)
                {
                    try
                    {
                        if (!lt.Has(layerName))
                        {
                            output.FailCount++;
                            continue;
                        }

                        ObjectId id = lt[layerName];
                        LayerTableRecord layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                        if (layer.IsDependent)
                        {
                            output.SkipCount++;
                            continue;
                        }

                        bool isCurrent = string.Equals(layer.Name, currentLayerName, StringComparison.OrdinalIgnoreCase);
                        if ((action == LayerStateAction.TurnOff || action == LayerStateAction.Freeze) && isCurrent)
                        {
                            output.SkipCount++;
                            continue;
                        }

                        switch (action)
                        {
                            case LayerStateAction.Lock:
                                layer.IsLocked = true;
                                break;
                            case LayerStateAction.Unlock:
                                layer.IsLocked = false;
                                break;
                            case LayerStateAction.TurnOn:
                                layer.IsOff = false;
                                break;
                            case LayerStateAction.TurnOff:
                                layer.IsOff = true;
                                break;
                            case LayerStateAction.Freeze:
                                layer.IsFrozen = true;
                                break;
                            case LayerStateAction.Thaw:
                                layer.IsFrozen = false;
                                break;
                        }
                        output.SuccessCount++;
                    }
                    catch
                    {
                        output.FailCount++;
                    }
                }

                tr.Commit();
            }

            output.Message = "处理完成：成功 " + output.SuccessCount + " 个，跳过 " + output.SkipCount + " 个，失败 " + output.FailCount + " 个。";
            return output;
        }

        public static LayerOperationResult SetOnlySelectedLayersVisible(Document doc, IEnumerable<string> layerNames)
        {
            var output = new LayerOperationResult();
            HashSet<string> keep = new HashSet<string>(NormalizeLayerNames(layerNames), StringComparer.OrdinalIgnoreCase);
            if (keep.Count == 0)
            {
                output.Message = "未选择有效图层。";
                return output;
            }

            Database db = doc.Database;
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                string currentLayerName = GetCurrentLayerName(db, tr);

                foreach (ObjectId id in lt)
                {
                    try
                    {
                        LayerTableRecord layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                        if (layer.IsDependent)
                        {
                            output.SkipCount++;
                            continue;
                        }

                        bool shouldKeep = keep.Contains(layer.Name);
                        bool isCurrent = string.Equals(layer.Name, currentLayerName, StringComparison.OrdinalIgnoreCase);

                        if (shouldKeep)
                        {
                            layer.IsOff = false;
                            layer.IsFrozen = false;
                            output.SuccessCount++;
                        }
                        else if (!isCurrent)
                        {
                            layer.IsOff = true;
                            output.SuccessCount++;
                        }
                        else
                        {
                            output.SkipCount++;
                        }
                    }
                    catch
                    {
                        output.FailCount++;
                    }
                }

                tr.Commit();
            }

            output.Message = "已仅显示所选图层；当前图层与外部参照图层会自动跳过。";
            return output;
        }

        public static LayerOperationResult ShowAndThawAllLayers(Document doc)
        {
            var output = new LayerOperationResult();
            Database db = doc.Database;
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in lt)
                {
                    try
                    {
                        LayerTableRecord layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                        if (layer.IsDependent)
                        {
                            output.SkipCount++;
                            continue;
                        }
                        layer.IsOff = false;
                        layer.IsFrozen = false;
                        output.SuccessCount++;
                    }
                    catch
                    {
                        output.FailCount++;
                    }
                }
                tr.Commit();
            }
            output.Message = "已打开并解冻全部可处理图层。";
            return output;
        }

        public static LayerOperationResult SetCurrentLayer(Document doc, string layerName)
        {
            var output = new LayerOperationResult();
            if (string.IsNullOrWhiteSpace(layerName))
            {
                output.Message = "未选择图层。";
                return output;
            }

            Database db = doc.Database;
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName))
                {
                    output.Message = "图层不存在：" + layerName;
                    return output;
                }

                ObjectId id = lt[layerName];
                LayerTableRecord layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                if (layer.IsDependent)
                {
                    output.Message = "外部参照依赖图层不能设为当前图层。";
                    return output;
                }

                layer.IsOff = false;
                layer.IsFrozen = false;
                db.Clayer = id;
                output.SuccessCount = 1;
                output.Message = "当前图层已设为：" + layerName;
                tr.Commit();
            }
            return output;
        }

        public static LayerOperationResult CreateLayers(Document doc, IEnumerable<string> layerNames, short colorIndex)
        {
            var output = new LayerOperationResult();
            List<string> names = NormalizeLayerNames(layerNames);
            if (names.Count == 0)
            {
                output.Message = "没有可创建的图层名。";
                return output;
            }

            Database db = doc.Database;
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (string name in names)
                {
                    try
                    {
                        if (lt.Has(name))
                        {
                            output.SkipCount++;
                            continue;
                        }

                        lt.UpgradeOpen();
                        var layer = new LayerTableRecord();
                        layer.Name = name;
                        layer.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex <= 0 ? (short)7 : colorIndex);
                        ObjectId id = lt.Add(layer);
                        tr.AddNewlyCreatedDBObject(layer, true);
                        output.SuccessCount++;
                    }
                    catch
                    {
                        output.FailCount++;
                    }
                }
                tr.Commit();
            }

            output.Message = "创建完成：新增 " + output.SuccessCount + " 个，已存在 " + output.SkipCount + " 个，失败 " + output.FailCount + " 个。";
            return output;
        }

        public static LayerOperationResult CreateDefaultPipeLayers(Document doc)
        {
            LayerOperationResult result = CreateLayers(doc, DefaultPipeLayers, 7);
            try
            {
                LayerOperationResult metadataResult = AutoInferLayerMetadata(doc, DefaultPipeLayers, true);
                result.Message += "\n默认图层属性：" + metadataResult.Message;
            }
            catch
            {
                result.Message += "\n默认图层已创建，但自动写入父属性/标签失败。";
            }
            return result;
        }


        public static LayerOperationResult SetLayerMetadata(Document doc, IEnumerable<string> layerNames, LayerMetadata metadata)
        {
            var output = new LayerOperationResult();
            List<string> layers = NormalizeLayerNames(layerNames);
            if (layers.Count == 0)
            {
                output.Message = "未选择有效图层。";
                return output;
            }

            metadata = metadata == null ? new LayerMetadata() : metadata.Clone();
            metadata.Tags = NormalizeTags(metadata.Tags);

            Database db = doc.Database;
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (string layerName in layers)
                {
                    try
                    {
                        if (!lt.Has(layerName))
                        {
                            output.FailCount++;
                            continue;
                        }

                        LayerTableRecord layer = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead);
                        if (layer.IsDependent)
                        {
                            output.SkipCount++;
                            continue;
                        }

                        WriteLayerMetadata(layer, tr, metadata, true);
                        output.SuccessCount++;
                    }
                    catch
                    {
                        output.FailCount++;
                    }
                }
                tr.Commit();
            }

            output.Message = "图层属性已写入：成功 " + output.SuccessCount + " 个，跳过 " + output.SkipCount + " 个，失败 " + output.FailCount + " 个。";
            return output;
        }

        public static LayerOperationResult AutoInferLayerMetadata(Document doc, IEnumerable<string> layerNames, bool overwriteExisting)
        {
            var output = new LayerOperationResult();
            List<string> layers = NormalizeLayerNames(layerNames);
            if (layers.Count == 0)
            {
                output.Message = "未选择有效图层。";
                return output;
            }

            List<LayerRecognitionRule> rules = LoadRecognitionRules();
            Database db = doc.Database;
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (string layerName in layers)
                {
                    try
                    {
                        if (!lt.Has(layerName))
                        {
                            output.FailCount++;
                            continue;
                        }

                        LayerTableRecord layer = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead);
                        if (layer.IsDependent)
                        {
                            output.SkipCount++;
                            continue;
                        }

                        LayerMetadata current = ReadLayerMetadata(layer, tr);
                        if (!overwriteExisting && current != null && !current.IsEmpty)
                        {
                            output.SkipCount++;
                            continue;
                        }

                        LayerMetadata inferred = InferLayerMetadataFromName(layer.Name, rules);
                        if (inferred == null || inferred.IsEmpty)
                        {
                            output.SkipCount++;
                            continue;
                        }

                        WriteLayerMetadata(layer, tr, inferred, true);
                        output.SuccessCount++;
                    }
                    catch
                    {
                        output.FailCount++;
                    }
                }
                tr.Commit();
            }

            output.Message = "自动识别完成：写入 " + output.SuccessCount + " 个，跳过 " + output.SkipCount + " 个，失败 " + output.FailCount + " 个。识别规则来自属性识别表。";
            return output;
        }

        public static LayerMetadata GetLayerMetadata(Database db, Transaction tr, string layerName)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(layerName)) return new LayerMetadata();
            try
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName)) return new LayerMetadata();
                LayerTableRecord layer = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead);
                return ReadLayerMetadata(layer, tr);
            }
            catch
            {
                return new LayerMetadata();
            }
        }

        public static void SetLayerMetadata(Database db, Transaction tr, string layerName, LayerMetadata metadata)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(layerName)) return;
            try
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName)) return;
                LayerTableRecord layer = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead);
                WriteLayerMetadata(layer, tr, metadata ?? new LayerMetadata(), true);
            }
            catch
            {
            }
        }

        public static void EnsureLayerMetadata(Database db, Transaction tr, string layerName, LayerMetadata metadata, bool overwriteExisting)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(layerName) || metadata == null) return;
            try
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName)) return;
                LayerTableRecord layer = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead);
                LayerMetadata current = ReadLayerMetadata(layer, tr);
                if (!overwriteExisting && current != null && !current.IsEmpty) return;
                WriteLayerMetadata(layer, tr, metadata, true);
            }
            catch
            {
            }
        }

        public static LayerMetadata InferLayerMetadataFromName(string layerName)
        {
            return InferLayerMetadataFromName(layerName, LoadRecognitionRules());
        }

        public static LayerMetadata InferLayerMetadataFromName(string layerName, IEnumerable<LayerRecognitionRule> rules)
        {
            var metadata = new LayerMetadata();
            if (string.IsNullOrWhiteSpace(layerName)) return metadata;

            List<LayerRecognitionRule> usableRules = rules == null
                ? GetDefaultRecognitionRules()
                : rules.Where(r => r != null && r.Enabled).ToList();
            if (usableRules.Count == 0) usableRules = GetDefaultRecognitionRules();

            var tags = new List<string>();
            foreach (LayerRecognitionRule rule in usableRules)
            {
                Match match;
                if (!IsRuleMatch(rule, layerName, out match)) continue;

                Dictionary<string, string> tokens = BuildRecognitionTokens(layerName, match);
                string parentGroup = ExpandRecognitionTemplate(rule.ParentGroup, tokens).Trim();
                string parentClass = ExpandRecognitionTemplate(rule.ParentClass, tokens).Trim();
                string tagText = ExpandRecognitionTemplate(rule.TagText, tokens).Trim();

                if (!string.IsNullOrWhiteSpace(parentGroup)) metadata.ParentGroup = parentGroup;
                if (!string.IsNullOrWhiteSpace(parentClass)) metadata.ParentClass = parentClass;
                tags.AddRange(LayerMetadata.ParseTags(tagText));

                if (rule.StopAfterMatch) break;
            }

            metadata.Tags = NormalizeTags(tags);
            return metadata;
        }

        public static string GetRecognitionRulesFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "CDBox", "LayerAttributeRecognitionRules.xml");
        }

        public static List<LayerRecognitionRule> LoadRecognitionRules()
        {
            string path = GetRecognitionRulesFilePath();
            try
            {
                if (File.Exists(path))
                {
                    using (FileStream stream = File.OpenRead(path))
                    {
                        var serializer = new XmlSerializer(typeof(LayerRecognitionRuleSet));
                        LayerRecognitionRuleSet set = serializer.Deserialize(stream) as LayerRecognitionRuleSet;
                        if (set != null && set.Rules != null && set.Rules.Count > 0)
                        {
                            return set.Rules.Select(r => r == null ? new LayerRecognitionRule() : r.Clone()).ToList();
                        }
                    }
                }
            }
            catch
            {
            }

            List<LayerRecognitionRule> defaults = GetDefaultRecognitionRules();
            try
            {
                SaveRecognitionRules(defaults);
            }
            catch
            {
            }
            return defaults;
        }

        public static void SaveRecognitionRules(IEnumerable<LayerRecognitionRule> rules)
        {
            string path = GetRecognitionRulesFilePath();
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var set = new LayerRecognitionRuleSet();
            set.Rules = rules == null
                ? new List<LayerRecognitionRule>()
                : rules.Select(r => r == null ? new LayerRecognitionRule() : r.Clone()).ToList();

            using (FileStream stream = File.Create(path))
            {
                var serializer = new XmlSerializer(typeof(LayerRecognitionRuleSet));
                serializer.Serialize(stream, set);
            }
        }

        public static List<LayerRecognitionRule> GetDefaultRecognitionRules()
        {
            // 默认属性识别表保持为用户可直接理解和调整的项目规则。
            // 注意：如果 %APPDATA%\CDBox\LayerAttributeRecognitionRules.xml 已存在，
            // 将优先读取用户表；点击“恢复默认表”时会恢复为这里的规则。
            return new List<LayerRecognitionRule>
            {
                new LayerRecognitionRule
                {
                    Enabled = true,
                    MatchMode = MatchModeExact,
                    Pattern = "ZJ",
                    ParentGroup = "注记",
                    ParentClass = "通用注记",
                    TagText = "注记",
                    StopAfterMatch = true
                },
                new LayerRecognitionRule
                {
                    Enabled = true,
                    MatchMode = MatchModeExact,
                    Pattern = "主管注记",
                    ParentGroup = "注记",
                    ParentClass = "管线长度注记",
                    TagText = "注记、管线注记、长度注记",
                    StopAfterMatch = true
                },
                new LayerRecognitionRule
                {
                    Enabled = true,
                    MatchMode = MatchModeExact,
                    Pattern = "支管注记",
                    ParentGroup = "注记",
                    ParentClass = "管线长度注记",
                    TagText = "注记、管线注记、长度注记",
                    StopAfterMatch = true
                },
                new LayerRecognitionRule
                {
                    Enabled = true,
                    MatchMode = MatchModeWildcard,
                    Pattern = "*井*",
                    ParentGroup = "井",
                    ParentClass = string.Empty,
                    TagText = "井、{Bracket}",
                    StopAfterMatch = true
                },
                new LayerRecognitionRule
                {
                    Enabled = true,
                    MatchMode = MatchModeWildcard,
                    Pattern = "200*",
                    ParentGroup = "主管",
                    ParentClass = string.Empty,
                    TagText = "{DN}、{PipeMaterial}、{Bracket}",
                    StopAfterMatch = true
                },
                new LayerRecognitionRule
                {
                    Enabled = true,
                    MatchMode = MatchModeWildcard,
                    Pattern = "300*",
                    ParentGroup = "主管",
                    ParentClass = string.Empty,
                    TagText = "{DN}、{PipeMaterial}、{Bracket}",
                    StopAfterMatch = true
                },
                new LayerRecognitionRule
                {
                    Enabled = true,
                    MatchMode = MatchModeWildcard,
                    Pattern = "110*",
                    ParentGroup = "支管",
                    ParentClass = "{BaseName}",
                    TagText = "{DN}、{PipeMaterial}、{Bracket}",
                    StopAfterMatch = true
                },
                new LayerRecognitionRule
                {
                    Enabled = true,
                    MatchMode = MatchModeWildcard,
                    Pattern = "75*",
                    ParentGroup = "支管",
                    ParentClass = "{BaseName}",
                    TagText = "{DN}、{PipeMaterial}、{Bracket}",
                    StopAfterMatch = true
                },
                new LayerRecognitionRule
                {
                    Enabled = true,
                    MatchMode = MatchModeWildcard,
                    Pattern = "160*",
                    ParentGroup = "支管",
                    ParentClass = "{BaseName}",
                    TagText = "{DN}、{PipeMaterial}、{Bracket}",
                    StopAfterMatch = true
                },
                new LayerRecognitionRule
                {
                    Enabled = true,
                    MatchMode = MatchModeWildcard,
                    Pattern = "*混凝土*",
                    ParentGroup = "混凝土",
                    ParentClass = string.Empty,
                    TagText = string.Empty,
                    StopAfterMatch = true
                },
                new LayerRecognitionRule
                {
                    Enabled = true,
                    MatchMode = MatchModeWildcard,
                    Pattern = "*化粪池*",
                    ParentGroup = "化粪池",
                    ParentClass = string.Empty,
                    TagText = string.Empty,
                    StopAfterMatch = true
                },
                new LayerRecognitionRule
                {
                    Enabled = false,
                    MatchMode = MatchModeRegex,
                    Pattern = @"^\d+(?:\.\d+)?.*管(?:[（\(].*[）\)])?$",
                    ParentGroup = "支管",
                    ParentClass = string.Empty,
                    TagText = "{Bracket}、{DN}",
                    StopAfterMatch = true
                }
            };
        }

        private static bool IsRuleMatch(LayerRecognitionRule rule, string layerName, out Match match)
        {
            match = null;
            if (rule == null || !rule.Enabled || string.IsNullOrWhiteSpace(rule.Pattern) || string.IsNullOrWhiteSpace(layerName)) return false;

            string mode = string.IsNullOrWhiteSpace(rule.MatchMode) ? MatchModeWildcard : rule.MatchMode.Trim();
            string pattern = rule.Pattern.Trim();
            try
            {
                if (string.Equals(mode, MatchModeExact, StringComparison.CurrentCultureIgnoreCase))
                {
                    return string.Equals(layerName, pattern, StringComparison.CurrentCultureIgnoreCase);
                }
                if (string.Equals(mode, MatchModeContains, StringComparison.CurrentCultureIgnoreCase))
                {
                    return layerName.IndexOf(pattern, StringComparison.CurrentCultureIgnoreCase) >= 0;
                }
                if (string.Equals(mode, MatchModeRegex, StringComparison.CurrentCultureIgnoreCase))
                {
                    match = Regex.Match(layerName, pattern, RegexOptions.IgnoreCase);
                    return match.Success;
                }

                string regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                match = Regex.Match(layerName, regex, RegexOptions.IgnoreCase);
                return match.Success;
            }
            catch
            {
                return false;
            }
        }

        private static Dictionary<string, string> BuildRecognitionTokens(string layerName, Match match)
        {
            var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string name = layerName == null ? string.Empty : layerName.Trim();
            string baseName = name;
            string bracketText = string.Empty;

            Match bracket = Regex.Match(name, "^(?<base>.+?)[（\\(](?<tag>.+?)[）\\)]$");
            if (bracket.Success)
            {
                baseName = bracket.Groups["base"].Value.Trim();
                bracketText = bracket.Groups["tag"].Value.Trim();
            }

            string leadingNumber = string.Empty;
            string pipeMaterial = string.Empty;
            Match pipe = Regex.Match(baseName, "^(?<dn>\\d+(?:\\.\\d+)?)(?<material>.*?)(?:管)?$", RegexOptions.IgnoreCase);
            if (pipe.Success)
            {
                leadingNumber = pipe.Groups["dn"].Value.Trim();
                pipeMaterial = pipe.Groups["material"].Value.Trim();
                if (pipeMaterial.EndsWith("管", StringComparison.CurrentCultureIgnoreCase))
                {
                    pipeMaterial = pipeMaterial.Substring(0, pipeMaterial.Length - 1);
                }
            }

            tokens["LayerName"] = name;
            tokens["BaseName"] = baseName;
            tokens["Bracket"] = bracketText;
            tokens["LeadingNumber"] = leadingNumber;
            tokens["DN"] = string.IsNullOrWhiteSpace(leadingNumber) ? string.Empty : "DN" + leadingNumber;
            tokens["PipeMaterial"] = pipeMaterial;

            if (match != null && match.Success)
            {
                for (int i = 0; i < match.Groups.Count; i++)
                {
                    tokens[i.ToString()] = match.Groups[i].Value == null ? string.Empty : match.Groups[i].Value.Trim();
                }
            }
            return tokens;
        }

        private static string ExpandRecognitionTemplate(string template, Dictionary<string, string> tokens)
        {
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;
            if (tokens == null) tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            return Regex.Replace(template, "\\{(?<key>[^{}]+)\\}", delegate (Match m)
            {
                string key = m.Groups["key"].Value.Trim();
                string value;
                if (tokens.TryGetValue(key, out value)) return value ?? string.Empty;
                return string.Empty;
            });
        }

        private static LayerMetadata ReadLayerMetadata(LayerTableRecord layer, Transaction tr)
        {
            var metadata = new LayerMetadata();
            if (layer == null || tr == null) return metadata;

            try
            {
                Xrecord record = GetLayerMetadataRecord(layer, tr);
                if (record == null || record.Data == null) return metadata;

                foreach (TypedValue value in record.Data)
                {
                    if (value.Value == null) continue;
                    string text = value.Value.ToString();
                    int index = text.IndexOf('=');
                    if (index <= 0) continue;

                    string key = text.Substring(0, index).Trim();
                    string val = text.Substring(index + 1).Trim();
                    if (string.Equals(key, "ParentGroup", StringComparison.OrdinalIgnoreCase)) metadata.ParentGroup = val;
                    else if (string.Equals(key, "ParentClass", StringComparison.OrdinalIgnoreCase)) metadata.ParentClass = val;
                    else if (string.Equals(key, "Tags", StringComparison.OrdinalIgnoreCase)) metadata.Tags = LayerMetadata.ParseTags(val);
                }
            }
            catch
            {
                return new LayerMetadata();
            }

            metadata.Tags = NormalizeTags(metadata.Tags);
            return metadata;
        }

        private static void WriteLayerMetadata(LayerTableRecord layer, Transaction tr, LayerMetadata metadata, bool allowEmpty)
        {
            if (layer == null || tr == null) return;
            metadata = metadata == null ? new LayerMetadata() : metadata.Clone();
            metadata.ParentGroup = metadata.ParentGroup == null ? string.Empty : metadata.ParentGroup.Trim();
            metadata.ParentClass = metadata.ParentClass == null ? string.Empty : metadata.ParentClass.Trim();
            metadata.Tags = NormalizeTags(metadata.Tags);
            if (!allowEmpty && metadata.IsEmpty) return;
            if (layer.IsDependent) return;

            if (!layer.IsWriteEnabled) layer.UpgradeOpen();
            if (layer.ExtensionDictionary.IsNull) layer.CreateExtensionDictionary();

            DBDictionary dict = (DBDictionary)tr.GetObject(layer.ExtensionDictionary, OpenMode.ForWrite);
            Xrecord record = null;
            if (dict.Contains(LayerMetadataXrecordName))
            {
                record = tr.GetObject(dict.GetAt(LayerMetadataXrecordName), OpenMode.ForWrite, false) as Xrecord;
            }
            else
            {
                record = new Xrecord();
                dict.SetAt(LayerMetadataXrecordName, record);
                tr.AddNewlyCreatedDBObject(record, true);
            }

            if (record != null)
            {
                record.Data = new ResultBuffer(
                    new TypedValue((int)DxfCode.Text, "ParentGroup=" + metadata.ParentGroup),
                    new TypedValue((int)DxfCode.Text, "ParentClass=" + metadata.ParentClass),
                    new TypedValue((int)DxfCode.Text, "Tags=" + metadata.TagText));
                WriteLayerMetadataBackup(layer, tr, record.Data);
            }
        }

        private static Xrecord GetLayerMetadataRecord(LayerTableRecord layer, Transaction tr)
        {
            if (layer == null || tr == null) return null;
            try
            {
                if (!layer.ExtensionDictionary.IsNull)
                {
                    DBDictionary dict = tr.GetObject(layer.ExtensionDictionary, OpenMode.ForRead, false) as DBDictionary;
                    if (dict != null && dict.Contains(LayerMetadataXrecordName))
                    {
                        Xrecord primary = tr.GetObject(dict.GetAt(LayerMetadataXrecordName), OpenMode.ForRead, false) as Xrecord;
                        if (primary != null && primary.Data != null) return primary;
                    }
                }

                Database db = layer.Database;
                DBDictionary index = GetLayerMetadataIndex(db, tr, false);
                string key = layer.Handle.ToString();
                return index != null && index.Contains(key)
                    ? tr.GetObject(index.GetAt(key), OpenMode.ForRead, false) as Xrecord
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static void WriteLayerMetadataBackup(LayerTableRecord layer, Transaction tr, ResultBuffer data)
        {
            if (layer == null || tr == null || data == null) return;
            DBDictionary index = GetLayerMetadataIndex(layer.Database, tr, true);
            if (index == null) return;
            string key = layer.Handle.ToString();
            Xrecord record;
            if (index.Contains(key))
            {
                record = tr.GetObject(index.GetAt(key), OpenMode.ForWrite, false) as Xrecord;
            }
            else
            {
                if (!index.IsWriteEnabled) index.UpgradeOpen();
                record = new Xrecord();
                index.SetAt(key, record);
                tr.AddNewlyCreatedDBObject(record, true);
            }
            if (record != null) record.Data = new ResultBuffer(data.AsArray());
        }

        private static DBDictionary GetLayerMetadataIndex(Database db, Transaction tr, bool create)
        {
            if (db == null || tr == null) return null;
            DBDictionary nod = tr.GetObject(db.NamedObjectsDictionaryId, create ? OpenMode.ForWrite : OpenMode.ForRead, false) as DBDictionary;
            if (nod == null) return null;
            if (nod.Contains(LayerMetadataIndexDictionaryName))
                return tr.GetObject(nod.GetAt(LayerMetadataIndexDictionaryName), create ? OpenMode.ForWrite : OpenMode.ForRead, false) as DBDictionary;
            if (!create) return null;
            var index = new DBDictionary();
            nod.SetAt(LayerMetadataIndexDictionaryName, index);
            tr.AddNewlyCreatedDBObject(index, true);
            return index;
        }

        private static List<string> NormalizeTags(IEnumerable<string> tags)
        {
            var output = new List<string>();
            var set = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            if (tags == null) return output;

            foreach (string raw in tags)
            {
                if (raw == null) continue;
                string tag = raw.Trim();
                if (tag.Length == 0) continue;
                if (set.Contains(tag)) continue;
                set.Add(tag);
                output.Add(tag);
            }
            return output;
        }

        public static List<string> ParseLayerNames(string text)
        {
            if (text == null) return new List<string>();
            string normalized = text.Replace('\r', ',').Replace('\n', ',').Replace('，', ',').Replace(';', ',').Replace('；', ',');
            string[] tokens = normalized.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            return NormalizeLayerNames(tokens);
        }

        private static string BuildStatusText(LayerInfo item)
        {
            var parts = new List<string>();
            if (item.IsCurrent) parts.Add("当前");
            if (item.IsOff) parts.Add("关闭");
            if (item.IsFrozen) parts.Add("冻结");
            if (item.IsLocked) parts.Add("锁定");
            if (item.IsDependent) parts.Add("外参");
            if (parts.Count == 0) return "正常";
            return string.Join("/", parts.ToArray());
        }

        private static string GetLineTypeName(LayerTableRecord layer, Transaction tr)
        {
            try
            {
                if (layer.LinetypeObjectId.IsNull) return string.Empty;
                LinetypeTableRecord lineType = tr.GetObject(layer.LinetypeObjectId, OpenMode.ForRead) as LinetypeTableRecord;
                return lineType == null ? string.Empty : lineType.Name;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static Dictionary<string, int> CountObjectsByLayer(Database db, Transaction tr)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId btrId in bt)
            {
                BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) continue;
                if (!btr.IsLayout && !string.Equals(btr.Name, BlockTableRecord.ModelSpace, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (ObjectId entId in btr)
                {
                    try
                    {
                        Entity ent = tr.GetObject(entId, OpenMode.ForRead, false) as Entity;
                        if (ent == null || string.IsNullOrEmpty(ent.Layer)) continue;
                        if (!counts.ContainsKey(ent.Layer)) counts[ent.Layer] = 0;
                        counts[ent.Layer]++;
                    }
                    catch
                    {
                        // 某些代理对象读取失败时跳过，避免整个界面打不开。
                    }
                }
            }
            return counts;
        }

        private static List<ObjectId> GetEntityIdsByLayers(Database db, Transaction tr, IEnumerable<string> layerNames)
        {
            HashSet<string> layerSet = new HashSet<string>(NormalizeLayerNames(layerNames), StringComparer.OrdinalIgnoreCase);
            var ids = new List<ObjectId>();
            if (layerSet.Count == 0) return ids;

            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId btrId in bt)
            {
                BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) continue;
                if (!btr.IsLayout && !string.Equals(btr.Name, BlockTableRecord.ModelSpace, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (ObjectId entId in btr)
                {
                    try
                    {
                        Entity ent = tr.GetObject(entId, OpenMode.ForRead, false) as Entity;
                        if (ent != null && layerSet.Contains(ent.Layer)) ids.Add(entId);
                    }
                    catch
                    {
                    }
                }
            }
            return ids;
        }

        private static SelectionFilter BuildLayerSelectionFilter(IList<string> layers)
        {
            if (layers.Count == 1)
            {
                return new SelectionFilter(new[] { new TypedValue((int)DxfCode.LayerName, layers[0]) });
            }

            var values = new List<TypedValue>();
            values.Add(new TypedValue((int)DxfCode.Operator, "<OR"));
            foreach (string layer in layers)
            {
                values.Add(new TypedValue((int)DxfCode.LayerName, layer));
            }
            values.Add(new TypedValue((int)DxfCode.Operator, "OR>"));
            return new SelectionFilter(values.ToArray());
        }

        private static ObjectId ResolveLayerId(LayerTable lt, string layerName)
        {
            try
            {
                if (lt != null && lt.Has(layerName)) return lt[layerName];
            }
            catch
            {
            }
            return ObjectId.Null;
        }

        private static string GetCurrentLayerName(Database db, Transaction tr)
        {
            try
            {
                LayerTableRecord current = (LayerTableRecord)tr.GetObject(db.Clayer, OpenMode.ForRead);
                return current.Name;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static List<string> NormalizeLayerNames(IEnumerable<string> layerNames)
        {
            var output = new List<string>();
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (layerNames == null) return output;

            foreach (string raw in layerNames)
            {
                if (raw == null) continue;
                string name = raw.Trim();
                if (name.Length == 0) continue;
                if (set.Contains(name)) continue;
                set.Add(name);
                output.Add(name);
            }
            return output;
        }
    }
}
