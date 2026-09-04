using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CDBox.Common.Features.ShortCodeRecognition
{
    public sealed class ShortCodeRecognitionResult
    {
        public int ValidRecordCount { get; set; }
        public int InvalidLineCount { get; set; }
        public int PolylineCount { get; set; }
        public int JumpConnectionCount { get; set; }
        public string EncodingName { get; set; }
        public IList<ShortCodeParseIssue> Issues { get; set; }

        public string ToEditorMessage()
        {
            StringBuilder text = new StringBuilder();
            text.Append("\n[简码识别] 完成：有效测点 ")
                .Append(ValidRecordCount)
                .Append("，生成多段线 ").Append(PolylineCount);
            if (JumpConnectionCount > 0)
                text.Append("（含跳点连接 ")
                    .Append(JumpConnectionCount).Append("）");
            if (InvalidLineCount > 0)
                text.Append("，跳过异常行 ").Append(InvalidLineCount);
            if (!string.IsNullOrWhiteSpace(EncodingName))
                text.Append("，文本编码 ").Append(EncodingName);
            text.Append("。");
            if (Issues != null)
            {
                int count = Math.Min(5, Issues.Count);
                for (int i = 0; i < count; i++)
                {
                    ShortCodeParseIssue issue = Issues[i];
                    text.Append("\n  第 ").Append(issue.LineNumber)
                        .Append(" 行：").Append(issue.Message);
                }
                if (InvalidLineCount > count)
                    text.Append("\n  其余异常行已省略，不影响有效数据识别。");
            }
            return text.ToString();
        }
    }

    public sealed class ShortCodeRecognitionModule
    {
        public ShortCodeRecognitionResult Run(Document document,
            string filePath, ShortCodeRecognitionSettings settings)
        {
            if (document == null) throw new ArgumentNullException("document");
            settings = settings ?? new ShortCodeRecognitionSettings();
            settings.Normalize();

            ShortCodeReadResult read =
                ShortCodeRecognitionParser.ReadFile(filePath);
            List<ShortCodePath> paths =
                ShortCodeRecognitionParser.BuildPaths(read.Records, settings);
            int drawn = 0;
            int jumps = 0;

            if (paths.Count > 0)
            {
                using (document.LockDocument())
                using (Transaction transaction = document.Database
                    .TransactionManager.StartTransaction())
                {
                    BlockTableRecord space =
                        transaction.GetObject(
                            document.Database.CurrentSpaceId,
                            OpenMode.ForWrite, false) as BlockTableRecord;
                    if (space == null)
                        throw new InvalidOperationException(
                            "无法打开当前绘图空间。");

                    foreach (ShortCodePath path in paths)
                    {
                        if (path == null || path.Points.Count < 2) continue;
                        Polyline polyline = new Polyline();
                        polyline.SetDatabaseDefaults(document.Database);
                        for (int i = 0; i < path.Points.Count; i++)
                        {
                            ShortCodeCoordinateRecord point = path.Points[i];
                            polyline.AddVertexAt(i,
                                new Point2d(point.X, point.Y), 0, 0, 0);
                        }
                        polyline.Closed = path.Closed;
                        space.AppendEntity(polyline);
                        transaction.AddNewlyCreatedDBObject(polyline, true);
                        drawn++;
                        if (path.IsJumpConnection) jumps++;
                    }
                    transaction.Commit();
                }
            }

            return new ShortCodeRecognitionResult
            {
                ValidRecordCount = read.Records.Count,
                InvalidLineCount = read.InvalidLineCount,
                PolylineCount = drawn,
                JumpConnectionCount = jumps,
                EncodingName = read.EncodingName,
                Issues = read.Issues
            };
        }
    }
}
