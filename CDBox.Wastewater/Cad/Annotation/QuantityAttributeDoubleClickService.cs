using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CDBox.Wastewater.Module;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    internal static class QuantityAttributeDoubleClickService
    {
        public static bool CanOpen(Document document,
            ObjectId[] selectedIds)
        {
            ObjectId id;
            return TryResolve(document, selectedIds, out id);
        }

        public static bool TryOpenWithOverlapSelection(Document document,
            ObjectId[] selectedIds, Point screenPoint)
        {
            ObjectId selected;
            if (!TryResolve(document, selectedIds, out selected)) return false;
            List<PipeSelectionCandidate> candidates = FindCandidates(document,
                selected, screenPoint);
            if (candidates.Count > 1)
            {
                OverlappingPipeSelectionService.EnrichDisplay(document,
                    candidates);
                PipeSelectionCandidate chosen =
                    OverlappingPipeSelectionService.Select(document,
                        candidates, null);
                if (chosen == null) return true;
                selected = chosen.ObjectId;
            }
            else if (candidates.Count == 1) selected = candidates[0].ObjectId;
            return Open(document, selected);
        }

        internal static bool Open(Document document, ObjectId id)
        {
            QuantityPipeSelectionInfo info;
            try { info = QuantityPipeAttributeService.ReadPipe(document, id); }
            catch { return false; }
            if (!Supported(info) || WastewaterRuntimeServices
                    .OpenAttributeEditor == null) return false;
            WastewaterRuntimeServices.OpenAttributeEditor(
                WastewaterQuantityDashboardService.GetDocumentId(document),
                info.HandleText);
            return true;
        }

        internal static bool TryResolve(Document document,
            IEnumerable<ObjectId> selectedIds, out ObjectId objectId)
        {
            objectId = ObjectId.Null;
            if (document == null || selectedIds == null) return false;
            foreach (ObjectId id in selectedIds.Distinct())
            {
                QuantityPipeSelectionInfo info;
                try { info = QuantityPipeAttributeService.ReadPipe(document,
                    id); }
                catch { continue; }
                if (!Supported(info)) continue;
                objectId = id;
                return true;
            }
            return false;
        }

        private static bool Supported(QuantityPipeSelectionInfo info)
        {
            if (info == null || info.Attributes == null) return false;
            string kind = info.InferredKind ?? info.Attributes.ObjectKind;
            return QuantityPipeAttributes.IsMainPipeKind(kind)
                || QuantityPipeAttributes.IsBranchKind(kind)
                || QuantityPipeAttributes.IsNodeKind(kind)
                || info.Attributes.IsSpecialObject
                    && info.HasSavedAttributes;
        }

        private static List<PipeSelectionCandidate> FindCandidates(
            Document document, ObjectId selected, Point screenPoint)
        {
            var output = new List<PipeSelectionCandidate>();
            if (document == null || selected.IsNull) return output;
            Point3d point = Point3d.Origin;
            try { point = document.Editor.PointToWorld(screenPoint); }
            catch { }
            try
            {
                using (Transaction transaction = document.Database
                    .TransactionManager.StartTransaction())
                {
                    output = PipeLengthAnnotationService
                        .FindOverlappingCandidates(document.Database,
                            transaction, document.Editor, selected, point)
                        .Where(item => item != null && !item.ObjectId.IsNull)
                        .ToList();
                    transaction.Commit();
                }
            }
            catch { output.Clear(); }
            return output;
        }
    }
}
