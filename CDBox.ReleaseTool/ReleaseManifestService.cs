using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CDBox.ReleaseTool;

internal static class ReleaseManifestService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public static ReleaseChannelConfiguration LoadConfiguration(
        string configPath, string channel)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException("发布渠道配置不存在。", configPath);
        ReleaseConfiguration? configuration = JsonSerializer.Deserialize<ReleaseConfiguration>(
            File.ReadAllText(configPath, Encoding.UTF8), JsonOptions);
        if (configuration?.Channels == null
            || !configuration.Channels.TryGetValue(channel, out ReleaseChannelConfiguration? result)
            || result == null)
            throw new InvalidOperationException("发布渠道配置中不存在通道：" + channel);
        result.Channel = NormalizeChannel(result.Channel);
        if (!string.Equals(result.Channel, NormalizeChannel(channel),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("发布渠道配置键与 channel 字段不一致。");
        ValidateHttpsBaseUrl(result.UpdateBaseUrl, "updateBaseUrl");
        ValidateHttpsBaseUrl(result.DownloadBaseUrl, "downloadBaseUrl");
        ValidateHttpsBaseUrl(result.WebsiteBaseUrl, "websiteBaseUrl");
        return result;
    }

    public static ReleaseGenerationResult Generate(
        string installerPath, string releaseVersion, string installerVersion,
        string title, string summary, string channel,
        DateTimeOffset publishedAt, string configPath, string schemaPath,
        string outputPath)
    {
        var installer = new FileInfo(installerPath);
        if (!installer.Exists || installer.Length <= 0)
            throw new InvalidOperationException("安装器文件不存在或为空：" + installer.FullName);
        if (!string.Equals(installer.Extension, ".exe",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("正式发行物必须是 EXE 安装器。");

        channel = NormalizeChannel(channel);
        string normalizedReleaseVersion = (releaseVersion ?? string.Empty).Trim();
        string normalizedInstallerVersion = (installerVersion ?? string.Empty).Trim();
        ReleaseChannelConfiguration configuration = LoadConfiguration(
            configPath, channel);
        string hash = ComputeSha256(installer.FullName);
        var urls = new ReleaseUrlBuilder(configuration);
        var manifest = new ReleaseManifest
        {
            SchemaVersion = 2,
            Channel = channel,
            GeneratedAt = DateTimeOffset.Now,
            Release = new ReleaseInformation
            {
                Version = normalizedReleaseVersion,
                Title = (title ?? string.Empty).Trim(),
                PublishedAt = publishedAt,
                Summary = (summary ?? string.Empty).Trim(),
                ReleaseNotesUrl = urls.BuildReleaseNotesUrl(normalizedReleaseVersion)
            },
            Installer = new InstallerInformation
            {
                Version = normalizedInstallerVersion,
                DownloadUrl = urls.BuildInstallerLatestUrl(),
                Package = new InstallerPackageInformation
                {
                    FileName = installer.Name,
                    VersionedUrl = urls.BuildInstallerVersionedUrl(
                        normalizedInstallerVersion, installer.Name),
                    SizeBytes = installer.Length,
                    Sha256 = hash
                }
            }
        };

        ReleaseManifestValidator.ValidateSchemaDefinition(schemaPath);
        ReleaseManifestValidator.Validate(manifest, configuration,
            installer.FullName);
        string json = JsonSerializer.Serialize(manifest, JsonOptions);
        ReleaseManifestValidator.ValidateSerialized(json);

        string fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)
            ?? throw new InvalidOperationException("Manifest 输出目录无效。"));
        File.WriteAllText(fullOutputPath, json + Environment.NewLine,
            new UTF8Encoding(false));

        return new ReleaseGenerationResult
        {
            ManifestPath = fullOutputPath,
            ManifestUrl = urls.BuildManifestUrl(),
            InstallerPath = installer.FullName,
            InstallerSize = installer.Length,
            InstallerSha256 = hash,
            LatestUrl = manifest.Installer.DownloadUrl,
            VersionedUrl = manifest.Installer.Package.VersionedUrl,
            Channel = channel
        };
    }

    public static void ValidateExisting(string manifestPath,
        string configPath, string schemaPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Manifest 文件不存在。", manifestPath);
        string json = File.ReadAllText(manifestPath, Encoding.UTF8);
        ReleaseManifestValidator.ValidateSchemaDefinition(schemaPath);
        ReleaseManifestValidator.ValidateSerialized(json);
        ReleaseManifest? manifest = JsonSerializer.Deserialize<ReleaseManifest>(
            json, JsonOptions);
        if (manifest == null)
            throw new InvalidOperationException("Manifest 无法反序列化。");
        ReleaseChannelConfiguration configuration = LoadConfiguration(
            configPath, manifest.Channel);
        ReleaseManifestValidator.Validate(manifest, configuration, null);
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static string NormalizeChannel(string channel)
    {
        string value = (channel ?? string.Empty).Trim().ToLowerInvariant();
        if (value != "stable" && value != "preview")
            throw new InvalidOperationException(
                "发布通道只能是 stable 或 preview。");
        return value;
    }

    private static void ValidateHttpsBaseUrl(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(name + " 必须是 HTTPS 绝对地址。");
    }
}

internal sealed class ReleaseUrlBuilder
{
    private readonly ReleaseChannelConfiguration _configuration;

    public ReleaseUrlBuilder(ReleaseChannelConfiguration configuration)
    {
        _configuration = configuration
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    public string BuildInstallerLatestUrl()
    {
        return Combine(_configuration.DownloadBaseUrl,
            "installer/latest/CDBoxInstaller.exe");
    }

    public string BuildInstallerVersionedUrl(string version, string fileName)
    {
        return Combine(_configuration.DownloadBaseUrl, "installer/"
            + Segment(version) + "/" + Segment(fileName));
    }

    public string BuildReleaseNotesUrl(string version)
    {
        return Combine(_configuration.WebsiteBaseUrl,
            "releases/" + Segment(version));
    }

    public string BuildManifestUrl()
    {
        return Combine(_configuration.UpdateBaseUrl, "update.json");
    }

    private static string Combine(string baseUrl, string relative)
    {
        string normalized = (baseUrl ?? string.Empty).Trim().TrimEnd('/') + "/";
        return new Uri(new Uri(normalized, UriKind.Absolute), relative).AbsoluteUri;
    }

    private static string Segment(string value)
    {
        string text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            throw new InvalidOperationException("URL 路径参数不能为空。");
        return Uri.EscapeDataString(text);
    }
}

internal static class ReleaseManifestValidator
{
    private static readonly Regex VersionPattern = new(
        @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?$",
        RegexOptions.CultureInvariant);
    private static readonly Regex Sha256Pattern = new(
        "^[0-9A-F]{64}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> RepositoryHosts = new(
        new[] { "github.com", "raw.githubusercontent.com", "gitee.com",
            "gitcode.com", "raw.gitcode.com" },
        StringComparer.OrdinalIgnoreCase);

    public static void ValidateSchemaDefinition(string schemaPath)
    {
        if (!File.Exists(schemaPath))
            throw new FileNotFoundException("update.schema.json 不存在。", schemaPath);
        using JsonDocument schema = JsonDocument.Parse(
            File.ReadAllText(schemaPath, Encoding.UTF8));
        JsonElement root = schema.RootElement;
        if (!root.TryGetProperty("properties", out JsonElement properties)
            || !properties.TryGetProperty("schemaVersion", out JsonElement schemaVersion)
            || !schemaVersion.TryGetProperty("const", out JsonElement version)
            || version.GetInt32() != 2)
            throw new InvalidOperationException(
                "update.schema.json 未固定 schemaVersion = 2。");
        RequireSchemaFields(root, "schemaVersion", "channel", "generatedAt",
            "release", "installer");
    }

    public static void Validate(ReleaseManifest manifest,
        ReleaseChannelConfiguration configuration, string? installerPath)
    {
        if (manifest.SchemaVersion != 2)
            throw new InvalidOperationException("schemaVersion 必须为 2。");
        if (manifest.Channel != "stable" && manifest.Channel != "preview")
            throw new InvalidOperationException("channel 必须为 stable 或 preview。");
        if (!string.Equals(manifest.Channel, configuration.Channel,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Manifest 与发布配置通道不一致。");
        RequireVersion(manifest.Release.Version, "release.version");
        RequireVersion(manifest.Installer.Version, "installer.version");
        if (manifest.GeneratedAt == default)
            throw new InvalidOperationException("generatedAt 必须是有效日期。");
        if (manifest.Release.PublishedAt == default)
            throw new InvalidOperationException("release.publishedAt 必须是有效日期。");
        RequireText(manifest.Release.Title, "release.title");
        RequireText(manifest.Release.Summary, "release.summary");
        RequireHttps(manifest.Release.ReleaseNotesUrl,
            "release.releaseNotesUrl");
        RequireHttps(manifest.Installer.DownloadUrl, "installer.downloadUrl");
        RequireHttps(manifest.Installer.Package.VersionedUrl,
            "installer.package.versionedUrl");
        RequireText(manifest.Installer.Package.FileName,
            "installer.package.fileName");
        if (!manifest.Installer.Package.FileName.EndsWith(".exe",
                StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(manifest.Installer.Package.FileName)
                != manifest.Installer.Package.FileName)
            throw new InvalidOperationException(
                "installer.package.fileName 必须是纯 EXE 文件名。");
        if (manifest.Installer.Package.SizeBytes <= 0)
            throw new InvalidOperationException(
                "installer.package.sizeBytes 必须大于 0。");
        if (!Sha256Pattern.IsMatch(manifest.Installer.Package.Sha256 ?? string.Empty))
            throw new InvalidOperationException(
                "installer.package.sha256 必须是 64 位大写十六进制字符。");

        ValidateOfficialUrl(manifest.Installer.DownloadUrl,
            configuration.DownloadBaseUrl, "installer.downloadUrl");
        ValidateOfficialUrl(manifest.Installer.Package.VersionedUrl,
            configuration.DownloadBaseUrl, "installer.package.versionedUrl");
        ValidateOfficialUrl(manifest.Release.ReleaseNotesUrl,
            configuration.WebsiteBaseUrl, "release.releaseNotesUrl");
        ValidateOfficialUrl(new ReleaseUrlBuilder(configuration).BuildManifestUrl(),
            configuration.UpdateBaseUrl, "Manifest URL");
        var urls = new ReleaseUrlBuilder(configuration);
        RequireExactUrl(manifest.Installer.DownloadUrl,
            urls.BuildInstallerLatestUrl(), "installer.downloadUrl");
        RequireExactUrl(manifest.Installer.Package.VersionedUrl,
            urls.BuildInstallerVersionedUrl(manifest.Installer.Version,
                manifest.Installer.Package.FileName),
            "installer.package.versionedUrl");
        RequireExactUrl(manifest.Release.ReleaseNotesUrl,
            urls.BuildReleaseNotesUrl(manifest.Release.Version),
            "release.releaseNotesUrl");

        if (!string.IsNullOrWhiteSpace(installerPath))
        {
            var file = new FileInfo(installerPath);
            if (!file.Exists || file.Length != manifest.Installer.Package.SizeBytes)
                throw new InvalidOperationException("安装器文件大小与 Manifest 不一致。");
            if (!string.Equals(file.Name, manifest.Installer.Package.FileName,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("安装器文件名与 Manifest 不一致。");
            using FileStream stream = File.OpenRead(file.FullName);
            string actualHash = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(actualHash, manifest.Installer.Package.Sha256,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("安装器 SHA-256 与 Manifest 不一致。");
        }
    }

    public static void ValidateSerialized(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Manifest 根节点必须是对象。");
        RequireOnlyProperties(root, "Manifest", "schemaVersion", "channel",
            "generatedAt", "release", "installer");
        RequireJsonInt(root, "schemaVersion", 2);
        string channel = RequireJsonString(root, "channel");
        if (channel != "stable" && channel != "preview")
            throw new InvalidOperationException("Manifest channel 无效。");
        RequireJsonDate(root, "generatedAt");
        JsonElement release = RequireJsonObject(root, "release");
        RequireOnlyProperties(release, "release", "version", "title",
            "publishedAt", "summary", "releaseNotesUrl");
        RequireJsonString(release, "version");
        RequireJsonString(release, "title");
        RequireJsonDate(release, "publishedAt");
        RequireJsonString(release, "summary");
        RequireJsonString(release, "releaseNotesUrl");
        JsonElement installer = RequireJsonObject(root, "installer");
        RequireOnlyProperties(installer, "installer", "version", "downloadUrl",
            "package");
        RequireJsonString(installer, "version");
        RequireJsonString(installer, "downloadUrl");
        JsonElement package = RequireJsonObject(installer, "package");
        RequireOnlyProperties(package, "installer.package", "fileName",
            "versionedUrl", "sizeBytes", "sha256");
        RequireJsonString(package, "fileName");
        RequireJsonString(package, "versionedUrl");
        if (!package.TryGetProperty("sizeBytes", out JsonElement size)
            || size.ValueKind != JsonValueKind.Number || size.GetInt64() <= 0)
            throw new InvalidOperationException("Manifest sizeBytes 无效。");
        string hash = RequireJsonString(package, "sha256");
        if (!Sha256Pattern.IsMatch(hash))
            throw new InvalidOperationException("Manifest sha256 无效。");
    }

    private static void RequireSchemaFields(JsonElement schema,
        params string[] names)
    {
        if (!schema.TryGetProperty("required", out JsonElement required)
            || required.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Schema 缺少 required 约束。");
        var values = new HashSet<string>(required.EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty),
            StringComparer.Ordinal);
        foreach (string name in names)
            if (!values.Contains(name))
                throw new InvalidOperationException("Schema required 缺少：" + name);
    }

    private static void RequireVersion(string value, string name)
    {
        if (!VersionPattern.IsMatch((value ?? string.Empty).Trim()))
            throw new InvalidOperationException(name + " 不是有效版本号。");
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(name + " 不能为空。");
    }

    private static Uri RequireHttps(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(name + " 必须是 HTTPS 地址。");
        return uri;
    }

    private static void ValidateOfficialUrl(string value, string baseUrl,
        string name)
    {
        Uri uri = RequireHttps(value, name);
        Uri expected = RequireHttps(baseUrl, name + " 配置");
        if (RepositoryHosts.Contains(uri.Host))
            throw new InvalidOperationException(name + " 不得指向代码仓库下载域名。");
        if (!string.Equals(uri.Host, expected.Host,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(name + " 不属于配置的官方域名。");
    }

    private static void RequireExactUrl(string actual, string expected,
        string name)
    {
        if (!string.Equals(new Uri(actual).AbsoluteUri,
                new Uri(expected).AbsoluteUri, StringComparison.Ordinal))
            throw new InvalidOperationException(name + " 不符合统一 URL 生成规则。");
    }

    private static void RequireOnlyProperties(JsonElement value, string name,
        params string[] allowed)
    {
        var names = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
            if (!names.Contains(property.Name))
                throw new InvalidOperationException(name + " 包含 Schema 未定义字段："
                    + property.Name);
    }

    private static JsonElement RequireJsonObject(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement value)
            || value.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Manifest 缺少对象：" + name);
        return value;
    }

    private static string RequireJsonString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidOperationException("Manifest 缺少字符串：" + name);
        return value.GetString()!;
    }

    private static void RequireJsonDate(JsonElement parent, string name)
    {
        string value = RequireJsonString(parent, name);
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out _))
            throw new InvalidOperationException("Manifest 日期无效：" + name);
    }

    private static void RequireJsonInt(JsonElement parent, string name,
        int expected)
    {
        if (!parent.TryGetProperty(name, out JsonElement value)
            || value.ValueKind != JsonValueKind.Number
            || value.GetInt32() != expected)
            throw new InvalidOperationException("Manifest 整数字段无效：" + name);
    }
}
