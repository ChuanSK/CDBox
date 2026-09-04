using System.Collections.Generic;
using CDBox.Shared.Wastewater.Drafting;
using TCPipeAutoDraw.Modules.LongitudinalProfile;

namespace CDBox.Wastewater.Features.LongitudinalProfile
{
    public sealed class WastewaterLongitudinalProfileEngine
        : IWastewaterLongitudinalProfileEngine
    {
        public string SettingsPath
        {
            get
            {
                return WastewaterLongitudinalProfileSettingsStore
                    .SettingsPath;
            }
        }

        public LongitudinalProfileSettings LoadSettings()
        {
            return WastewaterLongitudinalProfileSettingsStore.Load();
        }

        public void SaveSettings(LongitudinalProfileSettings settings)
        {
            WastewaterLongitudinalProfileSettingsStore.Save(settings);
        }

        public LongitudinalProfileBuildResult Build(
            IEnumerable<LongitudinalProfilePipeData> pipes,
            IEnumerable<LongitudinalProfileWellData> wells)
        {
            return WastewaterLongitudinalProfileCalculator.Build(pipes,
                wells);
        }

        public LongitudinalProfileBuildResult BuildBetweenNodes(
            IEnumerable<LongitudinalProfilePipeData> pipes,
            IEnumerable<LongitudinalProfileWellData> wells,
            string startNode, string endNode)
        {
            return WastewaterLongitudinalProfileCalculator.BuildBetweenNodes(
                pipes, wells, startNode, endNode);
        }

        public LongitudinalProfileLayout CalculateLayout(
            LongitudinalProfileData profile,
            LongitudinalProfileSettings settings)
        {
            return WastewaterLongitudinalProfileLayoutCalculator.Calculate(
                profile, settings);
        }
    }
}
