using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CDBox.RealEstate.Models;

namespace CDBox.RealEstate.Geometry
{
    /// <summary>Analytic circular-segment corrections; no tessellated area is used in the result.</summary>
    public static class BuildingCurvedBoundaryPlanner
    {
        public static BuildingAnnotationPlan Create(IList<BuildingPoint2> vertices, IList<double> bulges,
            double textHeight, bool adaptive)
        {
            if (vertices == null || bulges == null || vertices.Count != bulges.Count || vertices.Count < 2)
                throw new ArgumentException("圆弧边界的顶点和弧度数据不完整。");
            if (bulges.All(b => Math.Abs(b)<1e-9)) return BuildingLengthAnnotationPlanner.Create(vertices,textHeight,adaptive);
            var edges = Enumerable.Range(0,vertices.Count).Select(i=>new BuildingAreaBoundaryEdge {
                Start=P(vertices[i]),End=P(vertices[(i+1)%vertices.Count]),Bulge=bulges[i]}).ToList();
            BuildingBoundaryMath.Validate(edges);
            double chordSigned = 0;
            for(int i=0;i<vertices.Count;i++) chordSigned+=Cross(vertices[i]-vertices[0],vertices[(i+1)%vertices.Count]-vertices[0])/2;
            double raw = chordSigned + edges.Where(IsArc).Sum(e=>{
                var arc=BuildingBoundaryMath.Arc(e);return arc.Radius*arc.Radius*(arc.Sweep-Math.Sin(arc.Sweep))/2;});
            if(Math.Abs(raw)<1e-9)throw new ArgumentException("房屋圆弧边界面积为零。");
            double orientation=Math.Sign(raw);
            if(Math.Abs(chordSigned)>1e-9 && Math.Sign(chordSigned)!=orientation)
                throw new ArgumentException("圆弧边界的弦多边形方向异常，请拆分后选择。");
            BuildingAnnotationPlan plan = Math.Abs(chordSigned)>1e-9
                ? BuildingLengthAnnotationPlanner.Create(vertices,textHeight,adaptive) : new BuildingAnnotationPlan {TextHeight=textHeight};
            var chordSegments=plan.BoundarySegments.ToList();plan.BoundarySegments.Clear();plan.Boundary.Clear();plan.Boundary.AddRange(edges);
            foreach(var edge in edges)
            {
                var a=V(edge.Start);var b=V(edge.End);
                if(!IsArc(edge))
                {
                    plan.BoundarySegments.Add(Segment(a,b,plan.TextHeight,false));continue;
                }
                var arc=BuildingBoundaryMath.Arc(edge);double theta=Math.Abs(arc.Sweep),sign=Math.Sign(arc.Sweep)*orientation;
                double midAngle=arc.StartAngle+arc.Sweep/2;
                var midpoint=new BuildingPoint2(arc.Center.X+arc.Radius*Math.Cos(midAngle),arc.Center.Y+arc.Radius*Math.Sin(midAngle));
                var boundary=Segment(a,b,plan.TextHeight,false);
                boundary.Length=arc.Radius*theta;boundary.Text=N(M(boundary.Length));
                boundary.TextPosition=midpoint+(midpoint-V(arc.Center))*(plan.TextHeight*.95/arc.Radius);
                boundary.TextRotation=Rotation(midAngle+Math.PI/2);plan.BoundarySegments.Add(boundary);
                // Retain the arc's chord only where it supports a straight-part triangle/rectangle formula.
                foreach(var chord in chordSegments.Where(s=>Same(s.Start,a)&&Same(s.End,b)||Same(s.Start,b)&&Same(s.End,a)))
                {
                    bool needed=plan.AreaTerms.Any(t=>Same(V(t.BaseStart),a)&&Same(V(t.BaseEnd),b)
                        || Same(V(t.BaseStart),b)&&Same(V(t.BaseEnd),a)
                        || t.Method=="rectangle");
                    if(needed) {chord.IsAuxiliary=true;plan.AuxiliarySegments.Add(chord);}
                }
                decimal radius=M(arc.Radius), degrees=Math.Round((decimal)(theta*180/Math.PI),6,MidpointRounding.AwayFromZero);
                if(radius<=0)throw new ArgumentException("圆弧半径在注记精度下为零。");
                decimal pi=3.141592653589793m;
                decimal angleRadians=degrees*pi/180m;
                decimal area=(decimal)sign*radius*radius*(angleRadians-(decimal)Math.Sin((double)angleRadians))/2m;
                var radial=Segment(V(arc.Center),midpoint,plan.TextHeight,true);radial.Text="R"+N(radius);plan.AuxiliarySegments.Add(radial);
                string r=N(radius),d=degrees.ToString("0.######",CultureInfo.InvariantCulture);
                string expression=d+" ÷ 360 × π × "+r+"² − "+r+"² × sin("+d+"°) ÷ 2";
                plan.AreaTerms.Add(new BuildingAreaTerm {Sequence=plan.AreaTerms.Count+1,Method="arcSegment",
                    BaseStart=edge.Start,BaseEnd=edge.End,Apex=P(midpoint),HeightFoot=arc.Center,
                    Vertices=new List<BuildingAreaPoint>{edge.Start,edge.End,P(midpoint)},Radius=radius,AngleDegrees=degrees,
                    Area=area,GeometryArea=sign*arc.Radius*arc.Radius*(theta-Math.Sin(theta))/2,
                    Formula=(sign<0?"−(":"(")+expression+")"});
            }
            plan.GeometryArea=Math.Abs(raw);plan.CanCalculateAreaFromBoundary=false;
            plan.CalculatedArea=Math.Round(plan.AreaTerms.Sum(t=>t.Area),2,MidpointRounding.AwayFromZero);
            plan.AreaFormula=string.Join(" + ",plan.AreaTerms.Select(t=>"("+t.Formula+")"))+" = "+N(plan.CalculatedArea);
            if(Math.Abs(plan.AreaTerms.Sum(t=>t.GeometryArea)-plan.GeometryArea)>Math.Max(1e-7,plan.GeometryArea*1e-8))
                throw new ArgumentException("圆弧分项面积校验失败。");
            return plan;
        }
        private static bool IsArc(BuildingAreaBoundaryEdge e)=>Math.Abs(e.Bulge)>1e-9;
        private static bool Same(BuildingPoint2 a,BuildingPoint2 b)=>(a-b).Length<1e-7;
        private static BuildingPoint2 V(BuildingAreaPoint p)=>new BuildingPoint2(p.X,p.Y);
        private static BuildingAreaPoint P(BuildingPoint2 p)=>new BuildingAreaPoint{X=p.X,Y=p.Y};
        private static decimal M(double n)=>Math.Round((decimal)n,2,MidpointRounding.AwayFromZero);
        private static string N(decimal n)=>n.ToString("0.00",CultureInfo.InvariantCulture);
        private static double Cross(BuildingPoint2 a,BuildingPoint2 b)=>a.X*b.Y-a.Y*b.X;
        private static double Rotation(double angle){while(angle>Math.PI/2)angle-=Math.PI;while(angle< -Math.PI/2)angle+=Math.PI;return angle;}
        private static BuildingPlannedSegment Segment(BuildingPoint2 a,BuildingPoint2 b,double height,bool auxiliary)
        {
            var v=b-a;double len=v.Length;if(len<1e-9)throw new ArgumentException("圆弧边界存在重复顶点。");
            return new BuildingPlannedSegment{Start=a,End=b,Length=len,Text=N(M(len)),IsAuxiliary=auxiliary,
                TextRotation=Rotation(Math.Atan2(v.Y,v.X)),TextPosition=(a+b)*.5+new BuildingPoint2(-v.Y/len,v.X/len)*height*.8};
        }
    }
}
