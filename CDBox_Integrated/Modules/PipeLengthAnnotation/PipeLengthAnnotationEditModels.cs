using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    internal sealed class PipeLengthAnnotationBindingContent
    {
        public string TopText { get; set; }
        public string UserText { get; set; }
        public string SystemLengthText { get; set; }
        public string BottomText { get; set; }
        public bool IsQuantityPipe { get; set; }

        public PipeLengthAnnotationBindingContent()
        {
            TopText = string.Empty;
            UserText = string.Empty;
            SystemLengthText = string.Empty;
            BottomText = string.Empty;
        }
    }

    public sealed class PipeLengthAnnotationEditModel
    {
        public ObjectId SelectedObjectId { get; set; }
        public string AnnotationId { get; set; }
        public string UserText { get; set; }
        public string SystemLengthText { get; set; }
        public string DetachedLengthToken { get; set; }
        public string TopText { get; set; }
        public string BottomText { get; set; }
        public string TextStyleName { get; set; }
        public double TextHeight { get; set; }
        public string LayerName { get; set; }
        public short TextColorIndex { get; set; }
        public short LeaderColorIndex { get; set; }
        public string LinetypeName { get; set; }
        public LineWeight LineWeight { get; set; }
        public string SourceObjectId { get; set; }
        public string SourceHandle { get; set; }
        public string SourceLayerName { get; set; }
        public string SourceObjectType { get; set; }
        public Point3d BindingPoint { get; set; }
        public bool HasBindingPoint { get; set; }
        public bool IsBound { get; set; }
        public bool BindingIsValid { get; set; }
        public bool HasBottomAnnotation { get; set; }
        public List<string> TextStyleNames { get; set; }
        public List<string> LayerNames { get; set; }
        public List<string> LinetypeNames { get; set; }

        public PipeLengthAnnotationEditModel()
        {
            AnnotationId = string.Empty;
            UserText = string.Empty;
            SystemLengthText = string.Empty;
            DetachedLengthToken = string.Empty;
            TopText = string.Empty;
            BottomText = string.Empty;
            TextStyleName = string.Empty;
            LayerName = string.Empty;
            LinetypeName = string.Empty;
            SourceObjectId = string.Empty;
            SourceHandle = string.Empty;
            SourceLayerName = string.Empty;
            SourceObjectType = string.Empty;
            BindingPoint = Point3d.Origin;
            TextStyleNames = new List<string>();
            LayerNames = new List<string>();
            LinetypeNames = new List<string>();
            TextColorIndex = 7;
            LeaderColorIndex = 7;
            TextHeight = 1.0;
            LineWeight = LineWeight.ByLayer;
            IsBound = true;
            BindingIsValid = true;
        }
    }
}
