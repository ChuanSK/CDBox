using System;
using System.Collections.Generic;
using System.Linq;

namespace TCPipeAutoDraw.Core.FloatingCenter
{
    public enum FloatingSnapEdge
    {
        None,
        Left,
        Top,
        Right,
        Bottom
    }

    public sealed class FloatingWorkArea
    {
        public string DeviceName { get; set; }
        public bool IsPrimary { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Right { get { return Left + Math.Max(0.0, Width); } }
        public double Bottom { get { return Top + Math.Max(0.0, Height); } }
    }

    public sealed class FloatingPositionState
    {
        public string MonitorDeviceName { get; set; }
        public FloatingSnapEdge SnapEdge { get; set; }
        public double EdgeOffset { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
    }

    public sealed class FloatingResolvedPosition
    {
        public FloatingWorkArea WorkArea { get; set; }
        public FloatingPositionState State { get; set; }
    }

    public static class FloatingCenterPlacement
    {
        public const double DefaultSnapDistance = 16.0;

        public static FloatingResolvedPosition Restore(FloatingPositionState saved,
            IEnumerable<FloatingWorkArea> availableAreas, double windowWidth,
            double windowHeight, double fallbackMargin = 24.0)
        {
            List<FloatingWorkArea> areas = (availableAreas ??
                Enumerable.Empty<FloatingWorkArea>()).Where(IsUsable).ToList();
            if (areas.Count == 0)
                areas.Add(new FloatingWorkArea
                {
                    DeviceName = "default",
                    IsPrimary = true,
                    Left = 0,
                    Top = 0,
                    Width = Math.Max(windowWidth + fallbackMargin * 2.0, 800),
                    Height = Math.Max(windowHeight + fallbackMargin * 2.0, 600)
                });

            FloatingWorkArea area = null;
            if (saved != null && !string.IsNullOrWhiteSpace(saved.MonitorDeviceName))
                area = areas.FirstOrDefault(x => string.Equals(x.DeviceName,
                    saved.MonitorDeviceName, StringComparison.OrdinalIgnoreCase));
            area = area ?? areas.FirstOrDefault(x => x.IsPrimary) ?? areas[0];

            double left = saved == null ? area.Right - windowWidth - fallbackMargin : saved.Left;
            double top = saved == null ? area.Top + fallbackMargin : saved.Top;
            if (saved != null)
            {
                switch (saved.SnapEdge)
                {
                    case FloatingSnapEdge.Left:
                        left = area.Left;
                        top = area.Top + saved.EdgeOffset;
                        break;
                    case FloatingSnapEdge.Right:
                        left = area.Right - windowWidth;
                        top = area.Top + saved.EdgeOffset;
                        break;
                    case FloatingSnapEdge.Top:
                        left = area.Left + saved.EdgeOffset;
                        top = area.Top;
                        break;
                    case FloatingSnapEdge.Bottom:
                        left = area.Left + saved.EdgeOffset;
                        top = area.Bottom - windowHeight;
                        break;
                }
            }
            FloatingResolvedPosition resolved = ConstrainAndSnap(left, top,
                area, windowWidth, windowHeight, false, DefaultSnapDistance);
            if (saved != null && saved.SnapEdge != FloatingSnapEdge.None)
            {
                resolved.State.SnapEdge = saved.SnapEdge;
                resolved.State.EdgeOffset = saved.SnapEdge == FloatingSnapEdge.Left
                    || saved.SnapEdge == FloatingSnapEdge.Right
                    ? resolved.State.Top - area.Top
                    : resolved.State.Left - area.Left;
            }
            return resolved;
        }

        public static FloatingResolvedPosition ConstrainAndSnap(double left,
            double top, FloatingWorkArea area, double windowWidth,
            double windowHeight, bool snapEnabled,
            double snapDistance = DefaultSnapDistance)
        {
            if (!IsUsable(area)) throw new ArgumentException("工作区无效。", "area");
            double width = Math.Max(1.0, windowWidth);
            double height = Math.Max(1.0, windowHeight);
            double maximumLeft = Math.Max(area.Left, area.Right - width);
            double maximumTop = Math.Max(area.Top, area.Bottom - height);
            left = ClampFinite(left, area.Left, maximumLeft, area.Left);
            top = ClampFinite(top, area.Top, maximumTop, area.Top);

            FloatingSnapEdge edge = FloatingSnapEdge.None;
            if (snapEnabled)
            {
                double threshold = Math.Max(0.0, snapDistance);
                var candidates = new[]
                {
                    new { Edge = FloatingSnapEdge.Left, Distance = Math.Abs(left - area.Left) },
                    new { Edge = FloatingSnapEdge.Right, Distance = Math.Abs(area.Right - (left + width)) },
                    new { Edge = FloatingSnapEdge.Top, Distance = Math.Abs(top - area.Top) },
                    new { Edge = FloatingSnapEdge.Bottom, Distance = Math.Abs(area.Bottom - (top + height)) }
                };
                var nearest = candidates.OrderBy(x => x.Distance).First();
                if (nearest.Distance <= threshold) edge = nearest.Edge;
                if (edge == FloatingSnapEdge.Left) left = area.Left;
                else if (edge == FloatingSnapEdge.Right) left = maximumLeft;
                else if (edge == FloatingSnapEdge.Top) top = area.Top;
                else if (edge == FloatingSnapEdge.Bottom) top = maximumTop;
            }

            double offset = edge == FloatingSnapEdge.Left || edge == FloatingSnapEdge.Right
                ? top - area.Top
                : edge == FloatingSnapEdge.Top || edge == FloatingSnapEdge.Bottom
                    ? left - area.Left : 0.0;
            return new FloatingResolvedPosition
            {
                WorkArea = area,
                State = new FloatingPositionState
                {
                    MonitorDeviceName = area.DeviceName ?? string.Empty,
                    SnapEdge = edge,
                    EdgeOffset = offset,
                    Left = left,
                    Top = top
                }
            };
        }

        private static bool IsUsable(FloatingWorkArea area)
        {
            return area != null && area.Width > 0.0 && area.Height > 0.0;
        }

        private static double ClampFinite(double value, double minimum,
            double maximum, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) value = fallback;
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
