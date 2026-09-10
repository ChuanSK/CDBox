extern alias WastewaterBusiness;
using PipeSelectionCandidate = WastewaterBusiness::TCPipeAutoDraw.Modules.PipeLengthAnnotation.PipeSelectionCandidate;
using QuantityPipeAttributeService = WastewaterBusiness::TCPipeAutoDraw.Modules.QuantityCalculation.QuantityPipeAttributeService;
using QuantityPipeSelectionInfo = WastewaterBusiness::TCPipeAutoDraw.Modules.QuantityCalculation.QuantityPipeSelectionInfo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using TCPipeAutoDraw.Modules.AnnotationHud;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{

    internal static class OverlappingPipeSelectionService
    {
        public static PipeSelectionCandidate Select(Document doc,
            IList<PipeSelectionCandidate> candidates, Transaction activeTransaction)
        {
            if (candidates == null || candidates.Count == 0) return null;
            var sessionCandidates = new List<PipeSelectionCandidate>();
            var seen = new HashSet<ObjectId>();
            for (int i = 0; i < candidates.Count; i++)
            {
                PipeSelectionCandidate candidate = candidates[i];
                if (candidate == null || candidate.ObjectId.IsNull || !seen.Add(candidate.ObjectId)) continue;
                sessionCandidates.Add(candidate);
            }
            if (sessionCandidates.Count == 0) return null;
            if (sessionCandidates.Count == 1) return sessionCandidates[0];
            Point? animationOrigin = ResolveAnimationOrigin(doc, sessionCandidates[0]);
            var window = new OverlappingPipeSelectionWindow(sessionCandidates, animationOrigin);
            CDBoxStudioSettings settings = CDBoxStudioSettingsStore.Load();
            window.ApplyAppearance(settings.AnnotationHudNormalOpacity,
                settings.AnnotationHudHoverOpacity, settings.AnnotationHudGlowEnabled,
                settings.AnnotationHudGlowIntensity);
            ObjectId highlighted = ObjectId.Null;
            window.SelectionChanged += delegate(object sender, PipeSelectionCandidateEventArgs e)
            {
                if (doc == null || e == null || e.Candidate == null) return;
                Highlight(doc, activeTransaction, highlighted, false);
                highlighted = e.Candidate.ObjectId;
                Highlight(doc, activeTransaction, highlighted, true);
                Point? selectedOrigin = ResolveAnimationOrigin(doc, e.Candidate);
                if (selectedOrigin.HasValue) window.SetAnimationOrigin(selectedOrigin.Value);
            };
            try
            {
                IntPtr owner = AcadApp.MainWindow == null ? IntPtr.Zero : AcadApp.MainWindow.Handle;
                if (owner != IntPtr.Zero) new WindowInteropHelper(window).Owner = owner;
                bool accepted = AcadApp.ShowModalWindow(window) == true;
                return accepted ? window.SelectedCandidate : null;
            }
            finally
            {
                Highlight(doc, activeTransaction, highlighted, false);
                try { doc.Editor.Regen(); } catch { }
            }
        }

        public static void EnrichDisplay(Document doc, IList<PipeSelectionCandidate> candidates)
        {
            if (doc == null || candidates == null) return;
            for (int i = 0; i < candidates.Count; i++)
            {
                PipeSelectionCandidate candidate = candidates[i];
                QuantityPipeSelectionInfo info = null;
                try { info = QuantityPipeAttributeService.ReadPipe(doc, candidate.ObjectId); }
                catch { }
                string kind = info == null || string.IsNullOrWhiteSpace(info.InferredKind)
                    ? "普通长度对象" : info.InferredKind;
                string spec = string.Empty;
                if (info != null && info.Attributes != null)
                {
                    string material = (info.Attributes.Material ?? string.Empty).Trim();
                    string diameter = (info.Attributes.Diameter ?? string.Empty).Trim();
                    spec = (material + " " + diameter).Trim();
                }
                candidate.Title = string.IsNullOrWhiteSpace(spec) ? kind : kind + " · " + spec;
                candidate.Detail = (candidate.LayerName ?? string.Empty)
                    + " · 长度 " + candidate.Length.ToString("0.##", CultureInfo.InvariantCulture) + "m";
            }
        }

        private static void Highlight(Document doc, Transaction activeTransaction, ObjectId id, bool enabled)
        {
            if (doc == null || id.IsNull) return;
            try
            {
                if (activeTransaction != null)
                {
                    Entity entity = activeTransaction.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity != null)
                    {
                        if (enabled) entity.Highlight(); else entity.Unhighlight();
                    }
                }
                else
                {
                    using (Transaction tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
                    {
                        Entity entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                        if (entity != null)
                        {
                            if (enabled) entity.Highlight(); else entity.Unhighlight();
                        }
                        tr.Commit();
                    }
                }
                doc.Editor.UpdateScreen();
            }
            catch { }
        }

        private static Point? ResolveAnimationOrigin(Document doc, PipeSelectionCandidate candidate)
        {
            if (doc != null && candidate != null)
            {
                try { return doc.Editor.PointToScreen(candidate.AnchorPoint, 0); }
                catch { }
            }
            return null;
        }
    }

    internal sealed class PipeSelectionCandidateEventArgs : EventArgs
    {
        public PipeSelectionCandidate Candidate { get; private set; }
        public PipeSelectionCandidateEventArgs(PipeSelectionCandidate candidate) { Candidate = candidate; }
    }

    internal sealed class OverlappingPipeSelectionWindow : AnnotationHudWindowBase
    {
        private readonly IList<PipeSelectionCandidate> _candidates;
        private readonly List<Border> _rows = new List<Border>();
        private int _selectedIndex;
        private readonly Point? _openingAnimationOrigin;
        private bool _mouseConfirmEnabled;
        private DateTime _openedAtUtc;
        private DispatcherTimer _mouseReleaseTimer;

        public event EventHandler<PipeSelectionCandidateEventArgs> SelectionChanged;
        public PipeSelectionCandidate SelectedCandidate { get; private set; }

        public OverlappingPipeSelectionWindow(IList<PipeSelectionCandidate> candidates,
            Point? openingAnimationOrigin)
            : base("重叠管线选择", 380, true)
        {
            _candidates = candidates ?? new List<PipeSelectionCandidate>();
            _openingAnimationOrigin = openingAnimationOrigin;
            _selectedIndex = -1;
            SelectedCandidate = null;
            MaxHeight = 560;
            var title = new TextBlock
            {
                Text = "此处有 " + _candidates.Count + " 个长度对象",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = TCPipeAutoDraw.UI.Studio.WastewaterNativeAppearance.Brush("#253550"),
                Cursor = Cursors.SizeAll
            };
            EnableDrag(title);
            Body.Children.Add(title);
            Body.Children.Add(new TextBlock
            {
                Text = "TAB / ↑↓ / 滚轮切换，单击、回车或空格确认",
                Margin = new Thickness(0, 3, 0, 9),
                Foreground = TCPipeAutoDraw.UI.Studio.WastewaterNativeAppearance.Brush("#65758A"),
                FontSize = 10.8
            });

            var stack = new StackPanel();
            var scroll = new ScrollViewer
            {
                Content = stack,
                MaxHeight = 390,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Body.Children.Add(scroll);
            for (int i = 0; i < _candidates.Count; i++)
            {
                int index = i;
                var row = new Border
                {
                    CornerRadius = new CornerRadius(7),
                    BorderThickness = new Thickness(1),
                    BorderBrush = TCPipeAutoDraw.UI.Studio.WastewaterNativeAppearance.Brush("#D8E2EF"),
                    Background = Brushes.White,
                    Padding = new Thickness(9, 7, 9, 7),
                    Margin = new Thickness(0, 0, 0, 5),
                    Cursor = Cursors.Hand
                };
                var content = new Grid();
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var number = new TextBlock
                {
                    Text = (i + 1).ToString(CultureInfo.InvariantCulture),
                    Foreground = TCPipeAutoDraw.UI.Studio.CDBoxAccentAppearance.AccentBrush,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                content.Children.Add(number);
                var text = new StackPanel();
                text.Children.Add(new TextBlock
                {
                    Text = _candidates[i].Title,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TCPipeAutoDraw.UI.Studio.WastewaterNativeAppearance.Brush("#253550"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                text.Children.Add(new TextBlock
                {
                    Text = _candidates[i].Detail,
                    Foreground = TCPipeAutoDraw.UI.Studio.WastewaterNativeAppearance.Brush("#65758A"),
                    FontSize = 10.8,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                Grid.SetColumn(text, 1);
                content.Children.Add(text);
                row.Child = content;
                row.MouseEnter += delegate { SelectIndex(index); };
                row.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    SelectIndex(index);
                    if (!_mouseConfirmEnabled
                        || (DateTime.UtcNow - _openedAtUtc).TotalMilliseconds < 160.0) return;
                    ConfirmSelection();
                };
                _rows.Add(row);
                stack.Children.Add(row);
            }
            PreviewKeyDown += KeyDownHandler;
            PreviewMouseWheel += MouseWheelHandler;
            Loaded += delegate
            {
                _openedAtUtc = DateTime.UtcNow;
                _mouseConfirmEnabled = Mouse.LeftButton == MouseButtonState.Released;
                if (!_mouseConfirmEnabled)
                {
                    _mouseReleaseTimer = new DispatcherTimer(
                        TimeSpan.FromMilliseconds(40), DispatcherPriority.Input,
                        delegate
                        {
                            if (Mouse.LeftButton != MouseButtonState.Released) return;
                            _mouseConfirmEnabled = true;
                            if (_mouseReleaseTimer != null) _mouseReleaseTimer.Stop();
                            _mouseReleaseTimer = null;
                        }, Dispatcher);
                    _mouseReleaseTimer.Start();
                }
                SelectIndex(0, true);
                Point origin = _openingAnimationOrigin ?? GetCursorScreenPosition();
                ShowAnimated(origin, null, null);
                Activate();
                Focus();
            };
            Closed += delegate
            {
                if (_mouseReleaseTimer != null) _mouseReleaseTimer.Stop();
                _mouseReleaseTimer = null;
            };
        }

        protected override void OnEscapePressed()
        {
            CompleteDialogAnimated(false);
        }

        private void KeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab || e.Key == Key.Down || e.Key == Key.Up)
            {
                int delta = e.Key == Key.Up || (e.Key == Key.Tab &&
                    (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) ? -1 : 1;
                SelectIndex(_selectedIndex + delta);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                ConfirmSelection();
                e.Handled = true;
            }
        }

        private void MouseWheelHandler(object sender, MouseWheelEventArgs e)
        {
            SelectIndex(_selectedIndex + (e.Delta > 0 ? -1 : 1));
            e.Handled = true;
        }

        private void SelectIndex(int index, bool force = false)
        {
            if (_candidates.Count == 0) return;
            index %= _candidates.Count;
            if (index < 0) index += _candidates.Count;
            if (!force && index == _selectedIndex) return;
            _selectedIndex = index;
            for (int i = 0; i < _rows.Count; i++)
            {
                bool selected = i == _selectedIndex;
                _rows[i].Background = selected ? TCPipeAutoDraw.UI.Studio.CDBoxAccentAppearance.SoftBrush : Brushes.White;
                _rows[i].BorderBrush = selected ? TCPipeAutoDraw.UI.Studio.CDBoxAccentAppearance.AccentBrush : TCPipeAutoDraw.UI.Studio.WastewaterNativeAppearance.Brush("#D8E2EF");
                _rows[i].BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            }
            EventHandler<PipeSelectionCandidateEventArgs> handler = SelectionChanged;
            if (handler != null) handler(this,
                new PipeSelectionCandidateEventArgs(_candidates[_selectedIndex]));
        }

        private void ConfirmSelection()
        {
            if (_candidates.Count == 0) return;
            SelectedCandidate = _candidates[_selectedIndex];
            CompleteDialogAnimated(true);
        }
    }
}
