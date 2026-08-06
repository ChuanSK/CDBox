using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using TCPipeAutoDraw.Modules.LayerManager;
using TCPipeAutoDraw.Modules.AnnotationHud;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// ???????
    /// ????????????/???????? ExtensionDictionary + Xrecord ???
    /// </summary>
    public static class QuantityPipeAttributeService
    {
        public const string PipeAttributeXrecordName = "CDBoxQuantityPipeAttributes";
        private const string PipeAttributeIndexDictionaryName = "CDBoxQuantityAttributeIndex";

        public static QuantityPipeSelectionInfo SelectPipeAndRead(Document doc)
        {
            return SelectQuantityObjectAndRead(doc);
        }

        public static QuantityPipeSelectionInfo SelectQuantityObjectAndRead(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            Editor ed = doc.Editor;

            var opt = new PromptEntityOptions("\n????????????????????");
            PromptEntityResult res = ed.GetHudEntity(opt);
            if (res.Status != PromptStatus.OK) return null;

            return ReadPipe(doc, res.ObjectId);
        }

        public static QuantityPipeSelectionInfo ReadFirstImpliedOrPrompt(Document doc)
        {
            List<QuantityPipeSelectionInfo> infos = ReadImpliedOrPromptMany(doc);
            if (infos == null || infos.Count == 0) return null;
            return infos[0];
        }

        public static List<QuantityPipeSelectionInfo> ReadImpliedOrPromptMany(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");

            ObjectId[] ids = ReadImpliedOrPromptObjectIds(doc);
            List<QuantityPipeSelectionInfo> infos = new List<QuantityPipeSelectionInfo>();
            if (ids == null || ids.Length == 0) return infos;

            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i].IsNull) continue;
                QuantityPipeSelectionInfo info = ReadPipe(doc, ids[i]);
                if (info != null) infos.Add(info);
            }

            return infos;
        }

        public static ObjectId[] ReadImpliedOrPromptObjectIds(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            Editor ed = doc.Editor;

            try
            {
                PromptSelectionResult implied = ed.SelectImplied();
                if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
                {
                    ObjectId[] impliedIds = implied.Value.GetObjectIds();
                    ed.SetImpliedSelection(new ObjectId[0]);
                    return FilterNonNullIds(impliedIds);
                }
            }
            catch
            {
            }

            PromptSelectionOptions opt = new PromptSelectionOptions();
            opt.MessageForAdding = "\n??????????????????????";
            opt.MessageForRemoval = "\n?????";

            PromptSelectionResult res = ed.GetHudSelection(opt);
            if (res.Status != PromptStatus.OK || res.Value == null || res.Value.Count == 0) return new ObjectId[0];
            return FilterNonNullIds(res.Value.GetObjectIds());
        }

        private static ObjectId[] FilterNonNullIds(ObjectId[] ids)
        {
            if (ids == null || ids.Length == 0) return new ObjectId[0];
            List<ObjectId> result = new List<ObjectId>();
            HashSet<ObjectId> set = new HashSet<ObjectId>();
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i].IsNull) continue;
                if (set.Contains(ids[i])) continue;
                set.Add(ids[i]);
                result.Add(ids[i]);
            }
            return result.ToArray();
        }

        private static void ReportProgress(Action<int, int, string> progress, int current, int total, string message)
        {
            if (progress == null) return;
            progress(current, total, message);
        }


        private static List<ObjectId> NormalizeObjectIds(IEnumerable<ObjectId> objectIds)
        {
            List<ObjectId> result = new List<ObjectId>();
            if (objectIds == null) return result;

            HashSet<ObjectId> set = new HashSet<ObjectId>();
            foreach (ObjectId id in objectIds)
            {
                if (id.IsNull) continue;
                if (set.Contains(id)) continue;
                set.Add(id);
                result.Add(id);
            }
            return result;
        }

        public static QuantityPipeSelectionInfo ReadPipe(Document doc, ObjectId objectId)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (objectId.IsNull) return null;

            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity entity = tr.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                if (entity == null)
                {
                    tr.Commit();
                    return null;
                }

                bool hasSaved = HasPipeAttributes(entity, tr);
                bool hasSavedDrawLengthWidthHeightFlag = hasSaved && HasPipeAttributeKey(entity, tr, "DrawLengthWidthHeightAnnotation");
                QuantityPipeAttributes savedAttributes = hasSaved ? ReadPipeAttributes(entity, tr) : null;
                string inferredKind = savedAttributes != null
                    && (savedAttributes.IsSpecialObject || IsSupportedAttributeKind(savedAttributes.ObjectKind))
                    ? savedAttributes.ObjectKind
                    : InferSupportedObjectKind(db, tr, entity);
                if (string.IsNullOrWhiteSpace(inferredKind))
                {
                    tr.Commit();
                    return null;
                }

                QuantityPipeAttributes attrs = hasSaved ? savedAttributes : BuildDefaultAttributesFromEntity(db, tr, entity, inferredKind);
                if (!attrs.IsSpecialObject) attrs.ObjectKind = inferredKind;
                if (hasSaved && !hasSavedDrawLengthWidthHeightFlag)
                {
                    attrs.DrawLengthWidthHeightAnnotation = QuantityPipeAttributes.IsMainPipeKind(inferredKind);
                }
                ApplyLayerMetadata(db, tr, entity, attrs);

                Curve curve = entity as Curve;
                double cadLength = curve == null ? 0.0 : GetCurveLength(curve);

                if (curve != null && QuantityPipeAttributes.IsMainPipeKind(attrs.ObjectKind) && !attrs.IsSpecialObject)
                {
                    TryFillConnectedNodeInfo(db, tr, entity, curve, attrs);
                }

                var info = new QuantityPipeSelectionInfo
                {
                    ObjectId = objectId,
                    HandleText = entity.Handle.ToString(),
                    LayerName = entity.Layer ?? string.Empty,
                    ObjectTypeName = entity.GetType().Name,
                    CadLength = cadLength,
                    HasSavedAttributes = hasSaved,
                    InferredKind = inferredKind,
                    Attributes = attrs
                };
                tr.Commit();
                return info;
            }
        }


        public static List<ObjectId> FindObjectsWithSavedAttributes(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");

            List<ObjectId> ids = new List<ObjectId>();
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord space = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead, false) as BlockTableRecord;
                if (space != null)
                {
                    foreach (ObjectId id in space)
                    {
                        try
                        {
                            Entity entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                            if (entity == null) continue;
                            if (HasPipeAttributes(entity, tr)) ids.Add(id);
                        }
                        catch
                        {
                        }
                    }
                }
                tr.Commit();
            }
            return ids;
        }

        internal static bool TryReadSavedAttributes(Database db, Transaction tr,
            ObjectId objectId, out QuantityPipeAttributes attributes)
        {
            attributes = null;
            if (db == null || tr == null || objectId.IsNull) return false;
            try
            {
                Entity entity = tr.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                if (entity == null || !HasPipeAttributes(entity, tr)) return false;
                attributes = ReadPipeAttributes(entity, tr);
                return attributes != null;
            }
            catch { return false; }
        }

        internal static List<string> RepairClonedAttributeObjects(Database db,
            Transaction tr, IDictionary<ObjectId, ObjectId> cloneMap)
        {
            var changedNodeHandles = new List<string>();
            if (db == null || tr == null || cloneMap == null) return changedNodeHandles;
            foreach (KeyValuePair<ObjectId, ObjectId> pair in cloneMap)
            {
                if (pair.Value.IsNull) continue;
                Entity clone;
                try { clone = tr.GetObject(pair.Value, OpenMode.ForWrite, false) as Entity; }
                catch { continue; }
                if (clone == null) continue;

                QuantityPipeAttributes attributes = HasPipeAttributes(clone, tr)
                    ? ReadPipeAttributes(clone, tr)
                    : null;
                bool originalInDestination = false;
                try
                {
                    originalInDestination = !pair.Key.IsNull && pair.Key.Database == db;
                }
                catch { }
                if (attributes == null && originalInDestination)
                {
                    try
                    {
                        Entity original = tr.GetObject(pair.Key, OpenMode.ForRead, false) as Entity;
                        if (original != null && HasPipeAttributes(original, tr))
                            attributes = ReadPipeAttributes(original, tr);
                    }
                    catch { }
                }
                if (attributes == null) continue;

                try { WritePipeAttributes(clone, tr, attributes.Clone()); }
                catch { continue; }
                if (QuantityPipeAttributes.IsNodeKind(attributes.ObjectKind))
                    changedNodeHandles.Add(clone.Handle.ToString());
            }
            return changedNodeHandles;
        }

        public static QuantityPipeWriteResult WritePipeAttributes(Document doc, ObjectId objectId, QuantityPipeAttributes attributes)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (objectId.IsNull) return new QuantityPipeWriteResult { Success = false, Message = "??????" };
            attributes = attributes == null ? QuantityPipeAttributes.Default : attributes.Clone();

            Database db = doc.Database;
            string changedNodeHandle = string.Empty;
            string changedPipeHandle = string.Empty;
            var linkedPipeHandles = new List<string>();
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity entity = tr.GetObject(objectId, OpenMode.ForWrite, false) as Entity;
                if (entity == null)
                {
                    tr.Commit();
                    return new QuantityPipeWriteResult { Success = false, Message = "???????????" };
                }

                QuantityPipeAttributes previous = HasPipeAttributes(entity, tr) ? ReadPipeAttributes(entity, tr) : attributes.Clone();
                attributes = NormalizeAttributesForWrite(db, tr, entity, attributes, previous, "Save");
                WritePipeAttributes(entity, tr, attributes);

                // ??????????????????????????????????
                // ??????????????????????
                if (QuantityPipeAttributes.IsNodeKind(attributes.ObjectKind))
                {
                    linkedPipeHandles = RefreshMainPipeDepthsLinkedToNode(db, tr, objectId, attributes);
                    changedNodeHandle = entity.Handle.ToString();
                }
                else if (QuantityPipeAttributes.IsMainPipeKind(attributes.ObjectKind)
                    || QuantityPipeAttributes.IsBranchKind(attributes.ObjectKind))
                {
                    changedPipeHandle = entity.Handle.ToString();
                }

                tr.Commit();
            }

            RefreshBoundNodeAnnotations(doc, changedNodeHandle);
            if (!string.IsNullOrWhiteSpace(changedPipeHandle)) linkedPipeHandles.Add(changedPipeHandle);
            RefreshBoundPipeAnnotations(doc, linkedPipeHandles);

            return new QuantityPipeWriteResult { Success = true, SuccessCount = 1, Message = "??????????" };
        }

        private static void RefreshBoundNodeAnnotations(Document doc,
            IEnumerable<string> sourceHandles)
        {
            if (doc == null || sourceHandles == null) return;
            try { SimpleAnnotationObjectService.RefreshNodeAnnotationsForSourceHandles(doc, sourceHandles); }
            catch { }
        }

        private static void RefreshBoundNodeAnnotations(Document doc, string sourceHandle)
        {
            if (string.IsNullOrWhiteSpace(sourceHandle)) return;
            RefreshBoundNodeAnnotations(doc, new[] { sourceHandle });
        }

        private static void RefreshBoundPipeAnnotations(Document doc, IEnumerable<string> sourceHandles)
        {
            if (doc == null || sourceHandles == null) return;
            try
            {
                PipeLengthAnnotationObjectService.RefreshBindingsForSourceHandles(
                    doc, sourceHandles, string.Empty);
            }
            catch
            {
                // ??????????????????
            }
        }

        private static void RefreshBoundAnnotationsAfterBatch(Document doc,
            IEnumerable<string> sourceHandles)
        {
            if (doc == null || sourceHandles == null) return;
            var handles = new HashSet<string>(
                sourceHandles.Where(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);
            if (handles.Count == 0) return;

            // ?????????????????????????????
            // ???????????????????????????????
            // ???????????
            RefreshBoundNodeAnnotations(doc, handles);
            RefreshBoundPipeAnnotations(doc, handles);
        }

        private static List<string> RefreshMainPipeDepthsLinkedToNode(Database db, Transaction tr,
            ObjectId nodeObjectId, QuantityPipeAttributes nodeAttrs)
        {
            var changedHandles = new List<string>();
            if (db == null || tr == null || nodeAttrs == null) return changedHandles;
            if (string.IsNullOrWhiteSpace(nodeAttrs.NodeNo)) return changedHandles;

            string nodeNo = nodeAttrs.NodeNo.Trim();
            BlockTableRecord space = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead, false) as BlockTableRecord;
            if (space == null) return changedHandles;

            foreach (ObjectId id in space)
            {
                if (id.IsNull || id == nodeObjectId) continue;

                try
                {
                    Entity entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity == null) continue;
                    if (!HasPipeAttributes(entity, tr)) continue;

                    QuantityPipeAttributes pipeAttrs = ReadPipeAttributes(entity, tr);
                    if (pipeAttrs == null || !QuantityPipeAttributes.IsMainPipeKind(pipeAttrs.ObjectKind)) continue;

                    bool changed = false;
                    if (!string.IsNullOrWhiteSpace(pipeAttrs.StartNode)
                        && string.Equals(pipeAttrs.StartNode.Trim(), nodeNo, StringComparison.CurrentCultureIgnoreCase))
                    {
                        pipeAttrs.StartDepth = CalculatePipeEndpointDepthFromNode(pipeAttrs, nodeAttrs);
                        changed = true;
                    }

                    if (!string.IsNullOrWhiteSpace(pipeAttrs.EndNode)
                        && string.Equals(pipeAttrs.EndNode.Trim(), nodeNo, StringComparison.CurrentCultureIgnoreCase))
                    {
                        pipeAttrs.EndDepth = CalculatePipeEndpointDepthFromNode(pipeAttrs, nodeAttrs);
                        changed = true;
                    }

                    if (!changed) continue;
                    QuantityPipeAttributes startWell = !string.IsNullOrWhiteSpace(pipeAttrs.StartNode)
                        && string.Equals(pipeAttrs.StartNode.Trim(), nodeNo, StringComparison.CurrentCultureIgnoreCase)
                        ? nodeAttrs : null;
                    QuantityPipeAttributes endWell = !string.IsNullOrWhiteSpace(pipeAttrs.EndNode)
                        && string.Equals(pipeAttrs.EndNode.Trim(), nodeNo, StringComparison.CurrentCultureIgnoreCase)
                        ? nodeAttrs : null;
                    QuantityDependencyResult normalized = QuantityDependencyService.NormalizeDraft(
                        pipeAttrs,
                        QuantityStructureLayer.Parse(pipeAttrs.BackfillStructure),
                        pipeAttrs,
                        startWell,
                        endWell,
                        null,
                        "ConnectedWell");
                    pipeAttrs = normalized.Attributes;

                    if (!entity.IsWriteEnabled) entity.UpgradeOpen();
                    WritePipeAttributes(entity, tr, pipeAttrs);
                    changedHandles.Add(entity.Handle.ToString());
                }
                catch
                {
                    // ??????????????????
                }
            }
            return changedHandles;
        }

        public static QuantityPipeWriteResult ApplyToSelection(Document doc, QuantityPipeAttributes sourceAttributes, bool keepIdentityFields)
        {
            return ApplyToSelection(doc, sourceAttributes, keepIdentityFields, null);
        }

        public static QuantityPipeWriteResult ApplyToSelection(Document doc, QuantityPipeAttributes sourceAttributes, bool keepIdentityFields, Action<int, int, string> progress)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            sourceAttributes = sourceAttributes == null ? QuantityPipeAttributes.Default : sourceAttributes.Clone();

            Editor ed = doc.Editor;
            var opt = new PromptSelectionOptions();
            opt.MessageForAdding = "\n????????????????/????";
            opt.MessageForRemoval = "\n?????";

            PromptSelectionResult res = ed.GetHudSelection(opt);
            if (res.Status != PromptStatus.OK || res.Value == null || res.Value.Count == 0)
            {
                return new QuantityPipeWriteResult { Success = false, Message = "??????" };
            }

            int success = 0;
            int skip = 0;
            int unavailableLayerSkip = 0;
            int fail = 0;
            int total = res.Value == null ? 0 : res.Value.Count;
            int processed = 0;
            bool showProgress = progress != null && total > 1;
            var changedAnnotationSourceHandles = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            if (showProgress) ReportProgress(progress, 0, total, "????????...");

            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selected in res.Value)
                {
                    if (selected == null || selected.ObjectId.IsNull)
                    {
                        skip++;
                        processed++;
                        if (showProgress) ReportProgress(progress, processed, total, "?????????" + processed + "/" + total);
                        continue;
                    }

                    try
                    {
                        Entity entity = tr.GetObject(selected.ObjectId, OpenMode.ForRead, false) as Entity;
                        if (entity == null)
                        {
                            skip++;
                            continue;
                        }

                        if (IsEntityLayerUnavailable(db, tr, entity))
                        {
                            skip++;
                            unavailableLayerSkip++;
                            continue;
                        }

                        QuantityPipeAttributes attrs = sourceAttributes.Clone();
                        if (string.IsNullOrWhiteSpace(attrs.ObjectKind)) attrs.ObjectKind = InferObjectKind(db, tr, entity);
                        ApplyLayerMetadata(db, tr, entity, attrs);

                        bool hasOld = HasPipeAttributes(entity, tr);
                        QuantityPipeAttributes old = hasOld ? ReadPipeAttributes(entity, tr) : attrs.Clone();
                        if (keepIdentityFields && hasOld)
                        {
                            attrs.StartNode = old.StartNode;
                            attrs.EndNode = old.EndNode;
                            attrs.NodeNo = old.NodeNo;
                            attrs.UseManualLength = old.UseManualLength;
                            attrs.ManualLength = old.ManualLength;
                            attrs.StartDepth = old.StartDepth;
                            attrs.EndDepth = old.EndDepth;
                            attrs.AverageDepth = old.AverageDepth;
                            attrs.GroundElevation = old.GroundElevation;
                            attrs.WellDepth = old.WellDepth;
                            attrs.Remark = old.Remark;
                            attrs.IsSpecialObject = old.IsSpecialObject;
                        }

                        attrs = NormalizeAttributesForWrite(db, tr, entity, attrs, old, "BatchWrite");
                        if (!entity.IsWriteEnabled) entity.UpgradeOpen();
                        WritePipeAttributes(entity, tr, attrs);
                        changedAnnotationSourceHandles.Add(
                            entity.Handle.ToString());
                        if (QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind))
                        {
                            foreach (string linkedHandle in
                                RefreshMainPipeDepthsLinkedToNode(
                                    db, tr, entity.ObjectId, attrs))
                                changedAnnotationSourceHandles.Add(linkedHandle);
                        }
                        success++;
                    }
                    catch
                    {
                        fail++;
                    }
                    finally
                    {
                        processed++;
                        if (showProgress) ReportProgress(progress, processed, total, "?????????" + processed + "/" + total);
                    }
                }

                tr.Commit();
            }
            RefreshBoundAnnotationsAfterBatch(doc,
                changedAnnotationSourceHandles);

            string message = "????????? " + success + " ???? " + skip + " ???? " + fail + " ??";
            if (unavailableLayerSkip > 0) message += "\n?? " + unavailableLayerSkip + " ??????????????????";
            if (success > 0)
                message += "\n???????????????";
            return new QuantityPipeWriteResult
            {
                Success = success > 0,
                SuccessCount = success,
                SkipCount = skip,
                FailCount = fail,
                Message = message
            };
        }

        public static QuantityPipeWriteResult ApplyDefaultProfilesToObjects(Document doc, IEnumerable<ObjectId> objectIds)
        {
            return ApplyDefaultProfilesToObjects(doc, objectIds, null);
        }

        public static QuantityPipeWriteResult ApplyDefaultProfilesToObjects(Document doc, IEnumerable<ObjectId> objectIds, Action<int, int, string> progress)
        {
            if (doc == null) throw new ArgumentNullException("doc");

            List<ObjectId> ids = NormalizeObjectIds(objectIds);
            if (ids.Count == 0)
            {
                return new QuantityPipeWriteResult { Success = false, Message = "??????" };
            }

            int success = 0;
            int skip = 0;
            int fail = 0;
            int mainCount = 0;
            int branchCount = 0;
            int nodeCount = 0;
            List<string> errors = new List<string>();
            var changedAnnotationSourceHandles = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            bool showProgress = progress != null && ids.Count > 1;
            if (showProgress) ReportProgress(progress, 0, ids.Count, "???????????...");

            Database db = doc.Database;
            int lockedSkip = 0;
            using (doc.LockDocument())
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    try
                    {
                        string appliedKind = string.Empty;
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            Entity entity = tr.GetObject(ids[i], OpenMode.ForRead, false) as Entity;
                            if (entity == null)
                            {
                                skip++;
                                tr.Commit();
                                continue;
                            }

                            string kind = InferSupportedObjectKind(db, tr, entity);
                            if (string.IsNullOrWhiteSpace(kind))
                            {
                                skip++;
                                tr.Commit();
                                continue;
                            }

                            if (IsEntityLayerUnavailable(db, tr, entity))
                            {
                                skip++;
                                lockedSkip++;
                                tr.Commit();
                                continue;
                            }

                            appliedKind = kind;

                            QuantityPipeAttributes defaultAttrs = QuantityAttributeDefaultStore.LoadForKind(kind);
                            defaultAttrs.ObjectKind = kind;
                            ApplySmartDefaults(db, tr, entity, entity.Layer, defaultAttrs, false);
                            defaultAttrs.ObjectKind = kind;

                            bool hasSavedAttributes = HasPipeAttributes(entity, tr);
                            QuantityPipeAttributes attrs = hasSavedAttributes ? ReadPipeAttributes(entity, tr) : defaultAttrs.Clone();
                            if (attrs.IsSpecialObject)
                            {
                                skip++;
                                tr.Commit();
                                continue;
                            }
                            QuantityPipeAttributes previous = attrs.Clone();
                            attrs.ObjectKind = kind;
                            ApplyLayerMetadata(db, tr, entity, attrs);

                            // v19 ????????? SX ???????????
                            // ??????????????????????????????????? SXMRB ??????
                            // ?????????/????/????????????????????????
                            RefreshAttributeModel(db, tr, entity, attrs, defaultAttrs, kind, true, false);

                            attrs = NormalizeAttributesForWrite(db, tr, entity, attrs, previous, "ApplyDefaults");
                            if (!entity.IsWriteEnabled) entity.UpgradeOpen();
                            WritePipeAttributes(entity, tr, attrs);
                            changedAnnotationSourceHandles.Add(
                                entity.Handle.ToString());
                            if (QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind))
                            {
                                foreach (string linkedHandle in
                                    RefreshMainPipeDepthsLinkedToNode(
                                        db, tr, entity.ObjectId, attrs))
                                    changedAnnotationSourceHandles.Add(
                                        linkedHandle);
                            }
                            tr.Commit();
                        }

                        success++;
                        if (appliedKind == QuantityPipeAttributes.KindNodeWell) nodeCount++;
                        else if (appliedKind == QuantityPipeAttributes.KindBranchPipe) branchCount++;
                        else mainCount++;
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        if (IsLockViolation(ex))
                        {
                            skip++;
                            lockedSkip++;
                            if (errors.Count < 3) errors.Add("??????????????");
                        }
                        else
                        {
                            fail++;
                            if (errors.Count < 3) errors.Add(ex.Message);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        fail++;
                        if (errors.Count < 3) errors.Add(ex.Message);
                    }
                    finally
                    {
                        if (showProgress) ReportProgress(progress, i + 1, ids.Count, "????????????" + (i + 1) + "/" + ids.Count);
                    }
                }
            }
            RefreshBoundAnnotationsAfterBatch(doc,
                changedAnnotationSourceHandles);

            string message = "?????????? " + success + " ?";
            if (success > 0) message += "??? " + mainCount + "??? " + branchCount + "???/? " + nodeCount + "?";
            message += "??? " + skip + " ???? " + fail + " ??";
            if (lockedSkip > 0) message += "\n?? " + lockedSkip + " ?????????????????????????";
            message += "\n????????????????????????????????????";
            message += "\n?????????????????????????????????????????????????";
            if (success > 0)
                message += "\n???????????????";
            if (errors.Count > 0) message += "\n????????" + string.Join("?", errors.ToArray());

            return new QuantityPipeWriteResult
            {
                Success = success > 0,
                SuccessCount = success,
                SkipCount = skip,
                FailCount = fail,
                Message = message
            };
        }

        public static QuantityPipeWriteResult ClearAttributesFromObjects(Document doc, IEnumerable<ObjectId> objectIds)
        {
            if (doc == null) throw new ArgumentNullException("doc");

            List<ObjectId> ids = NormalizeObjectIds(objectIds);
            if (ids.Count == 0)
            {
                return new QuantityPipeWriteResult { Success = false, Message = "??????" };
            }

            int success = 0;
            int skip = 0;
            int fail = 0;
            List<string> errors = new List<string>();
            Database db = doc.Database;
            int lockedSkip = 0;

            using (doc.LockDocument())
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    try
                    {
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            Entity entity = tr.GetObject(ids[i], OpenMode.ForRead, false) as Entity;
                            if (entity == null)
                            {
                                skip++;
                                tr.Commit();
                                continue;
                            }

                            if (!HasPipeAttributes(entity, tr))
                            {
                                skip++;
                                tr.Commit();
                                continue;
                            }

                            if (IsEntityLayerUnavailable(db, tr, entity))
                            {
                                skip++;
                                lockedSkip++;
                                tr.Commit();
                                continue;
                            }

                            if (!entity.IsWriteEnabled) entity.UpgradeOpen();
                            if (!RemovePipeAttributes(entity, tr))
                            {
                                skip++;
                                tr.Commit();
                                continue;
                            }

                            tr.Commit();
                            success++;
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        if (IsLockViolation(ex))
                        {
                            skip++;
                            lockedSkip++;
                            if (errors.Count < 3) errors.Add("??????????????");
                        }
                        else
                        {
                            fail++;
                            if (errors.Count < 3) errors.Add(ex.Message);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        fail++;
                        if (errors.Count < 3) errors.Add(ex.Message);
                    }
                }
            }

            string message = "????????? " + success + " ???? " + skip + " ???? " + fail + " ??";
            if (lockedSkip > 0) message += "\n?? " + lockedSkip + " ?????????????????????????";
            if (errors.Count > 0) message += "\n????????" + string.Join("?", errors.ToArray());

            return new QuantityPipeWriteResult
            {
                Success = success > 0,
                SuccessCount = success,
                SkipCount = skip,
                FailCount = fail,
                Message = message
            };
        }

        public static QuantityPipeAttributes ApplySmartDefaults(Document doc, string layerName, QuantityPipeAttributes attributes)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            attributes = attributes == null ? QuantityPipeAttributes.Default : attributes.Clone();

            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                ApplySmartDefaults(db, tr, null, layerName, attributes, true);
                tr.Commit();
            }
            return attributes;
        }

        public static QuantityPipeAttributes ApplySmartDefaults(Document doc, ObjectId objectId, QuantityPipeAttributes attributes)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            attributes = attributes == null ? QuantityPipeAttributes.Default : attributes.Clone();
            if (attributes.IsSpecialObject || objectId.IsNull) return attributes;

            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity entity = tr.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                ApplySmartDefaults(db, tr, entity, entity == null ? string.Empty : entity.Layer, attributes, true);
                Curve curve = entity as Curve;
                if (curve != null && QuantityPipeAttributes.IsMainPipeKind(attributes.ObjectKind))
                {
                    TryFillConnectedNodeInfo(db, tr, entity, curve, attributes);
                }
                tr.Commit();
            }
            return attributes;
        }

        public static QuantityPipeAttributes ReDetectConnectedNodeInfo(Document doc, ObjectId objectId, QuantityPipeAttributes attributes)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            attributes = attributes == null ? QuantityPipeAttributes.DefaultMainPipe : attributes.Clone();
            if (attributes.IsSpecialObject || objectId.IsNull) return attributes;
            attributes.ObjectKind = QuantityPipeAttributes.KindMainPipe;

            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity entity = tr.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                Curve curve = entity as Curve;
                if (entity != null) ApplyLayerMetadata(db, tr, entity, attributes);
                if (entity != null && curve != null)
                {
                    TryFillConnectedNodeInfo(db, tr, entity, curve, attributes, true);
                    RecalculateMainPipeAverageDepth(attributes);
                }
                tr.Commit();
            }

            return attributes;
        }

        public static QuantityPipeAttributes RefreshAttributesForObject(Document doc, ObjectId objectId, QuantityPipeAttributes attributes)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            attributes = attributes == null ? QuantityPipeAttributes.Default : attributes.Clone();
            if (attributes.IsSpecialObject || objectId.IsNull) return attributes;

            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity entity = tr.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                string kind = attributes.IsSpecialObject ? attributes.ObjectKind : (entity == null ? attributes.ObjectKind : InferSupportedObjectKind(db, tr, entity));
                if (string.IsNullOrWhiteSpace(kind)) kind = attributes.ObjectKind;
                if (string.IsNullOrWhiteSpace(kind)) kind = QuantityPipeAttributes.KindMainPipe;

                QuantityPipeAttributes defaults = QuantityAttributeDefaultStore.LoadForKind(kind);
                defaults.ObjectKind = kind;
                ApplySmartDefaults(db, tr, entity, entity == null ? string.Empty : entity.Layer, defaults, false);
                RefreshAttributeModel(db, tr, entity, attributes, defaults, kind, false, false);
                tr.Commit();
            }
            return attributes;
        }

        public static QuantityDependencyResult CalculateDraft(
            Document doc,
            ObjectId objectId,
            QuantityPipeAttributes attributes,
            IEnumerable<QuantityStructureLayer> layers,
            string changedField)
        {
            return CalculateDraft(doc, objectId, attributes, layers, changedField, null);
        }

        public static QuantityDependencyResult CalculateDraft(
            Document doc,
            ObjectId objectId,
            QuantityPipeAttributes attributes,
            IEnumerable<QuantityStructureLayer> layers,
            string changedField,
            QuantityPipeAttributes previousDraft)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            attributes = attributes == null ? QuantityPipeAttributes.Default : attributes.Clone();

            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity entity = objectId.IsNull ? null : tr.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                string kind = attributes.IsSpecialObject ? attributes.ObjectKind : (entity == null ? attributes.ObjectKind : InferSupportedObjectKind(db, tr, entity));
                if (!attributes.IsSpecialObject && !string.IsNullOrWhiteSpace(kind)) attributes.ObjectKind = kind;
                if (entity != null) ApplyLayerMetadata(db, tr, entity, attributes);

                QuantityPipeAttributes previous = previousDraft == null ? attributes.Clone() : previousDraft.Clone();
                if (previousDraft == null && entity != null && HasPipeAttributes(entity, tr)) previous = ReadPipeAttributes(entity, tr);

                QuantityPipeAttributes startWell = null;
                QuantityPipeAttributes endWell = null;
                if (QuantityPipeAttributes.IsMainPipeKind(attributes.ObjectKind) && !attributes.IsSpecialObject)
                {
                    ObjectId spaceId = entity == null || entity.OwnerId.IsNull ? db.CurrentSpaceId : entity.OwnerId;
                    List<NodeCandidate> candidates = CollectNodeCandidates(db, tr, spaceId);
                    startWell = FindNodeAttributesByNo(candidates, attributes.StartNode);
                    endWell = FindNodeAttributesByNo(candidates, attributes.EndNode);
                }

                QuantityPipeAttributes branchDefaults = QuantityPipeAttributes.IsBranchKind(attributes.ObjectKind)
                    ? QuantityAttributeDefaultStore.LoadForKind(QuantityPipeAttributes.KindBranchPipe)
                    : null;
                QuantityDependencyResult result = QuantityDependencyService.NormalizeDraft(
                    attributes, layers, previous, startWell, endWell, branchDefaults, changedField);
                tr.Commit();
                return result;
            }
        }

        private static QuantityPipeAttributes FindNodeAttributesByNo(IEnumerable<NodeCandidate> candidates, string nodeNo)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(nodeNo)) return null;
            string wanted = nodeNo.Trim();
            foreach (NodeCandidate candidate in candidates)
            {
                QuantityPipeAttributes attrs = candidate == null ? null : candidate.Attributes;
                if (attrs == null || string.IsNullOrWhiteSpace(attrs.NodeNo)) continue;
                if (string.Equals(attrs.NodeNo.Trim(), wanted, StringComparison.CurrentCultureIgnoreCase)) return attrs.Clone();
            }
            return null;
        }

        private static QuantityPipeAttributes NormalizeAttributesForWrite(
            Database db,
            Transaction tr,
            Entity entity,
            QuantityPipeAttributes attributes,
            QuantityPipeAttributes previous,
            string changedField)
        {
            attributes = attributes == null ? QuantityPipeAttributes.Default : attributes.Clone();
            previous = previous == null ? attributes.Clone() : previous.Clone();

            if (entity != null)
            {
                if (!attributes.IsSpecialObject && string.IsNullOrWhiteSpace(attributes.ObjectKind)) attributes.ObjectKind = InferObjectKind(db, tr, entity);
                ApplyLayerMetadata(db, tr, entity, attributes);
            }

            QuantityPipeAttributes startWell = null;
            QuantityPipeAttributes endWell = null;
            if (QuantityPipeAttributes.IsMainPipeKind(attributes.ObjectKind) && !attributes.IsSpecialObject)
            {
                ObjectId spaceId = entity == null || entity.OwnerId.IsNull ? db.CurrentSpaceId : entity.OwnerId;
                List<NodeCandidate> candidates = CollectNodeCandidates(db, tr, spaceId);
                startWell = FindNodeAttributesByNo(candidates, attributes.StartNode);
                endWell = FindNodeAttributesByNo(candidates, attributes.EndNode);
            }

            QuantityPipeAttributes branchDefaults = QuantityPipeAttributes.IsBranchKind(attributes.ObjectKind)
                ? QuantityAttributeDefaultStore.LoadForKind(QuantityPipeAttributes.KindBranchPipe)
                : null;
            return QuantityDependencyService.NormalizeDraft(
                attributes,
                QuantityStructureLayer.Parse(attributes.BackfillStructure),
                previous,
                startWell,
                endWell,
                branchDefaults,
                changedField).Attributes;
        }

        public static QuantityPipeAttributes SelectConnectedNodeForMainPipe(Document doc, ObjectId pipeObjectId, QuantityPipeAttributes attributes, bool forStart)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            attributes = attributes == null ? QuantityPipeAttributes.DefaultMainPipe : attributes.Clone();
            attributes.ObjectKind = QuantityPipeAttributes.KindMainPipe;

            Database db = doc.Database;
            List<NodeCandidate> candidates;
            ObjectId textStyleId = ObjectId.Null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity pipeEntity = pipeObjectId.IsNull ? null : tr.GetObject(pipeObjectId, OpenMode.ForRead, false) as Entity;
                ObjectId spaceId = pipeEntity == null || pipeEntity.OwnerId.IsNull ? db.CurrentSpaceId : pipeEntity.OwnerId;
                candidates = CollectNodeCandidates(db, tr, spaceId);
                try { textStyleId = db.Textstyle; } catch { textStyleId = ObjectId.Null; }
                tr.Commit();
            }

            if (candidates == null || candidates.Count == 0)
            {
                doc.Editor.WriteHudMessage("\n???????????????????????=????=???????");
                return attributes;
            }

            var jig = new QuantityNodeSelectPreviewJig(candidates, forStart, textStyleId);
            PromptResult dragResult = doc.Editor.DragWithHud(jig,
                forStart
                    ? "??????????????????????????"
                    : "??????????????????????????");
            if (dragResult.Status != PromptStatus.OK) return attributes;

            NodeCandidate node = jig.SelectedCandidate ?? FindNearestNodeCandidate(candidates, jig.PickPoint);
            if (node != null)
            {
                // ??????????????????????????/?????
                if (forStart) ApplyNodeToPipeStart(attributes, node, true);
                else ApplyNodeToPipeEnd(attributes, node, true);

                string nodeNo = node.Attributes == null ? string.Empty : (node.Attributes.NodeNo ?? string.Empty);
                doc.Editor.WriteHudMessage(string.IsNullOrWhiteSpace(nodeNo)
                    ? "\n?????????"
                    : "\n??????" + nodeNo);
            }

            return attributes;
        }

        /// <summary>
        /// ?????????????????????????
        /// ????????????????????????????
        /// ?? null ?????????????
        /// </summary>
        public static QuantityPipeAttributes SelectNodeWithPreview(
            Document doc, bool forStart)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            Database db = doc.Database;
            List<NodeCandidate> candidates;
            ObjectId textStyleId = ObjectId.Null;
            using (Transaction tr =
                db.TransactionManager.StartTransaction())
            {
                candidates = CollectNodeCandidates(db, tr,
                    db.CurrentSpaceId);
                try { textStyleId = db.Textstyle; }
                catch { textStyleId = ObjectId.Null; }
                tr.Commit();
            }

            if (candidates == null || candidates.Count == 0)
            {
                doc.Editor.WriteHudMessage(
                    "\n???????????????????????=????=????????");
                return null;
            }

            var jig = new QuantityNodeSelectPreviewJig(candidates,
                forStart, textStyleId);
            PromptResult dragResult = doc.Editor.DragWithHud(jig,
                forStart
                    ? "?????????????????????????"
                    : "?????????????????????????");
            if (dragResult.Status != PromptStatus.OK) return null;

            NodeCandidate selected = jig.SelectedCandidate
                ?? FindNearestNodeCandidate(candidates, jig.PickPoint);
            if (selected == null || selected.Attributes == null)
                return null;
            return selected.Attributes.Clone();
        }

        public static void SaveDefaultProfile(string kind, QuantityPipeAttributes attrs)
        {
            QuantityAttributeDefaultStore.SaveForKind(kind, attrs);
        }

        public static QuantityPipeAttributes LoadDefaultProfile(string kind)
        {
            return QuantityAttributeDefaultStore.LoadForKind(kind);
        }

        /// <summary>
        /// ??????????????????
        /// ? ApplySmartDefaults ??????????/??????????????????
        /// ???????????????/???????????????
        /// </summary>
        public static QuantityPipeAttributes LoadDefaultProfileForObject(Document doc, ObjectId objectId, string kind)
        {
            return LoadDefaultProfileForObject(doc, objectId, kind, null);
        }

        public static QuantityPipeAttributes LoadDefaultProfileForObject(Document doc, ObjectId objectId, string kind, QuantityPipeAttributes currentAttributes)
        {
            if (doc == null) throw new ArgumentNullException("doc");

            QuantityPipeAttributes defaults = LoadDefaultProfile(kind);
            defaults.ObjectKind = string.IsNullOrWhiteSpace(kind) ? defaults.ObjectKind : kind;
            if (objectId.IsNull)
            {
                QuantityPipeAttributes detached = currentAttributes == null ? defaults.Clone() : currentAttributes.Clone();
                ApplyDefaultControlledFields(detached, defaults, defaults.ObjectKind);
                return detached;
            }

            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity entity = tr.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                string effectiveKind = entity == null ? kind : InferSupportedObjectKind(db, tr, entity);
                if (string.IsNullOrWhiteSpace(effectiveKind)) effectiveKind = kind;
                if (string.IsNullOrWhiteSpace(effectiveKind)) effectiveKind = defaults.ObjectKind;
                defaults.ObjectKind = effectiveKind;

                QuantityPipeAttributes attrs = currentAttributes == null
                    ? (entity != null && HasPipeAttributes(entity, tr) ? ReadPipeAttributes(entity, tr) : defaults.Clone())
                    : currentAttributes.Clone();
                attrs.ObjectKind = effectiveKind;
                RefreshAttributeModel(db, tr, entity, attrs, defaults, effectiveKind, true, false);

                tr.Commit();
                return attrs;
            }
        }

        private static QuantityPipeAttributes BuildDefaultAttributesFromEntity(Database db, Transaction tr, Entity entity, string inferredKind)
        {
            QuantityPipeAttributes attrs = QuantityAttributeDefaultStore.LoadForKind(inferredKind);
            attrs.ObjectKind = string.IsNullOrWhiteSpace(inferredKind) ? attrs.ObjectKind : inferredKind;
            ApplySmartDefaults(db, tr, entity, entity == null ? string.Empty : entity.Layer, attrs, false);
            return attrs;
        }

        private static void FillMissingAttributesFromDefault(Entity entity, Transaction tr, QuantityPipeAttributes target, QuantityPipeAttributes defaults, string kind)
        {
            if (target == null || defaults == null) return;

            HashSet<string> keys = ReadPipeAttributeKeySet(entity, tr);
            target.ObjectKind = string.IsNullOrWhiteSpace(kind) ? target.ObjectKind : kind;

            // ???????????????????????????????????????????
            target.LayerParentGroup = defaults.LayerParentGroup ?? string.Empty;
            target.LayerParentClass = defaults.LayerParentClass ?? string.Empty;
            target.LayerTags = defaults.LayerTags ?? string.Empty;

            target.Enabled = FillBoolIfMissing(keys, "Enabled", target.Enabled, defaults.Enabled);
            target.Material = FillStringIfEmpty(target.Material, defaults.Material);
            target.Diameter = FillStringIfEmpty(target.Diameter, defaults.Diameter);
            target.UseManualLength = FillBoolIfMissing(keys, "UseManualLength", target.UseManualLength, defaults.UseManualLength);
            target.ManualLength = FillDoubleIfMissing(keys, "ManualLength", target.ManualLength, defaults.ManualLength);
            target.DrawLengthWidthHeightAnnotation = FillBoolIfMissing(keys, "DrawLengthWidthHeightAnnotation", target.DrawLengthWidthHeightAnnotation, defaults.DrawLengthWidthHeightAnnotation);

            if (QuantityPipeAttributes.IsMainPipeKind(target.ObjectKind))
            {
                bool startWasEmpty = string.IsNullOrWhiteSpace(target.StartNode);
                bool endWasEmpty = string.IsNullOrWhiteSpace(target.EndNode);

                target.StartNode = FillStringIfEmpty(target.StartNode, defaults.StartNode);
                target.EndNode = FillStringIfEmpty(target.EndNode, defaults.EndNode);

                // ?????????????????????????????????????????
                if (!HasKey(keys, "StartDepth") || (startWasEmpty && target.StartDepth <= 0 && defaults.StartDepth > 0)) target.StartDepth = defaults.StartDepth;
                if (!HasKey(keys, "EndDepth") || (endWasEmpty && target.EndDepth <= 0 && defaults.EndDepth > 0)) target.EndDepth = defaults.EndDepth;
                if (!HasKey(keys, "AverageDepth") || (target.AverageDepth <= 0 && defaults.AverageDepth > 0)) target.AverageDepth = defaults.AverageDepth;
            }
            else if (QuantityPipeAttributes.IsBranchKind(target.ObjectKind))
            {
                target.BranchType = FillStringIfEmpty(target.BranchType, defaults.BranchType);
                target.BranchIncludeInCalculation = FillBoolIfMissing(keys, "BranchIncludeInCalculation", target.BranchIncludeInCalculation, defaults.BranchIncludeInCalculation);
                target.BranchDepth = FillPositiveDoubleIfMissingOrEmpty(keys, "BranchDepth", target.BranchDepth, defaults.BranchDepth);
            }
            else if (QuantityPipeAttributes.IsNodeKind(target.ObjectKind))
            {
                target.NodeNo = FillStringIfEmpty(target.NodeNo, defaults.NodeNo);
                target.WellSpec = FillStringIfEmpty(target.WellSpec, defaults.WellSpec);
                target.WellCoverMaterial = FillStringIfEmpty(target.WellCoverMaterial, defaults.WellCoverMaterial);
                target.WellMaterialType = FillStringIfEmpty(target.WellMaterialType, defaults.WellMaterialType);
                target.WellType = FillStringIfEmpty(target.WellType, defaults.WellType);
                target.SiltWellDeductDepth500 = FillPositiveDoubleIfMissingOrEmpty(keys, "SiltWellDeductDepth500", target.SiltWellDeductDepth500, defaults.SiltWellDeductDepth500);
                target.SiltWellDeductDepth700 = FillPositiveDoubleIfMissingOrEmpty(keys, "SiltWellDeductDepth700", target.SiltWellDeductDepth700, defaults.SiltWellDeductDepth700);
                target.GroundElevation = FillDoubleIfMissing(keys, "GroundElevation", target.GroundElevation, defaults.GroundElevation);
                target.WellDepth = FillPositiveDoubleIfMissingOrEmpty(keys, "WellDepth", target.WellDepth, defaults.WellDepth);
                target.ShaftLength = FillDoubleIfMissing(keys, "ShaftLength", target.ShaftLength, defaults.ShaftLength);
                target.ExcavationLength = FillPositiveDoubleIfMissingOrEmpty(keys, "ExcavationLength", target.ExcavationLength, defaults.ExcavationLength);
                target.ExcavationWidth = FillPositiveDoubleIfMissingOrEmpty(keys, "ExcavationWidth", target.ExcavationWidth, defaults.ExcavationWidth);
                target.CoverPlate = FillStringIfEmpty(target.CoverPlate, defaults.CoverPlate);
            }

            target.TrenchWidth = FillPositiveDoubleIfMissingOrEmpty(keys, "TrenchWidth", target.TrenchWidth, defaults.TrenchWidth);
            // ?????? 0 ???????????????????????
            target.RoadThickness = FillDoubleIfMissing(keys, "RoadThickness", target.RoadThickness, defaults.RoadThickness);
            target.ExcavationType = FillStringIfEmpty(target.ExcavationType, defaults.ExcavationType);
            target.BackfillType = FillStringIfEmpty(target.BackfillType, defaults.BackfillType);
            target.BackfillStructure = FillStringIfEmpty(target.BackfillStructure, defaults.BackfillStructure);
            target.SandCushionThickness = FillDoubleIfMissing(keys, "SandCushionThickness", target.SandCushionThickness, defaults.SandCushionThickness);
            target.GravelCushionThickness = FillDoubleIfMissing(keys, "GravelCushionThickness", target.GravelCushionThickness, defaults.GravelCushionThickness);
            target.C25RestoreThickness = FillDoubleIfMissing(keys, "C25RestoreThickness", target.C25RestoreThickness, defaults.C25RestoreThickness);
            target.PipeOuterDiameter = FillPositiveDoubleIfMissingOrEmpty(keys, "PipeOuterDiameter", target.PipeOuterDiameter, defaults.PipeOuterDiameter);
            target.DeductPipeVolume = FillBoolIfMissing(keys, "DeductPipeVolume", target.DeductPipeVolume, defaults.DeductPipeVolume);

            target.BackfillStructure = QuantityPipeAttributes.NormalizeStructureLayerText(target.BackfillStructure);
            QuantityPipeAttributes.ApplyStructureLayerText(target);
        }

        private static void RefreshAttributeModel(Database db, Transaction tr, Entity entity, QuantityPipeAttributes target, QuantityPipeAttributes defaults, string kind, bool syncDefaultControlledFields, bool preserveAverageDepth)
        {
            if (target == null) return;
            if (defaults == null) defaults = QuantityPipeAttributes.DefaultForKind(kind);
            target.ObjectKind = string.IsNullOrWhiteSpace(kind) ? target.ObjectKind : kind;

            ApplyLayerMetadata(db, tr, entity, target);
            ApplySmartDefaults(db, tr, entity, entity == null ? string.Empty : entity.Layer, target, false);

            if (syncDefaultControlledFields) ApplyDefaultControlledFields(target, defaults, kind);
            else FillMissingAttributesFromDefault(entity, tr, target, defaults, kind);

            string refreshSource = BuildEntitySourceText(db, tr, entity, target);
            ApplyRecognizedSpecification(target, refreshSource);

            if (QuantityPipeAttributes.IsBranchKind(target.ObjectKind))
            {
                string source = refreshSource;
                string explicitBranchType = InferExplicitBranchType(source);
                if (!string.IsNullOrWhiteSpace(explicitBranchType))
                {
                    target.BranchType = explicitBranchType;
                    if (ShouldForceSpecialBranchStructure(explicitBranchType))
                    {
                        target.BackfillType = InferBranchBackfillType(explicitBranchType, source);
                        target.BackfillStructure = BuildDefaultBackfillStructure(target);
                    }
                }
            }

            Curve curve = entity as Curve;
            if (curve != null && QuantityPipeAttributes.IsMainPipeKind(target.ObjectKind))
            {
                TryFillConnectedNodeInfo(db, tr, entity, curve, target, true);
                RecalculateMainPipeAverageDepth(target);
            }
            else if (QuantityPipeAttributes.IsMainPipeKind(target.ObjectKind) && !preserveAverageDepth)
            {
                RecalculateMainPipeAverageDepth(target);
            }

            target.BackfillStructure = QuantityPipeAttributes.NormalizeStructureLayerText(target.BackfillStructure);
            QuantityPipeAttributes.ApplyStructureLayerText(target);
            if (target.PipeOuterDiameter <= 0 && !string.IsNullOrWhiteSpace(target.Diameter)) target.PipeOuterDiameter = InferOuterDiameter(target.Diameter);
            if (target.Enabled == false) target.Enabled = true;
            target.Remark = string.Empty;
        }

        private static void ApplyRecognizedSpecification(QuantityPipeAttributes target, string sourceText)
        {
            if (target == null) return;
            if (QuantityPipeAttributes.IsNodeKind(target.ObjectKind))
            {
                string wellSpec = InferWellSpec(sourceText);
                if (!string.IsNullOrWhiteSpace(wellSpec)) target.WellSpec = wellSpec;
                return;
            }

            string diameter = InferDiameter(sourceText);
            if (!string.IsNullOrWhiteSpace(diameter))
            {
                target.Diameter = diameter;
                double outer = InferOuterDiameter(diameter);
                if (outer > 0) target.PipeOuterDiameter = outer;
            }
        }

        private static void ApplyDefaultControlledFields(QuantityPipeAttributes target, QuantityPipeAttributes defaults, string kind)
        {
            if (target == null || defaults == null) return;

            target.Enabled = defaults.Enabled;
            target.Material = defaults.Material ?? string.Empty;
            target.Diameter = defaults.Diameter ?? string.Empty;
            target.DrawLengthWidthHeightAnnotation = defaults.DrawLengthWidthHeightAnnotation;
            target.TrenchWidth = defaults.TrenchWidth;
            target.RoadThickness = defaults.RoadThickness;
            target.ExcavationType = defaults.ExcavationType ?? string.Empty;
            target.BackfillType = defaults.BackfillType ?? string.Empty;
            target.BackfillStructure = defaults.BackfillStructure ?? string.Empty;
            target.SandCushionThickness = defaults.SandCushionThickness;
            target.GravelCushionThickness = defaults.GravelCushionThickness;
            target.C25RestoreThickness = defaults.C25RestoreThickness;
            target.PipeOuterDiameter = defaults.PipeOuterDiameter;
            target.DeductPipeVolume = defaults.DeductPipeVolume;

            if (QuantityPipeAttributes.IsBranchKind(kind))
            {
                target.BranchType = defaults.BranchType ?? target.BranchType;
                target.BranchIncludeInCalculation = defaults.BranchIncludeInCalculation;
                if (defaults.BranchDepth > 0) target.BranchDepth = defaults.BranchDepth;
            }
            else if (QuantityPipeAttributes.IsNodeKind(kind))
            {
                target.WellSpec = string.IsNullOrWhiteSpace(target.WellSpec) ? (defaults.WellSpec ?? string.Empty) : target.WellSpec;
                target.WellMaterialType = defaults.WellMaterialType ?? string.Empty;
                target.WellCoverMaterial = defaults.WellCoverMaterial ?? string.Empty;
                target.WellType = string.IsNullOrWhiteSpace(target.WellType) ? (defaults.WellType ?? string.Empty) : target.WellType;
                target.SiltWellDeductDepth500 = defaults.SiltWellDeductDepth500 > 0 ? defaults.SiltWellDeductDepth500 : target.SiltWellDeductDepth500;
                target.SiltWellDeductDepth700 = defaults.SiltWellDeductDepth700 > 0 ? defaults.SiltWellDeductDepth700 : target.SiltWellDeductDepth700;
                if (defaults.ExcavationLength > 0) target.ExcavationLength = defaults.ExcavationLength;
                if (defaults.ExcavationWidth > 0) target.ExcavationWidth = defaults.ExcavationWidth;
                target.CoverPlate = defaults.CoverPlate ?? target.CoverPlate;
            }
        }

        private static string BuildEntitySourceText(Database db, Transaction tr, Entity entity, QuantityPipeAttributes attrs)
        {
            string source = entity == null ? string.Empty : (entity.Layer ?? string.Empty);
            LayerMetadata meta = GetLayerMetadataSafe(db, tr, entity == null ? string.Empty : entity.Layer);
            if (meta != null)
            {
                source += " " + (meta.ParentGroup ?? string.Empty) + " " + (meta.ParentClass ?? string.Empty) + " " + (meta.TagText ?? string.Empty);
            }
            if (attrs != null)
            {
                source += " " + (attrs.Diameter ?? string.Empty) + " " + (attrs.WellSpec ?? string.Empty) + " " + (attrs.BranchType ?? string.Empty) + " " + (attrs.WellType ?? string.Empty);
            }
            return source;
        }

        private static string FillStringIfEmpty(string target, string value)
        {
            return string.IsNullOrWhiteSpace(target) && !string.IsNullOrWhiteSpace(value) ? value : (target ?? string.Empty);
        }

        private static bool FillBoolIfMissing(HashSet<string> keys, string key, bool target, bool value)
        {
            return !HasKey(keys, key) ? value : target;
        }

        private static double FillDoubleIfMissing(HashSet<string> keys, string key, double target, double value)
        {
            return !HasKey(keys, key) ? value : target;
        }

        private static double FillPositiveDoubleIfMissingOrEmpty(HashSet<string> keys, string key, double target, double value)
        {
            return (!HasKey(keys, key) || (target <= 0 && value > 0)) ? value : target;
        }

        private static bool HasKey(HashSet<string> keys, string key)
        {
            return keys != null && !string.IsNullOrWhiteSpace(key) && keys.Contains(key);
        }

        private static HashSet<string> ReadPipeAttributeKeySet(Entity entity, Transaction tr)
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (entity == null || tr == null) return keys;

            try
            {
                Xrecord record = GetPipeAttributeRecord(entity, tr);
                if (record == null || record.Data == null) return keys;

                foreach (TypedValue value in record.Data)
                {
                    if (value.Value == null) continue;
                    string text = value.Value.ToString();
                    int index = text.IndexOf('=');
                    if (index <= 0) continue;
                    string key = text.Substring(0, index).Trim();
                    if (key.Length > 0) keys.Add(key);
                }
            }
            catch
            {
            }

            return keys;
        }

        private static string FingerprintAttributes(QuantityPipeAttributes a)
        {
            if (a == null) return string.Empty;
            return string.Join("|", new string[]
            {
                a.Enabled.ToString(), a.ObjectKind ?? string.Empty, a.IsSpecialObject.ToString(), a.LayerParentGroup ?? string.Empty, a.LayerParentClass ?? string.Empty, a.LayerTags ?? string.Empty,
                a.Material ?? string.Empty, a.Diameter ?? string.Empty, a.UseManualLength.ToString(), Format(a.ManualLength), a.DrawLengthWidthHeightAnnotation.ToString(),
                a.StartNode ?? string.Empty, a.EndNode ?? string.Empty, Format(a.StartDepth), Format(a.EndDepth), Format(a.AverageDepth),
                Format(a.TrenchWidth), Format(a.RoadThickness), a.ExcavationType ?? string.Empty, a.BackfillType ?? string.Empty, QuantityPipeAttributes.NormalizeStructureLayerText(a.BackfillStructure),
                a.BranchType ?? string.Empty, a.BranchIncludeInCalculation.ToString(), Format(a.BranchDepth),
                a.NodeNo ?? string.Empty, a.WellSpec ?? string.Empty, a.WellCoverMaterial ?? string.Empty, a.WellMaterialType ?? string.Empty, a.WellType ?? string.Empty,
                Format(a.SiltWellDeductDepth500), Format(a.SiltWellDeductDepth700), Format(a.GroundElevation), Format(a.WellDepth), Format(a.ShaftLength), Format(a.ExcavationLength), Format(a.ExcavationWidth), a.CoverPlate ?? string.Empty,
                Format(a.SandCushionThickness), Format(a.GravelCushionThickness), Format(a.C25RestoreThickness), Format(a.PipeOuterDiameter), a.DeductPipeVolume.ToString(), a.Remark ?? string.Empty
            });
        }

        private static void ApplySmartDefaults(Database db, Transaction tr, Entity entity, string layerName, QuantityPipeAttributes attrs, bool overwrite)
        {
            if (attrs == null) return;
            if (attrs.IsSpecialObject) return;
            string sourceText = layerName ?? string.Empty;
            LayerMetadata meta = GetLayerMetadataSafe(db, tr, layerName);
            if (meta != null)
            {
                attrs.LayerParentGroup = meta.ParentGroup ?? string.Empty;
                attrs.LayerParentClass = meta.ParentClass ?? string.Empty;
                attrs.LayerTags = meta.TagText ?? string.Empty;
                sourceText += " " + attrs.LayerParentGroup + " " + attrs.LayerParentClass
                    + " " + attrs.LayerTags + " " + (meta.Specification ?? string.Empty)
                    + " " + (meta.Material ?? string.Empty);
            }

            string inferredKind = InferObjectKind(sourceText, entity);
            if (overwrite || string.IsNullOrWhiteSpace(attrs.ObjectKind)) attrs.ObjectKind = inferredKind;

            string diameter = InferDiameter(sourceText);
            if (!string.IsNullOrWhiteSpace(diameter) && (overwrite || string.IsNullOrWhiteSpace(attrs.Diameter)))
            {
                attrs.Diameter = diameter;
            }

            string wellSpec = InferWellSpec(sourceText);
            // ????????????????????????????????
            // ???700?????????? ?500 ????????????? 1.3 m?
            if (!string.IsNullOrWhiteSpace(wellSpec)
                && (overwrite || string.IsNullOrWhiteSpace(attrs.WellSpec)
                    || QuantityPipeAttributes.IsNodeKind(inferredKind)))
            {
                attrs.WellSpec = wellSpec;
            }

            if ((overwrite || string.IsNullOrWhiteSpace(attrs.Material)) && ContainsAny(sourceText, "HDPE", "???", "??"))
            {
                attrs.Material = "???????????????(HDPE)";
            }
            else if ((overwrite || string.IsNullOrWhiteSpace(attrs.Material)) && ContainsAny(sourceText, "PVC", "UPVC"))
            {
                attrs.Material = "PVC?";
            }

            string explicitBranchType = string.Empty;
            if (QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind))
            {
                explicitBranchType = InferExplicitBranchType(sourceText);
                if (overwrite || string.IsNullOrWhiteSpace(attrs.ExcavationType)) attrs.ExcavationType = "????";
                if (!string.IsNullOrWhiteSpace(explicitBranchType)) attrs.BranchType = explicitBranchType;
                else if (overwrite || string.IsNullOrWhiteSpace(attrs.BranchType)) attrs.BranchType = InferBranchType(sourceText);
                attrs.BranchIncludeInCalculation = ShouldIncludeBranch(attrs.BranchType, sourceText);
                if (overwrite || attrs.BranchDepth <= 0) attrs.BranchDepth = attrs.BranchDepth > 0 ? attrs.BranchDepth : 0.6;
            }
            else if (QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind))
            {
                if (overwrite || string.IsNullOrWhiteSpace(attrs.WellType)) attrs.WellType = InferWellType(sourceText);
                if (overwrite || string.IsNullOrWhiteSpace(attrs.WellMaterialType)) attrs.WellMaterialType = InferWellMaterialType(sourceText);
                if (overwrite || attrs.SiltWellDeductDepth500 <= 0) attrs.SiltWellDeductDepth500 = 0.20;
                if (overwrite || attrs.SiltWellDeductDepth700 <= 0) attrs.SiltWellDeductDepth700 = 0.50;
                if (overwrite || string.IsNullOrWhiteSpace(attrs.WellCoverMaterial)) attrs.WellCoverMaterial = "????";
                if (overwrite || attrs.ExcavationLength <= 0 || attrs.ExcavationWidth <= 0)
                {
                    double size = ContainsAny(attrs.WellSpec, "700") ? 1.5 : 1.3;
                    attrs.ExcavationLength = size;
                    attrs.ExcavationWidth = size;
                }
                if (overwrite || string.IsNullOrWhiteSpace(attrs.CoverPlate)) attrs.CoverPlate = ContainsAny(attrs.WellSpec, "700") ? "1600????" : "1200????";
            }
            else
            {
                if (overwrite || string.IsNullOrWhiteSpace(attrs.ExcavationType)) attrs.ExcavationType = "????";
            }

            double outer = InferOuterDiameter(attrs.Diameter);
            if (outer > 0 && (overwrite || attrs.PipeOuterDiameter <= 0)) attrs.PipeOuterDiameter = outer;

            if (overwrite || attrs.TrenchWidth <= 0)
            {
                attrs.TrenchWidth = InferTrenchWidth(attrs.Diameter, attrs.TrenchWidth);
            }

            if (overwrite || attrs.RoadThickness < 0)
            {
                attrs.RoadThickness = InferRoadThickness(sourceText, attrs.RoadThickness);
            }
            else if (attrs.RoadThickness <= 0 && !ContainsAny(sourceText, "??", "??", "???"))
            {
                attrs.RoadThickness = InferRoadThickness(sourceText, attrs.RoadThickness);
            }

            if (overwrite || attrs.SandCushionThickness <= 0) attrs.SandCushionThickness = QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind) ? 0.10 : 0.15;
            if (overwrite || attrs.GravelCushionThickness <= 0) attrs.GravelCushionThickness = 0.10;
            if (overwrite || attrs.C25RestoreThickness <= 0) attrs.C25RestoreThickness = QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind) ? 0.30 : 0.25;

            if (QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind))
            {
                bool hasExplicitBranchTag = !string.IsNullOrWhiteSpace(explicitBranchType);
                bool forceSpecialBranchStructure = ShouldForceSpecialBranchStructure(explicitBranchType);

                // ????????????? SXMRB ??????????
                // ???????????????????????????????????
                // ???? / ?? / ?????????????????????????????????
                if (overwrite || string.IsNullOrWhiteSpace(attrs.BackfillType) || forceSpecialBranchStructure) attrs.BackfillType = InferBranchBackfillType(attrs.BranchType, sourceText);
                if (overwrite || string.IsNullOrWhiteSpace(attrs.BackfillStructure) || forceSpecialBranchStructure) attrs.BackfillStructure = BuildDefaultBackfillStructure(attrs);
                ApplyNoStructureThicknessRules(attrs);
            }
            else
            {
                if (overwrite || string.IsNullOrWhiteSpace(attrs.BackfillType)) attrs.BackfillType = ContainsAny(sourceText, "??") ? "????" : "?????";
                if (overwrite || string.IsNullOrWhiteSpace(attrs.BackfillStructure)) attrs.BackfillStructure = BuildDefaultBackfillStructure(attrs);
            }

            if (overwrite || attrs.Enabled == false) attrs.Enabled = true;
            if (QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind)) attrs.DeductPipeVolume = false;
            else attrs.DeductPipeVolume = true;
        }

        private static void ApplyLayerMetadata(Database db, Transaction tr, Entity entity, QuantityPipeAttributes attrs)
        {
            if (attrs == null) return;
            LayerMetadata meta = GetLayerMetadataSafe(db, tr, entity == null ? string.Empty : entity.Layer);
            attrs.LayerParentGroup = meta == null ? string.Empty : (meta.ParentGroup ?? string.Empty);
            attrs.LayerParentClass = meta == null ? string.Empty : (meta.ParentClass ?? string.Empty);
            attrs.LayerTags = meta == null ? string.Empty : (meta.TagText ?? string.Empty);
            if (meta == null || attrs.IsSpecialObject
                || QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind)) return;

            string recognizedDiameter = InferDiameterFromSpecification(
                meta.Specification);
            if (!string.IsNullOrWhiteSpace(recognizedDiameter))
            {
                attrs.Diameter = recognizedDiameter;
                double outerDiameter = InferOuterDiameter(recognizedDiameter);
                if (outerDiameter > 0) attrs.PipeOuterDiameter = outerDiameter;
            }
        }

        private static LayerMetadata GetLayerMetadataSafe(Database db, Transaction tr, string layerName)
        {
            try
            {
                return LayerManagerService.GetLayerMetadata(db, tr, layerName) ?? new LayerMetadata();
            }
            catch
            {
                return new LayerMetadata();
            }
        }

        private static string InferSupportedObjectKind(Database db, Transaction tr, Entity entity)
        {
            if (entity == null) return string.Empty;
            LayerMetadata meta = GetLayerMetadataSafe(db, tr, entity.Layer);
            return InferSupportedObjectKind(meta, entity);
        }

        private static bool IsSupportedAttributeKind(string kind)
        {
            return QuantityPipeAttributes.IsMainPipeKind(kind)
                || QuantityPipeAttributes.IsBranchKind(kind)
                || QuantityPipeAttributes.IsNodeKind(kind);
        }

        private static string InferSupportedObjectKind(string sourceText, Entity entity)
        {
            // ???????????????????????????????/?????
            // ????????????????????????
            return string.Empty;
        }

        private static string InferSupportedObjectKind(LayerMetadata meta, Entity entity)
        {
            if (meta == null || entity == null) return string.Empty;

            string parent = NormalizeLayerMetadataText(meta.ParentGroup);
            string cls = NormalizeLayerMetadataText(meta.ParentClass);

            bool isCurve = entity is Curve;
            bool isNodeEntity = entity is Circle || entity is DBPoint || entity is BlockReference;

            if (TextEquals(parent, "??") && isCurve) return QuantityPipeAttributes.KindMainPipe;
            if (TextEquals(parent, "??") && isCurve) return QuantityPipeAttributes.KindBranchPipe;
            if (TextEquals(parent, "?") && isNodeEntity && IsSupportedWellClass(cls)) return QuantityPipeAttributes.KindNodeWell;

            return string.Empty;
        }

        private static string InferObjectKind(Database db, Transaction tr, Entity entity)
        {
            if (entity == null) return string.Empty;
            LayerMetadata meta = GetLayerMetadataSafe(db, tr, entity.Layer);
            return InferSupportedObjectKind(meta, entity);
        }

        private static string InferObjectKind(string sourceText, Entity entity)
        {
            // ? v14 ????????????????????????/??/??????????????
            return string.Empty;
        }

        private static bool IsSupportedWellClass(string layerClass)
        {
            // ?????????????????????????????????????????
            string cls = NormalizeLayerMetadataText(layerClass);
            return TextEquals(cls, "?????");
        }

        private static string NormalizeLayerMetadataText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text.Replace(" ", string.Empty)
                .Replace("?", string.Empty)
                .Replace("/", string.Empty)
                .Replace("?", string.Empty)
                .Replace(",", string.Empty)
                .Replace("?", string.Empty)
                .Replace("\\", string.Empty)
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .Trim();
        }

        private static bool TextEquals(string left, string right)
        {
            return string.Equals(NormalizeLayerMetadataText(left), NormalizeLayerMetadataText(right), StringComparison.CurrentCultureIgnoreCase);
        }

        private static bool IsEntityLayerUnavailable(Database db, Transaction tr, Entity entity)
        {
            if (db == null || tr == null || entity == null) return false;
            try
            {
                LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead, false) as LayerTable;
                if (lt == null || !lt.Has(entity.Layer)) return false;
                LayerTableRecord layer = tr.GetObject(lt[entity.Layer], OpenMode.ForRead, false) as LayerTableRecord;
                return layer != null && (layer.IsLocked || layer.IsFrozen || layer.IsOff);
            }
            catch
            {
                return false;
            }
        }

        private static bool HasPipeAttributes(Entity entity, Transaction tr)
        {
            return GetPipeAttributeRecord(entity, tr) != null;
        }

        private static bool HasPipeAttributeKey(Entity entity, Transaction tr, string keyName)
        {
            if (entity == null || tr == null || string.IsNullOrWhiteSpace(keyName)) return false;

            try
            {
                Xrecord record = GetPipeAttributeRecord(entity, tr);
                if (record == null || record.Data == null) return false;

                foreach (TypedValue value in record.Data)
                {
                    if (value.Value == null) continue;
                    string text = value.Value.ToString();
                    int index = text.IndexOf('=');
                    if (index <= 0) continue;

                    string key = text.Substring(0, index).Trim();
                    if (string.Equals(key, keyName, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool RemovePipeAttributes(Entity entity, Transaction tr)
        {
            if (entity == null || tr == null) return false;
            bool removed = false;
            try
            {
                if (!entity.ExtensionDictionary.IsNull)
                {
                    DBDictionary dict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForRead, false) as DBDictionary;
                    if (dict != null && dict.Contains(PipeAttributeXrecordName))
                    {
                        if (!dict.IsWriteEnabled) dict.UpgradeOpen();
                        ObjectId recordId = dict.GetAt(PipeAttributeXrecordName);
                        dict.Remove(PipeAttributeXrecordName);
                        EraseRecord(tr, recordId);
                        removed = true;
                    }
                }

                DBDictionary index = GetPipeAttributeIndex(entity.Database, tr, false);
                string key = entity.Handle.ToString();
                if (index != null && index.Contains(key))
                {
                    if (!index.IsWriteEnabled) index.UpgradeOpen();
                    ObjectId backupId = index.GetAt(key);
                    index.Remove(key);
                    EraseRecord(tr, backupId);
                    removed = true;
                }
                return removed;
            }
            catch
            {
                return removed;
            }
        }

        private static QuantityPipeAttributes ReadPipeAttributes(Entity entity, Transaction tr)
        {
            QuantityPipeAttributes attrs = QuantityPipeAttributes.Default.Clone();
            if (entity == null || tr == null) return attrs;

            try
            {
                Xrecord record = GetPipeAttributeRecord(entity, tr);
                if (record == null || record.Data == null) return attrs;

                bool hasDrawLengthWidthHeightFlag = false;

                foreach (TypedValue value in record.Data)
                {
                    if (value.Value == null) continue;
                    string text = value.Value.ToString();
                    int index = text.IndexOf('=');
                    if (index <= 0) continue;

                    string key = text.Substring(0, index).Trim();
                    string val = text.Substring(index + 1).Trim();
                    if (string.Equals(key, "DrawLengthWidthHeightAnnotation", StringComparison.OrdinalIgnoreCase))
                    {
                        hasDrawLengthWidthHeightFlag = true;
                    }
                    ApplyKeyValue(attrs, key, val);
                }

                if (!hasDrawLengthWidthHeightFlag)
                {
                    attrs.DrawLengthWidthHeightAnnotation = QuantityPipeAttributes.IsMainPipeKind(attrs.ObjectKind);
                }
            }
            catch
            {
                return attrs;
            }

            attrs.BackfillStructure = QuantityPipeAttributes.NormalizeStructureLayerText(attrs.BackfillStructure);
            QuantityPipeAttributes.ApplyStructureLayerText(attrs);
            return attrs;
        }

        private static void WritePipeAttributes(Entity entity, Transaction tr, QuantityPipeAttributes attrs)
        {
            if (entity == null || tr == null || attrs == null) return;
            if (!entity.IsWriteEnabled) entity.UpgradeOpen();
            if (entity.ExtensionDictionary.IsNull) entity.CreateExtensionDictionary();

            DBDictionary dict = (DBDictionary)tr.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite);
            Xrecord record = null;
            if (dict.Contains(PipeAttributeXrecordName))
            {
                record = tr.GetObject(dict.GetAt(PipeAttributeXrecordName), OpenMode.ForWrite, false) as Xrecord;
            }
            else
            {
                record = new Xrecord();
                dict.SetAt(PipeAttributeXrecordName, record);
                tr.AddNewlyCreatedDBObject(record, true);
            }

            if (record == null) return;

            attrs.BackfillStructure = QuantityPipeAttributes.NormalizeStructureLayerText(attrs.BackfillStructure);
            QuantityPipeAttributes.ApplyStructureLayerText(attrs);

            record.Data = new ResultBuffer(
                Pair("SchemaVersion", QuantityPipeAttributes.SchemaVersion),
                Pair("Enabled", attrs.Enabled),
                Pair("ObjectKind", attrs.ObjectKind),
                Pair("IsSpecialObject", attrs.IsSpecialObject),
                Pair("LayerParentGroup", attrs.LayerParentGroup),
                Pair("LayerParentClass", attrs.LayerParentClass),
                Pair("LayerTags", attrs.LayerTags),
                Pair("Material", attrs.Material),
                Pair("Diameter", attrs.Diameter),
                Pair("UseManualLength", attrs.UseManualLength),
                Pair("ManualLength", attrs.ManualLength),
                Pair("DrawLengthWidthHeightAnnotation", attrs.DrawLengthWidthHeightAnnotation),
                Pair("StartNode", attrs.StartNode),
                Pair("EndNode", attrs.EndNode),
                Pair("StartDepth", attrs.StartDepth),
                Pair("EndDepth", attrs.EndDepth),
                Pair("AverageDepth", attrs.AverageDepth),
                Pair("TrenchWidth", attrs.TrenchWidth),
                Pair("RoadThickness", attrs.RoadThickness),
                Pair("ExcavationType", attrs.ExcavationType),
                Pair("BackfillType", attrs.BackfillType),
                Pair("BackfillStructure", attrs.BackfillStructure),
                Pair("BranchType", attrs.BranchType),
                Pair("BranchIncludeInCalculation", attrs.BranchIncludeInCalculation),
                Pair("BranchDepth", attrs.BranchDepth),
                Pair("NodeNo", attrs.NodeNo),
                Pair("WellSpec", attrs.WellSpec),
                Pair("WellCoverMaterial", attrs.WellCoverMaterial),
                Pair("WellMaterialType", attrs.WellMaterialType),
                Pair("WellType", attrs.WellType),
                Pair("SiltWellDeductDepth500", attrs.SiltWellDeductDepth500),
                Pair("SiltWellDeductDepth700", attrs.SiltWellDeductDepth700),
                Pair("GroundElevation", attrs.GroundElevation),
                Pair("WellDepth", attrs.WellDepth),
                Pair("ShaftLength", attrs.ShaftLength),
                Pair("ExcavationLength", attrs.ExcavationLength),
                Pair("ExcavationWidth", attrs.ExcavationWidth),
                Pair("CoverPlate", attrs.CoverPlate),
                Pair("SandCushionThickness", attrs.SandCushionThickness),
                Pair("GravelCushionThickness", attrs.GravelCushionThickness),
                Pair("C25RestoreThickness", attrs.C25RestoreThickness),
                Pair("PipeOuterDiameter", attrs.PipeOuterDiameter),
                Pair("DeductPipeVolume", attrs.DeductPipeVolume),
                Pair("Remark", attrs.Remark),
                Pair("LastModified", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
            WritePipeAttributeBackup(entity, tr, record.Data);
        }

        private static Xrecord GetPipeAttributeRecord(Entity entity, Transaction tr)
        {
            if (entity == null || tr == null) return null;
            try
            {
                if (!entity.ExtensionDictionary.IsNull)
                {
                    DBDictionary dict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForRead, false) as DBDictionary;
                    if (dict != null && dict.Contains(PipeAttributeXrecordName))
                    {
                        Xrecord primary = tr.GetObject(dict.GetAt(PipeAttributeXrecordName), OpenMode.ForRead, false) as Xrecord;
                        if (primary != null && primary.Data != null) return primary;
                    }
                }

                DBDictionary index = GetPipeAttributeIndex(entity.Database, tr, false);
                string key = entity.Handle.ToString();
                return index != null && index.Contains(key)
                    ? tr.GetObject(index.GetAt(key), OpenMode.ForRead, false) as Xrecord
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static void WritePipeAttributeBackup(Entity entity, Transaction tr, ResultBuffer data)
        {
            if (entity == null || tr == null || data == null) return;
            DBDictionary index = GetPipeAttributeIndex(entity.Database, tr, true);
            if (index == null) return;
            string key = entity.Handle.ToString();
            Xrecord record;
            if (index.Contains(key))
            {
                record = tr.GetObject(index.GetAt(key), OpenMode.ForWrite, false) as Xrecord;
            }
            else
            {
                if (!index.IsWriteEnabled) index.UpgradeOpen();
                record = new Xrecord();
                index.SetAt(key, record);
                tr.AddNewlyCreatedDBObject(record, true);
            }
            if (record != null) record.Data = new ResultBuffer(data.AsArray());
        }

        private static DBDictionary GetPipeAttributeIndex(Database db, Transaction tr, bool create)
        {
            if (db == null || tr == null) return null;
            DBDictionary nod = tr.GetObject(db.NamedObjectsDictionaryId, create ? OpenMode.ForWrite : OpenMode.ForRead, false) as DBDictionary;
            if (nod == null) return null;
            if (nod.Contains(PipeAttributeIndexDictionaryName))
                return tr.GetObject(nod.GetAt(PipeAttributeIndexDictionaryName), create ? OpenMode.ForWrite : OpenMode.ForRead, false) as DBDictionary;
            if (!create) return null;
            var index = new DBDictionary();
            nod.SetAt(PipeAttributeIndexDictionaryName, index);
            tr.AddNewlyCreatedDBObject(index, true);
            return index;
        }

        private static void EraseRecord(Transaction tr, ObjectId id)
        {
            try
            {
                DBObject obj = tr.GetObject(id, OpenMode.ForWrite, false);
                if (obj != null && !obj.IsErased) obj.Erase();
            }
            catch
            {
            }
        }

        private static TypedValue Pair(string key, string value)
        {
            return new TypedValue((int)DxfCode.Text, key + "=" + (value ?? string.Empty));
        }

        private static TypedValue Pair(string key, bool value)
        {
            return Pair(key, value ? "true" : "false");
        }

        private static TypedValue Pair(string key, double value)
        {
            return Pair(key, value.ToString("0.########", CultureInfo.InvariantCulture));
        }

        private static void ApplyKeyValue(QuantityPipeAttributes attrs, string key, string val)
        {
            if (attrs == null || string.IsNullOrWhiteSpace(key)) return;
            if (string.Equals(key, "Enabled", StringComparison.OrdinalIgnoreCase)) attrs.Enabled = QuantityPipeAttributes.ParseBool(val, attrs.Enabled);
            else if (string.Equals(key, "ObjectKind", StringComparison.OrdinalIgnoreCase)) attrs.ObjectKind = val;
            else if (string.Equals(key, "IsSpecialObject", StringComparison.OrdinalIgnoreCase)) attrs.IsSpecialObject = QuantityPipeAttributes.ParseBool(val, attrs.IsSpecialObject);
            else if (string.Equals(key, "LayerParentGroup", StringComparison.OrdinalIgnoreCase)) attrs.LayerParentGroup = val;
            else if (string.Equals(key, "LayerParentClass", StringComparison.OrdinalIgnoreCase)) attrs.LayerParentClass = val;
            else if (string.Equals(key, "LayerTags", StringComparison.OrdinalIgnoreCase)) attrs.LayerTags = val;
            else if (string.Equals(key, "Material", StringComparison.OrdinalIgnoreCase)) attrs.Material = val;
            else if (string.Equals(key, "Diameter", StringComparison.OrdinalIgnoreCase)) attrs.Diameter = val;
            else if (string.Equals(key, "UseManualLength", StringComparison.OrdinalIgnoreCase)) attrs.UseManualLength = QuantityPipeAttributes.ParseBool(val, attrs.UseManualLength);
            else if (string.Equals(key, "ManualLength", StringComparison.OrdinalIgnoreCase)) attrs.ManualLength = QuantityPipeAttributes.ParseDouble(val, attrs.ManualLength);
            else if (string.Equals(key, "DrawLengthWidthHeightAnnotation", StringComparison.OrdinalIgnoreCase)) attrs.DrawLengthWidthHeightAnnotation = QuantityPipeAttributes.ParseBool(val, attrs.DrawLengthWidthHeightAnnotation);
            else if (string.Equals(key, "StartNode", StringComparison.OrdinalIgnoreCase)) attrs.StartNode = val;
            else if (string.Equals(key, "EndNode", StringComparison.OrdinalIgnoreCase)) attrs.EndNode = val;
            else if (string.Equals(key, "StartDepth", StringComparison.OrdinalIgnoreCase)) attrs.StartDepth = QuantityPipeAttributes.ParseDouble(val, attrs.StartDepth);
            else if (string.Equals(key, "EndDepth", StringComparison.OrdinalIgnoreCase)) attrs.EndDepth = QuantityPipeAttributes.ParseDouble(val, attrs.EndDepth);
            else if (string.Equals(key, "AverageDepth", StringComparison.OrdinalIgnoreCase)) attrs.AverageDepth = QuantityPipeAttributes.ParseDouble(val, attrs.AverageDepth);
            else if (string.Equals(key, "TrenchWidth", StringComparison.OrdinalIgnoreCase)) attrs.TrenchWidth = QuantityPipeAttributes.ParseDouble(val, attrs.TrenchWidth);
            else if (string.Equals(key, "RoadThickness", StringComparison.OrdinalIgnoreCase)) attrs.RoadThickness = QuantityPipeAttributes.ParseDouble(val, attrs.RoadThickness);
            else if (string.Equals(key, "ExcavationType", StringComparison.OrdinalIgnoreCase)) attrs.ExcavationType = val;
            else if (string.Equals(key, "BackfillType", StringComparison.OrdinalIgnoreCase)) attrs.BackfillType = val;
            else if (string.Equals(key, "BackfillStructure", StringComparison.OrdinalIgnoreCase)) attrs.BackfillStructure = val;
            else if (string.Equals(key, "BranchType", StringComparison.OrdinalIgnoreCase)) attrs.BranchType = val;
            else if (string.Equals(key, "BranchIncludeInCalculation", StringComparison.OrdinalIgnoreCase)) attrs.BranchIncludeInCalculation = QuantityPipeAttributes.ParseBool(val, attrs.BranchIncludeInCalculation);
            else if (string.Equals(key, "BranchDepth", StringComparison.OrdinalIgnoreCase)) attrs.BranchDepth = QuantityPipeAttributes.ParseDouble(val, attrs.BranchDepth);
            else if (string.Equals(key, "NodeNo", StringComparison.OrdinalIgnoreCase)) attrs.NodeNo = val;
            else if (string.Equals(key, "WellSpec", StringComparison.OrdinalIgnoreCase)) attrs.WellSpec = val;
            else if (string.Equals(key, "WellCoverMaterial", StringComparison.OrdinalIgnoreCase)) attrs.WellCoverMaterial = val;
            else if (string.Equals(key, "WellMaterialType", StringComparison.OrdinalIgnoreCase)) attrs.WellMaterialType = val;
            else if (string.Equals(key, "WellType", StringComparison.OrdinalIgnoreCase)) attrs.WellType = val;
            else if (string.Equals(key, "SiltWellDeductDepth500", StringComparison.OrdinalIgnoreCase)) attrs.SiltWellDeductDepth500 = QuantityPipeAttributes.ParseDouble(val, attrs.SiltWellDeductDepth500);
            else if (string.Equals(key, "SiltWellDeductDepth700", StringComparison.OrdinalIgnoreCase)) attrs.SiltWellDeductDepth700 = QuantityPipeAttributes.ParseDouble(val, attrs.SiltWellDeductDepth700);
            else if (string.Equals(key, "GroundElevation", StringComparison.OrdinalIgnoreCase)) attrs.GroundElevation = QuantityPipeAttributes.ParseDouble(val, attrs.GroundElevation);
            else if (string.Equals(key, "WellDepth", StringComparison.OrdinalIgnoreCase)) attrs.WellDepth = QuantityPipeAttributes.ParseDouble(val, attrs.WellDepth);
            else if (string.Equals(key, "ShaftLength", StringComparison.OrdinalIgnoreCase)) attrs.ShaftLength = QuantityPipeAttributes.ParseDouble(val, attrs.ShaftLength);
            else if (string.Equals(key, "ExcavationLength", StringComparison.OrdinalIgnoreCase)) attrs.ExcavationLength = QuantityPipeAttributes.ParseDouble(val, attrs.ExcavationLength);
            else if (string.Equals(key, "ExcavationWidth", StringComparison.OrdinalIgnoreCase)) attrs.ExcavationWidth = QuantityPipeAttributes.ParseDouble(val, attrs.ExcavationWidth);
            else if (string.Equals(key, "CoverPlate", StringComparison.OrdinalIgnoreCase)) attrs.CoverPlate = val;
            else if (string.Equals(key, "SandCushionThickness", StringComparison.OrdinalIgnoreCase)) attrs.SandCushionThickness = QuantityPipeAttributes.ParseDouble(val, attrs.SandCushionThickness);
            else if (string.Equals(key, "GravelCushionThickness", StringComparison.OrdinalIgnoreCase)) attrs.GravelCushionThickness = QuantityPipeAttributes.ParseDouble(val, attrs.GravelCushionThickness);
            else if (string.Equals(key, "C25RestoreThickness", StringComparison.OrdinalIgnoreCase)) attrs.C25RestoreThickness = QuantityPipeAttributes.ParseDouble(val, attrs.C25RestoreThickness);
            else if (string.Equals(key, "PipeOuterDiameter", StringComparison.OrdinalIgnoreCase)) attrs.PipeOuterDiameter = QuantityPipeAttributes.ParseDouble(val, attrs.PipeOuterDiameter);
            else if (string.Equals(key, "DeductPipeVolume", StringComparison.OrdinalIgnoreCase)) attrs.DeductPipeVolume = QuantityPipeAttributes.ParseBool(val, attrs.DeductPipeVolume);
            else if (string.Equals(key, "Remark", StringComparison.OrdinalIgnoreCase)) attrs.Remark = val;
        }

        private static double GetCurveLength(Curve curve)
        {
            if (curve == null) return 0.0;
            try
            {
                return Math.Abs(curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam));
            }
            catch
            {
                try
                {
                    Autodesk.AutoCAD.DatabaseServices.Polyline pl = curve as Autodesk.AutoCAD.DatabaseServices.Polyline;
                    if (pl != null) return pl.Length;
                }
                catch
                {
                }
            }
            return 0.0;
        }

        private static void TryFillConnectedNodeInfo(Database db, Transaction tr, Entity pipeEntity, Curve pipeCurve, QuantityPipeAttributes attrs)
        {
            TryFillConnectedNodeInfo(db, tr, pipeEntity, pipeCurve, attrs, false);
        }

        private static void TryFillConnectedNodeInfo(Database db, Transaction tr, Entity pipeEntity, Curve pipeCurve, QuantityPipeAttributes attrs, bool overwriteExisting)
        {
            if (db == null || tr == null || pipeEntity == null || pipeCurve == null || attrs == null) return;
            try
            {
                Point3d start = pipeCurve.GetPointAtParameter(pipeCurve.StartParam);
                Point3d end = pipeCurve.GetPointAtParameter(pipeCurve.EndParam);

                ObjectId spaceId = pipeEntity.OwnerId.IsNull ? db.CurrentSpaceId : pipeEntity.OwnerId;
                BlockTableRecord space = tr.GetObject(spaceId, OpenMode.ForRead, false) as BlockTableRecord;
                if (space == null) return;

                NodeCandidate startNode = null;
                NodeCandidate endNode = null;
                foreach (ObjectId id in space)
                {
                    if (id == pipeEntity.ObjectId) continue;
                    Entity entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity == null) continue;

                    // ?????????????/????????????????????
                    if (!IsWellParentLayer(db, tr, entity)) continue;

                    Point3d connectionPoint;
                    bool hasConnection = TryGetPipeNodeIntersectionPoint(pipeCurve, entity, start, end, out connectionPoint);
                    double dStart;
                    double dEnd;
                    double matchLimit = GetNodeEndpointMatchLimit(entity);

                    if (hasConnection)
                    {
                        dStart = Distance2d(connectionPoint, start);
                        dEnd = Distance2d(connectionPoint, end);
                    }
                    else
                    {
                        // ??????????????????????????????/????? IntersectWith ????
                        Point3d center;
                        double diagonal;
                        if (!TryGetEntityCenter(entity, out center, out diagonal)) continue;
                        dStart = Distance2d(center, start);
                        dEnd = Distance2d(center, end);
                        matchLimit = Math.Max(matchLimit, Math.Max(0.80, diagonal * 1.50));
                    }

                    if (Math.Min(dStart, dEnd) > matchLimit) continue;

                    bool candidateHasSavedAttributes = HasPipeAttributes(entity, tr);
                    QuantityPipeAttributes nodeAttrs = candidateHasSavedAttributes
                        ? ReadPipeAttributes(entity, tr)
                        : BuildDefaultAttributesFromEntity(db, tr, entity, QuantityPipeAttributes.KindNodeWell);
                    nodeAttrs.ObjectKind = QuantityPipeAttributes.KindNodeWell;
                    if (string.IsNullOrWhiteSpace(nodeAttrs.NodeNo)) nodeAttrs.NodeNo = ExtractSimpleNodeNoFromEntity(entity);

                    if (dStart <= dEnd)
                    {
                        if (IsBetterNodeCandidate(startNode, nodeAttrs, dStart, candidateHasSavedAttributes))
                        {
                            startNode = new NodeCandidate(nodeAttrs, dStart, candidateHasSavedAttributes);
                        }
                    }
                    else
                    {
                        if (IsBetterNodeCandidate(endNode, nodeAttrs, dEnd, candidateHasSavedAttributes))
                        {
                            endNode = new NodeCandidate(nodeAttrs, dEnd, candidateHasSavedAttributes);
                        }
                    }
                }

                ApplyNodeToPipeStart(attrs, startNode, overwriteExisting);
                ApplyNodeToPipeEnd(attrs, endNode, overwriteExisting);
                RecalculateMainPipeAverageDepth(attrs);
            }
            catch
            {
            }
        }

        private static bool IsWellParentLayer(Database db, Transaction tr, Entity entity)
        {
            if (entity == null) return false;
            if (!IsSupportedNodeGeometry(entity)) return false;

            LayerMetadata meta = GetLayerMetadataSafe(db, tr, entity.Layer);
            string parent = meta == null ? string.Empty : (meta.ParentGroup ?? string.Empty);
            string cls = meta == null ? string.Empty : (meta.ParentClass ?? string.Empty);

            // ????????????????=????=???????
            return TextEquals(parent, "?") && IsSupportedWellClass(cls);
        }

        private static bool IsSupportedNodeGeometry(Entity entity)
        {
            if (entity == null) return false;
            if (entity is Circle || entity is BlockReference || entity is DBPoint || entity is Ellipse) return true;
            if (entity is Autodesk.AutoCAD.DatabaseServices.Polyline) return true;
            if (entity is Polyline2d || entity is Polyline3d) return true;
            if (entity is Arc) return true;
            return false;
        }

        private static bool TryGetPipeNodeIntersectionPoint(Curve pipeCurve, Entity nodeEntity, Point3d pipeStart, Point3d pipeEnd, out Point3d point)
        {
            point = Point3d.Origin;
            if (pipeCurve == null || nodeEntity == null) return false;

            // ???????? + ???????????????????/??????
            // ????????????? AutoCAD IntersectWith ???
            // ????????????????????????????
            if (IsPointInsideOrOnNodeEntity(pipeStart, nodeEntity))
            {
                point = pipeStart;
                return true;
            }
            if (IsPointInsideOrOnNodeEntity(pipeEnd, nodeEntity))
            {
                point = pipeEnd;
                return true;
            }

            try
            {
                var points = new Point3dCollection();
                pipeCurve.IntersectWith(nodeEntity, Intersect.OnBothOperands, points, IntPtr.Zero, IntPtr.Zero);
                if (points.Count > 0)
                {
                    point = ChooseIntersectionClosestToPipeEnd(points, pipeStart, pipeEnd);
                    return true;
                }
            }
            catch
            {
            }

            // ? DBPoint????????????????????????????
            Point3d nodePosition;
            if (TryGetNodePosition(nodeEntity, out nodePosition))
            {
                double tolerance = GetNodeConnectionTolerance(nodeEntity);
                if (Distance2d(pipeStart, nodePosition) <= tolerance)
                {
                    point = pipeStart;
                    return true;
                }
                if (Distance2d(pipeEnd, nodePosition) <= tolerance)
                {
                    point = pipeEnd;
                    return true;
                }
            }

            return false;
        }

        private static Point3d ChooseIntersectionClosestToPipeEnd(Point3dCollection points, Point3d pipeStart, Point3d pipeEnd)
        {
            if (points == null || points.Count == 0) return Point3d.Origin;

            Point3d best = points[0];
            double bestScore = Math.Min(best.DistanceTo(pipeStart), best.DistanceTo(pipeEnd));
            for (int i = 1; i < points.Count; i++)
            {
                Point3d p = points[i];
                double score = Math.Min(p.DistanceTo(pipeStart), p.DistanceTo(pipeEnd));
                if (score < bestScore)
                {
                    best = p;
                    bestScore = score;
                }
            }
            return best;
        }

        private static bool IsPointInsideOrOnNodeEntity(Point3d point, Entity nodeEntity)
        {
            if (nodeEntity == null) return false;

            double tolerance = GetNodeConnectionTolerance(nodeEntity);
            try
            {
                Circle circle = nodeEntity as Circle;
                if (circle != null)
                {
                    return Distance2d(point, circle.Center) <= circle.Radius + tolerance;
                }

                DBPoint dbPoint = nodeEntity as DBPoint;
                if (dbPoint != null)
                {
                    return Distance2d(point, dbPoint.Position) <= tolerance;
                }

                BlockReference block = nodeEntity as BlockReference;
                if (block != null)
                {
                    if (IsPointInsideExtents2d(point, block.GeometricExtents, tolerance)) return true;

                    double radius = EstimateBlockReferenceRadius2d(block);
                    if (radius > 0 && Distance2d(point, block.Position) <= radius + tolerance) return true;
                    return Distance2d(point, block.Position) <= tolerance;
                }

                if (IsPointInsideExtents2d(point, nodeEntity.GeometricExtents, tolerance)) return true;
            }
            catch
            {
            }

            return false;
        }

        private static bool IsPointInsideExtents2d(Point3d point, Extents3d extents, double tolerance)
        {
            // ????????????? Z??????? Z=0?
            // ??????????? XY???? Z?
            Point3d min = extents.MinPoint;
            Point3d max = extents.MaxPoint;
            return point.X >= min.X - tolerance && point.X <= max.X + tolerance
                && point.Y >= min.Y - tolerance && point.Y <= max.Y + tolerance;
        }

        private static double Distance2d(Point3d a, Point3d b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double GetExtentsDiagonal2d(Extents3d extents)
        {
            double dx = extents.MaxPoint.X - extents.MinPoint.X;
            double dy = extents.MaxPoint.Y - extents.MinPoint.Y;
            if (dx < 0) dx = -dx;
            if (dy < 0) dy = -dy;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double EstimateBlockReferenceRadius2d(BlockReference block)
        {
            if (block == null) return 0.0;

            try
            {
                Extents3d ext = block.GeometricExtents;
                double width = Math.Abs(ext.MaxPoint.X - ext.MinPoint.X);
                double height = Math.Abs(ext.MaxPoint.Y - ext.MinPoint.Y);
                double radius = Math.Max(width, height) / 2.0;
                if (radius > 0) return radius;
            }
            catch
            {
            }

            try
            {
                double sx = Math.Abs(block.ScaleFactors.X);
                double sy = Math.Abs(block.ScaleFactors.Y);
                double scale = Math.Max(sx, sy);
                if (scale > 0) return scale * 2.0;
            }
            catch
            {
            }

            return 0.0;
        }

        private static double GetNodeConnectionTolerance(Entity entity)
        {
            // ??????????????/???????????????
            // ??????????????????????????????
            double tolerance = 0.05;
            try
            {
                Extents3d ext = entity.GeometricExtents;
                double diagonal = GetExtentsDiagonal2d(ext);
                if (diagonal > 0)
                {
                    tolerance = Math.Max(tolerance, Math.Min(0.50, diagonal * 0.03));
                }
            }
            catch
            {
            }
            return tolerance;
        }

        private static double GetNodeEndpointMatchLimit(Entity entity)
        {
            // ???????/????????????????????????????
            // ?????????????????????????
            double limit = 0.5;
            try
            {
                Extents3d ext = entity.GeometricExtents;
                double diagonal = GetExtentsDiagonal2d(ext);
                if (diagonal > 0) limit = Math.Max(limit, diagonal);
            }
            catch
            {
            }
            return limit;
        }

        private static bool IsBetterNodeCandidate(NodeCandidate current, QuantityPipeAttributes candidate, double distance, bool hasSavedAttributes)
        {
            if (candidate == null) return false;
            if (current == null) return true;

            // ????????????????????????????????????
            // ?????????????????????
            if (hasSavedAttributes != current.HasSavedAttributes) return hasSavedAttributes;

            bool candidateHasIdentity = !string.IsNullOrWhiteSpace(candidate.NodeNo) || candidate.WellDepth > 0;
            bool currentHasIdentity = current.Attributes != null
                && (!string.IsNullOrWhiteSpace(current.Attributes.NodeNo) || current.Attributes.WellDepth > 0);
            if (candidateHasIdentity != currentHasIdentity) return candidateHasIdentity;

            return distance < current.Distance;
        }

        private static NodeCandidate FindNearestNodeCandidate(Database db, Transaction tr, ObjectId spaceId, Point3d pickPoint)
        {
            return FindNearestNodeCandidate(CollectNodeCandidates(db, tr, spaceId), pickPoint);
        }

        private static NodeCandidate FindNearestNodeCandidate(List<NodeCandidate> candidates, Point3d pickPoint)
        {
            if (candidates == null || candidates.Count == 0) return null;
            NodeCandidate best = null;
            foreach (NodeCandidate candidate in candidates)
            {
                if (candidate == null || candidate.Attributes == null) continue;
                double distance = Distance2d(candidate.Center, pickPoint);
                var withDistance = new NodeCandidate(candidate.Attributes, distance, candidate.HasSavedAttributes, candidate.Center);
                if (IsBetterNodeCandidate(best, candidate.Attributes, distance, candidate.HasSavedAttributes)) best = withDistance;
            }
            return best;
        }

        private static List<NodeCandidate> CollectNodeCandidates(Database db, Transaction tr, ObjectId spaceId)
        {
            var candidates = new List<NodeCandidate>();
            var textRefs = new List<TextReference>();
            if (db == null || tr == null) return candidates;

            BlockTableRecord space = tr.GetObject(spaceId.IsNull ? db.CurrentSpaceId : spaceId, OpenMode.ForRead, false) as BlockTableRecord;
            if (space == null) return candidates;

            foreach (ObjectId id in space)
            {
                Entity entity = null;
                try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                catch { continue; }
                if (entity == null) continue;

                TextReference textRef;
                if (TryCreateNodeTextReference(entity, out textRef)) textRefs.Add(textRef);
            }

            foreach (ObjectId id in space)
            {
                Entity entity = null;
                try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                catch { continue; }
                if (entity == null) continue;
                if (!IsWellParentLayer(db, tr, entity)) continue;

                Point3d center;
                double diagonal;
                if (!TryGetEntityCenter(entity, out center, out diagonal)) continue;

                bool hasSaved = HasPipeAttributes(entity, tr);
                QuantityPipeAttributes nodeAttrs = hasSaved
                    ? ReadPipeAttributes(entity, tr)
                    : BuildDefaultAttributesFromEntity(db, tr, entity, QuantityPipeAttributes.KindNodeWell);
                nodeAttrs.ObjectKind = QuantityPipeAttributes.KindNodeWell;
                if (string.IsNullOrWhiteSpace(nodeAttrs.NodeNo)) nodeAttrs.NodeNo = ExtractSimpleNodeNoFromEntity(entity);

                candidates.Add(new NodeCandidate(nodeAttrs, 0.0, hasSaved, center));
            }

            FillMissingNodeNumbers(candidates, textRefs);
            return candidates;
        }

        private static bool TryCreateNodeTextReference(Entity entity, out TextReference reference)
        {
            reference = null;
            if (entity == null) return false;

            string text = string.Empty;
            Point3d position = Point3d.Origin;

            DBText dbText = entity as DBText;
            if (dbText != null)
            {
                text = dbText.TextString;
                position = dbText.Position;
            }

            MText mText = entity as MText;
            if (mText != null)
            {
                text = mText.Text;
                position = mText.Location;
            }

            text = CleanSimpleNodeNo(text);
            if (!IsLikelyNodeNoText(text)) return false;

            reference = new TextReference { Text = text, Position = position };
            return true;
        }

        private static void FillMissingNodeNumbers(List<NodeCandidate> candidates, List<TextReference> textRefs)
        {
            if (candidates == null || textRefs == null || textRefs.Count == 0) return;

            foreach (NodeCandidate candidate in candidates)
            {
                if (candidate == null || candidate.Attributes == null) continue;
                if (!string.IsNullOrWhiteSpace(candidate.Attributes.NodeNo)) continue;

                TextReference best = null;
                double bestDist = double.MaxValue;
                foreach (TextReference text in textRefs)
                {
                    if (text == null) continue;
                    double dist = Distance2d(candidate.Center, text.Position);
                    if (dist > 10.0) continue;
                    if (dist < bestDist)
                    {
                        best = text;
                        bestDist = dist;
                    }
                }

                if (best != null) candidate.Attributes.NodeNo = best.Text;
            }
        }

        private static bool IsLikelyNodeNoText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return Regex.IsMatch(text.Trim(), @"^[A-Za-z?-?]*\d+(?:[-?]\d+)?$", RegexOptions.IgnoreCase);
        }

        private static bool TryGetEntityCenter(Entity entity, out Point3d center, out double diagonal)
        {
            center = Point3d.Origin;
            diagonal = 0.0;
            if (entity == null) return false;
            try
            {
                Extents3d ext = entity.GeometricExtents;
                center = new Point3d((ext.MinPoint.X + ext.MaxPoint.X) / 2.0, (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0, (ext.MinPoint.Z + ext.MaxPoint.Z) / 2.0);
                diagonal = GetExtentsDiagonal2d(ext);
                return true;
            }
            catch
            {
            }
            if (TryGetNodePosition(entity, out center))
            {
                diagonal = 0.0;
                return true;
            }
            return false;
        }

        private static string ExtractSimpleNodeNoFromEntity(Entity entity)
        {
            DBText text = entity as DBText;
            if (text != null) return CleanSimpleNodeNo(text.TextString);
            MText mtext = entity as MText;
            if (mtext != null) return CleanSimpleNodeNo(mtext.Text);
            return string.Empty;
        }

        private static string CleanSimpleNodeNo(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string value = Regex.Replace(text, @"\\[A-Za-z0-9]+;", string.Empty);
            value = value.Replace("{", string.Empty).Replace("}", string.Empty).Trim();
            Match m = Regex.Match(value, @"[A-Za-z]*\d+(?:[-?]\d+)?", RegexOptions.IgnoreCase);
            return m.Success ? m.Value : value;
        }

        private static double GetPipeCushionHeight(QuantityPipeAttributes attrs)
        {
            if (attrs == null) return 0.0;
            List<QuantityStructureLayer> layers = QuantityStructureLayer.Parse(attrs.BackfillStructure);
            double height = QuantityStructureLayer.ResolvePipeCushionHeight(layers, attrs.SandCushionThickness);
            return height < 0 ? 0.0 : height;
        }

        private static void RecalculateMainPipeAverageDepth(QuantityPipeAttributes attrs)
        {
            if (attrs == null) return;
            if (!QuantityPipeAttributes.IsMainPipeKind(attrs.ObjectKind)) return;

            // v22??????????????????? + ????????
            // ??????????????????
            // ???????????????????????????????
            // ?????????????
            double startExcavationDepth = attrs.StartDepth > 0 ? attrs.StartDepth : 0.0;
            double endExcavationDepth = attrs.EndDepth > 0 ? attrs.EndDepth : 0.0;

            if (startExcavationDepth > 0 && endExcavationDepth > 0) attrs.AverageDepth = RoundForAttributeEditor((startExcavationDepth + endExcavationDepth) / 2.0);
            else if (startExcavationDepth > 0) attrs.AverageDepth = RoundForAttributeEditor(startExcavationDepth);
            else if (endExcavationDepth > 0) attrs.AverageDepth = RoundForAttributeEditor(endExcavationDepth);
            else attrs.AverageDepth = 0.0;
        }

        private static double RoundForAttributeEditor(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0.0;
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static double CalculatePipeEndpointDepthFromNode(QuantityPipeAttributes pipeAttrs, QuantityPipeAttributes nodeAttrs)
        {
            if (nodeAttrs == null) return 0.0;
            double pipeCushion = GetPipeCushionHeight(pipeAttrs);
            double fallback = nodeAttrs.WellDepth > 0 ? nodeAttrs.WellDepth + Math.Max(pipeCushion, 0.0) : 0.0;
            return QuantityPipeAttributes.CalculatePipeExcavationDepthByWell(nodeAttrs, pipeCushion, fallback);
        }

        private static void ApplyNodeToPipeStart(QuantityPipeAttributes attrs, NodeCandidate node)
        {
            ApplyNodeToPipeStart(attrs, node, false);
        }

        private static void ApplyNodeToPipeStart(QuantityPipeAttributes attrs, NodeCandidate node, bool overwrite)
        {
            if (attrs == null || node == null || node.Attributes == null) return;
            if ((overwrite || string.IsNullOrWhiteSpace(attrs.StartNode)) && !string.IsNullOrWhiteSpace(node.Attributes.NodeNo)) attrs.StartNode = node.Attributes.NodeNo;
            if ((overwrite || attrs.StartDepth <= 0) && node.Attributes.WellDepth > 0)
            {
                attrs.StartDepth = CalculatePipeEndpointDepthFromNode(attrs, node.Attributes);
            }
        }

        private static void ApplyNodeToPipeEnd(QuantityPipeAttributes attrs, NodeCandidate node)
        {
            ApplyNodeToPipeEnd(attrs, node, false);
        }

        private static void ApplyNodeToPipeEnd(QuantityPipeAttributes attrs, NodeCandidate node, bool overwrite)
        {
            if (attrs == null || node == null || node.Attributes == null) return;
            if ((overwrite || string.IsNullOrWhiteSpace(attrs.EndNode)) && !string.IsNullOrWhiteSpace(node.Attributes.NodeNo)) attrs.EndNode = node.Attributes.NodeNo;
            if ((overwrite || attrs.EndDepth <= 0) && node.Attributes.WellDepth > 0)
            {
                attrs.EndDepth = CalculatePipeEndpointDepthFromNode(attrs, node.Attributes);
            }
        }

        private sealed class NodeCandidate
        {
            public QuantityPipeAttributes Attributes { get; private set; }
            public double Distance { get; private set; }
            public bool HasSavedAttributes { get; private set; }
            public Point3d Center { get; private set; }

            public NodeCandidate(QuantityPipeAttributes attrs, double distance, bool hasSavedAttributes)
                : this(attrs, distance, hasSavedAttributes, Point3d.Origin)
            {
            }

            public NodeCandidate(QuantityPipeAttributes attrs, double distance, bool hasSavedAttributes, Point3d center)
            {
                Attributes = attrs;
                Distance = distance;
                HasSavedAttributes = hasSavedAttributes;
                Center = center;
            }
        }

        private sealed class QuantityNodeSelectPreviewJig : DrawJig
        {
            private const double DuplicateTolerance = 0.001;
            private readonly List<NodeCandidate> _candidates;
            private readonly bool _forStart;
            private readonly ObjectId _textStyleId;
            private Point3d _pickPoint;
            private NodeCandidate _selectedCandidate;

            public QuantityNodeSelectPreviewJig(List<NodeCandidate> candidates, bool forStart, ObjectId textStyleId)
            {
                _candidates = candidates ?? new List<NodeCandidate>();
                _forStart = forStart;
                _textStyleId = textStyleId;
                _pickPoint = _candidates.Count > 0 ? _candidates[0].Center : Point3d.Origin;
                _selectedCandidate = FindNearestNodeCandidate(_candidates, _pickPoint);
            }

            public Point3d PickPoint
            {
                get { return _pickPoint; }
            }

            public NodeCandidate SelectedCandidate
            {
                get { return _selectedCandidate; }
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var options = new JigPromptPointOptions("\n ");
                options.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NoZeroResponseAccepted;
                PromptPointResult result = prompts.AcquirePoint(options);
                if (result.Status != PromptStatus.OK) return SamplerStatus.Cancel;

                if (result.Value.DistanceTo(_pickPoint) < DuplicateTolerance) return SamplerStatus.NoChange;

                _pickPoint = result.Value;
                _selectedCandidate = FindNearestNodeCandidate(_candidates, _pickPoint);
                return SamplerStatus.OK;
            }

            protected override bool WorldDraw(WorldDraw draw)
            {
                if (draw == null || draw.Geometry == null) return true;
                _selectedCandidate = FindNearestNodeCandidate(_candidates, _pickPoint);
                if (_selectedCandidate == null) return true;

                using (var leader = new Autodesk.AutoCAD.DatabaseServices.Polyline())
                {
                    leader.AddVertexAt(0, new Point2d(_selectedCandidate.Center.X, _selectedCandidate.Center.Y), 0, 0, 0);
                    leader.AddVertexAt(1, new Point2d(_pickPoint.X, _pickPoint.Y), 0, 0, 0);
                    leader.ColorIndex = 1;
                    draw.Geometry.Draw(leader);
                }

                string nodeNo = _selectedCandidate.Attributes == null ? string.Empty : (_selectedCandidate.Attributes.NodeNo ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(nodeNo)) DrawPreviewText(draw, _pickPoint, nodeNo, 1.0, 1, _textStyleId);

                return true;
            }

            private static void DrawPreviewText(WorldDraw draw, Point3d position, string text, double textHeight, short colorIndex, ObjectId textStyleId)
            {
                if (draw == null || draw.Geometry == null || string.IsNullOrWhiteSpace(text)) return;
                try
                {
                    using (var dbText = new DBText())
                    {
                        dbText.Height = textHeight <= 0 ? 1.0 : textHeight;
                        dbText.TextString = text.Trim();
                        dbText.ColorIndex = colorIndex <= 0 ? (short)7 : colorIndex;
                        dbText.HorizontalMode = TextHorizontalMode.TextCenter;
                        dbText.VerticalMode = TextVerticalMode.TextVerticalMid;
                        dbText.Position = position;
                        dbText.AlignmentPoint = position;
                        if (!textStyleId.IsNull) dbText.TextStyleId = textStyleId;
                        draw.Geometry.Draw(dbText);
                    }
                }
                catch
                {
                    try
                    {
                        draw.Geometry.Text(position, Vector3d.ZAxis, Vector3d.XAxis, textHeight <= 0 ? 1.0 : textHeight, 1.0, 0.0, text.Trim());
                    }
                    catch { }
                }
            }
        }

        private sealed class TextReference
        {
            public string Text { get; set; }
            public Point3d Position { get; set; }
        }

        private static bool TryGetNodePosition(Entity entity, out Point3d position)
        {
            position = Point3d.Origin;
            try
            {
                Circle circle = entity as Circle;
                if (circle != null)
                {
                    position = circle.Center;
                    return true;
                }
                DBPoint point = entity as DBPoint;
                if (point != null)
                {
                    position = point.Position;
                    return true;
                }
                BlockReference block = entity as BlockReference;
                if (block != null)
                {
                    position = block.Position;
                    return true;
                }
                DBText text = entity as DBText;
                if (text != null)
                {
                    position = text.Position;
                    return true;
                }
                MText mtext = entity as MText;
                if (mtext != null)
                {
                    position = mtext.Location;
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private static string InferDiameter(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string source = text.Trim();

            Match dn = Regex.Match(source, @"DN\s*(?<n>\d{2,4})", RegexOptions.IgnoreCase);
            if (dn.Success) return "DN" + dn.Groups["n"].Value;

            Match numberPipe = Regex.Match(source, @"(?<!\d)(?<n>\d{2,4})(?:\s*)?(?:PVC|UPVC|HDPE|PE|?|??|??|??)", RegexOptions.IgnoreCase);
            if (numberPipe.Success) return "DN" + numberPipe.Groups["n"].Value;

            // ??????????110PVC??75PVC????????110??
            // ????????????/??/????????????????????? 500 ???????
            if (ContainsAny(source, "??", "??", "??", "??", "??", "PVC", "HDPE", "PE", "??"))
            {
                Match isolated = Regex.Match(source, @"(?<!\d)(?<n>\d{2,4})(?!\d)");
                if (isolated.Success) return "DN" + isolated.Groups["n"].Value;
            }

            return string.Empty;
        }

        private static string InferDiameterFromSpecification(string specification)
        {
            string diameter = InferDiameter(specification);
            if (!string.IsNullOrWhiteSpace(diameter)) return diameter;
            Match number = Regex.Match(specification ?? string.Empty,
                @"(?<!\d)(?<n>\d{2,4})(?!\d)", RegexOptions.IgnoreCase);
            return number.Success ? "DN" + number.Groups["n"].Value
                : string.Empty;
        }

        private static string InferWellSpec(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string source = text.Trim();

            Match phi = Regex.Match(source, @"[??]\s*(?<n>\d{3,4})");
            if (phi.Success) return "?" + phi.Groups["n"].Value;

            Match well = Regex.Match(source, @"(?<!\d)(?<n>500|700|800|1000|1200|1500)(?!\d).{0,8}?(?:?|??|??|??|??)", RegexOptions.IgnoreCase);
            if (well.Success) return "?" + well.Groups["n"].Value;

            Match anyWellNumber = Regex.Match(source, @"(?:?|??|??|??|??).{0,8}?(?<n>500|700|800|1000|1200|1500)(?!\d)", RegexOptions.IgnoreCase);
            if (anyWellNumber.Success) return "?" + anyWellNumber.Groups["n"].Value;

            // ????????=????=??/????????????500??700??
            if (ContainsAny(source, "?", "??", "??", "??", "??"))
            {
                Match isolated = Regex.Match(source, @"(?<!\d)(?<n>500|700|800|1000|1200|1500)(?!\d)", RegexOptions.IgnoreCase);
                if (isolated.Success) return "?" + isolated.Groups["n"].Value;
            }

            return string.Empty;
        }

        private static double InferOuterDiameter(string diameter)
        {
            if (string.IsNullOrWhiteSpace(diameter)) return 0.0;
            Match m = Regex.Match(diameter, @"(?<n>\d{2,4})");
            if (!m.Success) return 0.0;
            double dn;
            if (!double.TryParse(m.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out dn)) return 0.0;
            return dn / 1000.0;
        }

        private static double InferTrenchWidth(string diameter, double fallback)
        {
            double outer = InferOuterDiameter(diameter);
            if (outer <= 0) return fallback > 0 ? fallback : 0.6;
            if (outer <= 0.12) return 0.4;
            if (outer <= 0.2) return 0.6;
            if (outer <= 0.3) return 0.8;
            return Math.Max(outer + 0.5, fallback > 0 ? fallback : 0.8);
        }

        private static double InferRoadThickness(string text, double fallback)
        {
            if (ContainsAny(text, "??", "??", "???")) return 0.0;
            if (ContainsAny(text, "??", "??")) return 0.12;
            if (fallback > 0) return fallback;
            return 0.20;
        }

        private static string InferExplicitBranchType(string text)
        {
            if (ContainsAny(text, "????", "??")) return "????";
            if (ContainsAny(text, "??")) return "??";
            if (ContainsAny(text, "??")) return "??";
            if (ContainsAny(text, "???", "?????")) return "???";
            return string.Empty;
        }

        private static string InferBranchType(string text)
        {
            string explicitType = InferExplicitBranchType(text);
            if (!string.IsNullOrWhiteSpace(explicitType)) return explicitType;
            if (ContainsAny(text, "?")) return "???";
            if (ContainsAny(text, "??")) return "??";
            return "???";
        }

        private static bool ShouldIncludeBranch(string branchType, string sourceText)
        {
            string text = (branchType ?? string.Empty) + " " + (sourceText ?? string.Empty);
            if (ContainsAny(text, "???", "???", "??", "??")) return false;
            if (ContainsAny(text, "???", "?????", "????", "??")) return true;
            return true;
        }

        private static bool ShouldForceSpecialBranchStructure(string branchType)
        {
            if (string.IsNullOrWhiteSpace(branchType)) return false;

            // ???????????????????????????
            // ???????????????????
            return ContainsAny(branchType, "??", "??", "????", "??");
        }

        private static string InferBranchBackfillType(string branchType, string sourceText)
        {
            string text = (branchType ?? string.Empty) + " " + (sourceText ?? string.Empty);
            if (ContainsAny(text, "????", "??")) return "????";
            if (ContainsAny(text, "??", "??")) return "????";
            return "?????";
        }

        private static void ApplyNoStructureThicknessRules(QuantityPipeAttributes attrs)
        {
            if (attrs == null) return;
            if (!QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind)) return;

            string type = attrs.BranchType ?? string.Empty;
            if (ContainsAny(type, "??", "??"))
            {
                attrs.SandCushionThickness = 0.0;
                attrs.GravelCushionThickness = 0.0;
                attrs.C25RestoreThickness = 0.0;
                attrs.BranchIncludeInCalculation = false;
            }
            else if (ContainsAny(type, "????", "??"))
            {
                attrs.SandCushionThickness = 0.0;
                attrs.GravelCushionThickness = 0.0;
                attrs.C25RestoreThickness = 0.0;
                attrs.BranchIncludeInCalculation = true;
            }
        }

        private static string InferWellMaterialType(string text)
        {
            if (ContainsAny(text, "??", "??")) return "???";
            if (ContainsAny(text, "??", "???", "??")) return "??????";
            return "?????";
        }

        private static string InferWellType(string text)
        {
            // ??????? / ????????????????????????
            // ??????????????????????????????????
            if (IsAmbiguousCheckSiltWellClass(text)) return "???";

            string cleaned = RemoveAmbiguousWellTypePhrases(text);
            if (ContainsAny(cleaned, "???", "??")) return "???";
            if (ContainsAny(cleaned, "???", "??")) return "???";
            return "???";
        }

        private static bool IsAmbiguousCheckSiltWellClass(string text)
        {
            string value = NormalizeLayerMetadataText(text);
            return value.IndexOf("??", StringComparison.CurrentCultureIgnoreCase) >= 0
                && value.IndexOf("??", StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static string RemoveAmbiguousWellTypePhrases(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string cleaned = text;
            cleaned = Regex.Replace(cleaned, @"??\s*??\s*[?,?/\\;?|_\-]*\s*???", "???", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"??\s*???", "???", RegexOptions.IgnoreCase);
            return cleaned;
        }

        private static string BuildDefaultBackfillStructure(QuantityPipeAttributes attrs)
        {
            if (attrs == null) return string.Empty;
            if (QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind))
            {
                return "????C25?? " + Format(attrs.C25RestoreThickness) + " ??"
                    + Environment.NewLine + "???????? " + Format(attrs.GravelCushionThickness) + " ??"
                    + Environment.NewLine + "????? 0.80"
                    + Environment.NewLine + "????? " + Format(attrs.SandCushionThickness) + " ?? ??";
            }
            if (QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind))
            {
                string branchType = attrs.BranchType ?? string.Empty;
                if (ContainsAny(branchType, "??", "??")) return string.Empty;
                if (ContainsAny(branchType, "????", "??"))
                {
                    double depth = attrs.BranchDepth > 0 ? attrs.BranchDepth : 0.6;
                    return "???? " + Format(depth) + " ???";
                }

                double c25 = attrs.C25RestoreThickness > 0 ? attrs.C25RestoreThickness : 0.25;
                double sand = attrs.SandCushionThickness > 0 ? attrs.SandCushionThickness : 0.10;
                return "C25??? " + Format(c25) + " ??"
                    + Environment.NewLine + "????? 0.25 ???"
                    + Environment.NewLine + "????? " + Format(sand) + " ?? ??";
            }
            return "C25??? " + Format(attrs.C25RestoreThickness) + " ??"
                + Environment.NewLine + "???? " + Format(attrs.GravelCushionThickness) + " ??"
                + Environment.NewLine + "????? 0.80 ???"
                + Environment.NewLine + "????? " + Format(attrs.SandCushionThickness) + " ?? ??";
        }

        private static bool IsLockViolation(Autodesk.AutoCAD.Runtime.Exception ex)
        {
            if (ex == null) return false;

            string status = string.Empty;
            try
            {
                status = ex.ErrorStatus.ToString();
            }
            catch
            {
                status = string.Empty;
            }

            string message = ex.Message ?? string.Empty;
            string all = status + " " + message;

            return all.IndexOf("LockViolation", StringComparison.OrdinalIgnoreCase) >= 0
                || all.IndexOf("???", StringComparison.OrdinalIgnoreCase) >= 0
                || all.IndexOf("locked", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Format(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(text) || values == null) return false;
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}
