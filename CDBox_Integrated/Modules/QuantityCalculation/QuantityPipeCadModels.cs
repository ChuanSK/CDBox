using Autodesk.AutoCAD.DatabaseServices;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 基础 CAD 适配层使用的对象选择结果。业务属性模型位于 Shared。
    /// </summary>
    public sealed class QuantityPipeSelectionInfo
    {
        public ObjectId ObjectId { get; set; }
        public string HandleText { get; set; }
        public string LayerName { get; set; }
        public string ObjectTypeName { get; set; }
        public double CadLength { get; set; }
        public bool HasSavedAttributes { get; set; }
        public string InferredKind { get; set; }
        public QuantityPipeAttributes Attributes { get; set; }

        public QuantityPipeSelectionInfo()
        {
            HandleText = string.Empty;
            LayerName = string.Empty;
            ObjectTypeName = string.Empty;
            InferredKind = string.Empty;
            Attributes = QuantityPipeAttributes.Default;
        }
    }

    public sealed class QuantityPipeWriteResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int SuccessCount { get; set; }
        public int SkipCount { get; set; }
        public int FailCount { get; set; }

        public QuantityPipeWriteResult()
        {
            Message = string.Empty;
        }
    }
}
