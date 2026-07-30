using System;
using System.Web.Script.Serialization;
using TCPipeAutoDraw.Modules.ShortCodeRecognition;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioShortCodeSettingsRoutes
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
            if (name == "getshortcodesettings")
            {
                result = LoadResult("CDBoxShortCodeSettingsLoad");
                return true;
            }
            if (name == "saveshortcodesettings")
            {
                try
                {
                    ShortCodeRecognitionSettings settings =
                        Serializer.Deserialize<ShortCodeRecognitionSettings>(
                            request.Argument ?? string.Empty)
                        ?? new ShortCodeRecognitionSettings();
                    settings.Normalize();
                    ShortCodeRecognitionSettingsStore.Save(settings);
                    result = LoadResult("CDBoxShortCodeSettingsSaved");
                    result.ToastKind = "success";
                }
                catch (Exception ex)
                {
                    result = new CDBoxStudioRouteResult
                    {
                        Handled = true,
                        ToastKind = "error",
                        ExecuteScript =
                            "window.CDBoxShortCodeSettingsError && window.CDBoxShortCodeSettingsError("
                            + Serializer.Serialize(ex.Message) + ");"
                    };
                }
                return true;
            }
            return false;
        }

        private static CDBoxStudioRouteResult LoadResult(string function)
        {
            return new CDBoxStudioRouteResult
            {
                Handled = true,
                ExecuteScript = "window." + function + " && window."
                    + function + "("
                    + Serializer.Serialize(
                        ShortCodeRecognitionSettingsStore.Load()) + ");"
            };
        }
    }
}
