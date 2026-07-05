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

namespace TCPipeAutoDraw.Modules.SurfaceAreaAnnotation
{
    /// <summary>
    /// 表面积标注核心服务。
    /// 正式模式：自动调用 CASS surfacearea 命令，使用 CASS 结果注记，并可删除 CASS 自动生成的三角网/三角面积文字。
    /// 插件内置 TIN 估算入口已移除，避免与 CASS 成果不一致。
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
            options.CalculationMode = SurfaceAreaCalculationMode.CassCommand;

            if (options.CalculationMode == SurfaceAreaCalculationMode.CassCommand)
            {
                return StartCassCommandCalculateAndAnnotate(doc, options);
            }

            Editor ed = doc.Editor;
            PromptEntityOptions peo = new PromptEntityOptions("\n请选择一个闭合区域边界（闭合多段线/圆/闭合曲线）");
            peo.SetRejectMessage("\n必须选择闭合曲线对象。 ");
            peo.AddAllowedClass(typeof(Curve), false);

            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                return new SurfaceAreaAnnotationResult { Success = false, Message = "已取消选择。" };
            }

            PromptPointOptions ppoStart = new PromptPointOptions("\n请指定引线拉出位置");
            PromptPointResult pprStart = ed.GetPoint(ppoStart);
            if (pprStart.Status != PromptStatus.OK)
            {
                return new SurfaceAreaAnnotationResult { Success = false, Message = "已取消引线拉出位置。" };
            }

            PromptPointOptions ppoEnd = new PromptPointOptions("\n请指定引线结束位置（注记位置）");
            ppoEnd.UseBasePoint = true;
            ppoEnd.BasePoint = pprStart.Value;
            PromptPointResult pprEnd = ed.GetPoint(ppoEnd);
            if (pprEnd.Status != PromptStatus.OK)
            {
                return new SurfaceAreaAnnotationResult { Success = false, Message = "已取消注记位置。" };
            }

            return CalculateAndAnnotate(doc, per.ObjectId, pprStart.Value, pprEnd.Value, true, options);
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
                    Message = "已有一个 CASS 表面积标注流程正在执行，请完成或取消后再试。"
                };
            }

            // 关键修正：不再使用 SendStringToExecute 排队执行 CASS，也不再在 Idle 中继续流程。
            // SendStringToExecute / Idle 在当前 CASS 组合中会留下额外空回车，导致 AutoCAD 自动重复上一次命令（TCSURF）。
            // 这里改为 Editor.Command 同步调用 surfacearea，CASS 结束后立即由插件读取结果、清理生成物、提示两点注记。
            PromptEntityOptions peo = new PromptEntityOptions("\n请选择闭合边界");
            peo.SetRejectMessage("\n必须选择闭合曲线对象。 ");
            peo.AddAllowedClass(typeof(Curve), false);

            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                return new SurfaceAreaAnnotationResult
                {
                    Success = false,
                    CalculationMode = SurfaceAreaCalculationMode.CassCommand,
                    Message = "已取消选择闭合边界。"
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

            // 不再用插件自己的高程点识别结果决定是否调用 CASS。
            // CASS 对“图上高程点”的识别规则与插件读取 DBText/块/Z 值的规则可能不完全一致，
            // 因此这里始终先调用 CASS surfacearea；若 CASS 未生成本次 surface.log，再自动降级为普通面积标注。

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

                // 双保险记录 CASS 生成物：
                // 1) ObjectAppended 事件记录；
                // 2) 调用前后扫描当前空间差集。
                // COM SendCommand 在 CASS11 中可能是异步排队执行，不能在发送后立刻读取结果。
                // 因此把“调用前快照”保存到 session，等 CASS 完成后由 TCBMJ_CASS_FINISH 统一收尾。
                session.BeforeObjectIds = SnapshotCurrentSpaceEntityIds(doc);
                try { doc.Database.ObjectAppended += CassDatabase_ObjectAppended; } catch { }

                ed.WriteMessage("\n[表面积标注] 已选择边界，正在通过 LISP 调用 CASS surfacearea 计算。边界图层：" + boundaryLayer);
                ed.WriteMessage("\n[表面积标注] CASS 完成后将自动进入引线/注记位置选择。\n");

                RunCassSurfaceAreaCommandSynchronously(doc, per.ObjectId, options.BoundaryInterval, boundaryHandle);

                return new SurfaceAreaAnnotationResult
                {
                    Success = true,
                    CalculationMode = SurfaceAreaCalculationMode.CassCommand,
                    AsyncStarted = true,
                    BoundaryObjectId = per.ObjectId,
                    BoundaryLayerName = boundaryLayer,
                    Message = "已启动 CASS surfacearea 计算，等待 CASS 完成后自动继续注记。"
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
                    Message = "调用 CASS surfacearea 失败：" + ex.Message
                };
            }
        }

        private static void RunCassSurfaceAreaCommandSynchronously(Document doc, ObjectId boundaryId, double interval, string boundaryHandle)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (string.IsNullOrWhiteSpace(boundaryHandle)) throw new ArgumentException("边界对象句柄为空。", "boundaryHandle");

            // Editor.Command 直接传 ObjectId / SelectionSet 时，CASS11 的 surfacearea 可能只启动命令，
            // 但没有真正接收到边界对象，因此不会生成三角网，也不会输出表面积。
            // 这里改为调用你已实测成功的同款 LISP，并在 CASS 计算完成后由 LISP 继续调用插件收尾命令：
            // (progn (command "surfacearea" "2" (handent "边界Handle") 5) (command "TCBMJ_CASS_FINISH") (princ))
            // 这样不会在 SendCommand 返回后过早读取 surface.log / 处理注记。
            string intervalText = interval.ToString(CultureInfo.InvariantCulture);
            string safeHandle = EscapeLispString(boundaryHandle);
            string lisp = "(progn (command \"" + CassSurfaceAreaCommandName + "\" \"2\" (handent \"" + safeHandle + "\") " + intervalText + ") (command \"" + CassFinishCommandName + "\") (princ))";

            try
            {
                object acadDocument = doc.GetAcadDocument();
                if (acadDocument == null)
                {
                    throw new InvalidOperationException("无法取得 AutoCAD COM 文档对象。");
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
                throw new InvalidOperationException("无法通过 LISP 调用 CASS surfacearea。发送内容：" + lisp + "；错误：" + ex.Message, ex);
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
                            result.Message = "选择对象不是曲线边界。";
                            return result;
                        }

                        if (!IsClosedCurve(boundary))
                        {
                            result.Message = "选择对象不是闭合边界。请先闭合多段线，或选择圆/闭合曲线。";
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
                    result.Message = "无法取得边界对象句柄，不能传递给 CASS surfacearea。";
                    return result;
                }

                result.Success = true;
                return result;
            }
            catch (System.Exception ex)
            {
                result.Message = "读取边界对象失败：" + ex.Message;
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
            session.Doc.Editor.WriteMessage("\n[表面积标注] CASS surfacearea 已取消。\n");
        }

        private static void CassDocument_CommandFailed(object sender, CommandEventArgs e)
        {
            CassCommandSession session = _pendingCassSession;
            if (session == null || e == null) return;
            if (!IsSurfaceAreaCommand(e.GlobalCommandName)) return;

            CleanupCassSession(session, true);
            session.Doc.Editor.WriteMessage("\n[表面积标注] CASS surfacearea 执行失败。\n");
        }

        private static void CassDocument_LispEnded(object sender, EventArgs e)
        {
            CassCommandSession session = _pendingCassSession;
            if (session == null) return;

            // CASS surfacearea 是通过 (command ...) 在 LISP 中执行的。
            // 有些 CASS/AutoCAD 组合不会稳定触发 surfacearea 的 CommandEnded，
            // 但 LISP 结束时 CASS 计算通常已经完成，因此这里也安排收尾流程。
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

            // 先停止记录，避免插件后续注记对象也被当作 CASS 生成物删除。
            DetachCassSessionEvents(session);

            // 不再通过 SendStringToExecute 排队执行 TCBMJ_CASS_FINISH。
            // 在某些 AutoCAD/CASS 组合中，从 LispEnded/CommandEnded 事件里发送命令会停在队列中，
            // 直到用户再次输入 TCSURF 或其他命令后才继续，表现为“需要再次点击开始计算并标注”。
            // 改为在 WinForms Idle 中直接进入收尾流程：读取 CASS 结果、删除生成物、提示两点注记。
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
                        // 兜底：如果 Idle 事件不可用，再使用内部收尾命令。
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
                // 如果 AutoCAD 仍有命令在执行，继续等待下一次空闲，避免在 CASS 命令尚未完全退出时弹出 GetPoint。
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
                if (current != null) current.Editor.WriteMessage("\n[表面积标注] 没有待完成的 CASS 表面积标注流程。\n");
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

                // 正式调用模式：CASS 负责计算，插件读取本次生成/更新的 surface.log。
                // 不再读取图上三角面积文字，因为 CASS 可能把文字生成在特殊对象/块中，插件不一定能稳定识别。
                // 这里要求 surface.log 的修改时间晚于本次流程启动时间，避免误读旧日志。
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
                        ed.WriteMessage("\n[表面积标注] CASS 已执行，但未能读取本次生成的 surface.log 结果：" + message);
                        if (deletedOnFail > 0) ed.WriteMessage(" 已删除 CASS 生成对象 " + deletedOnFail + " 个。");
                        ed.WriteMessage(" 同时普通面积读取失败：" + planMessage + "\n");
                        return;
                    }

                    ed.WriteMessage("\n[表面积标注] CASS 未生成/未更新本次 surface.log，已自动改为普通面积标注。原因：" + message);
                    if (deletedOnFail > 0) ed.WriteMessage(" 已删除 CASS 生成对象 " + deletedOnFail + " 个。");
                    ed.WriteMessage("\n");

                    SurfaceAreaAnnotationResult fallbackResult = AnnotatePlanAreaFallback(
                        doc,
                        boundaryId,
                        planCheck,
                        options,
                        "CASS 未生成/未更新本次 surface.log，已改为面积标注。",
                        session.AppendedObjectIds.Count,
                        deletedOnFail);
                    ed.WriteMessage(fallbackResult.ToEditorMessage());
                    return;
                }

                int deleted = 0;
                if (options.DeleteCassGeneratedObjects)
                {
                    deleted = DeleteCassGeneratedObjects(doc, session.AppendedObjectIds, boundaryId);
                }

                SurfaceAreaAnnotationResult result = AnnotateCassCommandResult(doc, boundaryId, cassArea, cassLogPath, cassLogLine, session.AppendedObjectIds.Count, deleted, options);
                ed.WriteMessage(result.ToEditorMessage());
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[表面积标注] CASS 表面积注记失败：" + ex.Message + "\n");
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

            PromptPointOptions ppoStart = new PromptPointOptions("\n请指定引线拉出位置");
            PromptPointResult pprStart = doc.Editor.GetPoint(ppoStart);
            if (pprStart.Status != PromptStatus.OK)
            {
                result.Message = "已取消引线拉出位置。";
                return result;
            }

            PromptPointOptions ppoEnd = new PromptPointOptions("\n请指定引线结束位置（注记位置）");
            ppoEnd.UseBasePoint = true;
            ppoEnd.BasePoint = pprStart.Value;
            PromptPointResult pprEnd = doc.Editor.GetPoint(ppoEnd);
            if (pprEnd.Status != PromptStatus.OK)
            {
                result.Message = "已取消注记位置。";
                return result;
            }

            result.AnnotationPoint = pprEnd.Value;
            string annotationLayer = ResolveAnnotationLayer(options, result.BoundaryLayerName);
            result.AnnotationLayerName = annotationLayer;
            result.AnnotationFontName = options.AnnotationFontName;

            using (doc.LockDocument())
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    string text = BuildAnnotationText(options, result);
                    AttachmentPoint attachment = GetTextAttachment(result.AnnotationPoint, pprStart.Value);
                    result.AnnotationObjectId = DrawAnnotationDbText(db, tr, result.AnnotationPoint, text, options.TextHeight, annotationLayer, 1, attachment, options.AnnotationFontName);

                    if (options.DrawLeader)
                    {
                        result.LeaderObjectId = DrawLeader(db, tr, pprStart.Value, result.AnnotationObjectId, options.TextHeight, annotationLayer, attachment);
                    }

                    tr.Commit();
                }
            }

            result.Success = true;
            result.Message = "CASS 表面积计算并注记完成。";
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
                            message = "选择对象不是曲线边界。";
                            return false;
                        }

                        if (!IsClosedCurve(boundary))
                        {
                            message = "选择对象不是闭合边界。请先闭合多段线，或选择圆/闭合曲线。";
                            return false;
                        }

                        result.BoundaryLayerName = boundary.Layer;
                        List<Point2d> boundary2d = SampleBoundary(boundary, options.BoundaryInterval);
                        if (boundary2d.Count < 3)
                        {
                            message = "边界采样失败，无法形成有效闭合区域。";
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
                message = "读取边界面积和高程点失败：" + ex.Message;
                return false;
            }
        }

        private static SurfaceAreaAnnotationResult AnnotatePlanAreaFallback(Document doc, ObjectId boundaryId, SurfaceAreaAnnotationResult areaCheck, SurfaceAreaAnnotationOptions options)
        {
            return AnnotatePlanAreaFallback(doc, boundaryId, areaCheck, options, "无有效高程，已改为面积标注。", 0, 0);
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
                fallbackMessage = "已改为面积标注。";
            }

            PromptPointOptions ppoStart = new PromptPointOptions("\n请指定引线拉出位置");
            PromptPointResult pprStart = doc.Editor.GetPoint(ppoStart);
            if (pprStart.Status != PromptStatus.OK)
            {
                result.Message = fallbackMessage + "但已取消引线拉出位置。";
                return result;
            }

            PromptPointOptions ppoEnd = new PromptPointOptions("\n请指定引线结束位置（注记位置）");
            ppoEnd.UseBasePoint = true;
            ppoEnd.BasePoint = pprStart.Value;
            PromptPointResult pprEnd = doc.Editor.GetPoint(ppoEnd);
            if (pprEnd.Status != PromptStatus.OK)
            {
                result.Message = fallbackMessage + "但已取消注记位置。";
                return result;
            }

            result.AnnotationPoint = pprEnd.Value;
            string annotationLayer = ResolveAnnotationLayer(options, result.BoundaryLayerName);
            result.AnnotationLayerName = annotationLayer;
            result.AnnotationFontName = options.AnnotationFontName;

            using (doc.LockDocument())
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    string text = BuildAnnotationText(options, result);
                    AttachmentPoint attachment = GetTextAttachment(result.AnnotationPoint, pprStart.Value);
                    result.AnnotationObjectId = DrawAnnotationDbText(db, tr, result.AnnotationPoint, text, options.TextHeight, annotationLayer, 1, attachment, options.AnnotationFontName);

                    if (options.DrawLeader)
                    {
                        result.LeaderObjectId = DrawLeader(db, tr, pprStart.Value, result.AnnotationObjectId, options.TextHeight, annotationLayer, attachment);
                    }

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
                message = "当前文档为空。";
                return false;
            }

            if (objectIds == null || objectIds.Count == 0)
            {
                message = "没有记录到 CASS 新增对象。";
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
                message = "本次 CASS 生成对象中没有找到可识别的三角面积文字。";
                return false;
            }

            double sum = 0.0;
            foreach (double value in values) sum += value;

            if (sum <= Eps)
            {
                message = "已识别三角面积文字，但合计值无效。";
                return false;
            }

            area = sum;
            areaTextCount = values.Count;
            sourceLine = "CASS 本次生成三角面积文字合计：" + sum.ToString("0.###", CultureInfo.InvariantCulture) + "（" + values.Count + " 个面积文字）";
            return true;
        }

        private static bool TryParseCassGeneratedAreaText(string rawText, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(rawText)) return false;

            string text = StripMTextCodes(rawText)
                .Replace("㎡", "")
                .Replace("平方米", "")
                .Replace("平方", "")
                .Replace("米", "")
                .Trim();

            if (string.IsNullOrWhiteSpace(text)) return false;

            // CASS 三角面积标注通常是纯小数，如 6.009。这里要求整段文本只含一个数值，
            // 避免把图名、说明文字或插件注记误当作面积。优先要求有小数点，避免三角编号类整数被误加。
            Match exact = Regex.Match(text, @"^\s*([+-]?\d+(?:[\.,]\d+)?)\s*$");
            if (!exact.Success) return false;

            string number = exact.Groups[1].Value;
            if (number.IndexOf('.') < 0 && number.IndexOf(',') < 0)
            {
                return false;
            }

            if (!TryParseFlexibleDouble(number, out value)) return false;
            if (value <= 0) return false;

            // 单个三角面积不应大到离谱。这里不做强边界限制，只排除明显异常数值。
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
                        result.Message = "选择对象不是曲线边界。";
                        return result;
                    }

                    if (!IsClosedCurve(boundary))
                    {
                        result.Message = "选择对象不是闭合边界。请先闭合多段线，或选择圆/闭合曲线。";
                        return result;
                    }

                    result.BoundaryLayerName = boundary.Layer;

                    List<Point2d> boundary2d = SampleBoundary(boundary, options.BoundaryInterval);
                    if (boundary2d.Count < 3)
                    {
                        result.Message = "边界采样失败，无法形成有效闭合区域。";
                        return result;
                    }

                    result.PlanArea = Math.Abs(PolygonArea(boundary2d));
                    result.BoundaryPointCount = boundary2d.Count;

                    result.CalculationMode = options.CalculationMode;

                    if (options.CalculationMode == SurfaceAreaCalculationMode.CassSurfaceLog)
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
                            result.Message = "闭合区域内可识别的高程点少于 3 个。请确认图上有 DBPoint、带 Z 值块，或纯数字高程文字。";
                            return result;
                        }

                        List<Triangle> terrainTriangles = BuildTerrainTriangles(elevations);
                        List<TinPoint> tinPoints = BuildTinPoints(boundary2d, elevations, terrainTriangles);
                        if (tinPoints.Count < 3)
                        {
                            result.Message = "有效三角网点不足，无法计算表面积。";
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
                            result.Message = "三角网生成失败，未得到有效表面积。";
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

                    tr.Commit();
                }
            }

            result.Success = true;
            result.Message = "表面积计算并注记完成。";
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
            // 正式版不再开放插件内置估算，所有主流程统一走 CASS surfacearea。
            if (options.CalculationMode != SurfaceAreaCalculationMode.CassCommand)
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
                // 某些曲线类型可能不可靠，继续用首尾点兜底。
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

            // 按指定间隔补充边界插值点。
            double d = interval;
            while (d < length - DuplicateTolerance)
            {
                distances.Add(d);
                d += interval;
            }

            // 对闭合多段线额外加入所有顶点距离。
            // 旧版本只按周长等分采样，矩形/折线边界会漏掉角点，平面面积和表面积都会偏小；
            // CASS 计算通常会保留边界顶点，因此这里也强制保留，以尽量贴近 CASS 结果。
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
                        // 个别异常顶点忽略，不影响整体采样。
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
                        // 无属性或读取失败时，尝试用块插入点 Z 值。
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
            value = value.Replace("，", ".");

            Match match = Regex.Match(value, @"^\s*([+-]?\d+(?:\.\d+)?)\s*(?:m|M|米)?\s*$");
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

            // CASS 命令自动调用模式必须读取“本次”生成/更新的日志，不能回退读取旧 surface.log。
            // 只有手动指定读取旧日志的备用模式才允许使用已有文件。
            if (string.IsNullOrWhiteSpace(filePath) && minWriteTime <= DateTime.MinValue)
            {
                filePath = existingCandidates.Select(x => x.Path).FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                message = "未找到 CASS 输出的 surface.log。请先用 CASS 执行“工程应用 → 计算表面积 → 根据图上高程点”，或在界面中手动指定 surface.log 路径。";
                return false;
            }

            string text;
            try
            {
                text = File.ReadAllText(filePath, Encoding.Default);
            }
            catch (System.Exception ex)
            {
                message = "读取 CASS surface.log 失败：" + ex.Message;
                return false;
            }

            if (!TryParseSurfaceAreaFromCassLog(text, out area, out resultLine))
            {
                message = "已找到 surface.log，但无法解析表面积数值：" + filePath;
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

            // 优先解析“表面积 = 741.520 平方米”这类总结果行。
            Regex directTotal = new Regex(@"(?:表\s*面\s*积|surface\s*area)\s*[=＝]\s*([+-]?\d+(?:[\.,]\d+)?)", RegexOptions.IgnoreCase);
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

            // CASS11 的 surface.log 常见情况：只列每个三角形的“表面积: x.xxx”，没有最终总面积。
            // 这里汇总所有三角面积；默认保留 2 位时与命令行“表面积 = xxx 平方米”一致。
            Regex triangleArea = new Regex(@"^\s*表\s*面\s*积\s*[:：]\s*([+-]?\d+(?:[\.,]\d+)?)\s*$", RegexOptions.IgnoreCase);
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
                resultLine = "surface.log 三角面积合计：" + sum.ToString("0.###", CultureInfo.InvariantCulture) + "（" + count + " 个三角）";
                return true;
            }

            Regex withUnit = new Regex(@"([+-]?\d+(?:[\.,]\d+)?)\s*(?:平方\s*米|平方米|m2|㎡)", RegexOptions.IgnoreCase);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i] ?? string.Empty;
                if (line.IndexOf("平方", StringComparison.CurrentCultureIgnoreCase) >= 0
                    || line.IndexOf("㎡", StringComparison.CurrentCultureIgnoreCase) >= 0
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
                .Replace("{图层名}", layerName)
                .Replace("{层名}", layerName)
                .Replace("{LayerName}", layerName)
                .Replace("{表面积}", surfaceAreaText)
                .Replace("{SurfaceArea}", surfaceAreaText)
                .Replace("{平面面积}", planAreaText)
                .Replace("{PlanArea}", planAreaText);

            // 无有效高程时会降级为平面面积标注。
            // 此时沿用用户原模板的数值格式，但注记名称不再显示“表面积”。
            if (result != null && result.PlanAreaFallback)
            {
                text = text.Replace("表面积", "面积");
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
            // 引线拉出点在注记左侧时，文字向右排；在注记右侧时，文字向左排。
            // 这样引线始终接到注记横线靠近边界的一侧，避免穿过文字。
            return leaderStartPoint.X <= annotationPoint.X ? AttachmentPoint.BottomLeft : AttachmentPoint.BottomRight;
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

            // 注记实体改为 AutoCAD 单行“文字”(DBText)，不再使用“多行文字”(MText)。
            // 引线在注记左侧时采用左对齐，文字向右排；引线在注记右侧时采用右对齐，文字向左排。
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
                // 个别文字样式可能不需要或不支持调整，对注记生成无影响。
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

            // DBText 是单行文字，模板中的换行统一压成空格，避免生成 MText 特有的 \P 控制符。
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

            // 横线长度直接取文字实体的实际几何范围，避免用户修改字高、文字样式后仍按估算长度绘制。
            // 向下留出约 0.22 倍字高的距离，让横线不贴文字。
            double underlineGap = Math.Max(textHeight * 0.22, 0.05);
            double y = minY - underlineGap;

            underlineStart = new Point3d(minX, y, z);
            underlineEnd = new Point3d(maxX, y, z);

            // 极端情况下部分 CAD 文字样式可能返回零宽范围，兜底给一段最小横线，避免引线退化。
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
