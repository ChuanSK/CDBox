using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CDBox.Shared;
using CDBoxUpdater;
using TCPipeAutoDraw.Core.Startup;
using TCPipeAutoDraw.Core.Colors;
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
            Run("??????", TestKindRecognition);
            Run("??????????", TestEffectiveLengthAndDefaultClone);
            Run("?????", TestStructureLayers);
            Run("???????", TestQuantityPipeClassification);
            Run("????????????", TestSpecialObjectsSkipQualityCheck);
            Run("???????", TestSiltWellDepth);
            Run("???????", TestQuantityDependencyRules);
            Run("??????", TestPrimitiveParsing);
            Run("Studio ????", TestStudioRouteRequest);
            Run("????????", TestBuiltInUpdateSourcePriority);
            Run("?? A ???????", TestStageANewInstallDefaults);
            Run("????????", TestNumericInputSteps);
            Run("?????????", TestQuantityDashboardSharedPage);
            Run("?????????", TestQuantityCalculationProcessExport);
            Run("?????????", TestQuantityAttributeEditorSharedPage);
            Run("??????????", TestLayerManagerCustomParents);
            Run("?????????", TestStructuredLayerRecognition);
            Run("???????? ACI ??", TestColorPickerIntegration);
            Run("??? Preview 10 ????", TestSectionDrawingSharedPage);
            Run("???????????", TestQuantityDefaultsTableInteraction);
            Run("??????????????", TestAnnotationHudTextComposition);
            Run("??????????", TestNodeAnnotationTextComposition);
            Run("Excel ???????????", TestExcelTableRangeReading);
            Run("?????????", TestFrameLayoutSettingsNormalization);
            Run("??????????????", TestShortCodeRecognition);
            Run("?????????????", TestLongitudinalProfileCalculation);
            Run("??????????", TestLegacyUpdateSourceValidation);
            Run("?????????", TestUpdaterRejectsZipTraversal);
            Run("????????", TestUpdaterReplacesAndBacksUpBundle);

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
            True(QuantityPipeAttributes.IsNodeKind("???"), "?????????");
            True(QuantityPipeAttributes.IsBranchKind("????"), "??????????");
            True(QuantityPipeAttributes.IsMainPipeKind("????"), "????????");
            Equal(QuantityPipeAttributes.KindNodeWell, QuantityPipeAttributes.DefaultForKind("???").ObjectKind, "?????");
            Equal(QuantityPipeAttributes.KindBranchPipe, QuantityPipeAttributes.DefaultForKind("??").ObjectKind, "?????");
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
                "????????????");
            Equal("TopRight", settings.NorthReferencePosition,
                "?????????????");
            Equal("BottomLeft", settings.ScaleReferencePosition,
                "???????????");
            Near(1.0, settings.NorthSize, 1e-9,
                "???????????");
            Equal("1:500", settings.ScaleText,
                "???????????");
            Near(0.1, settings.ScaleTextHeight, 1e-9,
                "????????????");
            Equal((short)1, settings.ScaleColorIndex,
                "????????????");
            Equal(1, settings.FramesPerRow,
                "?????????");
            Near(0.0, settings.HorizontalGap, 1e-9,
                "????????");
            Near(10.0, settings.VerticalGap, 1e-9,
                "?????????????");
            Equal("Double", settings.TemplateViewMode,
                "????????????");
            settings.TemplateViewMode = "single";
            settings.Normalize();
            Equal("Single", settings.TemplateViewMode,
                "???????????");
        }

        private static void TestShortCodeRecognition()
        {
            string data =
                "1,????,100,200,1\r\n"
                + "2,?,101,201,2\r\n"
                + "??????????\r\n"
                + "3,?,102,202,3\r\n"
                + "4,???,103,203,4\r\n"
                + "5,0?,104,204,5\r\n";
            ShortCodeReadResult parsed =
                ShortCodeRecognitionParser.ParseText(data);
            Equal(5, parsed.Records.Count,
                "??????????????");
            Equal(1, parsed.InvalidLineCount,
                "???????????????");

            string encodedPath = Path.Combine(Path.GetTempPath(),
                "cdbox-shortcode-" + Guid.NewGuid().ToString("N") + ".dat");
            string utf16Path = encodedPath + ".utf16.dat";
            try
            {
                File.WriteAllText(encodedPath,
                    "1,????,100,200,1\r\n2,+,101,201,2",
                    Encoding.GetEncoding(54936));
                ShortCodeReadResult encoded =
                    ShortCodeRecognitionParser.ReadFile(encodedPath);
                Equal(2, encoded.Records.Count,
                    "GB18030 ???????????");
                Equal("????", encoded.Records[0].Code,
                    "?????????");
                File.WriteAllText(utf16Path,
                    "1,????,100,200,1\r\n2,+,101,201,2",
                    new UnicodeEncoding(false, false, true));
                ShortCodeReadResult utf16 =
                    ShortCodeRecognitionParser.ReadFile(utf16Path);
                Equal("????", utf16.Records[0].Code,
                    "? BOM ? UTF-16 ??????????");
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
                "CASS ?????????????");
            Equal(5, cassPaths[0].Points.Count,
                "CASS ???????????????????");

            var settings = new ShortCodeRecognitionSettings
            {
                RecognitionSymbol = "?",
                ConnectPreviousPoint = true,
                ConnectNextPoint = true,
                AutoClose = true
            };
            List<ShortCodePath> paths =
                ShortCodeRecognitionParser.BuildPaths(
                    parsed.Records, settings);
            Equal(2, paths.Count,
                "??????????????????");
            Equal(4, paths[0].Points.Count,
                "?????????????????");
            True(paths[0].Closed,
                "??????????????????");
            True(paths[1].IsJumpConnection,
                "??????????????");
            False(paths[1].Closed,
                "?????????????");

            settings.RecognitionSymbol = "\r\n";
            settings.Normalize();
            Equal("+", settings.RecognitionSymbol,
                "???????????????");

            string standalone =
                CDBoxStudioShortCodeSettingsPage.BuildStandaloneDocument(
                    new CDBoxStudioSettings());
            string embedded =
                CDBoxStudioShortCodeSettingsPage.BuildEmbeddedSection(
                    new CDBoxStudioSettings());
            Contains(standalone, "??????",
                "????????????");
            Contains(embedded, "shortCodeSettingsFrame",
                "??????????????");
            Contains(standalone, "????????",
                "?????????????");
            Contains(standalone, "????????",
                "?????????????");
            Contains(standalone, "????",
                "????????????");
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
                    Foundation = "????",
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
                    WellSpec = "?500",
                    WellType = "???"
                },
                new LongitudinalProfileWellData
                {
                    NodeNo = "W-52",
                    GroundElevation = 1407.404,
                    WellDepth = 0.485,
                    WellSpec = "?500",
                    WellType = "???"
                },
                new LongitudinalProfileWellData
                {
                    NodeNo = "W-53",
                    GroundElevation = 1407.000,
                    WellDepth = 0.800,
                    WellSpec = "?700",
                    WellType = "???",
                    SiltWellDeductDepth700 = 0.50
                }
            };
            LongitudinalProfileBuildResult result =
                LongitudinalProfileCalculator.Build(pipes, wells);
            True(result.Success, result.Message);
            Equal(3, result.Profile.Nodes.Count,
                "????????????");
            Equal("W-51", result.Profile.Nodes[0].NodeNo,
                "?????????????");
            Near(1406.991,
                result.Profile.Nodes[0].DesignInvertElevation, 1e-9,
                "??????????????????");
            Near(1406.919,
                result.Profile.Nodes[1].DesignInvertElevation, 1e-9,
                "???????????");
            Near(4.340,
                result.Profile.Spans[0].SlopePermille, 0.001,
                "??????????????????????");
            Near(0.434,
                result.Profile.Spans[0].SlopePercent, 0.001,
                "?????????????????");
            Equal("????", result.Profile.Spans[0].Foundation,
                "?????????????");
            Near(1406.700,
                result.Profile.Nodes[2].DesignInvertElevation, 1e-9,
                "700?????????????0.50????");
            Near(0.300, result.Profile.Nodes[2].PipeBottomDepth, 1e-9,
                "?????????????????");
            Near(36.59, result.Profile.Nodes[2].CumulativeDistance, 1e-9,
                "??????");

            LongitudinalProfileBuildResult reversed =
                LongitudinalProfileCalculator.BuildBetweenNodes(
                    pipes, wells, "W-53", "W-51");
            True(reversed.Success, reversed.Message);
            Equal("W-53", reversed.Profile.Nodes[0].NodeNo,
                "???????????????????");
            Equal("W-51", reversed.Profile.Nodes[2].NodeNo,
                "???????????????????");

            LongitudinalProfileBuildResult reversedSingle =
                LongitudinalProfileCalculator.BuildBetweenNodes(
                    new[] { pipes[1] }, wells, "W-52", "W-51");
            True(reversedSingle.Success, reversedSingle.Message);
            Equal("W-52", reversedSingle.Profile.Nodes[0].NodeNo,
                "??????????????????????");

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
            False(disconnected.Success, "?????????");

            var settings = new LongitudinalProfileSettings
            {
                HeaderWidth = -1,
                HorizontalScale = 0,
                VerticalScale = double.NaN,
                Rows = new List<LongitudinalProfileRowSettings>()
            };
            settings.Normalize();
            Near(45.0, settings.HeaderWidth, 1e-9,
                "??????????");
            Near(6.0, settings.HeaderTextHeight, 1e-9,
                "???????????????");
            Equal("??", settings.HeaderTextStyleName,
                "???????????????");
            Near(5.0, settings.HorizontalGridInterval, 1e-9,
                "???????????????");
            Equal("CDBox-???", settings.LayerName,
                "?????????????");
            Equal(8, settings.Rows.Count,
                "???????????????");
            Equal("PipeFoundation", settings.Rows[6].Key,
                "???????????");
            True(settings.Rows.TrueForAll(x => x.TextStyleName == "??"),
                "????????????????");

            LongitudinalProfileLayout layout =
                LongitudinalProfileLayoutCalculator.Calculate(
                    result.Profile, settings);
            Near(0.5, layout.HorizontalFactor, 1e-9,
                "??1:1000????????0.5?");
            Near(5.0, layout.VerticalFactor, 1e-9,
                "??1:100????????5?");
            Near(22.5, layout.HeaderRight, 1e-9,
                "???????????????");
            Near(42.5, layout.TableTop, 1e-9,
                "????????");
            Near(35.0, layout.Row("GroundElevation").Bottom, 1e-9,
                "?????????????????????");
            Near(0.0, layout.Row("WellNumber").Bottom, 1e-9,
                "?????????????");
            Near(43.295, layout.DataRight, 1e-9,
                "??????????????");
            Near(45.0, layout.PlotRight, 1e-9,
                "??????????????");

            string standalone =
                CDBoxStudioLongitudinalProfileSettingsPage
                    .BuildStandaloneDocument(new CDBoxStudioSettings());
            string embedded =
                CDBoxStudioLongitudinalProfileSettingsPage
                    .BuildEmbeddedSection(new CDBoxStudioSettings());
            Contains(standalone, "?????",
                "?????????????WebView2??");
            Contains(standalone, "??????",
                "????????????");
            Contains(standalone, "????",
                "??????????");
            Contains(embedded, "longitudinalProfileSettingsFrame",
                "???????????????");
            False(standalone.Contains("???"),
                "?????????????");
            False(standalone.Contains("????"),
                "??????????????");
        }

        private static void TestAnnotationHudTextComposition()
        {
            Equal("?????12.50m", PipeLengthAnnotationTextComposer.Compose("?????", "12.50m"),
                "????????????????");

            string user;
            string system;
            PipeLengthAnnotationTextComposer.SplitLegacyText("?????12.50m", 12.5, out user, out system);
            Equal("?????", user, "??????????");
            Equal("12.50m", system, "??????????");
            Equal("9.20m", PipeLengthAnnotationTextComposer.FormatLike(9.2, system),
                "??????????????");
            Equal("9.20", PipeLengthAnnotationTextComposer.FormatLike(9.2, "12.50"),
                "????????????????");
            Equal("9.200m", PipeLengthAnnotationTextComposer.FormatWithDecimals(9.2, 3, system),
                "????????????????");
            Equal("9.2m", PipeLengthAnnotationTextComposer.FormatWithDecimals(9.2, 1, system),
                "??????????????");

            string frozen = "?????12.50m????12.50m";
            Equal("?????12.50m????",
                PipeLengthAnnotationTextComposer.RemoveDetachedLengthToken(frozen, "12.50m"),
                "???????????????????");

            PipeLengthAnnotationTextComposer.SplitLegacyText("????", 12.5, out user, out system);
            Equal("????", user, "??????????????");
            Equal(string.Empty, system, "???????????????");

            PipeLengthAnnotationTextComposer.SplitLegacyText("DN110 ?????110.00m", 110.0,
                out user, out system);
            Equal("DN110 ?????", user, "???????????????????");
            Equal("110.00m", system, "???????????");
            Equal("????9.20m??1.50m??2.00m",
                PipeLengthAnnotationTextComposer.ReplaceDerivedLengthToken(
                    "????12.50m??1.50m??2.00m", "12.50m", "9.20m"),
                "???????????????????");
            Equal("????9.20m??12.50m??2.00m",
                PipeLengthAnnotationTextComposer.ReplaceDerivedLengthToken(
                    "????12.50m??12.50m??2.00m", "12.50", "9.20"),
                "??????????????????");
            Equal("?12.50m??9.20m",
                PipeLengthAnnotationTextComposer.ReplaceDerivedLengthToken(
                    "?12.50m??12.50m", "12.50", "9.20"),
                "???????????????????????");

            List<string> bottomLines = PipeLengthAnnotationTextComposer
                .SplitBottomLines("???\r\n???\\P???");
            Equal(3, bottomLines.Count, "????????????????");
            Equal("???", bottomLines[1], "?????????????");
        }

        private static void TestSpecialObjectsSkipQualityCheck()
        {
            True(QuantityDashboardClassification.ShouldIncludeInQualityCheck(null),
                "???????????????");
            True(QuantityDashboardClassification.ShouldIncludeInQualityCheck(
                new QuantityPipeAttributes()), "???????????????");
            False(QuantityDashboardClassification.ShouldIncludeInQualityCheck(
                new QuantityPipeAttributes { IsSpecialObject = true }),
                "??????????????????????");
        }

        private static void TestNodeAnnotationTextComposition()
        {
            Dictionary<string, string> normal = NodeAnnotationTextComposer.Compose(
                "J12", 2.345, 1.2, false);
            Equal("J12", normal["NodeNo"], "???????????");
            Equal("??:2.35m", normal["WellDepth"], "???????????");
            Equal("??:1.20m", normal["ShaftLength"], "???????????");
            False(normal.ContainsKey("WellType"), "???????????????");

            Dictionary<string, string> silt = NodeAnnotationTextComposer.Compose(
                string.Empty, 3.0, 2.0, true);
            Equal("???", silt["NodeNo"], "??????????????");
            Equal("???", silt["WellType"], "??????????????");
        }

        private static void TestBuiltInUpdateSourcePriority()
        {
            IList<CDBoxStudioUpdateSource> sources = CDBoxStudioUpdateSourceCatalog.CreateManifestSources();
            Equal(3, sources.Count, "??????? update.json ???");
            Equal("Gitee", sources[0].Name, "Gitee ???????");
            Equal(CDBoxStudioUpdateSourceCatalog.GiteeManifestUrl, sources[0].Url, "Gitee ??");
            Equal("GitCode", sources[1].Name, "GitCode ???????");
            Equal(CDBoxStudioUpdateSourceCatalog.GitCodeManifestUrl, sources[1].Url, "GitCode ??");
            Equal("GitHub", sources[2].Name, "GitHub ???????");
            Equal(CDBoxStudioUpdateSourceCatalog.GitHubManifestUrl, sources[2].Url, "GitHub ??");
            for (int i = 0; i < sources.Count; i++) True(sources[i].Enabled, "?????????");
        }

        private static void TestColorPickerIntegration()
        {
            byte red;
            byte green;
            byte blue;
            CDBoxColorConverter.AciToRgb(1, out red, out green, out blue);
            Equal((byte)255, red, "ACI 1 ????");
            Equal((byte)0, green, "ACI 1 ????");
            Equal((byte)0, blue, "ACI 1 ????");
            CDBoxColorConverter.AciToRgb(12, out red, out green, out blue);
            Equal((byte)204, red, "ACI 12 ??????? AutoCAD ????");
            Equal((byte)0, green, "ACI 12 ??????? AutoCAD ????");
            Equal((byte)0, blue, "ACI 12 ??????? AutoCAD ????");
            Equal(1, CDBoxColorConverter.RgbToNearestAci(255, 0, 0), "??????? ACI 1");
            True(CDBoxColorConverter.TryParseHex("#0A80FF", out red, out green, out blue), "HEX ????");
            Equal((byte)10, red, "HEX ????");
            Equal((byte)128, green, "HEX ????");
            Equal((byte)255, blue, "HEX ????");

            CDBoxColor preserved = CDBoxColorOutputResolver.Resolve(
                CDBoxColor.FromRgb(250, 20, 20), CDBoxColor.FromIndex(3),
                CDBoxColorOutputMode.PreserveOriginalType);
            Equal(CDBoxColorType.IndexColor, preserved.Type, "?????????? ACI");
            CDBoxColor trueColor = CDBoxColorOutputResolver.Resolve(
                CDBoxColor.FromIndex(1), CDBoxColor.FromIndex(3),
                CDBoxColorOutputMode.PreferTrueColor);
            Equal(CDBoxColorType.TrueColor, trueColor.Type, "????????????");
            CDBoxColor standard = CDBoxColorOutputResolver.Resolve(
                CDBoxColor.FromRgb(239, 68, 68), CDBoxColor.FromIndex(7),
                CDBoxColorOutputMode.CDBoxStandard);
            Equal(CDBoxColorType.CDBoxStandard, standard.Type, "???????? CDBox ???");
            Equal("????", standard.DisplayName, "???????????????");

            string annotationScript = CDBoxStudioAnnotationSettingsPage.BuildComponentScript();
            Contains(annotationScript, "data-color-picker", "???????????????");
            Contains(annotationScript, "openAnnotationColorPicker", "??????????????");
            Contains(annotationScript, "CDBoxAnnotationColorSelected", "?????????????");
            False(annotationScript.Contains("<select class=\"as-select\" data-color-select"), "??????????");

            string layerScript = CDBoxStudioLayerManagerPage.BuildComponentScript();
            Contains(layerScript, "data-layer-color", "????????????");
            Contains(layerScript, "openLayerColorPicker", "???????????????");
            Contains(CDBoxStudioLayerManagerPage.BuildStyles(false), "lm-color-button", "???????????");

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
            Contains(pickerPage, "confirmColorPicker", "?????????? WebView2 ????");
            Contains(pickerPage, "browseCadColorBook", "???????? AutoCAD ??????");
            Contains(pickerPage, "CDBoxColorPickerFromCad", "????????????????");
            Contains(pickerPage, "data-theme=", "???????? Studio ??");
        }

        private static void TestStructuredLayerRecognition()
        {
            var rules = new List<LayerRecognitionRule>
            {
                new LayerRecognitionRule
                {
                    Name = "??????",
                    Priority = 200,
                    MatchMode = LayerRecognitionEngine.MatchModeKeywords,
                    Pattern = "300,??",
                    ParentGroup = "??",
                    StopAfterMatch = true
                },
                new LayerRecognitionRule
                {
                    Name = "??????",
                    Priority = 200,
                    MatchMode = "???",
                    Pattern = "*110PVC*",
                    ParentGroup = "??",
                    StopAfterMatch = true
                }
            };

            LayerRecognitionResult main = LayerRecognitionEngine.Recognize("300????????", rules);
            Equal("??", main.Metadata.ParentGroup, "300 ??????????");
            Equal("DN300", main.Attributes.Specification, "???? DN ????");
            Equal("???", main.Attributes.Material, "???????????");
            Equal("?????", main.Attributes.ConstructionType, "????????????");
            True(main.Confidence >= 0.8, "??????????????");

            LayerRecognitionResult reordered = LayerRecognitionEngine.Recognize("???300???", rules);
            Equal("DN300", reordered.Attributes.Specification, "???????????");
            Equal("??", reordered.Metadata.ParentGroup, "???????????????????");

            LayerRecognitionResult standard = LayerRecognitionEngine.Recognize(
                "??-DN300-???-?????", rules);
            Equal("??", standard.Metadata.ParentGroup, "?????????");
            Equal("??", standard.Attributes.ObjectType, "??????????");
            Equal(LayerRecognitionStatuses.Standard, standard.Status, "?????????????");

            LayerRecognitionResult branch = LayerRecognitionEngine.Recognize("??110PVC??", rules);
            Equal("??", branch.Metadata.ParentGroup, "??????????");
            Equal("DN110", branch.Attributes.Specification, "???????");
            Equal("PVC", branch.Attributes.Material, "???????");
            Equal("??", branch.Attributes.ConstructionType, "?????????");

            LayerRecognitionResult well = LayerRecognitionEngine.Recognize("700????", rules);
            Equal("?", well.Metadata.ParentGroup, "??????");
            Equal("D700", well.Attributes.Specification, "????????????");
            Equal(LayerRecognitionStatuses.Incomplete, well.Status, "?????????????");
            Contains(well.Explanation, "??", "???????????");

            LayerRecognitionResult node = LayerRecognitionEngine.Recognize("?-???-D700-??-???", rules);
            Equal("???", node.Attributes.NodeType, "??????");
            Equal("D700", node.Attributes.Specification, "??????? D ??");

            LayerRecognitionResult structure = LayerRecognitionEngine.Recognize("C25???(15cm)", rules);
            Equal("???", structure.Metadata.ParentGroup, "????????????");
            Equal("C25", structure.Attributes.StrengthGrade, "????????");
            Equal("T150", structure.Attributes.Thickness, "15cm ???? T150");

            LayerRecognitionResult facility = LayerRecognitionEngine.Recognize("???2m?", rules);
            Equal("???", facility.Metadata.ParentGroup, "?????????");
            Equal("???", facility.Metadata.ParentClass, "??????????");
            Equal("V2m?", facility.Attributes.Volume, "????????");

            LayerRecognitionResult annotation = LayerRecognitionEngine.Recognize("ZJ", rules);
            Equal("??", annotation.Metadata.ParentGroup, "ZJ ?????");
            Equal("????", annotation.Metadata.ParentClass, "ZJ ????????");

            LayerRecognitionResult mainAnnotation = LayerRecognitionEngine.Recognize("????", rules);
            Equal("??", mainAnnotation.Metadata.ParentGroup, "???????????");
            Equal("??????", mainAnnotation.Metadata.ParentClass, "??????????????");

            LayerRecognitionResult branchAnnotation = LayerRecognitionEngine.Recognize("????", rules);
            Equal("??", branchAnnotation.Metadata.ParentGroup, "???????????");
            Equal("??????", branchAnnotation.Metadata.ParentClass, "??????????????");

            LayerRecognitionResult point = LayerRecognitionEngine.Recognize("????", rules);
            Equal("??", point.Metadata.ParentGroup, "?????????");
            Equal("??", point.Attributes.Purpose, "?????????");

            LayerRecognitionResult unknown = LayerRecognitionEngine.Recognize("????1", rules);
            Equal(LayerRecognitionStatuses.Unrecognized, unknown.Status, "??????????????");
            True(unknown.NeedsConfirmation, "?????????????");

            LayerRecognitionResult excluded = LayerRecognitionEngine.Recognize(
                "300?????",
                new[]
                {
                    new LayerRecognitionRule
                    {
                        MatchMode = LayerRecognitionEngine.MatchModeKeywords,
                        Pattern = "300,??",
                        ExcludePattern = "??",
                        ParentGroup = "??"
                    }
                });
            Equal(0, excluded.MatchedRules.Count, "????????????");

            LayerRecognitionResult templated = LayerRecognitionEngine.Recognize(
                "??-DN450-HDPE-??",
                new[]
                {
                    new LayerRecognitionRule
                    {
                        Name = "??????",
                        MatchMode = LayerRecognitionEngine.MatchModeTemplate,
                        Pattern = "??-{DN}-{Material}-*",
                        ParentGroup = "??"
                    }
                });
            Equal(1, templated.MatchedRules.Count, "????????????");
            Equal("??", templated.Metadata.ParentGroup, "?????????");
        }

        private static void TestEffectiveLengthAndDefaultClone()
        {
            QuantityPipeAttributes attrs = QuantityPipeAttributes.DefaultMainPipe;
            attrs.UseManualLength = true;
            attrs.ManualLength = 12.5;
            attrs.StartNode = "W1";
            attrs.EndNode = "W2";
            attrs.NodeNo = "W1";
            attrs.Remark = "????";

            Near(12.5, attrs.EffectiveLength(9.0), 1e-9, "???????");
            QuantityPipeAttributes profile = attrs.CloneForDefaultProfile();
            Equal(string.Empty, profile.StartNode, "??????????");
            Equal(string.Empty, profile.EndNode, "??????????");
            Equal(string.Empty, profile.NodeNo, "???????????");
            False(profile.UseManualLength, "???????????");
            Near(0.0, profile.ManualLength, 1e-9, "???????????");

            profile.Material = "????";
            False(string.Equals(attrs.Material, profile.Material, StringComparison.Ordinal), "??????????");
        }

        private static void TestStructureLayers()
        {
            var attrs = new QuantityPipeAttributes
            {
                BackfillStructure = "C25??? 0.25 ??????? 0.10 ???????? 0.80 ????????? 0.15 ??"
            };

            QuantityPipeAttributes.ApplyStructureLayerText(attrs);
            Near(0.25, attrs.C25RestoreThickness, 1e-9, "C25 ??");
            Near(0.10, attrs.GravelCushionThickness, 1e-9, "????");
            Near(0.15, attrs.SandCushionThickness, 1e-9, "?????");

            List<QuantityStructureLayer> layers = QuantityStructureLayer.Parse(attrs.BackfillStructure);
            Equal(4, layers.Count, "?????");
            True(layers[2].IsPipeLayer, "?????");
            True(QuantityStructureLayer.IsSandBackfill(layers[2]), "???????");
            True(QuantityStructureLayer.IsSandCushion(layers[3]), "???????");
            List<QuantityStructureLayer> typedLayers = QuantityStructureLayer.Parse(
                "????? 0.80 ???\n????? 0.15 ?? ??");
            True(typedLayers[0].IsPipeLayer, "????????????");
            True(typedLayers[1].IsCushionLayer, "???????????");
            False(typedLayers[1].IsPipeLayer, "???????????");
            Near(0.15, QuantityStructureLayer.ResolvePipeCushionHeight(typedLayers, 9.0), 1e-9,
                "?????????????");
            True(QuantityStructureLayer.Serialize(typedLayers, false).IndexOf("??", StringComparison.Ordinal) >= 0,
                "?????????????");
            QuantityStructureLayer legacyBelowWell = QuantityStructureLayer.Parse("???? 0.10 ?? ???")[0];
            True(legacyBelowWell.IsBelowWellLayer && legacyBelowWell.IsCushionLayer,
                "?????????????");
            QuantityStructureLayer legacyPipeCushion = QuantityStructureLayer.Parse("????? 0.15 ?? ???")[0];
            True(legacyPipeCushion.IsCushionLayer && !legacyPipeCushion.IsPipeLayer,
                "?????????????????");
            string serializedNodeLayers = QuantityStructureLayer.Serialize(
                new[] { legacyBelowWell }, true);
            True(serializedNodeLayers.IndexOf("??", StringComparison.Ordinal) >= 0,
                "?????????????");
            False(serializedNodeLayers.IndexOf("???", StringComparison.Ordinal) >= 0,
                "???????????????");
            Equal("C25???", QuantityStructureLayer.Parse("C25??? 0.15 ??")[0].Name, "????????????????");
            Equal("C25???", QuantityStructureLayer.Parse("C ??? 0.15 ??")[0].Name, "????? C25 ???????");

            QuantityStructureLayer sandEncasement = QuantityStructureLayer.Parse("????? 0.60 ???")[0];
            True(QuantityStructureLayer.IsSandBackfill(sandEncasement), "?????????????");
            False(QuantityStructureLayer.IsConcretePipeEncasement(sandEncasement), "????????? C25 ???");
            QuantityStructureLayer concreteEncasement = QuantityStructureLayer.Parse("C25??? 0.30 ???")[0];
            True(QuantityStructureLayer.IsConcretePipeEncasement(concreteEncasement), "??????????? C25 ???");
            False(QuantityStructureLayer.IsC25Restore(concreteEncasement), "C25 ????????? C25 ??");

            var noConcrete = new QuantityPipeAttributes
            {
                C25RestoreThickness = 0.25,
                BackfillStructure = "????? 0.80 ???\n????? 0.15 ??"
            };
            QuantityPipeAttributes.ApplyStructureLayerText(noConcrete);
            Near(0.0, noConcrete.C25RestoreThickness, 1e-9, "??????? C25 ?????????");

            List<QuantityStructureLayer> withoutBackfill = QuantityStructureLayer.Parse("C25??? 0.30 ???\n????? 0.10 ??");
            Near(0.60, QuantityEngineeringMath.CalculatePipeRemainingBackfillHeight(1.00, withoutBackfill), 1e-9, "???????????????????");
            Near(1.10, QuantityEngineeringMath.CalculateEarthworkOut(0.10, 1.00, 0.0), 1e-9, "?????????????");
            Near(0.70, QuantityEngineeringMath.CalculateEarthworkOut(0.10, 1.00, 0.40), 1e-9, "???????????????");
        }

        private static void TestQuantityPipeClassification()
        {
            Equal("DN110PVC?", QuantityDashboardClassification.BuildPipeType("110", "PVC"), "??????????????");
            Equal("DN110PVC?", QuantityDashboardClassification.BuildPipeType("DN110", "PVC?"), "?????????");
            Equal("DN110PE?", QuantityDashboardClassification.BuildPipeType("DN110", "PE"), "???????????");
        }

        private static void TestSiltWellDepth()
        {
            QuantityPipeAttributes well = QuantityPipeAttributes.DefaultNodeWell;
            well.WellType = "???";
            well.WellSpec = "?700";
            well.WellDepth = 2.0;
            well.SiltWellDeductDepth700 = 0.50;

            Near(0.50, QuantityPipeAttributes.GetSiltWellDeductDepth(well), 1e-9, "?700 ?????");
            Near(1.65, QuantityPipeAttributes.CalculatePipeExcavationDepthByWell(well, 0.15, 9.0), 1e-9, "????");
            Near(1.50, QuantityPipeAttributes.CalculatePipeExcavationDepthByWell(well, 9.0), 1e-9, "???????????????");
        }

        private static void TestQuantityDependencyRules()
        {
            QuantityPipeAttributes pipe = QuantityPipeAttributes.DefaultMainPipe;
            pipe.StartNode = "W1";
            pipe.EndNode = "W2";
            QuantityPipeAttributes startWell = QuantityPipeAttributes.DefaultNodeWell;
            startWell.NodeNo = "W1";
            startWell.WellDepth = 1.00;
            startWell.WellType = "???";
            QuantityPipeAttributes endWell = QuantityPipeAttributes.DefaultNodeWell;
            endWell.NodeNo = "W2";
            endWell.WellDepth = 1.20;
            endWell.WellType = "???";
            endWell.WellSpec = "?500";

            QuantityDependencyResult main = QuantityDependencyService.NormalizeDraft(
                pipe,
                QuantityStructureLayer.Parse(pipe.BackfillStructure),
                pipe,
                startWell,
                endWell,
                null,
                "Load");
            Near(1.15, main.Attributes.StartDepth, 1e-9, "?????????????????");
            Near(1.15, main.Attributes.EndDepth, 1e-9, "????????????");
            Near(1.15, main.Attributes.AverageDepth, 1e-9, "????????????");

            QuantityPipeAttributes well = QuantityPipeAttributes.DefaultNodeWell;
            well.WellDepth = 0.46;
            well.BackfillStructure = "????C25?? 0.30 ??\n???????? 0.10 ??\n????? 0.80\n????? 0.10 ?? ???";
            QuantityDependencyResult node = QuantityDependencyService.NormalizeDraft(
                well,
                QuantityStructureLayer.Parse(well.BackfillStructure),
                well,
                null,
                null,
                null,
                "WellDepth");
            QuantityStructureLayer backfill = node.Layers.Find(x => QuantityStructureLayer.IsSandBackfill(x));
            Near(0.06, backfill.Height, 1e-9, "?????????????????");
            Near(0.56, node.RealExcavationDepth, 1e-9, "?????????????");

            well.WellDepth = 0.20;
            QuantityDependencyResult negative = QuantityDependencyService.NormalizeDraft(
                well,
                QuantityStructureLayer.Parse(well.BackfillStructure),
                well,
                null,
                null,
                null,
                "WellDepth");
            True(negative.Layers.Find(x => QuantityStructureLayer.IsSandBackfill(x)).Height < 0, "?????????????");
            True(negative.Warnings.Count > 0, "???????????");

            QuantityPipeAttributes manualPipe = QuantityPipeAttributes.DefaultMainPipe;
            manualPipe.StartDepth = 1.20;
            manualPipe.EndDepth = 1.40;
            manualPipe.BackfillStructure = "????? 0.90\n????? 0.10 ?? ???";
            QuantityDependencyResult manualDepth = QuantityDependencyService.NormalizeDraft(
                manualPipe,
                QuantityStructureLayer.Parse(manualPipe.BackfillStructure),
                manualPipe,
                null,
                null,
                null,
                "StartDepth");
            Near(1.30, manualDepth.Attributes.AverageDepth, 1e-9, "???????????????????");
            Near(1.20, manualDepth.Layers.Find(x => QuantityStructureLayer.IsSandBackfill(x)).Height, 1e-9, "?????????????");

            QuantityPipeAttributes precisionPipe = QuantityPipeAttributes.DefaultMainPipe;
            precisionPipe.StartDepth = 1.234;
            precisionPipe.EndDepth = 1.238;
            precisionPipe.BackfillStructure = "????? 0.00\n????? 0.10 ?? ??";
            QuantityDependencyResult precisionResult = QuantityDependencyService.NormalizeDraft(
                precisionPipe,
                QuantityStructureLayer.Parse(precisionPipe.BackfillStructure),
                precisionPipe,
                null,
                null,
                null,
                "StartDepth");
            Near(1.234, precisionResult.Attributes.StartDepth, 1e-9, "????????????????");
            Near(1.238, precisionResult.Attributes.EndDepth, 1e-9, "????????????????");
            Near(1.24, precisionResult.Attributes.AverageDepth, 1e-9, "????????????????");
            Near(1.14, precisionResult.Layers.Find(x => QuantityStructureLayer.IsSandBackfill(x)).Height, 1e-9,
                "??????????????????");

            QuantityPipeAttributes typedWell = QuantityPipeAttributes.DefaultNodeWell;
            typedWell.WellDepth = 0.46;
            typedWell.BackfillStructure =
                "????? 0.20 ??\n????? 0.10 ?? ???\n???? 0.15 ?? ??";
            QuantityDependencyResult typedWellResult = QuantityDependencyService.NormalizeDraft(
                typedWell,
                QuantityStructureLayer.Parse(typedWell.BackfillStructure),
                typedWell,
                null,
                null,
                null,
                "WellDepth");
            Near(0.61, typedWellResult.RealExcavationDepth, 1e-9,
                "????????????????????");

            QuantityPipeAttributes diameter700Well = QuantityPipeAttributes.DefaultNodeWell;
            diameter700Well.WellSpec = "?700";
            QuantityDependencyResult diameter700Result = QuantityDependencyService.NormalizeDraft(
                diameter700Well,
                QuantityStructureLayer.Parse(diameter700Well.BackfillStructure),
                QuantityPipeAttributes.DefaultNodeWell,
                null,
                null,
                null,
                "WellSpec");
            Near(1.5, diameter700Result.Attributes.ExcavationLength, 1e-9,
                "700 ???????? 1.5m ????");
            Near(1.5, diameter700Result.Attributes.ExcavationWidth, 1e-9,
                "700 ???????? 1.5m ????");

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
                "500 ????????????????");
            Near(1.3, diameter500Result.Attributes.ExcavationWidth, 1e-9,
                "500 ????????????????");

            QuantityPipeAttributes changedBackTo500 = QuantityPipeAttributes.DefaultNodeWell;
            changedBackTo500.WellSpec = "?500";
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
                "???? 700 ?? 500 ????? 1.3m ????");
            Near(1.3, changedBackTo500Result.Attributes.ExcavationWidth, 1e-9,
                "???? 700 ?? 500 ????? 1.3m ????");

            QuantityPipeAttributes oldPipe = QuantityPipeAttributes.DefaultMainPipe;
            oldPipe.StartDepth = 1.10;
            oldPipe.EndDepth = 1.10;
            oldPipe.BackfillStructure = "????? 1.00\n????? 0.10 ?? ???";
            List<QuantityStructureLayer> changedLayers = QuantityStructureLayer.Parse("????? 1.00\n????? 0.20 ?? ???");
            QuantityDependencyResult cushionChanged = QuantityDependencyService.NormalizeDraft(
                oldPipe,
                changedLayers,
                oldPipe,
                null,
                null,
                null,
                "StructureLayers");
            Near(1.10, cushionChanged.Attributes.StartDepth, 1e-9, "?????????????????");
            Near(1.10, cushionChanged.Attributes.EndDepth, 1e-9, "?????????????????");
            Near(0.90, cushionChanged.Layers.Find(x => QuantityStructureLayer.IsSandBackfill(x)).Height, 1e-9, "???????????????????");

            QuantityPipeAttributes undersizedPipeLayer = QuantityPipeAttributes.DefaultMainPipe;
            undersizedPipeLayer.StartNode = "W1";
            undersizedPipeLayer.EndNode = "W2";
            undersizedPipeLayer.StartDepth = 1.00;
            undersizedPipeLayer.EndDepth = 1.00;
            undersizedPipeLayer.PipeOuterDiameter = 0.30;
            List<QuantityStructureLayer> undersizedLayers = QuantityStructureLayer.Parse(
                "????? 0.10 ???\n????? 0.10 ?? ??");
            QuantityDependencyResult undersizedResult = QuantityDependencyService.NormalizeDraft(
                undersizedPipeLayer,
                undersizedLayers,
                undersizedPipeLayer,
                null,
                null,
                null,
                "StructureLayers");
            True(undersizedResult.Warnings.Exists(x => x.IndexOf("??????",
                StringComparison.Ordinal) >= 0), "??????????????????????");

            QuantityPipeAttributes special = QuantityPipeAttributes.DefaultMainPipe;
            special.IsSpecialObject = true;
            special.StartDepth = 9.0;
            special.EndDepth = 8.0;
            special.AverageDepth = 0.256;
            special.PipeOuterDiameter = 0.30;
            special.BackfillStructure = "????? 0.25 ???";
            QuantityDependencyResult specialResult = QuantityDependencyService.NormalizeDraft(
                special,
                QuantityStructureLayer.Parse(special.BackfillStructure),
                special,
                startWell,
                endWell,
                null,
                "AverageDepth");
            Near(9.0, specialResult.Attributes.StartDepth, 1e-9, "?????????????????");
            Near(8.0, specialResult.Attributes.EndDepth, 1e-9, "?????????????????");
            Near(0.26, specialResult.Attributes.AverageDepth, 1e-9, "???????????????????");
            Near(0.26, specialResult.Layers[0].Height, 1e-9, "???????????????????");
            True(specialResult.Warnings.Exists(x => x.IndexOf("??????", StringComparison.Ordinal) >= 0), "???????????????");

            QuantityPipeAttributes specialBranch = QuantityPipeAttributes.DefaultBranchPipe;
            specialBranch.IsSpecialObject = true;
            specialBranch.BranchType = "??";
            specialBranch.BranchIncludeInCalculation = true;
            QuantityDependencyResult specialBranchResult = QuantityDependencyService.NormalizeDraft(
                specialBranch,
                QuantityStructureLayer.Parse("?????? 0.18"),
                specialBranch,
                null,
                null,
                QuantityPipeAttributes.DefaultBranchPipe,
                "BranchType");
            True(specialBranchResult.Attributes.BranchIncludeInCalculation, "????????????????");
            Equal(1, specialBranchResult.Layers.Count, "???????????????");

            QuantityPipeAttributes exposed = QuantityPipeAttributes.DefaultBranchPipe;
            exposed.BranchType = "??";
            QuantityDependencyResult emptyBranch = QuantityDependencyService.NormalizeDraft(
                exposed,
                new List<QuantityStructureLayer>(),
                exposed,
                null,
                null,
                null,
                "BranchType");
            Equal(0, emptyBranch.Layers.Count, "????????????");
            False(emptyBranch.Attributes.BranchIncludeInCalculation, "???????????");

            QuantityPipeAttributes soilBranch = QuantityPipeAttributes.DefaultBranchPipe;
            soilBranch.BranchType = "????";
            soilBranch.BranchDepth = 0.85;
            QuantityDependencyResult soil = QuantityDependencyService.NormalizeDraft(
                soilBranch,
                new List<QuantityStructureLayer>(),
                soilBranch,
                null,
                null,
                null,
                "BranchType");
            Equal(1, soil.Layers.Count, "????????????");
            Near(0.85, soil.Layers[0].Height, 1e-9, "?????????????");
        }

        private static void TestPrimitiveParsing()
        {
            True(QuantityPipeAttributes.ParseBool("?", false), "????");
            False(QuantityPipeAttributes.ParseBool("0", true), "????");
            Near(1.25, QuantityPipeAttributes.ParseDouble("1.25", 0.0), 1e-9, "????");
            Near(7.0, QuantityPipeAttributes.ParseDouble("invalid", 7.0), 1e-9, "??????");
        }

        private static void TestStudioRouteRequest()
        {
            CDBoxStudioRouteRequest current = CDBoxStudioRouteRequest.Parse("studio|run|module%3Alayer-manager");
            Equal("run", current.Name, "??????");
            Equal("module:layer-manager", current.Argument, "????????");

            CDBoxStudioRouteRequest legacy = CDBoxStudioRouteRequest.Parse("filter:???");
            Equal("filter", legacy.Name, "?????");
            Equal("???", legacy.Argument, "?????");
        }

        private static void TestStageANewInstallDefaults()
        {
            CDBoxAppSettings settings = CDBoxAppSettings.Default;
            True(settings.PromptInstallOnLoad, "??????????");
            True(settings.ShouldPromptForInstall(false, "release:30101"), "??????????????");
            False(settings.ShouldPromptForInstall(true, "release:30101"), "???????????????");
            settings.PromptInstallOnLoad = false;
            settings.LastInstallPromptIdentity = "release:30101";
            False(settings.ShouldPromptForInstall(false, "release:30101"), "????????????????");
            True(settings.ShouldPromptForInstall(false, "release:30102"), "?????????????????????");
        }

        private static void TestQuantityDashboardSharedPage()
        {
            string embedded = CDBoxStudioQuantityDashboardPage.BuildEmbeddedSection();
            string standalone = CDBoxStudioQuantityDashboardPage.BuildStandaloneDocument("fresh", false, "test.log");

            True(embedded.IndexOf("quantityDashboardPage", StringComparison.Ordinal) >= 0, "?????????????");
            True(standalone.IndexOf("CDBoxQuantityDashboardPage.create", StringComparison.Ordinal) >= 0, "???????????????");
            True(standalone.IndexOf("standalone:true", StringComparison.Ordinal) >= 0, "????????????");
            True(standalone.IndexOf("data-theme=\"fresh\"", StringComparison.Ordinal) >= 0, "?????? Studio ??");
            True(standalone.IndexOf("class=\"no-animations\"", StringComparison.Ordinal) >= 0, "??????????");
            False(standalone.IndexOf("??????????", StringComparison.Ordinal) >= 0, "???????????");
            False(standalone.IndexOf("?????????????", StringComparison.Ordinal) >= 0, "????????????");
            False(standalone.IndexOf("??????????", StringComparison.Ordinal) >= 0, "?????????????");
            True(standalone.IndexOf("style=\"display:none\"><div class=\"qd-panel-head\"><div><h3>???????", StringComparison.Ordinal) >= 0, "???????????");
            False(standalone.IndexOf("<small>" + "'+html(sub)", StringComparison.Ordinal) >= 0, "????????????");
            True(standalone.IndexOf("????", StringComparison.Ordinal) >= 0, "???????????????");
            True(standalone.IndexOf("??????", StringComparison.Ordinal) >= 0, "?????????????");
            True(standalone.IndexOf("??????", StringComparison.Ordinal) >= 0, "????????????????");
            True(standalone.IndexOf("exportQuantityCalculationProcess", StringComparison.Ordinal) >= 0, "???????????????");
        }

        private static void TestQuantityCalculationProcessExport()
        {
            string root = NewTemporaryDirectory("quantity-audit");
            try
            {
                var snapshot = new QuantityDashboardSnapshot();
                snapshot.document.name = "????.dwg";
                snapshot.scope.regionName = "????";
                snapshot.status.updatedAt = "2026-07-20 12:00:00";
                snapshot.referenceItems.Add(new QuantityDashboardReferenceItem { item = "????", quantity = 1.23456789, unit = "m?", source = QuantityDashboardSources.Property });
                var pipe = new QuantityMainPipeCalculationRow
                {
                    Index = 1,
                    HandleText = "A1",
                    LayerName = "W-PIPE",
                    StartNode = "W1",
                    EndNode = "W2",
                    Length = 12.3456789,
                    MechanicalExcavation = 1.23456789,
                    DataStatus = "??"
                };
                pipe.CalculationSteps.Add(new QuantityCalculationStep
                {
                    ItemName = "????",
                    Formula = "?? ? ?? ? ??",
                    Substitution = "12.3456789 ? 0.8 ? 1.2",
                    Result = 1.23456789,
                    Unit = "m?"
                });
                snapshot.calculationAudit.mainPipes.Add(pipe);

                string path = Path.Combine(root, "????.xlsx");
                QuantityDashboardExportService.ExportCalculationProcess(path, snapshot);
                True(File.Exists(path) && new FileInfo(path).Length > 0, "????????????");
                using (ZipArchive archive = ZipFile.OpenRead(path))
                {
                    True(archive.GetEntry("xl/workbook.xml") != null, "???????? xlsx ???");
                    True(archive.GetEntry("xl/worksheets/sheet4.xml") != null, "????????????????");
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

            True(embedded.IndexOf("quantityAttributeEditorPage", StringComparison.Ordinal) >= 0, "???????????????");
            True(standalone.IndexOf("CDBoxQuantityAttributeEditorPage.create", StringComparison.Ordinal) >= 0, "?????????????");
            True(standalone.IndexOf("standalone:true", StringComparison.Ordinal) >= 0, "??????????????");
            True(standalone.IndexOf("3.4.1", StringComparison.Ordinal) >= 0, "????? 3.4.1 ??");
            True(standalone.IndexOf("data-theme=\"dark\"", StringComparison.Ordinal) >= 0, "????????????");
            True(standalone.IndexOf("qa-structure", StringComparison.Ordinal) >= 0, "???????????");
            True(standalone.IndexOf("data-layer", StringComparison.Ordinal) >= 0, "???????????????");
            True(standalone.IndexOf("bindLayerDrag", StringComparison.Ordinal) >= 0, "??????????");
            True(standalone.IndexOf("['IsSpecialObject','????','bool']", StringComparison.Ordinal) >= 0, "??????????????");
            True(standalone.IndexOf("!self.attrs.IsSpecialObject", StringComparison.Ordinal) >= 0, "???????????");
            True(standalone.IndexOf("special?'disabled'", StringComparison.Ordinal) >= 0, "???????????");
            True(standalone.IndexOf("step=\"0.01\"", StringComparison.Ordinal) >= 0, "???????? 0.01 ???");
            True(standalone.IndexOf("class=\"qa-drag\" draggable=\"true\"", StringComparison.Ordinal) >= 0, "?????????????");
            False(standalone.IndexOf("class=\"qa-layer-row\" draggable=\"true\"", StringComparison.Ordinal) >= 0, "???????????");
            True(standalone.IndexOf("calculateQuantityDraft", StringComparison.Ordinal) >= 0, "?????????? C# ??????");
            True(standalone.IndexOf("scheduleDraft", StringComparison.Ordinal) >= 0, "???????????????");
            True(standalone.IndexOf("captureFocus", StringComparison.Ordinal) >= 0, "??????????????");
            True(standalone.IndexOf("stash.appendChild(e)", StringComparison.Ordinal) >= 0, "????????????????????");
            True(standalone.IndexOf("oncompositionstart", StringComparison.Ordinal) >= 0, "?????????????????");
            True(standalone.IndexOf("function fmtInput", StringComparison.Ordinal) >= 0, "???????????");
            True(standalone.IndexOf("function fmt2", StringComparison.Ordinal) >= 0, "??????????????????");
            True(standalone.IndexOf("function round2", StringComparison.Ordinal) >= 0
                && standalone.IndexOf("+1e-9", StringComparison.Ordinal) >= 0,
                "???????????????????");
            True(standalone.IndexOf("fmt2(c.cadLength", StringComparison.Ordinal) >= 0
                && standalone.IndexOf("fmt2(c.realExcavationDepth", StringComparison.Ordinal) >= 0,
                "?????????????????????");
            True(standalone.IndexOf("data-layer=\"role\"", StringComparison.Ordinal) >= 0, "??????????????");
            True(standalone.IndexOf(">???</option>", StringComparison.Ordinal) >= 0, "?????????");
            True(standalone.IndexOf(">??</option>", StringComparison.Ordinal) >= 0, "????????");
            True(standalone.IndexOf("e.isComposing||self.composing", StringComparison.Ordinal) >= 0, "???????????????");
            True(standalone.IndexOf("getAttribute('data-layer')==='name')return", StringComparison.Ordinal) >= 0,
                "??????????????????");
            False(standalone.IndexOf("data-sec=", StringComparison.Ordinal) >= 0, "?????????????");
            False(standalone.IndexOf("??????", StringComparison.Ordinal) >= 0, "?????????????????");
            True(standalone.IndexOf("??????", StringComparison.Ordinal) >= 0, "?????????????");
            False(standalone.IndexOf("function parseLayers", StringComparison.Ordinal) >= 0, "???????????????");
            False(standalone.IndexOf("function encodeLayers", StringComparison.Ordinal) >= 0, "?????????????????");
            False(standalone.IndexOf("['Remark','??'", StringComparison.Ordinal) >= 0, "???????????????");
            False(standalone.IndexOf("????", StringComparison.Ordinal) >= 0, "???????????????");
            False(embedded.IndexOf("????", StringComparison.Ordinal) >= 0, "?????????????????");
            False(standalone.IndexOf(">????<", StringComparison.Ordinal) >= 0, "???????????????");
            False(standalone.IndexOf(">????<", StringComparison.Ordinal) >= 0, "????????????????");
            False(standalone.IndexOf(">??????<", StringComparison.Ordinal) >= 0, "????????????????");
            False(standalone.IndexOf("data-act=\"\"close\"\"", StringComparison.Ordinal) >= 0, "?????????????");
            True(standalone.IndexOf(">??<", StringComparison.Ordinal) >= 0, "????????????????");
            True(standalone.IndexOf("<span>??</span>", StringComparison.Ordinal) >= 0, "????????????");
            True(standalone.IndexOf("<span>??</span>", StringComparison.Ordinal) >= 0, "??????? CAD ??");
            False(standalone.IndexOf("<span>Handle</span>", StringComparison.Ordinal) >= 0, "???????? Handle");
            False(standalone.IndexOf("CAD / ????", StringComparison.Ordinal) >= 0, "????????????");
        }

        private static void TestNumericInputSteps()
        {
            string annotationScript = CDBoxStudioAnnotationSettingsPage.BuildComponentScript();
            string annotationEmbedded = CDBoxStudioAnnotationSettingsPage.BuildEmbeddedSection();
            string annotationStandalone = CDBoxStudioAnnotationSettingsPage.BuildStandaloneDocument(new CDBoxStudioSettings(), "test.log", "pipeLength");
            True(annotationScript.IndexOf("step=\"0.01\"", StringComparison.Ordinal) >= 0, "?????????? 0.01 ???");
            False(annotationScript.IndexOf("step=\"0.1\"", StringComparison.Ordinal) >= 0, "???????? 0.1 ????");
            False(annotationScript.IndexOf("step=\"0.05\"", StringComparison.Ordinal) >= 0, "???????? 0.05 ????");
            False(annotationScript.IndexOf("step=\"0.001\"", StringComparison.Ordinal) >= 0, "???????? 0.001 ????");
            Contains(annotationScript, "surface.calculationMode", "??????????????");
            Contains(annotationScript, "label:'?????'", "????????????");
            Contains(annotationScript, "label:'????'", "???????????");
            False(annotationScript.IndexOf("?? CASS surfacearea ??????", StringComparison.Ordinal) >= 0, "???????????????");
            False(annotationEmbedded.IndexOf("data-action=\"reset-current\"", StringComparison.Ordinal) >= 0, "????????????????");
            False(annotationStandalone.IndexOf("data-action=\"reset-current\"", StringComparison.Ordinal) >= 0, "????????????????");
            False(annotationStandalone.IndexOf("data-action=\"close\">??", StringComparison.Ordinal) >= 0, "??????????????");
            True(annotationStandalone.IndexOf("<h2>????</h2></div><div class=\"as-head-actions\"><button", StringComparison.Ordinal) >= 0, "?????????????");
        }

        private static void TestLayerManagerCustomParents()
        {
            string script = CDBoxStudioLayerManagerPage.BuildComponentScript();
            string styles = CDBoxStudioLayerManagerPage.BuildStyles(false);
            True(styles.Length > 10000, "Layer Manager shared styles must not be missing");
            True(styles.IndexOf(".lm-page", StringComparison.Ordinal) >= 0, "Layer Manager page layout styles must be present");
            True(styles.IndexOf(".lm-grid-header", StringComparison.Ordinal) >= 0, "Layer Manager grid styles must be present");
            True(styles.IndexOf(".lm-grid-row.dragging", StringComparison.Ordinal) >= 0, "Layer Manager drag state styles must be present");
            False(script.IndexOf("branch('other','??'", StringComparison.Ordinal) >= 0, "??????????????");
            True(script.IndexOf("out+=otherChildren;", StringComparison.Ordinal) >= 0, "?????????????");
            True(script.IndexOf("draggedLayerName", StringComparison.Ordinal) >= 0, "??????????");
            True(script.IndexOf("class=\"lm-drag-handle\" draggable=\"true\"", StringComparison.Ordinal) >= 0, "???????????");
            False(script.IndexOf("lm-grid-row '+(dirty?'dirty ':'')+(selected?'selected ':'')+(failure?'failed ':'')+'\" draggable=\"true\"", StringComparison.Ordinal) >= 0, "??????????");
            True(script.IndexOf("selectRange", StringComparison.Ordinal) >= 0, "?????? Shift ????");
            True(script.IndexOf("ev.shiftKey", StringComparison.Ordinal) >= 0, "?????? Shift ????");
            True(script.IndexOf("bindMarqueeSelection", StringComparison.Ordinal) >= 0, "??????????");
            True(script.IndexOf("lm-selection-box", StringComparison.Ordinal) >= 0, "??????????");
            True(script.IndexOf("recognitionFilter", StringComparison.Ordinal) >= 0, "????????????");
            True(script.IndexOf("recognizedParent", StringComparison.Ordinal) >= 0, "????????????????");
            True(script.IndexOf("confidencePercent", StringComparison.Ordinal) >= 0, "???????????");
            True(script.IndexOf(">??????</button>", StringComparison.Ordinal) >= 0, "??????????????");
            True(script.IndexOf(">????</button>", StringComparison.Ordinal) >= 0, "??????????????");
            True(script.IndexOf("openManualPresetDialog", StringComparison.Ordinal) >= 0, "???????????");
            True(script.IndexOf("openDrawingPresetDialog", StringComparison.Ordinal) >= 0, "???????????????");
            True(script.IndexOf("deleteLayerPreset", StringComparison.Ordinal) >= 0, "?????????????");
            True(styles.IndexOf(".lm-preset-form", StringComparison.Ordinal) >= 0, "????????? WebView2 ??");
            True(styles.IndexOf(".lm-recognition-status", StringComparison.Ordinal) >= 0, "?????????");
            False(script.IndexOf("?????????????", StringComparison.Ordinal) >= 0, "?????????????");
            False(script.IndexOf(">????<", StringComparison.Ordinal) >= 0, "?????????????????");
            False(script.IndexOf(">????<", StringComparison.Ordinal) >= 0, "???????????????");
            False(script.IndexOf("data-action=\"close\">??", StringComparison.Ordinal) >= 0, "???????????????");
        }

        private static void TestQuantityDefaultsTableInteraction()
        {
            string script = CDBoxStudioQuantityDefaultsPage.BuildEmbeddedBridgeScript();
            True(script.IndexOf("class=\"layer-drag-handle\" draggable=\"true\"", StringComparison.Ordinal) >= 0, "??????????????");
            True(script.IndexOf("draggedLayer", StringComparison.Ordinal) >= 0, "??????????");
            False(script.IndexOf("<tr draggable=\"true\"", StringComparison.Ordinal) >= 0, "???????????");
            False(script.IndexOf(">??<", StringComparison.Ordinal) >= 0, "???????????");
            False(script.IndexOf(">??<", StringComparison.Ordinal) >= 0, "???????????");
            False(script.IndexOf("data-layer-row-action=\"up\"", StringComparison.Ordinal) >= 0, "????????????");
            False(script.IndexOf("data-layer-row-action=\"down\"", StringComparison.Ordinal) >= 0, "????????????");
            False(script.IndexOf("?????????", StringComparison.Ordinal) >= 0, "??????????????");
            True(script.IndexOf("???", StringComparison.Ordinal) >= 0, "???????????????");
            True(script.IndexOf(">???</option>", StringComparison.Ordinal) >= 0, "????????????");
            True(script.IndexOf(">??</option>", StringComparison.Ordinal) >= 0, "???????????");
            False(script.IndexOf(">???</option>", StringComparison.Ordinal) >= 0, "???????????????");
            string standalone = CDBoxStudioQuantityDefaultsPage.BuildStandaloneDocument(new CDBoxStudioSettings(), "test.log");
            False(standalone.IndexOf("????", StringComparison.Ordinal) >= 0, "???????????????");
            False(standalone.IndexOf("id=\"restoreDefaultProfilesButton\"", StringComparison.Ordinal) >= 0, "???????????????");
            False(standalone.IndexOf("id=\"closeWindow\"", StringComparison.Ordinal) >= 0, "?????????????");
            True(standalone.IndexOf("class=\"qd-tabs-row\"", StringComparison.Ordinal) >= 0, "??????????????????");
            string embedded = CDBoxStudioQuantityDefaultsPage.BuildEmbeddedSection();
            False(embedded.IndexOf("????", StringComparison.Ordinal) >= 0, "???????????????");
            False(embedded.IndexOf("settings-head", StringComparison.Ordinal) >= 0, "?????????????");
            False(embedded.IndexOf("restoreDefaultProfilesButton", StringComparison.Ordinal) >= 0, "???????????????");
            True(embedded.IndexOf("class=\"qd-tabs-row\"", StringComparison.Ordinal) >= 0, "??????????????????");
        }

        private static void TestSectionDrawingSharedPage()
        {
            string script = CDBoxStudioSectionDrawingPage.BuildComponentScript();
            string styles = CDBoxStudioSectionDrawingPage.BuildStyles(false);
            var settings = new CDBoxStudioSettings { Theme = "dark", AnimationsEnabled = false };
            string standalone = CDBoxStudioSectionDrawingPage.BuildStandaloneDocument(settings, "studio.log");

            True(styles.Length > 8000, "???????????");
            True(script.IndexOf("<svg data-preview", StringComparison.Ordinal) >= 0, "????????? SVG ??");
            True(script.IndexOf("sd-hatch-diag", StringComparison.Ordinal) >= 0, "??????????????");
            True(script.IndexOf("bindLayerDrag", StringComparison.Ordinal) >= 0, "????????????");
            True(script.IndexOf("bindPipeDrag", StringComparison.Ordinal) >= 0, "???????????");
            True(script.IndexOf("addEventListener('wheel'", StringComparison.Ordinal) >= 0, "?????????");
            True(styles.IndexOf("grid-template-columns:minmax(0,1fr) 460px", StringComparison.Ordinal) >= 0, "????????????");
            True(script.IndexOf("rebalanceHeights", StringComparison.Ordinal) >= 0, "???????????????");
            True(script.IndexOf("data-field='TotalHeight' value='${fmt(o.TotalHeight)}'></label>", StringComparison.Ordinal) >= 0, "?????????????");
            True(script.IndexOf("if(key==='TotalHeight'){this.options.LockTotalHeight=true", StringComparison.Ordinal) >= 0, "???????????????????");
            True(script.IndexOf("<span>????</span><textarea", StringComparison.Ordinal) >= 0, "???????????");
            True(script.IndexOf("titleLines", StringComparison.Ordinal) >= 0, "???????????");
            True(script.IndexOf("<span>????</span><select data-field='TextStyleName'", StringComparison.Ordinal) >= 0, "????????????????");
            True(script.IndexOf("data-layer-field='HatchPatternName'", StringComparison.Ordinal) >= 0, "???????????");
            True(script.IndexOf("list='sd-hatch-patterns'", StringComparison.Ordinal) >= 0, "??????????????");
            True(script.IndexOf("step='0.0001' min='0' data-layer-field='HatchScale'", StringComparison.Ordinal) >= 0, "?????????????");
            True(script.IndexOf("data-field='DrawingScale'", StringComparison.Ordinal) >= 0, "????????????");
            True(script.IndexOf("function pipeDiameter", StringComparison.Ordinal) >= 0, "??????????");
            True(script.IndexOf("pk==='PipeText'", StringComparison.Ordinal) >= 0, "???????????????");
            True(script.IndexOf("translate(450 325) scale(${this.zoom}) translate(-450 -325)", StringComparison.Ordinal) >= 0, "???????????");
            True(script.IndexOf("class='sd-drag' draggable='true'", StringComparison.Ordinal) >= 0, "????????????");
            False(script.IndexOf("sd-layer-row' draggable='true'", StringComparison.Ordinal) >= 0, "?????????????");
            False(script.IndexOf("sd-pipe-row' draggable='true'", StringComparison.Ordinal) >= 0, "????????????");
            False(script.IndexOf("data-layer-field='HatchAngle'", StringComparison.Ordinal) >= 0, "???????????????");
            False(script.IndexOf("data-act='reset'>????", StringComparison.Ordinal) >= 0, "?????????????");
            False(script.IndexOf("data-act='close'>??", StringComparison.Ordinal) >= 0, "??????????????");
            True(standalone.IndexOf("Preview 10", StringComparison.Ordinal) >= 0, "?????? Preview 10 ??");
            True(standalone.IndexOf("standalone:true", StringComparison.Ordinal) >= 0, "????????????");
            True(standalone.IndexOf("data-theme=\"dark\"", StringComparison.Ordinal) >= 0, "?????? Studio ??");
            False(standalone.IndexOf("????", StringComparison.Ordinal) >= 0, "?????????????");
            False(script.IndexOf(">????<", StringComparison.Ordinal) >= 0, "???????????????");

            double diameter;
            True(SectionPipeOptions.TryParsePipeDiameter("DN200", out diameter), "????? DN ????");
            Near(0.2, diameter, 1e-9, "DN200 ???? 0.2m ??");
            True(SectionPipeOptions.TryParsePipeDiameter("?? DN315", out diameter), "???? DN ????????");
            Near(0.315, diameter, 1e-9, "DN315 ???? 0.315m ??");
        }

        private static void TestLegacyUpdateSourceValidation()
        {
            string root = NewTemporaryDirectory("update-source");
            try
            {
                string dll = Path.Combine(root, "CDBox.dll");
                File.WriteAllText(dll, "main");
                CDBoxUpdateSourceValidationResult invalid = CDBoxUpdateSourceValidator.Validate(dll);
                False(invalid.Valid, "?? CDBox.dll ?????????");
                True(invalid.Message.IndexOf("Microsoft.Web.WebView2.WinForms.dll", StringComparison.Ordinal) >= 0, "???????? WebView2 ??");

                foreach (string dependency in CDBoxRequiredRuntimeFiles.ManagedDependencies)
                {
                    File.WriteAllText(Path.Combine(root, dependency), dependency);
                }
                Directory.CreateDirectory(Path.Combine(root, "Updater"));
                File.WriteAllText(Path.Combine(root, "Updater", "CDBoxUpdater.exe"), "updater");
                Directory.CreateDirectory(Path.Combine(root, "runtimes", "win-x64", "native"));
                File.WriteAllText(Path.Combine(root, "runtimes", "win-x64", "native", "WebView2Loader.dll"), "loader");

                CDBoxUpdateSourceValidationResult valid = CDBoxUpdateSourceValidator.Validate(dll);
                True(valid.Valid, "???????????");
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
                True(failure is InvalidOperationException, "?? ZIP ????");
                True(failure.Message.IndexOf("????", StringComparison.Ordinal) >= 0, "?????????");
                False(File.Exists(Path.Combine(root, "escaped.txt")), "????????????");
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
            Contains(page, "browseExcelWorkbook", "Excel ? CAD ??? WebView2 ??????");
            Contains(page, "openExcelSelection", "Excel ? CAD ????? Excel ?????");
            Contains(page, "pollExcelSelection", "Excel ? CAD ???????????");
            Contains(page, "value=\"exploded\"", "Excel ? CAD ??????????");
            Contains(page, "value=\"table\"", "Excel ? CAD ????? TABLE ??");
            Contains(page, "value=\"block\"", "Excel ? CAD ??????");
            Contains(page, "confirmSavedExcelFallback", "Excel ?????????? WebView2 ??????");
            Contains(page, "saveExcelToCadPreferences", "Excel ? CAD ?????????");
            Contains(page, "value=\"block\" checked", "?????????????");
            Contains(page, "value=\"print\" checked", "??????????????");
            Contains(page, "value=\"3.25\"", "??????????????");
            Contains(page, "????", "?????????????");
            Contains(page, "id=\"gridLayer\"", "??????????");
            Contains(page, "id=\"contentLayer\"", "??????????");
            Contains(page, "value=\"EX_GRID\" selected",
                "????????????????");
            Contains(page, "value=\"EX_TEXT\" selected",
                "????????????????");
            Contains(page, "name=\"entityColor\" value=\"layer\" checked",
                "????????????????");
            Contains(page, "????", "???????????");
            Contains(page, ".ex-input-row input[type=text]", "????????????????");
            Contains(page, "input[type=radio]{position:absolute", "???????");
            False(page.Contains("WebView2 ????"), "??????????");
            False(page.Contains("????"), "???????????");

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
                    legacyTitle.CreateCell(0).SetCellValue("????");
                    legacySheet.AddMergedRegion(new CellRangeAddress(0, 0, 0, 1));
                    IRow legacyRow = legacySheet.CreateRow(1);
                    legacyRow.CreateCell(0).SetCellValue(12.5);
                    legacyRow.CreateCell(1).SetCellValue("????");
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
                Equal(2, legacy.RowCount, "?? XLS ????????");
                Equal("????", legacy.GetCell(0, 0).Text,
                    "?? XLS ??? HSSFRow.Hidden ??????");
                Near(95.0, legacy.ColumnPixelWidths[0], 0.01,
                    "Excel ?????????????????");

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
                    title.SetCellValue("????");
                    title.CellStyle = titleStyle;
                    sheet.AddMergedRegion(new CellRangeAddress(0, 0, 0, 1));

                    IRow second = sheet.CreateRow(1);
                    second.CreateCell(0).SetCellValue(10);
                    second.CreateCell(1).SetCellValue("?");
                    second.CreateCell(2).CellFormula = "A2+5";

                    IRow third = sheet.CreateRow(2);
                    third.HeightInPoints = 30;
                    third.CreateCell(0).SetCellValue("??");
                    third.CreateCell(1).SetCellValue("?");
                    third.CreateCell(2).SetCellValue(20);

                    sheet.SetColumnWidth(0, 20 * 256);
                    sheet.SetColumnWidth(1, 12 * 256);
                    workbook.SetPrintArea(0, 1, 2, 1, 2);
                    workbook.GetCreationHelper().CreateFormulaEvaluator().EvaluateAll();
                    using (FileStream stream = File.Create(path)) workbook.Write(stream);
                }

                Equal("B2:D4", ExcelTableReader.NormalizeRangeAddress(
                    "'Main'!$B$2:$D$4"), "???????????????????");

                using (FileStream stream = File.OpenRead(path))
                using (var verificationWorkbook = new XSSFWorkbook(stream))
                {
                    ICell formulaCell = verificationWorkbook.GetSheet("Main")
                        .GetRow(1).GetCell(2);
                    Equal(CellType.Formula, formulaCell.CellType,
                        "?????????????");
                    CellValue evaluated = verificationWorkbook.GetCreationHelper()
                        .CreateFormulaEvaluator().Evaluate(formulaCell);
                    Equal(CellType.Numeric, evaluated.CellType,
                        "NPOI ???????");
                    Near(15, evaluated.NumberValue, 0.000001,
                        "NPOI ??????");
                }

                ExcelWorkbookInfo info = ExcelTableReader.Inspect(path);
                Equal(1, info.Sheets.Count, "????????");
                Equal("A1:C3", info.Sheets[0].UsedRange, "?????????");

                var usedOptions = new ExcelToCadOptions
                {
                    FilePath = path,
                    SheetName = "Main",
                    RangeMode = ExcelTableRangeMode.UsedRange
                };
                ExcelTableModel used = ExcelTableReader.Read(usedOptions);
                Equal(3, used.RowCount, "??????");
                Equal(3, used.ColumnCount, "??????");
                Equal(1, used.MergedRanges.Count, "????????");
                Equal("????", used.GetEffectiveCell(0, 1).Text,
                    "???????????");
                True(used.GetCell(0, 0).Style.Bold, "???????");
                True(used.GetCell(0, 0).Style.BackgroundColor != null,
                    "??????");
                Equal("15", used.GetCell(1, 2).Text, "?????????");
                True(used.ColumnPixelWidths[0] > used.ColumnPixelWidths[1],
                    "???????");
                True(used.RowPixelHeights[2] > used.RowPixelHeights[1],
                    "???????");

                var selectedOptions = new ExcelToCadOptions
                {
                    FilePath = path,
                    SheetName = "Main",
                    RangeMode = ExcelTableRangeMode.LiveSelection,
                    SelectedRange = "$B$2:$C$3"
                };
                ExcelTableModel selected = ExcelTableReader.Read(selectedOptions);
                Equal(2, selected.RowCount, "????????");
                Equal(2, selected.ColumnCount, "????????");
                Equal("?", selected.GetCell(0, 0).Text, "???????????");
                Equal("20", selected.GetCell(1, 1).Text, "???????????");

                var printOptions = new ExcelToCadOptions
                {
                    FilePath = path,
                    SheetName = "Main",
                    RangeMode = ExcelTableRangeMode.PrintArea
                };
                ExcelTableModel printed = ExcelTableReader.Read(printOptions);
                Equal("B2:C3", printed.SourceRange, "??????????");
                Equal(2, printed.RowCount, "??????");
                Equal(2, printed.ColumnCount, "??????");

                selectedOptions.SelectedRange = "A1:ZZ100";
                Exception tooLarge = Capture(delegate
                {
                    ExcelTableReader.Read(selectedOptions);
                });
                True(tooLarge is InvalidOperationException,
                    "?????????????");
                Contains(tooLarge.Message, "????????",
                    "?????????????");
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
                True(outcome.Success, "?? bundle ?????");
                True(progress.Count >= 6, "??????????????");
                Equal(25, progress[0], "????????????");
                Equal(98, progress[progress.Count - 1], "??????????????");
                for (int i = 1; i < progress.Count; i++) True(progress[i] >= progress[i - 1], "????????");
                True(Directory.Exists(outcome.BackupBundlePath), "? bundle ?????");
                True(File.Exists(Path.Combine(outcome.BackupBundlePath, "Contents", "old-version.txt")), "????????");
                Equal("new-version", File.ReadAllText(Path.Combine(target, "Contents", "CDBox.dll"), Encoding.UTF8), "?????????");
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
