using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CDBox.Shared.Services;
using CDBox.Shared.UI;
using TCPipeAutoDraw.Modules.SectionDrawing;
using TCPipeAutoDraw.UI;
using TCPipeAutoDraw.UI.Studio;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CDBox.Wastewater.UI
{
    internal sealed class WastewaterSectionDrawingPageController
    {
        public const string PageId = "wastewater-section-drawing";
        private readonly ICDBoxPageAppearanceService _appearance;
        private readonly ICDBoxLogger _logger;
        private readonly JavaScriptSerializer _serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public WastewaterSectionDrawingPageController(
            ICDBoxPageAppearanceService appearance, ICDBoxLogger logger)
        {
            _appearance = appearance
                ?? throw new ArgumentNullException("appearance");
            _logger = logger ?? throw new ArgumentNullException("logger");
        }

        public CDBoxPageDefinition CreatePage()
        {
            return new CDBoxPageDefinition(PageId, "断面图生成",
                BuildDocument, Route)
            {
                Width = 1260,
                Height = 820,
                MinimumWidth = 980,
                MinimumHeight = 680
            };
        }

        private string BuildDocument()
        {
            CDBoxPageAppearance appearance = _appearance.GetAppearance()
                ?? new CDBoxPageAppearance();
            var settings = new CDBoxStudioSettings
            {
                Theme = appearance.Theme,
                AnimationsEnabled = appearance.AnimationsEnabled
            };
            return CDBoxStudioSectionDrawingPage.BuildStandaloneDocument(
                settings, appearance.LogFilePath);
        }

        private CDBoxPageRouteResult Route(CDBoxPageRouteRequest request)
        {
            if (request == null) return new CDBoxPageRouteResult
                { Handled = false };
            string name = (request.Name ?? string.Empty).Trim()
                .ToLowerInvariant();
            try
            {
                if (name == "ready" || name == "getsectiondrawingoptions")
                    return Load("CDBoxSectionDrawingLoad");
                if (name == "savesectiondrawingoptions")
                {
                    SectionDrawingOptions options = Read(request.Argument);
                    SectionDrawingSettingsStore.Save(options);
                    CDBoxPageRouteResult result = Load(
                        "CDBoxSectionDrawingSaved");
                    result.ToastKind = "success";
                    result.ToastMessage = "断面设置已保存";
                    return result;
                }
                if (name == "drawsectiondrawing")
                {
                    SectionDrawingOptions options = Read(request.Argument);
                    SectionDrawingSettingsStore.Save(options);
                    return new CDBoxPageRouteResult
                    {
                        Handled = true,
                        ActionToRun = delegate
                        {
                            Document document = AcadApp.DocumentManager
                                .MdiActiveDocument;
                            if (document == null) return;
                            SectionDrawingResult result = SectionDrawingService
                                .SelectPositionAndDraw(document, options);
                            document.Editor.WriteHudMessage(
                                result.ToEditorMessage());
                        },
                        ToastKind = "info",
                        ToastMessage = "正在图中放置断面图"
                    };
                }
                if (name == "openlegacysectiondrawing")
                    return new CDBoxPageRouteResult
                    {
                        Handled = true,
                        ActionToRun = delegate
                        {
                            Document document = AcadApp.DocumentManager
                                .MdiActiveDocument;
                            if (document == null) return;
                            using (var form = new SectionDrawingForm(document))
                                form.ShowDialog(new AcadMainWindow());
                        }
                    };
                if (name == "close") return new CDBoxPageRouteResult
                    { Handled = true, RestorePageAfterAction = false };
            }
            catch (Exception ex)
            {
                _logger.Error("断面图页面操作失败。", ex);
                return new CDBoxPageRouteResult
                {
                    Handled = true,
                    ToastKind = "error",
                    ToastMessage = ex.Message
                };
            }
            return new CDBoxPageRouteResult { Handled = false };
        }

        private SectionDrawingOptions Read(string json)
        {
            SectionDrawingOptions options = _serializer
                .Deserialize<SectionDrawingOptions>(json ?? string.Empty)
                ?? throw new InvalidOperationException("断面参数无法解析。");
            SectionLayoutCalculator.Normalize(options);
            if (options.Layers == null || !options.Layers.Any(layer =>
                    layer != null && layer.DrawLayer && layer.Height > 0))
                throw new InvalidOperationException(
                    "至少需要启用一个结构层。");
            return options;
        }

        private CDBoxPageRouteResult Load(string functionName)
        {
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            SectionDrawingOptions options = SectionDrawingSettingsStore.Load();
            var context = new
            {
                options,
                defaults = SectionDrawingOptions.Default.Clone(),
                textStyles = TableNames<TextStyleTable,
                    TextStyleTableRecord>(document,
                    db => db.TextStyleTableId,
                    record => record.Name, "STANDARD"),
                dimensionStyles = TableNames<DimStyleTable,
                    DimStyleTableRecord>(document,
                    db => db.DimStyleTableId,
                    record => record.Name, "当前尺寸样式"),
                layerNames = TableNames<LayerTable, LayerTableRecord>(
                    document, db => db.LayerTableId,
                    record => record.Name, "0"),
                hatchPatterns = SectionDrawingForm
                    .GetAvailableHatchPatternNames(document)
            };
            return new CDBoxPageRouteResult
            {
                Handled = true,
                ExecuteScript = "window." + functionName + " && window."
                    + functionName + "(" + _serializer.Serialize(context)
                    + ");"
            };
        }

        private static List<string> TableNames<TTable, TRecord>(
            Document document, Func<Database, ObjectId> tableId,
            Func<TRecord, string> name, string fallback)
            where TTable : SymbolTable
            where TRecord : SymbolTableRecord
        {
            var output = new List<string>();
            try
            {
                if (document != null)
                    using (Transaction transaction = document.Database
                        .TransactionManager.StartOpenCloseTransaction())
                    {
                        TTable table = transaction.GetObject(
                            tableId(document.Database), OpenMode.ForRead)
                            as TTable;
                        if (table != null)
                            foreach (ObjectId id in table)
                            {
                                TRecord record = transaction.GetObject(id,
                                    OpenMode.ForRead, false) as TRecord;
                                string value = record == null
                                    ? string.Empty : name(record);
                                if (!string.IsNullOrWhiteSpace(value))
                                    output.Add(value);
                            }
                    }
            }
            catch { }
            if (!output.Any(value => string.Equals(value, fallback,
                    StringComparison.CurrentCultureIgnoreCase)))
                output.Add(fallback);
            return output.Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(value => value,
                    StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }
}
