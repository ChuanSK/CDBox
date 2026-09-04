using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CDBox.Wastewater.Module;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 看板与正式工程量表专用的只读 CAD 适配层。只读取污水模块
    /// 自己维护的 Xrecord，不再反向依赖基础程序集的属性编辑服务。
    /// </summary>
    internal static class WastewaterQuantityCadReader
    {
        public static List<ObjectId> FindObjectsWithSavedAttributes(
            Document document)
        {
            if (document == null) throw new ArgumentNullException("document");
            var result = new List<ObjectId>();
            IQuantityAttributeCadStore store = QuantityAttributeCadStoreRegistry
                .GetRequired();
            Database db = document.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord space = tr.GetObject(db.CurrentSpaceId,
                    OpenMode.ForRead, false) as BlockTableRecord;
                if (space != null)
                    foreach (ObjectId id in space)
                        try
                        {
                            Entity entity = tr.GetObject(id, OpenMode.ForRead,
                                false) as Entity;
                            if (entity != null && store.HasAttributes(entity, tr))
                                result.Add(id);
                        }
                        catch { }
                tr.Commit();
            }
            return result;
        }

        public static WastewaterQuantityCadObject Read(Document document,
            ObjectId objectId)
        {
            if (document == null || objectId.IsNull) return null;
            string handleText = string.Empty;
            try
            {
                using (Transaction handleTransaction = document.Database
                    .TransactionManager.StartTransaction())
                {
                    Entity handleEntity = handleTransaction.GetObject(
                        objectId, OpenMode.ForRead, false) as Entity;
                    if (handleEntity != null)
                        handleText = handleEntity.Handle.ToString();
                    handleTransaction.Commit();
                }
            }
            catch { }
            IQuantityAttributeEditorCadService adapter =
                WastewaterRuntimeServices.AttributeCad;
            if (adapter != null && handleText.Length > 0)
                try
                {
                    QuantityAttributeCadObject exact = adapter.Read(
                        document.Name, handleText);
                    if (exact != null && exact.Selected)
                        return new WastewaterQuantityCadObject
                        {
                            ObjectId = objectId,
                            HandleText = exact.Handle,
                            LayerName = exact.LayerName,
                            ObjectTypeName = exact.ObjectTypeName,
                            CadLength = exact.CadLength,
                            HasSavedAttributes = exact.HasSavedAttributes,
                            InferredKind = exact.InferredKind,
                            Attributes = exact.Attributes == null
                                ? QuantityPipeAttributes.Default
                                : exact.Attributes.Clone()
                        };
                }
                catch { }

            IQuantityAttributeCadStore store = QuantityAttributeCadStoreRegistry
                .GetRequired();
            Database db = document.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity entity;
                try
                {
                    entity = tr.GetObject(objectId, OpenMode.ForRead, false)
                        as Entity;
                }
                catch { return null; }
                if (entity == null || !store.HasAttributes(entity, tr))
                    return null;
                QuantityPipeAttributes attributes = store.ReadAttributes(
                    entity, tr);
                Curve curve = entity as Curve;
                double length = 0.0;
                if (curve != null)
                    try
                    {
                        length = curve.GetDistanceAtParameter(
                            curve.EndParam) - curve.GetDistanceAtParameter(
                                curve.StartParam);
                    }
                    catch { }
                var result = new WastewaterQuantityCadObject
                {
                    ObjectId = objectId,
                    HandleText = entity.Handle.ToString(),
                    LayerName = entity.Layer ?? string.Empty,
                    ObjectTypeName = entity.GetType().Name,
                    CadLength = Math.Abs(length),
                    HasSavedAttributes = true,
                    InferredKind = attributes == null
                        ? string.Empty : attributes.ObjectKind,
                    Attributes = attributes ?? QuantityPipeAttributes.Default
                };
                tr.Commit();
                return result;
            }
        }
    }

    internal sealed class WastewaterQuantityCadObject
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
}
