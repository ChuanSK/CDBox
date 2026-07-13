using System;
using System.Collections.Generic;
using System.IO;

namespace TCPipeAutoDraw.Core.Startup
{
    internal sealed class CDBoxUpdateSourceValidationResult
    {
        public bool Valid { get; set; }
        public string Message { get; set; }
        public IList<string> MissingFiles { get; set; }
        public CDBoxUpdateSourceValidationResult() { Message = string.Empty; MissingFiles = new List<string>(); }
    }

    internal static class CDBoxUpdateSourceValidator
    {
        public static CDBoxUpdateSourceValidationResult Validate(string selectedDllPath)
        {
            var missing = new List<string>();
            string sourceDir = string.IsNullOrWhiteSpace(selectedDllPath) ? string.Empty : Path.GetDirectoryName(selectedDllPath);
            CheckFile(selectedDllPath, "CDBox.dll", missing);
            CheckFile(Path.Combine(sourceDir ?? string.Empty, "Microsoft.Web.WebView2.Core.dll"), "Microsoft.Web.WebView2.Core.dll", missing);
            CheckFile(Path.Combine(sourceDir ?? string.Empty, "Microsoft.Web.WebView2.WinForms.dll"), "Microsoft.Web.WebView2.WinForms.dll", missing);
            CheckFile(Path.Combine(sourceDir ?? string.Empty, "Updater", "CDBoxUpdater.exe"), "Updater\\CDBoxUpdater.exe", missing);
            if (!FindFile(sourceDir, "WebView2Loader.dll")) missing.Add("runtimes\\win-x64\\native\\WebView2Loader.dll");

            bool valid = missing.Count == 0;
            string message = valid
                ? "更新源目录包含全部必要运行时文件。"
                : "所选 CDBox.dll 旁缺少必要依赖，已拒绝更新：\r\n- " + string.Join("\r\n- ", missing.ToArray())
                    + "\r\n\r\n请从完整构建输出目录选择，例如：bin\\Release\\net48\\CDBox.dll；不要从只包含单个 DLL 的目录更新。";
            return new CDBoxUpdateSourceValidationResult { Valid = valid, Message = message, MissingFiles = missing };
        }

        private static void CheckFile(string path, string displayName, IList<string> missing)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) missing.Add(displayName);
        }

        private static bool FindFile(string root, string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return false;
                return Directory.GetFiles(root, fileName, SearchOption.AllDirectories).Length > 0;
            }
            catch { return false; }
        }
    }
}
