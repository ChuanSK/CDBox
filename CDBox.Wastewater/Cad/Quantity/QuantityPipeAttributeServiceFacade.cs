using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CDBox.Shared.Wastewater.Cad;
using CDBox.Wastewater.Module;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
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
    }

    internal sealed class QuantityPipeEndpointConnectionResult
    {
        public bool StartEvaluated { get; set; }
        public bool StartConnected { get; set; }
        public bool EndEvaluated { get; set; }
        public bool EndConnected { get; set; }
        public string DetectedStartNode { get; set; }
        public string DetectedEndNode { get; set; }
    }

    public static class QuantityPipeAttributeService
    {
        public const string PipeAttributeXrecordName =
            "CDBoxQuantityPipeAttributes";

        public static QuantityPipeAttributes SelectNodeWithPreview(
            Document document, bool selectStart)
        {
            return Service.SelectNodeWithPreview(document, selectStart);
        }

        public static IEnumerable<ObjectId> FindObjectsWithSavedAttributes(
            Document document, Action<int, int, string> progress = null)
        {
            return Service.FindObjectsWithSavedAttributes(document, progress)
                .OfType<ObjectId>();
        }

        public static List<ObjectId> FindSupportedNodeObjectIds(
            Document document, Action<int, int, string> progress = null)
        {
            return Service.FindSupportedNodeObjectIds(document, progress)
                .OfType<ObjectId>().ToList();
        }

        public static QuantityPipeSelectionInfo ReadPipe(Document document,
            ObjectId id, List<ObjectId> knownNodeIds = null)
        {
            WastewaterCadObjectSnapshot snapshot = Service.Read(document,
                id, knownNodeIds == null ? null
                    : knownNodeIds.Cast<object>());
            return Map(snapshot);
        }

        internal static QuantityPipeEndpointConnectionResult
            ResolvePhysicalNodeConnections(Document document, ObjectId id,
                QuantityPipeAttributes attributes,
                IEnumerable<ObjectId> knownNodeIds = null)
        {
            WastewaterNodeConnectionSnapshot result = Service
                .ResolvePhysicalNodeConnections(document, id, attributes,
                    (knownNodeIds ?? Enumerable.Empty<ObjectId>())
                        .Cast<object>());
            if (result == null) return null;
            return new QuantityPipeEndpointConnectionResult
            {
                StartEvaluated = result.StartEvaluated,
                StartConnected = result.StartConnected,
                EndEvaluated = result.EndEvaluated,
                EndConnected = result.EndConnected,
                DetectedStartNode = result.DetectedStartNode,
                DetectedEndNode = result.DetectedEndNode
            };
        }

        public static List<string> RepairClonedAttributeObjects(Database db,
            Transaction transaction,
            IDictionary<ObjectId, ObjectId> cloneMap)
        {
            return Service.RepairClonedAttributeObjects(db, transaction,
                cloneMap).ToList();
        }

        public static bool TryReadSavedAttributes(Database database,
            Transaction transaction, ObjectId id,
            out QuantityPipeAttributes attributes)
        {
            return Service.TryReadSavedAttributes(database, transaction, id,
                out attributes);
        }

        private static IWastewaterCadDataService Service
        {
            get
            {
                return WastewaterRuntimeServices.CadData
                    ?? throw new InvalidOperationException(
                        "污水 CAD 数据适配服务尚未初始化。");
            }
        }

        private static QuantityPipeSelectionInfo Map(
            WastewaterCadObjectSnapshot snapshot)
        {
            if (snapshot == null) return null;
            return new QuantityPipeSelectionInfo
            {
                ObjectId = snapshot.ObjectId is ObjectId
                    ? (ObjectId)snapshot.ObjectId : ObjectId.Null,
                HandleText = snapshot.Handle,
                LayerName = snapshot.LayerName,
                ObjectTypeName = snapshot.ObjectTypeName,
                CadLength = snapshot.CadLength,
                HasSavedAttributes = snapshot.HasSavedAttributes,
                InferredKind = snapshot.InferredKind,
                Attributes = snapshot.Attributes
            };
        }
    }
}
