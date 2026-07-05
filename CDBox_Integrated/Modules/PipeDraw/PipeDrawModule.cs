using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using TCPipeAutoDraw.Core.Cad;

namespace TCPipeAutoDraw.Modules.PipeDraw
{
    /// <summary>
    /// 展点绘制管线模块。
    /// 入口由 Commands 调用；本类只负责读取、解析、组线、绘制。
    /// </summary>
    public sealed class PipeDrawModule
    {
        private const string NoteLayer = "AUTO_管线注记";
        private const string NodeLayer = "AUTO_节点";

        private readonly Dictionary<string, List<Point3d>> _paths = new Dictionary<string, List<Point3d>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Point3d>> _buryPaths = new Dictionary<string, List<Point3d>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Point3d> _nodes = new Dictionary<string, Point3d>(StringComparer.OrdinalIgnoreCase);

        private PipeDrawOptions _options;
        private PipeDrawResult _result;
        private PipeDefinitionProvider _defs;

        public PipeDrawResult Run(Document doc, string filePath, PipeDrawOptions options)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("测点文件路径不能为空。", "filePath");

            _options = options ?? PipeDrawOptions.Default;
            _result = new PipeDrawResult();
            _defs = new PipeDefinitionProvider(_result.Messages);
            _paths.Clear();
            _buryPaths.Clear();
            _nodes.Clear();

            List<PointRow> rows = PointFileReader.Read(filePath, _options, _result.Messages);
            _result.RowCount = rows.Count;
            if (rows.Count == 0)
            {
                _result.Messages.Add("没有读取到有效测点。 ");
                return _result;
            }

            using (doc.LockDocument())
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    CadLayerService.EnsureLayer(db, tr, NoteLayer, 1);
                    CadLayerService.EnsureLayer(db, tr, NodeLayer, 1);

                    foreach (PointRow row in rows)
                    {
                        ProcessCode(db, tr, row.Code, row.Point);
                    }

                    FlushAllOpenPaths(db, tr);
                    tr.Commit();
                }
            }

            _result.NodeCount = _nodes.Count;
            return _result;
        }

        private void ProcessCode(Database db, Transaction tr, string rawCode, Point3d pt)
        {
            string code = (rawCode ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length == 0) return;

            CodeParts parts = StripAction(code);
            string body = parts.Body;
            string action = parts.Action;

            if (body.Contains(":"))
            {
                ProcessNodeDecl(db, tr, body, pt);
            }
            else if (body.Contains("@"))
            {
                ProcessNodeRef(db, tr, body, pt);
            }
            else if (body.Contains(">"))
            {
                ProcessSwitch(db, tr, body, pt);
            }
            else if (body.Contains("&"))
            {
                ProcessBury(db, tr, body, action, pt);
            }
            else if (body.Contains("+"))
            {
                ProcessPlus(db, tr, body, pt);
            }
            else
            {
                ProcessBasicObject(db, tr, body, action, pt);
            }
        }

        private static CodeParts StripAction(string code)
        {
            if (code.Length >= 2)
            {
                char last = code[code.Length - 1];
                if (last == 'S' || last == 'E')
                {
                    return new CodeParts(code.Substring(0, code.Length - 1), last.ToString());
                }
            }
            return new CodeParts(code, string.Empty);
        }

        private void ProcessBasicObject(Database db, Transaction tr, string obj, string action, Point3d pt)
        {
            if (IsPointObject(obj))
            {
                DrawPointObject(db, tr, obj, pt);
                return;
            }

            if (action == "S")
            {
                StartPath(_paths, obj, pt);
            }
            else if (action == "E")
            {
                AppendPath(_paths, obj, pt);
                FlushPath(db, tr, _paths, obj, false);
            }
            else
            {
                AppendPath(_paths, obj, pt);
            }
        }

        private void ProcessSwitch(Database db, Transaction tr, string code, Point3d pt)
        {
            string[] parts = code.Split('>');
            if (parts.Length != 2)
            {
                AddMessage("切换代码格式错误：" + code);
                return;
            }

            string from = parts[0].Trim();
            string to = parts[1].Trim();

            AppendPath(_paths, from, pt);
            FlushPath(db, tr, _paths, from, false);

            StartPath(_paths, to, pt);
            AddText(db, tr, CadDrawService.Offset(pt, 2, 2), GetNote(from) + " 接 " + GetNote(to));
        }

        private void ProcessPlus(Database db, Transaction tr, string code, Point3d pt)
        {
            string[] objs = code.Split('+')
                .Select(delegate (string x) { return x.Trim(); })
                .Where(delegate (string x) { return x.Length > 0; })
                .ToArray();

            foreach (string obj in objs)
            {
                AppendPath(_paths, obj, pt);
            }
            AddText(db, tr, CadDrawService.Offset(pt, 2, 2), "连接：" + code);
        }

        private void ProcessNodeDecl(Database db, Transaction tr, string code, Point3d pt)
        {
            string[] parts = code.Split(':');
            if (parts.Length != 2)
            {
                AddMessage("节点声明格式错误：" + code);
                return;
            }

            string nodeName = parts[0].Trim();
            string[] objs = parts[1].Split('+')
                .Select(delegate (string x) { return x.Trim(); })
                .Where(delegate (string x) { return x.Length > 0; })
                .ToArray();

            _nodes[nodeName] = pt;
            DrawNode(db, tr, nodeName, pt);

            foreach (string obj in objs)
            {
                AppendPath(_paths, obj, pt);
            }
        }

        private void ProcessNodeRef(Database db, Transaction tr, string code, Point3d pt)
        {
            string[] parts = code.Split('@');
            if (parts.Length != 2)
            {
                AddMessage("节点引用格式错误：" + code);
                return;
            }

            string obj = parts[0].Trim();
            string nodeName = parts[1].Trim();

            Point3d nodePt;
            if (!_nodes.TryGetValue(nodeName, out nodePt))
            {
                AddMessage(code + " 引用失败：未找到节点 " + nodeName);
                return;
            }

            List<Point3d> oldPath;
            if (_paths.TryGetValue(obj, out oldPath) && oldPath.Count > 1)
            {
                FlushPath(db, tr, _paths, obj, false);
            }

            StartPath(_paths, obj, nodePt);
            AppendPath(_paths, obj, pt);
            AddText(db, tr, CadDrawService.Offset(pt, 2, 2), GetNote(obj) + " 从 " + nodeName + " 引出");
        }

        private void ProcessBury(Database db, Transaction tr, string body, string action, Point3d pt)
        {
            string[] objs = body.Split('&')
                .Select(delegate (string x) { return x.Trim(); })
                .Where(delegate (string x) { return x.Length > 0; })
                .ToArray();

            if (objs.Length < 2)
            {
                AddMessage("并埋代码格式错误：" + body);
                return;
            }

            string main = objs[0];
            string[] attaches = objs.Skip(1).ToArray();

            // 优化点：并埋主对象也遵循 S/E；旧版主对象通常等到文件结束才输出。
            if (action == "S") StartPath(_paths, main, pt);
            else if (action == "E")
            {
                AppendPath(_paths, main, pt);
                FlushPath(db, tr, _paths, main, false);
            }
            else AppendPath(_paths, main, pt);

            foreach (string obj in attaches)
            {
                if (action == "S") StartPath(_buryPaths, obj, pt);
                else if (action == "E")
                {
                    AppendPath(_buryPaths, obj, pt);
                    FlushPath(db, tr, _buryPaths, obj, true);
                }
                else AppendPath(_buryPaths, obj, pt);
            }

            if (action == "S") AddText(db, tr, CadDrawService.Offset(pt, 2, 2), "并埋开始：" + body);
            if (action == "E") AddText(db, tr, CadDrawService.Offset(pt, 2, 2), "并埋结束：" + body);
        }

        private void StartPath(Dictionary<string, List<Point3d>> map, string obj, Point3d pt)
        {
            map[obj] = new List<Point3d> { pt };
        }

        private void AppendPath(Dictionary<string, List<Point3d>> map, string obj, Point3d pt)
        {
            List<Point3d> points;
            if (!map.TryGetValue(obj, out points))
            {
                points = new List<Point3d>();
                map[obj] = points;
            }

            if (points.Count == 0 || !IsSamePoint(points[points.Count - 1], pt))
            {
                points.Add(pt);
            }
        }

        private void FlushPath(Database db, Transaction tr, Dictionary<string, List<Point3d>> map, string obj, bool isBuried)
        {
            List<Point3d> points;
            if (!map.TryGetValue(obj, out points))
            {
                AddMessage(obj + " 没有可输出的路径。 ");
                return;
            }

            if (points.Count >= 2)
            {
                DrawObjPolyline(db, tr, obj, points, isBuried);
            }
            else
            {
                AddMessage(obj + " 路径点数不足，未生成多段线。 ");
            }

            map.Remove(obj);
        }

        private void FlushAllOpenPaths(Database db, Transaction tr)
        {
            foreach (string key in _paths.Keys.ToList())
            {
                List<Point3d> points;
                if (_paths.TryGetValue(key, out points) && points.Count >= 2)
                {
                    DrawObjPolyline(db, tr, key, points, false);
                    AddMessage(key + " 未显式 E 结束，已在文件结束时自动输出开放多段线。 ");
                }
            }
            _paths.Clear();

            foreach (string key in _buryPaths.Keys.ToList())
            {
                List<Point3d> points;
                if (_buryPaths.TryGetValue(key, out points) && points.Count >= 2)
                {
                    DrawObjPolyline(db, tr, key, points, true);
                    AddMessage(key + " 并埋段未显式 E 结束，已在文件结束时自动输出开放多段线。 ");
                }
            }
            _buryPaths.Clear();
        }

        private static bool IsSamePoint(Point3d a, Point3d b)
        {
            return a.DistanceTo(b) < 1e-8;
        }

        private bool IsPointObject(string obj)
        {
            return obj.StartsWith("J", StringComparison.OrdinalIgnoreCase);
        }

        private void DrawObjPolyline(Database db, Transaction tr, string obj, IReadOnlyList<Point3d> points, bool isBuried)
        {
            string layer = isBuried ? GetBuriedLayer(obj) : GetBaseLayer(obj);
            short color = isBuried ? GetBuriedColor(obj) : GetBaseColor(obj);
            string note = isBuried ? GetNote(obj) + "（并埋）" : GetNote(obj);

            ObjectId id = CadDrawService.DrawPolyline(db, tr, points, layer, color);
            if (!id.IsNull) _result.PolylineCount++;

            if (_options.AddNotes)
            {
                Point3d notePt = CadDrawService.GetMidPointAlongPath(points);
                AddText(db, tr, CadDrawService.Offset(notePt, 2, 2), note);
            }
        }

        private void DrawPointObject(Database db, Transaction tr, string obj, Point3d pt)
        {
            string layer = GetBaseLayer(obj);
            ObjectId id = CadDrawService.DrawCircle(db, tr, pt, _options.NodeRadius, layer, GetBaseColor(obj));
            if (!id.IsNull) _result.PointObjectCount++;
            AddText(db, tr, CadDrawService.Offset(pt, 2, 2), GetNote(obj));
        }

        private void DrawNode(Database db, Transaction tr, string nodeName, Point3d pt)
        {
            ObjectId id = CadDrawService.DrawCircle(db, tr, pt, _options.NodeRadius, NodeLayer, 1);
            if (!id.IsNull) _result.PointObjectCount++;
            AddText(db, tr, CadDrawService.Offset(pt, 4, 4), nodeName + " 三通/连接节点");
        }

        private void AddText(Database db, Transaction tr, Point3d pt, string text)
        {
            if (!_options.AddNotes || string.IsNullOrWhiteSpace(text)) return;
            ObjectId id = CadDrawService.DrawText(db, tr, pt, text, _options.TextHeight, NoteLayer, 1);
            if (!id.IsNull) _result.TextCount++;
        }

        private PipeObjectDef GetDef(string obj)
        {
            return _defs.Get(obj);
        }

        private string GetBaseLayer(string obj)
        {
            return GetDef(obj).BaseLayer;
        }

        private short GetBaseColor(string obj)
        {
            return GetDef(obj).BaseColor;
        }

        private string GetBuriedLayer(string obj)
        {
            PipeObjectDef def = GetDef(obj);
            if (string.IsNullOrWhiteSpace(def.BuriedLayer)) return def.BaseLayer + "（并埋）";
            return def.BuriedLayer;
        }

        private short GetBuriedColor(string obj)
        {
            return GetDef(obj).BuriedColor;
        }

        private string GetNote(string obj)
        {
            return GetDef(obj).DefaultNote;
        }

        private void AddMessage(string message)
        {
            _result.Messages.Add(message);
        }
    }
}
