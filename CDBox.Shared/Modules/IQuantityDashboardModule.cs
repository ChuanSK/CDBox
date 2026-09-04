using System;
using System.Collections.Generic;

namespace CDBox.Shared.Modules
{
    /// <summary>
    /// 污水工程量看板与正式工程量表的模块入口。
    /// </summary>
    public interface IQuantityDashboardModule
    {
        void OpenQuantityDashboard();
        void ExportFormalQuantityReport();
    }

    /// <summary>
    /// 必装基础组件提供的工程量变更监听宿主。
    /// </summary>
    public interface IQuantityDashboardMonitorService
    {
        void Configure(Action<string> scriptSink);
        void SetLiveEnabled(bool enabled);
        void MarkDirty(string reason);
    }

    /// <summary>
    /// 污水模块提供给基础检查与同步流程的区域范围兼容边界。
    /// CAD 类型通过 object 传递，避免 Shared 引用 AutoCAD 程序集。
    /// </summary>
    public interface IQuantityDashboardScopeService
    {
        IReadOnlyList<string> FilterHandlesToSavedScope(
            object document, IEnumerable<string> handles);
        object GetSavedRegionObjectId(object database, object transaction);
        bool IsEntityIncluded(object entity, object region);
    }

    public static class QuantityDashboardScopeRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IQuantityDashboardScopeService _current;

        public static void Register(IQuantityDashboardScopeService service)
        {
            if (service == null) throw new ArgumentNullException("service");
            lock (SyncRoot) _current = service;
        }

        public static void Unregister(IQuantityDashboardScopeService service)
        {
            lock (SyncRoot)
                if (ReferenceEquals(_current, service)) _current = null;
        }

        public static IQuantityDashboardScopeService GetOrDefault()
        {
            lock (SyncRoot) return _current;
        }
    }
}
