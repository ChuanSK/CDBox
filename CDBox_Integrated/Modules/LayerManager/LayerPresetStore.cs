using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace TCPipeAutoDraw.Modules.LayerManager
{
    public sealed class LayerPresetDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsBuiltIn { get; set; }
        public List<string> Layers { get; set; }

        public LayerPresetDefinition()
        {
            Id = string.Empty;
            Name = string.Empty;
            Description = string.Empty;
            Layers = new List<string>();
        }

        public LayerPresetDefinition Clone()
        {
            return new LayerPresetDefinition
            {
                Id = Id ?? string.Empty,
                Name = Name ?? string.Empty,
                Description = Description ?? string.Empty,
                IsBuiltIn = IsBuiltIn,
                Layers = new List<string>(Layers ?? new List<string>())
            };
        }
    }

    [XmlRoot("CDBoxLayerPresets")]
    public sealed class LayerPresetStoreData
    {
        [XmlArray("Presets")]
        [XmlArrayItem("Preset")]
        public List<LayerPresetDefinition> Presets { get; set; }

        public LayerPresetStoreData()
        {
            Presets = new List<LayerPresetDefinition>();
        }
    }

    public static class LayerPresetStore
    {
        private const string CompatibilityPresetId = "builtin-compatibility";
        private const string StandardPresetId = "builtin-standard-drainage";

        public static List<LayerPresetDefinition> LoadAll()
        {
            List<LayerPresetDefinition> result = BuildBuiltInPresets();
            LayerPresetStoreData data = LoadCustomData();
            foreach (LayerPresetDefinition preset in data.Presets ?? new List<LayerPresetDefinition>())
            {
                LayerPresetDefinition normalized = NormalizePreset(preset, false);
                if (normalized == null) continue;
                if (result.Any(x => string.Equals(x.Id, normalized.Id,
                    StringComparison.OrdinalIgnoreCase))) continue;
                result.Add(normalized);
            }
            return result;
        }

        public static LayerPresetDefinition Find(string presetId)
        {
            string id = (presetId ?? string.Empty).Trim();
            if (id.Length == 0) id = CompatibilityPresetId;
            return LoadAll().FirstOrDefault(x => string.Equals(x.Id, id,
                StringComparison.OrdinalIgnoreCase));
        }

        public static LayerPresetDefinition SaveCustom(string name, IEnumerable<string> layers)
        {
            string presetName = (name ?? string.Empty).Trim();
            if (presetName.Length == 0) throw new InvalidOperationException("请输入预设名称。");
            List<string> normalizedLayers = NormalizeLayerNames(layers);
            if (normalizedLayers.Count == 0) throw new InvalidOperationException("预设中至少需要一个有效图层名。");

            List<LayerPresetDefinition> builtIns = BuildBuiltInPresets();
            if (builtIns.Any(x => string.Equals(x.Name, presetName,
                StringComparison.CurrentCultureIgnoreCase)))
            {
                throw new InvalidOperationException("该名称已由内置预设使用，请换一个名称。");
            }

            LayerPresetStoreData data = LoadCustomData();
            if (data.Presets == null) data.Presets = new List<LayerPresetDefinition>();
            LayerPresetDefinition preset = data.Presets
                .FirstOrDefault(x => x != null && string.Equals(x.Name, presetName,
                    StringComparison.CurrentCultureIgnoreCase));
            if (preset == null)
            {
                preset = new LayerPresetDefinition
                {
                    Id = "custom-" + Guid.NewGuid().ToString("N"),
                    Name = presetName
                };
                data.Presets.Add(preset);
            }

            preset.Name = presetName;
            preset.Description = "用户自定义预设";
            preset.IsBuiltIn = false;
            preset.Layers = normalizedLayers;
            SaveCustomData(data);
            return NormalizePreset(preset, false);
        }

        public static bool DeleteCustom(string presetId)
        {
            string id = (presetId ?? string.Empty).Trim();
            if (id.Length == 0) return false;
            if (BuildBuiltInPresets().Any(x => string.Equals(x.Id, id,
                StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("内置预设不能删除。");
            }

            LayerPresetStoreData data = LoadCustomData();
            if (data.Presets == null) data.Presets = new List<LayerPresetDefinition>();
            int removed = data.Presets.RemoveAll(x => x != null
                && string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (removed > 0) SaveCustomData(data);
            return removed > 0;
        }

        private static List<LayerPresetDefinition> BuildBuiltInPresets()
        {
            return new List<LayerPresetDefinition>
            {
                new LayerPresetDefinition
                {
                    Id = CompatibilityPresetId,
                    Name = "兼容默认管网",
                    Description = "保留现有项目的传统命名，适合继续使用旧图纸与旧模板。",
                    IsBuiltIn = true,
                    Layers = new List<string>(LayerManagerService.DefaultPipeLayers)
                },
                new LayerPresetDefinition
                {
                    Id = StandardPresetId,
                    Name = "标准雨污管网",
                    Description = "采用 CDBox 结构化命名，包含主管、支管、井、结构层与常用注记。",
                    IsBuiltIn = true,
                    Layers = new List<string>
                    {
                        "主管-DN200-波纹管",
                        "主管-DN300-双壁波纹管-粗砂回填",
                        "主管-DN300-波纹管-混凝土恢复",
                        "主管-DN300-波纹管-原土回填",
                        "支管-DN75-PVC-明管",
                        "支管-DN110-PVC-并埋",
                        "支管-DN110-PVC-混凝土恢复",
                        "井-检查井-D500-砖砌-铸铁盖",
                        "井-检查井-D700-混凝土-铸铁盖",
                        "井-沉泥井-D700-砖砌-铸铁盖",
                        "井-315小井-D315-塑料-混凝土盖",
                        "结构层-C25混凝土-T150-路面恢复",
                        "结构层-中粗砂-T100-垫层",
                        "结构层-碎石-T150-垫层",
                        "注记-管线长度-主管",
                        "注记-管线长度-支管",
                        "注记-节点",
                        "注记-工程量",
                        "注记-断面"
                    }
                }
            };
        }

        private static LayerPresetStoreData LoadCustomData()
        {
            string path = GetStorePath();
            if (!File.Exists(path)) return new LayerPresetStoreData();
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    var serializer = new XmlSerializer(typeof(LayerPresetStoreData));
                    return serializer.Deserialize(stream) as LayerPresetStoreData
                        ?? new LayerPresetStoreData();
                }
            }
            catch
            {
                return new LayerPresetStoreData();
            }
        }

        private static void SaveCustomData(LayerPresetStoreData data)
        {
            string path = GetStorePath();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            string tempPath = path + ".tmp";
            using (FileStream stream = File.Create(tempPath))
            {
                var serializer = new XmlSerializer(typeof(LayerPresetStoreData));
                serializer.Serialize(stream, data ?? new LayerPresetStoreData());
            }
            if (File.Exists(path)) File.Replace(tempPath, path, null);
            else File.Move(tempPath, path);
        }

        private static string GetStorePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "CDBox", "LayerPresets.xml");
        }

        private static LayerPresetDefinition NormalizePreset(LayerPresetDefinition preset,
            bool builtIn)
        {
            if (preset == null) return null;
            string id = (preset.Id ?? string.Empty).Trim();
            string name = (preset.Name ?? string.Empty).Trim();
            List<string> layers = NormalizeLayerNames(preset.Layers);
            if (id.Length == 0 || name.Length == 0 || layers.Count == 0) return null;
            return new LayerPresetDefinition
            {
                Id = id,
                Name = name,
                Description = (preset.Description ?? string.Empty).Trim(),
                IsBuiltIn = builtIn,
                Layers = layers
            };
        }

        private static List<string> NormalizeLayerNames(IEnumerable<string> layers)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (string raw in layers ?? Enumerable.Empty<string>())
            {
                string name = (raw ?? string.Empty).Trim();
                if (name.Length == 0 || seen.Contains(name)) continue;
                seen.Add(name);
                result.Add(name);
            }
            return result;
        }
    }
}
