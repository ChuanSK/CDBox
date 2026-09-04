using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

namespace CDBox.Setup
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Any(arg => string.Equals(arg, "--verify-payload", StringComparison.OrdinalIgnoreCase)))
                {
                    InstallerPayload.VerifyAvailable();
                    return 0;
                }

                int diagnosticsIndex = Array.FindIndex(args, arg => string.Equals(arg, "--write-diagnostics", StringComparison.OrdinalIgnoreCase));
                if (diagnosticsIndex >= 0 && diagnosticsIndex + 1 < args.Length)
                {
                    WriteDiagnostics(args[diagnosticsIndex + 1]);
                    return 0;
                }

                if (!IsAdministrator())
                {
                    return RelaunchElevated(args) ? 0 : 1;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new InstallerForm(
                    CadDetectionService.DetectSupportedVersions(),
                    ParseLaunchOptions(args)));
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "CDBox 安装器",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static bool RelaunchElevated(string[] args)
        {
            try
            {
                string executable = Assembly.GetExecutingAssembly().Location;
                string arguments = string.Join(" ", args.Select(QuoteArgument).Concat(new[] { "--elevated" }));
                Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                });
                return true;
            }
            catch
            {
                MessageBox.Show(
                    "安装需要管理员权限。请允许 Windows 权限确认后重试。",
                    "CDBox 安装器",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
        }

        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static InstallerLaunchOptions ParseLaunchOptions(
            string[] args)
        {
            var options = new InstallerLaunchOptions
            {
                UpdateMode = args.Any(arg => string.Equals(arg,
                    "--update", StringComparison.OrdinalIgnoreCase)),
                TargetCadExecutablePath = GetArgument(args,
                    "--target-cad")
            };
            string components = GetArgument(args, "--components");
            options.SelectedComponentIds = (components ?? string.Empty)
                .Split(new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0).ToArray();
            return options;
        }

        private static string GetArgument(string[] args, string name)
        {
            int index = Array.FindIndex(args, arg => string.Equals(arg,
                name, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < args.Length
                ? args[index + 1] : string.Empty;
        }

        private static void WriteDiagnostics(string outputPath)
        {
            StringBuilder builder = new StringBuilder();
            foreach (CadInstallation installation in CadDetectionService.DetectSupportedVersions())
            {
                builder.Append(installation.Version.Year)
                    .Append('|')
                    .Append(installation.IsDetected ? "detected" : "not-detected")
                    .Append('|')
                    .Append(installation.ProductKey)
                    .Append('|')
                    .AppendLine(installation.InstallDirectory);
            }

            File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(false));
        }
    }
}
