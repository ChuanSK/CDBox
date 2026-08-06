using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Modules.LongitudinalProfile
{
    public sealed class LongitudinalProfileCommands
    {
        [CommandMethod("ZDM", CommandFlags.Modal
            | CommandFlags.UsePickSet)]
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
                foreach (ObjectId id in
                    QuantityPipeAttributeService
                        .FindObjectsWithSavedAttributes(document))
                {
                    QuantityPipeSelectionInfo info =
                        QuantityPipeAttributeService.ReadPipe(
                            document, id);
                    if (info == null || info.Attributes == null)
                        continue;
                    QuantityPipeAttributes attrs = info.Attributes;
                    if (QuantityPipeAttributes.IsMainPipeKind(
                            attrs.ObjectKind)
                        && info.HasSavedAttributes
                        && info.CadLength > 0)
                    {
                        pipes.Add(new LongitudinalProfilePipeData
                        {
                            SourceId = info.HandleText,
                            StartNode = attrs.StartNode,
                            EndNode = attrs.EndNode,
                            Diameter = attrs.Diameter,
                            Foundation = ResolveFoundation(attrs),
                            OuterDiameter = attrs.PipeOuterDiameter,
                            PlanLength = info.CadLength,
                            SelectionOrder = order++
                        });
                    }
                    else if (QuantityPipeAttributes.IsNodeKind(
                        attrs.ObjectKind))
                    {
                        wells.Add(new LongitudinalProfileWellData
                        {
                            SourceId = info.HandleText,
                            NodeNo = attrs.NodeNo,
                            WellSpec = attrs.WellSpec,
                            WellType = attrs.WellType,
                            GroundElevation = attrs.GroundElevation,
                            WellDepth = attrs.WellDepth,
                            SiltWellDeductDepth500 =
                                attrs.SiltWellDeductDepth500,
                            SiltWellDeductDepth700 =
                                attrs.SiltWellDeductDepth700
                        });
                    }
                }

                LongitudinalProfileBuildResult built =
                    LongitudinalProfileCalculator.BuildBetweenNodes(
                        pipes, wells, startNo, endNo);
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

        [CommandMethod("ZDMSZ", CommandFlags.Modal)]
        public void OpenSettings()
        {
            try
            {
                CDBoxStudioLongitudinalProfileSettingsWindow.ShowWindow(
                    new AcadMainWindow());
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
    }
}
