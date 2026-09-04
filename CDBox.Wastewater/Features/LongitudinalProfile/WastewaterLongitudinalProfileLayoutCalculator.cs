using System;
using System.Linq;
using TCPipeAutoDraw.Modules.LongitudinalProfile;

namespace CDBox.Wastewater.Features.LongitudinalProfile
{
    internal static class WastewaterLongitudinalProfileLayoutCalculator
    {
        public static LongitudinalProfileLayout Calculate(
            LongitudinalProfileData profile,
            LongitudinalProfileSettings sourceSettings)
        {
            if (profile == null || profile.Nodes == null
                || profile.Nodes.Count < 2)
                throw new ArgumentException("纵断面至少需要两个井节点。",
                    "profile");
            LongitudinalProfileSettings settings =
                (sourceSettings ?? new LongitudinalProfileSettings()).Clone();
            settings.Normalize();

            double outputScale =
                LongitudinalProfileLayoutCalculator.ReferenceOutputScale;
            var layout = new LongitudinalProfileLayout
            {
                HeaderLeft = 0.0,
                HeaderRight = settings.HeaderWidth * outputScale,
                TableBottom = 0.0,
                HorizontalFactor = 1000.0 / settings.HorizontalScale
                    * outputScale,
                VerticalFactor = 1000.0 / settings.VerticalScale
                    * outputScale,
                OutputScale = outputScale
            };
            layout.DataLeft = layout.HeaderRight
                + settings.HeaderChartGap * outputScale;

            double totalRows = settings.Rows.Sum(x => x.Height)
                * outputScale;
            layout.TableTop = totalRows;
            double top = totalRows;
            foreach (LongitudinalProfileRowSettings row in settings.Rows)
            {
                layout.Rows.Add(new LongitudinalProfileRowLayout
                {
                    Settings = row,
                    Bottom = top - row.Height * outputScale,
                    Top = top
                });
                top -= row.Height * outputScale;
            }

            layout.ChartBottom = layout.TableTop
                + LongitudinalProfileLayoutCalculator.ChartGap;
            double min = profile.Nodes.Min(x =>
                Math.Min(x.GroundElevation - x.WellDepth,
                    Math.Min(x.DesignInvertElevation,
                        x.GroundElevation)));
            double max = profile.Nodes.Max(x =>
                Math.Max(x.DesignInvertElevation, x.GroundElevation));
            double interval = settings.ElevationGridInterval;
            layout.DatumElevation = Math.Floor(
                (min - settings.ElevationPadding * 2.0) / interval)
                * interval;
            layout.TopElevation = Math.Ceiling(
                (max + settings.ElevationPadding) / interval) * interval;
            if (layout.TopElevation <= layout.DatumElevation)
                layout.TopElevation = layout.DatumElevation
                    + Math.Max(interval, 1.0);
            layout.ChartTop = layout.ChartBottom
                + (layout.TopElevation - layout.DatumElevation)
                * layout.VerticalFactor;

            layout.StaffRight = layout.HeaderRight;
            layout.StaffLeft = layout.StaffRight
                - LongitudinalProfileLayoutCalculator.ElevationStaffWidth;
            layout.PlotLeft = layout.DataLeft;
            double distance = profile.Nodes.Max(x => x.CumulativeDistance);
            layout.DataRight = layout.X(distance);
            double horizontalInterval = settings.HorizontalGridInterval;
            double roundedDistance = Math.Ceiling(
                Math.Max(distance, horizontalInterval) / horizontalInterval)
                * horizontalInterval;
            double plotWidth = roundedDistance * layout.HorizontalFactor;
            layout.PlotRight = layout.PlotLeft + plotWidth;
            double requiredRight = layout.DataRight
                + LongitudinalProfileLayoutCalculator.WellHalfWidth;
            if (profile.EndExtension != null)
            {
                requiredRight += LongitudinalProfileLayoutCalculator
                    .BoundaryExtensionLength
                    + LongitudinalProfileLayoutCalculator
                        .BoundaryBreakHalfSize;
            }
            double gridWidth = horizontalInterval
                * layout.HorizontalFactor;
            while (layout.PlotRight < requiredRight - 1e-8)
                layout.PlotRight += gridWidth;
            return layout;
        }
    }
}
