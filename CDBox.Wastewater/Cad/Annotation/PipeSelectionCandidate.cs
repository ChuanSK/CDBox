using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    internal sealed class PipeSelectionCandidate
    {
        public ObjectId ObjectId { get; set; }
        public Point3d AnchorPoint { get; set; }
        public double Distance { get; set; }
        public double Length { get; set; }
        public string LayerName { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }

        public PipeSelectionCandidate()
        {
            ObjectId = ObjectId.Null;
            AnchorPoint = Point3d.Origin;
            LayerName = string.Empty;
            Title = "长度对象";
            Detail = string.Empty;
        }
    }
}
