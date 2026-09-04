using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.Modules.LongitudinalProfile
{
    public sealed class LongitudinalProfileRowLayout
    {
        public LongitudinalProfileRowSettings Settings { get; set; }
        public double Bottom { get; set; }
        public double Top { get; set; }
        public double Center { get { return (Bottom + Top) / 2.0; } }
    }

    public sealed class LongitudinalProfileLayout
    {
        public double HeaderLeft { get; set; }
        public double HeaderRight { get; set; }
        public double DataLeft { get; set; }
        public double StaffLeft { get; set; }
        public double StaffRight { get; set; }
        public double PlotLeft { get; set; }
        public double DataRight { get; set; }
        public double PlotRight { get; set; }
        public double TableBottom { get; set; }
        public double TableTop { get; set; }
        public double ChartBottom { get; set; }
        public double ChartTop { get; set; }
        public double DatumElevation { get; set; }
        public double TopElevation { get; set; }
        public double HorizontalFactor { get; set; }
        public double VerticalFactor { get; set; }
        public double OutputScale { get; set; }
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

        public double Scale(double value)
        {
            return value * OutputScale;
        }

        public LongitudinalProfileRowLayout Row(string key)
        {
            return Rows.First(x => string.Equals(x.Settings.Key, key,
                StringComparison.OrdinalIgnoreCase));
        }

        public bool TryRow(string key,
            out LongitudinalProfileRowLayout row)
        {
            row = Rows.FirstOrDefault(x => string.Equals(x.Settings.Key,
                key, StringComparison.OrdinalIgnoreCase));
            return row != null;
        }

        public IEnumerable<LongitudinalProfileRowLayout> RowsFor(string key)
        {
            return Rows.Where(x => string.Equals(x.Settings.Key, key,
                StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>纵断面版式计算的基础兼容门面。</summary>
    public static class LongitudinalProfileLayoutCalculator
    {
        public const double ReferenceOutputScale = 0.5;
        public const double ChartGap = 5.0;
        public const double ElevationStaffWidth = 2.0;
        public const double WellHalfWidth = 0.5;
        public const double BoundaryExtensionLength = 0.5;
        public const double BoundaryBreakHalfSize = 0.375;
        public const double BoundaryBreakStraightLength = 3.125;

        public static LongitudinalProfileLayout Calculate(
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings)
        {
            return CDBox.Shared.Wastewater.Drafting
                .WastewaterLongitudinalProfileRegistry.GetRequired()
                .CalculateLayout(profile, settings);
        }
    }
}
