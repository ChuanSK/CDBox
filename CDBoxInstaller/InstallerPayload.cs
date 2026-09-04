using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using CDBox.Shared.Components;

namespace CDBox.Setup
{
    internal sealed class InstallerPayload : IDisposable
    {
        private const string EmbeddedPayloadName = "CDBoxInstaller.Payload.zip";
        private string _temporaryRoot;

        private InstallerPayload(string bundleDirectory, string temporaryRoot,
            CDBoxComponentManifest componentManifest)
        {
            BundleDirectory = bundleDirectory;
            _temporaryRoot = temporaryRoot;
            ComponentManifest = componentManifest;
        }

        public string BundleDirectory { get; }
        public CDBoxComponentManifest ComponentManifest { get; }

        public static CDBoxComponentManifest ReadAvailableComponentManifest()
        {
            Stream resource = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(EmbeddedPayloadName);
            if (resource != null)
            {
                using (resource)
                using (var archive = new ZipArchive(resource,
                    ZipArchiveMode.Read, false))
                {
                    ZipArchiveEntry entry = archive.Entries.FirstOrDefault(x =>
                        (x.FullName ?? string.Empty).Replace('\\', '/')
                            .EndsWith("/Contents/components.json",
                                StringComparison.OrdinalIgnoreCase));
                    if (entry == null)
                        throw new InvalidDataException(
                            "安装包缺少组件清单 components.json。");
                    using (var reader = new StreamReader(entry.Open(),
                        Encoding.UTF8, true))
                        return CDBoxComponentBundle.DeserializeManifest(
                            reader.ReadToEnd());
                }
            }

            return CDBoxComponentCatalog.CreateManifest("5.1.0");
        }

        public static InstallerPayload Open()
        {
            Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedPayloadName);
            if (resource != null)
            {
                string temporaryRoot = CreateTemporaryRoot();
                try
                {
                    using (resource)
                    {
                        ExtractZipSafely(resource, temporaryRoot);
                    }

                    string bundle = LocateAndValidateBundle(temporaryRoot);
                    return new InstallerPayload(bundle, temporaryRoot,
                        CDBoxComponentBundle.ReadManifest(bundle));
                }
                catch
                {
                    DeleteTemporaryRoot(temporaryRoot);
                    throw;
                }
            }

            throw new InvalidOperationException(
                "安装器中未包含内嵌 CDBox 安装文件，请重新下载安装器。后续版本不再支持相邻 ZIP 或本地目录载荷。");
        }

        public static void VerifyAvailable()
        {
            using (InstallerPayload payload = Open())
            {
                ValidateBundle(payload.BundleDirectory);
            }
        }

        private static string CreateTemporaryRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "CDBox.Setup." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void ExtractZipSafely(Stream zipStream, string destinationRoot)
        {
            string normalizedRoot = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read, false))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string normalizedEntryName = (entry.FullName ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                    if (string.IsNullOrWhiteSpace(normalizedEntryName))
                    {
                        continue;
                    }

                    string destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, normalizedEntryName));
                    if (!destinationPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("安装文件包含无效路径：" + entry.FullName);
                    }

                    if (normalizedEntryName.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                    entry.ExtractToFile(destinationPath, true);
                }
            }
        }

        private static string LocateAndValidateBundle(string searchRoot)
        {
            string direct = Path.Combine(searchRoot, "CDBox.bundle");
            if (Directory.Exists(direct))
            {
                ValidateBundle(direct);
                return direct;
            }

            string[] candidates = Directory.GetDirectories(searchRoot, "CDBox.bundle", SearchOption.AllDirectories);
            if (candidates.Length != 1)
            {
                throw new InvalidDataException("安装文件中应包含且仅包含一个 CDBox.bundle。");
            }

            ValidateBundle(candidates[0]);
            return candidates[0];
        }

        internal static void ValidateBundle(string bundleDirectory)
        {
            string[] requiredFiles =
            {
                "PackageContents.xml",
                @"Contents\CDBox.dll",
                @"Contents\CDBox.Shared.dll",
                @"Contents\CDBox.Common.dll",
                @"Contents\CDBox.Wastewater.dll",
                @"Contents\CDBox.RealEstate.dll",
                @"Contents\components.json",
                @"Contents\Microsoft.Web.WebView2.Core.dll",
                @"Contents\Microsoft.Web.WebView2.WinForms.dll",
                @"Contents\runtimes\win-x64\native\WebView2Loader.dll"
            };

            foreach (string relativePath in requiredFiles)
            {
                if (!File.Exists(Path.Combine(bundleDirectory, relativePath)))
                {
                    throw new InvalidDataException("安装文件不完整，缺少：" + relativePath);
                }
            }

            CDBoxComponentManifest manifest =
                CDBoxComponentBundle.ReadManifest(bundleDirectory);
            CDBoxComponentBundle.ValidatePayloadBundle(
                bundleDirectory, manifest);
        }

        public void Dispose()
        {
            if (string.IsNullOrWhiteSpace(_temporaryRoot))
            {
                return;
            }

            DeleteTemporaryRoot(_temporaryRoot);
            _temporaryRoot = null;
        }

        private static void DeleteTemporaryRoot(string temporaryRoot)
        {
            if (string.IsNullOrWhiteSpace(temporaryRoot) || !Directory.Exists(temporaryRoot))
            {
                return;
            }

            string fullTemporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullCandidate = Path.GetFullPath(temporaryRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullCandidate.StartsWith(fullTemporaryRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(temporaryRoot).StartsWith("CDBox.Setup.", StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                Directory.Delete(temporaryRoot, true);
            }
            catch
            {
                // Temporary files can be cleaned by the operating system later.
            }
        }
    }
}
