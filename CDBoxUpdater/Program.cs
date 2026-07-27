using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;

namespace CDBoxUpdater
{
    internal static class Program
    {
        private const int WaitLogIntervalSeconds = 30;

        [STAThread]
        private static int Main(string[] args)
        {
            string pendingPath = GetArgument(args, "--pending");
            bool elevatedContinuation = HasFlag(args, "--elevated");
            UpdateProgressWindowHost progressWindow = null;
            bool autoCadExitConfirmed = false;
            var result = new UpdateResultRecord
            {
                PendingUpdatePath = pendingPath ?? string.Empty
            };

            try
            {
                if (string.IsNullOrWhiteSpace(pendingPath))
                {
                    throw new InvalidOperationException("缺少 --pending 参数。");
                }

                pendingPath = Path.GetFullPath(pendingPath);
                result.PendingUpdatePath = pendingPath;
                PendingUpdateManifest pending = LoadPending(pendingPath);
                ConfigurePaths(pending, result);

                UpdaterLogger.Info("CDBoxUpdater 启动。Pending=" + pendingPath
                    + ", ElevatedContinuation=" + elevatedContinuation
                    + ", Target=" + pending.TargetBundlePath);

                result.Status = "waiting-for-autocad";
                result.Message = "正在等待 AutoCAD 退出。";
                WriteResult(pending.LastResultPath, result);

                // AutoCAD 主窗口关闭后立即显示更新器；进程仍在释放资源时显示等待状态，
                // 直到确认所有相关 AutoCAD 进程完全退出才开始替换文件。
                progressWindow = WaitForAutoCadExit(pending, progressWindow);
                autoCadExitConfirmed = true;
                if (progressWindow == null) progressWindow = UpdateProgressWindowHost.Start(pending.TargetVersion);
                progressWindow.Report(15, "已确认 AutoCAD 退出，正在准备安装…");

                progressWindow.Report(20, "正在检查安装目录权限…");
                ValidatePendingRuntimePaths(pending);
                if (!BundleInstaller.CanWriteTarget(pending.TargetBundlePath))
                {
                    if (!elevatedContinuation)
                    {
                        UpdaterLogger.Warn("目标目录不可写，准备按需申请管理员权限。Target=" + pending.TargetBundlePath);
                        if (TryRelaunchElevated(pendingPath))
                        {
                            result.ElevationRequested = true;
                            result.Status = "elevation-requested";
                            result.Message = "目标目录需要管理员权限，已启动提权后的更新器。";
                            result.FinishedAtUtc = DateTime.UtcNow.ToString("o");
                            WriteResult(pending.LastResultPath, result);
                            UpdaterLogger.Info(result.Message);
                            progressWindow.CloseForRelaunch();
                            return 0;
                        }

                        throw new UnauthorizedAccessException("目标目录不可写，且未能获得管理员权限。");
                    }

                    throw new UnauthorizedAccessException("以管理员权限运行后仍无法写入目标目录：" + pending.TargetBundlePath);
                }

                result.Status = "installing";
                result.Message = "正在解压、校验并替换 CDBox.bundle。";
                WriteResult(pending.LastResultPath, result);

                BundleInstallOutcome outcome;
                try
                {
                    outcome = BundleInstaller.Install(pending, delegate(int percent, string message)
                    {
                        progressWindow.Report(percent, message);
                    });
                }
                catch (Exception installEx)
                {
                    var bundleInstallException = installEx as BundleInstallException;
                    bool safeToRetryElevated = bundleInstallException == null || bundleInstallException.RolledBack;
                    if (!elevatedContinuation && safeToRetryElevated && IsAccessDenied(installEx) && TryRelaunchElevated(pendingPath))
                    {
                        result.ElevationRequested = true;
                        result.RolledBack = bundleInstallException != null && bundleInstallException.RolledBack;
                        result.BackupBundlePath = bundleInstallException == null ? string.Empty : bundleInstallException.BackupBundlePath;
                        result.PackageSha256Actual = bundleInstallException == null ? string.Empty : bundleInstallException.PackageSha256Actual;
                        result.Status = "elevation-requested";
                        result.Message = "安装阶段检测到访问被拒绝，已启动管理员权限更新器重试。";
                        result.FinishedAtUtc = DateTime.UtcNow.ToString("o");
                        WriteResult(pending.LastResultPath, result);
                        UpdaterLogger.Info(result.Message);
                        progressWindow.CloseForRelaunch();
                        return 0;
                    }
                    throw;
                }

                result.Success = outcome.Success;
                result.RolledBack = outcome.RolledBack;
                result.BackupBundlePath = outcome.BackupBundlePath ?? string.Empty;
                result.PackageSha256Actual = outcome.PackageSha256Actual ?? string.Empty;
                result.Status = outcome.Success ? "success" : "failed";
                result.Message = outcome.Message ?? string.Empty;
                result.FinishedAtUtc = DateTime.UtcNow.ToString("o");
                WriteResult(pending.LastResultPath, result);

                UpdaterLogger.Info("CDBoxUpdater 完成。Success=" + result.Success
                    + ", RolledBack=" + result.RolledBack
                    + ", Backup=" + result.BackupBundlePath);
                progressWindow.Complete(result.Success,
                    result.Success
                        ? "更新已完成，现在可以重新打开 AutoCAD。"
                        : (string.IsNullOrWhiteSpace(result.Message) ? "更新未完成，请查看更新日志。" : result.Message));
                return result.Success ? 0 : 1;
            }
            catch (Exception ex)
            {
                var installException = ex as BundleInstallException;
                if (installException != null)
                {
                    result.RolledBack = installException.RolledBack;
                    result.BackupBundlePath = installException.BackupBundlePath;
                    result.PackageSha256Actual = installException.PackageSha256Actual;
                }

                result.Success = false;
                result.Status = "failed";
                result.ErrorMessage = ex.ToString();
                result.Message = ex.Message;
                result.FinishedAtUtc = DateTime.UtcNow.ToString("o");
                UpdaterLogger.Error("CDBoxUpdater 执行失败。", ex);

                try
                {
                    string resultPath = ResolveResultPath(pendingPath, result.UpdaterLogPath);
                    WriteResult(resultPath, result);
                }
                catch (Exception writeEx)
                {
                    UpdaterLogger.Error("写入 last-update-result.json 失败。", writeEx);
                }

                if (progressWindow == null && autoCadExitConfirmed)
                {
                    try { progressWindow = UpdateProgressWindowHost.Start(result.TargetVersion); }
                    catch (Exception windowEx) { UpdaterLogger.Error("打开更新失败提示窗口失败。", windowEx); }
                }

                if (progressWindow != null)
                {
                    progressWindow.Complete(false, "更新未完成：" + ex.Message);
                }

                return 1;
            }
        }

        private static PendingUpdateManifest LoadPending(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("pending-update.json 不存在。", path);
            string json = File.ReadAllText(path, Encoding.UTF8);
            var serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 * 4 };
            PendingUpdateManifest pending = serializer.Deserialize<PendingUpdateManifest>(json);
            if (pending == null) throw new InvalidOperationException("pending-update.json 解析结果为空。");
            return pending;
        }

        private static void ConfigurePaths(PendingUpdateManifest pending, UpdateResultRecord result)
        {
            if (!string.IsNullOrWhiteSpace(pending.UpdaterLogPath)) UpdaterLogger.Configure(pending.UpdaterLogPath);

            result.CurrentVersion = pending.CurrentVersion ?? string.Empty;
            result.TargetVersion = pending.TargetVersion ?? string.Empty;
            result.VersionCode = pending.VersionCode;
            result.PackagePath = pending.PackagePath ?? string.Empty;
            result.PackageSha256Expected = pending.PackageSha256 ?? string.Empty;
            result.TargetBundlePath = pending.TargetBundlePath ?? string.Empty;
            result.UpdaterLogPath = UpdaterLogger.LogPath;
        }

        private static void ValidatePendingRuntimePaths(PendingUpdateManifest pending)
        {
            if (pending.SchemaVersion != 1) throw new InvalidOperationException("不支持的 pending-update.json 版本：" + pending.SchemaVersion);
            if (string.IsNullOrWhiteSpace(pending.PackagePath) || !File.Exists(pending.PackagePath)) throw new FileNotFoundException("更新包不存在。", pending.PackagePath ?? string.Empty);
            if (string.IsNullOrWhiteSpace(pending.TargetBundlePath)) throw new InvalidOperationException("TargetBundlePath 为空。");
            if (string.IsNullOrWhiteSpace(pending.LastResultPath)) throw new InvalidOperationException("LastResultPath 为空。");

            string target = Path.GetFullPath(pending.TargetBundlePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string leaf = new DirectoryInfo(target).Name;
            if (!leaf.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("更新器只允许替换 .bundle 目录。当前目标：" + target);
            }

            if (!string.IsNullOrWhiteSpace(pending.ProtectedUserConfigDirectory))
            {
                string protectedPath = Path.GetFullPath(pending.ProtectedUserConfigDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(target, protectedPath, StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith(protectedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("目标 bundle 不得指向 Studio 用户配置目录：" + protectedPath);
                }
            }
        }

        private static UpdateProgressWindowHost WaitForAutoCadExit(
            PendingUpdateManifest pending,
            UpdateProgressWindowHost progressWindow)
        {
            string processName = NormalizeProcessName(pending.AutoCadProcessName);
            DateTime lastLog = DateTime.MinValue;

            while (true)
            {
                List<Process> running = FindBlockingProcesses(processName, pending.AutoCadProcessId, pending.AutoCadProcessStartTimeUtc);
                if (running.Count == 0)
                {
                    UpdaterLogger.Info("已确认 AutoCAD 进程退出，开始安装更新。");
                    return progressWindow;
                }

                if (AllMainWindowsClosed(running))
                {
                    if (progressWindow == null)
                    {
                        try
                        {
                            progressWindow = UpdateProgressWindowHost.Start(pending.TargetVersion);
                            UpdaterLogger.Info("检测到 AutoCAD 主窗口已关闭，已显示更新等待窗口。");
                        }
                        catch (Exception ex)
                        {
                            UpdaterLogger.Error("AutoCAD 窗口关闭后显示更新器失败，将继续后台等待。", ex);
                        }
                    }
                    if (progressWindow != null) progressWindow.ReportWaitingForAutoCadExit();
                }

                if ((DateTime.UtcNow - lastLog).TotalSeconds >= WaitLogIntervalSeconds)
                {
                    var ids = new List<string>();
                    foreach (Process process in running)
                    {
                        try { ids.Add(process.Id.ToString()); }
                        catch { }
                        finally { process.Dispose(); }
                    }
                    UpdaterLogger.Info("等待 AutoCAD 退出。ProcessName=" + processName + ", PID=" + string.Join(",", ids.ToArray()));
                    lastLog = DateTime.UtcNow;
                }
                else
                {
                    foreach (Process process in running) process.Dispose();
                }

                Thread.Sleep(1000);
            }
        }

        private static bool AllMainWindowsClosed(IList<Process> processes)
        {
            if (processes == null || processes.Count == 0) return false;
            foreach (Process process in processes)
            {
                if (process == null) continue;
                try
                {
                    process.Refresh();
                    IntPtr handle = process.MainWindowHandle;
                    if (handle != IntPtr.Zero && IsWindowVisible(handle)) return false;
                }
                catch
                {
                    // 无法确认窗口状态时继续保持后台等待，避免过早显示更新器。
                    return false;
                }
            }
            return true;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private static List<Process> FindBlockingProcesses(string processName, int expectedPid, string expectedStartTimeUtc)
        {
            var result = new List<Process>();
            int currentPid = Process.GetCurrentProcess().Id;

            if (!string.IsNullOrWhiteSpace(processName))
            {
                try
                {
                    foreach (Process process in Process.GetProcessesByName(processName))
                    {
                        if (process.Id == currentPid)
                        {
                            process.Dispose();
                            continue;
                        }
                        result.Add(process);
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    UpdaterLogger.Warn("枚举 AutoCAD 进程失败，将退回到 PID 检查：" + ex.Message);
                }
            }

            if (expectedPid <= 0 || expectedPid == currentPid) return result;
            try
            {
                Process process = Process.GetProcessById(expectedPid);
                if (MatchesExpectedStartTime(process, expectedStartTimeUtc)) result.Add(process);
                else process.Dispose();
            }
            catch (ArgumentException)
            {
            }
            catch (Exception ex)
            {
                UpdaterLogger.Warn("检查 AutoCAD PID 失败：" + ex.Message);
            }
            return result;
        }

        private static bool MatchesExpectedStartTime(Process process, string expectedStartTimeUtc)
        {
            if (process == null) return false;
            if (string.IsNullOrWhiteSpace(expectedStartTimeUtc)) return true;

            DateTime expected;
            if (!DateTime.TryParse(expectedStartTimeUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out expected)) return true;
            try
            {
                DateTime actual = process.StartTime.ToUniversalTime();
                return Math.Abs((actual - expected.ToUniversalTime()).TotalSeconds) < 2;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsAccessDenied(Exception ex)
        {
            Exception current = ex;
            while (current != null)
            {
                if (current is UnauthorizedAccessException || current is System.Security.SecurityException) return true;
                var win32 = current as Win32Exception;
                if (win32 != null && win32.NativeErrorCode == 5) return true;
                current = current.InnerException;
            }
            return false;
        }

        private static bool TryRelaunchElevated(string pendingPath)
        {
            try
            {
                string executable = Process.GetCurrentProcess().MainModule.FileName;
                var info = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "--pending " + Quote(pendingPath) + " --elevated",
                    WorkingDirectory = Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process process = Process.Start(info);
                return process != null;
            }
            catch (Win32Exception ex)
            {
                UpdaterLogger.Error("管理员权限申请失败或被用户取消。", ex);
                return false;
            }
            catch (Exception ex)
            {
                UpdaterLogger.Error("重新启动管理员更新器失败。", ex);
                return false;
            }
        }

        private static void WriteResult(string path, UpdateResultRecord result)
        {
            if (result == null || string.IsNullOrWhiteSpace(path)) return;
            result.UpdaterLogPath = UpdaterLogger.LogPath;

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 * 4 };
            string json = serializer.Serialize(result);
            string temporary = fullPath + ".tmp";
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            if (File.Exists(fullPath)) File.Delete(fullPath);
            File.Move(temporary, fullPath);
        }

        private static string ResolveResultPath(string pendingPath, string updaterLogPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(pendingPath) && File.Exists(pendingPath))
                {
                    PendingUpdateManifest pending = LoadPending(pendingPath);
                    if (!string.IsNullOrWhiteSpace(pending.LastResultPath)) return pending.LastResultPath;
                }
            }
            catch
            {
            }

            string baseDirectory = !string.IsNullOrWhiteSpace(updaterLogPath)
                ? Path.GetDirectoryName(updaterLogPath)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CDBox", "Studio");
            return Path.Combine(baseDirectory ?? Environment.CurrentDirectory, "last-update-result.json");
        }

        private static string GetArgument(string[] args, string name)
        {
            if (args == null || string.IsNullOrWhiteSpace(name)) return string.Empty;
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) continue;
                return i + 1 < args.Length ? args[i + 1] : string.Empty;
            }
            return string.Empty;
        }

        private static bool HasFlag(string[] args, string name)
        {
            if (args == null || string.IsNullOrWhiteSpace(name)) return false;
            foreach (string arg in args)
            {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string NormalizeProcessName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return string.Empty;
            string name = Path.GetFileNameWithoutExtension(processName.Trim());
            return name ?? string.Empty;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
