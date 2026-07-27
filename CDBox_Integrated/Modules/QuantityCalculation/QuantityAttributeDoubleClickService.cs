using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
using System.Windows;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;
using TCPipeAutoDraw.UI;
using TCPipeAutoDraw.UI.Studio;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    internal static class QuantityAttributeDoubleClickService
    {
        public static bool CanOpen(Document doc, ObjectId[] selectedIds)
        {
            ObjectId objectId;
            return TryResolveOpenableObject(doc, selectedIds, out objectId);
        }

        public static bool TryOpen(Document doc, ObjectId[] selectedIds)
        {
            ObjectId objectId;
            if (!TryResolveOpenableObject(doc, selectedIds, out objectId)) return false;
            QuantityPipeSelectionInfo info;
            try { info = QuantityPipeAttributeService.ReadPipe(doc, objectId); }
            catch { return false; }
            if (!IsSupportedEditableObject(info)) return false;
            CDBoxStudioQuantityAttributeEditorWindow.ShowWindow(new AcadMainWindow(), info,
                QuantityDashboardService.GetDocumentId(doc));
            return true;
        }

        public static bool TryOpenWithOverlapSelection(Document doc,
            ObjectId[] selectedIds, Point screenPoint)
        {
            ObjectId selectedObjectId;
            ObjectId[] currentIds = ReadCurrentSelection(doc);
            if (!TryResolveOpenableObject(doc, currentIds, out selectedObjectId)
                && !TryResolveOpenableObject(doc, selectedIds, out selectedObjectId)) return false;

            List<PipeSelectionCandidate> candidates = FindOpenableCandidates(
                doc, screenPoint);
            if (candidates.Count > 1)
            {
                CDBoxStudioLogger.Info("双击属性编辑器检测到重叠管线：" + candidates.Count + " 个候选。");
                OverlappingPipeSelectionService.EnrichDisplay(doc, candidates);
                PipeSelectionCandidate selected = OverlappingPipeSelectionService.Select(
                    doc, candidates, null);
                if (selected == null) return true;
                selectedObjectId = selected.ObjectId;
            }
            else if (candidates.Count == 1)
            {
                // The click position is more reliable than the implied selection after a
                // modeless editor closes. AutoCAD may not have updated that selection yet.
                selectedObjectId = candidates[0].ObjectId;
            }

            return TryOpen(doc, new[] { selectedObjectId });
        }

        private static List<PipeSelectionCandidate> FindOpenableCandidates(
            Document doc, Point screenPoint)
        {
            var result = new List<PipeSelectionCandidate>();
            if (doc == null) return result;
            List<PipeSelectionCandidate> candidates;
            try
            {
                Point3d pickedPoint = doc.Editor.PointToWorld(screenPoint);
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    candidates = PipeLengthAnnotationService.FindCandidatesAtPoint(
                        doc.Database, tr, doc.Editor, pickedPoint);
                    tr.Commit();
                }
            }
            catch
            {
                return result;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                PipeSelectionCandidate candidate = candidates[i];
                if (candidate == null || candidate.ObjectId.IsNull) continue;
                ObjectId openable;
                if (!TryResolveOpenableObject(doc, new[] { candidate.ObjectId }, out openable)) continue;
                result.Add(candidate);
            }
            return result;
        }

        private static ObjectId[] ReadCurrentSelection(Document doc)
        {
            if (doc == null) return new ObjectId[0];
            try
            {
                Autodesk.AutoCAD.EditorInput.PromptSelectionResult selection =
                    doc.Editor.SelectImplied();
                if (selection.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK
                    && selection.Value != null) return selection.Value.GetObjectIds();
            }
            catch { }
            return new ObjectId[0];
        }

        public static bool TryResolveOpenableObject(Document doc,
            IEnumerable<ObjectId> selectedIds, out ObjectId objectId)
        {
            objectId = ObjectId.Null;
            if (doc == null || selectedIds == null) return false;
            var visited = new HashSet<ObjectId>();
            foreach (ObjectId id in selectedIds)
            {
                if (id.IsNull || !visited.Add(id)) continue;
                QuantityPipeSelectionInfo info;
                try { info = QuantityPipeAttributeService.ReadPipe(doc, id); }
                catch { continue; }
                if (!IsSupportedEditableObject(info)) continue;
                objectId = id;
                return true;
            }
            return false;
        }

        private static bool IsSupportedEditableObject(QuantityPipeSelectionInfo info)
        {
            if (info == null || info.Attributes == null) return false;
            string kind = info.InferredKind ?? info.Attributes.ObjectKind;
            if (QuantityPipeAttributes.IsMainPipeKind(kind)
                || QuantityPipeAttributes.IsBranchKind(kind)
                || QuantityPipeAttributes.IsNodeKind(kind)) return true;
            return info.Attributes.IsSpecialObject && info.HasSavedAttributes;
        }
    }
}
