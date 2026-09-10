using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using CDBox.RealEstate.Geometry;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.Services;
using CDBox.RealEstate.Settings;

namespace CDBox.CoreTests
{
    internal static class BuildingPropertyFeatureTests
    {
        private static void Check(bool test,string message){if(!test)throw new Exception(message);}
        private static BuildingPoint2 P(double x,double y)=>new BuildingPoint2(x,y);
        private static BuildingAnnotationPlan Rect(double x,double y,double w,double h)=>BuildingLengthAnnotationPlanner.Create(new[]{P(x,y),P(x+w,y),P(x+w,y+h),P(x,y+h)},.3,false);
        private static BuildingAreaComponent Component(string handle,string layer,BuildingAnnotationPlan p)=>BuildingAreaService.Component(handle,layer,p);
        public static void Geometry()
        {
            var polygon=new[]{P(0,0),P(6,0),P(6,1),P(2.2,1.3),P(2,5),P(0,5)};
            var plan=BuildingLengthAnnotationPlanner.Create(polygon,.3,false);
            var diagonals=plan.AuxiliarySegments.Where(s=>!s.IsHeight).ToList();
            Check(polygon.Any(p=>diagonals.All(d=>(d.Start-p).Length<1e-7||(d.End-p).Length<1e-7)),"可见顶点应成为所有划分线的共同起点");
            Check(plan.AreaTerms.Count==polygon.Length-2,"简单多边形采用最少的 n-2 个三角形");
            foreach(var diagonal in diagonals.Where(d=>d.Annotate))
                Check(plan.AreaTerms.Any(t=>Match(t.BaseStart,t.BaseEnd,diagonal)),"只标注作为计算底边的划分线长度");
            var u=BuildingLengthAnnotationPlanner.Create(new[]{P(0,0),P(6,0),P(6,5),P(4,5),P(4,2),P(2,2),P(2,5),P(0,5)},.3,false);
            Check(u.AreaTerms.Count==3&&u.AreaTerms.All(t=>t.Method=="rectangle")&&Math.Abs(u.GeometryArea-24)<1e-7,"直角 U 形优先三个矩形");
            var half=BuildingCurvedBoundaryPlanner.Create(new[]{P(-10,0),P(10,0)},new[]{1d,0d},.3,false);
            Check(half.CalculatedArea==157.08m&&half.AreaTerms.Single().Method=="arcSegment","半圆使用精确弓形计算");
            var circle=BuildingCurvedBoundaryPlanner.Create(new[]{P(-10,0),P(10,0)},new[]{1d,1d},.3,false);
            Check(circle.CalculatedArea==314.16m&&circle.AuxiliarySegments.Any(s=>s.Text=="R10.00"),"圆形使用圆弧公式及半径注记");
            var reverse=BuildingCurvedBoundaryPlanner.Create(new[]{P(10,0),P(-10,0)},new[]{-1d,-1d},.3,false);
            Check(reverse.CalculatedArea==circle.CalculatedArea,"反向圆弧不改变面积");
            var cut=BuildingCurvedBoundaryPlanner.Create(new[]{P(0,0),P(10,0),P(10,10),P(0,10)},new[]{0d,-.2d,0d,0d},.3,false);
            Check(cut.CalculatedArea<100&&cut.AreaTerms.Any(t=>t.Area<0),"内凹圆弧正确扣除弓形");
        }
        private static bool Match(BuildingAreaPoint a,BuildingAreaPoint b,BuildingPlannedSegment d)=>
            Near(a,d.Start)&&Near(b,d.End)||Near(a,d.End)&&Near(b,d.Start);
        private static bool Near(BuildingAreaPoint a,BuildingPoint2 b)=>Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)<1e-7;
        public static void HollowContainment()
        {
            var full=Component("full","房屋",Rect(0,0,10,10));
            var c=BuildingAreaService.Calculate("doc",new[]{full,Component("inside","中空",Rect(2,2,2,2)),Component("outside","中空",Rect(20,20,2,2)),Component("crossing","中空",Rect(9,9,2,2))});
            Check(c.Area==96&&c.ExcludedHollowHandles.Count==2,"外部和跨出边界的中空不多扣减");
            var circle=BuildingCurvedBoundaryPlanner.Create(new[]{P(-10,0),P(10,0)},new[]{1d,1d},.3,false);
            c=BuildingAreaService.Calculate("doc",new[]{Component("circle","房屋",circle),Component("inside","中空",Rect(-1,-1,2,2)),Component("outside","中空",Rect(9,9,1,1))});
            Check(c.Area==310.16m&&c.ExcludedHollowHandles.Single()=="outside","圆弧包含判断与解析面积一致");
            var concave=BuildingLengthAnnotationPlanner.Create(new[]{P(0,0),P(6,0),P(6,5),P(4,5),P(4,2),P(2,2),P(2,5),P(0,5)},.3,false);
            c=BuildingAreaService.Calculate("doc",new[]{Component("u","房屋",concave),Component("bridge","中空",Rect(1,3,4,1))});
            Check(c.ExcludedHollowHandles.Single()=="bridge","中空顶点在内部但边跨出凹边界也应排除");
        }
        public static BuildingAreaCalculation Snapshot(decimal area,string handle)
        {
            return new BuildingAreaCalculation {DocumentId="property-doc",Area=area,FullArea=area,Formula=area.ToString("0.#####"),Components=new List<BuildingAreaComponent>{new BuildingAreaComponent{SourceHandle=handle,LayerName="房屋",Area=area,Factor=1,Formula=area.ToString("0.#####"),Terms=new List<BuildingAreaTerm>{new BuildingAreaTerm{Method="manualFixture",Formula=area.ToString("0.#####"),Area=area}}}}};
        }
        public static ParcelSurveyRecord Example()
        {
            var r=new ParcelSurveyRecord{ParcelName="李四",DocumentId="property-doc",ScopeType="parcel",ParcelId="property-parcel"};r.Normalize();r.Field("house.followParcelOwner").BooleanValue=true;r.Field(ParcelSurveyFieldKeys.OwnerName).TextValue="李四";
            var b=BuildingAreaService.CreateBuilding(Snapshot(127.62m,"1"),new BuildingCaptureInput{BuildingNumber="1幢",Floor="一层",Structure="钢混混合"});
            b.FloorAreas.Add(new BuildingFloorArea{Name="一层",Calculation=Snapshot(27.14m,"2")});
            b.FloorAreas.Add(new BuildingFloorArea{Name="二层",Calculation=Snapshot(154.76m,"3")});
            b.FloorAreas.Add(new BuildingFloorArea{Name="三层",Calculation=Snapshot(154.76m,"4")});
            b.FloorAreas.Add(new BuildingFloorArea{Name="二至三层通道",Calculation=Snapshot(11.135m,"5"),Count=2});r.Buildings.Add(b);BuildingAreaService.Synchronize(r);return r;
        }
        public static void FloorsAndMerge()
        {
            var half=new BuildingFloorArea {Name="二至三层通道",Count=2,Calculation=new BuildingAreaCalculation{Area=11.14m,HalfEnclosedArea=22.27m}};
            Check(BuildingAreaService.RowArea(half)==22.27m,"半封乘跨层数后再舍入，避免先舍入再乘造成分位误差");
            var r=Example();Check(r.Buildings[0].Fields["building.area"].NumericValue==486.55m,"特殊跨层明细按明确倍数计入");
            Check(r.Field(ParcelSurveyFieldKeys.BuildingFootprintTotal).NumericValue==154.76m,"两个同名一层分别计入占地");
            Check(r.Buildings[0].FloorAreas.Count(x=>x.Name=="一层")==2,"重复层次名不合并");
            string dir=Path.Combine(Path.GetTempPath(),"CDBoxFloorMerge-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                var store=new ParcelSurveyStore(Path.Combine(dir,"data.json"));store.Save(r);var stale=store.GetRecord(r.Id);
                var next=BuildingAreaService.CreateBuilding(Snapshot(10,"six"),new BuildingCaptureInput{Floor="四层"});
                store.AddCapturedBuilding(r.DocumentId,r.Id,next,null,r.Buildings[0].Id);
                stale.Buildings[0].FloorAreas[0].Name="一层商业";var merged=store.SavePreservingCurrentScope(stale);
                Check(merged.Buildings[0].FloorAreas.Count==6&&merged.Buildings[0].FloorAreas[0].Name=="一层商业","旧草稿保留层次编辑并合并新层次");
                var deletedDraft=store.GetRecord(r.Id);deletedDraft.Buildings.Clear();
                var seventh=BuildingAreaService.CreateBuilding(Snapshot(12,"seven"),new BuildingCaptureInput{Floor="五层"});
                store.AddCapturedBuilding(r.DocumentId,r.Id,seventh,null,r.Buildings[0].Id);
                var restored=store.SavePreservingCurrentScope(deletedDraft);
                Check(restored.Buildings.Count==1&&restored.Buildings[0].FloorAreas.Count==7,"旧草稿删除整幢不能丢失刚追加的新层次");
                merged.Buildings[0].FloorAreas.RemoveAt(1);store.Save(merged);
                Check(store.GetRecord(r.Id).Field(ParcelSurveyFieldKeys.BuildingFootprintTotal).NumericValue==127.62m,"删除底层明细重算占地");
            }
            finally{Directory.Delete(dir,true);}
        }
        public static void Export(string templatePath,string directory)
        {
            var r=Example();string file=Path.Combine(directory,BuildingPropertyWordExporter.DefaultFileName(r));BuildingPropertyWordExporter.Export(templatePath,file,r);
            XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            using(var zip=ZipFile.OpenRead(file))using(var stream=zip.GetEntry("word/document.xml").Open())
            {
                var doc=XDocument.Load(stream);string text=string.Concat(doc.Descendants(w+"t").Select(x=>x.Value));
                Check(text.Contains("二至三层通道")&&text.Contains("486.55")&&text.Contains("154.76")&&!text.Contains("张三"),"房产导出使用真实层次、合计与权利人");
                Check(doc.Descendants(w+"tr").Count()==11,"参考房产的五条层次加小计、占地与合计结构正确");
                Check(doc.Descendants(w+"t").Count(t=>t.Value=="一层")==2,"同名层次在文档中独立占行");
            }
            var four=Example();
            for(int i=2;i<=4;i++)four.Buildings.Add(BuildingAreaService.CreateBuilding(Snapshot(10*i,"extra"+i),new BuildingCaptureInput{BuildingNumber=i+"幢",Floor="一层"}));
            string surveyTemplate=Path.Combine(Path.GetDirectoryName(templatePath),"地籍调查表.docx");
            string survey=Path.Combine(directory,"四幢调查表.docx");ParcelSurveyWordExporter.Export(surveyTemplate,survey,four);
            using(var zip=ZipFile.OpenRead(survey))using(var stream=zip.GetEntry("word/document.xml").Open())
            {
                var doc=XDocument.Load(stream);var houses=doc.Descendants(w+"tbl").Where(t=>t.Descendants(w+"t").Any(x=>x.Value.Contains("房屋基本信息调查表"))).ToList();
                Check(houses.Count==1&&houses[0].Elements(w+"tr").Count()==18,"四幢在同一表中使用四条数据行");
                Check(string.Concat(houses[0].Elements(w+"tr").ElementAt(14).Descendants(w+"t").Select(t=>t.Value)).Contains("3幢"),"第三幢位于第一页第三行");
                Check(string.Concat(houses[0].Elements(w+"tr").ElementAt(15).Descendants(w+"t").Select(t=>t.Value)).Contains("4幢"),"第四幢位于第四行");
            }
            foreach (int count in new[] {0,1,2,7,20})
            {
                var variable=Example();variable.Buildings.Clear();
                for(int i=1;i<=count;i++) variable.Buildings.Add(BuildingAreaService.CreateBuilding(Snapshot(i*10,"n"+i),
                    new BuildingCaptureInput{BuildingNumber=i+"幢",Floor="一层"}));
                variable.Field("project.rightsSurveyor").TextValue="调查员示例";
                variable.Field("project.rightsSurveyDate").TextValue=count==0?"":"2026-12-21";
                var path=Path.Combine(directory,count+"幢调查表.docx");ParcelSurveyWordExporter.Export(surveyTemplate,path,variable);
                using(var zip=ZipFile.OpenRead(path))using(var stream=zip.GetEntry("word/document.xml").Open())
                {
                    var doc=XDocument.Load(stream);var house=doc.Descendants(w+"tbl").Single(t=>t.Descendants(w+"t").Any(x=>x.Value.Contains("房屋基本信息调查表")));
                    var rows=house.Elements(w+"tr").ToList();
                    Check(rows.Count==14+count,"动态增减房屋行，不能留下模板空行或截断超额房屋");
                    Check(rows.Take(12).All(row=>row.Element(w+"trPr").Element(w+"tblHeader")!=null),"文档设置连续表头的重复属性");
                    Check(string.Concat(rows[12+count].Descendants(w+"t").Select(t=>t.Value)).Contains("房产草图"),"房屋增减后草图和说明仍在最后");
                    for(int i=0;i<count;i++)
                    {
                        Check(string.Concat(rows[12+i].Elements(w+"tc").ElementAt(1).Descendants(w+"t").Select(t=>t.Value))==(i+1)+"幢","每幢独立一行且顺序不变");
                        Check((string)rows[12+i].Elements(w+"tc").First().Element(w+"tcPr").Element(w+"vMerge").Attribute(w+"val")=="continue","房屋状况纵向合并延伸到新增行");
                    }
                    var footer=house.ElementsAfterSelf().First();string text=string.Concat(footer.Descendants(w+"t").Select(t=>t.Value));
                    Check(text.Contains("日期：")&&text.Contains("年")&&text.Contains("月")&&text.Contains("日"),"空日期也保留年月日占位符");
                    if(count>0)Check(text.Contains("日期：2026年12月21日"),"分别替换年月日数字");
                    Check(!footer.Descendants(w+"tab").Any(),"不再将模板日期改成表格右端制表位");
                }
            }
        }
    }
}
