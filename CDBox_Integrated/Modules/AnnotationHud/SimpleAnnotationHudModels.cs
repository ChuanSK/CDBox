using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using TCPipeAutoDraw.Core.Colors;

namespace TCPipeAutoDraw.Modules.AnnotationHud
{
    internal enum SimpleAnnotationKind
    {
        Node,
        SurfaceArea
    }

    internal sealed class SimpleAnnotationTextLine
    {
        public ObjectId ObjectId { get; set; }
        public string Role { get; set; }
        public string Label { get; set; }
        public string Text { get; set; }
        public CDBoxColor Color { get; set; }

        public SimpleAnnotationTextLine()
        {
            ObjectId = ObjectId.Null;
            Role = string.Empty;
            Label = string.Empty;
            Text = string.Empty;
            Color = CDBoxColor.FromIndex(7);
        }
    }

    internal sealed class SimpleAnnotationHudModel
    {
        public SimpleAnnotationKind Kind { get; set; }
        public string AnnotationId { get; set; }
        public string Header { get; set; }
        public string Summary { get; set; }
        public Point3d AnchorPoint { get; set; }
        public string SourceHandle { get; set; }
        public ObjectId SourceObjectId { get; set; }
        public List<SimpleAnnotationTextLine> Lines { get; private set; }
        public ObjectId LeaderObjectId { get; set; }
        public double TextHeight { get; set; }
        public string TextStyleName { get; set; }
        public string LayerName { get; set; }
        public CDBoxColor PrimaryColor { get; set; }
        public CDBoxColor SecondaryColor { get; set; }
        public string LinetypeName { get; set; }
        public LineWeight LineWeight { get; set; }
        public List<string> TextStyleNames { get; set; }
        public List<string> LayerNames { get; set; }
        public List<string> LinetypeNames { get; set; }

        public SimpleAnnotationHudModel()
        {
            AnnotationId = string.Empty;
            Header = string.Empty;
            Summary = string.Empty;
            AnchorPoint = Point3d.Origin;
            SourceHandle = string.Empty;
            SourceObjectId = ObjectId.Null;
            Lines = new List<SimpleAnnotationTextLine>();
            LeaderObjectId = ObjectId.Null;
            TextStyleName = string.Empty;
            LayerName = string.Empty;
            PrimaryColor = CDBoxColor.FromIndex(7);
            SecondaryColor = CDBoxColor.FromIndex(7);
            LinetypeName = "ByLayer";
            LineWeight = LineWeight.ByLayer;
            TextStyleNames = new List<string>();
            LayerNames = new List<string>();
            LinetypeNames = new List<string>();
        }

        public ObjectId[] GetObjectIds()
        {
            var ids = new List<ObjectId>();
            for (int i = 0; i < Lines.Count; i++)
            {
                if (!Lines[i].ObjectId.IsNull && !ids.Contains(Lines[i].ObjectId)) ids.Add(Lines[i].ObjectId);
            }
            if (!LeaderObjectId.IsNull && !ids.Contains(LeaderObjectId)) ids.Add(LeaderObjectId);
            return ids.ToArray();
        }
    }
}
