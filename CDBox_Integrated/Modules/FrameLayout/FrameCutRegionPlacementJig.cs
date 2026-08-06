using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

using System;
using DbPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    /// <summary>
    /// ????????????????
    /// </summary>
    internal sealed class FrameCutRegionPlacementJig : DrawJig
    {
        private enum PlacementStage
        {
            Center,
            Rotation
        }

        private readonly double _width;
        private readonly double _height;
        private readonly PlacementStage _stage;
        private Point3d _center;
        private Point3d _cursor;
        private double _rotation;
        private bool _hasSample;

        private FrameCutRegionPlacementJig(double width, double height,
            PlacementStage stage, Point3d center)
        {
            _width = width;
            _height = height;
            _stage = stage;
            _center = center;
            _cursor = center;
        }

        public Point3d Center
        {
            get { return _stage == PlacementStage.Center ? _cursor : _center; }
        }

        public double Rotation
        {
            get { return _rotation; }
        }

        public static FrameCutRegionPlacementJig ForCenter(
            double width, double height)
        {
            return new FrameCutRegionPlacementJig(width, height,
                PlacementStage.Center, Point3d.Origin);
        }

        public static FrameCutRegionPlacementJig ForRotation(
            Point3d center, double width, double height)
        {
            return new FrameCutRegionPlacementJig(width, height,
                PlacementStage.Rotation, center);
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            JigPromptPointOptions options =
                new JigPromptPointOptions("\n ");
            options.UserInputControls = UserInputControls.Accept3dCoordinates
                | UserInputControls.NoZeroResponseAccepted;
            if (_stage == PlacementStage.Rotation)
            {
                options.BasePoint = _center;
                options.UseBasePoint = true;
            }

            PromptPointResult result = prompts.AcquirePoint(options);
            if (result.Status != PromptStatus.OK) return SamplerStatus.Cancel;
            if (_hasSample && result.Value.DistanceTo(_cursor) <= 1e-8)
                return SamplerStatus.NoChange;
            if (_stage == PlacementStage.Rotation)
            {
                Vector3d direction = result.Value - _center;
                if (direction.Length <= GeometryHelper.Eps)
                    return SamplerStatus.NoChange;
                _rotation = Math.Atan2(direction.Y, direction.X);
            }
            _cursor = result.Value;
            _hasSample = true;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            if (draw == null || draw.Geometry == null) return true;
            Point3d center = Center;
            Vector3d axisU = new Vector3d(Math.Cos(_rotation),
                Math.Sin(_rotation), 0).GetNormal();
            Vector3d axisV = GeometryHelper.GetPerpLeft(axisU);
            Point3d lowerLeft = center - axisU * (_width * 0.5)
                - axisV * (_height * 0.5);
            Point3d p1 = lowerLeft;
            Point3d p2 = p1 + axisU * _width;
            Point3d p3 = p2 + axisV * _height;
            Point3d p4 = p1 + axisV * _height;

            using (DbPolyline rectangle = new DbPolyline())
            {
                rectangle.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
                rectangle.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
                rectangle.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
                rectangle.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
                rectangle.Closed = true;
                rectangle.ColorIndex = 1;
                draw.Geometry.Draw(rectangle);
            }
            return true;
        }
    }
}
