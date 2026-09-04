using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CDBox.Shared.Wastewater.Cad;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace TCPipeAutoDraw.Core.Modules
{
    internal sealed class WastewaterCadDataAdapter
        : IWastewaterCadDataService
    {
        public QuantityPipeAttributes SelectNodeWithPreview(object document,
            bool selectStart)
        {
            return QuantityPipeAttributeService.SelectNodeWithPreview(
                RequiredDocument(document), selectStart);
        }

        public IReadOnlyList<object> FindObjectsWithSavedAttributes(
            object document, Action<int, int, string> progress)
        {
            return QuantityPipeAttributeService.FindObjectsWithSavedAttributes(
                RequiredDocument(document), progress).Cast<object>().ToList();
        }

        public IReadOnlyList<object> FindSupportedNodeObjectIds(
            object document, Action<int, int, string> progress)
        {
            return QuantityPipeAttributeService.FindSupportedNodeObjectIds(
                RequiredDocument(document), progress).Cast<object>().ToList();
        }

        public WastewaterCadObjectSnapshot Read(object document,
            object objectId, IEnumerable<object> knownNodeIds = null)
        {
            Document doc = RequiredDocument(document);
            ObjectId id = RequiredObjectId(objectId);
            List<ObjectId> known = knownNodeIds == null ? null
                : knownNodeIds.OfType<ObjectId>().ToList();
            QuantityPipeSelectionInfo info = known == null
                ? QuantityPipeAttributeService.ReadPipe(doc, id)
                : QuantityPipeAttributeService.ReadPipe(doc, id, known);
            return Map(info);
        }

        public WastewaterNodeConnectionSnapshot
            ResolvePhysicalNodeConnections(object document, object objectId,
                QuantityPipeAttributes attributes,
                IEnumerable<object> knownNodeIds)
        {
            QuantityPipeEndpointConnectionResult result =
                QuantityPipeAttributeService.ResolvePhysicalNodeConnections(
                    RequiredDocument(document), RequiredObjectId(objectId),
                    attributes, (knownNodeIds ?? Enumerable.Empty<object>())
                        .OfType<ObjectId>().ToList());
            if (result == null) return null;
            return new WastewaterNodeConnectionSnapshot
            {
                StartEvaluated = result.StartEvaluated,
                StartConnected = result.StartConnected,
                EndEvaluated = result.EndEvaluated,
                EndConnected = result.EndConnected,
                DetectedStartNode = result.DetectedStartNode,
                DetectedEndNode = result.DetectedEndNode
            };
        }

        public IReadOnlyList<string> RepairClonedAttributeObjects(
            object database, object transaction, object cloneMap)
        {
            return QuantityPipeAttributeService.RepairClonedAttributeObjects(
                database as Database, transaction as Transaction,
                cloneMap as IDictionary<ObjectId, ObjectId>);
        }

        public bool TryReadSavedAttributes(object database,
            object transaction, object objectId,
            out QuantityPipeAttributes attributes)
        {
            return QuantityPipeAttributeService.TryReadSavedAttributes(
                database as Database, transaction as Transaction,
                RequiredObjectId(objectId), out attributes);
        }

        private static WastewaterCadObjectSnapshot Map(
            QuantityPipeSelectionInfo info)
        {
            if (info == null) return null;
            return new WastewaterCadObjectSnapshot
            {
                ObjectId = info.ObjectId,
                Handle = info.HandleText,
                LayerName = info.LayerName,
                ObjectTypeName = info.ObjectTypeName,
                CadLength = info.CadLength,
                HasSavedAttributes = info.HasSavedAttributes,
                InferredKind = info.InferredKind,
                Attributes = info.Attributes == null
                    ? QuantityPipeAttributes.Default
                    : info.Attributes.Clone()
            };
        }

        private static Document RequiredDocument(object value)
        {
            Document document = value as Document;
            if (document == null)
                throw new ArgumentException("CAD Document 参数无效。",
                    "value");
            return document;
        }

        private static ObjectId RequiredObjectId(object value)
        {
            return value is ObjectId ? (ObjectId)value : ObjectId.Null;
        }
    }
}
