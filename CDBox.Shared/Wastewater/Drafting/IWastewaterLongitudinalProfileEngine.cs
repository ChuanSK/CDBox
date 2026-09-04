using System;
using System.Collections.Generic;
using TCPipeAutoDraw.Modules.LongitudinalProfile;

namespace CDBox.Shared.Wastewater.Drafting
{
    public interface IWastewaterLongitudinalProfileEngine
    {
        LongitudinalProfileBuildResult Build(
            IEnumerable<LongitudinalProfilePipeData> pipes,
            IEnumerable<LongitudinalProfileWellData> wells);
        LongitudinalProfileBuildResult BuildBetweenNodes(
            IEnumerable<LongitudinalProfilePipeData> pipes,
            IEnumerable<LongitudinalProfileWellData> wells,
            string startNode, string endNode);
        LongitudinalProfileLayout CalculateLayout(
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings);
        string SettingsPath { get; }
        LongitudinalProfileSettings LoadSettings();
        void SaveSettings(LongitudinalProfileSettings settings);
    }

    public static class WastewaterLongitudinalProfileRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IWastewaterLongitudinalProfileEngine _current;

        public static void Register(IWastewaterLongitudinalProfileEngine engine)
        {
            if (engine == null) throw new ArgumentNullException("engine");
            lock (SyncRoot) _current = engine;
        }

        public static void Unregister(
            IWastewaterLongitudinalProfileEngine engine)
        {
            lock (SyncRoot)
                if (ReferenceEquals(_current, engine)) _current = null;
        }

        public static IWastewaterLongitudinalProfileEngine GetRequired()
        {
            IWastewaterLongitudinalProfileEngine current = Current;
            if (current != null) return current;
            throw new InvalidOperationException(
                "污水纵断面模块尚未安装或初始化。");
        }

        public static IWastewaterLongitudinalProfileEngine Current
        {
            get { lock (SyncRoot) return _current; }
        }

        public static bool IsAvailable
        {
            get { lock (SyncRoot) return _current != null; }
        }
    }
}
