using System;
using System.Web.Script.Serialization;
using TCPipeAutoDraw.Modules.FrameLayout;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioFrameSettingsRoutes
    {
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static bool TryRoute(CDBoxStudioRouteRequest request,
            out CDBoxStudioRouteResult result)
        {
            result = null;
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return false;
            string name = request.Name.Trim().ToLowerInvariant();
            if (name == "getframesettings")
            {
                result = LoadResult("CDBoxFrameSettingsLoad");
                return true;
            }
            if (name == "saveframesettings")
            {
                try
                {
                    FrameSettingsPayload payload =
                        Serializer.Deserialize<FrameSettingsPayload>(
                            request.Argument ?? string.Empty)
                        ?? new FrameSettingsPayload();
                    if (payload.settings == null)
                        throw new InvalidOperationException("未收到图框设置。");
                    payload.settings.Normalize();
                    FrameLayoutSettingsStore.Save(payload.settings);

                    if (!string.IsNullOrWhiteSpace(payload.templateId))
                    {
                        FrameTemplateCatalog catalog =
                            FrameTemplateCatalogStore.Load();
                        FrameTemplateCatalogItem item = catalog.Templates.Find(x =>
                            string.Equals(x.Id, payload.templateId,
                                StringComparison.OrdinalIgnoreCase));
                        if (item != null)
                        {
                            item.TemplateName =
                                string.IsNullOrWhiteSpace(payload.templateName)
                                    ? item.TemplateName
                                    : payload.templateName.Trim();
                            item.PaperSize =
                                FramePaperSizes.NormalizeName(payload.paperSize);
                            item.ApplyMargins(payload.marginLeft,
                                payload.marginTop, payload.marginRight,
                                payload.marginBottom);
                            FrameTemplateCatalogStore.Upsert(item);
                        }
                    }
                    result = LoadResult("CDBoxFrameSettingsSaved");
                    result.ToastKind = "success";
                }
                catch (Exception ex)
                {
                    result = Error(ex.Message);
                }
                return true;
            }
            if (name == "saveframetemplateview")
            {
                try
                {
                    FrameLayoutSettings settings =
                        FrameLayoutSettingsStore.Load();
                    settings.TemplateViewMode = string.Equals(
                        request.Argument, "single",
                        StringComparison.OrdinalIgnoreCase)
                            ? "Single" : "Double";
                    FrameLayoutSettingsStore.Save(settings);
                    result = new CDBoxStudioRouteResult { Handled = true };
                }
                catch (Exception ex)
                {
                    result = Error(ex.Message);
                }
                return true;
            }
            if (name == "deleteframetemplate")
            {
                try
                {
                    FrameTemplateCatalogStore.Delete(request.Argument, true);
                    result = LoadResult("CDBoxFrameSettingsLoad");
                }
                catch (Exception ex)
                {
                    result = Error(ex.Message);
                }
                return true;
            }
            if (name == "setdefaultframetemplate")
            {
                FrameTemplateCatalogStore.SetDefault(request.Argument);
                result = LoadResult("CDBoxFrameSettingsLoad");
                return true;
            }
            if (name == "runframeaction")
            {
                string action = (request.Argument ?? string.Empty).Trim()
                    .ToLowerInvariant();
                result = new CDBoxStudioRouteResult
                {
                    Handled = true,
                    RefreshPage = true,
                    ActionToRun = CreateAction(action)
                };
                return true;
            }
            return false;
        }

        private static CDBoxStudioAction CreateAction(string action)
        {
            FrameLayoutCommands commands = new FrameLayoutCommands();
            switch (action)
            {
                case "add":
                    return NewAction("frame:add", "添加图框模板",
                        commands.AddFrameTemplate);
                case "cut":
                    return NewAction("frame:cut", "布置裁图区域",
                        commands.PlaceCutRegions);
                case "layout":
                    return NewAction("frame:layout", "布置图框",
                        commands.LayoutFrames);
                case "place":
                    return NewAction("frame:place", "直接布框",
                        commands.PlaceFramesDirectly);
                default:
                    throw new InvalidOperationException("未知图框操作。");
            }
        }

        private static CDBoxStudioAction NewAction(string id, string title,
            Action run)
        {
            return new CDBoxStudioAction(id, title, "图框工具", string.Empty,
                string.Empty, string.Empty, CDBoxStudioActionKind.Module, true,
                true, run);
        }

        private static CDBoxStudioRouteResult LoadResult(string function)
        {
            return new CDBoxStudioRouteResult
            {
                Handled = true,
                ExecuteScript = "window." + function + " && window."
                    + function + "("
                    + Serializer.Serialize(
                        CDBoxStudioFrameSettingsPage.BuildContext()) + ");"
            };
        }

        private static CDBoxStudioRouteResult Error(string message)
        {
            return new CDBoxStudioRouteResult
            {
                Handled = true,
                ToastKind = "error",
                ExecuteScript =
                    "window.CDBoxFrameSettingsError && window.CDBoxFrameSettingsError("
                    + Serializer.Serialize(message ?? "图框设置操作失败。") + ");"
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
