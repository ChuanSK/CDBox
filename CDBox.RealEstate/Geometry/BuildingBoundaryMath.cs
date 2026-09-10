using System;
using System.Collections.Generic;
using System.Linq;
using CDBox.RealEstate.Models;

namespace CDBox.RealEstate.Geometry
{
    public static class BuildingBoundaryMath
    {
        private const double Epsilon=1e-7;
        public sealed class CircularArc { public BuildingAreaPoint Center; public double Radius,StartAngle,Sweep; }
        public static CircularArc Arc(BuildingAreaBoundaryEdge edge)
        {
            double dx=edge.End.X-edge.Start.X,dy=edge.End.Y-edge.Start.Y,b=edge.Bulge;
            double length=Math.Sqrt(dx*dx+dy*dy);
            if(length<Epsilon||Math.Abs(b)<1e-12||double.IsNaN(b)||double.IsInfinity(b))throw new ArgumentException("圆弧参数无效。");
            var center=new BuildingAreaPoint{X=(edge.Start.X+edge.End.X)/2-dy*(1-b*b)/(4*b),Y=(edge.Start.Y+edge.End.Y)/2+dx*(1-b*b)/(4*b)};
            return new CircularArc{Center=center,Radius=length*(1+b*b)/(4*Math.Abs(b)),StartAngle=Math.Atan2(edge.Start.Y-center.Y,edge.Start.X-center.X),Sweep=4*Math.Atan(b)};
        }
        public static BuildingAreaPoint PointAt(BuildingAreaBoundaryEdge e,double t)
        {
            if(Math.Abs(e.Bulge)<1e-9)return P(e.Start.X+(e.End.X-e.Start.X)*t,e.Start.Y+(e.End.Y-e.Start.Y)*t);
            var a=Arc(e);return P(a.Center.X+a.Radius*Math.Cos(a.StartAngle+a.Sweep*t),a.Center.Y+a.Radius*Math.Sin(a.StartAngle+a.Sweep*t));
        }
        public static bool ContainsContour(IList<BuildingAreaBoundaryEdge> outer,IList<BuildingAreaBoundaryEdge> inner)
        {
            if(outer==null||inner==null||outer.Count<2||inner.Count<2)return false;
            foreach(var edge in inner)
            {
                var cuts=new List<double>{0,1};
                foreach(var boundary in outer)foreach(var point in Intersections(edge,boundary))cuts.Add(Parameter(edge,point));
                cuts=cuts.Where(t=>t>=-Epsilon&&t<=1+Epsilon).Select(t=>Math.Max(0,Math.Min(1,t))).OrderBy(t=>t).ToList();
                for(int i=0;i<cuts.Count;i++)
                {
                    if(!ContainsPoint(outer,PointAt(edge,cuts[i])))return false;
                    if(i>0&&!ContainsPoint(outer,PointAt(edge,(cuts[i-1]+cuts[i])/2)))return false;
                }
            }
            return true;
        }
        public static bool ContainsPoint(IList<BuildingAreaBoundaryEdge> edges,BuildingAreaPoint p)
        {
            bool inside=false;
            foreach(var e in edges)
            {
                if(On(e,p))return true;
                if(Math.Abs(e.Bulge)<1e-9)
                {
                    if((e.Start.Y>p.Y)!=(e.End.Y>p.Y)&&p.X<(e.End.X-e.Start.X)*(p.Y-e.Start.Y)/(e.End.Y-e.Start.Y)+e.Start.X)inside=!inside;
                    continue;
                }
                var arc=Arc(e);var cuts=new List<double>{0,1};
                foreach(double angle in new[]{Math.PI/2,3*Math.PI/2})
                {double t=AngleParameter(arc,angle);if(t>0&&t<1)cuts.Add(t);}
                cuts.Sort();double y=(p.Y-arc.Center.Y)/arc.Radius;if(y< -1||y>1)continue;
                double first=Math.Asin(Math.Max(-1,Math.Min(1,y)));
                var roots=new[]{first,Math.PI-first};
                for(int i=1;i<cuts.Count;i++)
                {
                    if((PointAt(e,cuts[i-1]).Y>p.Y)==(PointAt(e,cuts[i]).Y>p.Y))continue;
                    foreach(var angle in roots)
                    {
                        double t=AngleParameter(arc,angle);
                        if(t>=cuts[i-1]-1e-10&&t<=cuts[i]+1e-10&&arc.Center.X+arc.Radius*Math.Cos(angle)>p.X){inside=!inside;break;}
                    }
                }
            }
            return inside;
        }
        public static void Validate(IList<BuildingAreaBoundaryEdge> edges)
        {
            for(int i=0;i<edges.Count;i++)
            {
                var e=edges[i];
                if(!Finite(e.Start.X)||!Finite(e.Start.Y)||!Finite(e.End.X)||!Finite(e.End.Y)||!Finite(e.Bulge)||Distance(e.Start,e.End)<Epsilon)
                    throw new ArgumentException("边界存在无效参数或重复顶点。");
                for(int j=i+1;j<edges.Count;j++)
                {
                    var f=edges[j];bool adjacent=j==i+1||i==0&&j==edges.Count-1;
                    foreach(var p in Intersections(e,f))
                        if(!adjacent||!(Near(p,e.Start)||Near(p,e.End))||!(Near(p,f.Start)||Near(p,f.End)))
                            throw new ArgumentException("房屋边界存在自交或圆弧重叠。");
                    if(On(f,PointAt(e,.5))||On(e,PointAt(f,.5)))throw new ArgumentException("房屋边界存在重叠边。");
                }
            }
        }
        public static IEnumerable<BuildingAreaPoint> Intersections(BuildingAreaBoundaryEdge a,BuildingAreaBoundaryEdge b)
        {
            bool ca=Math.Abs(a.Bulge)>=1e-9,cb=Math.Abs(b.Bulge)>=1e-9;
            if(!ca&&!cb)
            {
                double ax=a.End.X-a.Start.X,ay=a.End.Y-a.Start.Y,bx=b.End.X-b.Start.X,by=b.End.Y-b.Start.Y;
                double cross=ax*by-ay*bx;
                if(Math.Abs(cross)<1e-12){foreach(var p in new[]{a.Start,a.End,b.Start,b.End})if(On(a,p)&&On(b,p))yield return p;yield break;}
                double t=((b.Start.X-a.Start.X)*by-(b.Start.Y-a.Start.Y)*bx)/cross;
                var point=PointAt(a,t);if(t>=-Epsilon&&t<=1+Epsilon&&On(b,point))yield return point;yield break;
            }
            if(!ca||!cb)
            {
                var line=ca?b:a;var curved=ca?a:b;var arc=Arc(curved);
                double dx=line.End.X-line.Start.X,dy=line.End.Y-line.Start.Y,x=line.Start.X-arc.Center.X,y=line.Start.Y-arc.Center.Y;
                double q=dx*dx+dy*dy,h=x*dx+y*dy,disc=h*h-q*(x*x+y*y-arc.Radius*arc.Radius);
                if(disc< -Epsilon)yield break;
                double root=Math.Sqrt(Math.Max(0,disc));
                foreach(double t in new[]{(-h-root)/q,(-h+root)/q})
                {var p=PointAt(line,t);if(t>=-Epsilon&&t<=1+Epsilon&&On(curved,p))yield return p;}
                yield break;
            }
            var aa=Arc(a);var bb=Arc(b);double distance=Distance(aa.Center,bb.Center);
            if(distance<Epsilon)
            {if(Math.Abs(aa.Radius-bb.Radius)<Epsilon)foreach(var p in new[]{a.Start,a.End,b.Start,b.End})if(On(a,p)&&On(b,p))yield return p;yield break;}
            if(distance>aa.Radius+bb.Radius+Epsilon||distance<Math.Abs(aa.Radius-bb.Radius)-Epsilon)yield break;
            double along=(aa.Radius*aa.Radius-bb.Radius*bb.Radius+distance*distance)/(2*distance),height=Math.Sqrt(Math.Max(0,aa.Radius*aa.Radius-along*along));
            double ux=(bb.Center.X-aa.Center.X)/distance,uy=(bb.Center.Y-aa.Center.Y)/distance;
            foreach(double sign in new[]{-1d,1d})
            {var p=P(aa.Center.X+ux*along-sign*uy*height,aa.Center.Y+uy*along+sign*ux*height);if(On(a,p)&&On(b,p))yield return p;}
        }
        private static double Parameter(BuildingAreaBoundaryEdge e,BuildingAreaPoint p)
        {
            if(Math.Abs(e.Bulge)>=1e-9){var a=Arc(e);return AngleParameter(a,Math.Atan2(p.Y-a.Center.Y,p.X-a.Center.X));}
            double dx=e.End.X-e.Start.X,dy=e.End.Y-e.Start.Y;return ((p.X-e.Start.X)*dx+(p.Y-e.Start.Y)*dy)/(dx*dx+dy*dy);
        }
        private static double AngleParameter(CircularArc a,double angle)
        {double delta=a.Sweep>0?angle-a.StartAngle:a.StartAngle-angle;delta%=2*Math.PI;if(delta< -1e-10)delta+=2*Math.PI;return delta/Math.Abs(a.Sweep);}
        private static bool On(BuildingAreaBoundaryEdge e,BuildingAreaPoint p)
        {
            if(Near(p,e.Start)||Near(p,e.End))return true;double t=Parameter(e,p);if(t< -1e-9||t>1+1e-9)return false;
            if(Math.Abs(e.Bulge)>=1e-9){var a=Arc(e);return Math.Abs(Distance(a.Center,p)-a.Radius)<Epsilon;}
            return Distance(PointAt(e,t),p)<Epsilon;
        }
        private static bool Near(BuildingAreaPoint a,BuildingAreaPoint b)=>Distance(a,b)<Epsilon;
        private static double Distance(BuildingAreaPoint a,BuildingAreaPoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
        private static BuildingAreaPoint P(double x,double y)=>new BuildingAreaPoint{X=x,Y=y};
        private static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
    }
}
