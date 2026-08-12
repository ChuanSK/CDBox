using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using TCPipeAutoDraw.Core.Colors;
using TCPipeAutoDraw.Modules.LongitudinalProfile;
using TCPipeAutoDraw.UI.Controls;

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
            if (name == "getlongitudinalprofiletextstyles")
            {
                result = TextStylesResult();
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
            if (name == "openlongitudinalprofilecolorpicker")
            {
                result = OpenColorPickerResult(request.Argument);
                return true;
            }
            return false;
        }

        private static CDBoxStudioRouteResult OpenColorPickerResult(
            string payload)
        {
            var result = new CDBoxStudioRouteResult { Handled = true };
            try
            {
                ColorRequest request = string.IsNullOrWhiteSpace(payload)
                    ? new ColorRequest()
                    : Serializer.Deserialize<ColorRequest>(payload)
                        ?? new ColorRequest();
                short current = request.index >= 0 && request.index <= 256
                    ? request.index : (short)7;
                CDBoxColor selected;
                if (!ColorPickerWindow.TryPick(
                    CDBoxColor.FromIndex(current), out selected,
                    true, false, false, false, false)) return result;
                short index = CDBoxColorService.ToCompatibleColorIndex(
                    selected, current);
                CDBoxColor normalized = CDBoxColor.FromIndex(index);
                result.ExecuteScript =
                    "window.CDBoxLongitudinalProfileColorSelected && window.CDBoxLongitudinalProfileColorSelected("
                    + Serializer.Serialize(new ColorResponse
                    {
                        path = request.path ?? string.Empty,
                        index = index,
                        name = normalized.DisplayName,
                        cssColor = normalized.Hex
                    }) + ");";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "颜色选择器打开失败：" + ex.Message;
            }
            return result;
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

        private static CDBoxStudioRouteResult TextStylesResult()
        {
            var names = new List<string>();
            try
            {
                Document document = Application.DocumentManager
                    .MdiActiveDocument;
                if (document != null)
                {
                    using (Transaction tr = document.Database
                        .TransactionManager.StartOpenCloseTransaction())
                    {
                        TextStyleTable table = (TextStyleTable)tr.GetObject(
                            document.Database.TextStyleTableId,
                            OpenMode.ForRead);
                        foreach (ObjectId id in table)
                        {
                            TextStyleTableRecord record =
                                tr.GetObject(id, OpenMode.ForRead, false)
                                    as TextStyleTableRecord;
                            if (record != null
                                && !string.IsNullOrWhiteSpace(record.Name))
                                names.Add(record.Name.Trim());
                        }
                    }
                }
            }
            catch { }
            if (names.Count == 0)
                names.AddRange(new[] { "宋体", "HZ", "Standard" });
            names = names.Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            return new CDBoxStudioRouteResult
            {
                Handled = true,
                ExecuteScript =
                    "window.CDBoxLongitudinalProfileTextStylesLoad && window.CDBoxLongitudinalProfileTextStylesLoad("
                    + Serializer.Serialize(names) + ");"
            };
        }

        private sealed class ColorRequest
        {
            public string path { get; set; }
            public short index { get; set; }
        }

        private sealed class ColorResponse
        {
            public string path { get; set; }
            public short index { get; set; }
            public string name { get; set; }
            public string cssColor { get; set; }
        }
    }
}
