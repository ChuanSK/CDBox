using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.Modules.LongitudinalProfile
{
    internal sealed class LongitudinalProfileRowLayout
    {
        public LongitudinalProfileRowSettings Settings { get; set; }
        public double Bottom { get; set; }
        public double Top { get; set; }
        public double Center { get { return (Bottom + Top) / 2.0; } }
    }

    internal sealed class LongitudinalProfileLayout
    {
        public double HeaderLeft { get; set; }
        public double HeaderRight { get; set; }
        public double DataLeft { get; set; }
        public double StaffLeft { get; set; }
        public double StaffRight { get; set; }
        public double PlotLeft { get; set; }
        public double PlotRight { get; set; }
        public double TableBottom { get; set; }
        public double TableTop { get; set; }
        public double ChartBottom { get; set; }
        public double ChartTop { get; set; }
        public double DatumElevation { get; set; }
        public double TopElevation { get; set; }
        public double HorizontalFactor { get; set; }
        public double VerticalFactor { get; set; }
        public List<LongitudinalProfileRowLayout> Rows { get; set; }

        public double Width { get { return PlotRight - HeaderLeft; } }
        public double Height { get { return ChartTop - TableBottom; } }

        public LongitudinalProfileLayout()
        {
            Rows = new List<LongitudinalProfileRowLayout>();
        }

        public double X(double cumulativeDistance)
        {
            return PlotLeft + cumulativeDistance * HorizontalFactor;
        }

        public double Y(double elevation)
        {
            return ChartBottom
                + (elevation - DatumElevation) * VerticalFactor;
        }

        public LongitudinalProfileRowLayout Row(string key)
        {
            return Rows.First(x => string.Equals(x.Settings.Key, key,
                StringComparison.OrdinalIgnoreCase));
        }
    }

    internal static class LongitudinalProfileLayoutCalculator
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

            var layout = new LongitudinalProfileLayout
            {
                HeaderLeft = 0.0,
                HeaderRight = settings.HeaderWidth,
                DataLeft = settings.HeaderWidth + settings.HeaderChartGap,
                TableBottom = 0.0,
                HorizontalFactor = 1000.0 / settings.HorizontalScale,
                VerticalFactor = 1000.0 / settings.VerticalScale
            };

            double totalRows = settings.Rows.Sum(x => x.Height);
            layout.TableTop = totalRows;
            double top = totalRows;
            foreach (LongitudinalProfileRowSettings row in settings.Rows)
            {
                layout.Rows.Add(new LongitudinalProfileRowLayout
                {
                    Settings = row,
                    Bottom = top - row.Height,
                    Top = top
                });
                top -= row.Height;
            }

            layout.ChartBottom =
                layout.TableTop + settings.HeaderChartGap;
            double min = profile.Nodes.Min(x =>
                Math.Min(x.DesignInvertElevation, x.GroundElevation));
            double max = profile.Nodes.Max(x =>
                Math.Max(x.DesignInvertElevation, x.GroundElevation));
            double interval = settings.ElevationGridInterval;
            layout.DatumElevation = Math.Floor(
                (min - settings.ElevationPadding) / interval) * interval;
            layout.TopElevation = Math.Ceiling(
                (max + settings.ElevationPadding) / interval) * interval;
            if (layout.TopElevation <= layout.DatumElevation)
                layout.TopElevation =
                    layout.DatumElevation + Math.Max(interval, 1.0);
            layout.ChartTop = layout.ChartBottom
                + (layout.TopElevation - layout.DatumElevation)
                * layout.VerticalFactor;

            layout.StaffLeft = layout.DataLeft;
            layout.StaffRight = layout.StaffLeft + 4.0;
            layout.PlotLeft = layout.StaffRight + 4.0;
            double distance = profile.Nodes.Max(x => x.CumulativeDistance);
            double plotWidth = Math.Max(15.0,
                distance * layout.HorizontalFactor);
            layout.PlotRight = layout.PlotLeft + plotWidth;
            return layout;
        }
    }
}
