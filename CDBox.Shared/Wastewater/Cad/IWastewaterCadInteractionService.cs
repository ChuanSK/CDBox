using System;
using System.Collections.Generic;

namespace CDBox.Shared.Wastewater.Cad
{
    /// <summary>
    /// 污水模块拥有的 CAD 交互、绑定成果刷新与属性双击入口。
    /// 基础组件只通过 object 传递宿主对象，避免反向引用污水程序集
    /// 或 AutoCAD 版本相关类型。
    /// </summary>
    public interface IWastewaterCadInteractionService
    {
        void Initialize();
        void Terminate();
        void RefreshAppearance();
        void RefreshNodeAnnotations(object document,
            IEnumerable<string> sourceHandles);
        void RefreshPipeAnnotations(object document,
            IEnumerable<string> sourceHandles);
        bool CanOpenAttribute(object document,
            IEnumerable<object> selectedObjectIds);
        bool TryOpenAttribute(object document,
            IEnumerable<object> selectedObjectIds);
        bool TryOpenAttributeWithOverlap(object document,
            IEnumerable<object> selectedObjectIds,
            double screenX, double screenY);
    }

    public static class WastewaterCadInteractionRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IWastewaterCadInteractionService _current;

        public static IWastewaterCadInteractionService Current
        {
            get { lock (SyncRoot) return _current; }
        }

        public static bool IsAvailable
        {
            get { lock (SyncRoot) return _current != null; }
        }

        public static void Register(IWastewaterCadInteractionService service)
        {
            if (service == null) throw new ArgumentNullException("service");
            lock (SyncRoot) _current = service;
        }

        public static void Unregister(IWastewaterCadInteractionService service)
        {
            lock (SyncRoot)
                if (ReferenceEquals(_current, service)) _current = null;
        }
    }
}
