using System;
using System.Collections.Generic;
using System.Linq;

namespace CDBox.RealEstate.Models
{
    public static class ParcelBoundaryRecognitionApplicator
    {
        public static void Apply(ParcelSurveyRecord record,
            ParcelBoundaryCadSelection selection)
        {
            if (record == null) throw new ArgumentNullException("record");
            if (selection == null) throw new ArgumentNullException("selection");
            record.Normalize();
            IList<ParcelBoundaryCadVertex> vertices = selection.Vertices
                ?? new List<ParcelBoundaryCadVertex>();
            int count = vertices.Count;
            int start = vertices.ToList().FindIndex(x => x != null
                && x.SourceIndex == selection.SelectedStartSourceIndex);
            if (count < 3 || start < 0)
                throw new InvalidOperationException(
                    "权属线节点数据无效，请重新选择宗地。");

            bool clockwise = selection.ConfiguredClockwise;
            int step = clockwise == selection.NativeClockwise ? 1 : -1;
            string prefix = string.IsNullOrWhiteSpace(
                selection.StartPointPrefix) ? "J"
                : selection.StartPointPrefix.Trim();
            int startNumber = selection.StartPointNumber > 0
                ? selection.StartPointNumber : 1;
            var points = new List<ParcelBoundaryPointRecord>();
            for (int i = 0; i < count; i++)
            {
                int raw = Mod(start + step * i, count);
                ParcelBoundaryCadVertex vertex = vertices[raw];
                int next = Mod(raw + step, count);
                decimal edge = step == 1
                    ? vertex.DistanceToNext
                    : vertices[next].DistanceToNext;
                points.Add(new ParcelBoundaryPointRecord
                {
                    Sequence = i + 1,
                    PointNumber = prefix + (startNumber + i),
                    X = vertex.X,
                    Y = vertex.Y,
                    DistanceToNext = edge,
                    MarkerType = "喷涂",
                    Description = string.Empty,
                    Confirmed = true,
                    Status = ParcelFieldStatus.Automatic
                });
            }

            record.Boundary.Points = points;
            record.Boundary.Segments.Clear();
            record.Boundary.SignatureGroups.Clear();
            record.Boundary.ParcelBoundaryClosed = true;
            record.Boundary.SourceObjectHandle =
                selection.SourceObjectHandle ?? string.Empty;
            record.Boundary.SourceLayerName =
                selection.SourceLayerName ?? string.Empty;
            record.Boundary.SourceClockwise = clockwise;
            record.Boundary.SourceArea = selection.Area;

            ParcelSurveyFieldValue area = record.Field(
                ParcelSurveyFieldKeys.ParcelArea);
            if (area != null)
            {
                area.NumericValue = selection.Area;
                area.TextValue = string.Empty;
                area.Status = ParcelFieldStatus.Automatic;
                area.Confirmed = false;
            }
            ParcelSurveyFieldValue owner = record.Field("rights.ownerName");
            if (owner != null)
            {
                owner.TextValue = (selection.OwnerName ?? string.Empty).Trim();
                owner.NumericValue = null;
                owner.Status = ParcelFieldStatus.Manual;
                owner.Confirmed = true;
            }
            record.Normalize();
        }

        private static int Mod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }
}
