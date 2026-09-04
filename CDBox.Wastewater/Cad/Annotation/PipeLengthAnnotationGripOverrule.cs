using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    /// <summary>
    /// 为标准 DBText/Polyline 提供两类受控夹点，同时保持 DWG 对象仍为普通 CAD 图元。
    /// </summary>
    internal sealed class PipeLengthAnnotationGripOverrule : GripOverrule
    {
        private static PipeLengthAnnotationGripOverrule _instance;
        private static BindingMarkerDrawableOverrule _markerOverrule;
        private static readonly Dictionary<ObjectId, PendingBindingEdit> PendingBindings =
            new Dictionary<ObjectId, PendingBindingEdit>();
        private static readonly HashSet<ObjectId> MovedBindingGrips = new HashSet<ObjectId>();
        private static readonly Queue<Action> IdleActions = new Queue<Action>();
        private static bool _idleHooked;
        private static bool _markerRefreshQueued;
        private static bool _markerSuspended;
        private static Database _selectedMarkerDatabase;
        private static ObjectId _selectedMarkerLeaderId = ObjectId.Null;
        private static string _selectedMarkerAnnotationId = string.Empty;
        private static double _selectedMarkerRadius = 0.05;
        private static Vector3d _selectedMarkerNormal = Vector3d.ZAxis;

        public static void Initialize()
        {
            if (_instance != null) return;
            _instance = new PipeLengthAnnotationGripOverrule();
            // 必须使用 AutoCAD 原生 XData 过滤。SetCustomFilter 会让所有普通
            // Polyline/DBText 进入托管 IsApplicable；AutoCAD 2016 在
            // COPYCLIP/PASTECLIP 的临时克隆对象上执行该回调时可能原生崩溃。
            _instance.SetXDataFilter(
                PipeLengthAnnotationObjectService.AnnotationXDataApplicationName);
            Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _instance, false);
            Overrule.AddOverrule(RXObject.GetClass(typeof(DBText)), _instance, false);
            _markerOverrule = new BindingMarkerDrawableOverrule();
            _markerOverrule.SetXDataFilter(
                PipeLengthAnnotationObjectService.AnnotationXDataApplicationName);
            Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _markerOverrule, false);
            Overrule.Overruling = true;
        }

        public static void Terminate()
        {
            if (_instance == null) return;
            try { Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _instance); } catch { }
            try { Overrule.RemoveOverrule(RXObject.GetClass(typeof(DBText)), _instance); } catch { }
            if (_markerOverrule != null)
            {
                try { Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _markerOverrule); } catch { }
                try { _markerOverrule.Dispose(); } catch { }
                _markerOverrule = null;
            }
            _instance.Dispose();
            _instance = null;
            PendingBindings.Clear();
            MovedBindingGrips.Clear();
            _selectedMarkerDatabase = null;
            _selectedMarkerLeaderId = ObjectId.Null;
            _selectedMarkerAnnotationId = string.Empty;
            _markerRefreshQueued = false;
            _markerSuspended = false;
            lock (IdleActions) IdleActions.Clear();
            if (_idleHooked)
            {
                AcadApp.Idle -= OnIdle;
                _idleHooked = false;
            }
        }

        public override void GetGripPoints(Entity entity, GripDataCollection grips, double currentViewUnitSize,
            int gripSize, Vector3d currentViewDirection, GetGripPointsFlags bitFlags)
        {
            string annotationId;
            string part;
            if (!PipeLengthAnnotationObjectService.TryGetAnnotationPartFromXData(
                entity, out annotationId, out part)) return;

            Polyline leader = entity as Polyline;
            if (leader != null && string.Equals(part, "LeaderLine", StringComparison.OrdinalIgnoreCase)
                && leader.NumberOfVertices >= 3)
            {
                grips.Add(new BindingGripData(leader.GetPoint3dAt(0)));
                return;
            }

            DBText text = entity as DBText;
            if (text != null && (string.Equals(part, "MainText", StringComparison.OrdinalIgnoreCase)
                || PipeLengthAnnotationObjectService.IsSecondaryAnnotationPart(part)))
            {
                grips.Add(new TextGripData(GetTextPoint(text)));
            }
        }

        public override bool IsApplicable(RXObject overruledSubject)
        {
            Entity entity = overruledSubject as Entity;
            string annotationId;
            string annotationPart;
            return entity != null
                && PipeLengthAnnotationObjectService.TryGetAnnotationPartFromXData(
                    entity, out annotationId, out annotationPart);
        }

        public override void MoveGripPointsAt(Entity entity, GripDataCollection grips, Vector3d offset,
            MoveGripPointsFlags bitFlags)
        {
            if (entity == null || grips == null || grips.Count == 0) return;
            for (int i = 0; i < grips.Count; i++)
            {
                BindingGripData binding = grips[i] as BindingGripData;
                if (binding != null)
                {
                    Polyline leader = entity as Polyline;
                    if (leader == null || leader.NumberOfVertices < 3) continue;
                    if (!entity.ObjectId.IsNull && !PendingBindings.ContainsKey(entity.ObjectId))
                    {
                        Document dragDocument = null;
                        try { dragDocument = AcadApp.DocumentManager.GetDocument(entity.Database); } catch { }
                        string annotationId;
                        string annotationPart;
                        PipeLengthAnnotationObjectService.TryGetAnnotationPart(
                            entity, out annotationId, out annotationPart);
                        PendingBindings[entity.ObjectId] = new PendingBindingEdit(
                            binding.OriginalPoint, annotationId);
                        MovedBindingGrips.Add(entity.ObjectId);
                        if (dragDocument != null && !string.IsNullOrWhiteSpace(annotationId))
                        {
                            PipeLengthAnnotationInteractionService.NotifySpatialEditStarted(dragDocument, annotationId);
                        }
                    }
                    Point3d point = binding.OriginalPoint + offset;
                    leader.SetPointAt(0, new Point2d(point.X, point.Y));
                    continue;
                }

                // 文字夹点通过 OnHotGrip 进入与新建标注相同的定位预览，不执行原生拉伸。
            }
        }

        internal static void CompletePendingBindings(Document doc)
        {
            if (doc == null || PendingBindings.Count == 0) return;
            var completed = new List<KeyValuePair<ObjectId, PendingBindingEdit>>();
            foreach (KeyValuePair<ObjectId, PendingBindingEdit> item in PendingBindings)
            {
                try
                {
                    if (item.Key.Database == doc.Database) completed.Add(item);
                }
                catch { }
            }
            for (int i = 0; i < completed.Count; i++)
            {
                KeyValuePair<ObjectId, PendingBindingEdit> item = completed[i];
                PendingBindings.Remove(item.Key);
                QueueAutoBind(doc, item.Key, item.Value);
            }
        }

        internal static void CancelPendingBindings(Document doc)
        {
            if (doc == null || PendingBindings.Count == 0) return;
            var cancelled = new List<KeyValuePair<ObjectId, PendingBindingEdit>>();
            foreach (KeyValuePair<ObjectId, PendingBindingEdit> item in PendingBindings)
            {
                try
                {
                    if (item.Key.Database == doc.Database) cancelled.Add(item);
                }
                catch { }
            }
            for (int i = 0; i < cancelled.Count; i++)
            {
                KeyValuePair<ObjectId, PendingBindingEdit> item = cancelled[i];
                PendingBindings.Remove(item.Key);
                PipeLengthAnnotationInteractionService.NotifySpatialEditFinished(
                    doc, item.Value.AnnotationId);
            }
            RequestMarkerRefresh(doc);
        }

        internal static void SetSelectedAnnotation(Document doc, string annotationId)
        {
            if (doc == null || string.IsNullOrWhiteSpace(annotationId))
            {
                ClearSelectedAnnotation(doc);
                return;
            }

            bool changed = _selectedMarkerDatabase != doc.Database
                || !string.Equals(_selectedMarkerAnnotationId, annotationId,
                    StringComparison.OrdinalIgnoreCase);
            _selectedMarkerDatabase = doc.Database;
            _selectedMarkerAnnotationId = annotationId;
            _selectedMarkerLeaderId = PipeLengthAnnotationObjectService.GetAnnotationLeaderId(
                doc, annotationId);
            if (changed) _markerSuspended = false;
            UpdateMarkerMetrics(doc);
            if (changed) RequestMarkerRefresh(doc);
        }

        internal static void ClearSelectedAnnotation(Document doc)
        {
            if (_selectedMarkerDatabase == null) return;
            if (doc != null && _selectedMarkerDatabase != doc.Database) return;
            _selectedMarkerDatabase = null;
            _selectedMarkerLeaderId = ObjectId.Null;
            _selectedMarkerAnnotationId = string.Empty;
            _markerSuspended = false;
            RequestMarkerRefresh(doc);
        }

        internal static void SuspendSelectedMarker(Document doc, string annotationId)
        {
            if (doc == null || _selectedMarkerDatabase != doc.Database
                || !string.Equals(_selectedMarkerAnnotationId, annotationId,
                    StringComparison.OrdinalIgnoreCase)) return;
            _markerSuspended = true;
        }

        internal static void ResumeSelectedMarker(Document doc, string annotationId)
        {
            if (doc == null || _selectedMarkerDatabase != doc.Database
                || !string.Equals(_selectedMarkerAnnotationId, annotationId,
                    StringComparison.OrdinalIgnoreCase)) return;
            _markerSuspended = false;
            UpdateMarkerMetrics(doc);
            RequestMarkerRefresh(doc);
        }

        private static void UpdateMarkerMetrics(Document doc)
        {
            if (doc == null) return;
            try
            {
                using (ViewTableRecord view = doc.Editor.GetCurrentView())
                {
                    Vector3d direction = view.ViewDirection;
                    _selectedMarkerNormal = direction.Length < 0.0000001
                        ? Vector3d.ZAxis
                        : direction.GetNormal();

                    object screenObject = AcadApp.GetSystemVariable("SCREENSIZE");
                    object gripObject = AcadApp.GetSystemVariable("GRIPSIZE");
                    if (!(screenObject is Point2d) || view.Height <= 0.0) return;
                    Point2d screen = (Point2d)screenObject;
                    if (screen.Y <= 1.0) return;
                    int gripSize = 5;
                    try { gripSize = Math.Max(Convert.ToInt32(gripObject), 1); } catch { }
                    _selectedMarkerRadius = Math.Max(view.Height / screen.Y * gripSize * 1.15, 0.000001);
                }
            }
            catch { }
        }

        private static void RequestMarkerRefresh(Document doc)
        {
            if (doc == null || _markerRefreshQueued) return;
            _markerRefreshQueued = true;
            QueueIdle(delegate
            {
                _markerRefreshQueued = false;
                if (_markerSuspended) return;
                try
                {
                    Document active = AcadApp.DocumentManager.MdiActiveDocument;
                    if (active != null && (active == doc
                        || (_selectedMarkerDatabase != null
                            && active.Database == _selectedMarkerDatabase))) active.Editor.Regen();
                }
                catch { }
            });
        }

        private static bool TryGetVisibleMarker(Polyline leader, out double radius, out Vector3d normal)
        {
            radius = _selectedMarkerRadius;
            normal = _selectedMarkerNormal;
            if (leader == null || leader.NumberOfVertices == 0 || _selectedMarkerDatabase == null
                || _selectedMarkerLeaderId.IsNull || leader.Database != _selectedMarkerDatabase
                || leader.ObjectId != _selectedMarkerLeaderId || _markerSuspended
                || PendingBindings.Count > 0) return false;
            return true;
        }

        private static void QueueAutoBind(Document doc, ObjectId leaderId, PendingBindingEdit edit)
        {
            if (doc == null || leaderId.IsNull || edit == null) return;
            QueueIdle(delegate
            {
                try
                {
                    AcadApp.DocumentManager.ExecuteInCommandContextAsync(delegate(object state)
                    {
                        try
                        {
                            PipeLengthAnnotationObjectService.AutoBindLeaderGrip(
                                doc, leaderId, edit.OriginalPoint);
                        }
                        catch (System.Exception ex)
                        {
                            try { doc.Editor.WriteHudMessage("\n[CDBox 标注绑定] 自动更新失败：" + ex.Message); }
                            catch { }
                        }
                        finally
                        {
                            PipeLengthAnnotationInteractionService.NotifySpatialEditFinished(
                                doc, edit.AnnotationId);
                            RequestMarkerRefresh(doc);
                        }
                        return Task.CompletedTask;
                    }, null);
                }
                catch (System.Exception ex)
                {
                    try { doc.Editor.WriteHudMessage("\n[CDBox 标注绑定] 自动更新调度失败：" + ex.Message); }
                    catch { }
                    PipeLengthAnnotationInteractionService.NotifySpatialEditFinished(
                        doc, edit.AnnotationId);
                    RequestMarkerRefresh(doc);
                }
            });
        }

        private static void QueueReposition(ObjectId objectId, string annotationId)
        {
            if (objectId.IsNull) return;
            QueueIdle(delegate
            {
                Document doc = null;
                try { doc = AcadApp.DocumentManager.GetDocument(objectId.Database); } catch { }
                if (doc == null) return;
                Document capturedDoc = doc;
                AcadApp.DocumentManager.ExecuteInCommandContextAsync(delegate(object state)
                {
                    try { PipeLengthAnnotationService.RepositionExistingAnnotation(capturedDoc, objectId); }
                    finally
                    {
                        PipeLengthAnnotationInteractionService.NotifySpatialEditFinished(capturedDoc, annotationId);
                    }
                    return Task.CompletedTask;
                }, null);
            });
        }

        private static void QueueIdle(Action action)
        {
            if (action == null) return;
            lock (IdleActions) IdleActions.Enqueue(action);
            if (_idleHooked) return;
            AcadApp.Idle += OnIdle;
            _idleHooked = true;
        }

        private static void OnIdle(object sender, EventArgs e)
        {
            AcadApp.Idle -= OnIdle;
            _idleHooked = false;
            while (true)
            {
                Action action;
                lock (IdleActions)
                {
                    if (IdleActions.Count == 0) break;
                    action = IdleActions.Dequeue();
                }
                try { action(); } catch { }
            }
        }

        private static Point3d GetTextPoint(DBText text)
        {
            try
            {
                if (text.HorizontalMode != TextHorizontalMode.TextLeft || text.VerticalMode != TextVerticalMode.TextBase)
                {
                    return text.AlignmentPoint;
                }
            }
            catch { }
            return text.Position;
        }

        private sealed class BindingMarkerDrawableOverrule
            : Autodesk.AutoCAD.GraphicsInterface.DrawableOverrule
        {
            public override bool IsApplicable(RXObject overruledSubject)
            {
                double radius;
                Vector3d normal;
                return TryGetVisibleMarker(overruledSubject as Polyline, out radius, out normal);
            }

            public override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.Drawable drawable,
                Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
            {
                bool result = base.WorldDraw(drawable, draw);
                Polyline leader = drawable as Polyline;
                double radius;
                Vector3d normal;
                if (draw == null || draw.Geometry == null || draw.IsDragging
                    || (draw.Context != null && draw.Context.IsPlotGeneration)
                    || !TryGetVisibleMarker(leader, out radius, out normal)) return result;
                try
                {
                    draw.SubEntityTraits.TrueColor = new EntityColor(255, 24, 24);
                    draw.SubEntityTraits.LineWeight = LineWeight.LineWeight050;
                    draw.Geometry.Circle(leader.GetPoint3dAt(0), radius, normal);
                }
                catch { }
                return result;
            }
        }

        private sealed class BindingGripData : GripData
        {
            public Point3d OriginalPoint { get; private set; }
            public BindingGripData(Point3d point)
            {
                OriginalPoint = point;
                GripPoint = point;
                DrawAtDragImageGripPoint = true;
            }
            public override string GetTooltip() { return "拖动绑定点并直接落在目标管线上"; }

            public override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw,
                ObjectId entityId, DrawType type, Point3d? imageGripPoint, double gripSize)
            {
                // The visible selected-state marker is drawn with the leader itself.
                // Consuming the drag glyph here keeps the marker hidden while dragging.
                return true;
            }

            public override void OnGripStatusChanged(ObjectId entityId, Status status)
            {
                base.OnGripStatusChanged(entityId, status);
                if (entityId.IsNull) return;

                Document doc = null;
                try { doc = AcadApp.DocumentManager.GetDocument(entityId.Database); } catch { }
                if (doc == null) return;

                string annotationId;
                if (!PipeLengthAnnotationObjectService.TryGetAnnotationId(entityId, out annotationId)
                    || string.IsNullOrWhiteSpace(annotationId)) return;

                if (status == Status.GripStart)
                {
                    PipeLengthAnnotationInteractionService.NotifySpatialEditStarted(doc, annotationId);
                    return;
                }

                if (status != Status.GripEnd && status != Status.GripAbort) return;
                if (MovedBindingGrips.Remove(entityId)) return;
                PipeLengthAnnotationInteractionService.NotifySpatialEditFinished(doc, annotationId);
            }
        }

        private sealed class PendingBindingEdit
        {
            public Point3d OriginalPoint { get; private set; }
            public string AnnotationId { get; private set; }

            public PendingBindingEdit(Point3d originalPoint, string annotationId)
            {
                OriginalPoint = originalPoint;
                AnnotationId = annotationId ?? string.Empty;
            }
        }

        private sealed class TextGripData : GripData
        {
            public TextGripData(Point3d point)
            {
                GripPoint = point;
                TriggerGrip = true;
                RubberBandLineDisabled = true;
            }
            public override string GetTooltip() { return "拖动文字与横线"; }
            public override ReturnValue OnHotGrip(ObjectId entityId, Context context)
            {
                string annotationId;
                PipeLengthAnnotationObjectService.TryGetAnnotationId(entityId, out annotationId);
                Document doc = null;
                try { doc = AcadApp.DocumentManager.GetDocument(entityId.Database); } catch { }
                if (doc != null)
                {
                    PipeLengthAnnotationInteractionService.NotifySpatialEditStarted(doc, annotationId);
                }
                QueueReposition(entityId, annotationId);
                return ReturnValue.GripHotToWarm;
            }
        }
    }
}
