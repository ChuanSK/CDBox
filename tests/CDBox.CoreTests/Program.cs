using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CDBoxUpdater;
using TCPipeAutoDraw.Core.Startup;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.UI.Studio;

namespace CDBox.CoreTests
{
    internal static class Program
    {
        private static readonly List<string> Failures = new List<string>();
        private static int _passed;

        private static int Main()
        {
            Run("对象类型识别", TestKindRecognition);
            Run("有效长度与默认表克隆", TestEffectiveLengthAndDefaultClone);
            Run("结构层解析", TestStructureLayers);
            Run("沉泥井管沟深度", TestSiltWellDepth);
            Run("常用文本解析", TestPrimitiveParsing);
            Run("Studio 路由消息", TestStudioRouteRequest);
            Run("Studio 收藏与最近使用", TestStudioState);
            Run("工程量看板共享页面", TestQuantityDashboardSharedPage);
            Run("属性编辑器共享页面", TestQuantityAttributeEditorSharedPage);
            Run("旧版更新源完整性校验", TestLegacyUpdateSourceValidation);
            Run("更新包路径越界防护", TestUpdaterRejectsZipTraversal);
            Run("更新器替换与备份", TestUpdaterReplacesAndBacksUpBundle);

            Console.WriteLine();
            Console.WriteLine("CDBox.CoreTests: {0} passed, {1} failed", _passed, Failures.Count);
            foreach (string failure in Failures) Console.Error.WriteLine("FAIL: " + failure);
            return Failures.Count == 0 ? 0 : 1;
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine("PASS: " + name);
            }
            catch (Exception ex)
            {
                Failures.Add(name + " - " + ex.Message);
            }
        }

        private static void TestKindRecognition()
        {
            True(QuantityPipeAttributes.IsNodeKind("沉泥井"), "沉泥井应识别为井类");
            True(QuantityPipeAttributes.IsBranchKind("雨水支管"), "雨水支管应识别为支管");
            True(QuantityPipeAttributes.IsMainPipeKind("污水主管"), "主管应识别为主管");
            Equal(QuantityPipeAttributes.KindNodeWell, QuantityPipeAttributes.DefaultForKind("检查井").ObjectKind, "井类默认表");
            Equal(QuantityPipeAttributes.KindBranchPipe, QuantityPipeAttributes.DefaultForKind("支管").ObjectKind, "支管默认表");
        }

        private static void TestEffectiveLengthAndDefaultClone()
        {
            QuantityPipeAttributes attrs = QuantityPipeAttributes.DefaultMainPipe;
            attrs.UseManualLength = true;
            attrs.ManualLength = 12.5;
            attrs.StartNode = "W1";
            attrs.EndNode = "W2";
            attrs.NodeNo = "W1";
            attrs.Remark = "临时备注";

            Near(12.5, attrs.EffectiveLength(9.0), 1e-9, "手动长度应优先");
            QuantityPipeAttributes profile = attrs.CloneForDefaultProfile();
            Equal(string.Empty, profile.StartNode, "默认表不应保存起点井");
            Equal(string.Empty, profile.EndNode, "默认表不应保存终点井");
            Equal(string.Empty, profile.NodeNo, "默认表不应保存节点编号");
            False(profile.UseManualLength, "默认表不应启用手动长度");
            Near(0.0, profile.ManualLength, 1e-9, "默认表不应保存手动长度");

            profile.Material = "测试材料";
            False(string.Equals(attrs.Material, profile.Material, StringComparison.Ordinal), "克隆后对象应相互独立");
        }

        private static void TestStructureLayers()
        {
            var attrs = new QuantityPipeAttributes
            {
                BackfillStructure = "C25砼恢复 0.25 锁定；碎石垫层 0.10 锁定；中粗砂回填 0.80 管线层；中粗砂垫层 0.15 锁定"
            };

            QuantityPipeAttributes.ApplyStructureLayerText(attrs);
            Near(0.25, attrs.C25RestoreThickness, 1e-9, "C25 厚度");
            Near(0.10, attrs.GravelCushionThickness, 1e-9, "碎石厚度");
            Near(0.15, attrs.SandCushionThickness, 1e-9, "砂垫层厚度");

            List<QuantityStructureLayer> layers = QuantityStructureLayer.Parse(attrs.BackfillStructure);
            Equal(4, layers.Count, "结构层数量");
            True(layers[2].IsPipeLayer, "管线层标记");
            True(QuantityStructureLayer.IsSandBackfill(layers[2]), "中粗砂回填分类");
            True(QuantityStructureLayer.IsSandCushion(layers[3]), "中粗砂垫层分类");
        }

        private static void TestSiltWellDepth()
        {
            QuantityPipeAttributes well = QuantityPipeAttributes.DefaultNodeWell;
            well.WellType = "沉泥井";
            well.WellSpec = "φ700";
            well.WellDepth = 2.0;
            well.SiltWellDeductDepth700 = 0.50;

            Near(0.50, QuantityPipeAttributes.GetSiltWellDeductDepth(well), 1e-9, "φ700 沉泥井扣减");
            Near(1.65, QuantityPipeAttributes.CalculatePipeExcavationDepthByWell(well, 0.15, 9.0), 1e-9, "管沟深度");
        }

        private static void TestPrimitiveParsing()
        {
            True(QuantityPipeAttributes.ParseBool("是", false), "中文真值");
            False(QuantityPipeAttributes.ParseBool("0", true), "数字假值");
            Near(1.25, QuantityPipeAttributes.ParseDouble("1.25", 0.0), 1e-9, "小数解析");
            Near(7.0, QuantityPipeAttributes.ParseDouble("invalid", 7.0), 1e-9, "非法小数回退");
        }

        private static void TestStudioRouteRequest()
        {
            CDBoxStudioRouteRequest current = CDBoxStudioRouteRequest.Parse("studio|run|module%3Alayer-manager");
            Equal("run", current.Name, "当前消息名称");
            Equal("module:layer-manager", current.Argument, "当前消息参数解码");

            CDBoxStudioRouteRequest legacy = CDBoxStudioRouteRequest.Parse("filter:工程量");
            Equal("filter", legacy.Name, "旧消息名称");
            Equal("工程量", legacy.Argument, "旧消息参数");
        }

        private static void TestStudioState()
        {
            var state = new CDBoxStudioState();
            True(state.ToggleFavorite("module:layer-manager"), "首次收藏应返回 true");
            True(state.IsFavorite("MODULE:LAYER-MANAGER"), "收藏 Id 应忽略大小写");
            False(state.ToggleFavorite("module:layer-manager"), "再次切换应取消收藏");

            for (int i = 0; i < 30; i++) state.MarkRecent("action:" + i);
            Equal(24, state.RecentItems.Count, "最近使用最多保留 24 项");
            state.MarkRecent("action:29");
            Equal(2, state.GetRecent("action:29").UseCount, "重复使用应累计次数");

            state.ToggleFavorite("action:29");
            state.RemoveMissingActions(new[] { "action:28" });
            False(state.IsFavorite("action:29"), "不存在的收藏应被清理");
            True(state.HasRecent("action:28"), "仍存在的最近项应保留");
        }

        private static void TestQuantityDashboardSharedPage()
        {
            string embedded = CDBoxStudioQuantityDashboardPage.BuildEmbeddedSection();
            string standalone = CDBoxStudioQuantityDashboardPage.BuildStandaloneDocument("fresh", false, "test.log");

            True(embedded.IndexOf("quantityDashboardPage", StringComparison.Ordinal) >= 0, "内嵌页应提供共享组件根节点");
            True(standalone.IndexOf("CDBoxQuantityDashboardPage.create", StringComparison.Ordinal) >= 0, "独立页应创建同一个共享页面组件");
            True(standalone.IndexOf("standalone:true", StringComparison.Ordinal) >= 0, "独立页应启用独立宿主模式");
            True(standalone.IndexOf("data-theme=\"fresh\"", StringComparison.Ordinal) >= 0, "独立页应继承 Studio 主题");
            True(standalone.IndexOf("class=\"no-animations\"", StringComparison.Ordinal) >= 0, "独立页应继承动画设置");
        }

        private static void TestQuantityAttributeEditorSharedPage()
        {
            string embedded = CDBoxStudioQuantityAttributeEditorPage.BuildEmbeddedSection();
            string standalone = CDBoxStudioQuantityAttributeEditorPage.BuildStandaloneDocument(
                new CDBoxStudioSettings { Theme = "dark", AnimationsEnabled = false }, "test.log", "drawing.dwg", "A1");

            True(embedded.IndexOf("quantityAttributeEditorPage", StringComparison.Ordinal) >= 0, "内嵌属性编辑器应提供共享根节点");
            True(standalone.IndexOf("CDBoxQuantityAttributeEditorPage.create", StringComparison.Ordinal) >= 0, "独立窗口应创建同一共享组件");
            True(standalone.IndexOf("standalone:true", StringComparison.Ordinal) >= 0, "独立属性编辑器应启用独立模式");
            True(standalone.IndexOf("Preview 9", StringComparison.Ordinal) >= 0, "页面应显示 Preview 9 身份");
            True(standalone.IndexOf("data-theme=\"dark\"", StringComparison.Ordinal) >= 0, "独立属性编辑器应继承主题");
        }

        private static void TestLegacyUpdateSourceValidation()
        {
            string root = NewTemporaryDirectory("update-source");
            try
            {
                string dll = Path.Combine(root, "CDBox.dll");
                File.WriteAllText(dll, "main");
                CDBoxUpdateSourceValidationResult invalid = CDBoxUpdateSourceValidator.Validate(dll);
                False(invalid.Valid, "只有 CDBox.dll 的目录必须拒绝更新");
                True(invalid.Message.IndexOf("Microsoft.Web.WebView2.WinForms.dll", StringComparison.Ordinal) >= 0, "错误应指出缺失的 WebView2 依赖");

                File.WriteAllText(Path.Combine(root, "Microsoft.Web.WebView2.Core.dll"), "core");
                File.WriteAllText(Path.Combine(root, "Microsoft.Web.WebView2.WinForms.dll"), "forms");
                Directory.CreateDirectory(Path.Combine(root, "Updater"));
                File.WriteAllText(Path.Combine(root, "Updater", "CDBoxUpdater.exe"), "updater");
                Directory.CreateDirectory(Path.Combine(root, "runtimes", "win-x64", "native"));
                File.WriteAllText(Path.Combine(root, "runtimes", "win-x64", "native", "WebView2Loader.dll"), "loader");

                CDBoxUpdateSourceValidationResult valid = CDBoxUpdateSourceValidator.Validate(dll);
                True(valid.Valid, "完整构建输出应允许更新");
            }
            finally { DeleteDirectory(root); }
        }

        private static void TestUpdaterRejectsZipTraversal()
        {
            string root = NewTemporaryDirectory("zip-traversal");
            try
            {
                string packagePath = Path.Combine(root, "malicious.zip");
                using (FileStream stream = File.Create(packagePath))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    WriteEntry(archive, "../escaped.txt", "should-not-exist");
                }

                var pending = NewPending(packagePath, Path.Combine(root, "CDBox.bundle"), Path.Combine(root, "work"));
                Exception failure = Capture(delegate { BundleInstaller.Install(pending); });
                True(failure is InvalidOperationException, "越界 ZIP 应被拒绝");
                True(failure.Message.IndexOf("越界路径", StringComparison.Ordinal) >= 0, "错误应说明路径越界");
                False(File.Exists(Path.Combine(root, "escaped.txt")), "不得在解压目录外创建文件");
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        private static void TestUpdaterReplacesAndBacksUpBundle()
        {
            string root = NewTemporaryDirectory("bundle-replace");
            try
            {
                string target = Path.Combine(root, "CDBox.bundle");
                Directory.CreateDirectory(Path.Combine(target, "Contents"));
                File.WriteAllText(Path.Combine(target, "Contents", "old-version.txt"), "old", Encoding.UTF8);

                string packagePath = Path.Combine(root, "release.zip");
                using (FileStream stream = File.Create(packagePath))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    WriteEntry(archive, "CDBox.bundle/PackageContents.xml", "<ApplicationPackage />");
                    WriteEntry(archive, "CDBox.bundle/Contents/CDBox.dll", "new-version");
                    WriteEntry(archive, "CDBox.bundle/Contents/Microsoft.Web.WebView2.Core.dll", "core");
                    WriteEntry(archive, "CDBox.bundle/Contents/Microsoft.Web.WebView2.WinForms.dll", "forms");
                    WriteEntry(archive, "CDBox.bundle/Contents/runtimes/win-x64/native/WebView2Loader.dll", "loader");
                    WriteEntry(archive, "CDBox.bundle/Contents/Updater/CDBoxUpdater.exe", "updater");
                }

                BundleInstallOutcome outcome = BundleInstaller.Install(NewPending(packagePath, target, Path.Combine(root, "work")));
                True(outcome.Success, "有效 bundle 应替换成功");
                True(Directory.Exists(outcome.BackupBundlePath), "旧 bundle 应保留备份");
                True(File.Exists(Path.Combine(outcome.BackupBundlePath, "Contents", "old-version.txt")), "备份应包含旧文件");
                Equal("new-version", File.ReadAllText(Path.Combine(target, "Contents", "CDBox.dll"), Encoding.UTF8), "目标应包含新版文件");
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        private static PendingUpdateManifest NewPending(string packagePath, string target, string work)
        {
            return new PendingUpdateManifest
            {
                SchemaVersion = 1,
                PackagePath = packagePath,
                PackageSha256 = ComputeSha256(packagePath),
                TargetBundlePath = target,
                WorkDirectory = work
            };
        }

        private static string NewTemporaryDirectory(string name)
        {
            string path = Path.Combine(Path.GetTempPath(), "CDBox.CoreTests", name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void WriteEntry(ZipArchive archive, string path, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
            using (StreamWriter writer = new StreamWriter(entry.Open(), new UTF8Encoding(false))) writer.Write(content);
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) text.Append(value.ToString("X2"));
                return text.ToString();
            }
        }

        private static Exception Capture(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static void DeleteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            Directory.Delete(path, true);
        }

        private static void True(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void False(bool value, string message)
        {
            if (value) throw new InvalidOperationException(message);
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + ": expected=" + expected + ", actual=" + actual);
            }
        }

        private static void Near(double expected, double actual, double tolerance, string message)
        {
            if (Math.Abs(expected - actual) > tolerance)
            {
                throw new InvalidOperationException(message + ": expected=" + expected + ", actual=" + actual);
            }
        }
    }
}
