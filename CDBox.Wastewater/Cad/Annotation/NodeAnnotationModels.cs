using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace TCPipeAutoDraw.Modules.NodeAnnotation
{
    public sealed class NodeAnnotationCandidate
    {
        public ObjectId ObjectId { get; set; }
        public Point3d Center { get; set; }
        public string NodeNo { get; set; }
        public string WellType { get; set; }
        public double WellDepth { get; set; }
        public double ShaftLength { get; set; }
        public string LayerName { get; set; }
        public string SourceText { get; set; }
        public double ExtentsDiagonal { get; set; }
        public bool HasSavedAttributes { get; set; }

        public NodeAnnotationCandidate()
        {
            ObjectId = ObjectId.Null;
            Center = Point3d.Origin;
            NodeNo = string.Empty;
            WellType = string.Empty;
            LayerName = string.Empty;
            SourceText = string.Empty;
        }
    }

    public sealed class NodeAnnotationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string NodeNo { get; set; }
        public string WellType { get; set; }
        public string AnnotationLayerName { get; set; }
        public ObjectId NodeObjectId { get; set; }
        public List<ObjectId> TextObjectIds { get; private set; }
        public Point3d AnnotationPoint { get; set; }
        public Point3d NodeCenter { get; set; }

        public NodeAnnotationResult()
        {
            Message = string.Empty;
            NodeNo = string.Empty;
            WellType = string.Empty;
            AnnotationLayerName = string.Empty;
            NodeObjectId = ObjectId.Null;
            TextObjectIds = new List<ObjectId>();
            AnnotationPoint = Point3d.Origin;
            NodeCenter = Point3d.Origin;
        }

        public string ToEditorMessage()
        {
            if (!Success) return "\n[节点标注] " + Message;
            return "\n[节点标注] 完成。节点：" + (string.IsNullOrWhiteSpace(NodeNo) ? "未编号" : NodeNo)
                + "；井类型：" + (string.IsNullOrWhiteSpace(WellType) ? "未识别" : WellType)
                + "；注记图层：" + AnnotationLayerName + "。";
        }
    }
}
