using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CDBox.Shared;
using CDBox.Shared.Modules;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using CDBox.RealEstate.Module;
using CDBoxUpdater;
using TCPipeAutoDraw.Modules.WastewaterResultTable;
using TCPipeAutoDraw.Core.Startup;
using TCPipeAutoDraw.Core.Colors;
using TCPipeAutoDraw.Core.FloatingCenter;
using TCPipeAutoDraw.Core.Check;
using TCPipeAutoDraw.Core.Sync;
using TCPipeAutoDraw.Modules.QuantityCalculation;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;
using TCPipeAutoDraw.Modules.NodeAnnotation;
using TCPipeAutoDraw.Modules.ExcelToCad;
using TCPipeAutoDraw.Modules.LayerManager;
using TCPipeAutoDraw.Modules.SectionDrawing;
using TCPipeAutoDraw.Modules.FrameLayout;
using TCPipeAutoDraw.Modules.ShortCodeRecognition;
using TCPipeAutoDraw.Modules.LongitudinalProfile;
using TCPipeAutoDraw.UI.Studio;
using TCPipeAutoDraw.UI.FloatingCenter;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;

namespace CDBox.CoreTests
{
    internal static class Program
    {
        private static readonly List<string> Failures = new List<string>();
        private static int _passed;

        private static int Main()
        {
            Run("对象类型识别", TestKindRecognition);
            Run("节点图层严格识别", TestNodeLayerRecognitionPolicy);
            Run("有效长度与默认表克隆", TestEffectiveLengthAndDefaultClone);
            Run("结构层解析", TestStructureLayers);
            Run("工程量管线分类", TestQuantityPipeClassification);
            Run("特殊对象跳过数据质量检查", TestSpecialObjectsSkipQualityCheck);
            Run("沉泥井管沟深度", TestSiltWellDepth);
            Run("工程量依赖联动", TestQuantityDependencyRules);
            Run("常用文本解析", TestPrimitiveParsing);
            Run("Studio 路由消息", TestStudioRouteRequest);
            Run("内置更新源优先级", TestBuiltInUpdateSourcePriority);
            Run("阶段 A 新安装启动职责", TestStageANewInstallDefaults);
            Run("RealEstate 最小模块边界", TestRealEstateModuleBoundary);
            Run("数值输入步长统一", TestNumericInputSteps);
            Run("工程量看板共享页面", TestQuantityDashboardSharedPage);
            Run("工程量计算过程导出", TestQuantityCalculationProcessExport);
            Run("属性编辑器共享页面", TestQuantityAttributeEditorSharedPage);
            Run("图层管理器自定义父级", TestLayerManagerCustomParents);
            Run("图层名称结构化识别", TestStructuredLayerRecognition);
            Run("统一颜色选择器与 ACI 转换", TestColorPickerIntegration);
            Run("断面图 Preview 10 共享页面", TestSectionDrawingSharedPage);
            Run("属性默认表统一表格交互", TestQuantityDefaultsTableInteraction);
            Run("标注浮窗文字组合与旧数据迁移", TestAnnotationHudTextComposition);
            Run("节点标注绑定文字组合", TestNodeAnnotationTextComposition);
            Run("Excel 表格范围读取与样式保留", TestExcelTableRangeReading);
            Run("图框布置设置归一化", TestFrameLayoutSettingsNormalization);
            Run("旋转裁图矩形尺寸与方向", TestFrameCutRegionMath);
            Run("简码识别容错解析与共享设置页", TestShortCodeRecognition);
            Run("纵断面路径、高程与坡度计算", TestLongitudinalProfileCalculation);
            Run("旧版更新源完整性校验", TestLegacyUpdateSourceValidation);
            Run("更新包路径越界防护", TestUpdaterRejectsZipTraversal);
            Run("更新器替换与备份", TestUpdaterReplacesAndBacksUpBundle);
            Run("污水管成果表排序与格式", TestWastewaterResultTableFormatting);
            Run("悬浮球 Phase 0 消息与文档隔离", TestFloatingCenterPhaseZero);
            Run("悬浮球 Phase 1 位置与状态呈现", TestFloatingCenterPhaseOne);
            Run("悬浮球 Phase 2 提示队列与进度终结", TestFloatingCenterPhaseTwo);
            Run("悬浮球 Phase 3 图纸检查聚合与隔离", TestFloatingCenterPhaseThree);
            Run("悬浮球 Phase 4 同步任务聚合与历史", TestFloatingCenterPhaseFour);
            Run("悬浮球 Phase 5 兼容通知迁移策略", TestFloatingCenterPhaseFive);

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

        private static void TestNodeLayerRecognitionPolicy()
        {
            True(QuantityNodeRecognitionPolicy.IsSupportedLayer(
                    "井", "检查、沉泥井"),
                "节点必须接受规定的井父属性与合并分类");
            True(QuantityNodeRecognitionPolicy.IsSupportedLayer(
                    " 井 ", "检查/沉泥井"),
                "分类分隔符差异不应影响同一规则");
            False(QuantityNodeRecognitionPolicy.IsSupportedLayer(
                    "井", "检查井"),
                "只有井父属性但分类不符合时不得识别为节点");
            False(QuantityNodeRecognitionPolicy.IsSupportedLayer(
                    "构筑物", "检查、沉泥井"),
                "只有分类符合但父属性不是井时不得识别为节点");
            Equal(string.Empty,
                DrawingCheckClassification.NormalizeKind("315井"),
                "自定义父属性 315井 不得被误判为内置井类型");
            Equal(string.Empty,
                DrawingCheckClassification.NormalizeKind("CDBox图层"),
                "插件输出图层父属性不应参与工程对象检查");
        }

        private static void TestFrameLayoutSettingsNormalization()
        {
            var settings = new FrameLayoutSettings
            {
                NorthDirectionMode = "invalid",
                NorthReferencePosition = "invalid",
                ScaleReferencePosition = "BottomLeft",
                NorthSize = -1,
                ScaleText = " ",
                ScaleTextHeight = 0,
                ScaleColorIndex = 0,
                FramesPerRow = 0,
                HorizontalGap = -2,
                VerticalGap = double.NaN,
                TemplateViewMode = "invalid"
            };
            settings.Normalize();
            Equal("Auto", settings.NorthDirectionMode,
                "无效指北方向应回退为自动");
            Equal("TopRight", settings.NorthReferencePosition,
                "无效参考位置应回退到右上角");
            Equal("BottomLeft", settings.ScaleReferencePosition,
                "有效比例参考位置应保留");
            Near(1.0, settings.NorthSize, 1e-9,
                "指北针大小应限制为正值");
            Equal("1:500", settings.ScaleText,
                "空比例文字应使用默认值");
            Near(0.1, settings.ScaleTextHeight, 1e-9,
                "比例文字高度应限制为正值");
            Equal((short)1, settings.ScaleColorIndex,
                "无效颜色索引应回退到红色");
            Equal(1, settings.FramesPerRow,
                "每行图框数至少为一");
            Near(0.0, settings.HorizontalGap, 1e-9,
                "布框间距不得为负");
            Near(10.0, settings.VerticalGap, 1e-9,
                "非数值布框间距应使用默认值");
            Equal("Double", settings.TemplateViewMode,
                "无效模板视图应回退到双列");
            settings.TemplateViewMode = "single";
            settings.Normalize();
            Equal("Single", settings.TemplateViewMode,
                "有效单列模板视图应保留");
        }

        private static void TestFrameCutRegionMath()
        {
            True(FrameCutRegionMath.Fits(210.0, 148.5, 210.0, 148.5),
                "旋转前后的矩形应按自身边长校验，不应按世界坐标包围盒误判");
            False(FrameCutRegionMath.Fits(210.01, 148.5, 210.0, 148.5),
                "真实边长超过图框可用范围时仍应拒绝");
        }

        private static void TestShortCodeRecognition()
        {
            string data =
                "1,道路起点,100,200,1\r\n"
                + "2,连,101,201,2\r\n"
                + "中文异常行，不足字段\r\n"
                + "3,连,102,202,3\r\n"
                + "4,检查井,103,203,4\r\n"
                + "5,0连,104,204,5\r\n";
            ShortCodeReadResult parsed =
                ShortCodeRecognitionParser.ParseText(data);
            Equal(5, parsed.Records.Count,
                "中文代码不应妨碍有效坐标读取");
            Equal(1, parsed.InvalidLineCount,
                "异常行应单独跳过而不是终止文件");

            string encodedPath = Path.Combine(Path.GetTempPath(),
                "cdbox-shortcode-" + Guid.NewGuid().ToString("N") + ".dat");
            string utf16Path = encodedPath + ".utf16.dat";
            try
            {
                File.WriteAllText(encodedPath,
                    "1,中文道路,100,200,1\r\n2,+,101,201,2",
                    Encoding.GetEncoding(54936));
                ShortCodeReadResult encoded =
                    ShortCodeRecognitionParser.ReadFile(encodedPath);
                Equal(2, encoded.Records.Count,
                    "GB18030 中文简码文件应完整读取");
                Equal("中文道路", encoded.Records[0].Code,
                    "中文简码不应被损坏");
                File.WriteAllText(utf16Path,
                    "1,中文围墙,100,200,1\r\n2,+,101,201,2",
                    new UnicodeEncoding(false, false, true));
                ShortCodeReadResult utf16 =
                    ShortCodeRecognitionParser.ReadFile(utf16Path);
                Equal("中文围墙", utf16.Records[0].Code,
                    "无 BOM 的 UTF-16 中文简码也应安全识别");
            }
            finally
            {
                if (File.Exists(encodedPath)) File.Delete(encodedPath);
                if (File.Exists(utf16Path)) File.Delete(utf16Path);
            }

            ShortCodeReadResult cassExample =
                ShortCodeRecognitionParser.ParseText(
                    "4,X2,54116.1,31129.0,491.7\r\n"
                    + "5,+,54128.0,31140.1,492.2\r\n"
                    + "6,+,54136.8,31153.4,493.7\r\n"
                    + "7,+,54143.5,31175.0,492.5\r\n"
                    + "8,+,54151.9,31195.3,494.2\r\n"
                    + "9,W0,54161.3,31214.6,494.8");
            List<ShortCodePath> cassPaths =
                ShortCodeRecognitionParser.BuildPaths(
                    cassExample.Records,
                    new ShortCodeRecognitionSettings());
            Equal(1, cassPaths.Count,
                "CASS 连续加号示例应生成一条路径");
            Equal(5, cassPaths[0].Points.Count,
                "CASS 默认应连接起始地物码点但不串入下一地物");

            var settings = new ShortCodeRecognitionSettings
            {
                RecognitionSymbol = "连",
                ConnectPreviousPoint = true,
                ConnectNextPoint = true,
                AutoClose = true
            };
            List<ShortCodePath> paths =
                ShortCodeRecognitionParser.BuildPaths(
                    parsed.Records, settings);
            Equal(2, paths.Count,
                "连续关系码与数字跳点关系码都应被识别");
            Equal(4, paths[0].Points.Count,
                "首尾相邻点选项应扩展连续关系码路径");
            True(paths[0].Closed,
                "自动闭合应应用于至少三个点的连续路径");
            True(paths[1].IsJumpConnection,
                "数字前缀关系码应建立跳点连接");
            False(paths[1].Closed,
                "两点跳线不应生成无意义闭合");

            settings.RecognitionSymbol = "\r\n";
            settings.Normalize();
            Equal("+", settings.RecognitionSymbol,
                "空白或换行符号应安全回退为加号");

            string standalone =
                CDBoxStudioShortCodeSettingsPage.BuildStandaloneDocument(
                    new CDBoxStudioSettings());
            string embedded =
                CDBoxStudioShortCodeSettingsPage.BuildEmbeddedSection(
                    new CDBoxStudioSettings());
            Contains(standalone, "简码识别设置",
                "独立设置页应使用统一页面");
            Contains(embedded, "shortCodeSettingsFrame",
                "内嵌设置页应复用同一页面文档");
            Contains(standalone, "连接开头前一个点",
                "设置页应提供开头前一点选项");
            Contains(standalone, "连接结尾后一个点",
                "设置页应提供结尾后一点选项");
            Contains(standalone, "自动闭合",
                "设置页应提供自动闭合选项");
        }

        private static void TestLongitudinalProfileCalculation()
        {
            var pipes = new List<LongitudinalProfilePipeData>
            {
                new LongitudinalProfilePipeData
                {
                    SourceId = "P2",
                    StartNode = "W-52",
                    EndNode = "W-53",
                    Diameter = "DN200",
                    PlanLength = 20.0,
                    SelectionOrder = 1
                },
                new LongitudinalProfilePipeData
                {
                    SourceId = "P1",
                    StartNode = "W-51",
                    EndNode = "W-52",
                    Diameter = "DN200",
                    Foundation = "砂石基础",
                    PlanLength = 16.59,
                    SelectionOrder = 0
                }
            };
            var wells = new List<LongitudinalProfileWellData>
            {
                new LongitudinalProfileWellData
                {
                    NodeNo = "W-51",
                    GroundElevation = 1407.859,
                    WellDepth = 0.868,
                    WellSpec = "φ500",
                    WellType = "检查井"
                },
                new LongitudinalProfileWellData
                {
                    NodeNo = "W-52",
                    GroundElevation = 1407.404,
                    WellDepth = 0.485,
                    WellSpec = "φ500",
                    WellType = "检查井"
                },
                new LongitudinalProfileWellData
                {
                    NodeNo = "W-53",
                    GroundElevation = 1407.000,
                    WellDepth = 0.800,
                    WellSpec = "φ700",
                    WellType = "沉泥井",
                    SiltWellDeductDepth700 = 0.50
                }
            };
            LongitudinalProfileBuildResult result =
                LongitudinalProfileCalculator.Build(pipes, wells);
            True(result.Success, result.Message);
            Equal(3, result.Profile.Nodes.Count,
                "两段管线应生成三个井节点");
            Equal("W-51", result.Profile.Nodes[0].NodeNo,
                "路径应按首选管线的端点排序");
            Near(1406.991,
                result.Profile.Nodes[0].DesignInvertElevation, 1e-9,
                "设计管内底标高应为自然地面标高减井深");
            Near(1406.919,
                result.Profile.Nodes[1].DesignInvertElevation, 1e-9,
                "终点设计管内底标高计算");
            Near(4.340,
                result.Profile.Spans[0].SlopePermille, 0.001,
                "坡度应按两端管内底高差除平面距离计算为千分比");
            Near(0.434,
                result.Profile.Spans[0].SlopePercent, 0.001,
                "参考图中的坡度显示值应按百分比输出");
            Equal("砂石基础", result.Profile.Spans[0].Foundation,
                "管道基础应传入纵断面数据栏");
            Near(1406.700,
                result.Profile.Nodes[2].DesignInvertElevation, 1e-9,
                "700沉泥井设计管内底标高应增加0.50米扣减值");
            Near(0.300, result.Profile.Nodes[2].PipeBottomDepth, 1e-9,
                "沉泥井管内底埋深应从井深扣除沉泥段");
            Near(36.59, result.Profile.Nodes[2].CumulativeDistance, 1e-9,
                "累计平面距离");

            LongitudinalProfileBuildResult reversed =
                LongitudinalProfileCalculator.BuildBetweenNodes(
                    pipes, wells, "W-53", "W-51");
            True(reversed.Success, reversed.Message);
            Equal("W-53", reversed.Profile.Nodes[0].NodeNo,
                "纵断面左端必须保持为用户选择的起点节点");
            Equal("W-51", reversed.Profile.Nodes[2].NodeNo,
                "纵断面右端必须保持为用户选择的终点节点");

            LongitudinalProfileBuildResult reversedSingle =
                LongitudinalProfileCalculator.BuildBetweenNodes(
                    new[] { pipes[1] }, wells, "W-52", "W-51");
            True(reversedSingle.Success, reversedSingle.Message);
            Equal("W-52", reversedSingle.Profile.Nodes[0].NodeNo,
                "单管段也不能按原实体方向颠倒用户选择的起终点");

            var connectionPipes = new List<LongitudinalProfilePipeData>
            {
                new LongitudinalProfilePipeData
                {
                    SourceId = "UP", StartNode = "X", EndNode = "A",
                    Diameter = "DN300", OuterDiameter = 0.3,
                    PlanLength = 10, StartDepth = 1.4,
                    EndDepth = 1.1, HasGeometry = true,
                    GeometryStartX = -10, GeometryStartY = 0,
                    GeometryEndX = 0, GeometryEndY = 0
                },
                new LongitudinalProfilePipeData
                {
                    SourceId = "MAIN-1", StartNode = "A", EndNode = "B",
                    Diameter = "DN300", PlanLength = 10,
                    HasGeometry = true, GeometryStartX = 0,
                    GeometryStartY = 0, GeometryEndX = 10,
                    GeometryEndY = 0
                },
                new LongitudinalProfilePipeData
                {
                    SourceId = "MAIN-2", StartNode = "B", EndNode = "C",
                    Diameter = "DN300", PlanLength = 10,
                    HasGeometry = true, GeometryStartX = 10,
                    GeometryStartY = 0, GeometryEndX = 20,
                    GeometryEndY = 0
                },
                new LongitudinalProfilePipeData
                {
                    SourceId = "SIDE-L", StartNode = "B", EndNode = "D",
                    Diameter = "DN200", PlanLength = 6,
                    StartDepth = 1.25, HasGeometry = true,
                    GeometryStartX = 10, GeometryStartY = 0,
                    GeometryEndX = 10, GeometryEndY = 6
                },
                new LongitudinalProfilePipeData
                {
                    SourceId = "DOWN", StartNode = "C", EndNode = "Y",
                    Diameter = "DN300", OuterDiameter = 0.3,
                    PlanLength = 10, StartDepth = 1.2,
                    EndDepth = 1.3, HasGeometry = true,
                    GeometryStartX = 20, GeometryStartY = 0,
                    GeometryEndX = 30, GeometryEndY = 0
                }
            };
            var connectionWells = new List<LongitudinalProfileWellData>
            {
                PositionedWell("X", -10, 0, 101, 1.4),
                PositionedWell("A", 0, 0, 100, 1),
                PositionedWell("B", 10, 0, 99, 1),
                PositionedWell("C", 20, 0, 98, 1),
                PositionedWell("D", 10, 6, 99, 1),
                PositionedWell("Y", 30, 0, 97, 1.3)
            };
            LongitudinalProfileBuildResult withConnection =
                LongitudinalProfileCalculator.BuildBetweenNodes(
                    connectionPipes, connectionWells, "A", "C");
            True(withConnection.Success, withConnection.Message);
            Equal(1, withConnection.Profile.Connections.Count,
                "路径外接入井的管线应生成一个侧面接入断面");
            Equal("左侧", withConnection.Profile.Connections[0].Side,
                "从起点向终点观察，位于路径左侧的接入管应标注为左侧");
            Near(withConnection.Profile.Nodes[1].DesignInvertElevation,
                withConnection.Profile.Connections[0].InvertElevation,
                1e-9, "侧面接入口高程应使用接口所在井的设计管内底标高");

            True(withConnection.Profile.StartExtension != null,
                "起点井前的连续主管应生成外延主管示意");
            True(withConnection.Profile.EndExtension != null,
                "终点井后的连续主管应生成外延主管示意");
            Equal("UP", withConnection.Profile.StartExtension.SourceId,
                "起点外延主管应识别路径反向延伸的主管");
            Equal("DOWN", withConnection.Profile.EndExtension.SourceId,
                "终点外延主管应识别路径正向延伸的主管");
            Near(withConnection.Profile.Nodes[0].DesignInvertElevation,
                withConnection.Profile.StartExtension
                    .BoundaryInvertElevation, 1e-9,
                "外延主管在端井处必须与所选纵断面的设计管底衔接");
            Near(99.7,
                withConnection.Profile.StartExtension
                    .OutsideInvertElevation, 1e-9,
                "外延主管应保留井外原主管的坡度");
            LongitudinalProfileLayout extendedLayout =
                LongitudinalProfileLayoutCalculator.Calculate(
                    withConnection.Profile,
                    new LongitudinalProfileSettings());
            Near(37.5, extendedLayout.PlotRight, 1e-9,
                "右端井、外延主管或截断线超出网格时应向右增加一格");

            LongitudinalProfileBuildResult disconnected =
                LongitudinalProfileCalculator.Build(new[]
                {
                    pipes[0],
                    new LongitudinalProfilePipeData
                    {
                        StartNode = "X1",
                        EndNode = "X2",
                        PlanLength = 1,
                        SelectionOrder = 2
                    }
                }, wells);
            False(disconnected.Success, "不连通管线应被拒绝");

            var settings = new LongitudinalProfileSettings
            {
                HeaderWidth = -1,
                HorizontalScale = 0,
                VerticalScale = double.NaN,
                Rows = new List<LongitudinalProfileRowSettings>()
            };
            settings.Normalize();
            Near(45.0, settings.HeaderWidth, 1e-9,
                "表头宽度应回退默认值");
            Near(6.0, settings.HeaderTextHeight, 1e-9,
                "无效表头文字高度应回退默认值");
            Equal("宋体", settings.HeaderTextStyleName,
                "空表头文字样式应回退默认值");
            Near(5.0, settings.HorizontalGridInterval, 1e-9,
                "当前水平网格间距应固化为默认值");
            Equal("CDBox-纵断面", settings.LayerName,
                "当前绘图图层应固化为默认值");
            Equal(8, settings.Rows.Count,
                "纵断面设置应始终保留八个数据栏");
            Equal("PipeFoundation", settings.Rows[6].Key,
                "纵断面应包含管道基础栏");
            True(settings.Rows.TrueForAll(x => x.TextStyleName == "宋体"),
                "当前数据栏文字样式应固化为默认值");

            var subsetSettings = new LongitudinalProfileSettings
            {
                HorizontalScale = 25,
                VerticalScale = 25,
                HeaderTextHeight = 99,
                HeaderTextStyleName = "HZ",
                HeaderTextColorIndex = 3,
                Rows = new List<LongitudinalProfileRowSettings>
                {
                    new LongitudinalProfileRowSettings
                    {
                        Key = "WellNumber", Height = 10,
                        TextHeight = 2.5, TextStyleName = "HZ",
                        TextColorIndex = 3
                    },
                    new LongitudinalProfileRowSettings
                    {
                        Key = "GroundElevation", Height = 15,
                        TextHeight = 2.5, TextStyleName = "HZ",
                        TextColorIndex = 4
                    },
                    new LongitudinalProfileRowSettings
                    {
                        Key = "WellNumber", Height = 12,
                        TextHeight = 2.0, TextStyleName = "Standard",
                        TextColorIndex = 5
                    }
                }
            };
            subsetSettings.Normalize();
            Near(1000, subsetSettings.HorizontalScale, 1e-9,
                "横向比例应固定为内置默认值");
            Near(100, subsetSettings.VerticalScale, 1e-9,
                "纵向比例应固定为内置默认值");
            Near(99, subsetSettings.HeaderTextHeight, 1e-9,
                "表头文字高度应保留用户设置");
            Equal("HZ", subsetSettings.HeaderTextStyleName,
                "表头文字样式应保留用户设置");
            Equal((short)3, subsetSettings.HeaderTextColorIndex,
                "表头文字颜色应保留用户设置");
            Equal(3, subsetSettings.Rows.Count,
                "删除后的栏目子集和重复栏目均应保持");
            Equal("WellNumber", subsetSettings.Rows[0].Key,
                "栏目拖动后的顺序应保持");
            Equal((short)3, subsetSettings.Rows[0].TextColorIndex,
                "栏目文本颜色应归一化并保留");
            Equal("WellNumber", subsetSettings.Rows[2].Key,
                "同一种栏类型应允许重复添加");

            LongitudinalProfileLayout layout =
                LongitudinalProfileLayoutCalculator.Calculate(
                    result.Profile, settings);
            Near(0.5, layout.HorizontalFactor, 1e-9,
                "横向1:1000应按参考图换算为0.5倍");
            Near(5.0, layout.VerticalFactor, 1e-9,
                "纵向1:100应按参考图换算为5倍");
            Near(22.5, layout.HeaderRight, 1e-9,
                "参考图表头宽度应为设置值的一半");
            Near(42.5, layout.TableTop, 1e-9,
                "八行参考表格总高");
            Near(35.0, layout.Row("GroundElevation").Bottom, 1e-9,
                "自然地面标高栏应按参考图缩放并位于表格顶部");
            Near(0.0, layout.Row("WellNumber").Bottom, 1e-9,
                "井编号栏底部应与插入点对齐");
            Near(43.295, layout.DataRight, 1e-9,
                "数据表应结束于最后一个井节点");
            Near(45.0, layout.PlotRight, 1e-9,
                "图面应按水平网格间距向右补齐");

            string standalone =
                CDBoxStudioLongitudinalProfileSettingsPage
                    .BuildStandaloneDocument(new CDBoxStudioSettings());
            string embedded =
                CDBoxStudioLongitudinalProfileSettingsPage
                    .BuildEmbeddedSection(new CDBoxStudioSettings());
            Contains(standalone, "纵断面设置",
                "独立纵断面设置页应使用统一WebView2页面");
            Contains(standalone, "自然地面标高",
                "设置页应包含纵断面数据栏");
            Contains(standalone, "坐标网格",
                "设置页应包含显示样式");
            False(standalone.Contains(">横向比例<"),
                "横向比例不应继续显示为设置项");
            False(standalone.Contains(">纵向比例<"),
                "纵向比例不应继续显示为设置项");
            Contains(standalone, "openLongitudinalProfileColorPicker",
                "所有颜色项应调用CDBox颜色选择器");
            Contains(standalone, "id=\"headerTextHeight\"",
                "表头文字高度应恢复为设置项");
            Contains(standalone, "id=\"headerTextStyle\"",
                "表头文字样式应恢复为选择控件");
            Contains(standalone, "id=\"headerTextColor\"",
                "表头文字颜色应恢复为CDBox颜色控件");
            Contains(standalone, "getLongitudinalProfileTextStyles",
                "文字样式应读取当前CAD图纸样式");
            Contains(standalone,
                "class=\"drag-handle\" draggable=\"true\"",
                "纵断面栏目应仅通过拖拽柄排序");
            False(standalone.Contains("tr.draggable=true"),
                "纵断面栏目整行不得触发拖动");
            Contains(standalone,
                "<select data-key=\"TextStyleName\">",
                "栏目文字样式应使用选择控件");
            False(standalone.Contains("used.indexOf(item.Key)"),
                "栏类型不应因已有同类栏目而锁定");
            Contains(standalone, "拖动排序",
                "纵断面栏目应支持拖动排序");
            Contains(standalone,
                "class=\"icon-btn danger\" data-delete-row",
                "纵断面栏目删除应复用结构层的×图标按钮");
            False(standalone.Contains(">删除</button>"),
                "纵断面栏目不应保留文字删除按钮");
            Contains(embedded, "longitudinalProfileSettingsFrame",
                "内嵌页应复用同一纵断面设置文档");
            False(standalone.Contains("预览框"),
                "纵断面设置页不应保留预览框");
            False(standalone.Contains("文件导入"),
                "纵断面设置页不应保留文件导入");
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
            Equal("9.200m", PipeLengthAnnotationTextComposer.FormatWithDecimals(9.2, 3, system),
                "标注精度设置应覆盖旧标注的小数位");
            Equal("9.2m", PipeLengthAnnotationTextComposer.FormatWithDecimals(9.2, 1, system),
                "刷新标注时应采用当前精度设置");

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
            Equal("开挖：长9.20m、宽1.50m、高2.00m",
                PipeLengthAnnotationTextComposer.ReplaceDerivedLengthToken(
                    "开挖：长12.50m、宽1.50m、高2.00m", "12.50m", "9.20m"),
                "下侧注记中的派生长度应随源管线长度刷新");
            Equal("开挖：长9.20m、宽12.50m、高2.00m",
                PipeLengthAnnotationTextComposer.ReplaceDerivedLengthToken(
                    "开挖：长12.50m、宽12.50m、高2.00m", "12.50", "9.20"),
                "长度与宽度数值相同时只应更新长度字段");
            Equal("宽12.50m；长9.20m",
                PipeLengthAnnotationTextComposer.ReplaceDerivedLengthToken(
                    "宽12.50m；长12.50m", "12.50", "9.20"),
                "自定义下侧文字中长度字段不在首位时也应准确更新");

            List<string> bottomLines = PipeLengthAnnotationTextComposer
                .SplitBottomLines("第一行\r\n第二行\\P第三行");
            Equal(3, bottomLines.Count, "下侧注记应按换行拆为多个单行文字");
            Equal("第二行", bottomLines[1], "下侧注记换行内容应保持顺序");
        }

        private static void TestSpecialObjectsSkipQualityCheck()
        {
            True(QuantityDashboardClassification.ShouldIncludeInQualityCheck(null),
                "缺少属性时仍应进入数据质量检查");
            True(QuantityDashboardClassification.ShouldIncludeInQualityCheck(
                new QuantityPipeAttributes()), "普通属性对象应进入数据质量检查");
            False(QuantityDashboardClassification.ShouldIncludeInQualityCheck(
                new QuantityPipeAttributes { IsSpecialObject = true }),
                "已勾选特殊对象的属性对象不应进入数据质量检查");
        }

        private static void TestNodeAnnotationTextComposition()
        {
            Dictionary<string, string> normal = NodeAnnotationTextComposer.Compose(
                "J12", 2.345, 1.2, false);
            Equal("J12", normal["NodeNo"], "节点编号应进入绑定文字");
            Equal("井深:2.35m", normal["WellDepth"], "井深应固定保留两位小数");
            Equal("井筒:1.20m", normal["ShaftLength"], "井筒应固定保留两位小数");
            False(normal.ContainsKey("WellType"), "普通检查井不应增加沉泥井文字行");

            Dictionary<string, string> silt = NodeAnnotationTextComposer.Compose(
                string.Empty, 3.0, 2.0, true);
            Equal("未编号", silt["NodeNo"], "空节点编号应使用统一占位文字");
            Equal("沉泥井", silt["WellType"], "沉泥井绑定应包含井类型文字行");
        }

        private static void TestBuiltInUpdateSourcePriority()
        {
            IList<CDBoxStudioUpdateSource> sources = CDBoxStudioUpdateSourceCatalog.CreateManifestSources();
            Equal(3, sources.Count, "应固定提供三个 update.json 更新源");
            Equal("Gitee", sources[0].Name, "Gitee 应为第一更新源");
            Equal(CDBoxStudioUpdateSourceCatalog.GiteeManifestUrl, sources[0].Url, "Gitee 地址");
            Equal("GitCode", sources[1].Name, "GitCode 应为第二更新源");
            Equal(CDBoxStudioUpdateSourceCatalog.GitCodeManifestUrl, sources[1].Url, "GitCode 地址");
            Equal("GitHub", sources[2].Name, "GitHub 应为第三更新源");
            Equal(CDBoxStudioUpdateSourceCatalog.GitHubManifestUrl, sources[2].Url, "GitHub 地址");
            for (int i = 0; i < sources.Count; i++) True(sources[i].Enabled, "内置更新源必须启用");
        }

        private static void TestColorPickerIntegration()
        {
            byte red;
            byte green;
            byte blue;
            CDBoxColorConverter.AciToRgb(1, out red, out green, out blue);
            Equal((byte)255, red, "ACI 1 红色分量");
            Equal((byte)0, green, "ACI 1 绿色分量");
            Equal((byte)0, blue, "ACI 1 蓝色分量");
            CDBoxColorConverter.AciToRgb(12, out red, out green, out blue);
            Equal((byte)204, red, "ACI 12 红色分量应匹配 AutoCAD 标准色表");
            Equal((byte)0, green, "ACI 12 绿色分量应匹配 AutoCAD 标准色表");
            Equal((byte)0, blue, "ACI 12 蓝色分量应匹配 AutoCAD 标准色表");
            Equal(1, CDBoxColorConverter.RgbToNearestAci(255, 0, 0), "纯红色应映射为 ACI 1");
            True(CDBoxColorConverter.TryParseHex("#0A80FF", out red, out green, out blue), "HEX 应可解析");
            Equal((byte)10, red, "HEX 红色分量");
            Equal((byte)128, green, "HEX 绿色分量");
            Equal((byte)255, blue, "HEX 蓝色分量");

            CDBoxColor preserved = CDBoxColorOutputResolver.Resolve(
                CDBoxColor.FromRgb(250, 20, 20), CDBoxColor.FromIndex(3),
                CDBoxColorOutputMode.PreserveOriginalType);
            Equal(CDBoxColorType.IndexColor, preserved.Type, "保持原类型应继续写入 ACI");
            CDBoxColor trueColor = CDBoxColorOutputResolver.Resolve(
                CDBoxColor.FromIndex(1), CDBoxColor.FromIndex(3),
                CDBoxColorOutputMode.PreferTrueColor);
            Equal(CDBoxColorType.TrueColor, trueColor.Type, "优先真彩色应转换颜色类型");
            CDBoxColor standard = CDBoxColorOutputResolver.Resolve(
                CDBoxColor.FromRgb(239, 68, 68), CDBoxColor.FromIndex(7),
                CDBoxColorOutputMode.CDBoxStandard);
            Equal(CDBoxColorType.CDBoxStandard, standard.Type, "标准模式应映射到 CDBox 标准色");
            Equal("错误对象", standard.DisplayName, "标准色应选择最近的工程语义颜色");

            string annotationScript = CDBoxStudioAnnotationSettingsPage.BuildComponentScript();
            Contains(annotationScript, "data-color-picker", "标注设置应渲染统一颜色选择按钮");
            Contains(annotationScript, "openAnnotationColorPicker", "标注设置应打开统一颜色选择器");
            Contains(annotationScript, "CDBoxAnnotationColorSelected", "标注设置应接收颜色选择结果");
            False(annotationScript.Contains("<select class=\"as-select\" data-color-select"), "旧颜色下拉框应被替换");

            string layerScript = CDBoxStudioLayerManagerPage.BuildComponentScript();
            Contains(layerScript, "data-layer-color", "图层颜色应成为可点击入口");
            Contains(layerScript, "openLayerColorPicker", "图层管理器应打开统一颜色选择器");
            Contains(CDBoxStudioLayerManagerPage.BuildStyles(false), "lm-color-button", "图层颜色按钮样式应存在");

            string pickerPage = CDBoxStudioColorPickerPage.BuildStandaloneDocument(
                new CDBoxStudioSettings(), CDBoxColor.FromIndex(7),
                new CDBoxStudioColorPickerOptions
                {
                    AllowByLayer = true,
                    AllowByBlock = true,
                    AllowTrueColor = true,
                    AllowColorBook = true,
                    AllowStandard = true
                });
            Contains(pickerPage, "confirmColorPicker", "颜色选择器应通过统一 WebView2 路由确认");
            Contains(pickerPage, "browseCadColorBook", "颜色选择器应保留 AutoCAD 配色系统入口");
            Contains(pickerPage, "CDBoxColorPickerFromCad", "颜色选择器应接收原生色册选择结果");
            Contains(pickerPage, "data-theme=", "颜色选择器应使用 Studio 主题");
        }

        private static void TestStructuredLayerRecognition()
        {
            var rules = new List<LayerRecognitionRule>
            {
                new LayerRecognitionRule
                {
                    Name = "主管常用管径",
                    Priority = 200,
                    MatchMode = LayerRecognitionEngine.MatchModeKeywords,
                    Pattern = "300,波纹",
                    ParentGroup = "主管",
                    StopAfterMatch = true
                },
                new LayerRecognitionRule
                {
                    Name = "支管常用管径",
                    Priority = 200,
                    MatchMode = "通配符",
                    Pattern = "*110PVC*",
                    ParentGroup = "支管",
                    StopAfterMatch = true
                }
            };

            LayerRecognitionResult main = LayerRecognitionEngine.Recognize("300波纹管（砼恢复）", rules);
            Equal("主管", main.Metadata.ParentGroup, "300 波纹管规则应识别主管");
            Equal("DN300", main.Attributes.Specification, "应识别无 DN 前缀管径");
            Equal("波纹管", main.Attributes.Material, "波纹简称应归一为波纹管");
            Equal("混凝土恢复", main.Attributes.ConstructionType, "砼恢复应归一为混凝土恢复");
            True(main.Confidence >= 0.8, "完整管线识别应达到较高置信度");

            LayerRecognitionResult reordered = LayerRecognitionEngine.Recognize("波纹管300砼恢复", rules);
            Equal("DN300", reordered.Attributes.Specification, "材料在前时也应识别管径");
            Equal("主管", reordered.Metadata.ParentGroup, "材料在前的常见旧名称应保持主管兼容归属");

            LayerRecognitionResult standard = LayerRecognitionEngine.Recognize(
                "主管-DN300-波纹管-混凝土恢复", rules);
            Equal("主管", standard.Metadata.ParentGroup, "标准名应识别父属性");
            Equal("管线", standard.Attributes.ObjectType, "标准名应识别对象类型");
            Equal(LayerRecognitionStatuses.Standard, standard.Status, "规范连字符名称应判定为标准");

            LayerRecognitionResult branch = LayerRecognitionEngine.Recognize("支管110PVC明管", rules);
            Equal("支管", branch.Metadata.ParentGroup, "支管名称应识别父属性");
            Equal("DN110", branch.Attributes.Specification, "支管应识别管径");
            Equal("PVC", branch.Attributes.Material, "支管应识别材料");
            Equal("明管", branch.Attributes.ConstructionType, "支管应识别施工方式");

            LayerRecognitionResult well = LayerRecognitionEngine.Recognize("700铸铁井盖", rules);
            Equal("井", well.Metadata.ParentGroup, "井盖应归入井");
            Equal("D700", well.Attributes.Specification, "井盖应按井径而非管径提取");
            Equal(LayerRecognitionStatuses.Incomplete, well.Status, "缺少井型时应提示信息不完整");
            Contains(well.Explanation, "井型", "识别解释应指出缺少井型");

            LayerRecognitionResult node = LayerRecognitionEngine.Recognize("井-沉泥井-D700-砖砌-铸铁盖", rules);
            Equal("沉泥井", node.Attributes.NodeType, "应识别沉泥井");
            Equal("D700", node.Attributes.Specification, "标准井名应识别 D 规格");

            LayerRecognitionResult structure = LayerRecognitionEngine.Recognize("C25混凝土(15cm)", rules);
            Equal("结构层", structure.Metadata.ParentGroup, "混凝土厚度名应归入结构层");
            Equal("C25", structure.Attributes.StrengthGrade, "应识别混凝土强度");
            Equal("T150", structure.Attributes.Thickness, "15cm 应换算为 T150");

            LayerRecognitionResult facility = LayerRecognitionEngine.Recognize("隔油池2m³", rules);
            Equal("构筑物", facility.Metadata.ParentGroup, "隔油池应归入构筑物");
            Equal("隔油池", facility.Metadata.ParentClass, "隔油池类型应作为分类");
            Equal("V2m³", facility.Attributes.Volume, "应识别构筑物容积");

            LayerRecognitionResult annotation = LayerRecognitionEngine.Recognize("ZJ", rules);
            Equal("注记", annotation.Metadata.ParentGroup, "ZJ 应归入注记");
            Equal("通用注记", annotation.Metadata.ParentClass, "ZJ 应识别为通用注记");

            LayerRecognitionResult mainAnnotation = LayerRecognitionEngine.Recognize("主管注记", rules);
            Equal("注记", mainAnnotation.Metadata.ParentGroup, "主管注记不得误归入主管");
            Equal("管线长度注记", mainAnnotation.Metadata.ParentClass, "主管注记应识别为管线长度注记");

            LayerRecognitionResult branchAnnotation = LayerRecognitionEngine.Recognize("支管注记", rules);
            Equal("注记", branchAnnotation.Metadata.ParentGroup, "支管注记不得误归入支管");
            Equal("管线长度注记", branchAnnotation.Metadata.ParentClass, "支管注记应识别为管线长度注记");

            LayerRecognitionResult point = LayerRecognitionEngine.Recognize("测点代码", rules);
            Equal("测点", point.Metadata.ParentGroup, "测点代码应归入测点");
            Equal("代码", point.Attributes.Purpose, "测点代码应识别用途");

            LayerRecognitionResult unknown = LayerRecognitionEngine.Recognize("新建图层1", rules);
            Equal(LayerRecognitionStatuses.Unrecognized, unknown.Status, "无语义的新建图层应保持未识别");
            True(unknown.NeedsConfirmation, "未识别结果必须等待人工确认");

            LayerRecognitionResult excluded = LayerRecognitionEngine.Recognize(
                "300波纹管废弃",
                new[]
                {
                    new LayerRecognitionRule
                    {
                        MatchMode = LayerRecognitionEngine.MatchModeKeywords,
                        Pattern = "300,波纹",
                        ExcludePattern = "废弃",
                        ParentGroup = "主管"
                    }
                });
            Equal(0, excluded.MatchedRules.Count, "排除关键词应阻止规则命中");

            LayerRecognitionResult templated = LayerRecognitionEngine.Recognize(
                "主管-DN450-HDPE-顶管",
                new[]
                {
                    new LayerRecognitionRule
                    {
                        Name = "标准管线模板",
                        MatchMode = LayerRecognitionEngine.MatchModeTemplate,
                        Pattern = "主管-{DN}-{Material}-*",
                        ParentGroup = "主管"
                    }
                });
            Equal(1, templated.MatchedRules.Count, "模板规则应识别结构化名称");
            Equal("主管", templated.Metadata.ParentGroup, "模板规则输出应生效");
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
            List<QuantityStructureLayer> typedLayers = QuantityStructureLayer.Parse(
                "中粗砂回填 0.80 管线层\n中粗砂垫层 0.15 锁定 垫层");
            True(typedLayers[0].IsPipeLayer, "显式管线层类型应正确解析");
            True(typedLayers[1].IsCushionLayer, "显式垫层类型应正确解析");
            False(typedLayers[1].IsPipeLayer, "垫层与管线层类型应互斥");
            Near(0.15, QuantityStructureLayer.ResolvePipeCushionHeight(typedLayers, 9.0), 1e-9,
                "显式垫层应决定管线端点深度");
            True(QuantityStructureLayer.Serialize(typedLayers, false).IndexOf("垫层", StringComparison.Ordinal) >= 0,
                "管线结构层应持久化垫层类型");
            QuantityStructureLayer legacyBelowWell = QuantityStructureLayer.Parse("碎石垫层 0.10 锁定 井下层")[0];
            True(legacyBelowWell.IsBelowWellLayer && legacyBelowWell.IsCushionLayer,
                "旧版井下层应兼容迁移为垫层");
            QuantityStructureLayer legacyPipeCushion = QuantityStructureLayer.Parse("中粗砂垫层 0.15 锁定 管线层")[0];
            True(legacyPipeCushion.IsCushionLayer && !legacyPipeCushion.IsPipeLayer,
                "旧版管线下垫层应从管线层迁移为垫层");
            string serializedNodeLayers = QuantityStructureLayer.Serialize(
                new[] { legacyBelowWell }, true);
            True(serializedNodeLayers.IndexOf("垫层", StringComparison.Ordinal) >= 0,
                "井底结构层应统一保存为垫层");
            False(serializedNodeLayers.IndexOf("井下层", StringComparison.Ordinal) >= 0,
                "新数据不应继续写入旧井下层名称");
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

            QuantityPipeAttributes precisionPipe = QuantityPipeAttributes.DefaultMainPipe;
            precisionPipe.StartDepth = 1.234;
            precisionPipe.EndDepth = 1.238;
            precisionPipe.BackfillStructure = "中粗砂回填 0.00\n中粗砂垫层 0.10 锁定 垫层";
            QuantityDependencyResult precisionResult = QuantityDependencyService.NormalizeDraft(
                precisionPipe,
                QuantityStructureLayer.Parse(precisionPipe.BackfillStructure),
                precisionPipe,
                null,
                null,
                null,
                "StartDepth");
            Near(1.234, precisionResult.Attributes.StartDepth, 1e-9, "端点实际深度不应因界面精度而丢失");
            Near(1.238, precisionResult.Attributes.EndDepth, 1e-9, "端点实际深度不应因界面精度而丢失");
            Near(1.24, precisionResult.Attributes.AverageDepth, 1e-9, "属性编辑器平均深度应保留两位小数");
            Near(1.14, precisionResult.Layers.Find(x => QuantityStructureLayer.IsSandBackfill(x)).Height, 1e-9,
                "自动管线层应使用保留两位后的平均深度");

            QuantityPipeAttributes typedWell = QuantityPipeAttributes.DefaultNodeWell;
            typedWell.WellDepth = 0.46;
            typedWell.BackfillStructure =
                "井内一般层 0.20 锁定\n井内管线层 0.10 锁定 管线层\n井底垫层 0.15 锁定 垫层";
            QuantityDependencyResult typedWellResult = QuantityDependencyService.NormalizeDraft(
                typedWell,
                QuantityStructureLayer.Parse(typedWell.BackfillStructure),
                typedWell,
                null,
                null,
                null,
                "WellDepth");
            Near(0.61, typedWellResult.RealExcavationDepth, 1e-9,
                "真实井深只应叠加井的垫层，不应叠加管线层");

            QuantityPipeAttributes diameter700Well = QuantityPipeAttributes.DefaultNodeWell;
            diameter700Well.WellSpec = "φ700";
            QuantityDependencyResult diameter700Result = QuantityDependencyService.NormalizeDraft(
                diameter700Well,
                QuantityStructureLayer.Parse(diameter700Well.BackfillStructure),
                QuantityPipeAttributes.DefaultNodeWell,
                null,
                null,
                null,
                "WellSpec");
            Near(1.5, diameter700Result.Attributes.ExcavationLength, 1e-9,
                "700 直径井应自动使用 1.5m 开挖长度");
            Near(1.5, diameter700Result.Attributes.ExcavationWidth, 1e-9,
                "700 直径井应自动使用 1.5m 开挖宽度");

            QuantityPipeAttributes diameter500Well = QuantityPipeAttributes.DefaultNodeWell;
            QuantityDependencyResult diameter500Result = QuantityDependencyService.NormalizeDraft(
                diameter500Well,
                QuantityStructureLayer.Parse(diameter500Well.BackfillStructure),
                diameter500Well,
                null,
                null,
                null,
                "WellSpec");
            Near(1.3, diameter500Result.Attributes.ExcavationLength, 1e-9,
                "500 直径井的开挖长度默认值应保持不变");
            Near(1.3, diameter500Result.Attributes.ExcavationWidth, 1e-9,
                "500 直径井的开挖宽度默认值应保持不变");

            QuantityPipeAttributes changedBackTo500 = QuantityPipeAttributes.DefaultNodeWell;
            changedBackTo500.WellSpec = "φ500";
            changedBackTo500.ExcavationLength = 1.5;
            changedBackTo500.ExcavationWidth = 1.5;
            QuantityDependencyResult changedBackTo500Result = QuantityDependencyService.NormalizeDraft(
                changedBackTo500,
                QuantityStructureLayer.Parse(changedBackTo500.BackfillStructure),
                changedBackTo500,
                null,
                null,
                null,
                "WellSpec");
            Near(1.3, changedBackTo500Result.Attributes.ExcavationLength, 1e-9,
                "井规格从 700 改回 500 时应恢复原 1.3m 默认长度");
            Near(1.3, changedBackTo500Result.Attributes.ExcavationWidth, 1e-9,
                "井规格从 700 改回 500 时应恢复原 1.3m 默认宽度");

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

            QuantityPipeAttributes undersizedPipeLayer = QuantityPipeAttributes.DefaultMainPipe;
            undersizedPipeLayer.StartNode = "W1";
            undersizedPipeLayer.EndNode = "W2";
            undersizedPipeLayer.StartDepth = 1.00;
            undersizedPipeLayer.EndDepth = 1.00;
            undersizedPipeLayer.PipeOuterDiameter = 0.30;
            List<QuantityStructureLayer> undersizedLayers = QuantityStructureLayer.Parse(
                "中粗砂回填 0.10 管线层\n中粗砂垫层 0.10 锁定 垫层");
            QuantityDependencyResult undersizedResult = QuantityDependencyService.NormalizeDraft(
                undersizedPipeLayer,
                undersizedLayers,
                undersizedPipeLayer,
                null,
                null,
                null,
                "StructureLayers");
            True(undersizedResult.Warnings.Exists(x => x.IndexOf("小于管道外径",
                StringComparison.Ordinal) >= 0), "主管管线层输入高度小于外径时必须返回明确提示");

            QuantityPipeAttributes special = QuantityPipeAttributes.DefaultMainPipe;
            special.IsSpecialObject = true;
            special.StartDepth = 9.0;
            special.EndDepth = 8.0;
            special.AverageDepth = 0.256;
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
            Near(0.26, specialResult.Attributes.AverageDepth, 1e-9, "特殊对象平均深度应按用户定义值保留两位");
            Near(0.26, specialResult.Layers[0].Height, 1e-9, "特殊对象结构层应从两位小数平均深度派生");
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
            True(settings.ShouldPromptForInstall(false, "release:30101"), "首次加载且未安装时应提示安装");
            False(settings.ShouldPromptForInstall(true, "release:30101"), "自动加载注册有效时不应重复提示");
            settings.PromptInstallOnLoad = false;
            settings.LastInstallPromptIdentity = "release:30101";
            False(settings.ShouldPromptForInstall(false, "release:30101"), "当前版本选择不再提示后应保持静默");
            True(settings.ShouldPromptForInstall(false, "release:30102"), "升级后的首次加载应重新提供一次安装修复提示");
        }

        private static void TestRealEstateModuleBoundary()
        {
            var logger = new CapturingLogger();
            var pages = new CapturingPageService();
            var services = new CDBoxServiceRegistry()
                .Register<ICDBoxLogger>(logger)
                .Register<ICDBoxPageService>(pages);
            var module = new RealEstateModule();

            module.Initialize(services);
            Equal("realestate", module.Id, "不动产模块 Id");
            Equal("CDBox 不动产", module.Name, "不动产模块名称");
            module.OpenWorkspace();

            True(pages.LastPage != null, "不动产必须通过统一页面服务打开工作区");
            Equal("realestate-home", pages.LastPage.Id, "不动产起始页 Id");
            string html = pages.LastPage.HtmlFactory();
            Contains(html, "CDBox 不动产", "不动产起始页标题");
            Contains(html, "studio|ready|realestate", "不动产页面应使用兼容 Studio 路由");
            True(pages.LastPage.RouteHandler(
                new CDBoxPageRouteRequest("ready", "realestate")).Handled,
                "不动产页面应处理 ready 路由");
            False(pages.LastPage.RouteHandler(
                new CDBoxPageRouteRequest("unknown", string.Empty)).Handled,
                "不动产页面不得吞掉未知路由");
            True(logger.InfoCount >= 2, "不动产初始化与打开工作区应写入统一日志");

            module.Shutdown();
            Exception afterShutdown = Capture(module.OpenWorkspace);
            True(afterShutdown is InvalidOperationException,
                "关闭后的不动产模块不得继续打开工作区");
            True(Capture(delegate
                {
                    new CDBoxServiceRegistry()
                        .GetRequired<ICDBoxPageService>();
                }) is InvalidOperationException,
                "缺失的公共服务必须明确失败");

            CDBoxModuleLoadResult loaded =
                CDBoxKnownModuleLoader.LoadAndInitialize(
                    typeof(RealEstateModule).Assembly.Location,
                    typeof(RealEstateModule).FullName,
                    services);
            True(loaded.Success, "固定路径加载器应能初始化 RealEstate");
            loaded.Module.Shutdown();

            CDBoxModuleLoadResult initializationFailure =
                CDBoxKnownModuleLoader.LoadAndInitialize(
                    typeof(FailingWorkspaceModule).Assembly.Location,
                    typeof(FailingWorkspaceModule).FullName,
                    services);
            False(initializationFailure.Success,
                "模块初始化异常必须被限制在加载边界内");
            True(initializationFailure.Error is InvalidOperationException,
                "模块初始化异常应保留原始错误原因");

            CDBoxModuleLoadResult missing =
                CDBoxKnownModuleLoader.LoadAndInitialize(
                    Path.Combine(Path.GetTempPath(),
                        Guid.NewGuid().ToString("N"),
                        "CDBox.RealEstate.dll"),
                    typeof(RealEstateModule).FullName,
                    services);
            False(missing.Success, "RealEstate DLL 缺失时加载器必须安全失败");
            True(missing.Error is FileNotFoundException,
                "RealEstate DLL 缺失时应保留明确错误原因");
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
            True(standalone.IndexOf("导出计算过程", StringComparison.Ordinal) >= 0, "看板应提供工程量计算过程导出按钮");
            True(standalone.IndexOf("exportQuantityCalculationProcess", StringComparison.Ordinal) >= 0, "计算过程按钮应调用专用导出路由");
        }

        private static void TestQuantityCalculationProcessExport()
        {
            string root = NewTemporaryDirectory("quantity-audit");
            try
            {
                var snapshot = new QuantityDashboardSnapshot();
                snapshot.document.name = "审计测试.dwg";
                snapshot.scope.regionName = "整张图纸";
                snapshot.status.updatedAt = "2026-07-20 12:00:00";
                snapshot.referenceItems.Add(new QuantityDashboardReferenceItem { item = "机械开挖", quantity = 1.23456789, unit = "m³", source = QuantityDashboardSources.Property });
                var pipe = new QuantityMainPipeCalculationRow
                {
                    Index = 1,
                    HandleText = "A1",
                    LayerName = "W-PIPE",
                    StartNode = "W1",
                    EndNode = "W2",
                    Length = 12.3456789,
                    MechanicalExcavation = 1.23456789,
                    DataStatus = "正常"
                };
                pipe.CalculationSteps.Add(new QuantityCalculationStep
                {
                    ItemName = "机械开挖",
                    Formula = "长度 × 宽度 × 深度",
                    Substitution = "12.3456789 × 0.8 × 1.2",
                    Result = 1.23456789,
                    Unit = "m³"
                });
                snapshot.calculationAudit.mainPipes.Add(pipe);

                string path = Path.Combine(root, "计算过程.xlsx");
                QuantityDashboardExportService.ExportCalculationProcess(path, snapshot);
                True(File.Exists(path) && new FileInfo(path).Length > 0, "计算过程工作簿应成功生成");
                using (ZipArchive archive = ZipFile.OpenRead(path))
                {
                    True(archive.GetEntry("xl/workbook.xml") != null, "导出文件应为有效 xlsx 工作簿");
                    True(archive.GetEntry("xl/worksheets/sheet4.xml") != null, "导出文件应包含逐项计算过程工作表");
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void TestQuantityAttributeEditorSharedPage()
        {
            string embedded = CDBoxStudioQuantityAttributeEditorPage.BuildEmbeddedSection();
            string standalone = CDBoxStudioQuantityAttributeEditorPage.BuildStandaloneDocument(
                new CDBoxStudioSettings { Theme = "dark", AnimationsEnabled = false }, "test.log", "drawing.dwg", "A1");

            True(embedded.IndexOf("quantityAttributeEditorPage", StringComparison.Ordinal) >= 0, "内嵌属性编辑器应提供共享根节点");
            True(standalone.IndexOf("CDBoxQuantityAttributeEditorPage.create", StringComparison.Ordinal) >= 0, "独立窗口应创建同一共享组件");
            True(standalone.IndexOf("standalone:true", StringComparison.Ordinal) >= 0, "独立属性编辑器应启用独立模式");
            True(standalone.IndexOf("3.6.2", StringComparison.Ordinal) >= 0, "页面应显示 3.6.2 身份");
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
            True(standalone.IndexOf("function fmt2", StringComparison.Ordinal) >= 0, "平均深度与结构层应提供两位小数格式化");
            True(standalone.IndexOf("function round2", StringComparison.Ordinal) >= 0
                && standalone.IndexOf("+1e-9", StringComparison.Ordinal) >= 0,
                "两位小数显示应先修正浮点尾差再四舍五入");
            True(standalone.IndexOf("fmt2(c.cadLength", StringComparison.Ordinal) >= 0
                && standalone.IndexOf("fmt2(c.realExcavationDepth", StringComparison.Ordinal) >= 0,
                "所有固定两位数值显示应复用统一四舍五入格式");
            True(standalone.IndexOf("data-layer=\"role\"", StringComparison.Ordinal) >= 0, "结构层应使用统一层类型选择项");
            True(standalone.IndexOf(">一般层</option>", StringComparison.Ordinal) >= 0, "层类型应包含一般层");
            True(standalone.IndexOf(">垫层</option>", StringComparison.Ordinal) >= 0, "层类型应包含垫层");
            True(standalone.IndexOf("e.isComposing||self.composing", StringComparison.Ordinal) >= 0, "拼音合成期间不应提交结构层名称");
            True(standalone.IndexOf("getAttribute('data-layer')==='name')return", StringComparison.Ordinal) >= 0,
                "结构层名称输入完成前不应触发草稿重绘");
            True(standalone.IndexOf("class=\"icon-btn danger\" data-act=\"delete-layer\"", StringComparison.Ordinal) >= 0,
                "属性编辑器结构层删除应复用×图标按钮");
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
            Contains(annotationScript, "surface.calculationMode", "表面积设置应提供计算模式选择");
            Contains(annotationScript, "label:'表面积标注'", "计算设置应提供表面积标注");
            Contains(annotationScript, "label:'面积标注'", "计算设置应提供面积标注");
            False(annotationScript.IndexOf("调用 CASS surfacearea 计算（固定）", StringComparison.Ordinal) >= 0, "计算设置不应继续显示为固定模式");
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
            True(script.IndexOf("selectRange", StringComparison.Ordinal) >= 0, "图层表应支持 Shift 连续多选");
            True(script.IndexOf("ev.shiftKey", StringComparison.Ordinal) >= 0, "图层表应识别 Shift 多选手势");
            True(script.IndexOf("bindMarqueeSelection", StringComparison.Ordinal) >= 0, "图层表应绑定框选交互");
            True(script.IndexOf("lm-selection-box", StringComparison.Ordinal) >= 0, "图层表应生成框选区域");
            True(script.IndexOf("recognitionFilter", StringComparison.Ordinal) >= 0, "图层表应支持识别状态筛选");
            True(script.IndexOf("recognizedParent", StringComparison.Ordinal) >= 0, "未写入属性时树应使用识别建议预览");
            True(script.IndexOf("confidencePercent", StringComparison.Ordinal) >= 0, "图层表应展示识别置信度");
            True(script.IndexOf(">应用识别结果</button>", StringComparison.Ordinal) >= 0, "识别写入按钮应明确为应用结果");
            True(script.IndexOf(">图层预设</button>", StringComparison.Ordinal) >= 0, "图层管理器应提供多套预设入口");
            True(script.IndexOf("openManualPresetDialog", StringComparison.Ordinal) >= 0, "图层预设应支持手工新增");
            True(script.IndexOf("openDrawingPresetDialog", StringComparison.Ordinal) >= 0, "图层预设应支持保存当前图纸图层");
            True(script.IndexOf("deleteLayerPreset", StringComparison.Ordinal) >= 0, "图层预设应支持删除自定义项");
            True(styles.IndexOf(".lm-preset-form", StringComparison.Ordinal) >= 0, "图层预设应使用统一 WebView2 样式");
            True(styles.IndexOf(".lm-recognition-status", StringComparison.Ordinal) >= 0, "识别状态样式应存在");
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
            True(script.IndexOf("层类型", StringComparison.Ordinal) >= 0, "默认表结构层应显示统一层类型列");
            True(script.IndexOf(">一般层</option>", StringComparison.Ordinal) >= 0, "默认表层类型应包含一般层");
            True(script.IndexOf(">垫层</option>", StringComparison.Ordinal) >= 0, "默认表层类型应包含垫层");
            False(script.IndexOf(">井下层</option>", StringComparison.Ordinal) >= 0, "默认表不应继续显示旧井下层选项");
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
            True(script.IndexOf("function pipeDiameter", StringComparison.Ordinal) >= 0, "管径文字应能反推外径");
            True(script.IndexOf("pk==='PipeText'", StringComparison.Ordinal) >= 0, "修改管径文字时应同步外径输入框");
            True(script.IndexOf("translate(450 325) scale(${this.zoom}) translate(-450 -325)", StringComparison.Ordinal) >= 0, "预览缩放应围绕视框中心");
            True(script.IndexOf("class='sd-drag' draggable='true'", StringComparison.Ordinal) >= 0, "断面表格应使用独立拖拽柄");
            True(script.IndexOf("class='icon-btn danger' data-act='deleteLayer'", StringComparison.Ordinal) >= 0,
                "断面结构层删除应复用×图标按钮");
            True(script.IndexOf("class='icon-btn danger' data-act='deletePipe'", StringComparison.Ordinal) >= 0,
                "断面管道删除应复用×图标按钮");
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

            double diameter;
            True(SectionPipeOptions.TryParsePipeDiameter("DN200", out diameter), "应识别标准 DN 管径文字");
            Near(0.2, diameter, 1e-9, "DN200 应同步为 0.2m 外径");
            True(SectionPipeOptions.TryParsePipeDiameter("管径 DN315", out diameter), "带说明的 DN 管径文字也应识别");
            Near(0.315, diameter, 1e-9, "DN315 应同步为 0.315m 外径");
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

        private static void TestExcelTableRangeReading()
        {
            var savedDialogSettings = new ExcelToCadDialogSettings
            {
                RangeMode = ExcelTableRangeMode.PrintArea,
                OutputType = CadExcelTableOutputType.Block,
                TextHeight = 3.25,
                PreserveBackgroundColors = false,
                PreserveTextColors = true,
                PreserveMergedCells = false,
                DrawGridLines = true,
                GridLayerName = "EX_GRID",
                ContentLayerName = "EX_TEXT",
                EntityColorMode = ExcelCadEntityColorMode.ByLayer
            };
            string page = CDBoxStudioExcelToCadPage.BuildStandaloneDocument(
                new CDBoxStudioSettings(), 2.5, savedDialogSettings,
                new[] { "0", "EX_GRID", "EX_TEXT" });
            Contains(page, "browseExcelWorkbook", "Excel 转 CAD 应通过 WebView2 路由选择文件");
            Contains(page, "openExcelSelection", "Excel 转 CAD 应支持打开 Excel 并同步选区");
            Contains(page, "pollExcelSelection", "Excel 转 CAD 应持续读取用户当前选择");
            Contains(page, "value=\"exploded\"", "Excel 转 CAD 应提供分解线文字输出");
            Contains(page, "value=\"table\"", "Excel 转 CAD 应提供原生 TABLE 输出");
            Contains(page, "value=\"block\"", "Excel 转 CAD 应提供块输出");
            Contains(page, "confirmSavedExcelFallback", "Excel 未保存状态失败时应在 WebView2 页面确认回退");
            Contains(page, "saveExcelToCadPreferences", "Excel 转 CAD 应即时保存用户设置");
            Contains(page, "value=\"block\" checked", "页面应恢复上次选择的块形式");
            Contains(page, "value=\"print\" checked", "页面应恢复上次选择的数据范围");
            Contains(page, "value=\"3.25\"", "页面应恢复上次使用的文字高度");
            Contains(page, "实体图层", "表格设置应提供实体图层选项");
            Contains(page, "id=\"gridLayer\"", "应能选择单元格线图层");
            Contains(page, "id=\"contentLayer\"", "应能选择表格内容图层");
            Contains(page, "value=\"EX_GRID\" selected",
                "页面应恢复上次选择的单元格线图层");
            Contains(page, "value=\"EX_TEXT\" selected",
                "页面应恢复上次选择的表格内容图层");
            Contains(page, "name=\"entityColor\" value=\"layer\" checked",
                "页面应恢复上次选择的颜色随层模式");
            Contains(page, "表格设置", "设置卡片应使用简洁标题");
            Contains(page, ".ex-input-row input[type=text]", "当前选择输入框应使用完整行宽样式");
            Contains(page, "input[type=radio]{position:absolute", "单选圆点应隐藏");
            False(page.Contains("WebView2 统一界面"), "不应显示实现技术徽标");
            False(page.Contains("复刻设置"), "不应继续显示旧设置标题");

            string root = NewTemporaryDirectory("excel-to-cad");
            string path = Path.Combine(root, "table.xlsx");
            string legacyPath = Path.Combine(root, "legacy.xls");
            try
            {
                using (var legacyWorkbook = new HSSFWorkbook())
                {
                    IFont defaultFont = legacyWorkbook.GetFontAt(0);
                    defaultFont.FontName = "Arial";
                    defaultFont.FontHeightInPoints = 12;
                    ISheet legacySheet = legacyWorkbook.CreateSheet("Legacy");
                    legacySheet.SetColumnWidth(0, 2698);
                    IRow legacyTitle = legacySheet.CreateRow(0);
                    legacyTitle.CreateCell(0).SetCellValue("旧版表格");
                    legacySheet.AddMergedRegion(new CellRangeAddress(0, 0, 0, 1));
                    IRow legacyRow = legacySheet.CreateRow(1);
                    legacyRow.CreateCell(0).SetCellValue(12.5);
                    legacyRow.CreateCell(1).SetCellValue("正常读取");
                    using (FileStream stream = File.Create(legacyPath))
                        legacyWorkbook.Write(stream);
                }
                var legacyOptions = new ExcelToCadOptions
                {
                    FilePath = legacyPath,
                    SheetName = "Legacy",
                    RangeMode = ExcelTableRangeMode.UsedRange
                };
                ExcelTableModel legacy = ExcelTableReader.Read(legacyOptions);
                Equal(2, legacy.RowCount, "旧版 XLS 应读取全部可见行");
                Equal("旧版表格", legacy.GetCell(0, 0).Text,
                    "旧版 XLS 不应因 HSSFRow.Hidden 未实现而失败");
                Near(95.0, legacy.ColumnPixelWidths[0], 0.01,
                    "Excel 列宽应按默认字体的实际数字宽度换算");

                using (var workbook = new XSSFWorkbook())
                {
                    ISheet sheet = workbook.CreateSheet("Main");
                    ICellStyle titleStyle = workbook.CreateCellStyle();
                    titleStyle.Alignment = NPOI.SS.UserModel.HorizontalAlignment.Center;
                    titleStyle.VerticalAlignment = NPOI.SS.UserModel.VerticalAlignment.Center;
                    titleStyle.FillForegroundColor = IndexedColors.Yellow.Index;
                    titleStyle.FillPattern = FillPattern.SolidForeground;
                    IFont titleFont = workbook.CreateFont();
                    titleFont.IsBold = true;
                    titleStyle.SetFont(titleFont);

                    IRow first = sheet.CreateRow(0);
                    first.HeightInPoints = 24;
                    ICell title = first.CreateCell(0);
                    title.SetCellValue("测试表格");
                    title.CellStyle = titleStyle;
                    sheet.AddMergedRegion(new CellRangeAddress(0, 0, 0, 1));

                    IRow second = sheet.CreateRow(1);
                    second.CreateCell(0).SetCellValue(10);
                    second.CreateCell(1).SetCellValue("甲");
                    second.CreateCell(2).CellFormula = "A2+5";

                    IRow third = sheet.CreateRow(2);
                    third.HeightInPoints = 30;
                    third.CreateCell(0).SetCellValue("尾行");
                    third.CreateCell(1).SetCellValue("乙");
                    third.CreateCell(2).SetCellValue(20);

                    sheet.SetColumnWidth(0, 20 * 256);
                    sheet.SetColumnWidth(1, 12 * 256);
                    workbook.SetPrintArea(0, 1, 2, 1, 2);
                    workbook.GetCreationHelper().CreateFormulaEvaluator().EvaluateAll();
                    using (FileStream stream = File.Create(path)) workbook.Write(stream);
                }

                Equal("B2:D4", ExcelTableReader.NormalizeRangeAddress(
                    "'Main'!$B$2:$D$4"), "选择区域地址应去除工作表名和绝对引用符");

                using (FileStream stream = File.OpenRead(path))
                using (var verificationWorkbook = new XSSFWorkbook(stream))
                {
                    ICell formulaCell = verificationWorkbook.GetSheet("Main")
                        .GetRow(1).GetCell(2);
                    Equal(CellType.Formula, formulaCell.CellType,
                        "测试工作簿应保存公式单元格");
                    CellValue evaluated = verificationWorkbook.GetCreationHelper()
                        .CreateFormulaEvaluator().Evaluate(formulaCell);
                    Equal(CellType.Numeric, evaluated.CellType,
                        "NPOI 应计算简单公式");
                    Near(15, evaluated.NumberValue, 0.000001,
                        "NPOI 公式计算结果");
                }

                ExcelWorkbookInfo info = ExcelTableReader.Inspect(path);
                Equal(1, info.Sheets.Count, "应读取工作表列表");
                Equal("A1:C3", info.Sheets[0].UsedRange, "应识别实际使用区域");

                var usedOptions = new ExcelToCadOptions
                {
                    FilePath = path,
                    SheetName = "Main",
                    RangeMode = ExcelTableRangeMode.UsedRange
                };
                ExcelTableModel used = ExcelTableReader.Read(usedOptions);
                Equal(3, used.RowCount, "使用区域行数");
                Equal(3, used.ColumnCount, "使用区域列数");
                Equal(1, used.MergedRanges.Count, "合并单元格应保留");
                Equal("测试表格", used.GetEffectiveCell(0, 1).Text,
                    "合并区域应使用锚点文字");
                True(used.GetCell(0, 0).Style.Bold, "粗体样式应保留");
                True(used.GetCell(0, 0).Style.BackgroundColor != null,
                    "背景色应保留");
                Equal("15", used.GetCell(1, 2).Text, "公式应读取显示结果");
                True(used.ColumnPixelWidths[0] > used.ColumnPixelWidths[1],
                    "列宽比例应保留");
                True(used.RowPixelHeights[2] > used.RowPixelHeights[1],
                    "行高比例应保留");

                var selectedOptions = new ExcelToCadOptions
                {
                    FilePath = path,
                    SheetName = "Main",
                    RangeMode = ExcelTableRangeMode.LiveSelection,
                    SelectedRange = "$B$2:$C$3"
                };
                ExcelTableModel selected = ExcelTableReader.Read(selectedOptions);
                Equal(2, selected.RowCount, "当前选择区域行数");
                Equal(2, selected.ColumnCount, "当前选择区域列数");
                Equal("甲", selected.GetCell(0, 0).Text, "当前选择区域起始单元格");
                Equal("20", selected.GetCell(1, 1).Text, "当前选择区域结束单元格");

                var printOptions = new ExcelToCadOptions
                {
                    FilePath = path,
                    SheetName = "Main",
                    RangeMode = ExcelTableRangeMode.PrintArea
                };
                ExcelTableModel printed = ExcelTableReader.Read(printOptions);
                Equal("B2:C3", printed.SourceRange, "打印区域应被正确解析");
                Equal(2, printed.RowCount, "打印区域行数");
                Equal(2, printed.ColumnCount, "打印区域列数");

                selectedOptions.SelectedRange = "A1:ZZ100";
                Exception tooLarge = Capture(delegate
                {
                    ExcelTableReader.Read(selectedOptions);
                });
                True(tooLarge is InvalidOperationException,
                    "超过上限的选择区域应被拒绝");
                Contains(tooLarge.Message, "超过单次转换上限",
                    "区域过大提示应说明转换上限");
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
                string staleBackup = Path.Combine(root,
                    "CDBox.bundle.backup.20200101_000000.stale");
                string staleStaging = Path.Combine(root,
                    "CDBox.bundle.new.stale");
                string legacyBackup = Path.Combine(root,
                    "CDBox.bundle.update-backup");
                string unrelated = Path.Combine(root, "customer.backup.files");
                Directory.CreateDirectory(staleBackup);
                Directory.CreateDirectory(staleStaging);
                Directory.CreateDirectory(legacyBackup);
                Directory.CreateDirectory(unrelated);

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
                False(Directory.Exists(staleBackup), "更新成功后应清理旧 bundle 备份");
                False(Directory.Exists(staleStaging), "更新成功后应清理遗留的临时 bundle");
                False(Directory.Exists(legacyBackup), "更新成功后应清理旧更新器遗留备份");
                True(Directory.Exists(unrelated), "不得清理不属于更新器的目录");
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        private static void TestWastewaterResultTableFormatting()
        {
            Equal("污水管成果表",
                WastewaterResultTableDefaults.EntityLayerName,
                "成果表的表格线和文字应统一放到专用图层");
            IList<WastewaterResultTableRow> rows =
                WastewaterResultTableFormatter.SortRows(new[]
                {
                    new WastewaterResultTableRow { NodeNo = "W10" },
                    new WastewaterResultTableRow { NodeNo = "w2" },
                    new WastewaterResultTableRow { NodeNo = "W1" },
                    new WastewaterResultTableRow { NodeNo = "W02" },
                    new WastewaterResultTableRow { NodeNo = "" }
                });
            Equal("W1", rows[0].NodeNo, "混合编号应按数字自然排序");
            Equal("w2", rows[1].NodeNo, "大小写字母应参与同一自然排序");
            Equal("W02", rows[2].NodeNo, "数值相同的前导零编号应排在其后");
            Equal("W10", rows[3].NodeNo, "两位数字不得排到 W2 之前");
            Equal(string.Empty, rows[4].NodeNo, "空井号应排在末尾");

            var row = new WastewaterResultTableRow
            {
                GroundElevation = 100.125,
                WellDepth = 1.375
            };
            Near(98.75, row.BottomElevation, 1e-9,
                "检查井和沉泥井的井底标高均应为自然标高减井深");
            Equal("98.750", WastewaterResultTableFormatter.Elevation(
                row.BottomElevation), "井底标高应保留三位小数");
            Equal("%%c700", WastewaterResultTableFormatter.Diameter(
                "700铸铁井盖"), "井规格应按模板输出直径符号");
        }

        private static void TestFloatingCenterPhaseZero()
        {
            string currentDocument = "doc-a";
            var center = new FloatingCenterService(
                () => currentDocument);
            int changedCount = 0;
            center.Changed += delegate { changedCount++; };

            center.Publish(new FloatingMessage
            {
                Kind = FloatingMessageKind.Information,
                Title = "图层检查",
                Summary = "发现一项变化",
                MergeKey = "layer-change"
            });
            center.Publish(new FloatingMessage
            {
                Kind = FloatingMessageKind.Information,
                Title = "图层检查",
                Summary = "发现一项变化",
                MergeKey = "layer-change"
            });
            IList<FloatingMessage> historyA = center.GetHistory("doc-a");
            Equal(1, historyA.Count, "相同 MergeKey 的短时消息应合并");
            Equal(2, historyA[0].RepeatCount, "合并消息应记录重复次数");

            center.Publish(new FloatingMessage
            {
                DocumentId = "doc-a",
                Kind = FloatingMessageKind.Information,
                Title = "提示卡合并",
                Summary = "第一次",
                MergeKey = "presentation-merge",
                PresentAsCard = true
            });
            center.Publish(new FloatingMessage
            {
                DocumentId = "doc-a",
                Kind = FloatingMessageKind.Information,
                Title = "提示卡合并",
                Summary = "第二次",
                MergeKey = "presentation-merge",
                PresentAsCard = true
            });
            True(center.GetHistory("doc-a")[0].PresentAsCard,
                "合并后的最新消息必须保留提示卡呈现标记");

            currentDocument = "doc-b";
            center.Publish(new FloatingMessage
            {
                Kind = FloatingMessageKind.Warning,
                Title = "图纸 B",
                Summary = "警告",
                MergeKey = "layer-change"
            });
            Equal(2, center.GetHistory("doc-a").Count,
                "其他图纸消息不得进入图纸 A");
            Equal(1, center.GetHistory("doc-b").Count,
                "图纸 B 应有独立历史");

            using (IPromptSession prompt = center.BeginPrompt(
                new FloatingPrompt
                {
                    DocumentId = "doc-a",
                    Title = "选择对象",
                    Message = "请选择主管"
                }))
            {
                Equal(FloatingActivityState.WaitingForCadInput,
                    center.GetStatus("doc-a").Activity,
                    "活动 Prompt 应进入 CAD 输入等待状态");
                Equal(1, center.GetActiveMessages("doc-a").Count,
                    "Prompt 应作为活动消息存在");
                prompt.Update("请继续选择主管");
                Equal("请继续选择主管",
                    center.GetActiveMessages("doc-a")[0].Summary,
                    "Prompt 更新应刷新活动消息");
            }
            Equal(FloatingActivityState.Idle,
                center.GetStatus("doc-a").Activity,
                "Prompt 释放后应回到空闲状态");

            using (IProgressHandle progress = center.BeginProgress(
                new FloatingProgressSpec
                {
                    DocumentId = "doc-a",
                    Title = "图纸检查",
                    Message = "正在检查"
                }))
            {
                progress.Report(135, "接近完成");
                FloatingMessage active =
                    center.GetActiveMessages("doc-a")[0];
                Near(100.0, active.Progress.Value, 1e-9,
                    "进度必须限制在 0 至 100");
                Equal(FloatingActivityState.Working,
                    center.GetStatus("doc-a").Activity,
                    "活动进度应进入工作状态");
                progress.Complete("检查完成");
                True(progress.IsCompleted, "完成后进度句柄应终结");
            }
            Equal(0, center.GetStatus("doc-a").ActiveProgressCount,
                "完成后不得残留永久进度");
            Equal(FloatingMessageKind.Success,
                center.GetHistory("doc-a")[0].Kind,
                "进度完成应生成成功历史");

            FloatingMessage warning = center.Publish(new FloatingMessage
            {
                DocumentId = "doc-a",
                Kind = FloatingMessageKind.Warning,
                Title = "待处理警告",
                Summary = "主管缺少终点井",
                IsPersistent = true
            });
            center.Publish(new FloatingMessage
            {
                DocumentId = "doc-a",
                Kind = FloatingMessageKind.Error,
                Title = "严重错误",
                Summary = "数据结构不可识别",
                IsPersistent = true
            });
            FloatingStatusSnapshot status = center.GetStatus("doc-a");
            Equal(FloatingHealthState.Critical, status.Health,
                "严重错误应覆盖警告成为最高健康状态");
            Equal(2, status.TaskGroupCount,
                "角标应统计活动任务组而非实体数量");
            Equal(FloatingMessageKind.Error,
                center.GetActiveMessages("doc-a")[0].Kind,
                "活动消息应按严重程度排序");
            center.Dismiss("doc-a", warning.Id);
            Equal(1, center.GetStatus("doc-a").TaskGroupCount,
                "关闭一个任务后角标应同步减少");
            True(changedCount >= 8,
                "消息、Prompt、进度和关闭均应发布状态变化");

            center.ClearDocument("doc-a");
            Equal(0, center.GetHistory("doc-a").Count,
                "清理文档状态后不得保留旧图纸历史");
            Equal(1, center.GetHistory("doc-b").Count,
                "清理图纸 A 不得影响图纸 B");
        }

        private static void TestFloatingCenterPhaseOne()
        {
            var primary = new FloatingWorkArea
            {
                DeviceName = "DISPLAY-A",
                IsPrimary = true,
                Left = 0,
                Top = 0,
                Width = 1920,
                Height = 1040
            };
            var secondary = new FloatingWorkArea
            {
                DeviceName = "DISPLAY-B",
                Left = 1920,
                Top = 0,
                Width = 1280,
                Height = 1024
            };
            FloatingResolvedPosition snapped =
                FloatingCenterPlacement.ConstrainAndSnap(3138, 420,
                    secondary, 72, 72, true);
            Equal(FloatingSnapEdge.Right, snapped.State.SnapEdge,
                "靠近右边缘应吸附并记录边缘");
            Near(3128, snapped.State.Left, 1e-9,
                "吸附位置应保留在第二显示器工作区内");
            Near(420, snapped.State.EdgeOffset, 1e-9,
                "左右吸附应记录纵向边缘偏移");

            FloatingResolvedPosition restored = FloatingCenterPlacement.Restore(
                snapped.State, new[] { primary, secondary }, 72, 72);
            Equal("DISPLAY-B", restored.State.MonitorDeviceName,
                "显示器仍存在时应恢复到原显示器");
            Equal(FloatingSnapEdge.Right, restored.State.SnapEdge,
                "恢复后必须保留吸附边语义");
            Near(3128, restored.State.Left, 1e-9,
                "右侧吸附恢复后应贴紧原显示器右边缘");

            var removedMonitorPosition = new FloatingPositionState
            {
                MonitorDeviceName = "DISPLAY-MISSING",
                SnapEdge = FloatingSnapEdge.None,
                Left = 9000,
                Top = -9000
            };
            FloatingResolvedPosition recovered = FloatingCenterPlacement.Restore(
                removedMonitorPosition, new[] { primary }, 72, 72);
            True(recovered.State.Left >= primary.Left &&
                recovered.State.Left <= primary.Right - 72,
                "显示器移除后横向位置必须收回主屏");
            True(recovered.State.Top >= primary.Top &&
                recovered.State.Top <= primary.Bottom - 72,
                "显示器移除后纵向位置必须收回主屏");

            Equal(string.Empty, FloatingCenterPresentation.Badge(0),
                "无任务时不显示角标");
            Equal("9+", FloatingCenterPresentation.Badge(15),
                "任务组超过九个应显示 9+");
            Equal("存在严重问题", FloatingCenterPresentation.HealthText(
                FloatingHealthState.Critical), "严重状态文字");

            double? progress = FloatingCenterPresentation.OverallProgress(
                new[]
                {
                    new FloatingMessage
                    {
                        Kind = FloatingMessageKind.Progress,
                        Progress = 20,
                        IsIndeterminate = false
                    },
                    new FloatingMessage
                    {
                        Kind = FloatingMessageKind.Progress,
                        Progress = 80,
                        IsIndeterminate = false
                    }
                });
            Near(50.0, progress.Value, 1e-9,
                "多个确定进度应取平均值");
            True(!FloatingCenterPresentation.OverallProgress(new[]
                {
                    new FloatingMessage
                    {
                        Kind = FloatingMessageKind.Progress,
                        IsIndeterminate = true
                    }
                }).HasValue, "不确定进度应驱动旋转环而非错误百分比");
        }

        private static void TestFloatingCenterPhaseTwo()
        {
            var queue = new FloatingMessagePresentationQueue(3);
            queue.Enqueue(new FloatingMessage
            {
                Id = "info",
                DocumentId = "doc-a",
                Kind = FloatingMessageKind.Information,
                Title = "普通通知",
                CreatedAt = DateTime.UtcNow.AddSeconds(-3)
            });
            queue.Enqueue(new FloatingMessage
            {
                Id = "warning",
                DocumentId = "doc-a",
                Kind = FloatingMessageKind.Warning,
                Title = "警告",
                CreatedAt = DateTime.UtcNow.AddSeconds(-2)
            });
            queue.Enqueue(new FloatingMessage
            {
                Id = "prompt",
                DocumentId = "doc-a",
                Kind = FloatingMessageKind.Prompt,
                Title = "选择对象",
                CreatedAt = DateTime.UtcNow.AddSeconds(-1)
            });
            queue.Enqueue(new FloatingMessage
            {
                Id = "other-doc",
                DocumentId = "doc-b",
                Kind = FloatingMessageKind.Error,
                Title = "图纸 B 错误"
            });
            Equal(3, queue.Count, "超过上限时队列必须保持固定容量");
            Equal("prompt", queue.Dequeue("doc-a").Id,
                "CAD Prompt 应优先于普通警告和通知");
            Equal("other-doc", queue.Dequeue("doc-b").Id,
                "不同图纸提示必须隔离取出");

            var mergeQueue = new FloatingMessagePresentationQueue(4);
            mergeQueue.Enqueue(new FloatingMessage
            {
                Id = "first",
                DocumentId = "doc-a",
                Kind = FloatingMessageKind.Information,
                MergeKey = "same",
                Summary = "第一次"
            });
            mergeQueue.Enqueue(new FloatingMessage
            {
                Id = "second",
                DocumentId = "doc-a",
                Kind = FloatingMessageKind.Information,
                MergeKey = "same",
                Summary = "第二次",
                RepeatCount = 2
            });
            Equal(1, mergeQueue.Count, "相同 MergeKey 不应重复排队");
            Equal("第二次", mergeQueue.Dequeue("doc-a").Summary,
                "合并队列应保留最新呈现内容");

            var lifecycleQueue = new FloatingMessagePresentationQueue(6);
            var pendingProgress = new FloatingMessage
            {
                Id = "pending-progress",
                DocumentId = "doc-a",
                Kind = FloatingMessageKind.Progress,
                IsPersistent = true,
                UpdatedAt = DateTime.UtcNow.AddSeconds(-2)
            };
            lifecycleQueue.Enqueue(pendingProgress);
            lifecycleQueue.Enqueue(new FloatingMessage
            {
                Id = "placement-prompt",
                DocumentId = "doc-a",
                Kind = FloatingMessageKind.Prompt,
                IsPersistent = true,
                UpdatedAt = DateTime.UtcNow
            });
            lifecycleQueue.RemoveSupersededBy(new FloatingMessage
            {
                Id = "placement-prompt",
                DocumentId = "doc-a",
                Kind = FloatingMessageKind.Prompt,
                UpdatedAt = DateTime.UtcNow
            });
            True(!lifecycleQueue.Contains("doc-a", "pending-progress"),
                "CAD 输入提示出现时不应保留已被取代的旧进度卡");
            lifecycleQueue.RemoveInactivePersistent("doc-a",
                new[] { "placement-prompt" });
            Equal(1, lifecycleQueue.Count,
                "仍处于活动状态的 CAD 输入提示必须保留");
            lifecycleQueue.RemoveInactivePersistent("doc-a",
                new string[0]);
            Equal(0, lifecycleQueue.Count,
                "进度或提示结束后必须清除排队中的持久卡片");

            var center = new FloatingCenterService(() => "doc-progress");
            IProgressHandle progress = center.BeginProgress(
                new FloatingProgressSpec
                {
                    Title = "兼容进度",
                    Message = "处理中"
                });
            progress.Report(25, "已完成四分之一");
            progress.Dispose();
            Equal(0, center.GetActiveMessages("doc-progress").Count,
                "仅释放兼容进度句柄时应静默清除活动项");
            Equal(0, center.GetHistory("doc-progress").Count,
                "仅释放句柄不应产生虚假的取消历史");
        }

        private static void TestFloatingCenterPhaseThree()
        {
            var layers = new List<DrawingCheckLayerSnapshot>
            {
                new DrawingCheckLayerSnapshot
                {
                    LayerName = "污水主管-DN300",
                    ObjectHandles = new List<string> { "10" }
                },
                new DrawingCheckLayerSnapshot
                {
                    LayerName = "旧项目自定义图层",
                    ParentGroup = "未知父属性",
                    ObjectHandles = new List<string> { "40" }
                }
            };
            var objects = new List<DrawingCheckObjectSnapshot>
            {
                new DrawingCheckObjectSnapshot
                {
                    Handle = "10",
                    LayerName = "污水主管-DN300",
                    LayerParentGroup = "主管",
                    ObjectKind = "主管",
                    HasSavedAttributes = true,
                    Diameter = "DN300",
                    Material = "HDPE",
                    StartNode = "W1",
                    EndNode = "W1",
                    AverageDepth = 0.20,
                    PipeOuterDiameter = 0.30,
                    BackfillStructure = "管线层 0.20 管线层",
                    PipeLayerBelowDiameter = true,
                    StartConnectionEvaluated = true,
                    StartConnectedToAssignedNode = true,
                    EndConnectionEvaluated = true,
                    EndConnectedToAssignedNode = true
                },
                new DrawingCheckObjectSnapshot
                {
                    Handle = "20",
                    LayerParentGroup = "井",
                    ObjectKind = "节点/检查井",
                    HasSavedAttributes = true,
                    IsSpecialObject = true,
                    WellDepth = 0
                }
            };
            var annotations = new List<DrawingCheckAnnotationSnapshot>
            {
                new DrawingCheckAnnotationSnapshot
                {
                    AnnotationId = "annotation-a",
                    AnnotationHandle = "30",
                    SourceHandle = "99",
                    BindingState = "Invalid",
                    SourceExists = false
                }
            };

            List<DrawingCheckIssue> issues = DrawingCheckRuleEvaluator.Evaluate(
                "doc-check-a", layers, objects, annotations);
            False(issues.Any(x => x.RuleId ==
                    DrawingCheckRuleEvaluator.LayerParentMissingRule),
                "图纸检查不应再报告图层父属性缺失");
            False(issues.Any(x => x.RuleId ==
                    DrawingCheckRuleEvaluator.LayerParentInvalidRule),
                "图纸检查不应再报告图层父属性无效");
            True(issues.Any(x => x.RuleId ==
                    DrawingCheckRuleEvaluator.PipeRelationRule),
                "主管起终点相同应形成管线关系问题");
            False(issues.Any(x => string.Equals(x.Title,
                    "管线端点与关联井未连接", StringComparison.Ordinal)),
                "属性编辑器连接识别成功时不得再按简化几何规则误报断点");
            True(issues.Any(x => x.RuleId ==
                    DrawingCheckRuleEvaluator.DataValidityRule),
                "深度或管线层小于外径应形成数据合法性问题");
            True(issues.Any(x => x.RuleId ==
                    DrawingCheckRuleEvaluator.AnnotationBindingRule),
                "失效标注绑定应形成检查问题");
            True(!issues.Any(x => x.ObjectHandles.Contains("20")),
                "特殊对象必须跳过数据质量检查");

            var invertObjects = new List<DrawingCheckObjectSnapshot>
            {
                new DrawingCheckObjectSnapshot
                {
                    Handle = "A1", ObjectKind = "主管",
                    HasSavedAttributes = true,
                    DetectedEndNode = "W9",
                    EndInvertElevation = 100.00
                },
                new DrawingCheckObjectSnapshot
                {
                    Handle = "A2", ObjectKind = "主管",
                    HasSavedAttributes = true,
                    DetectedStartNode = "W9",
                    StartInvertElevation = 99.92
                }
            };
            List<DrawingCheckIssue> invertIssues =
                DrawingCheckRuleEvaluator.Evaluate("doc-invert",
                    new List<DrawingCheckLayerSnapshot>(), invertObjects,
                    new List<DrawingCheckAnnotationSnapshot>());
            DrawingCheckIssue invertIssue = invertIssues.FirstOrDefault(x =>
                x.RuleId == DrawingCheckRuleEvaluator
                    .PipeInvertConsistencyRule);
            True(invertIssue != null,
                "同一物理节点的管线内底标高不一致时应生成弱提示");
            Equal(DrawingCheckSeverity.Info, invertIssue.Severity,
                "内底标高不一致仅作弱提示，不得升级为强错误");

            List<DrawingCheckGroup> groups = DrawingCheckRuleEvaluator.Group(
                "doc-check-a", issues);
            Equal(groups.Select(x => x.Id).Distinct().Count(), groups.Count,
                "规则组标识必须稳定且唯一");
            True(groups.Count < issues.Count,
                "同类对象问题应聚合为规则组而不是逐对象占用角标");

            var manager = new DrawingCheckManager();
            manager.Begin("doc-check-a", "扫描");
            True(manager.GetSnapshot("doc-check-a").IsRunning,
                "开始检查后应记录运行状态");
            manager.Complete("doc-check-a", issues);
            True(!manager.GetSnapshot("doc-check-a").IsRunning,
                "检查完成后应清除运行状态");
            DrawingCheckSnapshot beforeIgnore = manager.GetSnapshot(
                "doc-check-a");
            DrawingCheckGroup ignoredGroup = beforeIgnore.Groups[0];
            int activeGroupCount = beforeIgnore.Groups.Count;
            List<DrawingCheckIssue> ignored = manager.IgnoreGroup(
                "doc-check-a", ignoredGroup.Id);
            True(ignored.Count > 0, "问题组应可转入已忽略清单");
            DrawingCheckSnapshot afterIgnore = manager.GetSnapshot(
                "doc-check-a");
            Equal(activeGroupCount - 1, afterIgnore.Groups.Count,
                "已忽略问题不应继续留在活动任务中");
            Equal(1, afterIgnore.IgnoredGroups.Count,
                "已忽略问题应保留在独立清单中");
            List<DrawingCheckIssue> restored = manager.RestoreGroup(
                "doc-check-a", afterIgnore.IgnoredGroups[0].Id);
            True(restored.Count > 0, "已忽略问题应可恢复");
            DrawingCheckSnapshot afterRestore = manager.GetSnapshot(
                "doc-check-a");
            Equal(activeGroupCount, afterRestore.Groups.Count,
                "恢复后问题应重新进入活动任务");
            Equal(0, afterRestore.IgnoredGroups.Count,
                "恢复后已忽略清单应移除对应问题");
            string persistedDocument = "test:" + Guid.NewGuid().ToString("N");
            DrawingCheckIgnoreStore.Add(persistedDocument, ignored);
            HashSet<string> persistedKeys = DrawingCheckIgnoreStore.LoadKeys(
                persistedDocument);
            True(ignored.All(x => persistedKeys.Contains(x.IgnoreKey)),
                "忽略记录应跨检查持久保存");
            List<DrawingCheckIssue> persistedIssues =
                DrawingCheckIgnoreStore.LoadIssues(persistedDocument,
                    "doc-check-a");
            Equal(ignored.Count, persistedIssues.Count,
                "已忽略清单应可从持久记录恢复完整问题");
            DrawingCheckIgnoreStore.RemoveKeys(persistedDocument,
                ignored.Select(x => x.IgnoreKey));
            Equal(0, DrawingCheckIgnoreStore.LoadKeys(
                persistedDocument).Count,
                "恢复问题后应删除对应持久忽略记录");
            Equal(0, manager.GetSnapshot("doc-check-b").Groups.Count,
                "不同图纸的检查结果必须隔离");
            manager.ClearDocument("doc-check-a");
            Equal(0, manager.GetSnapshot("doc-check-a").Groups.Count,
                "图纸关闭后应清理该图纸检查结果");
        }

        private static LongitudinalProfileWellData PositionedWell(
            string nodeNo, double x, double y,
            double groundElevation, double wellDepth)
        {
            return new LongitudinalProfileWellData
            {
                NodeNo = nodeNo,
                GroundElevation = groundElevation,
                WellDepth = wellDepth,
                HasPosition = true,
                PositionX = x,
                PositionY = y
            };
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

        private static void TestFloatingCenterPhaseFive()
        {
            False(FloatingLegacyRoutingPolicy
                    .RequiresSynchronousDecision("OK"),
                "单按钮通知不应继续创建独立同步窗口");
            True(FloatingLegacyRoutingPolicy
                    .RequiresSynchronousDecision("YesNo"),
                "需要返回选择结果的确认必须保留同步决策交互");
            True(FloatingLegacyRoutingPolicy
                    .RequiresSynchronousDecision("OKCancel"),
                "可取消操作必须等待用户选择");
            False(FloatingLegacyRoutingPolicy
                    .AllowsIndependentProgressWindow,
                "Phase 5 后不应再创建独立进度窗口");
        }

        private static void TestFloatingCenterPhaseFour()
        {
            var manager = new SyncManager();
            SyncTask first = manager.MarkDirty(new SyncTask
            {
                DocumentId = "doc-sync-a",
                MergeKey = "annotation",
                Type = SyncTaskType.Annotation,
                Title = "标注需要同步",
                ObjectHandles = new List<string> { "A1", "A2" },
                ChangedProperties = new List<string> { "长度" }
            });
            SyncTask merged = manager.MarkDirty(new SyncTask
            {
                DocumentId = "doc-sync-a",
                MergeKey = "annotation",
                Type = SyncTaskType.Annotation,
                ObjectHandles = new List<string> { "a2", "A3" },
                ChangedProperties = new List<string> { "管径" },
                Risk = SyncRiskLevel.ConfirmationRequired
            });

            Equal(first.Id, merged.Id, "同类同步应按图纸和合并键聚合");
            Equal(3, merged.AffectedObjectCount, "同步影响对象应去重合并");
            Equal(2, merged.ChangedProperties.Count,
                "同步属性应保留不同变更来源");
            Equal(SyncRiskLevel.ConfirmationRequired, merged.Risk,
                "聚合后应保留较高风险级别");

            SyncTask running = manager.Begin("doc-sync-a", merged.Id);
            Equal(SyncTaskState.Running, running.State,
                "执行同步时任务应进入运行态");
            SyncTask completed = manager.Complete("doc-sync-a", merged.Id,
                "已同步");
            Equal(SyncTaskState.Completed, completed.State,
                "完成同步应记录完成态");
            Equal(0, manager.GetSnapshot("doc-sync-a").Tasks.Count,
                "已完成任务不应继续留在待处理区");
            Equal(1, manager.GetSnapshot("doc-sync-a").History.Count,
                "已完成任务应进入同步历史");

            manager.MarkDirty(new SyncTask
            {
                DocumentId = "doc-sync-b",
                MergeKey = "calculation",
                Type = SyncTaskType.Calculation
            });
            Equal(0, manager.GetSnapshot("doc-sync-a").Tasks.Count,
                "不同图纸的同步任务必须隔离");
            Equal(1, manager.GetSnapshot("doc-sync-b").Tasks.Count,
                "目标图纸应保留自己的同步任务");
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

        private sealed class CapturingLogger : ICDBoxLogger
        {
            public int InfoCount { get; private set; }

            public void Info(string message)
            {
                InfoCount++;
            }

            public void Warn(string message)
            {
            }

            public void Error(string message, Exception exception)
            {
            }
        }

        private sealed class CapturingPageService : ICDBoxPageService
        {
            public CDBoxPageDefinition LastPage { get; private set; }

            public void Show(CDBoxPageDefinition page)
            {
                LastPage = page;
            }
        }

        private static void True(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void False(bool value, string message)
        {
            if (value) throw new InvalidOperationException(message);
        }

        private static void Contains(string value, string expected, string message)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf(expected, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(message + ": missing=" + expected);
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
