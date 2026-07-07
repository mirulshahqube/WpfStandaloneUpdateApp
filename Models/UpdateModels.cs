using Velopack;

namespace WpfStandaloneUpdateApp.Models
{
    public enum UpdateSeverity
    {
        None,

        /// <summary>Minor / patch bump - safe to stage and apply on the next natural app exit.</summary>
        Minor,

        /// <summary>Major version bump - requires the user to update before continuing.</summary>
        Major
    }

    public class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public UpdateSeverity Severity { get; set; }
        public string? CurrentVersion { get; set; }
        public string? AvailableVersion { get; set; }

        /// <summary>The raw Velopack update descriptor, needed to actually download/apply it.</summary>
        public UpdateInfo? UpdateInfo { get; set; }
    }
}
