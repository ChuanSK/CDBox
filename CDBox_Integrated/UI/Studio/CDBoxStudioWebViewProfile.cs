using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioWebViewProfile
    {
        private const string ProductDirectoryName = "CDBox";
        private const string WebViewDirectoryName = "WebView2";

        public static string PreferredUserDataFolder
        {
            get
            {
                return ResolvePreferredUserDataFolder(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    Path.GetTempPath());
            }
        }

        public static string EnsureWritableUserDataFolder()
        {
            return EnsureWritableDirectory(PreferredUserDataFolder,
                "WebView2 用户数据目录");
        }

        public static string EnsureWritableRecoveryFolder()
        {
            string root = Path.GetTempPath();
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException(
                    "无法获取当前用户临时目录，WebView2 无法创建备用数据目录。");
            string folder = Path.Combine(root, ProductDirectoryName,
                WebViewDirectoryName, "Recovery-"
                + Process.GetCurrentProcess().Id);
            return EnsureWritableDirectory(folder,
                "WebView2 备用数据目录");
        }

        internal static string ResolvePreferredUserDataFolder(
            string localApplicationData, string temporaryDirectory)
        {
            string root = string.IsNullOrWhiteSpace(localApplicationData)
                ? temporaryDirectory : localApplicationData;
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException(
                    "无法获取当前用户的本地数据目录。");
            return Path.GetFullPath(Path.Combine(root,
                ProductDirectoryName, WebViewDirectoryName));
        }

        private static string EnsureWritableDirectory(string folder,
            string description)
        {
            try
            {
                string fullPath = Path.GetFullPath(folder);
                Directory.CreateDirectory(fullPath);
                string probe = Path.Combine(fullPath, ".cdbox-write-"
                    + Guid.NewGuid().ToString("N") + ".tmp");
                using (new FileStream(probe, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 1,
                    FileOptions.DeleteOnClose))
                {
                }
                return fullPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(description
                    + "不可写：" + folder + "。请检查当前用户的目录权限。", ex);
            }
        }
    }
}
