using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using TCPipeAutoDraw.Modules.SurfaceAreaAnnotation;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    public enum AnnotationLayerLinkMode
    {
        ParentGroup = 0,
        ParentClass = 1,
        FirstMatchedTag = 2,
        ParentGroupAndTag = 3
    }

    public sealed class PipeLengthAnnotationOptions
    {
        public double TextHeight { get; set; }
        public int DecimalPlaces { get; set; }
        public string AnnotationTemplate { get; set; }
        public string AnnotationFontName { get; set; }
        public AnnotationLayerMode LayerMode { get; set; }
        public string SelectedLayerName { get; set; }
        public string AnnotationLayerName { get; set; }
        public bool DrawLeader { get; set; }

        /// <summary>
        /// 开启后，注记图层不再只按上方图层设置，而是根据被标注管线图层的父属性/标签自动分配。
        /// </summary>
        public bool EnableSourceMetadataLayerLink { get; set; }

        /// <summary>
        /// 自动分配方式。第一版主要使用 ParentGroup：主管 -> 主管注记，支管 -> 支管注记。
        /// </summary>
        public AnnotationLayerLinkMode LayerLinkMode { get; set; }

        /// <summary>
        /// 自动注记图层后缀。默认“注记”。
        /// </summary>
        public string AutoAnnotationLayerSuffix { get; set; }

        /// <summary>
        /// 被标注管线没有父属性/标签时使用的图层。
        /// </summary>
        public string FallbackAnnotationLayerName { get; set; }

        /// <summary>
        /// 自动创建注记图层时，同时写入 CDBox 父属性/标签。
        /// </summary>
        public bool WriteAutoAnnotationLayerMetadata { get; set; }

        /// <summary>
        /// 按标签分层时参与匹配的标签，逗号/顿号/分号分隔。为空时使用全部标签中的第一个。
        /// </summary>
        public string AnnotationSplitTagText { get; set; }

        /// <summary>
        /// 是否在横线下方额外生成开挖等补充注记。
        /// </summary>
        public bool DrawBottomAnnotation { get; set; }

        /// <summary>
        /// 横线下方补充注记模板。
        /// 支持：{长度}/{长}/{Length}、{宽}/{Width}、{高}/{Height}、{深}/{Depth}、{图层名}。
        /// </summary>
        public string BottomAnnotationTemplate { get; set; }

        public double ExcavationWidth { get; set; }
        public double ExcavationHeight { get; set; }
        public double ExcavationDepth { get; set; }

        public static PipeLengthAnnotationOptions Default
        {
            get
            {
                return new PipeLengthAnnotationOptions
                {
                    TextHeight = 1.0,
                    DecimalPlaces = 2,
                    AnnotationTemplate = "{图层名}长度：{长度}m",
                    AnnotationFontName = "宋体",
                    LayerMode = AnnotationLayerMode.DefaultZJ,
                    SelectedLayerName = "ZJ",
                    AnnotationLayerName = "ZJ",
                    DrawLeader = true,
                    EnableSourceMetadataLayerLink = false,
                    LayerLinkMode = AnnotationLayerLinkMode.ParentGroup,
                    AutoAnnotationLayerSuffix = "注记",
                    FallbackAnnotationLayerName = "未分类注记",
                    WriteAutoAnnotationLayerMetadata = true,
                    AnnotationSplitTagText = "明管、并埋、雨水、砼恢复",
                    DrawBottomAnnotation = false,
                    BottomAnnotationTemplate = "开挖：长{长度}m、宽{宽}m、高{深}m",
                    ExcavationWidth = 0.0,
                    ExcavationHeight = 0.0,
                    ExcavationDepth = 0.0
                };
            }
        }
    }

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
