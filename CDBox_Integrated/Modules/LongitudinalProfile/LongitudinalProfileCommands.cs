using System;
using System.Collections.Generic;
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
        [CommandMethod("CDPROFILE", CommandFlags.Modal
            | CommandFlags.UsePickSet)]
        [CommandMethod("CDZDM", CommandFlags.Modal
            | CommandFlags.UsePickSet)]
        public void Generate()
        {
            Document document =
                AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            Editor editor = document.Editor;
            try
            {
                PromptSelectionResult selection =
                    editor.GetSelection(new PromptSelectionOptions
                    {
                        MessageForAdding =
                            "\n请选择一条或多条连续主管管线：",
                        MessageForRemoval =
                            "\n移除不参与纵断面的对象："
                    });
                if (selection.Status != PromptStatus.OK
                    || selection.Value == null
                    || selection.Value.Count == 0)
                    return;

                var pipes =
                    new List<LongitudinalProfilePipeData>();
                int order = 0;
                foreach (SelectedObject selected in selection.Value)
                {
                    if (selected == null || selected.ObjectId.IsNull)
                        continue;
                    QuantityPipeSelectionInfo info =
                        QuantityPipeAttributeService.ReadPipe(
                            document, selected.ObjectId);
                    if (info == null || !info.HasSavedAttributes
                        || info.Attributes == null
                        || !QuantityPipeAttributes.IsMainPipeKind(
                            info.Attributes.ObjectKind)
                        || info.CadLength <= 0)
                        continue;
                    pipes.Add(new LongitudinalProfilePipeData
                    {
                        SourceId = info.HandleText,
                        StartNode = info.Attributes.StartNode,
                        EndNode = info.Attributes.EndNode,
                        Diameter = info.Attributes.Diameter,
                        OuterDiameter =
                            info.Attributes.PipeOuterDiameter,
                        PlanLength =
                            info.Attributes.EffectiveLength(info.CadLength),
                        SelectionOrder = order++
                    });
                }
                if (pipes.Count == 0)
                {
                    editor.WriteMessage(
                        "\n[纵断面] 所选对象中没有已保存工程量属性的主管管线。");
                    return;
                }

                var wells =
                    new List<LongitudinalProfileWellData>();
                foreach (ObjectId id in
                    QuantityPipeAttributeService
                        .FindObjectsWithSavedAttributes(document))
                {
                    QuantityPipeSelectionInfo info =
                        QuantityPipeAttributeService.ReadPipe(document, id);
                    if (info == null || info.Attributes == null
                        || !QuantityPipeAttributes.IsNodeKind(
                            info.Attributes.ObjectKind))
                        continue;
                    QuantityPipeAttributes attrs = info.Attributes;
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

                LongitudinalProfileBuildResult built =
                    LongitudinalProfileCalculator.Build(pipes, wells);
                if (!built.Success)
                {
                    editor.WriteMessage("\n[纵断面] " + built.Message);
                    CDBoxMessageBox.Show(new AcadMainWindow(),
                        built.Message, "纵断面生成",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }

                LongitudinalProfileDrawingResult drawn =
                    LongitudinalProfileDrawingService
                        .SelectPositionAndDraw(document, built.Profile,
                            LongitudinalProfileSettingsStore.Load());
                editor.WriteMessage("\n[纵断面] " + drawn.Message
                    + (drawn.Success
                        ? " 生成对象：" + drawn.EntityCount + " 个。"
                        : string.Empty));
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(
                    "\n[纵断面] 生成失败，图纸未写入不完整结果："
                    + ex.Message);
            }
        }

        [CommandMethod("CDPROFILESET", CommandFlags.Modal)]
        [CommandMethod("CDZDMSZ", CommandFlags.Modal)]
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
    }
}
