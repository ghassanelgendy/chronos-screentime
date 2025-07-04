using System.Net.Http;
using System.Security.Cryptography;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Windows;

namespace chronos_screentime.Services
{
    public class IncrementalUpdateService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static DateTime _lastUpdateCheck = DateTime.MinValue;
        private static readonly TimeSpan UpdateCheckInterval = UpdateConfig.UpdateCheckInterval;

        public class ManifestFile
        {
            public string path { get; set; }
            public string hash { get; set; }
        }

        public class Manifest
        {
            public string version { get; set; }
            public string base_url { get; set; }
            public List<ManifestFile> files { get; set; }
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

                if (manifest == null)
                {
                    throw new Exception("Failed to parse manifest file");
                }

                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var filesToUpdate = new List<ManifestFile>();

                foreach (var file in manifest.files)
                {
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

                // Download and update files
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
            catch (Exception ex)
            {
                throw new Exception($"Incremental update failed: {ex.Message}");
            }
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