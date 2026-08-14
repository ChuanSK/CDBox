using System;
using System.Threading;
using System.Windows.Interop;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using TCPipeAutoDraw.Core.FloatingCenter;
using TCPipeAutoDraw.UI.Studio;
using TCPipeAutoDraw.UI.FloatingCenter;
using FloatingCenterHub = TCPipeAutoDraw.Core.FloatingCenter.FloatingCenter;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using WinForms = System.Windows.Forms;

namespace TCPipeAutoDraw.UI
{
    internal static class CDBoxNotificationService
    {
        private static readonly object SyncRoot = new object();
        private static Dispatcher _dispatcher;

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
            CDBoxUiResponsiveness.Initialize(current);
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
            if (!FloatingLegacyRoutingPolicy.RequiresSynchronousDecision(
                buttons.ToString()))
            {
                if (TryPostWorkbench(text, ResolveToastKind(icon)))
                {
                    CDBoxUiResponsiveness.YieldToRender(null, true);
                    return WinForms.DialogResult.OK;
                }
                PublishStructured(caption, text, ResolveKind(icon));
                CDBoxUiResponsiveness.YieldToRender(null, true);
                if (!FloatingCenterController.CanPresent)
                    WriteEditorFallback(caption, text);
                return WinForms.DialogResult.OK;
            }

            var window = new CDBoxDecisionWindow(caption, text,
                ResolveKind(icon), buttons, defaultButton, false);
            ApplyAppearance(window);
            SetOwner(window, owner);
            try
            {
                try { AcadApp.ShowModalWindow(window); }
                catch { window.ShowDialog(); }
                PublishDialogResult(caption, text, icon, buttons,
                    window.Result);
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
            var window = new CDBoxDecisionWindow(title, message,
                CDBoxNotificationKind.Question,
                WinForms.MessageBoxButtons.YesNo,
                WinForms.MessageBoxDefaultButton.Button1, true,
                yesText, noText);
            ApplyAppearance(window);
            SetOwner(window, owner);
            try
            {
                try { AcadApp.ShowModalWindow(window); }
                catch { window.ShowDialog(); }
                doNotAskAgain = window.DoNotAskAgain;
                PublishDecisionResult(title, message, window.Result,
                    doNotAskAgain);
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
            if (TryPostWorkbench(message, ResolveToastKind(kind)))
            {
                CDBoxUiResponsiveness.YieldToRender(null, true);
                return;
            }
            PublishStructured(title, message, kind);
            CDBoxUiResponsiveness.YieldToRender(null, true);
            if (FloatingCenterController.CanPresent) return;
            WriteEditorFallback(title, message);
        }

        public static IDisposable BeginCommandPrompt(string title,
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return DelegateScope.Empty;
            IPromptSession structuredPrompt = FloatingCenterHub.Current.BeginPrompt(
                new FloatingPrompt
                {
                    Source = "LegacyCommandPrompt",
                    Title = title,
                    Message = message
                });
            if (FloatingCenterController.CanPresent)
                return new DelegateScope(delegate
                {
                    try { structuredPrompt.Dispose(); }
                    catch { }
                });
            WriteEditorFallback(title, message);
            return new DelegateScope(delegate
            {
                try { structuredPrompt.Dispose(); } catch { }
            });
        }

        private static void ApplyAppearance(CDBoxDecisionWindow window)
        {
            CDBoxStudioSettings settings = CDBoxStudioSettingsStore.Load();
            window.ApplyAppearance(settings.AnnotationHudNormalOpacity,
                settings.AnnotationHudHoverOpacity,
                settings.AnnotationHudGlowEnabled,
                settings.AnnotationHudGlowIntensity);
        }

        private static void SetOwner(CDBoxDecisionWindow window,
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

        private static void WriteEditorFallback(string title, string message)
        {
            try
            {
                Document document = AcadApp.DocumentManager.MdiActiveDocument;
                if (document == null || document.Editor == null) return;
                string caption = string.IsNullOrWhiteSpace(title)
                    ? "CDBox" : title.Trim();
                document.Editor.WriteMessage("\n[" + caption + "] "
                    + (message ?? string.Empty).Trim() + "\n");
            }
            catch { }
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

        private static void PublishStructured(string title, string message,
            CDBoxNotificationKind kind)
        {
            try
            {
                FloatingCenterHub.Current.Publish(new FloatingMessage
                {
                    Source = "LegacyNotification",
                    Kind = ResolveFloatingKind(kind),
                    Title = string.IsNullOrWhiteSpace(title)
                        ? "CDBox" : title.Trim(),
                    Summary = (message ?? string.Empty).Trim(),
                    MergeKey = "legacy:" + kind + ":"
                        + (title ?? string.Empty).Trim(),
                    PresentAsCard = true,
                    RecordInHistory = true
                });
            }
            catch { }
        }

        private static void PublishDialogResult(string title, string message,
            WinForms.MessageBoxIcon icon, WinForms.MessageBoxButtons buttons,
            WinForms.DialogResult result)
        {
            if (buttons == WinForms.MessageBoxButtons.OK)
            {
                PublishStructured(title, message, ResolveKind(icon));
                return;
            }
            PublishDecisionResult(title, message, result, false);
        }

        private static void PublishDecisionResult(string title,
            string message, WinForms.DialogResult result,
            bool doNotAskAgain)
        {
            try
            {
                FloatingCenterHub.Current.Publish(new FloatingMessage
                {
                    Source = "LegacyDecision",
                    Kind = FloatingMessageKind.Decision,
                    Priority = FloatingMessagePriority.High,
                    Title = string.IsNullOrWhiteSpace(title)
                        ? "CDBox" : title.Trim(),
                    Summary = (message ?? string.Empty).Trim(),
                    Detail = "选择结果：" + result
                        + (doNotAskAgain ? "；以后不再提示" : string.Empty),
                    MergeKey = "legacy-decision:"
                        + (title ?? string.Empty).Trim(),
                    RecordInHistory = true
                });
            }
            catch { }
        }

        private static FloatingMessageKind ResolveFloatingKind(
            CDBoxNotificationKind kind)
        {
            if (kind == CDBoxNotificationKind.Success)
                return FloatingMessageKind.Success;
            if (kind == CDBoxNotificationKind.Warning)
                return FloatingMessageKind.Warning;
            if (kind == CDBoxNotificationKind.Error)
                return FloatingMessageKind.Error;
            if (kind == CDBoxNotificationKind.Question)
                return FloatingMessageKind.Decision;
            return FloatingMessageKind.Information;
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
