namespace CDBox.Shared.Services
{
    /// <summary>
    /// 必装基础图层管理器向业务模块公开的最小分类写入契约。
    /// </summary>
    public interface ICDBoxLayerClassificationService
    {
        void EnsureGeneratedLayerClassification(string layerName,
            string parentGroup);
        object GetLayerMetadata(object database, object transaction,
            string layerName);
    }
}
