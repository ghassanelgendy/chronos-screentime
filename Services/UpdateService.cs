using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using System.Linq;
using System.Threading;
using chronos_screentime.Windows;

namespace chronos_screentime.Services
{
    public class UpdateInfo
    {
        public string Version { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public string InstallerUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    public class UpdateService
    {
        private const string CurrentVersion = "1.1.1";
        private const string UpdateCheckUrl = "https://api.github.com/repos/ghassanelgendy/chronos-screentime/releases/latest";
        private const string RepositoryUrl = "https://github.com/ghassanelgendy/chronos-screentime";
        private static readonly HttpClient _httpClient = new HttpClient();
        private static DateTime _lastUpdateCheck = DateTime.MinValue;
        private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);
        private static DateTime _appStartupTime = DateTime.Now;
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10); // 10 second delay after startup

        // Add static fields to hold retry timers
        private static System.Threading.Timer? _retryTimer = null;
        private static System.Threading.Timer? _retrySilentTimer = null;
        private static MainWindow? _mainWindow = null;

        static UpdateService()
        {
            // Set up GitHub API headers
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Chronos-Screen-Time-Tracker");
        }

        /// <summary>
        /// Sets the main window reference for showing update dialogs
        /// </summary>
        public static void SetMainWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public static async Task CheckForUpdatesAsync()
        {
            try
            {
                var updateInfo = await GetLatestVersionAsync();
                
                if (updateInfo != null && IsNewerVersion(updateInfo.Version))
                {
                    // Check if updates should be suppressed based on user's previous dismissals
                    if (ShouldSuppressUpdateDialog())
                    {
                        System.Diagnostics.Debug.WriteLine("Update check skipped due to user suppression");
                        return;
                    }

                    // Check if enough time has passed since app startup
                    if (DateTime.Now - _appStartupTime < StartupDelay)
                    {
                        System.Diagnostics.Debug.WriteLine($"Update found but delaying dialog due to startup delay. App started {DateTime.Now - _appStartupTime} ago, need {StartupDelay}");
                        
                        // Schedule a retry after the startup delay
                        var remainingDelay = StartupDelay - (DateTime.Now - _appStartupTime);
                        _retryTimer?.Dispose();
                        _retryTimer = new System.Threading.Timer(async _ =>
                        {
                            await Application.Current.Dispatcher.InvokeAsync(async () =>
                            {
                                try
                                {
                                    await CheckForUpdatesAsync();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Delayed update check failed: {ex.Message}");
                                }
                                finally
                                {
                                    _retryTimer?.Dispose();
                                    _retryTimer = null;
                                }
                            });
                        }, null, (int)remainingDelay.TotalMilliseconds, Timeout.Infinite);
                        
                        return;
                    }

                    bool shouldUpdate = false;
                    
                    // Use the new update dialog if main window is available
                    if (_mainWindow != null)
                    {
                        shouldUpdate = await _mainWindow.ShowUpdateDialogAsync(updateInfo);
                    }
                    else
                    {
                        // Fallback to MessageBox if main window is not available
                        var result = MessageBox.Show(
                            $"A new version ({updateInfo.Version}) is available!\n\n" +
                            $"Release Notes:\n{updateInfo.ReleaseNotes}\n\n" +
                            $"Would you like to download and install the update now?",
                            "Update Available",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);
                        
                        shouldUpdate = result == MessageBoxResult.Yes;
                    }

                    if (shouldUpdate)
                    {
                        await DownloadAndInstallUpdateAsync(updateInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Update check failed: {ex.Message}\n\nPlease check your internet connection.",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        public static async Task CheckForUpdatesSilentlyAsync()
        {
            // Only check once per day
            if (DateTime.Now - _lastUpdateCheck < UpdateCheckInterval)
                return;

            _lastUpdateCheck = DateTime.Now;

            try
            {
                var updateInfo = await GetLatestVersionAsync();
                
                if (updateInfo != null && IsNewerVersion(updateInfo.Version))
                {
                    // Check if updates should be suppressed based on user's previous dismissals
                    if (ShouldSuppressUpdateDialog())
                    {
                        System.Diagnostics.Debug.WriteLine("Silent update check skipped due to user suppression");
                        return;
                    }

                    // Check if enough time has passed since app startup
                    if (DateTime.Now - _appStartupTime < StartupDelay)
                    {
                        System.Diagnostics.Debug.WriteLine($"Update found but delaying dialog due to startup delay. App started {DateTime.Now - _appStartupTime} ago, need {StartupDelay}");
                        
                        // Schedule a retry after the startup delay
                        var remainingDelay = StartupDelay - (DateTime.Now - _appStartupTime);
                        _retrySilentTimer?.Dispose();
                        _retrySilentTimer = new System.Threading.Timer(async _ =>
                        {
                            await Application.Current.Dispatcher.InvokeAsync(async () =>
                            {
                                try
                                {
                                    await CheckForUpdatesSilentlyAsync();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Delayed silent update check failed: {ex.Message}");
                                }
                                finally
                                {
                                    _retrySilentTimer?.Dispose();
                                    _retrySilentTimer = null;
                                }
                            });
                        }, null, (int)remainingDelay.TotalMilliseconds, Timeout.Infinite);
                        
                        return;
                    }

                    bool shouldUpdate = false;
                    
                    // Use the new update dialog if main window is available
                    if (_mainWindow != null)
                    {
                        shouldUpdate = await _mainWindow.ShowUpdateDialogAsync(updateInfo);
                    }
                    else
                    {
                        // Fallback to MessageBox if main window is not available
                        var result = MessageBox.Show(
                            $"A new version ({updateInfo.Version}) is available!\n\n" +
                            $"Release Notes:\n{updateInfo.ReleaseNotes}\n\n" +
                            $"Would you like to download and install the update now?",
                            "Update Available",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);
                        
                        shouldUpdate = result == MessageBoxResult.Yes;
                    }

                    if (shouldUpdate)
                    {
                        await DownloadAndInstallUpdateAsync(updateInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Silent update check failed: {ex.Message}");
            }
        }

        private static async Task<UpdateInfo?> GetLatestVersionAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(UpdateCheckUrl);
                var releaseData = JsonConvert.DeserializeObject<dynamic>(response);
                
                if (releaseData != null)
                {
                    var updateInfo = new UpdateInfo
                    {
                        Version = releaseData.tag_name?.ToString() ?? string.Empty,
                        DownloadUrl = releaseData.html_url?.ToString() ?? string.Empty,
                        ReleaseNotes = releaseData.body?.ToString() ?? string.Empty,
                        ReleaseDate = DateTime.Parse(releaseData.published_at?.ToString() ?? DateTime.Now.ToString())
                    };

                    // Find the installer download URL
                    if (releaseData.assets != null)
                    {
                        foreach (var asset in releaseData.assets)
                        {
                            var assetName = asset.name?.ToString() ?? "";
                            if (assetName.Contains("Setup.exe") || assetName.Contains("Installer.exe"))
                            {
                                updateInfo.InstallerUrl = asset.browser_download_url?.ToString() ?? "";
                                updateInfo.FileSize = asset.size ?? 0;
                                break;
                            }
                        }
                    }

                    return updateInfo;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting latest version: {ex.Message}");
            }
            
            return null;
        }

        private static bool IsNewerVersion(string newVersion)
        {
            try
            {
                // Remove 'v' prefix if present
                newVersion = newVersion.TrimStart('v');
                var currentVersion = CurrentVersion.TrimStart('v');
                
                var newVersionParts = newVersion.Split('.');
                var currentVersionParts = currentVersion.Split('.');
                
                // Compare version parts
                for (int i = 0; i < Math.Max(newVersionParts.Length, currentVersionParts.Length); i++)
                {
                    var newPart = i < newVersionParts.Length ? int.Parse(newVersionParts[i]) : 0;
                    var currentPart = i < currentVersionParts.Length ? int.Parse(currentVersionParts[i]) : 0;
                    
                    if (newPart > currentPart) return true;
                    if (newPart < currentPart) return false;
                }
                
                return false; // Same version
            }
            catch
            {
                return false; // If version parsing fails, assume no update
            }
        }

        private static async Task DownloadAndInstallUpdateAsync(UpdateInfo updateInfo)
        {
            try
            {
                if (!string.IsNullOrEmpty(updateInfo.InstallerUrl))
                {
                    // Direct download and install
                    await DownloadAndRunInstallerAsync(updateInfo);
                }
                else
                {
                    // Fallback to opening browser
                    OpenDownloadPage(updateInfo);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error starting update: {ex.Message}\n\n" +
                    $"Please download manually from: {updateInfo.DownloadUrl}",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static async Task DownloadAndRunInstallerAsync(UpdateInfo updateInfo)
        {
            try
            {
                var tempPath = Path.GetTempPath();
                var installerPath = Path.Combine(tempPath, $"ChronosScreenTimeTracker-{updateInfo.Version}-Setup.exe");

                // Show download progress
                var progressWindow = new UpdateProgressWindow(updateInfo);
                progressWindow.Show();

                // Download the installer
                using (var response = await _httpClient.GetAsync(updateInfo.InstallerUrl))
                using (var fileStream = File.Create(installerPath))
                {
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var buffer = new byte[8192];
                    var totalBytesRead = 0L;

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        int bytesRead;
                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                var progress = (double)totalBytesRead / totalBytes;
                                progressWindow.UpdateProgress(progress);
                            }
                        }
                    }
                }

                progressWindow.Close();

                // Run the installer
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(psi);

                // Close the current application
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download installer: {ex.Message}");
            }
        }

        private static void OpenDownloadPage(UpdateInfo updateInfo)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = updateInfo.DownloadUrl,
                    UseShellExecute = true
                };
                
                System.Diagnostics.Process.Start(psi);

                if (_mainWindow != null)
                {
                    // Show modern modal dialog
                    _mainWindow.Dispatcher.Invoke(async () =>
                    {
                        await _mainWindow.ShowDownloadStartedDialogAsync(updateInfo.Version, updateInfo.DownloadUrl);
                    });
                }
                else
                {
                    // Fallback to MessageBox
                    MessageBox.Show(
                        $"The download page has been opened in your browser.\n\n" +
                        $"Please download and install version {updateInfo.Version} manually.\n\n" +
                        $"After installation, restart the application.",
                        "Download Started",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error opening download page: {ex.Message}\n\n" +
                    $"Please visit: {updateInfo.DownloadUrl}",
                    "Download Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public static string GetCurrentVersion()
        {
            return CurrentVersion;
        }

        /// <summary>
        /// Resets the app startup time to now. Useful for testing or when the app is restarted.
        /// </summary>
        public static void ResetStartupTime()
        {
            _appStartupTime = DateTime.Now;
            System.Diagnostics.Debug.WriteLine($"UpdateService: Startup time reset to {_appStartupTime}");
        }

        /// <summary>
        /// Checks if enough time has passed since app startup to show update dialogs.
        /// </summary>
        /// <returns>True if the startup delay has passed, false otherwise.</returns>
        public static bool IsStartupDelayPassed()
        {
            return DateTime.Now - _appStartupTime >= StartupDelay;
        }

        /// <summary>
        /// Gets the remaining time until update dialogs can be shown.
        /// </summary>
        /// <returns>TimeSpan representing the remaining delay time.</returns>
        public static TimeSpan GetRemainingStartupDelay()
        {
            var elapsed = DateTime.Now - _appStartupTime;
            return elapsed >= StartupDelay ? TimeSpan.Zero : StartupDelay - elapsed;
        }

        /// <summary>
        /// Checks if update dialogs should be suppressed based on user's previous dismissals
        /// </summary>
        /// <returns>True if updates should be suppressed, false otherwise</returns>
        private static bool ShouldSuppressUpdateDialog()
        {
            try
            {
                // Get current settings
                var settingsService = new SettingsService();
                var settings = settingsService.CurrentSettings;

                var now = DateTime.Now;

                // Check if user dismissed an update recently
                if (settings.LastUpdateDismissedDate.HasValue)
                {
                    var daysSinceDismissed = (now - settings.LastUpdateDismissedDate.Value).TotalDays;
                    if (daysSinceDismissed < settings.AutoUpdateSuppressionDays)
                    {
                        System.Diagnostics.Debug.WriteLine($"Update suppressed: User dismissed update {daysSinceDismissed:F1} days ago, suppression period is {settings.AutoUpdateSuppressionDays} days");
                        return true;
                    }
                }

                // Check if user cancelled an update recently
                if (settings.LastUpdateCancelledDate.HasValue)
                {
                    var daysSinceCancelled = (now - settings.LastUpdateCancelledDate.Value).TotalDays;
                    if (daysSinceCancelled < settings.CancelUpdateSuppressionDays)
                    {
                        System.Diagnostics.Debug.WriteLine($"Update suppressed: User cancelled update {daysSinceCancelled:F1} days ago, suppression period is {settings.CancelUpdateSuppressionDays} days");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking update suppression: {ex.Message}");
                return false; // Don't suppress if there's an error
            }
        }

        /// <summary>
        /// Records that the user dismissed an update (clicked "Later")
        /// </summary>
        public static void RecordUpdateDismissed()
        {
            try
            {
                var settingsService = new SettingsService();
                settingsService.UpdateSettings(s => s.LastUpdateDismissedDate = DateTime.Now);
                System.Diagnostics.Debug.WriteLine("Update dismissed - suppression period started");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error recording update dismissed: {ex.Message}");
            }
        }

        /// <summary>
        /// Records that the user cancelled an update (clicked "Cancel" or closed dialog)
        /// </summary>
        public static void RecordUpdateCancelled()
        {
            try
            {
                var settingsService = new SettingsService();
                settingsService.UpdateSettings(s => s.LastUpdateCancelledDate = DateTime.Now);
                System.Diagnostics.Debug.WriteLine("Update cancelled - longer suppression period started");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error recording update cancelled: {ex.Message}");
            }
        }

        /// <summary>
        /// Manual update check that bypasses suppression logic - always shows dialog if update is available
        /// </summary>
        public static async Task CheckForUpdatesManuallyAsync()
        {
            try
            {
                var updateInfo = await GetLatestVersionAsync();
                
                if (updateInfo != null && IsNewerVersion(updateInfo.Version))
                {
                    // Check if enough time has passed since app startup
                    if (DateTime.Now - _appStartupTime < StartupDelay)
                    {
                        System.Diagnostics.Debug.WriteLine($"Manual update found but delaying dialog due to startup delay. App started {DateTime.Now - _appStartupTime} ago, need {StartupDelay}");
                        
                        // Schedule a retry after the startup delay
                        var remainingDelay = StartupDelay - (DateTime.Now - _appStartupTime);
                        _retryTimer?.Dispose();
                        _retryTimer = new System.Threading.Timer(async _ =>
                        {
                            await Application.Current.Dispatcher.InvokeAsync(async () =>
                            {
                                try
                                {
                                    await CheckForUpdatesManuallyAsync();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Delayed manual update check failed: {ex.Message}");
                                }
                                finally
                                {
                                    _retryTimer?.Dispose();
                                    _retryTimer = null;
                                }
                            });
                        }, null, (int)remainingDelay.TotalMilliseconds, Timeout.Infinite);
                        
                        return;
                    }

                    bool shouldUpdate = false;
                    
                    // Use the new update dialog if main window is available
                    if (_mainWindow != null)
                    {
                        shouldUpdate = await _mainWindow.ShowUpdateDialogAsync(updateInfo);
                    }
                    else
                    {
                        // Fallback to MessageBox if main window is not available
                        var result = MessageBox.Show(
                            $"A new version ({updateInfo.Version}) is available!\n\n" +
                            $"Release Notes:\n{updateInfo.ReleaseNotes}\n\n" +
                            $"Would you like to download and install the update now?",
                            "Update Available",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);
                        
                        shouldUpdate = result == MessageBoxResult.Yes;
                    }

                    if (shouldUpdate)
                    {
                        await DownloadAndInstallUpdateAsync(updateInfo);
                    }
                }
                else
                {
                    // No update available - show info dialog
                    if (_mainWindow != null)
                    {
                        await _mainWindow.Dispatcher.InvokeAsync(async () =>
                        {
                            await _mainWindow.ShowInfoDialogAsync("No Updates Available", "You are already running the latest version of Chronos Screen Time Tracker.");
                        });
                    }
                    else
                    {
                        MessageBox.Show("You are already running the latest version of Chronos Screen Time Tracker.", "No Updates Available", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Update check failed: {ex.Message}\n\nPlease check your internet connection.",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
} 