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
            return ExportExcel(record);
        }

        public static string ExportExcel(ParcelSurveyRecord record)
        {
            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            var dialog = new SaveFileDialog
            {
                Title = "导出权籍调查表",
                Filter = "Excel 97-2003 工作簿 (*.xls)|*.xls",
                DefaultExt = ".xls",
                AddExtension = true,
                FileName = ParcelSurveyExcelExporter.DefaultExportFileName
            };
            SetInitialDirectory(dialog);
            if (dialog.ShowDialog() != true) return string.Empty;
            ParcelSurveyExcelExporter.Export(dialog.FileName, record);
            return dialog.FileName;
        }

        public static string ExportWord(ParcelSurveyRecord record)
        {
            record = record ?? new ParcelSurveyRecord();
            record.Normalize();
            var dialog = new SaveFileDialog
            {
                Title = "导出地籍调查表",
                Filter = "Word 文档 (*.docx)|*.docx",
                DefaultExt = ".docx",
                AddExtension = true,
                FileName = ParcelSurveyWordExporter.DefaultExportFileName
            };
            SetInitialDirectory(dialog);
            if (dialog.ShowDialog() != true) return string.Empty;
            ParcelSurveyWordExporter.Export(dialog.FileName, record);
            return dialog.FileName;
        }

        private static void SetInitialDirectory(SaveFileDialog dialog)
        {
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
        }
    }
}
