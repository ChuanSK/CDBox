using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace TCPipeAutoDraw.Core.Cad
{
    /// <summary>
    /// 公共图层服务。管线绘制、图层管理、后续注记/断面/工程量模块都可以复用。
    /// </summary>
    public static class CadLayerService
    {
        public static ObjectId EnsureLayer(Database db, Transaction tr, string layerName, short colorIndex)
        {
            if (db == null) throw new ArgumentNullException("db");
            if (tr == null) throw new ArgumentNullException("tr");
            if (string.IsNullOrWhiteSpace(layerName)) throw new ArgumentException("图层名不能为空。", "layerName");

            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(layerName)) return lt[layerName];

            lt.UpgradeOpen();
            var layer = new LayerTableRecord();
            layer.Name = layerName;
            layer.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex <= 0 ? (short)7 : colorIndex);
            ObjectId id = lt.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
            return id;
        }

        public static string GetCurrentLayerName(Database db, Transaction tr)
        {
            try
            {
                LayerTableRecord current = (LayerTableRecord)tr.GetObject(db.Clayer, OpenMode.ForRead);
                return current.Name;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static List<string> NormalizeLayerNames(IEnumerable<string> layerNames)
        {
            var output = new List<string>();
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (layerNames == null) return output;

            foreach (string raw in layerNames)
            {
                if (raw == null) continue;
                string name = raw.Trim();
                if (name.Length == 0) continue;
                if (set.Contains(name)) continue;
                set.Add(name);
                output.Add(name);
            }
            return output;
        }
    }
}
