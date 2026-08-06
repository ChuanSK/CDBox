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
                    return CDBoxInstallResult.Fail("????????? CDBox.dll??????", installRoot);
                }

                CDBoxUpdateSourceValidationResult sourceValidation = CDBoxUpdateSourceValidator.Validate(sourceAssemblyPath);
                if (!sourceValidation.Valid)
                {
                    return CDBoxInstallResult.Fail("?????????? CDBox ???????????\r\n\r\n" + sourceValidation.Message, installRoot);
                }

                Directory.CreateDirectory(contentsDir);
                string sourceDir = Path.GetDirectoryName(sourceAssemblyPath);
                CopyRuntimeFiles(sourceDir, contentsDir);
                WritePackageContents(installRoot);
                RegisterDemandLoad(GetInstalledDllPath());

                string selfCheck = RunInstallSelfCheck(installRoot, GetMainAssemblyFileName());
                CDBoxInstallLogger.Info("??????????" + installRoot + "?????" + (sourceDir ?? string.Empty));
                CDBoxInstallLogger.Info("???????" + selfCheck.Replace("\r\n", " | "));

                return CDBoxInstallResult.Ok(BuildInstallMessage("CDBox ???? CAD ????????? CAD ??????", selfCheck), installRoot, selfCheck);
            }
            catch (UnauthorizedAccessException ex)
            {
                return CDBoxInstallResult.Fail("?????? CAD ?????????????? CAD ???????? CDBox.bundle ?? CAD ???\r\n\r\n" + ex.Message, installRoot);
            }
            catch (Exception ex)
            {
                return CDBoxInstallResult.Fail("?????" + ex.Message, installRoot);
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
                // ?????????????????????????
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
                return CDBoxInstallResult.Ok("CDBox ??????????????????", installRoot);
            }

            bool deferred = TryStartDeferredDelete(installRoot, out string deferredMessage);
            if (deferred)
            {
                return new CDBoxInstallResult
                {
                    Success = true,
                    InstallRoot = installRoot,
                    DeferredDeleteStarted = true,
                    Message = "CDBox ???????????? DLL ???? CAD ????????? CAD ??????????"
                };
            }

            return CDBoxInstallResult.Fail("CDBox ??????????????????????" + deleteMessage + "\r\n" + deferredMessage + "\r\n??? CAD ??????" + installRoot, installRoot);
        }

        public static CDBoxInstallResult ScheduleUpdateFromDll(string newDllPath)
        {
            string installRoot = GetInstallRoot();
            string packageSourceRoot = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(newDllPath) || !File.Exists(newDllPath))
                {
                    return CDBoxInstallResult.Fail("???????? CDBox.dll?", installRoot);
                }

                string sourceDir = Path.GetDirectoryName(newDllPath);
                if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
                {
                    return CDBoxInstallResult.Fail("?????? DLL ?????", installRoot);
                }

                string selectedName = Path.GetFileName(newDllPath);
                string currentName = GetMainAssemblyFileName();
                if (!string.Equals(selectedName, currentName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(selectedName, "CDBox.dll", StringComparison.OrdinalIgnoreCase))
                {
                    return CDBoxInstallResult.Fail("??? CDBox ? DLL ????????" + selectedName, installRoot);
                }

                CDBoxUpdateSourceValidationResult sourceValidation = CDBoxUpdateSourceValidator.Validate(newDllPath);
                if (!sourceValidation.Valid)
                {
                    CDBoxInstallLogger.Warn("????????" + sourceValidation.Message.Replace("\r\n", " | "));
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
                CDBoxInstallLogger.Info("????????????????" + stagingBundle + "?????" + sourceDir);
                CDBoxInstallLogger.Info("?????????" + selfCheck.Replace("\r\n", " | "));

                if (!stagingValid)
                {
                    return CDBoxInstallResult.Fail("????????????????????????\r\n\r\n" + selfCheck, installRoot);
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
                    SourceName = "????????",
                    SourceUrl = Path.GetFullPath(newDllPath),
                    FinishedAt = DateTime.Now
                };

                string sourceUpdater = Path.Combine(sourceDir, "Updater", "CDBoxUpdater.exe");
                CDBoxStudioUpdaterLaunchResult launch = CDBoxStudioUpdaterLauncher.PrepareAndLaunch(download, sourceUpdater);
                if (launch == null || !launch.Started)
                {
                    string launchError = launch == null ? "???????????" : launch.ErrorMessage;
                    return CDBoxInstallResult.Fail(
                        "???????????????????????????" + launchError
                        + "\r\n\r\n???????" + packagePath,
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
                        "???????????????????????????????? AutoCAD?????????????????? CAD?????????????????????",
                        selfCheck)
                };
            }
            catch (Exception ex)
            {
                CDBoxInstallLogger.Error("???????", ex);
                return CDBoxInstallResult.Fail("???????" + ex.Message + "\r\n\r\n???" + CDBoxInstallLogger.LogFilePath, installRoot);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(packageSourceRoot) && Directory.Exists(packageSourceRoot))
                {
                    try { Directory.Delete(packageSourceRoot, true); }
                    catch (Exception ex) { CDBoxInstallLogger.Warn("?????????????" + ex.Message); }
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
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir)) throw new DirectoryNotFoundException("???????" + sourceDir);

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
                CDBoxInstallLogger.Info("???????" + sourceDir + " -> " + targetDir);
            }
            catch (Exception ex)
            {
                CDBoxInstallLogger.Warn("?????????" + sourceDir + " -> " + targetDir + "?" + ex.Message);
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
            if (string.IsNullOrWhiteSpace(appKeyPath)) throw new InvalidOperationException("?????? AutoCAD ????????????????????? ");

            using (RegistryKey appKey = Registry.CurrentUser.CreateSubKey(appKeyPath + "\\" + AppName))
            {
                if (appKey == null) throw new InvalidOperationException("???? CDBox ????????? ");

                appKey.SetValue("DESCRIPTION", "CDBox ????????", RegistryValueKind.String);
                appKey.SetValue("LOADCTRLS", 2, RegistryValueKind.DWord); // 2 = AutoCAD ?????
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
            xml.AppendLine("<ApplicationPackage SchemaVersion=\"1.0\" AppVersion=\"" + EscapeXml(appVersion) + "\" Name=\"CDBox\" Description=\"CDBox ????????\" Author=\"CDBox\" ProductCode=\"" + productCode + "\" UpgradeCode=\"" + upgradeCode + "\">");
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
                message += "\r\n\r\n??/?????\r\n" + selfCheck;
            }

            message += "\r\n\r\n???" + CDBoxInstallLogger.LogFilePath;
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
                    errors.Add("??????");
                    passed = false;
                    return FormatSelfCheck(ok, warnings, errors);
                }

                string contentsDir = Path.Combine(bundleRoot, ContentsFolderName);
                CheckDirectory(bundleRoot, "bundle ???", ok, errors);
                CheckDirectory(contentsDir, "Contents ??", ok, errors);
                CheckFile(Path.Combine(bundleRoot, "PackageContents.xml"), "PackageContents.xml", ok, errors);
                CheckFile(Path.Combine(contentsDir, string.IsNullOrWhiteSpace(assemblyFileName) ? GetMainAssemblyFileName() : assemblyFileName), "CDBox ? DLL", ok, errors);

                CheckFile(Path.Combine(contentsDir, "Microsoft.Web.WebView2.Core.dll"), "WebView2 Core DLL", ok, errors);
                CheckFile(Path.Combine(contentsDir, "Microsoft.Web.WebView2.WinForms.dll"), "WebView2 WinForms DLL", ok, errors);

                if (FindFileRecursive(contentsDir, "WebView2Loader.dll")) ok.Add("WebView2Loader.dll ???");
                else errors.Add("?? WebView2Loader.dll????? runtimes\\win-x64\\native?");

                if (Directory.Exists(Path.Combine(contentsDir, "runtimes"))) ok.Add("runtimes ?????");
                else warnings.Add("??? runtimes ???? Studio ???????? WebView2/NPOI ? NuGet ??????? DLL ??");

                if (HasFrontendAssets(contentsDir)) ok.Add("Studio/Web ?????????????");
                else warnings.Add("????? Studio/Web ????????? Preview ?? HTML ??????????????????????????");

                CheckFile(Path.Combine(contentsDir, "Updater", "CDBoxUpdater.exe"), "CDBoxUpdater.exe", ok, errors);
            }
            catch (Exception ex)
            {
                errors.Add("?????" + ex.Message);
                CDBoxInstallLogger.Error("???????", ex);
            }

            passed = errors.Count == 0;
            return FormatSelfCheck(ok, warnings, errors);
        }

        private static void CheckDirectory(string path, string displayName, IList<string> ok, IList<string> errors)
        {
            if (Directory.Exists(path)) ok.Add(displayName + "??");
            else errors.Add(displayName + "????" + path);
        }

        private static void CheckFile(string path, string displayName, IList<string> ok, IList<string> errors)
        {
            if (File.Exists(path)) ok.Add(displayName + "??");
            else errors.Add(displayName + "????" + path);
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

            return true; // Preview 2.1 ???? HTML ??????????????????????
        }

        private static string FormatSelfCheck(IList<string> ok, IList<string> warnings, IList<string> errors)
        {
            var sb = new StringBuilder();
            sb.Append(errors.Count == 0 ? "??" : "????");
            sb.Append("??? ").Append(ok.Count).Append(" ?");
            if (warnings.Count > 0) sb.Append("??? ").Append(warnings.Count).Append(" ?");
            if (errors.Count > 0) sb.Append("??? ").Append(errors.Count).Append(" ?");
            sb.Append("?");

            foreach (string item in errors) sb.Append("\r\n[??] ").Append(item);
            foreach (string item in warnings) sb.Append("\r\n[??] ").Append(item);
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
