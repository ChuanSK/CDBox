using System.Collections.Generic;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace CDBox.Wastewater.UI
{
    internal sealed class WastewaterQuantityAttributeEditorRequest
    {
        public string documentId { get; set; }
        public string handle { get; set; }
        public QuantityPipeAttributes attributes { get; set; }
        public List<QuantityStructureLayer> layers { get; set; }
        public string changedField { get; set; }
        public long requestId { get; set; }
        public bool forStart { get; set; }

        public WastewaterQuantityAttributeEditorRequest()
        {
            layers = new List<QuantityStructureLayer>();
            changedField = string.Empty;
        }
    }

    internal sealed class WastewaterQuantityAttributeEditorContext
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
        public List<QuantityStructureLayer> layers { get; set; }
        public List<string> warnings { get; set; }
        public object calculation { get; set; }
        public double realExcavationDepth { get; set; }
        public long requestId { get; set; }

        public WastewaterQuantityAttributeEditorContext()
        {
            documentId = string.Empty; documentName = string.Empty; handle = string.Empty;
            layerName = string.Empty; objectTypeName = string.Empty; inferredKind = string.Empty;
            message = string.Empty; attributes = QuantityPipeAttributes.Default;
            layers = new List<QuantityStructureLayer>(); warnings = new List<string>();
        }
    }
}
