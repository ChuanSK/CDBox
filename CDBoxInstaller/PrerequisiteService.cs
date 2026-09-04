using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace CDBox.Setup
{
    internal static class PrerequisiteService
    {
        internal const string WebView2BootstrapperUrl =
            "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
        internal const string DotNetFramework48WebInstallerUrl =
            "https://go.microsoft.com/fwlink/?linkid=2088631";

        private const string WebView2ClientId =
            "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
        private const int DotNetFramework48MinimumRelease = 528040;

        public static PrerequisiteStatus Detect()
        {
            int release = ReadDotNetFrameworkRelease();
            string webViewVersion = ReadWebView2Version();
            return new PrerequisiteStatus
            {
                DotNetFrameworkRelease = release,
                DotNetFramework48Installed = release >=
                    DotNetFramework48MinimumRelease,
                WebView2Version = webViewVersion,
                WebView2Installed = !string.IsNullOrWhiteSpace(
                    webViewVersion)
            };
        }

        public static PrerequisiteStatus EnsureRequiredComponents(
            Action<string> progress)
        {
            PrerequisiteStatus status = Detect();
            if (!status.DotNetFramework48Installed)
            {
                progress?.Invoke("未检测到 .NET Framework 4.8，正在下载微软官方安装程序…");
                int exitCode = DownloadAndRunMicrosoftInstaller(
                    DotNetFramework48WebInstallerUrl, "ndp48-web.exe",
                    "/q /norestart", progress);
                status = Detect();
                if (!status.DotNetFramework48Installed)
                {
                    if (RequiresRestart(exitCode))
                        throw new InvalidOperationException(
                            ".NET Framework 4.8 已安装，必须重启 Windows 后再运行 CDBox 安装器。");
                    throw new InvalidOperationException(
                        ".NET Framework 4.8 自动安装未完成，请检查网络或 Windows 更新状态。安装程序退出码："
                        + exitCode);
                }
            }

            if (!status.WebView2Installed)
            {
                progress?.Invoke("未检测到 WebView2 Runtime，正在下载微软官方安装程序…");
                int exitCode = DownloadAndRunMicrosoftInstaller(
                    WebView2BootstrapperUrl,
                    "MicrosoftEdgeWebview2Setup.exe",
                    "/silent /install", progress);
                status = Detect();
                if (!status.WebView2Installed)
                    throw new InvalidOperationException(
                        "Microsoft Edge WebView2 Runtime 自动安装未完成。安装程序退出码："
                        + exitCode + "。请检查网络连接后重试。");
            }

            progress?.Invoke("运行组件检查完成：.NET Framework 4.8、WebView2 Runtime 均可用。 ");
            return status;
        }

        private static int DownloadAndRunMicrosoftInstaller(string url,
            string fileName, string arguments, Action<string> progress)
        {
            string temporaryDirectory = Path.Combine(Path.GetTempPath(),
                "CDBox.Setup.Component." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            string installerPath = Path.Combine(temporaryDirectory,
                fileName);
            try
            {
                ServicePointManager.SecurityProtocol |=
                    SecurityProtocolType.Tls12;
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] =
                        "CDBox-Installer/5.1.0";
                    client.DownloadFile(url, installerPath);
                }
                VerifyMicrosoftSignature(installerPath);
                progress?.Invoke("组件安装程序已通过微软签名检查，正在静默安装…");
                using (Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    WorkingDirectory = temporaryDirectory
                }))
                {
                    if (process == null)
                        throw new InvalidOperationException(
                            "无法启动组件安装程序。");
                    if (!process.WaitForExit(10 * 60 * 1000))
                        throw new TimeoutException(
                            "组件安装程序仍在运行，请等待其完成后重新运行 CDBox 安装器。");
                    int exitCode = process.ExitCode;
                    if (exitCode != 0 && !RequiresRestart(exitCode))
                        throw new InvalidOperationException(
                            "组件安装程序返回错误码：" + exitCode);
                    return exitCode;
                }
            }
            finally
            {
                TryDeleteTemporaryDirectory(temporaryDirectory);
            }
        }

        private static void VerifyMicrosoftSignature(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                throw new InvalidDataException("下载的组件安装程序为空。 ");
            try
            {
                using (var certificate = new X509Certificate2(
                    X509Certificate.CreateFromSignedFile(path)))
                {
                    string subject = certificate.Subject ?? string.Empty;
                    if (subject.IndexOf("Microsoft Corporation",
                            StringComparison.OrdinalIgnoreCase) < 0)
                        throw new InvalidDataException(
                            "组件安装程序不是由 Microsoft Corporation 签名，已拒绝运行。 ");
                }
            }
            catch (InvalidDataException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    "无法验证组件安装程序的微软数字签名。", ex);
            }
        }

        private static int ReadDotNetFrameworkRelease()
        {
            foreach (RegistryView view in RegistryViews())
            {
                try
                {
                    using (RegistryKey root = RegistryKey.OpenBaseKey(
                        RegistryHive.LocalMachine, view))
                    using (RegistryKey key = root.OpenSubKey(
                        @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                    {
                        object value = key == null ? null
                            : key.GetValue("Release");
                        int release;
                        if (value != null && int.TryParse(
                                Convert.ToString(value), out release))
                            return release;
                    }
                }
                catch { }
            }
            return 0;
        }

        private static string ReadWebView2Version()
        {
            foreach (Tuple<RegistryHive, RegistryView> location in
                WebViewRegistryLocations())
            {
                try
                {
                    using (RegistryKey root = RegistryKey.OpenBaseKey(
                        location.Item1, location.Item2))
                    using (RegistryKey key = root.OpenSubKey(
                        @"SOFTWARE\Microsoft\EdgeUpdate\Clients\"
                        + WebView2ClientId))
                    {
                        string version = Convert.ToString(key == null
                            ? null : key.GetValue("pv")) ?? string.Empty;
                        if (IsVersion(version)) return version.Trim();
                    }
                }
                catch { }
            }

            foreach (string root in WebViewApplicationDirectories())
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    string version = Directory.GetDirectories(root)
                        .Select(Path.GetFileName).Where(IsVersion)
                        .OrderByDescending(x => ParseVersion(x))
                        .FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(version)
                        && File.Exists(Path.Combine(root, version,
                            "msedgewebview2.exe"))) return version;
                }
                catch { }
            }
            return string.Empty;
        }

        private static bool IsVersion(string value)
        {
            Version parsed;
            return Version.TryParse((value ?? string.Empty).Trim(),
                out parsed) && parsed.Major > 0;
        }

        private static Version ParseVersion(string value)
        {
            Version parsed;
            return Version.TryParse(value, out parsed)
                ? parsed : new Version(0, 0);
        }

        private static bool RequiresRestart(int exitCode)
        {
            return exitCode == 1641 || exitCode == 3010;
        }

        private static string[] WebViewApplicationDirectories()
        {
            return new[]
            {
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft", "EdgeWebView", "Application"),
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),
                    "Microsoft", "EdgeWebView", "Application"),
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "EdgeWebView", "Application")
            };
        }

        private static Tuple<RegistryHive, RegistryView>[]
            WebViewRegistryLocations()
        {
            return new[]
            {
                Tuple.Create(RegistryHive.LocalMachine,
                    RegistryView.Registry32),
                Tuple.Create(RegistryHive.LocalMachine,
                    RegistryView.Registry64),
                Tuple.Create(RegistryHive.CurrentUser,
                    RegistryView.Default)
            };
        }

        private static RegistryView[] RegistryViews()
        {
            return new[] { RegistryView.Registry64,
                RegistryView.Registry32 };
        }

        private static void TryDeleteTemporaryDirectory(string path)
        {
            try
            {
                string tempRoot = Path.GetFullPath(Path.GetTempPath())
                    .TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                string candidate = Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (candidate.StartsWith(tempRoot,
                        StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(path).StartsWith(
                        "CDBox.Setup.Component.",
                        StringComparison.Ordinal))
                    Directory.Delete(path, true);
            }
            catch { }
        }
    }
}
