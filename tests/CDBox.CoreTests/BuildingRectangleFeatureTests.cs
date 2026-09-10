using System;
using System.Linq;
using CDBox.RealEstate.Geometry;

namespace CDBox.CoreTests
{
    internal static class BuildingRectangleFeatureTests
    {
        private static BuildingPoint2 P(double x, double y) => new BuildingPoint2(x, y);
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Geometry()
        {
            var outlines = new[] {
                new[] { P(0,0),P(21.79,0),P(21.79,13.21),P(14.78,13.21),P(14.78,8.45),P(6.79,8.45),P(6.79,13.21),P(0,13.21) },
                new[] { P(0,0),P(6,0),P(6,1),P(2,1),P(2,5),P(0,5) },
                new[] { P(0,0),P(3,0),P(3,4),P(5,4),P(5,6),P(-2,6),P(-2,4),P(0,4) },
                new[] { P(0,0),P(6,0),P(6,2),P(4,2),P(4,4),P(2,4),P(2,6),P(0,6) }
            };
            int[] counts = {3,2,2,3}; double[] areas = {249.8135,14,26,24};
            for (int i=0;i<outlines.Length;i++)
            {
                var source=outlines[i]; var original=BuildingLengthAnnotationPlanner.Create(source,.2,false);
                foreach(var shape in new[] {source,Enumerable.Reverse(source).ToArray(),source.Skip(2).Concat(source.Take(2)).ToArray(),
                    source.Select(p=>P(500000+p.X*Math.Cos(.37)-p.Y*Math.Sin(.37),3500000+p.X*Math.Sin(.37)+p.Y*Math.Cos(.37))).ToArray()})
                {
                    var plan=BuildingLengthAnnotationPlanner.Create(shape,.2,false);
                    Check(plan.IsOrthogonal&&plan.AreaTerms.Count==counts[i]&&plan.AreaTerms.All(t=>t.Method=="rectangle"),"旋转、反向和大坐标直角图形均优先矩形分割");
                    Check(Math.Abs(plan.GeometryArea-areas[i])<1e-6&&Math.Abs(plan.AreaTerms.Sum(t=>t.GeometryArea)-areas[i])<1e-6,"矩形面积无遗漏或重复");
                    Check(plan.CalculatedArea==original.CalculatedArea,"旋转不改变按尺寸公式计算的面积");
                    Check(plan.AreaTerms.All(t=>t.Area==t.BaseLength*t.Height),"矩形使用长乘宽计算");
                    Check(!plan.AuxiliarySegments.Any(s=>s.IsHeight),"矩形不绘制三角形高度");
                }
            }
            var u=BuildingLengthAnnotationPlanner.Create(outlines[0],.2,false);
            Check(u.AuxiliarySegments.Count(s=>s.Draw)==2&&Math.Abs(u.AuxiliarySegments.Where(s=>s.Draw).Sum(s=>s.Length)-13.8)<1e-7,"图示 U 形使用两条较短的水平分割线");
            Check(!u.AuxiliarySegments.Any(s=>s.Annotate)&&u.AreaFormula.Contains("13.21 − 4.76"),"使用已有边长推算下部高度，不增加冗余注记");
            Check(u.CalculatedArea==249.81m,"图示尺寸的公式结果为 249.81 平方米");
            // Rectangles must cover the source, not its notch or bounding box.
            for (double x=.25;x<21.79;x+=.5) for(double y=.25;y<13.21;y+=.5)
            {
                bool inside=x<6.79||x>14.78||y<8.45;
                int covered=u.AreaTerms.Count(t=>x>t.Vertices.Min(p=>p.X)&&x<t.Vertices.Max(p=>p.X)
                    &&y>t.Vertices.Min(p=>p.Y)&&y<t.Vertices.Max(p=>p.Y));
                Check(covered==(inside?1:0),"矩形分割不覆盖凹口，不重叠");
            }
            var arc=BuildingCurvedBoundaryPlanner.Create(outlines[0],new[]{.1,0d,0d,0d,0d,0d,0d,0d},.2,false);
            Check(arc.AreaTerms.Count(t=>t.Method=="rectangle")==3&&arc.AreaTerms.Count(t=>t.Method=="arcSegment")==1,"圆弧修正可与矩形直线部分组合");
        }
    }
}
