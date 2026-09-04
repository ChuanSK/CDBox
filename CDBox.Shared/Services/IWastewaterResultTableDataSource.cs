using System.Collections.Generic;

namespace CDBox.Shared.Services
{
    /// <summary>
    /// 基础程序集向污水业务提供的井属性读取边界。使用 Handle 和纯数据
    /// 对象，避免污水模块反向引用 CDBox.dll 中的旧工程量实现。
    /// </summary>
    public interface IWastewaterResultTableDataSource
    {
        IList<WastewaterResultTableNodeData> ReadNodes(
            IEnumerable<string> objectHandles);
    }

    public sealed class WastewaterResultTableNodeData
    {
        public string ObjectHandle { get; set; }
        public string NodeNo { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double GroundElevation { get; set; }
        public double WellDepth { get; set; }
        public string WellSpec { get; set; }

        public WastewaterResultTableNodeData()
        {
            ObjectHandle = string.Empty;
            NodeNo = string.Empty;
            WellSpec = string.Empty;
        }
    }
}
