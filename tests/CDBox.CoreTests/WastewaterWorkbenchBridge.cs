extern alias BaseUi;

namespace TCPipeAutoDraw.UI.Studio
{
    // Linked legacy page tests use a small settings DTO. Delegate all appearance
    // rendering to the actual base-assembly workbench, rather than duplicating CSS.
    internal static class WastewaterWorkbench
    {
        public static string Apply(string html, string page, CDBoxStudioSettings settings)
        {
            return BaseUi::TCPipeAutoDraw.UI.Studio.WastewaterWorkbench.Apply(
                html, page, settings.Theme, settings.AnimationsEnabled);
        }
    }
}
