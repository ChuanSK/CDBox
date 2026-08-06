using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using TCPipeAutoDraw.Core.Modules;
using TCPipeAutoDraw.Core.Startup;
using TCPipeAutoDraw.Modules.LayerManager;
using TCPipeAutoDraw.Modules.AnnotationSettings;
using TCPipeAutoDraw.Modules.FrameLayout;
using TCPipeAutoDraw.Modules.PipeDraw;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;
using TCPipeAutoDraw.Modules.NodeAnnotation;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.Modules.SectionDrawing;
using TCPipeAutoDraw.Modules.SurfaceAreaAnnotation;
using TCPipeAutoDraw.Modules.ExcelToCad;
using TCPipeAutoDraw.UI;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Commands
{
    /// <summary>
    /// ?????????????????????????? Modules ??
    /// </summary>
    public sealed class TCPipeCommands : IExtensionApplication
    {
        private const int StartupCdboxSuppressSeconds = 20;
        private bool _startupWorkflowQueued;
        private bool _startupWorkflowFinished;
        private DateTime _initializedAtUtc;
        private int _startupUpdateCheckStarted;

        public void Initialize()
        {
            _initializedAtUtc = DateTime.UtcNow;
            _startupWorkflowFinished = false;
            CDBoxNotificationService.InitializeForCurrentThread();

            Document doc = AcadApp.DocumentManager.MdiActiveDocument;

            if (IsHeadlessSelfTest())
            {
                if (doc != null) doc.Editor.WriteHudMessage("\n[CDBox] ?????????????????????????????\n");
                _startupWorkflowFinished = true;
                return;
            }

            EnsureMenuBar();
            PipeLengthAnnotationInteractionService.Initialize();
            QueueStartupWorkflow();
        }

        public void Terminate()
        {
            PipeLengthAnnotationInteractionService.Terminate();
            if (IsHeadlessSelfTest()) return;
            CDBoxMenuService.RemoveMenu();
        }

        [CommandMethod("CDSELFTEST", CommandFlags.Modal)]
        public void RunSelfTest()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            Editor editor = doc == null ? null : doc.Editor;
            var results = new List<string>();
            int failureCount = 0;

            Action<bool, string, string> check = delegate(bool passed, string name, string detail)
            {
                if (!passed) failureCount++;
                results.Add("[" + (passed ? "PASS" : "FAIL") + "] " + name + (string.IsNullOrWhiteSpace(detail) ? string.Empty : "?" + detail));
            };

            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string baseDirectory = Path.GetDirectoryName(assemblyPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            check(File.Exists(assemblyPath), "????", assemblyPath);
            check(string.Equals(CDBoxStudioUpdateService.ReleaseIdentity, "CDBox-Studio-Preview-3.4.1", StringComparison.OrdinalIgnoreCase), "????", CDBoxStudioUpdateService.ReleaseIdentity);
            check(CDBoxStudioUpdateService.CurrentVersionCode == 30401, "???", CDBoxStudioUpdateService.CurrentVersionCode.ToString());
            check(File.Exists(Path.Combine(baseDirectory, "Microsoft.Web.WebView2.Core.dll")), "WebView2 Core", Path.Combine(baseDirectory, "Microsoft.Web.WebView2.Core.dll"));
            check(File.Exists(Path.Combine(baseDirectory, "Microsoft.Web.WebView2.WinForms.dll")), "WebView2 WinForms", Path.Combine(baseDirectory, "Microsoft.Web.WebView2.WinForms.dll"));
            check(File.Exists(Path.Combine(baseDirectory, "runtimes", "win-x64", "native", "WebView2Loader.dll")), "WebView2 Loader", Path.Combine(baseDirectory, "runtimes", "win-x64", "native", "WebView2Loader.dll"));
            check(File.Exists(Path.Combine(baseDirectory, "Updater", "CDBoxUpdater.exe")), "?????", Path.Combine(baseDirectory, "Updater", "CDBoxUpdater.exe"));
            check(File.Exists(Path.Combine(baseDirectory, "Templates", "????????.xls")), "?????", Path.Combine(baseDirectory, "Templates", "????????.xls"));

            CDBoxStudioRuntimeInfo runtime = CDBoxStudioRuntime.Detect();
            check(runtime != null && runtime.Available, "WebView2 Runtime", runtime == null ? "??????" : (runtime.Available ? runtime.Version : runtime.ErrorMessage));
            check(doc != null, "????", doc == null ? "???????" : doc.Name);

            if (doc != null)
            {
                try
                {
                    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        LayerTable layers = tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead, false) as LayerTable;
                        int layerCount = 0;
                        if (layers != null)
                        {
                            foreach (ObjectId ignored in layers) layerCount++;
                        }
                        check(layers != null, "???????", layers == null ? "???????" : "???=" + layerCount);
                        tr.Commit();
                    }
                }
                catch (System.Exception ex)
                {
                    check(false, "???????", ex.Message);
                }
            }

            string processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            check(!string.IsNullOrWhiteSpace(processName), "????", processName);

            if (editor != null)
            {
                editor.WriteHudMessage("\n========== CDBox Self Test ==========");
                foreach (string line in results) editor.WriteHudMessage("\n" + line);
                editor.WriteHudMessage("\nCDBOX_SELFTEST_RESULT=" + (failureCount == 0 ? "PASS" : "FAIL") + "\n");
            }
            else
            {
                foreach (string line in results) Console.WriteLine(line);
                Console.WriteLine("CDBOX_SELFTEST_RESULT=" + (failureCount == 0 ? "PASS" : "FAIL"));
            }
        }

        private static bool IsHeadlessSelfTest()
        {
            string value = Environment.GetEnvironmentVariable("CDBOX_HEADLESS_SELFTEST");
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        [CommandMethod("CDMENU", CommandFlags.Modal)]
        public void RebuildMenuBar()
        {
            EnsureMenuBar(true);
        }

        [CommandMethod("CDMENURESET", CommandFlags.Modal)]
        public void ResetMenuBar()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            try
            {
                CDBoxMenuService.ResetMenu();
                if (doc != null) doc.Editor.WriteHudMessage("\n[CDBox ??] ????????????????????");
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), ex.Message, "CDBox ??????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        [CommandMethod("CDMENUDUMP", CommandFlags.Modal)]
        public void DumpMenuBar()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            string text = CDBoxMenuService.GetDiagnosticText();
            if (doc != null) doc.Editor.WriteHudMessage("\n" + text.Replace("\r", string.Empty).Replace("\n", "\n"));
        }

        private void EnsureMenuBar(bool showResultMessage = false)
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            try
            {
                CDBoxMenuService.EnsureMenu(showResultMessage);
                if (showResultMessage && doc != null) doc.Editor.WriteHudMessage("\n[CDBox ??] ????????????");
            }
            catch (System.Exception ex)
            {
                if (showResultMessage)
                    TCPipeAutoDraw.UI.CDBoxMessageBox.Show(
                        new AcadMainWindow(), ex.Message,
                        "CDBox ??????", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                else if (doc != null)
                    doc.Editor.WriteHudMessage(
                        "\n[CDBox ??] ?????" + ex.Message);
            }
        }

        [CommandMethod("CDBOX", CommandFlags.Modal)]
        public void OpenToolbox()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (ShouldSuppressStartupCdboxCommand())
            {
                if (doc != null) doc.Editor.WriteHudMessage("\n");
                return;
            }

            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), "????????", "CDBox", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowStudio();
        }

        [CommandMethod("CDSTUDIO", CommandFlags.Modal)]
        public void OpenStudio()
        {
            ShowStudio();
        }

        [CommandMethod("CDS", CommandFlags.Modal)]
        public void OpenStudioAlias()
        {
            ShowStudio();
        }

        [CommandMethod("CDSET", CommandFlags.Modal)]
        public void OpenSettings()
        {
            CDBoxStudioSettingsWindow.ShowWindow(new AcadMainWindow());
        }

        [CommandMethod("CDABOUT", CommandFlags.Modal)]
        public void OpenAboutChaozhongqing()
        {
            TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), "????????????????????", "????????", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        [CommandMethod("CDINSTALL", CommandFlags.Modal)]
        public void InstallAutoLoad()
        {
            CDBoxInstallResult result = CDBoxInstaller.InstallToCadDirectory();
            CDBoxAppSettings settings = CDBoxAppSettingsStore.Load();
            if (result.Success) settings.InstalledPath = result.InstallRoot;
            CDBoxAppSettingsStore.Save(settings);

            TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), result.Message + "\r\n\r\n?????" + result.InstallRoot, result.Success ? "CDBox ????" : "CDBox ????", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        [CommandMethod("CDUPDATE", CommandFlags.Modal)]
        public void UpdateAutoLoad()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "?????????????? CDBox.dll????????????";
                dialog.Filter = "CDBox.dll|CDBox.dll|DLL ?? (*.dll)|*.dll|???? (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;

                if (dialog.ShowDialog(new AcadMainWindow()) != DialogResult.OK) return;

                CDBoxInstallResult result = CDBoxInstaller.ScheduleUpdateFromDll(dialog.FileName);
                CDBoxAppSettings settings = CDBoxAppSettingsStore.Load();
                if (result.Success) settings.InstalledPath = result.InstallRoot;
                CDBoxAppSettingsStore.Save(settings);

                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), result.Message + "\r\n\r\n?????" + result.InstallRoot, result.Success ? "CDBox ??" : "CDBox ????", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
        }

        [CommandMethod("CDUNINSTALL", CommandFlags.Modal)]
        public void UninstallAutoLoad()
        {
            DialogResult confirm = TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), "???? CDBox ?????????????\r\n\r\n?????????? CAD ??????????? CAD ????????", "?? CDBox", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            CDBoxMenuService.RemoveMenu(true);

            CDBoxInstallResult result = CDBoxInstaller.Uninstall();
            CDBoxAppSettings settings = CDBoxAppSettingsStore.Load();
            settings.InstalledPath = string.Empty;
            CDBoxAppSettingsStore.Save(settings);

            TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), result.Message + "\r\n\r\n?????" + result.InstallRoot, result.Success ? "CDBox ??" : "CDBox ????", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void QueueStartupWorkflow()
        {
            if (_startupWorkflowQueued) return;
            _startupWorkflowQueued = true;
            AcadApp.Idle += OnAcadIdleForStartupWorkflow;
        }

        private void OnAcadIdleForStartupWorkflow(object sender, EventArgs e)
        {
            AcadApp.Idle -= OnAcadIdleForStartupWorkflow;
            try
            {
                RunStartupWorkflow();
            }
            finally
            {
                _startupWorkflowFinished = true;
            }
        }

        private void RunStartupWorkflow()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            try
            {
                CDBoxAppSettings settings = CDBoxAppSettingsStore.Load();
                bool settingsChanged = false;
                string installPromptIdentity = CDBoxStudioUpdateService.ReleaseIdentity + ":" + CDBoxStudioUpdateService.CurrentVersionCode.ToString();

                if (settings.ShouldPromptForInstall(CDBoxInstaller.IsInstalled(), installPromptIdentity))
                {
                    bool doNotAsk;
                    DialogResult installResult = CDBoxPromptDialog.ShowYesNo(
                        new AcadMainWindow(),
                        "CDBox ??????",
                        "??? CDBox ????? CAD ?????\r\n\r\n?????????????? CAD ????? CDBox??????? NETLOAD?\r\n\r\n?????" + CDBoxInstaller.GetInstallRoot(),
                        "??",
                        "????",
                        out doNotAsk);

                    if (installResult == DialogResult.Yes)
                    {
                        CDBoxInstallResult result = CDBoxInstaller.InstallToCadDirectory();
                        if (result.Success)
                        {
                            settings.InstalledPath = result.InstallRoot;
                            settingsChanged = true;
                        }

                        TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), result.Message + "\r\n\r\n?????" + result.InstallRoot, result.Success ? "CDBox ????" : "CDBox ????", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                    }

                    settings.LastInstallPromptIdentity = installPromptIdentity;
                    settingsChanged = true;

                    if (doNotAsk)
                    {
                        settings.PromptInstallOnLoad = false;
                        settingsChanged = true;
                    }
                }

                if (settingsChanged) CDBoxAppSettingsStore.Save(settings);
            }
            catch (System.Exception ex)
            {
                if (doc != null) doc.Editor.WriteHudMessage("\n[????] " + ex.Message);
            }

            QueueStartupUpdateCheck();
        }

        private void QueueStartupUpdateCheck()
        {
            if (Interlocked.Exchange(ref _startupUpdateCheckStarted, 1) != 0) return;
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            ThreadPool.QueueUserWorkItem(delegate
            {
                CDBoxStudioUpdateResult result;
                try
                {
                    result = CDBoxStudioUpdateService.Check(CDBoxStudioSettingsStore.Load());
                }
                catch (System.Exception ex)
                {
                    CDBoxStudioLogger.Error("?????????", ex);
                    return;
                }

                if (result == null || !result.Success || !result.UpdateAvailable) return;
                try
                {
                    dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                    {
                        try
                        {
                            if (AcadApp.DocumentManager.MdiActiveDocument == null) return;
                            string message = "?? CDBox ??? " + result.LatestVersion
                                + "????? " + result.CurrentVersion + "??\r\n\r\n"
                                + "???????????????????????? CDBox ??????????";
                            if (!string.IsNullOrWhiteSpace(result.Notes))
                                message += "\r\n\r\n?????\r\n" + result.Notes.Trim();
                            DialogResult choice = TCPipeAutoDraw.UI.CDBoxMessageBox.Show(
                                new AcadMainWindow(), message, "CDBox ????",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                                MessageBoxDefaultButton.Button2);
                            if (choice == DialogResult.Yes)
                                CDBoxStudioSettingsWindow.ShowWindow(new AcadMainWindow());
                        }
                        catch (System.Exception ex)
                        {
                            CDBoxStudioLogger.Error("???????????", ex);
                        }
                    }));
                }
                catch (System.Exception ex)
                {
                    CDBoxStudioLogger.Error("???????????", ex);
                }
            });
        }


        private bool ShouldSuppressStartupCdboxCommand()
        {
            try
            {
                if (!CDBoxInstaller.IsRunningFromInstallFolder()) return false;

                TimeSpan elapsed = DateTime.UtcNow - _initializedAtUtc;
                if (!_startupWorkflowFinished) return true;
                if (elapsed.TotalSeconds >= 0 && elapsed.TotalSeconds <= StartupCdboxSuppressSeconds) return true;
            }
            catch
            {
            }

            return false;
        }

        private void ShowStudio()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), "????????", "CDBox Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioHost.Show(CreateDefaultModules());
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), ex.Message, "CDBox Studio ????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private IList<ITCModule> CreateDefaultModules()
        {
            return TCModuleRegistry.CreateDefaultModules(DrawPipeFromPrompt, ShowLayerManager, ShowAnnotationSettingsForm, ShowSurfaceAreaAnnotationForm, ShowPipeLengthAnnotationForm, ShowNodeAnnotationSettings, ShowSectionDrawing, RunFrameTemplateAdd, RunFrameCutLayout, ShowQuantityPipeAttributeEditor, RunQuantityCalculationReport, ShowExcelToCad);
        }

        [CommandMethod("TCP_LAYERS", CommandFlags.Modal)]
        public void ShowLayerManagerByFullName()
        {
            ShowLayerManager();
        }

        [CommandMethod("CDLAYER", CommandFlags.Modal)]
        public void ShowLayerManagerByShortName()
        {
            ShowLayerManager();
        }

        [CommandMethod("TCGL", CommandFlags.Modal)]
        public void ShowLayerManagerByChineseShortName()
        {
            ShowLayerManager();
        }

        [CommandMethod("GDTC", CommandFlags.Modal)]
        public void ShowLayerManagerByOldLayerName()
        {
            ShowLayerManager();
        }


        [CommandMethod("CDBZSET", CommandFlags.Modal)]
        public void ShowAnnotationSettingsByEnglishName()
        {
            ShowAnnotationSettingsForm();
        }

        [CommandMethod("BZSZ", CommandFlags.Modal)]
        public void ShowAnnotationSettingsByChineseShortName()
        {
            ShowAnnotationSettingsForm();
        }


        [CommandMethod("CDSURF", CommandFlags.Modal)]
        public void ShowSurfaceAreaAnnotationByEnglishName()
        {
            RunSurfaceAreaAnnotationDirect();
        }

        [CommandMethod("BMJ", CommandFlags.Modal)]
        public void ShowSurfaceAreaAnnotationByChineseShortName()
        {
            RunSurfaceAreaAnnotationDirect();
        }

        [CommandMethod("MJBZ", CommandFlags.Modal)]
        public void ShowSurfaceAreaAnnotationByAreaChineseName()
        {
            RunSurfaceAreaAnnotationDirect();
        }


        [CommandMethod("CDLEN", CommandFlags.Modal)]
        public void ShowPipeLengthAnnotationByEnglishName()
        {
            RunPipeLengthAnnotationDirect();
        }

        [CommandMethod("GCBZ", CommandFlags.Modal)]
        public void ShowPipeLengthAnnotationByChineseShortName()
        {
            RunPipeLengthAnnotationDirect();
        }

        [CommandMethod("GXCDBZ", CommandFlags.Modal)]
        public void ShowPipeLengthAnnotationByOldChineseName()
        {
            RunPipeLengthAnnotationDirect();
        }

        [CommandMethod("CDNODE", CommandFlags.Modal)]
        public void ShowNodeAnnotationByEnglishName()
        {
            RunNodeAnnotationDirect();
        }

        [CommandMethod("JDBZ", CommandFlags.Modal)]
        public void ShowNodeAnnotationByChineseShortName()
        {
            RunNodeAnnotationDirect();
        }


        [CommandMethod("CDSEC", CommandFlags.Modal)]
        public void ShowSectionDrawingByEnglishName()
        {
            ShowSectionDrawing();
        }

        [CommandMethod("DM", CommandFlags.Modal)]
        public void ShowSectionDrawingByChineseShortName()
        {
            ShowSectionDrawing();
        }

        [CommandMethod("DMTS", CommandFlags.Modal)]
        public void ShowSectionDrawingByOldChineseName()
        {
            ShowSectionDrawing();
        }

        [CommandMethod("DMLEGACY", CommandFlags.Modal)]
        public void ShowLegacySectionDrawing()
        {
            ShowSectionDrawingLegacy();
        }


        [CommandMethod("PLDM", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void GenerateSectionDrawingBatchByShortName()
        {
            RunSectionDrawingBatch();
        }

        [CommandMethod("CDQTY", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void ShowQuantityPipeAttributeEditorByEnglishName()
        {
            ShowQuantityPipeAttributeEditor();
        }

        [CommandMethod("SX", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void ShowQuantityPipeAttributeEditorBySxName()
        {
            ShowQuantityPipeAttributeEditor();
        }

        [CommandMethod("GCSX", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void ShowQuantityPipeAttributeEditorByChineseShortName()
        {
            ShowQuantityPipeAttributeEditor();
        }

        [CommandMethod("SXLEGACY", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void ShowLegacyQuantityPipeAttributeEditor()
        {
            ShowQuantityPipeAttributeEditor(true);
        }

        [CommandMethod("SXQC", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void ClearQuantityAttributesByShortName()
        {
            ClearQuantityAttributes();
        }

        [CommandMethod("CDQTYCLEAR", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void ClearQuantityAttributesByEnglishName()
        {
            ClearQuantityAttributes();
        }

        [CommandMethod("SXMRB", CommandFlags.Modal)]
        public void ShowQuantityDefaultProfileEditorByShortName()
        {
            ShowQuantityDefaultProfileEditor();
        }

        [CommandMethod("GCL", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void GenerateQuantityCalculationReportByShortName()
        {
            RunQuantityCalculationReport();
        }

        [CommandMethod("CDEXCEL", CommandFlags.Modal)]
        public void ShowExcelToCadByEnglishName()
        {
            ShowExcelToCad();
        }

        [CommandMethod("GU_XL", CommandFlags.Modal)]
        public void ShowExcelToCadByLegacyName()
        {
            ShowExcelToCad();
        }

        [CommandMethod("CDQBOARD", CommandFlags.Modal)]
        public void ShowQuantityDashboardWindowByEnglishName()
        {
            ShowQuantityDashboardWindow();
        }


        [CommandMethod("CDDRAWNET", CommandFlags.Modal)]
        public void DrawPipeFromPrompt()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "??????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Editor ed = doc.Editor;
            var opt = new PromptOpenFileOptions("\n???????? TXT/DAT/CSV ??")
            {
                Filter = "??/???? (*.txt;*.dat;*.csv)|*.txt;*.dat;*.csv|???? (*.*)|*.*"
            };

            PromptFileNameResult res = ed.GetFileNameForOpen(opt);
            if (res.Status != PromptStatus.OK) return;

            try
            {
                var module = new PipeDrawModule();
                PipeDrawResult result = module.Run(doc, res.StringResult, PipeDrawOptions.Default);
                ed.WriteHudMessage(result.ToEditorMessage());
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        [CommandMethod("ZDTXNET", CommandFlags.Modal)]
        public void DrawPipeFromPromptAlias()
        {
            DrawPipeFromPrompt();
        }

        private void ShowLayerManager()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioLayerManagerWindow.ShowWindow(new AcadMainWindow());
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void ShowAnnotationSettingsForm()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioAnnotationSettingsWindow.ShowWindow(new AcadMainWindow(), "surface");
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void ShowSurfaceAreaAnnotationForm()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "?????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioAnnotationSettingsWindow.ShowWindow(new AcadMainWindow(), "surface");
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "?????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void ShowPipeLengthAnnotationForm()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "??????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioAnnotationSettingsWindow.ShowWindow(new AcadMainWindow(), "pipeLength");
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "??????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        private void RunSurfaceAreaAnnotationDirect()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "?????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                SurfaceAreaAnnotationOptions options = SurfaceAreaAnnotationSettingsStore.Load();
                SurfaceAreaAnnotationResult result = SurfaceAreaAnnotationService.SelectCalculateAndAnnotate(doc, options);
                doc.Editor.WriteHudMessage(result.ToEditorMessage());
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "???????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunPipeLengthAnnotationDirect()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "??????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                PipeLengthAnnotationOptions options = PipeLengthAnnotationSettingsStore.Load();
                while (true)
                {
                    PipeLengthAnnotationResult result = PipeLengthAnnotationService.SelectCalculateAndAnnotate(doc, options);
                    doc.Editor.WriteHudMessage(result.ToEditorMessage());
                    if (result.IsCancelled) break;
                }
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        private void RunNodeAnnotationDirect()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                NodeAnnotationOptions options = NodeAnnotationSettingsStore.Load();
                while (true)
                {
                    NodeAnnotationResult result = NodeAnnotationService.SelectAndAnnotate(doc, options);
                    doc.Editor.WriteHudMessage(result.ToEditorMessage());
                    if (!result.Success) break;
                }
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "??????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        private void ShowSectionDrawing()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "?????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioSectionDrawingWindow.ShowWindow(new AcadMainWindow());
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "?????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowNodeAnnotationSettings()
        {
            CDBoxStudioAnnotationSettingsWindow.ShowWindow(new AcadMainWindow(), "node");
        }

        private void ShowSectionDrawingLegacy()
        {
            ShowSectionDrawing();
        }


        private void RunSectionDrawingBatch()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "?????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                SectionBatchDrawingResult result;
                using (var progress = new CDBoxProgressForm("???????", new AcadMainWindow()))
                {
                    result = SectionBatchDrawingService.Run(doc, progress.Report);
                }
                if (result.Success)
                {
                    TCPipeAutoDraw.UI.CDBoxMessageBox.Show("???????????? " + result.SuccessSectionCount + " ?????", "?????", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    TCPipeAutoDraw.UI.CDBoxMessageBox.Show(result.Message, "?????", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "???????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunFrameTemplateAdd()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "??????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                new FrameLayoutCommands().AddFrameTemplate();
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunFrameCutLayout()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "??????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                new FrameLayoutCommands().CutAndLayoutFrames();
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowQuantityPipeAttributeEditor()
        {
            ShowQuantityPipeAttributeEditor(false);
        }

        private void ShowQuantityPipeAttributeEditor(bool legacy)
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                ObjectId[] ids = QuantityPipeAttributeService.ReadImpliedOrPromptObjectIds(doc);
                if (ids == null || ids.Length == 0) return;

                if (ids.Length == 1)
                {
                    if (!legacy && QuantityAttributeDoubleClickService.TryOpen(doc, ids)) return;

                    QuantityPipeSelectionInfo info = QuantityPipeAttributeService.ReadPipe(doc, ids[0]);
                    if (info == null)
                    {
                        TCPipeAutoDraw.UI.CDBoxMessageBox.Show("?????????????????/??????????\n?????????????/???????????", "????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (legacy)
                    {
                        using (Form form = QuantityAttributeEditorFormFactory.Create(doc, info))
                            form.ShowDialog(new AcadMainWindow());
                    }
                    else
                    {
                        CDBoxStudioQuantityAttributeEditorWindow.ShowWindow(new AcadMainWindow(), info,
                            QuantityDashboardService.GetDocumentId(doc));
                    }
                    return;
                }

                QuantityPipeWriteResult result;
                using (var progress = new CDBoxProgressForm("????", new AcadMainWindow()))
                {
                    result = QuantityPipeAttributeService.ApplyDefaultProfilesToObjects(doc, ids, progress.Report);
                }
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(result.Message, "????", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearQuantityAttributes()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                ObjectId[] ids = QuantityPipeAttributeService.ReadImpliedOrPromptObjectIds(doc);
                if (ids == null || ids.Length == 0) return;

                DialogResult confirm = TCPipeAutoDraw.UI.CDBoxMessageBox.Show("??????????????????????", "????", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (confirm != DialogResult.OK) return;

                QuantityPipeWriteResult result = QuantityPipeAttributeService.ClearAttributesFromObjects(doc, ids);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(result.Message, "????", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "??????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowQuantityDefaultProfileEditor()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "?????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioDefaultProfilesWindow.ShowWindow(new AcadMainWindow());
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "?????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowExcelToCad()
        {
            ExcelToCadCommandService.Run(AcadApp.DocumentManager.MdiActiveDocument,
                new AcadMainWindow());
        }

        private void RunQuantityCalculationReport()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("????????", "???????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                ObjectId[] scopeIds = PromptQuantityReportScope(doc);
                if (scopeIds == null || scopeIds.Length == 0) return;

                string defaultName = BuildQuantityReportDefaultFileName(doc);
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Title = "????????";
                    dialog.Filter = "Excel 97-2003 ??? (*.xls)|*.xls|???? (*.*)|*.*";
                    dialog.FileName = defaultName;
                    dialog.AddExtension = true;
                    dialog.DefaultExt = "xls";
                    TrySetSaveDialogInitialDirectory(dialog, doc);
                    if (dialog.ShowDialog(new AcadMainWindow()) != DialogResult.OK) return;

                    QuantityCalculationReport report;
                    using (var progress = new CDBoxProgressForm("???????", new AcadMainWindow()))
                    {
                        report = QuantityCalculationReportService.BuildReport(doc, scopeIds, progress.Report);
                        progress.ReportMarquee("?????????...");
                        QuantityExcelXmlExporter.Export(dialog.FileName, report);
                        progress.Complete("??????????");
                    }

                    string message = "??????????? " + report.MainPipes.Count + " ????/??? " + report.Wells.Count + " ??";
                    if (report.Warnings.Count > 0) message += " ?? " + report.Warnings.Count + " ???????????";
                    TCPipeAutoDraw.UI.CDBoxMessageBox.Show(
                        message + "\r\n\r\n" + dialog.FileName,
                        "???????", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "?????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowQuantityDashboardWindow()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), "????????", "???????", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioQuantityDashboardWindow.ShowWindow(new AcadMainWindow());
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), ex.Message, "???????????", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static ObjectId[] PromptQuantityReportScope(Document doc)
        {
            if (doc == null) return new ObjectId[0];
            Editor ed = doc.Editor;

            PromptSelectionOptions opt = new PromptSelectionOptions();
            opt.MessageForAdding = "\n?????????????????????/????";
            opt.MessageForRemoval = "\n?????";

            PromptSelectionResult res = ed.GetHudSelection(opt);
            if (res.Status != PromptStatus.OK || res.Value == null || res.Value.Count == 0)
            {
                ed.WriteHudMessage("\n[GCL] ????????????");
                return new ObjectId[0];
            }

            return res.Value.GetObjectIds();
        }

        private static string BuildQuantityReportDefaultFileName(Document doc)
        {
            string title = GetDrawingTitle(doc);
            if (string.IsNullOrWhiteSpace(title)) title = "????";
            return SanitizeFileName(title + "??????") + ".xls";
        }

        private static string GetDrawingTitle(Document doc)
        {
            if (doc == null) return string.Empty;

            try
            {
                if (doc.Database != null)
                {
                    DatabaseSummaryInfo summary = doc.Database.SummaryInfo;
                    if (!string.IsNullOrWhiteSpace(summary.Title)) return summary.Title.Trim();
                }
            }
            catch
            {
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(doc.Name)) return Path.GetFileNameWithoutExtension(doc.Name);
            }
            catch
            {
            }

            return string.Empty;
        }

        private static void TrySetSaveDialogInitialDirectory(SaveFileDialog dialog, Document doc)
        {
            if (dialog == null || doc == null) return;
            try
            {
                string drawingPath = doc.Name;
                string dir = string.IsNullOrWhiteSpace(drawingPath) ? string.Empty : Path.GetDirectoryName(drawingPath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir)) dialog.InitialDirectory = dir;
            }
            catch
            {
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "??????";
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++) fileName = fileName.Replace(invalid[i], '_');
            return fileName.Trim();
        }

    }
}
