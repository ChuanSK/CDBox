using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace TCPipeAutoDraw.Modules.SurfaceAreaAnnotation
{
    /// <summary>
    /// 表面积成果实体的专用元数据写入。只有通过污水业务的表面积标注
    /// 命令生成的实体才写入该记录，并可进入统一标注浮窗。
    /// </summary>
    internal static class SurfaceAreaAnnotationMetadataService
    {
        private const string RecordName = "CDBoxSimpleAnnotation";

        public static void Attach(Database database, Transaction transaction,
            ObjectId boundaryObjectId, ObjectId textObjectId,
            ObjectId leaderObjectId)
        {
            if (database == null || transaction == null
                || textObjectId.IsNull) return;
            string annotationId = Guid.NewGuid().ToString("D");
            string sourceHandle = ReadHandle(boundaryObjectId);
            Write(transaction, textObjectId, annotationId, "MainText",
                sourceHandle);
            if (!leaderObjectId.IsNull)
                Write(transaction, leaderObjectId, annotationId, "Leader",
                    sourceHandle);
        }

        private static void Write(Transaction transaction, ObjectId objectId,
            string annotationId, string role, string sourceHandle)
        {
            Entity entity = transaction.GetObject(objectId,
                OpenMode.ForWrite, false) as Entity;
            if (entity == null) return;
            if (entity.ExtensionDictionary.IsNull)
                entity.CreateExtensionDictionary();
            DBDictionary dictionary = transaction.GetObject(
                entity.ExtensionDictionary, OpenMode.ForWrite, false)
                as DBDictionary;
            if (dictionary == null) return;
            Xrecord record;
            if (dictionary.Contains(RecordName))
                record = transaction.GetObject(dictionary.GetAt(RecordName),
                    OpenMode.ForWrite, false) as Xrecord;
            else
            {
                record = new Xrecord();
                dictionary.SetAt(RecordName, record);
                transaction.AddNewlyCreatedDBObject(record, true);
            }
            if (record == null) return;
            record.Data = new ResultBuffer(
                new TypedValue((int)DxfCode.Text, "Version=1"),
                new TypedValue((int)DxfCode.Text, "Kind=SurfaceArea"),
                new TypedValue((int)DxfCode.Text,
                    "AnnotationId=" + annotationId),
                new TypedValue((int)DxfCode.Text, "Role=" + role),
                new TypedValue((int)DxfCode.Text,
                    "SourceHandle=" + sourceHandle));
        }

        private static string ReadHandle(ObjectId objectId)
        {
            if (objectId.IsNull) return string.Empty;
            try { return objectId.Handle.ToString(); }
            catch { return string.Empty; }
        }
    }
}
