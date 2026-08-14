using System;
using System.Collections.Generic;

namespace CDBox.Shared.Services
{
    public interface ICDBoxServices
    {
        T GetRequired<T>() where T : class;
        bool TryGet<T>(out T service) where T : class;
    }

    /// <summary>
    /// 仅用于模块边界的轻量服务注册表，不承担对象生命周期管理。
    /// </summary>
    public sealed class CDBoxServiceRegistry : ICDBoxServices
    {
        private readonly object _syncRoot = new object();
        private readonly Dictionary<Type, object> _services =
            new Dictionary<Type, object>();

        public CDBoxServiceRegistry Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException("service");
            lock (_syncRoot)
            {
                _services[typeof(T)] = service;
            }
            return this;
        }

        public T GetRequired<T>() where T : class
        {
            T service;
            if (TryGet(out service)) return service;
            throw new InvalidOperationException(
                "CDBox 服务尚未注册：" + typeof(T).FullName);
        }

        public bool TryGet<T>(out T service) where T : class
        {
            lock (_syncRoot)
            {
                object value;
                if (_services.TryGetValue(typeof(T), out value))
                {
                    service = value as T;
                    return service != null;
                }
            }

            service = null;
            return false;
        }
    }
}
