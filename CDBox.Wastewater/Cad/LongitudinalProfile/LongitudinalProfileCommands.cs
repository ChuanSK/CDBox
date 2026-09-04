using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Modules.LongitudinalProfile
{
    public sealed class LongitudinalProfileCommands
    {
        public void Generate()
        {
            Document document =
                AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            try
            {
                QuantityPipeAttributes startNode =
                    QuantityPipeAttributeService.SelectNodeWithPreview(
                        document, true);
                if (startNode == null) return;
                QuantityPipeAttributes endNode =
                    QuantityPipeAttributeService.SelectNodeWithPreview(
                        document, false);
                if (endNode == null) return;
                string startNo = (startNode.NodeNo ?? string.Empty).Trim();
                string endNo = (endNode.NodeNo ?? string.Empty).Trim();
                if (startNo.Length == 0 || endNo.Length == 0)
                {
                    editor.WriteHudMessage(
                        "\n[纵断面] 起点节点或终点节点缺少井编号。");
                    return;
                }
                if (string.Equals(startNo, endNo,
                    StringComparison.CurrentCultureIgnoreCase))
                {
                    editor.WriteHudMessage(
                        "\n[纵断面] 起点节点和终点节点不能相同。");
                    return;
                }

                var pipes =
                    new List<LongitudinalProfilePipeData>();
                var wells =
                    new List<LongitudinalProfileWellData>();
                int order = 0;
                LongitudinalProfileBuildResult built;
                using (var progress = CDBoxProgressSession.Start(document,
                    "纵断面数据准备", "正在检索图纸中的主管和井对象…",
                    "LongitudinalProfile", "profile-data-progress"))
                {
                    progress.ReportMarquee(
                        "正在检索图纸中的主管和井对象...");
                    List<ObjectId> sourceIds =
                        QuantityPipeAttributeService
                            .FindObjectsWithSavedAttributes(document,
                                delegate(int current, int total,
                                    string message)
                                {
                                    progress.ReportMarquee(message + "（"
                                        + current + " / " + total + "）");
                                })
                            .ToList();
                    List<ObjectId> knownNodeObjectIds =
                        QuantityPipeAttributeService
                            .FindSupportedNodeObjectIds(document,
                                delegate(int current, int total,
                                    string message)
                                {
                                    progress.ReportMarquee(message + "（"
                                        + current + " / " + total + "）");
                                });
                    int total = Math.Max(1, sourceIds.Count + 1);
                    int reportStep = Math.Max(1,
                        sourceIds.Count / 100);
                    progress.Report(0, total,
                        "正在读取主管和井属性...");
                    for (int sourceIndex = 0;
                        sourceIndex < sourceIds.Count; sourceIndex++)
                    {
                        ObjectId id = sourceIds[sourceIndex];
                        QuantityPipeSelectionInfo info =
                            QuantityPipeAttributeService.ReadPipe(
                                document, id, knownNodeObjectIds);
                        if (info != null && info.Attributes != null)
                        {
                            QuantityPipeAttributes attrs =
                                info.Attributes;
                            if (QuantityPipeAttributes.IsMainPipeKind(
                                    attrs.ObjectKind)
                                && info.HasSavedAttributes
                                && info.CadLength > 0)
                            {
                                QuantityPipeEndpointConnectionResult connection =
                                    attrs.IsSpecialObject
                                        ? QuantityPipeAttributeService
                                            .ResolvePhysicalNodeConnections(
                                                document, id, attrs,
                                                knownNodeObjectIds)
                                        : null;
                                string physicalStart = connection == null
                                    ? attrs.StartNode
                                    : connection.DetectedStartNode;
                                string physicalEnd = connection == null
                                    ? attrs.EndNode
                                    : connection.DetectedEndNode;
                                string pipeStart = !string.IsNullOrWhiteSpace(
                                    physicalStart) ? physicalStart :
                                    (attrs.IsSpecialObject
                                        ? attrs.StartNode : string.Empty);
                                string pipeEnd = !string.IsNullOrWhiteSpace(
                                    physicalEnd) ? physicalEnd :
                                    (attrs.IsSpecialObject
                                        ? attrs.EndNode : string.Empty);
                                var pipe =
                                    new LongitudinalProfilePipeData
                                {
                                    SourceId = info.HandleText,
                                    StartNode = pipeStart,
                                    EndNode = pipeEnd,
                                    Diameter = attrs.Diameter,
                                    Foundation = ResolveFoundation(attrs),
                                    OuterDiameter =
                                        attrs.PipeOuterDiameter,
                                    PlanLength = info.CadLength,
                                    SelectionOrder = order++,
                                    StartDepth = attrs.StartDepth,
                                    EndDepth = attrs.EndDepth,
                                    StartInvertElevation =
                                        attrs.StartInvertElevation,
                                    EndInvertElevation =
                                        attrs.EndInvertElevation
                                };
                                Point3d geometryStart;
                                Point3d geometryEnd;
                                if (TryReadCurveEndpoints(document, id,
                                    out geometryStart,
                                    out geometryEnd))
                                {
                                    pipe.HasGeometry = true;
                                    pipe.GeometryStartX =
                                        geometryStart.X;
                                    pipe.GeometryStartY =
                                        geometryStart.Y;
                                    pipe.GeometryEndX = geometryEnd.X;
                                    pipe.GeometryEndY = geometryEnd.Y;
                                }
                                pipes.Add(pipe);
                            }
                            else if (QuantityPipeAttributes.IsNodeKind(
                                attrs.ObjectKind))
                            {
                                var well =
                                    new LongitudinalProfileWellData
                                {
                                    SourceId = info.HandleText,
                                    NodeNo = attrs.NodeNo,
                                    WellSpec = attrs.WellSpec,
                                    WellType = attrs.WellType,
                                    GroundElevation =
                                        attrs.GroundElevation,
                                    WellDepth = attrs.WellDepth,
                                    SiltWellDeductDepth500 =
                                        attrs.SiltWellDeductDepth500,
                                    SiltWellDeductDepth700 =
                                        attrs.SiltWellDeductDepth700
                                };
                                Point3d position;
                                if (TryReadEntityPosition(document, id,
                                    out position))
                                {
                                    well.HasPosition = true;
                                    well.PositionX = position.X;
                                    well.PositionY = position.Y;
                                }
                                wells.Add(well);
                            }
                        }
                        if ((sourceIndex + 1) % reportStep == 0
                            || sourceIndex + 1 == sourceIds.Count)
                        {
                            progress.Report(sourceIndex + 1, total,
                                "正在读取对象属性（"
                                + (sourceIndex + 1) + " / "
                                + sourceIds.Count + "）...");
                        }
                    }
                    progress.Report(sourceIds.Count, total,
                        "正在检查管线连通关系并计算纵断面...");
                    built = LongitudinalProfileCalculator
                        .BuildBetweenNodes(pipes, wells, startNo, endNo);
                    progress.Report(total, total,
                        built.Success
                            ? "纵断面数据准备完成。"
                            : "纵断面数据检查完成。");
                    progress.Complete(built.Success
                        ? "纵断面数据准备完成。"
                        : "纵断面数据检查完成。");
                }
                if (!built.Success)
                {
                    CDBoxMessageBox.Show(new AcadMainWindow(),
                        built.Message, "纵断面生成",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }

                LongitudinalProfileDrawingResult drawn =
                    LongitudinalProfileReferenceDrawingService
                        .SelectPositionAndDraw(document, built.Profile,
                            LongitudinalProfileSettingsStore.Load());
                editor.WriteHudMessage("\n[纵断面] " + drawn.Message
                    + (drawn.Success
                        ? " 生成对象：" + drawn.EntityCount + " 个。"
                        : string.Empty));
            }
            catch (System.Exception ex)
            {
                editor.WriteHudMessage(
                    "\n[纵断面] 生成失败，图纸未写入不完整结果："
                    + ex.Message);
            }
        }

        public void OpenSettings()
        {
            try
            {
                if (CDBox.Wastewater.Module.WastewaterRuntimeServices
                        .OpenLongitudinalSettings == null)
                    throw new InvalidOperationException(
                        "纵断面设置页面尚未初始化。");
                CDBox.Wastewater.Module.WastewaterRuntimeServices
                    .OpenLongitudinalSettings();
            }
            catch (System.Exception ex)
            {
                CDBoxMessageBox.Show(new AcadMainWindow(), ex.Message,
                    "纵断面设置",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private static string ResolveFoundation(
            QuantityPipeAttributes attributes)
        {
            if (attributes == null) return string.Empty;
            string[] names = QuantityStructureLayer
                .Parse(attributes.BackfillStructure)
                .Where(layer => layer != null && layer.IsCushionLayer)
                .Select(layer => (layer.Name ?? string.Empty).Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            return names.Length > 0
                ? string.Join("、", names)
                : string.Empty;
        }

        private static bool TryReadCurveEndpoints(
            Document document, ObjectId id,
            out Point3d start, out Point3d end)
        {
            start = Point3d.Origin;
            end = Point3d.Origin;
            try
            {
                using (Transaction tr = document.Database
                    .TransactionManager.StartOpenCloseTransaction())
                {
                    Curve curve = tr.GetObject(id, OpenMode.ForRead,
                        false) as Curve;
                    if (curve == null) return false;
                    start = curve.StartPoint;
                    end = curve.EndPoint;
                    return start.DistanceTo(end) > 1e-8;
                }
            }
            catch { return false; }
        }

        private static bool TryReadEntityPosition(
            Document document, ObjectId id, out Point3d position)
        {
            position = Point3d.Origin;
            try
            {
                using (Transaction tr = document.Database
                    .TransactionManager.StartOpenCloseTransaction())
                {
                    Entity entity = tr.GetObject(id, OpenMode.ForRead,
                        false) as Entity;
                    if (entity == null) return false;
                    BlockReference block = entity as BlockReference;
                    if (block != null)
                    {
                        position = block.Position;
                        return true;
                    }
                    DBPoint point = entity as DBPoint;
                    if (point != null)
                    {
                        position = point.Position;
                        return true;
                    }
                    Circle circle = entity as Circle;
                    if (circle != null)
                    {
                        position = circle.Center;
                        return true;
                    }
                    Extents3d extents = entity.GeometricExtents;
                    position = new Point3d(
                        (extents.MinPoint.X + extents.MaxPoint.X) / 2.0,
                        (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0,
                        (extents.MinPoint.Z + extents.MaxPoint.Z) / 2.0);
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
