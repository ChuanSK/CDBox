using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using CDBox.RealEstate.Geometry;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.Services;
using CDBox.RealEstate.Settings;

namespace CDBox.CoreTests
{
    internal static class BuildingAreaFeatureTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Near(double expected, double actual, string message) => Check(Math.Abs(expected - actual) < 1e-7, message);
        private static void Reject(Action action, string message)
        { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; } Check(rejected, message); }
        private static BuildingPoint2 P(double x, double y) => new BuildingPoint2(x, y);
        private static BuildingAnnotationPlan Plan(params BuildingPoint2[] points) => BuildingLengthAnnotationPlanner.Create(points, .5, true);
        private static BuildingAnnotationPlan Rectangle(double w, double h) => Plan(P(0,0),P(w,0),P(w,h),P(0,h));
        private static BuildingAreaCalculation Calculation(string suffix = "") => BuildingAreaService.Calculate("drawing-A", new[] {
            BuildingAreaService.Component("A1"+suffix,"房屋",Rectangle(10,10)),
            BuildingAreaService.Component("A2"+suffix,"中空",Rectangle(2,5)),
            BuildingAreaService.Component("A3"+suffix,"半封",Rectangle(4,2)) });

        public static void Geometry()
        {
            var polygons = new[] {
                new[] {P(0,0),P(4,0),P(2,2),P(4,4),P(0,4)},
                new[] {P(0,0),P(5,0),P(1,1)},
                new[] {P(0,0),P(5,0),P(7,3),P(2,3)} };
            double[] expected = {12,2.5,15};
            for (int index=0;index<polygons.Length;index++)
            {
                var points=polygons[index];var plan=Plan(points);
                Near(expected[index],plan.GeometryArea,"已知图形的几何面积必须正确");
                Check(plan.AreaTerms.Count==points.Length-2,"三角划分必须完整");
                Check(plan.AuxiliarySegments.Count(s=>s.IsHeight)==plan.AreaTerms.Count,"每个三角形均需绘制高度");
                Near(plan.GeometryArea,plan.AreaTerms.Sum(t=>t.GeometryArea),"所有三角形几何面积必须闭合");
                foreach(var t in plan.AreaTerms)
                {
                    double dx=t.BaseEnd.X-t.BaseStart.X,dy=t.BaseEnd.Y-t.BaseStart.Y;
                    double hx=t.Apex.X-t.HeightFoot.X,hy=t.Apex.Y-t.HeightFoot.Y;
                    Near(0,dx*hx+dy*hy,"高度必须垂直于公式使用的底边");
                    double q=((t.HeightFoot.X-t.BaseStart.X)*dx+(t.HeightFoot.Y-t.BaseStart.Y)*dy)/(dx*dx+dy*dy);
                    Check(q>=-1e-9&&q<=1+1e-9,"最长边上的垂足必须位于三角形内部");
                    Check(t.Area==t.BaseLength*t.Height/2m,"面积严格使用已注记的底高计算");
                    Check(plan.AuxiliarySegments.Any(s=>s.IsHeight&&Math.Abs(s.Start.X-t.Apex.X)<1e-9&&Math.Abs(s.End.Y-t.HeightFoot.Y)<1e-9),"公式的高度在 CAD 规划中存在");
                }
                var reversed=Plan(Enumerable.Reverse(points).ToArray());
                Check(plan.AreaFormula==reversed.AreaFormula,"顺逆时针应产生同一分割及公式，避免舍入结果漂移");
                var shifted=Plan(points.Skip(2).Concat(points.Take(2)).ToArray());
                Check(plan.AreaFormula==shifted.AreaFormula,"闭合线起点变化不影响结果");
            }
            var far=Plan(P(500000,3500000),P(500006,3500000),P(500006,3500001),P(500002,3500001),P(500002,3500005),P(500000,3500005));
            Near(14,far.GeometryArea,"大坐标不丢失面积精度");
            Check(Rectangle(3.65,2.57).CalculatedArea==9.38m,"矩形面积按两位小数输出");
            var halfway = Rectangle(1.005, 2.015);
            Check(halfway.AreaTerms[0].BaseLength == 1.01m && halfway.AreaTerms[0].Height == 2.02m,
                "半分位按四舍五入计算");
            Check(halfway.BoundarySegments[0].Text == "1.01" && halfway.BoundarySegments[1].Text == "2.02",
                "图上注记值与公式使用的值严格一致");
            Check(!Plan(P(0,0),P(100,0),P(100.3,100),P(.3,100)).CanCalculateAreaFromBoundary,
                "略微倾斜的平行四边形也必须三角划分，不能误当矩形");
            Check(Plan(P(0,0),P(2,0),P(4,0),P(4,3),P(0,3),P(0,0)).AreaTerms.Count==1,"重复闭合点和共线点可规范化");
            Reject(()=>Plan(P(0,0),P(4,4),P(0,4),P(4,0)),"自交边界必须拒绝");
            Reject(()=>Plan(P(0,0),P(1,0),P(2,0)),"零面积边界必须拒绝");
        }

        public static void WeightedArea()
        {
            var c=Calculation();Check(c.FullArea==100&&c.HollowArea==10&&c.HalfEnclosedArea==8&&c.Area==94,"100−10+8×0.5=94");
            Check(BuildingAreaService.ClassifyLayer("房屋中空说明")==BuildingAreaRole.Full,"识别完整图层名而非任意文字片段");
            Check(BuildingAreaService.ClassifyLayer("外参|中空")==BuildingAreaRole.Hollow,"外参图层前缀不改变角色");
            var b=BuildingAreaService.CreateBuilding(c,new BuildingCaptureInput {TotalFloors="3"});
            Check(b.Fields["building.area"].NumericValue==94&&b.Fields["building.number"].TextValue=="","编号可空且总层数不重复乘算");
            Check(!b.Fields["building.footprintArea"].NumericValue.HasValue,"不得推断或覆盖占地面积");
            var record=new ParcelSurveyRecord();record.Normalize();record.Buildings.Add(b);
            record.Field(ParcelSurveyFieldKeys.BuildingFootprintTotal).NumericValue=123;
            BuildingAreaService.Synchronize(record);
            Check(record.Field(ParcelSurveyFieldKeys.BuildingAreaTotal).NumericValue==94&&record.Field(ParcelSurveyFieldKeys.BuildingAreaTotal).Status==ParcelFieldStatus.Automatic,"建筑总面积自动汇总");
            Check(!record.Field(ParcelSurveyFieldKeys.BuildingFootprintTotal).NumericValue.HasValue,"没有确认底层时占地面积等待汇总");
            record.Buildings.Add(new ParcelBuildingRecord());BuildingAreaService.Synchronize(record);
            Check(!record.Field(ParcelSurveyFieldKeys.BuildingAreaTotal).NumericValue.HasValue,"未填完整时不能把部分和显示为建筑总面积");
            record.Buildings.Clear();BuildingAreaService.Synchronize(record);Check(record.Field(ParcelSurveyFieldKeys.BuildingAreaTotal).NumericValue==0,"删除最后一间后自动归零");
            Reject(()=>BuildingAreaService.CreateBuilding(c,new BuildingCaptureInput {TotalFloors="1.5"}),"非整数层数不能通过");
        }

        private static void WithStore(Action<ParcelSurveyStore> action)
        {
            string root=Path.Combine(Path.GetTempPath(),"CDBoxBuildingTests-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try {action(new ParcelSurveyStore(Path.Combine(root,"data.json")));}
            finally {Directory.Delete(root,true);}
        }
        private static ParcelSurveyRecord Parcel(ParcelSurveyStore store)
        {
            var r=new ParcelSurveyRecord{DocumentId="drawing-A",DocumentName="示例.dwg",ScopeType="parcel",ParcelId="parcel-A",ParcelName="甲宗地"};
            return store.Save(r);
        }
        public static void Persistence() => WithStore(store=>{
            var parcel=Parcel(store);var stale=store.GetRecord(parcel.Id);
            var added=BuildingAreaService.CreateBuilding(Calculation(),new BuildingCaptureInput());
            store.AddCapturedBuilding("drawing-A",parcel.Id,added);
            stale.Field("project.name").TextValue="未丢失的草稿";
            var merged=store.SavePreservingCurrentScope(stale);
            Check(merged.Buildings.Count==1&&merged.BuildingsRevision==1,"旧草稿保存不能删除刚添加的房屋");
            var read=store.GetRecord(parcel.Id);
            Check(read.Field("project.name").TextValue=="未丢失的草稿"&&read.Field(ParcelSurveyFieldKeys.BuildingAreaTotal).NumericValue==94,"普通草稿和派生值同时正确");
            Check(read.Buildings[0].AreaCalculation.Components[0].Terms[0].BaseEnd.X==10,"公式和辅助线坐标持久化回读");
            var data=BuildingAreaTableDataSource.Create(read).Single();
            Check(data.Calculation.Components.Count==3&&data.Calculation.Formula.Contains("0.5"),"导出接口保留公式、结果及计入系数");
            read.Buildings[0].AreaCalculation.Area=999;read.Buildings[0].Fields["building.area"].NumericValue=999;
            store.Save(read);Check(store.GetRecord(parcel.Id).Buildings[0].Fields["building.area"].NumericValue==94,"旧页面不能改写已测量公式来源");
            read=store.GetRecord(parcel.Id);read.Buildings.Clear();store.Save(read);
            Check(store.GetRecord(parcel.Id).Buildings.Count==0,"已知版本可以正常删除房屋");
        });
        public static void FailureSafety() => WithStore(store=>{
            var r=Parcel(store);var b=BuildingAreaService.CreateBuilding(Calculation(),new BuildingCaptureInput());
            Reject(()=>store.AddCapturedBuilding("drawing-A",r.Id,b,()=>{throw new InvalidOperationException("模拟绘图事务失败");}),"CAD 提交失败应上报");
            Check(store.GetRecord(r.Id).Buildings.Count==0,"CAD 提交失败应回滚调查记录");
            store.AddCapturedBuilding("drawing-A",r.Id,b);
            Reject(()=>store.AddCapturedBuilding("drawing-A",r.Id,BuildingAreaService.CreateBuilding(Calculation(),new BuildingCaptureInput())),"重复图形不能再次计入");
            Reject(()=>store.AddCapturedBuilding("drawing-B",r.Id,BuildingAreaService.CreateBuilding(Calculation("B"),new BuildingCaptureInput())),"不得写入另一张图纸");
            Reject(()=>store.AddCapturedBuilding("drawing-A","deleted",BuildingAreaService.CreateBuilding(Calculation("B"),new BuildingCaptureInput())),"已删除宗地不得被恢复");
            Check(store.GetRecord(r.Id).Buildings.Count==1,"拒绝操作不改变已有房屋数量");
        });
    }
}
