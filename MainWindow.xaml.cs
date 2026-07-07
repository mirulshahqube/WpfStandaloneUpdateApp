using System;
using System.Threading.Tasks;
using System.Windows;
using Velopack;
using WpfStandaloneUpdateApp.Models;
using WpfStandaloneUpdateApp.Services;

namespace WpfStandaloneUpdateApp
{
    public partial class MainWindow : Window
    {
#if DEBUG
        // While debugging: point at a local folder instead of GitHub, so you can test the
        // full check/download/apply flow without publishing anything.
        //   1. Pack a "new version" locally:
        //      vpk pack -u WpfStandaloneUpdateApp -v 1.1.0 -p .\publish -e WpfStandaloneUpdateApp.exe -o C:\local-updates
        //   2. Point this at that same folder (below).
        // NOTE: this alone is not enough to test the full flow under F5 - Velopack still
        // sees IsInstalled == false unless the app is genuinely running from an installed
        // copy. See README "Debugging locally" for the two real options: (A) install
        // locally via the generated Setup.exe and attach the VS debugger to that running
        // process, which is the most reliable way to test end-to-end; or (B) pair this
        // local source with a Velopack test locator to simulate being "installed" under
        // F5 - check current usage at https://docs.velopack.io/integrating/testing since
        // the exact API here has shifted across versions.
        private readonly VelopackUpdateService _updateService =
            new(new Velopack.Sources.SimpleWebSource("file:///C:/local-updates"));
#else
        // TODO: replace with your actual GitHub repo (must have Releases published via vpk).
        private readonly VelopackUpdateService _updateService =
            new("https://github.com/mirulshahqube/WpfStandaloneUpdateApp");
#endif

        private UpdateCheckResult? _lastResult;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await RunStartupCheckAsync();
        }

        private async Task RunStartupCheckAsync()
        {
            CurrentVersionText.Text = _updateService.GetCurrentVersionString();
            await CheckForUpdatesAsync(isManual: false);
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync(isManual: true);
        }

        private async Task CheckForUpdatesAsync(bool isManual)
        {
            CheckUpdateButton.IsEnabled = false;
            StatusText.Text = "Checking for updates...";

            try
            {
                var result = await _updateService.CheckForUpdatesAsync();
                _lastResult = result;
                CurrentVersionText.Text = result.CurrentVersion;

                switch (result.Severity)
                {
                    case UpdateSeverity.None:
                        StatusText.Text = isManual ? "You're on the latest version." : "Up to date.";
                        break;

                    case UpdateSeverity.Minor:
                        StatusText.Text = $"Preparing update {result.AvailableVersion} in the background...";
                        _ = InstallMinorUpdateInBackgroundAsync(result);
                        break;

                    case UpdateSeverity.Major:
                        StatusText.Text = $"Major update {result.AvailableVersion} available.";
                        ShowMajorUpdateOverlay(result);
                        break;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Update check failed: {ex.Message}";
            }
            finally
            {
                CheckUpdateButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Minor/patch path: downloads silently, then stages the update to apply on the
        /// next natural app exit - never interrupts the current session.
        /// </summary>
        private async Task InstallMinorUpdateInBackgroundAsync(UpdateCheckResult result)
        {
            if (result.UpdateInfo is null) return;

            Dispatcher.Invoke(() => BackgroundUpdateProgress.Visibility = Visibility.Visible);
            try
            {
                await _updateService.DownloadUpdatesAsync(result.UpdateInfo);
                _updateService.ApplyOnNextExit(result.UpdateInfo);

                Dispatcher.Invoke(() =>
                {
                    BackgroundUpdateProgress.Visibility = Visibility.Collapsed;
                    StatusText.Text = $"Update {result.AvailableVersion} ready - it'll finish installing next time you close the app.";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    BackgroundUpdateProgress.Visibility = Visibility.Collapsed;
                    StatusText.Text = $"Background update failed: {ex.Message}";
                });
            }
        }

        private void ShowMajorUpdateOverlay(UpdateCheckResult result)
        {
            MajorUpdateDetailsText.Text =
                $"Version {result.AvailableVersion} is required (you have {result.CurrentVersion}). " +
                "The app is locked until you update.";
            MajorUpdateProgress.Visibility = Visibility.Collapsed;
            MajorUpdateStatusText.Text = string.Empty;
            MajorUpdateButton.IsEnabled = true;

            MainContent.IsEnabled = false;
            MajorUpdateOverlay.Visibility = Visibility.Visible;
        }

        private async void MajorUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult?.UpdateInfo is null) return;

            MajorUpdateButton.IsEnabled = false;
            MajorUpdateProgress.Visibility = Visibility.Visible;
            MajorUpdateStatusText.Text = "Downloading update...";

            try
            {
                await _updateService.DownloadUpdatesAsync(_lastResult.UpdateInfo, pct =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        MajorUpdateProgress.Value = pct;
                        MajorUpdateStatusText.Text = $"{pct}% downloaded...";
                    });
                });

                MajorUpdateStatusText.Text = "Update downloaded. Restarting app...";
                _updateService.ApplyNowAndRestart(_lastResult.UpdateInfo);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MajorUpdateStatusText.Text = $"Update failed: {ex.Message}";
                MajorUpdateButton.IsEnabled = true;
            }
        }

        private void DoWorkButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Did some work at " + DateTime.Now.ToLongTimeString();
        }
    }
}
