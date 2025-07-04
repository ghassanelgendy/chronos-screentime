using System.Net.Http;
using System.Security.Cryptography;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Windows;
using System.IO.Compression;
using chronos_screentime.Windows;

namespace chronos_screentime.Services
{
    public class IncrementalUpdateService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static DateTime _lastUpdateCheck = DateTime.MinValue;
        private static readonly TimeSpan UpdateCheckInterval = UpdateConfig.UpdateCheckInterval;

        public class ManifestFile
        {
            public string? path { get; set; }
            public string? hash { get; set; }
        }

        public class Manifest
        {
            public string? version { get; set; }
            public string? base_url { get; set; }
            public List<ManifestFile>? files { get; set; }
        }

        public static async Task CheckAndUpdateAsync(string manifestUrl, bool forceCheck = false)
        {
            // Only check once per day unless forced
            if (!forceCheck && DateTime.Now - _lastUpdateCheck < UpdateCheckInterval)
                return;

            _lastUpdateCheck = DateTime.Now;

            try
            {
                // Download manifest
                var manifestJson = await _httpClient.GetStringAsync(manifestUrl);
                var manifest = JsonConvert.DeserializeObject<Manifest>(manifestJson);

                if (manifest == null || manifest.files == null || string.IsNullOrEmpty(manifest.version) || string.IsNullOrEmpty(manifest.base_url))
                {
                    throw new Exception("Failed to parse manifest file or missing required fields");
                }

                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var filesToUpdate = new List<ManifestFile>();

                foreach (var file in manifest.files)
                {
                    if (string.IsNullOrEmpty(file.path) || string.IsNullOrEmpty(file.hash))
                        continue;
                        
                    var localPath = Path.Combine(appDir, file.path);
                    if (!File.Exists(localPath) || GetFileHash(localPath) != file.hash)
                    {
                        filesToUpdate.Add(file);
                    }
                }

                if (filesToUpdate.Count == 0)
                {
                    if (forceCheck)
                    {
                        MessageBox.Show("You are running the latest version.", "No Update Needed");
                    }
                    return;
                }

                // Show progress dialog
                var result = MessageBox.Show(
                    $"Found {filesToUpdate.Count} file(s) to update.\n\n" +
                    $"Version: {manifest.version}\n\n" +
                    $"Would you like to download and install the update now?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result != MessageBoxResult.Yes)
                    return;

                // Try ZIP-based update first, fallback to individual files
                try
                {
                    await DownloadAndUpdateFromZipAsync(manifest, filesToUpdate);
                }
                catch (Exception zipEx)
                {
                    System.Diagnostics.Debug.WriteLine($"ZIP update failed, falling back to individual files: {zipEx.Message}");
                    await DownloadAndUpdateIndividualFilesAsync(manifest, filesToUpdate);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Incremental update failed: {ex.Message}");
            }
        }

        private static async Task DownloadAndUpdateFromZipAsync(Manifest manifest, List<ManifestFile> filesToUpdate)
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var tempDir = Path.Combine(Path.GetTempPath(), $"ChronosUpdate_{Guid.NewGuid()}");
            var zipUrl = $"{manifest.base_url}/ChronosScreenTimeTracker-v{manifest.version}.zip";
            var zipPath = Path.Combine(Path.GetTempPath(), $"ChronosScreenTimeTracker-v{manifest.version}.zip");

            try
            {
                // Create temporary directory
                Directory.CreateDirectory(tempDir);

                // Show progress window
                var progressWindow = new UpdateProgressWindow(new UpdateInfo 
                { 
                    Version = manifest.version,
                    FileSize = 0 // We don't know the size yet
                });
                progressWindow.Show();

                // Download ZIP file
                using (var response = await _httpClient.GetAsync(zipUrl))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Failed to download ZIP file: {response.StatusCode}");
                    }

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var buffer = new byte[8192];
                    var totalBytesRead = 0L;

                    using (var fileStream = File.Create(zipPath))
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
                ZipFile.ExtractToDirectory(zipPath, tempDir);

                // Update files from extracted ZIP
                var updatedCount = 0;
                foreach (var file in filesToUpdate)
                {
                    if (string.IsNullOrEmpty(file.path) || string.IsNullOrEmpty(file.hash))
                        continue;
                        
                    var extractedPath = Path.Combine(tempDir, file.path);
                    var localPath = Path.Combine(appDir, file.path);

                    if (File.Exists(extractedPath))
                    {
                        // Verify hash before copying
                        var extractedHash = GetFileHash(extractedPath);
                        if (extractedHash == file.hash)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                            File.Copy(extractedPath, localPath, true);
                            updatedCount++;
                        }
                        else
                        {
                            throw new Exception($"Hash mismatch for {file.path}");
                        }
                    }
                }

                MessageBox.Show(
                    $"Update complete! {updatedCount} file(s) updated from ZIP.\n\n" +
                    $"Please restart the application to apply changes.",
                    "Update Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            finally
            {
                // Clean up temporary files
                try
                {
                    if (File.Exists(zipPath))
                        File.Delete(zipPath);
                    
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to clean up temporary files: {ex.Message}");
                }
            }
        }

        private static async Task DownloadAndUpdateIndividualFilesAsync(Manifest manifest, List<ManifestFile> filesToUpdate)
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;

            // Download and update files individually
            foreach (var file in filesToUpdate)
            {
                var url = $"{manifest.base_url}/{file.path}";
                var localPath = Path.Combine(appDir, file.path);

                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

                using (var response = await _httpClient.GetAsync(url))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Failed to download {file.path}: {response.StatusCode}");
                    }

                    using (var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }
            }

            MessageBox.Show(
                $"Update complete! {filesToUpdate.Count} file(s) updated.\n\n" +
                $"Please restart the application to apply changes.",
                "Update Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private static string GetFileHash(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }
    }
} 