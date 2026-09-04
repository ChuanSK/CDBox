using System.Globalization;
using System.Text.Json;

namespace CDBox.ReleaseTool;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
                throw new InvalidOperationException("请指定 generate 或 validate。");
            string command = args[0].Trim().ToLowerInvariant();
            Dictionary<string, string> options = ReadOptions(args.Skip(1));
            if (command == "generate")
            {
                DateTimeOffset publishedAt = DateTimeOffset.Parse(
                    Required(options, "published-at"), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);
                ReleaseGenerationResult result = ReleaseManifestService.Generate(
                    Required(options, "installer"),
                    Required(options, "release-version"),
                    Required(options, "installer-version"),
                    Required(options, "title"),
                    Required(options, "summary"),
                    Required(options, "channel"),
                    publishedAt,
                    Required(options, "config"),
                    Required(options, "schema"),
                    Required(options, "output"));
                Console.WriteLine(JsonSerializer.Serialize(result,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));
                return 0;
            }
            if (command == "validate")
            {
                ReleaseManifestService.ValidateExisting(
                    Required(options, "manifest"),
                    Required(options, "config"),
                    Required(options, "schema"));
                Console.WriteLine("READY TO PUBLISH");
                return 0;
            }
            throw new InvalidOperationException("未知命令：" + command);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Release Manifest 处理失败：" + ex.Message);
            return 1;
        }
    }

    private static Dictionary<string, string> ReadOptions(IEnumerable<string> args)
    {
        string[] values = args.ToArray();
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < values.Length; i++)
        {
            string key = values[i];
            if (!key.StartsWith("--", StringComparison.Ordinal)
                || i + 1 >= values.Length)
                throw new InvalidOperationException("命令行参数格式错误：" + key);
            result[key.Substring(2)] = values[++i];
        }
        return result;
    }

    private static string Required(IDictionary<string, string> options,
        string name)
    {
        if (!options.TryGetValue(name, out string? value)
            || string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("缺少参数 --" + name + "。");
        return value.Trim();
    }
}
