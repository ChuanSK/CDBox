using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioQuantityAttributeEditorRequest
    {
        public string documentId { get; set; }
        public string handle { get; set; }
        public QuantityPipeAttributes attributes { get; set; }
        public bool forStart { get; set; }
    }

    internal sealed class CDBoxStudioQuantityAttributeEditorContext
    {
        public bool selected { get; set; }
        public string documentId { get; set; }
        public string documentName { get; set; }
        public string handle { get; set; }
        public string layerName { get; set; }
        public string objectTypeName { get; set; }
        public string inferredKind { get; set; }
        public double cadLength { get; set; }
        public double effectiveLength { get; set; }
        public bool hasSavedAttributes { get; set; }
        public string message { get; set; }
        public QuantityPipeAttributes attributes { get; set; }

        public CDBoxStudioQuantityAttributeEditorContext()
        {
            documentId = string.Empty; documentName = string.Empty; handle = string.Empty;
            layerName = string.Empty; objectTypeName = string.Empty; inferredKind = string.Empty;
            message = string.Empty; attributes = QuantityPipeAttributes.Default;
        }
    }
}
