// Canonical implementation owned by CDBox.Common.
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    public sealed class FramePaperSize
    {
        public string Name { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public FramePaperSize Clone()
        {
            return new FramePaperSize { Name = Name, Width = Width, Height = Height };
        }
    }

    public static class FramePaperSizes
    {
        private static readonly FramePaperSize[] Presets =
        {
            new FramePaperSize { Name = "A0", Width = 1189.0, Height = 841.0 },
            new FramePaperSize { Name = "A1", Width = 841.0, Height = 594.0 },
            new FramePaperSize { Name = "A2", Width = 594.0, Height = 420.0 },
            new FramePaperSize { Name = "A3", Width = 420.0, Height = 297.0 },
            new FramePaperSize { Name = "A4", Width = 297.0, Height = 210.0 }
        };

        public static IList<FramePaperSize> GetAll()
        {
            return Presets.Select(x => x.Clone()).ToList();
        }

        public static bool TryGet(string name, out FramePaperSize size)
        {
            size = Presets.FirstOrDefault(x =>
                string.Equals(x.Name, (name ?? string.Empty).Trim(),
                    StringComparison.OrdinalIgnoreCase));
            if (size == null) return false;
            size = size.Clone();
            return true;
        }

        public static string NormalizeName(string name)
        {
            FramePaperSize size;
            return TryGet(name, out size) ? size.Name : "自定义";
        }
    }

    public sealed class FrameTemplateCatalogItem
    {
        public FrameTemplateCatalogItem()
        {
            Id = Guid.NewGuid().ToString("N");
            TemplateName = "自定义图框";
            PaperSize = "A3";
            BlockName = string.Empty;
            SourceDwgPath = string.Empty;
            ScaleX = 1.0;
            ScaleY = 1.0;
            ScaleZ = 1.0;
            PreviewSegments = new List<FrameTemplatePreviewSegment>();
            SavedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
        }

        public string Id { get; set; }
        public string TemplateName { get; set; }
        public string PaperSize { get; set; }
        public string BlockName { get; set; }
        public string SourceDwgPath { get; set; }
        public double ScaleX { get; set; }
        public double ScaleY { get; set; }
        public double ScaleZ { get; set; }
        public double ValidMinX { get; set; }
        public double ValidMinY { get; set; }
        public double ValidMaxX { get; set; }
        public double ValidMaxY { get; set; }
        public double FrameMinX { get; set; }
        public double FrameMinY { get; set; }
        public double FrameMaxX { get; set; }
        public double FrameMaxY { get; set; }
        public double MarginLeft { get; set; }
        public double MarginRight { get; set; }
        public double MarginBottom { get; set; }
        public double MarginTop { get; set; }
        public bool IsDefault { get; set; }
        public int PreviewEntityCount { get; set; }
        public List<FrameTemplatePreviewSegment> PreviewSegments { get; set; }
        public string SavedAt { get; set; }

        public double BaseValidWidth
        {
            get { return Math.Abs(ValidMaxX - ValidMinX) * Math.Abs(ScaleX); }
        }

        public double BaseValidHeight
        {
            get { return Math.Abs(ValidMaxY - ValidMinY) * Math.Abs(ScaleY); }
        }

        public double ValidWidth
        {
            get
            {
                return Math.Max(0, BaseValidWidth
                    - Math.Max(0, MarginLeft) - Math.Max(0, MarginRight));
            }
        }

        public double ValidHeight
        {
            get
            {
                return Math.Max(0, BaseValidHeight
                    - Math.Max(0, MarginBottom) - Math.Max(0, MarginTop));
            }
        }

        public double FrameWidth
        {
            get { return Math.Abs(FrameMaxX - FrameMinX) * Math.Abs(ScaleX); }
        }

        public double FrameHeight
        {
            get { return Math.Abs(FrameMaxY - FrameMinY) * Math.Abs(ScaleY); }
        }

        public double EffectiveValidMinX
        {
            get
            {
                return Math.Min(ValidMinX, ValidMaxX)
                    + Math.Max(0, MarginLeft)
                    / Math.Max(1e-9, Math.Abs(ScaleX));
            }
        }

        public double EffectiveValidMaxX
        {
            get
            {
                return Math.Max(ValidMinX, ValidMaxX)
                    - Math.Max(0, MarginRight)
                    / Math.Max(1e-9, Math.Abs(ScaleX));
            }
        }

        public double EffectiveValidMinY
        {
            get
            {
                return Math.Min(ValidMinY, ValidMaxY)
                    + Math.Max(0, MarginBottom)
                    / Math.Max(1e-9, Math.Abs(ScaleY));
            }
        }

        public double EffectiveValidMaxY
        {
            get
            {
                return Math.Max(ValidMinY, ValidMaxY)
                    - Math.Max(0, MarginTop)
                    / Math.Max(1e-9, Math.Abs(ScaleY));
            }
        }

        public FrameTemplateInfo ToTemplateInfo()
        {
            return new FrameTemplateInfo
            {
                TemplateName = TemplateName,
                BlockName = BlockName,
                ScaleX = ScaleX,
                ScaleY = ScaleY,
                ScaleZ = ScaleZ,
                ValidMin = new Point2d(EffectiveValidMinX, EffectiveValidMinY),
                ValidMax = new Point2d(EffectiveValidMaxX, EffectiveValidMaxY),
                FrameMin = new Point2d(FrameMinX, FrameMinY),
                FrameMax = new Point2d(FrameMaxX, FrameMaxY),
                SavedAt = ParseDate(SavedAt)
            };
        }

        public void ApplyMargins(double left, double top, double right, double bottom)
        {
            left = Math.Max(0, left);
            right = Math.Max(0, right);
            top = Math.Max(0, top);
            bottom = Math.Max(0, bottom);

            if (left + right >= BaseValidWidth - 1e-6
                || top + bottom >= BaseValidHeight - 1e-6)
                throw new InvalidOperationException(
                    "留白设置已超过用户选择的图框内裁图区域。");

            MarginLeft = left;
            MarginRight = right;
            MarginTop = top;
            MarginBottom = bottom;
        }

        public bool IsValid(out string message)
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                message = "模板编号无效。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(BlockName))
            {
                message = "模板块名为空。";
                return false;
            }
            if (ValidWidth <= 1e-6 || ValidHeight <= 1e-6)
            {
                message = "裁图区域尺寸无效。";
                return false;
            }
            if (FrameWidth <= 1e-6 || FrameHeight <= 1e-6)
            {
                message = "图框尺寸无效。";
                return false;
            }
            message = "OK";
            return true;
        }

        public static FrameTemplateCatalogItem FromInfo(FrameTemplateInfo info,
            string paperSize, string sourceDwgPath, string id = null)
        {
            if (info == null) throw new ArgumentNullException("info");
            return new FrameTemplateCatalogItem
            {
                Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id,
                TemplateName = string.IsNullOrWhiteSpace(info.TemplateName)
                    ? "自定义图框" : info.TemplateName.Trim(),
                PaperSize = FramePaperSizes.NormalizeName(paperSize),
                BlockName = info.BlockName ?? string.Empty,
                SourceDwgPath = sourceDwgPath ?? string.Empty,
                ScaleX = info.ScaleX,
                ScaleY = info.ScaleY,
                ScaleZ = info.ScaleZ,
                ValidMinX = info.ValidMin.X,
                ValidMinY = info.ValidMin.Y,
                ValidMaxX = info.ValidMax.X,
                ValidMaxY = info.ValidMax.Y,
                FrameMinX = info.FrameMin.X,
                FrameMinY = info.FrameMin.Y,
                FrameMaxX = info.FrameMax.X,
                FrameMaxY = info.FrameMax.Y,
                SavedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture)
            };
        }

        private static DateTime ParseDate(string value)
        {
            DateTime parsed;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out parsed) ? parsed : DateTime.Now;
        }
    }

    public sealed class FrameTemplateCatalog
    {
        public FrameTemplateCatalog()
        {
            Version = 3;
            Templates = new List<FrameTemplateCatalogItem>();
        }

        public int Version { get; set; }
        public List<FrameTemplateCatalogItem> Templates { get; set; }
    }

    public static class FrameTemplateCatalogStore
    {
        private static readonly object Gate = new object();
        private static readonly JavaScriptSerializer Serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static string DataDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CDBox", "Studio", "FrameLayout");
            }
        }

        public static string CatalogPath
        {
            get { return Path.Combine(DataDirectory, "FrameTemplates.json"); }
        }

        public static string TemplateDirectory
        {
            get { return Path.Combine(DataDirectory, "Templates"); }
        }

        public static string GetTemplateDwgPath(string id)
        {
            string safeId = new string((id ?? string.Empty)
                .Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrWhiteSpace(safeId)) safeId = Guid.NewGuid().ToString("N");
            return Path.Combine(TemplateDirectory, "Frame_" + safeId + ".dwg");
        }

        public static string ToStoredTemplatePath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath)) return string.Empty;
            try
            {
                string full = Path.GetFullPath(absolutePath);
                string root = Path.GetFullPath(TemplateDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return Path.Combine("Templates", Path.GetFileName(full));
            }
            catch { }
            return absolutePath;
        }

        public static string ResolveTemplatePath(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return string.Empty;
            try
            {
                return Path.IsPathRooted(storedPath)
                    ? Path.GetFullPath(storedPath)
                    : Path.GetFullPath(Path.Combine(DataDirectory, storedPath));
            }
            catch
            {
                return storedPath;
            }
        }

        public static FrameTemplateCatalog Load()
        {
            lock (Gate)
            {
                EnsureDirectories();
                FrameTemplateCatalog catalog = null;
                try
                {
                    if (File.Exists(CatalogPath))
                        catalog = Serializer.Deserialize<FrameTemplateCatalog>(
                            File.ReadAllText(CatalogPath));
                }
                catch
                {
                    catalog = null;
                }

                if (catalog == null)
                    catalog = new FrameTemplateCatalog();
                if (catalog.Templates == null)
                    catalog.Templates = new List<FrameTemplateCatalogItem>();

                Normalize(catalog);
                TryMigrateLegacy(catalog);
                return catalog;
            }
        }

        public static void Save(FrameTemplateCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            lock (Gate)
            {
                EnsureDirectories();
                Normalize(catalog);
                string json = Serializer.Serialize(catalog);
                string temp = CatalogPath + ".tmp";
                File.WriteAllText(temp, json);
                if (File.Exists(CatalogPath))
                    File.Replace(temp, CatalogPath, CatalogPath + ".bak", true);
                else
                    File.Move(temp, CatalogPath);
            }
        }

        public static FrameTemplateCatalogItem Find(string id)
        {
            return Load().Templates.FirstOrDefault(x =>
                string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static FrameTemplateCatalogItem GetDefault(string paperSize)
        {
            List<FrameTemplateCatalogItem> matches = Load().Templates
                .Where(x => string.Equals(x.PaperSize, paperSize,
                    StringComparison.OrdinalIgnoreCase)).ToList();
            return matches.FirstOrDefault(x => x.IsDefault) ?? matches.FirstOrDefault();
        }

        public static void Upsert(FrameTemplateCatalogItem item)
        {
            if (item == null) throw new ArgumentNullException("item");
            string message;
            if (!item.IsValid(out message)) throw new InvalidOperationException(message);
            FrameTemplateCatalog catalog = Load();
            int index = catalog.Templates.FindIndex(x =>
                string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) catalog.Templates[index] = item;
            else catalog.Templates.Add(item);
            if (!catalog.Templates.Any(x =>
                string.Equals(x.PaperSize, item.PaperSize, StringComparison.OrdinalIgnoreCase)
                && x.IsDefault))
                item.IsDefault = true;
            Save(catalog);
        }

        public static bool Delete(string id, bool deleteDwg)
        {
            FrameTemplateCatalog catalog = Load();
            FrameTemplateCatalogItem item = catalog.Templates.FirstOrDefault(x =>
                string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (item == null) return false;
            catalog.Templates.Remove(item);
            FrameTemplateCatalogItem next = catalog.Templates.FirstOrDefault(x =>
                string.Equals(x.PaperSize, item.PaperSize, StringComparison.OrdinalIgnoreCase));
            if (item.IsDefault && next != null) next.IsDefault = true;
            Save(catalog);
            if (deleteDwg && !string.IsNullOrWhiteSpace(item.SourceDwgPath))
            {
                try
                {
                    string sourcePath = ResolveTemplatePath(item.SourceDwgPath);
                    if (File.Exists(sourcePath)
                        && Path.GetFullPath(sourcePath).StartsWith(
                            Path.GetFullPath(TemplateDirectory)
                                .TrimEnd(Path.DirectorySeparatorChar)
                                + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase))
                        File.Delete(sourcePath);
                }
                catch { }
            }
            return true;
        }

        public static void SetDefault(string id)
        {
            FrameTemplateCatalog catalog = Load();
            FrameTemplateCatalogItem selected = catalog.Templates.FirstOrDefault(x =>
                string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (selected == null) return;
            foreach (FrameTemplateCatalogItem item in catalog.Templates)
            {
                if (string.Equals(item.PaperSize, selected.PaperSize,
                    StringComparison.OrdinalIgnoreCase))
                    item.IsDefault = string.Equals(item.Id, selected.Id,
                        StringComparison.OrdinalIgnoreCase);
            }
            Save(catalog);
        }

        private static void Normalize(FrameTemplateCatalog catalog)
        {
            bool migrateLegacyMargins = catalog.Version < 3;
            catalog.Version = Math.Max(3, catalog.Version);
            catalog.Templates = catalog.Templates
                .Where(x => x != null)
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Id)
                    ? Guid.NewGuid().ToString("N") : x.Id,
                    StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First()).ToList();
            foreach (FrameTemplateCatalogItem item in catalog.Templates)
            {
                if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(item.TemplateName)) item.TemplateName = "自定义图框";
                item.PaperSize = FramePaperSizes.NormalizeName(item.PaperSize);
                if (Math.Abs(item.ScaleX) <= 1e-9) item.ScaleX = 1.0;
                if (Math.Abs(item.ScaleY) <= 1e-9) item.ScaleY = 1.0;
                if (Math.Abs(item.ScaleZ) <= 1e-9) item.ScaleZ = 1.0;
                if (migrateLegacyMargins)
                {
                    // V2 的留白值是“图框外边缘到有效区”的派生值，而且
                    // ApplyMargins 会直接改写所选有效区。升级时将现有有效区
                    // 视为用户所选基础区，避免旧数据被二次内缩。
                    item.MarginLeft = 0;
                    item.MarginRight = 0;
                    item.MarginTop = 0;
                    item.MarginBottom = 0;
                }
                else
                {
                    item.MarginLeft = Math.Max(0, item.MarginLeft);
                    item.MarginRight = Math.Max(0, item.MarginRight);
                    item.MarginTop = Math.Max(0, item.MarginTop);
                    item.MarginBottom = Math.Max(0, item.MarginBottom);
                    if (item.MarginLeft + item.MarginRight
                            >= item.BaseValidWidth - 1e-6
                        || item.MarginTop + item.MarginBottom
                            >= item.BaseValidHeight - 1e-6)
                    {
                        item.MarginLeft = 0;
                        item.MarginRight = 0;
                        item.MarginTop = 0;
                        item.MarginBottom = 0;
                    }
                }
                if (item.PreviewSegments == null)
                    item.PreviewSegments = new List<FrameTemplatePreviewSegment>();
                item.SourceDwgPath = ToStoredTemplatePath(item.SourceDwgPath);
            }
            foreach (IGrouping<string, FrameTemplateCatalogItem> group in
                catalog.Templates.GroupBy(x => x.PaperSize,
                    StringComparer.OrdinalIgnoreCase))
            {
                FrameTemplateCatalogItem firstDefault = group.FirstOrDefault(x => x.IsDefault)
                    ?? group.FirstOrDefault();
                bool used = false;
                foreach (FrameTemplateCatalogItem item in group)
                {
                    item.IsDefault = !used && item == firstDefault;
                    if (item.IsDefault) used = true;
                }
            }
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(TemplateDirectory);
        }

        private static void TryMigrateLegacy(FrameTemplateCatalog catalog)
        {
            if (catalog.Templates.Count > 0) return;
            try
            {
                string path = FrameTemplateInfo.GetDefaultConfigPath();
                FrameTemplateInfo info = FrameTemplateInfo.Load(path);
                string message;
                if (info == null || !info.IsValid(out message)) return;
                FrameTemplateCatalogItem item =
                    FrameTemplateCatalogItem.FromInfo(info, "自定义", string.Empty);
                item.IsDefault = true;
                catalog.Templates.Add(item);
                Save(catalog);
            }
            catch { }
        }
    }

    public sealed class FrameTemplatePreviewSegment
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
    }
}
