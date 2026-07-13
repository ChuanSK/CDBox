using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TCPipeAutoDraw.Modules.QuantityCalculation;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioDefaultProfiles
    {
        public static string DefaultsFilePath
        {
            get { return QuantityAttributeDefaultStore.SettingsFilePath; }
        }

        public static QuantityAttributeDefaults LoadDefaults()
        {
            return QuantityAttributeDefaultStore.Load();
        }

        public static QuantityAttributeDefaults LoadBuiltInDefaults()
        {
            return new QuantityAttributeDefaults
            {
                MainPipe = QuantityPipeAttributes.DefaultMainPipe.CloneForDefaultProfile(),
                BranchPipe = QuantityPipeAttributes.DefaultBranchPipe.CloneForDefaultProfile(),
                NodeWell = QuantityPipeAttributes.DefaultNodeWell.CloneForDefaultProfile()
            };
        }

        public static string BuildDefaultsJson(QuantityAttributeDefaults defaults)
        {
            defaults = defaults ?? new QuantityAttributeDefaults();
            var json = new StringBuilder();
            json.Append("{\"profiles\":[");
            AppendProfile(json, "main", "主管默认表", QuantityPipeAttributes.KindMainPipe, defaults.MainPipe ?? QuantityPipeAttributes.DefaultMainPipe, true);
            json.Append(',');
            AppendProfile(json, "branch", "支管默认表", QuantityPipeAttributes.KindBranchPipe, defaults.BranchPipe ?? QuantityPipeAttributes.DefaultBranchPipe, true);
            json.Append(',');
            AppendProfile(json, "node", "节点/检查井默认表", QuantityPipeAttributes.KindNodeWell, defaults.NodeWell ?? QuantityPipeAttributes.DefaultNodeWell, false);
            json.Append("]}");
            return json.ToString();
        }

        public static int SavePayload(string payload)
        {
            QuantityAttributeDefaults defaults = LoadDefaults() ?? LoadBuiltInDefaults();
            if (defaults.MainPipe == null) defaults.MainPipe = QuantityPipeAttributes.DefaultMainPipe.CloneForDefaultProfile();
            if (defaults.BranchPipe == null) defaults.BranchPipe = QuantityPipeAttributes.DefaultBranchPipe.CloneForDefaultProfile();
            if (defaults.NodeWell == null) defaults.NodeWell = QuantityPipeAttributes.DefaultNodeWell.CloneForDefaultProfile();
            int count = 0;

            if (!string.IsNullOrWhiteSpace(payload))
            {
                string[] lines = payload.Replace("\r\n", "\n").Replace('\r', '\n').Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string rawLine in lines)
                {
                    string[] parts = rawLine.Split('\t');
                    if (parts.Length < 4) continue;

                    string kind = FromBase64(parts[0]);
                    string key = FromBase64(parts[1]);
                    string type = FromBase64(parts[2]);
                    string value = FromBase64(parts[3]);
                    QuantityPipeAttributes attrs = GetProfile(defaults, kind);
                    if (attrs == null || string.IsNullOrWhiteSpace(key)) continue;

                    if (ApplyField(attrs, key, type, value)) count++;
                }
            }

            defaults.MainPipe.ObjectKind = QuantityPipeAttributes.KindMainPipe;
            defaults.BranchPipe.ObjectKind = QuantityPipeAttributes.KindBranchPipe;
            defaults.NodeWell.ObjectKind = QuantityPipeAttributes.KindNodeWell;
            QuantityPipeAttributes.ApplyStructureLayerText(defaults.MainPipe);
            QuantityPipeAttributes.ApplyStructureLayerText(defaults.BranchPipe);
            QuantityPipeAttributes.ApplyStructureLayerText(defaults.NodeWell);
            QuantityAttributeDefaultStore.Save(defaults);
            CDBoxStudioLogger.Info("Studio 保存属性默认表：" + count + " 个字段。路径：" + DefaultsFilePath);
            return count;
        }

        private static QuantityPipeAttributes GetProfile(QuantityAttributeDefaults defaults, string kind)
        {
            if (defaults == null) return null;
            if (QuantityPipeAttributes.IsNodeKind(kind)) return defaults.NodeWell;
            if (QuantityPipeAttributes.IsBranchKind(kind)) return defaults.BranchPipe;
            return defaults.MainPipe;
        }

        private static bool ApplyField(QuantityPipeAttributes attrs, string key, string type, string value)
        {
            if (attrs == null || string.IsNullOrWhiteSpace(key)) return false;
            key = key.Trim();
            bool boolValue = IsTrue(value);
            double numberValue = ParseDouble(value);

            switch (key.ToLowerInvariant())
            {
                case "enabled": attrs.Enabled = boolValue; return true;
                case "material": attrs.Material = value ?? string.Empty; return true;
                case "diameter": attrs.Diameter = value ?? string.Empty; return true;
                case "drawlengthwidthheightannotation": attrs.DrawLengthWidthHeightAnnotation = boolValue; return true;
                case "trenchwidth": attrs.TrenchWidth = numberValue; return true;
                case "roadthickness": attrs.RoadThickness = numberValue; return true;
                case "excavationtype": attrs.ExcavationType = value ?? string.Empty; return true;
                case "backfilltype": attrs.BackfillType = value ?? string.Empty; return true;
                case "backfillstructure": attrs.BackfillStructure = QuantityPipeAttributes.NormalizeStructureLayerText(value ?? string.Empty); return true;
                case "branchtype": attrs.BranchType = value ?? string.Empty; return true;
                case "branchincludeincalculation": attrs.BranchIncludeInCalculation = boolValue; return true;
                case "branchdepth": attrs.BranchDepth = numberValue; return true;
                case "wellspec": attrs.WellSpec = value ?? string.Empty; return true;
                case "wellcovermaterial": attrs.WellCoverMaterial = value ?? string.Empty; return true;
                case "wellmaterialtype": attrs.WellMaterialType = value ?? string.Empty; return true;
                case "welltype": attrs.WellType = value ?? string.Empty; return true;
                case "siltwelldeductdepth500": attrs.SiltWellDeductDepth500 = numberValue; return true;
                case "siltwelldeductdepth700": attrs.SiltWellDeductDepth700 = numberValue; return true;
                case "excavationlength": attrs.ExcavationLength = numberValue; return true;
                case "excavationwidth": attrs.ExcavationWidth = numberValue; return true;
                case "coverplate": attrs.CoverPlate = value ?? string.Empty; return true;
                case "sandcushionthickness": attrs.SandCushionThickness = numberValue; return true;
                case "gravelcushionthickness": attrs.GravelCushionThickness = numberValue; return true;
                case "c25restorethickness": attrs.C25RestoreThickness = numberValue; return true;
                case "pipeouterdiameter": attrs.PipeOuterDiameter = numberValue; return true;
                case "deductpipevolume": attrs.DeductPipeVolume = boolValue; return true;
                default:
                    CDBoxStudioLogger.Warn("属性默认表保存时忽略未知字段：" + key);
                    return false;
            }
        }

        private static void AppendProfile(StringBuilder json, string id, string title, string kind, QuantityPipeAttributes attrs, bool includePipeDeduct)
        {
            attrs = attrs == null ? QuantityPipeAttributes.DefaultForKind(kind) : attrs.CloneForDefaultProfile();
            attrs.ObjectKind = kind;
            QuantityPipeAttributes.ApplyStructureLayerText(attrs);

            json.Append('{');
            AppendJsonProperty(json, "id", id); json.Append(',');
            AppendJsonProperty(json, "title", title); json.Append(',');
            AppendJsonProperty(json, "kind", kind); json.Append(',');
            json.Append("\"fields\":[");

            var first = true;
            AppendBoolField(json, ref first, "Enabled", "参与统计", attrs.Enabled, "关闭后对象仍可保留属性，但工程量统计默认跳过。");

            if (QuantityPipeAttributes.IsMainPipeKind(kind))
            {
                AppendTextField(json, ref first, "Material", "管材", attrs.Material, string.Empty);
                AppendBoolField(json, ref first, "DrawLengthWidthHeightAnnotation", "标注长宽高", attrs.DrawLengthWidthHeightAnnotation, "主管默认建议开启，供管长标注读取。");
                AppendNumberField(json, ref first, "TrenchWidth", "开挖宽度 m", attrs.TrenchWidth, string.Empty);
                AppendNumberField(json, ref first, "RoadThickness", "原路面结构层 m", attrs.RoadThickness, string.Empty);
                AppendSelectField(json, ref first, "ExcavationType", "开挖方式", attrs.ExcavationType, new[] { "机械开挖", "人工开挖" }, string.Empty);
                AppendSelectField(json, ref first, "BackfillType", "回填类型", attrs.BackfillType, new[] { "中粗砂回填", "原土回填", "混合/特殊" }, string.Empty);
                AppendTextAreaField(json, ref first, "BackfillStructure", "回填结构层", attrs.BackfillStructure, "每行一层：层名 厚度 标记。可用“锁定”“管线层”“井下层”。");
            }
            else if (QuantityPipeAttributes.IsBranchKind(kind))
            {
                AppendTextField(json, ref first, "Material", "管材", attrs.Material, string.Empty);
                AppendBoolField(json, ref first, "DrawLengthWidthHeightAnnotation", "标注长宽高", attrs.DrawLengthWidthHeightAnnotation, "支管默认建议关闭。");
                AppendBoolField(json, ref first, "BranchIncludeInCalculation", "加入计算", attrs.BranchIncludeInCalculation, string.Empty);
                AppendNumberField(json, ref first, "RoadThickness", "原路面结构层 m", attrs.RoadThickness, string.Empty);
                AppendNumberField(json, ref first, "TrenchWidth", "开挖宽度 m", attrs.TrenchWidth, string.Empty);
                AppendNumberField(json, ref first, "BranchDepth", "开挖深度 m", attrs.BranchDepth, string.Empty);
                AppendSelectField(json, ref first, "ExcavationType", "开挖方式", attrs.ExcavationType, new[] { "人工开挖", "机械开挖" }, string.Empty);
                AppendSelectField(json, ref first, "BackfillType", "回填类型", attrs.BackfillType, new[] { "中粗砂回填", "原土回填", "混合/特殊", "无结构层" }, string.Empty);
                AppendTextAreaField(json, ref first, "BackfillStructure", "回填结构层", attrs.BackfillStructure, "明管/并埋可为空；原土回填可只保留一层原土回填。");
            }
            else
            {
                AppendSelectField(json, ref first, "WellMaterialType", "井材料类型", attrs.WellMaterialType, new[] { "成品塑料井", "砖砌井", "现浇混凝土井" }, string.Empty);
                AppendSelectField(json, ref first, "WellCoverMaterial", "井盖类型/材料", attrs.WellCoverMaterial, new[] { "混凝土井盖", "铸铁井盖" }, string.Empty);
                AppendSelectField(json, ref first, "WellType", "井类型", attrs.WellType, new[] { "检查井", "沉泥井", "跌水井" }, string.Empty);
                AppendNumberField(json, ref first, "SiltWellDeductDepth500", "500沉泥扣减 m", attrs.SiltWellDeductDepth500, string.Empty);
                AppendNumberField(json, ref first, "SiltWellDeductDepth700", "700沉泥扣减 m", attrs.SiltWellDeductDepth700, string.Empty);
                AppendNumberField(json, ref first, "RoadThickness", "原路面结构层 m", attrs.RoadThickness, string.Empty);
                AppendNumberField(json, ref first, "ExcavationLength", "开挖长 m", attrs.ExcavationLength, string.Empty);
                AppendNumberField(json, ref first, "ExcavationWidth", "开挖宽 m", attrs.ExcavationWidth, string.Empty);
                AppendSelectField(json, ref first, "ExcavationType", "开挖方式", attrs.ExcavationType, new[] { "机械开挖", "人工开挖" }, string.Empty);
                AppendSelectField(json, ref first, "BackfillType", "回填类型", attrs.BackfillType, new[] { "中粗砂回填", "原土回填", "混合/特殊" }, string.Empty);
                AppendTextAreaField(json, ref first, "BackfillStructure", "井结构层", attrs.BackfillStructure, "可在层尾追加“井下层”，用于井深总高与开挖深度计算。");
                AppendTextField(json, ref first, "CoverPlate", "承压盖板", attrs.CoverPlate, string.Empty);
            }

            // Preview 5.1：垫层/恢复厚度由结构层派生，不再作为默认表独立输入项展示。
            if (includePipeDeduct)
            {
                AppendBoolField(json, ref first, "DeductPipeVolume", "扣除管身体积", attrs.DeductPipeVolume, string.Empty);
            }

            json.Append("]}");
        }

        private static void AppendTextField(StringBuilder json, ref bool first, string key, string label, string value, string help)
        {
            AppendField(json, ref first, key, label, "text", value, null, false, help);
        }

        private static void AppendTextAreaField(StringBuilder json, ref bool first, string key, string label, string value, string help)
        {
            AppendField(json, ref first, key, label, "textarea", value, null, true, help);
        }

        private static void AppendNumberField(StringBuilder json, ref bool first, string key, string label, double value, string help)
        {
            AppendField(json, ref first, key, label, "number", FormatNumber(value), null, false, help);
        }

        private static void AppendBoolField(StringBuilder json, ref bool first, string key, string label, bool value, string help)
        {
            AppendField(json, ref first, key, label, "bool", value ? "1" : "0", null, false, help);
        }

        private static void AppendSelectField(StringBuilder json, ref bool first, string key, string label, string value, string[] options, string help)
        {
            AppendField(json, ref first, key, label, "select", value, options, false, help);
        }

        private static void AppendField(StringBuilder json, ref bool first, string key, string label, string type, string value, string[] options, bool wide, string help)
        {
            if (!first) json.Append(',');
            first = false;
            json.Append('{');
            AppendJsonProperty(json, "key", key); json.Append(',');
            AppendJsonProperty(json, "label", label); json.Append(',');
            AppendJsonProperty(json, "type", type); json.Append(',');
            AppendJsonProperty(json, "value", value ?? string.Empty); json.Append(',');
            json.Append("\"wide\":").Append(wide ? "true" : "false").Append(',');
            AppendJsonProperty(json, "help", help ?? string.Empty);
            if (options != null && options.Length > 0)
            {
                json.Append(",\"options\":[");
                for (int i = 0; i < options.Length; i++)
                {
                    if (i > 0) json.Append(',');
                    AppendJsonString(json, options[i]);
                }
                json.Append(']');
            }
            json.Append('}');
        }

        private static void AppendJsonProperty(StringBuilder json, string name, string value)
        {
            AppendJsonString(json, name);
            json.Append(':');
            AppendJsonString(json, value ?? string.Empty);
        }

        private static void AppendJsonString(StringBuilder json, string value)
        {
            json.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                foreach (char ch in value)
                {
                    switch (ch)
                    {
                        case '\\': json.Append("\\\\"); break;
                        case '"': json.Append("\\\""); break;
                        case '\r': json.Append("\\r"); break;
                        case '\n': json.Append("\\n"); break;
                        case '\t': json.Append("\\t"); break;
                        default:
                            if (ch < ' ') json.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                            else json.Append(ch);
                            break;
                    }
                }
            }
            json.Append('"');
        }

        private static string FormatNumber(double value)
        {
            if (Math.Abs(value) < 0.0000001) return string.Empty;
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static double ParseDouble(string value)
        {
            double result;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) return result;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result)) return result;
            return 0.0;
        }

        private static bool IsTrue(string value)
        {
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        private static string FromBase64(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return value;
            }
        }
    }
}
