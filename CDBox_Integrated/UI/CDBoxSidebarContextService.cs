using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using TCPipeAutoDraw.Modules.LayerManager;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace TCPipeAutoDraw.UI
{
    internal sealed class CDBoxSidebarField
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    internal sealed class CDBoxSidebarAction
    {
        public string Text { get; set; }
        public string CommandName { get; set; }
    }

    internal sealed class CDBoxSidebarContext
    {
        public CDBoxSidebarContext()
        {
            Fields = new List<CDBoxSidebarField>();
            Actions = new List<CDBoxSidebarAction>();
            Issues = new List<string>();
        }

        public string Signature { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public List<CDBoxSidebarField> Fields { get; private set; }
        public List<CDBoxSidebarAction> Actions { get; private set; }
        public List<string> Issues { get; private set; }
    }

    internal static class CDBoxSidebarContextService
    {
        public static string GetSelectionSignature(Document doc)
        {
            if (doc == null) return "no-document";
            try
            {
                PromptSelectionResult result = doc.Editor.SelectImplied();
                ObjectId[] ids = result.Status == PromptStatus.OK && result.Value != null
                    ? result.Value.GetObjectIds()
                    : new ObjectId[0];
                var parts = new List<string> { doc.Name ?? string.Empty, ids.Length.ToString(CultureInfo.InvariantCulture) };
                for (int i = 0; i < ids.Length; i++) parts.Add(ids[i].IsNull ? "0" : ids[i].Handle.ToString());
                return string.Join("|", parts.ToArray());
            }
            catch
            {
                return "selection-unavailable";
            }
        }

        public static CDBoxSidebarContext ReadCurrent(Document doc)
        {
            var context = new CDBoxSidebarContext { Signature = GetSelectionSignature(doc) };
            if (doc == null)
            {
                context.Title = "未打开图纸";
                context.Subtitle = "CAD 文档不可用";
                context.Issues.Add("请先打开图纸");
                AddAction(context, "打开 Studio", "CDSTUDIO");
                return context;
            }

            ObjectId[] ids;
            try
            {
                PromptSelectionResult result = doc.Editor.SelectImplied();
                ids = result.Status == PromptStatus.OK && result.Value != null
                    ? result.Value.GetObjectIds()
                    : new ObjectId[0];
            }
            catch
            {
                ids = new ObjectId[0];
            }

            if (ids.Length == 0)
            {
                context.Title = "当前未选择对象";
                context.Subtitle = "0 个对象";
                AddAction(context, "图层管理", "TCGL");
                AddAction(context, "属性编辑", "SX");
                AddAction(context, "打开 Studio", "CDSTUDIO");
                return context;
            }

            if (ids.Length > 1)
            {
                context.Title = "已选择 " + ids.Length.ToString(CultureInfo.InvariantCulture) + " 个对象";
                context.Subtitle = "多对象选择";
                AddAction(context, "批量属性", "SX");
                AddAction(context, "管长标注", "GCBZ");
                AddAction(context, "批量断面", "PLDM");
                AddAction(context, "图层管理", "TCGL");
                return context;
            }

            ObjectId id = ids[0];
            try
            {
                QuantityPipeSelectionInfo info = QuantityPipeAttributeService.ReadPipe(doc, id);
                if (info != null)
                {
                    FillQuantityContext(context, info);
                    return context;
                }
            }
            catch
            {
                // 非工程量对象或对象正由 CAD 命令修改时，回退到通用实体摘要。
            }

            FillGenericEntityContext(doc, id, context);
            return context;
        }

        private static void FillQuantityContext(CDBoxSidebarContext context, QuantityPipeSelectionInfo info)
        {
            QuantityPipeAttributes attributes = info.Attributes ?? QuantityPipeAttributes.Default;
            bool isNode = QuantityPipeAttributes.IsNodeKind(attributes.ObjectKind);
            bool isBranch = QuantityPipeAttributes.IsBranchKind(attributes.ObjectKind);

            context.Title = string.IsNullOrWhiteSpace(attributes.ObjectKind) ? "CDBox 对象" : attributes.ObjectKind;
            context.Subtitle = (info.ObjectTypeName ?? "对象") + " · " + (info.LayerName ?? string.Empty);

            if (isNode)
            {
                AddField(context, "编号", attributes.NodeNo);
                AddField(context, "规格", attributes.WellSpec);
                AddField(context, "井型", attributes.WellType);
                AddField(context, "井深", FormatLength(attributes.WellDepth));
                AddAction(context, "编辑属性", "SX");
                AddAction(context, "节点标注", "JDBZ");
            }
            else
            {
                AddField(context, "管材 / 管径", JoinNonEmpty(attributes.Material, attributes.Diameter));
                double length = attributes.UseManualLength ? attributes.ManualLength : info.CadLength;
                AddField(context, "统计长度", FormatLength(length));
                if (isBranch)
                {
                    AddField(context, "支管类型", attributes.BranchType);
                }
                else
                {
                    AddField(context, "起点 / 终点", JoinNonEmpty(attributes.StartNode, attributes.EndNode, " → "));
                    AddField(context, "平均深度", FormatLength(attributes.AverageDepth));
                }
                AddAction(context, "编辑属性", "SX");
                AddAction(context, "管长标注", "GCBZ");
                AddAction(context, "生成断面", "PLDM");
            }

            AddAction(context, "图层管理", "TCGL");
            if (!info.HasSavedAttributes) context.Issues.Add("尚未保存 CDBox 属性");
            if (string.IsNullOrWhiteSpace(attributes.LayerParentGroup)) context.Issues.Add("图层未设置父属性");
        }

        private static void FillGenericEntityContext(Document doc, ObjectId id, CDBoxSidebarContext context)
        {
            context.Title = "CAD 对象";
            context.Subtitle = "当前选择";
            try
            {
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    Entity entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity != null)
                    {
                        context.Title = entity.GetType().Name;
                        context.Subtitle = entity.Layer ?? string.Empty;
                        AddField(context, "图层", entity.Layer);
                        LayerMetadata metadata = LayerManagerService.GetLayerMetadata(doc.Database, tr, entity.Layer);
                        if (metadata != null)
                        {
                            AddField(context, "父属性", metadata.ParentGroup);
                            AddField(context, "分类", metadata.ParentClass);
                            AddField(context, "标签", metadata.TagText);
                            if (metadata.IsEmpty) context.Issues.Add("图层未设置识别信息");
                        }
                    }
                    tr.Commit();
                }
            }
            catch
            {
                context.Issues.Add("对象摘要暂时不可用");
            }
            AddAction(context, "属性编辑", "SX");
            AddAction(context, "图层管理", "TCGL");
        }

        private static void AddField(CDBoxSidebarContext context, string name, string value)
        {
            if (context == null || string.IsNullOrWhiteSpace(value)) return;
            context.Fields.Add(new CDBoxSidebarField { Name = name, Value = value.Trim() });
        }

        private static void AddAction(CDBoxSidebarContext context, string text, string commandName)
        {
            context.Actions.Add(new CDBoxSidebarAction { Text = text, CommandName = commandName });
        }

        private static string FormatLength(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture) + " m";
        }

        private static string JoinNonEmpty(string first, string second)
        {
            return JoinNonEmpty(first, second, " · ");
        }

        private static string JoinNonEmpty(string first, string second, string separator)
        {
            first = first == null ? string.Empty : first.Trim();
            second = second == null ? string.Empty : second.Trim();
            if (first.Length == 0) return second;
            if (second.Length == 0) return first;
            return first + separator + second;
        }
    }
}
