using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioUpdateService
    {
        public static readonly string CurrentVersion = ReadInformationalVersion("5.1.9");
        public static readonly int CurrentVersionCode = ReadAssemblyMetadataInt("CDBoxVersionCode", 50109);
        public static readonly string ReleaseIdentity = ReadAssemblyMetadata("CDBoxReleaseIdentity", "CDBox-Studio-Preview-5.1.9");
        public static readonly string ReleaseTitle = ReadAssemblyMetadata("CDBoxReleaseTitle", "CDBox Studio Preview 5.1.9");
        public static readonly string DefaultChannel = ReadAssemblyMetadata("CDBoxUpdateChannel", "preview");
        private const int MaxResponseCharacters = 128 * 1024;

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
            return Check(settings, ReadServiceResponse);
        }

        // 可注入只读传输，测试无需访问真实发布服务。
        internal static CDBoxStudioUpdateResult Check(CDBoxStudioSettings settings, Func<string, string> read)
        {
            settings = settings ?? new CDBoxStudioSettings();
            settings.Normalize();
            var result = new CDBoxStudioUpdateResult { Channel = settings.UpdateChannel };
            try
            {
                string url = CDBoxStudioUpdateSourceCatalog.BuildServiceUrl(
                    settings.ReleaseServiceUrl, settings.UpdateChannel);
                CDBoxStudioLogger.Info("查询版本服务。Channel=" + settings.UpdateChannel);
                result = ParseResponse(read(url), settings.UpdateChannel, CurrentVersion);
                CDBoxStudioLogger.Info("版本检查完成。Channel=" + result.Channel
                    + ", Latest=" + result.LatestVersion + ", UpdateAvailable=" + result.UpdateAvailable);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.UpdateAvailable = false;
                result.ErrorMessage = ex.Message;
                CDBoxStudioLogger.Warn("版本服务检查失败：" + ex.Message);
            }
            return result;
        }

        internal static CDBoxStudioUpdateResult ParseResponse(string json, string channel, string currentVersion)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaxResponseCharacters)
                throw new InvalidOperationException("版本服务响应为空或过大。");
            var serializer = new JavaScriptSerializer { MaxJsonLength = MaxResponseCharacters, RecursionLimit = 16 };
            var root = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null) throw new InvalidOperationException("版本服务响应必须为 JSON 对象。");
            string actualChannel = ReadText(root, "channel", true).ToLowerInvariant();
            if ((channel != "stable" && channel != "preview") || actualChannel != channel)
                throw new InvalidOperationException("版本服务返回的通道与所选通道不一致。");
            object value;
            if (!root.TryGetValue("release", out value))
                throw new InvalidOperationException("版本服务缺少 release 字段。");
            var result = new CDBoxStudioUpdateResult
            {
                Channel = channel, CurrentVersion = currentVersion, Success = true
            };
            if (value == null) return result;
            var release = value as Dictionary<string, object>;
            if (release == null) throw new InvalidOperationException("版本服务 release 必须为对象或 null。");
            string version = ReadText(release, "version", true);
            if (!CDBoxReleaseVersionComparer.IsValid(version)
                || !CDBoxReleaseVersionComparer.IsValid(currentVersion))
                throw new InvalidOperationException("版本服务或本地版本号格式无效。");
            if (channel == "stable" && version.IndexOf('-') >= 0)
                throw new InvalidOperationException("Stable 通道不能返回预发行版本。");
            result.HasRelease = true;
            result.LatestVersion = version;
            result.Title = ReadText(release, "title");
            result.Summary = ReadText(release, "summary");
            result.Notes = ReadText(release, "changelog");
            result.ReleaseDate = ReadText(release, "publishedAt");
            result.UpdateAvailable = CDBoxReleaseVersionComparer.Compare(version, currentVersion) > 0;
            // 忽略额外管理字段；不读取下载 URL、安装器、存储标识或校验码。
            return result;
        }

        private static string ReadText(Dictionary<string, object> map, string key, bool required = false)
        {
            object value;
            if (!map.TryGetValue(key, out value) || value == null)
            {
                if (required) throw new InvalidOperationException("版本服务缺少 " + key + "。");
                return string.Empty;
            }
            var text = value as string;
            if (text == null || (required && string.IsNullOrWhiteSpace(text)))
                throw new InvalidOperationException("版本服务 " + key + " 必须为文本。");
            return text.Trim();
        }

        private static string ReadServiceResponse(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;
            request.UserAgent = "CDBox/" + CurrentVersion;
            request.Accept = "application/json";
            request.AllowAutoRedirect = false;
            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(
                System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new InvalidOperationException("版本服务返回状态 " + (int)response.StatusCode + "。");
                using (var stream = response.GetResponseStream())
                {
                    if (stream == null) throw new InvalidOperationException("版本服务返回空响应。");
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var text = new StringBuilder();
                        var buffer = new char[4096];
                        int count;
                        var elapsed = System.Diagnostics.Stopwatch.StartNew();
                        while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (text.Length + count > MaxResponseCharacters)
                                throw new InvalidOperationException("版本服务响应过大。");
                            if (elapsed.ElapsedMilliseconds > 15000)
                                throw new TimeoutException("版本服务读取超时。");
                            text.Append(buffer, 0, count);
                        }
                        return text.ToString();
                    }
                }
            }
        }
    }
}
