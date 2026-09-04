using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using CDBox.Wastewater.Module;

namespace TCPipeAutoDraw.Modules.LayerManager
{
    public static class LayerManagerService
    {
        public static LayerMetadata GetLayerMetadata(Database database,
            Transaction transaction, string layerName)
        {
            object value = WastewaterRuntimeServices.LayerClassification == null
                ? null : WastewaterRuntimeServices.LayerClassification
                    .GetLayerMetadata(database, transaction, layerName);
            return value as LayerMetadata ?? new LayerMetadata();
        }

        public static LayerMetadata InferLayerMetadataFromName(
            string layerName)
        {
            LayerRecognitionResult result = LayerRecognitionEngine.Recognize(
                layerName, new List<LayerRecognitionRule>());
            return result == null || result.Metadata == null
                ? new LayerMetadata() : result.Metadata;
        }

        public static void EnsureLayerMetadata(Database database,
            Transaction transaction, string layerName,
            LayerMetadata metadata, bool overwriteExisting)
        {
            if (WastewaterRuntimeServices.LayerClassification == null) return;
            WastewaterRuntimeServices.LayerClassification
                .EnsureGeneratedLayerClassification(layerName,
                    metadata == null ? string.Empty : metadata.ParentGroup);
        }

        public static void SetLayerMetadata(Database database,
            Transaction transaction, string layerName,
            LayerMetadata metadata)
        {
            EnsureLayerMetadata(database, transaction, layerName, metadata,
                true);
        }
    }
}
