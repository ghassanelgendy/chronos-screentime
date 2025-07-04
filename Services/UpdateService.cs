using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using System.Linq;
using System.IO.Compression;
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
        private const string CurrentVersion = "1.1.7";
        private const string UpdateCheckUrl = "https://api.github.com/repos/ghassanelgendy/chronos-screentime/releases/latest";
        private const string RepositoryUrl = "https://github.com/ghassanelgendy/chronos-screentime";
        private static readonly HttpClient _httpClient = new HttpClient();
        private static DateTime _lastUpdateCheck = DateTime.MinValue;
        private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);

        static UpdateService()
        {
            // Set up GitHub API headers
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Chronos-Screen-Time-Tracker");
        }

        public static async Task CheckForUpdatesAsync()
        {
            try
            {
                var updateInfo = await GetLatestVersionAsync();
                
                if (updateInfo != null && IsNewerVersion(updateInfo.Version))
                {
                    var result = MessageBox.Show(
                        $"A new version ({updateInfo.Version}) is available!\n\n" +
                        $"Release Notes:\n{updateInfo.ReleaseNotes}\n\n" +
                        $"Would you like to download and install the update now?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
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
                    // Show notification that update is available
                    var result = MessageBox.Show(
                        $"A new version ({updateInfo.Version}) is available!\n\n" +
                        $"Release Notes:\n{updateInfo.ReleaseNotes}\n\n" +
                        $"Would you like to download and install the update now?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
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

                    // Find the installer download URL (prefer ZIP, fallback to installer)
                    if (releaseData.assets != null)
                    {
                        string? zipUrl = null;
                        long zipSize = 0;
                        
                        foreach (var asset in releaseData.assets)
                        {
                            var assetName = asset.name?.ToString() ?? "";
                            var downloadUrl = asset.browser_download_url?.ToString() ?? "";
                            
                            // Prefer ZIP files for incremental updates
                            if (assetName.EndsWith(".zip") && assetName.Contains("ChronosScreenTimeTracker"))
                            {
                                zipUrl = downloadUrl;
                                zipSize = asset.size ?? 0;
                            }
                            // Fallback to installer
                            else if ((assetName.Contains("Setup.exe") || assetName.Contains("Installer.exe")) && string.IsNullOrEmpty(updateInfo.InstallerUrl))
                            {
                                updateInfo.InstallerUrl = downloadUrl;
                                updateInfo.FileSize = asset.size ?? 0;
                            }
                        }
                        
                        // If we found a ZIP, use it for incremental updates
                        if (!string.IsNullOrEmpty(zipUrl))
                        {
                            updateInfo.InstallerUrl = zipUrl;
                            updateInfo.FileSize = zipSize;
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
                    // Check if it's a ZIP file for incremental updates
                    if (updateInfo.InstallerUrl.EndsWith(".zip"))
                    {
                        // Use incremental update service for ZIP files
                        await DownloadAndExtractZipAsync(updateInfo);
                    }
                    else
                    {
                        // Direct download and install for installer files
                        await DownloadAndRunInstallerAsync(updateInfo);
                    }
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

        private static async Task DownloadAndExtractZipAsync(UpdateInfo updateInfo)
        {
            try
            {
                var tempPath = Path.GetTempPath();
                var zipPath = Path.Combine(tempPath, $"ChronosScreenTimeTracker-{updateInfo.Version}.zip");
                var extractPath = Path.Combine(tempPath, $"ChronosUpdate_{Guid.NewGuid()}");

                // Show download progress
                var progressWindow = new UpdateProgressWindow(updateInfo);
                progressWindow.Show();

                // Download the ZIP file
                using (var response = await _httpClient.GetAsync(updateInfo.InstallerUrl))
                using (var fileStream = File.Create(zipPath))
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

                // Extract ZIP to temporary directory
                Directory.CreateDirectory(extractPath);
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath);

                // Copy files to application directory
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var updatedCount = 0;

                foreach (var file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(extractPath, file);
                    var targetPath = Path.Combine(appDir, relativePath);
                    var targetDir = Path.GetDirectoryName(targetPath);

                    if (!string.IsNullOrEmpty(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    File.Copy(file, targetPath, true);
                    updatedCount++;
                }

                // Clean up temporary files
                try
                {
                    File.Delete(zipPath);
                    Directory.Delete(extractPath, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to clean up temporary files: {ex.Message}");
                }

                MessageBox.Show(
                    $"Update complete! {updatedCount} file(s) updated from ZIP.\n\n" +
                    $"Please restart the application to apply changes.",
                    "Update Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Close the current application
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download and extract ZIP: {ex.Message}");
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
                
                MessageBox.Show(
                    $"The download page has been opened in your browser.\n\n" +
                    $"Please download and install version {updateInfo.Version} manually.\n\n" +
                    $"After installation, restart the application.",
                    "Download Started",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
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
    }
} 