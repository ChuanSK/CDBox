using System;
using System.Collections.Generic;
using System.IO;
using CDBox.Shared;

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
            foreach (string dependency in CDBoxRequiredRuntimeFiles.ManagedDependencies)
            {
                CheckFile(Path.Combine(sourceDir ?? string.Empty, dependency), dependency, missing);
            }
            CheckFile(
                Path.Combine(sourceDir ?? string.Empty, CDBoxRequiredRuntimeFiles.UpdaterRelativePath),
                CDBoxRequiredRuntimeFiles.UpdaterRelativePath,
                missing);
            CheckFile(
                Path.Combine(sourceDir ?? string.Empty, CDBoxRequiredRuntimeFiles.WebView2LoaderRelativePath),
                CDBoxRequiredRuntimeFiles.WebView2LoaderRelativePath,
                missing);

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

    }
}
