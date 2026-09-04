namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 旧默认表调用点兼容门面。文件读写实现位于 Wastewater 模块。
    /// </summary>
    internal static class QuantityAttributeDefaultStore
    {
        public static string SettingsFilePath
        {
            get
            {
                return QuantityAttributeEngineRegistry.GetRequired()
                    .DefaultSettingsFilePath;
            }
        }

        public static QuantityAttributeDefaults Load()
        {
            return QuantityAttributeEngineRegistry.GetRequired()
                .LoadDefaults();
        }

        public static void Save(QuantityAttributeDefaults defaults)
        {
            QuantityAttributeEngineRegistry.GetRequired()
                .SaveDefaults(defaults);
        }

        public static QuantityPipeAttributes LoadForKind(string kind)
        {
            return QuantityAttributeEngineRegistry.GetRequired()
                .LoadDefaultForKind(kind);
        }

        public static void SaveForKind(string kind,
            QuantityPipeAttributes attrs)
        {
            QuantityAttributeEngineRegistry.GetRequired()
                .SaveDefaultForKind(kind, attrs);
        }
    }
}
