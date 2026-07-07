using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
using WpfStandaloneUpdateApp.Models;

namespace WpfStandaloneUpdateApp.Services
{
    /// <summary>
    /// Wraps Velopack's UpdateManager. GitHub Releases on the given repo acts as the
    /// centralized "latest version" source - Velopack reads the release tags/assets there
    /// automatically, so you never have to host or maintain a manifest file yourself.
    ///
    /// NOTE: Velopack's public API has moved fast across versions. If any member here
    /// doesn't compile against the Velopack version you installed, check the current
    /// reference at https://docs.velopack.io/reference/cs/Velopack/UpdateManager - the
    /// shape (check -> download -> apply-on-exit) is stable even if exact member names
    /// have shifted slightly.
    /// </summary>
    public class VelopackUpdateService
    {
        private readonly UpdateManager _updateManager;

        /// <param name="githubRepoUrl">e.g. "https://github.com/yourname/yourrepo"</param>
        public VelopackUpdateService(string githubRepoUrl)
        {
            var source = new GithubSource(githubRepoUrl, accessToken: null, prerelease: false);
            _updateManager = new UpdateManager(source);
        }

        /// <summary>
        /// Debug-only entry point: point this at a local folder (produced by `vpk pack`)
        /// instead of GitHub, so you can exercise the full check/download/apply flow from
        /// Visual Studio without publishing anything. See README "Debugging locally".
        /// </summary>
        public VelopackUpdateService(IUpdateSource source)
        {
            _updateManager = new UpdateManager(source);
        }

        public string GetCurrentVersionString()
        {
            // Returns null if the app is running outside of a Velopack-managed install
            // (e.g. launched directly from Visual Studio via F5 instead of via the
            // installed shortcut) - there's no "current version" to report in that case.
            return _updateManager.CurrentVersion?.ToString() ?? "Dev build (not installed via Velopack)";
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            var result = new UpdateCheckResult { CurrentVersion = GetCurrentVersionString() };

            if (!_updateManager.IsInstalled)
            {
                // Nothing to check - we're not running from an installed copy.
                result.Severity = UpdateSeverity.None;
                return result;
            }

            var updateInfo = await _updateManager.CheckForUpdatesAsync();
            if (updateInfo is null)
            {
                result.Severity = UpdateSeverity.None;
                return result;
            }

            var current = _updateManager.CurrentVersion;
            var target = updateInfo.TargetFullRelease.Version;

            result.IsUpdateAvailable = true;
            result.AvailableVersion = target.ToString();
            result.UpdateInfo = updateInfo;
            result.Severity = ClassifySeverity(current, target);
            return result;
        }

        private static UpdateSeverity ClassifySeverity(SemanticVersion? current, SemanticVersion target)
        {
            if (current is null)
                return UpdateSeverity.Minor; // be conservative if we can't compare

            if (target.Major > current.Major)
                return UpdateSeverity.Major;

            if (target.Minor > current.Minor || target.Patch > current.Patch)
                return UpdateSeverity.Minor;

            return UpdateSeverity.None;
        }

        public Task DownloadUpdatesAsync(UpdateInfo updateInfo, Action<int>? progress = null)
        {
            return _updateManager.DownloadUpdatesAsync(updateInfo, progress);
        }

        /// <summary>
        /// Minor/patch path: stages the already-downloaded update and applies it the next
        /// time the app exits on its own - no forced restart, nothing visible to the user
        /// right now.
        /// </summary>
        public void ApplyOnNextExit(UpdateInfo updateInfo)
        {
            _updateManager.WaitExitThenApplyUpdates(updateInfo, silent: true, restart: true);
        }

        /// <summary>
        /// Major path: call this right after the download completes, once the user has
        /// clicked "Update Now". It tells Velopack's updater to wait for this process to
        /// exit, apply the update, and relaunch - so you should call
        /// Application.Current.Shutdown() immediately after this.
        /// </summary>
        public void ApplyNowAndRestart(UpdateInfo updateInfo)
        {
            _updateManager.WaitExitThenApplyUpdates(updateInfo, silent: false, restart: true);
        }
    }
}
