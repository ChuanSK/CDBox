using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TCPipeAutoDraw.Modules.LayerManager;

namespace TCPipeAutoDraw.Modules.LayerManager
{
    internal static class LayerRecognitionRulesStore
    {
        public static string RulesFilePath
        {
            get { return LayerManagerService.GetRecognitionRulesFilePath(); }
        }

        public static List<LayerRecognitionRule> LoadRules()
        {
            try
            {
                return LayerManagerService.LoadRecognitionRules()
                    .Select(r => r == null ? new LayerRecognitionRule() : r.Clone())
                    .ToList();
            }
            catch (Exception)
            {
                return LoadDefaultRules();
            }
        }

        public static List<LayerRecognitionRule> LoadDefaultRules()
        {
            try
            {
                return LayerManagerService.GetDefaultRecognitionRules()
                    .Select(r => r == null ? new LayerRecognitionRule() : r.Clone())
                    .ToList();
            }
            catch (Exception)
            {
                return new List<LayerRecognitionRule>();
            }
        }

        public static int SavePayload(string payload)
        {
            List<LayerRecognitionRule> rules = DecodePayload(payload);
            LayerManagerService.SaveRecognitionRules(rules);
            return rules.Count;
        }

        public static string BuildRulesJson(IEnumerable<LayerRecognitionRule> rules)
        {
            var sb = new StringBuilder();
            sb.Append("[");
            bool first = true;
            foreach (LayerRecognitionRule rule in rules ?? Enumerable.Empty<LayerRecognitionRule>())
            {
                LayerRecognitionRule item = rule == null ? new LayerRecognitionRule() : rule.Clone();
                if (!first) sb.Append(",");
                first = false;
                sb.Append("{")
                    .Append("\"id\":\"").Append(EscapeJson(item.Id)).Append("\",")
                    .Append("\"name\":\"").Append(EscapeJson(item.Name)).Append("\",")
                    .Append("\"enabled\":").Append(item.Enabled ? "true" : "false").Append(",")
                    .Append("\"priority\":").Append(item.Priority.ToString(CultureInfo.InvariantCulture)).Append(",")
                    .Append("\"scope\":\"").Append(EscapeJson(item.Scope)).Append("\",")
                    .Append("\"matchMode\":\"").Append(EscapeJson(NormalizeMatchMode(item.MatchMode))).Append("\",")
                    .Append("\"pattern\":\"").Append(EscapeJson(item.Pattern)).Append("\",")
                    .Append("\"excludePattern\":\"").Append(EscapeJson(item.ExcludePattern)).Append("\",")
                    .Append("\"parentGroup\":\"").Append(EscapeJson(item.ParentGroup)).Append("\",")
                    .Append("\"parentClass\":\"").Append(EscapeJson(item.ParentClass)).Append("\",")
                    .Append("\"tagText\":\"").Append(EscapeJson(item.TagText)).Append("\",")
                    .Append("\"mergeMode\":\"").Append(EscapeJson(item.MergeMode)).Append("\",")
                    .Append("\"applicableObjectTypes\":\"").Append(EscapeJson(item.ApplicableObjectTypes)).Append("\",")
                    .Append("\"source\":\"").Append(EscapeJson(item.Source)).Append("\",")
                    .Append("\"confidenceBase\":").Append(item.ConfidenceBase.ToString("0.####", CultureInfo.InvariantCulture)).Append(",")
                    .Append("\"stopAfterMatch\":").Append(item.StopAfterMatch ? "true" : "false")
                    .Append("}");
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static List<LayerRecognitionRule> DecodePayload(string payload)
        {
            var rules = new List<LayerRecognitionRule>();
            if (string.IsNullOrWhiteSpace(payload)) return rules;

            string[] lines = payload.Replace("\r\n", "\n").Replace('\r', '\n').Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string[] fields = line.Split('\t');
                if (fields.Length < 7) continue;

                if (fields.Length >= 15)
                {
                    int priority;
                    double confidence;
                    int.TryParse(DecodeField(fields[1]), NumberStyles.Integer, CultureInfo.InvariantCulture, out priority);
                    if (!double.TryParse(DecodeField(fields[13]), NumberStyles.Float, CultureInfo.InvariantCulture, out confidence))
                        confidence = 0.78;
                    rules.Add(new LayerRecognitionRule
                    {
                        Enabled = IsTrue(DecodeField(fields[0])),
                        Priority = priority <= 0 ? 100 : priority,
                        Name = DecodeField(fields[2]).Trim(),
                        Scope = DecodeField(fields[3]).Trim(),
                        MatchMode = NormalizeMatchMode(DecodeField(fields[4])),
                        Pattern = DecodeField(fields[5]).Trim(),
                        ExcludePattern = DecodeField(fields[6]).Trim(),
                        ParentGroup = DecodeField(fields[7]).Trim(),
                        ParentClass = DecodeField(fields[8]).Trim(),
                        TagText = DecodeField(fields[9]).Trim(),
                        MergeMode = DecodeField(fields[10]).Trim(),
                        ApplicableObjectTypes = DecodeField(fields[11]).Trim(),
                        Source = DecodeField(fields[12]).Trim(),
                        ConfidenceBase = Math.Max(0d, Math.Min(1d, confidence)),
                        StopAfterMatch = IsTrue(DecodeField(fields[14]))
                    });
                }
                else
                {
                    rules.Add(new LayerRecognitionRule
                    {
                        Enabled = IsTrue(DecodeField(fields[0])),
                        MatchMode = NormalizeMatchMode(DecodeField(fields[1])),
                        Pattern = DecodeField(fields[2]).Trim(),
                        ParentGroup = DecodeField(fields[3]).Trim(),
                        ParentClass = DecodeField(fields[4]).Trim(),
                        TagText = DecodeField(fields[5]).Trim(),
                        StopAfterMatch = IsTrue(DecodeField(fields[6]))
                    });
                }
            }

            return rules;
        }

        private static string DecodeField(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            try
            {
                byte[] bytes = Convert.FromBase64String(value.Trim());
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return value ?? string.Empty;
            }
        }

        private static bool IsTrue(string value)
        {
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeMatchMode(string value)
        {
            string mode = string.IsNullOrWhiteSpace(value) ? LayerManagerService.MatchModeWildcard : value.Trim();
            if (string.Equals(mode, LayerManagerService.MatchModeExact, StringComparison.CurrentCultureIgnoreCase)) return LayerManagerService.MatchModeExact;
            if (string.Equals(mode, LayerManagerService.MatchModeContains, StringComparison.CurrentCultureIgnoreCase)) return LayerManagerService.MatchModeContains;
            if (string.Equals(mode, LayerManagerService.MatchModeRegex, StringComparison.CurrentCultureIgnoreCase)) return LayerManagerService.MatchModeRegex;
            if (string.Equals(mode, LayerManagerService.MatchModeKeywords, StringComparison.CurrentCultureIgnoreCase)) return LayerManagerService.MatchModeKeywords;
            if (string.Equals(mode, LayerManagerService.MatchModeTemplate, StringComparison.CurrentCultureIgnoreCase)) return LayerManagerService.MatchModeTemplate;
            return LayerManagerService.MatchModeWildcard;
        }

        private static string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var sb = new StringBuilder(text.Length + 16);
            foreach (char ch in text)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '<': sb.Append("\\u003c"); break;
                    case '>': sb.Append("\\u003e"); break;
                    case '&': sb.Append("\\u0026"); break;
                    default:
                        if (ch < 32) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
