using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI;
using TCPipeAutoDraw.UI.Studio;

namespace TCPipeAutoDraw.Core.Modules
{
    /// <summary>
    /// 基础组件保留的 CAD 会话适配器。它只负责解析图纸和 ObjectId、
    /// 锁定图纸及调用 CAD 编排服务，不包含编辑器页面或 Xrecord 实现。
    /// </summary>
    internal sealed class QuantityAttributeEditorCadAdapter
        : IQuantityAttributeEditorCadService
    {
        public QuantityAttributeCadObject Read(string documentId,
            string objectHandle)
        {
            Document document = ResolveDocument(documentId);
            if (document == null) return Empty("未找到当前图纸。", null);
            if (string.IsNullOrWhiteSpace(objectHandle))
                return Empty("请选择主管、支管或节点/检查井对象。",
                    document);
            return Map(document, QuantityPipeAttributeService.ReadPipe(
                document, ResolveObjectId(document, objectHandle)));
        }

        public QuantityAttributeCadObject Select(string documentId)
        {
            Document document = ResolveDocument(documentId);
            if (document == null) return Empty("未找到当前图纸。", null);
            QuantityPipeSelectionInfo info = QuantityPipeAttributeService
                .SelectQuantityObjectAndRead(document);
            return info == null ? Empty("未选择对象。", document)
                : Map(document, info);
        }

        public QuantityAttributeCadObject Save(string documentId,
            string objectHandle, QuantityPipeAttributes attributes,
            IEnumerable<QuantityStructureLayer> layers)
        {
            Document document = RequiredDocument(documentId);
            ObjectId id = ResolveObjectId(document, objectHandle);
            QuantityPipeSelectionInfo info = RequiredInfo(document, id);
            QuantityPipeAttributes draft = Prepare(attributes, layers, info);
            QuantityDependencyResult normalized = QuantityPipeAttributeService
                .CalculateDraft(document, id, draft, layers, "Save");
            QuantityPipeWriteResult write;
            using (document.LockDocument())
                write = QuantityPipeAttributeService.WritePipeAttributes(
                    document, id, normalized.Attributes);
            if (!write.Success)
                throw new InvalidOperationException(write.Message);
            QuantityAttributeCadObject result = Map(document,
                QuantityPipeAttributeService.ReadPipe(document, id));
            result.Message = write.Message;
            QuantityDashboardLiveMonitor.MarkDirty(document,
                "quantity-attribute-saved");
            return result;
        }

        public QuantityDependencyResult CalculateDraft(string documentId,
            string objectHandle, QuantityPipeAttributes attributes,
            IEnumerable<QuantityStructureLayer> layers,
            string changedField, QuantityPipeAttributes previousDraft)
        {
            Document document = RequiredDocument(documentId);
            ObjectId id = ResolveObjectId(document, objectHandle);
            QuantityPipeSelectionInfo info = RequiredInfo(document, id);
            QuantityPipeAttributes draft = Prepare(attributes, layers, info);
            return QuantityPipeAttributeService.CalculateDraft(document, id,
                draft, layers, changedField, previousDraft);
        }

        public QuantityAttributeCadObject Refresh(string documentId,
            string objectHandle, QuantityPipeAttributes attributes)
        {
            return Transform(documentId, objectHandle, attributes,
                delegate(Document document, ObjectId id,
                    QuantityPipeAttributes draft)
                {
                    using (document.LockDocument())
                        return QuantityPipeAttributeService
                            .RefreshAttributesForObject(document, id, draft);
                }, "已刷新自动识别规格、节点和派生数值。");
        }

        public QuantityAttributeCadObject ReloadDefault(string documentId,
            string objectHandle, QuantityPipeAttributes attributes)
        {
            return Transform(documentId, objectHandle, attributes,
                delegate(Document document, ObjectId id,
                    QuantityPipeAttributes draft)
                {
                    using (document.LockDocument())
                        return QuantityPipeAttributeService
                            .LoadDefaultProfileForObject(document, id,
                                draft.ObjectKind, draft);
                }, "已按当前对象类型重新载入默认表。");
        }

        public QuantityAttributeCadObject SelectConnectedNode(
            string documentId, string objectHandle,
            QuantityPipeAttributes attributes, bool forStart)
        {
            return Transform(documentId, objectHandle, attributes,
                delegate(Document document, ObjectId id,
                    QuantityPipeAttributes draft)
                {
                    return QuantityPipeAttributeService
                        .SelectConnectedNodeForMainPipe(document, id, draft,
                            forStart);
                }, forStart ? "已更新起点井。" : "已更新终点井。");
        }

        public void OpenLegacy(string documentId, string objectHandle)
        {
            Document document = RequiredDocument(documentId);
            QuantityPipeSelectionInfo info = RequiredInfo(document,
                ResolveObjectId(document, objectHandle));
            using (Form form = QuantityAttributeEditorFormFactory.Create(
                document, info))
                form.ShowDialog(new AcadMainWindow());
        }

        private static QuantityAttributeCadObject Transform(string documentId,
            string objectHandle, QuantityPipeAttributes attributes,
            Func<Document, ObjectId, QuantityPipeAttributes,
                QuantityPipeAttributes> action, string message)
        {
            Document document = RequiredDocument(documentId);
            ObjectId id = ResolveObjectId(document, objectHandle);
            QuantityPipeSelectionInfo info = RequiredInfo(document, id);
            QuantityPipeAttributes draft = attributes == null
                ? info.Attributes.Clone() : attributes.Clone();
            draft.ObjectKind = info.InferredKind;
            QuantityPipeAttributes next = action(document, id, draft)
                ?? draft;
            info.Attributes = next;
            QuantityAttributeCadObject result = Map(document, info);
            result.Message = message;
            return result;
        }

        private static QuantityPipeAttributes Prepare(
            QuantityPipeAttributes attributes,
            IEnumerable<QuantityStructureLayer> layers,
            QuantityPipeSelectionInfo info)
        {
            QuantityPipeAttributes draft = attributes == null
                ? info.Attributes.Clone() : attributes.Clone();
            draft.ObjectKind = info.InferredKind;
            if (layers != null)
                draft.BackfillStructure = QuantityStructureLayer.Serialize(
                    layers, QuantityPipeAttributes.IsNodeKind(
                        draft.ObjectKind));
            return draft;
        }

        private static QuantityPipeSelectionInfo RequiredInfo(
            Document document, ObjectId id)
        {
            QuantityPipeSelectionInfo info = QuantityPipeAttributeService
                .ReadPipe(document, id);
            if (info == null)
                throw new InvalidOperationException(
                    "对象已删除或不再支持属性编辑。");
            return info;
        }

        private static QuantityAttributeCadObject Map(Document document,
            QuantityPipeSelectionInfo info)
        {
            if (info == null)
                return Empty("所选对象无法识别为主管、支管或节点/检查井。",
                    document);
            return new QuantityAttributeCadObject
            {
                Selected = true,
                DocumentId = QuantityDashboardService.GetDocumentId(document),
                DocumentName = document == null ? string.Empty : document.Name,
                Handle = info.HandleText,
                LayerName = info.LayerName,
                ObjectTypeName = info.ObjectTypeName,
                InferredKind = info.InferredKind,
                CadLength = info.CadLength,
                HasSavedAttributes = info.HasSavedAttributes,
                Message = info.HasSavedAttributes
                    ? "已读取对象现有属性。"
                    : "对象尚未保存属性，已套用默认表。",
                Attributes = info.Attributes == null
                    ? QuantityPipeAttributes.DefaultForKind(info.InferredKind)
                    : info.Attributes.Clone()
            };
        }

        private static QuantityAttributeCadObject Empty(string message,
            Document document)
        {
            return new QuantityAttributeCadObject
            {
                Selected = false,
                DocumentId = QuantityDashboardService.GetDocumentId(document),
                DocumentName = document == null ? string.Empty : document.Name,
                Message = message ?? string.Empty
            };
        }

        private static Document ResolveDocument(string documentId)
        {
            return QuantityDashboardService.ResolveDocument(documentId);
        }

        private static Document RequiredDocument(string documentId)
        {
            Document document = ResolveDocument(documentId);
            if (document == null)
                throw new InvalidOperationException("目标图纸已关闭。");
            return document;
        }

        private static ObjectId ResolveObjectId(Document document,
            string objectHandle)
        {
            long value;
            if (document == null || !long.TryParse(
                (objectHandle ?? string.Empty).Trim(), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out value))
                throw new InvalidOperationException("对象句柄无效。");
            try
            {
                return document.Database.GetObjectId(false,
                    new Handle(value), 0);
            }
            catch
            {
                throw new InvalidOperationException(
                    "对象已删除或不属于目标图纸。");
            }
        }
    }
}
