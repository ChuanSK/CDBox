using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using CDBox.Shared.UI;
using CDBox.Shared.Services;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace CDBox.Wastewater.UI
{
    /// <summary>
    /// 属性编辑器页面、JSON 路由与业务联动控制器。所有页面行为归污水
    /// 程序集所有；基础组件只通过 IQuantityAttributeEditorCadService
    /// 提供不透明的 CAD 会话操作。
    /// </summary>
    public sealed class WastewaterQuantityAttributeEditorController
    {
        public const string PageId = "quantity-attribute-editor";
        private readonly IQuantityAttributeEditorCadService _cad;
        private readonly ICDBoxPageAppearanceService _appearance;
        private readonly ICDBoxLogger _logger;
        private readonly JavaScriptSerializer _serializer;
        private string _documentId;
        private string _objectHandle;

        public WastewaterQuantityAttributeEditorController(
            IQuantityAttributeEditorCadService cad,
            ICDBoxPageAppearanceService appearance,
            ICDBoxLogger logger)
        {
            _cad = cad ?? throw new ArgumentNullException("cad");
            _appearance = appearance
                ?? throw new ArgumentNullException("appearance");
            _logger = logger ?? throw new ArgumentNullException("logger");
            _serializer = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 128
            };
            _documentId = string.Empty;
            _objectHandle = string.Empty;
        }

        public void SetTarget(string documentId, string objectHandle)
        {
            _documentId = documentId ?? string.Empty;
            _objectHandle = objectHandle ?? string.Empty;
        }

        public CDBoxPageDefinition CreatePage()
        {
            return new CDBoxPageDefinition(PageId, "属性编辑器 · 5.1.0",
                BuildDocument, Route)
            {
                Width = 1180,
                Height = 820,
                MinimumWidth = 900,
                MinimumHeight = 650
            };
        }

        private string BuildDocument()
        {
            return WastewaterQuantityAttributeEditorPage
                .BuildStandaloneDocument(_appearance.GetAppearance(),
                    _documentId, _objectHandle);
        }

        private CDBoxPageRouteResult Route(CDBoxPageRouteRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return new CDBoxPageRouteResult { Handled = false };
            string name = request.Name.Trim().ToLowerInvariant();
            if (name == "ready")
                return Result(null, "属性编辑器已就绪", "success");
            if (name == "quantityattributeeditorpageerror")
            {
                _logger.Warn("属性编辑器页面异常：" + request.Argument);
                return Result(null, "属性编辑器页面异常，已写入日志",
                    "error");
            }

            try
            {
                if (name == "selectquantityattributeobject")
                    return Interaction(delegate
                    {
                        WastewaterQuantityAttributeEditorRequest input =
                            ReadOptional(request.Argument);
                        return ContextResult(FromCad(
                            _cad.Select(input.documentId),
                            "未选择对象。"), "info", false);
                    });
                if (name == "selectquantityattributenode")
                    return Interaction(delegate
                    {
                        WastewaterQuantityAttributeEditorRequest input =
                            ReadRequired(request.Argument);
                        QuantityPipeAttributes attributes = Prepare(input);
                        return ContextResult(FromCad(
                            _cad.SelectConnectedNode(input.documentId,
                                input.handle, attributes, input.forStart),
                            string.Empty, input.requestId), "info", false);
                    });
                if (name == "openlegacyquantityattributeeditor")
                    return Interaction(delegate
                    {
                        WastewaterQuantityAttributeEditorRequest input =
                            ReadRequired(request.Argument);
                        _cad.OpenLegacy(input.documentId, input.handle);
                        return Result(null, "旧版属性编辑器已关闭", "info");
                    });

                WastewaterQuantityAttributeEditorContext context;
                if (name == "getquantityattributecontext")
                {
                    WastewaterQuantityAttributeEditorRequest input =
                        ReadOptional(request.Argument);
                    context = FromCad(_cad.Read(input.documentId,
                        input.handle), string.Empty);
                }
                else if (name == "savequantityattributes")
                {
                    WastewaterQuantityAttributeEditorRequest input =
                        ReadRequired(request.Argument);
                    context = FromCad(_cad.Save(input.documentId,
                        input.handle, Prepare(input), input.layers),
                        string.Empty, -1);
                }
                else if (name == "calculatequantitydraft")
                    context = Calculate(request.Argument);
                else if (name == "refreshquantityattributes")
                {
                    WastewaterQuantityAttributeEditorRequest input =
                        ReadRequired(request.Argument);
                    context = FromCad(_cad.Refresh(input.documentId,
                        input.handle, Prepare(input)), string.Empty,
                        input.requestId);
                }
                else if (name == "reloadquantityattributedefault")
                {
                    WastewaterQuantityAttributeEditorRequest input =
                        ReadRequired(request.Argument);
                    context = FromCad(_cad.ReloadDefault(input.documentId,
                        input.handle, Prepare(input)), string.Empty,
                        input.requestId);
                }
                else return new CDBoxPageRouteResult { Handled = false };

                return ContextResult(context,
                    name == "savequantityattributes" ? "success" : "info",
                    name == "calculatequantitydraft");
            }
            catch (Exception ex)
            {
                _logger.Error("属性编辑器路由失败：" + request.Name, ex);
                return new CDBoxPageRouteResult
                {
                    Handled = true,
                    ToastKind = "error",
                    ToastMessage = ex.Message,
                    ExecuteScript =
                        "window.CDBoxQuantityAttributeEditorFailed && "
                        + "window.CDBoxQuantityAttributeEditorFailed("
                        + Js(ex.Message) + ");"
                };
            }
        }

        private CDBoxPageRouteResult Interaction(
            Func<CDBoxPageRouteResult> action)
        {
            return new CDBoxPageRouteResult
            {
                Handled = true,
                InteractionToRun = action
            };
        }

        private WastewaterQuantityAttributeEditorContext Calculate(
            string payload)
        {
            WastewaterQuantityAttributeEditorRequest input =
                ReadRequired(payload);
            QuantityPipeAttributes attributes = Prepare(input);
            if (string.Equals(input.changedField, "SwapEndpoints",
                StringComparison.OrdinalIgnoreCase))
                SwapEndpoints(attributes);
            QuantityDependencyResult normalized = _cad.CalculateDraft(
                input.documentId, input.handle, attributes, input.layers,
                input.changedField, input.attributes);
            QuantityAttributeCadObject selected = _cad.Read(input.documentId,
                input.handle);
            WastewaterQuantityAttributeEditorContext context = BuildContext(
                selected, normalized);
            context.message = "派生值已联动更新。";
            context.requestId = input.requestId;
            return context;
        }

        private WastewaterQuantityAttributeEditorContext FromCad(
            QuantityAttributeCadObject selected, string emptyMessage,
            long requestId = 0)
        {
            if (selected == null || !selected.Selected)
                return new WastewaterQuantityAttributeEditorContext
                {
                    selected = false,
                    documentId = selected == null ? string.Empty
                        : selected.DocumentId,
                    documentName = selected == null ? string.Empty
                        : selected.DocumentName,
                    message = selected == null
                        ? (emptyMessage ?? string.Empty)
                        : selected.Message
                };
            QuantityPipeAttributes attributes = selected.Attributes == null
                ? QuantityPipeAttributes.DefaultForKind(
                    selected.InferredKind)
                : selected.Attributes.Clone();
            QuantityDependencyResult normalized = _cad.CalculateDraft(
                selected.DocumentId, selected.Handle, attributes,
                QuantityStructureLayer.Parse(attributes.BackfillStructure),
                "Load", null);
            WastewaterQuantityAttributeEditorContext context = BuildContext(
                selected, normalized);
            context.requestId = requestId;
            return context;
        }

        private static WastewaterQuantityAttributeEditorContext BuildContext(
            QuantityAttributeCadObject selected,
            QuantityDependencyResult normalized)
        {
            QuantityPipeAttributes attributes = normalized == null
                || normalized.Attributes == null
                ? (selected.Attributes ?? QuantityPipeAttributes.DefaultForKind(
                    selected.InferredKind)).Clone()
                : normalized.Attributes.Clone();
            return new WastewaterQuantityAttributeEditorContext
            {
                selected = true,
                documentId = selected.DocumentId,
                documentName = selected.DocumentName,
                handle = selected.Handle,
                layerName = selected.LayerName,
                objectTypeName = selected.ObjectTypeName,
                inferredKind = selected.InferredKind,
                cadLength = selected.CadLength,
                effectiveLength = attributes.EffectiveLength(
                    selected.CadLength),
                hasSavedAttributes = selected.HasSavedAttributes,
                message = selected.Message,
                attributes = attributes,
                layers = normalized == null
                    ? QuantityStructureLayer.Parse(
                        attributes.BackfillStructure)
                    : normalized.Layers,
                warnings = normalized == null
                    ? new List<string>() : normalized.Warnings,
                calculation = null,
                realExcavationDepth = normalized == null
                    ? 0.0 : normalized.RealExcavationDepth
            };
        }

        private QuantityPipeAttributes Prepare(
            WastewaterQuantityAttributeEditorRequest input)
        {
            QuantityPipeAttributes attributes = input.attributes == null
                ? QuantityPipeAttributes.Default.Clone()
                : input.attributes.Clone();
            if (input.layers != null)
                attributes.BackfillStructure = QuantityStructureLayer.Serialize(
                    input.layers, QuantityPipeAttributes.IsNodeKind(
                        attributes.ObjectKind));
            return attributes;
        }

        private WastewaterQuantityAttributeEditorRequest ReadOptional(
            string payload)
        {
            return string.IsNullOrWhiteSpace(payload)
                ? new WastewaterQuantityAttributeEditorRequest()
                : _serializer.Deserialize<
                    WastewaterQuantityAttributeEditorRequest>(payload)
                    ?? new WastewaterQuantityAttributeEditorRequest();
        }

        private WastewaterQuantityAttributeEditorRequest ReadRequired(
            string payload)
        {
            WastewaterQuantityAttributeEditorRequest input =
                ReadOptional(payload);
            if (string.IsNullOrWhiteSpace(input.handle))
                throw new InvalidOperationException("请先选择对象。");
            return input;
        }

        private CDBoxPageRouteResult ContextResult(
            WastewaterQuantityAttributeEditorContext context, string kind,
            bool draftCalculation)
        {
            return new CDBoxPageRouteResult
            {
                Handled = true,
                ToastKind = kind,
                ToastMessage = draftCalculation ? null : context.message,
                ExecuteScript =
                    "window.CDBoxQuantityAttributeEditorLoad && "
                    + "window.CDBoxQuantityAttributeEditorLoad("
                    + _serializer.Serialize(context) + ");"
            };
        }

        private CDBoxPageRouteResult Result(
            WastewaterQuantityAttributeEditorContext context,
            string message, string kind)
        {
            return context == null
                ? new CDBoxPageRouteResult
                {
                    Handled = true,
                    ToastMessage = message,
                    ToastKind = kind
                }
                : ContextResult(context, kind, false);
        }

        private static void SwapEndpoints(QuantityPipeAttributes attributes)
        {
            string node = attributes.StartNode;
            double depth = attributes.StartDepth;
            double invert = attributes.StartInvertElevation;
            attributes.StartNode = attributes.EndNode;
            attributes.StartDepth = attributes.EndDepth;
            attributes.StartInvertElevation = attributes.EndInvertElevation;
            attributes.EndNode = node;
            attributes.EndDepth = depth;
            attributes.EndInvertElevation = invert;
        }

        private static string Js(string value)
        {
            return "'" + (value ?? string.Empty).Replace("\\", "\\\\")
                .Replace("'", "\\'").Replace("\r", "\\r")
                .Replace("\n", "\\n") + "'";
        }
    }
}
