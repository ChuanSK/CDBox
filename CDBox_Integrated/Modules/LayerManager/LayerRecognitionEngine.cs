using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TCPipeAutoDraw.Modules.LayerManager
{
    /// <summary>
    /// 将宽松的外部图层名转换为稳定的内部属性。引擎本身不写图、不改名，
    /// 只生成可解释的预览结果，供图层管理器确认后应用。
    /// </summary>
    public static class LayerRecognitionEngine
    {
        public const string MatchModeKeywords = "关键词组合";
        public const string MatchModeTemplate = "模板";

        private static readonly string[] PipeMaterials =
        {
            "钢筋混凝土管", "HDPE双壁波纹管", "双壁波纹管", "波纹管",
            "球墨铸铁", "铸铁", "UPVC", "HDPE", "PVC", "PE", "钢管"
        };

        private static readonly string[] ConstructionTypes =
        {
            "混凝土恢复", "原土回填", "砂石回填", "中粗砂回填", "砖砌", "明管", "并埋", "包封"
        };

        private static readonly string[] NodeTypes =
        {
            "沉泥检查井", "沉泥井", "检查井", "跌水井", "阀门井", "雨水口", "手孔井", "工作井", "接收井"
        };

        private static readonly string[] StructureTypes =
        {
            "钢筋混凝土", "混凝土", "沥青混凝土", "级配碎石", "碎石", "中粗砂", "砂垫层"
        };

        public static LayerRecognitionResult Recognize(
            string layerName,
            IEnumerable<LayerRecognitionRule> rules)
        {
            return Recognize(layerName, rules, null);
        }

        public static LayerRecognitionResult Recognize(
            string layerName,
            IEnumerable<LayerRecognitionRule> rules,
            LayerMetadata existingMetadata)
        {
            string raw = (layerName ?? string.Empty).Trim();
            var result = new LayerRecognitionResult
            {
                RawName = raw,
                NormalizedName = NormalizeName(raw)
            };

            if (raw.Length == 0)
            {
                result.Explanation = "图层名为空，无法识别。";
                result.NeedsConfirmation = true;
                return result;
            }

            ExtractStructuredAttributes(result);
            ApplyRules(result, rules);
            InferParentAndCategory(result);
            ApplyExistingMetadata(result, existingMetadata);
            BuildMetadata(result);
            EvaluateResult(result);
            return result;
        }

        public static string NormalizeName(string layerName)
        {
            string text = (layerName ?? string.Empty).Trim();
            if (text.Length == 0) return string.Empty;

            text = text
                .Replace('（', '(')
                .Replace('）', ')')
                .Replace('【', '[')
                .Replace('】', ']')
                .Replace('，', ',')
                .Replace('；', ';')
                .Replace('：', ':')
                .Replace('—', '-')
                .Replace('–', '-')
                .Replace('_', '-');

            text = Regex.Replace(text, @"混泥土|砼", "混凝土", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"波纹(?!管)", "波纹管", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?i)\bupvc\b", "UPVC");
            text = Regex.Replace(text, @"(?i)\bhdpe\b", "HDPE");
            text = Regex.Replace(text, @"(?i)\bpvc\b", "PVC");
            text = Regex.Replace(text, @"(?i)\bpe\b", "PE");
            text = Regex.Replace(text, @"(?i)\bdn\s*(\d+(?:\.\d+)?)", "DN$1");
            text = Regex.Replace(text, @"(?i)\bd\s*(\d+(?:\.\d+)?)", "D$1");
            text = Regex.Replace(text, @"\s+", string.Empty);
            text = Regex.Replace(text, @"-{2,}", "-").Trim('-');
            return text;
        }

        private static void ExtractStructuredAttributes(LayerRecognitionResult result)
        {
            string name = result.NormalizedName;
            LayerStructuredAttributes attributes = result.Attributes;
            bool wellContext = name.IndexOf("井", StringComparison.CurrentCultureIgnoreCase) >= 0;

            string dn = MatchValue(name, @"(?i)DN(?<value>\d+(?:\.\d+)?)");
            if (dn.Length == 0 && !wellContext)
            {
                dn = MatchValue(name,
                    @"(?<![A-Za-z0-9])(?<value>\d{2,4}(?:\.\d+)?)(?=(?:UPVC|HDPE|PVC|PE|钢筋混凝土|双壁波纹|波纹|铸铁|钢管))");
            }
            if (dn.Length == 0 && !wellContext)
            {
                dn = MatchValue(name,
                    @"(?:UPVC|HDPE|PVC|PE|钢筋混凝土管?|双壁波纹管?|波纹管?|铸铁管?|钢管)(?<value>\d{2,4}(?:\.\d+)?)");
            }
            if (dn.Length == 0 && !wellContext && ContainsAny(name, PipeMaterials))
            {
                dn = MatchValue(name, @"^(?<value>\d{2,4}(?:\.\d+)?)");
            }

            string wellDiameter = string.Empty;
            if (wellContext)
            {
                wellDiameter = MatchValue(name, @"(?i)(?:^|[-(])D(?<value>\d{2,4}(?:\.\d+)?)");
                if (wellDiameter.Length == 0)
                {
                    wellDiameter = MatchValue(name, @"(?<value>\d{3,4})(?=(?:铸铁)?井(?:盖)?|井(?:盖)?)");
                }
            }

            if (dn.Length > 0)
            {
                attributes.Specification = "DN" + NormalizeNumberText(dn);
                result.Evidence.Add("从图层名提取管径 " + attributes.Specification);
            }
            else if (wellDiameter.Length > 0)
            {
                attributes.Specification = "D" + NormalizeNumberText(wellDiameter);
                result.Evidence.Add("从图层名提取井径 " + attributes.Specification);
            }

            string material = FirstContained(name, PipeMaterials);
            if (material.Length > 0)
            {
                attributes.Material = CanonicalMaterial(material);
                result.Evidence.Add("识别材料 " + attributes.Material);
            }

            string construction = FirstContained(name, ConstructionTypes);
            if (construction.Length == 0 && name.IndexOf("混凝土恢复", StringComparison.CurrentCultureIgnoreCase) >= 0)
                construction = "混凝土恢复";
            if (construction.Length > 0)
            {
                attributes.ConstructionType = construction;
                result.Evidence.Add("识别施工方式 " + construction);
            }

            string nodeType = FirstContained(name, NodeTypes);
            if (nodeType.Length > 0)
            {
                attributes.NodeType = nodeType;
                result.Evidence.Add("识别节点类型 " + nodeType);
            }

            string strength = MatchValue(name, @"(?i)(?<value>C\d{2,3})");
            if (strength.Length > 0)
            {
                attributes.StrengthGrade = strength.ToUpperInvariant();
                result.Evidence.Add("识别强度等级 " + attributes.StrengthGrade);
            }

            string thickness = ExtractThickness(name);
            if (thickness.Length > 0)
            {
                attributes.Thickness = thickness;
                result.Evidence.Add("识别厚度 " + thickness);
            }

            string volume = MatchValue(name, @"(?<value>\d+(?:\.\d+)?)\s*(?:m3|m³|立方米)");
            if (volume.Length > 0)
            {
                attributes.Volume = "V" + NormalizeNumberText(volume) + "m³";
                result.Evidence.Add("识别容积 " + attributes.Volume);
            }

            if (name.IndexOf("化粪池", StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                attributes.ObjectType = "构筑物";
                attributes.Purpose = "化粪池";
            }
            else if (name.IndexOf("隔油池", StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                attributes.ObjectType = "构筑物";
                attributes.Purpose = "隔油池";
            }
            else if (name.IndexOf("测点", StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                attributes.ObjectType = "测点";
                attributes.Purpose = name.IndexOf("代码", StringComparison.CurrentCultureIgnoreCase) >= 0 ? "代码" : string.Empty;
            }
            else if (IsAnnotationName(name))
            {
                attributes.ObjectType = "注记";
                attributes.Purpose = name.IndexOf("主管", StringComparison.CurrentCultureIgnoreCase) >= 0
                    || name.IndexOf("支管", StringComparison.CurrentCultureIgnoreCase) >= 0
                    ? "管线长度注记"
                    : "通用注记";
            }
            else if (name.IndexOf("井", StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                attributes.ObjectType = "井";
            }
            else if (attributes.StrengthGrade.Length > 0 || attributes.Thickness.Length > 0)
            {
                attributes.ObjectType = "结构层";
                attributes.StructureType = FirstContained(name, StructureTypes);
            }
            else if (attributes.Material.Length > 0 || dn.Length > 0
                || name.IndexOf("主管", StringComparison.CurrentCultureIgnoreCase) >= 0
                || name.IndexOf("支管", StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                attributes.ObjectType = "管线";
            }
        }

        private static void ApplyRules(LayerRecognitionResult result, IEnumerable<LayerRecognitionRule> rules)
        {
            List<LayerRecognitionRule> ordered = (rules ?? Enumerable.Empty<LayerRecognitionRule>())
                .Select((rule, index) => new { Rule = rule, Index = index })
                .Where(x => x.Rule != null && x.Rule.Enabled)
                .OrderByDescending(x => x.Rule.Priority)
                .ThenBy(x => x.Index)
                .Select(x => x.Rule)
                .ToList();

            foreach (LayerRecognitionRule rule in ordered)
            {
                Match match;
                if (!IsRuleMatch(rule, result.RawName, result.NormalizedName, out match)) continue;

                Dictionary<string, string> tokens = BuildTokens(result, match);
                string parent = Expand(rule.ParentGroup, tokens).Trim();
                string category = Expand(rule.ParentClass, tokens).Trim();
                List<string> tags = LayerMetadata.ParseTags(Expand(rule.TagText, tokens));
                string mergeMode = (rule.MergeMode ?? string.Empty).Trim();

                result.Metadata.ParentGroup = MergeValue(
                    result.Metadata.ParentGroup, parent, mergeMode, result, "父属性");
                result.Metadata.ParentClass = MergeValue(
                    result.Metadata.ParentClass, category, mergeMode, result, "分类");
                MergeTags(result.Metadata.Tags, tags);

                string label = string.IsNullOrWhiteSpace(rule.Name)
                    ? ((rule.MatchMode ?? string.Empty) + "：" + (rule.Pattern ?? string.Empty))
                    : rule.Name.Trim();
                result.MatchedRules.Add(label);
                result.Evidence.Add("命中规则 " + label);

                if (rule.ConfidenceBase > 0)
                {
                    result.Confidence = Math.Max(result.Confidence, Math.Min(1d, rule.ConfidenceBase));
                }

                if (rule.StopAfterMatch || string.Equals(mergeMode, "首条命中", StringComparison.CurrentCultureIgnoreCase))
                    break;
            }
        }

        private static void InferParentAndCategory(LayerRecognitionResult result)
        {
            string name = result.NormalizedName;
            LayerStructuredAttributes attributes = result.Attributes;
            string inferredParent = string.Empty;

            bool main = name.IndexOf("主管", StringComparison.CurrentCultureIgnoreCase) >= 0;
            bool branch = name.IndexOf("支管", StringComparison.CurrentCultureIgnoreCase) >= 0;
            if (attributes.ObjectType == "注记") inferredParent = "注记";
            else if (attributes.ObjectType == "测点") inferredParent = "测点";
            else if (attributes.ObjectType == "井") inferredParent = "井";
            else if (attributes.ObjectType == "结构层") inferredParent = "结构层";
            else if (attributes.ObjectType == "构筑物") inferredParent = "构筑物";
            else if (main && branch)
            {
                result.Conflicts.Add("图层名同时包含主管与支管");
            }
            else if (main) inferredParent = "主管";
            else if (branch) inferredParent = "支管";
            else if (attributes.ObjectType == "管线" && attributes.Specification.Length > 2)
            {
                string diameter = attributes.Specification.Substring(2);
                if (diameter == "200" || diameter == "300") inferredParent = "主管";
                else if (diameter == "75" || diameter == "110" || diameter == "160") inferredParent = "支管";
                if (inferredParent.Length > 0)
                    result.Evidence.Add("按兼容默认管径表识别为" + inferredParent);
            }

            string legacyParent = result.Metadata.ParentGroup ?? string.Empty;
            if (string.Equals(legacyParent, "混凝土", StringComparison.CurrentCultureIgnoreCase)
                && attributes.ObjectType == "结构层")
            {
                result.Metadata.ParentGroup = "结构层";
                if (string.IsNullOrWhiteSpace(result.Metadata.ParentClass)) result.Metadata.ParentClass = "混凝土";
            }
            else if (string.Equals(legacyParent, "化粪池", StringComparison.CurrentCultureIgnoreCase))
            {
                result.Metadata.ParentGroup = "构筑物";
                if (string.IsNullOrWhiteSpace(result.Metadata.ParentClass)) result.Metadata.ParentClass = "化粪池";
            }

            if (string.IsNullOrWhiteSpace(result.Metadata.ParentGroup))
            {
                result.Metadata.ParentGroup = inferredParent;
            }
            else if (inferredParent.Length > 0
                && !string.Equals(result.Metadata.ParentGroup, inferredParent, StringComparison.CurrentCultureIgnoreCase))
            {
                result.Conflicts.Add("规则父属性“" + result.Metadata.ParentGroup + "”与名称识别“" + inferredParent + "”不一致");
            }

            if (string.IsNullOrWhiteSpace(result.Metadata.ParentClass))
            {
                if (attributes.ObjectType == "井" && attributes.NodeType.Length > 0)
                    result.Metadata.ParentClass = attributes.NodeType;
                else if (attributes.ObjectType == "构筑物")
                    result.Metadata.ParentClass = attributes.Purpose;
                else if (attributes.ObjectType == "注记")
                    result.Metadata.ParentClass = attributes.Purpose;
                else if (attributes.ObjectType == "测点")
                    result.Metadata.ParentClass = attributes.Purpose;
                else if (attributes.ObjectType == "结构层")
                    result.Metadata.ParentClass = attributes.StructureType;
            }
        }

        private static void ApplyExistingMetadata(LayerRecognitionResult result, LayerMetadata existing)
        {
            if (existing == null || existing.IsEmpty) return;
            if (IsGeneratedRecognitionSource(existing.RecognitionSource))
            {
                result.Evidence.Add("已重新扫描先前自动识别结果");
                return;
            }

            if (!string.IsNullOrWhiteSpace(existing.ParentGroup))
            {
                if (!string.IsNullOrWhiteSpace(result.Metadata.ParentGroup)
                    && !string.Equals(existing.ParentGroup, result.Metadata.ParentGroup, StringComparison.CurrentCultureIgnoreCase))
                {
                    result.Conflicts.Add("现有父属性“" + existing.ParentGroup + "”与识别建议“" + result.Metadata.ParentGroup + "”不同");
                }
                result.Metadata.ParentGroup = existing.ParentGroup.Trim();
            }
            if (!string.IsNullOrWhiteSpace(existing.ParentClass)) result.Metadata.ParentClass = existing.ParentClass.Trim();
            MergeTags(result.Metadata.Tags, existing.Tags);

            result.Attributes.ObjectType = OverrideIfPresent(result.Attributes.ObjectType, existing.ObjectType);
            result.Attributes.Specification = OverrideIfPresent(result.Attributes.Specification, existing.Specification);
            result.Attributes.Material = OverrideIfPresent(result.Attributes.Material, existing.Material);
            result.Attributes.ConstructionType = OverrideIfPresent(result.Attributes.ConstructionType, existing.ConstructionType);
            result.Attributes.NodeType = OverrideIfPresent(result.Attributes.NodeType, existing.NodeType);
            result.Attributes.StructureType = OverrideIfPresent(result.Attributes.StructureType, existing.StructureType);
            result.Attributes.Purpose = OverrideIfPresent(result.Attributes.Purpose, existing.Purpose);
            result.Source = string.IsNullOrWhiteSpace(existing.RecognitionSource) ? "现有图层属性" : existing.RecognitionSource;
            result.Confidence = Math.Max(result.Confidence, existing.RecognitionConfidence);
            result.Evidence.Add("保留现有显式图层属性");
        }

        private static void BuildMetadata(LayerRecognitionResult result)
        {
            LayerStructuredAttributes a = result.Attributes;
            LayerMetadata m = result.Metadata;

            m.ParentGroup = (m.ParentGroup ?? string.Empty).Trim();
            m.ParentClass = (m.ParentClass ?? string.Empty).Trim();
            m.Tags = NormalizeTags((m.Tags ?? new List<string>())
                .Concat(new[]
                {
                    a.Specification, a.Material, a.ConstructionType, a.NodeType,
                    a.StrengthGrade, a.Thickness, a.Volume, a.Purpose
                }));
            m.NormalizedName = result.NormalizedName;
            m.ObjectType = a.ObjectType;
            m.Specification = a.Specification;
            m.Material = a.Material;
            m.ConstructionType = a.ConstructionType;
            m.NodeType = a.NodeType;
            m.StructureType = a.StructureType;
            m.Purpose = a.Purpose;

            result.SuggestedName = BuildSuggestedName(result);
            m.SuggestedName = result.SuggestedName;
        }

        private static void EvaluateResult(LayerRecognitionResult result)
        {
            LayerStructuredAttributes a = result.Attributes;
            LayerMetadata m = result.Metadata;

            if (a.ObjectType == "管线")
            {
                if (string.IsNullOrWhiteSpace(m.ParentGroup)) result.MissingFields.Add("主管/支管归属");
                if (string.IsNullOrWhiteSpace(a.Specification)) result.MissingFields.Add("管径");
                if (string.IsNullOrWhiteSpace(a.Material)) result.MissingFields.Add("材料");
            }
            else if (a.ObjectType == "井")
            {
                if (string.IsNullOrWhiteSpace(a.NodeType)) result.MissingFields.Add("井型");
            }
            else if (a.ObjectType == "结构层")
            {
                if (string.IsNullOrWhiteSpace(a.StructureType)) result.MissingFields.Add("结构层类型");
                if (string.IsNullOrWhiteSpace(a.Thickness)) result.MissingFields.Add("厚度");
            }

            bool mixed = result.Conflicts.Any(x => x.IndexOf("同时包含", StringComparison.CurrentCultureIgnoreCase) >= 0);
            bool hasMeaning = !m.IsEmpty || !string.IsNullOrWhiteSpace(a.ObjectType);
            if (mixed) result.Status = LayerRecognitionStatuses.Mixed;
            else if (result.Conflicts.Count > 0) result.Status = LayerRecognitionStatuses.Conflict;
            else if (!hasMeaning) result.Status = LayerRecognitionStatuses.Unrecognized;
            else if (result.MissingFields.Count > 0) result.Status = LayerRecognitionStatuses.Incomplete;
            else if (IsStandardName(result.RawName, result.SuggestedName)) result.Status = LayerRecognitionStatuses.Standard;
            else result.Status = LayerRecognitionStatuses.Compatible;

            double confidence = result.Confidence;
            if (!string.IsNullOrWhiteSpace(m.ParentGroup)) confidence += 0.30;
            if (!string.IsNullOrWhiteSpace(a.ObjectType)) confidence += 0.16;
            if (!string.IsNullOrWhiteSpace(a.Specification)) confidence += 0.12;
            if (!string.IsNullOrWhiteSpace(a.Material)) confidence += 0.10;
            if (!string.IsNullOrWhiteSpace(a.ConstructionType) || !string.IsNullOrWhiteSpace(a.NodeType)
                || !string.IsNullOrWhiteSpace(a.StructureType) || !string.IsNullOrWhiteSpace(a.Purpose)) confidence += 0.10;
            confidence -= result.MissingFields.Count * 0.08;
            confidence -= result.Conflicts.Count * 0.18;
            if (!hasMeaning) confidence = 0;
            result.Confidence = Math.Max(0, Math.Min(1, confidence));

            if (string.IsNullOrWhiteSpace(result.Source))
            {
                if (result.MatchedRules.Count > 0 && result.Evidence.Count > result.MatchedRules.Count)
                    result.Source = "规则 + 名称提取";
                else if (result.MatchedRules.Count > 0) result.Source = "识别规则";
                else if (hasMeaning) result.Source = "名称提取";
                else result.Source = "未命中";
            }

            result.NeedsConfirmation = result.Status == LayerRecognitionStatuses.Conflict
                || result.Status == LayerRecognitionStatuses.Mixed
                || result.Status == LayerRecognitionStatuses.Incomplete
                || result.Status == LayerRecognitionStatuses.Unrecognized
                || result.Confidence < 0.82;

            var explanation = new List<string>();
            if (result.Evidence.Count > 0) explanation.Add(string.Join("；", result.Evidence.Distinct().ToArray()));
            if (result.MissingFields.Count > 0) explanation.Add("待补充：" + string.Join("、", result.MissingFields.Distinct().ToArray()));
            if (result.Conflicts.Count > 0) explanation.Add("需确认：" + string.Join("；", result.Conflicts.Distinct().ToArray()));
            if (explanation.Count == 0) explanation.Add("未从图层名或规则中识别到有效属性。");
            result.Explanation = string.Join("。", explanation.ToArray());

            m.RecognitionStatus = result.Status;
            m.RecognitionConfidence = result.Confidence;
            m.RecognitionSource = result.Source;
        }

        private static string BuildSuggestedName(LayerRecognitionResult result)
        {
            LayerMetadata m = result.Metadata;
            LayerStructuredAttributes a = result.Attributes;
            var parts = new List<string>();
            AddUnique(parts, m.ParentGroup);
            AddUnique(parts, a.NodeType);
            AddUnique(parts, a.Specification);
            AddUnique(parts, a.Material);
            AddUnique(parts, a.StrengthGrade);
            AddUnique(parts, a.StructureType);
            AddUnique(parts, a.Thickness);
            AddUnique(parts, a.ConstructionType);
            AddUnique(parts, a.Purpose);
            AddUnique(parts, a.Volume);

            if (parts.Count == 0) return result.NormalizedName;
            return string.Join("-", parts.ToArray());
        }

        private static bool IsRuleMatch(
            LayerRecognitionRule rule,
            string rawName,
            string normalizedName,
            out Match match)
        {
            match = null;
            if (rule == null || !rule.Enabled || string.IsNullOrWhiteSpace(rule.Pattern)) return false;
            if (!string.IsNullOrWhiteSpace(rule.Scope)
                && rule.Scope.IndexOf("图层", StringComparison.CurrentCultureIgnoreCase) < 0
                && rule.Scope.IndexOf("名称", StringComparison.CurrentCultureIgnoreCase) < 0)
                return false;

            string candidate = string.IsNullOrWhiteSpace(normalizedName) ? rawName : normalizedName;
            if (MatchesExclusion(rule.ExcludePattern, candidate)) return false;

            string mode = string.IsNullOrWhiteSpace(rule.MatchMode) ? "通配符" : rule.MatchMode.Trim();
            string pattern = rule.Pattern.Trim();
            try
            {
                if (string.Equals(mode, "精确", StringComparison.CurrentCultureIgnoreCase))
                    return string.Equals(candidate, NormalizeName(pattern), StringComparison.CurrentCultureIgnoreCase);
                if (string.Equals(mode, "包含", StringComparison.CurrentCultureIgnoreCase))
                    return candidate.IndexOf(NormalizeName(pattern), StringComparison.CurrentCultureIgnoreCase) >= 0;
                if (string.Equals(mode, "正则", StringComparison.CurrentCultureIgnoreCase))
                {
                    match = Regex.Match(rawName ?? string.Empty, pattern, RegexOptions.IgnoreCase);
                    return match.Success;
                }
                if (string.Equals(mode, MatchModeKeywords, StringComparison.CurrentCultureIgnoreCase))
                {
                    string[] required = SplitKeywords(pattern);
                    return required.Length > 0 && required.All(x =>
                        candidate.IndexOf(NormalizeName(x), StringComparison.CurrentCultureIgnoreCase) >= 0);
                }
                if (string.Equals(mode, MatchModeTemplate, StringComparison.CurrentCultureIgnoreCase))
                {
                    string regex = BuildTemplateRegex(pattern);
                    match = Regex.Match(candidate, regex, RegexOptions.IgnoreCase);
                    return match.Success;
                }

                string wildcardRegex = "^" + Regex.Escape(NormalizeName(pattern))
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                match = Regex.Match(candidate, wildcardRegex, RegexOptions.IgnoreCase);
                return match.Success;
            }
            catch
            {
                return false;
            }
        }

        private static bool MatchesExclusion(string exclusion, string candidate)
        {
            if (string.IsNullOrWhiteSpace(exclusion)) return false;
            foreach (string token in SplitKeywords(exclusion))
            {
                string value = NormalizeName(token);
                if (value.Length > 0 && candidate.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string BuildTemplateRegex(string template)
        {
            string escaped = Regex.Escape(NormalizeName(template));
            escaped = escaped
                .Replace(@"\{DN}", @"(?<DN>DN?\d+(?:\.\d+)?)")
                .Replace(@"\{D}", @"(?<D>D\d+(?:\.\d+)?)")
                .Replace(@"\{T}", @"(?<T>T\d+(?:\.\d+)?)")
                .Replace(@"\{Material}", @"(?<Material>.+?)")
                .Replace(@"\{Type}", @"(?<Type>.+?)")
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".");
            return "^" + escaped + "$";
        }

        private static Dictionary<string, string> BuildTokens(LayerRecognitionResult result, Match match)
        {
            var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "LayerName", result.RawName },
                { "NormalizedName", result.NormalizedName },
                { "BaseName", RemoveBracketSuffix(result.NormalizedName) },
                { "Bracket", GetBracketText(result.NormalizedName) },
                { "DN", result.Attributes.Specification.StartsWith("DN", StringComparison.OrdinalIgnoreCase) ? result.Attributes.Specification : string.Empty },
                { "D", result.Attributes.Specification.StartsWith("D", StringComparison.OrdinalIgnoreCase) && !result.Attributes.Specification.StartsWith("DN", StringComparison.OrdinalIgnoreCase) ? result.Attributes.Specification : string.Empty },
                { "PipeMaterial", result.Attributes.Material },
                { "Material", result.Attributes.Material },
                { "ConstructionType", result.Attributes.ConstructionType },
                { "NodeType", result.Attributes.NodeType }
            };

            if (match != null && match.Success)
            {
                for (int i = 0; i < match.Groups.Count; i++)
                    tokens[i.ToString(CultureInfo.InvariantCulture)] = match.Groups[i].Value.Trim();
                foreach (string groupName in new[] { "DN", "D", "T", "Material", "Type", "value" })
                {
                    Group group = match.Groups[groupName];
                    if (group != null && group.Success) tokens[groupName] = group.Value.Trim();
                }
            }
            return tokens;
        }

        private static string Expand(string template, IDictionary<string, string> tokens)
        {
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;
            return Regex.Replace(template, @"\{(?<key>[^{}]+)\}", delegate (Match m)
            {
                string value;
                return tokens != null && tokens.TryGetValue(m.Groups["key"].Value.Trim(), out value)
                    ? value ?? string.Empty
                    : string.Empty;
            });
        }

        private static string MergeValue(
            string current,
            string incoming,
            string mergeMode,
            LayerRecognitionResult result,
            string fieldName)
        {
            if (string.IsNullOrWhiteSpace(incoming)) return current ?? string.Empty;
            incoming = incoming.Trim();
            current = current ?? string.Empty;

            if (current.Length == 0)
            {
                return incoming;
            }
            if (string.Equals(current, incoming, StringComparison.CurrentCultureIgnoreCase)) return current;

            if (string.Equals(mergeMode, "覆盖", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(mergeMode, "最后命中", StringComparison.CurrentCultureIgnoreCase))
            {
                return incoming;
            }
            result.Conflicts.Add(fieldName + "存在多个候选：“" + current + "”与“" + incoming + "”");
            return current;
        }

        private static void MergeTags(List<string> target, IEnumerable<string> source)
        {
            if (target == null || source == null) return;
            foreach (string tag in source)
            {
                string value = (tag ?? string.Empty).Trim();
                if (value.Length == 0) continue;
                if (!target.Any(x => string.Equals(x, value, StringComparison.CurrentCultureIgnoreCase)))
                    target.Add(value);
            }
        }

        private static List<string> NormalizeTags(IEnumerable<string> tags)
        {
            var result = new List<string>();
            MergeTags(result, tags);
            return result;
        }

        private static string ExtractThickness(string name)
        {
            string direct = MatchValue(name, @"(?i)T(?<value>\d+(?:\.\d+)?)");
            if (direct.Length > 0) return "T" + NormalizeNumberText(direct);

            bool structureContext = Regex.IsMatch(name,
                @"混凝土|碎石|砂|垫层|基层|面层|恢复|结构层",
                RegexOptions.IgnoreCase);
            if (!structureContext) return string.Empty;

            Match unit = Regex.Match(name, @"(?<value>\d+(?:\.\d+)?)\s*(?<unit>mm|cm|m)(?![A-Za-z0-9³])", RegexOptions.IgnoreCase);
            if (!unit.Success) return string.Empty;

            double value;
            if (!double.TryParse(unit.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return string.Empty;
            string unitName = unit.Groups["unit"].Value.ToLowerInvariant();
            double millimeters = unitName == "m" ? value * 1000d : unitName == "cm" ? value * 10d : value;
            return "T" + Math.Round(millimeters, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
        }

        private static string MatchValue(string text, string pattern)
        {
            Match match = Regex.Match(text ?? string.Empty, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
        }

        private static string NormalizeNumberText(string text)
        {
            double number;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                ? number.ToString("0.###", CultureInfo.InvariantCulture)
                : (text ?? string.Empty).Trim();
        }

        private static string FirstContained(string text, IEnumerable<string> candidates)
        {
            return (candidates ?? Enumerable.Empty<string>())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)
                    && (text ?? string.Empty).IndexOf(x, StringComparison.CurrentCultureIgnoreCase) >= 0)
                ?? string.Empty;
        }

        private static bool ContainsAny(string text, IEnumerable<string> candidates)
        {
            return FirstContained(text, candidates).Length > 0;
        }

        private static string CanonicalMaterial(string material)
        {
            if (string.Equals(material, "HDPE双壁波纹管", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(material, "双壁波纹管", StringComparison.CurrentCultureIgnoreCase))
                return "波纹管";
            return material ?? string.Empty;
        }

        private static bool IsAnnotationName(string name)
        {
            return string.Equals(name, "ZJ", StringComparison.OrdinalIgnoreCase)
                || name.IndexOf("注记", StringComparison.CurrentCultureIgnoreCase) >= 0
                || name.IndexOf("标注", StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static string RemoveBracketSuffix(string name)
        {
            return Regex.Replace(name ?? string.Empty, @"[([][^)\]]+[)\]]$", string.Empty).Trim();
        }

        private static string GetBracketText(string name)
        {
            Match match = Regex.Match(name ?? string.Empty, @"[([](?<value>[^)\]]+)[)\]]$");
            return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
        }

        private static string[] SplitKeywords(string text)
        {
            return (text ?? string.Empty)
                .Replace('，', ',')
                .Replace('、', ',')
                .Replace('；', ',')
                .Replace(';', ',')
                .Replace('|', ',')
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();
        }

        private static string OverrideIfPresent(string target, string value)
        {
            return string.IsNullOrWhiteSpace(value) ? target ?? string.Empty : value.Trim();
        }

        private static void AddUnique(List<string> parts, string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length == 0) return;
            if (!parts.Any(x => string.Equals(x, value, StringComparison.CurrentCultureIgnoreCase)))
                parts.Add(value);
        }

        private static bool IsStandardName(string raw, string suggested)
        {
            if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(suggested)) return false;
            return string.Equals(NormalizeName(raw), NormalizeName(suggested), StringComparison.CurrentCultureIgnoreCase)
                && raw.IndexOf("-", StringComparison.Ordinal) >= 0;
        }

        private static bool IsGeneratedRecognitionSource(string source)
        {
            string value = (source ?? string.Empty).Trim();
            return string.Equals(value, "识别规则", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(value, "名称提取", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(value, "规则 + 名称提取", StringComparison.CurrentCultureIgnoreCase)
                || value.StartsWith("CDBox 内置", StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
