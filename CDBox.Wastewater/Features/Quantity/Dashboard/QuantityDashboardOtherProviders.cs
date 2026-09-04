using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using TCPipeAutoDraw.Modules.LayerManager;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    internal interface IQuantityDashboardOtherProvider
    {
        int Priority { get; }
        bool TryCreate(Entity entity, LayerMetadata metadata, out QuantityDashboardDetailRow detail);
    }

    internal static class QuantityDashboardOtherProviderRegistry
    {
        private static readonly List<IQuantityDashboardOtherProvider> Providers = new List<IQuantityDashboardOtherProvider>
        {
            new DN315WellQuantityProvider(),
            new SepticTankQuantityProvider(),
            new GenericOtherQuantityProvider()
        };

        static QuantityDashboardOtherProviderRegistry()
        {
            Providers.Sort(delegate(IQuantityDashboardOtherProvider a, IQuantityDashboardOtherProvider b)
            {
                return a.Priority.CompareTo(b.Priority);
            });
        }

        public static bool TryCreate(Entity entity, LayerMetadata metadata, out QuantityDashboardDetailRow detail)
        {
            detail = null;
            if (entity == null || !IsPointLikeFacility(entity)) return false;
            foreach (IQuantityDashboardOtherProvider provider in Providers)
            {
                if (provider.TryCreate(entity, metadata, out detail) && detail != null) return true;
            }
            return false;
        }

        internal static string BuildSearchText(Entity entity, LayerMetadata metadata)
        {
            return ((entity == null ? string.Empty : entity.Layer) + " "
                + (metadata == null ? string.Empty : metadata.ParentGroup) + " "
                + (metadata == null ? string.Empty : metadata.ParentClass) + " "
                + (metadata == null ? string.Empty : metadata.TagText)).Trim();
        }

        internal static bool ContainsAny(string text, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value) && text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static bool IsPointLikeFacility(Entity entity)
        {
            if (entity is BlockReference || entity is DBPoint || entity is Circle) return true;
            Polyline polyline = entity as Polyline;
            return polyline != null && polyline.Closed;
        }
    }

    internal sealed class DN315WellQuantityProvider : IQuantityDashboardOtherProvider
    {
        public int Priority { get { return 10; } }

        public bool TryCreate(Entity entity, LayerMetadata metadata, out QuantityDashboardDetailRow detail)
        {
            detail = null;
            string text = QuantityDashboardOtherProviderRegistry.BuildSearchText(entity, metadata);
            if (!QuantityDashboardOtherProviderRegistry.ContainsAny(text, "315井", "DN315", "Φ315", "φ315")) return false;

            const double length = 0.80;
            const double width = 0.80;
            const double depth = 1.00;
            const double cushion = 0.10;
            double area = length * width;
            double wellArea = Math.PI * Math.Pow(0.315 / 2.0, 2.0);
            detail = new QuantityDashboardDetailRow
            {
                handle = entity.Handle.ToString(),
                category = "其他",
                subcategory = "315井",
                name = "315井",
                layerName = entity.Layer,
                specification = "DN315",
                depth = depth,
                width = width,
                excavationVolume = Round(area * (depth + cushion)),
                backfillVolume = Round(Math.Max(0.0, area - wellArea) * depth),
                beddingVolume = Round(area * cushion),
                count = 1,
                source = QuantityDashboardSources.DefaultEstimate,
                status = "默认参数估算"
            };
            return true;
        }

        private static double Round(double value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }

    internal sealed class SepticTankQuantityProvider : IQuantityDashboardOtherProvider
    {
        public int Priority { get { return 20; } }

        public bool TryCreate(Entity entity, LayerMetadata metadata, out QuantityDashboardDetailRow detail)
        {
            detail = null;
            string text = QuantityDashboardOtherProviderRegistry.BuildSearchText(entity, metadata);
            if (!QuantityDashboardOtherProviderRegistry.ContainsAny(text, "化粪池")) return false;

            string model = QuantityDashboardOtherProviderRegistry.ContainsAny(text, "3号", "3#", "3型") ? "3号"
                : QuantityDashboardOtherProviderRegistry.ContainsAny(text, "2号", "2#", "2型") ? "2号" : "通用";
            double excavation = model == "3号" ? 48.0 : model == "2号" ? 34.0 : 24.0;
            double backfill = model == "3号" ? 16.0 : model == "2号" ? 12.0 : 8.0;
            double bedding = model == "3号" ? 4.2 : model == "2号" ? 3.2 : 2.4;
            double concrete = model == "3号" ? 6.5 : model == "2号" ? 5.0 : 3.8;
            detail = new QuantityDashboardDetailRow
            {
                handle = entity.Handle.ToString(),
                category = "其他",
                subcategory = "化粪池",
                name = "化粪池 " + model,
                layerName = entity.Layer,
                specification = model,
                excavationVolume = excavation,
                backfillVolume = backfill,
                beddingVolume = bedding,
                concreteVolume = concrete,
                count = 1,
                source = QuantityDashboardSources.DefaultEstimate,
                status = "型号默认参数估算"
            };
            return true;
        }
    }

    internal sealed class GenericOtherQuantityProvider : IQuantityDashboardOtherProvider
    {
        public int Priority { get { return 100; } }

        public bool TryCreate(Entity entity, LayerMetadata metadata, out QuantityDashboardDetailRow detail)
        {
            detail = null;
            string text = QuantityDashboardOtherProviderRegistry.BuildSearchText(entity, metadata);
            if (!QuantityDashboardOtherProviderRegistry.ContainsAny(text, "雨水口", "阀门", "构筑物", "设施", "水表", "消火栓")) return false;
            string category = metadata == null ? string.Empty : metadata.ParentClass;
            if (string.IsNullOrWhiteSpace(category)) category = metadata == null ? string.Empty : metadata.ParentGroup;
            if (string.IsNullOrWhiteSpace(category)) category = "其他设施";
            detail = new QuantityDashboardDetailRow
            {
                handle = entity.Handle.ToString(),
                category = "其他",
                subcategory = category.Trim(),
                name = category.Trim(),
                layerName = entity.Layer,
                count = 1,
                source = QuantityDashboardSources.CountOnly,
                status = "仅数量统计"
            };
            return true;
        }
    }
}
