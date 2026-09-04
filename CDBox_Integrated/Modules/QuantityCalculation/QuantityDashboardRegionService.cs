using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CDBox.Shared.Modules;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 基础检查、同步与旧属性流程使用的区域兼容门面。
    /// 区域实体的创建、编辑、存储和判定均由污水模块实现。
    /// </summary>
    public static class QuantityDashboardRegionService
    {
        public static List<string> FilterHandlesToSavedScope(
            Document document, IEnumerable<string> handles)
        {
            var values = (handles ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()).Distinct(
                    System.StringComparer.OrdinalIgnoreCase).ToList();
            IQuantityDashboardScopeService service =
                QuantityDashboardScopeRegistry.GetOrDefault();
            if (service == null) return values;
            return (service.FilterHandlesToSavedScope(document, values)
                ?? values).ToList();
        }

        public static ObjectId GetSavedRegionObjectId(Database database,
            Transaction transaction)
        {
            IQuantityDashboardScopeService service =
                QuantityDashboardScopeRegistry.GetOrDefault();
            if (service == null) return ObjectId.Null;
            object value = service.GetSavedRegionObjectId(database,
                transaction);
            return value is ObjectId ? (ObjectId)value : ObjectId.Null;
        }

        public static bool IsEntityIncluded(Entity entity, Polyline region)
        {
            IQuantityDashboardScopeService service =
                QuantityDashboardScopeRegistry.GetOrDefault();
            return service == null || service.IsEntityIncluded(entity, region);
        }
    }
}
