namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioSettings
    {
        public string Theme { get; set; } = "light";
        public bool AnimationsEnabled { get; set; } = true;
        public void Normalize()
        {
            string value = (Theme ?? string.Empty).Trim().ToLowerInvariant();
            Theme = value == "fresh" || value == "dark" ? value : "light";
        }
    }
}
