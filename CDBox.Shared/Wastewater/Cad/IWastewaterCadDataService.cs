using System;
using System.Collections.Generic;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace CDBox.Shared.Wastewater.Cad
{
    public sealed class WastewaterCadObjectSnapshot
    {
        public object ObjectId { get; set; }
        public string Handle { get; set; }
        public string LayerName { get; set; }
        public string ObjectTypeName { get; set; }
        public double CadLength { get; set; }
        public bool HasSavedAttributes { get; set; }
        public string InferredKind { get; set; }
        public QuantityPipeAttributes Attributes { get; set; }

        public WastewaterCadObjectSnapshot()
        {
            Handle = string.Empty;
            LayerName = string.Empty;
            ObjectTypeName = string.Empty;
            InferredKind = string.Empty;
            Attributes = QuantityPipeAttributes.Default;
        }
    }

    public sealed class WastewaterNodeConnectionSnapshot
    {
        public bool StartEvaluated { get; set; }
        public bool StartConnected { get; set; }
        public bool EndEvaluated { get; set; }
        public bool EndConnected { get; set; }
        public string DetectedStartNode { get; set; }
        public string DetectedEndNode { get; set; }

        public WastewaterNodeConnectionSnapshot()
        {
            DetectedStartNode = string.Empty;
            DetectedEndNode = string.Empty;
        }
    }

    public interface IWastewaterCadDataService
    {
        QuantityPipeAttributes SelectNodeWithPreview(object document,
            bool selectStart);
        IReadOnlyList<object> FindObjectsWithSavedAttributes(
            object document, Action<int, int, string> progress);
        IReadOnlyList<object> FindSupportedNodeObjectIds(object document,
            Action<int, int, string> progress);
        WastewaterCadObjectSnapshot Read(object document, object objectId,
            IEnumerable<object> knownNodeIds = null);
        WastewaterNodeConnectionSnapshot ResolvePhysicalNodeConnections(
            object document, object objectId,
            QuantityPipeAttributes attributes,
            IEnumerable<object> knownNodeIds);
        IReadOnlyList<string> RepairClonedAttributeObjects(object database,
            object transaction, object cloneMap);
        bool TryReadSavedAttributes(object database, object transaction,
            object objectId, out QuantityPipeAttributes attributes);
    }
}
