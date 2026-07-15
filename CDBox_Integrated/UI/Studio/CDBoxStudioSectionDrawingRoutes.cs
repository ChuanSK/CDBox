using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using TCPipeAutoDraw.Modules.SectionDrawing;
using TCPipeAutoDraw.UI;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioSectionDrawingRoutes
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static bool TryRoute(CDBoxStudioRouteRequest request, Action<string> scriptSink, out CDBoxStudioRouteResult result)
        {
            result = null;
            if (request == null || string.IsNullOrWhiteSpace(request.Name)) return false;

            string name = request.Name.Trim().ToLowerInvariant();
            if (name == "getsectiondrawingoptions")
            {
                result = new CDBoxStudioRouteResult
                {
                    Handled = true,
                    ExecuteScript = BuildLoadScript("CDBoxSectionDrawingLoad", SectionDrawingSettingsStore.Load())
                };
                return true;
            }

            if (name == "savesectiondrawingoptions")
            {
                try
                {
                    SectionDrawingOptions options = ReadOptions(request.Argument);
                    SectionDrawingSettingsStore.Save(options);
                    result = new CDBoxStudioRouteResult
                    {
                        Handled = true,
                        ToastKind = "success",
                        ExecuteScript = BuildLoadScript("CDBoxSectionDrawingSaved", options)
                    };
                }
                catch (Exception ex)
                {
                    result = Error("断面设置保存失败：" + ex.Message);
                    CDBoxStudioLogger.Error("保存 Preview 10 断面设置失败。", ex);
                }
                return true;
            }

            if (name == "drawsectiondrawing")
            {
                try
                {
                    SectionDrawingOptions options = ReadOptions(request.Argument);
                    SectionDrawingSettingsStore.Save(options);
                    result = new CDBoxStudioRouteResult
                    {
                        Handled = true,
                        RefreshPage = false,
                        ActionToRun = NewAction("section-drawing:draw", "断面图生成", delegate
                        {
                            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                            if (doc == null) throw new InvalidOperationException("未找到当前图纸。");
                            SectionDrawingResult drawingResult = SectionDrawingService.SelectPositionAndDraw(doc, options);
                            doc.Editor.WriteMessage(drawingResult.ToEditorMessage());
                            SendResult(scriptSink, drawingResult.Message, drawingResult.Success);
                        })
                    };
                }
                catch (Exception ex)
                {
                    result = Error("断面参数无效：" + ex.Message);
                }
                return true;
            }

            if (name == "openlegacysectiondrawing")
            {
                result = new CDBoxStudioRouteResult
                {
                    Handled = true,
                    RefreshPage = true,
                    ActionToRun = NewAction("section-drawing:legacy", "旧版断面图界面", delegate
                    {
                        Document doc = AcadApp.DocumentManager.MdiActiveDocument;
                        if (doc == null) throw new InvalidOperationException("未找到当前图纸。");
                        using (var form = new SectionDrawingForm(doc)) form.ShowDialog(new AcadMainWindow());
                    })
                };
                return true;
            }

            return false;
        }

        private static SectionDrawingOptions ReadOptions(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) throw new InvalidOperationException("未收到断面参数。");
            SectionDrawingOptions options = Serializer.Deserialize<SectionDrawingOptions>(payload);
            if (options == null) throw new InvalidOperationException("断面参数无法解析。");
            SectionLayoutCalculator.Normalize(options);
            bool drawable = false;
            for (int i = 0; i < options.Layers.Count; i++)
            {
                if (options.Layers[i] != null) options.Layers[i].HatchAngle = 0.0;
                if (options.Layers[i] != null && options.Layers[i].DrawLayer && options.Layers[i].Height > 0)
                {
                    drawable = true;
                    break;
                }
            }
            if (!drawable) throw new InvalidOperationException("至少需要启用一个结构层。");
            return options;
        }

        private static string BuildLoadScript(string functionName, SectionDrawingOptions options)
        {
            SectionDrawingOptions current = options ?? SectionDrawingOptions.Default.Clone();
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            var context = new
            {
                options = current,
                defaults = SectionDrawingOptions.Default.Clone(),
                textStyles = LoadTextStyleNames(doc, current.TextStyleName),
                dimensionStyles = LoadDimensionStyleNames(doc, current.DimensionStyleName),
                layerNames = LoadLayerNames(doc, current.BorderLayerName),
                hatchPatterns = SectionDrawingForm.GetAvailableHatchPatternNames(doc)
            };
            return "window." + functionName + " && window." + functionName + "(" + Serializer.Serialize(context) + ");";
        }

        private static List<string> LoadTextStyleNames(Document doc, string preferred)
        {
            var names = new List<string>();
            string current = string.Empty;
            try
            {
                Database db = doc == null ? null : doc.Database;
                if (db != null)
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        TextStyleTable table = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                        foreach (ObjectId id in table)
                        {
                            TextStyleTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as TextStyleTableRecord;
                            if (record != null && !record.IsErased) AddIfMissing(names, record.Name);
                        }
                        if (!db.Textstyle.IsNull)
                        {
                            TextStyleTableRecord record = tr.GetObject(db.Textstyle, OpenMode.ForRead, false) as TextStyleTableRecord;
                            if (record != null) current = record.Name;
                        }
                        tr.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Warn("读取断面注记样式失败：" + ex.Message);
            }
            AddIfMissing(names, preferred);
            AddIfMissing(names, "STANDARD");
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            MoveToTop(names, !string.IsNullOrWhiteSpace(preferred) ? preferred : current);
            return names;
        }

        private static List<string> LoadDimensionStyleNames(Document doc, string preferred)
        {
            var names = new List<string> { "当前尺寸样式" };
            try
            {
                Database db = doc == null ? null : doc.Database;
                if (db != null)
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        DimStyleTable table = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                        foreach (ObjectId id in table)
                        {
                            DimStyleTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as DimStyleTableRecord;
                            if (record != null && !record.IsErased) AddIfMissing(names, record.Name);
                        }
                        tr.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Warn("读取断面标注样式失败：" + ex.Message);
            }
            AddIfMissing(names, preferred);
            return names;
        }

        private static List<string> LoadLayerNames(Document doc, string preferred)
        {
            var names = new List<string>();
            try
            {
                Database db = doc == null ? null : doc.Database;
                if (db != null)
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        LayerTable table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                        foreach (ObjectId id in table)
                        {
                            LayerTableRecord record = tr.GetObject(id, OpenMode.ForRead, false) as LayerTableRecord;
                            if (record != null && !record.IsErased) AddIfMissing(names, record.Name);
                        }
                        tr.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                CDBoxStudioLogger.Warn("读取断面图层失败：" + ex.Message);
            }
            AddIfMissing(names, preferred);
            AddIfMissing(names, "0");
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            MoveToTop(names, !string.IsNullOrWhiteSpace(preferred) ? preferred : "0");
            return names;
        }

        private static void AddIfMissing(List<string> names, string value)
        {
            if (names == null || string.IsNullOrWhiteSpace(value)) return;
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], value, StringComparison.CurrentCultureIgnoreCase)) return;
            }
            names.Add(value);
        }

        private static void MoveToTop(List<string> names, string value)
        {
            if (names == null || string.IsNullOrWhiteSpace(value)) return;
            for (int i = 0; i < names.Count; i++)
            {
                if (!string.Equals(names[i], value, StringComparison.CurrentCultureIgnoreCase)) continue;
                string found = names[i];
                names.RemoveAt(i);
                names.Insert(0, found);
                return;
            }
        }

        private static void SendResult(Action<string> scriptSink, string message, bool success)
        {
            if (scriptSink == null) return;
            string text = string.IsNullOrWhiteSpace(message) ? (success ? "断面图已生成。" : "未生成断面图。") : message;
            scriptSink("window.CDBoxSectionDrawingResult && window.CDBoxSectionDrawingResult(" + Serializer.Serialize(text) + "," + (success ? "true" : "false") + ");");
        }

        private static CDBoxStudioAction NewAction(string id, string title, Action action)
        {
            return new CDBoxStudioAction(id, title, "断面", string.Empty, "DM", "Preview 10", CDBoxStudioActionKind.Module, true, true, action);
        }

        private static CDBoxStudioRouteResult Error(string message)
        {
            return new CDBoxStudioRouteResult { Handled = true, ToastKind = "error", ToastMessage = message };
        }
    }
}
