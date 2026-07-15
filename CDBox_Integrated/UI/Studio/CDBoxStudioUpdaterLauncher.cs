using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using TCPipeAutoDraw.Core.Startup;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioUpdaterLauncher
    {
        private const string UpdaterFileName = "CDBoxUpdater.exe";

        public static CDBoxStudioUpdaterLaunchResult PrepareAndLaunch(CDBoxStudioUpdateDownloadResult download)
        {
            return PrepareAndLaunch(download, string.Empty);
        }

        public static CDBoxStudioUpdaterLaunchResult PrepareAndLaunch(CDBoxStudioUpdateDownloadResult download, string preferredUpdaterPath)
        {
            var result = new CDBoxStudioUpdaterLaunchResult();

            try
            {
                if (download == null || !download.Success || !download.Verified)
                {
                    throw new InvalidOperationException("更新包尚未下载并通过 SHA256 校验，不能启动更新器。");
                }

                if (string.IsNullOrWhiteSpace(download.FilePath) || !File.Exists(download.FilePath))
                {
                    throw new FileNotFoundException("未找到已校验的更新包。", download == null ? string.Empty : download.FilePath);
                }

                string targetBundlePath = ResolveTargetBundlePath();
                if (string.IsNullOrWhiteSpace(targetBundlePath))
                {
                    throw new InvalidOperationException("无法确定当前 CDBox.bundle 目标目录。");
                }

                EnsureNoUpdaterIsRunning();

                string sourceUpdater = !string.IsNullOrWhiteSpace(preferredUpdaterPath) && File.Exists(preferredUpdaterPath)
                    ? Path.GetFullPath(preferredUpdaterPath)
                    : FindBundledUpdater();
                if (string.IsNullOrWhiteSpace(sourceUpdater) || !File.Exists(sourceUpdater))
                {
                    throw new FileNotFoundException("未找到 CDBoxUpdater.exe。请确认发布包包含 Contents\\Updater\\CDBoxUpdater.exe。", sourceUpdater ?? string.Empty);
                }

                string updaterDirectory = Path.Combine(
                    CDBoxStudioLogger.LogDirectory,
                    "Updater",
                    Math.Max(0, download.VersionCode).ToString());
                Directory.CreateDirectory(updaterDirectory);

                string updaterPath = CopyUpdaterOutsideBundle(sourceUpdater, updaterDirectory);
                string packageDirectory = Path.GetDirectoryName(download.FilePath);
                if (string.IsNullOrWhiteSpace(packageDirectory)) packageDirectory = CDBoxStudioLogger.LogDirectory;

                string pendingPath = Path.Combine(packageDirectory, "pending-update.json");
                string updaterLogPath = Path.Combine(CDBoxStudioLogger.LogDirectory, "updater.log");
                string lastResultPath = Path.Combine(CDBoxStudioLogger.LogDirectory, "last-update-result.json");
                string workDirectory = Path.Combine(packageDirectory, "installer-work");

                Process current = Process.GetCurrentProcess();
                string processStartTimeUtc = string.Empty;
                try { processStartTimeUtc = current.StartTime.ToUniversalTime().ToString("o"); }
                catch { }

                var pending = new CDBoxStudioPendingUpdate
                {
                    SchemaVersion = 1,
                    Channel = string.IsNullOrWhiteSpace(download.Channel) ? CDBoxStudioUpdateService.DefaultChannel : download.Channel,
                    CurrentVersion = download.CurrentVersion,
                    TargetVersion = download.LatestVersion,
                    VersionCode = download.VersionCode,
                    PackagePath = Path.GetFullPath(download.FilePath),
                    PackageFileName = download.PackageFileName,
                    PackageSha256 = download.Sha256Expected,
                    TargetBundlePath = Path.GetFullPath(targetBundlePath),
                    AutoCadProcessId = current.Id,
                    AutoCadProcessName = current.ProcessName,
                    AutoCadProcessStartTimeUtc = processStartTimeUtc,
                    CreatedAtUtc = DateTime.UtcNow.ToString("o"),
                    WorkDirectory = Path.GetFullPath(workDirectory),
                    UpdaterLogPath = Path.GetFullPath(updaterLogPath),
                    LastResultPath = Path.GetFullPath(lastResultPath),
                    StudioLogPath = Path.GetFullPath(CDBoxStudioLogger.LogFilePath),
                    ProtectedUserConfigDirectory = Path.GetFullPath(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "CDBox"))
                };

                WritePendingUpdateAtomically(pendingPath, pending);
                result.Prepared = true;
                result.PendingUpdatePath = pendingPath;
                result.UpdaterPath = updaterPath;
                result.TargetBundlePath = pending.TargetBundlePath;
                result.UpdaterLogPath = updaterLogPath;
                result.LastResultPath = lastResultPath;

                string arguments = "--pending " + Quote(pendingPath);
                var startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = arguments,
                    WorkingDirectory = updaterDirectory,
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                Process started = Process.Start(startInfo);
                if (started == null)
                {
                    throw new InvalidOperationException("系统未能启动 CDBoxUpdater.exe。");
                }

                result.Started = true;
                result.CommandLine = Quote(updaterPath) + " " + arguments;
                CDBoxStudioLogger.Info("已生成 pending-update.json 并启动独立更新器。Pending=" + pendingPath
                    + ", Updater=" + updaterPath
                    + ", Target=" + pending.TargetBundlePath
                    + ", AutoCAD PID=" + pending.AutoCadProcessId);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                CDBoxStudioLogger.Error("准备或启动 CDBoxUpdater.exe 失败。", ex);
            }

            return result;
        }

        private static void EnsureNoUpdaterIsRunning()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("CDBoxUpdater");
                if (processes != null && processes.Length > 0)
                {
                    foreach (Process process in processes) process.Dispose();
                    throw new InvalidOperationException("已有 CDBoxUpdater 正在等待或安装更新，请先关闭 AutoCAD 并等待当前更新完成。");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Warn("检查运行中的 CDBoxUpdater 失败：" + ex.Message);
            }
        }

        private static string ResolveTargetBundlePath()
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                DirectoryInfo current = string.IsNullOrWhiteSpace(assemblyPath)
                    ? null
                    : new FileInfo(assemblyPath).Directory;

                for (int i = 0; i < 8 && current != null; i++)
                {
                    if (string.Equals(current.Name, "CDBox.bundle", StringComparison.OrdinalIgnoreCase))
                    {
                        return current.FullName;
                    }
                    current = current.Parent;
                }
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Warn("从当前程序集位置解析 CDBox.bundle 失败：" + ex.Message);
            }

            try
            {
                return CDBoxInstaller.GetInstallRoot();
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Warn("从安装器解析 CDBox.bundle 失败：" + ex.Message);
                return string.Empty;
            }
        }

        private static string FindBundledUpdater()
        {
            string assemblyDirectory = string.Empty;
            try { assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
            catch { }

            string bundlePath = ResolveTargetBundlePath();
            string[] candidates =
            {
                Path.Combine(assemblyDirectory ?? string.Empty, "Updater", UpdaterFileName),
                Path.Combine(assemblyDirectory ?? string.Empty, UpdaterFileName),
                Path.Combine(bundlePath ?? string.Empty, "Contents", "Updater", UpdaterFileName),
                Path.Combine(bundlePath ?? string.Empty, "Contents", UpdaterFileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "Updater", UpdaterFileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, UpdaterFileName)
            };

            foreach (string candidate in candidates)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate)) return Path.GetFullPath(candidate);
                }
                catch { }
            }

            return string.Empty;
        }

        private static string CopyUpdaterOutsideBundle(string sourceUpdater, string destinationDirectory)
        {
            string sourceDirectory = Path.GetDirectoryName(sourceUpdater);
            if (string.IsNullOrWhiteSpace(sourceDirectory)) throw new DirectoryNotFoundException("更新器源目录不存在。");

            Directory.CreateDirectory(destinationDirectory);
            foreach (string file in Directory.GetFiles(sourceDirectory, "CDBoxUpdater.*", SearchOption.TopDirectoryOnly))
            {
                string destination = Path.Combine(destinationDirectory, Path.GetFileName(file));
                File.Copy(file, destination, true);
            }

            string updaterPath = Path.Combine(destinationDirectory, UpdaterFileName);
            if (!File.Exists(updaterPath)) throw new FileNotFoundException("复制更新器失败。", updaterPath);
            return updaterPath;
        }

        private static void WritePendingUpdateAtomically(string path, CDBoxStudioPendingUpdate pending)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
            string json = serializer.Serialize(pending);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, json, new UTF8Encoding(false));

            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
