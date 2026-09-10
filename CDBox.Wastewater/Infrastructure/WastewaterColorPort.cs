using CDBox.Shared.UI;
using TCPipeAutoDraw.Core.Colors;
using AcadColor = Autodesk.AutoCAD.Colors.Color;
namespace CDBox.Wastewater.Infrastructure
{
    /// <summary>使用基础组件的统一颜色转换和输出策略。</summary>
    internal static class WastewaterColorPort
    {
        public static CDBoxColor FromCadColor(AcadColor color) { return CDBoxUiGateway.Call<CDBoxColor>("base.colors", "FromCadColor", color); }
        public static AcadColor ToCadColor(CDBoxColor color) { return CDBoxUiGateway.Call<AcadColor>("base.colors", "ToCadColor", color); }
        public static CDBoxColor PrepareForWrite(CDBoxColor selected, CDBoxColor original) { return CDBoxUiGateway.Call<CDBoxColor>("base.colors", "PrepareForWrite", selected, original); }
    }
}
