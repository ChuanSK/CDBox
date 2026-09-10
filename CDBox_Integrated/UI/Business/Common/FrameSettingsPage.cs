using System;
using System.Web.Script.Serialization;
using CDBox.Common.Services;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using TCPipeAutoDraw.Modules.FrameLayout;
using TCPipeAutoDraw.UI.Studio;

namespace CDBox.Common.UI
{
    internal static class FrameSettingsPage
    {
        public const string PageId = "common-frame-settings";
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static CDBoxPageDefinition Create(
            FrameLayoutCommandService commands, ICDBoxLogger logger)
        {
            Func<string> html = delegate
            {
                return CDBoxStudioFrameSettingsPage
                    .BuildStandaloneDocument(CDBoxStudioSettingsStore.Load());
            };
            Func<CDBoxPageRouteRequest, CDBoxPageRouteResult> route =
                request => Route(request, commands, logger);
            return new CDBoxPageDefinition(PageId, "图框设置", html, route)
            {
                Width = 1280,
                Height = 850,
                MinimumWidth = 980,
                MinimumHeight = 680
            };
        }

        private static CDBoxPageRouteResult Route(
            CDBoxPageRouteRequest request,
            FrameLayoutCommandService commands, ICDBoxLogger logger)
        {
            var result = new CDBoxPageRouteResult { Handled = true };
            if (request == null) return result;
            string name = (request.Name ?? string.Empty).Trim()
                .ToLowerInvariant();
            try
            {
                switch (name)
                {
                    case "ready":
                        return result;
                    case "getframesettings":
                        return LoadResult("CDBoxFrameSettingsLoad");
                    case "saveframesettings":
                        SaveSettings(request.Argument);
                        result = LoadResult("CDBoxFrameSettingsSaved");
                        result.ToastKind = "success";
                        result.ToastMessage = "图框设置已保存";
                        return result;
                    case "saveframetemplateview":
                        FrameLayoutSettings settings =
                            FrameLayoutSettingsStore.Load();
                        settings.TemplateViewMode = string.Equals(
                            request.Argument, "single",
                            StringComparison.OrdinalIgnoreCase)
                                ? "Single" : "Double";
                        FrameLayoutSettingsStore.Save(settings);
                        return result;
                    case "deleteframetemplate":
                        FrameTemplateCatalogStore.Delete(
                            request.Argument, true);
                        return LoadResult("CDBoxFrameSettingsLoad");
                    case "setdefaultframetemplate":
                        FrameTemplateCatalogStore.SetDefault(
                            request.Argument);
                        return LoadResult("CDBoxFrameSettingsLoad");
                    case "runframeaction":
                        result.ActionToRun = commands.ResolveAction(
                            request.Argument);
                        result.RefreshPage = true;
                        return result;
                    default:
                        result.Handled = false;
                        return result;
                }
            }
            catch (Exception ex)
            {
                logger.Error("图框设置页面操作失败：" + name, ex);
                return Error(ex.Message);
            }
        }

        private static void SaveSettings(string argument)
        {
            FrameSettingsPayload payload =
                Serializer.Deserialize<FrameSettingsPayload>(
                    argument ?? string.Empty) ?? new FrameSettingsPayload();
            if (payload.settings == null)
                throw new InvalidOperationException("未收到图框设置。");
            payload.settings.Normalize();
            FrameLayoutSettingsStore.Save(payload.settings);

            if (string.IsNullOrWhiteSpace(payload.templateId)) return;
            FrameTemplateCatalog catalog = FrameTemplateCatalogStore.Load();
            FrameTemplateCatalogItem item = catalog.Templates.Find(x =>
                string.Equals(x.Id, payload.templateId,
                    StringComparison.OrdinalIgnoreCase));
            if (item == null) return;
            item.TemplateName = string.IsNullOrWhiteSpace(payload.templateName)
                ? item.TemplateName : payload.templateName.Trim();
            item.PaperSize = FramePaperSizes.NormalizeName(payload.paperSize);
            item.ApplyMargins(payload.marginLeft, payload.marginTop,
                payload.marginRight, payload.marginBottom);
            FrameTemplateCatalogStore.Upsert(item);
        }

        private static CDBoxPageRouteResult LoadResult(string function)
        {
            return new CDBoxPageRouteResult
            {
                Handled = true,
                ExecuteScript = "window." + function + " && window."
                    + function + "("
                    + Serializer.Serialize(
                        CDBoxStudioFrameSettingsPage.BuildContext()) + ");"
            };
        }

        private static CDBoxPageRouteResult Error(string message)
        {
            return new CDBoxPageRouteResult
            {
                Handled = true,
                ToastKind = "error",
                ToastMessage = message,
                ExecuteScript =
                    "window.CDBoxFrameSettingsError && window.CDBoxFrameSettingsError("
                    + Serializer.Serialize(message
                        ?? "图框设置操作失败。") + ");"
            };
        }

        private sealed class FrameSettingsPayload
        {
            public string templateId { get; set; }
            public string templateName { get; set; }
            public string paperSize { get; set; }
            public double marginTop { get; set; }
            public double marginRight { get; set; }
            public double marginBottom { get; set; }
            public double marginLeft { get; set; }
            public FrameLayoutSettings settings { get; set; }
        }
    }
}
