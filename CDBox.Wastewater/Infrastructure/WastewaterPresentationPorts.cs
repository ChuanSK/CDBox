using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CDBox.Shared.UI;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    /// <summary>图形业务到基础标注交互 UI 的端口。</summary>
    internal static class PipeAnnotationPresentation
    {
        private const string Id = "wastewater.annotation-interaction";
        public static void Initialize() { CDBoxUiGateway.Call(Id, "Initialize"); }
        public static void Terminate() { CDBoxUiGateway.Call(Id, "Terminate"); }
        public static void RefreshAppearance() { CDBoxUiGateway.Call(Id, "RefreshAppearance"); }
        public static void NotifySpatialEditStarted(Document doc, string annotationId) { CDBoxUiGateway.Call(Id, "NotifySpatialEditStarted", doc, annotationId); }
        public static void NotifySpatialEditFinished(Document doc, string annotationId) { CDBoxUiGateway.Call(Id, "NotifySpatialEditFinished", doc, annotationId); }
        public static void RefreshCard(Document doc, ObjectId id) { CDBoxUiGateway.Call(Id, "RefreshCard", doc, id); }
    }
    internal static class OverlappingPipePresentation
    {
        public static PipeSelectionCandidate Select(Document document, IList<PipeSelectionCandidate> candidates, Transaction transaction)
        {
            return CDBoxUiGateway.Call<PipeSelectionCandidate>("wastewater.overlap", "Select", document, candidates, transaction);
        }
        public static void EnrichDisplay(Document document, IList<PipeSelectionCandidate> candidates)
        {
            CDBoxUiGateway.Call("wastewater.overlap", "EnrichDisplay", document, candidates);
        }
    }
}
