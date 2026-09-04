using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace CDBox.Shared.Components
{
    public static class CDBoxComponentIds
    {
        public const string Base = "base";
        public const string Common = "common";
        public const string Wastewater = "wastewater";
        public const string RealEstate = "realestate";
    }

    public sealed class CDBoxComponentDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public bool Required { get; set; }
        public bool DefaultSelected { get; set; }
        public bool CanChangeSelection { get; set; }
        public string StatusText { get; set; }
        public int MinimumAutoCadYear { get; set; }
        public int MaximumAutoCadYear { get; set; }
        public string MinimumCoreVersion { get; set; }
        public string[] Dependencies { get; set; }
        public string[] RequiredBundlePaths { get; set; }
        public string[] OwnedBundlePaths { get; set; }

        public CDBoxComponentDefinition()
        {
            Id = string.Empty;
            DisplayName = string.Empty;
            StatusText = string.Empty;
            MinimumCoreVersion = string.Empty;
            Dependencies = new string[0];
            RequiredBundlePaths = new string[0];
            OwnedBundlePaths = new string[0];
        }
    }

    public sealed class CDBoxComponentManifest
    {
        public int SchemaVersion { get; set; }
        public string ProductVersion { get; set; }
        public CDBoxComponentDefinition[] Components { get; set; }

        public CDBoxComponentManifest()
        {
            SchemaVersion = 1;
            ProductVersion = string.Empty;
            Components = new CDBoxComponentDefinition[0];
        }
    }

    public sealed class CDBoxInstalledComponentState
    {
        public int SchemaVersion { get; set; }
        public string ProductVersion { get; set; }
        public string[] SelectedComponentIds { get; set; }

        public CDBoxInstalledComponentState()
        {
            SchemaVersion = 1;
            ProductVersion = string.Empty;
            SelectedComponentIds = new string[0];
        }
    }

    public sealed class CDBoxInstalledComponentSnapshot
    {
        public string[] SelectedComponentIds { get; set; }
        public string[] SelectedComponentNames { get; set; }
        public bool StateFilePresent { get; set; }
        public bool StateFileValid { get; set; }
        public bool InferredFromFiles { get; set; }
        public string Warning { get; set; }

        public CDBoxInstalledComponentSnapshot()
        {
            SelectedComponentIds = new string[0];
            SelectedComponentNames = new string[0];
            Warning = string.Empty;
        }
    }

    /// <summary>
    /// 四类安装组件的稳定身份与当前迁移状态。图层管理器属于必装基础
    /// 组件；公共业务、污水业务与不动产业务均可独立选择。
    /// </summary>
    public static class CDBoxComponentCatalog
    {
        private static readonly IReadOnlyList<CDBoxComponentDefinition>
            Definitions = new[]
            {
                new CDBoxComponentDefinition
                {
                    Id = CDBoxComponentIds.Base,
                    DisplayName = "基础组件",
                    Required = true,
                    DefaultSelected = true,
                    CanChangeSelection = false,
                    StatusText = "必须安装",
                    RequiredBundlePaths = new[]
                    {
                        "PackageContents.xml",
                        @"Contents\CDBox.dll",
                        @"Contents\CDBox.Shared.dll",
                        @"Contents\components.json"
                    }
                },
                new CDBoxComponentDefinition
                {
                    Id = CDBoxComponentIds.Common,
                    DisplayName = "公共业务",
                    DefaultSelected = true,
                    CanChangeSelection = true,
                    StatusText = "可选安装",
                    Dependencies = new[] { CDBoxComponentIds.Base },
                    RequiredBundlePaths = new[]
                    {
                        @"Contents\CDBox.Common.dll"
                    },
                    OwnedBundlePaths = new[]
                    {
                        @"Contents\CDBox.Common.dll",
                        @"Contents\CDBox.Common.pdb"
                    }
                },
                new CDBoxComponentDefinition
                {
                    Id = CDBoxComponentIds.Wastewater,
                    DisplayName = "污水业务",
                    DefaultSelected = true,
                    CanChangeSelection = true,
                    StatusText = "可选安装",
                    Dependencies = new[] { CDBoxComponentIds.Base },
                    RequiredBundlePaths = new[]
                    {
                        @"Contents\CDBox.Wastewater.dll",
                        @"Contents\Templates\工程量计算表模板.xls"
                    },
                    OwnedBundlePaths = new[]
                    {
                        @"Contents\CDBox.Wastewater.dll",
                        @"Contents\CDBox.Wastewater.pdb",
                        @"Contents\Templates\工程量计算表模板.xls"
                    }
                },
                new CDBoxComponentDefinition
                {
                    Id = CDBoxComponentIds.RealEstate,
                    DisplayName = "不动产业务",
                    DefaultSelected = true,
                    CanChangeSelection = true,
                    StatusText = "可选安装",
                    Dependencies = new[] { CDBoxComponentIds.Base },
                    RequiredBundlePaths = new[]
                    {
                        @"Contents\CDBox.RealEstate.dll"
                    },
                    OwnedBundlePaths = new[]
                    {
                        @"Contents\CDBox.RealEstate.dll",
                        @"Contents\CDBox.RealEstate.pdb",
                        @"Contents\Templates\权籍调查表.xls",
                        @"Contents\Templates\地籍调查表.docx",
                        @"Contents\Templates\四张检查表.xls"
                    }
                }
            };

        public static IReadOnlyList<CDBoxComponentDefinition> All
        {
            get { return Definitions; }
        }

        public static CDBoxComponentManifest CreateManifest(
            string productVersion)
        {
            return new CDBoxComponentManifest
            {
                SchemaVersion = 1,
                ProductVersion = productVersion ?? string.Empty,
                Components = Definitions.Select(Clone).ToArray()
            };
        }

        public static string[] DefaultSelection()
        {
            return Definitions.Where(item => item.Required
                    || item.DefaultSelected)
                .Select(item => item.Id).ToArray();
        }

        private static CDBoxComponentDefinition Clone(
            CDBoxComponentDefinition source)
        {
            return new CDBoxComponentDefinition
            {
                Id = source.Id,
                DisplayName = source.DisplayName,
                Required = source.Required,
                DefaultSelected = source.DefaultSelected,
                CanChangeSelection = source.CanChangeSelection,
                StatusText = source.StatusText,
                MinimumAutoCadYear = source.MinimumAutoCadYear,
                MaximumAutoCadYear = source.MaximumAutoCadYear,
                MinimumCoreVersion = source.MinimumCoreVersion,
                Dependencies = (source.Dependencies ?? new string[0]).ToArray(),
                RequiredBundlePaths = (source.RequiredBundlePaths
                    ?? new string[0]).ToArray(),
                OwnedBundlePaths = (source.OwnedBundlePaths
                    ?? new string[0]).ToArray()
            };
        }
    }

    /// <summary>
    /// 发布包组件清单及已安装组件状态的统一读写与裁剪逻辑。
    /// 所有删除操作只允许发生在调用者提供的 bundle 暂存目录内。
    /// </summary>
    public static class CDBoxComponentBundle
    {
        public const string ManifestRelativePath = @"Contents\components.json";
        public const string InstalledStateRelativePath =
            @"Contents\components.installed.json";

        public static CDBoxComponentManifest ReadManifest(string bundleRoot)
        {
            string path = SafeBundlePath(bundleRoot, ManifestRelativePath);
            if (!File.Exists(path))
                throw new InvalidDataException("组件清单不存在："
                    + ManifestRelativePath);
            CDBoxComponentManifest manifest = Deserialize<CDBoxComponentManifest>(
                File.ReadAllText(path, Encoding.UTF8));
            ValidateManifest(manifest);
            return manifest;
        }

        public static string SerializeManifest(CDBoxComponentManifest manifest)
        {
            ValidateManifest(manifest);
            return Serialize(manifest);
        }

        public static CDBoxComponentManifest DeserializeManifest(string json)
        {
            CDBoxComponentManifest manifest =
                Deserialize<CDBoxComponentManifest>(json);
            ValidateManifest(manifest);
            return manifest;
        }

        public static string[] ReadInstalledSelection(
            string bundleRoot, CDBoxComponentManifest manifest)
        {
            return InspectInstalledBundle(bundleRoot, manifest)
                .SelectedComponentIds;
        }

        public static CDBoxInstalledComponentSnapshot InspectInstalledBundle(
            string bundleRoot, CDBoxComponentManifest manifest)
        {
            ValidateManifest(manifest);
            var snapshot = new CDBoxInstalledComponentSnapshot();
            string path = SafeBundlePath(bundleRoot,
                InstalledStateRelativePath);
            snapshot.StateFilePresent = File.Exists(path);
            string[] selected = null;

            if (snapshot.StateFilePresent)
            {
                try
                {
                    CDBoxInstalledComponentState state =
                        Deserialize<CDBoxInstalledComponentState>(
                            File.ReadAllText(path, Encoding.UTF8));
                    if (state.SchemaVersion != 1)
                        throw new InvalidDataException(
                            "不支持的已安装组件状态版本："
                            + state.SchemaVersion);
                    selected = NormalizeSelection(manifest,
                        state.SelectedComponentIds);
                    snapshot.StateFileValid = true;
                }
                catch (Exception ex)
                {
                    snapshot.Warning = "已安装组件状态文件无效，已根据当前文件重新识别："
                        + ex.Message;
                }
            }
            else
            {
                snapshot.Warning = "未找到已安装组件状态文件，已根据当前文件识别安装组合。";
            }

            if (selected == null)
            {
                selected = InferInstalledSelectionFromFiles(bundleRoot,
                    manifest);
                snapshot.InferredFromFiles = true;
            }

            snapshot.SelectedComponentIds = selected;
            var selectedSet = new HashSet<string>(selected,
                StringComparer.OrdinalIgnoreCase);
            snapshot.SelectedComponentNames = manifest.Components
                .Where(item => selectedSet.Contains(item.Id))
                .Select(item => item.DisplayName).ToArray();
            return snapshot;
        }

        public static void ApplySelection(string bundleRoot,
            CDBoxComponentManifest manifest,
            IEnumerable<string> selectedComponentIds)
        {
            ValidateManifest(manifest);
            string[] selected = NormalizeSelection(manifest,
                selectedComponentIds);
            var selectedSet = new HashSet<string>(selected,
                StringComparer.OrdinalIgnoreCase);

            foreach (CDBoxComponentDefinition component in manifest.Components)
            {
                if (selectedSet.Contains(component.Id)) continue;
                foreach (string relativePath in component.OwnedBundlePaths
                    ?? new string[0])
                {
                    string fullPath = SafeBundlePath(bundleRoot, relativePath);
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                    else if (Directory.Exists(fullPath))
                        Directory.Delete(fullPath, true);
                }
            }

            ValidateInstalledBundle(bundleRoot, manifest, selected);
            var state = new CDBoxInstalledComponentState
            {
                SchemaVersion = 1,
                ProductVersion = manifest.ProductVersion ?? string.Empty,
                SelectedComponentIds = selected
            };
            string statePath = SafeBundlePath(bundleRoot,
                InstalledStateRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(statePath));
            File.WriteAllText(statePath, Serialize(state),
                new UTF8Encoding(false));
        }

        public static void ValidatePayloadBundle(string bundleRoot,
            CDBoxComponentManifest manifest)
        {
            ValidateManifest(manifest);
            foreach (CDBoxComponentDefinition component in manifest.Components)
                RequirePaths(bundleRoot, component.RequiredBundlePaths,
                    component.DisplayName);
        }

        public static void ValidateInstalledBundle(string bundleRoot,
            CDBoxComponentManifest manifest,
            IEnumerable<string> selectedComponentIds = null)
        {
            ValidateManifest(manifest);
            string[] selected = selectedComponentIds == null
                ? ReadInstalledSelection(bundleRoot, manifest)
                : NormalizeSelection(manifest, selectedComponentIds);
            var selectedSet = new HashSet<string>(selected,
                StringComparer.OrdinalIgnoreCase);
            foreach (CDBoxComponentDefinition component in manifest.Components)
                if (component.Required || selectedSet.Contains(component.Id))
                    RequirePaths(bundleRoot, component.RequiredBundlePaths,
                        component.DisplayName);
        }

        public static bool IsInstalledFromContentsDirectory(
            string contentsDirectory, string componentId)
        {
            if (string.IsNullOrWhiteSpace(contentsDirectory)
                || string.IsNullOrWhiteSpace(componentId)) return false;
            string fullContents = Path.GetFullPath(contentsDirectory);
            string bundleRoot = Directory.GetParent(fullContents) == null
                ? string.Empty : Directory.GetParent(fullContents).FullName;
            try
            {
                CDBoxComponentManifest manifest = ReadManifest(bundleRoot);
                return ReadInstalledSelection(bundleRoot, manifest).Any(id =>
                    string.Equals(id, componentId,
                        StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                CDBoxComponentDefinition fallback = CDBoxComponentCatalog.All
                    .FirstOrDefault(item => string.Equals(item.Id, componentId,
                        StringComparison.OrdinalIgnoreCase));
                if (fallback == null) return false;
                if (fallback.Required) return true;
                string primaryPath = (fallback.RequiredBundlePaths
                    ?? new string[0]).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(primaryPath)) return false;
                const string prefix = "Contents\\";
                if (primaryPath.StartsWith(prefix,
                    StringComparison.OrdinalIgnoreCase))
                    primaryPath = primaryPath.Substring(prefix.Length);
                return File.Exists(Path.Combine(fullContents, primaryPath));
            }
        }

        public static string[] NormalizeSelection(
            CDBoxComponentManifest manifest,
            IEnumerable<string> selectedComponentIds)
        {
            ValidateManifest(manifest);
            var known = new HashSet<string>(manifest.Components
                .Select(item => item.Id), StringComparer.OrdinalIgnoreCase);
            var selected = new HashSet<string>(
                selectedComponentIds ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            selected.RemoveWhere(id => !known.Contains(id));
            foreach (CDBoxComponentDefinition component in manifest.Components)
                if (component.Required) selected.Add(component.Id);

            bool changed;
            do
            {
                changed = false;
                foreach (CDBoxComponentDefinition component in manifest.Components)
                {
                    if (!selected.Contains(component.Id)) continue;
                    foreach (string dependency in component.Dependencies
                        ?? new string[0])
                        if (known.Contains(dependency)
                            && selected.Add(dependency)) changed = true;
                }
            } while (changed);

            return manifest.Components.Where(item => selected.Contains(item.Id))
                .Select(item => item.Id).ToArray();
        }

        private static string[] DefaultSelection(
            CDBoxComponentManifest manifest)
        {
            return NormalizeSelection(manifest, manifest.Components
                .Where(item => item.Required || item.DefaultSelected)
                .Select(item => item.Id));
        }

        private static string[] InferInstalledSelectionFromFiles(
            string bundleRoot, CDBoxComponentManifest manifest)
        {
            var selected = new List<string>();
            foreach (CDBoxComponentDefinition component in manifest.Components)
            {
                if (component.Required)
                {
                    selected.Add(component.Id);
                    continue;
                }

                string[] requiredPaths = component.RequiredBundlePaths
                    ?? new string[0];
                if (requiredPaths.Length == 0) continue;
                bool installed = requiredPaths.All(relativePath =>
                {
                    string fullPath = SafeBundlePath(bundleRoot, relativePath);
                    return File.Exists(fullPath) || Directory.Exists(fullPath);
                });
                if (installed) selected.Add(component.Id);
            }
            return NormalizeSelection(manifest, selected);
        }

        private static void ValidateManifest(CDBoxComponentManifest manifest)
        {
            if (manifest == null || manifest.SchemaVersion != 1
                || manifest.Components == null
                || manifest.Components.Length == 0)
                throw new InvalidDataException("组件清单格式无效。");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CDBoxComponentDefinition component in manifest.Components)
            {
                if (component == null || string.IsNullOrWhiteSpace(component.Id)
                    || string.IsNullOrWhiteSpace(component.DisplayName)
                    || !ids.Add(component.Id))
                    throw new InvalidDataException("组件清单包含无效或重复的组件。");
                foreach (string dependency in component.Dependencies
                    ?? new string[0])
                    if (string.IsNullOrWhiteSpace(dependency))
                        throw new InvalidDataException("组件依赖不能为空。");
                foreach (string relativePath in component.RequiredBundlePaths
                    ?? new string[0]) ValidateRelativePath(relativePath);
                foreach (string relativePath in component.OwnedBundlePaths
                    ?? new string[0]) ValidateRelativePath(relativePath);
            }
            foreach (CDBoxComponentDefinition component in manifest.Components)
                foreach (string dependency in component.Dependencies
                    ?? new string[0])
                    if (!ids.Contains(dependency))
                        throw new InvalidDataException("组件依赖不存在："
                            + component.Id + " -> " + dependency);
            if (!manifest.Components.Any(item => item.Required
                && string.Equals(item.Id, CDBoxComponentIds.Base,
                    StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("组件清单缺少必须安装的基础组件。");
        }

        private static void RequirePaths(string bundleRoot,
            IEnumerable<string> relativePaths, string componentName)
        {
            foreach (string relativePath in relativePaths
                ?? Enumerable.Empty<string>())
                if (!File.Exists(SafeBundlePath(bundleRoot, relativePath))
                    && !Directory.Exists(SafeBundlePath(bundleRoot, relativePath)))
                    throw new InvalidDataException((componentName ?? "组件")
                        + "缺少安装文件：" + relativePath);
        }

        private static string SafeBundlePath(string bundleRoot,
            string relativePath)
        {
            if (string.IsNullOrWhiteSpace(bundleRoot))
                throw new DirectoryNotFoundException("bundle 目录不能为空。");
            ValidateRelativePath(relativePath);
            string root = Path.GetFullPath(bundleRoot)
                .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(bundleRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(root,
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("组件文件路径越界："
                    + relativePath);
            return candidate;
        }

        private static void ValidateRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)
                || Path.IsPathRooted(relativePath))
                throw new InvalidDataException("组件文件路径无效："
                    + relativePath);
            string normalized = relativePath.Replace('/', '\\');
            if (normalized.Split('\\').Any(part => part == ".."))
                throw new InvalidDataException("组件文件路径越界："
                    + relativePath);
        }

        private static string Serialize(object value)
        {
            return new JavaScriptSerializer().Serialize(value);
        }

        private static T Deserialize<T>(string json)
        {
            T value = new JavaScriptSerializer().Deserialize<T>(json);
            if (value == null) throw new InvalidDataException("组件数据为空。");
            return value;
        }
    }
}
