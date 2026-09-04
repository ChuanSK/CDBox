using System.Collections.Generic;
using System.IO;

namespace TCPipeAutoDraw.Modules.LayerManager
{
    // 页面脚本测试不调用 CAD 图层操作；规则存储只需要稳定的测试边界。
    internal static class LayerManagerService
    {
        public const string MatchModeExact = "精确";
        public const string MatchModeContains = "包含";
        public const string MatchModeWildcard = "通配符";
        public const string MatchModeRegex = "正则";
        public const string MatchModeKeywords = "关键词组合";
        public const string MatchModeTemplate = "结构模板";

        public static string GetRecognitionRulesFilePath()
        {
            return Path.Combine(Path.GetTempPath(),
                "CDBox.CoreTests.LayerRecognitionRules.xml");
        }

        public static List<LayerRecognitionRule> LoadRecognitionRules()
        {
            return GetDefaultRecognitionRules();
        }

        public static List<LayerRecognitionRule> GetDefaultRecognitionRules()
        {
            return new List<LayerRecognitionRule>();
        }

        public static void SaveRecognitionRules(
            IEnumerable<LayerRecognitionRule> rules)
        {
        }
    }
}
