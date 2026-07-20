using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace TCPipeAutoDraw.Modules.SectionDrawing
{
    internal static class SectionDrawingBindingService
    {
        internal const string SourceHandlesRecordName = "CDBoxSectionSourceHandles";
        internal const string GeneratedHandlesRecordName = "CDBoxGeneratedSectionHandles";

        public static void Bind(Transaction tr, IEnumerable<ObjectId> sourceIds, IEnumerable<ObjectId> generatedIds)
        {
            if (tr == null) return;
            List<ObjectId> sources = Normalize(sourceIds);
            List<ObjectId> generated = Normalize(generatedIds);
            if (sources.Count == 0 || generated.Count == 0) return;

            List<string> sourceHandles = ToHandles(sources);
            List<string> generatedHandles = ToHandles(generated);
            foreach (ObjectId id in generated)
            {
                TryWriteHandles(tr, id, SourceHandlesRecordName, sourceHandles, false);
            }
            foreach (ObjectId id in sources)
            {
                TryWriteHandles(tr, id, GeneratedHandlesRecordName, generatedHandles, true);
            }
        }

        private static void TryWriteHandles(Transaction tr, ObjectId id, string recordName, IEnumerable<string> handles, bool merge)
        {
            try
            {
                DBObject owner = tr.GetObject(id, OpenMode.ForWrite, false);
                WriteHandles(owner, tr, recordName, handles, merge);
            }
            catch
            {
                // 锁定图层等只影响反向索引，不能导致整张断面回滚。
            }
        }

        private static void WriteHandles(DBObject owner, Transaction tr, string recordName, IEnumerable<string> handles, bool merge)
        {
            if (owner == null || tr == null || string.IsNullOrWhiteSpace(recordName)) return;
            if (owner.ExtensionDictionary.IsNull) owner.CreateExtensionDictionary();
            DBDictionary dictionary = tr.GetObject(owner.ExtensionDictionary, OpenMode.ForWrite, false) as DBDictionary;
            if (dictionary == null) return;

            Xrecord record;
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (dictionary.Contains(recordName))
            {
                record = tr.GetObject(dictionary.GetAt(recordName), OpenMode.ForWrite, false) as Xrecord;
                if (merge && record != null && record.Data != null)
                {
                    foreach (TypedValue value in record.Data)
                    {
                        string text = value.Value == null ? string.Empty : value.Value.ToString();
                        if (text.StartsWith("Handle=", StringComparison.OrdinalIgnoreCase)) values.Add(text.Substring(7));
                    }
                }
            }
            else
            {
                record = new Xrecord();
                dictionary.SetAt(recordName, record);
                tr.AddNewlyCreatedDBObject(record, true);
            }
            if (record == null) return;

            foreach (string handle in handles)
            {
                if (!string.IsNullOrWhiteSpace(handle)) values.Add(handle.Trim());
            }
            var data = new List<TypedValue> { new TypedValue((int)DxfCode.Text, "SchemaVersion=1") };
            foreach (string handle in values) data.Add(new TypedValue((int)DxfCode.Text, "Handle=" + handle));
            record.Data = new ResultBuffer(data.ToArray());
        }

        private static List<ObjectId> Normalize(IEnumerable<ObjectId> ids)
        {
            var result = new List<ObjectId>();
            var seen = new HashSet<ObjectId>();
            if (ids == null) return result;
            foreach (ObjectId id in ids)
            {
                if (id.IsNull || !id.IsValid || id.IsErased || !seen.Add(id)) continue;
                result.Add(id);
            }
            return result;
        }

        private static List<string> ToHandles(IEnumerable<ObjectId> ids)
        {
            var result = new List<string>();
            foreach (ObjectId id in ids) result.Add(id.Handle.ToString());
            return result;
        }
    }
}
