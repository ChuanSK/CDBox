using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Microsoft.Win32;
using TCPipeAutoDraw.UI.Studio;

namespace TCPipeAutoDraw.Core.Startup
{
    internal static class CDBoxInstaller
    {
        private const string AppName = "CDBox";
        private const string BundleFolderName = "CDBox.bundle";
        private const string ContentsFolderName = "Contents";

        public static string GetCadDirectory()
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                string dir = Path.GetDirectoryName(exe);
                if (!string.IsNullOrWhiteSpace(dir)) return dir;
            }
            catch
            {
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string GetInstallRoot()
        {
            return Path.Combine(GetCadDirectory(), BundleFolderName);
        }

        public static string GetInstalledDllPath()
        {
            return GetInstalledDllPath(GetMainAssemblyFileName());
        }

        private static string GetInstalledDllPath(string assemblyFileName)
        {
            if (string.IsNullOrWhiteSpace(assemblyFileName)) assemblyFileName = GetMainAssemblyFileName();
            return Path.Combine(Path.Combine(GetInstallRoot(), ContentsFolderName), assemblyFileName);
        }

        public static bool IsInstalled()
        {
            try
            {
                string loader = GetRegisteredLoaderPath();
                return !string.IsNullOrWhiteSpace(loader) && File.Exists(loader);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsRunningFromInstallFolder()
        {
            try
            {
                string assemblyPath = GetMainAssemblyPath();
                string installRoot = GetInstallRoot();
                if (string.IsNullOrWhiteSpace(assemblyPath) || string.IsNullOrWhiteSpace(installRoot)) return false;

                string fullAssembly = Path.GetFullPath(assemblyPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullRoot = Path.GetFullPath(installRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return fullAssembly.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string GetRegisteredLoaderPath()
        {
            try
            {
                string appKeyPath = GetCurrentUserApplicationsKeyPath();
                if (string.IsNullOrWhiteSpace(appKeyPath)) return string.Empty;

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(appKeyPath + "\\" + AppName))
                {
                    object value = key == null ? null : key.GetValue("LOADER");
                    return Convert.ToString(value) ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        public static CDBoxInstallResult InstallToCadDirectory()
        {
            string installRoot = GetInstallRoot();
            string contentsDir = Path.Combine(installRoot, ContentsFolderName);

            try
            {
                string sourceAssemblyPath = GetMainAssemblyPath();
                if (string.IsNullOrWhiteSpace(sourceAssemblyPath) || !File.Exists(sourceAssemblyPath))
                {
                    return CDBoxInstallResult.Fail("未能定位当前加载的 CDBox.dll，无法安装。", installRoot);
                }

                CDBoxUpdateSourceValidationResult sourceValidation = CDBoxUpdateSourceValidator.Validate(sourceAssemblyPath);
                if (!sourceValidation.Valid)
                {
                    return CDBoxInstallResult.Fail("当前加载目录不是完整 CDBox 构建输出，已拒绝安装。\r\n\r\n" + sourceValidation.Message, installRoot);
                }

                Directory.CreateDirectory(contentsDir);
                string sourceDir = Path.GetDirectoryName(sourceAssemblyPath);
                CopyRuntimeFiles(sourceDir, contentsDir);
                WritePackageContents(installRoot);
                RegisterDemandLoad(GetInstalledDllPath());

                string selfCheck = RunInstallSelfCheck(installRoot, GetMainAssemblyFileName());
                CDBoxInstallLogger.Info("安装完成。安装目录：" + installRoot + "；源目录：" + (sourceDir ?? string.Empty));
                CDBoxInstallLogger.Info("安装自检结果：" + selfCheck.Replace("\r\n", " | "));

                return CDBoxInstallResult.Ok(BuildInstallMessage("CDBox 已安装到 CAD 所在目录，之后启动 CAD 会自动加载。", selfCheck), installRoot, selfCheck);
            }
            catch (UnauthorizedAccessException ex)
            {
                return CDBoxInstallResult.Fail("没有权限写入 CAD 所在目录。请以管理员身份运行 CAD 后重试，或手动将 CDBox.bundle 放到 CAD 目录。\r\n\r\n" + ex.Message, installRoot);
            }
            catch (Exception ex)
            {
                return CDBoxInstallResult.Fail("安装失败：" + ex.Message, installRoot);
            }
        }

        public static CDBoxInstallResult Uninstall()
        {
            string installRoot = GetInstallRoot();

            try
            {
                RemoveDemandLoadRegistration();
            }
            catch
            {
                // 注册表清理失败不阻止后续删除，最终会在提示中说明。
            }

            bool deleted = false;
            string deleteMessage = string.Empty;
            try
            {
                if (Directory.Exists(installRoot))
                {
                    Directory.Delete(installRoot, true);
                }
                deleted = true;
            }
            catch (Exception ex)
            {
                deleteMessage = ex.Message;
            }

            if (deleted)
            {
                return CDBoxInstallResult.Ok("CDBox 自动加载注册已移除，安装目录已删除。", installRoot);
            }

            bool deferred = TryStartDeferredDelete(installRoot, out string deferredMessage);
            if (deferred)
            {
                return new CDBoxInstallResult
                {
                    Success = true,
                    InstallRoot = installRoot,
                    DeferredDeleteStarted = true,
                    Message = "CDBox 自动加载注册已移除。当前 DLL 可能正被 CAD 占用，已安排在关闭 CAD 后自动删除安装目录。"
                };
            }

            return CDBoxInstallResult.Fail("CDBox 自动加载注册已移除，但安装目录暂时无法删除：" + deleteMessage + "\r\n" + deferredMessage + "\r\n请关闭 CAD 后手动删除：" + installRoot, installRoot);
        }

        public static CDBoxInstallResult ScheduleUpdateFromDll(string newDllPath)
        {
            string installRoot = GetInstallRoot();
            string packageSourceRoot = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(newDllPath) || !File.Exists(newDllPath))
                {
                    return CDBoxInstallResult.Fail("未找到选择的新版 CDBox.dll。", installRoot);
                }

                string sourceDir = Path.GetDirectoryName(newDllPath);
                if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
                {
                    return CDBoxInstallResult.Fail("未能定位新版 DLL 所在目录。", installRoot);
                }

                string selectedName = Path.GetFileName(newDllPath);
                string currentName = GetMainAssemblyFileName();
                if (!string.Equals(selectedName, currentName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(selectedName, "CDBox.dll", StringComparison.OrdinalIgnoreCase))
                {
                    return CDBoxInstallResult.Fail("请选择 CDBox 主 DLL 文件。当前选择：" + selectedName, installRoot);
                }

                CDBoxUpdateSourceValidationResult sourceValidation = CDBoxUpdateSourceValidator.Validate(newDllPath);
                if (!sourceValidation.Valid)
                {
                    CDBoxInstallLogger.Warn("更新源校验失败：" + sourceValidation.Message.Replace("\r\n", " | "));
                    return CDBoxInstallResult.Fail(sourceValidation.Message, installRoot);
                }

                string updateDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CDBox",
                    "Updates",
                    "Local",
                    DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                packageSourceRoot = Path.Combine(updateDirectory, "package-source");
                string stagingBundle = Path.Combine(packageSourceRoot, BundleFolderName);
                string stagingContents = Path.Combine(stagingBundle, ContentsFolderName);
                string packagePath = Path.Combine(updateDirectory, "CDBox.Local.Update.zip");

                Directory.CreateDirectory(stagingContents);
                CopyRuntimeFiles(sourceDir, stagingContents);
                WritePackageContents(stagingBundle, Path.GetFileName(newDllPath));
                RegisterDemandLoad(GetInstalledDllPath(Path.GetFileName(newDllPath)));

                bool stagingValid;
                string selfCheck = RunInstallSelfCheck(stagingBundle, Path.GetFileName(newDllPath), out stagingValid);
                CDBoxInstallLogger.Info("更新暂存目录准备完成。暂存目录：" + stagingBundle + "；源目录：" + sourceDir);
                CDBoxInstallLogger.Info("更新暂存自检结果：" + selfCheck.Replace("\r\n", " | "));

                if (!stagingValid)
                {
                    return CDBoxInstallResult.Fail("更新暂存包未通过完整性检查，现有安装不会被替换。\r\n\r\n" + selfCheck, installRoot);
                }

                ZipFile.CreateFromDirectory(packageSourceRoot, packagePath, CompressionLevel.Optimal, false);
                string packageSha256 = ComputeSha256(packagePath);
                Directory.Delete(packageSourceRoot, true);
                packageSourceRoot = string.Empty;

                string targetVersion = ReadFileVersion(newDllPath);
                var download = new CDBoxStudioUpdateDownloadResult
                {
                    Success = true,
                    Verified = true,
                    CurrentVersion = CDBoxStudioUpdateService.CurrentVersion,
                    CurrentVersionCode = CDBoxStudioUpdateService.CurrentVersionCode,
                    LatestVersion = targetVersion,
                    VersionCode = CDBoxStudioUpdateService.CurrentVersionCode,
                    Channel = "local",
                    PackageFileName = Path.GetFileName(packagePath),
                    PackageSizeBytes = new FileInfo(packagePath).Length,
                    Sha256Expected = packageSha256,
                    Sha256Actual = packageSha256,
                    FilePath = packagePath,
                    SourceName = "本地完整构建输出",
                    SourceUrl = Path.GetFullPath(newDllPath),
                    FinishedAt = DateTime.Now
                };

                string sourceUpdater = Path.Combine(sourceDir, "Updater", "CDBoxUpdater.exe");
                CDBoxStudioUpdaterLaunchResult launch = CDBoxStudioUpdaterLauncher.PrepareAndLaunch(download, sourceUpdater);
                if (launch == null || !launch.Started)
                {
                    string launchError = launch == null ? "更新器未返回启动结果。" : launch.ErrorMessage;
                    return CDBoxInstallResult.Fail(
                        "本地更新包已完成依赖收集和校验，但独立更新器启动失败：" + launchError
                        + "\r\n\r\n已保留更新包：" + packagePath,
                        installRoot);
                }

                return new CDBoxInstallResult
                {
                    Success = true,
                    InstallRoot = installRoot,
                    DeferredDeleteStarted = true,
                    SelfCheckReport = selfCheck,
                    LogFilePath = CDBoxInstallLogger.LogFilePath,
                    Message = BuildInstallMessage(
                        "本地更新包已完成全部依赖收集与校验，独立更新器已启动。请正常关闭 AutoCAD，并在更新完成提示出现前不要再次打开 CAD；安装阶段将显示与网络更新相同的进度窗口。",
                        selfCheck)
                };
            }
            catch (Exception ex)
            {
                CDBoxInstallLogger.Error("安排更新失败。", ex);
                return CDBoxInstallResult.Fail("安排更新失败：" + ex.Message + "\r\n\r\n日志：" + CDBoxInstallLogger.LogFilePath, installRoot);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(packageSourceRoot) && Directory.Exists(packageSourceRoot))
                {
                    try { Directory.Delete(packageSourceRoot, true); }
                    catch (Exception ex) { CDBoxInstallLogger.Warn("清理本地更新暂存目录失败：" + ex.Message); }
                }
            }
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) text.Append(value.ToString("x2"));
                return text.ToString();
            }
        }

        private static string ReadFileVersion(string path)
        {
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(info.ProductVersion)) return info.ProductVersion.Trim();
                if (!string.IsNullOrWhiteSpace(info.FileVersion)) return info.FileVersion.Trim();
            }
            catch
            {
            }
            return "local-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        private static void CopyRuntimeFiles(string sourceDir, string contentsDir)
        {
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir)) throw new DirectoryNotFoundException("源目录不存在：" + sourceDir);

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".dll", ".exe", ".pdb", ".config", ".json", ".xml", ".deps", ".targets", ".props",
                ".html", ".htm", ".css", ".js", ".mjs", ".map", ".wasm",
                ".ico", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp",
                ".woff", ".woff2", ".ttf", ".eot", ".txt", ".csv", ".xls", ".xlsx"
            };

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string ext = Path.GetExtension(file);
                if (!allowedExtensions.Contains(ext)) continue;

                string name = Path.GetFileName(file);
                if (IsAutoCadManagedAssembly(name)) continue;

                string dest = Path.Combine(contentsDir, name);
                if (PathsEqual(file, dest)) continue;
                File.Copy(file, dest, true);
            }

            CopyKnownResourceDirectories(sourceDir, contentsDir);
            CopyTemplates(sourceDir, contentsDir);
        }

        private static void CopyKnownResourceDirectories(string sourceDir, string contentsDir)
        {
            foreach (string root in GetSourceSearchRoots(sourceDir))
            {
                CopyDirectoryIfExists(Path.Combine(root, "runtimes"), Path.Combine(contentsDir, "runtimes"));
                CopyDirectoryIfExists(Path.Combine(root, "Studio"), Path.Combine(contentsDir, "Studio"));
                CopyDirectoryIfExists(Path.Combine(root, "Web"), Path.Combine(contentsDir, "Web"));
                CopyDirectoryIfExists(Path.Combine(root, "wwwroot"), Path.Combine(contentsDir, "wwwroot"));
                CopyDirectoryIfExists(Path.Combine(root, "dist"), Path.Combine(contentsDir, "dist"));
                CopyDirectoryIfExists(Path.Combine(root, "assets"), Path.Combine(contentsDir, "assets"));
                CopyDirectoryIfExists(Path.Combine(root, "Updater"), Path.Combine(contentsDir, "Updater"));
                CopyDirectoryIfExists(Path.Combine(root, "CDBox_Integrated", "UI", "Studio", "Web"), Path.Combine(contentsDir, "Studio", "Web"));
                CopyDirectoryIfExists(Path.Combine(root, "CDBox_Integrated", "UI", "Studio", "dist"), Path.Combine(contentsDir, "Studio", "dist"));
                CopyDirectoryIfExists(Path.Combine(root, "CDBox_Integrated", "Studio"), Path.Combine(contentsDir, "Studio"));
                CopyDirectoryIfExists(Path.Combine(root, "CDBox_Integrated", "Web"), Path.Combine(contentsDir, "Web"));
                CopyDirectoryIfExists(Path.Combine(root, "CDBox_Integrated", "wwwroot"), Path.Combine(contentsDir, "wwwroot"));
            }
        }

        private static IEnumerable<string> GetSourceSearchRoots(string sourceDir)
        {
            var roots = new List<string>();
            AddRoot(roots, sourceDir);

            try
            {
                DirectoryInfo current = string.IsNullOrWhiteSpace(sourceDir) ? null : new DirectoryInfo(sourceDir);
                for (int i = 0; i < 6 && current != null; i++)
                {
                    AddRoot(roots, current.FullName);
                    current = current.Parent;
                }
            }
            catch
            {
            }

            AddRoot(roots, AppDomain.CurrentDomain.BaseDirectory);
            return roots;
        }

        private static void AddRoot(IList<string> roots, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            foreach (string existing in roots)
            {
                if (PathsEqual(existing, path)) return;
            }
            roots.Add(path);
        }

        private static void CopyDirectoryIfExists(string sourceDir, string targetDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir)) return;
                if (PathsEqual(sourceDir, targetDir)) return;
                CopyDirectory(sourceDir, targetDir);
                CDBoxInstallLogger.Info("复制资源目录：" + sourceDir + " -> " + targetDir);
            }
            catch (Exception ex)
            {
                CDBoxInstallLogger.Warn("资源目录复制失败：" + sourceDir + " -> " + targetDir + "；" + ex.Message);
            }
        }

        private static void CopyTemplates(string sourceDir, string contentsDir)
        {
            string targetTemplates = Path.Combine(contentsDir, "Templates");
            string[] candidates = new[]
            {
                Path.Combine(sourceDir, "Templates"),
                Path.Combine(sourceDir, "CDBox_Integrated", "Templates"),
                Path.Combine(Directory.GetParent(sourceDir) == null ? sourceDir : Directory.GetParent(sourceDir).FullName, "CDBox_Integrated", "Templates")
            };

            foreach (string candidate in candidates)
            {
                if (!Directory.Exists(candidate)) continue;
                CopyDirectory(candidate, targetTemplates);
                return;
            }
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (PathsEqual(sourceDir, targetDir)) return;
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, dest);
            }
        }


        private static bool PathsEqual(string left, string right)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
                return string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool IsAutoCadManagedAssembly(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            return fileName.Equals("AcMgd.dll", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals("AcDbMgd.dll", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals("AcCoreMgd.dll", StringComparison.OrdinalIgnoreCase);
        }

        private static void RegisterDemandLoad(string loaderPath)
        {
            string appKeyPath = GetCurrentUserApplicationsKeyPath();
            if (string.IsNullOrWhiteSpace(appKeyPath)) throw new InvalidOperationException("未能获取当前 AutoCAD 注册表路径。可先打开任意图纸后再执行安装。 ");

            using (RegistryKey appKey = Registry.CurrentUser.CreateSubKey(appKeyPath + "\\" + AppName))
            {
                if (appKey == null) throw new InvalidOperationException("无法创建 CDBox 自动加载注册表项。 ");

                appKey.SetValue("DESCRIPTION", "CDBox 管线测绘辅助插件", RegistryValueKind.String);
                appKey.SetValue("LOADCTRLS", 2, RegistryValueKind.DWord); // 2 = AutoCAD 启动时加载
                appKey.SetValue("LOADER", loaderPath, RegistryValueKind.String);
                appKey.SetValue("MANAGED", 1, RegistryValueKind.DWord);
            }
        }

        private static void RemoveDemandLoadRegistration()
        {
            string appKeyPath = GetCurrentUserApplicationsKeyPath();
            if (string.IsNullOrWhiteSpace(appKeyPath)) return;

            using (RegistryKey applicationsKey = Registry.CurrentUser.OpenSubKey(appKeyPath, true))
            {
                if (applicationsKey == null) return;
                try { applicationsKey.DeleteSubKeyTree(AppName, false); }
                catch { }
            }
        }

        private static string GetCurrentUserApplicationsKeyPath()
        {
            try
            {
                string productRoot = HostApplicationServices.Current.UserRegistryProductRootKey;
                if (string.IsNullOrWhiteSpace(productRoot)) return string.Empty;
                return productRoot.TrimEnd('\\') + "\\Applications";
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetMainAssemblyPath()
        {
            try
            {
                return Assembly.GetExecutingAssembly().Location;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetMainAssemblyFileName()
        {
            string path = GetMainAssemblyPath();
            string name = string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFileName(path);
            return string.IsNullOrWhiteSpace(name) ? "CDBox.dll" : name;
        }

        private static void WritePackageContents(string installRoot)
        {
            WritePackageContents(installRoot, GetMainAssemblyFileName());
        }

        private static void WritePackageContents(string installRoot, string assemblyFileName)
        {
            string productCode = "{2A1EBD9E-B00B-4C2E-8EE7-3A1C7151BEE1}";
            string upgradeCode = "{E8BB42A0-7694-4C58-98D6-13C2D2814A1E}";
            if (string.IsNullOrWhiteSpace(assemblyFileName)) assemblyFileName = GetMainAssemblyFileName();
            string appVersion = string.IsNullOrWhiteSpace(CDBoxStudioUpdateService.CurrentVersion)
                ? "3.4.1" : CDBoxStudioUpdateService.CurrentVersion;

            string[] commands = new[] { "CDBOX", "CDSTUDIO", "CDS", "CDSET", "CDINSTALL", "CDUNINSTALL", "CDUPDATE", "CDABOUT", "CDBZSET", "BZSZ", "CDLAYER", "TCGL", "CDSURF", "BMJ", "MJBZ", "CDLEN", "GCBZ", "CDNODE", "JDBZ", "CDSEC", "DM", "PLDM", "ZDM", "ZDMSZ", "SX", "SXQC", "SXMRB", "GCL", "CDQBOARD", "CDEXCEL", "GU_XL", "TCFRAMEADD", "TCFRAMECUT", "TCFRAMELAYOUT", "TCFRAMEPLACE", "TCFRAMESET", "CDSHORTCODE", "CDJMSB", "CDSHORTCODESET", "CDJMSZ" };
            var xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            xml.AppendLine("<ApplicationPackage SchemaVersion=\"1.0\" AppVersion=\"" + EscapeXml(appVersion) + "\" Name=\"CDBox\" Description=\"CDBox 管线测绘辅助插件\" Author=\"CDBox\" ProductCode=\"" + productCode + "\" UpgradeCode=\"" + upgradeCode + "\">");
            xml.AppendLine("  <CompanyDetails Name=\"CDBox\" />");
            xml.AppendLine("  <Components>");
            xml.AppendLine("    <ComponentEntry AppName=\"CDBox\" AppDescription=\"CDBox AutoCAD Plugin\" ModuleName=\"./Contents/" + EscapeXml(assemblyFileName) + "\" AppType=\".Net\" LoadOnAutoCADStartup=\"True\" LoadOnCommandInvocation=\"False\">");
            xml.AppendLine("      <Commands GroupName=\"CDBoxCommands\">");
            foreach (string command in commands)
            {
                xml.Append("        <Command Global=\"");
                xml.Append(EscapeXml(command));
                xml.Append("\" Local=\"");
                xml.Append(EscapeXml(command));
                xml.AppendLine("\" />");
            }
            xml.AppendLine("      </Commands>");
            xml.AppendLine("    </ComponentEntry>");
            xml.AppendLine("  </Components>");
            xml.AppendLine("</ApplicationPackage>");

            File.WriteAllText(Path.Combine(installRoot, "PackageContents.xml"), xml.ToString(), new UTF8Encoding(true));
        }

        private static string BuildInstallMessage(string mainMessage, string selfCheck)
        {
            string message = mainMessage ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(selfCheck))
            {
                message += "\r\n\r\n安装/更新自检：\r\n" + selfCheck;
            }

            message += "\r\n\r\n日志：" + CDBoxInstallLogger.LogFilePath;
            return message;
        }

        private static string RunInstallSelfCheck(string bundleRoot, string assemblyFileName)
        {
            bool passed;
            return RunInstallSelfCheck(bundleRoot, assemblyFileName, out passed);
        }

        private static string RunInstallSelfCheck(string bundleRoot, string assemblyFileName, out bool passed)
        {
            var ok = new List<string>();
            var warnings = new List<string>();
            var errors = new List<string>();

            try
            {
                if (string.IsNullOrWhiteSpace(bundleRoot))
                {
                    errors.Add("安装目录为空");
                    passed = false;
                    return FormatSelfCheck(ok, warnings, errors);
                }

                string contentsDir = Path.Combine(bundleRoot, ContentsFolderName);
                CheckDirectory(bundleRoot, "bundle 根目录", ok, errors);
                CheckDirectory(contentsDir, "Contents 目录", ok, errors);
                CheckFile(Path.Combine(bundleRoot, "PackageContents.xml"), "PackageContents.xml", ok, errors);
                CheckFile(Path.Combine(contentsDir, string.IsNullOrWhiteSpace(assemblyFileName) ? GetMainAssemblyFileName() : assemblyFileName), "CDBox 主 DLL", ok, errors);

                CheckFile(Path.Combine(contentsDir, "Microsoft.Web.WebView2.Core.dll"), "WebView2 Core DLL", ok, errors);
                CheckFile(Path.Combine(contentsDir, "Microsoft.Web.WebView2.WinForms.dll"), "WebView2 WinForms DLL", ok, errors);

                if (FindFileRecursive(contentsDir, "WebView2Loader.dll")) ok.Add("WebView2Loader.dll 已安装");
                else errors.Add("缺少 WebView2Loader.dll（通常位于 runtimes\\win-x64\\native）");

                if (Directory.Exists(Path.Combine(contentsDir, "runtimes"))) ok.Add("runtimes 目录已安装");
                else warnings.Add("未发现 runtimes 目录；若 Studio 无法启动，请确认 WebView2/NPOI 等 NuGet 运行时文件已随 DLL 输出");

                if (HasFrontendAssets(contentsDir)) ok.Add("Studio/Web 前端资源目录已安装或已内置");
                else warnings.Add("未发现独立 Studio/Web 前端资源目录；当前 Preview 内置 HTML 可忽略，后续使用前端构建产物时请确认已复制到输出目录");

                CheckFile(Path.Combine(contentsDir, "Updater", "CDBoxUpdater.exe"), "CDBoxUpdater.exe", ok, errors);
            }
            catch (Exception ex)
            {
                errors.Add("自检异常：" + ex.Message);
                CDBoxInstallLogger.Error("安装自检异常。", ex);
            }

            passed = errors.Count == 0;
            return FormatSelfCheck(ok, warnings, errors);
        }

        private static void CheckDirectory(string path, string displayName, IList<string> ok, IList<string> errors)
        {
            if (Directory.Exists(path)) ok.Add(displayName + "存在");
            else errors.Add(displayName + "不存在：" + path);
        }

        private static void CheckFile(string path, string displayName, IList<string> ok, IList<string> errors)
        {
            if (File.Exists(path)) ok.Add(displayName + "存在");
            else errors.Add(displayName + "不存在：" + path);
        }

        private static bool FindFileRecursive(string root, string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return false;
                foreach (string file in Directory.GetFiles(root, fileName, SearchOption.AllDirectories))
                {
                    if (File.Exists(file)) return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool HasFrontendAssets(string contentsDir)
        {
            string[] dirs =
            {
                Path.Combine(contentsDir, "Studio"),
                Path.Combine(contentsDir, "Web"),
                Path.Combine(contentsDir, "wwwroot"),
                Path.Combine(contentsDir, "dist"),
                Path.Combine(contentsDir, "assets")
            };

            foreach (string dir in dirs)
            {
                try
                {
                    if (Directory.Exists(dir) && Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Length > 0) return true;
                }
                catch
                {
                }
            }

            return true; // Preview 2.1 仍以内置 HTML 字符串为主，未提供独立前端目录时不阻断安装。
        }

        private static string FormatSelfCheck(IList<string> ok, IList<string> warnings, IList<string> errors)
        {
            var sb = new StringBuilder();
            sb.Append(errors.Count == 0 ? "通过" : "存在问题");
            sb.Append("；通过 ").Append(ok.Count).Append(" 项");
            if (warnings.Count > 0) sb.Append("，警告 ").Append(warnings.Count).Append(" 项");
            if (errors.Count > 0) sb.Append("，错误 ").Append(errors.Count).Append(" 项");
            sb.Append("。");

            foreach (string item in errors) sb.Append("\r\n[错误] ").Append(item);
            foreach (string item in warnings) sb.Append("\r\n[提示] ").Append(item);
            return sb.ToString();
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        private static bool TryStartDeferredDelete(string installRoot, out string message)
        {
            message = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(installRoot) || !Directory.Exists(installRoot)) return true;

                int pid = Process.GetCurrentProcess().Id;
                string cmdPath = Path.Combine(Path.GetTempPath(), "CDBox_Uninstall_" + pid.ToString() + ".cmd");
                string script = "@echo off\r\n" +
                    "setlocal\r\n" +
                    ":wait\r\n" +
                    "tasklist /FI \"PID eq " + pid.ToString() + "\" | find \"" + pid.ToString() + "\" >nul\r\n" +
                    "if not errorlevel 1 (\r\n" +
                    "  timeout /t 2 /nobreak >nul\r\n" +
                    "  goto wait\r\n" +
                    ")\r\n" +
                    "rmdir /S /Q \"" + installRoot + "\"\r\n" +
                    "del \"%~f0\"\r\n";

                File.WriteAllText(cmdPath, script, Encoding.Default);
                Process.Start(new ProcessStartInfo
                {
                    FileName = cmdPath,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }
    }
}
