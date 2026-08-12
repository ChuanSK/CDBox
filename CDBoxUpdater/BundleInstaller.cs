using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CDBox.Shared;

namespace CDBoxUpdater
{
    internal static class BundleInstaller
    {
        private const int FileOperationRetries = 12;
        private const int RetryDelayMilliseconds = 500;

        public static BundleInstallOutcome Install(PendingUpdateManifest pending)
        {
            return Install(pending, null);
        }

        public static BundleInstallOutcome Install(PendingUpdateManifest pending, Action<int, string> progress)
        {
            if (pending == null) throw new ArgumentNullException("pending");
            Report(progress, 25, "正在校验更新包…");
            ValidatePending(pending);

            var outcome = new BundleInstallOutcome();
            string packagePath = Path.GetFullPath(pending.PackagePath);
            string targetBundle = NormalizeDirectoryPath(pending.TargetBundlePath);
            string targetParent = Directory.GetParent(targetBundle) == null
                ? string.Empty
                : Directory.GetParent(targetBundle).FullName;

            if (string.IsNullOrWhiteSpace(targetParent)) throw new InvalidOperationException("无法确定目标 bundle 的父目录。");

            UpdaterLogger.Info("开始二次校验更新包 SHA256。Package=" + packagePath);
            string actualHash = ComputeSha256(packagePath);
            outcome.PackageSha256Actual = actualHash;
            if (!SameHash(actualHash, pending.PackageSha256))
            {
                throw new InvalidOperationException("更新包 SHA256 二次校验失败。期望 " + pending.PackageSha256 + "，实际 " + actualHash + "。");
            }
            UpdaterLogger.Info("更新包 SHA256 二次校验通过。Hash=" + actualHash);
            Report(progress, 40, "更新包校验完成，正在解压…");

            string workRoot = string.IsNullOrWhiteSpace(pending.WorkDirectory)
                ? Path.Combine(Path.GetDirectoryName(packagePath) ?? Path.GetTempPath(), "installer-work")
                : Path.GetFullPath(pending.WorkDirectory);
            string extractionRoot = Path.Combine(workRoot, "extracted-" + Guid.NewGuid().ToString("N"));
            string localNewBundle = Path.Combine(targetParent, "CDBox.bundle.new." + Guid.NewGuid().ToString("N"));
            string backupBundle = Path.Combine(targetParent, "CDBox.bundle.backup." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "." + Guid.NewGuid().ToString("N").Substring(0, 8));
            bool oldBundleMoved = false;

            try
            {
                Directory.CreateDirectory(workRoot);
                Directory.CreateDirectory(extractionRoot);
                ExtractZipSecure(packagePath, extractionRoot);

                string extractedBundle = FindExtractedBundle(extractionRoot);
                ValidateBundle(extractedBundle);
                UpdaterLogger.Info("更新包解压和必要文件校验通过。Bundle=" + extractedBundle);
                Report(progress, 58, "正在准备新版本文件…");

                DeleteDirectoryIfExists(localNewBundle);
                CopyDirectory(extractedBundle, localNewBundle);
                ValidateBundle(localNewBundle);
                UpdaterLogger.Info("目标磁盘暂存 bundle 校验通过。Staging=" + localNewBundle);
                Report(progress, 74, "正在备份当前版本…");

                if (Directory.Exists(targetBundle))
                {
                    MoveDirectoryWithRetry(targetBundle, backupBundle);
                    oldBundleMoved = true;
                    outcome.BackupBundlePath = backupBundle;
                    UpdaterLogger.Info("旧 bundle 已备份。Backup=" + backupBundle);
                }

                try
                {
                    Report(progress, 88, "正在替换 CDBox 文件…");
                    MoveDirectoryWithRetry(localNewBundle, targetBundle);
                    ValidateBundle(targetBundle);
                    Report(progress, 98, "正在完成最终校验…");
                    int cleaned = 0;
                    try
                    {
                        cleaned = CleanupObsoleteInstallArtifacts(
                            targetParent, outcome.BackupBundlePath);
                    }
                    catch (Exception cleanupEx)
                    {
                        UpdaterLogger.Warn("清理旧更新备份失败，不影响本次更新："
                            + cleanupEx.Message);
                    }
                    outcome.Success = true;
                    outcome.Message = "CDBox.bundle 已成功替换。旧版本备份保留在："
                        + (string.IsNullOrWhiteSpace(outcome.BackupBundlePath)
                            ? "无" : outcome.BackupBundlePath)
                        + (cleaned > 0
                            ? "；已清理旧备份/临时目录 " + cleaned + " 个。"
                            : string.Empty);
                    UpdaterLogger.Info(outcome.Message);
                    return outcome;
                }
                catch (Exception replaceEx)
                {
                    UpdaterLogger.Error("替换或替换后校验失败，开始自动回滚。", replaceEx);
                    outcome.RolledBack = Rollback(targetBundle, backupBundle, oldBundleMoved);
                    throw new BundleInstallException(
                        "替换 CDBox.bundle 失败。" + (outcome.RolledBack ? "已自动回滚旧版本。" : "自动回滚失败，请查看 updater.log。"),
                        replaceEx,
                        outcome.RolledBack,
                        outcome.BackupBundlePath,
                        outcome.PackageSha256Actual);
                }
            }
            finally
            {
                try { DeleteDirectoryIfExists(extractionRoot); }
                catch (Exception ex) { UpdaterLogger.Warn("清理解压暂存目录失败：" + ex.Message); }

                try
                {
                    if (Directory.Exists(localNewBundle)) DeleteDirectoryIfExists(localNewBundle);
                }
                catch (Exception ex)
                {
                    UpdaterLogger.Warn("清理目标磁盘暂存 bundle 失败：" + ex.Message);
                }
            }
        }

        private static void Report(Action<int, string> progress, int percent, string message)
        {
            if (progress == null) return;
            try { progress(percent, message); } catch { }
        }

        public static bool CanWriteTarget(string targetBundlePath)
        {
            string target = NormalizeDirectoryPath(targetBundlePath);
            DirectoryInfo parentInfo = Directory.GetParent(target);
            if (parentInfo == null) return false;
            string parent = parentInfo.FullName;

            try
            {
                Directory.CreateDirectory(parent);
                ProbeWrite(parent);
                if (Directory.Exists(target)) ProbeWrite(target);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static void ProbeWrite(string directory)
        {
            string probe = Path.Combine(directory, ".cdbox-updater-write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(probe, "test", Encoding.ASCII);
            File.Delete(probe);
        }

        private static void ValidatePending(PendingUpdateManifest pending)
        {
            if (pending.SchemaVersion != 1) throw new InvalidOperationException("不支持的 pending-update.json 版本：" + pending.SchemaVersion);
            if (string.IsNullOrWhiteSpace(pending.PackagePath) || !File.Exists(pending.PackagePath)) throw new FileNotFoundException("更新包不存在。", pending.PackagePath ?? string.Empty);
            if (!IsUsableSha256(pending.PackageSha256)) throw new InvalidOperationException("pending-update.json 中的 SHA256 无效。");
            if (string.IsNullOrWhiteSpace(pending.TargetBundlePath)) throw new InvalidOperationException("pending-update.json 未提供 TargetBundlePath。");

            string target = NormalizeDirectoryPath(pending.TargetBundlePath);
            string leaf = new DirectoryInfo(target).Name;
            if (!leaf.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("为安全起见，更新器只允许替换 .bundle 目录。当前目标：" + target);
            }
        }

        private static void ExtractZipSecure(string packagePath, string destinationRoot)
        {
            string fullDestinationRoot = NormalizeDirectoryPath(destinationRoot) + Path.DirectorySeparatorChar;
            using (FileStream stream = File.OpenRead(packagePath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, false, Encoding.UTF8))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string entryName = (entry.FullName ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                    if (string.IsNullOrWhiteSpace(entryName)) continue;

                    string destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entryName));
                    if (!destinationPath.StartsWith(fullDestinationRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("更新包包含越界路径，已拒绝解压：" + entry.FullName);
                    }

                    bool isDirectory = string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal);
                    if (isDirectory)
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    string parent = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                    using (Stream input = entry.Open())
                    using (FileStream output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        input.CopyTo(output);
                    }
                }
            }
        }

        private static string FindExtractedBundle(string extractionRoot)
        {
            string direct = Path.Combine(extractionRoot, "CDBox.bundle");
            if (Directory.Exists(direct)) return direct;

            if (File.Exists(Path.Combine(extractionRoot, "PackageContents.xml")) && Directory.Exists(Path.Combine(extractionRoot, "Contents")))
            {
                return extractionRoot;
            }

            foreach (string directory in Directory.GetDirectories(extractionRoot, "*.bundle", SearchOption.AllDirectories))
            {
                if (File.Exists(Path.Combine(directory, "PackageContents.xml")) && Directory.Exists(Path.Combine(directory, "Contents")))
                {
                    return directory;
                }
            }

            throw new InvalidOperationException("更新包中未找到完整 CDBox.bundle。压缩包应包含 CDBox.bundle\\PackageContents.xml 和 CDBox.bundle\\Contents。");
        }

        private static void ValidateBundle(string bundleRoot)
        {
            ValidateBundle(bundleRoot, true);
        }

        private static void ValidateBundle(string bundleRoot, bool requireUpdater)
        {
            if (string.IsNullOrWhiteSpace(bundleRoot) || !Directory.Exists(bundleRoot)) throw new DirectoryNotFoundException("bundle 目录不存在：" + bundleRoot);

            var missing = new List<string>();
            RequireFile(bundleRoot, "PackageContents.xml", missing);
            RequireFile(bundleRoot, Path.Combine("Contents", "CDBox.dll"), missing);
            foreach (string dependency in CDBoxRequiredRuntimeFiles.ManagedDependencies)
            {
                RequireFile(bundleRoot, Path.Combine("Contents", dependency), missing);
            }

            string contents = Path.Combine(bundleRoot, "Contents");
            RequireFile(bundleRoot, Path.Combine("Contents", CDBoxRequiredRuntimeFiles.WebView2LoaderRelativePath), missing);
            if (requireUpdater) RequireFile(bundleRoot, Path.Combine("Contents", CDBoxRequiredRuntimeFiles.UpdaterRelativePath), missing);

            if (missing.Count > 0)
            {
                throw new InvalidOperationException("bundle 必要文件校验失败，缺少：" + string.Join("；", missing.ToArray()));
            }
        }

        private static void RequireFile(string bundleRoot, string relativePath, IList<string> missing)
        {
            if (!File.Exists(Path.Combine(bundleRoot, relativePath))) missing.Add(relativePath);
        }

        private static bool Rollback(string targetBundle, string backupBundle, bool oldBundleMoved)
        {
            if (!oldBundleMoved || string.IsNullOrWhiteSpace(backupBundle) || !Directory.Exists(backupBundle)) return false;

            try
            {
                if (Directory.Exists(targetBundle)) DeleteDirectoryIfExists(targetBundle);
                MoveDirectoryWithRetry(backupBundle, targetBundle);
                ValidateBundle(targetBundle, false);
                UpdaterLogger.Info("自动回滚完成。Target=" + targetBundle);
                return true;
            }
            catch (Exception ex)
            {
                UpdaterLogger.Error("自动回滚失败。Backup=" + backupBundle + ", Target=" + targetBundle, ex);
                return false;
            }
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                string destination = Path.Combine(destinationDirectory, Path.GetFileName(file));
                CopyFileWithRetry(file, destination);
            }

            foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                string destination = Path.Combine(destinationDirectory, Path.GetFileName(directory));
                CopyDirectory(directory, destination);
            }
        }

        private static void CopyFileWithRetry(string source, string destination)
        {
            ExecuteWithRetry(delegate
            {
                string parent = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                File.Copy(source, destination, true);
            }, "复制文件 " + source + " -> " + destination);
        }

        private static void MoveDirectoryWithRetry(string source, string destination)
        {
            ExecuteWithRetry(delegate
            {
                if (Directory.Exists(destination)) throw new IOException("目标目录已存在：" + destination);
                Directory.Move(source, destination);
            }, "移动目录 " + source + " -> " + destination);
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            ExecuteWithRetry(delegate
            {
                if (!Directory.Exists(path)) return;
                NormalizeAttributes(path);
                Directory.Delete(path, true);
            }, "删除目录 " + path);
        }

        private static int CleanupObsoleteInstallArtifacts(
            string targetParent, string retainedBackup)
        {
            if (string.IsNullOrWhiteSpace(targetParent)
                || !Directory.Exists(targetParent)) return 0;

            string parent = NormalizeDirectoryPath(targetParent);
            string retained = string.IsNullOrWhiteSpace(retainedBackup)
                ? string.Empty : NormalizeDirectoryPath(retainedBackup);
            int cleaned = 0;
            string[] patterns =
            {
                "CDBox.bundle.backup.*",
                "CDBox.bundle.update-backup",
                "CDBox.bundle.new.*"
            };
            foreach (string pattern in patterns)
            {
                string[] directories;
                try
                {
                    directories = Directory.GetDirectories(parent, pattern,
                        SearchOption.TopDirectoryOnly);
                }
                catch (Exception ex)
                {
                    UpdaterLogger.Warn("扫描更新备份目录失败：" + ex.Message);
                    continue;
                }

                foreach (string candidate in directories)
                {
                    string full = NormalizeDirectoryPath(candidate);
                    DirectoryInfo owner = Directory.GetParent(full);
                    if (owner == null
                        || !string.Equals(owner.FullName, parent,
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(full, retained,
                            StringComparison.OrdinalIgnoreCase))
                        continue;
                    try
                    {
                        DeleteDirectoryIfExists(full);
                        cleaned++;
                        UpdaterLogger.Info("已清理旧更新备份/临时目录："
                            + full);
                    }
                    catch (Exception ex)
                    {
                        UpdaterLogger.Warn("清理旧更新备份/临时目录失败："
                            + full + "，" + ex.Message);
                    }
                }
            }
            return cleaned;
        }

        private static void NormalizeAttributes(string root)
        {
            try
            {
                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
                foreach (string directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)) File.SetAttributes(directory, FileAttributes.Directory);
                File.SetAttributes(root, FileAttributes.Directory);
            }
            catch
            {
            }
        }

        private static void ExecuteWithRetry(Action action, string operation)
        {
            Exception last = null;
            for (int i = 1; i <= FileOperationRetries; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    if (i == FileOperationRetries) break;
                    System.Threading.Thread.Sleep(RetryDelayMilliseconds);
                }
            }
            throw new IOException(operation + "失败。", last);
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        private static bool SameHash(string left, string right)
        {
            return string.Equals(NormalizeSha256(left), NormalizeSha256(right), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUsableSha256(string value)
        {
            string normalized = NormalizeSha256(value);
            if (normalized.Length != 64) return false;
            foreach (char c in normalized)
            {
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!isHex) return false;
            }
            return true;
        }

        private static string NormalizeSha256(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string NormalizeDirectoryPath(string path)
        {
            return Path.GetFullPath(path ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
