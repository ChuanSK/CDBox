using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using TCPipeAutoDraw.Modules.LongitudinalProfile;
using TCPipeAutoDraw.UI.Studio;

namespace CDBox.Wastewater.UI
{
    internal sealed class WastewaterLongitudinalProfileSettingsController
    {
        public const string PageId =
            "wastewater-longitudinal-profile-settings";
        private readonly ICDBoxPageAppearanceService _appearance;
        private readonly ICDBoxColorPickerService _colors;
        private readonly ICDBoxLogger _logger;
        private readonly JavaScriptSerializer _serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public WastewaterLongitudinalProfileSettingsController(
            ICDBoxPageAppearanceService appearance,
            ICDBoxColorPickerService colors, ICDBoxLogger logger)
        {
            _appearance = appearance
                ?? throw new ArgumentNullException("appearance");
            _colors = colors ?? throw new ArgumentNullException("colors");
            _logger = logger ?? throw new ArgumentNullException("logger");
        }

        public CDBoxPageDefinition CreatePage()
        {
            return new CDBoxPageDefinition(PageId, "纵断面设置",
                BuildDocument, Route)
            {
                Width = 1180,
                Height = 820,
                MinimumWidth = 920,
                MinimumHeight = 660
            };
        }

        private string BuildDocument()
        {
            CDBoxPageAppearance appearance = _appearance.GetAppearance()
                ?? new CDBoxPageAppearance();
            var studio = CDBoxStudioSettingsStore.Load();
            studio.Theme = appearance.Theme;
            studio.AnimationsEnabled = appearance.AnimationsEnabled;
            return CDBoxStudioLongitudinalProfileSettingsPage
                .BuildStandaloneDocument(studio);
        }

        private CDBoxPageRouteResult Route(CDBoxPageRouteRequest request)
        {
            if (request == null) return new CDBoxPageRouteResult
                { Handled = false };
            string name = (request.Name ?? string.Empty).Trim()
                .ToLowerInvariant();
            try
            {
                if (name == "ready"
                    || name == "getlongitudinalprofilesettings")
                    return Settings("CDBoxLongitudinalProfileSettingsLoad");
                if (name == "getlongitudinalprofiletextstyles")
                    return TextStyles();
                if (name == "savelongitudinalprofilesettings")
                {
                    LongitudinalProfileSettings settings = _serializer
                        .Deserialize<LongitudinalProfileSettings>(
                            request.Argument ?? string.Empty)
                        ?? new LongitudinalProfileSettings();
                    settings.Normalize();
                    LongitudinalProfileSettingsStore.Save(settings);
                    CDBoxPageRouteResult result = Settings(
                        "CDBoxLongitudinalProfileSettingsSaved");
                    result.ToastKind = "success";
                    result.ToastMessage = "纵断面设置已保存";
                    return result;
                }
                if (name == "openlongitudinalprofilecolorpicker")
                    return PickColor(request.Argument);
            }
            catch (Exception ex)
            {
                _logger.Error("纵断面设置页面操作失败。", ex);
                return new CDBoxPageRouteResult
                {
                    Handled = true,
                    ToastKind = "error",
                    ToastMessage = ex.Message
                };
            }
            return new CDBoxPageRouteResult { Handled = false };
        }

        private CDBoxPageRouteResult Settings(string functionName)
        {
            return Script(functionName,
                LongitudinalProfileSettingsStore.Load());
        }

        private CDBoxPageRouteResult TextStyles()
        {
            var names = new List<string>();
            try
            {
                Document document = Application.DocumentManager
                    .MdiActiveDocument;
                if (document != null)
                    using (Transaction transaction = document.Database
                        .TransactionManager.StartOpenCloseTransaction())
                    {
                        TextStyleTable table = transaction.GetObject(
                            document.Database.TextStyleTableId,
                            OpenMode.ForRead) as TextStyleTable;
                        if (table != null)
                            foreach (ObjectId id in table)
                            {
                                TextStyleTableRecord record = transaction
                                    .GetObject(id, OpenMode.ForRead, false)
                                    as TextStyleTableRecord;
                                if (record != null
                                    && !string.IsNullOrWhiteSpace(record.Name))
                                    names.Add(record.Name.Trim());
                            }
                    }
            }
            catch { }
            if (names.Count == 0)
                names.AddRange(new[] { "宋体", "HZ", "Standard" });
            return Script("CDBoxLongitudinalProfileTextStylesLoad",
                names.Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(value => value,
                        StringComparer.CurrentCultureIgnoreCase).ToList());
        }

        private CDBoxPageRouteResult PickColor(string json)
        {
            ColorRequest request = _serializer.Deserialize<ColorRequest>(
                json ?? string.Empty) ?? new ColorRequest();
            CDBoxModuleColor selected;
            if (!_colors.TryPick(CDBoxModuleColor.FromIndex(request.index),
                    out selected, true, true))
                return new CDBoxPageRouteResult { Handled = true };
            int index = selected == null ? request.index : selected.Index;
            if (index < 0 || index > 256) index = 7;
            return Script("CDBoxLongitudinalProfileColorSelected", new
            {
                path = request.path ?? string.Empty,
                index,
                name = selected == null ? "ACI " + index
                    : selected.DisplayName,
                cssColor = selected == null
                    ? string.Empty : "rgb(" + selected.R + ","
                        + selected.G + "," + selected.B + ")"
            });
        }

        private CDBoxPageRouteResult Script(string functionName,
            object value)
        {
            return new CDBoxPageRouteResult
            {
                Handled = true,
                ExecuteScript = "window." + functionName + " && window."
                    + functionName + "(" + _serializer.Serialize(value)
                    + ");"
            };
        }

        private sealed class ColorRequest
        {
            public string path { get; set; }
            public int index { get; set; }

            public ColorRequest()
            {
                path = string.Empty;
                index = 7;
            }
        }
    }
}
