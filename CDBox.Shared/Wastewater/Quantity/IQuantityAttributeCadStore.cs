using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 污水模块拥有的 CAD Xrecord 属性存储边界。CAD 对象采用不透明引用，
    /// 使 Shared 继续保持对 AutoCAD 程序集零依赖。
    /// </summary>
    public interface IQuantityAttributeCadStore
    {
        bool HasAttributes(object entity, object transaction);
        bool HasAttributeKey(object entity, object transaction,
            string keyName);
        IReadOnlyCollection<string> ReadAttributeKeys(object entity,
            object transaction);
        QuantityPipeAttributes ReadAttributes(object entity,
            object transaction);
        void WriteAttributes(object entity, object transaction,
            QuantityPipeAttributes attributes);
        bool RemoveAttributes(object entity, object transaction);
        bool TryReadSavedAttributes(object database, object transaction,
            object objectId, out QuantityPipeAttributes attributes);
        IReadOnlyList<string> RepairClonedAttributeObjects(object database,
            object transaction, object cloneMap);
    }

    public static class QuantityAttributeCadStoreRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IQuantityAttributeCadStore _current;

        public static bool IsAvailable
        {
            get { lock (SyncRoot) return _current != null; }
        }

        public static void Register(IQuantityAttributeCadStore store)
        {
            if (store == null) throw new ArgumentNullException("store");
            lock (SyncRoot) _current = store;
        }

        public static void Unregister(IQuantityAttributeCadStore store)
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(_current, store)) _current = null;
            }
        }

        public static IQuantityAttributeCadStore GetRequired()
        {
            lock (SyncRoot)
            {
                if (_current == null)
                    throw new InvalidOperationException(
                        "污水业务属性存储组件尚未加载。");
                return _current;
            }
        }

        public static IQuantityAttributeCadStore GetOrDefault()
        {
            lock (SyncRoot) return _current;
        }
    }
}
