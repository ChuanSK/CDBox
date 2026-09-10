using System;

namespace CDBox.Shared.UI
{
    /// <summary>业务向基础 UI 发出的调用。此契约不包含控件、样式或页面实现。</summary>
    public static class CDBoxUiGateway
    {
        private static Func<string, object, string, object[], object> _dispatch;

        public static void Register(Func<string, object, string, object[], object> dispatch)
        {
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        }

        public static T Call<T>(string id, string operation, params object[] arguments)
        {
            return (T)Invoke(id, null, operation, arguments);
        }

        public static void Call(string id, string operation, params object[] arguments)
        {
            Invoke(id, null, operation, arguments);
        }

        internal static object Invoke(string id, object instance, string operation, object[] arguments)
        {
            var dispatch = _dispatch;
            if (dispatch == null)
                throw new InvalidOperationException("基础 UI 服务尚未初始化。");
            return dispatch(id, instance, operation, arguments ?? new object[0]);
        }
    }

    /// <summary>延迟创建基础组件中的页面会话，模块初始化不加载页面类型。</summary>
    public sealed class CDBoxUiSession : IDisposable
    {
        private readonly string _id;
        private readonly object[] _arguments;
        private object _instance;
        private bool _disposed;

        public CDBoxUiSession(string id, params object[] arguments)
        {
            _id = id;
            _arguments = arguments ?? new object[0];
        }

        private object Instance
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException(nameof(CDBoxUiSession));
                return _instance ?? (_instance = CDBoxUiGateway.Invoke(_id, null, ".ctor", _arguments));
            }
        }

        public CDBoxPageDefinition CreatePage()
        {
            return (CDBoxPageDefinition)CDBoxUiGateway.Invoke(_id, Instance, "CreatePage", null);
        }

        public void SetTarget(string documentId, string objectHandle)
        {
            CDBoxUiGateway.Invoke(_id, Instance, "SetTarget", new object[] { documentId, objectHandle });
        }

        public void RefreshDocument()
        {
            CDBoxUiGateway.Invoke(_id, Instance, "RefreshDocument", null);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var disposable = _instance as IDisposable;
            _instance = null;
            if (disposable != null) disposable.Dispose();
        }
    }
}
