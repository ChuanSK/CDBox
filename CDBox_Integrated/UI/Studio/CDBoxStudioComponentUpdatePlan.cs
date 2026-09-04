using System;
using System.IO;
using CDBox.Shared.Components;
using TCPipeAutoDraw.Core.Startup;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioComponentUpdatePlan
    {
        public string BundleRoot { get; set; }
        public string[] SelectedComponentIds { get; set; }
        public string[] SelectedComponentNames { get; set; }
        public string Warning { get; set; }
        public bool InstallationDetected { get; set; }

        public CDBoxStudioComponentUpdatePlan()
        {
            BundleRoot = string.Empty;
            SelectedComponentIds = new string[0];
            SelectedComponentNames = new string[0];
            Warning = string.Empty;
        }

        public string Summary
        {
            get
            {
                if (!InstallationDetected) return "未检测到已安装组件";
                return SelectedComponentNames.Length == 0
                    ? "基础组件"
                    : string.Join("、", SelectedComponentNames);
            }
        }

        public static CDBoxStudioComponentUpdatePlan Capture()
        {
            string root;
            try { root = CDBoxInstallationLocator.GetInstallRoot(); }
            catch { root = string.Empty; }
            return Capture(root);
        }

        public static CDBoxStudioComponentUpdatePlan Capture(string bundleRoot)
        {
            var plan = new CDBoxStudioComponentUpdatePlan
            {
                BundleRoot = bundleRoot ?? string.Empty
            };
            try
            {
                if (string.IsNullOrWhiteSpace(bundleRoot)
                    || !Directory.Exists(bundleRoot)
                    || !File.Exists(Path.Combine(bundleRoot, "Contents",
                        "CDBox.dll")))
                    return plan;

                plan.InstallationDetected = true;
                string manifestPath = Path.Combine(bundleRoot,
                    CDBoxComponentBundle.ManifestRelativePath);
                CDBoxComponentManifest manifest;
                if (File.Exists(manifestPath))
                {
                    manifest = CDBoxComponentBundle.ReadManifest(bundleRoot);
                }
                else
                {
                    manifest = CDBoxComponentCatalog.CreateManifest(
                        CDBoxStudioUpdateService.CurrentVersion);
                    plan.Warning = "当前安装来自旧版且没有组件清单，已根据现有文件识别安装组合。";
                }

                CDBoxInstalledComponentSnapshot snapshot =
                    CDBoxComponentBundle.InspectInstalledBundle(bundleRoot,
                        manifest);
                plan.SelectedComponentIds = snapshot.SelectedComponentIds
                    ?? new string[0];
                plan.SelectedComponentNames = snapshot.SelectedComponentNames
                    ?? new string[0];
                if (!string.IsNullOrWhiteSpace(snapshot.Warning))
                    plan.Warning = snapshot.Warning;
            }
            catch (Exception ex)
            {
                plan.Warning = "读取当前组件组合失败：" + ex.Message;
                plan.SelectedComponentIds = new[] { CDBoxComponentIds.Base };
                plan.SelectedComponentNames = new[] { "基础组件" };
            }
            return plan;
        }
    }
}
