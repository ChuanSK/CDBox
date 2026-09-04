// Canonical implementation owned by CDBox.Common.
using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Globalization;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    internal static class FrameScaleTextResolver
    {
        public static string Resolve(Database database, string fallback)
        {
            string annotationCandidate = string.Empty;
            if (database != null)
            {
                try
                {
                    AnnotationScale scale = database.Cannoscale;
                    if (scale != null)
                    {
                        string name = (scale.Name ?? string.Empty).Trim();
                        if (name.IndexOf(':') > 0)
                        {
                            annotationCandidate = name;
                            double namedDenominator;
                            if (TryReadDenominator(name, out namedDenominator)
                                && namedDenominator > 1.0 + 1e-6)
                                return name;
                        }

                        double paperUnits = Math.Abs(scale.PaperUnits);
                        double drawingUnits = Math.Abs(scale.DrawingUnits);
                        if (paperUnits > 1e-9 && drawingUnits > 1e-9)
                        {
                            double denominator = drawingUnits / paperUnits;
                            if (denominator > 1.0 + 1e-6)
                                return FormatRatio(denominator);
                            if (string.IsNullOrWhiteSpace(annotationCandidate))
                                annotationCandidate = FormatRatio(denominator);
                        }
                        if (scale.Scale > 1e-9)
                        {
                            double denominator = 1.0 / scale.Scale;
                            if (denominator > 1.0 + 1e-6)
                                return FormatRatio(denominator);
                            if (string.IsNullOrWhiteSpace(annotationCandidate))
                                annotationCandidate = FormatRatio(denominator);
                        }
                    }
                }
                catch { }

                try
                {
                    double dimscale = Math.Abs(database.Dimscale);
                    if (dimscale > 1.0 + 1e-6) return FormatRatio(dimscale);
                }
                catch { }

                // CASS 的 SCALE 命令将“当前比例尺 1:n”的 n 保存在
                // USERR1 中，而 CANNOSCALE 与 DIMSCALE 可能仍保持 1。
                try
                {
                    object raw = AcadApp.GetSystemVariable("USERR1");
                    double cassScale = Math.Abs(Convert.ToDouble(raw,
                        CultureInfo.InvariantCulture));
                    if (cassScale > 1.0 + 1e-6
                        && cassScale <= 10000000.0)
                        return FormatRatio(cassScale);
                }
                catch { }
            }
            if (!string.IsNullOrWhiteSpace(annotationCandidate))
                return annotationCandidate;
            return string.IsNullOrWhiteSpace(fallback)
                ? "1:1" : fallback.Trim();
        }

        private static bool TryReadDenominator(string value,
            out double denominator)
        {
            denominator = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            int colon = value.IndexOf(':');
            if (colon < 0 || colon >= value.Length - 1) return false;
            double numerator;
            double rawDenominator;
            if (!double.TryParse(value.Substring(0, colon).Trim(),
                NumberStyles.Float, CultureInfo.InvariantCulture,
                out numerator)
                || !double.TryParse(value.Substring(colon + 1).Trim(),
                    NumberStyles.Float, CultureInfo.InvariantCulture,
                    out rawDenominator)
                || Math.Abs(numerator) <= 1e-9) return false;
            denominator = Math.Abs(rawDenominator / numerator);
            return denominator > 1e-9;
        }

        private static string FormatRatio(double denominator)
        {
            if (double.IsNaN(denominator) || double.IsInfinity(denominator)
                || denominator <= 0) return "1:1";
            double rounded = Math.Round(denominator);
            string text = Math.Abs(denominator - rounded) <= 1e-6
                ? rounded.ToString("0", CultureInfo.InvariantCulture)
                : denominator.ToString("0.###", CultureInfo.InvariantCulture);
            return "1:" + text;
        }
    }
}
