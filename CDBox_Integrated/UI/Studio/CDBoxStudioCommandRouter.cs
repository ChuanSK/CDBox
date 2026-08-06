using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using TCPipeAutoDraw.Modules.LayerManager;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.Modules.ExcelToCad;
using TCPipeAutoDraw.Core.Startup;
using TCPipeAutoDraw.UI;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioCommandRouter : IDisposable
    {
        private readonly Dictionary<string, CDBoxStudioAction> _actionsById;
        private readonly CDBoxStudioSettings _settings;
        private readonly Action<string> _scriptSink;
        private readonly CDBoxStudioUpdateRoutes _updateRoutes;
        private readonly CDBoxStudioExcelToCadWindow _excelToCadRoutes;

        public CDBoxStudioCommandRouter(Dictionary<string, CDBoxStudioAction> actionsById, CDBoxStudioSettings settings)
            : this(actionsById, settings, null)
        {
        }

        public CDBoxStudioCommandRouter(Dictionary<string, CDBoxStudioAction> actionsById, CDBoxStudioSettings settings, Action<string> scriptSink)
        {
            if (actionsById == null) throw new ArgumentNullException("actionsById");
            _actionsById = actionsById;
            _settings = settings ?? new CDBoxStudioSettings();
            _scriptSink = scriptSink;
            CDBoxStudioQuantityDashboardRoutes.Configure(_scriptSink);
            _updateRoutes = new CDBoxStudioUpdateRoutes(_settings, _scriptSink, ApplySettingsArgument);
            _excelToCadRoutes = CDBoxStudioExcelToCadWindow.CreateEmbedded(
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument,
                ExcelToCadCommandService.ResolveDefaultTextHeight());
        }

        public CDBoxStudioRouteResult Route(CDBoxStudioRouteRequest request)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "info" };
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                result.Handled = false;
                return result;
            }

            CDBoxStudioRouteResult excelToCadResult;
            if (_excelToCadRoutes.TryRoute(request,
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument,
                out excelToCadResult)) return excelToCadResult;

            CDBoxStudioRouteResult quantityDashboardResult;
            if (CDBoxStudioQuantityDashboardRoutes.TryRoute(request, out quantityDashboardResult)) return quantityDashboardResult;

            CDBoxStudioRouteResult quantityAttributeResult;
            if (CDBoxStudioQuantityAttributeEditorRoutes.TryRoute(request, out quantityAttributeResult)) return quantityAttributeResult;

            CDBoxStudioRouteResult sectionDrawingResult;
            if (CDBoxStudioSectionDrawingRoutes.TryRoute(request, _scriptSink, out sectionDrawingResult)) return sectionDrawingResult;

            CDBoxStudioRouteResult frameSettingsResult;
            if (CDBoxStudioFrameSettingsRoutes.TryRoute(request,
                out frameSettingsResult)) return frameSettingsResult;

            CDBoxStudioRouteResult shortCodeSettingsResult;
            if (CDBoxStudioShortCodeSettingsRoutes.TryRoute(request,
                out shortCodeSettingsResult)) return shortCodeSettingsResult;

            CDBoxStudioRouteResult longitudinalProfileSettingsResult;
            if (CDBoxStudioLongitudinalProfileSettingsRoutes.TryRoute(
                request, out longitudinalProfileSettingsResult))
                return longitudinalProfileSettingsResult;

            CDBoxStudioRouteResult layerManagerResult;
            if (CDBoxStudioLayerManagerRoutes.TryRoute(request, false, out layerManagerResult)) return layerManagerResult;

            CDBoxStudioRouteResult annotationResult;
            if (CDBoxStudioAnnotationSettingsRoutes.TryRoute(request, false, out annotationResult)) return annotationResult;

            CDBoxStudioRouteResult updateResult;
            if (_updateRoutes.TryRoute(request, out updateResult)) return updateResult;

            string name = request.Name.Trim().ToLowerInvariant();
            switch (name)
            {
                case "ready":
                    CDBoxStudioLogger.Info("Studio ???????????" + _actionsById.Count);
                    result.ToastMessage = "CDBox Studio ???";
                    result.ToastKind = "success";
                    return result;

                case "run":
                    return RouteRun(request.Argument);

                case "settings":
                    return RouteSettings(request.Argument);

                case "installplugin":
                    return RouteInstallPlugin();

                case "localupdateplugin":
                    return RouteLocalUpdatePlugin();

                case "uninstallplugin":
                    return RouteUninstallPlugin();

                case "saverecognitionrules":
                    return RouteSaveRecognitionRules(request.Argument);

                case "openlegacyrecognition":
                    return RouteOpenLegacyRecognitionRules();

                case "openrecognitionwindow":
                    return RouteOpenRecognitionWindow();

                case "savedefaultprofiles":
                    return RouteSaveDefaultProfiles(request.Argument);

                case "opendefaultprofileswindow":
                    return RouteOpenDefaultProfilesWindow();

                case "openlegacydefaultprofiles":
                    return RouteOpenLegacyDefaultProfiles();

                case "openlayermanagerwindow":
                    return RouteOpenLayerManagerWindow();

                case "openquantitydashboardwindow":
                    return RouteOpenQuantityDashboardWindow();

                case "openquantityattributeeditorwindow":
                    return RouteOpenQuantityAttributeEditorWindow(request.Argument);

                case "opensectiondrawingwindow":
                    return RouteOpenSectionDrawingWindow();

                case "openframesettingswindow":
                    return RouteOpenFrameSettingsWindow();

                case "openlongitudinalprofilesettingswindow":
                    CDBoxStudioLongitudinalProfileSettingsWindow.ShowWindow(
                        new AcadMainWindow());
                    result.ToastKind = "success";
                    return result;

                case "layermanageropened":
                    return RouteLayerManagerOpened();

                case "openannotationsettingswindow":
                    return RouteOpenAnnotationSettingsWindow(request.Argument);

                case "opensettingswindow":
                    CDBoxStudioSettingsWindow.ShowWindow(new AcadMainWindow());
                    result.ToastMessage = "??? CDBox??";
                    result.ToastKind = "success";
                    return result;

                case "annotationsettingsopened":
                    return RouteAnnotationSettingsOpened(request.Argument);

                case "openlogs":
                    return RouteOpenLogs();

                case "filter":
                    result.WindowTitleSuffix = request.Argument;
                    return result;

                default:
                    result.Handled = false;
                    CDBoxStudioLogger.Warn("???? Studio ?????" + request.Name);
                    return result;
            }
        }

        private CDBoxStudioRouteResult RouteRun(string id)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "info" };

            CDBoxStudioAction action;
            if (string.IsNullOrWhiteSpace(id) || !_actionsById.TryGetValue(id.Trim(), out action) || action == null)
            {
                result.ToastMessage = "????????";
                result.ToastKind = "warning";
                CDBoxStudioLogger.Warn("?????????????" + (id ?? string.Empty));
                return result;
            }

            if (!action.Enabled)
            {
                result.ToastMessage = action.Title + " ????";
                result.ToastKind = "warning";
                return result;
            }

            CDBoxStudioLogger.Info("?????" + action.Title + " [" + action.Id + "]");
            result.ActionToRun = action;
            result.RefreshPage = true;
            return result;
        }

        private CDBoxStudioRouteResult RouteSettings(string argument)
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = false,
                ToastKind = "success",
                ToastMessage = "Studio ?????"
            };

            try
            {
                ApplySettingsArgument(argument);
                CDBoxStudioSettingsStore.Save(_settings);
                PipeLengthAnnotationInteractionService.RefreshAppearance();
                CDBoxAppSettings appSettings = CDBoxAppSettingsStore.Load();
                appSettings.PromptInstallOnLoad = ReadBooleanArgument(argument, "promptinstall", appSettings.PromptInstallOnLoad);
                CDBoxAppSettingsStore.Save(appSettings);
                CDBoxStudioLogger.Info("?? Studio ???Theme=" + _settings.Theme
                    + ", AnimationsEnabled=" + _settings.AnimationsEnabled
                    + ", AnnotationHudNormalOpacity=" + _settings.AnnotationHudNormalOpacity.ToString("0.##", CultureInfo.InvariantCulture)
                    + ", AnnotationHudHoverOpacity=" + _settings.AnnotationHudHoverOpacity.ToString("0.##", CultureInfo.InvariantCulture)
                    + ", AnnotationHudGlowEnabled=" + _settings.AnnotationHudGlowEnabled
                    + ", AnnotationHudGlowIntensity=" + _settings.AnnotationHudGlowIntensity.ToString("0.##", CultureInfo.InvariantCulture)
                    + ", DoubleClickOpenEnabled=" + _settings.DoubleClickOpenEnabled
                    + ", ColorOutputMode=" + _settings.ColorOutputMode
                    + ", UpdateChannel=" + _settings.UpdateChannel);
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "???????" + ex.Message;
                CDBoxStudioLogger.Error("?? Studio ?????", ex);
            }

            return result;
        }

        private void ApplySettingsArgument(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument)) return;

            string[] parts = argument.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string[] kv = part.Split(new[] { '=' }, 2);
                string key = kv.Length > 0 ? kv[0].Trim().ToLowerInvariant() : string.Empty;
                string value = kv.Length > 1 ? DecodeArgumentValue(kv[1].Trim()) : string.Empty;

                switch (key)
                {
                    case "theme":
                        if (CDBoxStudioSettings.IsValidTheme(value)) _settings.Theme = value.ToLowerInvariant();
                        break;
                    case "animations":
                        _settings.AnimationsEnabled = IsTrue(value);
                        break;
                    case "hudnormalopacity":
                        _settings.AnnotationHudNormalOpacity = ParseDouble(value,
                            _settings.AnnotationHudNormalOpacity);
                        break;
                    case "hudhoveropacity":
                        _settings.AnnotationHudHoverOpacity = ParseDouble(value,
                            _settings.AnnotationHudHoverOpacity);
                        break;
                    case "hudglowenabled":
                        _settings.AnnotationHudGlowEnabled = IsTrue(value);
                        break;
                    case "hudglowintensity":
                        _settings.AnnotationHudGlowIntensity = ParseDouble(value,
                            _settings.AnnotationHudGlowIntensity);
                        break;
                    case "doubleclickopen":
                        _settings.DoubleClickOpenEnabled = IsTrue(value);
                        break;
                    case "coloroutputmode":
                        TCPipeAutoDraw.Core.Colors.CDBoxColorOutputMode outputMode;
                        if (Enum.TryParse(value, true, out outputMode)) _settings.ColorOutputMode = outputMode;
                        break;
                    case "updatechannel":
                        _settings.UpdateChannel = value;
                        break;
                }
            }

            _settings.Normalize();
        }

        private static double ParseDouble(string value, double fallback)
        {
            double parsed;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                out parsed) ? parsed : fallback;
        }

        private static string DecodeArgumentValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            try
            {
                return Uri.UnescapeDataString(value.Replace("+", "%20"));
            }
            catch
            {
                return value;
            }
        }

        private static bool IsTrue(string value)
        {
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ReadBooleanArgument(string argument, string targetKey, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(argument)) return fallback;
            foreach (string part in argument.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split(new[] { '=' }, 2);
                if (kv.Length == 0 || !string.Equals(kv[0].Trim(), targetKey, StringComparison.OrdinalIgnoreCase)) continue;
                return kv.Length > 1 && IsTrue(DecodeArgumentValue(kv[1].Trim()));
            }
            return fallback;
        }

        private CDBoxStudioRouteResult RouteInstallPlugin()
        {
            CDBoxInstallResult install = CDBoxInstaller.InstallToCadDirectory();
            CDBoxAppSettings app = CDBoxAppSettingsStore.Load();
            if (install.Success) app.InstalledPath = install.InstallRoot;
            CDBoxAppSettingsStore.Save(app);
            return new CDBoxStudioRouteResult { Handled = true, RefreshPage = true, ToastKind = install.Success ? "success" : "error", ToastMessage = install.Message };
        }

        private CDBoxStudioRouteResult RouteLocalUpdatePlugin()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "?????????????? CDBox.dll";
                dialog.Filter = "CDBox.dll|CDBox.dll|DLL ?? (*.dll)|*.dll|???? (*.*)|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(new AcadMainWindow()) != DialogResult.OK)
                    return new CDBoxStudioRouteResult { Handled = true };

                CDBoxInstallResult update = CDBoxInstaller.ScheduleUpdateFromDll(dialog.FileName);
                CDBoxAppSettings app = CDBoxAppSettingsStore.Load();
                if (update.Success) app.InstalledPath = update.InstallRoot;
                CDBoxAppSettingsStore.Save(app);
                string prompt = update.Success
                    ? "????????????????????\r\n\r\n????? AutoCAD?????? CAD ??????????"
                    : update.Message;
                CDBoxMessageBox.Show(new AcadMainWindow(), prompt,
                    update.Success ? "???????" : "??????",
                    MessageBoxButtons.OK,
                    update.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                return new CDBoxStudioRouteResult
                {
                    Handled = true,
                    RefreshPage = false,
                    ToastKind = update.Success ? "success" : "error",
                    ToastMessage = update.Success ? "??????????? AutoCAD ????" : "????????"
                };
            }
        }

        private CDBoxStudioRouteResult RouteUninstallPlugin()
        {
            DialogResult confirm = CDBoxPromptDialog.ShowYesNo(new AcadMainWindow(), "?? CDBox", "???? CDBox ?????????????", "??", "??", out _);
            if (confirm != DialogResult.Yes) return new CDBoxStudioRouteResult { Handled = true };

            CDBoxInstallResult uninstall = CDBoxInstaller.Uninstall();
            CDBoxAppSettings app = CDBoxAppSettingsStore.Load();
            app.InstalledPath = string.Empty;
            CDBoxAppSettingsStore.Save(app);
            return new CDBoxStudioRouteResult { Handled = true, RefreshPage = true, ToastKind = uninstall.Success ? "success" : "warning", ToastMessage = uninstall.Message };
        }


        private CDBoxStudioRouteResult RouteSaveRecognitionRules(string payload)
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = false,
                ToastKind = "success"
            };

            try
            {
                int count = CDBoxStudioRecognitionRules.SavePayload(payload);
                result.ToastMessage = "?????????" + count + " ???";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "??????????" + ex.Message;
                CDBoxStudioLogger.Error("Studio ??????????", ex);
            }

            return result;
        }

        private CDBoxStudioRouteResult RouteSaveDefaultProfiles(string payload)
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = false,
                ToastKind = "success"
            };

            try
            {
                int count = CDBoxStudioDefaultProfiles.SavePayload(payload);
                result.ToastMessage = "?????????" + count + " ???";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "??????????" + ex.Message;
                CDBoxStudioLogger.Error("Studio ??????????", ex);
            }

            return result;
        }

        private CDBoxStudioRouteResult RouteOpenDefaultProfilesWindow()
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = false,
                ToastKind = "success"
            };

            try
            {
                CDBoxStudioDefaultProfilesWindow.ShowWindow(new AcadMainWindow());
                result.ToastMessage = "????????????";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "??????????????" + ex.Message;
                CDBoxStudioLogger.Error("????????? WebView2 ?????", ex);
            }

            return result;
        }

        private CDBoxStudioRouteResult RouteOpenLegacyDefaultProfiles()
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = true,
                ToastKind = "success"
            };

            try
            {
                using (var form = new QuantityDefaultProfileForm())
                {
                    form.ShowDialog(new AcadMainWindow());
                }

                result.ToastMessage = "????????????????";
                CDBoxStudioLogger.Info("?????????????????" + CDBoxStudioDefaultProfiles.DefaultsFilePath);
            }
            catch (Exception ex)
            {
                result.RefreshPage = false;
                result.ToastKind = "error";
                result.ToastMessage = "????????????" + ex.Message;
                CDBoxStudioLogger.Error("????????????", ex);
            }

            return result;
        }

        private CDBoxStudioRouteResult RouteOpenRecognitionWindow()
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = false,
                ToastKind = "success"
            };

            try
            {
                CDBoxStudioRecognitionRulesWindow.ShowWindow(new AcadMainWindow());
                result.ToastMessage = "????????????";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "??????????????" + ex.Message;
                CDBoxStudioLogger.Error("????????? WebView2 ?????", ex);
            }

            return result;
        }

        private CDBoxStudioRouteResult RouteOpenLegacyRecognitionRules()
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                RefreshPage = true,
                ToastKind = "success"
            };

            try
            {
                using (var form = new LayerRecognitionRulesForm(LayerManagerService.LoadRecognitionRules(), LayerManagerService.GetRecognitionRulesFilePath()))
                {
                    DialogResult dialogResult = form.ShowDialog(new AcadMainWindow());
                    if (dialogResult == DialogResult.OK)
                    {
                        LayerManagerService.SaveRecognitionRules(form.Rules);
                        result.ToastMessage = "??????????";
                        CDBoxStudioLogger.Info("?????????????????" + LayerManagerService.GetRecognitionRulesFilePath());
                    }
                    else
                    {
                        result.RefreshPage = false;
                        result.ToastKind = "info";
                        result.ToastMessage = "??????????";
                    }
                }
            }
            catch (Exception ex)
            {
                result.RefreshPage = false;
                result.ToastKind = "error";
                result.ToastMessage = "????????????" + ex.Message;
                CDBoxStudioLogger.Error("????????????", ex);
            }

            return result;
        }


        private CDBoxStudioRouteResult RouteOpenLayerManagerWindow()
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioLayerManagerWindow.ShowWindow(new AcadMainWindow());
                result.ToastMessage = "????????????";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "??????????????" + ex.Message;
                CDBoxStudioLogger.Error("????????? WebView2 ?????", ex);
            }
            return result;
        }

        private CDBoxStudioRouteResult RouteOpenQuantityDashboardWindow()
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioQuantityDashboardWindow.ShowWindow(new AcadMainWindow());
                result.ToastMessage = "??????????????";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "????????????????" + ex.Message;
                CDBoxStudioLogger.Error("??????????? WebView2 ?????", ex);
            }
            return result;
        }

        private CDBoxStudioRouteResult RouteOpenQuantityAttributeEditorWindow(string payload)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioQuantityAttributeEditorRequest request = CDBoxStudioQuantityAttributeEditorApi.Deserialize<CDBoxStudioQuantityAttributeEditorRequest>(payload) ?? new CDBoxStudioQuantityAttributeEditorRequest();
                Autodesk.AutoCAD.ApplicationServices.Document doc = QuantityDashboardService.ResolveDocument(request.documentId);
                QuantityPipeSelectionInfo info = doc == null || string.IsNullOrWhiteSpace(request.handle) ? null : QuantityPipeAttributeService.ReadPipe(doc, ResolveHandle(doc, request.handle));
                CDBoxStudioQuantityAttributeEditorWindow.ShowWindow(new AcadMainWindow(), info, request.documentId);
                result.ToastMessage = "????????????";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error"; result.ToastMessage = "??????????????" + ex.Message;
                CDBoxStudioLogger.Error("??????? 3.4.1 ???????", ex);
            }
            return result;
        }

        private CDBoxStudioRouteResult RouteOpenSectionDrawingWindow()
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioSectionDrawingWindow.ShowWindow(new AcadMainWindow());
                result.ToastMessage = "????????????";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "??????????????" + ex.Message;
                CDBoxStudioLogger.Error("??????? Preview 10 ???????", ex);
            }
            return result;
        }

        private CDBoxStudioRouteResult RouteOpenFrameSettingsWindow()
        {
            var result = new CDBoxStudioRouteResult
            {
                Handled = true,
                ToastKind = "success"
            };
            try
            {
                CDBoxStudioFrameSettingsWindow.ShowWindow(new AcadMainWindow());
                result.ToastMessage = "???????????";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "?????????????" + ex.Message;
                CDBoxStudioLogger.Error("???????? WebView2 ?????", ex);
            }
            return result;
        }

        private static Autodesk.AutoCAD.DatabaseServices.ObjectId ResolveHandle(Autodesk.AutoCAD.ApplicationServices.Document doc, string handle)
        {
            long value;
            if (doc == null || !long.TryParse(handle, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value)) return Autodesk.AutoCAD.DatabaseServices.ObjectId.Null;
            try { return doc.Database.GetObjectId(false, new Autodesk.AutoCAD.DatabaseServices.Handle(value), 0); }
            catch { return Autodesk.AutoCAD.DatabaseServices.ObjectId.Null; }
        }

        public void Dispose()
        {
            CDBoxStudioQuantityDashboardRoutes.Unconfigure(_scriptSink);
            if (_excelToCadRoutes != null) _excelToCadRoutes.Dispose();
        }

        private CDBoxStudioRouteResult RouteLayerManagerOpened()
        {
            const string actionId = "module:layer-manager";
            CDBoxStudioAction action;
            if (_actionsById.TryGetValue(actionId, out action) && action != null)
            {
            }

            CDBoxStudioLogger.Info("??????? WebView2 ???");
            return new CDBoxStudioRouteResult { Handled = true, RefreshPage = false };
        }

        private CDBoxStudioRouteResult RouteOpenAnnotationSettingsWindow(string section)
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };
            try
            {
                CDBoxStudioAnnotationSettingsWindow.ShowWindow(new AcadMainWindow(), section);
                result.ToastMessage = "???????????";
            }
            catch (Exception ex)
            {
                result.ToastKind = "error";
                result.ToastMessage = "?????????????" + ex.Message;
                CDBoxStudioLogger.Error("???????? WebView2 ?????", ex);
            }
            return result;
        }

        private CDBoxStudioRouteResult RouteAnnotationSettingsOpened(string section)
        {
            const string actionId = "module:annotation-settings";
            CDBoxStudioAction action;
            if (_actionsById.TryGetValue(actionId, out action) && action != null)
            {
            }

            CDBoxStudioLogger.Info("?????? WebView2 ??????" + (section ?? "surface"));
            return new CDBoxStudioRouteResult { Handled = true, RefreshPage = false };
        }

        private CDBoxStudioRouteResult RouteOpenLogs()
        {
            var result = new CDBoxStudioRouteResult { Handled = true, ToastKind = "success" };

            try
            {
                CDBoxStudioLogger.OpenLogFolder();
                result.ToastMessage = "??? Studio ????";
                CDBoxStudioLogger.Info("?? Studio ????????" + CDBoxStudioLogger.LogDirectory);
            }
            catch (Exception ex)
            {
                result.ToastMessage = "?????????" + ex.Message;
                result.ToastKind = "error";
                CDBoxStudioLogger.Error("?? Studio ???????", ex);
            }

            return result;
        }

    }
}
