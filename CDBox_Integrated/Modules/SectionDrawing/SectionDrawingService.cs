using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using TCPipeAutoDraw.Core.Cad;

namespace TCPipeAutoDraw.Modules.SectionDrawing
{
    public static class SectionDrawingService
    {
        private const double DimensionTextHeightFactor = 1.0;

        public static SectionDrawingResult SelectPositionAndDraw(Document doc, SectionDrawingOptions options)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (options == null) throw new ArgumentNullException("options");

            SectionLayoutCalculator.Normalize(options);
            SectionDrawingOptions drawingOptions = SectionDrawingScaleService.CreateScaledOptions(options);
            if (!HasDrawableLayer(drawingOptions))
            {
                return new SectionDrawingResult
                {
                    Success = false,
                    Message = "至少需要勾选一层进行绘制。"
                };
            }
            Editor ed = doc.Editor;

            ObjectId previewTextStyleId = ResolveTextStyleId(doc, drawingOptions.TextStyleName);
            var jig = new SectionPlacementJig(drawingOptions, previewTextStyleId);
            PromptResult prompt = ed.Drag(jig);
            if (prompt.Status != PromptStatus.OK)
            {
                return new SectionDrawingResult
                {
                    Success = false,
                    Message = "已取消绘制。"
                };
            }

            return DrawScaled(doc, drawingOptions, jig.Position, null);
        }

        private static bool HasDrawableLayer(SectionDrawingOptions options)
        {
            if (options == null || options.Layers == null) return false;
            for (int i = 0; i < options.Layers.Count; i++)
            {
                if (options.Layers[i] != null && options.Layers[i].DrawLayer && options.Layers[i].Height > 0) return true;
            }
            return false;
        }

        public static SectionDrawingResult Draw(Document doc, SectionDrawingOptions options, Point3d insertPoint)
        {
            return Draw(doc, options, insertPoint, null);
        }

        public static SectionDrawingResult Draw(Document doc, SectionDrawingOptions options, Point3d insertPoint, IEnumerable<ObjectId> sourceIds)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (options == null) throw new ArgumentNullException("options");
            SectionLayoutCalculator.Normalize(options);
            return DrawScaled(doc, SectionDrawingScaleService.CreateScaledOptions(options), insertPoint, sourceIds);
        }

        private static SectionDrawingResult DrawScaled(Document doc, SectionDrawingOptions options, Point3d insertPoint, IEnumerable<ObjectId> sourceIds)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (options == null) throw new ArgumentNullException("options");

            SectionLayoutCalculator.Normalize(options);
            SectionLayout layout = SectionLayoutCalculator.Calculate(options);
            if (layout.TotalHeight <= 0.0 || layout.Layers.Count == 0)
            {
                return new SectionDrawingResult
                {
                    Success = false,
                    Message = "至少需要勾选一层进行绘制。"
                };
            }

            var result = new SectionDrawingResult { Success = true };
            var createdIds = new List<ObjectId>();
            Database db = doc.Database;

            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                CadLayerService.EnsureLayer(db, tr, options.BorderLayerName, 7);
                CadLayerService.EnsureLayer(db, tr, options.TextLayerName, 7);
                CadLayerService.EnsureLayer(db, tr, options.HatchLayerName, 8);
                CadLayerService.EnsureLayer(db, tr, options.DimensionLayerName, 7);

                var bodyRectIds = new Dictionary<int, ObjectId>();
                foreach (SectionLayerLayout layerLayout in layout.Layers)
                {
                    ObjectId bodyRectId = DrawRectangle(db, tr, layerLayout.BodyRect, insertPoint, options.BorderLayerName, 7);
                    TrackCreated(bodyRectId, createdIds, result);
                    bodyRectIds[layerLayout.SourceIndex] = bodyRectId;

                    ObjectId labelRectId = DrawRectangle(db, tr, layerLayout.LabelRect, insertPoint, options.BorderLayerName, 7);
                    TrackCreated(labelRectId, createdIds, result);

                    if (!string.IsNullOrWhiteSpace(layerLayout.Layer.LeftLabel))
                    {
                        ObjectId textId = DrawSingleLineTextInRect(db, tr, layerLayout.LabelRect, insertPoint, layerLayout.Layer.LeftLabel, GetLeftLabelTextHeight(options.TextHeight), options.TextLayerName, 7, options.TextStyleName);
                        TrackCreated(textId, createdIds, result);
                    }
                }

                var pipeBoundaryIdsByLayer = new Dictionary<int, List<ObjectId>>();
                foreach (SectionPipeLayout pipeLayout in layout.Pipes)
                {
                    Point3d center = new Point3d(insertPoint.X + pipeLayout.Center.X, insertPoint.Y + pipeLayout.Center.Y, insertPoint.Z);
                    ObjectId circleId = CadDrawService.DrawCircle(db, tr, center, pipeLayout.Radius, options.BorderLayerName, 7);
                    if (!circleId.IsNull)
                    {
                        TrackCreated(circleId, createdIds, result);
                        List<ObjectId> ids;
                        if (!pipeBoundaryIdsByLayer.TryGetValue(pipeLayout.HostLayerIndex, out ids))
                        {
                            ids = new List<ObjectId>();
                            pipeBoundaryIdsByLayer[pipeLayout.HostLayerIndex] = ids;
                        }
                        ids.Add(circleId);
                    }

                    string pipeText = pipeLayout.Pipe == null ? string.Empty : pipeLayout.Pipe.PipeText;
                    if (!string.IsNullOrWhiteSpace(pipeText))
                    {
                        double pipeTextHeight = GetAdaptivePipeTextHeight(pipeText, pipeLayout.Radius);
                        ObjectId textId = DrawSingleLineCenteredText(db, tr, center, pipeText.Trim(), pipeTextHeight, options.TextLayerName, 7, options.TextStyleName);
                        TrackCreated(textId, createdIds, result);
                    }
                }

                foreach (SectionLayerLayout layerLayout in layout.Layers)
                {
                    ObjectId bodyRectId;
                    if (!bodyRectIds.TryGetValue(layerLayout.SourceIndex, out bodyRectId)) bodyRectId = ObjectId.Null;
                    List<ObjectId> innerLoops;
                    pipeBoundaryIdsByLayer.TryGetValue(layerLayout.SourceIndex, out innerLoops);
                    ObjectId hatchId = DrawHatch(db, tr, bodyRectId, innerLoops, layerLayout.Layer.HatchPatternName, layerLayout.Layer.HatchScale, layerLayout.Layer.HatchAngle, options.HatchLayerName);
                    if (!hatchId.IsNull) TrackCreated(hatchId, createdIds, result);
                    else if (!IsEmptyHatch(layerLayout.Layer.HatchPatternName)) result.HatchFailureCount++;
                }

                if (options.DrawTopDimension)
                {
                    ObjectId dimId = DrawRotatedDimension(db, tr,
                        new Point3d(insertPoint.X, insertPoint.Y + layout.TotalHeight, insertPoint.Z),
                        new Point3d(insertPoint.X + options.Width, insertPoint.Y + layout.TotalHeight, insertPoint.Z),
                        new Point3d(insertPoint.X + options.Width / 2.0, insertPoint.Y + layout.TotalHeight + options.TopDimensionOffset, insertPoint.Z),
                        0.0,
                        options.DimensionLayerName,
                        options.DimensionStyleName,
                        options.TextHeight * DimensionTextHeightFactor,
                        options.DrawingScale);
                    TrackCreated(dimId, createdIds, result);
                }

                if (options.DrawBottomDimension)
                {
                    ObjectId dimId = DrawRotatedDimension(db, tr,
                        new Point3d(insertPoint.X, insertPoint.Y, insertPoint.Z),
                        new Point3d(insertPoint.X + options.Width, insertPoint.Y, insertPoint.Z),
                        new Point3d(insertPoint.X + options.Width / 2.0, insertPoint.Y - options.BottomDimensionOffset, insertPoint.Z),
                        0.0,
                        options.DimensionLayerName,
                        options.DimensionStyleName,
                        options.TextHeight * DimensionTextHeightFactor,
                        options.DrawingScale);
                    TrackCreated(dimId, createdIds, result);
                }

                if (options.DrawRightDimensions)
                {
                    foreach (SectionLayerLayout layerLayout in layout.Layers)
                    {
                        ObjectId dimId = DrawRotatedDimension(db, tr,
                            new Point3d(insertPoint.X + options.Width, insertPoint.Y + layerLayout.Bottom, insertPoint.Z),
                            new Point3d(insertPoint.X + options.Width, insertPoint.Y + layerLayout.Top, insertPoint.Z),
                            new Point3d(insertPoint.X + options.Width + options.RightDimensionOffset, insertPoint.Y + (layerLayout.Bottom + layerLayout.Top) / 2.0, insertPoint.Z),
                            Math.PI / 2.0,
                            options.DimensionLayerName,
                            options.DimensionStyleName,
                            options.TextHeight * DimensionTextHeightFactor,
                            options.DrawingScale);
                        TrackCreated(dimId, createdIds, result);
                    }
                }

                if (options.DrawTotalHeightDimension && layout.TotalHeight > 0)
                {
                    double totalOffset = SectionLayoutCalculator.GetTotalHeightDimensionOffset(options);
                    ObjectId dimId = DrawRotatedDimension(db, tr,
                        new Point3d(insertPoint.X + options.Width, insertPoint.Y, insertPoint.Z),
                        new Point3d(insertPoint.X + options.Width, insertPoint.Y + layout.TotalHeight, insertPoint.Z),
                        new Point3d(insertPoint.X + options.Width + totalOffset, insertPoint.Y + layout.TotalHeight / 2.0, insertPoint.Z),
                        Math.PI / 2.0,
                        options.DimensionLayerName,
                        options.DimensionStyleName,
                        options.TextHeight * DimensionTextHeightFactor,
                        options.DrawingScale);
                    TrackCreated(dimId, createdIds, result);
                }

                if (options.DrawTitle && !string.IsNullOrWhiteSpace(options.SectionTitle))
                {
                    double baseOffset = options.DrawBottomDimension ? options.BottomDimensionOffset : 0.0;
                    Point3d titleBasePoint = new Point3d(insertPoint.X + options.Width / 2.0, insertPoint.Y - baseOffset - options.TitleOffset, insertPoint.Z);
                    DrawTitleLines(db, tr, titleBasePoint, options, createdIds, result);
                }

                SectionDrawingBindingService.Bind(tr, sourceIds, createdIds);
                tr.Commit();
            }

            result.Message = result.HatchFailureCount > 0
                ? "断面图已生成，部分填充失败。"
                : "断面图已生成。";
            return result;
        }

        private static void TrackCreated(ObjectId id, IList<ObjectId> createdIds, SectionDrawingResult result)
        {
            if (id.IsNull) return;
            if (createdIds != null) createdIds.Add(id);
            if (result != null) result.EntityCount++;
        }

        private static ObjectId DrawRectangle(Database db, Transaction tr, Rect2d rect, Point3d origin, string layerName, short colorIndex)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return ObjectId.Null;
            CadLayerService.EnsureLayer(db, tr, layerName, colorIndex);
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            var pl = new Autodesk.AutoCAD.DatabaseServices.Polyline();
            pl.AddVertexAt(0, new Point2d(origin.X + rect.Left, origin.Y + rect.Bottom), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(origin.X + rect.Right, origin.Y + rect.Bottom), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(origin.X + rect.Right, origin.Y + rect.Top), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(origin.X + rect.Left, origin.Y + rect.Top), 0, 0, 0);
            pl.Closed = true;
            pl.Elevation = origin.Z;
            pl.Layer = layerName;

            ObjectId id = btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            return id;
        }

        private static ObjectId DrawHatch(Database db, Transaction tr, ObjectId boundaryId, List<ObjectId> innerBoundaryIds, string patternName, double scale, double angleDegrees, string layerName)
        {
            if (boundaryId.IsNull || IsEmptyHatch(patternName)) return ObjectId.Null;

            string cleanPatternName = patternName == null ? string.Empty : patternName.Trim();
            if (string.IsNullOrWhiteSpace(cleanPatternName)) return ObjectId.Null;

            double safeScale = scale <= 0 ? 1.0 : scale;
            double safeAngleRadians = angleDegrees * Math.PI / 180.0;

            // 先按 AutoCAD 原生预定义图案生成，这是 ANSI、AR-XXX、常用 CAD 图案最稳定的方式。
            ObjectId hatchId = TryDrawHatchCore(db, tr, boundaryId, innerBoundaryIds, cleanPatternName, safeScale, safeAngleRadians, layerName, HatchPatternType.PreDefined);
            if (!hatchId.IsNull) return hatchId;

            // 少数从 acadiso.pat 或自定义 pat 中读取到的图案，在部分环境下需要按 CustomDefined 再尝试一次。
            hatchId = TryDrawHatchCore(db, tr, boundaryId, innerBoundaryIds, cleanPatternName, safeScale, safeAngleRadians, layerName, HatchPatternType.CustomDefined);
            if (!hatchId.IsNull) return hatchId;

            return ObjectId.Null;
        }

        private static ObjectId TryDrawHatchCore(Database db, Transaction tr, ObjectId boundaryId, List<ObjectId> innerBoundaryIds, string patternName, double scale, double angleRadians, string layerName, HatchPatternType patternType)
        {
            ObjectId hatchId = ObjectId.Null;
            Hatch hatch = null;
            try
            {
                CadLayerService.EnsureLayer(db, tr, layerName, 8);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                hatch = new Hatch();
                hatch.SetDatabaseDefaults();
                hatch.Layer = layerName;
                hatch.HatchStyle = HatchStyle.Normal;

                // 注意：这里必须保持 Associative=true，并且使用正式图形边界。
                // 上一版改为非关联并使用临时边界后，在部分 AutoCAD/CASS 环境会导致所有 Hatch 都无法生成。
                hatchId = btr.AppendEntity(hatch);
                tr.AddNewlyCreatedDBObject(hatch, true);
                hatch.Associative = true;

                hatch.SetHatchPattern(patternType, patternName);
                hatch.PatternScale = scale <= 0 ? 1.0 : scale;
                hatch.PatternAngle = angleRadians;

                var outer = new ObjectIdCollection();
                outer.Add(boundaryId);
                hatch.AppendLoop(HatchLoopTypes.External, outer);

                if (innerBoundaryIds != null)
                {
                    foreach (ObjectId id in innerBoundaryIds)
                    {
                        if (id.IsNull) continue;
                        var inner = new ObjectIdCollection();
                        inner.Add(id);
                        hatch.AppendLoop(HatchLoopTypes.Default, inner);
                    }
                }

                hatch.EvaluateHatch(true);
                return hatchId;
            }
            catch
            {
                try
                {
                    if (!hatchId.IsNull)
                    {
                        Entity ent = tr.GetObject(hatchId, OpenMode.ForWrite, false) as Entity;
                        if (ent != null && !ent.IsErased) ent.Erase();
                    }
                    else if (hatch != null && !hatch.IsErased)
                    {
                        hatch.Erase();
                    }
                }
                catch { }
                return ObjectId.Null;
            }
        }

        private static bool IsEmptyHatch(string patternName)
        {
            if (string.IsNullOrWhiteSpace(patternName)) return true;
            string name = patternName.Trim();
            return string.Equals(name, "无", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(name, "无填充", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(name, "NONE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "NO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "OFF", StringComparison.OrdinalIgnoreCase);
        }

        private static double GetLeftLabelTextHeight(double baseTextHeight)
        {
            return Math.Max(0.01, (baseTextHeight <= 0 ? 0.08 : baseTextHeight) * 1.4375);
        }

        private static void DrawTitleLines(Database db, Transaction tr, Point3d topCenterPoint, SectionDrawingOptions options, IList<ObjectId> createdIds, SectionDrawingResult result)
        {
            if (options == null || string.IsNullOrWhiteSpace(options.SectionTitle)) return;
            string[] lines = options.SectionTitle.Replace("\r\n", "\n").Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.None);
            double height = options.TextHeight * 1.25;
            double lineGap = Math.Max(height * 1.35, 0.04);
            int visibleIndex = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                Point3d p = new Point3d(topCenterPoint.X, topCenterPoint.Y - visibleIndex * lineGap, topCenterPoint.Z);
                ObjectId id = DrawCenteredDbText(db, tr, p, line.Trim(), height, options.TextLayerName, 7, options.TextStyleName);
                TrackCreated(id, createdIds, result);
                visibleIndex++;
            }
        }

        private static ObjectId DrawSingleLineTextInRect(Database db, Transaction tr, Rect2d rect, Point3d origin, string text, double preferredHeight, string layerName, short colorIndex, string textStyleName)
        {
            if (string.IsNullOrWhiteSpace(text)) return ObjectId.Null;

            string cleanText = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (cleanText.Length == 0) return ObjectId.Null;

            double height = preferredHeight <= 0 ? 0.08 : preferredHeight;
            if (rect.Height > 0) height = Math.Min(height, Math.Max(0.01, rect.Height * 0.58));

            // 单行 DBText 不能像 MText 一样自动换行/裁切，因此这里按更保守的
            // 文字宽度估算主动缩小高度，保证“C25路面恢复”等长注记不会碰框。
            double availableWidth = Math.Max(0.01, rect.Width - Math.Max(0.035, height * 0.55));
            double widthLimitedHeight = availableWidth / Math.Max(0.1, GetLabelWeightedTextLength(cleanText) * 1.02);
            height = Math.Min(height, widthLimitedHeight);
            height = Math.Max(0.01, height);

            return DrawCenteredDbText(db, tr, rect.Center(origin), cleanText, height, layerName, colorIndex, textStyleName);
        }

        private static ObjectId DrawCenteredDbText(Database db, Transaction tr, Point3d position, string text, double height, string layerName, short colorIndex, string textStyleName)
        {
            if (string.IsNullOrWhiteSpace(text)) return ObjectId.Null;
            try
            {
                CadLayerService.EnsureLayer(db, tr, layerName, colorIndex);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                ObjectId textStyleId = GetTextStyleId(db, tr, textStyleName);

                var dbText = new DBText();
                dbText.SetDatabaseDefaults();
                dbText.TextString = text;
                dbText.Height = height <= 0 ? 0.08 : height;
                dbText.Layer = layerName;
                if (!textStyleId.IsNull) dbText.TextStyleId = textStyleId;
                dbText.HorizontalMode = TextHorizontalMode.TextCenter;
                dbText.VerticalMode = TextVerticalMode.TextVerticalMid;
                dbText.Position = position;
                dbText.AlignmentPoint = position;

                ObjectId id = btr.AppendEntity(dbText);
                tr.AddNewlyCreatedDBObject(dbText, true);
                try { dbText.AdjustAlignment(db); } catch { }
                return id;
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        private static ObjectId GetTextStyleId(Database db, Transaction tr, string textStyleName)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(textStyleName)) return ObjectId.Null;
            try
            {
                TextStyleTable tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                if (tst.Has(textStyleName)) return tst[textStyleName];
                foreach (ObjectId id in tst)
                {
                    TextStyleTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as TextStyleTableRecord;
                    if (record == null || record.IsErased) continue;
                    if (string.Equals(record.Name, textStyleName, StringComparison.CurrentCultureIgnoreCase)) return id;
                }
            }
            catch { }
            return ObjectId.Null;
        }

        private static double GetAdaptivePipeTextHeight(string text, double radius)
        {
            if (string.IsNullOrWhiteSpace(text) || radius <= 0.0) return 0.01;

            double diameter = radius * 2.0;
            double maxTextWidth = diameter * 0.78;
            double maxTextHeight = diameter * 0.38;
            // 管径注记恢复为之前较保守的估算逻辑，避免 DN200/DN300 等文字撑出管圆。
            double weightedLength = GetPipeWeightedTextLength(text.Trim());
            double heightByWidth = maxTextWidth / Math.Max(0.1, weightedLength * 0.62);
            double height = Math.Min(maxTextHeight, heightByWidth);
            return Math.Max(0.01, height);
        }

        private static double GetLabelWeightedTextLength(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1.0;
            double length = 0.0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) length += 0.35;
                else if (c < 128) length += 0.72;
                else length += 1.22;
            }
            return Math.Max(1.0, length);
        }

        private static double GetPipeWeightedTextLength(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1.0;
            double length = 0.0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) length += 0.35;
                else if (c < 128) length += 1.0;
                else length += 1.8;
            }
            return Math.Max(1.0, length);
        }

        private static double GetWeightedTextLength(string text)
        {
            return GetLabelWeightedTextLength(text);
        }

        private static ObjectId DrawSingleLineCenteredText(Database db, Transaction tr, Point3d position, string text, double height, string layerName, short colorIndex, string textStyleName)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(text)) return ObjectId.Null;

            CadLayerService.EnsureLayer(db, tr, layerName, colorIndex);
            ObjectId textStyleId = GetExistingTextStyle(db, tr, textStyleName);
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            var dbText = new DBText();
            dbText.TextString = text.Replace("\r", " ").Replace("\n", " ");
            dbText.Height = height <= 0.0 ? 0.01 : height;
            dbText.Layer = layerName;
            dbText.HorizontalMode = TextHorizontalMode.TextCenter;
            dbText.VerticalMode = TextVerticalMode.TextVerticalMid;
            dbText.Position = position;
            dbText.AlignmentPoint = position;
            if (!textStyleId.IsNull) dbText.TextStyleId = textStyleId;

            ObjectId id = btr.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);
            try { dbText.AdjustAlignment(db); } catch { }
            return id;
        }

        private static ObjectId GetExistingTextStyle(Database db, Transaction tr, string textStyleName)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(textStyleName)) return ObjectId.Null;
            textStyleName = textStyleName.Trim();

            try
            {
                TextStyleTable tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                if (tst.Has(textStyleName)) return tst[textStyleName];

                foreach (ObjectId id in tst)
                {
                    TextStyleTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as TextStyleTableRecord;
                    if (record == null || record.IsErased) continue;
                    if (string.Equals(record.Name, textStyleName, StringComparison.CurrentCultureIgnoreCase)) return id;
                }
            }
            catch
            {
            }

            return ObjectId.Null;
        }


        private static ObjectId ResolveTextStyleId(Document doc, string textStyleName)
        {
            if (doc == null || doc.Database == null) return ObjectId.Null;

            try
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    ObjectId id = GetTextStyleId(db, tr, textStyleName);
                    tr.Commit();
                    return id;
                }
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        private static ObjectId DrawRotatedDimension(Database db, Transaction tr, Point3d p1, Point3d p2, Point3d dimLinePoint, double rotation, string layerName, string dimStyleName, double dimTextHeight, double drawingScale)
        {
            try
            {
                CadLayerService.EnsureLayer(db, tr, layerName, 7);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                ObjectId dimStyleId = GetDimensionStyleId(db, tr, dimStyleName);
                if (dimStyleId.IsNull) dimStyleId = db.Dimstyle;

                var dim = new RotatedDimension(rotation, p1, p2, dimLinePoint, string.Empty, dimStyleId);
                dim.SetDatabaseDefaults();
                dim.Layer = layerName;
                dim.DimensionStyle = dimStyleId;
                if (dimTextHeight > 0.0) dim.Dimtxt = dimTextHeight;
                double safeScale = drawingScale <= 0 ? 1.0 : drawingScale;
                DimStyleTableRecord style = tr.GetObject(dimStyleId, OpenMode.ForRead, false) as DimStyleTableRecord;
                double baseMeasurementFactor = style == null || Math.Abs(style.Dimlfac) <= 0.000000001 ? 1.0 : style.Dimlfac;
                dim.Dimlfac = baseMeasurementFactor / safeScale;
                if (style != null && Math.Abs(safeScale - 1.0) > 0.000000001)
                {
                    dim.Dimasz = style.Dimasz * safeScale;
                    dim.Dimexe = style.Dimexe * safeScale;
                    dim.Dimexo = style.Dimexo * safeScale;
                    dim.Dimgap = style.Dimgap * safeScale;
                }

                ObjectId id = btr.AppendEntity(dim);
                tr.AddNewlyCreatedDBObject(dim, true);
                return id;
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        private static ObjectId GetDimensionStyleId(Database db, Transaction tr, string dimStyleName)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(dimStyleName)) return ObjectId.Null;
            try
            {
                DimStyleTable dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                if (dst.Has(dimStyleName)) return dst[dimStyleName];
                foreach (ObjectId id in dst)
                {
                    DimStyleTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as DimStyleTableRecord;
                    if (record == null || record.IsErased) continue;
                    if (string.Equals(record.Name, dimStyleName, StringComparison.CurrentCultureIgnoreCase)) return id;
                }
            }
            catch { }
            return ObjectId.Null;
        }
    }
}
