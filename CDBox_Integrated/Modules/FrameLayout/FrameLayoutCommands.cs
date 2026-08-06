using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TCPipeAutoDraw.UI;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    public class FrameLayoutCommands
    {
        [CommandMethod("TCFRAMEADD")]
        public void AddFrameTemplate()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor editor = doc.Editor;
            Database db = doc.Database;

            try
            {
                ObjectId frameId = PromptFrameOrImportFromFile(doc);
                if (frameId.IsNull) return;

                string paperSize = ChoosePaperSize(null);
                if (string.IsNullOrWhiteSpace(paperSize)) return;
                string templateName = BuildDefaultTemplateName(paperSize);

                PromptPointResult first = editor.GetHudPoint(
                    "\n????????????????");
                if (first.Status != PromptStatus.OK) return;
                PromptCornerOptions cornerOptions = new PromptCornerOptions(
                    "\n???????????", first.Value);
                PromptPointResult second = editor.GetHudCorner(cornerOptions);
                if (second.Status != PromptStatus.OK) return;

                FrameTemplateCatalogItem item;
                string resourcePath;
                using (DocumentLock documentLock = doc.LockDocument())
                using (Transaction transaction =
                    db.TransactionManager.StartTransaction())
                {
                    BlockReference reference = transaction.GetObject(frameId,
                        OpenMode.ForRead, false) as BlockReference;
                    if (reference == null)
                        throw new InvalidOperationException("????????????????");

                    FrameTemplateService service = new FrameTemplateService();
                    FrameTemplateInfo info = service.CreateInfoFromBlockReference(
                        db, transaction, reference, first.Value, second.Value);
                    string validation;
                    if (!info.IsValid(out validation))
                        throw new InvalidOperationException(validation);

                    string id = Guid.NewGuid().ToString("N");
                    resourcePath = FrameTemplateCatalogStore.GetTemplateDwgPath(id);
                    ObjectId definitionId = reference.IsDynamicBlock
                        ? reference.DynamicBlockTableRecord
                        : reference.BlockTableRecord;
                    service.ExportBlockDefinition(db, definitionId, resourcePath);

                    info.TemplateName = templateName;
                    info.BlockName = FrameTemplateService.BuildCatalogBlockName(id);
                    item = FrameTemplateCatalogItem.FromInfo(info, paperSize,
                        FrameTemplateCatalogStore.ToStoredTemplatePath(resourcePath),
                        id);
                    List<FrameTemplatePreviewSegment> previewSegments;
                    int previewEntityCount;
                    if (FrameTemplatePreviewService.TryBuild(transaction,
                        definitionId, out previewSegments,
                        out previewEntityCount))
                    {
                        item.PreviewSegments = previewSegments;
                        item.PreviewEntityCount = previewEntityCount;
                    }
                    service.EnsureTemplateBlock(db, transaction, item);
                    transaction.Commit();
                }

                FrameTemplateCatalogStore.Upsert(item);
                editor.WriteHudMessage(
                    "\n[????] ??? {0} / {1}????? {2:0.###} ? {3:0.###}?",
                    item.PaperSize, item.TemplateName, item.ValidWidth,
                    item.ValidHeight);
            }
            catch (System.Exception ex)
            {
                editor.WriteHudMessage("\n[????] ?????{0}", ex.Message);
            }
        }

        [CommandMethod("TCFRAMECUT")]
        public void PlaceCutRegions()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor editor = doc.Editor;
            Database db = doc.Database;

            try
            {
                FrameTemplateCatalog catalog = FrameTemplateCatalogStore.Load();
                FrameTemplateCatalogItem template = PromptTemplate(editor,
                    catalog, null, null);
                if (template == null) return;

                string mode;
                if (!CDBoxStudioFrameChoiceWindow.TryChoose(
                    "??????", "???????????????",
                    new List<CDBoxStudioFrameChoiceItem>
                    {
                        new CDBoxStudioFrameChoiceItem
                        {
                            Value = "Rectangle",
                            Title = "??????",
                            Detail = "?????????????????????",
                            Badge = "??"
                        },
                        new CDBoxStudioFrameChoiceItem
                        {
                            Value = "Curve",
                            Title = "??????",
                            Detail = "?????????????????????"
                        }
                    }, "Rectangle", out mode, new AcadMainWindow())) return;

                if (string.Equals(mode, "Curve",
                    StringComparison.OrdinalIgnoreCase))
                    AttachClosedCurves(doc, template);
                else
                    PlaceRectangles(doc, template);
            }
            catch (System.Exception ex)
            {
                editor.WriteHudMessage("\n[????] ?????{0}", ex.Message);
            }
        }

        public void CutAndLayoutFrames()
        {
            PlaceCutRegions();
        }

        [CommandMethod("TCFRAMELAYOUT")]
        public void LayoutFrames()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor editor = doc.Editor;
            Database db = doc.Database;

            try
            {
                FrameCutRegionService regionService = new FrameCutRegionService();
                IList<FrameCutRegionInfo> regions =
                    regionService.ReadSelectedRegions(editor, db);
                if (regions.Count == 0)
                {
                    editor.WriteHudMessage("\n[????] ????????????");
                    return;
                }

                FrameTemplateCatalog catalog = FrameTemplateCatalogStore.Load();
                Dictionary<string, FrameTemplateCatalogItem> choices =
                    new Dictionary<string, FrameTemplateCatalogItem>(
                        StringComparer.OrdinalIgnoreCase);
                foreach (IGrouping<string, FrameCutRegionInfo> group in regions
                    .GroupBy(x => x.PaperSize ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase))
                {
                    FrameCutRegionInfo first = group.First();
                    FrameTemplateCatalogItem selected = PromptTemplate(editor,
                        catalog, group.Key, first.TemplateId);
                    if (selected == null) return;
                    foreach (FrameCutRegionInfo region in group)
                    {
                        string key = region.TemplateId ?? string.Empty;
                        choices[key] = selected;
                    }
                }

                PromptPointResult point = editor.GetHudPoint(
                    "\n??????????????");
                if (point.Status != PromptStatus.OK) return;

                FrameLayoutService.LayoutResult result;
                using (DocumentLock documentLock = doc.LockDocument())
                using (Transaction transaction =
                    db.TransactionManager.StartTransaction())
                {
                    result = new FrameLayoutService().LayoutSelectedRegions(
                        editor, db, transaction, regions, choices, point.Value,
                        FrameLayoutSettingsStore.Load());
                    transaction.Commit();
                }
                editor.Regen();
                editor.WriteHudMessage(
                    "\n[????] ??? {0} ????? {1} ?????? {2} ??",
                    result.Count, result.Rows,
                    FrameLayoutSettingsStore.Load().FramesPerRow);
                WriteLayoutWarnings(editor, result);
            }
            catch (System.Exception ex)
            {
                editor.WriteHudMessage("\n[????] ???{0}", ex.Message);
            }
        }

        [CommandMethod("TCFRAMEPLACE")]
        public void PlaceFramesDirectly()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor editor = doc.Editor;
            Database db = doc.Database;
            try
            {
                FrameTemplateCatalogItem template = PromptTemplate(editor,
                    FrameTemplateCatalogStore.Load(), null, null);
                if (template == null) return;
                PromptIntegerOptions countOptions = new PromptIntegerOptions(
                    "\n?????? <1>: ")
                {
                    AllowNegative = false,
                    AllowZero = false,
                    DefaultValue = 1,
                    UseDefaultValue = true,
                    LowerLimit = 1,
                    UpperLimit = 10000
                };
                PromptIntegerResult count = editor.GetHudInteger(countOptions);
                if (count.Status != PromptStatus.OK) return;
                PromptPointResult point = editor.GetHudPoint(
                    "\n??????????????");
                if (point.Status != PromptStatus.OK) return;

                FrameLayoutService.LayoutResult result;
                using (DocumentLock documentLock = doc.LockDocument())
                using (Transaction transaction =
                    db.TransactionManager.StartTransaction())
                {
                    result = new FrameLayoutService().PlaceFrames(db, transaction,
                        template, point.Value, count.Value,
                        FrameLayoutSettingsStore.Load());
                    transaction.Commit();
                }
                editor.Regen();
                editor.WriteHudMessage("\n[????] ??? {0} ? {1} ???",
                    result.Count, template.PaperSize);
                WriteLayoutWarnings(editor, result);
            }
            catch (System.Exception ex)
            {
                editor.WriteHudMessage("\n[????] ???{0}", ex.Message);
            }
        }

        [CommandMethod("TCFRAMESET")]
        public void OpenFrameSettings()
        {
            CDBoxStudioFrameSettingsWindow.ShowWindow(new AcadMainWindow());
        }

        private static void PlaceRectangles(Document doc,
            FrameTemplateCatalogItem template)
        {
            Editor editor = doc.Editor;
            Database db = doc.Database;
            int count = 0;
            while (true)
            {
                FrameCutRegionPlacementJig centerJig =
                    FrameCutRegionPlacementJig.ForCenter(
                        template.ValidWidth, template.ValidHeight);
                string centerPrompt = count > 0
                    ? "??????????????? Enter ? Esc ??"
                    : "??????????????????";
                PromptResult positionResult = editor.DragWithHud(
                    centerJig, centerPrompt);
                if (positionResult.Status != PromptStatus.OK) break;

                FrameCutRegionPlacementJig rotationJig =
                    FrameCutRegionPlacementJig.ForRotation(centerJig.Center,
                        template.ValidWidth, template.ValidHeight);
                PromptResult rotationResult = editor.DragWithHud(
                    rotationJig, "???????????????????");
                if (rotationResult.Status != PromptStatus.OK) break;

                ObjectId previewId;
                using (DocumentLock documentLock = doc.LockDocument())
                using (Transaction transaction =
                    db.TransactionManager.StartTransaction())
                {
                    previewId = new FrameCutRegionService()
                        .CreateRectanglePreview(db, transaction,
                            centerJig.Center, template.ValidWidth,
                            template.ValidHeight, rotationJig.Rotation);
                    transaction.Commit();
                }
                editor.Regen();

                try
                {
                    double cutRotation;
                    if (!TryPromptDirection(editor, out cutRotation))
                    {
                        EraseCutRegionPreview(doc, previewId);
                        break;
                    }
                    using (DocumentLock documentLock = doc.LockDocument())
                    using (Transaction transaction =
                        db.TransactionManager.StartTransaction())
                    {
                        new FrameCutRegionService().FinalizeRectangle(
                            transaction, previewId, template.Id,
                            template.PaperSize, cutRotation);
                        transaction.Commit();
                    }
                    previewId = ObjectId.Null;
                    count++;
                }
                catch
                {
                    EraseCutRegionPreview(doc, previewId);
                    throw;
                }
            }
            if (count > 0)
            {
                editor.Regen();
                editor.WriteHudMessage("\n[????] ??? {0} ? {1} ??????",
                    count, template.PaperSize);
            }
        }

        private static void EraseCutRegionPreview(Document doc,
            ObjectId previewId)
        {
            if (doc == null || previewId.IsNull) return;
            try
            {
                using (DocumentLock documentLock = doc.LockDocument())
                using (Transaction transaction = doc.Database
                    .TransactionManager.StartTransaction())
                {
                    Entity entity = transaction.GetObject(previewId,
                        OpenMode.ForWrite, false) as Entity;
                    if (entity != null) entity.Erase();
                    transaction.Commit();
                }
                doc.Editor.Regen();
            }
            catch
            {
            }
        }

        private static void AttachClosedCurves(Document doc,
            FrameTemplateCatalogItem template)
        {
            Editor editor = doc.Editor;
            Database db = doc.Database;
            PromptSelectionOptions options = new PromptSelectionOptions
            {
                MessageForAdding = "\n????????????"
            };
            PromptSelectionResult selection = editor.GetHudSelection(options);
            if (selection.Status != PromptStatus.OK) return;
            double rotation;
            if (!TryPromptDirection(editor, out rotation)) return;
            int count = 0;
            List<string> rejected = new List<string>();
            List<string> oversized = new List<string>();
            using (DocumentLock documentLock = doc.LockDocument())
            using (Transaction transaction =
                db.TransactionManager.StartTransaction())
            {
                CadDbHelper.EnsureLayer(db, transaction,
                    FrameCutRegionService.RegionLayerName);
                FrameCutRegionService service = new FrameCutRegionService();
                foreach (SelectedObject selected in selection.Value)
                {
                    if (selected == null || selected.ObjectId.IsNull) continue;
                    Entity entity = transaction.GetObject(selected.ObjectId,
                        OpenMode.ForRead, false) as Entity;
                    if (!(entity is Curve))
                    {
                        rejected.Add(selected.ObjectId.Handle.ToString()
                            + " ????");
                        continue;
                    }
                    string message;
                    if (!service.ValidateBoundary(entity, rotation,
                        template.ValidWidth, template.ValidHeight, out message))
                    {
                        string detail = "?? " + selected.ObjectId.Handle
                            + "?" + message;
                        if (message.IndexOf("??",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                            oversized.Add(detail);
                        else
                            rejected.Add(detail);
                        continue;
                    }
                    entity.UpgradeOpen();
                    entity.Layer = FrameCutRegionService.RegionLayerName;
                    entity.ColorIndex = 1;
                    service.AttachMetadata(transaction, entity, template.Id,
                        template.PaperSize, rotation);
                    count++;
                }
                transaction.Commit();
            }
            editor.Regen();
            editor.WriteHudMessage("\n[????] ??? {0} ??????", count);
            if (oversized.Count > 0)
            {
                CDBoxStudioFrameNoticeWindow.ShowNotice(
                    "??????????", oversized,
                    new AcadMainWindow());
            }
            foreach (string message in rejected.Take(5))
                editor.WriteHudMessage("\n  ???{0}", message);
            if (rejected.Count > 5)
                editor.WriteHudMessage("\n  ?? {0} ???????", rejected.Count - 5);
        }

        private static bool TryPromptDirection(Editor editor,
            out double rotation)
        {
            rotation = 0;
            PromptPointResult start = editor.GetHudPoint(
                "\n?????????");
            if (start.Status != PromptStatus.OK) return false;
            PromptPointOptions endOptions = new PromptPointOptions(
                "\n?????????")
            {
                BasePoint = start.Value,
                UseBasePoint = true
            };
            PromptPointResult end = editor.GetHudPoint(endOptions);
            if (end.Status != PromptStatus.OK) return false;
            Vector3d direction = end.Value - start.Value;
            if (direction.Length <= GeometryHelper.Eps)
            {
                editor.WriteHudMessage("\n[????] ???????");
                return false;
            }
            rotation = Math.Atan2(direction.Y, direction.X);
            return true;
        }

        private static FrameTemplateCatalogItem PromptTemplate(Editor editor,
            FrameTemplateCatalog catalog, string requiredPaperSize,
            string preferredTemplateId)
        {
            if (catalog == null || catalog.Templates == null
                || catalog.Templates.Count == 0)
            {
                editor.WriteHudMessage(
                    "\n[????] ???????????????????????");
                return null;
            }

            List<FrameTemplateCatalogItem> matches = catalog.Templates
                .Where(x => string.IsNullOrWhiteSpace(requiredPaperSize)
                    || string.Equals(x.PaperSize, requiredPaperSize,
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => PaperOrder(x.PaperSize))
                .ThenByDescending(x => x.IsDefault)
                .ThenBy(x => x.TemplateName)
                .ToList();
            if (matches.Count == 0)
            {
                editor.WriteHudMessage("\n[????] ?? {0} ?????",
                    string.IsNullOrWhiteSpace(requiredPaperSize)
                        ? "???" : requiredPaperSize);
                return null;
            }
            FrameTemplateCatalogItem preferred = matches.FirstOrDefault(x =>
                string.Equals(x.Id, preferredTemplateId,
                    StringComparison.OrdinalIgnoreCase))
                ?? matches.FirstOrDefault(x => x.IsDefault)
                ?? matches[0];
            string selectedId;
            if (!CDBoxStudioFrameChoiceWindow.TryChoose(
                "??????",
                string.IsNullOrWhiteSpace(requiredPaperSize)
                    ? "?????????????????"
                    : "?? " + requiredPaperSize + " ??????????",
                matches.Select(x => new CDBoxStudioFrameChoiceItem
                {
                    Value = x.Id,
                    Title = x.TemplateName,
                    Detail = x.PaperSize + " ? ???? "
                        + x.ValidWidth.ToString("0.##") + " ? "
                        + x.ValidHeight.ToString("0.##"),
                    Badge = x.IsDefault ? "??" : x.PaperSize
                }).ToList(), preferred.Id, out selectedId,
                new AcadMainWindow())) return null;
            return matches.FirstOrDefault(x => string.Equals(x.Id, selectedId,
                StringComparison.OrdinalIgnoreCase));
        }

        private static string ChoosePaperSize(string defaultValue)
        {
            string fallback = FramePaperSizes.NormalizeName(defaultValue);
            if (string.Equals(fallback, "???",
                StringComparison.OrdinalIgnoreCase)) fallback = "A3";
            string selected;
            return CDBoxStudioFrameChoiceWindow.TryChoose(
                "????", "?????????????",
                FramePaperSizes.GetAll().Select(x =>
                    new CDBoxStudioFrameChoiceItem
                    {
                        Value = x.Name,
                        Title = x.Name,
                        Detail = x.Width.ToString("0") + " ? "
                            + x.Height.ToString("0"),
                        Badge = "mm"
                    }).ToList(), fallback, out selected, new AcadMainWindow())
                ? selected : null;
        }

        private static string BuildDefaultTemplateName(string paperSize)
        {
            string baseName = (paperSize ?? "A3") + " ??";
            List<FrameTemplateCatalogItem> existing =
                FrameTemplateCatalogStore.Load().Templates
                    .Where(x => string.Equals(x.PaperSize, paperSize,
                        StringComparison.OrdinalIgnoreCase)).ToList();
            if (!existing.Any(x => string.Equals(x.TemplateName, baseName,
                StringComparison.OrdinalIgnoreCase))) return baseName;
            int suffix = 2;
            while (existing.Any(x => string.Equals(x.TemplateName,
                baseName + " " + suffix, StringComparison.OrdinalIgnoreCase)))
                suffix++;
            return baseName + " " + suffix;
        }

        private static int PaperOrder(string paper)
        {
            if (string.IsNullOrWhiteSpace(paper)) return 999;
            if (paper.Length == 2 && char.ToUpperInvariant(paper[0]) == 'A'
                && char.IsDigit(paper[1]))
                return paper[1] - '0';
            return 100;
        }

        private static void WriteLayoutWarnings(Editor editor,
            FrameLayoutService.LayoutResult result)
        {
            if (editor == null || result == null || result.Warnings == null
                || result.Warnings.Count == 0) return;
            int limit = Math.Min(5, result.Warnings.Count);
            for (int i = 0; i < limit; i++)
                editor.WriteHudMessage("\n  ???{0}", result.Warnings[i]);
            if (result.Warnings.Count > limit)
                editor.WriteHudMessage("\n  ?? {0} ????????",
                    result.Warnings.Count - limit);
        }

        private ObjectId PromptFrameOrImportFromFile(Document doc)
        {
            Editor editor = doc.Editor;
            string mode;
            if (!CDBoxStudioFrameChoiceWindow.TryChoose(
                "??????", "??????????????????????",
                new List<CDBoxStudioFrameChoiceItem>
                {
                    new CDBoxStudioFrameChoiceItem
                    {
                        Value = "Selection",
                        Title = "???????",
                        Detail = "??????????????????????",
                        Badge = "??"
                    },
                    new CDBoxStudioFrameChoiceItem
                    {
                        Value = "File",
                        Title = "???????",
                        Detail = "???? DWG ???????????"
                    }
                }, "Selection", out mode, new AcadMainWindow()))
                return ObjectId.Null;

            if (string.Equals(mode, "Selection",
                StringComparison.OrdinalIgnoreCase))
            {
                PromptSelectionOptions selectionOptions =
                    new PromptSelectionOptions
                    {
                        MessageForAdding =
                            "\n??????????????????????"
                    };
                PromptSelectionResult selection =
                    editor.GetHudSelection(selectionOptions);
                if (selection.Status != PromptStatus.OK
                    || selection.Value.Count == 0) return ObjectId.Null;
                if (selection.Value.Count == 1)
                {
                    ObjectId id = selection.Value[0].ObjectId;
                    using (Transaction transaction = doc.Database
                        .TransactionManager.StartOpenCloseTransaction())
                    {
                        bool isBlock = transaction.GetObject(id, OpenMode.ForRead,
                            false) is BlockReference;
                        transaction.Commit();
                        if (isBlock) return id;
                    }
                }
                return CreateFrameBlockFromSelection(doc, selection.Value);
            }

            PromptOpenFileOptions fileOptions = new PromptOpenFileOptions(
                "\n?????? DWG?")
            {
                Filter = "DWG ?? (*.dwg)|*.dwg|???? (*.*)|*.*"
            };
            PromptFileNameResult file = editor.GetFileNameForOpen(fileOptions);
            if (file.Status != PromptStatus.OK
                || string.IsNullOrWhiteSpace(file.StringResult)
                || !File.Exists(file.StringResult)) return ObjectId.Null;

            PromptPointResult point = editor.GetHudPoint(
                "\n??????????????????");
            if (point.Status != PromptStatus.OK) return ObjectId.Null;
            string blockName;
            using (DocumentLock documentLock = doc.LockDocument())
            {
                return new FrameTemplateService().ImportTemplateDwgAsBlock(doc,
                    file.StringResult, point.Value, out blockName);
            }
        }

        private static ObjectId CreateFrameBlockFromSelection(Document doc,
            SelectionSet selection)
        {
            Database db = doc.Database;
            using (DocumentLock documentLock = doc.LockDocument())
            using (Transaction transaction =
                db.TransactionManager.StartTransaction())
            {
                ObjectIdCollection ids = new ObjectIdCollection();
                foreach (SelectedObject selected in selection)
                {
                    if (selected == null || selected.ObjectId.IsNull) continue;
                    if (transaction.GetObject(selected.ObjectId,
                        OpenMode.ForRead, false) is Entity)
                        ids.Add(selected.ObjectId);
                }
                if (ids.Count == 0) return ObjectId.Null;

                BlockTable table = (BlockTable)transaction.GetObject(
                    db.BlockTableId, OpenMode.ForWrite);
                BlockTableRecord record = new BlockTableRecord
                {
                    Name = CadDbHelper.MakeUniqueBlockName(db, transaction,
                        "CDBOX_FRAME_SOURCE"),
                    Origin = Point3d.Origin
                };
                ObjectId recordId = table.Add(record);
                transaction.AddNewlyCreatedDBObject(record, true);
                db.DeepCloneObjects(ids, recordId, new IdMapping(), false);
                BlockReference reference = new BlockReference(Point3d.Origin,
                    recordId);
                ObjectId referenceId = CadDbHelper.AppendToModelSpace(db,
                    transaction, reference);
                transaction.Commit();
                return referenceId;
            }
        }
    }
}
