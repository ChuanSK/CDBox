using System.Collections.Generic;
using CDBox.Shared.Wastewater.Drafting;
using TCPipeAutoDraw.Modules.NodeAnnotation;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;

namespace CDBox.Wastewater.Features.Annotation
{
    public sealed class WastewaterAnnotationEngine
        : IWastewaterAnnotationEngine
    {
        public PipeLengthAnnotationOptions LoadPipeOptions()
        {
            return PipeLengthAnnotationSettingsStore.Load();
        }

        public void SavePipeOptions(PipeLengthAnnotationOptions options,
            bool strict)
        {
            if (strict) PipeLengthAnnotationSettingsStore.SaveStrict(options);
            else PipeLengthAnnotationSettingsStore.Save(options);
        }

        public NodeAnnotationOptions LoadNodeOptions()
        {
            return NodeAnnotationSettingsStore.Load();
        }

        public void SaveNodeOptions(NodeAnnotationOptions options,
            bool strict)
        {
            if (strict) NodeAnnotationSettingsStore.SaveStrict(options);
            else NodeAnnotationSettingsStore.Save(options);
        }

        public string ComposePipeText(string userText,
            string systemLengthText)
        {
            return PipeLengthAnnotationTextComposer.Compose(userText,
                systemLengthText);
        }

        public void SplitLegacyPipeText(string fullText, double sourceLength,
            out string userText, out string systemLengthText)
        {
            PipeLengthAnnotationTextComposer.SplitLegacyText(fullText,
                sourceLength, out userText, out systemLengthText);
        }

        public string FormatPipeLengthLike(double length,
            string previousToken)
        {
            return PipeLengthAnnotationTextComposer.FormatLike(length,
                previousToken);
        }

        public string FormatPipeLength(double length, int decimals,
            string previousToken)
        {
            return PipeLengthAnnotationTextComposer.FormatWithDecimals(
                length, decimals, previousToken);
        }

        public void SplitLastPipeLengthToken(string fullText,
            out string userText, out string systemLengthText)
        {
            PipeLengthAnnotationTextComposer.SplitLastLengthToken(fullText,
                out userText, out systemLengthText);
        }

        public string RemoveDetachedPipeLengthToken(string frozenText,
            string oldToken)
        {
            return PipeLengthAnnotationTextComposer.RemoveDetachedLengthToken(
                frozenText, oldToken);
        }

        public string ReplaceDerivedPipeLengthToken(string text,
            string oldToken, string newToken)
        {
            return PipeLengthAnnotationTextComposer.ReplaceDerivedLengthToken(
                text, oldToken, newToken);
        }

        public List<string> SplitPipeBottomLines(string text)
        {
            return PipeLengthAnnotationTextComposer.SplitBottomLines(text);
        }

        public Dictionary<string, string> ComposeNodeText(string nodeNo,
            double wellDepth, double shaftLength, bool isSiltWell)
        {
            return NodeAnnotationTextComposer.Compose(nodeNo, wellDepth,
                shaftLength, isSiltWell);
        }
    }
}
