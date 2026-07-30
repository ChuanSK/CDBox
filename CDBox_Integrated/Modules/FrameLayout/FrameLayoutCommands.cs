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

                PromptPointResult first = editor.GetPoint(
                    "\n选择图框内裁图区域的第一个角点：");
                if (first.Status != PromptStatus.OK) return;
                PromptCornerOptions cornerOptions = new PromptCornerOptions(
                    "\n选择裁图区域的对角点：", first.Value);
                PromptPointResult second = editor.GetCorner(cornerOptions);
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
                        throw new InvalidOperationException("目前仅支持将块参照作为图框模板。");

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
                editor.WriteMessage(
                    "\n[图框模板] 已添加 {0} / {1}，裁图区域 {2:0.###} × {3:0.###}。",
                    item.PaperSize, item.TemplateName, item.ValidWidth,
                    item.ValidHeight);
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\n[图框模板] 添加失败：{0}", ex.Message);
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
                    "选择裁图形状", "选择后进入绘图区布置裁图区域。",
                    new List<CDBoxStudioFrameChoiceItem>
                    {
                        new CDBoxStudioFrameChoiceItem
                        {
                            Value = "Rectangle",
                            Title = "矩形裁图区域",
                            Detail = "按所选图框的可用区域尺寸动态布置红色矩形。",
                            Badge = "默认"
                        },
                        new CDBoxStudioFrameChoiceItem
                        {
                            Value = "Curve",
                            Title = "已有闭合曲线",
                            Detail = "选择一个或多个小于图幅可用区域的闭合曲线。"
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
                editor.WriteMessage("\n[裁图区域] 布置失败：{0}", ex.Message);
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
                    editor.WriteMessage("\n[图框布置] 选择中没有有效裁图区域。");
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

                PromptPointResult point = editor.GetPoint(
                    "\n选择第一个图框的左下角位置：");
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
                editor.WriteMessage(
                    "\n[图框布置] 已生成 {0} 个图框，共 {1} 行，每行最多 {2} 个。",
                    result.Count, result.Rows,
                    FrameLayoutSettingsStore.Load().FramesPerRow);
                WriteLayoutWarnings(editor, result);
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\n[图框布置] 失败：{0}", ex.Message);
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
                    "\n输入图框数量 <1>: ")
                {
                    AllowNegative = false,
                    AllowZero = false,
                    DefaultValue = 1,
                    UseDefaultValue = true,
                    LowerLimit = 1,
                    UpperLimit = 10000
                };
                PromptIntegerResult count = editor.GetInteger(countOptions);
                if (count.Status != PromptStatus.OK) return;
                PromptPointResult point = editor.GetPoint(
                    "\n选择第一个图框的左下角位置：");
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
                editor.WriteMessage("\n[直接布框] 已生成 {0} 个 {1} 图框。",
                    result.Count, template.PaperSize);
                WriteLayoutWarnings(editor, result);
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\n[直接布框] 失败：{0}", ex.Message);
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
                if (count > 0)
                    editor.WriteMessage(
                        "\n继续移动光标布置下一裁图矩形，按 Enter 或 Esc 完成。");
                FrameCutRegionPlacementJig centerJig =
                    FrameCutRegionPlacementJig.ForCenter(
                        template.ValidWidth, template.ValidHeight);
                PromptResult positionResult = editor.Drag(centerJig);
                if (positionResult.Status != PromptStatus.OK) break;

                FrameCutRegionPlacementJig rotationJig =
                    FrameCutRegionPlacementJig.ForRotation(centerJig.Center,
                        template.ValidWidth, template.ValidHeight);
                PromptResult rotationResult = editor.Drag(rotationJig);
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
                editor.WriteMessage("\n[裁图区域] 已布置 {0} 个 {1} 红色裁图框。",
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
                MessageForAdding = "\n选择一个或多个闭合曲线："
            };
            PromptSelectionResult selection = editor.GetSelection(options);
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
                            + " 不是曲线");
                        continue;
                    }
                    string message;
                    if (!service.ValidateBoundary(entity, rotation,
                        template.ValidWidth, template.ValidHeight, out message))
                    {
                        string detail = "对象 " + selected.ObjectId.Handle
                            + "：" + message;
                        if (message.IndexOf("超过",
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
            editor.WriteMessage("\n[裁图区域] 已设置 {0} 个闭合曲线。", count);
            if (oversized.Count > 0)
            {
                CDBoxStudioFrameNoticeWindow.ShowNotice(
                    "裁图区域尺寸超过图框", oversized,
                    new AcadMainWindow());
            }
            foreach (string message in rejected.Take(5))
                editor.WriteMessage("\n  跳过：{0}", message);
            if (rejected.Count > 5)
                editor.WriteMessage("\n  另有 {0} 个对象未设置。", rejected.Count - 5);
        }

        private static bool TryPromptDirection(Editor editor,
            out double rotation)
        {
            rotation = 0;
            PromptPointResult start = editor.GetPoint(
                "\n选择裁图方向起点：");
            if (start.Status != PromptStatus.OK) return false;
            PromptPointOptions endOptions = new PromptPointOptions(
                "\n选择裁图方向终点：")
            {
                BasePoint = start.Value,
                UseBasePoint = true
            };
            PromptPointResult end = editor.GetPoint(endOptions);
            if (end.Status != PromptStatus.OK) return false;
            Vector3d direction = end.Value - start.Value;
            if (direction.Length <= GeometryHelper.Eps)
            {
                editor.WriteMessage("\n[裁图区域] 裁图方向无效。");
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
                editor.WriteMessage(
                    "\n[图框模板] 尚未添加自定义图框，请先在图框设置中添加模板。");
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
                editor.WriteMessage("\n[图框模板] 没有 {0} 图框模板。",
                    string.IsNullOrWhiteSpace(requiredPaperSize)
                        ? "可用的" : requiredPaperSize);
                return null;
            }
            FrameTemplateCatalogItem preferred = matches.FirstOrDefault(x =>
                string.Equals(x.Id, preferredTemplateId,
                    StringComparison.OrdinalIgnoreCase))
                ?? matches.FirstOrDefault(x => x.IsDefault)
                ?? matches[0];
            string selectedId;
            if (!CDBoxStudioFrameChoiceWindow.TryChoose(
                "选择图框模板",
                string.IsNullOrWhiteSpace(requiredPaperSize)
                    ? "选择本次操作使用的图幅和图框模板。"
                    : "选择 " + requiredPaperSize + " 图幅使用的图框模板。",
                matches.Select(x => new CDBoxStudioFrameChoiceItem
                {
                    Value = x.Id,
                    Title = x.TemplateName,
                    Detail = x.PaperSize + " · 可用区域 "
                        + x.ValidWidth.ToString("0.##") + " × "
                        + x.ValidHeight.ToString("0.##"),
                    Badge = x.IsDefault ? "默认" : x.PaperSize
                }).ToList(), preferred.Id, out selectedId,
                new AcadMainWindow())) return null;
            return matches.FirstOrDefault(x => string.Equals(x.Id, selectedId,
                StringComparison.OrdinalIgnoreCase));
        }

        private static string ChoosePaperSize(string defaultValue)
        {
            string fallback = FramePaperSizes.NormalizeName(defaultValue);
            if (string.Equals(fallback, "自定义",
                StringComparison.OrdinalIgnoreCase)) fallback = "A3";
            string selected;
            return CDBoxStudioFrameChoiceWindow.TryChoose(
                "选择图幅", "为新图框模板指定所属图幅。",
                FramePaperSizes.GetAll().Select(x =>
                    new CDBoxStudioFrameChoiceItem
                    {
                        Value = x.Name,
                        Title = x.Name,
                        Detail = x.Width.ToString("0") + " × "
                            + x.Height.ToString("0"),
                        Badge = "mm"
                    }).ToList(), fallback, out selected, new AcadMainWindow())
                ? selected : null;
        }

        private static string BuildDefaultTemplateName(string paperSize)
        {
            string baseName = (paperSize ?? "A3") + " 图框";
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
                editor.WriteMessage("\n  警告：{0}", result.Warnings[i]);
            if (result.Warnings.Count > limit)
                editor.WriteMessage("\n  另有 {0} 条附加标注警告。",
                    result.Warnings.Count - limit);
        }

        private ObjectId PromptFrameOrImportFromFile(Document doc)
        {
            Editor editor = doc.Editor;
            string mode;
            if (!CDBoxStudioFrameChoiceWindow.TryChoose(
                "添加图框模板", "选择模板来源，随后进入相应的对象或文件选择。",
                new List<CDBoxStudioFrameChoiceItem>
                {
                    new CDBoxStudioFrameChoiceItem
                    {
                        Value = "Selection",
                        Title = "从当前图纸选择",
                        Detail = "选择块参照，或将散线、文字自动组合为模板块。",
                        Badge = "默认"
                    },
                    new CDBoxStudioFrameChoiceItem
                    {
                        Value = "File",
                        Title = "从模板图纸导入",
                        Detail = "选择外部 DWG 文件作为图框模板来源。"
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
                            "\n选择组成图框的对象（单独选择块可直接使用）："
                    };
                PromptSelectionResult selection =
                    editor.GetSelection(selectionOptions);
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
                "\n选择图框模板 DWG：")
            {
                Filter = "DWG 文件 (*.dwg)|*.dwg|所有文件 (*.*)|*.*"
            };
            PromptFileNameResult file = editor.GetFileNameForOpen(fileOptions);
            if (file.Status != PromptStatus.OK
                || string.IsNullOrWhiteSpace(file.StringResult)
                || !File.Exists(file.StringResult)) return ObjectId.Null;

            PromptPointResult point = editor.GetPoint(
                "\n选择模板图框可见范围的中心插入位置：");
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
