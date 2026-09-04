using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CDBox.Shared.Services;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Core.Modules
{
    /// <summary>
    /// 将主程序集中的既有井属性存储格式转换为污水模块可消费的纯数据。
    /// </summary>
    internal sealed class WastewaterResultTableDataSourceAdapter
        : IWastewaterResultTableDataSource
    {
        public IList<WastewaterResultTableNodeData> ReadNodes(
            IEnumerable<string> objectHandles)
        {
            var result = new List<WastewaterResultTableNodeData>();
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null || objectHandles == null) return result;

            Database database = document.Database;
            using (Transaction transaction = database.TransactionManager
                .StartOpenCloseTransaction())
            {
                foreach (string handleText in objectHandles)
                {
                    ObjectId id;
                    if (!TryResolve(database, handleText, out id)) continue;

                    QuantityPipeAttributes attributes;
                    if (!QuantityPipeAttributeService.TryReadSavedAttributes(
                            database, transaction, id, out attributes)
                        || attributes == null
                        || !QuantityPipeAttributes.IsNodeKind(
                            attributes.ObjectKind))
                        continue;

                    Entity entity = transaction.GetObject(id,
                        OpenMode.ForRead, false) as Entity;
                    Point3d center;
                    if (entity == null || !TryGetCenter(entity, out center))
                        continue;

                    result.Add(new WastewaterResultTableNodeData
                    {
                        ObjectHandle = id.Handle.ToString(),
                        NodeNo = attributes.NodeNo ?? string.Empty,
                        PositionX = center.X,
                        PositionY = center.Y,
                        GroundElevation = attributes.GroundElevation,
                        WellDepth = attributes.WellDepth,
                        WellSpec = attributes.WellSpec ?? string.Empty
                    });
                }
                transaction.Commit();
            }
            return result;
        }

        private static bool TryResolve(Database database, string text,
            out ObjectId id)
        {
            id = ObjectId.Null;
            long value;
            if (database == null || !long.TryParse(
                    (text ?? string.Empty).Trim(), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out value))
                return false;
            try
            {
                id = database.GetObjectId(false, new Handle(value), 0);
                return !id.IsNull && !id.IsErased;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetCenter(Entity entity, out Point3d center)
        {
            BlockReference block = entity as BlockReference;
            if (block != null)
            {
                center = block.Position;
                return true;
            }
            DBPoint point = entity as DBPoint;
            if (point != null)
            {
                center = point.Position;
                return true;
            }
            Circle circle = entity as Circle;
            if (circle != null)
            {
                center = circle.Center;
                return true;
            }
            try
            {
                Extents3d extents = entity.GeometricExtents;
                center = new Point3d(
                    (extents.MinPoint.X + extents.MaxPoint.X) / 2.0,
                    (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0,
                    (extents.MinPoint.Z + extents.MaxPoint.Z) / 2.0);
                return true;
            }
            catch
            {
                center = Point3d.Origin;
                return false;
            }
        }
    }
}
