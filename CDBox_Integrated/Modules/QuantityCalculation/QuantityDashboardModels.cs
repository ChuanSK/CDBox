using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    public static class QuantityDashboardSources
    {
        public const string ExactGeometry = "精确几何计算";
        public const string Property = "属性计算";
        public const string DefaultEstimate = "默认参数估算";
        public const string CountOnly = "仅数量统计";
        public const string Failed = "无法计算";
    }

    public sealed class QuantityDashboardRequest
    {
        public string documentId { get; set; }
        public string scopeType { get; set; }
        public string regionId { get; set; }
        public bool liveMode { get; set; }

        public QuantityDashboardRequest()
        {
            documentId = string.Empty;
            scopeType = "whole";
            regionId = string.Empty;
            liveMode = true;
        }
    }

    public sealed class QuantityDashboardContext
    {
        public List<QuantityDashboardDocumentInfo> documents { get; set; }
        public List<QuantityDashboardRegionInfo> regions { get; set; }
        public QuantityDashboardRequest request { get; set; }
        public QuantityDashboardSnapshot cachedSnapshot { get; set; }

        public QuantityDashboardContext()
        {
            documents = new List<QuantityDashboardDocumentInfo>();
            regions = new List<QuantityDashboardRegionInfo>();
            request = new QuantityDashboardRequest();
        }
    }

    public sealed class QuantityDashboardDocumentInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public string path { get; set; }
        public bool isActive { get; set; }

        public QuantityDashboardDocumentInfo()
        {
            id = string.Empty;
            name = string.Empty;
            path = string.Empty;
        }
    }

    public sealed class QuantityDashboardRegionInfo
    {
        public string regionId { get; set; }
        public string regionName { get; set; }
        public string handle { get; set; }
        public string createdAt { get; set; }
        public bool boundaryValid { get; set; }

        public QuantityDashboardRegionInfo()
        {
            regionId = string.Empty;
            regionName = string.Empty;
            handle = string.Empty;
            createdAt = string.Empty;
            boundaryValid = true;
        }
    }

    public sealed class QuantityDashboardSnapshot
    {
        public string cacheKey { get; set; }
        public QuantityDashboardDocumentInfo document { get; set; }
        public QuantityDashboardScopeInfo scope { get; set; }
        public QuantityDashboardStatus status { get; set; }
        public QuantityDashboardSummary summary { get; set; }
        public QuantityDashboardCategorySummary wells { get; set; }
        public QuantityDashboardCategorySummary mainPipes { get; set; }
        public QuantityDashboardCategorySummary branchPipes { get; set; }
        public QuantityDashboardCategorySummary others { get; set; }
        public QuantityDashboardSourceSummary sourceSummary { get; set; }
        public List<QuantityDashboardReferenceItem> referenceItems { get; set; }
        public List<QuantityDashboardQualityIssue> qualityIssues { get; set; }
        public List<QuantityDashboardDetailRow> details { get; set; }
        public QuantityDashboardCharts charts { get; set; }
        public List<string> warnings { get; set; }

        public QuantityDashboardSnapshot()
        {
            cacheKey = string.Empty;
            document = new QuantityDashboardDocumentInfo();
            scope = new QuantityDashboardScopeInfo();
            status = new QuantityDashboardStatus();
            summary = new QuantityDashboardSummary();
            wells = new QuantityDashboardCategorySummary { key = "wells", title = "井类" };
            mainPipes = new QuantityDashboardCategorySummary { key = "mainPipes", title = "主管" };
            branchPipes = new QuantityDashboardCategorySummary { key = "branchPipes", title = "支管" };
            others = new QuantityDashboardCategorySummary { key = "others", title = "其他" };
            sourceSummary = new QuantityDashboardSourceSummary();
            referenceItems = new List<QuantityDashboardReferenceItem>();
            qualityIssues = new List<QuantityDashboardQualityIssue>();
            details = new List<QuantityDashboardDetailRow>();
            charts = new QuantityDashboardCharts();
            warnings = new List<string>();
        }
    }

    public sealed class QuantityDashboardScopeInfo
    {
        public string type { get; set; }
        public string regionId { get; set; }
        public string regionName { get; set; }
        public string pipeRule { get; set; }
        public string pointRule { get; set; }

        public QuantityDashboardScopeInfo()
        {
            type = "whole";
            regionId = string.Empty;
            regionName = "整张图纸";
            pipeRule = "相交管线按整条计入";
            pointRule = "点状对象按中心点计入";
        }
    }

    public sealed class QuantityDashboardStatus
    {
        public bool isLive { get; set; }
        public bool isCalculating { get; set; }
        public bool fromCache { get; set; }
        public bool latestFailed { get; set; }
        public string updatedAt { get; set; }
        public double dataCompleteness { get; set; }
        public int issueCount { get; set; }
        public int totalObjectCount { get; set; }
        public int calculatedObjectCount { get; set; }
        public int failedObjectCount { get; set; }
        public string message { get; set; }

        public QuantityDashboardStatus()
        {
            isLive = true;
            updatedAt = string.Empty;
            message = string.Empty;
        }
    }

    public sealed class QuantityDashboardSummary
    {
        public double totalPipeLength { get; set; }
        public double mainPipeLength { get; set; }
        public double branchPipeLength { get; set; }
        public double excavationVolume { get; set; }
        public double backfillVolume { get; set; }
        public double beddingVolume { get; set; }
        public double restorationArea { get; set; }
        public double concreteVolume { get; set; }
        public double sandVolume { get; set; }
        public double gravelVolume { get; set; }
        public int facilityCount { get; set; }
        public int wellCount { get; set; }
        public int otherFacilityCount { get; set; }
    }

    public sealed class QuantityDashboardCategorySummary
    {
        public string key { get; set; }
        public string title { get; set; }
        public int count { get; set; }
        public double length { get; set; }
        public double averageDepth { get; set; }
        public double excavationVolume { get; set; }
        public double backfillVolume { get; set; }
        public double pipeDeductionVolume { get; set; }
        public double beddingVolume { get; set; }
        public double restorationArea { get; set; }
        public double concreteVolume { get; set; }
        public double sandVolume { get; set; }
        public double gravelVolume { get; set; }
        public double originalSoilVolume { get; set; }
        public double cumulativeDepth { get; set; }
        public int coverCount { get; set; }
        public double exposedPipeLength { get; set; }
        public double coBuriedLength { get; set; }
        public Dictionary<string, double> byMaterial { get; set; }
        public Dictionary<string, double> bySpecification { get; set; }
        public Dictionary<string, double> byType { get; set; }
        public Dictionary<string, double> byLayerMaterial { get; set; }

        public QuantityDashboardCategorySummary()
        {
            key = string.Empty;
            title = string.Empty;
            byMaterial = new Dictionary<string, double>(StringComparer.CurrentCultureIgnoreCase);
            bySpecification = new Dictionary<string, double>(StringComparer.CurrentCultureIgnoreCase);
            byType = new Dictionary<string, double>(StringComparer.CurrentCultureIgnoreCase);
            byLayerMaterial = new Dictionary<string, double>(StringComparer.CurrentCultureIgnoreCase);
        }
    }

    public sealed class QuantityDashboardSourceSummary
    {
        public int propertyOrGeometryCount { get; set; }
        public int defaultEstimateCount { get; set; }
        public int countOnlyCount { get; set; }
        public int failedCount { get; set; }
        public double propertyOrGeometryPercent { get; set; }
        public double defaultEstimatePercent { get; set; }
        public double countOnlyPercent { get; set; }
        public double failedPercent { get; set; }
    }

    public sealed class QuantityDashboardReferenceItem
    {
        public string item { get; set; }
        public double quantity { get; set; }
        public string unit { get; set; }
        public string remark { get; set; }
        public string source { get; set; }

        public QuantityDashboardReferenceItem()
        {
            item = string.Empty;
            unit = string.Empty;
            remark = string.Empty;
            source = string.Empty;
        }
    }

    public sealed class QuantityDashboardDetailRow
    {
        public string id { get; set; }
        public string handle { get; set; }
        public string category { get; set; }
        public string subcategory { get; set; }
        public string name { get; set; }
        public string layerName { get; set; }
        public string material { get; set; }
        public string specification { get; set; }
        public string startNode { get; set; }
        public string endNode { get; set; }
        public double length { get; set; }
        public double depth { get; set; }
        public double width { get; set; }
        public double excavationVolume { get; set; }
        public double backfillVolume { get; set; }
        public double beddingVolume { get; set; }
        public double restorationArea { get; set; }
        public double concreteVolume { get; set; }
        public int count { get; set; }
        public string source { get; set; }
        public string status { get; set; }
        public List<string> issueCodes { get; set; }

        public QuantityDashboardDetailRow()
        {
            id = Guid.NewGuid().ToString("N");
            handle = string.Empty;
            category = string.Empty;
            subcategory = string.Empty;
            name = string.Empty;
            layerName = string.Empty;
            material = string.Empty;
            specification = string.Empty;
            startNode = string.Empty;
            endNode = string.Empty;
            source = QuantityDashboardSources.Property;
            status = "正常";
            issueCodes = new List<string>();
        }
    }

    public sealed class QuantityDashboardQualityIssue
    {
        public string code { get; set; }
        public string title { get; set; }
        public string severity { get; set; }
        public string category { get; set; }
        public int count { get; set; }
        public string message { get; set; }
        public List<string> handles { get; set; }

        public QuantityDashboardQualityIssue()
        {
            code = string.Empty;
            title = string.Empty;
            severity = "warning";
            category = string.Empty;
            message = string.Empty;
            handles = new List<string>();
        }
    }

    public sealed class QuantityDashboardChartItem
    {
        public string name { get; set; }
        public double value { get; set; }
        public string unit { get; set; }
        public string filter { get; set; }

        public QuantityDashboardChartItem()
        {
            name = string.Empty;
            unit = string.Empty;
            filter = string.Empty;
        }
    }

    public sealed class QuantityDashboardCharts
    {
        public List<QuantityDashboardChartItem> facilities { get; set; }
        public List<QuantityDashboardChartItem> pipeLengths { get; set; }
        public List<QuantityDashboardChartItem> diametersMain { get; set; }
        public List<QuantityDashboardChartItem> diametersBranch { get; set; }
        public List<QuantityDashboardChartItem> excavation { get; set; }
        public List<QuantityDashboardChartItem> backfill { get; set; }
        public List<QuantityDashboardChartItem> layerMaterials { get; set; }
        public List<QuantityDashboardChartItem> restorations { get; set; }

        public QuantityDashboardCharts()
        {
            facilities = new List<QuantityDashboardChartItem>();
            pipeLengths = new List<QuantityDashboardChartItem>();
            diametersMain = new List<QuantityDashboardChartItem>();
            diametersBranch = new List<QuantityDashboardChartItem>();
            excavation = new List<QuantityDashboardChartItem>();
            backfill = new List<QuantityDashboardChartItem>();
            layerMaterials = new List<QuantityDashboardChartItem>();
            restorations = new List<QuantityDashboardChartItem>();
        }
    }

    public sealed class QuantityDashboardObjectActionRequest
    {
        public string documentId { get; set; }
        public List<string> handles { get; set; }

        public QuantityDashboardObjectActionRequest()
        {
            documentId = string.Empty;
            handles = new List<string>();
        }
    }

    public sealed class QuantityDashboardRegionRequest
    {
        public string documentId { get; set; }
        public string regionId { get; set; }
        public string regionName { get; set; }

        public QuantityDashboardRegionRequest()
        {
            documentId = string.Empty;
            regionId = string.Empty;
            regionName = string.Empty;
        }
    }
}
