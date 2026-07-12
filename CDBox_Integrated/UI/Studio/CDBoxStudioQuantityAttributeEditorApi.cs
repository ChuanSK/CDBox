using System;
using System.Globalization;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioQuantityAttributeEditorApi
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 128 };

        public static string Serialize(object value) { return Serializer.Serialize(value); }
        public static T Deserialize<T>(string value) where T : class { return string.IsNullOrWhiteSpace(value) ? null : Serializer.Deserialize<T>(value); }

        public static CDBoxStudioQuantityAttributeEditorContext GetContext(string payload)
        {
            CDBoxStudioQuantityAttributeEditorRequest request = Deserialize<CDBoxStudioQuantityAttributeEditorRequest>(payload) ?? new CDBoxStudioQuantityAttributeEditorRequest();
            Document doc = QuantityDashboardService.ResolveDocument(request.documentId);
            if (doc == null) return Empty("未找到当前图纸。");
            if (string.IsNullOrWhiteSpace(request.handle)) return Empty("请选择主管、支管或节点/检查井对象。", doc);
            return FromInfo(doc, QuantityPipeAttributeService.ReadPipe(doc, ResolveObjectId(doc, request.handle)));
        }

        public static CDBoxStudioQuantityAttributeEditorContext Select(string payload)
        {
            CDBoxStudioQuantityAttributeEditorRequest request = Deserialize<CDBoxStudioQuantityAttributeEditorRequest>(payload) ?? new CDBoxStudioQuantityAttributeEditorRequest();
            Document doc = QuantityDashboardService.ResolveDocument(request.documentId);
            if (doc == null) return Empty("未找到当前图纸。");
            QuantityPipeSelectionInfo info = QuantityPipeAttributeService.SelectQuantityObjectAndRead(doc);
            return info == null ? Empty("未选择对象。", doc) : FromInfo(doc, info);
        }

        public static CDBoxStudioQuantityAttributeEditorContext Save(string payload)
        {
            CDBoxStudioQuantityAttributeEditorRequest request = Required(payload);
            Document doc = RequiredDocument(request);
            ObjectId id = ResolveObjectId(doc, request.handle);
            QuantityPipeSelectionInfo info = QuantityPipeAttributeService.ReadPipe(doc, id);
            if (info == null) throw new InvalidOperationException("对象已删除或不再支持属性编辑。");
            QuantityPipeAttributes attrs = request.attributes == null ? info.Attributes.Clone() : request.attributes.Clone();
            attrs.ObjectKind = info.InferredKind;
            attrs.BackfillStructure = QuantityPipeAttributes.NormalizeStructureLayerText(attrs.BackfillStructure);
            QuantityPipeAttributes.ApplyStructureLayerText(attrs);
            if (QuantityPipeAttributes.IsMainPipeKind(attrs.ObjectKind) && (attrs.StartDepth > 0 || attrs.EndDepth > 0))
            {
                attrs.AverageDepth = attrs.StartDepth > 0 && attrs.EndDepth > 0
                    ? (attrs.StartDepth + attrs.EndDepth) / 2.0
                    : Math.Max(attrs.StartDepth, attrs.EndDepth);
            }
            QuantityPipeWriteResult write;
            using (doc.LockDocument()) write = QuantityPipeAttributeService.WritePipeAttributes(doc, id, attrs);
            if (!write.Success) throw new InvalidOperationException(write.Message);
            CDBoxStudioQuantityAttributeEditorContext context = FromInfo(doc, QuantityPipeAttributeService.ReadPipe(doc, id));
            context.message = write.Message;
            QuantityDashboardLiveMonitor.MarkDirty("quantity-attribute-saved");
            return context;
        }

        public static CDBoxStudioQuantityAttributeEditorContext Refresh(string payload)
        {
            return Transform(payload, delegate(Document doc, ObjectId id, QuantityPipeAttributes attrs)
            {
                using (doc.LockDocument()) return QuantityPipeAttributeService.RefreshAttributesForObject(doc, id, attrs);
            }, "已刷新自动识别规格、节点和派生数值。");
        }

        public static CDBoxStudioQuantityAttributeEditorContext ReloadDefault(string payload)
        {
            return Transform(payload, delegate(Document doc, ObjectId id, QuantityPipeAttributes attrs)
            {
                using (doc.LockDocument()) return QuantityPipeAttributeService.LoadDefaultProfileForObject(doc, id, attrs.ObjectKind);
            }, "已按当前对象类型重新载入默认表。");
        }

        public static CDBoxStudioQuantityAttributeEditorContext SelectNode(string payload)
        {
            CDBoxStudioQuantityAttributeEditorRequest request = Required(payload);
            return Transform(payload, delegate(Document doc, ObjectId id, QuantityPipeAttributes attrs)
            {
                return QuantityPipeAttributeService.SelectConnectedNodeForMainPipe(doc, id, attrs, request.forStart);
            }, request.forStart ? "已更新起点井。" : "已更新终点井。");
        }

        public static void OpenLegacy(string payload)
        {
            CDBoxStudioQuantityAttributeEditorRequest request = Required(payload);
            Document doc = RequiredDocument(request);
            QuantityPipeSelectionInfo info = QuantityPipeAttributeService.ReadPipe(doc, ResolveObjectId(doc, request.handle));
            if (info == null) throw new InvalidOperationException("未找到可编辑对象。");
            using (Form form = QuantityAttributeEditorFormFactory.Create(doc, info)) form.ShowDialog(new AcadMainWindow());
        }

        private static CDBoxStudioQuantityAttributeEditorContext Transform(string payload, Func<Document, ObjectId, QuantityPipeAttributes, QuantityPipeAttributes> action, string message)
        {
            CDBoxStudioQuantityAttributeEditorRequest request = Required(payload);
            Document doc = RequiredDocument(request);
            ObjectId id = ResolveObjectId(doc, request.handle);
            QuantityPipeSelectionInfo info = QuantityPipeAttributeService.ReadPipe(doc, id);
            if (info == null) throw new InvalidOperationException("对象已删除或不再支持属性编辑。");
            QuantityPipeAttributes attrs = request.attributes == null ? info.Attributes.Clone() : request.attributes.Clone();
            attrs.ObjectKind = info.InferredKind;
            QuantityPipeAttributes next = action(doc, id, attrs) ?? attrs;
            info.Attributes = next;
            CDBoxStudioQuantityAttributeEditorContext context = FromInfo(doc, info);
            context.message = message;
            return context;
        }

        private static CDBoxStudioQuantityAttributeEditorRequest Required(string payload)
        {
            CDBoxStudioQuantityAttributeEditorRequest request = Deserialize<CDBoxStudioQuantityAttributeEditorRequest>(payload);
            if (request == null || string.IsNullOrWhiteSpace(request.handle)) throw new InvalidOperationException("请先选择对象。");
            return request;
        }

        private static Document RequiredDocument(CDBoxStudioQuantityAttributeEditorRequest request)
        {
            Document doc = QuantityDashboardService.ResolveDocument(request.documentId);
            if (doc == null) throw new InvalidOperationException("目标图纸已关闭。");
            return doc;
        }

        private static ObjectId ResolveObjectId(Document doc, string handle)
        {
            long value;
            if (doc == null || !long.TryParse((handle ?? string.Empty).Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) throw new InvalidOperationException("对象句柄无效。");
            try { return doc.Database.GetObjectId(false, new Handle(value), 0); }
            catch { throw new InvalidOperationException("对象已删除或不属于目标图纸。"); }
        }

        public static CDBoxStudioQuantityAttributeEditorContext FromInfo(Document doc, QuantityPipeSelectionInfo info)
        {
            if (info == null) return Empty("所选对象无法识别为主管、支管或节点/检查井。", doc);
            QuantityPipeAttributes attrs = info.Attributes == null ? QuantityPipeAttributes.DefaultForKind(info.InferredKind) : info.Attributes.Clone();
            return new CDBoxStudioQuantityAttributeEditorContext
            {
                selected = true, documentId = QuantityDashboardService.GetDocumentId(doc), documentName = doc == null ? string.Empty : doc.Name,
                handle = info.HandleText, layerName = info.LayerName, objectTypeName = info.ObjectTypeName, inferredKind = info.InferredKind,
                cadLength = info.CadLength, effectiveLength = attrs.EffectiveLength(info.CadLength), hasSavedAttributes = info.HasSavedAttributes,
                message = info.HasSavedAttributes ? "已读取对象现有属性。" : "对象尚未保存属性，已套用默认表。", attributes = attrs
            };
        }

        private static CDBoxStudioQuantityAttributeEditorContext Empty(string message, Document doc = null)
        {
            return new CDBoxStudioQuantityAttributeEditorContext { selected = false, documentId = QuantityDashboardService.GetDocumentId(doc), documentName = doc == null ? string.Empty : doc.Name, message = message };
        }
    }
}
