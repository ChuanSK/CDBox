using System;
using System.Web.Script.Serialization;

namespace TCPipeAutoDraw.UI.Studio
{
    internal sealed class CDBoxStudioUpdateResult
    {
        public bool Success { get; set; }
        public bool HasRelease { get; set; }
        public bool UpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = CDBoxStudioUpdateService.CurrentVersion;
        public string LatestVersion { get; set; } = "";
        public string Channel { get; set; } = "";
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Notes { get; set; } = "";
        public string ReleaseDate { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public DateTime CheckedAt { get; set; } = DateTime.Now;

        public string ToJson()
        {
            return new JavaScriptSerializer().Serialize(new
            {
                success = Success, hasRelease = HasRelease, updateAvailable = UpdateAvailable,
                currentVersion = CurrentVersion, latestVersion = LatestVersion, channel = Channel,
                title = Title, summary = Summary, notes = Notes, releaseDate = ReleaseDate,
                errorMessage = ErrorMessage, checkedAt = CheckedAt.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
    }
}
