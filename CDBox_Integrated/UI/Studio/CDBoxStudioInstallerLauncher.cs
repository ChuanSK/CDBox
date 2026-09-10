using System;
using System.Diagnostics;
using System.IO;
using TCPipeAutoDraw.Core.Startup;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioInstallerLauncher
    {
        public static string ManagementInstallerPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                    "CDBox", "CDBox组件管理器.exe");
            }
        }

        public static bool ManagementInstallerExists
        {
            get { return File.Exists(ManagementInstallerPath); }
        }

        public static CDBoxStudioInstallerLaunchResult
            PrepareAndLaunchManagementInstaller()
        {
            return LaunchInstaller(ManagementInstallerPath,
                "本地组件管理器");
        }

        private static CDBoxStudioInstallerLaunchResult LaunchInstaller(
            string installerPath, string source)
        {
            return LaunchInstaller(installerPath, source,
                CDBoxStudioComponentUpdatePlan.Capture());
        }

        private static CDBoxStudioInstallerLaunchResult LaunchInstaller(
            string installerPath, string source,
            CDBoxStudioComponentUpdatePlan plan)
        {
            var result = new CDBoxStudioInstallerLaunchResult();
            try
            {
                if (string.IsNullOrWhiteSpace(installerPath)
                    || !File.Exists(installerPath))
                    throw new FileNotFoundException(
                        "未找到 CDBox 组件管理器。", installerPath);
                if (plan == null || !plan.InstallationDetected)
                    throw new InvalidOperationException(
                        "无法读取当前安装状态，已中止启动安装器。");
                string resolved = Path.GetFullPath(installerPath);
                string cadExecutable = CDBoxInstallationLocator
                    .GetCadExecutablePath();
                string arguments = "--update --target-cad "
                    + Quote(cadExecutable) + " --components "
                    + Quote(string.Join(",", plan.SelectedComponentIds));
                Process started = Process.Start(new ProcessStartInfo
                {
                    FileName = resolved,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(resolved)
                        ?? AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = true
                });
                if (started == null)
                    throw new InvalidOperationException(
                        "系统未能启动 CDBox 安装器。");
                result.Started = true;
                result.InstallerPath = resolved;
                result.CommandLine = Quote(resolved) + " " + arguments;
                CDBoxStudioLogger.Info("已启动" + source + "。Installer="
                    + resolved + ", Components="
                    + string.Join(",", plan.SelectedComponentIds)
                    + ", CAD=" + cadExecutable);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                CDBoxStudioLogger.Error("启动" + source + "失败。", ex);
            }
            return result;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty)
                .Replace("\"", "\\\"") + "\"";
        }
    }
}
