using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CDBox.Shared.Wastewater.Cad;
using TCPipeAutoDraw.Modules.AnnotationHud;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    internal sealed class WastewaterCadInteractionService
        : IWastewaterCadInteractionService
    {
        public void Initialize()
        {
            PipeAnnotationPresentation.Initialize();
        }

        public void Terminate()
        {
            PipeAnnotationPresentation.Terminate();
        }

        public void RefreshAppearance()
        {
            PipeAnnotationPresentation.RefreshAppearance();
        }

        public void RefreshNodeAnnotations(object document,
            IEnumerable<string> sourceHandles)
        {
            Document cadDocument = document as Document;
            if (cadDocument == null) return;
            SimpleAnnotationObjectService
                .RefreshNodeAnnotationsForSourceHandles(cadDocument,
                    sourceHandles ?? Enumerable.Empty<string>());
        }

        public void RefreshPipeAnnotations(object document,
            IEnumerable<string> sourceHandles)
        {
            Document cadDocument = document as Document;
            if (cadDocument == null) return;
            PipeLengthAnnotationObjectService
                .RefreshBindingsForSourceHandles(cadDocument,
                    sourceHandles ?? Enumerable.Empty<string>(),
                    string.Empty);
        }

        public bool CanOpenAttribute(object document,
            IEnumerable<object> selectedObjectIds)
        {
            Document cadDocument = document as Document;
            return cadDocument != null && QuantityAttributeDoubleClickService
                .CanOpen(cadDocument, ToObjectIds(selectedObjectIds));
        }

        public bool TryOpenAttribute(object document,
            IEnumerable<object> selectedObjectIds)
        {
            Document cadDocument = document as Document;
            if (cadDocument == null) return false;
            ObjectId id;
            if (!QuantityAttributeDoubleClickService.TryResolve(
                    cadDocument, ToObjectIds(selectedObjectIds), out id))
                return false;
            return QuantityAttributeDoubleClickService.Open(cadDocument, id);
        }

        public bool TryOpenAttributeWithOverlap(object document,
            IEnumerable<object> selectedObjectIds,
            double screenX, double screenY)
        {
            Document cadDocument = document as Document;
            return cadDocument != null && QuantityAttributeDoubleClickService
                .TryOpenWithOverlapSelection(cadDocument,
                    ToObjectIds(selectedObjectIds),
                    new Point(screenX, screenY));
        }

        private static ObjectId[] ToObjectIds(IEnumerable<object> values)
        {
            return (values ?? Enumerable.Empty<object>())
                .OfType<ObjectId>().ToArray();
        }
    }
}
