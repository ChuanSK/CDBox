using Autodesk.AutoCAD.Geometry;

using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    /// <summary>
    /// 图框模板配置。
    /// 第一版使用本地 ini 文本保存，避免引入 JSON 依赖，兼容 AutoCAD .NET Framework 项目。
    /// </summary>
    public class FrameTemplateInfo
    {
        public string TemplateName { get; set; }
        public string BlockName { get; set; }

        /// <summary>
        /// 图框块参照原始缩放。
        /// </summary>
        public double ScaleX { get; set; } = 1.0;
        public double ScaleY { get; set; } = 1.0;
        public double ScaleZ { get; set; } = 1.0;

        /// <summary>
        /// 有效绘制区域，保存为块定义局部坐标。
        /// </summary>
        public Point2d ValidMin { get; set; }
        public Point2d ValidMax { get; set; }

        /// <summary>
        /// 图框外包框，保存为块定义局部坐标。仅用于检查与提示。
        /// </summary>
        public Point2d FrameMin { get; set; }
        public Point2d FrameMax { get; set; }

        public double LastOverlap { get; set; } = 0.0;

        /// <summary>
        /// 指北针相对于有效绘制区域右上角的内缩距离，单位为图纸单位。
        /// 例如图框比例为 0.5 时，会乘以 ScaleX / ScaleY 后落到模型空间。
        /// </summary>
        public double NorthOffsetX { get; set; } = 12.0;
        public double NorthOffsetY { get; set; } = 12.0;

        public DateTime SavedAt { get; set; } = DateTime.Now;

        public double ValidWidthLocal
        {
            get { return Math.Abs(ValidMax.X - ValidMin.X); }
        }

        public double ValidHeightLocal
        {
            get { return Math.Abs(ValidMax.Y - ValidMin.Y); }
        }

        public double ValidWidthWorld
        {
            get { return ValidWidthLocal * Math.Abs(ScaleX); }
        }

        public double ValidHeightWorld
        {
            get { return ValidHeightLocal * Math.Abs(ScaleY); }
        }

        public Point2d ValidCenterLocal
        {
            get
            {
                return new Point2d(
                    (ValidMin.X + ValidMax.X) * 0.5,
                    (ValidMin.Y + ValidMax.Y) * 0.5
                );
            }
        }

        public Point2d ValidRightTopLocal
        {
            get
            {
                return new Point2d(
                    Math.Max(ValidMin.X, ValidMax.X),
                    Math.Max(ValidMin.Y, ValidMax.Y)
                );
            }
        }

        public bool IsValid(out string message)
        {
            if (string.IsNullOrWhiteSpace(BlockName))
            {
                message = "模板块名为空。";
                return false;
            }

            if (ValidWidthLocal <= 1e-6 || ValidHeightLocal <= 1e-6)
            {
                message = "有效绘制区域无效，请重新添加模板并框选有效绘制区域。";
                return false;
            }

            if (Math.Abs(ScaleX) <= 1e-9 || Math.Abs(ScaleY) <= 1e-9)
            {
                message = "模板缩放比例无效。";
                return false;
            }

            message = "OK";
            return true;
        }

        public static string GetDefaultConfigPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            try
            {
                string asmPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string asmDir = Path.GetDirectoryName(asmPath);
                if (!string.IsNullOrWhiteSpace(asmDir))
                    baseDir = asmDir;
            }
            catch
            {
            }

            string dir = Path.Combine(baseDir, "FrameLayoutConfig");

            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                return Path.Combine(dir, "FrameTemplate.ini");
            }
            catch
            {
                string fallbackDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CDBox",
                    "FrameLayoutConfig"
                );

                if (!Directory.Exists(fallbackDir))
                    Directory.CreateDirectory(fallbackDir);

                return Path.Combine(fallbackDir, "FrameTemplate.ini");
            }
        }

        public void Save(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            StringBuilder sb = new StringBuilder();

            Write(sb, "TemplateName", TemplateName);
            Write(sb, "BlockName", BlockName);
            Write(sb, "ScaleX", ScaleX);
            Write(sb, "ScaleY", ScaleY);
            Write(sb, "ScaleZ", ScaleZ);
            Write(sb, "ValidMinX", ValidMin.X);
            Write(sb, "ValidMinY", ValidMin.Y);
            Write(sb, "ValidMaxX", ValidMax.X);
            Write(sb, "ValidMaxY", ValidMax.Y);
            Write(sb, "FrameMinX", FrameMin.X);
            Write(sb, "FrameMinY", FrameMin.Y);
            Write(sb, "FrameMaxX", FrameMax.X);
            Write(sb, "FrameMaxY", FrameMax.Y);
            Write(sb, "LastOverlap", LastOverlap);
            Write(sb, "NorthOffsetX", NorthOffsetX);
            Write(sb, "NorthOffsetY", NorthOffsetY);
            Write(sb, "SavedAt", SavedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        public static FrameTemplateInfo Load(string path)
        {
            if (!File.Exists(path))
                return null;

            FrameTemplateInfo info = new FrameTemplateInfo();
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);

            foreach (string raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                int idx = raw.IndexOf('=');
                if (idx <= 0)
                    continue;

                string key = raw.Substring(0, idx).Trim();
                string value = raw.Substring(idx + 1).Trim();

                switch (key)
                {
                    case "TemplateName":
                        info.TemplateName = value;
                        break;
                    case "BlockName":
                        info.BlockName = value;
                        break;
                    case "ScaleX":
                        info.ScaleX = ToDouble(value, 1.0);
                        break;
                    case "ScaleY":
                        info.ScaleY = ToDouble(value, 1.0);
                        break;
                    case "ScaleZ":
                        info.ScaleZ = ToDouble(value, 1.0);
                        break;
                    case "ValidMinX":
                        info.ValidMin = new Point2d(ToDouble(value, 0), info.ValidMin.Y);
                        break;
                    case "ValidMinY":
                        info.ValidMin = new Point2d(info.ValidMin.X, ToDouble(value, 0));
                        break;
                    case "ValidMaxX":
                        info.ValidMax = new Point2d(ToDouble(value, 0), info.ValidMax.Y);
                        break;
                    case "ValidMaxY":
                        info.ValidMax = new Point2d(info.ValidMax.X, ToDouble(value, 0));
                        break;
                    case "FrameMinX":
                        info.FrameMin = new Point2d(ToDouble(value, 0), info.FrameMin.Y);
                        break;
                    case "FrameMinY":
                        info.FrameMin = new Point2d(info.FrameMin.X, ToDouble(value, 0));
                        break;
                    case "FrameMaxX":
                        info.FrameMax = new Point2d(ToDouble(value, 0), info.FrameMax.Y);
                        break;
                    case "FrameMaxY":
                        info.FrameMax = new Point2d(info.FrameMax.X, ToDouble(value, 0));
                        break;
                    case "LastOverlap":
                        info.LastOverlap = ToDouble(value, 0);
                        break;
                    case "NorthOffsetX":
                        info.NorthOffsetX = ToDouble(value, 12);
                        break;
                    case "NorthOffsetY":
                        info.NorthOffsetY = ToDouble(value, 12);
                        break;
                    case "SavedAt":
                        DateTime dt;
                        if (DateTime.TryParse(value, out dt))
                            info.SavedAt = dt;
                        break;
                }
            }

            return info;
        }

        private static void Write(StringBuilder sb, string key, string value)
        {
            sb.Append(key).Append('=').Append(value ?? string.Empty).AppendLine();
        }

        private static void Write(StringBuilder sb, string key, double value)
        {
            sb.Append(key)
                .Append('=')
                .Append(value.ToString("0.########", CultureInfo.InvariantCulture))
                .AppendLine();
        }

        private static double ToDouble(string s, double fallback = 0.0)
        {
            double v;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;

            if (double.TryParse(s, out v))
                return v;

            return fallback;
        }
    }
}
