using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CDBox.Shared;
using CDBoxUpdater;
using TCPipeAutoDraw.Core.Startup;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;
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
            Run("工程量管线分类", TestQuantityPipeClassification);
            Run("沉泥井管沟深度", TestSiltWellDepth);
            Run("工程量依赖联动", TestQuantityDependencyRules);
            Run("常用文本解析", TestPrimitiveParsing);
            Run("Studio 路由消息", TestStudioRouteRequest);
            Run("阶段 A 新安装启动职责", TestStageANewInstallDefaults);
            Run("数值输入步长统一", TestNumericInputSteps);
            Run("工程量看板共享页面", TestQuantityDashboardSharedPage);
            Run("属性编辑器共享页面", TestQuantityAttributeEditorSharedPage);
            Run("图层管理器自定义父级", TestLayerManagerCustomParents);
            Run("断面图 Preview 10 共享页面", TestSectionDrawingSharedPage);
            Run("属性默认表统一表格交互", TestQuantityDefaultsTableInteraction);
            Run("标注浮窗文字组合与旧数据迁移", TestAnnotationHudTextComposition);
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

        private static void TestAnnotationHudTextComposition()
        {
            Equal("给水长度：12.50m", PipeLengthAnnotationTextComposer.Compose("给水长度：", "12.50m"),
                "绑定标注应组合用户文字与系统长度");

            string user;
            string system;
            PipeLengthAnnotationTextComposer.SplitLegacyText("给水长度：12.50m", 12.5, out user, out system);
            Equal("给水长度：", user, "旧标注应拆出用户文字");
            Equal("12.50m", system, "旧标注应拆出系统长度");
            Equal("9.20m", PipeLengthAnnotationTextComposer.FormatLike(9.2, system),
                "更新长度应保留原小数位与单位");
            Equal("9.20", PipeLengthAnnotationTextComposer.FormatLike(9.2, "12.50"),
                "无单位的旧标注不应被强行追加单位");

            string frozen = "给水长度：12.50m（复核）12.50m";
            Equal("给水长度：12.50m（复核）",
                PipeLengthAnnotationTextComposer.RemoveDetachedLengthToken(frozen, "12.50m"),
                "重新绑定只应移除最后一个已记录长度片段");

            PipeLengthAnnotationTextComposer.SplitLegacyText("人工说明", 12.5, out user, out system);
            Equal("人工说明", user, "没有长度片段时必须保留原文字");
            Equal(string.Empty, system, "没有长度片段时不得虚构系统长度");

            PipeLengthAnnotationTextComposer.SplitLegacyText("DN110 给水长度：110.00m", 110.0,
                out user, out system);
            Equal("DN110 给水长度：", user, "管径与长度数值相同时应优先拆分末尾长度");
            Equal("110.00m", system, "末尾长度片段应保持完整");
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
            Equal("C25砼恢复", QuantityStructureLayer.Parse("C25砼恢复 0.15 锁定")[0].Name, "结构层名称中的材料强度数字应保留");
            Equal("C25砼恢复", QuantityStructureLayer.Parse("C 砼恢复 0.15 锁定")[0].Name, "旧版损坏的 C25 层名应自动修复");

            QuantityStructureLayer sandEncasement = QuantityStructureLayer.Parse("中粗砂包管 0.60 管线层")[0];
            True(QuantityStructureLayer.IsSandBackfill(sandEncasement), "中粗砂包管应归入中粗砂回填");
            False(QuantityStructureLayer.IsConcretePipeEncasement(sandEncasement), "中粗砂包管不得归入 C25 砼包管");
            QuantityStructureLayer concreteEncasement = QuantityStructureLayer.Parse("C25砼包管 0.30 管线层")[0];
            True(QuantityStructureLayer.IsConcretePipeEncasement(concreteEncasement), "只有混凝土包管层才归入 C25 砼包管");
            False(QuantityStructureLayer.IsC25Restore(concreteEncasement), "C25 砼包管不得重复归入 C25 恢复");

            var noConcrete = new QuantityPipeAttributes
            {
                C25RestoreThickness = 0.25,
                BackfillStructure = "中粗砂回填 0.80 管线层\n中粗砂垫层 0.15 锁定"
            };
            QuantityPipeAttributes.ApplyStructureLayerText(noConcrete);
            Near(0.0, noConcrete.C25RestoreThickness, 1e-9, "显式结构层没有 C25 时应清除旧缓存厚度");

            List<QuantityStructureLayer> withoutBackfill = QuantityStructureLayer.Parse("C25砼包管 0.30 管线层\n中粗砂垫层 0.10 锁定");
            Near(0.60, QuantityEngineeringMath.CalculatePipeRemainingBackfillHeight(1.00, withoutBackfill), 1e-9, "推算剩余回填高度时必须扣除混凝土包管层");
            Near(1.10, QuantityEngineeringMath.CalculateEarthworkOut(0.10, 1.00, 0.0), 1e-9, "新增中粗砂不得抵扣土方外运");
            Near(0.70, QuantityEngineeringMath.CalculateEarthworkOut(0.10, 1.00, 0.40), 1e-9, "只有可回用原土才能抵扣土方外运");
        }

        private static void TestQuantityPipeClassification()
        {
            Equal("DN110PVC管", QuantityDashboardClassification.BuildPipeType("110", "PVC"), "数字管径与管材应形成统一类型");
            Equal("DN110PVC管", QuantityDashboardClassification.BuildPipeType("DN110", "PVC管"), "已有前后缀不应重复");
            Equal("DN110PE管", QuantityDashboardClassification.BuildPipeType("DN110", "PE"), "不同管材应形成不同类型");
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
            Near(1.50, QuantityPipeAttributes.CalculatePipeExcavationDepthByWell(well, 9.0), 1e-9, "未传主管垫层时不得误用井下垫层");
        }

        private static void TestQuantityDependencyRules()
        {
            QuantityPipeAttributes pipe = QuantityPipeAttributes.DefaultMainPipe;
            pipe.StartNode = "W1";
            pipe.EndNode = "W2";
            QuantityPipeAttributes startWell = QuantityPipeAttributes.DefaultNodeWell;
            startWell.NodeNo = "W1";
            startWell.WellDepth = 1.00;
            startWell.WellType = "检查井";
            QuantityPipeAttributes endWell = QuantityPipeAttributes.DefaultNodeWell;
            endWell.NodeNo = "W2";
            endWell.WellDepth = 1.20;
            endWell.WellType = "沉泥井";
            endWell.WellSpec = "φ500";

            QuantityDependencyResult main = QuantityDependencyService.NormalizeDraft(
                pipe,
                QuantityStructureLayer.Parse(pipe.BackfillStructure),
                pipe,
                startWell,
                endWell,
                null,
                "Load");
            Near(1.15, main.Attributes.StartDepth, 1e-9, "检查井端点应使用井深加当前主管垫层");
            Near(1.15, main.Attributes.EndDepth, 1e-9, "沉泥井端点应扣除沉泥深度");
            Near(1.15, main.Attributes.AverageDepth, 1e-9, "平均深度不得再次叠加垫层");

            QuantityPipeAttributes well = QuantityPipeAttributes.DefaultNodeWell;
            well.WellDepth = 0.46;
            well.BackfillStructure = "承压盖板C25基础 0.30 锁定\n承压盖板碎石垫层 0.10 锁定\n中粗砂回填 0.80\n中粗砂垫层 0.10 锁定 井下层";
            QuantityDependencyResult node = QuantityDependencyService.NormalizeDraft(
                well,
                QuantityStructureLayer.Parse(well.BackfillStructure),
                well,
                null,
                null,
                null,
                "WellDepth");
            QuantityStructureLayer backfill = node.Layers.Find(x => QuantityStructureLayer.IsSandBackfill(x));
            Near(0.06, backfill.Height, 1e-9, "井下层不得参与井深范围内结构层扣减");
            Near(0.56, node.RealExcavationDepth, 1e-9, "井真实开挖深度应包含井下层");

            well.WellDepth = 0.20;
            QuantityDependencyResult negative = QuantityDependencyService.NormalizeDraft(
                well,
                QuantityStructureLayer.Parse(well.BackfillStructure),
                well,
                null,
                null,
                null,
                "WellDepth");
            True(negative.Layers.Find(x => QuantityStructureLayer.IsSandBackfill(x)).Height < 0, "锁定层超过井深时应保留负数");
            True(negative.Warnings.Count > 0, "负结构层应返回校验提示");

            QuantityPipeAttributes manualPipe = QuantityPipeAttributes.DefaultMainPipe;
            manualPipe.StartDepth = 1.20;
            manualPipe.EndDepth = 1.40;
            manualPipe.BackfillStructure = "中粗砂回填 0.90\n中粗砂垫层 0.10 锁定 管线层";
            QuantityDependencyResult manualDepth = QuantityDependencyService.NormalizeDraft(
                manualPipe,
                QuantityStructureLayer.Parse(manualPipe.BackfillStructure),
                manualPipe,
                null,
                null,
                null,
                "StartDepth");
            Near(1.30, manualDepth.Attributes.AverageDepth, 1e-9, "手工修改端点后平均深度应直接取两端平均");
            Near(1.20, manualDepth.Layers.Find(x => QuantityStructureLayer.IsSandBackfill(x)).Height, 1e-9, "端点变化应实时重算非锁定层");

            QuantityPipeAttributes oldPipe = QuantityPipeAttributes.DefaultMainPipe;
            oldPipe.StartDepth = 1.10;
            oldPipe.EndDepth = 1.10;
            oldPipe.BackfillStructure = "中粗砂回填 1.00\n中粗砂垫层 0.10 锁定 管线层";
            List<QuantityStructureLayer> changedLayers = QuantityStructureLayer.Parse("中粗砂回填 1.00\n中粗砂垫层 0.20 锁定 管线层");
            QuantityDependencyResult cushionChanged = QuantityDependencyService.NormalizeDraft(
                oldPipe,
                changedLayers,
                oldPipe,
                null,
                null,
                null,
                "StructureLayers");
            Near(1.10, cushionChanged.Attributes.StartDepth, 1e-9, "修改结构层不得反向改变主管起点深度");
            Near(1.10, cushionChanged.Attributes.EndDepth, 1e-9, "修改结构层不得反向改变主管终点深度");
            Near(0.90, cushionChanged.Layers.Find(x => QuantityStructureLayer.IsSandBackfill(x)).Height, 1e-9, "垫层变化后应按既有平均深度重算未锁定层");

            QuantityPipeAttributes special = QuantityPipeAttributes.DefaultMainPipe;
            special.IsSpecialObject = true;
            special.StartDepth = 9.0;
            special.EndDepth = 8.0;
            special.AverageDepth = 0.25;
            special.PipeOuterDiameter = 0.30;
            special.BackfillStructure = "中粗砂回填 0.25 管线层";
            QuantityDependencyResult specialResult = QuantityDependencyService.NormalizeDraft(
                special,
                QuantityStructureLayer.Parse(special.BackfillStructure),
                special,
                startWell,
                endWell,
                null,
                "AverageDepth");
            Near(9.0, specialResult.Attributes.StartDepth, 1e-9, "特殊对象不得自动识别并覆盖起点深度");
            Near(8.0, specialResult.Attributes.EndDepth, 1e-9, "特殊对象不得自动识别并覆盖终点深度");
            Near(0.25, specialResult.Attributes.AverageDepth, 1e-9, "特殊对象平均深度应完全由用户定义");
            Near(0.25, specialResult.Layers[0].Height, 1e-9, "特殊对象结构层应从手工平均深度派生");
            True(specialResult.Warnings.Exists(x => x.IndexOf("小于管道外径", StringComparison.Ordinal) >= 0), "管线层厚度小于管道外径时应提示");

            QuantityPipeAttributes specialBranch = QuantityPipeAttributes.DefaultBranchPipe;
            specialBranch.IsSpecialObject = true;
            specialBranch.BranchType = "明管";
            specialBranch.BranchIncludeInCalculation = true;
            QuantityDependencyResult specialBranchResult = QuantityDependencyService.NormalizeDraft(
                specialBranch,
                QuantityStructureLayer.Parse("自定义结构层 0.18"),
                specialBranch,
                null,
                null,
                QuantityPipeAttributes.DefaultBranchPipe,
                "BranchType");
            True(specialBranchResult.Attributes.BranchIncludeInCalculation, "特殊对象不得套用支管类型自动规则");
            Equal(1, specialBranchResult.Layers.Count, "特殊对象不得自动清空用户结构层");

            QuantityPipeAttributes exposed = QuantityPipeAttributes.DefaultBranchPipe;
            exposed.BranchType = "明管";
            QuantityDependencyResult emptyBranch = QuantityDependencyService.NormalizeDraft(
                exposed,
                new List<QuantityStructureLayer>(),
                exposed,
                null,
                null,
                null,
                "BranchType");
            Equal(0, emptyBranch.Layers.Count, "明管应允许并保持空结构层");
            False(emptyBranch.Attributes.BranchIncludeInCalculation, "明管不得加入工程量计算");

            QuantityPipeAttributes soilBranch = QuantityPipeAttributes.DefaultBranchPipe;
            soilBranch.BranchType = "原土回填";
            soilBranch.BranchDepth = 0.85;
            QuantityDependencyResult soil = QuantityDependencyService.NormalizeDraft(
                soilBranch,
                new List<QuantityStructureLayer>(),
                soilBranch,
                null,
                null,
                null,
                "BranchType");
            Equal(1, soil.Layers.Count, "原土支管应建立唯一结构层");
            Near(0.85, soil.Layers[0].Height, 1e-9, "原土支管层高应等于支管深度");
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

        private static void TestStageANewInstallDefaults()
        {
            CDBoxAppSettings settings = CDBoxAppSettings.Default;
            True(settings.PromptInstallOnLoad, "安装位置提示仍应保留");
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
            False(standalone.IndexOf("当前工程量快速估算台", StringComparison.Ordinal) >= 0, "看板不应显示冗余副标题");
            False(standalone.IndexOf("当前结果为基于图纸现有属性", StringComparison.Ordinal) >= 0, "看板不应显示估算说明小字");
            False(standalone.IndexOf("适合截图、复制或导出", StringComparison.Ordinal) >= 0, "参考表不应显示用途说明小字");
            True(standalone.IndexOf("style=\"display:none\"><div class=\"qd-panel-head\"><div><h3>动态工程量图表", StringComparison.Ordinal) >= 0, "动态图表卡片当前应隐藏");
            False(standalone.IndexOf("<small>" + "'+html(sub)", StringComparison.Ordinal) >= 0, "汇总卡片不应显示说明小字");
            True(standalone.IndexOf("道路拆除", StringComparison.Ordinal) >= 0, "看板总览应直接显示道路拆除指标");
            True(standalone.IndexOf("余土道渣外运", StringComparison.Ordinal) >= 0, "看板总览应直接显示外运指标");
        }

        private static void TestQuantityAttributeEditorSharedPage()
        {
            string embedded = CDBoxStudioQuantityAttributeEditorPage.BuildEmbeddedSection();
            string standalone = CDBoxStudioQuantityAttributeEditorPage.BuildStandaloneDocument(
                new CDBoxStudioSettings { Theme = "dark", AnimationsEnabled = false }, "test.log", "drawing.dwg", "A1");

            True(embedded.IndexOf("quantityAttributeEditorPage", StringComparison.Ordinal) >= 0, "内嵌属性编辑器应提供共享根节点");
            True(standalone.IndexOf("CDBoxQuantityAttributeEditorPage.create", StringComparison.Ordinal) >= 0, "独立窗口应创建同一共享组件");
            True(standalone.IndexOf("standalone:true", StringComparison.Ordinal) >= 0, "独立属性编辑器应启用独立模式");
            True(standalone.IndexOf("3.1.1", StringComparison.Ordinal) >= 0, "页面应显示 3.1.1 身份");
            True(standalone.IndexOf("data-theme=\"dark\"", StringComparison.Ordinal) >= 0, "独立属性编辑器应继承主题");
            True(standalone.IndexOf("qa-structure", StringComparison.Ordinal) >= 0, "结构层应使用表格编辑器");
            True(standalone.IndexOf("data-layer", StringComparison.Ordinal) >= 0, "结构层表格应允许直接编辑单元格");
            True(standalone.IndexOf("bindLayerDrag", StringComparison.Ordinal) >= 0, "结构层应支持拖动排序");
            True(standalone.IndexOf("['IsSpecialObject','特殊对象','bool']", StringComparison.Ordinal) >= 0, "属性编辑器应提供特殊对象开关");
            True(standalone.IndexOf("!self.attrs.IsSpecialObject", StringComparison.Ordinal) >= 0, "特殊对象应解锁平均深度");
            True(standalone.IndexOf("special?'disabled'", StringComparison.Ordinal) >= 0, "特殊对象应禁用自动识别");
            True(standalone.IndexOf("step=\"0.01\"", StringComparison.Ordinal) >= 0, "常规数值输入应以 0.01 为步长");
            True(standalone.IndexOf("class=\"qa-drag\" draggable=\"true\"", StringComparison.Ordinal) >= 0, "结构层应通过独立拖拽柄排序");
            False(standalone.IndexOf("class=\"qa-layer-row\" draggable=\"true\"", StringComparison.Ordinal) >= 0, "结构层整行不得触发拖拽");
            True(standalone.IndexOf("calculateQuantityDraft", StringComparison.Ordinal) >= 0, "源字段变化应调用统一 C# 草稿联动服务");
            True(standalone.IndexOf("scheduleDraft", StringComparison.Ordinal) >= 0, "属性编辑器应实时请求派生值更新");
            True(standalone.IndexOf("captureFocus", StringComparison.Ordinal) >= 0, "草稿回传后应恢复当前输入焦点");
            True(standalone.IndexOf("stash.appendChild(e)", StringComparison.Ordinal) >= 0, "草稿重绘时应保留原输入控件及数字光标位置");
            True(standalone.IndexOf("oncompositionstart", StringComparison.Ordinal) >= 0, "中文输入法合成期间不应触发页面重绘");
            True(standalone.IndexOf("function fmtInput", StringComparison.Ordinal) >= 0, "界面数值应清除浮点尾差");
            False(standalone.IndexOf("data-sec=", StringComparison.Ordinal) >= 0, "属性编辑器不应保留左侧导航");
            False(standalone.IndexOf("图层识别信息", StringComparison.Ordinal) >= 0, "属性编辑器不应显示图层识别信息卡片");
            True(standalone.IndexOf("属性与结构层", StringComparison.Ordinal) >= 0, "基本参数与结构层应合并显示");
            False(standalone.IndexOf("function parseLayers", StringComparison.Ordinal) >= 0, "前端不得自行解析结构层业务文本");
            False(standalone.IndexOf("function encodeLayers", StringComparison.Ordinal) >= 0, "前端不得建立第二套结构层序列化逻辑");
            False(standalone.IndexOf("['Remark','备注'", StringComparison.Ordinal) >= 0, "新界面不应恢复已删除的备注字段");
            False(standalone.IndexOf("打开旧版", StringComparison.Ordinal) >= 0, "属性编辑器不应保留旧版入口按钮");
            False(embedded.IndexOf("独立窗口", StringComparison.Ordinal) >= 0, "内嵌属性编辑器不应保留独立窗口按钮");
            False(standalone.IndexOf(">重新选择<", StringComparison.Ordinal) >= 0, "属性编辑器不应保留重新选择按钮");
            False(standalone.IndexOf(">智能刷新<", StringComparison.Ordinal) >= 0, "属性编辑器刷新按钮不应保留旧文案");
            False(standalone.IndexOf(">按默认表重填<", StringComparison.Ordinal) >= 0, "属性编辑器不应保留默认表重填按钮");
            False(standalone.IndexOf("data-act=\"\"close\"\"", StringComparison.Ordinal) >= 0, "属性编辑器不应保留关闭按钮");
            True(standalone.IndexOf(">刷新<", StringComparison.Ordinal) >= 0, "属性编辑器应显示精简后的刷新按钮");
            True(standalone.IndexOf("<span>图层</span>", StringComparison.Ordinal) >= 0, "对象摘要应只显示图层信息");
            True(standalone.IndexOf("<span>长度</span>", StringComparison.Ordinal) >= 0, "对象摘要应显示 CAD 长度");
            False(standalone.IndexOf("<span>Handle</span>", StringComparison.Ordinal) >= 0, "对象摘要不应显示 Handle");
            False(standalone.IndexOf("CAD / 有效长度", StringComparison.Ordinal) >= 0, "对象摘要不应显示有效长度");
        }

        private static void TestNumericInputSteps()
        {
            string annotationScript = CDBoxStudioAnnotationSettingsPage.BuildComponentScript();
            string annotationEmbedded = CDBoxStudioAnnotationSettingsPage.BuildEmbeddedSection();
            string annotationStandalone = CDBoxStudioAnnotationSettingsPage.BuildStandaloneDocument(new CDBoxStudioSettings(), "test.log", "pipeLength");
            True(annotationScript.IndexOf("step=\"0.01\"", StringComparison.Ordinal) >= 0, "标注设置小数输入应以 0.01 为步长");
            False(annotationScript.IndexOf("step=\"0.1\"", StringComparison.Ordinal) >= 0, "标注设置不应保留 0.1 小数步长");
            False(annotationScript.IndexOf("step=\"0.05\"", StringComparison.Ordinal) >= 0, "标注设置不应保留 0.05 小数步长");
            False(annotationScript.IndexOf("step=\"0.001\"", StringComparison.Ordinal) >= 0, "标注设置不应保留 0.001 小数步长");
            False(annotationEmbedded.IndexOf("data-action=\"reset-current\"", StringComparison.Ordinal) >= 0, "内嵌标注设置不应保留恢复默认按钮");
            False(annotationStandalone.IndexOf("data-action=\"reset-current\"", StringComparison.Ordinal) >= 0, "独立标注设置不应保留恢复默认按钮");
            False(annotationStandalone.IndexOf("data-action=\"close\">关闭", StringComparison.Ordinal) >= 0, "独立标注设置不应保留关闭按钮");
            True(annotationStandalone.IndexOf("<h2>标注设置</h2></div><div class=\"as-head-actions\"><button", StringComparison.Ordinal) >= 0, "保存设置应与标题同排并靠右");
        }

        private static void TestLayerManagerCustomParents()
        {
            string script = CDBoxStudioLayerManagerPage.BuildComponentScript();
            string styles = CDBoxStudioLayerManagerPage.BuildStyles(false);
            True(styles.Length > 10000, "Layer Manager shared styles must not be missing");
            True(styles.IndexOf(".lm-page", StringComparison.Ordinal) >= 0, "Layer Manager page layout styles must be present");
            True(styles.IndexOf(".lm-grid-header", StringComparison.Ordinal) >= 0, "Layer Manager grid styles must be present");
            True(styles.IndexOf(".lm-grid-row.dragging", StringComparison.Ordinal) >= 0, "Layer Manager drag state styles must be present");
            False(script.IndexOf("branch('other','其他'", StringComparison.Ordinal) >= 0, "不应再生成合成的“其他”父级");
            True(script.IndexOf("out+=otherChildren;", StringComparison.Ordinal) >= 0, "自定义父级应直接显示在树根");
            True(script.IndexOf("draggedLayerName", StringComparison.Ordinal) >= 0, "图层表应支持拖动排序");
            True(script.IndexOf("class=\"lm-drag-handle\" draggable=\"true\"", StringComparison.Ordinal) >= 0, "图层表应使用独立拖拽柄");
            False(script.IndexOf("lm-grid-row '+(dirty?'dirty ':'')+(selected?'selected ':'')+(failure?'failed ':'')+'\" draggable=\"true\"", StringComparison.Ordinal) >= 0, "图层整行不得触发拖拽");
            False(script.IndexOf("树状分类筛选、行内属性编辑", StringComparison.Ordinal) >= 0, "图层管理器不应显示冗余说明");
            False(script.IndexOf(">独立窗口<", StringComparison.Ordinal) >= 0, "内嵌图层管理器不应保留独立窗口按钮");
            False(script.IndexOf(">打开旧版<", StringComparison.Ordinal) >= 0, "独立图层管理器不应保留旧版入口");
            False(script.IndexOf("data-action=\"close\">关闭", StringComparison.Ordinal) >= 0, "独立图层管理器不应保留关闭按钮");
        }

        private static void TestQuantityDefaultsTableInteraction()
        {
            string script = CDBoxStudioQuantityDefaultsPage.BuildEmbeddedBridgeScript();
            True(script.IndexOf("class=\"layer-drag-handle\" draggable=\"true\"", StringComparison.Ordinal) >= 0, "默认表结构层应使用独立拖拽柄");
            True(script.IndexOf("draggedLayer", StringComparison.Ordinal) >= 0, "默认表应绑定拖动排序");
            False(script.IndexOf("<tr draggable=\"true\"", StringComparison.Ordinal) >= 0, "默认表整行不得触发拖拽");
            False(script.IndexOf(">上移<", StringComparison.Ordinal) >= 0, "默认表不应保留上移按钮");
            False(script.IndexOf(">下移<", StringComparison.Ordinal) >= 0, "默认表不应保留下移按钮");
            False(script.IndexOf("data-layer-row-action=\"up\"", StringComparison.Ordinal) >= 0, "默认表不应保留行上移操作");
            False(script.IndexOf("data-layer-row-action=\"down\"", StringComparison.Ordinal) >= 0, "默认表不应保留行下移操作");
            False(script.IndexOf("恢复此表默认结构层", StringComparison.Ordinal) >= 0, "默认表不应保留结构层恢复按钮");
            string standalone = CDBoxStudioQuantityDefaultsPage.BuildStandaloneDocument(new CDBoxStudioSettings(), "test.log");
            False(standalone.IndexOf("打开旧版", StringComparison.Ordinal) >= 0, "属性默认表不应保留旧版入口按钮");
            False(standalone.IndexOf("id=\"restoreDefaultProfilesButton\"", StringComparison.Ordinal) >= 0, "独立默认表不应保留恢复默认按钮");
            False(standalone.IndexOf("id=\"closeWindow\"", StringComparison.Ordinal) >= 0, "独立默认表不应保留关闭按钮");
            True(standalone.IndexOf("class=\"qd-tabs-row\"", StringComparison.Ordinal) >= 0, "独立默认表保存按钮应与默认表标签同排");
            string embedded = CDBoxStudioQuantityDefaultsPage.BuildEmbeddedSection();
            False(embedded.IndexOf("返回总览", StringComparison.Ordinal) >= 0, "内嵌默认表不应保留返回总览按钮");
            False(embedded.IndexOf("settings-head", StringComparison.Ordinal) >= 0, "内嵌默认表不应保留标题卡片");
            False(embedded.IndexOf("restoreDefaultProfilesButton", StringComparison.Ordinal) >= 0, "内嵌默认表不应保留恢复默认按钮");
            True(embedded.IndexOf("class=\"qd-tabs-row\"", StringComparison.Ordinal) >= 0, "内嵌默认表保存按钮应与默认表标签同排");
        }

        private static void TestSectionDrawingSharedPage()
        {
            string script = CDBoxStudioSectionDrawingPage.BuildComponentScript();
            string styles = CDBoxStudioSectionDrawingPage.BuildStyles(false);
            var settings = new CDBoxStudioSettings { Theme = "dark", AnimationsEnabled = false };
            string standalone = CDBoxStudioSectionDrawingPage.BuildStandaloneDocument(settings, "studio.log");

            True(styles.Length > 8000, "断面图共享样式不应缺失");
            True(script.IndexOf("<svg data-preview", StringComparison.Ordinal) >= 0, "断面图应使用自定义 SVG 预览");
            True(script.IndexOf("sd-hatch-diag", StringComparison.Ordinal) >= 0, "断面图预览应包含矢量填充图案");
            True(script.IndexOf("bindLayerDrag", StringComparison.Ordinal) >= 0, "结构层表格应支持拖动排序");
            True(script.IndexOf("bindPipeDrag", StringComparison.Ordinal) >= 0, "管道表格应支持拖动排序");
            True(script.IndexOf("addEventListener('wheel'", StringComparison.Ordinal) >= 0, "预览应支持滚轮缩放");
            True(styles.IndexOf("grid-template-columns:minmax(0,1fr) 460px", StringComparison.Ordinal) >= 0, "右侧预览区应保持固定宽度");
            True(script.IndexOf("rebalanceHeights", StringComparison.Ordinal) >= 0, "锁定总高度后应自动重算未锁定层");
            True(script.IndexOf("data-field='TotalHeight' value='${fmt(o.TotalHeight)}'></label>", StringComparison.Ordinal) >= 0, "总高度输入框应始终允许编辑");
            True(script.IndexOf("if(key==='TotalHeight'){this.options.LockTotalHeight=true", StringComparison.Ordinal) >= 0, "直接修改总高度时应自动切换到锁定输入值");
            True(script.IndexOf("<span>管段注记</span><textarea", StringComparison.Ordinal) >= 0, "管段注记应支持多行输入");
            True(script.IndexOf("titleLines", StringComparison.Ordinal) >= 0, "多行管段注记应逐行预览");
            True(script.IndexOf("<span>注记样式</span><select data-field='TextStyleName'", StringComparison.Ordinal) >= 0, "注记样式应使用当前图纸样式下拉框");
            True(script.IndexOf("data-layer-field='HatchPatternName'", StringComparison.Ordinal) >= 0, "填充图案应使用选择控件");
            True(script.IndexOf("list='sd-hatch-patterns'", StringComparison.Ordinal) >= 0, "填充图案应支持搜索和手工输入");
            True(script.IndexOf("step='0.0001' min='0' data-layer-field='HatchScale'", StringComparison.Ordinal) >= 0, "填充比例应支持精细小数步长");
            True(script.IndexOf("data-field='DrawingScale'", StringComparison.Ordinal) >= 0, "断面图应提供绘图放大倍数");
            True(script.IndexOf("translate(450 325) scale(${this.zoom}) translate(-450 -325)", StringComparison.Ordinal) >= 0, "预览缩放应围绕视框中心");
            True(script.IndexOf("class='sd-drag' draggable='true'", StringComparison.Ordinal) >= 0, "断面表格应使用独立拖拽柄");
            False(script.IndexOf("sd-layer-row' draggable='true'", StringComparison.Ordinal) >= 0, "断面结构层整行不得触发拖拽");
            False(script.IndexOf("sd-pipe-row' draggable='true'", StringComparison.Ordinal) >= 0, "断面管道整行不得触发拖拽");
            False(script.IndexOf("data-layer-field='HatchAngle'", StringComparison.Ordinal) >= 0, "结构层表格不应保留填充角度选项");
            False(script.IndexOf("data-act='reset'>恢复默认", StringComparison.Ordinal) >= 0, "断面图不应保留恢复默认按钮");
            False(script.IndexOf("data-act='close'>关闭", StringComparison.Ordinal) >= 0, "断面图独立页不应保留关闭按钮");
            True(standalone.IndexOf("Preview 10", StringComparison.Ordinal) >= 0, "独立页应显示 Preview 10 身份");
            True(standalone.IndexOf("standalone:true", StringComparison.Ordinal) >= 0, "独立页应启用独立宿主模式");
            True(standalone.IndexOf("data-theme=\"dark\"", StringComparison.Ordinal) >= 0, "独立页应继承 Studio 主题");
            False(standalone.IndexOf("打开旧版", StringComparison.Ordinal) >= 0, "断面图不应保留旧版入口按钮");
            False(script.IndexOf(">独立窗口<", StringComparison.Ordinal) >= 0, "内嵌断面图不应保留独立窗口按钮");
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

                foreach (string dependency in CDBoxRequiredRuntimeFiles.ManagedDependencies)
                {
                    File.WriteAllText(Path.Combine(root, dependency), dependency);
                }
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
                    foreach (string dependency in CDBoxRequiredRuntimeFiles.ManagedDependencies)
                    {
                        WriteEntry(archive, "CDBox.bundle/Contents/" + dependency, dependency);
                    }
                    WriteEntry(archive, "CDBox.bundle/Contents/runtimes/win-x64/native/WebView2Loader.dll", "loader");
                    WriteEntry(archive, "CDBox.bundle/Contents/Updater/CDBoxUpdater.exe", "updater");
                }

                var progress = new List<int>();
                BundleInstallOutcome outcome = BundleInstaller.Install(
                    NewPending(packagePath, target, Path.Combine(root, "work")),
                    delegate(int percent, string message) { progress.Add(percent); });
                True(outcome.Success, "有效 bundle 应替换成功");
                True(progress.Count >= 6, "更新器应报告实际安装阶段进度");
                Equal(25, progress[0], "安装进度应从校验阶段开始");
                Equal(98, progress[progress.Count - 1], "安装完成前应执行最终校验进度");
                for (int i = 1; i < progress.Count; i++) True(progress[i] >= progress[i - 1], "安装进度不得倒退");
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
