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
        /// ??????????????????????????????????/???????
        /// </summary>
        public bool EnableSourceMetadataLayerLink { get; set; }

        /// <summary>
        /// ?????????????? ParentGroup??? -> ??????? -> ?????
        /// </summary>
        public AnnotationLayerLinkMode LayerLinkMode { get; set; }

        /// <summary>
        /// ????????????????
        /// </summary>
        public string AutoAnnotationLayerSuffix { get; set; }

        /// <summary>
        /// ??????????/?????????
        /// </summary>
        public string FallbackAnnotationLayerName { get; set; }

        /// <summary>
        /// ?????????????? CDBox ???/???
        /// </summary>
        public bool WriteAutoAnnotationLayerMetadata { get; set; }

        /// <summary>
        /// ????????????????/??/????????????????????
        /// </summary>
        public string AnnotationSplitTagText { get; set; }

        /// <summary>
        /// ???????????????????
        /// </summary>
        public bool DrawBottomAnnotation { get; set; }

        /// <summary>
        /// ???????????
        /// ???{??}/{?}/{Length}?{?}/{Width}?{?}/{Height}?{?}/{Depth}?{???}?
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
                    AnnotationTemplate = "{???}???{??}m",
                    AnnotationFontName = "??",
                    LayerMode = AnnotationLayerMode.DefaultZJ,
                    SelectedLayerName = "ZJ",
                    AnnotationLayerName = "ZJ",
                    DrawLeader = true,
                    EnableSourceMetadataLayerLink = false,
                    LayerLinkMode = AnnotationLayerLinkMode.ParentGroup,
                    AutoAnnotationLayerSuffix = "??",
                    FallbackAnnotationLayerName = "?????",
                    WriteAutoAnnotationLayerMetadata = true,
                    AnnotationSplitTagText = "????????????",
                    DrawBottomAnnotation = false,
                    BottomAnnotationTemplate = "????{??}m??{?}m??{?}m",
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
            if (!Success) return "\n[??????] " + Message;

            string meta = string.Empty;
            if (!string.IsNullOrWhiteSpace(PipeParentGroup)) meta += "?????" + PipeParentGroup;
            if (!string.IsNullOrWhiteSpace(PipeParentClass)) meta += "????" + PipeParentClass;

            return "\n[??????] ????????" + PipeLayerName
                + meta
                + "??????" + AnnotationLayerName
                + "??????" + AnnotationFontName
                + "????" + Length.ToString("0.###") + "m?";
        }
    }
}
