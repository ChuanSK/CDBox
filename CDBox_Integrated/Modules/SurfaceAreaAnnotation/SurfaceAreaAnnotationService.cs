using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using TCPipeAutoDraw.Core.Cad;
using TCPipeAutoDraw.Modules.AnnotationHud;

namespace TCPipeAutoDraw.Modules.SurfaceAreaAnnotation
{
    /// <summary>
    /// ??????????
    /// ????????? CASS surfacearea ????? CASS ????????? CASS ????????/???????
    /// ???? TIN ??????????? CASS ??????
    /// </summary>
    public static class SurfaceAreaAnnotationService
    {
        private const double Eps = 1e-8;
        private const double DuplicateTolerance = 0.001;
        private const string CassSurfaceAreaCommandName = "surfacearea";
        private const string CassFinishCommandName = "TCBMJ_CASS_FINISH";

        private static CassCommandSession _pendingCassSession;
        private static bool _cassFinishIdleHandlerAttached;

        public static SurfaceAreaAnnotationResult SelectCalculateAndAnnotate(Document doc, SurfaceAreaAnnotationOptions options)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            options = NormalizeOptions(options);

            if (options.CalculationMode == SurfaceAreaCalculationMode.CassCommand)
            {
                return StartCassCommandCalculateAndAnnotate(doc, options);
            }

            Editor ed = doc.Editor;
            PromptEntityOptions peo = new PromptEntityOptions("\n?????????????????/?/?????");
            peo.SetRejectMessage("\n??????????? ");
            peo.AddAllowedClass(typeof(Curve), false);

            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                return new SurfaceAreaAnnotationResult { Success = false, Message = "??????" };
            }

            SurfaceAreaAnnotationResult previewResult;
            string previewMessage;
            if (!TryBuildBoundaryAreaCheck(doc, per.ObjectId, options,
                out previewResult, out previewMessage))
            {
                return new SurfaceAreaAnnotationResult
                {
                    Success = false,
                    CalculationMode = options.CalculationMode,
                    BoundaryObjectId = per.ObjectId,
                    Message = previewMessage
                };
            }
            previewResult.CalculationMode = options.CalculationMode;

            PromptPointOptions ppoStart = new PromptPointOptions("\n?????????");
            PromptPointResult pprStart = ed.GetPoint(ppoStart);
            if (pprStart.Status != PromptStatus.OK)
            {
                return new SurfaceAreaAnnotationResult { Success = false, Message = "??????????" };
            }

            string previewText = BuildAnnotationText(options, previewResult);
            Point3d annotationPoint;
            if (!TryPromptAnnotationPointWithPreview(doc, pprStart.Value,
                previewText, options, out annotationPoint))
            {
                return new SurfaceAreaAnnotationResult { Success = false, Message = "????????" };
            }

            return CalculateAndAnnotate(doc, per.ObjectId, pprStart.Value,
                annotationPoint, true, options);
        }

        private static SurfaceAreaAnnotationResult StartCassCommandCalculateAndAnnotate(Document doc, SurfaceAreaAnnotationOptions options)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            options = NormalizeOptions(options);
            options.CalculationMode = SurfaceAreaCalculationMode.CassCommand;

            Editor ed = doc.Editor;

            if (_pendingCassSession != null)
            {
                return new SurfaceAreaAnnotationResult
                {
                    Success = false,
                    CalculationMode = SurfaceAreaCalculationMode.CassCommand,
                    Message = "???? CASS ??????????????????????"
                };
            }

            // ????????? SendStringToExecute ???? CASS????? Idle ??????
            // SendStringToExecute / Idle ??? CASS ?????????????? AutoCAD ??????????TCSURF??
            // ???? Editor.Command ???? surfacearea?CASS ??????????????????????????
            PromptEntityOptions peo = new PromptEntityOptions("\n???????");
            peo.SetRejectMessage("\n??????????? ");
            peo.AddAllowedClass(typeof(Curve), false);

            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                return new SurfaceAreaAnnotationResult
                {
                    Success = false,
                    CalculationMode = SurfaceAreaCalculationMode.CassCommand,
                    Message = "??????????"
                };
            }

            string boundaryHandle;
            string boundaryLayer;
            SurfaceAreaAnnotationResult boundaryCheck = ValidateAndGetBoundaryForCass(doc, per.ObjectId, out boundaryHandle, out boundaryLayer);
            if (!boundaryCheck.Success)
            {
                return boundaryCheck;
            }

            SurfaceAreaAnnotationResult areaCheck;
            string areaCheckMessage;
            if (!TryBuildBoundaryAreaCheck(doc, per.ObjectId, options, out areaCheck, out areaCheckMessage))
            {
                boundaryCheck.Message = areaCheckMessage;
                return boundaryCheck;
            }

            // ????????????????????? CASS?
            // CASS ?????????????????? DBText/?/Z ????????????
            // ????????? CASS surfacearea?? CASS ????? surface.log??????????????

            string previousUsers5 = string.Empty;
            try
            {
                object oldValue = Application.GetSystemVariable("USERS5");
                previousUsers5 = oldValue == null ? string.Empty : oldValue.ToString();
            }
            catch
            {
                previousUsers5 = string.Empty;
            }

            var session = new CassCommandSession
            {
                Doc = doc,
                Options = options,
                StartTime = DateTime.Now,
                PreviousUsers5 = previousUsers5,
                BoundaryObjectId = per.ObjectId,
                BoundaryHandle = boundaryHandle,
                BoundaryLayerName = boundaryLayer
            };

            _pendingCassSession = session;

            try
            {
                try { Application.SetSystemVariable("USERS5", boundaryHandle ?? string.Empty); } catch { }

                // ????? CASS ????
                // 1) ObjectAppended ?????
                // 2) ?????????????
                // COM SendCommand ? CASS11 ????????????????????????
                // ????????????? session?? CASS ???? TCBMJ_CASS_FINISH ?????
                session.BeforeObjectIds = SnapshotCurrentSpaceEntityIds(doc);
                try { doc.Database.ObjectAppended += CassDatabase_ObjectAppended; } catch { }

                ed.WriteMessage("\n[?????] ?????????? LISP ?? CASS surfacearea ????????" + boundaryLayer);
                ed.WriteMessage("\n[?????] CASS ??????????/???????\n");

                RunCassSurfaceAreaCommandSynchronously(doc, per.ObjectId, options.BoundaryInterval, boundaryHandle);

                return new SurfaceAreaAnnotationResult
                {
                    Success = true,
                    CalculationMode = SurfaceAreaCalculationMode.CassCommand,
                    AsyncStarted = true,
                    BoundaryObjectId = per.ObjectId,
                    BoundaryLayerName = boundaryLayer,
                    Message = "??? CASS surfacearea ????? CASS ??????????"
                };
            }
            catch (System.Exception ex)
            {
                try { doc.Database.ObjectAppended -= CassDatabase_ObjectAppended; } catch { }
                CleanupCassSession(session, true);

                return new SurfaceAreaAnnotationResult
                {
                    Success = false,
                    CalculationMode = SurfaceAreaCalculationMode.CassCommand,
                    BoundaryObjectId = per.ObjectId,
                    BoundaryLayerName = boundaryLayer,
                    Message = "?? CASS surfacearea ???" + ex.Message
                };
            }
        }

        private static void RunCassSurfaceAreaCommandSynchronously(Document doc, ObjectId boundaryId, double interval, string boundaryHandle)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (string.IsNullOrWhiteSpace(boundaryHandle)) throw new ArgumentException("?????????", "boundaryHandle");

            // Editor.Command ??? ObjectId / SelectionSet ??CASS11 ? surfacearea ????????
            // ????????????????????????????????
            // ??????????????? LISP??? CASS ?????? LISP ???????????
            // (progn (command "surfacearea" "2" (handent "??Handle") 5) (command "TCBMJ_CASS_FINISH") (princ))
            // ????? SendCommand ??????? surface.log / ?????
            string intervalText = interval.ToString(CultureInfo.InvariantCulture);
            string safeHandle = EscapeLispString(boundaryHandle);
            string lisp = "(progn (command \"" + CassSurfaceAreaCommandName + "\" \"2\" (handent \"" + safeHandle + "\") " + intervalText + ") (command \"" + CassFinishCommandName + "\") (princ))";

            try
            {
                object acadDocument = doc.GetAcadDocument();
                if (acadDocument == null)
                {
                    throw new InvalidOperationException("???? AutoCAD COM ?????");
                }

                acadDocument.GetType().InvokeMember(
                    "SendCommand",
                    BindingFlags.InvokeMethod,
                    null,
                    acadDocument,
                    new object[] { lisp + "\n" });
            }
            catch (System.Exception ex)
            {
                throw new InvalidOperationException("???? LISP ?? CASS surfacearea??????" + lisp + "????" + ex.Message, ex);
            }
        }

        private static HashSet<ObjectId> SnapshotCurrentSpaceEntityIds(Document doc)
        {
            var ids = new HashSet<ObjectId>();
            if (doc == null) return ids;

            try
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead, false) as BlockTableRecord;
                    if (btr != null)
                    {
                        foreach (ObjectId id in btr)
                        {
                            if (!id.IsNull) ids.Add(id);
                        }
                    }
                    tr.Commit();
                }
            }
            catch
            {
            }

            return ids;
        }

        private static void MergeNewObjectsFromSnapshot(Document doc, HashSet<ObjectId> beforeObjectIds, CassCommandSession session)
        {
            if (doc == null || session == null) return;
            beforeObjectIds = beforeObjectIds ?? new HashSet<ObjectId>();

            try
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead, false) as BlockTableRecord;
                    if (btr != null)
                    {
                        foreach (ObjectId id in btr)
                        {
                            if (id.IsNull) continue;
                            if (beforeObjectIds.Contains(id)) continue;
                            if (id == session.BoundaryObjectId) continue;

                            if (!session.AppendedObjectIds.Contains(id))
                            {
                                session.AppendedObjectIds.Add(id);
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            catch
            {
            }
        }

        private static SurfaceAreaAnnotationResult ValidateAndGetBoundaryForCass(Document doc, ObjectId boundaryId, out string handleText, out string layerName)
        {
            handleText = string.Empty;
            layerName = string.Empty;

            var result = new SurfaceAreaAnnotationResult();
            result.CalculationMode = SurfaceAreaCalculationMode.CassCommand;
            result.BoundaryObjectId = boundaryId;

            try
            {
                using (doc.LockDocument())
                {
                    Database db = doc.Database;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        Curve boundary = tr.GetObject(boundaryId, OpenMode.ForRead, false) as Curve;
                        if (boundary == null)
                        {
                            result.Message = "???????????";
                            return result;
                        }

                        if (!IsClosedCurve(boundary))
                        {
                            result.Message = "???????????????????????/?????";
                            return result;
                        }

                        handleText = boundary.Handle.ToString();
                        layerName = boundary.Layer;
                        result.BoundaryLayerName = layerName;
                        tr.Commit();
                    }
                }

                if (string.IsNullOrWhiteSpace(handleText))
                {
                    result.Message = "???????????????? CASS surfacearea?";
                    return result;
                }

                result.Success = true;
                return result;
            }
            catch (System.Exception ex)
            {
                result.Message = "?????????" + ex.Message;
                return result;
            }
        }

        private static string EscapeLispString(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void AttachCassSessionEvents(CassCommandSession session)
        {
            if (session == null || session.Doc == null) return;

            try { session.Doc.Database.ObjectAppended += CassDatabase_ObjectAppended; } catch { }
            try { session.Doc.CommandEnded += CassDocument_CommandEnded; } catch { }
            try { session.Doc.CommandCancelled += CassDocument_CommandCancelled; } catch { }
            try { session.Doc.CommandFailed += CassDocument_CommandFailed; } catch { }
            try { session.Doc.LispEnded += CassDocument_LispEnded; } catch { }
        }

        private static void DetachCassSessionEvents(CassCommandSession session)
        {
            if (session == null || session.Doc == null) return;

            try { session.Doc.Database.ObjectAppended -= CassDatabase_ObjectAppended; } catch { }
            try { session.Doc.CommandEnded -= CassDocument_CommandEnded; } catch { }
            try { session.Doc.CommandCancelled -= CassDocument_CommandCancelled; } catch { }
            try { session.Doc.CommandFailed -= CassDocument_CommandFailed; } catch { }
            try { session.Doc.LispEnded -= CassDocument_LispEnded; } catch { }
        }

        private static void CassDatabase_ObjectAppended(object sender, ObjectEventArgs e)
        {
            CassCommandSession session = _pendingCassSession;
            if (session == null || e == null || e.DBObject == null) return;

            Entity entity = e.DBObject as Entity;
            if (entity == null) return;

            try
            {
                ObjectId id = entity.ObjectId;
                if (!id.IsNull && !session.AppendedObjectIds.Contains(id))
                {
                    session.AppendedObjectIds.Add(id);
                }
            }
            catch
            {
            }
        }

        private static void CassDocument_CommandEnded(object sender, CommandEventArgs e)
        {
            CassCommandSession session = _pendingCassSession;
            if (session == null || e == null) return;

            if (IsSurfaceAreaCommand(e.GlobalCommandName))
            {
                session.SurfaceAreaCommandSeen = true;
                ScheduleCassFinish(session);
            }
        }

        private static void CassDocument_CommandCancelled(object sender, CommandEventArgs e)
        {
            CassCommandSession session = _pendingCassSession;
            if (session == null || e == null) return;
            if (!IsSurfaceAreaCommand(e.GlobalCommandName)) return;

            CleanupCassSession(session, true);
            session.Doc.Editor.WriteHudMessage("\n[?????] CASS surfacearea ????\n");
        }

        private static void CassDocument_CommandFailed(object sender, CommandEventArgs e)
        {
            CassCommandSession session = _pendingCassSession;
            if (session == null || e == null) return;
            if (!IsSurfaceAreaCommand(e.GlobalCommandName)) return;

            CleanupCassSession(session, true);
            session.Doc.Editor.WriteHudMessage("\n[?????] CASS surfacearea ?????\n");
        }

        private static void CassDocument_LispEnded(object sender, EventArgs e)
        {
            CassCommandSession session = _pendingCassSession;
            if (session == null) return;

            // CASS surfacearea ??? (command ...) ? LISP ?????
            // ?? CASS/AutoCAD ???????? surfacearea ? CommandEnded?
            // ? LISP ??? CASS ?????????????????????
            if (!session.FinishScheduled)
            {
                ScheduleCassFinish(session);
            }
        }

        private static bool IsSurfaceAreaCommand(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName)) return false;
            return string.Equals(commandName.Trim(), CassSurfaceAreaCommandName, StringComparison.OrdinalIgnoreCase)
                || commandName.IndexOf(CassSurfaceAreaCommandName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ScheduleCassFinish(CassCommandSession session)
        {
            if (session == null || session.Doc == null || session.FinishScheduled) return;
            session.FinishScheduled = true;

            // ???????????????????? CASS ??????
            DetachCassSessionEvents(session);

            // ???? SendStringToExecute ???? TCBMJ_CASS_FINISH?
            // ??? AutoCAD/CASS ????? LispEnded/CommandEnded ??????????????
            // ???????? TCSURF ?????????????????????????????
            // ??? WinForms Idle ???????????? CASS ????????????????
            QueueCassFinishOnIdle();
        }

        private static void QueueCassFinishOnIdle()
        {
            if (_cassFinishIdleHandlerAttached) return;

            _cassFinishIdleHandlerAttached = true;
            try
            {
                System.Windows.Forms.Application.Idle += CassFinishOnIdle;
            }
            catch
            {
                _cassFinishIdleHandlerAttached = false;

                CassCommandSession session = _pendingCassSession;
                if (session != null && session.Doc != null)
                {
                    try
                    {
                        // ????? Idle ????????????????
                        session.Doc.SendStringToExecute(CassFinishCommandName + " ", true, false, false);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void CassFinishOnIdle(object sender, EventArgs e)
        {
            try
            {
                System.Windows.Forms.Application.Idle -= CassFinishOnIdle;
            }
            catch
            {
            }
            _cassFinishIdleHandlerAttached = false;

            CassCommandSession session = _pendingCassSession;
            if (session == null || session.Doc == null) return;

            try
            {
                // ?? AutoCAD ????????????????????? CASS ??????????? GetPoint?
                string activeCommand = session.Doc.CommandInProgress;
                if (!string.IsNullOrWhiteSpace(activeCommand))
                {
                    QueueCassFinishOnIdle();
                    return;
                }
            }
            catch
            {
            }

            FinishPendingCassCommandAnnotation();
        }

        public static void FinishPendingCassCommandAnnotation()
        {
            CassCommandSession session = _pendingCassSession;
            if (session == null || session.Doc == null)
            {
                Document current = Application.DocumentManager.MdiActiveDocument;
                if (current != null) current.Editor.WriteHudMessage("\n[?????] ?????? CASS ????????\n");
                return;
            }

            Document doc = session.Doc;
            Editor ed = doc.Editor;
            SurfaceAreaAnnotationOptions options = NormalizeOptions(session.Options);
            _pendingCassSession = null;
            DetachCassSessionEvents(session);

            try
            {
                MergeNewObjectsFromSnapshot(doc, session.BeforeObjectIds, session);
                ObjectId boundaryId = session.BoundaryObjectId.IsNull ? GetBoundaryObjectIdFromUsers5(doc.Database) : session.BoundaryObjectId;
                double cassArea;
                string message;

                // ???????CASS ?????????????/??? surface.log?
                // ??????????????? CASS ????????????/??????????????
                // ???? surface.log ????????????????????????
                string cassLogPath;
                string cassLogLine;
                DateTime minLogTime = session.StartTime.AddSeconds(-2);
                if (!TryReadCassSurfaceLog(doc, options.CassSurfaceLogPath, minLogTime, out cassArea, out cassLogPath, out cassLogLine, out message))
                {
                    int deletedOnFail = 0;
                    if (options.DeleteCassGeneratedObjects)
                    {
                        deletedOnFail = DeleteCassGeneratedObjects(doc, session.AppendedObjectIds, boundaryId);
                    }

                    SurfaceAreaAnnotationResult planCheck;
                    string planMessage;
                    if (!TryBuildBoundaryAreaCheck(doc, boundaryId, options, out planCheck, out planMessage))
                    {
                        ed.WriteHudMessage("\n[?????] CASS ?????????????? surface.log ???" + message);
                        if (deletedOnFail > 0) ed.WriteHudMessage(" ??? CASS ???? " + deletedOnFail + " ??");
                        ed.WriteHudMessage(" ???????????" + planMessage + "\n");
                        return;
                    }

                    ed.WriteHudMessage("\n[?????] CASS ???/????? surface.log????????????????" + message);
                    if (deletedOnFail > 0) ed.WriteHudMessage(" ??? CASS ???? " + deletedOnFail + " ??");
                    ed.WriteHudMessage("\n");

                    SurfaceAreaAnnotationResult fallbackResult = AnnotatePlanAreaFallback(
                        doc,
                        boundaryId,
                        planCheck,
                        options,
                        "CASS ???/????? surface.log?????????",
                        session.AppendedObjectIds.Count,
                        deletedOnFail);
                    ed.WriteHudMessage(fallbackResult.ToEditorMessage());
                    return;
                }

                int deleted = 0;
                if (options.DeleteCassGeneratedObjects)
                {
                    deleted = DeleteCassGeneratedObjects(doc, session.AppendedObjectIds, boundaryId);
                }

                SurfaceAreaAnnotationResult result = AnnotateCassCommandResult(doc, boundaryId, cassArea, cassLogPath, cassLogLine, session.AppendedObjectIds.Count, deleted, options);
                ed.WriteHudMessage(result.ToEditorMessage());
            }
            catch (System.Exception ex)
            {
                ed.WriteHudMessage("\n[?????] CASS ????????" + ex.Message + "\n");
            }
            finally
            {
                RestoreUsers5(session);
            }
        }

        private static SurfaceAreaAnnotationResult AnnotateCassCommandResult(Document doc, ObjectId boundaryId, double cassArea, string logPath, string logLine, int generatedCount, int deletedCount, SurfaceAreaAnnotationOptions options)
        {
            var result = new SurfaceAreaAnnotationResult();
            result.CalculationMode = SurfaceAreaCalculationMode.CassCommand;
            result.BoundaryObjectId = boundaryId;
            result.SurfaceArea = cassArea;
            result.CassSurfaceLogPath = logPath;
            result.CassSurfaceLogLine = logLine;
            result.CassGeneratedObjectCount = generatedCount;
            result.CassDeletedObjectCount = deletedCount;

            List<Point2d> boundary2d = new List<Point2d>();

            using (doc.LockDocument())
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Curve boundary = ObjectId.Null.Equals(boundaryId) ? null : tr.GetObject(boundaryId, OpenMode.ForRead, false) as Curve;
                    if (boundary != null)
                    {
                        result.BoundaryLayerName = boundary.Layer;
                        boundary2d = SampleBoundary(boundary, options.BoundaryInterval);
                        if (boundary2d.Count >= 3)
                        {
                            result.PlanArea = Math.Abs(PolygonArea(boundary2d));
                            result.BoundaryPointCount = boundary2d.Count;
                        }
                    }
                    tr.Commit();
                }
            }

            if (string.IsNullOrWhiteSpace(result.BoundaryLayerName)) result.BoundaryLayerName = "";

            PromptPointOptions ppoStart = new PromptPointOptions("\n?????????");
            PromptPointResult pprStart = doc.Editor.GetPoint(ppoStart);
            if (pprStart.Status != PromptStatus.OK)
            {
                result.Message = "??????????";
                return result;
            }

            string annotationLayer = ResolveAnnotationLayer(options, result.BoundaryLayerName);
            result.AnnotationLayerName = annotationLayer;
            result.AnnotationFontName = options.AnnotationFontName;
            string previewText = BuildAnnotationText(options, result);

            Point3d previewAnnotationPoint;
            if (!TryPromptAnnotationPointWithPreview(doc, pprStart.Value, previewText, options, out previewAnnotationPoint))
            {
                result.Message = "????????";
                return result;
            }

            result.AnnotationPoint = previewAnnotationPoint;

            using (doc.LockDocument())
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    string text = BuildAnnotationText(options, result);
                    AttachmentPoint attachment = GetTextAttachment(result.AnnotationPoint, pprStart.Value);
                    ObjectId textStyleId = GetExistingTextStyleId(db, tr, options.AnnotationFontName);
                    SurfaceTextLayoutMetrics metrics = BuildSurfaceTextLayoutMetrics(db, tr, text, options.TextHeight, textStyleId);
                    SurfacePreviewLayout layout = BuildSurfacePreviewLayout(text, options.TextHeight, result.AnnotationPoint, attachment, metrics);

                    result.AnnotationObjectId = DrawAnnotationDbTextCentered(db, tr, layout.TextPoint, text, options.TextHeight, annotationLayer, 7, textStyleId);

                    if (options.DrawLeader)
                    {
                        result.LeaderObjectId = DrawLeaderByUnderline(db, tr, pprStart.Value, layout.UnderlineStart, layout.UnderlineEnd, annotationLayer, attachment);
                    }

                    SimpleAnnotationObjectService.AttachSurfaceMetadata(db, tr,
                        result.BoundaryObjectId, result.AnnotationObjectId, result.LeaderObjectId);

                    tr.Commit();
                }
            }

            result.Success = true;
            result.Message = "CASS ???????????";
            return result;
        }

        private static bool TryBuildBoundaryAreaCheck(Document doc, ObjectId boundaryId, SurfaceAreaAnnotationOptions options, out SurfaceAreaAnnotationResult result, out string message)
        {
            result = new SurfaceAreaAnnotationResult();
            result.CalculationMode = SurfaceAreaCalculationMode.CassCommand;
            result.BoundaryObjectId = boundaryId;
            message = string.Empty;

            try
            {
                using (doc.LockDocument())
                {
                    Database db = doc.Database;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        Curve boundary = tr.GetObject(boundaryId, OpenMode.ForRead, false) as Curve;
                        if (boundary == null)
                        {
                            message = "???????????";
                            return false;
                        }

                        if (!IsClosedCurve(boundary))
                        {
                            message = "???????????????????????/?????";
                            return false;
                        }

                        result.BoundaryLayerName = boundary.Layer;
                        List<Point2d> boundary2d = SampleBoundary(boundary, options.BoundaryInterval);
                        if (boundary2d.Count < 3)
                        {
                            message = "??????????????????";
                            return false;
                        }

                        result.PlanArea = Math.Abs(PolygonArea(boundary2d));
                        result.SurfaceArea = result.PlanArea;
                        result.BoundaryPointCount = boundary2d.Count;
                        List<ElevationPoint> elevations = CollectElevationPoints(db, tr, boundaryId, boundary2d);
                        result.ElevationPointCount = elevations == null ? 0 : elevations.Count;
                        result.AnnotationPoint = GetSimpleAnnotationPoint(boundary2d);
                        result.Success = true;

                        tr.Commit();
                    }
                }

                return true;
            }
            catch (System.Exception ex)
            {
                message = "?????????????" + ex.Message;
                return false;
            }
        }

        private static SurfaceAreaAnnotationResult AnnotatePlanAreaFallback(Document doc, ObjectId boundaryId, SurfaceAreaAnnotationResult areaCheck, SurfaceAreaAnnotationOptions options)
        {
            return AnnotatePlanAreaFallback(doc, boundaryId, areaCheck, options, "??????????????", 0, 0);
        }

        private static SurfaceAreaAnnotationResult AnnotatePlanAreaFallback(Document doc, ObjectId boundaryId, SurfaceAreaAnnotationResult areaCheck, SurfaceAreaAnnotationOptions options, string fallbackMessage, int cassGeneratedCount, int cassDeletedCount)
        {
            var result = areaCheck ?? new SurfaceAreaAnnotationResult();
            result.Success = false;
            result.AsyncStarted = false;
            result.PlanAreaFallback = true;
            result.CalculationMode = SurfaceAreaCalculationMode.CassCommand;
            result.BoundaryObjectId = boundaryId;
            result.SurfaceArea = result.PlanArea;
            result.CassSurfaceLogPath = string.Empty;
            result.CassSurfaceLogLine = string.Empty;
            result.CassGeneratedObjectCount = cassGeneratedCount;
            result.CassDeletedObjectCount = cassDeletedCount;

            if (string.IsNullOrWhiteSpace(fallbackMessage))
            {
                fallbackMessage = "????????";
            }

            PromptPointOptions ppoStart = new PromptPointOptions("\n?????????");
            PromptPointResult pprStart = doc.Editor.GetPoint(ppoStart);
            if (pprStart.Status != PromptStatus.OK)
            {
                result.Message = fallbackMessage + "???????????";
                return result;
            }

            string annotationLayer = ResolveAnnotationLayer(options, result.BoundaryLayerName);
            result.AnnotationLayerName = annotationLayer;
            result.AnnotationFontName = options.AnnotationFontName;
            string previewText = BuildAnnotationText(options, result);

            Point3d previewAnnotationPoint;
            if (!TryPromptAnnotationPointWithPreview(doc, pprStart.Value, previewText, options, out previewAnnotationPoint))
            {
                result.Message = fallbackMessage + "?????????";
                return result;
            }

            result.AnnotationPoint = previewAnnotationPoint;

            using (doc.LockDocument())
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    string text = BuildAnnotationText(options, result);
                    AttachmentPoint attachment = GetTextAttachment(result.AnnotationPoint, pprStart.Value);
                    ObjectId textStyleId = GetExistingTextStyleId(db, tr, options.AnnotationFontName);
                    SurfaceTextLayoutMetrics metrics = BuildSurfaceTextLayoutMetrics(db, tr, text, options.TextHeight, textStyleId);
                    SurfacePreviewLayout layout = BuildSurfacePreviewLayout(text, options.TextHeight, result.AnnotationPoint, attachment, metrics);

                    result.AnnotationObjectId = DrawAnnotationDbTextCentered(db, tr, layout.TextPoint, text, options.TextHeight, annotationLayer, 7, textStyleId);

                    if (options.DrawLeader)
                    {
                        result.LeaderObjectId = DrawLeaderByUnderline(db, tr, pprStart.Value, layout.UnderlineStart, layout.UnderlineEnd, annotationLayer, attachment);
                    }

                    SimpleAnnotationObjectService.AttachSurfaceMetadata(db, tr,
                        result.BoundaryObjectId, result.AnnotationObjectId, result.LeaderObjectId);

                    tr.Commit();
                }
            }

            result.Success = true;
            result.Message = fallbackMessage;
            return result;
        }

        private static ObjectId GetBoundaryObjectIdFromUsers5(Database db)
        {
            if (db == null) return ObjectId.Null;

            try
            {
                object value = Application.GetSystemVariable("USERS5");
                string handleText = value == null ? string.Empty : value.ToString();
                if (string.IsNullOrWhiteSpace(handleText)) return ObjectId.Null;

                long raw;
                if (!long.TryParse(handleText.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out raw)) return ObjectId.Null;

                return db.GetObjectId(false, new Handle(raw), 0);
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        private static bool TryReadCassAreaFromGeneratedText(Document doc, List<ObjectId> objectIds, out double area, out string sourceLine, out int areaTextCount, out string message)
        {
            area = 0.0;
            sourceLine = string.Empty;
            areaTextCount = 0;
            message = string.Empty;

            if (doc == null)
            {
                message = "???????";
                return false;
            }

            if (objectIds == null || objectIds.Count == 0)
            {
                message = "????? CASS ?????";
                return false;
            }

            var values = new List<double>();

            using (doc.LockDocument())
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in objectIds.Distinct())
                    {
                        if (id.IsNull) continue;

                        DBObject obj = null;
                        try
                        {
                            obj = tr.GetObject(id, OpenMode.ForRead, false);
                        }
                        catch
                        {
                            continue;
                        }

                        if (obj == null || obj.IsErased) continue;

                        string text = null;
                        DBText dbText = obj as DBText;
                        if (dbText != null)
                        {
                            text = dbText.TextString;
                        }
                        else
                        {
                            MText mText = obj as MText;
                            if (mText != null) text = mText.Contents;
                        }

                        double value;
                        if (TryParseCassGeneratedAreaText(text, out value))
                        {
                            values.Add(value);
                        }
                    }

                    tr.Commit();
                }
            }

            if (values.Count == 0)
            {
                message = "?? CASS ????????????????????";
                return false;
            }

            double sum = 0.0;
            foreach (double value in values) sum += value;

            if (sum <= Eps)
            {
                message = "?????????????????";
                return false;
            }

            area = sum;
            areaTextCount = values.Count;
            sourceLine = "CASS ?????????????" + sum.ToString("0.###", CultureInfo.InvariantCulture) + "?" + values.Count + " ??????";
            return true;
        }

        private static bool TryParseCassGeneratedAreaText(string rawText, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(rawText)) return false;

            string text = StripMTextCodes(rawText)
                .Replace("?", "")
                .Replace("???", "")
                .Replace("??", "")
                .Replace("?", "")
                .Trim();

            if (string.IsNullOrWhiteSpace(text)) return false;

            // CASS ?????????????? 6.009????????????????
            // ???????????????????????????????????????????
            Match exact = Regex.Match(text, @"^\s*([+-]?\d+(?:[\.,]\d+)?)\s*$");
            if (!exact.Success) return false;

            string number = exact.Groups[1].Value;
            if (number.IndexOf('.') < 0 && number.IndexOf(',') < 0)
            {
                return false;
            }

            if (!TryParseFlexibleDouble(number, out value)) return false;
            if (value <= 0) return false;

            // ?????????????????????????????????
            if (value > 1000000000) return false;
            return true;
        }

        private static int DeleteCassGeneratedObjects(Document doc, List<ObjectId> objectIds, ObjectId boundaryId)
        {
            if (doc == null || objectIds == null || objectIds.Count == 0) return 0;

            int deleted = 0;
            using (doc.LockDocument())
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in objectIds.Distinct())
                    {
                        if (id.IsNull || id == boundaryId) continue;

                        try
                        {
                            Entity ent = tr.GetObject(id, OpenMode.ForWrite, false) as Entity;
                            if (ent == null || ent.IsErased) continue;
                            ent.Erase();
                            deleted++;
                        }
                        catch
                        {
                        }
                    }
                    tr.Commit();
                }
            }
            return deleted;
        }

        private static void CleanupCassSession(CassCommandSession session, bool restoreUsers5)
        {
            if (session == null) return;
            DetachCassSessionEvents(session);
            if (object.ReferenceEquals(_pendingCassSession, session)) _pendingCassSession = null;
            if (restoreUsers5) RestoreUsers5(session);
        }

        private static void RestoreUsers5(CassCommandSession session)
        {
            if (session == null || session.Users5Restored) return;
            session.Users5Restored = true;
            try { Application.SetSystemVariable("USERS5", session.PreviousUsers5 ?? string.Empty); } catch { }
        }

        public static SurfaceAreaAnnotationResult CalculateAndAnnotate(Document doc, ObjectId boundaryId, SurfaceAreaAnnotationOptions options)
        {
            return CalculateAndAnnotate(doc, boundaryId, Point3d.Origin, Point3d.Origin, false, options);
        }

        public static SurfaceAreaAnnotationResult CalculateAndAnnotate(Document doc, ObjectId boundaryId, Point3d annotationPoint, bool useUserAnnotationPoint, SurfaceAreaAnnotationOptions options)
        {
            return CalculateAndAnnotate(doc, boundaryId, Point3d.Origin, annotationPoint, useUserAnnotationPoint, options);
        }

        public static SurfaceAreaAnnotationResult CalculateAndAnnotate(Document doc, ObjectId boundaryId, Point3d leaderStartPoint, Point3d annotationPoint, bool useUserAnnotationPoint, SurfaceAreaAnnotationOptions options)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            options = NormalizeOptions(options);

            var result = new SurfaceAreaAnnotationResult();
            result.BoundaryObjectId = boundaryId;

            using (doc.LockDocument())
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Curve boundary = tr.GetObject(boundaryId, OpenMode.ForRead, false) as Curve;
                    if (boundary == null)
                    {
                        result.Message = "???????????";
                        return result;
                    }

                    if (!IsClosedCurve(boundary))
                    {
                        result.Message = "???????????????????????/?????";
                        return result;
                    }

                    result.BoundaryLayerName = boundary.Layer;

                    List<Point2d> boundary2d = SampleBoundary(boundary, options.BoundaryInterval);
                    if (boundary2d.Count < 3)
                    {
                        result.Message = "??????????????????";
                        return result;
                    }

                    result.PlanArea = Math.Abs(PolygonArea(boundary2d));
                    result.BoundaryPointCount = boundary2d.Count;

                    result.CalculationMode = options.CalculationMode;

                    if (options.CalculationMode == SurfaceAreaCalculationMode.PlanArea)
                    {
                        result.SurfaceArea = result.PlanArea;
                        result.ElevationPointCount = 0;
                        result.TriangleCount = 0;
                        result.AnnotationPoint = useUserAnnotationPoint
                            ? annotationPoint
                            : GetSimpleAnnotationPoint(boundary2d);
                    }
                    else if (options.CalculationMode == SurfaceAreaCalculationMode.CassSurfaceLog)
                    {
                        double cassArea;
                        string cassLogPath;
                        string cassLogLine;
                        string cassMessage;
                        if (!TryReadCassSurfaceLog(doc, options.CassSurfaceLogPath, out cassArea, out cassLogPath, out cassLogLine, out cassMessage))
                        {
                            result.Message = cassMessage;
                            return result;
                        }

                        result.SurfaceArea = cassArea;
                        result.CassSurfaceLogPath = cassLogPath;
                        result.CassSurfaceLogLine = cassLogLine;
                        result.ElevationPointCount = 0;
                        result.TriangleCount = 0;
                        result.AnnotationPoint = useUserAnnotationPoint
                            ? annotationPoint
                            : GetSimpleAnnotationPoint(boundary2d);
                    }
                    else
                    {
                        List<ElevationPoint> elevations = CollectElevationPoints(db, tr, boundaryId, boundary2d);
                        result.ElevationPointCount = elevations.Count;
                        if (elevations.Count < 3)
                        {
                            result.Message = "?????????????? 3 ???????? DBPoint?? Z ????????????";
                            return result;
                        }

                        List<Triangle> terrainTriangles = BuildTerrainTriangles(elevations);
                        List<TinPoint> tinPoints = BuildTinPoints(boundary2d, elevations, terrainTriangles);
                        if (tinPoints.Count < 3)
                        {
                            result.Message = "?????????????????";
                            return result;
                        }

                        List<Triangle> triangles = BuildDelaunay(tinPoints);
                        double surfaceArea = 0.0;
                        int usedTriangleCount = 0;
                        foreach (Triangle triangle in triangles)
                        {
                            if (!IsTriangleInsideBoundary(triangle, boundary2d)) continue;
                            double area = TriangleArea3d(triangle.A.Point, triangle.B.Point, triangle.C.Point);
                            if (area <= Eps) continue;
                            surfaceArea += area;
                            usedTriangleCount++;
                        }

                        if (surfaceArea <= Eps || usedTriangleCount == 0)
                        {
                            result.Message = "?????????????????";
                            return result;
                        }

                        result.SurfaceArea = surfaceArea;
                        result.TriangleCount = usedTriangleCount;
                        result.AnnotationPoint = useUserAnnotationPoint
                            ? annotationPoint
                            : GetAnnotationPoint(boundary2d, elevations, terrainTriangles);
                    }

                    string annotationLayer = ResolveAnnotationLayer(options, result.BoundaryLayerName);
                    result.AnnotationLayerName = annotationLayer;
                    result.AnnotationFontName = options.AnnotationFontName;

                    string text = BuildAnnotationText(options, result);
                    AttachmentPoint attachment = useUserAnnotationPoint
                        ? GetTextAttachment(result.AnnotationPoint, leaderStartPoint)
                        : GetTextAttachment(result.AnnotationPoint, boundary2d);
                    result.AnnotationObjectId = DrawAnnotationDbText(db, tr, result.AnnotationPoint, text, options.TextHeight, annotationLayer, 1, attachment, options.AnnotationFontName);

                    if (options.DrawLeader && useUserAnnotationPoint)
                    {
                        result.LeaderObjectId = DrawLeader(db, tr, leaderStartPoint, result.AnnotationObjectId, options.TextHeight, annotationLayer, attachment);
                    }

                    SimpleAnnotationObjectService.AttachSurfaceMetadata(db, tr,
                        result.BoundaryObjectId, result.AnnotationObjectId, result.LeaderObjectId);

                    tr.Commit();
                }
            }

            result.Success = true;
            result.Message = options.CalculationMode == SurfaceAreaCalculationMode.PlanArea
                ? "??????????"
                : "???????????";
            return result;
        }

        private static SurfaceAreaAnnotationOptions NormalizeOptions(SurfaceAreaAnnotationOptions options)
        {
            options = options ?? SurfaceAreaAnnotationOptions.Default;
            if (options.BoundaryInterval <= 0) options.BoundaryInterval = 5.0;
            if (options.TextHeight <= 0) options.TextHeight = 1.0;
            if (options.DecimalPlaces < 0) options.DecimalPlaces = 0;
            if (options.DecimalPlaces > 6) options.DecimalPlaces = 6;
            if (string.IsNullOrWhiteSpace(options.AnnotationTemplate))
            {
                options.AnnotationTemplate = SurfaceAreaAnnotationOptions.Default.AnnotationTemplate;
            }
            // ???? TIN / surface.log ?????????
            if (options.CalculationMode != SurfaceAreaCalculationMode.CassCommand
                && options.CalculationMode != SurfaceAreaCalculationMode.PlanArea)
            {
                options.CalculationMode = SurfaceAreaCalculationMode.CassCommand;
            }
            if (options.CassSurfaceLogPath == null)
            {
                options.CassSurfaceLogPath = string.Empty;
            }
            if (string.IsNullOrWhiteSpace(options.AnnotationFontName))
            {
                options.AnnotationFontName = SurfaceAreaAnnotationOptions.Default.AnnotationFontName;
            }
            if (string.IsNullOrWhiteSpace(options.AnnotationLayerName))
            {
                options.AnnotationLayerName = SurfaceAreaAnnotationOptions.Default.AnnotationLayerName;
            }
            if (string.IsNullOrWhiteSpace(options.SelectedLayerName))
            {
                options.SelectedLayerName = options.AnnotationLayerName;
            }
            return options;
        }

        private static bool IsClosedCurve(Curve curve)
        {
            try
            {
                if (curve.Closed) return true;
            }
            catch
            {
                // ?????????????????????
            }

            try
            {
                return curve.StartPoint.DistanceTo(curve.EndPoint) < 1e-6;
            }
            catch
            {
                return false;
            }
        }

        private static List<Point2d> SampleBoundary(Curve curve, double interval)
        {
            var result = new List<Point2d>();
            double length = GetCurveLength(curve);
            if (length <= Eps) return result;

            var distances = new List<double>();
            distances.Add(0.0);
            distances.Add(length);

            // ?????????????
            double d = interval;
            while (d < length - DuplicateTolerance)
            {
                distances.Add(d);
                d += interval;
            }

            // ?????????????????
            // ??????????????/???????????????????????
            // CASS ??????????????????????????? CASS ???
            Polyline pl = curve as Polyline;
            if (pl != null)
            {
                for (int i = 0; i < pl.NumberOfVertices; i++)
                {
                    try
                    {
                        distances.Add(pl.GetDistanceAtParameter(i));
                    }
                    catch
                    {
                        // ?????????????????
                    }
                }
            }

            distances = distances
                .Where(x => x >= -DuplicateTolerance && x <= length + DuplicateTolerance)
                .Select(x => Math.Max(0.0, Math.Min(length, x)))
                .OrderBy(x => x)
                .ToList();

            double last = double.NaN;
            foreach (double dist in distances)
            {
                if (!double.IsNaN(last) && Math.Abs(dist - last) < DuplicateTolerance) continue;
                last = dist;

                Point3d p = curve.GetPointAtDist(dist);
                AddPoint2dUnique(result, new Point2d(p.X, p.Y));
            }

            if (result.Count >= 2 && result[0].GetDistanceTo(result[result.Count - 1]) < DuplicateTolerance)
            {
                result.RemoveAt(result.Count - 1);
            }
            return result;
        }

        private static double GetCurveLength(Curve curve)
        {
            try
            {
                return curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam);
            }
            catch
            {
                try
                {
                    return curve.StartPoint.DistanceTo(curve.EndPoint);
                }
                catch
                {
                    return 0;
                }
            }
        }

        private static void AddPoint2dUnique(List<Point2d> points, Point2d p)
        {
            if (points.Count == 0)
            {
                points.Add(p);
                return;
            }

            if (points[points.Count - 1].GetDistanceTo(p) > DuplicateTolerance)
            {
                points.Add(p);
            }
        }

        private static List<ElevationPoint> CollectElevationPoints(Database db, Transaction tr, ObjectId boundaryId, List<Point2d> boundary)
        {
            var output = new List<ElevationPoint>();
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
            foreach (ObjectId id in btr)
            {
                if (id == boundaryId) continue;

                Entity ent = null;
                try
                {
                    ent = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                }
                catch
                {
                    continue;
                }

                if (ent == null || ent.IsErased) continue;

                DBPoint point = ent as DBPoint;
                if (point != null)
                {
                    TryAddElevation(output, boundary, point.Position, point.Position.Z);
                    continue;
                }

                DBText dbText = ent as DBText;
                if (dbText != null)
                {
                    double z;
                    if (TryParseElevationText(dbText.TextString, out z))
                    {
                        TryAddElevation(output, boundary, dbText.Position, z);
                    }
                    continue;
                }

                MText mText = ent as MText;
                if (mText != null)
                {
                    double z;
                    if (TryParseElevationText(mText.Contents, out z))
                    {
                        TryAddElevation(output, boundary, mText.Location, z);
                    }
                    continue;
                }

                BlockReference block = ent as BlockReference;
                if (block != null)
                {
                    bool addedFromAttribute = false;
                    try
                    {
                        foreach (ObjectId attId in block.AttributeCollection)
                        {
                            AttributeReference att = tr.GetObject(attId, OpenMode.ForRead, false) as AttributeReference;
                            if (att == null) continue;

                            double z;
                            if (TryParseElevationText(att.TextString, out z))
                            {
                                TryAddElevation(output, boundary, block.Position, z);
                                addedFromAttribute = true;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // ????????????????? Z ??
                    }

                    if (!addedFromAttribute && Math.Abs(block.Position.Z) > Eps)
                    {
                        TryAddElevation(output, boundary, block.Position, block.Position.Z);
                    }
                }
            }
            return output;
        }

        private static void TryAddElevation(List<ElevationPoint> output, List<Point2d> boundary, Point3d xy, double z)
        {
            var p2 = new Point2d(xy.X, xy.Y);
            if (!IsPointInPolygonInclusive(p2, boundary)) return;

            for (int i = 0; i < output.Count; i++)
            {
                if (output[i].XY.GetDistanceTo(p2) < DuplicateTolerance) return;
            }

            output.Add(new ElevationPoint(p2, z));
        }

        private static bool TryParseElevationText(string text, out double z)
        {
            z = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string value = text.Replace("\\P", " ").Replace("\r", " ").Replace("\n", " ").Trim();
            value = Regex.Replace(value, "[{}]", string.Empty);
            value = value.Replace("?", ".");

            Match match = Regex.Match(value, @"^\s*([+-]?\d+(?:\.\d+)?)\s*(?:m|M|?)?\s*$");
            if (!match.Success) return false;

            string number = match.Groups[1].Value;
            return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out z)
                || double.TryParse(number, NumberStyles.Float, CultureInfo.CurrentCulture, out z);
        }

        private static List<TinPoint> BuildTinPoints(List<Point2d> boundary, List<ElevationPoint> elevations, List<Triangle> terrainTriangles)
        {
            var points = new List<TinPoint>();
            int id = 0;

            foreach (Point2d b in boundary)
            {
                double z = InterpolateZ(b, elevations, terrainTriangles);
                AddTinPoint(points, new Point3d(b.X, b.Y, z), id++);
            }

            foreach (ElevationPoint e in elevations)
            {
                AddTinPoint(points, new Point3d(e.XY.X, e.XY.Y, e.Z), id++);
            }

            for (int i = 0; i < points.Count; i++) points[i].Id = i;
            return points;
        }

        private static List<Triangle> BuildTerrainTriangles(List<ElevationPoint> elevations)
        {
            var source = new List<TinPoint>();
            for (int i = 0; i < elevations.Count; i++)
            {
                ElevationPoint e = elevations[i];
                AddTinPoint(source, new Point3d(e.XY.X, e.XY.Y, e.Z), i);
            }

            for (int i = 0; i < source.Count; i++) source[i].Id = i;
            return BuildDelaunay(source);
        }

        private static void AddTinPoint(List<TinPoint> points, Point3d point, int id)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Point3d old = points[i].Point;
                double dx = old.X - point.X;
                double dy = old.Y - point.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < DuplicateTolerance) return;
            }
            points.Add(new TinPoint(id, point));
        }

        private static double InterpolateZ(Point2d p, List<ElevationPoint> elevations, List<Triangle> terrainTriangles)
        {
            double tinZ;
            if (TryInterpolateZByTerrainTin(p, terrainTriangles, out tinZ))
            {
                return tinZ;
            }

            return InterpolateZByIdw(p, elevations);
        }

        private static bool TryInterpolateZByTerrainTin(Point2d p, List<Triangle> terrainTriangles, out double z)
        {
            z = 0.0;
            if (terrainTriangles == null || terrainTriangles.Count == 0) return false;

            foreach (Triangle triangle in terrainTriangles)
            {
                double w1, w2, w3;
                if (!TryGetBarycentricWeights(p, ToPoint2d(triangle.A.Point), ToPoint2d(triangle.B.Point), ToPoint2d(triangle.C.Point), out w1, out w2, out w3))
                {
                    continue;
                }

                if (w1 >= -1e-8 && w2 >= -1e-8 && w3 >= -1e-8)
                {
                    z = triangle.A.Point.Z * w1 + triangle.B.Point.Z * w2 + triangle.C.Point.Z * w3;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetBarycentricWeights(Point2d p, Point2d a, Point2d b, Point2d c, out double w1, out double w2, out double w3)
        {
            w1 = 0.0;
            w2 = 0.0;
            w3 = 0.0;

            double det = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(det) < Eps) return false;

            w1 = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / det;
            w2 = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / det;
            w3 = 1.0 - w1 - w2;
            return true;
        }

        private static double InterpolateZByIdw(Point2d p, List<ElevationPoint> elevations)
        {
            var nearest = elevations
                .Select(delegate (ElevationPoint e)
                {
                    double dx = e.XY.X - p.X;
                    double dy = e.XY.Y - p.Y;
                    return new { Point = e, D2 = dx * dx + dy * dy };
                })
                .OrderBy(x => x.D2)
                .Take(8)
                .ToList();

            if (nearest.Count == 0) return 0;
            if (nearest[0].D2 < 1e-8) return nearest[0].Point.Z;

            double weightSum = 0;
            double zSum = 0;
            foreach (var item in nearest)
            {
                double w = 1.0 / Math.Max(item.D2, 1e-8);
                weightSum += w;
                zSum += item.Point.Z * w;
            }

            return weightSum <= Eps ? nearest[0].Point.Z : zSum / weightSum;
        }

        private static string ResolveAnnotationLayer(SurfaceAreaAnnotationOptions options, string boundaryLayerName)
        {
            if (options.UseBoundaryLayerForAnnotation && !string.IsNullOrWhiteSpace(boundaryLayerName))
            {
                return boundaryLayerName;
            }

            string layerName;
            switch (options.LayerMode)
            {
                case AnnotationLayerMode.ExistingLayer:
                    layerName = options.SelectedLayerName;
                    break;
                case AnnotationLayerMode.CustomLayer:
                    layerName = options.AnnotationLayerName;
                    break;
                default:
                    layerName = "ZJ";
                    break;
            }

            if (string.IsNullOrWhiteSpace(layerName)) layerName = "ZJ";
            return layerName.Trim();
        }

        private static bool TryReadCassSurfaceLog(Document doc, string configuredPath, out double area, out string actualPath, out string resultLine, out string message)
        {
            return TryReadCassSurfaceLog(doc, configuredPath, DateTime.MinValue, out area, out actualPath, out resultLine, out message);
        }

        private static bool TryReadCassSurfaceLog(Document doc, string configuredPath, DateTime minWriteTime, out double area, out string actualPath, out string resultLine, out string message)
        {
            area = 0.0;
            actualPath = string.Empty;
            resultLine = string.Empty;
            message = string.Empty;

            List<string> candidates = GetCassSurfaceLogCandidates(doc, configuredPath);
            var existingCandidates = candidates
                .Where(File.Exists)
                .Select(delegate (string p)
                {
                    DateTime t;
                    try { t = File.GetLastWriteTime(p); }
                    catch { t = DateTime.MinValue; }
                    return new { Path = p, Time = t };
                })
                .OrderByDescending(x => x.Time)
                .ToList();

            string filePath = existingCandidates
                .Where(x => minWriteTime <= DateTime.MinValue || x.Time >= minWriteTime)
                .Select(x => x.Path)
                .FirstOrDefault();

            // CASS ??????????????????/????????????? surface.log?
            // ??????????????????????????
            if (string.IsNullOrWhiteSpace(filePath) && minWriteTime <= DateTime.MinValue)
            {
                filePath = existingCandidates.Select(x => x.Path).FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                message = "??? CASS ??? surface.log???? CASS ??????? ? ????? ? ?????????????????? surface.log ???";
                return false;
            }

            string text;
            try
            {
                text = File.ReadAllText(filePath, Encoding.Default);
            }
            catch (System.Exception ex)
            {
                message = "?? CASS surface.log ???" + ex.Message;
                return false;
            }

            if (!TryParseSurfaceAreaFromCassLog(text, out area, out resultLine))
            {
                message = "??? surface.log????????????" + filePath;
                return false;
            }

            actualPath = filePath;
            return true;
        }

        private static List<string> GetCassSurfaceLogCandidates(Document doc, string configuredPath)
        {
            var candidates = new List<string>();
            AddPathIfNotEmpty(candidates, configuredPath);

            try
            {
                if (doc != null && doc.Database != null && !string.IsNullOrWhiteSpace(doc.Database.Filename))
                {
                    string folder = Path.GetDirectoryName(doc.Database.Filename);
                    if (!string.IsNullOrWhiteSpace(folder))
                    {
                        AddPathIfNotEmpty(candidates, Path.Combine(folder, "surface.log"));
                    }
                }
            }
            catch { }

            try { AddPathIfNotEmpty(candidates, Path.Combine(Directory.GetCurrentDirectory(), "surface.log")); }
            catch { }

            try { AddPathIfNotEmpty(candidates, Path.Combine(Environment.CurrentDirectory, "surface.log")); }
            catch { }

            try
            {
                string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrWhiteSpace(myDocuments))
                {
                    AddPathIfNotEmpty(candidates, Path.Combine(myDocuments, "surface.log"));
                }
            }
            catch { }

            return candidates
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static void AddPathIfNotEmpty(List<string> paths, string path)
        {
            if (paths == null || string.IsNullOrWhiteSpace(path)) return;
            paths.Add(path.Trim().Trim('"'));
        }

        private static bool TryParseSurfaceAreaFromCassLog(string logText, out double area, out string resultLine)
        {
            area = 0.0;
            resultLine = string.Empty;
            if (string.IsNullOrWhiteSpace(logText)) return false;

            string[] lines = logText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            // ???????? = 741.520 ???????????
            Regex directTotal = new Regex(@"(?:?\s*?\s*?|surface\s*area)\s*[=?]\s*([+-]?\d+(?:[\.,]\d+)?)", RegexOptions.IgnoreCase);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i] ?? string.Empty;
                Match m = directTotal.Match(line);
                if (m.Success && TryParseFlexibleDouble(m.Groups[1].Value, out area))
                {
                    resultLine = line.Trim();
                    return true;
                }
            }

            // CASS11 ? surface.log ?????????????????: x.xxx??????????
            // ??????????????? 2 ?????????? = xxx ???????
            Regex triangleArea = new Regex(@"^\s*?\s*?\s*?\s*[:?]\s*([+-]?\d+(?:[\.,]\d+)?)\s*$", RegexOptions.IgnoreCase);
            double sum = 0.0;
            int count = 0;
            foreach (string rawLine in lines)
            {
                string line = rawLine ?? string.Empty;
                Match m = triangleArea.Match(line);
                if (!m.Success) continue;

                double value;
                if (TryParseFlexibleDouble(m.Groups[1].Value, out value) && value > 0)
                {
                    sum += value;
                    count++;
                }
            }

            if (count > 0 && sum > 0)
            {
                area = sum;
                resultLine = "surface.log ???????" + sum.ToString("0.###", CultureInfo.InvariantCulture) + "?" + count + " ????";
                return true;
            }

            Regex withUnit = new Regex(@"([+-]?\d+(?:[\.,]\d+)?)\s*(?:??\s*?|???|m2|?)", RegexOptions.IgnoreCase);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i] ?? string.Empty;
                if (line.IndexOf("??", StringComparison.CurrentCultureIgnoreCase) >= 0
                    || line.IndexOf("?", StringComparison.CurrentCultureIgnoreCase) >= 0
                    || line.IndexOf("m2", StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    Match m = withUnit.Match(line);
                    if (m.Success && TryParseFlexibleDouble(m.Groups[1].Value, out area))
                    {
                        resultLine = line.Trim();
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryParseFlexibleDouble(string text, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string normalized = text.Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static string BuildAnnotationText(SurfaceAreaAnnotationOptions options, SurfaceAreaAnnotationResult result)
        {
            string format = "0";
            if (options.DecimalPlaces > 0) format += "." + new string('0', options.DecimalPlaces);

            string surfaceAreaText = result.SurfaceArea.ToString(format, CultureInfo.InvariantCulture);
            string planAreaText = result.PlanArea.ToString(format, CultureInfo.InvariantCulture);

            string layerName = string.IsNullOrWhiteSpace(result.BoundaryLayerName) ? "" : result.BoundaryLayerName;

            string text = options.AnnotationTemplate
                .Replace("{???}", layerName)
                .Replace("{??}", layerName)
                .Replace("{LayerName}", layerName)
                .Replace("{???}", surfaceAreaText)
                .Replace("{SurfaceArea}", surfaceAreaText)
                .Replace("{????}", planAreaText)
                .Replace("{PlanArea}", planAreaText);

            // ?????????????????
            // ??????????????????????????????
            if (result != null && (result.PlanAreaFallback
                || result.CalculationMode == SurfaceAreaCalculationMode.PlanArea))
            {
                text = text.Replace("???", "??");
            }

            return text;
        }

        private static AttachmentPoint GetTextAttachment(Point3d annotationPoint, List<Point2d> boundary)
        {
            Point2d centroid = GetPolygonCentroid(boundary);
            return annotationPoint.X <= centroid.X ? AttachmentPoint.BottomLeft : AttachmentPoint.BottomRight;
        }

        private static AttachmentPoint GetTextAttachment(Point3d annotationPoint, Point3d leaderStartPoint)
        {
            // ???????????????????????????????
            // ???????????????????????????
            return leaderStartPoint.X <= annotationPoint.X ? AttachmentPoint.BottomLeft : AttachmentPoint.BottomRight;
        }

        private static bool TryPromptAnnotationPointWithPreview(Document doc, Point3d leaderStartPoint, string text, SurfaceAreaAnnotationOptions options, out Point3d annotationPoint)
        {
            annotationPoint = Point3d.Origin;
            if (doc == null || doc.Editor == null) return false;

            options = NormalizeOptions(options);
            Database db = doc.Database;
            ObjectId textStyleId = ObjectId.Null;
            SurfaceTextLayoutMetrics metrics = CreateEstimatedSurfaceTextLayoutMetrics(text, options.TextHeight);

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    textStyleId = GetExistingTextStyleId(db, tr, options.AnnotationFontName);
                    metrics = BuildSurfaceTextLayoutMetrics(db, tr, text, options.TextHeight, textStyleId);
                    tr.Commit();
                }
            }
            catch
            {
                metrics = CreateEstimatedSurfaceTextLayoutMetrics(text, options.TextHeight);
            }

            var jig = new SurfaceAreaAnnotationPreviewJig(db, leaderStartPoint, text, options.TextHeight, textStyleId, metrics, options.DrawLeader);
            PromptResult dragResult = doc.Editor.Drag(jig);
            if (dragResult.Status != PromptStatus.OK) return false;

            annotationPoint = jig.AnnotationPoint;
            return true;
        }

        private sealed class SurfaceAreaAnnotationPreviewJig : DrawJig
        {
            private readonly Database _database;
            private readonly Point3d _leaderStartPoint;
            private readonly string _text;
            private readonly double _textHeight;
            private readonly ObjectId _textStyleId;
            private readonly SurfaceTextLayoutMetrics _metrics;
            private readonly bool _drawLeader;
            private Point3d _annotationPoint;

            public SurfaceAreaAnnotationPreviewJig(Database database, Point3d leaderStartPoint, string text, double textHeight, ObjectId textStyleId, SurfaceTextLayoutMetrics metrics, bool drawLeader)
            {
                _database = database;
                _leaderStartPoint = leaderStartPoint;
                _text = string.IsNullOrWhiteSpace(text) ? "?????" : text;
                _textHeight = textHeight <= 0 ? 1.0 : textHeight;
                _textStyleId = textStyleId;
                _metrics = metrics ?? CreateEstimatedSurfaceTextLayoutMetrics(_text, _textHeight);
                _drawLeader = drawLeader;
                _annotationPoint = leaderStartPoint;
            }

            public Point3d AnnotationPoint
            {
                get { return _annotationPoint; }
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var options = new JigPromptPointOptions("\n????????? ESC ???");
                options.UseBasePoint = true;
                options.BasePoint = _leaderStartPoint;
                options.UserInputControls = UserInputControls.Accept3dCoordinates
                    | UserInputControls.NoZeroResponseAccepted;

                PromptPointResult result = prompts.AcquirePoint(options);
                if (result.Status != PromptStatus.OK) return SamplerStatus.Cancel;

                if (result.Value.DistanceTo(_annotationPoint) < DuplicateTolerance)
                {
                    return SamplerStatus.NoChange;
                }

                _annotationPoint = result.Value;
                return SamplerStatus.OK;
            }

            protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
            {
                if (draw == null || draw.Geometry == null) return true;

                AttachmentPoint attachment = GetTextAttachment(_annotationPoint, _leaderStartPoint);
                SurfacePreviewLayout layout = BuildSurfacePreviewLayout(_text, _textHeight, _annotationPoint, attachment, _metrics);

                DrawPreviewDbText(draw, _database, layout.TextPoint, _text, _textHeight, _textStyleId);

                if (_drawLeader)
                {
                    using (var polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline())
                    {
                        Point3d leaderJoin = IsRightAttachment(attachment) ? layout.UnderlineEnd : layout.UnderlineStart;
                        Point3d farEnd = IsRightAttachment(attachment) ? layout.UnderlineStart : layout.UnderlineEnd;

                        polyline.AddVertexAt(0, new Point2d(_leaderStartPoint.X, _leaderStartPoint.Y), 0, 0, 0);
                        polyline.AddVertexAt(1, new Point2d(leaderJoin.X, leaderJoin.Y), 0, 0, 0);
                        polyline.AddVertexAt(2, new Point2d(farEnd.X, farEnd.Y), 0, 0, 0);
                        polyline.ColorIndex = 7;
                        draw.Geometry.Draw(polyline);
                    }
                }

                return true;
            }
        }

        private sealed class SurfacePreviewLayout
        {
            public Point3d TextPoint { get; set; }
            public double TextWidth { get; set; }
            public double UnderlineWidth { get; set; }
            public Point3d UnderlineStart { get; set; }
            public Point3d UnderlineEnd { get; set; }
        }

        private sealed class SurfaceTextLayoutMetrics
        {
            public double TextWidth { get; set; }
        }

        private static SurfacePreviewLayout BuildSurfacePreviewLayout(string text, double textHeight, Point3d annotationPoint, AttachmentPoint attachment, SurfaceTextLayoutMetrics metrics)
        {
            if (textHeight <= 0) textHeight = 1.0;
            metrics = metrics ?? CreateEstimatedSurfaceTextLayoutMetrics(text, textHeight);

            double textWidth = Math.Max(metrics.TextWidth, EstimatePreviewTextWidth(text, textHeight));
            double sideMargin = Math.Max(textHeight * 0.12, 0.03);
            double lineWidth = textWidth + sideMargin * 2.0;
            if (lineWidth < DuplicateTolerance) lineWidth = Math.Max(textHeight * 4.0, 1.0);

            double lineGap = Math.Max(textHeight * 0.22, 0.05);
            double lineY = annotationPoint.Y - lineGap;
            double z = annotationPoint.Z;
            double lineStartX;
            double lineEndX;

            // annotationPoint ????????????????????????????
            if (IsRightAttachment(attachment))
            {
                lineStartX = annotationPoint.X - lineWidth;
                lineEndX = annotationPoint.X;
            }
            else
            {
                lineStartX = annotationPoint.X;
                lineEndX = annotationPoint.X + lineWidth;
            }

            double centerX = (lineStartX + lineEndX) / 2.0;
            var layout = new SurfacePreviewLayout();
            layout.TextWidth = textWidth;
            layout.UnderlineWidth = lineWidth;
            layout.UnderlineStart = new Point3d(lineStartX, lineY, z);
            layout.UnderlineEnd = new Point3d(lineEndX, lineY, z);
            layout.TextPoint = new Point3d(centerX, annotationPoint.Y, z);
            return layout;
        }

        private static void DrawPreviewDbText(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw, Database db, Point3d centerBaselinePoint, string text, double textHeight, ObjectId textStyleId)
        {
            if (draw == null || draw.Geometry == null || string.IsNullOrWhiteSpace(text)) return;
            if (textHeight <= 0) textHeight = 1.0;

            string normalized = NormalizeDbTextString(text);
            try
            {
                using (var dbText = new DBText())
                {
                    if (db != null)
                    {
                        try { dbText.SetDatabaseDefaults(db); } catch { }
                    }

                    dbText.HorizontalMode = TextHorizontalMode.TextCenter;
                    dbText.Position = centerBaselinePoint;
                    dbText.AlignmentPoint = centerBaselinePoint;
                    dbText.Height = textHeight;
                    dbText.TextString = normalized;
                    dbText.ColorIndex = 7;
                    if (!textStyleId.IsNull) dbText.TextStyleId = textStyleId;
                    try { if (db != null) dbText.AdjustAlignment(db); } catch { }
                    draw.Geometry.Draw(dbText);
                }
            }
            catch
            {
                try
                {
                    draw.Geometry.Text(centerBaselinePoint, Vector3d.ZAxis, Vector3d.XAxis, textHeight, 1.0, 0.0, normalized);
                }
                catch { }
            }
        }

        private static SurfaceTextLayoutMetrics BuildSurfaceTextLayoutMetrics(Database db, Transaction tr, string text, double textHeight, ObjectId textStyleId)
        {
            if (textHeight <= 0) textHeight = 1.0;

            SurfaceTextLayoutMetrics estimated = CreateEstimatedSurfaceTextLayoutMetrics(text, textHeight);
            var metrics = new SurfaceTextLayoutMetrics();
            metrics.TextWidth = Math.Max(estimated.TextWidth, MeasureDbTextWidth(db, tr, text, textHeight, textStyleId, estimated.TextWidth));
            return metrics;
        }

        private static SurfaceTextLayoutMetrics CreateEstimatedSurfaceTextLayoutMetrics(string text, double textHeight)
        {
            var metrics = new SurfaceTextLayoutMetrics();
            metrics.TextWidth = EstimatePreviewTextWidth(text, textHeight);
            return metrics;
        }

        private static double MeasureDbTextWidth(Database db, Transaction tr, string text, double textHeight, ObjectId textStyleId, double fallbackWidth)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(text)) return fallbackWidth;
            if (textHeight <= 0) textHeight = 1.0;

            DBText tempText = null;
            try
            {
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                tempText = new DBText();
                try { tempText.SetDatabaseDefaults(db); } catch { }
                tempText.Position = Point3d.Origin;
                tempText.Height = textHeight;
                tempText.TextString = NormalizeDbTextString(text);
                if (!textStyleId.IsNull) tempText.TextStyleId = textStyleId;
                tempText.HorizontalMode = TextHorizontalMode.TextLeft;

                btr.AppendEntity(tempText);
                tr.AddNewlyCreatedDBObject(tempText, true);
                try { tempText.AdjustAlignment(db); } catch { }

                Extents3d extents = tempText.GeometricExtents;
                double width = Math.Abs(extents.MaxPoint.X - extents.MinPoint.X);

                try { tempText.Erase(); } catch { }
                return width > DuplicateTolerance ? Math.Max(width, fallbackWidth) : fallbackWidth;
            }
            catch
            {
                try
                {
                    if (tempText != null && !tempText.IsErased) tempText.Erase();
                }
                catch { }
                return fallbackWidth;
            }
        }

        private static double EstimatePreviewTextWidth(string text, double textHeight)
        {
            if (textHeight <= 0) textHeight = 1.0;
            text = NormalizeDbTextString(text);
            if (string.IsNullOrEmpty(text)) return Math.Max(textHeight * 4.0, 1.0);

            double widthFactor = 0.0;
            foreach (char ch in text)
            {
                if (ch <= 127)
                {
                    if (char.IsWhiteSpace(ch)) widthFactor += 0.35;
                    else if (char.IsDigit(ch)) widthFactor += 0.68;
                    else if (char.IsLetter(ch)) widthFactor += 0.72;
                    else if (ch == '.' || ch == ',' || ch == ':' || ch == ';') widthFactor += 0.42;
                    else widthFactor += 0.58;
                }
                else
                {
                    widthFactor += 1.08;
                }
            }

            return Math.Max(widthFactor * textHeight, textHeight * 4.0);
        }

        private static ObjectId DrawAnnotationDbTextCentered(Database db, Transaction tr, Point3d centerBaselinePoint, string text, double height, string layerName, short colorIndex, ObjectId textStyleId)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(text)) return ObjectId.Null;

            CadLayerService.EnsureLayer(db, tr, layerName, colorIndex);
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            var dbText = new DBText();
            try { dbText.SetDatabaseDefaults(db); } catch { }
            // ???????? HorizontalMode???? AlignmentPoint?
            // ???? AutoCAD ???? AlignmentPoint ??? eNotApplicable?
            dbText.HorizontalMode = TextHorizontalMode.TextCenter;
            dbText.Position = centerBaselinePoint;
            dbText.AlignmentPoint = centerBaselinePoint;
            dbText.Height = height <= 0 ? 1.0 : height;
            dbText.TextString = NormalizeDbTextString(text);
            dbText.Layer = layerName;
            dbText.ColorIndex = colorIndex;
            if (!textStyleId.IsNull) dbText.TextStyleId = textStyleId;

            ObjectId id = btr.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);

            try { dbText.AdjustAlignment(db); } catch { }
            return id;
        }

        private static ObjectId DrawLeaderByUnderline(Database db, Transaction tr, Point3d leaderStartPoint, Point3d underlineStart, Point3d underlineEnd, string layerName, AttachmentPoint attachment)
        {
            Point3d leaderJoin = IsRightAttachment(attachment) ? underlineEnd : underlineStart;
            Point3d farEnd = IsRightAttachment(attachment) ? underlineStart : underlineEnd;

            var points = new List<Point3d>();
            points.Add(leaderStartPoint);
            if (leaderStartPoint.DistanceTo(leaderJoin) > DuplicateTolerance) points.Add(leaderJoin);
            if (leaderJoin.DistanceTo(farEnd) > DuplicateTolerance) points.Add(farEnd);
            if (points.Count < 2) return ObjectId.Null;

            return CadDrawService.DrawPolyline(db, tr, points, layerName, 7);
        }

        private static ObjectId DrawAnnotationDbText(Database db, Transaction tr, Point3d position, string text, double height, string layerName, short colorIndex, AttachmentPoint attachment, string textStyleName)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(text)) return ObjectId.Null;

            CadLayerService.EnsureLayer(db, tr, layerName, colorIndex);
            ObjectId textStyleId = GetExistingTextStyleId(db, tr, textStyleName);
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            var dbText = new DBText();
            dbText.Position = position;
            dbText.Height = height <= 0 ? 1.0 : height;
            dbText.TextString = NormalizeDbTextString(text);
            dbText.Layer = layerName;
            if (!textStyleId.IsNull) dbText.TextStyleId = textStyleId;

            // ?????? AutoCAD ??????(DBText)???????????(MText)?
            // ????????????????????????????????????????
            if (IsRightAttachment(attachment))
            {
                dbText.HorizontalMode = TextHorizontalMode.TextRight;
                dbText.AlignmentPoint = position;
            }
            else
            {
                dbText.HorizontalMode = TextHorizontalMode.TextLeft;
            }

            ObjectId id = btr.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);

            try
            {
                dbText.AdjustAlignment(db);
            }
            catch
            {
                // ???????????????????????????
            }

            return id;
        }

        private static ObjectId GetExistingTextStyleId(Database db, Transaction tr, string textStyleName)
        {
            if (db == null || tr == null || string.IsNullOrWhiteSpace(textStyleName)) return ObjectId.Null;

            string target = textStyleName.Trim();
            try
            {
                TextStyleTable tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                if (tst.Has(target)) return tst[target];

                foreach (ObjectId id in tst)
                {
                    TextStyleTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as TextStyleTableRecord;
                    if (record == null || record.IsErased) continue;
                    if (string.Equals(record.Name, target, StringComparison.CurrentCultureIgnoreCase)) return id;
                }
            }
            catch
            {
            }

            return ObjectId.Null;
        }

        private static string NormalizeDbTextString(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // DBText ??????????????????????? MText ??? \P ????
            string normalized = text.Replace("\\P", " ").Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
            return Regex.Replace(normalized, @"\s+", " ").Trim();
        }

        private static ObjectId DrawLeader(Database db, Transaction tr, Point3d leaderStartPoint, ObjectId textObjectId, double textHeight, string layerName, AttachmentPoint attachment)
        {
            Point3d underlineStart;
            Point3d underlineEnd;
            if (!TryGetTextUnderlinePoints(tr, textObjectId, textHeight, attachment, out underlineStart, out underlineEnd))
            {
                return ObjectId.Null;
            }

            Point3d leaderJoin = IsRightAttachment(attachment) ? underlineEnd : underlineStart;
            Point3d farEnd = IsRightAttachment(attachment) ? underlineStart : underlineEnd;

            var points = new List<Point3d>();
            points.Add(leaderStartPoint);

            if (leaderStartPoint.DistanceTo(leaderJoin) > DuplicateTolerance)
            {
                points.Add(leaderJoin);
            }

            if (leaderJoin.DistanceTo(farEnd) > DuplicateTolerance)
            {
                points.Add(farEnd);
            }

            if (points.Count < 2) return ObjectId.Null;
            return CadDrawService.DrawPolyline(db, tr, points, layerName, 7);
        }

        private static bool TryGetTextUnderlinePoints(Transaction tr, ObjectId textObjectId, double textHeight, AttachmentPoint attachment, out Point3d underlineStart, out Point3d underlineEnd)
        {
            underlineStart = Point3d.Origin;
            underlineEnd = Point3d.Origin;
            if (textHeight <= 0) textHeight = 1.0;

            Entity textEntity = null;
            try
            {
                textEntity = tr.GetObject(textObjectId, OpenMode.ForRead, false) as Entity;
            }
            catch
            {
                return false;
            }

            if (textEntity == null) return false;

            Extents3d extents;
            try
            {
                extents = textEntity.GeometricExtents;
            }
            catch
            {
                return false;
            }

            double minX = Math.Min(extents.MinPoint.X, extents.MaxPoint.X);
            double maxX = Math.Max(extents.MinPoint.X, extents.MaxPoint.X);
            double minY = Math.Min(extents.MinPoint.Y, extents.MaxPoint.Y);
            double z = extents.MinPoint.Z;

            // ??????????????????????????????????????????
            // ????? 0.22 ???????????????
            double underlineGap = Math.Max(textHeight * 0.22, 0.05);
            double y = minY - underlineGap;

            underlineStart = new Point3d(minX, y, z);
            underlineEnd = new Point3d(maxX, y, z);

            // ??????? CAD ??????????????????????????????
            if (underlineStart.DistanceTo(underlineEnd) < DuplicateTolerance)
            {
                double fallbackWidth = Math.Max(textHeight * 6.0, 1.0);
                if (IsRightAttachment(attachment))
                {
                    underlineStart = new Point3d(maxX - fallbackWidth, y, z);
                    underlineEnd = new Point3d(maxX, y, z);
                }
                else
                {
                    underlineStart = new Point3d(minX, y, z);
                    underlineEnd = new Point3d(minX + fallbackWidth, y, z);
                }
            }

            return true;
        }

        private static bool IsRightAttachment(AttachmentPoint attachment)
        {
            return attachment == AttachmentPoint.BottomRight
                || attachment == AttachmentPoint.MiddleRight
                || attachment == AttachmentPoint.TopRight;
        }

        private static string StripMTextCodes(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\\P", "\n").Replace("{", string.Empty).Replace("}", string.Empty);
        }

        private static Point3d GetSimpleAnnotationPoint(List<Point2d> boundary)
        {
            Point2d centroid = GetPolygonCentroid(boundary);
            if (IsPointInPolygonInclusive(centroid, boundary))
            {
                return new Point3d(centroid.X, centroid.Y, 0);
            }

            if (boundary != null && boundary.Count > 0)
            {
                Point2d fallback = boundary[0];
                return new Point3d(fallback.X, fallback.Y, 0);
            }

            return Point3d.Origin;
        }

        private static Point3d GetAnnotationPoint(List<Point2d> boundary, List<ElevationPoint> elevations, List<Triangle> terrainTriangles)
        {
            Point2d centroid = GetPolygonCentroid(boundary);
            if (IsPointInPolygonInclusive(centroid, boundary))
            {
                return new Point3d(centroid.X, centroid.Y, InterpolateZ(centroid, elevations, terrainTriangles));
            }

            if (elevations.Count > 0)
            {
                ElevationPoint first = elevations[0];
                return new Point3d(first.XY.X, first.XY.Y, first.Z);
            }

            Point2d fallback = boundary[0];
            return new Point3d(fallback.X, fallback.Y, 0);
        }

        private static Point2d GetPolygonCentroid(List<Point2d> polygon)
        {
            double signedArea = 0;
            double cx = 0;
            double cy = 0;

            for (int i = 0; i < polygon.Count; i++)
            {
                Point2d a = polygon[i];
                Point2d b = polygon[(i + 1) % polygon.Count];
                double cross = a.X * b.Y - b.X * a.Y;
                signedArea += cross;
                cx += (a.X + b.X) * cross;
                cy += (a.Y + b.Y) * cross;
            }

            signedArea *= 0.5;
            if (Math.Abs(signedArea) < Eps)
            {
                double ax = 0;
                double ay = 0;
                foreach (Point2d p in polygon)
                {
                    ax += p.X;
                    ay += p.Y;
                }
                return new Point2d(ax / polygon.Count, ay / polygon.Count);
            }

            cx /= 6.0 * signedArea;
            cy /= 6.0 * signedArea;
            return new Point2d(cx, cy);
        }

        private static double PolygonArea(List<Point2d> polygon)
        {
            double area = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                Point2d a = polygon[i];
                Point2d b = polygon[(i + 1) % polygon.Count];
                area += a.X * b.Y - b.X * a.Y;
            }
            return area * 0.5;
        }

        private static bool IsTriangleInsideBoundary(Triangle triangle, List<Point2d> boundary)
        {
            Point2d a = ToPoint2d(triangle.A.Point);
            Point2d b = ToPoint2d(triangle.B.Point);
            Point2d c = ToPoint2d(triangle.C.Point);
            Point2d centroid = new Point2d((a.X + b.X + c.X) / 3.0, (a.Y + b.Y + c.Y) / 3.0);
            Point2d ab = new Point2d((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
            Point2d bc = new Point2d((b.X + c.X) / 2.0, (b.Y + c.Y) / 2.0);
            Point2d ca = new Point2d((c.X + a.X) / 2.0, (c.Y + a.Y) / 2.0);

            return IsPointInPolygonInclusive(centroid, boundary)
                && IsPointInPolygonInclusive(ab, boundary)
                && IsPointInPolygonInclusive(bc, boundary)
                && IsPointInPolygonInclusive(ca, boundary);
        }

        private static Point2d ToPoint2d(Point3d p)
        {
            return new Point2d(p.X, p.Y);
        }

        private static bool IsPointInPolygonInclusive(Point2d point, List<Point2d> polygon)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                Point2d a = polygon[i];
                Point2d b = polygon[(i + 1) % polygon.Count];
                if (DistanceToSegment(point, a, b) < DuplicateTolerance) return true;
            }

            bool inside = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count; i++)
            {
                Point2d pi = polygon[i];
                Point2d pj = polygon[j];
                bool intersect = ((pi.Y > point.Y) != (pj.Y > point.Y))
                    && (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y + Eps) + pi.X);
                if (intersect) inside = !inside;
                j = i;
            }
            return inside;
        }

        private static double DistanceToSegment(Point2d p, Point2d a, Point2d b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            if (len2 <= Eps) return p.GetDistanceTo(a);

            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            var foot = new Point2d(a.X + t * dx, a.Y + t * dy);
            return p.GetDistanceTo(foot);
        }

        private static double TriangleArea3d(Point3d a, Point3d b, Point3d c)
        {
            Vector3d ab = a.GetVectorTo(b);
            Vector3d ac = a.GetVectorTo(c);
            return ab.CrossProduct(ac).Length * 0.5;
        }

        private static List<Triangle> BuildDelaunay(List<TinPoint> input)
        {
            var points = new List<TinPoint>(input);
            var triangles = new List<Triangle>();
            if (points.Count < 3) return triangles;

            double minX = points.Min(p => p.Point.X);
            double minY = points.Min(p => p.Point.Y);
            double maxX = points.Max(p => p.Point.X);
            double maxY = points.Max(p => p.Point.Y);
            double dx = maxX - minX;
            double dy = maxY - minY;
            double delta = Math.Max(dx, dy);
            if (delta <= Eps) return triangles;

            double midX = (minX + maxX) / 2.0;
            double midY = (minY + maxY) / 2.0;
            var s1 = new TinPoint(points.Count, new Point3d(midX - 20 * delta, midY - delta, 0));
            var s2 = new TinPoint(points.Count + 1, new Point3d(midX, midY + 20 * delta, 0));
            var s3 = new TinPoint(points.Count + 2, new Point3d(midX + 20 * delta, midY - delta, 0));

            triangles.Add(new Triangle(s1, s2, s3));

            foreach (TinPoint point in points)
            {
                var badTriangles = new List<Triangle>();
                foreach (Triangle triangle in triangles)
                {
                    if (triangle.IsPointInsideCircumcircle(point)) badTriangles.Add(triangle);
                }

                var edgeMap = new Dictionary<EdgeKey, EdgeRecord>();
                foreach (Triangle triangle in badTriangles)
                {
                    AddEdge(edgeMap, triangle.A, triangle.B);
                    AddEdge(edgeMap, triangle.B, triangle.C);
                    AddEdge(edgeMap, triangle.C, triangle.A);
                }

                foreach (Triangle bad in badTriangles) triangles.Remove(bad);

                foreach (EdgeRecord edge in edgeMap.Values)
                {
                    if (edge.Count != 1) continue;
                    Triangle candidate = new Triangle(edge.A, edge.B, point);
                    if (candidate.Area2d > Eps) triangles.Add(candidate);
                }
            }

            triangles = triangles
                .Where(t => !t.ContainsVertex(s1.Id) && !t.ContainsVertex(s2.Id) && !t.ContainsVertex(s3.Id))
                .Where(t => t.Area2d > Eps)
                .ToList();
            return triangles;
        }

        private static void AddEdge(Dictionary<EdgeKey, EdgeRecord> map, TinPoint a, TinPoint b)
        {
            EdgeKey key = new EdgeKey(a.Id, b.Id);
            EdgeRecord record;
            if (map.TryGetValue(key, out record))
            {
                record.Count++;
                map[key] = record;
            }
            else
            {
                map[key] = new EdgeRecord(a, b, 1);
            }
        }

        private sealed class CassCommandSession
        {
            public Document Doc { get; set; }
            public SurfaceAreaAnnotationOptions Options { get; set; }
            public DateTime StartTime { get; set; }
            public string PreviousUsers5 { get; set; }
            public bool Users5Restored { get; set; }
            public bool SurfaceAreaCommandSeen { get; set; }
            public bool FinishScheduled { get; set; }
            public ObjectId BoundaryObjectId { get; set; }
            public string BoundaryHandle { get; set; }
            public string BoundaryLayerName { get; set; }
            public List<ObjectId> AppendedObjectIds { get; private set; }
            public HashSet<ObjectId> BeforeObjectIds { get; set; }

            public CassCommandSession()
            {
                PreviousUsers5 = string.Empty;
                BoundaryObjectId = ObjectId.Null;
                BoundaryHandle = string.Empty;
                BoundaryLayerName = string.Empty;
                AppendedObjectIds = new List<ObjectId>();
                BeforeObjectIds = new HashSet<ObjectId>();
            }
        }

        private sealed class ElevationPoint
        {
            public ElevationPoint(Point2d xy, double z)
            {
                XY = xy;
                Z = z;
            }

            public Point2d XY { get; private set; }
            public double Z { get; private set; }
        }

        private sealed class TinPoint
        {
            public TinPoint(int id, Point3d point)
            {
                Id = id;
                Point = point;
            }

            public int Id { get; set; }
            public Point3d Point { get; private set; }
        }

        private struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly int _a;
            private readonly int _b;

            public EdgeKey(int a, int b)
            {
                if (a < b)
                {
                    _a = a;
                    _b = b;
                }
                else
                {
                    _a = b;
                    _b = a;
                }
            }

            public bool Equals(EdgeKey other)
            {
                return _a == other._a && _b == other._b;
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey && Equals((EdgeKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_a * 397) ^ _b;
                }
            }
        }

        private struct EdgeRecord
        {
            public EdgeRecord(TinPoint a, TinPoint b, int count)
            {
                A = a;
                B = b;
                Count = count;
            }

            public TinPoint A { get; private set; }
            public TinPoint B { get; private set; }
            public int Count { get; set; }
        }

        private sealed class Triangle
        {
            public Triangle(TinPoint a, TinPoint b, TinPoint c)
            {
                A = a;
                B = b;
                C = c;
                Area2d = Math.Abs(((b.Point.X - a.Point.X) * (c.Point.Y - a.Point.Y)) - ((c.Point.X - a.Point.X) * (b.Point.Y - a.Point.Y))) * 0.5;
                BuildCircumcircle();
            }

            public TinPoint A { get; private set; }
            public TinPoint B { get; private set; }
            public TinPoint C { get; private set; }
            public double Area2d { get; private set; }
            private Point2d CircumCenter { get; set; }
            private double CircumRadius2 { get; set; }
            private bool HasCircumcircle { get; set; }

            public bool ContainsVertex(int id)
            {
                return A.Id == id || B.Id == id || C.Id == id;
            }

            public bool IsPointInsideCircumcircle(TinPoint point)
            {
                if (!HasCircumcircle) return false;
                double dx = point.Point.X - CircumCenter.X;
                double dy = point.Point.Y - CircumCenter.Y;
                double d2 = dx * dx + dy * dy;
                return d2 <= CircumRadius2 + 1e-6;
            }

            private void BuildCircumcircle()
            {
                double ax = A.Point.X;
                double ay = A.Point.Y;
                double bx = B.Point.X;
                double by = B.Point.Y;
                double cx = C.Point.X;
                double cy = C.Point.Y;

                double d = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
                if (Math.Abs(d) < Eps)
                {
                    HasCircumcircle = false;
                    CircumCenter = new Point2d(0, 0);
                    CircumRadius2 = 0;
                    return;
                }

                double ax2ay2 = ax * ax + ay * ay;
                double bx2by2 = bx * bx + by * by;
                double cx2cy2 = cx * cx + cy * cy;

                double ux = (ax2ay2 * (by - cy) + bx2by2 * (cy - ay) + cx2cy2 * (ay - by)) / d;
                double uy = (ax2ay2 * (cx - bx) + bx2by2 * (ax - cx) + cx2cy2 * (bx - ax)) / d;

                CircumCenter = new Point2d(ux, uy);
                double dx = ux - ax;
                double dy = uy - ay;
                CircumRadius2 = dx * dx + dy * dy;
                HasCircumcircle = true;
            }
        }
    }

    public sealed class SurfaceAreaCassCommandBridge
    {
        [CommandMethod("TCBMJ_CASS_FINISH", CommandFlags.Modal)]
        public void FinishCassSurfaceAreaAnnotation()
        {
            SurfaceAreaAnnotationService.FinishPendingCassCommandAnnotation();
        }
    }

}
