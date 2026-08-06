using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Interop;
using System.Windows.Threading;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using WinForms = System.Windows.Forms;

namespace TCPipeAutoDraw.UI
{
    internal static class CDBoxNotificationService
    {
        private const int MaximumQueuedToasts = 8;
        private static readonly object SyncRoot = new object();
        private static readonly Queue<ToastRequest> ToastQueue =
            new Queue<ToastRequest>();
        private static Dispatcher _dispatcher;
        private static CDBoxNotificationWindow _activeToast;
        private static CDBoxNotificationWindow _activePrompt;
        private static bool _replacingPrompt;

        [ThreadStatic]
        private static Action<string, string> _workbenchToast;

        public static void InitializeForCurrentThread()
        {
            if (_dispatcher != null) return;
            Dispatcher current = Dispatcher.FromThread(
                Thread.CurrentThread);
            if (current == null) current = Dispatcher.CurrentDispatcher;
            lock (SyncRoot)
            {
                if (_dispatcher == null) _dispatcher = current;
            }
        }

        public static IDisposable BeginWorkbenchScope(
            Action<string, string> toast)
        {
            Action<string, string> previous = _workbenchToast;
            _workbenchToast = toast;
            return new DelegateScope(delegate
            {
                _workbenchToast = previous;
            });
        }

        public static WinForms.DialogResult ShowDialog(
            WinForms.IWin32Window owner, string text, string caption,
            WinForms.MessageBoxButtons buttons, WinForms.MessageBoxIcon icon,
            WinForms.MessageBoxDefaultButton defaultButton)
        {
            if (buttons == WinForms.MessageBoxButtons.OK
                && TryPostWorkbench(text, ResolveToastKind(icon)))
                return WinForms.DialogResult.OK;

            var window = new CDBoxNotificationWindow(caption, text,
                ResolveKind(icon), buttons, defaultButton, false, false,
                false);
            ApplyAppearance(window);
            SetOwner(window, owner);
            try
            {
                try { AcadApp.ShowModalWindow(window); }
                catch { window.ShowDialog(); }
                return window.Result;
            }
            finally
            {
                if (window.IsVisible) window.CloseImmediately();
            }
        }

        public static WinForms.DialogResult ShowYesNoWithOption(
            WinForms.IWin32Window owner, string title, string message,
            string yesText, string noText, out bool doNotAskAgain)
        {
            var window = new CDBoxNotificationWindow(title, message,
                CDBoxNotificationKind.Question,
                WinForms.MessageBoxButtons.YesNo,
                WinForms.MessageBoxDefaultButton.Button1, false, false,
                true, yesText, noText);
            ApplyAppearance(window);
            SetOwner(window, owner);
            try
            {
                try { AcadApp.ShowModalWindow(window); }
                catch { window.ShowDialog(); }
                doNotAskAgain = window.DoNotAskAgain;
                return window.Result;
            }
            finally
            {
                if (window.IsVisible) window.CloseImmediately();
            }
        }

        public static void Notify(string title, string message,
            CDBoxNotificationKind kind)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (TryPostWorkbench(message, ResolveToastKind(kind))) return;
            Dispatch(delegate
            {
                EnqueueToast(new ToastRequest
                {
                    Title = string.IsNullOrWhiteSpace(title)
                        ? "CDBox" : title.Trim(),
                    Message = message.Trim(),
                    Kind = kind
                });
            });
        }

        public static IDisposable BeginCommandPrompt(string title,
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return DelegateScope.Empty;
            CDBoxNotificationWindow created = null;
            DispatchSynchronously(delegate
            {
                if (_activePrompt != null)
                {
                    _replacingPrompt = true;
                    try
                    {
                        try { _activePrompt.CloseImmediately(); }
                        catch { }
                        _activePrompt = null;
                    }
                    finally
                    {
                        _replacingPrompt = false;
                    }
                }
                created = CreateModelessWindow(title, message,
                    CDBoxNotificationKind.Information, false);
                _activePrompt = created;
                if (_activeToast != null)
                {
                    try { _activeToast.CloseImmediately(); }
                    catch { }
                    _activeToast = null;
                }
                created.Closed += delegate
                {
                    if (ReferenceEquals(_activePrompt, created))
                        _activePrompt = null;
                    if (!_replacingPrompt && _activeToast == null
                        && ToastQueue.Count > 0)
                        ShowToast(ToastQueue.Dequeue());
                };
                created.Show();
            });
            return new DelegateScope(delegate
            {
                Dispatch(delegate
                {
                    if (created == null) return;
                    try
                    {
                        if (created.IsVisible)
                            created.RequestAnimatedClose();
                    }
                    catch
                    {
                    }
                });
            });
        }

        private static void EnqueueToast(ToastRequest request)
        {
            if (request == null) return;
            if (_activeToast != null || _activePrompt != null)
            {
                while (ToastQueue.Count >= MaximumQueuedToasts)
                    ToastQueue.Dequeue();
                ToastQueue.Enqueue(request);
                return;
            }
            ShowToast(request);
        }

        private static void ShowToast(ToastRequest request)
        {
            CDBoxNotificationWindow window = CreateModelessWindow(
                request.Title, request.Message, request.Kind, true);
            _activeToast = window;
            window.Closed += delegate
            {
                if (ReferenceEquals(_activeToast, window))
                    _activeToast = null;
                if (_activePrompt == null && ToastQueue.Count > 0)
                    ShowToast(ToastQueue.Dequeue());
            };
            window.Show();
        }

        private static CDBoxNotificationWindow CreateModelessWindow(
            string title, string message, CDBoxNotificationKind kind,
            bool autoClose)
        {
            var window = new CDBoxNotificationWindow(title, message, kind,
                WinForms.MessageBoxButtons.OK,
                WinForms.MessageBoxDefaultButton.Button1, true, autoClose,
                false);
            ApplyAppearance(window);
            SetOwner(window, null);
            return window;
        }

        private static void ApplyAppearance(CDBoxNotificationWindow window)
        {
            CDBoxStudioSettings settings = CDBoxStudioSettingsStore.Load();
            window.ApplyAppearance(settings.AnnotationHudNormalOpacity,
                settings.AnnotationHudHoverOpacity,
                settings.AnnotationHudGlowEnabled,
                settings.AnnotationHudGlowIntensity);
        }

        private static void SetOwner(CDBoxNotificationWindow window,
            WinForms.IWin32Window owner)
        {
            if (window == null) return;
            try
            {
                IntPtr handle = owner == null ? IntPtr.Zero : owner.Handle;
                if (handle == IntPtr.Zero && AcadApp.MainWindow != null)
                    handle = AcadApp.MainWindow.Handle;
                if (handle != IntPtr.Zero)
                    new WindowInteropHelper(window).Owner = handle;
            }
            catch
            {
            }
        }

        private static bool TryPostWorkbench(string message, string kind)
        {
            Action<string, string> handler = _workbenchToast;
            if (handler == null || string.IsNullOrWhiteSpace(message))
                return false;
            try
            {
                handler(message.Trim(), kind);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void Dispatch(Action action)
        {
            if (action == null) return;
            InitializeForCurrentThread();
            Dispatcher dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }
            dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
        }

        private static void DispatchSynchronously(Action action)
        {
            if (action == null) return;
            InitializeForCurrentThread();
            Dispatcher dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }
            dispatcher.Invoke(DispatcherPriority.Normal, action);
        }

        private static CDBoxNotificationKind ResolveKind(
            WinForms.MessageBoxIcon icon)
        {
            if (icon == WinForms.MessageBoxIcon.Error
                || icon == WinForms.MessageBoxIcon.Hand
                || icon == WinForms.MessageBoxIcon.Stop)
                return CDBoxNotificationKind.Error;
            if (icon == WinForms.MessageBoxIcon.Warning
                || icon == WinForms.MessageBoxIcon.Exclamation)
                return CDBoxNotificationKind.Warning;
            if (icon == WinForms.MessageBoxIcon.Question)
                return CDBoxNotificationKind.Question;
            return CDBoxNotificationKind.Information;
        }

        private static string ResolveToastKind(WinForms.MessageBoxIcon icon)
        {
            return ResolveToastKind(ResolveKind(icon));
        }

        private static string ResolveToastKind(CDBoxNotificationKind kind)
        {
            if (kind == CDBoxNotificationKind.Error) return "error";
            if (kind == CDBoxNotificationKind.Warning) return "warning";
            if (kind == CDBoxNotificationKind.Success) return "success";
            return "info";
        }

        private sealed class ToastRequest
        {
            public string Title;
            public string Message;
            public CDBoxNotificationKind Kind;
        }

        private sealed class DelegateScope : IDisposable
        {
            public static readonly DelegateScope Empty =
                new DelegateScope(null);
            private Action _dispose;

            public DelegateScope(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                Action action = Interlocked.Exchange(ref _dispose, null);
                if (action != null) action();
            }
        }
    }
}
