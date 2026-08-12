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

    internal static class LongitudinalProfileLayoutCalculator
    {
        // 管立得纵断面样式中的几何尺寸与行高按 0.5 落入模型空间；
        // 用户设置的文字高度则是模型空间真实高度，不再重复缩放。
        // 横向 1:1000、纵向 1:100 分别对应 0.5 和 5.0 的坐标换算。
        internal const double ReferenceOutputScale = 0.5;
        internal const double ChartGap = 5.0;
        internal const double ElevationStaffWidth = 2.0;
        internal const double WellHalfWidth = 0.5;
        internal const double BoundaryExtensionLength = 0.5;
        internal const double BoundaryBreakHalfSize = 0.375;
        internal const double BoundaryBreakStraightLength = 3.125;

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
                HeaderRight =
                    settings.HeaderWidth * ReferenceOutputScale,
                TableBottom = 0.0,
                HorizontalFactor = 1000.0 / settings.HorizontalScale
                    * ReferenceOutputScale,
                VerticalFactor = 1000.0 / settings.VerticalScale
                    * ReferenceOutputScale,
                OutputScale = ReferenceOutputScale
            };
            // 参考样式在表头栏与数据栏之间保留半个栏间距：
            // 表头右边界 22.5，数据表及坐标网格从 25.0 开始。
            layout.DataLeft = layout.HeaderRight
                + settings.HeaderChartGap * ReferenceOutputScale;

            double totalRows = settings.Rows.Sum(x => x.Height)
                * ReferenceOutputScale;
            layout.TableTop = totalRows;
            double top = totalRows;
            foreach (LongitudinalProfileRowSettings row in settings.Rows)
            {
                layout.Rows.Add(new LongitudinalProfileRowLayout
                {
                    Settings = row,
                    Bottom = top - row.Height * ReferenceOutputScale,
                    Top = top
                });
                top -= row.Height * ReferenceOutputScale;
            }

            layout.ChartBottom = layout.TableTop + ChartGap;
            double min = profile.Nodes.Min(x =>
                Math.Min(x.GroundElevation - x.WellDepth,
                    Math.Min(x.DesignInvertElevation,
                        x.GroundElevation)));
            double max = profile.Nodes.Max(x =>
                Math.Max(x.DesignInvertElevation, x.GroundElevation));
            double interval = settings.ElevationGridInterval;
            // 管立得自动范围在最低井底以下留两份间距，
            // 在最高自然地面以上留一份间距。
            layout.DatumElevation = Math.Floor(
                (min - settings.ElevationPadding * 2.0) / interval)
                * interval;
            layout.TopElevation = Math.Ceiling(
                (max + settings.ElevationPadding) / interval) * interval;
            if (layout.TopElevation <= layout.DatumElevation)
                layout.TopElevation =
                    layout.DatumElevation + Math.Max(interval, 1.0);
            layout.ChartTop = layout.ChartBottom
                + (layout.TopElevation - layout.DatumElevation)
                * layout.VerticalFactor;

            layout.StaffRight = layout.HeaderRight;
            layout.StaffLeft = layout.StaffRight - ElevationStaffWidth;
            layout.PlotLeft = layout.DataLeft;
            double distance = profile.Nodes.Max(x => x.CumulativeDistance);
            layout.DataRight = layout.X(distance);
            double horizontalInterval = settings.HorizontalGridInterval;
            double roundedDistance = Math.Ceiling(
                Math.Max(distance, horizontalInterval) / horizontalInterval)
                * horizontalInterval;
            double plotWidth = roundedDistance * layout.HorizontalFactor;
            layout.PlotRight = layout.PlotLeft + plotWidth;
            double requiredRight = layout.DataRight + WellHalfWidth;
            if (profile.EndExtension != null)
            {
                requiredRight += BoundaryExtensionLength
                    + BoundaryBreakHalfSize;
            }
            double gridWidth = horizontalInterval
                * layout.HorizontalFactor;
            while (layout.PlotRight < requiredRight - 1e-8)
                layout.PlotRight += gridWidth;
            return layout;
        }
    }
}
