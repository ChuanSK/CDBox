using System.Collections.Generic;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioLayerManagerEnvelope
    {
        public bool success { get; set; }
        public string errorMessage { get; set; }
        public List<CDBoxStudioLayerRow> layers { get; set; }
        public List<string> parentOptions { get; set; }
        public Dictionary<string, List<string>> categoriesByParent { get; set; }
        public List<string> tagOptions { get; set; }

        public CDBoxStudioLayerManagerEnvelope()
        {
            success = true;
            errorMessage = string.Empty;
            layers = new List<CDBoxStudioLayerRow>();
            parentOptions = new List<string>();
            categoriesByParent = new Dictionary<string, List<string>>();
            tagOptions = new List<string>();
        }
    }

    internal sealed class CDBoxStudioLayerRow
    {
        public string name { get; set; }
        public string parent { get; set; }
        public string category { get; set; }
        public List<string> tags { get; set; }
        public bool isCurrent { get; set; }
        public bool isLocked { get; set; }
        public bool isFrozen { get; set; }
        public bool isOff { get; set; }
        public bool isDependent { get; set; }
        public bool isPlottable { get; set; }
        public int? objectCount { get; set; }
        public short colorIndex { get; set; }
        public string colorName { get; set; }
        public string colorHex { get; set; }
        public string colorType { get; set; }
        public string colorRgb { get; set; }
        public string linetype { get; set; }
        public string normalizedName { get; set; }
        public string suggestedName { get; set; }
        public string recognitionStatus { get; set; }
        public int confidencePercent { get; set; }
        public string recognitionSource { get; set; }
        public string recognitionExplanation { get; set; }
        public string recognitionConflicts { get; set; }
        public string recognitionMissingFields { get; set; }
        public string recognizedParent { get; set; }
        public string recognizedCategory { get; set; }
        public string objectType { get; set; }
        public string specification { get; set; }
        public string material { get; set; }
        public string constructionType { get; set; }
        public string nodeType { get; set; }
        public string structureType { get; set; }
        public string purpose { get; set; }

        public CDBoxStudioLayerRow()
        {
            name = string.Empty;
            parent = string.Empty;
            category = string.Empty;
            tags = new List<string>();
            colorName = string.Empty;
            colorHex = "#d1d5db";
            colorType = "IndexColor";
            colorRgb = string.Empty;
            linetype = string.Empty;
            normalizedName = string.Empty;
            suggestedName = string.Empty;
            recognitionStatus = string.Empty;
            recognitionSource = string.Empty;
            recognitionExplanation = string.Empty;
            recognitionConflicts = string.Empty;
            recognitionMissingFields = string.Empty;
            recognizedParent = string.Empty;
            recognizedCategory = string.Empty;
            objectType = string.Empty;
            specification = string.Empty;
            material = string.Empty;
            constructionType = string.Empty;
            nodeType = string.Empty;
            structureType = string.Empty;
            purpose = string.Empty;
        }
    }

    internal sealed class CDBoxStudioLayerMetadataSaveRequest
    {
        public List<CDBoxStudioLayerMetadataChange> changes { get; set; }
        public CDBoxStudioLayerMetadataSaveRequest() { changes = new List<CDBoxStudioLayerMetadataChange>(); }
    }

    internal sealed class CDBoxStudioLayerMetadataChange
    {
        public string name { get; set; }
        public string parent { get; set; }
        public string category { get; set; }
        public List<string> tags { get; set; }

        public CDBoxStudioLayerMetadataChange()
        {
            name = string.Empty;
            parent = string.Empty;
            category = string.Empty;
            tags = new List<string>();
        }
    }

    internal sealed class CDBoxStudioLayerFailure
    {
        public string layerName { get; set; }
        public string reason { get; set; }
    }

    internal sealed class CDBoxStudioLayerSaveResult
    {
        public bool success { get; set; }
        public int successCount { get; set; }
        public int failCount { get; set; }
        public List<string> savedLayers { get; set; }
        public List<CDBoxStudioLayerFailure> failures { get; set; }

        public CDBoxStudioLayerSaveResult()
        {
            savedLayers = new List<string>();
            failures = new List<CDBoxStudioLayerFailure>();
        }
    }

    internal sealed class CDBoxStudioLayerActionRequest
    {
        public string action { get; set; }
        public List<string> layers { get; set; }
        public bool forceUnlock { get; set; }
        public CDBoxStudioLayerActionRequest() { action = string.Empty; layers = new List<string>(); }
    }

    internal sealed class CDBoxStudioLayerRecognitionRequest
    {
        public List<string> layers { get; set; }
        public bool overwriteExisting { get; set; }
        public CDBoxStudioLayerRecognitionRequest() { layers = new List<string>(); }
    }

    internal sealed class CDBoxStudioLayerActionResult
    {
        public bool success { get; set; }
        public string action { get; set; }
        public string message { get; set; }
        public int successCount { get; set; }
        public int failCount { get; set; }
        public int skipCount { get; set; }
        public int objectCount { get; set; }
        public List<string> layers { get; set; }

        public CDBoxStudioLayerActionResult()
        {
            action = string.Empty;
            message = string.Empty;
            layers = new List<string>();
        }
    }
}
