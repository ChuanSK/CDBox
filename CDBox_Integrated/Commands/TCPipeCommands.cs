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
using TCPipeAutoDraw.Core.FloatingCenter;
using TCPipeAutoDraw.Core.Check;
using TCPipeAutoDraw.Core.Sync;
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
using TCPipeAutoDraw.UI.FloatingCenter;
using TCPipeAutoDraw.UI.Studio;
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
            FloatingCenterController.Initialize(FloatingCenter.Current);
            CadDrawingCheckCoordinator.Initialize();
            CadSyncCoordinator.Initialize();
            PipeLengthAnnotationInteractionService.Initialize();
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
            try { RealEstateModuleHost.Shutdown(); }
            catch (System.Exception ex)
            {
                CDBoxStudioLogger.Error(
                    "RealEstate 关闭边界发生未处理异常，已隔离。", ex);
            }
            PipeLengthAnnotationInteractionService.Terminate();
            CadSyncCoordinator.Terminate();
            CadDrawingCheckCoordinator.Terminate();
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
            check(File.Exists(Path.Combine(baseDirectory, "CDBox.RealEstate.dll")), "RealEstate 程序集", Path.Combine(baseDirectory, "CDBox.RealEstate.dll"));
            check(string.Equals(CDBoxStudioUpdateService.ReleaseIdentity, "CDBox-Studio-Preview-3.6.2", StringComparison.OrdinalIgnoreCase), "发布身份", CDBoxStudioUpdateService.ReleaseIdentity);
            check(CDBoxStudioUpdateService.CurrentVersionCode == 30602, "版本码", CDBoxStudioUpdateService.CurrentVersionCode.ToString());
            check(File.Exists(Path.Combine(baseDirectory, "Microsoft.Web.WebView2.Core.dll")), "WebView2 Core", Path.Combine(baseDirectory, "Microsoft.Web.WebView2.Core.dll"));
            check(File.Exists(Path.Combine(baseDirectory, "Microsoft.Web.WebView2.WinForms.dll")), "WebView2 WinForms", Path.Combine(baseDirectory, "Microsoft.Web.WebView2.WinForms.dll"));
            check(File.Exists(Path.Combine(baseDirectory, "runtimes", "win-x64", "native", "WebView2Loader.dll")), "WebView2 Loader", Path.Combine(baseDirectory, "runtimes", "win-x64", "native", "WebView2Loader.dll"));
            check(File.Exists(Path.Combine(baseDirectory, "Updater", "CDBoxUpdater.exe")), "独立更新器", Path.Combine(baseDirectory, "Updater", "CDBoxUpdater.exe"));
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

        [CommandMethod("CDRE", CommandFlags.Modal)]
        public void OpenRealEstate()
        {
            RealEstateModuleHost.OpenWorkspace();
        }

        [CommandMethod("CDBOXRE", CommandFlags.Modal)]
        public void OpenRealEstateAlias()
        {
            RealEstateModuleHost.OpenWorkspace();
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
            TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), "超重氢工具箱发布页入口将在后续版本接入。", "关于超重氢工具箱", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        [CommandMethod("CDINSTALL", CommandFlags.Modal)]
        public void InstallAutoLoad()
        {
            CDBoxInstallResult result = CDBoxInstaller.InstallToCadDirectory();
            CDBoxAppSettings settings = CDBoxAppSettingsStore.Load();
            if (result.Success) settings.InstalledPath = result.InstallRoot;
            CDBoxAppSettingsStore.Save(settings);

            TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), result.Message + "\r\n\r\n安装目录：" + result.InstallRoot, result.Success ? "CDBox 安装完成" : "CDBox 安装失败", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        [CommandMethod("CDUPDATE", CommandFlags.Modal)]
        public void UpdateAutoLoad()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择完整构建输出目录中的新版 CDBox.dll（将安装同目录全部依赖）";
                dialog.Filter = "CDBox.dll|CDBox.dll|DLL 文件 (*.dll)|*.dll|所有文件 (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;

                if (dialog.ShowDialog(new AcadMainWindow()) != DialogResult.OK) return;

                Document document = AcadApp.DocumentManager.MdiActiveDocument;
                CDBoxInstallResult result;
                using (var progress = CDBoxProgressSession.Start(document,
                    "本地更新准备", "正在检查并收集本地更新文件…",
                    "LocalUpdate", "local-update-progress", false))
                {
                    result = CDBoxInstaller.ScheduleUpdateFromDll(
                        dialog.FileName, progress.Report);
                    if (result.Success)
                        progress.Complete("本地更新已准备，请关闭 AutoCAD 完成安装。");
                    else
                        progress.Fail("本地更新准备失败。");
                }
                CDBoxAppSettings settings = CDBoxAppSettingsStore.Load();
                if (result.Success) settings.InstalledPath = result.InstallRoot;
                CDBoxAppSettingsStore.Save(settings);

                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), result.Message + "\r\n\r\n安装目录：" + result.InstallRoot, result.Success ? "CDBox 更新" : "CDBox 更新失败", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
        }

        [CommandMethod("CDUNINSTALL", CommandFlags.Modal)]
        public void UninstallAutoLoad()
        {
            DialogResult confirm = TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), "确定卸载 CDBox 自动加载并删除安装目录吗？\r\n\r\n当前已加载的插件本次 CAD 会话仍可继续使用，重启 CAD 后不再自动加载。", "卸载 CDBox", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            CDBoxMenuService.RemoveMenu(true);

            CDBoxInstallResult result = CDBoxInstaller.Uninstall();
            CDBoxAppSettings settings = CDBoxAppSettingsStore.Load();
            settings.InstalledPath = string.Empty;
            CDBoxAppSettingsStore.Save(settings);

            TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), result.Message + "\r\n\r\n安装目录：" + result.InstallRoot, result.Success ? "CDBox 卸载" : "CDBox 卸载提示", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
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
                        "CDBox 自动加载安装",
                        "检测到 CDBox 尚未安装到 CAD 所在目录。\r\n\r\n是否现在安装？安装后以后启动 CAD 会自动加载 CDBox，不需要再手动 NETLOAD。\r\n\r\n安装目录：" + CDBoxInstaller.GetInstallRoot(),
                        "安装",
                        "暂不安装",
                        out doNotAsk);

                    if (installResult == DialogResult.Yes)
                    {
                        CDBoxInstallResult result = CDBoxInstaller.InstallToCadDirectory();
                        if (result.Success)
                        {
                            settings.InstalledPath = result.InstallRoot;
                            settingsChanged = true;
                        }

                        TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), result.Message + "\r\n\r\n安装目录：" + result.InstallRoot, result.Success ? "CDBox 安装完成" : "CDBox 安装失败", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
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
                if (doc != null) doc.Editor.WriteHudMessage("\n[启动设置] " + ex.Message);
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
            IList<ITCModule> modules = TCModuleRegistry.CreateDefaultModules(DrawPipeFromPrompt, ShowLayerManager, ShowAnnotationSettingsForm, ShowSurfaceAreaAnnotationForm, ShowPipeLengthAnnotationForm, ShowNodeAnnotationSettings, ShowSectionDrawing, RunFrameTemplateAdd, RunFrameCutLayout, ShowQuantityPipeAttributeEditor, RunQuantityCalculationReport, ShowExcelToCad);
            if (RealEstateModuleHost.IsAvailable)
            {
                modules.Add(new TCModuleDescriptor(
                    "realestate",
                    "CDBox 不动产",
                    "打开独立的不动产业务工作区。",
                    "CDRE",
                    true,
                    RealEstateModuleHost.OpenWorkspace));
            }
            return modules;
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
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "图层管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioLayerManagerWindow.ShowWindow(new AcadMainWindow());
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "图层管理打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void ShowAnnotationSettingsForm()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "标注设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioAnnotationSettingsWindow.ShowWindow(new AcadMainWindow(), "surface");
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "标注设置打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void ShowSurfaceAreaAnnotationForm()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "表面积标注", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioAnnotationSettingsWindow.ShowWindow(new AcadMainWindow(), "surface");
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "表面积标注打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void ShowPipeLengthAnnotationForm()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "管线长度标注", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioAnnotationSettingsWindow.ShowWindow(new AcadMainWindow(), "pipeLength");
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "管线长度标注打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        private void RunSurfaceAreaAnnotationDirect()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "表面积标注", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "表面积标注失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunPipeLengthAnnotationDirect()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "管线长度标注", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "管线长度标注失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        private void RunNodeAnnotationDirect()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "节点标注", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "节点标注失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        private void ShowSectionDrawing()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "断面图生成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioSectionDrawingWindow.ShowWindow(new AcadMainWindow());
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "断面图生成打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "批量断面图", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                SectionBatchDrawingResult result;
                using (var progress = CDBoxProgressSession.Start(doc,
                    "批量断面图生成", "正在读取断面数据…",
                    "SectionBatch", "section-batch-progress"))
                {
                    result = SectionBatchDrawingService.Run(doc, progress.Report);
                    progress.Complete(result.Success
                        ? "批量断面图生成完成。" : "批量断面图处理结束。");
                }
                if (result.Success)
                {
                    TCPipeAutoDraw.UI.CDBoxMessageBox.Show("批量断面图生成完成：生成 " + result.SuccessSectionCount + " 张断面图。", "批量断面图", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    TCPipeAutoDraw.UI.CDBoxMessageBox.Show(result.Message, "批量断面图", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "批量断面图失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunFrameTemplateAdd()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "添加图框模板", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                new FrameLayoutCommands().AddFrameTemplate();
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "添加图框模板失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunFrameCutLayout()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "布置裁图区域", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                new FrameLayoutCommands().CutAndLayoutFrames();
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "布置裁图区域失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            ExcelToCadCommandService.Run(AcadApp.DocumentManager.MdiActiveDocument,
                new AcadMainWindow());
        }

        private void RunQuantityCalculationReport()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show("未找到当前图纸。", "工程量表格生成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                ObjectId[] scopeIds = PromptQuantityReportScope(doc);
                if (scopeIds == null || scopeIds.Length == 0) return;

                string defaultName = BuildQuantityReportDefaultFileName(doc);
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Title = "保存工程量计算表";
                    dialog.Filter = "Excel 97-2003 工作簿 (*.xls)|*.xls|所有文件 (*.*)|*.*";
                    dialog.FileName = defaultName;
                    dialog.AddExtension = true;
                    dialog.DefaultExt = "xls";
                    TrySetSaveDialogInitialDirectory(dialog, doc);
                    if (dialog.ShowDialog(new AcadMainWindow()) != DialogResult.OK) return;

                    QuantityCalculationReport report;
                    using (var progress = CDBoxProgressSession.Start(doc,
                        "工程量表格生成", "正在计算工程量…",
                        "QuantityReport", "quantity-report-progress"))
                    {
                        report = QuantityCalculationReportService.BuildReport(doc, scopeIds, progress.Report);
                        progress.ReportMarquee("正在写入工程量表格...");
                        QuantityExcelXmlExporter.Export(dialog.FileName, report);
                        progress.Complete("工程量表格生成完成。");
                    }

                    string message = "工程量表格已生成：主管 " + report.MainPipes.Count + " 条，节点/检查井 " + report.Wells.Count + " 个。";
                    if (report.Warnings.Count > 0) message += " 提示 " + report.Warnings.Count + " 条，请查看命令行信息。";
                    TCPipeAutoDraw.UI.CDBoxMessageBox.Show(
                        message + "\r\n\r\n" + dialog.FileName,
                        "工程量表格生成", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(ex.Message, "工程量表格生成失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowQuantityDashboardWindow()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), "未找到当前图纸。", "工程量动态看板", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CDBoxStudioQuantityDashboardWindow.ShowWindow(new AcadMainWindow());
            }
            catch (System.Exception ex)
            {
                TCPipeAutoDraw.UI.CDBoxMessageBox.Show(new AcadMainWindow(), ex.Message, "工程量动态看板打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static ObjectId[] PromptQuantityReportScope(Document doc)
        {
            if (doc == null) return new ObjectId[0];
            Editor ed = doc.Editor;

            PromptSelectionOptions opt = new PromptSelectionOptions();
            opt.MessageForAdding = "\n选择工程量统计范围内的主管和井对象（可框选/窗选）：";
            opt.MessageForRemoval = "\n移除对象：";

            PromptSelectionResult res = ed.GetHudSelection(opt);
            if (res.Status != PromptStatus.OK || res.Value == null || res.Value.Count == 0)
            {
                ed.WriteHudMessage("\n[GCL] 未选择统计范围，已取消。");
                return new ObjectId[0];
            }

            return res.Value.GetObjectIds();
        }

        private static string BuildQuantityReportDefaultFileName(Document doc)
        {
            string title = GetDrawingTitle(doc);
            if (string.IsNullOrWhiteSpace(title)) title = "当前图纸";
            return SanitizeFileName(title + "工程量计算表") + ".xls";
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
            if (string.IsNullOrWhiteSpace(fileName)) return "工程量计算表";
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++) fileName = fileName.Replace(invalid[i], '_');
            return fileName.Trim();
        }

    }
}
