using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using CDBox.RealEstate.Models;

namespace CDBox.RealEstate.Services
{
    public static class BuildingPropertyWordExporter
    {
        public const string TemplateFileName="房产.docx";
        private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        public static string DefaultFileName(ParcelSurveyRecord record)
        {
            string name=Name(record);foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');
            return name.TrimEnd(' ','.')+"房产.docx";
        }
        public static void Export(string outputPath,ParcelSurveyRecord record)
        {
            string folder=Path.GetDirectoryName(typeof(BuildingPropertyWordExporter).Assembly.Location);
            string template=Path.Combine(folder,"Templates",TemplateFileName);
            Export(template,outputPath,record);
        }
        public static void Export(string templatePath,string outputPath,ParcelSurveyRecord record)
        {
            if(record==null)throw new ArgumentNullException(nameof(record));
            if(!File.Exists(templatePath))throw new FileNotFoundException("未找到房产文档模板。",templatePath);
            if(string.IsNullOrWhiteSpace(outputPath))throw new ArgumentException("未指定房产文档保存路径。");
            if(string.Equals(Path.GetFullPath(templatePath),Path.GetFullPath(outputPath),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("不能覆盖房产模板。");
            BuildingAreaService.Synchronize(record);
            var folder=Path.GetDirectoryName(Path.GetFullPath(outputPath));Directory.CreateDirectory(folder);
            string temp=Path.Combine(folder,".cdbox-property-"+Guid.NewGuid().ToString("N")+".docx");
            try
            {
                File.Copy(templatePath,temp);
                using(var file=new FileStream(temp,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                using(var zip=new ZipArchive(file,ZipArchiveMode.Update))
                {
                    var entry=zip.GetEntry("word/document.xml");XDocument doc;
                    using(var stream=entry.Open())doc=XDocument.Load(stream,LoadOptions.PreserveWhitespace);
                    var table=doc.Root.Element(W+"body").Elements(W+"tbl").Single();var rows=table.Elements(W+"tr").ToList();
                    if(rows.Count<13)throw new InvalidDataException("房产模板的明细、小计或合计行缺失。");
                    var detail=new XElement(rows[2]);var subtotal=new XElement(rows[3]);var footprint=new XElement(rows[4]);
                    var total=new XElement(rows[11]);var landTotal=new XElement(rows[12]);
                    SetText(rows[0].Elements(W+"tc").First(),Name(record)+"房屋面积测算表");
                    foreach(var row in rows.Skip(2))row.Remove();
                    Header(rows[0]);Header(rows[1]);
                    string owner=Owner(record);int globalRow=0;
                    foreach(var building in record.Buildings)
                    {
                        var floors=building.FloorAreas??new List<BuildingFloorArea>();
                        int count=Math.Max(1,floors.Count);
                        for(int i=0;i<count;i++)
                        {
                            var row=new XElement(detail);var cells=row.Elements(W+"tc").ToList();ClearRow(row);
                            SetText(cells[0],i==0?Text(building,"building.number"):"");
                            SetText(cells[1],globalRow==0?owner:"");SetText(cells[2],i==0?Text(building,"building.structure"):"");
                            var floor=i<floors.Count?floors[i]:null;
                            SetText(cells[3],floor==null?Text(building,"building.floor"):floor.Name);
                            SetText(cells[4],floor==null?"人工录入":BuildingAreaService.RowExpression(floor));
                            SetText(cells[5],floor==null?Area(building,"building.area"):N(BuildingAreaService.RowArea(floor)));
                            Merge(cells[0],i==0);Merge(cells[1],globalRow++==0);Merge(cells[2],i==0);table.Add(row);
                        }
                        var sub=new XElement(subtotal);var sc=sub.Elements(W+"tc").ToList();ClearRow(sub);
                        SetText(sc[3],"小计");SetText(sc[4],Area(building,"building.area"));
                        for(int col=0;col<3;col++)Merge(sc[col],false);table.Add(sub);globalRow++;
                        var ground=new XElement(footprint);var gc=ground.Elements(W+"tc").ToList();ClearRow(ground);
                        SetText(gc[3],"占地");SetText(gc[4],string.Join(" + ",floors.Where(BuildingAreaService.IsGround).Select(BuildingAreaService.RowExpression)));
                        SetText(gc[5],Area(building,"building.footprintArea"));for(int col=0;col<3;col++)Merge(gc[col],false);table.Add(ground);globalRow++;
                    }
                    ClearRow(total);var tc=total.Elements(W+"tc").ToList();SetText(tc[0],"合计");SetText(tc[1],"建筑面积");SetText(tc[2],Value(record,ParcelSurveyFieldKeys.BuildingAreaTotal));Merge(tc[0],true);
                    ClearRow(landTotal);var lc=landTotal.Elements(W+"tc").ToList();SetText(lc[1],"占地面积");SetText(lc[2],Value(record,ParcelSurveyFieldKeys.BuildingFootprintTotal));Merge(lc[0],false);
                    table.Add(total,landTotal);
                    // Only the template's table and final section remain; example notes must never leak into exports.
                    doc.Root.Element(W+"body").Elements().Where(e=>e!=table&&e.Name!=W+"sectPr").Remove();
                    table.AddAfterSelf(new XElement(W+"p"));
                    entry.Delete();entry=zip.CreateEntry("word/document.xml");using(var stream=entry.Open())doc.Save(stream);
                }
                Verify(temp,record);
                if(File.Exists(outputPath))File.Replace(temp,outputPath,null);else File.Move(temp,outputPath);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        private static void Verify(string path,ParcelSurveyRecord record)
        {
            using(var zip=ZipFile.OpenRead(path))using(var stream=zip.GetEntry("word/document.xml").Open())
            {
                var table=XDocument.Load(stream).Descendants(W+"tbl").Single();int expected=4+record.Buildings.Sum(b=>Math.Max(1,b.FloorAreas.Count)+2);
                if(table.Elements(W+"tr").Count()!=expected)throw new InvalidDataException("房产文档明细行回读不匹配。");
                var cells=table.Elements(W+"tr").Last().Elements(W+"tc").ToList();
                if(string.Concat(cells.Last().Descendants(W+"t").Select(t=>t.Value))!=Value(record,ParcelSurveyFieldKeys.BuildingFootprintTotal))throw new InvalidDataException("房产文档占地合计回读不匹配。");
            }
        }
        private static string Name(ParcelSurveyRecord r)=>!string.IsNullOrWhiteSpace(r?.ParcelName)?r.ParcelName.Trim():!string.IsNullOrWhiteSpace(r?.RegionName)?r.RegionName.Trim():"未命名宗地";
        private static string Owner(ParcelSurveyRecord r)=>r.Field("house.followParcelOwner").BooleanValue?r.Field(ParcelSurveyFieldKeys.OwnerName).TextValue:r.Field("house.ownerName").TextValue;
        private static string Text(ParcelBuildingRecord b,string key)=>b.Fields[key].TextValue??"";
        private static string Area(ParcelBuildingRecord b,string key)=>b.Fields[key].NumericValue.HasValue?N(b.Fields[key].NumericValue.Value):"";
        private static string Value(ParcelSurveyRecord r,string key)=>r.Field(key).NumericValue.HasValue?N(r.Field(key).NumericValue.Value):"";
        private static string N(decimal n)=>n.ToString("0.00",CultureInfo.InvariantCulture);
        private static void Header(XElement row){var pr=row.Element(W+"trPr");if(pr==null){pr=new XElement(W+"trPr");row.AddFirst(pr);}var header=pr.Element(W+"tblHeader");if(header==null){header=new XElement(W+"tblHeader");pr.Add(header);}header.SetAttributeValue(W+"val","true");}
        private static void Merge(XElement cell,bool start){var p=cell.Element(W+"tcPr");if(p==null){p=new XElement(W+"tcPr");cell.AddFirst(p);}p.Elements(W+"vMerge").Remove();p.Add(new XElement(W+"vMerge",new XAttribute(W+"val",start?"restart":"continue")));}
        private static void ClearRow(XElement row)
        {
            row.Descendants(W+"trHeight").Remove();row.Descendants(W+"keepNext").Remove();row.Descendants(W+"vMerge").Remove();
            var pr=row.Element(W+"trPr");if(pr==null){pr=new XElement(W+"trPr");row.AddFirst(pr);}pr.Elements(W+"cantSplit").Remove();pr.Add(new XElement(W+"cantSplit"));
            foreach(var cell in row.Elements(W+"tc"))SetText(cell,"");
        }
        private static void SetText(XElement cell,string text)
        {
            var paragraph=cell.Elements(W+"p").FirstOrDefault();var pp=paragraph?.Element(W+"pPr");var rp=cell.Descendants(W+"rPr").FirstOrDefault();
            cell.Elements().Where(e=>e.Name!=W+"tcPr").Remove();
            cell.Add(new XElement(W+"p",pp==null?null:new XElement(pp),new XElement(W+"r",rp==null?null:new XElement(rp),
                new XElement(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),text??""))));
        }
    }
}
