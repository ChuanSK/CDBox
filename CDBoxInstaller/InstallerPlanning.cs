using System;
using System.Collections.Generic;
using System.Linq;
using CDBox.Shared.Components;

namespace CDBox.Setup
{
    internal static class InstallerTargetSelection
    {
        public static bool ShouldSelectByDefault(
            CadInstallation installation, string explicitCadExecutablePath,
            bool cdBoxInstalled)
        {
            if (installation == null || !installation.IsDetected)
                return false;
            if (string.IsNullOrWhiteSpace(explicitCadExecutablePath))
                return cdBoxInstalled;
            return PathsEqual(installation.AcadExecutablePath,
                explicitCadExecutablePath);
        }

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(System.IO.Path.GetFullPath(left),
                    System.IO.Path.GetFullPath(right),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }

    internal enum InstallerPlanAction
    {
        None,
        Install,
        Update,
        ApplyChanges,
        Repair,
        Blocked
    }

    internal enum InstallerComponentState
    {
        Required,
        Installed,
        NotInstalled,
        PendingInstall,
        PendingRemove,
        UpdateAvailable,
        RepairRequired,
        Incompatible
    }

    internal sealed class InstallerTargetSnapshot
    {
        public CadInstallation Installation { get; set; }
        public bool Selected { get; set; }
        public bool Installed { get; set; }
        public bool InstallationHealthy { get; set; } = true;
        public string InstalledVersion { get; set; } = string.Empty;
        public string[] InstalledComponentIds { get; set; } =
            new string[0];
    }

    internal sealed class InstallerPendingChange
    {
        public string TargetName { get; set; } = string.Empty;
        public string ComponentId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsRemoval { get; set; }
    }

    internal sealed class InstallerPlan
    {
        public InstallerPlanAction Action { get; set; }
        public string PrimaryButtonText { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public bool CanExecute { get; set; }
        public string[] DesiredComponentIds { get; set; } =
            new string[0];
        public InstallerPendingChange[] Changes { get; set; } =
            new InstallerPendingChange[0];
        public IDictionary<string, InstallerComponentState> ComponentStates
            { get; set; } = new Dictionary<string, InstallerComponentState>(
                StringComparer.OrdinalIgnoreCase);
        public IDictionary<string, string[]> IncompatibleTargets
            { get; set; } = new Dictionary<string, string[]>(
                StringComparer.OrdinalIgnoreCase);
    }

    internal static class InstallerPlanEvaluator
    {
        public static string[] DefaultComponentSelection(
            CDBoxComponentManifest manifest)
        {
            if (manifest == null || manifest.Components == null)
                return new string[0];
            return CDBoxComponentBundle.NormalizeSelection(manifest,
                manifest.Components.Select(component => component.Id));
        }

        public static InstallerPlan Evaluate(
            CDBoxComponentManifest manifest,
            IEnumerable<InstallerTargetSnapshot> targets,
            IEnumerable<string> desiredComponentIds,
            bool repairRequested)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            InstallerTargetSnapshot[] selected = (targets
                    ?? Enumerable.Empty<InstallerTargetSnapshot>())
                .Where(x => x != null && x.Selected
                    && x.Installation != null
                    && x.Installation.IsDetected).ToArray();
            string[] desired = CDBoxComponentBundle.NormalizeSelection(
                manifest, desiredComponentIds);
            var desiredSet = new HashSet<string>(desired,
                StringComparer.OrdinalIgnoreCase);
            var plan = new InstallerPlan
            {
                DesiredComponentIds = desired
            };

            if (selected.Length == 0)
            {
                plan.Action = InstallerPlanAction.Blocked;
                plan.PrimaryButtonText = "请选择安装目标";
                plan.Summary = "至少选择一个可用的 AutoCAD 版本。";
                PopulateComponentStates(plan, manifest, selected,
                    desiredSet, repairRequested);
                return plan;
            }

            PopulateCompatibility(plan, manifest, selected, desiredSet);
            if (plan.IncompatibleTargets.Count > 0)
            {
                plan.Action = InstallerPlanAction.Blocked;
                plan.PrimaryButtonText = "存在不兼容组件";
                plan.Summary = "当前组件组合不能用于全部所选 AutoCAD 版本。";
                PopulateComponentStates(plan, manifest, selected,
                    desiredSet, repairRequested);
                return plan;
            }

            var changes = new List<InstallerPendingChange>();
            bool hasNewTarget = false;
            bool hasUpdate = false;
            bool hasComponentChange = false;
            bool hasRepairIssue = false;
            foreach (InstallerTargetSnapshot target in selected)
            {
                string targetName = target.Installation.Version.DisplayName;
                var installed = new HashSet<string>(
                    target.InstalledComponentIds ?? new string[0],
                    StringComparer.OrdinalIgnoreCase);
                if (!target.Installed)
                {
                    hasNewTarget = true;
                    changes.Add(new InstallerPendingChange
                    {
                        TargetName = targetName,
                        Description = "+ 安装 CDBox 到 " + targetName
                    });
                    continue;
                }
                if (!target.InstallationHealthy)
                {
                    hasRepairIssue = true;
                    changes.Add(new InstallerPendingChange
                    {
                        TargetName = targetName,
                        Description = "! 修复 " + targetName
                            + " 中缺失或损坏的 CDBox 文件"
                    });
                }
                if (!SameVersion(target.InstalledVersion,
                        manifest.ProductVersion))
                {
                    hasUpdate = true;
                    changes.Add(new InstallerPendingChange
                    {
                        TargetName = targetName,
                        Description = "↑ 更新 " + targetName + " 至 "
                            + manifest.ProductVersion
                    });
                }
                foreach (CDBoxComponentDefinition component in
                    manifest.Components.Where(x => !x.Required))
                {
                    bool before = installed.Contains(component.Id);
                    bool after = desiredSet.Contains(component.Id);
                    if (before == after) continue;
                    hasComponentChange = true;
                    changes.Add(new InstallerPendingChange
                    {
                        TargetName = targetName,
                        ComponentId = component.Id,
                        IsRemoval = before && !after,
                        Description = (after ? "+ 安装 " : "− 删除 ")
                            + component.DisplayName + "（" + targetName
                            + "）"
                    });
                }
            }

            if (repairRequested)
            {
                plan.Action = InstallerPlanAction.Repair;
                plan.PrimaryButtonText = "修复安装";
                plan.Summary = "将完整替换所选目标中的 CDBox 文件并重新注册加载项。";
                plan.CanExecute = true;
                if (changes.Count == 0)
                    changes.Add(new InstallerPendingChange
                    {
                        Description = "修复所选 AutoCAD 中的 CDBox 安装"
                    });
            }
            else if (hasNewTarget)
            {
                plan.Action = InstallerPlanAction.Install;
                plan.PrimaryButtonText = selected.Length > 1
                    ? "安装到 " + selected.Length + " 个 AutoCAD 版本"
                    : "安装 CDBox";
                plan.Summary = "将在所选 AutoCAD 中安装当前版本和组件组合。";
                plan.CanExecute = true;
            }
            else if (hasUpdate)
            {
                plan.Action = InstallerPlanAction.Update;
                plan.PrimaryButtonText = "更新至 "
                    + manifest.ProductVersion;
                plan.Summary = "将完整替换旧版本并保留当前所选组件。";
                plan.CanExecute = true;
            }
            else if (hasRepairIssue)
            {
                plan.Action = InstallerPlanAction.Repair;
                plan.PrimaryButtonText = "修复安装";
                plan.Summary = "检测到已安装文件缺失或损坏，将完整替换并重新注册加载项。";
                plan.CanExecute = true;
            }
            else if (hasComponentChange)
            {
                plan.Action = InstallerPlanAction.ApplyChanges;
                plan.PrimaryButtonText = "应用 " + changes.Count
                    + " 项更改";
                plan.Summary = "将按待应用列表增加或移除业务组件。";
                plan.CanExecute = true;
            }
            else
            {
                plan.Action = InstallerPlanAction.None;
                plan.PrimaryButtonText = "已是最新配置";
                plan.Summary = "所选目标的版本和组件组合均无需更改。";
            }
            plan.Changes = changes.ToArray();
            PopulateComponentStates(plan, manifest, selected,
                desiredSet, repairRequested);
            return plan;
        }

        private static void PopulateCompatibility(InstallerPlan plan,
            CDBoxComponentManifest manifest,
            IEnumerable<InstallerTargetSnapshot> targets,
            ISet<string> desired)
        {
            foreach (CDBoxComponentDefinition component in
                manifest.Components.Where(x => desired.Contains(x.Id)))
            {
                string[] incompatible = targets.Where(target =>
                        !IsCompatible(component, target.Installation,
                            manifest.ProductVersion))
                    .Select(target => target.Installation.Version.DisplayName)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (incompatible.Length > 0)
                    plan.IncompatibleTargets[component.Id] = incompatible;
            }
        }

        private static void PopulateComponentStates(InstallerPlan plan,
            CDBoxComponentManifest manifest,
            InstallerTargetSnapshot[] targets, ISet<string> desired,
            bool repairRequested)
        {
            foreach (CDBoxComponentDefinition component in manifest.Components)
            {
                InstallerComponentState state;
                if (plan.IncompatibleTargets.ContainsKey(component.Id))
                    state = InstallerComponentState.Incompatible;
                else if (component.Required)
                    state = repairRequested || targets.Any(target =>
                        target.Installed && !target.InstallationHealthy)
                        ? InstallerComponentState.RepairRequired
                        : InstallerComponentState.Required;
                else
                {
                    bool anyInstalled = targets.Any(target => target.Installed
                        && (target.InstalledComponentIds ?? new string[0])
                            .Contains(component.Id,
                                StringComparer.OrdinalIgnoreCase));
                    bool allInstalled = targets.Length > 0 && targets.All(
                        target => target.Installed
                            && (target.InstalledComponentIds
                                ?? new string[0]).Contains(component.Id,
                                    StringComparer.OrdinalIgnoreCase));
                    if ((repairRequested || targets.Any(target =>
                            target.Installed
                            && !target.InstallationHealthy))
                        && desired.Contains(component.Id)
                        && allInstalled)
                        state = InstallerComponentState.RepairRequired;
                    else if (desired.Contains(component.Id) && !allInstalled)
                        state = InstallerComponentState.PendingInstall;
                    else if (!desired.Contains(component.Id) && anyInstalled)
                        state = InstallerComponentState.PendingRemove;
                    else if (desired.Contains(component.Id)
                        && targets.Any(target => target.Installed
                            && !SameVersion(target.InstalledVersion,
                                manifest.ProductVersion)))
                        state = InstallerComponentState.UpdateAvailable;
                    else if (desired.Contains(component.Id) && allInstalled)
                        state = InstallerComponentState.Installed;
                    else
                        state = InstallerComponentState.NotInstalled;
                }
                plan.ComponentStates[component.Id] = state;
            }
        }

        private static bool IsCompatible(CDBoxComponentDefinition component,
            CadInstallation target, string coreVersion)
        {
            if (component == null || target == null) return false;
            int year = target.Version.Year;
            if (component.MinimumAutoCadYear > 0 && year > 0
                && year < component.MinimumAutoCadYear) return false;
            if (component.MaximumAutoCadYear > 0 && year > 0
                && year > component.MaximumAutoCadYear) return false;
            Version minimum;
            Version current;
            if (Version.TryParse(component.MinimumCoreVersion, out minimum)
                && Version.TryParse(coreVersion, out current)
                && current.CompareTo(minimum) < 0) return false;
            return true;
        }

        private static bool SameVersion(string left, string right)
        {
            Version a;
            Version b;
            if (Version.TryParse((left ?? string.Empty).Trim(), out a)
                && Version.TryParse((right ?? string.Empty).Trim(), out b))
                return a == b;
            return string.Equals((left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
