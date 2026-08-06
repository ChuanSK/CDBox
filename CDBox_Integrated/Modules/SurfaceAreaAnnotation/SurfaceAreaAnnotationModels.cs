using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace TCPipeAutoDraw.Modules.SurfaceAreaAnnotation
{
    public enum AnnotationLayerMode
    {
        DefaultZJ = 0,
        ExistingLayer = 1,
        CustomLayer = 2
    }

    public enum SurfaceAreaCalculationMode
    {
        /// <summary>
        /// ???????? TIN ?????????????????
        /// </summary>
        BuiltInTin = 0,

        /// <summary>
        /// ???? CASS surface.log ???????????
        /// </summary>
        CassSurfaceLog = 1,

        /// <summary>
        /// ???? CASS surfacearea ????????? CASS ??????????
        /// </summary>
        CassCommand = 2,

        /// <summary>
        /// ???????????????????? CASS?
        /// </summary>
        PlanArea = 3
    }

    public sealed class SurfaceAreaAnnotationOptions
    {
        public double BoundaryInterval { get; set; }
        public double TextHeight { get; set; }
        public int DecimalPlaces { get; set; }
        public string AnnotationTemplate { get; set; }

        /// <summary>
        /// ?????CASS ?????????????
        /// </summary>
        public SurfaceAreaCalculationMode CalculationMode { get; set; }

        /// <summary>
        /// CASS surface.log ??????????????? DWG ????? AutoCAD ???????
        /// </summary>
        public string CassSurfaceLogPath { get; set; }

        /// <summary>
        /// CASS ??????????????????????????????????
        /// </summary>
        public bool DeleteCassGeneratedObjects { get; set; }

        /// <summary>
        /// ????????????? CAD ?????????????????????
        /// </summary>
        public string AnnotationFontName { get; set; }

        /// <summary>
        /// ????????? ZJ / ???? / ??????
        /// </summary>
        public AnnotationLayerMode LayerMode { get; set; }

        /// <summary>
        /// ??????????????
        /// </summary>
        public string SelectedLayerName { get; set; }

        /// <summary>
        /// ???????????????????? ZJ ????
        /// </summary>
        public string AnnotationLayerName { get; set; }

        /// <summary>
        /// ???????????????????????????????
        /// </summary>
        public bool UseBoundaryLayerForAnnotation { get; set; }

        public bool DrawLeader { get; set; }

        public static SurfaceAreaAnnotationOptions Default
        {
            get
            {
                return new SurfaceAreaAnnotationOptions
                {
                    BoundaryInterval = 5.0,
                    TextHeight = 1.0,
                    DecimalPlaces = 2,
                    AnnotationTemplate = "{???}????{???}?",
                    CalculationMode = SurfaceAreaCalculationMode.CassCommand,
                    CassSurfaceLogPath = string.Empty,
                    DeleteCassGeneratedObjects = true,
                    AnnotationFontName = "??",
                    LayerMode = AnnotationLayerMode.DefaultZJ,
                    SelectedLayerName = "ZJ",
                    AnnotationLayerName = "ZJ",
                    UseBoundaryLayerForAnnotation = false,
                    DrawLeader = true
                };
            }
        }
    }

    public sealed class SurfaceAreaAnnotationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string BoundaryLayerName { get; set; }
        public string AnnotationLayerName { get; set; }
        public string AnnotationFontName { get; set; }
        public SurfaceAreaCalculationMode CalculationMode { get; set; }
        public string CassSurfaceLogPath { get; set; }
        public string CassSurfaceLogLine { get; set; }
        public int CassGeneratedObjectCount { get; set; }
        public int CassDeletedObjectCount { get; set; }
        public bool AsyncStarted { get; set; }
        public bool PlanAreaFallback { get; set; }
        public double SurfaceArea { get; set; }
        public double PlanArea { get; set; }
        public int ElevationPointCount { get; set; }
        public int BoundaryPointCount { get; set; }
        public int TriangleCount { get; set; }
        public ObjectId BoundaryObjectId { get; set; }
        public ObjectId AnnotationObjectId { get; set; }
        public ObjectId LeaderObjectId { get; set; }
        public Point3d AnnotationPoint { get; set; }

        public SurfaceAreaAnnotationResult()
        {
            Message = string.Empty;
            BoundaryLayerName = string.Empty;
            AnnotationLayerName = string.Empty;
            AnnotationFontName = string.Empty;
            CalculationMode = SurfaceAreaCalculationMode.CassCommand;
            CassSurfaceLogPath = string.Empty;
            CassSurfaceLogLine = string.Empty;
            BoundaryObjectId = ObjectId.Null;
            AnnotationObjectId = ObjectId.Null;
            LeaderObjectId = ObjectId.Null;
            AnnotationPoint = Point3d.Origin;
        }

        public string ToEditorMessage()
        {
            if (!Success) return "\n[?????] " + Message;

            bool isPlanArea = PlanAreaFallback || CalculationMode == SurfaceAreaCalculationMode.PlanArea;
            string modeText = isPlanArea ? "????" : "CASS surfacearea";

            if (AsyncStarted)
            {
                return "\n[?????] " + Message;
            }

            string text = "\n[?????] ????????" + modeText
                + "??????" + BoundaryLayerName
                + "??????" + AnnotationLayerName
                + "??????" + AnnotationFontName
                + (isPlanArea ? "????" : "?????") + SurfaceArea.ToString("0.###") + "?"
                + (isPlanArea ? string.Empty : "??????" + PlanArea.ToString("0.###") + "?");

            if (PlanAreaFallback)
            {
                text += "????" + (string.IsNullOrWhiteSpace(Message) ? "???????" : Message.TrimEnd('?'));
                if (CassGeneratedObjectCount > 0 || CassDeletedObjectCount > 0)
                {
                    text += "?CASS?????" + CassGeneratedObjectCount + "?????" + CassDeletedObjectCount;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(CassSurfaceLogPath)) text += "?CASS?????" + CassSurfaceLogPath;
                if (CassGeneratedObjectCount > 0 || CassDeletedObjectCount > 0)
                {
                    text += "?CASS?????" + CassGeneratedObjectCount + "?????" + CassDeletedObjectCount;
                }
            }

            return text + "?";
        }
    }
}
