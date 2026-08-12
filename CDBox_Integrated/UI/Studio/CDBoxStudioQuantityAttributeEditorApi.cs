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
            QuantityPipeAttributes attrs = PrepareAttributes(request, info);
            QuantityDependencyResult normalized = QuantityPipeAttributeService.CalculateDraft(doc, id, attrs, request.layers, "Save");
            QuantityPipeWriteResult write;
            using (doc.LockDocument()) write = QuantityPipeAttributeService.WritePipeAttributes(doc, id, normalized.Attributes);
            if (!write.Success) throw new InvalidOperationException(write.Message);
            CDBoxStudioQuantityAttributeEditorContext context = FromInfo(doc, QuantityPipeAttributeService.ReadPipe(doc, id));
            context.message = write.Message;
            context.requestId = -1;
            QuantityDashboardLiveMonitor.MarkDirty(doc,
                "quantity-attribute-saved");
            return context;
        }

        public static CDBoxStudioQuantityAttributeEditorContext CalculateDraft(string payload)
        {
            CDBoxStudioQuantityAttributeEditorRequest request = Required(payload);
            Document doc = RequiredDocument(request);
            ObjectId id = ResolveObjectId(doc, request.handle);
            QuantityPipeSelectionInfo info = QuantityPipeAttributeService.ReadPipe(doc, id);
            if (info == null) throw new InvalidOperationException("对象已删除或不再支持属性编辑。");
            QuantityPipeAttributes attrs = PrepareAttributes(request, info);

            if (string.Equals(request.changedField, "SwapEndpoints", StringComparison.OrdinalIgnoreCase))
            {
                string node = attrs.StartNode;
                double depth = attrs.StartDepth;
                attrs.StartNode = attrs.EndNode;
                attrs.StartDepth = attrs.EndDepth;
                attrs.EndNode = node;
                attrs.EndDepth = depth;
            }

            QuantityDependencyResult normalized = QuantityPipeAttributeService.CalculateDraft(
                doc, id, attrs, request.layers, request.changedField, request.attributes);
            CDBoxStudioQuantityAttributeEditorContext context = FromDraft(doc, info, normalized);
            context.message = "派生值已联动更新。";
            context.requestId = request.requestId;
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
                using (doc.LockDocument()) return QuantityPipeAttributeService.LoadDefaultProfileForObject(doc, id, attrs.ObjectKind, attrs);
            }, "已按当前对象类型重新载入默认表。");
        }

        public static CDBoxStudioQuantityAttributeEditorContext SelectNode(string payload)
        {
            CDBoxStudioQuantityAttributeEditorRequest request = Required(payload);
            FormWindowState? previousState = CDBoxStudioQuantityAttributeEditorWindow.MinimizeForCadSelection();
            try
            {
                return Transform(payload, delegate(Document doc, ObjectId id, QuantityPipeAttributes attrs)
                {
                    return QuantityPipeAttributeService.SelectConnectedNodeForMainPipe(doc, id, attrs, request.forStart);
                }, request.forStart ? "已更新起点井。" : "已更新终点井。");
            }
            finally
            {
                CDBoxStudioQuantityAttributeEditorWindow.RestoreAfterCadSelection(previousState);
            }
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
            QuantityPipeAttributes attrs = PrepareAttributes(request, info);
            QuantityPipeAttributes next = action(doc, id, attrs) ?? attrs;
            info.Attributes = next;
            CDBoxStudioQuantityAttributeEditorContext context = FromInfo(doc, info);
            context.message = message;
            context.requestId = request.requestId;
            return context;
        }

        private static QuantityPipeAttributes PrepareAttributes(CDBoxStudioQuantityAttributeEditorRequest request, QuantityPipeSelectionInfo info)
        {
            QuantityPipeAttributes attrs = request.attributes == null ? info.Attributes.Clone() : request.attributes.Clone();
            attrs.ObjectKind = info.InferredKind;
            if (request.layers != null)
            {
                attrs.BackfillStructure = QuantityStructureLayer.Serialize(request.layers, QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind));
            }
            return attrs;
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
            QuantityDependencyResult normalized = QuantityPipeAttributeService.CalculateDraft(
                doc, info.ObjectId, attrs, QuantityStructureLayer.Parse(attrs.BackfillStructure), "Load");
            return FromDraft(doc, info, normalized);
        }

        private static CDBoxStudioQuantityAttributeEditorContext FromDraft(Document doc, QuantityPipeSelectionInfo info, QuantityDependencyResult normalized)
        {
            QuantityPipeAttributes attrs = normalized == null || normalized.Attributes == null
                ? (info.Attributes ?? QuantityPipeAttributes.DefaultForKind(info.InferredKind)).Clone()
                : normalized.Attributes.Clone();
            info.Attributes = attrs;
            object calculation = QuantityPipeAttributes.IsNodeKind(attrs.ObjectKind)
                ? (object)QuantityCalculationReportService.BuildDashboardWellRow(info, attrs, 1)
                : QuantityCalculationReportService.BuildDashboardPipeRow(
                    info, attrs, 1, null, QuantityPipeAttributes.IsBranchKind(attrs.ObjectKind));
            return new CDBoxStudioQuantityAttributeEditorContext
            {
                selected = true, documentId = QuantityDashboardService.GetDocumentId(doc), documentName = doc == null ? string.Empty : doc.Name,
                handle = info.HandleText, layerName = info.LayerName, objectTypeName = info.ObjectTypeName, inferredKind = info.InferredKind,
                cadLength = info.CadLength, effectiveLength = attrs.EffectiveLength(info.CadLength), hasSavedAttributes = info.HasSavedAttributes,
                message = info.HasSavedAttributes ? "已读取对象现有属性。" : "对象尚未保存属性，已套用默认表。", attributes = attrs,
                layers = normalized == null ? QuantityStructureLayer.Parse(attrs.BackfillStructure) : normalized.Layers,
                warnings = normalized == null ? new System.Collections.Generic.List<string>() : normalized.Warnings,
                calculation = calculation,
                realExcavationDepth = normalized == null ? 0.0 : normalized.RealExcavationDepth
            };
        }

        private static CDBoxStudioQuantityAttributeEditorContext Empty(string message, Document doc = null)
        {
            return new CDBoxStudioQuantityAttributeEditorContext { selected = false, documentId = QuantityDashboardService.GetDocumentId(doc), documentName = doc == null ? string.Empty : doc.Name, message = message };
        }
    }
}
