using System;
using System.Web.Script.Serialization;
using TCPipeAutoDraw.Modules.LongitudinalProfile;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioLongitudinalProfileSettingsRoutes
    {
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static bool TryRoute(
            CDBoxStudioRouteRequest request,
            out CDBoxStudioRouteResult result)
        {
            result = null;
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return false;
            string name = request.Name.Trim().ToLowerInvariant();
            if (name == "getlongitudinalprofilesettings")
            {
                result = LoadResult(
                    "CDBoxLongitudinalProfileSettingsLoad");
                return true;
            }
            if (name == "savelongitudinalprofilesettings")
            {
                try
                {
                    LongitudinalProfileSettings settings =
                        Serializer.Deserialize<LongitudinalProfileSettings>(
                            request.Argument ?? string.Empty)
                        ?? new LongitudinalProfileSettings();
                    settings.Normalize();
                    LongitudinalProfileSettingsStore.Save(settings);
                    result = LoadResult(
                        "CDBoxLongitudinalProfileSettingsSaved");
                    result.ToastKind = "success";
                }
                catch (Exception ex)
                {
                    result = new CDBoxStudioRouteResult
                    {
                        Handled = true,
                        ToastKind = "error",
                        ExecuteScript =
                            "window.CDBoxLongitudinalProfileSettingsError && window.CDBoxLongitudinalProfileSettingsError("
                            + Serializer.Serialize(ex.Message) + ");"
                    };
                }
                return true;
            }
            return false;
        }

        private static CDBoxStudioRouteResult LoadResult(
            string functionName)
        {
            return new CDBoxStudioRouteResult
            {
                Handled = true,
                ExecuteScript = "window." + functionName
                    + " && window." + functionName + "("
                    + Serializer.Serialize(
                        LongitudinalProfileSettingsStore.Load())
                    + ");"
            };
        }
    }
}
