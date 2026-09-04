using System;
using System.Collections.Generic;
using System.Linq;

namespace CDBox.RealEstate.Models
{
    public static class ParcelBoundaryNeighborSynchronizer
    {
        public static void Apply(IList<ParcelBoundaryPointRecord> points,
            IList<ParcelBoundarySegmentRecord> segments,
            IList<ParcelBoundarySignatureGroupRecord> groups)
        {
            if (segments == null || groups == null || groups.Count == 0)
                return;

            foreach (ParcelBoundarySegmentRecord segment in segments)
            {
                if (segment == null) continue;
                ParcelBoundarySignatureGroupRecord group = FindGroup(points,
                    segment, groups);
                segment.NeighborOwner = group == null
                    ? string.Empty : group.NeighborOwner ?? string.Empty;
                segment.NeighborParcelCode = group == null
                    ? string.Empty : group.NeighborParcelCode ?? string.Empty;
            }
        }

        public static ParcelBoundarySignatureGroupRecord FindGroup(
            IList<ParcelBoundaryPointRecord> points,
            ParcelBoundarySegmentRecord segment,
            IEnumerable<ParcelBoundarySignatureGroupRecord> groups)
        {
            if (points == null || segment == null || groups == null)
                return null;
            HashSet<string> segmentEdges = Edges(points,
                segment.StartPointNumber, segment.EndPointNumber);
            if (segmentEdges.Count == 0) return null;

            return groups.Where(x => x != null)
                .Select(x => new
                {
                    Group = x,
                    Edges = Edges(points, x.StartPointNumber,
                        x.EndPointNumber)
                })
                .Where(x => x.Edges.Count > 0
                    && segmentEdges.All(x.Edges.Contains))
                .OrderBy(x => x.Edges.Count)
                .Select(x => x.Group)
                .FirstOrDefault();
        }

        private static HashSet<string> Edges(
            IList<ParcelBoundaryPointRecord> points, string startNumber,
            string endNumber)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (points == null || points.Count < 2) return result;
            int start = IndexOf(points, startNumber);
            int end = IndexOf(points, endNumber);
            if (start < 0 || end < 0 || start == end) return result;

            int current = start;
            int guard = 0;
            while (current != end && guard++ < points.Count)
            {
                int next = (current + 1) % points.Count;
                result.Add(EdgeKey(points[current].PointNumber,
                    points[next].PointNumber));
                current = next;
            }
            if (current != end) result.Clear();
            return result;
        }

        private static int IndexOf(IList<ParcelBoundaryPointRecord> points,
            string pointNumber)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (string.Equals((points[i]?.PointNumber ?? string.Empty)
                        .Trim(), (pointNumber ?? string.Empty).Trim(),
                    StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        private static string EdgeKey(string start, string end)
        {
            return (start ?? string.Empty).Trim() + "\u001f"
                + (end ?? string.Empty).Trim();
        }
    }
}
