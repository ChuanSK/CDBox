using System;
using System.Collections.Generic;
using System.Linq;

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

        public LayerInfo()
        {
            Name = string.Empty;
            StatusText = string.Empty;
            Linetype = string.Empty;
            ParentGroup = string.Empty;
            ParentClass = string.Empty;
            TagText = string.Empty;
        }
    }

    public sealed class LayerMetadata
    {
        public string ParentGroup { get; set; }
        public string ParentClass { get; set; }
        public List<string> Tags { get; set; }

        public LayerMetadata()
        {
            ParentGroup = string.Empty;
            ParentClass = string.Empty;
            Tags = new List<string>();
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
                    && (Tags == null || Tags.Count == 0);
            }
        }

        public LayerMetadata Clone()
        {
            return new LayerMetadata
            {
                ParentGroup = ParentGroup ?? string.Empty,
                ParentClass = ParentClass ?? string.Empty,
                Tags = Tags == null ? new List<string>() : new List<string>(Tags)
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
        public bool Enabled { get; set; }
        public string MatchMode { get; set; }
        public string Pattern { get; set; }
        public string ParentGroup { get; set; }
        public string ParentClass { get; set; }
        public string TagText { get; set; }
        public bool StopAfterMatch { get; set; }

        public LayerRecognitionRule()
        {
            Enabled = true;
            MatchMode = "通配符";
            Pattern = string.Empty;
            ParentGroup = string.Empty;
            ParentClass = string.Empty;
            TagText = string.Empty;
            StopAfterMatch = true;
        }

        public LayerRecognitionRule Clone()
        {
            return new LayerRecognitionRule
            {
                Enabled = Enabled,
                MatchMode = MatchMode ?? string.Empty,
                Pattern = Pattern ?? string.Empty,
                ParentGroup = ParentGroup ?? string.Empty,
                ParentClass = ParentClass ?? string.Empty,
                TagText = TagText ?? string.Empty,
                StopAfterMatch = StopAfterMatch
            };
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
