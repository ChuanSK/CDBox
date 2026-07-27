using System.Collections.Generic;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioAnnotationSettingsEnvelope
    {
        public CDBoxStudioAnnotationSettingsValues current { get; set; }
        public CDBoxStudioAnnotationSettingsValues defaults { get; set; }
        public List<string> textStyles { get; set; }
        public List<CDBoxStudioAnnotationSelectOption> layerLinkModes { get; set; }
        public List<CDBoxStudioAnnotationColorOption> colors { get; set; }
    }

    internal sealed class CDBoxStudioAnnotationSettingsValues
    {
        public CDBoxStudioSurfaceAnnotationSettings surface { get; set; }
        public CDBoxStudioPipeLengthAnnotationSettings pipeLength { get; set; }
        public CDBoxStudioNodeAnnotationSettings node { get; set; }
    }

    internal sealed class CDBoxStudioSurfaceAnnotationSettings
    {
        public double boundaryInterval { get; set; }
        public string calculationMode { get; set; }
        public bool keepCassGeneratedObjects { get; set; }
        public double textHeight { get; set; }
        public int decimalPlaces { get; set; }
        public string annotationFontName { get; set; }
        public string annotationTemplate { get; set; }
    }

    internal sealed class CDBoxStudioPipeLengthAnnotationSettings
    {
        public double textHeight { get; set; }
        public int decimalPlaces { get; set; }
        public string annotationFontName { get; set; }
        public string annotationTemplate { get; set; }
        public bool enableSourceMetadataLayerLink { get; set; }
        public string layerLinkMode { get; set; }
        public string autoAnnotationLayerSuffix { get; set; }
        public string fallbackAnnotationLayerName { get; set; }
        public bool writeAutoAnnotationLayerMetadata { get; set; }
        public string annotationSplitTagText { get; set; }
        public bool drawBottomAnnotation { get; set; }
        public double excavationWidth { get; set; }
        public double excavationHeight { get; set; }
        public double excavationDepth { get; set; }
        public string bottomAnnotationTemplate { get; set; }
    }

    internal sealed class CDBoxStudioNodeAnnotationSettings
    {
        public double textHeight { get; set; }
        public int decimalPlaces { get; set; }
        public string annotationFontName { get; set; }
        public double lineSpacingFactor { get; set; }
        public short nodeNoColorIndex { get; set; }
        public short textColorIndex { get; set; }
        public short previewLeaderColorIndex { get; set; }
    }

    internal sealed class CDBoxStudioAnnotationSelectOption
    {
        public string value { get; set; }
        public string label { get; set; }
    }

    internal sealed class CDBoxStudioAnnotationColorOption
    {
        public short index { get; set; }
        public string name { get; set; }
        public string cssColor { get; set; }
        public string rgb { get; set; }
    }

    internal sealed class CDBoxStudioAnnotationColorPickerRequest
    {
        public string path { get; set; }
        public short index { get; set; }

        public CDBoxStudioAnnotationColorPickerRequest()
        {
            path = string.Empty;
            index = 7;
        }
    }

    internal sealed class CDBoxStudioAnnotationColorSelection
    {
        public string path { get; set; }
        public short index { get; set; }
        public string name { get; set; }
        public string cssColor { get; set; }
        public string rgb { get; set; }

        public CDBoxStudioAnnotationColorSelection()
        {
            path = string.Empty;
            name = string.Empty;
            cssColor = string.Empty;
            rgb = string.Empty;
        }
    }

    internal sealed class CDBoxStudioAnnotationDefaultResult
    {
        public string section { get; set; }
        public object value { get; set; }
    }
}
