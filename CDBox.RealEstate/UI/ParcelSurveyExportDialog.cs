using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using CDBox.RealEstate.Models;
using CDBox.RealEstate.Services;
using Microsoft.Win32;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace CDBox.RealEstate.UI
{
    internal static class ParcelSurveyExportDialog
    {
        public static string Export(ParcelSurveyRecord record)
        {
            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            string parcelCode = record.Field(
                ParcelSurveyFieldKeys.ParcelCode).TextValue;
            if (string.IsNullOrWhiteSpace(parcelCode))
                parcelCode = record.Field(
                    ParcelSurveyFieldKeys.ParcelSeaCode).TextValue;
            if (string.IsNullOrWhiteSpace(parcelCode)) parcelCode = "当前宗地";

            var dialog = new SaveFileDialog
            {
                Title = "导出权籍调查表",
                Filter = "Excel 97-2003 工作簿 (*.xls)|*.xls",
                DefaultExt = ".xls",
                AddExtension = true,
                FileName = Sanitize(parcelCode + "_权籍调查表_"
                    + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xls")
            };
            Document document = AcadApp.DocumentManager.MdiActiveDocument;
            try
            {
                string directory = document == null
                    ? string.Empty : Path.GetDirectoryName(document.Name);
                if (!string.IsNullOrWhiteSpace(directory)
                    && Directory.Exists(directory))
                    dialog.InitialDirectory = directory;
            }
            catch { }
            if (dialog.ShowDialog() != true) return string.Empty;
            ParcelSurveyExcelExporter.Export(dialog.FileName, record);
            return dialog.FileName;
        }

        private static string Sanitize(string fileName)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(invalid, '_');
            return fileName.Trim();
        }
    }
}
