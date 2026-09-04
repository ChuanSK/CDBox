using System;
using System.Collections.Generic;
using CDBox.Shared.Wastewater.Drafting;
using TCPipeAutoDraw.Modules.SectionDrawing;

namespace CDBox.Wastewater.Features.SectionDrawing
{
    public sealed class WastewaterSectionDrawingEngine
        : IWastewaterSectionDrawingEngine
    {
        public SectionDrawingOptions LoadOptions()
        {
            return SectionDrawingSettingsStore.Load();
        }

        public void SaveOptions(SectionDrawingOptions options)
        {
            SectionDrawingSettingsStore.Save(options);
        }

        public void Normalize(SectionDrawingOptions options)
        {
            NormalizeCore(options);
        }

        public SectionDrawingOptions CreateScaledOptions(
            SectionDrawingOptions source)
        {
            return WastewaterSectionDrawingScaleService
                .CreateScaledOptions(source);
        }

        public List<SectionBatchPlanGroup> PlanBatch(
            IEnumerable<SectionBatchSourceData> sources,
            SectionDrawingOptions baseOptions)
        {
            return WastewaterSectionBatchPlanner.Plan(sources, baseOptions);
        }

        internal static void NormalizeCore(SectionDrawingOptions options)
        {
            if (options == null) return;
            if (options.Width <= 0) options.Width = 1.0;
            if (options.TotalHeight <= 0) options.TotalHeight = 0.0;
            if (options.DrawingScale <= 0) options.DrawingScale = 1.0;
            options.DrawingScale = Math.Max(0.0001,
                Math.Min(10000.0, options.DrawingScale));
            if (options.TextHeight <= 0) options.TextHeight = 0.08;
            if (options.LeftLabelWidth <= 0) options.LeftLabelWidth = 0.45;
            if (options.TopDimensionOffset < 0)
                options.TopDimensionOffset = 0.12;
            if (options.BottomDimensionOffset < 0)
                options.BottomDimensionOffset = 0.12;
            if (options.RightDimensionOffset < 0)
                options.RightDimensionOffset = 0.08;
            if (options.TitleOffset < 0) options.TitleOffset = 0.12;
            if (string.IsNullOrWhiteSpace(options.BorderLayerName))
                options.BorderLayerName =
                    SectionDrawingOptions.Default.BorderLayerName;
            if (string.IsNullOrWhiteSpace(options.TextLayerName))
                options.TextLayerName = options.BorderLayerName;
            if (string.IsNullOrWhiteSpace(options.HatchLayerName))
                options.HatchLayerName = options.BorderLayerName;
            if (string.IsNullOrWhiteSpace(options.DimensionLayerName))
                options.DimensionLayerName = options.BorderLayerName;
            if (options.Pipe == null) options.Pipe = SectionPipeOptions.Default;
            if (options.Layers == null)
                options.Layers = new List<SectionLayerOptions>();
            if (options.Layers.Count == 0)
                options.Layers.Add(new SectionLayerOptions
                {
                    LeftLabel = "自定义层",
                    Height = 0.10
                });

            for (int i = 0; i < options.Layers.Count; i++)
            {
                if (options.Layers[i] == null)
                    options.Layers[i] = new SectionLayerOptions();
                SectionLayerOptions layer = options.Layers[i];
                if (layer.Height <= 0) layer.Height = 0.10;
                if (layer.HatchScale < 0) layer.HatchScale = 1.0;
                if (layer.Pipes == null)
                    layer.Pipes = new List<SectionPipeOptions>();
                for (int j = layer.Pipes.Count - 1; j >= 0; j--)
                    if (layer.Pipes[j] == null) layer.Pipes.RemoveAt(j);
                for (int j = 0; j < layer.Pipes.Count; j++)
                {
                    SectionPipeOptions pipe = layer.Pipes[j];
                    if (pipe.Diameter <= 0) pipe.Diameter = 0.30;
                    if (string.IsNullOrWhiteSpace(pipe.PipeText))
                        pipe.PipeText = SectionPipeOptions.BuildPipeText(
                            pipe.Diameter);
                    pipe.HostLayerIndex = i;
                }
            }

            options.LeftLabelWidth = Math.Max(options.LeftLabelWidth,
                EstimateRequiredLeftLabelWidth(options));
            if (!options.LockTotalHeight || options.TotalHeight <= 0)
            {
                double total = 0.0;
                foreach (SectionLayerOptions layer in options.Layers)
                    if (layer != null && layer.Height > 0)
                        total += layer.Height;
                options.TotalHeight = total;
            }

            bool hasLayerPipe = false;
            foreach (SectionLayerOptions layer in options.Layers)
                if (layer != null && layer.Pipes != null &&
                    layer.Pipes.Count > 0)
                {
                    hasLayerPipe = true;
                    break;
                }
            if (!hasLayerPipe && options.DrawPipeCircle &&
                options.Pipe != null && options.Pipe.Diameter > 0)
            {
                int index = options.Pipe.HostLayerIndex;
                if (index < 0) index = 0;
                if (index >= options.Layers.Count)
                    index = options.Layers.Count - 1;
                SectionPipeOptions migrated = options.Pipe.Clone();
                migrated.HostLayerIndex = index;
                options.Layers[index].Pipes.Add(migrated);
            }
        }

        private static double EstimateRequiredLeftLabelWidth(
            SectionDrawingOptions options)
        {
            if (options == null || options.Layers == null) return 0.45;
            double max = 0.45;
            double baseTextHeight = Math.Max(0.01,
                options.TextHeight <= 0 ? 0.08 : options.TextHeight);
            foreach (SectionLayerOptions layer in options.Layers)
            {
                if (layer == null || !layer.DrawLayer ||
                    string.IsNullOrWhiteSpace(layer.LeftLabel)) continue;
                double layerHeight = layer.Height <= 0 ? 0.10 : layer.Height;
                double desiredHeight = Math.Min(
                    Math.Max(0.01, baseTextHeight * 1.15),
                    Math.Max(0.01, layerHeight * 0.58));
                double width = desiredHeight *
                    GetWeightedTextLength(layer.LeftLabel.Trim()) * 1.02 +
                    Math.Max(baseTextHeight * 1.50, 0.09) + 0.08;
                if (width > max) max = width;
            }
            return Math.Min(Math.Max(max, 0.45), 2.5);
        }

        private static double GetWeightedTextLength(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1.0;
            double length = 0.0;
            foreach (char value in text)
            {
                if (char.IsWhiteSpace(value)) length += 0.35;
                else if (value < 128) length += 0.72;
                else length += 1.22;
            }
            return Math.Max(1.0, length);
        }
    }
}
