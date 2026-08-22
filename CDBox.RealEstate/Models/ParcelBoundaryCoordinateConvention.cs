using System;

namespace CDBox.RealEstate.Models
{
    public static class ParcelBoundaryCoordinateConvention
    {
        public const string Current = "cadastral-x-northing-y-easting-v1";

        public static decimal SurveyXFromCad(decimal cadX, decimal cadY)
        {
            return cadY;
        }

        public static decimal SurveyYFromCad(decimal cadX, decimal cadY)
        {
            return cadX;
        }

        public static decimal CadXFromSurvey(decimal surveyX,
            decimal surveyY)
        {
            return surveyY;
        }

        public static decimal CadYFromSurvey(decimal surveyX,
            decimal surveyY)
        {
            return surveyX;
        }

        public static void Normalize(ParcelBoundaryData boundary)
        {
            if (boundary == null) return;
            string convention = (boundary.CoordinateConvention
                ?? string.Empty).Trim();
            if (string.Equals(convention, Current,
                StringComparison.OrdinalIgnoreCase)) return;

            // 旧版本从 CAD 识别的宗地直接把 CAD 横坐标存入业务 X、
            // CAD 纵坐标存入业务 Y。地籍成果采用 X=北坐标、Y=东坐标，
            // 因此仅对有来源图元且尚未标记坐标约定的旧记录迁移一次。
            if (convention.Length == 0
                && !string.IsNullOrWhiteSpace(boundary.SourceObjectHandle))
            {
                foreach (ParcelBoundaryPointRecord point in boundary.Points)
                {
                    if (point == null) continue;
                    decimal? oldX = point.X;
                    point.X = point.Y;
                    point.Y = oldX;
                }
            }
            boundary.CoordinateConvention = Current;
        }
    }
}
