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
using TCPipeAutoDraw.Core.Business;
using TCPipeAutoDraw.Core.FloatingCenter;
using TCPipeAutoDraw.Core.Check;
using TCPipeAutoDraw.Core.Sync;
using TCPipeAutoDraw.Core.Startup;
using TCPipeAutoDraw.Modules.PipeDraw;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI;
using TCPipeAutoDraw.UI.FloatingCenter;
using TCPipeAutoDraw.UI.Studio;
using CDBox.Shared.Components;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Commands
{
    /// <summary>
    /// 统一插件入口：命令只负责启动界面或模块，业务逻辑放到 Modules 中。
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
            FloatingCenter.Configure(new FloatingCenterService(
                CadFloatingDocumentIdentity.GetCurrentDocumentId));
            CadFloatingCenterLifetime.Initialize(FloatingCenter.Current);
            CDBoxNotificationService.InitializeForCurrentThread();

            Document doc = AcadApp.DocumentManager.MdiActiveDocument;

            if (IsHeadlessSelfTest())
            {
                if (doc != null) doc.Editor.WriteHudMessage("\n[CDBox] 已进入无界面自检模式，跳过菜单、安装提示和侧边栏启动流程。\n");
                _startupWorkflowFinished = true;
                return;
            }

            EnsureMenuBar();
            CDBoxBusinessModeService.Initialize();
            FloatingCenterController.Initialize(FloatingCenter.Current);
            BaseLayerManagerHost.Initialize();
            try { CommonModuleHost.Initialize(); }
            catch (System.Exception ex)
            {
                CDBoxStudioLogger.Error(
                    "Common 初始化边界发生未处理异常，已隔离。", ex);
            }
            try { WastewaterModuleHost.Initialize(); }
            catch (System.Exception ex)
            {
                CDBoxStudioLogger.Error(
                    "Wastewater 初始化边界发生未处理异常，已隔离。", ex);
            }
            if (WastewaterModuleHost.IsAvailable)
            {
                CadDrawingCheckCoordinator.Initialize();
                CadSyncCoordinator.Initialize();
            }
            try { RealEstateModuleHost.Initialize(); }
            catch (System.Exception ex)
            {
                CDBoxStudioLogger.Error(
                    "RealEstate 初始化边界发生未处理异常，已隔离。", ex);
            }
            QueueStartupWorkflow();
        }

        public void Terminate()
        {
            CadSyncCoordinator.Terminate();
            CadDrawingCheckCoordinator.Terminate();
            try { RealEstateModuleHost.Shutdown(); }
            catch (System.Exception ex)
            {
                CDBoxStudioLogger.Error(
                    "RealEstate 关闭边界发生未处理异常，已隔离。", ex);
            }
            try { WastewaterModuleHost.Shutdown(); }
            catch (System.Exception ex)
            {
                CDBoxStudioLogger.Error(
                    "Wastewater 关闭边界发生未处理异常，已隔离。", ex);
            }
            try { CommonModuleHost.Shutdown(); }
            catch (System.Exception ex)
            {
                CDBoxStudioLogger.Error(
                    "Common 关闭边界发生未处理异常，已隔离。", ex);
            }
            BaseLayerManagerHost.Shutdown();
            FloatingCenterController.Terminate();
            CadFloatingCenterLifetime.Terminate();
            try { FloatingCenter.Current.ClearAll(); } catch { }
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
                results.Add("[" + (passed ? "PASS" : "FAIL") + "] " + name + (string.IsNullOrWhiteSpace(detail) ? string.Empty : "：" + detail));
            };

            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string baseDirectory = Path.GetDirectoryName(assemblyPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            check(File.Exists(assemblyPath), "主程序集", assemblyPath);
            check(File.Exists(Path.Combine(baseDirectory, "CDBox.Shared.dll")), "Shared 程序集", Path.Combine(baseDirectory, "CDBox.Shared.dll"));
            bool commonSelected = CDBoxComponentBundle
                .IsInstalledFromContentsDirectory(baseDirectory,
                    CDBoxComponentIds.Common);
            check(!commonSelected || File.Exists(Path.Combine(baseDirectory,
                    "CDBox.Common.dll")), "Common 程序集",
                commonSelected ? Path.Combine(baseDirectory,
                    "CDBox.Common.dll") : "未选择安装");
            bool wastewaterSelected = CDBoxComponentBundle
                .IsInstalledFromContentsDirectory(baseDirectory,
                    CDBoxComponentIds.Wastewater);
            check(!wastewaterSelected || File.Exists(Path.Combine(
                    baseDirectory, "CDBox.Wastewater.dll")),
                "Wastewater 程序集", wastewaterSelected
                    ? Path.Combine(baseDirectory, "CDBox.Wastewater.dll")
                    : "未选择安装");
            bool realEstateSelected = CDBoxComponentBundle
                .IsInstalledFromContentsDirectory(baseDirectory,
                    CDBoxComponentIds.RealEstate);
            check(!realEstateSelected || File.Exists(Path.Combine(baseDirectory,
                    "CDBox.RealEstate.dll")), "RealEstate 程序集",
                realEstateSelected ? Path.Combine(baseDirectory,
                    "CDBox.RealEstate.dll") : "未选择安装");
            check(string.Equals(CDBoxStudioUpdateService.ReleaseIdentity, "CDBox-Studio-Preview-5.1.0", StringComparison.OrdinalIgnoreCase), "发布身份", CDBoxStudioUpdateService.ReleaseIdentity);
            check(CDBoxStudioUpdateService.CurrentVersionCode == 50100, "版本码", CDBoxStudioUpdateService.CurrentVersionCode.ToString());
            check(File.Exists(Path.Combine(baseDirectory, "Microsoft.Web.WebView2.Core.dll")), "WebView2 Core", Path.Combine(baseDirectory, "Microsoft.Web.WebView2.Core.dll"));
            check(File.Exists(Path.Combine(baseDirectory, "Microsoft.Web.WebView2.WinForms.dll")), "WebView2 WinForms", Path.Combine(baseDirectory, "Microsoft.Web.WebView2.WinForms.dll"));
            check(File.Exists(Path.Combine(baseDirectory, "runtimes", "win-x64", "native", "WebView2Loader.dll")), "WebView2 Loader", Path.Combine(baseDirectory, "runtimes", "win-x64", "native", "WebView2Loader.dll"));
            check(File.Exists(Path.Combine(baseDirectory, "Templates", "工程量计算表模板.xls")), "工程量模板", Path.Combine(baseDirectory, "Templates", "工程量计算表模板.xls"));

            CDBoxStudioRuntimeInfo runtime = CDBoxStudioRuntime.Detect();
            check(runtime != null && runtime.Available, "WebView2 Runtime", runtime == null ? "检测结果为空" : (runtime.Available ? runtime.Version : runtime.ErrorMessage));
            check(doc != null, "当前图纸", doc == null ? "未找到活动文档" : doc.Name);

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
                        check(layers != null, "数据库只读事务", layers == null ? "无法打开图层表" : "图层数=" + layerCount);
                        tr.Commit();
                    }
                }
                catch (System.Exception ex)
                {
                    check(false, "数据库只读事务", ex.Message);
                }
            }

            string processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            check(!string.IsNullOrWhiteSpace(processName), "宿主进程", processName);

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

        [CommandMethod("CDBCHECK", CommandFlags.Modal)]
        public void RunDrawingCheck()
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            if (CadDrawingCheckCoordinator.IsRunning(document))
                CadDrawingCheckCoordinator.RequestCancel(document);
            else CadDrawingCheckCoordinator.Start(document, true);
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
                if (doc != null) doc.Editor.WriteHudMessage("\n[CDBox 菜单] 已清理旧菜单并重新生成超重氢工具箱菜单。");
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), ex.Message, "CDBox 菜单重置失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                if (showResultMessage && doc != null) doc.Editor.WriteHudMessage("\n[CDBox 菜单] 已重建超重氢工具箱菜单。");
            }
            catch (System.Exception ex)
            {
                if (showResultMessage)
                    TCPipeAutoDraw.UI.CDBoxMessageBox.Show(
                        new AcadMainWindow(), ex.Message,
                        "CDBox 菜单创建失败", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                else if (doc != null)
                    doc.Editor.WriteHudMessage(
                        "\n[CDBox 菜单] 创建失败：" + ex.Message);
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
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), "未找到当前图纸。", "CDBox", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WastewaterModuleHost.OpenWorkspace();
        }

        [CommandMethod("CDSTUDIO", CommandFlags.Modal)]
        public void OpenStudio()
        {
            WastewaterModuleHost.OpenWorkspace();
        }

        [CommandMethod("CDS", CommandFlags.Modal)]
        public void OpenStudioAlias()
        {
            WastewaterModuleHost.OpenWorkspace();
        }

        [CommandMethod("WSGCGB", CommandFlags.Modal
            | CommandFlags.UsePickSet)]
        [CommandMethod("CDWELLTABLE", CommandFlags.Modal
            | CommandFlags.UsePickSet)]
        public void DrawWastewaterResultTable()
        {
            WastewaterModuleHost.ExecuteCommand(
                "wastewater-result-table");
        }

        [CommandMethod("CDRE", CommandFlags.Modal)]
        public void OpenRealEstate()
        {
            CDBoxBusinessModeService.SetMode(CDBoxBusinessMode.RealEstate);
            RealEstateModuleHost.OpenWorkspace();
        }

        [CommandMethod("CDBOXRE", CommandFlags.Modal)]
        public void OpenRealEstateAlias()
        {
            CDBoxBusinessModeService.SetMode(CDBoxBusinessMode.RealEstate);
            RealEstateModuleHost.OpenWorkspace();
        }

        [CommandMethod("CDREDJ", CommandFlags.Modal)]
        public void OpenParcelSurveyEditor()
        {
            CDBoxBusinessModeService.SetMode(CDBoxBusinessMode.RealEstate);
            RealEstateModuleHost.ExecuteCommand("CDREDJ");
        }

        [CommandMethod("DJXX", CommandFlags.Modal)]
        public void OpenParcelSurveyEditorAlias()
        {
            CDBoxBusinessModeService.SetMode(CDBoxBusinessMode.RealEstate);
            RealEstateModuleHost.ExecuteCommand("CDREDJ");
        }

        [CommandMethod("CDRESELECTPARCEL", CommandFlags.Modal)]
        public void SelectRealEstateParcel()
        {
            CDBoxBusinessModeService.SetMode(CDBoxBusinessMode.RealEstate);
            RealEstateModuleHost.ExecuteCommand("CDRESELECTPARCEL");
        }

        [CommandMethod("CDREFILLSEGMENT", CommandFlags.Modal)]
        public void FillRealEstateBoundarySegments()
        {
            CDBoxBusinessModeService.SetMode(CDBoxBusinessMode.RealEstate);
            RealEstateModuleHost.ExecuteCommand("CDREFILLSEGMENT");
        }

        [CommandMethod("CDREFILLNEIGHBOR", CommandFlags.Modal)]
        public void FillRealEstateNeighborInformation()
        {
            CDBoxBusinessModeService.SetMode(CDBoxBusinessMode.RealEstate);
            RealEstateModuleHost.ExecuteCommand("CDREFILLNEIGHBOR");
        }

        [CommandMethod("CDREMAPSHEETFINISH", CommandFlags.Modal)]
        public void FinishRealEstateMapSheetRecognition()
        {
            RealEstateModuleHost.ExecuteCommand("CDREMAPSHEETFINISH");
        }

        [CommandMethod("CDREBL", CommandFlags.Modal)]
        public void AnnotateRealEstateBuildingLength()
        {
            CDBoxBusinessModeService.SetMode(CDBoxBusinessMode.RealEstate);
            RealEstateModuleHost.ExecuteCommand("CDREBL");
        }

        [CommandMethod("JZWBC", CommandFlags.Modal)]
        public void AnnotateRealEstateBuildingLengthAlias()
        {
            CDBoxBusinessModeService.SetMode(CDBoxBusinessMode.RealEstate);
            RealEstateModuleHost.ExecuteCommand("CDREBL");
        }

        [CommandMethod("CDREBLSZ", CommandFlags.Modal)]
        public void OpenRealEstateBuildingLengthSettings()
        {
            CDBoxBusinessModeService.SetMode(CDBoxBusinessMode.RealEstate);
            RealEstateModuleHost.ExecuteCommand("CDREBLSZ");
        }

        [CommandMethod("JZWBCSZ", CommandFlags.Modal)]
        public void OpenRealEstateBuildingLengthSettingsAlias()
        {
            CDBoxBusinessModeService.SetMode(CDBoxBusinessMode.RealEstate);
            RealEstateModuleHost.ExecuteCommand("CDREBLSZ");
        }

        [CommandMethod("CDSET", CommandFlags.Modal)]
        public void OpenSettings()
        {
            CDBoxStudioSettingsWindow.ShowWindow(new AcadMainWindow());
        }

        [CommandMethod("CDBALL", CommandFlags.Modal)]
        public void ToggleFloatingCenter()
        {
            FloatingCenterController.ToggleVisibility();
        }

        [CommandMethod("CDBALLRESET", CommandFlags.Modal)]
        public void ResetFloatingCenterPosition()
        {
            FloatingCenterController.ResetPosition();
        }

        [CommandMethod("CDBALLDEMO", CommandFlags.Modal)]
        public void ShowFloatingCenterDemo()
        {
            FloatingCenterController.ShowDemo(FloatingHealthState.Warning,
                FloatingActivityState.Working, 3, 42.0);
        }

        [CommandMethod("CDBALLDEMOCLEAR", CommandFlags.Modal)]
        public void ClearFloatingCenterDemo()
        {
            FloatingCenterController.ClearDemo();
        }

        [CommandMethod("CDABOUT", CommandFlags.Modal)]
        public void OpenAboutChaozhongqing()
        {
            const string url = "https://cdbox-release-cdbox-d9gsv9fvj6a1aed69.webapps.tcloudbase.com/";
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(
                    new AcadMainWindow(),
                    "无法打开 CDBox 发布站：" + ex.Message,
                    "关于超重氢工具箱",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
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
                    CDBoxStudioLogger.Error("启动检查更新失败。", ex);
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
                            string message = "发现 CDBox 新版本 " + result.LatestVersion
                                + "（当前版本 " + result.CurrentVersion + "）。\r\n\r\n"
                                + "启动检查只进行提示，不会自动下载或安装。是否打开 CDBox 设置查看并手动更新？";
                            if (!string.IsNullOrWhiteSpace(result.Notes))
                                message += "\r\n\r\n更新说明：\r\n" + result.Notes.Trim();
                            DialogResult choice = TCPipeAutoDraw.UI.CDBoxMessageBox.Show(
                                new AcadMainWindow(), message, "CDBox 更新提示",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                                MessageBoxDefaultButton.Button2);
                            if (choice == DialogResult.Yes)
                                CDBoxStudioSettingsWindow.ShowWindow(new AcadMainWindow());
                        }
                        catch (System.Exception ex)
                        {
                            CDBoxStudioLogger.Error("显示启动更新提示失败。", ex);
                        }
                    }));
                }
                catch (System.Exception ex)
                {
                    CDBoxStudioLogger.Error("调度启动更新提示失败。", ex);
                }
            });
        }


        private bool ShouldSuppressStartupCdboxCommand()
        {
            try
            {
                if (!CDBoxInstallationLocator.IsRunningFromInstallFolder()) return false;

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
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), "未找到当前图纸。", "CDBox Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioHost.Show(CreateDefaultModules());
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), ex.Message, "CDBox Studio 打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private IList<ITCModule> CreateDefaultModules()
        {
            IList<ITCModule> modules = TCModuleRegistry.CreateDefaultModules(
                ShowLayerManager, RunFrameTemplateAdd, RunFrameCutLayout,
                ShowExcelToCad);
            if (RealEstateModuleHost.IsAvailable)
            {
                modules.Add(new TCModuleDescriptor(
                    "realestate",
                    "CDBox 不动产",
                    "打开独立的不动产业务工作区。",
                    "CDRE",
                    true,
                    delegate
                    {
                        CDBoxBusinessModeService.SetMode(
                            CDBoxBusinessMode.RealEstate);
                        RealEstateModuleHost.OpenWorkspace();
                    }));
            }
            return modules;
        }

        [CommandMethod("CDSHORTCODE", CommandFlags.Modal)]
        [CommandMethod("CDJMSB", CommandFlags.Modal)]
        public void RecognizeShortCode()
        {
            CommonModuleHost.ExecuteCommand("short-code-recognize");
        }

        [CommandMethod("CDSHORTCODESET", CommandFlags.Modal)]
        [CommandMethod("CDJMSZ", CommandFlags.Modal)]
        public void OpenShortCodeSettings()
        {
            CommonModuleHost.ExecuteCommand("short-code-settings");
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

        [CommandMethod("TCBMJ_CASS_FINISH", CommandFlags.Modal)]
        public void FinishSurfaceAreaAnnotationCassCommand()
        {
            WastewaterModuleHost.ExecuteCommand(
                "wastewater-surface-area-cass-finish");
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

        [CommandMethod("PLDM", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void GenerateSectionDrawingBatchByShortName()
        {
            RunSectionDrawingBatch();
        }

        [CommandMethod("ZDM", CommandFlags.Modal)]
        public void GenerateLongitudinalProfile()
        {
            WastewaterModuleHost.ExecuteCommand(
                "wastewater-longitudinal-profile");
        }

        [CommandMethod("ZDMSZ", CommandFlags.Modal)]
        public void OpenLongitudinalProfileSettings()
        {
            WastewaterModuleHost.ExecuteCommand(
                "wastewater-longitudinal-profile-settings");
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

        [CommandMethod("TCFRAMEADD", CommandFlags.Modal)]
        public void AddFrameTemplate()
        {
            CommonModuleHost.ExecuteCommand("frame-template-add");
        }

        [CommandMethod("TCFRAMECUT", CommandFlags.Modal)]
        public void PlaceFrameCutRegions()
        {
            CommonModuleHost.ExecuteCommand("frame-cut-regions");
        }

        [CommandMethod("TCFRAMELAYOUT", CommandFlags.Modal)]
        public void LayoutFrames()
        {
            CommonModuleHost.ExecuteCommand("frame-layout");
        }

        [CommandMethod("TCFRAMEPLACE", CommandFlags.Modal)]
        public void PlaceFramesDirectly()
        {
            CommonModuleHost.ExecuteCommand("frame-place");
        }

        [CommandMethod("TCFRAMESET", CommandFlags.Modal)]
        public void OpenFrameSettings()
        {
            CommonModuleHost.ExecuteCommand("frame-settings");
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
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "展点绘制管线", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Editor ed = doc.Editor;
            var opt = new PromptOpenFileOptions("\n选择管线测点代码 TXT/DAT/CSV 文件")
            {
                Filter = "文本/数据文件 (*.txt;*.dat;*.csv)|*.txt;*.dat;*.csv|所有文件 (*.*)|*.*"
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
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "展点绘制管线失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        [CommandMethod("ZDTXNET", CommandFlags.Modal)]
        public void DrawPipeFromPromptAlias()
        {
            DrawPipeFromPrompt();
        }

        private void ShowLayerManager()
        {
            BaseLayerManagerHost.OpenManager();
        }


        private void ShowAnnotationSettingsForm()
        {
            WastewaterModuleHost.ExecuteCommand(
                "wastewater-annotation-settings");
        }


        private void RunSurfaceAreaAnnotationDirect()
        {
            WastewaterModuleHost.ExecuteCommand(
                "wastewater-surface-area-annotation");
        }

        private void RunPipeLengthAnnotationDirect()
        {
            WastewaterModuleHost.ExecuteCommand(
                "wastewater-pipe-length-annotation");
        }



        private void RunNodeAnnotationDirect()
        {
            WastewaterModuleHost.ExecuteCommand(
                "wastewater-node-annotation");
        }



        private void ShowSectionDrawing()
        {
            WastewaterModuleHost.ExecuteCommand(
                "wastewater-section-drawing");
        }

        private void RunSectionDrawingBatch()
        {
            WastewaterModuleHost.ExecuteCommand(
                "wastewater-section-drawing-batch");
        }

        private void RunFrameTemplateAdd()
        {
            CommonModuleHost.ExecuteCommand("frame-template-add");
        }

        private void RunFrameCutLayout()
        {
            CommonModuleHost.ExecuteCommand("frame-cut-regions");
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
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "管线属性", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                        TCPipeAutoDraw.UI.CDBoxMessageBox.Show("所选对象无法识别为主管、支管或节点/检查井，未填入属性。\n请先在图层管理中设置父属性/标签，或选择正确对象。", "管线属性", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                using (var progress = CDBoxProgressSession.Start(doc,
                    "管线属性", "正在按默认表处理对象…",
                    "QuantityAttributes", "quantity-attributes-progress"))
                {
                    result = QuantityPipeAttributeService.ApplyDefaultProfilesToObjects(doc, ids, progress.Report);
                    progress.Complete(result.Message);
                }
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(result.Message, "管线属性", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "管线属性打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearQuantityAttributes()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "属性清除", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                ObjectId[] ids = QuantityPipeAttributeService.ReadImpliedOrPromptObjectIds(doc);
                if (ids == null || ids.Length == 0) return;

                DialogResult confirm = TCPipeAutoDraw.UI.CDBoxMessageBox.Show("将清除所选对象上的工程量属性记录，是否继续？", "属性清除", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (confirm != DialogResult.OK) return;

                QuantityPipeWriteResult result = QuantityPipeAttributeService.ClearAttributesFromObjects(doc, ids);
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(result.Message, "属性清除", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "属性清除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowQuantityDefaultProfileEditor()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "属性默认表", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioDefaultProfilesWindow.ShowWindow(new AcadMainWindow());
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "属性默认表打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowExcelToCad()
        {
            CommonModuleHost.ExecuteCommand("excel-to-cad");
        }

        private void RunQuantityCalculationReport()
        {
            WastewaterModuleHost.ExportFormalQuantityReport();
        }

        private void ShowQuantityDashboardWindow()
        {
            WastewaterModuleHost.OpenQuantityDashboard();
        }

    }
}
