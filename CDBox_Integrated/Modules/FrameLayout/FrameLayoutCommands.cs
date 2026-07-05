using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using System;
using System.IO;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    public class FrameLayoutCommands
    {
        [CommandMethod("TCFRAMEADD")]
        public void AddFrameTemplate()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                ObjectId frameId = PromptFrameOrImportFromFile(doc);
                if (frameId.IsNull)
                {
                    ed.WriteMessage("\n未获得图框对象。");
                    return;
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockReference br = tr.GetObject(frameId, OpenMode.ForRead) as BlockReference;
                    if (br == null)
                    {
                        ed.WriteMessage("\n当前初版仅支持选择块参照作为图框模板。");
                        ed.WriteMessage("\n如果模板是散线散文字，请使用“直接回车导入模板文件”的方式，插件会自动打包成块。");
                        tr.Commit();
                        return;
                    }

                    ed.WriteMessage("\n请在图框内指定有效绘制区域。");

                    PromptPointResult p1 = ed.GetPoint("\n请选择有效绘制区域第一角点：");
                    if (p1.Status != PromptStatus.OK)
                    {
                        tr.Commit();
                        ed.WriteMessage("\n已取消。");
                        return;
                    }

                    PromptCornerOptions cOpt = new PromptCornerOptions("\n请选择有效绘制区域对角点：", p1.Value);
                    PromptPointResult p2 = ed.GetCorner(cOpt);
                    if (p2.Status != PromptStatus.OK)
                    {
                        tr.Commit();
                        ed.WriteMessage("\n已取消。");
                        return;
                    }

                    FrameTemplateService service = new FrameTemplateService();
                    FrameTemplateInfo info = service.CreateInfoFromBlockReference(db, tr, br, p1.Value, p2.Value);

                    string validMsg;
                    if (!info.IsValid(out validMsg))
                    {
                        ed.WriteMessage("\n模板无效：{0}", validMsg);
                        tr.Commit();
                        return;
                    }

                    string path = FrameTemplateInfo.GetDefaultConfigPath();
                    info.Save(path);

                    tr.Commit();

                    ed.WriteMessage("\n图框模板保存成功。");
                    ed.WriteMessage("\n模板块名：{0}", info.BlockName);
                    ed.WriteMessage("\n有效绘制区域：{0:0.###} × {1:0.###}", info.ValidWidthWorld, info.ValidHeightWorld);
                    ed.WriteMessage("\n配置文件：{0}", path);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n添加图框模板失败：{0}", ex.Message);
            }
        }

        [CommandMethod("TCFRAMECUT")]
        public void CutAndLayoutFrames()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                string configPath = FrameTemplateInfo.GetDefaultConfigPath();
                FrameTemplateInfo info = FrameTemplateInfo.Load(configPath);

                if (info == null)
                {
                    ed.WriteMessage("\n未找到图框模板配置，请先执行 TCFRAMEADD。");
                    return;
                }

                string msg;
                if (!info.IsValid(out msg))
                {
                    ed.WriteMessage("\n图框模板配置无效：{0}", msg);
                    return;
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    if (!CadDbHelper.BlockExists(db, tr, info.BlockName))
                    {
                        ed.WriteMessage("\n当前图中不存在模板块：{0}", info.BlockName);
                        ed.WriteMessage("\n请先执行 TCFRAMEADD，或在当前图中插入该模板块后重试。");
                        tr.Commit();
                        return;
                    }

                    tr.Commit();
                }

                PromptPointResult r1 = ed.GetPoint("\n请选择总裁图矩形第一角点：");
                if (r1.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n已取消。");
                    return;
                }

                PromptCornerOptions cOpt = new PromptCornerOptions("\n请选择总裁图矩形对角点：", r1.Value);
                PromptPointResult r2 = ed.GetCorner(cOpt);
                if (r2.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n已取消。");
                    return;
                }

                PromptPointResult d1 = ed.GetPoint("\n请选择裁图方向起点：");
                if (d1.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n已取消。");
                    return;
                }

                PromptPointOptions d2Opt = new PromptPointOptions("\n请选择裁图方向终点：");
                d2Opt.BasePoint = d1.Value;
                d2Opt.UseBasePoint = true;

                PromptPointResult d2 = ed.GetPoint(d2Opt);
                if (d2.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n已取消。");
                    return;
                }

                Vector3d dir = d2.Value - d1.Value;
                if (dir.Length < GeometryHelper.Eps)
                {
                    ed.WriteMessage("\n裁图方向长度为 0，无法生成图框。");
                    return;
                }

                PromptDoubleOptions overlapOpt = new PromptDoubleOptions("\n请输入搭接长度");
                overlapOpt.AllowNegative = false;
                overlapOpt.AllowZero = true;
                overlapOpt.DefaultValue = Math.Max(0, info.LastOverlap);
                overlapOpt.UseDefaultValue = true;

                PromptDoubleResult overlapRes = ed.GetDouble(overlapOpt);
                if (overlapRes.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n已取消。");
                    return;
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    FrameLayoutService layout = new FrameLayoutService();

                    FrameLayoutService.LayoutResult result = layout.LayoutFramesForRectangle(
                        db,
                        tr,
                        info,
                        r1.Value,
                        r2.Value,
                        dir,
                        overlapRes.Value
                    );

                    tr.Commit();

                    ed.WriteMessage("\n图框布置完成。");
                    ed.WriteMessage("\n共生成 {0} 个图框，列数 {1}，行数 {2}。", result.Count, result.Columns, result.Rows);
                    ed.WriteMessage("\n已生成 TC_裁图范围 矩形和 TC_指北针。");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n裁图布置失败：{0}", ex.Message);
            }
        }

        private ObjectId PromptFrameOrImportFromFile(Document doc)
        {
            Editor ed = doc.Editor;
            Database db = doc.Database;

            PromptEntityOptions opt = new PromptEntityOptions("\n请选择已有图框对象，直接回车/空格则从模板文件导入：");
            opt.AllowNone = true;
            opt.SetRejectMessage("\n请选择图框块参照。");
            opt.AddAllowedClass(typeof(BlockReference), true);

            PromptEntityResult res = ed.GetEntity(opt);

            if (res.Status == PromptStatus.OK)
                return res.ObjectId;

            if (res.Status != PromptStatus.None)
                return ObjectId.Null;

            PromptOpenFileOptions fileOpt = new PromptOpenFileOptions("\n请选择模板 DWG 文件：");
            fileOpt.Filter = "DWG 文件 (*.dwg)|*.dwg|所有文件 (*.*)|*.*";

            PromptFileNameResult fileRes = ed.GetFileNameForOpen(fileOpt);
            if (fileRes.Status != PromptStatus.OK)
                return ObjectId.Null;

            string path = fileRes.StringResult;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                ed.WriteMessage("\n模板文件不存在。");
                return ObjectId.Null;
            }

            PromptPointResult ins = ed.GetPoint("\n请选择模板图形插入位置：");
            if (ins.Status != PromptStatus.OK)
                return ObjectId.Null;

            FrameTemplateService service = new FrameTemplateService();

            string newBlockName;
            ObjectId brId = service.ImportTemplateDwgAsBlock(doc, path, ins.Value, out newBlockName);

            ed.WriteMessage("\n模板文件已导入当前图纸。");
            ed.WriteMessage("\n导入块名：{0}", newBlockName);

            PromptEntityOptions pickImportedOpt = new PromptEntityOptions("\n请选择要作为模板的图框对象，直接回车则使用刚导入的图框：");
            pickImportedOpt.AllowNone = true;
            pickImportedOpt.SetRejectMessage("\n请选择图框块参照。");
            pickImportedOpt.AddAllowedClass(typeof(BlockReference), true);

            PromptEntityResult pickImportedRes = ed.GetEntity(pickImportedOpt);

            if (pickImportedRes.Status == PromptStatus.OK)
                return pickImportedRes.ObjectId;

            if (pickImportedRes.Status == PromptStatus.None)
                return brId;

            return ObjectId.Null;
        }
    }
}
