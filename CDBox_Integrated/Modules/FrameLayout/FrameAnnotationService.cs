using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;

namespace TCPipeAutoDraw.Modules.FrameLayout
{
    internal sealed class FrameAnnotationService
    {
        public void Draw(Database db, Transaction tr,
            FrameTemplateCatalogItem template, Point3d insertPoint,
            double frameRotation, FrameLayoutSettings settings)
        {
            if (template == null || settings == null) return;
            settings.Normalize();

            if (settings.DrawNorthArrow)
            {
                Point3d position = ResolvePosition(template, insertPoint,
                    frameRotation, settings.NorthReferencePosition,
                    settings.NorthOffsetX, settings.NorthOffsetY);
                double rotation = string.Equals(settings.NorthDirectionMode,
                    "FrameUp", StringComparison.OrdinalIgnoreCase)
                    ? frameRotation : 0.0;
                new NorthArrowService().InsertNorthArrow(db, tr, position,
                    settings.NorthSize, rotation);
            }

            if (settings.DrawScaleLabel)
            {
                Point3d position = ResolvePosition(template, insertPoint,
                    frameRotation, settings.ScaleReferencePosition,
                    settings.ScaleOffsetX, settings.ScaleOffsetY);
                InsertScaleText(db, tr, position, frameRotation, settings);
            }
        }

        private static void InsertScaleText(Database db, Transaction tr,
            Point3d position, double rotation, FrameLayoutSettings settings)
        {
            CadDbHelper.EnsureGeneratedLayer(db, tr, "CDBOX_比例标注");
            DBText text = new DBText();
            text.TextString = FrameScaleTextResolver.Resolve(db,
                settings.ScaleText);
            text.Height = settings.ScaleTextHeight;
            text.Rotation = rotation;
            text.HorizontalMode = TextHorizontalMode.TextCenter;
            text.VerticalMode = TextVerticalMode.TextVerticalMid;
            text.Position = position;
            // AutoCAD/CASS 要求先设置对齐模式，再设置 AlignmentPoint；
            // 属性初始化器中的错误顺序会抛出 eNotApplicable。
            text.AlignmentPoint = position;
            text.ColorIndex = settings.ScaleColorIndex;
            text.Layer = "CDBOX_比例标注";
            TextStyleTable styles = (TextStyleTable)tr.GetObject(
                db.TextStyleTableId, OpenMode.ForRead);
            if (!string.IsNullOrWhiteSpace(settings.ScaleTextStyle)
                && styles.Has(settings.ScaleTextStyle))
                text.TextStyleId = styles[settings.ScaleTextStyle];
            else if (!db.Textstyle.IsNull)
                text.TextStyleId = db.Textstyle;
            CadDbHelper.AppendToModelSpace(db, tr, text);
            try { text.AdjustAlignment(db); } catch { }
        }

        internal static Point3d ResolvePosition(FrameTemplateCatalogItem template,
            Point3d insertPoint, double rotation, string reference,
            double horizontalOffset, double verticalOffset)
        {
            double x;
            double y;
            switch ((reference ?? string.Empty).Trim())
            {
                case "TopLeft":
                case "MiddleLeft":
                case "BottomLeft":
                    x = template.EffectiveValidMinX + horizontalOffset
                        / Math.Max(1e-9, Math.Abs(template.ScaleX));
                    break;
                case "TopCenter":
                case "Center":
                case "BottomCenter":
                    x = (template.EffectiveValidMinX
                            + template.EffectiveValidMaxX) * 0.5
                        + horizontalOffset / Math.Max(1e-9, Math.Abs(template.ScaleX));
                    break;
                default:
                    x = template.EffectiveValidMaxX - horizontalOffset
                        / Math.Max(1e-9, Math.Abs(template.ScaleX));
                    break;
            }

            switch ((reference ?? string.Empty).Trim())
            {
                case "BottomLeft":
                case "BottomCenter":
                case "BottomRight":
                    y = template.EffectiveValidMinY + verticalOffset
                        / Math.Max(1e-9, Math.Abs(template.ScaleY));
                    break;
                case "MiddleLeft":
                case "Center":
                case "MiddleRight":
                    y = (template.EffectiveValidMinY
                            + template.EffectiveValidMaxY) * 0.5
                        + verticalOffset / Math.Max(1e-9, Math.Abs(template.ScaleY));
                    break;
                default:
                    y = template.EffectiveValidMaxY - verticalOffset
                        / Math.Max(1e-9, Math.Abs(template.ScaleY));
                    break;
            }
            return GeometryHelper.LocalToWorld(new Point2d(x, y), insertPoint,
                rotation, template.ScaleX, template.ScaleY);
        }
    }
}
