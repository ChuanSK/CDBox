using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CDBox.Shared.Services;
using TCPipeAutoDraw.Modules.LayerManager;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Core.Modules
{
    /// <summary>
    /// 业务模块写入图层分类时使用的基础组件适配器。
    /// </summary>
    internal sealed class BaseLayerClassificationAdapter
        : ICDBoxLayerClassificationService
    {
        public void EnsureGeneratedLayerClassification(string layerName,
            string parentGroup)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return;
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;

            Database database = document.Database;
            using (Transaction transaction = database.TransactionManager
                .StartTransaction())
            {
                LayerTable table = transaction.GetObject(
                    database.LayerTableId, OpenMode.ForRead) as LayerTable;
                if (table == null || !table.Has(layerName)) return;

                LayerMetadata metadata = LayerManagerService.GetLayerMetadata(
                    database, transaction, layerName) ?? new LayerMetadata();
                string requested = string.IsNullOrWhiteSpace(parentGroup)
                    ? "CDBox图层" : parentGroup.Trim();
                if (!string.Equals(metadata.ParentGroup, requested,
                        StringComparison.Ordinal))
                {
                    metadata.ParentGroup = requested;
                    LayerManagerService.SetLayerMetadata(database,
                        transaction, layerName, metadata);
                }
                transaction.Commit();
            }
        }

        public object GetLayerMetadata(object database, object transaction,
            string layerName)
        {
            Database db = database as Database;
            Transaction tr = transaction as Transaction;
            if (db == null || tr == null || string.IsNullOrWhiteSpace(layerName))
                return new LayerMetadata();
            return LayerManagerService.GetLayerMetadata(db, tr,
                layerName) ?? new LayerMetadata();
        }
    }
}
