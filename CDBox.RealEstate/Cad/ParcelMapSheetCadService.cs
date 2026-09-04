using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CDBox.RealEstate.Models;
using CDBox.Shared.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace CDBox.RealEstate.Cad
{
    /// <summary>
    /// 以 CASS FENFU 的实际输出为准识别图幅号。命令完成后会读取并
    /// 删除本次临时生成的格网、图幅文字，不在图纸中留下辅助成果。
    /// </summary>
    internal sealed class ParcelMapSheetCadService
    {
        public const string FinishCommandName = "CDREMAPSHEETFINISH";
        private const string CassCommandName = "fenfu";

        private readonly ICDBoxNotificationService _notifications;
        private readonly ICDBoxLogger _logger;
        private PendingSession _pending;

        public ParcelMapSheetCadService(
            ICDBoxNotificationService notifications, ICDBoxLogger logger)
        {
            _notifications = notifications
                ?? throw new ArgumentNullException("notifications");
            _logger = logger ?? throw new ArgumentNullException("logger");
        }

        public bool IsPending { get { return _pending != null; } }

        public bool Start(ParcelSurveyRecord record,
            Action<ParcelMapSheetRecognitionResult> completed)
        {
            if (_pending != null)
            {
                Notify("已有图幅号识别正在执行，请稍候。",
                    CDBoxNotificationLevel.Warning);
                return false;
            }
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                Notify("当前没有可用的 CAD 图纸。",
                    CDBoxNotificationLevel.Warning);
                return false;
            }

            List<Point3d> points = CadPoints(record);
            if (points.Count < 3)
            {
                Notify("请先选择宗地并识别有效权属线。",
                    CDBoxNotificationLevel.Warning);
                return false;
            }

            double minX = points.Min(x => x.X);
            double minY = points.Min(x => x.Y);
            double maxX = points.Max(x => x.X);
            double maxY = points.Max(x => x.Y);
            if (maxX - minX <= 0.000001 || maxY - minY <= 0.000001)
            {
                Notify("宗地范围无效，无法识别图幅号。",
                    CDBoxNotificationLevel.Warning);
                return false;
            }

            var session = new PendingSession
            {
                Document = document,
                BeforeObjectIds = SnapshotCurrentSpaceEntityIds(document),
                Completed = completed,
                StartedAt = DateTime.Now
            };
            _pending = session;
            try
            {
                document.Database.ObjectAppended += DatabaseObjectAppended;
                string lisp = BuildCassLisp(minX, minY, maxX, maxY);
                object acadDocument = document.GetAcadDocument();
                if (acadDocument == null)
                    throw new InvalidOperationException(
                        "无法取得 AutoCAD 文档对象。");
                acadDocument.GetType().InvokeMember("SendCommand",
                    BindingFlags.InvokeMethod, null, acadDocument,
                    new object[] { lisp + "\n" });
                Notify("正在调用 CASS 建立临时格网并识别图幅号。",
                    CDBoxNotificationLevel.Information);
                return true;
            }
            catch (Exception ex)
            {
                Detach(session);
                _pending = null;
                _logger.Error("启动 CASS 图幅号识别失败。", ex);
                Notify("无法启动 CASS 图幅号识别：" + ex.Message,
                    CDBoxNotificationLevel.Error);
                return false;
            }
        }

        public void FinishPending()
        {
            PendingSession session = _pending;
            if (session == null)
            {
                Notify("没有等待完成的图幅号识别。",
                    CDBoxNotificationLevel.Warning);
                return;
            }
            _pending = null;
            Detach(session);

            ParcelMapSheetRecognitionResult result;
            try
            {
                IList<ObjectId> created = FindCreatedCurrentSpaceEntities(
                    session);
                IList<string> texts = ReadTexts(session.Document, created);
                IList<string> numbers = ParcelMapSheetNumberParser.Extract(
                    texts);
                int deleted = DeleteEntities(session.Document, created);
                if (numbers.Count == 0)
                {
                    result = new ParcelMapSheetRecognitionResult
                    {
                        Success = false,
                        Message = "CASS 已执行，但本次生成物中没有找到可识别的图幅号；已清理临时格网 "
                            + deleted + " 个图元。"
                    };
                }
                else
                {
                    result = new ParcelMapSheetRecognitionResult
                    {
                        Success = true,
                        MapSheetNumber = string.Join("、", numbers),
                        Message = "已识别图幅号 " + string.Join("、", numbers)
                            + "，并清理临时格网 " + deleted + " 个图元。"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Error("完成 CASS 图幅号识别失败。", ex);
                int deleted = 0;
                try
                {
                    deleted = DeleteEntities(session.Document,
                        FindCreatedCurrentSpaceEntities(session));
                }
                catch { }
                result = new ParcelMapSheetRecognitionResult
                {
                    Success = false,
                    Message = "读取 CASS 图幅号失败：" + ex.Message
                        + (deleted > 0 ? "；已清理临时图元 " + deleted
                            + " 个。" : string.Empty)
                };
            }

            try
            {
                if (session.Completed != null) session.Completed(result);
            }
            catch (Exception ex)
            {
                _logger.Error("保存图幅号识别结果失败。", ex);
            }
        }

        private void DatabaseObjectAppended(object sender, ObjectEventArgs e)
        {
            PendingSession session = _pending;
            if (session == null || e == null || e.DBObject == null) return;
            Entity entity = e.DBObject as Entity;
            if (entity == null) return;
            try
            {
                ObjectId id = entity.ObjectId;
                if (!id.IsNull) session.AppendedObjectIds.Add(id);
            }
            catch { }
        }

        private void Detach(PendingSession session)
        {
            if (session == null || session.Document == null) return;
            try
            {
                session.Document.Database.ObjectAppended -=
                    DatabaseObjectAppended;
            }
            catch { }
        }

        private static string BuildCassLisp(double minX, double minY,
            double maxX, double maxY)
        {
            // 参数依次对应：图幅尺寸 1、测区两角、去带号 1、
            // 取整方式 1、不绘空格网 2。收尾命令负责读取并清理生成物。
            return "(progn (command \"" + CassCommandName
                + "\" \"1\" (list " + LispNumber(minX) + " "
                + LispNumber(minY) + " 0.0) (list " + LispNumber(maxX)
                + " " + LispNumber(maxY)
                + " 0.0) \"1\" \"1\" \"2\") (command \""
                + FinishCommandName + "\") (princ))";
        }

        private static string LispNumber(double value)
        {
            return value.ToString("0.###############",
                CultureInfo.InvariantCulture);
        }

        private static List<Point3d> CadPoints(ParcelSurveyRecord record)
        {
            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            return record.Boundary.Points.Where(x => x != null
                    && x.X.HasValue && x.Y.HasValue)
                .Select(x => new Point3d(
                    (double)ParcelBoundaryCoordinateConvention.CadXFromSurvey(
                        x.X.Value, x.Y.Value),
                    (double)ParcelBoundaryCoordinateConvention.CadYFromSurvey(
                        x.X.Value, x.Y.Value), 0))
                .ToList();
        }

        private static HashSet<ObjectId> SnapshotCurrentSpaceEntityIds(
            Document document)
        {
            var ids = new HashSet<ObjectId>();
            if (document == null) return ids;
            using (Transaction transaction = document.Database
                .TransactionManager.StartOpenCloseTransaction())
            {
                BlockTableRecord space = transaction.GetObject(
                    document.Database.CurrentSpaceId, OpenMode.ForRead,
                    false) as BlockTableRecord;
                if (space != null)
                    foreach (ObjectId id in space)
                        if (!id.IsNull) ids.Add(id);
                transaction.Commit();
            }
            return ids;
        }

        private static IList<ObjectId> FindCreatedCurrentSpaceEntities(
            PendingSession session)
        {
            HashSet<ObjectId> after = SnapshotCurrentSpaceEntityIds(
                session.Document);
            after.ExceptWith(session.BeforeObjectIds
                ?? new HashSet<ObjectId>());
            foreach (ObjectId id in session.AppendedObjectIds)
            {
                try
                {
                    if (id.IsNull || !id.IsValid || id.IsErased) continue;
                    if (id.Database == session.Document.Database)
                        after.Add(id);
                }
                catch { }
            }

            // ObjectAppended 也会报告块定义内部对象；只清理当前空间的
            // 顶层生成实体，块内属性随块参照一起清除。
            var result = new List<ObjectId>();
            using (Transaction transaction = session.Document.Database
                .TransactionManager.StartOpenCloseTransaction())
            {
                foreach (ObjectId id in after)
                {
                    try
                    {
                        Entity entity = transaction.GetObject(id,
                            OpenMode.ForRead, false) as Entity;
                        if (entity != null && entity.OwnerId ==
                            session.Document.Database.CurrentSpaceId)
                            result.Add(id);
                    }
                    catch { }
                }
                transaction.Commit();
            }
            return result;
        }

        private static IList<string> ReadTexts(Document document,
            IEnumerable<ObjectId> ids)
        {
            var texts = new List<string>();
            using (Transaction transaction = document.Database
                .TransactionManager.StartOpenCloseTransaction())
            {
                foreach (ObjectId id in ids ?? Enumerable.Empty<ObjectId>())
                {
                    try
                    {
                        Entity entity = transaction.GetObject(id,
                            OpenMode.ForRead, false) as Entity;
                        AddEntityTexts(entity, transaction, texts, 0,
                            new HashSet<ObjectId>());
                    }
                    catch { }
                }
                transaction.Commit();
            }
            return texts;
        }

        private static void AddEntityTexts(Entity entity,
            Transaction transaction, IList<string> texts, int depth,
            ISet<ObjectId> visitedBlocks)
        {
            if (entity == null || depth > 4) return;
            DBText dbText = entity as DBText;
            if (dbText != null)
            {
                texts.Add(dbText.TextString ?? string.Empty);
                return;
            }
            MText mText = entity as MText;
            if (mText != null)
            {
                texts.Add(StripMTextCodes(mText.Contents));
                return;
            }
            AttributeReference attribute = entity as AttributeReference;
            if (attribute != null)
            {
                texts.Add(attribute.TextString ?? string.Empty);
                return;
            }
            BlockReference block = entity as BlockReference;
            if (block == null) return;
            foreach (ObjectId attributeId in block.AttributeCollection)
            {
                try
                {
                    AddEntityTexts(transaction.GetObject(attributeId,
                        OpenMode.ForRead, false) as Entity, transaction,
                        texts, depth + 1, visitedBlocks);
                }
                catch { }
            }
            ObjectId definitionId = block.BlockTableRecord;
            if (definitionId.IsNull || visitedBlocks.Contains(definitionId))
                return;
            visitedBlocks.Add(definitionId);
            try
            {
                BlockTableRecord definition = transaction.GetObject(
                    definitionId, OpenMode.ForRead, false)
                    as BlockTableRecord;
                if (definition != null)
                    foreach (ObjectId childId in definition)
                        AddEntityTexts(transaction.GetObject(childId,
                            OpenMode.ForRead, false) as Entity, transaction,
                            texts, depth + 1, visitedBlocks);
            }
            catch { }
        }

        private static string StripMTextCodes(string value)
        {
            string text = value ?? string.Empty;
            text = text.Replace("\\P", " ").Replace("\\~", " ")
                .Replace("{", string.Empty).Replace("}", string.Empty);
            return Regex.Replace(text, @"\\[^;]*;", string.Empty);
        }

        private static int DeleteEntities(Document document,
            IEnumerable<ObjectId> ids)
        {
            if (document == null) return 0;
            int deleted = 0;
            using (Transaction transaction = document.Database
                .TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in ids ?? Enumerable.Empty<ObjectId>())
                {
                    try
                    {
                        if (id.IsNull || !id.IsValid || id.IsErased) continue;
                        Entity entity = transaction.GetObject(id,
                            OpenMode.ForWrite, false) as Entity;
                        if (entity == null) continue;
                        entity.Erase(true);
                        deleted++;
                    }
                    catch { }
                }
                transaction.Commit();
            }
            return deleted;
        }

        private void Notify(string message, CDBoxNotificationLevel level)
        {
            _notifications.Show("图幅号识别", message, level);
        }

        private sealed class PendingSession
        {
            public Document Document { get; set; }
            public HashSet<ObjectId> BeforeObjectIds { get; set; }
            public HashSet<ObjectId> AppendedObjectIds { get; private set; }
            public DateTime StartedAt { get; set; }
            public Action<ParcelMapSheetRecognitionResult> Completed
            {
                get;
                set;
            }

            public PendingSession()
            {
                BeforeObjectIds = new HashSet<ObjectId>();
                AppendedObjectIds = new HashSet<ObjectId>();
            }
        }
    }

    internal sealed class ParcelMapSheetRecognitionResult
    {
        public bool Success { get; set; }
        public string MapSheetNumber { get; set; }
        public string Message { get; set; }

        public ParcelMapSheetRecognitionResult()
        {
            MapSheetNumber = string.Empty;
            Message = string.Empty;
        }
    }
}
