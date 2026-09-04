using System.Collections.Generic;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 基础宿主向污水属性编辑器公开的最小 CAD 交互能力。
    /// 页面与业务联动不依赖主程序集窗口、Document 或 ObjectId 类型。
    /// </summary>
    public interface IQuantityAttributeEditorCadService
    {
        QuantityAttributeCadObject Read(string documentId,
            string objectHandle);
        QuantityAttributeCadObject Select(string documentId);
        QuantityAttributeCadObject Save(string documentId,
            string objectHandle, QuantityPipeAttributes attributes,
            IEnumerable<QuantityStructureLayer> layers);
        QuantityDependencyResult CalculateDraft(string documentId,
            string objectHandle, QuantityPipeAttributes attributes,
            IEnumerable<QuantityStructureLayer> layers,
            string changedField, QuantityPipeAttributes previousDraft);
        QuantityAttributeCadObject Refresh(string documentId,
            string objectHandle, QuantityPipeAttributes attributes);
        QuantityAttributeCadObject ReloadDefault(string documentId,
            string objectHandle, QuantityPipeAttributes attributes);
        QuantityAttributeCadObject SelectConnectedNode(string documentId,
            string objectHandle, QuantityPipeAttributes attributes,
            bool forStart);
        void OpenLegacy(string documentId, string objectHandle);
    }

    public sealed class QuantityAttributeCadObject
    {
        public bool Selected { get; set; }
        public string DocumentId { get; set; }
        public string DocumentName { get; set; }
        public string Handle { get; set; }
        public string LayerName { get; set; }
        public string ObjectTypeName { get; set; }
        public string InferredKind { get; set; }
        public double CadLength { get; set; }
        public bool HasSavedAttributes { get; set; }
        public string Message { get; set; }
        public QuantityPipeAttributes Attributes { get; set; }

        public QuantityAttributeCadObject()
        {
            DocumentId = string.Empty;
            DocumentName = string.Empty;
            Handle = string.Empty;
            LayerName = string.Empty;
            ObjectTypeName = string.Empty;
            InferredKind = string.Empty;
            Message = string.Empty;
            Attributes = QuantityPipeAttributes.Default;
        }
    }
}
