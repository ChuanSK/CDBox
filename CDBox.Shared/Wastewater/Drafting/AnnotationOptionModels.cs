namespace TCPipeAutoDraw.Modules.SurfaceAreaAnnotation
{
    public enum AnnotationLayerMode
    {
        DefaultZJ = 0,
        ExistingLayer = 1,
        CustomLayer = 2
    }
}

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    using TCPipeAutoDraw.Modules.SurfaceAreaAnnotation;

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
        public bool EnableSourceMetadataLayerLink { get; set; }
        public AnnotationLayerLinkMode LayerLinkMode { get; set; }
        public string AutoAnnotationLayerSuffix { get; set; }
        public string FallbackAnnotationLayerName { get; set; }
        public bool WriteAutoAnnotationLayerMetadata { get; set; }
        public string AnnotationSplitTagText { get; set; }
        public bool DrawBottomAnnotation { get; set; }
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
                    BottomAnnotationTemplate =
                        "开挖：长{长度}m、宽{宽}m、高{深}m",
                    ExcavationWidth = 0.0,
                    ExcavationHeight = 0.0,
                    ExcavationDepth = 0.0
                };
            }
        }
    }
}

namespace TCPipeAutoDraw.Modules.NodeAnnotation
{
    public sealed class NodeAnnotationOptions
    {
        public const double DefaultLineSpacingFactor = 1.45;
        public double TextHeight { get; set; }
        public int DecimalPlaces { get; set; }
        public string AnnotationFontName { get; set; }
        public string AnnotationLayerName { get; set; }
        public double LineSpacingFactor { get; set; }
        public short NodeNoColorIndex { get; set; }
        public short TextColorIndex { get; set; }
        public short PreviewLeaderColorIndex { get; set; }

        public static NodeAnnotationOptions Default
        {
            get
            {
                return new NodeAnnotationOptions
                {
                    TextHeight = 1.0,
                    DecimalPlaces = 2,
                    AnnotationFontName = "宋体",
                    AnnotationLayerName = "ZJ",
                    LineSpacingFactor = DefaultLineSpacingFactor,
                    NodeNoColorIndex = 1,
                    TextColorIndex = 7,
                    PreviewLeaderColorIndex = 1
                };
            }
        }
    }

    public sealed class NodeAnnotationLine
    {
        public string Text { get; set; }
        public short ColorIndex { get; set; }

        public NodeAnnotationLine()
        {
            Text = string.Empty;
            ColorIndex = 7;
        }

        public NodeAnnotationLine(string text, short colorIndex)
        {
            Text = text ?? string.Empty;
            ColorIndex = colorIndex;
        }
    }
}
