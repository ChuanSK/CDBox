using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CDBox.Shared.Modules;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    internal sealed class WastewaterQuantityDashboardScopeAdapter
        : IQuantityDashboardScopeService
    {
        public IReadOnlyList<string> FilterHandlesToSavedScope(
            object document, IEnumerable<string> handles)
        {
            return WastewaterQuantityDashboardRegionService
                .FilterHandlesToSavedScope(document as Document, handles);
        }

        public object GetSavedRegionObjectId(object database,
            object transaction)
        {
            Database db = database as Database;
            Transaction tr = transaction as Transaction;
            return db == null || tr == null ? (object)ObjectId.Null
                : WastewaterQuantityDashboardRegionService
                    .GetSavedRegionObjectId(db, tr);
        }

        public bool IsEntityIncluded(object entity, object region)
        {
            return WastewaterQuantityDashboardRegionService.IsEntityIncluded(
                entity as Entity, region as Polyline);
        }
    }
}
