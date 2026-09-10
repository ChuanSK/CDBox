using CDBox.RealEstate.Models;
using CDBox.Shared.UI;

namespace CDBox.RealEstate.Services
{
    /// <summary>界址业务的输入端口；所有浮窗由基础 UI 服务实现。</summary>
    internal static class ParcelBoundaryInput
    {
        private const string Id = "realestate.boundary-dialogs";
        public static bool TryGetStartingPoint(out string prefix, out int number)
        {
            object[] args = { null, 1 };
            bool accepted = CDBoxUiGateway.Call<bool>(Id, "TryGetStartingPoint", args);
            prefix = (string)args[0]; number = (int)args[1]; return accepted;
        }
        public static bool TryGetOwnerName(string current, out string ownerName)
        {
            object[] args = { current, null };
            bool accepted = CDBoxUiGateway.Call<bool>(Id, "TryGetOwnerName", args);
            ownerName = (string)args[1]; return accepted;
        }
        public static bool TryGetNumberingDirection(bool initialClockwise, out bool clockwise)
        {
            object[] args = { initialClockwise, false };
            bool accepted = CDBoxUiGateway.Call<bool>(Id, "TryGetNumberingDirection", args);
            clockwise = (bool)args[1]; return accepted;
        }
        public static ParcelBoundarySegmentRecord EditSegment(ParcelBoundaryRangeSelection range, ParcelBoundarySegmentRecord existing)
        {
            return CDBoxUiGateway.Call<ParcelBoundarySegmentRecord>(Id, "EditSegment", range, existing);
        }
        public static ParcelBoundarySignatureGroupRecord EditSignature(ParcelBoundaryRangeSelection range,
            ParcelBoundarySignatureGroupRecord existing, ParcelBoundarySegmentRecord segment, string representative)
        {
            return CDBoxUiGateway.Call<ParcelBoundarySignatureGroupRecord>(Id, "EditSignature", range, existing, segment, representative);
        }
    }
}
