using System;
using System.Collections.Generic;
using System.Linq;
using TCPipeAutoDraw.Core.Colors;

namespace TCPipeAutoDraw.Modules.LayerManager
{
    public class LayerInfo
    {
        public bool Selected { get; set; }
        public string Name { get; set; }
        public string StatusText { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsOff { get; set; }
        public bool IsFrozen { get; set; }
        public bool IsLocked { get; set; }
        public bool IsDependent { get; set; }
        public bool IsPlottable { get; set; }
        public short ColorIndex { get; set; }
        public CDBoxColor Color { get; set; }
        public string Linetype { get; set; }
        public int ObjectCount { get; set; }

        /// <summary>
        /// CDBox 主父级。用于稳定树形归属，例如：主管、支管、注记、道路、构筑物。
        /// </summary>
        public string ParentGroup { get; set; }

        /// <summary>
        /// CDBox 二级分类。用于规格/类型归属，例如：110PVC管、75PVC管、管线长度注记。
        /// </summary>
        public string ParentClass { get; set; }

        /// <summary>
        /// 以逗号展示的标签文本。实际保存时会规范化去重。
        /// </summary>
        public string TagText { get; set; }

        /// <summary>
        /// 本次扫描得到的结构化识别结果。该结果用于预览，不会直接改名或覆盖图层属性。
        /// </summary>
        public string NormalizedName { get; set; }
        public string SuggestedName { get; set; }
        public string RecognitionStatus { get; set; }
        public double RecognitionConfidence { get; set; }
        public string RecognitionSource { get; set; }
        public string RecognitionExplanation { get; set; }
        public string RecognitionConflicts { get; set; }
        public string RecognitionMissingFields { get; set; }
        public string RecognitionParentGroup { get; set; }
        public string RecognitionParentClass { get; set; }
        public string ObjectType { get; set; }
        public string Specification { get; set; }
        public string Material { get; set; }
        public string ConstructionType { get; set; }
        public string NodeType { get; set; }
        public string StructureType { get; set; }
        public string Purpose { get; set; }

        public LayerInfo()
        {
            Name = string.Empty;
            StatusText = string.Empty;
            Linetype = string.Empty;
            ParentGroup = string.Empty;
            ParentClass = string.Empty;
            TagText = string.Empty;
            NormalizedName = string.Empty;
            SuggestedName = string.Empty;
            RecognitionStatus = LayerRecognitionStatuses.Unrecognized;
            RecognitionSource = string.Empty;
            RecognitionExplanation = string.Empty;
            RecognitionConflicts = string.Empty;
            RecognitionMissingFields = string.Empty;
            RecognitionParentGroup = string.Empty;
            RecognitionParentClass = string.Empty;
            ObjectType = string.Empty;
            Specification = string.Empty;
            Material = string.Empty;
            ConstructionType = string.Empty;
            NodeType = string.Empty;
            StructureType = string.Empty;
            Purpose = string.Empty;
            Color = CDBoxColor.FromIndex(7);
        }
    }

    public sealed class LayerMetadata
    {
        public string ParentGroup { get; set; }
        public string ParentClass { get; set; }
        public List<string> Tags { get; set; }
        public string NormalizedName { get; set; }
        public string SuggestedName { get; set; }
        public string ObjectType { get; set; }
        public string Specification { get; set; }
        public string Material { get; set; }
        public string ConstructionType { get; set; }
        public string NodeType { get; set; }
        public string StructureType { get; set; }
        public string Purpose { get; set; }
        public string RecognitionStatus { get; set; }
        public double RecognitionConfidence { get; set; }
        public string RecognitionSource { get; set; }

        public LayerMetadata()
        {
            ParentGroup = string.Empty;
            ParentClass = string.Empty;
            Tags = new List<string>();
            NormalizedName = string.Empty;
            SuggestedName = string.Empty;
            ObjectType = string.Empty;
            Specification = string.Empty;
            Material = string.Empty;
            ConstructionType = string.Empty;
            NodeType = string.Empty;
            StructureType = string.Empty;
            Purpose = string.Empty;
            RecognitionStatus = string.Empty;
            RecognitionSource = string.Empty;
        }

        public string TagText
        {
            get { return Tags == null ? string.Empty : string.Join("、", Tags.ToArray()); }
            set { Tags = ParseTags(value); }
        }

        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrWhiteSpace(ParentGroup)
                    && string.IsNullOrWhiteSpace(ParentClass)
                    && (Tags == null || Tags.Count == 0)
                    && string.IsNullOrWhiteSpace(ObjectType)
                    && string.IsNullOrWhiteSpace(Specification)
                    && string.IsNullOrWhiteSpace(Material)
                    && string.IsNullOrWhiteSpace(ConstructionType)
                    && string.IsNullOrWhiteSpace(NodeType)
                    && string.IsNullOrWhiteSpace(StructureType)
                    && string.IsNullOrWhiteSpace(Purpose);
            }
        }

        public LayerMetadata Clone()
        {
            return new LayerMetadata
            {
                ParentGroup = ParentGroup ?? string.Empty,
                ParentClass = ParentClass ?? string.Empty,
                Tags = Tags == null ? new List<string>() : new List<string>(Tags),
                NormalizedName = NormalizedName ?? string.Empty,
                SuggestedName = SuggestedName ?? string.Empty,
                ObjectType = ObjectType ?? string.Empty,
                Specification = Specification ?? string.Empty,
                Material = Material ?? string.Empty,
                ConstructionType = ConstructionType ?? string.Empty,
                NodeType = NodeType ?? string.Empty,
                StructureType = StructureType ?? string.Empty,
                Purpose = Purpose ?? string.Empty,
                RecognitionStatus = RecognitionStatus ?? string.Empty,
                RecognitionConfidence = RecognitionConfidence,
                RecognitionSource = RecognitionSource ?? string.Empty
            };
        }

        public static List<string> ParseTags(string text)
        {
            var result = new List<string>();
            var set = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return result;

            string normalized = text.Replace('，', ',').Replace('、', ',').Replace(';', ',').Replace('；', ',').Replace('|', ',').Replace('/', ',');
            string[] parts = normalized.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in parts)
            {
                string tag = raw == null ? string.Empty : raw.Trim();
                if (tag.Length == 0) continue;
                if (set.Contains(tag)) continue;
                set.Add(tag);
                result.Add(tag);
            }
            return result;
        }

        public static LayerMetadata FromText(string parentGroup, string parentClass, string tagText)
        {
            return new LayerMetadata
            {
                ParentGroup = parentGroup == null ? string.Empty : parentGroup.Trim(),
                ParentClass = parentClass == null ? string.Empty : parentClass.Trim(),
                Tags = ParseTags(tagText)
            };
        }
    }



    public sealed class LayerRecognitionRuleSet
    {
        public List<LayerRecognitionRule> Rules { get; set; }

        public LayerRecognitionRuleSet()
        {
            Rules = new List<LayerRecognitionRule>();
        }
    }

    public sealed class LayerRecognitionRule
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public int Priority { get; set; }
        public string Scope { get; set; }
        public string MatchMode { get; set; }
        public string Pattern { get; set; }
        public string ExcludePattern { get; set; }
        public string ParentGroup { get; set; }
        public string ParentClass { get; set; }
        public string TagText { get; set; }
        public string MergeMode { get; set; }
        public string ApplicableObjectTypes { get; set; }
        public string Source { get; set; }
        public double ConfidenceBase { get; set; }
        public bool StopAfterMatch { get; set; }

        public LayerRecognitionRule()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = string.Empty;
            Enabled = true;
            Priority = 100;
            Scope = "图层名";
            MatchMode = "通配符";
            Pattern = string.Empty;
            ExcludePattern = string.Empty;
            ParentGroup = string.Empty;
            ParentClass = string.Empty;
            TagText = string.Empty;
            MergeMode = "覆盖空值";
            ApplicableObjectTypes = string.Empty;
            Source = "用户规则";
            ConfidenceBase = 0.78;
            StopAfterMatch = true;
        }

        public LayerRecognitionRule Clone()
        {
            return new LayerRecognitionRule
            {
                Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id,
                Name = Name ?? string.Empty,
                Enabled = Enabled,
                Priority = Priority,
                Scope = Scope ?? string.Empty,
                MatchMode = MatchMode ?? string.Empty,
                Pattern = Pattern ?? string.Empty,
                ExcludePattern = ExcludePattern ?? string.Empty,
                ParentGroup = ParentGroup ?? string.Empty,
                ParentClass = ParentClass ?? string.Empty,
                TagText = TagText ?? string.Empty,
                MergeMode = MergeMode ?? string.Empty,
                ApplicableObjectTypes = ApplicableObjectTypes ?? string.Empty,
                Source = Source ?? string.Empty,
                ConfidenceBase = ConfidenceBase,
                StopAfterMatch = StopAfterMatch
            };
        }
    }

    public static class LayerRecognitionStatuses
    {
        public const string Standard = "标准";
        public const string Compatible = "兼容";
        public const string Incomplete = "信息不完整";
        public const string Conflict = "冲突";
        public const string Mixed = "混合";
        public const string Unrecognized = "未识别";
    }

    public sealed class LayerStructuredAttributes
    {
        public string ObjectType { get; set; }
        public string Specification { get; set; }
        public string Material { get; set; }
        public string ConstructionType { get; set; }
        public string NodeType { get; set; }
        public string StructureType { get; set; }
        public string Purpose { get; set; }
        public string StrengthGrade { get; set; }
        public string Thickness { get; set; }
        public string Volume { get; set; }

        public LayerStructuredAttributes()
        {
            ObjectType = string.Empty;
            Specification = string.Empty;
            Material = string.Empty;
            ConstructionType = string.Empty;
            NodeType = string.Empty;
            StructureType = string.Empty;
            Purpose = string.Empty;
            StrengthGrade = string.Empty;
            Thickness = string.Empty;
            Volume = string.Empty;
        }
    }

    public sealed class LayerRecognitionResult
    {
        public string RawName { get; set; }
        public string NormalizedName { get; set; }
        public string SuggestedName { get; set; }
        public string Status { get; set; }
        public double Confidence { get; set; }
        public string Source { get; set; }
        public string Explanation { get; set; }
        public bool NeedsConfirmation { get; set; }
        public LayerMetadata Metadata { get; set; }
        public LayerStructuredAttributes Attributes { get; set; }
        public List<string> Evidence { get; set; }
        public List<string> Conflicts { get; set; }
        public List<string> MissingFields { get; set; }
        public List<string> MatchedRules { get; set; }

        public LayerRecognitionResult()
        {
            RawName = string.Empty;
            NormalizedName = string.Empty;
            SuggestedName = string.Empty;
            Status = LayerRecognitionStatuses.Unrecognized;
            Source = string.Empty;
            Explanation = string.Empty;
            Metadata = new LayerMetadata();
            Attributes = new LayerStructuredAttributes();
            Evidence = new List<string>();
            Conflicts = new List<string>();
            MissingFields = new List<string>();
            MatchedRules = new List<string>();
        }
    }

    public class LayerOperationResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public int SkipCount { get; set; }
        public int ObjectCount { get; set; }
        public string Message { get; set; }

        public LayerOperationResult()
        {
            Message = string.Empty;
        }
    }

    public enum LayerStateAction
    {
        Lock,
        Unlock,
        TurnOn,
        TurnOff,
        Freeze,
        Thaw
    }
}
