using CDBox.Shared.Wastewater.Automation;
using TCPipeAutoDraw.Core.Sync;

namespace CDBox.Wastewater.Features.Sync
{
    /// <summary>
    /// 污水属性、计算、标注及显示同步任务状态机的模块入口。
    /// CAD 命令执行仍由基础适配层负责。
    /// </summary>
    public sealed class WastewaterSyncService : IWastewaterSyncService
    {
        public ISyncManager CreateManager()
        {
            return new SyncManager();
        }
    }
}
