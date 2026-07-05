using System.Collections.Generic;
using System.Text;
using Autodesk.AutoCAD.Geometry;

namespace TCPipeAutoDraw.Modules.PipeDraw
{
    public sealed class PipeDrawOptions
    {
        public bool AddNotes { get; set; }
        public double TextHeight { get; set; }
        public double NodeRadius { get; set; }
        public bool SwapXY { get; set; }

        public static PipeDrawOptions Default
        {
            get
            {
                return new PipeDrawOptions
                {
                    AddNotes = true,
                    TextHeight = 6.0,
                    NodeRadius = 5.0,
                    SwapXY = false
                };
            }
        }
    }

    public sealed class PipeDrawResult
    {
        public int RowCount { get; set; }
        public int NodeCount { get; set; }
        public int PolylineCount { get; set; }
        public int PointObjectCount { get; set; }
        public int TextCount { get; set; }
        public List<string> Messages { get; private set; }

        public PipeDrawResult()
        {
            Messages = new List<string>();
        }

        public string ToEditorMessage()
        {
            var sb = new StringBuilder();
            sb.Append("\n==============================");
            sb.Append("\n管线自动绘制完成。");
            sb.Append("\n读取测点：" + RowCount);
            sb.Append("\n生成多段线：" + PolylineCount);
            sb.Append("\n生成点状对象：" + PointObjectCount);
            sb.Append("\n生成文字：" + TextCount);
            sb.Append("\n节点数量：" + NodeCount);
            sb.Append("\n提示/错误数量：" + Messages.Count);
            if (Messages.Count > 0)
            {
                sb.Append("\n--- 提示/错误列表 ---");
                foreach (string message in Messages)
                {
                    sb.Append("\n" + message);
                }
            }
            sb.Append("\n==============================");
            return sb.ToString();
        }
    }

    internal sealed class PipeObjectDef
    {
        public string ObjCode { get; private set; }
        public string ObjName { get; private set; }
        public string BaseLayer { get; private set; }
        public short BaseColor { get; private set; }
        public string BuriedLayer { get; private set; }
        public short BuriedColor { get; private set; }
        public string DefaultNote { get; private set; }

        public PipeObjectDef(string objCode, string objName, string baseLayer, short baseColor, string buriedLayer, short buriedColor, string defaultNote)
        {
            ObjCode = objCode;
            ObjName = objName;
            BaseLayer = baseLayer;
            BaseColor = baseColor;
            BuriedLayer = buriedLayer ?? string.Empty;
            BuriedColor = buriedColor;
            DefaultNote = defaultNote;
        }
    }

    internal sealed class PointRow
    {
        public string PointId { get; private set; }
        public string Code { get; private set; }
        public Point3d Point { get; private set; }

        public PointRow(string pointId, string code, Point3d point)
        {
            PointId = pointId;
            Code = code;
            Point = point;
        }
    }

    internal sealed class CodeParts
    {
        public string Body { get; private set; }
        public string Action { get; private set; }

        public CodeParts(string body, string action)
        {
            Body = body;
            Action = action;
        }
    }
}
