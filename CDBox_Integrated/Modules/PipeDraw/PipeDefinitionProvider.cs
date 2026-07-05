using System;
using System.Collections.Generic;

namespace TCPipeAutoDraw.Modules.PipeDraw
{
    internal sealed class PipeDefinitionProvider
    {
        private readonly Dictionary<string, PipeObjectDef> _defs;
        private readonly HashSet<string> _unknownReported;
        private readonly IList<string> _messages;

        public PipeDefinitionProvider(IList<string> messages)
        {
            _messages = messages;
            _unknownReported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _defs = new Dictionary<string, PipeObjectDef>(StringComparer.OrdinalIgnoreCase);
            AddDefaults();
        }

        public PipeObjectDef Get(string obj)
        {
            PipeObjectDef def;
            if (_defs.TryGetValue(obj, out def)) return def;

            if (!_unknownReported.Contains(obj))
            {
                _unknownReported.Add(obj);
                if (_messages != null) _messages.Add("对象码未定义：" + obj + "，已使用临时图层。建议后续改为配置文件维护。 ");
            }
            return new PipeObjectDef(obj, obj, "未定义_" + obj, 7, string.Empty, 1, obj);
        }

        private void AddDefaults()
        {
            Add(new PipeObjectDef("Y1", "110雨水管", "110PVC管（雨水管）", 7, string.Empty, 1, "110雨水管"));
            Add(new PipeObjectDef("H1", "110砼恢复", "110PVC管（砼恢复）", 5, string.Empty, 1, "110砼恢复"));
            Add(new PipeObjectDef("P1", "110PVC管", "110PVC管（明管）", 4, "110PVC管（并埋）", 1, "110PVC管"));
            Add(new PipeObjectDef("M1", "110明管", "110PVC管（明管）", 4, "110PVC管（并埋）", 1, "110PVC管"));
            Add(new PipeObjectDef("B2", "200波纹管", "200波纹管（雨水管）", 7, string.Empty, 3, "200波纹管"));
            Add(new PipeObjectDef("B3", "200砼恢复", "200波纹管（砼恢复）", 3, string.Empty, 1, "200波纹管砼恢复"));
            Add(new PipeObjectDef("J1", "315井", "315小井（混凝土）", 4, string.Empty, 4, "315井"));
        }

        private void Add(PipeObjectDef def)
        {
            _defs[def.ObjCode] = def;
        }
    }
}
