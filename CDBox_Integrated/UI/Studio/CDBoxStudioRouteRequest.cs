using System;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioRouteRequest
    {
        public string Name { get; private set; }
        public string Argument { get; private set; }

        private CDBoxStudioRouteRequest(string name, string argument)
        {
            Name = name ?? string.Empty;
            Argument = argument ?? string.Empty;
        }

        public static CDBoxStudioRouteRequest Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new CDBoxStudioRouteRequest(string.Empty, string.Empty);

            string[] parts = raw.Split(new[] { '|' }, 3);
            if (parts.Length >= 2 && string.Equals(parts[0], "studio", StringComparison.OrdinalIgnoreCase))
            {
                string argument = parts.Length >= 3 ? Decode(parts[2]) : string.Empty;
                return new CDBoxStudioRouteRequest(parts[1], argument);
            }

            // 兼容第一版 run: / filter: 消息，避免前端缓存未刷新时失效。
            if (raw.StartsWith("run:", StringComparison.OrdinalIgnoreCase)) return new CDBoxStudioRouteRequest("run", raw.Substring(4));
            if (raw.StartsWith("filter:", StringComparison.OrdinalIgnoreCase)) return new CDBoxStudioRouteRequest("filter", raw.Substring(7));

            return new CDBoxStudioRouteRequest(string.Empty, raw);
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            try
            {
                return Uri.UnescapeDataString(value.Replace("+", "%20"));
            }
            catch
            {
                return value;
            }
        }
    }
}
