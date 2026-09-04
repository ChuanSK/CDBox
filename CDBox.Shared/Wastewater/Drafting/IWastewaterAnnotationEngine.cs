using System;
using System.Collections.Generic;
using TCPipeAutoDraw.Modules.NodeAnnotation;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;

namespace CDBox.Shared.Wastewater.Drafting
{
    public interface IWastewaterAnnotationEngine
    {
        PipeLengthAnnotationOptions LoadPipeOptions();
        void SavePipeOptions(PipeLengthAnnotationOptions options,
            bool strict);
        NodeAnnotationOptions LoadNodeOptions();
        void SaveNodeOptions(NodeAnnotationOptions options, bool strict);

        string ComposePipeText(string userText, string systemLengthText);
        void SplitLegacyPipeText(string fullText, double sourceLength,
            out string userText, out string systemLengthText);
        string FormatPipeLengthLike(double length, string previousToken);
        string FormatPipeLength(double length, int decimals,
            string previousToken);
        void SplitLastPipeLengthToken(string fullText, out string userText,
            out string systemLengthText);
        string RemoveDetachedPipeLengthToken(string frozenText,
            string oldToken);
        string ReplaceDerivedPipeLengthToken(string text, string oldToken,
            string newToken);
        List<string> SplitPipeBottomLines(string text);
        Dictionary<string, string> ComposeNodeText(string nodeNo,
            double wellDepth, double shaftLength, bool isSiltWell);
    }

    public static class WastewaterAnnotationRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IWastewaterAnnotationEngine _current;

        public static void Register(IWastewaterAnnotationEngine engine)
        {
            if (engine == null) throw new ArgumentNullException("engine");
            lock (SyncRoot) _current = engine;
        }

        public static void Unregister(IWastewaterAnnotationEngine engine)
        {
            lock (SyncRoot)
                if (ReferenceEquals(_current, engine)) _current = null;
        }

        public static IWastewaterAnnotationEngine Current
        {
            get { lock (SyncRoot) return _current; }
        }

        public static IWastewaterAnnotationEngine GetRequired()
        {
            IWastewaterAnnotationEngine current = Current;
            if (current != null) return current;
            throw new InvalidOperationException(
                "污水标注模块尚未安装或初始化。");
        }

        public static bool IsAvailable
        {
            get { lock (SyncRoot) return _current != null; }
        }
    }
}
