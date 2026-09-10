extern alias WastewaterBusiness;
using Rect2d = WastewaterBusiness::TCPipeAutoDraw.Modules.SectionDrawing.Rect2d;
using SectionLayerLayout = WastewaterBusiness::TCPipeAutoDraw.Modules.SectionDrawing.SectionLayerLayout;
using SectionLayout = WastewaterBusiness::TCPipeAutoDraw.Modules.SectionDrawing.SectionLayout;
using SectionLayoutCalculator = WastewaterBusiness::TCPipeAutoDraw.Modules.SectionDrawing.SectionLayoutCalculator;
using SectionPipeLayout = WastewaterBusiness::TCPipeAutoDraw.Modules.SectionDrawing.SectionPipeLayout;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Autodesk.AutoCAD.Geometry;

namespace TCPipeAutoDraw.Modules.SectionDrawing
{
    internal sealed class SectionPreviewControl : Control
    {
        private SectionDrawingOptions _options;

        public SectionDrawingOptions Options
        {
            get { return _options; }
            set
            {
                _options = value == null ? null : value.Clone();
                Invalidate();
            }
        }

        public SectionPreviewControl()
        {
            DoubleBuffered = true;
            BackColor = Color.Black;
            ForeColor = Color.White;
            MinimumSize = new Size(260, 260);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            if (_options == null)
            {
                DrawCenteredMessage(g, "暂无预览");
                return;
            }

            SectionDrawingOptions options = _options.Clone();
            try
            {
                SectionLayoutCalculator.Normalize(options);
                SectionLayout layout = SectionLayoutCalculator.Calculate(options);
                if (layout.TotalHeight <= 0 || options.Width <= 0)
                {
                    DrawCenteredMessage(g, "参数无效");
                    return;
                }

                float padding = 18f;
                double worldW = Math.Max(0.001, layout.ExtentMaxX - layout.ExtentMinX);
                double worldH = Math.Max(0.001, layout.ExtentMaxY - layout.ExtentMinY);
                float sx = (ClientSize.Width - padding * 2f) / (float)worldW;
                float sy = (ClientSize.Height - padding * 2f) / (float)worldH;
                float scale = Math.Max(1f, Math.Min(sx, sy));

                Func<double, float> mapX = x => padding + (float)((x - layout.ExtentMinX) * scale);
                Func<double, float> mapY = y => ClientSize.Height - padding - (float)((y - layout.ExtentMinY) * scale);

                using (var pen = new Pen(ForeColor, 1f))
                using (var dimPen = new Pen(Color.LightGray, 1f))
                using (var font = new System.Drawing.Font(Font.FontFamily, 8.5f, System.Drawing.FontStyle.Regular))
                using (var dimFont = new System.Drawing.Font(Font.FontFamily, 8.5f, System.Drawing.FontStyle.Regular))
                using (var titleFont = new System.Drawing.Font(Font.FontFamily, 10f, System.Drawing.FontStyle.Bold))
                {
                    foreach (SectionLayerLayout item in layout.Layers)
                    {
                        RectangleF body = ToRectangleF(item.BodyRect, mapX, mapY);
                        RectangleF label = ToRectangleF(item.LabelRect, mapX, mapY);
                        DrawPreviewHatch(g, body, item.Layer.HatchPatternName);
                        g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
                        g.DrawRectangle(pen, label.X, label.Y, label.Width, label.Height);
                        DrawStringCenter(g, item.Layer.LeftLabel, font, label, ForeColor);
                    }

                    foreach (SectionPipeLayout pipeLayout in layout.Pipes)
                    {
                        float cx = mapX(pipeLayout.Center.X);
                        float cy = mapY(pipeLayout.Center.Y);
                        float r = (float)(pipeLayout.Radius * scale);
                        using (var brush = new SolidBrush(BackColor))
                        {
                            g.FillEllipse(brush, cx - r, cy - r, r * 2f, r * 2f);
                        }
                        g.DrawEllipse(pen, cx - r, cy - r, r * 2f, r * 2f);
                        string pipeText = pipeLayout.Pipe == null ? string.Empty : pipeLayout.Pipe.PipeText;
                        DrawPipeTextCenter(g, pipeText, new RectangleF(cx - r, cy - r, r * 2f, r * 2f), ForeColor, Font.FontFamily);
                    }

                    if (options.DrawTopDimension)
                    {
                        DrawDimensionPreview(g, dimPen, dimFont, mapX, mapY, 0, layout.TotalHeight, options.Width, layout.TotalHeight, options.Width / 2.0, layout.TotalHeight + options.TopDimensionOffset, options.Width.ToString("0.##"));
                    }

                    if (options.DrawBottomDimension)
                    {
                        DrawDimensionPreview(g, dimPen, dimFont, mapX, mapY, 0, 0, options.Width, 0, options.Width / 2.0, -options.BottomDimensionOffset, options.Width.ToString("0.##"));
                    }

                    if (options.DrawRightDimensions)
                    {
                        foreach (SectionLayerLayout item in layout.Layers)
                        {
                            DrawDimensionPreview(g, dimPen, dimFont, mapX, mapY, options.Width, item.Bottom, options.Width, item.Top, options.Width + options.RightDimensionOffset, (item.Bottom + item.Top) / 2.0, item.Height.ToString("0.##"));
                        }
                    }

                    if (options.DrawTotalHeightDimension && layout.TotalHeight > 0)
                    {
                        double totalOffset = SectionLayoutCalculator.GetTotalHeightDimensionOffset(options);
                        DrawDimensionPreview(g, dimPen, dimFont, mapX, mapY, options.Width, 0, options.Width, layout.TotalHeight, options.Width + totalOffset, layout.TotalHeight / 2.0, layout.TotalHeight.ToString("0.##"));
                    }

                    if (options.DrawTitle && !string.IsNullOrWhiteSpace(options.SectionTitle))
                    {
                        double baseOffset = options.DrawBottomDimension ? options.BottomDimensionOffset : 0.0;
                        float tx = mapX(options.Width / 2.0);
                        float ty = mapY(-baseOffset - options.TitleOffset);
                        DrawTitleLines(g, options.SectionTitle, titleFont, tx, ty, ForeColor);
                    }
                }
            }
            catch (Exception ex)
            {
                DrawCenteredMessage(g, "预览失败：" + ex.Message);
            }
        }

        private static RectangleF ToRectangleF(Rect2d rect, Func<double, float> mapX, Func<double, float> mapY)
        {
            float x1 = mapX(rect.Left);
            float x2 = mapX(rect.Right);
            float yTop = mapY(rect.Top);
            float yBottom = mapY(rect.Bottom);
            return new RectangleF(Math.Min(x1, x2), Math.Min(yTop, yBottom), Math.Abs(x2 - x1), Math.Abs(yBottom - yTop));
        }

        private void DrawCenteredMessage(Graphics g, string text)
        {
            using (var font = new System.Drawing.Font(Font.FontFamily, 10f, System.Drawing.FontStyle.Regular))
            {
                DrawStringCenter(g, text, font, ClientRectangle, ForeColor);
            }
        }

        private static void DrawTitleLines(Graphics g, string text, Font font, float centerX, float topY, Color color)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.None);
            float lineGap = Math.Max(font.Height * 1.15f, 12f);
            int visibleIndex = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                float y = topY + visibleIndex * lineGap;
                DrawStringCenter(g, lines[i].Trim(), font, new RectangleF(centerX - 120, y - font.Height / 2f, 240, font.Height + 4), color);
                visibleIndex++;
            }
        }

        private static void DrawPipeTextCenter(Graphics g, string text, RectangleF rect, Color color, FontFamily fontFamily)
        {
            if (g == null || string.IsNullOrWhiteSpace(text) || rect.Width <= 2f || rect.Height <= 2f) return;

            string oneLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
            float maxWidth = rect.Width * 0.78f;
            float maxHeight = rect.Height * 0.38f;
            float fontSize = Math.Max(4f, maxHeight);

            for (int i = 0; i < 18; i++)
            {
                using (var testFont = new System.Drawing.Font(fontFamily, fontSize, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    SizeF size = g.MeasureString(oneLine, testFont, int.MaxValue, StringFormat.GenericTypographic);
                    if (size.Width <= maxWidth && size.Height <= maxHeight) break;
                }
                fontSize *= 0.9f;
                if (fontSize <= 3f) break;
            }

            using (var pipeFont = new System.Drawing.Font(fontFamily, fontSize, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel))
            {
                DrawStringCenter(g, oneLine, pipeFont, rect, color);
            }
        }

        private static void DrawStringCenter(Graphics g, string text, Font font, RectangleF rect, Color color)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            using (var brush = new SolidBrush(color))
            using (var sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(text, font, brush, rect, sf);
            }
        }

        private static void DrawDimensionPreview(Graphics g, Pen pen, Font font, Func<double, float> mapX, Func<double, float> mapY,
            double x1, double y1, double x2, double y2, double dx, double dy, string text)
        {
            float p1x = mapX(x1);
            float p1y = mapY(y1);
            float p2x = mapX(x2);
            float p2y = mapY(y2);
            float dpx = mapX(dx);
            float dpy = mapY(dy);
            bool horizontal = Math.Abs(y1 - y2) < Math.Abs(x1 - x2);

            if (horizontal)
            {
                g.DrawLine(pen, p1x, p1y, p1x, dpy);
                g.DrawLine(pen, p2x, p2y, p2x, dpy);
                g.DrawLine(pen, p1x, dpy, p2x, dpy);
            }
            else
            {
                g.DrawLine(pen, p1x, p1y, dpx, p1y);
                g.DrawLine(pen, p2x, p2y, dpx, p2y);
                g.DrawLine(pen, dpx, p1y, dpx, p2y);
            }
            DrawStringCenter(g, text, font, new RectangleF(dpx - 34, dpy - 12, 68, 24), Color.LightGray);
        }

        private static void DrawPreviewHatch(Graphics g, RectangleF rect, string patternName)
        {
            if (rect.Width <= 1 || rect.Height <= 1 || string.IsNullOrWhiteSpace(patternName)) return;
            string p = patternName.Trim().ToUpperInvariant();
            if (p == "无" || p == "NONE" || p == "NO" || p == "OFF") return;

            using (var brush = new SolidBrush(Color.FromArgb(24, Color.White)))
            {
                g.FillRectangle(brush, rect);
            }

            using (var pen = new Pen(Color.FromArgb(110, Color.White), 1f))
            {
                if (p.Contains("DOT") || p.Contains("GRAVEL") || p.Contains("CONC"))
                {
                    for (float y = rect.Top + 4; y < rect.Bottom; y += 8)
                    {
                        for (float x = rect.Left + 4; x < rect.Right; x += 8)
                        {
                            g.DrawEllipse(pen, x, y, 1.5f, 1.5f);
                        }
                    }
                }
                else
                {
                    for (float x = rect.Left - rect.Height; x < rect.Right + rect.Height; x += 8)
                    {
                        g.DrawLine(pen, x, rect.Bottom, x + rect.Height, rect.Top);
                    }
                }
            }
        }
    }
}
