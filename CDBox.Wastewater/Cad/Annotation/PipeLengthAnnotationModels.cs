using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    public sealed class PipeLengthAnnotationResult
    {
        public bool Success { get; set; }
        public bool IsCancelled { get; set; }
        public string Message { get; set; }
        public string PipeLayerName { get; set; }
        public string AnnotationLayerName { get; set; }
        public string AnnotationFontName { get; set; }
        public string PipeParentGroup { get; set; }
        public string PipeParentClass { get; set; }
        public string PipeTagText { get; set; }
        public string QuantityObjectKind { get; set; }
        public bool HasQuantityAttributes { get; set; }
        public bool DrawLengthWidthHeightAnnotation { get; set; }
        public double ExcavationWidth { get; set; }
        public double ExcavationHeight { get; set; }
        public double ExcavationDepth { get; set; }
        public double Length { get; set; }
        public ObjectId PipeObjectId { get; set; }
        public ObjectId AnnotationObjectId { get; set; }
        public ObjectId BottomAnnotationObjectId { get; set; }
        public List<ObjectId> BottomAnnotationObjectIds { get; private set; }
        public ObjectId LeaderObjectId { get; set; }
        public string AnnotationId { get; set; }
        public string SourceCDBoxObjectId { get; set; }
        public string AnnotationGroupName { get; set; }
        public ObjectId AnnotationGroupObjectId { get; set; }
        public Point3d AnnotationPoint { get; set; }
        public Point3d BindingPoint { get; set; }
        public string UserText { get; set; }
        public string SystemLengthText { get; set; }

        public PipeLengthAnnotationResult()
        {
            Message = string.Empty;
            PipeLayerName = string.Empty;
            AnnotationLayerName = string.Empty;
            AnnotationFontName = string.Empty;
            PipeParentGroup = string.Empty;
            PipeParentClass = string.Empty;
            PipeTagText = string.Empty;
            QuantityObjectKind = string.Empty;
            IsCancelled = false;
            HasQuantityAttributes = false;
            DrawLengthWidthHeightAnnotation = false;
            ExcavationWidth = 0.0;
            ExcavationHeight = 0.0;
            ExcavationDepth = 0.0;
            PipeObjectId = ObjectId.Null;
            AnnotationObjectId = ObjectId.Null;
            BottomAnnotationObjectId = ObjectId.Null;
            BottomAnnotationObjectIds = new List<ObjectId>();
            LeaderObjectId = ObjectId.Null;
            AnnotationId = string.Empty;
            SourceCDBoxObjectId = string.Empty;
            AnnotationGroupName = string.Empty;
            AnnotationGroupObjectId = ObjectId.Null;
            AnnotationPoint = Point3d.Origin;
            BindingPoint = Point3d.Origin;
            UserText = string.Empty;
            SystemLengthText = string.Empty;
        }

        public string ToEditorMessage()
        {
            if (!Success) return "\n[管线长度标注] " + Message;

            string meta = string.Empty;
            if (!string.IsNullOrWhiteSpace(PipeParentGroup)) meta += "；父属性：" + PipeParentGroup;
            if (!string.IsNullOrWhiteSpace(PipeParentClass)) meta += "；分类：" + PipeParentClass;

            return "\n[管线长度标注] 完成。管线图层：" + PipeLayerName
                + meta
                + "；注记图层：" + AnnotationLayerName
                + "；字体样式：" + AnnotationFontName
                + "；长度：" + Length.ToString("0.###") + "m。";
        }
    }
}
