using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CDBox.Shared.Wastewater.Cad;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 基础命令层的薄入口。属性识别、重叠对象预览和编辑器打开
    /// 全部由可选的污水模块实现。
    /// </summary>
    internal static class QuantityAttributeDoubleClickService
    {
        public static bool CanOpen(Document document,
            ObjectId[] selectedIds)
        {
            IWastewaterCadInteractionService service =
                WastewaterCadInteractionRegistry.Current;
            return service != null && service.CanOpenAttribute(document,
                Box(selectedIds));
        }

        public static bool TryOpen(Document document,
            ObjectId[] selectedIds)
        {
            IWastewaterCadInteractionService service =
                WastewaterCadInteractionRegistry.Current;
            return service != null && service.TryOpenAttribute(document,
                Box(selectedIds));
        }

        public static bool TryOpenWithOverlapSelection(Document document,
            ObjectId[] selectedIds, Point screenPoint)
        {
            IWastewaterCadInteractionService service =
                WastewaterCadInteractionRegistry.Current;
            return service != null && service.TryOpenAttributeWithOverlap(
                document, Box(selectedIds), screenPoint.X, screenPoint.Y);
        }

        private static IEnumerable<object> Box(IEnumerable<ObjectId> ids)
        {
            return (ids ?? Enumerable.Empty<ObjectId>())
                .Select(id => (object)id).ToArray();
        }
    }
}
