using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using CDBox.Shared.Components;

namespace CDBox.Setup
{
    internal static class BundleInstallService
    {
        private const string BundleName = "CDBox.bundle";

        public static bool IsAutoCadRunning(CadInstallation installation)
        {
            return IsAutoCadRunning(installation,
                GetRunningAutoCadExecutablePaths());
        }

        internal static bool IsAutoCadRunning(
            CadInstallation installation,
            IEnumerable<string> runningExecutablePaths)
        {
            if (installation == null || !installation.IsDetected)
                return false;
            string expected = NormalizeExecutablePath(
                installation.AcadExecutablePath);
            if (expected.Length == 0) return false;
            return (runningExecutablePaths ?? Enumerable.Empty<string>())
                .Select(NormalizeExecutablePath)
                .Any(path => path.Length > 0 && string.Equals(path,
                    expected, StringComparison.OrdinalIgnoreCase));
        }

        internal static string[] GetRunningAutoCadExecutablePaths()
        {
            var paths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            Process[] processes;
            try { processes = Process.GetProcessesByName("acad"); }
            catch { return new string[0]; }

            foreach (Process process in processes)
            {
                try
                {
                    string path = process.MainModule == null
                        ? string.Empty : process.MainModule.FileName;
                    path = NormalizeExecutablePath(path);
                    if (path.Length > 0) paths.Add(path);
                }
                catch
                {
                    // 安装器以管理员权限运行时通常可读取进程路径；
                    // 单个进程路径不可读时不得把所有 CAD 版本误判为运行中。
                }
                finally
                {
                    process.Dispose();
                }
            }
            return paths.ToArray();
        }

        public static bool IsInstalled(CadInstallation installation)
        {
            if (installation == null || !installation.IsDetected)
                return false;
            string bundle = GetBundlePath(installation.InstallDirectory);
            return File.Exists(Path.Combine(bundle, "Contents",
                "CDBox.dll"));
        }

        public static string GetInstalledBundlePath(
            CadInstallation installation)
        {
            return installation == null
                ? string.Empty
                : GetBundlePath(installation.InstallDirectory);
        }

        public static string ReadInstalledProductVersion(
            CadInstallation installation)
        {
            if (!IsInstalled(installation)) return string.Empty;
            string bundle = GetBundlePath(installation.InstallDirectory);
            try
            {
                string manifestPath = Path.Combine(bundle,
                    CDBoxComponentBundle.ManifestRelativePath);
                if (File.Exists(manifestPath))
                    return CDBoxComponentBundle.ReadManifest(bundle)
                        .ProductVersion ?? string.Empty;
            }
            catch
            {
                // Fall back to the loader's file version for legacy installs.
            }
            try
            {
                string loader = Path.Combine(bundle, "Contents",
                    "CDBox.dll");
                return FileVersionInfo.GetVersionInfo(loader)
                    .FileVersion ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string[] ReadInstalledComponentIds(
            CadInstallation installation,
            CDBoxComponentManifest availableManifest)
        {
            if (!IsInstalled(installation))
                return DefaultSelection(availableManifest);
            string bundle = GetBundlePath(installation.InstallDirectory);
            try
            {
                string manifestPath = Path.Combine(bundle,
                    CDBoxComponentBundle.ManifestRelativePath);
                CDBoxComponentManifest installedManifest =
                    File.Exists(manifestPath)
                        ? CDBoxComponentBundle.ReadManifest(bundle)
                        : availableManifest;
                string[] selected = CDBoxComponentBundle
                    .ReadInstalledSelection(bundle, installedManifest);
                return CDBoxComponentBundle.NormalizeSelection(
                    availableManifest, selected);
            }
            catch
            {
                return DefaultSelection(availableManifest);
            }
        }

        public static bool IsInstallationHealthy(
            CadInstallation installation,
            CDBoxComponentManifest availableManifest)
        {
            if (!IsInstalled(installation) || availableManifest == null)
                return false;
            try
            {
                string bundle = GetBundlePath(
                    installation.InstallDirectory);
                string[] selected = ReadInstalledComponentIds(
                    installation, availableManifest);
                CDBoxComponentBundle.ValidateInstalledBundle(bundle,
                    availableManifest, selected);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static IReadOnlyList<InstallResult> Install(
            string sourceBundleDirectory,
            IEnumerable<CadInstallation> selectedInstallations,
            CDBoxComponentManifest componentManifest,
            IEnumerable<string> selectedComponentIds,
            Action<string> progress)
        {
            InstallerPayload.ValidateBundle(sourceBundleDirectory);
            if (componentManifest == null)
                throw new InvalidOperationException("安装组件清单不能为空。");
            string[] components = CDBoxComponentBundle.NormalizeSelection(
                componentManifest, selectedComponentIds);

            CadInstallation[] targets = (selectedInstallations ?? Enumerable.Empty<CadInstallation>())
                .Where(item => item != null && item.IsDetected)
                .GroupBy(item => item.AcadExecutablePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();

            if (targets.Length == 0)
            {
                throw new InvalidOperationException("请至少选择一个已安装的 AutoCAD 版本。");
            }

            CadInstallation[] runningTargets = RunningTargets(targets);
            if (runningTargets.Length > 0)
            {
                throw new InvalidOperationException("检测到所选版本正在运行："
                    + string.Join("、", runningTargets.Select(item =>
                        item.Version.DisplayName))
                    + "。请关闭这些 AutoCAD 窗口后重试。");
            }

            PreserveManagementInstaller(progress);
            List<InstallResult> results = new List<InstallResult>();
            foreach (CadInstallation target in targets)
            {
                try
                {
                    progress?.Invoke("正在安装到 " + target.Version.DisplayName + "…");
                    InstallOne(sourceBundleDirectory, target,
                        componentManifest, components);
                    results.Add(new InstallResult(target, true, "安装成功"));
                    progress?.Invoke(target.Version.DisplayName + " 安装成功。");
                }
                catch (Exception ex)
                {
                    results.Add(new InstallResult(target, false, ex.Message));
                    progress?.Invoke(target.Version.DisplayName + " 安装失败：" + ex.Message);
                }
            }

            return results;
        }

        public static IReadOnlyList<InstallResult> Uninstall(
            IEnumerable<CadInstallation> selectedInstallations,
            Action<string> progress)
        {
            CadInstallation[] targets = NormalizeTargets(
                selectedInstallations);
            if (targets.Length == 0)
                throw new InvalidOperationException(
                    "请至少选择一个已安装的 AutoCAD 版本。");
            CadInstallation[] runningTargets = RunningTargets(targets);
            if (runningTargets.Length > 0)
                throw new InvalidOperationException(
                    "检测到所选版本正在运行："
                    + string.Join("、", runningTargets.Select(item =>
                        item.Version.DisplayName))
                    + "。请关闭这些 AutoCAD 窗口后重试。");

            var results = new List<InstallResult>();
            foreach (CadInstallation target in targets)
            {
                try
                {
                    progress?.Invoke("正在从 "
                        + target.Version.DisplayName + " 卸载 CDBox…");
                    UninstallOne(target);
                    results.Add(new InstallResult(target, true,
                        "卸载成功"));
                    progress?.Invoke(target.Version.DisplayName
                        + " 卸载成功。");
                }
                catch (Exception ex)
                {
                    results.Add(new InstallResult(target, false,
                        ex.Message));
                    progress?.Invoke(target.Version.DisplayName
                        + " 卸载失败：" + ex.Message);
                }
            }
            return results;
        }

        internal static void InstallBundleForValidation(string sourceBundleDirectory, string targetCadDirectory)
        {
            InstallerPayload.ValidateBundle(sourceBundleDirectory);
            CDBoxComponentManifest manifest =
                CDBoxComponentBundle.ReadManifest(sourceBundleDirectory);
            InstallBundleAtomically(sourceBundleDirectory,
                targetCadDirectory, manifest,
                manifest.Components.Where(item => item.Required
                    || item.DefaultSelected).Select(item => item.Id));
            DeleteBackupIfPresent(targetCadDirectory);
        }

        internal static void InstallBundleForValidation(
            string sourceBundleDirectory, string targetCadDirectory,
            IEnumerable<string> selectedComponentIds)
        {
            InstallerPayload.ValidateBundle(sourceBundleDirectory);
            CDBoxComponentManifest manifest =
                CDBoxComponentBundle.ReadManifest(sourceBundleDirectory);
            InstallBundleAtomically(sourceBundleDirectory,
                targetCadDirectory, manifest, selectedComponentIds);
            DeleteBackupIfPresent(targetCadDirectory);
        }

        internal static void UninstallBundleForValidation(
            string targetCadDirectory)
        {
            RemoveBundleDirectories(targetCadDirectory);
        }

        private static void InstallOne(string sourceBundleDirectory,
            CadInstallation installation,
            CDBoxComponentManifest componentManifest,
            IEnumerable<string> selectedComponentIds)
        {
            if (!File.Exists(installation.AcadExecutablePath))
            {
                throw new DirectoryNotFoundException("未找到 " + installation.AcadExecutablePath);
            }

            string targetBundle = GetBundlePath(
                installation.InstallDirectory);
            string backupBundle = GetBackupPath(
                installation.InstallDirectory);
            try
            {
                UnregisterDemandLoad(installation);
                string installedBundle = InstallBundleAtomically(
                    sourceBundleDirectory, installation.InstallDirectory,
                    componentManifest, selectedComponentIds);
                RegisterDemandLoad(installation, Path.Combine(installedBundle, "Contents", "CDBox.dll"));
                DeleteBackupIfPresent(installation.InstallDirectory);
            }
            catch
            {
                RestorePreviousInstall(installation, targetBundle,
                    backupBundle);
                throw;
            }
        }

        private static void UninstallOne(CadInstallation installation)
        {
            UnregisterDemandLoad(installation);
            RemoveBundleDirectories(installation.InstallDirectory);
        }

        private static string InstallBundleAtomically(
            string sourceBundleDirectory, string targetCadDirectory,
            CDBoxComponentManifest componentManifest,
            IEnumerable<string> selectedComponentIds)
        {
            if (string.IsNullOrWhiteSpace(targetCadDirectory) || !Directory.Exists(targetCadDirectory))
            {
                throw new DirectoryNotFoundException("AutoCAD 安装目录不存在：" + targetCadDirectory);
            }

            string targetBundle = Path.Combine(targetCadDirectory, BundleName);
            string stagingBundle = Path.Combine(targetCadDirectory, BundleName + ".installing." + Guid.NewGuid().ToString("N"));
            string backupBundle = GetBackupPath(targetCadDirectory);

            CleanupStagingDirectories(targetCadDirectory);
            if (Directory.Exists(stagingBundle))
                Directory.Delete(stagingBundle, true);

            CopyDirectory(sourceBundleDirectory, stagingBundle);
            CDBoxComponentBundle.ApplySelection(stagingBundle,
                componentManifest, selectedComponentIds);
            CDBoxComponentBundle.ValidateInstalledBundle(stagingBundle,
                componentManifest);

            if (Directory.Exists(backupBundle))
            {
                Directory.Delete(backupBundle, true);
            }

            bool movedExisting = false;
            try
            {
                if (Directory.Exists(targetBundle))
                {
                    Directory.Move(targetBundle, backupBundle);
                    movedExisting = true;
                }

                Directory.Move(stagingBundle, targetBundle);
                return targetBundle;
            }
            catch
            {
                if (Directory.Exists(stagingBundle))
                {
                    TryDeleteDirectory(stagingBundle);
                }

                if (movedExisting && !Directory.Exists(targetBundle) && Directory.Exists(backupBundle))
                {
                    Directory.Move(backupBundle, targetBundle);
                }

                throw;
            }
        }

        private static void RegisterDemandLoad(CadInstallation installation, string loaderPath)
        {
            string applicationPath = installation.CurrentUserProductRegistryPath + @"\Applications\CDBox";
            using (RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
            using (RegistryKey application = root.CreateSubKey(applicationPath, true))
            {
                if (application == null)
                {
                    throw new InvalidOperationException("无法写入 AutoCAD 插件加载配置。");
                }

                application.SetValue("DESCRIPTION", "CDBox", RegistryValueKind.String);
                application.SetValue("LOADCTRLS", 2, RegistryValueKind.DWord);
                application.SetValue("LOADER", loaderPath, RegistryValueKind.String);
                application.SetValue("MANAGED", 1, RegistryValueKind.DWord);
            }
        }

        private static void UnregisterDemandLoad(
            CadInstallation installation)
        {
            string applicationsPath = installation
                .CurrentUserProductRegistryPath + @"\Applications";
            using (RegistryKey root = RegistryKey.OpenBaseKey(
                RegistryHive.CurrentUser, RegistryView.Default))
            using (RegistryKey applications = root.OpenSubKey(
                applicationsPath, true))
            {
                if (applications == null) return;
                try { applications.DeleteSubKeyTree("CDBox", false); }
                catch (ArgumentException) { }
            }
        }

        private static void RestorePreviousInstall(
            CadInstallation installation, string targetBundle,
            string backupBundle)
        {
            try
            {
                if (Directory.Exists(backupBundle))
                {
                    if (Directory.Exists(targetBundle))
                        Directory.Delete(targetBundle, true);
                    Directory.Move(backupBundle, targetBundle);
                }
                string loader = Path.Combine(targetBundle, "Contents",
                    "CDBox.dll");
                if (File.Exists(loader))
                    RegisterDemandLoad(installation, loader);
            }
            catch
            {
                // Keep the original exception. A retained backup is recoverable.
            }
        }

        private static void DeleteBackupIfPresent(string targetCadDirectory)
        {
            string backupBundle = GetBackupPath(targetCadDirectory);
            if (Directory.Exists(backupBundle))
            {
                Directory.Delete(backupBundle, true);
            }
        }

        private static string GetBackupPath(string targetCadDirectory)
        {
            return Path.Combine(targetCadDirectory, BundleName + ".backup");
        }

        private static string GetBundlePath(string targetCadDirectory)
        {
            return Path.Combine(targetCadDirectory, BundleName);
        }

        private static CadInstallation[] NormalizeTargets(
            IEnumerable<CadInstallation> installations)
        {
            return (installations ?? Enumerable.Empty<CadInstallation>())
                .Where(item => item != null && item.IsDetected)
                .GroupBy(item => item.AcadExecutablePath,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()).ToArray();
        }

        private static CadInstallation[] RunningTargets(
            IEnumerable<CadInstallation> installations)
        {
            string[] runningPaths = GetRunningAutoCadExecutablePaths();
            return NormalizeTargets(installations).Where(item =>
                IsAutoCadRunning(item, runningPaths)).ToArray();
        }

        private static string NormalizeExecutablePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try
            {
                return Path.GetFullPath(path.Trim().Trim('"')).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim().Trim('"');
            }
        }

        private static string[] DefaultSelection(
            CDBoxComponentManifest manifest)
        {
            if (manifest == null) return new string[0];
            return CDBoxComponentBundle.NormalizeSelection(manifest,
                manifest.Components.Where(item => item.Required
                    || item.DefaultSelected).Select(item => item.Id));
        }

        private static void RemoveBundleDirectories(
            string targetCadDirectory)
        {
            string targetBundle = GetBundlePath(targetCadDirectory);
            string backupBundle = GetBackupPath(targetCadDirectory);
            if (Directory.Exists(targetBundle))
                Directory.Delete(targetBundle, true);
            if (Directory.Exists(backupBundle))
                Directory.Delete(backupBundle, true);
            CleanupStagingDirectories(targetCadDirectory);
        }

        private static void CleanupStagingDirectories(
            string targetCadDirectory)
        {
            if (string.IsNullOrWhiteSpace(targetCadDirectory)
                || !Directory.Exists(targetCadDirectory)) return;
            foreach (string pattern in new[]
            {
                BundleName + ".installing.*",
                BundleName + ".backup.*",
                BundleName + ".new.*"
            })
                foreach (string path in Directory.GetDirectories(
                    targetCadDirectory, pattern,
                    SearchOption.TopDirectoryOnly))
                    TryDeleteDirectory(path);
            TryDeleteDirectory(Path.Combine(targetCadDirectory,
                BundleName + ".update-backup"));
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            string sourceRoot = Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string destinationRoot = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (destinationRoot.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("安装目标不能位于安装源目录内。");
            }

            Directory.CreateDirectory(destinationDirectory);
            foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = directory.Substring(sourceRoot.Length);
                Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
            }

            foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(sourceRoot.Length);
                string destinationFile = Path.Combine(destinationDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                File.Copy(file, destinationFile, true);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch
            {
                // Best effort cleanup only.
            }
        }

        private static void PreserveManagementInstaller(Action<string> progress)
        {
            string source = Assembly.GetExecutingAssembly().Location;
            string destination = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
                "CDBox", "CDBox组件管理器.exe");
            PreserveInstallerCopy(source, destination);
            progress?.Invoke("已安装 CDBox 组件管理器，可在插件设置中再次打开。");
        }

        internal static void PreserveInstallerCopy(string source, string destination)
        {
            source = Path.GetFullPath(source);
            destination = Path.GetFullPath(destination);
            if (!File.Exists(source)) throw new FileNotFoundException(
                "未找到组件管理器安装源。", source);
            if (string.Equals(source, destination,
                StringComparison.OrdinalIgnoreCase)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string staging = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(source, staging);
                if (!SameFileContent(source, staging))
                    throw new IOException("组件管理器复制校验失败。");
                // An already-running installed manager need not be replaced
                // when its bytes match the installer being run.
                if (File.Exists(destination) && SameFileContent(staging, destination))
                    return;
                if (File.Exists(destination)) File.Replace(staging, destination, null);
                else File.Move(staging, destination);
            }
            finally
            {
                if (File.Exists(staging)) File.Delete(staging);
            }
        }

        private static bool SameFileContent(string first, string second)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
            using (var a = File.OpenRead(first))
            using (var b = File.OpenRead(second))
                return a.Length == b.Length && hash.ComputeHash(a)
                    .SequenceEqual(hash.ComputeHash(b));
        }
    }
}
