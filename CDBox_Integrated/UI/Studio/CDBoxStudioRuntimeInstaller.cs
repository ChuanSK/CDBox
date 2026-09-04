using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioRuntimeInstaller
    {
        internal const string BootstrapperUrl =
            "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

        private static readonly SemaphoreSlim InstallGate =
            new SemaphoreSlim(1, 1);

        public static async Task<CDBoxStudioRuntimeInfo>
            EnsureAvailableWithPromptAsync(IWin32Window owner,
                CDBoxStudioRuntimeInfo detected)
        {
            if (detected != null && detected.Available) return detected;
            await InstallGate.WaitAsync();
            try
            {
                CDBoxStudioRuntimeInfo current = CDBoxStudioRuntime.Detect();
                if (current != null && current.Available) return current;

                DialogResult answer = TCPipeAutoDraw.UI.CDBoxMessageBox.Show(
                    owner,
                    "未检测到 Microsoft Edge WebView2 Runtime，CDBox 界面无法打开。\r\n\r\n"
                    + "是否现在自动下载并安装微软官方 WebView2 组件？\r\n\r\n"
                    + "安装过程需要网络连接，完成后将继续打开当前界面。",
                    "安装 WebView2 Runtime",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);
                if (answer != DialogResult.Yes)
                {
                    if (current == null) current =
                        new CDBoxStudioRuntimeInfo();
                    current.ErrorMessage =
                        "未安装 WebView2 Runtime，用户已取消自动安装。";
                    return current;
                }

                CDBoxStudioLogger.Info(
                    "用户确认自动安装 WebView2 Runtime，开始下载微软官方引导程序。 ");
                InstallAttempt attempt = await Task.Run(
                    (Func<InstallAttempt>)InstallRuntime);
                if (!attempt.Success)
                {
                    CDBoxStudioLogger.Error(
                        "WebView2 Runtime 自动安装失败："
                        + attempt.Message, null);
                    return new CDBoxStudioRuntimeInfo
                    {
                        Available = false,
                        ErrorMessage = attempt.Message
                    };
                }

                current = CDBoxStudioRuntime.Detect();
                if (current != null && current.Available)
                {
                    CDBoxStudioLogger.Info(
                        "WebView2 Runtime 自动安装完成。版本："
                        + current.Version);
                    TCPipeAutoDraw.UI.CDBoxMessageBox.Show(owner,
                        "Microsoft Edge WebView2 Runtime 已安装完成，正在继续打开 CDBox 界面。",
                        "WebView2 安装完成", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return current;
                }

                return new CDBoxStudioRuntimeInfo
                {
                    Available = false,
                    ErrorMessage =
                        "WebView2 安装程序已结束，但仍未检测到运行时。请重新启动 AutoCAD 后重试。"
                };
            }
            finally
            {
                InstallGate.Release();
            }
        }

        private static InstallAttempt InstallRuntime()
        {
            string temporaryDirectory = Path.Combine(Path.GetTempPath(),
                "CDBox.WebView2." + Guid.NewGuid().ToString("N"));
            string installerPath = Path.Combine(temporaryDirectory,
                "MicrosoftEdgeWebview2Setup.exe");
            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                ServicePointManager.SecurityProtocol |=
                    SecurityProtocolType.Tls12;
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] =
                        "CDBox/5.1.0";
                    client.DownloadFile(BootstrapperUrl, installerPath);
                }
                VerifyMicrosoftSignature(installerPath);
                using (Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/silent /install",
                    UseShellExecute = true,
                    WorkingDirectory = temporaryDirectory
                }))
                {
                    if (process == null)
                        return InstallAttempt.Fail(
                            "无法启动 WebView2 安装程序。 ");
                    if (!process.WaitForExit(10 * 60 * 1000))
                        return InstallAttempt.Fail(
                            "WebView2 安装程序仍在运行，请等待完成后重新打开界面。 ");
                    int exitCode = process.ExitCode;
                    return exitCode == 0 || exitCode == 1641
                        || exitCode == 3010
                        ? InstallAttempt.Ok()
                        : InstallAttempt.Fail(
                            "WebView2 安装程序返回错误码："
                            + exitCode);
                }
            }
            catch (Exception ex)
            {
                return InstallAttempt.Fail(
                    "WebView2 自动下载安装失败：" + ex.Message);
            }
            finally
            {
                TryDeleteTemporaryDirectory(temporaryDirectory);
            }
        }

        private static void VerifyMicrosoftSignature(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                throw new InvalidDataException(
                    "下载的 WebView2 安装程序为空。 ");
            using (var certificate = new X509Certificate2(
                X509Certificate.CreateFromSignedFile(path)))
            {
                if ((certificate.Subject ?? string.Empty).IndexOf(
                        "Microsoft Corporation",
                        StringComparison.OrdinalIgnoreCase) < 0)
                    throw new InvalidDataException(
                        "WebView2 安装程序未通过微软签名检查，已拒绝运行。 ");
            }
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
                        "CDBox.WebView2.", StringComparison.Ordinal))
                    Directory.Delete(path, true);
            }
            catch { }
        }

        private sealed class InstallAttempt
        {
            public bool Success { get; private set; }
            public string Message { get; private set; }

            public static InstallAttempt Ok()
            {
                return new InstallAttempt { Success = true,
                    Message = string.Empty };
            }

            public static InstallAttempt Fail(string message)
            {
                return new InstallAttempt { Success = false,
                    Message = message ?? string.Empty };
            }
        }
    }
}
