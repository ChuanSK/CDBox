using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using TCPipeAutoDraw.Core.Check;
using TCPipeAutoDraw.Core.Sync;
using TCPipeAutoDraw.Core.FloatingCenter;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace TCPipeAutoDraw.UI.FloatingCenter
{
    internal static class FloatingCenterController
    {
        private static readonly object Gate = new object();
        private static IFloatingCenter _center;
        private static FloatingCenterWindow _window;
        private static FloatingMessageCardWindow _messageCard;
        private static readonly Dictionary<string, DateTime> PresentedCards =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static Dispatcher _dispatcher;
        private static DispatcherTimer _hostTimer;
        private static bool _initialized;
        private static bool _temporaryHidden;
        private static bool _lastCadMinimized;
        private static string _activeDocumentId = string.Empty;

        public static bool CanPresent
        {
            get
            {
                lock (Gate)
                    return _initialized && _center != null &&
                        CDBoxStudioSettingsStore.Load().FloatingCenterEnabled;
            }
        }

        public static void Initialize(IFloatingCenter center)
        {
            if (center == null) throw new ArgumentNullException("center");
            lock (Gate)
            {
                if (_initialized) return;
                _center = center;
                _dispatcher = Dispatcher.CurrentDispatcher;
                CDBoxUiResponsiveness.Initialize(_dispatcher);
                CDBoxStudioSettings initialSettings =
                    CDBoxStudioSettingsStore.Load();
                _temporaryHidden = !initialSettings.FloatingCenterShowOnStartup;
                _center.Changed += CenterChanged;
                try
                {
                    AcadApp.DocumentManager.DocumentActivated += DocumentActivated;
                    AcadApp.DocumentManager.DocumentToBeDestroyed += DocumentToBeDestroyed;
                }
                catch { }
                _hostTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(450),
                    DispatcherPriority.Background, HostTimerTick, _dispatcher);
                _hostTimer.Start();
                _initialized = true;
            }
            BeginOnUi(delegate { Refresh(true); });
        }

        public static void Terminate()
        {
            FloatingCenterWindow window = null;
            FloatingMessageCardWindow card = null;
            lock (Gate)
            {
                if (!_initialized) return;
                if (_center != null) _center.Changed -= CenterChanged;
                try
                {
                    AcadApp.DocumentManager.DocumentActivated -= DocumentActivated;
                    AcadApp.DocumentManager.DocumentToBeDestroyed -= DocumentToBeDestroyed;
                }
                catch { }
                if (_hostTimer != null) _hostTimer.Stop();
                _hostTimer = null;
                window = _window;
                _window = null;
                card = _messageCard;
                _messageCard = null;
                PresentedCards.Clear();
                _center = null;
                _dispatcher = null;
                _initialized = false;
                _temporaryHidden = false;
                _activeDocumentId = string.Empty;
                foreach (IProgressHandle handle in DemoProgressHandles.Values)
                {
                    try { handle.Dispose(); }
                    catch { }
                }
                DemoProgressHandles.Clear();
            }
            try { if (window != null) window.CloseImmediately(); }
            catch { }
            try { if (card != null) card.CloseImmediately(); }
            catch { }
        }

        public static void ToggleVisibility()
        {
            BeginOnUi(delegate
            {
                CDBoxStudioSettings settings = CDBoxStudioSettingsStore.Load();
                if (!settings.FloatingCenterEnabled)
                {
                    settings.FloatingCenterEnabled = true;
                    settings.FloatingCenterShowOnStartup = true;
                    CDBoxStudioSettingsStore.Save(settings);
                }
                if (_window != null && _window.IsVisible && !_temporaryHidden)
                {
                    _temporaryHidden = true;
                    _window.HideShell();
                    HideMessageCard(false);
                }
                else
                {
                    _temporaryHidden = false;
                    Refresh(true);
                }
            });
        }

        public static void ResetPosition()
        {
            BeginOnUi(delegate
            {
                EnsureWindow();
                if (_window != null) _window.ResetPosition();
                Refresh(false);
            });
        }

        public static void ApplySettings()
        {
            BeginOnUi(delegate
            {
                CDBoxStudioSettings settings = CDBoxStudioSettingsStore.Load();
                if (_window != null) _window.ApplySettings(settings);
                if (!settings.FloatingCenterEnabled)
                {
                    if (_window != null) _window.HideShell();
                    HideMessageCard(false);
                }
                else
                {
                    _temporaryHidden = false;
                    Refresh(false);
                }
            });
        }

        public static void ShowDemo(FloatingHealthState health,
            FloatingActivityState activity, int taskGroups,
            double? progress)
        {
            BeginOnUi(delegate
            {
                string documentId = CadFloatingDocumentIdentity.GetCurrentDocumentId();
                IFloatingCenter center = _center;
                if (center == null) return;
                if (health == FloatingHealthState.Warning ||
                    health == FloatingHealthState.Critical)
                {
                    center.Publish(new FloatingMessage
                    {
                        DocumentId = documentId,
                        Kind = health == FloatingHealthState.Critical
                            ? FloatingMessageKind.Error : FloatingMessageKind.Warning,
                        Title = "悬浮球模拟状态",
                        Summary = health == FloatingHealthState.Critical
                            ? "模拟严重问题，用于验证状态颜色与角标。"
                            : "模拟警告，用于验证状态颜色与角标。",
                        IsPersistent = true,
                        MergeKey = "floating-center-demo-health"
                    });
                }
                if (activity == FloatingActivityState.Working)
                {
                    IProgressHandle previous;
                    if (DemoProgressHandles.TryGetValue(documentId, out previous))
                    {
                        try { previous.Dispose(); }
                        catch { }
                    }
                    IProgressHandle handle = center.BeginProgress(
                        new FloatingProgressSpec
                        {
                            DocumentId = documentId,
                            Source = "FloatingCenterDemo",
                            Title = "悬浮球进度演示",
                            Message = progress.HasValue ? "正在验证进度环" : "正在等待处理",
                            IsIndeterminate = !progress.HasValue,
                            MergeKey = "floating-center-demo-progress"
                        });
                    handle.Report(progress, "正在验证进度环");
                    DemoProgressHandles[documentId] = handle;
                }
                while (taskGroups > 0)
                {
                    int index = taskGroups--;
                    center.Publish(new FloatingMessage
                    {
                        DocumentId = documentId,
                        Kind = FloatingMessageKind.Check,
                        Title = "模拟任务 " + index,
                        Summary = "Phase 1 不执行真实检查或同步。",
                        IsPersistent = true,
                        MergeKey = "floating-center-demo-task-" + index
                    });
                }
                _temporaryHidden = false;
                Refresh(false);
            });
        }

        public static void ClearDemo()
        {
            BeginOnUi(delegate
            {
                string id = CadFloatingDocumentIdentity.GetCurrentDocumentId();
                IProgressHandle handle;
                if (DemoProgressHandles.TryGetValue(id, out handle))
                {
                    try { handle.Cancel("演示已结束。"); }
                    catch { }
                    DemoProgressHandles.Remove(id);
                }
                IList<FloatingMessage> active = _center == null
                    ? new List<FloatingMessage>() : _center.GetActiveMessages(id);
                foreach (FloatingMessage message in active)
                {
                    if (message != null && (string.Equals(message.Source,
                        "FloatingCenterDemo", StringComparison.OrdinalIgnoreCase) ||
                        (message.MergeKey ?? string.Empty).StartsWith(
                            "floating-center-demo-", StringComparison.OrdinalIgnoreCase)))
                        _center.Dismiss(id, message.Id);
                }
                Refresh(false);
            });
        }

        private static readonly Dictionary<string, IProgressHandle>
            DemoProgressHandles = new Dictionary<string, IProgressHandle>(
                StringComparer.OrdinalIgnoreCase);

        private static void EnsureWindow()
        {
            if (_window != null) return;
            try
            {
                _window = new FloatingCenterWindow();
                IntPtr owner = IntPtr.Zero;
                try
                {
                    if (AcadApp.MainWindow != null)
                        owner = AcadApp.MainWindow.Handle;
                }
                catch { }
                if (owner != IntPtr.Zero)
                    new WindowInteropHelper(_window).Owner = owner;
                _window.HideRequested += delegate
                {
                    _temporaryHidden = true;
                    _window.HideShell();
                    HideMessageCard(false);
                };
                _window.SettingsRequested += delegate
                {
                    CDBoxStudioSettingsWindow.ShowWindow(new AcadMainWindow());
                };
                _window.ExpandedChanged += delegate
                {
                    if (_window.IsExpanded) HideMessageCard(false);
                    else
                    {
                        RemovePresentedActiveMessages(_activeDocumentId);
                        QueuePresentationForActiveDocument(_activeDocumentId);
                    }
                };
                _window.AnchorChanged += delegate
                {
                    if (_messageCard != null && _messageCard.IsVisible)
                        _messageCard.Reposition(_window.GetCollapsedAnchor());
                };
                _window.ActionRequested += delegate(object sender,
                    FloatingCenterActionEventArgs e)
                {
                    HandleAction(e == null ? string.Empty : e.ActionId);
                };
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("悬浮球窗口创建失败。", ex);
                _window = null;
            }
        }

        private static void HandleAction(string actionId)
        {
            Document document = null;
            try { document = AcadApp.DocumentManager.MdiActiveDocument; }
            catch { }
            if (document == null || string.IsNullOrWhiteSpace(actionId)) return;
            if (string.Equals(actionId, "drawing-check.refresh",
                StringComparison.OrdinalIgnoreCase))
            {
                if (CadDrawingCheckCoordinator.IsRunning(document))
                    CadDrawingCheckCoordinator.RequestCancel(document);
                else CadDrawingCheckCoordinator.Start(document, true);
                return;
            }
            const string locatePrefix = "drawing-check.locate|";
            if (actionId.StartsWith(locatePrefix,
                StringComparison.OrdinalIgnoreCase))
            {
                CadDrawingCheckCoordinator.Locate(document,
                    actionId.Substring(locatePrefix.Length));
                return;
            }
            const string ignorePrefix = "drawing-check.ignore|";
            if (actionId.StartsWith(ignorePrefix,
                StringComparison.OrdinalIgnoreCase))
            {
                CadDrawingCheckCoordinator.Ignore(document,
                    actionId.Substring(ignorePrefix.Length));
                return;
            }
            const string restorePrefix = "drawing-check.restore|";
            if (actionId.StartsWith(restorePrefix,
                StringComparison.OrdinalIgnoreCase))
            {
                CadDrawingCheckCoordinator.Restore(document,
                    actionId.Substring(restorePrefix.Length));
                return;
            }
            const string syncViewPrefix = "sync.view|";
            if (actionId.StartsWith(syncViewPrefix,
                StringComparison.OrdinalIgnoreCase))
            {
                CadSyncCoordinator.ViewImpact(document,
                    actionId.Substring(syncViewPrefix.Length));
                return;
            }
            const string syncRunPrefix = "sync.run|";
            if (actionId.StartsWith(syncRunPrefix,
                StringComparison.OrdinalIgnoreCase))
            {
                CadSyncCoordinator.RunTask(document,
                    actionId.Substring(syncRunPrefix.Length));
                return;
            }
            if (string.Equals(actionId, "sync.run-all",
                StringComparison.OrdinalIgnoreCase))
            {
                CadSyncCoordinator.RunAll(document, false);
                return;
            }
            if (string.Equals(actionId, "sync.confirm-all",
                StringComparison.OrdinalIgnoreCase))
            {
                CadSyncCoordinator.RunAll(document, true);
                return;
            }
            if (string.Equals(actionId, "sync.cancel-all",
                StringComparison.OrdinalIgnoreCase))
                CadSyncCoordinator.CancelBatch(document);
        }

        private static void Refresh(bool initial)
        {
            if (!_initialized) return;
            CDBoxStudioSettings settings = CDBoxStudioSettingsStore.Load();
            Document document = null;
            try { document = AcadApp.DocumentManager.MdiActiveDocument; }
            catch { }
            bool minimized = IsCadMinimized();
            _lastCadMinimized = minimized;
            if (!settings.FloatingCenterEnabled || document == null || minimized ||
                _temporaryHidden || (initial && !settings.FloatingCenterShowOnStartup))
            {
                if (document == null) _activeDocumentId = string.Empty;
                if (_window != null) _window.HideShell();
                HideMessageCard(false);
                return;
            }

            EnsureWindow();
            if (_window == null || _center == null) return;
            _window.ApplySettings(settings);
            string id = CadFloatingDocumentIdentity.GetDocumentId(document);
            _activeDocumentId = id;
            IList<FloatingMessage> active = _center.GetActiveMessages(id);
            IList<FloatingMessage> history = _center.GetHistory(id);
            _window.SetSnapshot(new FloatingCenterViewSnapshot
            {
                DocumentId = id,
                DocumentName = ResolveDocumentName(document),
                Status = _center.GetStatus(id),
                Active = active,
                History = history,
                Ignored = CadDrawingCheckCoordinator.GetIgnoredMessages(document)
            });
            _window.ShowShell(initial);
            if (!_window.IsExpanded) QueuePresentationForActiveDocument(id);
        }

        private static string ResolveDocumentName(Document document)
        {
            if (document == null) return "当前图纸";
            try
            {
                string name = Path.GetFileName(document.Name);
                return string.IsNullOrWhiteSpace(name) ? document.Name : name;
            }
            catch { return "当前图纸"; }
        }

        private static void CenterChanged(object sender,
            FloatingCenterChangedEventArgs e)
        {
            string id = e == null ? string.Empty : e.DocumentId;
            if (!string.IsNullOrWhiteSpace(_activeDocumentId) &&
                !string.Equals(id, _activeDocumentId,
                    StringComparison.OrdinalIgnoreCase)) return;
            BeginOnUi(delegate
            {
                QueuePresentationForActiveDocument(id);
                Refresh(false);
            });
        }

        private static void DocumentActivated(object sender,
            DocumentCollectionEventArgs e)
        {
            BeginOnUi(delegate
            {
                HideMessageCard(false);
                Refresh(false);
            });
        }

        private static void DocumentToBeDestroyed(object sender,
            DocumentCollectionEventArgs e)
        {
            string id = e == null || e.Document == null ? string.Empty :
                CadFloatingDocumentIdentity.GetDocumentId(e.Document);
            BeginOnUi(delegate
            {
                RemovePresentedDocument(id);
                HideMessageCard(false);
                Refresh(false);
            });
        }

        private static void HostTimerTick(object sender, EventArgs e)
        {
            bool minimized = IsCadMinimized();
            string documentId = string.Empty;
            try
            {
                Document current = AcadApp.DocumentManager.MdiActiveDocument;
                if (current != null)
                    documentId = CadFloatingDocumentIdentity.GetDocumentId(current);
            }
            catch { }
            if (minimized != _lastCadMinimized ||
                !string.Equals(documentId, _activeDocumentId,
                    StringComparison.OrdinalIgnoreCase))
            {
                _lastCadMinimized = minimized;
                Refresh(false);
            }
            else if (!minimized && _window == null)
            {
                Refresh(false);
            }
        }

        private static void BeginOnUi(Action action)
        {
            if (action == null) return;
            Dispatcher dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;
            try
            {
                if (dispatcher.CheckAccess()) action();
                else dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
            }
            catch { }
        }

        private static bool IsCadMinimized()
        {
            try
            {
                IntPtr handle = AcadApp.MainWindow == null
                    ? IntPtr.Zero : AcadApp.MainWindow.Handle;
                return handle == IntPtr.Zero || IsIconic(handle) ||
                    !IsWindowVisible(handle);
            }
            catch { return false; }
        }

        private static void QueuePresentationForActiveDocument(string documentId)
        {
            if (_center == null || string.IsNullOrWhiteSpace(documentId)) return;
            IList<FloatingMessage> active = _center.GetActiveMessages(documentId);
            var activeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FloatingMessage message in active)
                if (message != null && !string.IsNullOrWhiteSpace(message.Id))
                    activeIds.Add(message.Id);
            if (_messageCard != null)
                _messageCard.RemoveInactivePersistent(documentId, activeIds);
            foreach (FloatingMessage message in active)
            {
                if (message != null && message.PresentAsCard)
                    PresentOrQueue(message);
            }
            IList<FloatingMessage> history = _center.GetHistory(documentId);
            if (history.Count > 0 && history[0] != null &&
                history[0].PresentAsCard) PresentOrQueue(history[0]);
        }

        private static void PresentOrQueue(FloatingMessage message)
        {
            if (message == null || !message.PresentAsCard ||
                !string.Equals(message.DocumentId, _activeDocumentId,
                    StringComparison.OrdinalIgnoreCase)) return;
            string presentationKey = PresentationKey(message);
            DateTime version = message.UpdatedAt == default(DateTime)
                ? message.CreatedAt : message.UpdatedAt;
            DateTime shownVersion;
            if (PresentedCards.TryGetValue(presentationKey, out shownVersion)
                && version <= shownVersion)
            {
                return;
            }
            EnsureMessageCard();
            if (_messageCard == null) return;
            CDBoxStudioSettings settings = CDBoxStudioSettingsStore.Load();
            if (message.Kind == FloatingMessageKind.Prompt)
            {
                if (_window != null && _window.IsExpanded)
                    _window.ClosePanelImmediately();
            }
            if (_window == null || _window.IsExpanded) return;
            PresentedCards[presentationKey] = version;
            _messageCard.UpsertMessage(message,
                _window.GetCollapsedAnchor(), settings);
        }

        private static void EnsureMessageCard()
        {
            if (_messageCard != null) return;
            try
            {
                _messageCard = new FloatingMessageCardWindow();
                IntPtr owner = IntPtr.Zero;
                try
                {
                    if (AcadApp.MainWindow != null)
                        owner = AcadApp.MainWindow.Handle;
                }
                catch { }
                if (owner != IntPtr.Zero)
                    new WindowInteropHelper(_messageCard).Owner = owner;
                _messageCard.CardDismissed += delegate(object sender,
                    FloatingMessageCardEventArgs e)
                {
                    // Closing a toast only dismisses its presentation. Persistent
                    // tasks remain available in the expanded floating center.
                };
                _messageCard.DetailsRequested += delegate(object sender,
                    FloatingMessageCardEventArgs e)
                {
                    if (_window != null && e != null && e.Message != null &&
                        e.Message.Kind != FloatingMessageKind.Prompt)
                    {
                        _window.OpenPanel();
                    }
                };
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Error("悬浮球提示卡创建失败。", ex);
                _messageCard = null;
            }
        }

        private static void TryShowNextCard()
        {
            if (_window == null || !_window.IsVisible || _window.IsExpanded ||
                _temporaryHidden || IsCadMinimized()) return;
            QueuePresentationForActiveDocument(_activeDocumentId);
        }

        private static void HideMessageCard(bool clearQueue)
        {
            if (_messageCard != null && _messageCard.IsVisible)
                _messageCard.CloseImmediately();
        }

        private static string PresentationKey(FloatingMessage message)
        {
            return (message.DocumentId ?? string.Empty) + "|" +
                (string.IsNullOrWhiteSpace(message.Id)
                    ? message.MergeKey ?? string.Empty : message.Id);
        }

        private static DateTime MessageVersion(FloatingMessage message)
        {
            if (message == null) return DateTime.MinValue;
            return message.UpdatedAt == default(DateTime)
                ? message.CreatedAt : message.UpdatedAt;
        }

        private static void RemovePresentedDocument(string documentId)
        {
            if (string.IsNullOrWhiteSpace(documentId)) return;
            string prefix = documentId + "|";
            var remove = new List<string>();
            foreach (string key in PresentedCards.Keys)
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    remove.Add(key);
            foreach (string key in remove) PresentedCards.Remove(key);
        }

        private static void RemovePresentedActiveMessages(string documentId)
        {
            if (_center == null || string.IsNullOrWhiteSpace(documentId)) return;
            foreach (FloatingMessage message in _center.GetActiveMessages(documentId))
                if (message != null) PresentedCards.Remove(PresentationKey(message));
        }

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
    }
}
