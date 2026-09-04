using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioUpdateService
    {
        public static readonly string CurrentVersion = ReadInformationalVersion("5.1.0");
        public static readonly int CurrentVersionCode = ReadAssemblyMetadataInt("CDBoxVersionCode", 50100);
        public static readonly string ReleaseIdentity = ReadAssemblyMetadata("CDBoxReleaseIdentity", "CDBox-Studio-Preview-5.1.0");
        public static readonly string ReleaseTitle = ReadAssemblyMetadata("CDBoxReleaseTitle", "CDBox Studio Preview 5.1.0");
        public static readonly string DefaultChannel = ReadAssemblyMetadata("CDBoxUpdateChannel", "preview");
        public const string DefaultUpdateSourceName = "CDBox 官方发布源";
        public static readonly string DefaultUpdateSourceUrl =
            CDBoxStudioUpdateSourceCatalog.GetManifestUrl(DefaultChannel);

        private const int RequestTimeoutMilliseconds = 15000;
        private const int DownloadTimeoutMilliseconds = 30000;
        private const int BufferSize = 128 * 1024;

        public static IList<CDBoxStudioUpdateSource> GetBuiltInManifestSources()
        {
            return CDBoxStudioUpdateSourceCatalog.CreateManifestSources(
                DefaultChannel);
        }

        private static string ReadInformationalVersion(string fallback)
        {
            try
            {
                var attribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (attribute != null && !string.IsNullOrWhiteSpace(attribute.InformationalVersion)) return attribute.InformationalVersion.Trim();
            }
            catch
            {
            }
            return fallback;
        }

        private static string ReadAssemblyMetadata(string key, string fallback)
        {
            try
            {
                foreach (AssemblyMetadataAttribute attribute in Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>())
                {
                    if (attribute == null || !string.Equals(attribute.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrWhiteSpace(attribute.Value)) return attribute.Value.Trim();
                }
            }
            catch
            {
            }
            return fallback;
        }

        private static int ReadAssemblyMetadataInt(string key, int fallback)
        {
            int value;
            return int.TryParse(ReadAssemblyMetadata(key, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0
                ? value
                : fallback;
        }

        public static CDBoxStudioUpdateResult Check(CDBoxStudioSettings settings)
        {
            settings = settings ?? new CDBoxStudioSettings();
            settings.Normalize();
            IList<CDBoxStudioUpdateSource> sources = GetBuiltInManifestSources();
            var failures = new List<string>();
            CDBoxStudioUpdateResult lastResult = CreateBaseCheckResult();

            foreach (CDBoxStudioUpdateSource source in sources)
            {
                if (source == null || !source.Enabled || string.IsNullOrWhiteSpace(source.Url)) continue;
                CDBoxStudioUpdateResult result = CreateBaseCheckResult();
                result.SourceName = source.DisplayName;
                result.SourceUrl = source.Url.Trim();
                lastResult = result;
                try
                {
                    CDBoxStudioLogger.Info("开始检查 Studio 更新。固定通道=" + DefaultChannel
                        + ", ManifestSource=" + result.SourceName + ", Url=" + result.SourceUrl);
                    string json = DownloadString(result.SourceUrl);
                    if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("更新源返回内容为空。");

                    Dictionary<string, object> root = Deserialize(json);
                    FillResultFromFixedManifest(result, root, result.SourceUrl);
                    result.Success = true;
                    result.UpdateAvailable = IsUpdateAvailable(result.VersionCode, result.LatestVersion,
                        result.CurrentVersionCode, result.CurrentVersion);

                    CDBoxStudioLogger.Info("更新检查完成。ManifestSource=" + result.SourceName
                        + ", Current=" + result.CurrentVersion + "(" + result.CurrentVersionCode + ")"
                        + ", Latest=" + result.LatestVersion + "(" + result.VersionCode + ")"
                        + ", UpdateAvailable=" + result.UpdateAvailable
                        + ", InstallerSourceCount=" + result.Sources.Count);
                    return result;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.UpdateAvailable = false;
                    result.ErrorMessage = ex.Message;
                    failures.Add(result.SourceName + "：" + ex.Message);
                    CDBoxStudioLogger.Error("update.json 更新源失败，准备自动尝试下一个。Source="
                        + result.SourceName + ", Url=" + result.SourceUrl, ex);
                }
            }

            lastResult.Success = false;
            lastResult.UpdateAvailable = false;
            lastResult.SourceName = "内置更新源";
            lastResult.SourceUrl = string.Empty;
            lastResult.ErrorMessage = failures.Count == 0
                ? "没有可用的内置 update.json 更新源。"
                : "所有内置 update.json 更新源均失败：" + string.Join("；", failures.ToArray());
            CDBoxStudioLogger.Error("检查 Studio 更新失败。" + lastResult.ErrorMessage, null);
            return lastResult;
        }

        private static CDBoxStudioUpdateResult CreateBaseCheckResult()
        {
            var result = new CDBoxStudioUpdateResult
            {
                CurrentVersion = CurrentVersion,
                CurrentVersionCode = CurrentVersionCode,
                Channel = DefaultChannel,
                CheckedAt = DateTime.Now
            };
            CDBoxStudioComponentUpdatePlan plan =
                CDBoxStudioComponentUpdatePlan.Capture();
            result.InstalledComponentIds = plan.SelectedComponentIds;
            result.InstalledComponentNames = plan.SelectedComponentNames;
            result.ComponentPlanText = plan.Summary;
            result.ComponentStateWarning = plan.Warning;
            return result;
        }

        public static CDBoxStudioUpdateDownloadResult DownloadAndVerify(CDBoxStudioSettings settings)
        {
            return DownloadAndVerify(settings, null);
        }

        public static CDBoxStudioUpdateDownloadResult DownloadAndVerify(CDBoxStudioSettings settings, Action<int, string> progress)
        {
            settings = settings ?? new CDBoxStudioSettings();
            settings.Normalize();

            var download = new CDBoxStudioUpdateDownloadResult
            {
                StartedAt = DateTime.Now,
                CurrentVersion = CurrentVersion,
                CurrentVersionCode = CurrentVersionCode,
                Channel = DefaultChannel
            };
            CDBoxStudioComponentUpdatePlan initialPlan =
                CDBoxStudioComponentUpdatePlan.Capture();
            download.InstalledComponentIds = initialPlan.SelectedComponentIds;
            download.InstalledComponentNames = initialPlan.SelectedComponentNames;
            download.ComponentPlanText = initialPlan.Summary;
            download.ComponentStateWarning = initialPlan.Warning;

            try
            {
                CDBoxStudioLogger.Info("开始下载 CDBox 安装器。当前组件组合："
                    + download.ComponentPlanText
                    + "。下载并校验通过后将启动同一个安装器，由用户完成更新或调整业务模块。");
                ReportProgress(progress, 0, "正在检查 update.json");
                CDBoxStudioUpdateResult manifest = Check(settings);
                CopyManifestToDownloadResult(manifest, download);

                ReportProgress(progress, 5, "更新清单读取完成");

                if (!manifest.Success)
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(manifest.ErrorMessage) ? "检查更新失败，无法下载。" : manifest.ErrorMessage);
                }

                if (manifest.Sources.Count == 0)
                {
                    throw new InvalidOperationException("update.json 中 installer.package.versionedUrl 为空，无法下载安装器。");
                }

                if (!IsUsableSha256(manifest.Sha256))
                {
                    throw new InvalidOperationException("update.json 中 installer.package.sha256 为空或无效。为避免安装器被篡改，本阶段已中止下载。");
                }

                string downloadDir = GetUpdateDownloadDirectory(manifest.LatestVersion);
                Directory.CreateDirectory(downloadDir);
                string fileName = SanitizeFileName(string.IsNullOrWhiteSpace(manifest.InstallerFileName)
                    ? "CDBox安装器." + manifest.LatestVersion + ".exe"
                    : manifest.InstallerFileName);
                if (!fileName.EndsWith(".exe",
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "网络更新只接受 EXE 安装器，不再支持 ZIP 更新包："
                        + fileName);
                string finalPath = Path.Combine(downloadDir, fileName);

                if (File.Exists(finalPath))
                {
                    string existingHash = ComputeSha256(finalPath);
                    if (SameHash(existingHash, manifest.Sha256))
                    {
                        download.Success = true;
                        download.Verified = true;
                        download.FilePath = finalPath;
                        download.Sha256Expected = manifest.Sha256;
                        download.Sha256Actual = existingHash;
                        download.FinishedAt = DateTime.Now;
                        CDBoxStudioLogger.Info("安装器已存在且 SHA256 校验通过，跳过重复下载。Path=" + finalPath);
                        PrepareAndLaunchInstaller(download, progress);
                        return download;
                    }

                    CDBoxStudioLogger.Warn("已存在安装器但 SHA256 不匹配，将重新下载。Path=" + finalPath);
                    TryDelete(finalPath);
                }

                foreach (CDBoxStudioUpdateSource source in manifest.Sources)
                {
                    if (source == null || string.IsNullOrWhiteSpace(source.Url) || !source.Enabled) continue;

                    var attempt = new CDBoxStudioUpdateDownloadAttempt
                    {
                        SourceName = source.DisplayName,
                        SourceUrl = source.Url.Trim(),
                        BytesTotal = manifest.InstallerSizeBytes,
                        StartedAt = DateTime.Now
                    };
                    download.Attempts.Add(attempt);

                    string tempPath = Path.Combine(downloadDir, fileName + "." + Guid.NewGuid().ToString("N") + ".download");
                    try
                    {
                        CDBoxStudioLogger.Info("开始从下载源获取安装器：" + attempt.SourceName + " -> " + attempt.SourceUrl);
                        ReportProgress(progress, 8, "正在连接下载服务器");
                        DownloadFile(source.Url.Trim(), tempPath, manifest.InstallerSizeBytes, attempt, progress, attempt.SourceName);
                        attempt.InstallerPath = tempPath;

                        ReportProgress(progress, 96, "正在校验 SHA256");
                        string hash = ComputeSha256(tempPath);
                        attempt.Sha256Actual = hash;
                        if (!SameHash(hash, manifest.Sha256))
                        {
                            attempt.Success = false;
                            attempt.Verified = false;
                            attempt.ErrorMessage = "SHA256 校验失败。期望 " + manifest.Sha256 + "，实际 " + hash;
                            attempt.FinishedAt = DateTime.Now;
                            CDBoxStudioLogger.Warn("安装器 SHA256 校验失败，准备切换下载源。" + attempt.ErrorMessage);
                            TryDelete(tempPath);
                            continue;
                        }

                        if (File.Exists(finalPath)) TryDelete(finalPath);
                        File.Move(tempPath, finalPath);
                        attempt.InstallerPath = finalPath;
                        attempt.Success = true;
                        attempt.Verified = true;
                        attempt.FinishedAt = DateTime.Now;

                        download.Success = true;
                        download.Verified = true;
                        download.FilePath = finalPath;
                        download.SourceName = attempt.SourceName;
                        download.SourceUrl = attempt.SourceUrl;
                        download.Sha256Expected = manifest.Sha256;
                        download.Sha256Actual = hash;
                        download.FinishedAt = DateTime.Now;

                        CDBoxStudioLogger.Info("安装器下载完成且 SHA256 校验通过。Path=" + finalPath + ", Source=" + attempt.SourceName);
                        PrepareAndLaunchInstaller(download, progress);
                        return download;
                    }
                    catch (Exception ex)
                    {
                        attempt.Success = false;
                        attempt.Verified = false;
                        attempt.ErrorMessage = ex.Message;
                        attempt.FinishedAt = DateTime.Now;
                        TryDelete(tempPath);
                        CDBoxStudioLogger.Error("下载源失败，准备切换下一个下载源。Source=" + attempt.SourceName, ex);
                    }
                }

                throw new InvalidOperationException("所有下载源均失败，未能完成安装器下载与 SHA256 校验。");
            }
            catch (Exception ex)
            {
                download.Success = false;
                download.Verified = false;
                download.ErrorMessage = ex.Message;
                download.FinishedAt = DateTime.Now;
                CDBoxStudioLogger.Error("CDBox 安装器下载/校验失败。", ex);
            }

            return download;
        }

        private static void PrepareAndLaunchInstaller(CDBoxStudioUpdateDownloadResult download, Action<int, string> progress)
        {
            if (download == null || !download.Success || !download.Verified) return;

            ReportProgress(progress, 97,
                "正在启动统一安装器");
            CDBoxStudioInstallerLaunchResult launch =
                CDBoxStudioInstallerLauncher.PrepareAndLaunch(download);
            if (launch == null)
            {
                download.InstallerErrorMessage = "安装器启动结果为空。";
                ReportProgress(progress, 100, "安装器已校验，但未能启动");
                return;
            }

            download.InstallerPrepared = File.Exists(
                download.FilePath);
            download.InstallerStarted = launch.Started;
            download.InstallerErrorMessage = launch.ErrorMessage ?? string.Empty;
            download.InstallerCommand = launch.CommandLine ?? string.Empty;

            if (launch.Started)
            {
                ReportProgress(progress, 100,
                    "安装器已启动；请关闭 AutoCAD 后在安装器中完成更新");
                CDBoxStudioLogger.Info(
                    "统一安装器已启动。Installer="
                    + launch.InstallerPath);
            }
            else
            {
                ReportProgress(progress, 100,
                    "安装器已校验，但启动失败");
                CDBoxStudioLogger.Warn(
                    "安装器已校验，但未启动："
                    + download.InstallerErrorMessage);
            }
        }

        private static void ReportProgress(Action<int, string> progress, int percent, string message)
        {
            if (progress == null) return;
            try { progress(Math.Max(0, Math.Min(100, percent)), message ?? string.Empty); }
            catch (Exception ex) { CDBoxStudioLogger.Warn("更新进度回调失败：" + ex.Message); }
        }

        private static string DownloadString(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = RequestTimeoutMilliseconds;
            request.ReadWriteTimeout = RequestTimeoutMilliseconds;
            request.UserAgent = "CDBox-Studio/" + CurrentVersion;
            request.Accept = "application/json,text/plain,*/*";
            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

            using (var response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            {
                if (stream == null) return string.Empty;
                Encoding encoding = Encoding.UTF8;
                string charset = response.CharacterSet;
                if (!string.IsNullOrWhiteSpace(charset))
                {
                    try { encoding = Encoding.GetEncoding(charset); }
                    catch { encoding = Encoding.UTF8; }
                }

                using (var reader = new StreamReader(stream, encoding))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static void DownloadFile(string url, string path, long expectedBytes, CDBoxStudioUpdateDownloadAttempt attempt, Action<int, string> progress, string sourceName)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = DownloadTimeoutMilliseconds;
            request.ReadWriteTimeout = DownloadTimeoutMilliseconds;
            request.UserAgent = "CDBox-Studio/" + CurrentVersion;
            request.Accept = "application/vnd.microsoft.portable-executable,application/octet-stream,*/*";
            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

            using (var response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            {
                if (stream == null) throw new InvalidOperationException("下载响应流为空。");

                long total = response.ContentLength > 0 ? response.ContentLength : expectedBytes;
                if (attempt != null) attempt.BytesTotal = total;

                byte[] buffer = new byte[BufferSize];
                long received = 0;
                int lastLoggedPercent = -1;

                using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    while (true)
                    {
                        int read = stream.Read(buffer, 0, buffer.Length);
                        if (read <= 0) break;
                        file.Write(buffer, 0, read);
                        received += read;
                        if (attempt != null) attempt.BytesReceived = received;

                        if (total > 0)
                        {
                            int percent = (int)Math.Min(100, Math.Floor(received * 100d / total));
                            if (percent >= lastLoggedPercent + 10 || percent == 100)
                            {
                                lastLoggedPercent = percent;
                                ReportProgress(progress, Math.Max(10, Math.Min(95, percent)), "正在下载安装器：" + percent + "%");
                                CDBoxStudioLogger.Info("安装器下载进度：" + percent + "% (" + received + "/" + total + ")");
                            }
                        }
                    }
                }

                if (attempt != null) attempt.BytesReceived = received;
                if (expectedBytes > 0 && received != expectedBytes)
                {
                    CDBoxStudioLogger.Warn("下载大小与 update.json installer.package.sizeBytes 不一致。Expected=" + expectedBytes + ", Actual=" + received);
                }
            }
        }

        private static Dictionary<string, object> Deserialize(string json)
        {
            var serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = 1024 * 1024 * 8;
            object parsed = serializer.DeserializeObject(json);
            var root = parsed as Dictionary<string, object>;
            if (root == null) throw new InvalidOperationException("update.json 格式不正确：根节点不是 JSON 对象。");
            return root;
        }

        private static void FillResultFromFixedManifest(CDBoxStudioUpdateResult result, Dictionary<string, object> root, string sourceUrl)
        {
            if (result == null || root == null) throw new InvalidOperationException("update.json 格式不正确。");

            int schemaVersion = RequiredInt(root, "schemaVersion");
            if (schemaVersion != 2)
                throw new InvalidOperationException(
                    "当前客户端只支持 Release Manifest schemaVersion = 2。");
            result.Channel = RequiredString(root, "channel").ToLowerInvariant();
            result.SourceUrl = sourceUrl;

            if (!string.Equals(result.Channel, DefaultChannel, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "update.json 发布通道与当前客户端不一致。Manifest="
                    + result.Channel + ", Expected=" + DefaultChannel);
            }

            Dictionary<string, object> release = RequiredObject(root,
                "release");
            result.LatestVersion = RequiredString(release, "version");
            if (!CDBoxReleaseVersionComparer.IsValid(result.LatestVersion))
                throw new InvalidOperationException("update.json release.version 不是有效版本号。");
            result.VersionCode = VersionToCode(result.LatestVersion);
            result.Title = RequiredString(release, "title");
            result.ReleaseDate = RequiredString(release, "publishedAt");
            result.Notes = RequiredString(release, "summary");
            string releaseNotesUrl = RequiredOfficialHttpsUrl(release,
                "releaseNotesUrl");
            if (!string.Equals(releaseNotesUrl,
                    CDBoxStudioUpdateSourceCatalog.GetReleaseNotesUrl(
                        result.LatestVersion), StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "update.json release.releaseNotesUrl 不符合统一 URL 规则。");
            result.Mandatory = false;

            Dictionary<string, object> installer = RequiredObject(root,
                "installer");
            string installerVersion = RequiredString(installer, "version");
            string latestUrl = RequiredOfficialHttpsUrl(installer,
                "downloadUrl");
            if (!string.Equals(latestUrl,
                    CDBoxStudioUpdateSourceCatalog.LatestInstallerUrl,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "update.json installer.downloadUrl 不是官方 Latest 地址。");
            Dictionary<string, object> package = RequiredObject(installer,
                "package");
            result.InstallerFileName = RequiredString(package, "fileName");
            result.InstallerSizeBytes = RequiredLong(package, "sizeBytes");
            result.Sha256 = RequiredString(package, "sha256");
            if (!result.InstallerFileName.EndsWith(".exe",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "update.json installer.package.fileName 必须是 EXE 安装器。");
            if (result.InstallerSizeBytes <= 0)
                throw new InvalidOperationException(
                    "update.json installer.package.sizeBytes 必须大于 0。");
            string versionedUrl = RequiredOfficialHttpsUrl(package,
                "versionedUrl");
            string urlFileName = Uri.UnescapeDataString(Path.GetFileName(
                new Uri(versionedUrl).AbsolutePath));
            if (!string.Equals(urlFileName, result.InstallerFileName,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "update.json 版本安装器 URL 与文件名不一致。");
            if (!string.Equals(versionedUrl,
                    CDBoxStudioUpdateSourceCatalog.GetVersionedInstallerUrl(
                        installerVersion, result.InstallerFileName),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "update.json installer.package.versionedUrl 不符合统一 URL 规则。");
            result.DownloadUrl = versionedUrl;
            result.Sources.Add(new CDBoxStudioUpdateSource
            {
                Name = "CDBox 官方 CDN",
                Url = versionedUrl,
                Sha256 = result.Sha256,
                Enabled = true
            });
        }

        private static void CopyManifestToDownloadResult(CDBoxStudioUpdateResult manifest, CDBoxStudioUpdateDownloadResult download)
        {
            if (manifest == null || download == null) return;
            download.CurrentVersion = manifest.CurrentVersion;
            download.CurrentVersionCode = manifest.CurrentVersionCode;
            download.LatestVersion = manifest.LatestVersion;
            download.VersionCode = manifest.VersionCode;
            download.Channel = manifest.Channel;
            download.InstallerFileName = manifest.InstallerFileName;
            download.InstallerSizeBytes = manifest.InstallerSizeBytes;
            download.Sha256Expected = manifest.Sha256;
            download.InstalledComponentIds = manifest.InstalledComponentIds
                ?? new string[0];
            download.InstalledComponentNames = manifest.InstalledComponentNames
                ?? new string[0];
            download.ComponentPlanText = manifest.ComponentPlanText
                ?? string.Empty;
            download.ComponentStateWarning = manifest.ComponentStateWarning
                ?? string.Empty;
        }

        private static bool IsUpdateAvailable(int latestCode, string latestVersion, int currentCode, string currentVersion)
        {
            if (CDBoxReleaseVersionComparer.IsValid(latestVersion)
                && CDBoxReleaseVersionComparer.IsValid(currentVersion))
                return CDBoxReleaseVersionComparer.Compare(latestVersion,
                    currentVersion) > 0;
            if (latestCode > 0 && currentCode > 0) return latestCode > currentCode;
            return CompareVersions(latestVersion, currentVersion) > 0;
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        private static bool SameHash(string left, string right)
        {
            return string.Equals(NormalizeSha256(left), NormalizeSha256(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool IsUsableSha256(string value)
        {
            string hash = NormalizeSha256(value);
            return hash.Length == 64 && Regex.IsMatch(hash, "^[0-9a-f]{64}$");
        }

        private static string GetUpdateDownloadDirectory(string version)
        {
            string safeVersion = SanitizeFileName(string.IsNullOrWhiteSpace(version) ? "unknown" : version);
            return Path.Combine(CDBoxStudioLogger.LogDirectory, "Updates", safeVersion);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "CDBox安装器.exe";
            string text = value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) text = text.Replace(c, '_');
            return text;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Warn("删除临时更新文件失败：" + path + "，" + ex.Message);
            }
        }

        private static Dictionary<string, object> RequiredObject(Dictionary<string, object> map, string key)
        {
            object value;
            if (!TryGetValueIgnoreCase(map, key, out value)) throw new InvalidOperationException("update.json 格式不正确：缺少 " + key + "。");
            var result = value as Dictionary<string, object>;
            if (result == null) throw new InvalidOperationException("update.json 格式不正确：" + key + " 不是对象。");
            return result;
        }

        private static string RequiredHttpsUrl(Dictionary<string, object> map, string key)
        {
            string value = RequiredString(map, key);
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "update.json 格式不正确：" + key + " 必须是 HTTPS 绝对地址。");
            }

            return uri.AbsoluteUri;
        }

        private static string RequiredOfficialHttpsUrl(
            Dictionary<string, object> map, string key)
        {
            string value = RequiredHttpsUrl(map, key);
            var actual = new Uri(value);
            var official = new Uri(CDBoxStudioUpdateSourceCatalog.OfficialBaseUrl);
            if (!string.Equals(actual.Host, official.Host,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "update.json " + key + " 不属于 CDBox 官方发布域名。");
            return value;
        }

        private static string RequiredString(Dictionary<string, object> map, string key)
        {
            string value = OptionalString(map, key);
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("update.json 格式不正确：缺少 " + key + "。");
            return value;
        }

        private static string OptionalString(Dictionary<string, object> map, string key)
        {
            object value;
            if (!TryGetValueIgnoreCase(map, key, out value) || value == null) return string.Empty;
            return (Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
        }

        private static int RequiredInt(Dictionary<string, object> map, string key)
        {
            object value;
            if (!TryGetValueIgnoreCase(map, key, out value) || value == null) throw new InvalidOperationException("update.json 格式不正确：缺少 " + key + "。");
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { }
            int parsed;
            if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return parsed;
            throw new InvalidOperationException("update.json 格式不正确：" + key + " 不是整数。");
        }

        private static long RequiredLong(Dictionary<string, object> map, string key)
        {
            object value;
            if (!TryGetValueIgnoreCase(map, key, out value) || value == null) throw new InvalidOperationException("update.json 格式不正确：缺少 " + key + "。");
            try { return Convert.ToInt64(value, CultureInfo.InvariantCulture); }
            catch { }
            long parsed;
            if (long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return parsed;
            throw new InvalidOperationException("update.json 格式不正确：" + key + " 不是整数。");
        }

        private static bool TryGetValueIgnoreCase(Dictionary<string, object> map, string key, out object value)
        {
            value = null;
            if (map == null || string.IsNullOrWhiteSpace(key)) return false;
            if (map.TryGetValue(key, out value)) return true;
            foreach (KeyValuePair<string, object> pair in map)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }
            return false;
        }

        private static int CompareVersions(string left, string right)
        {
            List<int> a = ExtractVersionNumbers(left);
            List<int> b = ExtractVersionNumbers(right);
            int count = Math.Max(a.Count, b.Count);
            for (int i = 0; i < count; i++)
            {
                int av = i < a.Count ? a[i] : 0;
                int bv = i < b.Count ? b[i] : 0;
                if (av > bv) return 1;
                if (av < bv) return -1;
            }
            return 0;
        }

        private static int VersionToCode(string version)
        {
            List<int> parts = ExtractVersionNumbers(version);
            if (parts.Count == 0) return 0;

            long major = parts.Count > 0 ? parts[0] : 0;
            long minor = parts.Count > 1 ? parts[1] : 0;
            long patch = parts.Count > 2 ? parts[2] : 0;
            long value = major * 10000L + minor * 100L + patch;
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static List<int> ExtractVersionNumbers(string version)
        {
            var result = new List<int>();
            if (string.IsNullOrWhiteSpace(version)) return result;
            MatchCollection matches = Regex.Matches(version, "\\d+");
            foreach (Match match in matches)
            {
                int value;
                if (int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) result.Add(value);
            }
            return result;
        }
    }
}
