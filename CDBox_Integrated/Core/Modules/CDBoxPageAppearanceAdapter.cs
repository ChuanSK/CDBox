using CDBox.Shared.UI;
using TCPipeAutoDraw.UI.Studio;

namespace TCPipeAutoDraw.Core.Modules
{
    internal sealed class CDBoxPageAppearanceAdapter
        : ICDBoxPageAppearanceService
    {
        public CDBoxPageAppearance GetAppearance()
        {
            CDBoxStudioSettings settings = CDBoxStudioSettingsStore.Load();
            settings.Normalize();
            return new CDBoxPageAppearance
            {
                Theme = settings.Theme,
                AnimationsEnabled = settings.AnimationsEnabled,
                LogFilePath = CDBoxStudioLogger.LogFilePath
            };
        }
    }
}
