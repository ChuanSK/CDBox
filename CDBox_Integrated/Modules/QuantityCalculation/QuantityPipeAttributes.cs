using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TCPipeAutoDraw.Modules.QuantityCalculation
{
    /// <summary>
    /// 工程量属性编辑器使用的统一属性模型。
    /// 为了兼容第一版命名，类名仍保留 QuantityPipeAttributes；实际支持：主管、支管、节点/检查井。
    /// </summary>
    public sealed class QuantityPipeAttributes
    {
        public const string SchemaVersion = "3";
        public const string KindMainPipe = "主管";
        public const string KindBranchPipe = "支管";
        public const string KindNodeWell = "节点/检查井";

        [Category("00 通用")]
        [DisplayName("参与工程量统计")]
        [Description("关闭后对象仍保留属性，但后续工程量统计时默认跳过。")]
        public bool Enabled { get; set; }

        [Category("00 通用")]
        [DisplayName("对象类型")]
        [Description("由图层管理中的父属性优先识别：井/节点、主管、支管；也可手动修改。")]
        public string ObjectKind { get; set; }

        [Category("00 通用")]
        [DisplayName("特殊对象")]
        [Description("启用后不再自动识别和覆盖属性，平均深度允许手动设置。")]
        public bool IsSpecialObject { get; set; }

        [Category("00 通用")]
        [DisplayName("图层父属性")]
        [ReadOnly(true)]
        public string LayerParentGroup { get; set; }

        [Category("00 通用")]
        [DisplayName("图层分类")]
        [ReadOnly(true)]
        public string LayerParentClass { get; set; }

        [Category("00 通用")]
        [DisplayName("图层标签")]
        [ReadOnly(true)]
        public string LayerTags { get; set; }

        [Category("01 管线通用")]
        [DisplayName("管材")]
        public string Material { get; set; }

        [Category("01 管线通用")]
        [DisplayName("规格/管径")]
        [Description("例如 DN200、DN300、75PVC、110PVC。")]
        public string Diameter { get; set; }

        [Category("01 管线通用")]
        [DisplayName("使用手动统计长度")]
        public bool UseManualLength { get; set; }

        [Category("01 管线通用")]
        [DisplayName("手动统计长度 m")]
        public double ManualLength { get; set; }

        [Category("01 管线通用")]
        [DisplayName("是否标注长宽高")]
        [Description("用于管长标注：开启后自动读取本对象属性，在横线下方生成“长、宽、高、深”等补充注记。主管默认开启，支管默认关闭。")]
        public bool DrawLengthWidthHeightAnnotation { get; set; }

        [Category("02 主管属性")]
        [DisplayName("起点井")]
        [Description("可根据相接节点自动识别，非强绑定，可手动修改。")]
        public string StartNode { get; set; }

        [Category("02 主管属性")]
        [DisplayName("终点井")]
        [Description("可根据相接节点自动识别，非强绑定，可手动修改。")]
        public string EndNode { get; set; }

        [Category("02 主管属性")]
        [DisplayName("起点深度 m")]
        public double StartDepth { get; set; }

        [Category("02 主管属性")]
        [DisplayName("终点深度 m")]
        public double EndDepth { get; set; }

        [Category("02 主管属性")]
        [DisplayName("平均深度 m")]
        [Description("由当前起点深度与终点深度自动计算，不重复叠加管线垫层。")]
        public double AverageDepth { get; set; }

        [Category("02 主管属性")]
        [DisplayName("开挖宽度 m")]
        public double TrenchWidth { get; set; }

        [Category("02 主管属性")]
        [DisplayName("原路面结构层 m")]
        public double RoadThickness { get; set; }

        [Category("02 主管属性")]
        [DisplayName("开挖方式")]
        public string ExcavationType { get; set; }

        [Category("02 主管属性")]
        [DisplayName("回填类型")]
        public string BackfillType { get; set; }

        [Category("02 主管属性")]
        [DisplayName("回填结构层")]
        [Description("按一行一层保存结构层，支持：层级名称 高度 锁定 管线层。非锁定层可由属性编辑器按总高自动计算。")]
        public string BackfillStructure { get; set; }

        [Category("03 支管属性")]
        [DisplayName("支管类型")]
        [Description("例如：砼恢复、原土回填、明管、并埋、雨水等。")]
        public string BranchType { get; set; }

        [Category("03 支管属性")]
        [DisplayName("支管加入计算")]
        [Description("一般砼恢复、原土回填类支管需要加入工程量统计。")]
        public bool BranchIncludeInCalculation { get; set; }

        [Category("03 支管属性")]
        [DisplayName("支管开挖深度 m")]
        public double BranchDepth { get; set; }

        [Category("04 节点/井属性")]
        [DisplayName("节点/井编号")]
        public string NodeNo { get; set; }

        [Category("04 节点/井属性")]
        [DisplayName("井规格/直径")]
        [Description("例如 φ500、φ700。")]
        public string WellSpec { get; set; }

        [Category("04 节点/井属性")]
        [DisplayName("井盖类型/材料")]
        public string WellCoverMaterial { get; set; }

        [Category("04 节点/井属性")]
        [DisplayName("井材料类型")]
        [Description("例如：成品塑料井、砖砌井。")]
        public string WellMaterialType { get; set; }

        [Category("04 节点/井属性")]
        [DisplayName("井类型")]
        [Description("检查井、沉泥井、跌水井等。")]
        public string WellType { get; set; }

        [Category("04 节点/井属性")]
        [DisplayName("500沉泥井扣减深度 m")]
        [Description("管道端点连接500沉泥井时，管道开挖深度按：井深 + 当前主管管线垫层 - 本值。")]
        public double SiltWellDeductDepth500 { get; set; }

        [Category("04 节点/井属性")]
        [DisplayName("700沉泥井扣减深度 m")]
        [Description("管道端点连接700沉泥井时，管道开挖深度按：井深 + 当前主管管线垫层 - 本值。")]
        public double SiltWellDeductDepth700 { get; set; }

        [Category("04 节点/井属性")]
        [DisplayName("自然标高 m")]
        public double GroundElevation { get; set; }

        [Category("04 节点/井属性")]
        [DisplayName("井深 m")]
        public double WellDepth { get; set; }

        [Category("04 节点/井属性")]
        [DisplayName("井筒长度 m")]
        public double ShaftLength { get; set; }

        [Category("04 节点/井属性")]
        [DisplayName("开挖长 m")]
        public double ExcavationLength { get; set; }

        [Category("04 节点/井属性")]
        [DisplayName("开挖宽 m")]
        public double ExcavationWidth { get; set; }

        [Category("04 节点/井属性")]
        [DisplayName("承压盖板")]
        public string CoverPlate { get; set; }

        [Category("05 结构层/扣减")]
        [DisplayName("中粗砂垫层厚度 m")]
        public double SandCushionThickness { get; set; }

        [Category("05 结构层/扣减")]
        [DisplayName("碎石垫层厚度 m")]
        public double GravelCushionThickness { get; set; }

        [Category("05 结构层/扣减")]
        [DisplayName("C25恢复厚度 m")]
        public double C25RestoreThickness { get; set; }

        [Category("05 结构层/扣减")]
        [DisplayName("管道外径/计算直径 m")]
        public double PipeOuterDiameter { get; set; }

        [Category("05 结构层/扣减")]
        [DisplayName("扣除管身体积")]
        public bool DeductPipeVolume { get; set; }

        [Category("99 备注")]
        [DisplayName("备注")]
        public string Remark { get; set; }

        public QuantityPipeAttributes()
        {
            ObjectKind = KindMainPipe;
            LayerParentGroup = string.Empty;
            LayerParentClass = string.Empty;
            LayerTags = string.Empty;
            StartNode = string.Empty;
            EndNode = string.Empty;
            Material = string.Empty;
            Diameter = string.Empty;
            ExcavationType = string.Empty;
            BackfillType = string.Empty;
            BackfillStructure = string.Empty;
            BranchType = string.Empty;
            NodeNo = string.Empty;
            WellSpec = string.Empty;
            WellCoverMaterial = string.Empty;
            WellMaterialType = string.Empty;
            WellType = string.Empty;
            CoverPlate = string.Empty;
            Remark = string.Empty;
        }

        public double EffectiveLength(double cadLength)
        {
            if (UseManualLength && ManualLength > 0) return ManualLength;
            return cadLength;
        }

        public QuantityPipeAttributes Clone()
        {
            return new QuantityPipeAttributes
            {
                Enabled = Enabled,
                ObjectKind = ObjectKind ?? string.Empty,
                IsSpecialObject = IsSpecialObject,
                LayerParentGroup = LayerParentGroup ?? string.Empty,
                LayerParentClass = LayerParentClass ?? string.Empty,
                LayerTags = LayerTags ?? string.Empty,
                Material = Material ?? string.Empty,
                Diameter = Diameter ?? string.Empty,
                UseManualLength = UseManualLength,
                ManualLength = ManualLength,
                DrawLengthWidthHeightAnnotation = DrawLengthWidthHeightAnnotation,
                StartNode = StartNode ?? string.Empty,
                EndNode = EndNode ?? string.Empty,
                StartDepth = StartDepth,
                EndDepth = EndDepth,
                AverageDepth = AverageDepth,
                TrenchWidth = TrenchWidth,
                RoadThickness = RoadThickness,
                ExcavationType = ExcavationType ?? string.Empty,
                BackfillType = BackfillType ?? string.Empty,
                BackfillStructure = BackfillStructure ?? string.Empty,
                BranchType = BranchType ?? string.Empty,
                BranchIncludeInCalculation = BranchIncludeInCalculation,
                BranchDepth = BranchDepth,
                NodeNo = NodeNo ?? string.Empty,
                WellSpec = WellSpec ?? string.Empty,
                WellCoverMaterial = WellCoverMaterial ?? string.Empty,
                WellMaterialType = WellMaterialType ?? string.Empty,
                WellType = WellType ?? string.Empty,
                SiltWellDeductDepth500 = SiltWellDeductDepth500,
                SiltWellDeductDepth700 = SiltWellDeductDepth700,
                GroundElevation = GroundElevation,
                WellDepth = WellDepth,
                ShaftLength = ShaftLength,
                ExcavationLength = ExcavationLength,
                ExcavationWidth = ExcavationWidth,
                CoverPlate = CoverPlate ?? string.Empty,
                SandCushionThickness = SandCushionThickness,
                GravelCushionThickness = GravelCushionThickness,
                C25RestoreThickness = C25RestoreThickness,
                PipeOuterDiameter = PipeOuterDiameter,
                DeductPipeVolume = DeductPipeVolume,
                Remark = Remark ?? string.Empty
            };
        }

        public QuantityPipeAttributes CloneForDefaultProfile()
        {
            QuantityPipeAttributes copy = Clone();
            copy.StartNode = string.Empty;
            copy.EndNode = string.Empty;
            copy.NodeNo = string.Empty;
            copy.UseManualLength = false;
            copy.ManualLength = 0.0;
            copy.Remark = string.Empty;
            copy.LayerParentGroup = string.Empty;
            copy.LayerParentClass = string.Empty;
            copy.LayerTags = string.Empty;
            return copy;
        }

        public static QuantityPipeAttributes Default
        {
            get { return DefaultMainPipe; }
        }

        public static QuantityPipeAttributes DefaultMainPipe
        {
            get
            {
                return new QuantityPipeAttributes
                {
                    Enabled = true,
                    ObjectKind = KindMainPipe,
                    Material = "钢带增强高密度聚乙烯螺旋波纹管(HDPE)",
                    Diameter = "DN200",
                    DrawLengthWidthHeightAnnotation = true,
                    TrenchWidth = 0.6,
                    RoadThickness = 0.2,
                    ExcavationType = "机械开挖",
                    BackfillType = "中粗砂回填",
                    BackfillStructure = "C25砼恢复 0.25 锁定" + Environment.NewLine + "碎石垫层 0.10 锁定" + Environment.NewLine + "中粗砂回填 0.80 管线层" + Environment.NewLine + "中粗砂垫层 0.15 锁定",
                    SandCushionThickness = 0.15,
                    GravelCushionThickness = 0.10,
                    C25RestoreThickness = 0.25,
                    PipeOuterDiameter = 0.20,
                    DeductPipeVolume = true
                };
            }
        }

        public static QuantityPipeAttributes DefaultBranchPipe
        {
            get
            {
                return new QuantityPipeAttributes
                {
                    Enabled = true,
                    ObjectKind = KindBranchPipe,
                    Material = "PVC管",
                    Diameter = "DN110",
                    DrawLengthWidthHeightAnnotation = false,
                    TrenchWidth = 0.4,
                    RoadThickness = 0.12,
                    ExcavationType = "人工开挖",
                    BackfillType = "中粗砂回填",
                    BackfillStructure = "C25砼恢复 0.25 锁定" + Environment.NewLine + "中粗砂回填 0.25 管线层" + Environment.NewLine + "中粗砂垫层 0.10 锁定",
                    BranchType = "砼恢复",
                    BranchIncludeInCalculation = true,
                    BranchDepth = 0.6,
                    SandCushionThickness = 0.10,
                    GravelCushionThickness = 0.10,
                    C25RestoreThickness = 0.25,
                    PipeOuterDiameter = 0.11,
                    DeductPipeVolume = true
                };
            }
        }

        public static QuantityPipeAttributes DefaultNodeWell
        {
            get
            {
                return new QuantityPipeAttributes
                {
                    Enabled = true,
                    ObjectKind = KindNodeWell,
                    DrawLengthWidthHeightAnnotation = false,
                    WellSpec = "φ500",
                    WellCoverMaterial = "铸铁井盖",
                    WellMaterialType = "成品塑料井",
                    WellType = "检查井",
                    SiltWellDeductDepth500 = 0.20,
                    SiltWellDeductDepth700 = 0.50,
                    RoadThickness = 0.2,
                    ExcavationType = "机械开挖",
                    BackfillType = "中粗砂回填",
                    BackfillStructure = "承压盖板C25基础 0.30 锁定" + Environment.NewLine + "承压盖板碎石垫层 0.10 锁定" + Environment.NewLine + "中粗砂回填 0.80" + Environment.NewLine + "中粗砂垫层 0.15 锁定 井下层",
                    ExcavationLength = 1.3,
                    ExcavationWidth = 1.3,
                    CoverPlate = "1200承压盖板",
                    SandCushionThickness = 0.15,
                    GravelCushionThickness = 0.10,
                    C25RestoreThickness = 0.30,
                    DeductPipeVolume = false
                };
            }
        }

        public static QuantityPipeAttributes DefaultForKind(string kind)
        {
            if (IsNodeKind(kind)) return DefaultNodeWell;
            if (IsBranchKind(kind)) return DefaultBranchPipe;
            return DefaultMainPipe;
        }

        public static bool IsNodeKind(string kind)
        {
            return !string.IsNullOrWhiteSpace(kind) && kind.IndexOf("井", StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        public static bool IsBranchKind(string kind)
        {
            return !string.IsNullOrWhiteSpace(kind) && kind.IndexOf("支", StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        public static bool IsMainPipeKind(string kind)
        {
            return !IsNodeKind(kind) && !IsBranchKind(kind);
        }

        public static void ApplyStructureLayerText(QuantityPipeAttributes attrs)
        {
            if (attrs == null || string.IsNullOrWhiteSpace(attrs.BackfillStructure)) return;

            List<QuantityStructureLayer> layers = QuantityStructureLayer.Parse(attrs.BackfillStructure);
            attrs.SandCushionThickness = QuantityStructureLayer.SumHeight(layers, QuantityStructureLayer.IsSandCushion);
            attrs.GravelCushionThickness = QuantityStructureLayer.SumHeight(layers, QuantityStructureLayer.IsGravel);
            attrs.C25RestoreThickness = QuantityStructureLayer.SumHeight(layers, QuantityStructureLayer.IsC25Restore);
        }

        public static string NormalizeStructureLayerText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string normalized = text.Replace("；", "\n").Replace(";", "\n").Replace("，", "\n").Replace(",", "\n");
            string[] lines = normalized.Replace("\r\n", "\n").Replace('\r', '\n').Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(Environment.NewLine, lines);
        }

        private static bool ContainsAnyForStructure(string text, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(text) || values == null) return false;
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            }
            return false;
        }

        public static double GetWellBottomCushionHeight(QuantityPipeAttributes attrs)
        {
            if (attrs == null) return 0.0;
            double height = 0.0;
            List<QuantityStructureLayer> layers = QuantityStructureLayer.Parse(attrs.BackfillStructure);
            foreach (QuantityStructureLayer layer in layers)
            {
                if (layer == null) continue;
                if (layer.IsPipeLayer) height += layer.Height;
            }

            if (layers.Count == 0)
            {
                height = attrs.SandCushionThickness;
            }
            return height < 0 ? 0.0 : height;
        }

        public static double GetSiltWellDeductDepth(QuantityPipeAttributes wellAttrs)
        {
            if (wellAttrs == null) return 0.0;
            if (!ContainsAnyForStructure(wellAttrs.WellType, "沉泥")) return 0.0;

            if (ContainsAnyForStructure(wellAttrs.WellSpec, "700", "φ700", "Φ700"))
            {
                return wellAttrs.SiltWellDeductDepth700 > 0 ? wellAttrs.SiltWellDeductDepth700 : 0.50;
            }
            return wellAttrs.SiltWellDeductDepth500 > 0 ? wellAttrs.SiltWellDeductDepth500 : 0.20;
        }

        public static double CalculatePipeExcavationDepthByWell(QuantityPipeAttributes wellAttrs, double fallback)
        {
            return CalculatePipeExcavationDepthByWell(wellAttrs, 0.0, fallback);
        }

        public static double CalculatePipeExcavationDepthByWell(QuantityPipeAttributes wellAttrs, double pipeCushionHeight, double fallback)
        {
            if (wellAttrs == null) return fallback;
            double cushion = pipeCushionHeight > 0 ? pipeCushionHeight : 0.0;
            double depth = wellAttrs.WellDepth + cushion - GetSiltWellDeductDepth(wellAttrs);
            if (depth <= 0) return fallback;
            return depth;
        }

        public static double ParseDouble(string text, double fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            double value;
            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value;
            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return value;
            return fallback;
        }

        public static bool ParseBool(string text, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            bool value;
            if (bool.TryParse(text.Trim(), out value)) return value;
            string normalized = text.Trim().ToLowerInvariant();
            if (normalized == "1" || normalized == "yes" || normalized == "y" || normalized == "true" || normalized == "是") return true;
            if (normalized == "0" || normalized == "no" || normalized == "n" || normalized == "false" || normalized == "否") return false;
            return fallback;
        }
    }

    public sealed class QuantityPipeSelectionInfo
    {
        public Autodesk.AutoCAD.DatabaseServices.ObjectId ObjectId { get; set; }
        public string HandleText { get; set; }
        public string LayerName { get; set; }
        public string ObjectTypeName { get; set; }
        public double CadLength { get; set; }
        public bool HasSavedAttributes { get; set; }
        public string InferredKind { get; set; }
        public QuantityPipeAttributes Attributes { get; set; }

        public QuantityPipeSelectionInfo()
        {
            HandleText = string.Empty;
            LayerName = string.Empty;
            ObjectTypeName = string.Empty;
            InferredKind = string.Empty;
            Attributes = QuantityPipeAttributes.Default;
        }
    }

    public sealed class QuantityPipeWriteResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int SuccessCount { get; set; }
        public int SkipCount { get; set; }
        public int FailCount { get; set; }

        public QuantityPipeWriteResult()
        {
            Message = string.Empty;
        }
    }

    public sealed class QuantityAttributeDefaults
    {
        public QuantityPipeAttributes MainPipe { get; set; }
        public QuantityPipeAttributes BranchPipe { get; set; }
        public QuantityPipeAttributes NodeWell { get; set; }

        public QuantityAttributeDefaults()
        {
            MainPipe = QuantityPipeAttributes.DefaultMainPipe;
            BranchPipe = QuantityPipeAttributes.DefaultBranchPipe;
            NodeWell = QuantityPipeAttributes.DefaultNodeWell;
        }

        public QuantityPipeAttributes GetForKind(string kind)
        {
            if (QuantityPipeAttributes.IsNodeKind(kind)) return (NodeWell ?? QuantityPipeAttributes.DefaultNodeWell).Clone();
            if (QuantityPipeAttributes.IsBranchKind(kind)) return (BranchPipe ?? QuantityPipeAttributes.DefaultBranchPipe).Clone();
            return (MainPipe ?? QuantityPipeAttributes.DefaultMainPipe).Clone();
        }

        public void SetForKind(string kind, QuantityPipeAttributes attrs)
        {
            if (attrs == null) return;
            QuantityPipeAttributes copy = attrs.CloneForDefaultProfile();
            if (QuantityPipeAttributes.IsNodeKind(kind))
            {
                copy.ObjectKind = QuantityPipeAttributes.KindNodeWell;
                NodeWell = copy;
            }
            else if (QuantityPipeAttributes.IsBranchKind(kind))
            {
                copy.ObjectKind = QuantityPipeAttributes.KindBranchPipe;
                BranchPipe = copy;
            }
            else
            {
                copy.ObjectKind = QuantityPipeAttributes.KindMainPipe;
                MainPipe = copy;
            }
        }
    }
}
