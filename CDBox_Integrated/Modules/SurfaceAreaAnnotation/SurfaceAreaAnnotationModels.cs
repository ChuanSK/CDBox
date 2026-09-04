using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace TCPipeAutoDraw.Modules.SurfaceAreaAnnotation
{
    public enum SurfaceAreaCalculationMode
    {
        /// <summary>
        /// 已弃用：插件内置 TIN 估算算法。正式界面不再提供该模式。
        /// </summary>
        BuiltInTin = 0,

        /// <summary>
        /// 读取已有 CASS surface.log 结果。保留为备用模式。
        /// </summary>
        CassSurfaceLog = 1,

        /// <summary>
        /// 自动调用 CASS surfacearea 命令计算，然后读取 CASS 输出结果并自动注记。
        /// </summary>
        CassCommand = 2,

        /// <summary>
        /// 直接计算闭合边界的平面面积并注记，不调用 CASS。
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
        /// 计算方式：CASS 表面积或闭合边界平面面积。
        /// </summary>
        public SurfaceAreaCalculationMode CalculationMode { get; set; }

        /// <summary>
        /// CASS surface.log 文件路径。为空时自动尝试从当前 DWG 所在目录和 AutoCAD 当前目录查找。
        /// </summary>
        public string CassSurfaceLogPath { get; set; }

        /// <summary>
        /// CASS 命令执行后是否删除其自动生成的三角网、三角面积文字等对象。默认删除。
        /// </summary>
        public bool DeleteCassGeneratedObjects { get; set; }

        /// <summary>
        /// 注记文字样式名称。仅从当前 CAD 图形已有文字样式中选择，默认优先“宋体”。
        /// </summary>
        public string AnnotationFontName { get; set; }

        /// <summary>
        /// 注记图层模式：默认 ZJ / 已有图层 / 自定义图层。
        /// </summary>
        public AnnotationLayerMode LayerMode { get; set; }

        /// <summary>
        /// 已有图层模式下选择的图层名。
        /// </summary>
        public string SelectedLayerName { get; set; }

        /// <summary>
        /// 自定义图层模式下输入的图层名；默认也作为 ZJ 图层名。
        /// </summary>
        public string AnnotationLayerName { get; set; }

        /// <summary>
        /// 为兼容旧代码保留。新版本默认不再使用被选边界图层作为注记图层。
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
                    AnnotationTemplate = "{图层名}表面积：{表面积}㎡",
                    CalculationMode = SurfaceAreaCalculationMode.CassCommand,
                    CassSurfaceLogPath = string.Empty,
                    DeleteCassGeneratedObjects = true,
                    AnnotationFontName = "宋体",
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
            if (!Success) return "\n[表面积标注] " + Message;

            bool isPlanArea = PlanAreaFallback || CalculationMode == SurfaceAreaCalculationMode.PlanArea;
            string modeText = isPlanArea ? "面积标注" : "CASS surfacearea";

            if (AsyncStarted)
            {
                return "\n[表面积标注] " + Message;
            }

            string text = "\n[表面积标注] 完成。计算方式：" + modeText
                + "；边界图层：" + BoundaryLayerName
                + "；注记图层：" + AnnotationLayerName
                + "；字体样式：" + AnnotationFontName
                + (isPlanArea ? "；面积：" : "；表面积：") + SurfaceArea.ToString("0.###") + "㎡"
                + (isPlanArea ? string.Empty : "；平面面积：" + PlanArea.ToString("0.###") + "㎡");

            if (PlanAreaFallback)
            {
                text += "；提示：" + (string.IsNullOrWhiteSpace(Message) ? "已改为面积标注" : Message.TrimEnd('。'));
                if (CassGeneratedObjectCount > 0 || CassDeletedObjectCount > 0)
                {
                    text += "；CASS生成对象：" + CassGeneratedObjectCount + "；已删除：" + CassDeletedObjectCount;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(CassSurfaceLogPath)) text += "；CASS结果文件：" + CassSurfaceLogPath;
                if (CassGeneratedObjectCount > 0 || CassDeletedObjectCount > 0)
                {
                    text += "；CASS生成对象：" + CassGeneratedObjectCount + "；已删除：" + CassDeletedObjectCount;
                }
            }

            return text + "。";
        }
    }
}
