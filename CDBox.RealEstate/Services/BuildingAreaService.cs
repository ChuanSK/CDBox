using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CDBox.RealEstate.Geometry;
using CDBox.RealEstate.Models;

namespace CDBox.RealEstate.Services
{
    public static class BuildingAreaService
    {
        public static BuildingAreaRole ClassifyLayer(string layer)
        {
            string name = (layer ?? string.Empty).Trim();
            int xref = name.LastIndexOf('|');
            if (xref >= 0) name = name.Substring(xref + 1).Trim();
            return name == "中空" ? BuildingAreaRole.Hollow
                : name == "半封" ? BuildingAreaRole.HalfEnclosed : BuildingAreaRole.Full;
        }

        public static BuildingAreaComponent Component(string handle, string layer, BuildingAnnotationPlan plan)
        {
            var role = ClassifyLayer(layer);
            decimal factor = role == BuildingAreaRole.Hollow ? -1m : role == BuildingAreaRole.HalfEnclosed ? .5m : 1m;
            return new BuildingAreaComponent { SourceHandle = handle, LayerName = layer, Role = role,
                Factor = factor, Area = plan.CalculatedArea, Contribution = plan.CalculatedArea * factor,
                GeometryArea = plan.GeometryArea, Formula = plan.AreaFormula, Terms = plan.AreaTerms.ToList(), Boundary = plan.Boundary.ToList() };
        }

        public static BuildingAreaCalculation Calculate(string documentId, IEnumerable<BuildingAreaComponent> components)
        {
            var all = components.ToList();
            if (!all.Any(x => x.Role != BuildingAreaRole.Hollow))
                throw new InvalidOperationException("选择范围内缺少房屋或半封闭合图形。");
            var positive = all.Where(x=>x.Role!=BuildingAreaRole.Hollow).ToList();
            var excluded=all.Where(x=>x.Role==BuildingAreaRole.Hollow && !positive.Any(p=>
                BuildingBoundaryMath.ContainsContour(p.Boundary,x.Boundary))).ToList();
            all=all.Except(excluded).ToList();
            if (all.Any(x => x.Area <= 0))
                throw new InvalidOperationException("存在按注记精度计算后面积为零的图形，请检查边界与图纸单位。");
            var result = new BuildingAreaCalculation { DocumentId = documentId, Components = all,
                ExcludedHollowHandles = excluded.Select(x=>x.SourceHandle).ToList(),
                FullArea = all.Where(x => x.Role == BuildingAreaRole.Full).Sum(x => x.Area),
                HollowArea = all.Where(x => x.Role == BuildingAreaRole.Hollow).Sum(x => x.Area),
                HalfEnclosedArea = all.Where(x => x.Role == BuildingAreaRole.HalfEnclosed).Sum(x => x.Area) };
            result.Area = Math.Round(result.FullArea - result.HollowArea + result.HalfEnclosedArea / 2m,
                2, MidpointRounding.AwayFromZero);
            if (result.Area <= 0)
                throw new InvalidOperationException("扣减中空后的房屋面积无效，请检查框选范围与图层。");
            result.Formula = N(result.FullArea) + " − " + N(result.HollowArea) + " + "
                + N(result.HalfEnclosedArea) + " × 0.5 = " + N(result.Area);
            return result;
        }

        public static ParcelBuildingRecord CreateBuilding(BuildingAreaCalculation calculation, BuildingCaptureInput input)
        {
            if (calculation == null) throw new ArgumentNullException(nameof(calculation));
            input = input ?? new BuildingCaptureInput();
            var building = new ParcelBuildingRecord { AreaCalculation = calculation, HasFloorAreas = true };
            building.Normalize();
            SetText(building, "building.number", input.BuildingNumber);
            SetText(building, "building.householdNumber", input.HouseholdNumber);
            SetText(building, "building.floor", input.Floor);
            SetText(building, "building.structure", input.Structure);
            if (!string.IsNullOrWhiteSpace(input.TotalFloors))
            {
                int floors;
                if (!int.TryParse(input.TotalFloors, out floors) || floors <= 0)
                    throw new InvalidOperationException("总层数请填写正整数，或留空。");
                building.Fields["building.totalFloors"].NumericValue = floors;
                building.Fields["building.totalFloors"].Status = ParcelFieldStatus.Manual;
                building.Fields["building.totalFloors"].Confirmed = true;
            }
            SetArea(building.Fields["building.area"], calculation.Area);
            var row=new BuildingFloorArea {Name=(input.Floor??string.Empty).Trim(),Count=input.FloorCount,
                IsGroundFloor=input.IsGroundFloor,Calculation=calculation};
            ValidateFloor(row);building.FloorAreas.Add(row);
            SynchronizeBuilding(building);
            return building;
        }

        public static void Synchronize(ParcelSurveyRecord record)
        {
            if (record == null) return;
            record.Normalize();
            foreach (var building in record.Buildings) SynchronizeBuilding(building);
            var values = record.Buildings.Select(b => b.Fields["building.area"])
                .Where(v => v.Status != ParcelFieldStatus.NotApplicable).ToList();
            // Do not present a partial sum as a complete total while a manually entered house has no area.
            SetArea(record.Field(ParcelSurveyFieldKeys.BuildingAreaTotal),
                values.Any(v => !v.NumericValue.HasValue) ? (decimal?)null :
                    Math.Round(values.Sum(v => v.NumericValue ?? 0), 2, MidpointRounding.AwayFromZero));
            var footprints=record.Buildings.Select(b=>b.Fields["building.footprintArea"]).ToList();
            SetArea(record.Field(ParcelSurveyFieldKeys.BuildingFootprintTotal),
                footprints.Any(v=>!v.NumericValue.HasValue)?(decimal?)null:footprints.Sum(v=>v.NumericValue??0));
        }

        public static bool IsGroundName(string name)
        {
            name=(name??string.Empty).Trim();
            return name=="1"||Regex.IsMatch(name,@"^(第一层|一层|1层|一楼|1楼|首层|底层|1F)(?![至到\-])",RegexOptions.IgnoreCase);
        }
        public static bool IsGround(BuildingFloorArea row)=>row.IsGroundFloor??IsGroundName(row.Name);
        public static decimal RowArea(BuildingFloorArea row)=>Math.Round(
            (row.Calculation.FullArea-row.Calculation.HollowArea+row.Calculation.HalfEnclosedArea/2m)*row.Count,
            2,MidpointRounding.AwayFromZero);
        public static string Expression(BuildingAreaCalculation calculation)
        {
            return string.Join(" + ",calculation.Components.Select(c=>{
                string terms=string.Join(" + ",c.Terms.Select(t=>t.Formula)).Replace(" + −", " − ");
                if(string.IsNullOrWhiteSpace(terms))terms=N(c.Area);
                string signed="("+terms+")";
                return c.Factor==1?terms:c.Factor==-1?"−"+signed:signed+" × "+c.Factor.ToString("0.##",CultureInfo.InvariantCulture);
            })).Replace(" + −"," − ");
        }
        public static string RowExpression(BuildingFloorArea row)=>row.Count==1?Expression(row.Calculation):"["+Expression(row.Calculation)+"] × "+row.Count;
        public static void ValidateFloor(BuildingFloorArea row)
        {
            if(row==null||row.Calculation==null||row.Count<1||row.Count>1000)throw new InvalidOperationException("层次计算数据无效，计入层数应为 1 至 1000 的整数。");
            if(IsGround(row)&&row.Count!=1)throw new InvalidOperationException("底层计入占地时，计入层数必须为 1；其他楼层请另添加明细。");
        }
        public static void SynchronizeBuilding(ParcelBuildingRecord building)
        {
            building.Normalize();decimal? footprint=null;
            if(building.HasFloorAreas)
            {
                foreach(var row in building.FloorAreas)ValidateFloor(row);
                SetArea(building.Fields["building.area"],building.FloorAreas.Count==0?(decimal?)null:building.FloorAreas.Sum(RowArea));
                var ground=building.FloorAreas.Where(IsGround).ToList();
                if(ground.Count>0)footprint=ground.Sum(RowArea);
                building.Fields["building.floor"].TextValue=string.Join("、",building.FloorAreas.Select(r=>r.Name).Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct());
            }
            else if(IsGroundName(building.Fields["building.floor"].TextValue))footprint=building.Fields["building.area"].NumericValue;
            SetArea(building.Fields["building.footprintArea"],footprint);
        }

        private static void SetArea(ParcelSurveyFieldValue value, decimal? area)
        {
            value.Status = ParcelFieldStatus.Automatic; value.NumericValue = area;
            value.TextValue = string.Empty; value.Confirmed = area.HasValue;
        }
        private static void SetText(ParcelBuildingRecord building, string key, string text)
        {
            building.Fields[key].TextValue = (text ?? string.Empty).Trim();
            building.Fields[key].Status = ParcelFieldStatus.Manual;
            building.Fields[key].Confirmed = !string.IsNullOrWhiteSpace(text);
        }
        private static string N(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    // Template-neutral export contract. The future Word/Excel adapter receives all formula terms and factors.
    public interface IBuildingAreaTableExporter
    {
        void Export(BuildingAreaTableData data, string outputPath);
    }
    public sealed class BuildingAreaTableData
    {
        public string ParcelRecordId { get; set; }
        public string ParcelName { get; set; }
        public string BuildingId { get; set; }
        public string BuildingNumber { get; set; }
        public string HouseholdNumber { get; set; }
        public BuildingAreaCalculation Calculation { get; set; }
        public IList<BuildingFloorArea> FloorAreas { get; set; }
    }
    public static class BuildingAreaTableDataSource
    {
        public static IList<BuildingAreaTableData> Create(ParcelSurveyRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            record.Normalize();
            return record.Buildings.Where(b => b.HasFloorAreas || b.AreaCalculation != null).Select(b => new BuildingAreaTableData {
                ParcelRecordId = record.Id, ParcelName = record.ParcelName, BuildingId = b.Id,
                BuildingNumber = b.Fields["building.number"].TextValue,
                HouseholdNumber = b.Fields["building.householdNumber"].TextValue, Calculation = b.AreaCalculation,
                FloorAreas = b.FloorAreas
            }).ToList();
        }
    }
}
