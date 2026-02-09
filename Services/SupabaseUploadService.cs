using chronos_screentime.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace chronos_screentime.Services
{
    public class SupabaseUploadService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _supabaseUrl;
        private readonly string _supabaseAnonKey;
        private readonly string? _userId;
        private readonly string? _deviceId;
        private readonly string _cacheFilePath;
        private readonly string _appLockFilePath;
        private bool _isDisposed = false;

        public SupabaseUploadService(string supabaseUrl, string supabaseAnonKey, string? userId = null, string? deviceId = null)
        {
            _supabaseUrl = supabaseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(supabaseUrl));
            _supabaseAnonKey = supabaseAnonKey ?? throw new ArgumentNullException(nameof(supabaseAnonKey));
            _userId = userId;
            _deviceId = deviceId ?? Environment.MachineName;

            // Set up cache file path in AppData
            var cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ChronosScreenTime"
            );
            Directory.CreateDirectory(cacheDirectory);
            _cacheFilePath = Path.Combine(cacheDirectory, "supabase_upload_cache.json");
            _appLockFilePath = Path.Combine(cacheDirectory, "AppLock");

            // Delete AppLock file if it exists
            DeleteAppLockFile();

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("apikey", _supabaseAnonKey);
            _httpClient.DefaultRequestHeaders.Add("authorization", $"Bearer {_supabaseAnonKey}");
            // Note: Content-Type should be set on HttpContent, not as a default header
            _httpClient.Timeout = TimeSpan.FromMinutes(5); // 5 minute timeout for large uploads
        }

        public async Task<UploadResult> UploadScreentimeDataAsync(ScreenTimeData screenTimeData, string? userId = null, string? deviceId = null)
        {
            if (string.IsNullOrWhiteSpace(userId ?? _userId))
            {
                return new UploadResult
                {
                    Success = false,
                    ErrorMessage = "User ID is required for upload"
                };
            }

            try
            {
                var actualUserId = userId ?? _userId!;
                var actualDeviceId = deviceId ?? _deviceId!;

                // Delete AppLock file if it exists
                DeleteAppLockFile();

                // Load cache to check for duplicates
                var cache = LoadCache();
                
                // Convert ScreenTimeData to Edge Function format and filter duplicates
                var payload = ConvertToEdgeFunctionFormatFiltered(screenTimeData, actualUserId, actualDeviceId, cache);

                // Check if there's anything to upload
                if (!HasDataToUpload(payload))
                {
                    System.Diagnostics.Debug.WriteLine("No new data to upload - all data already in cache");
                    return new UploadResult
                    {
                        Success = true,
                        AppsInserted = 0,
                        WebsitesInserted = 0,
                        TotalApps = 0,
                        TotalWebsites = 0
                    };
                }

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Call Edge Function
                var functionUrl = $"{_supabaseUrl}/functions/v1/upload-screentime";
                var response = await _httpClient.PostAsync(functionUrl, content);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<UploadResponse>(responseContent);
                    System.Diagnostics.Debug.WriteLine($"Upload successful: {result?.Inserted?.Apps} apps, {result?.Inserted?.Websites} websites");
                    
                    // Update cache with uploaded data
                    UpdateCache(payload, cache);
                    
                    return new UploadResult
                    {
                        Success = true,
                        AppsInserted = result?.Inserted?.Apps ?? 0,
                        WebsitesInserted = result?.Inserted?.Websites ?? 0,
                        TotalApps = result?.Total?.Apps ?? 0,
                        TotalWebsites = result?.Total?.Websites ?? 0
                    };
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Upload failed: {response.StatusCode} - {responseContent}");
                    return new UploadResult
                    {
                        Success = false,
                        ErrorMessage = $"Upload failed: {response.StatusCode} - {responseContent}"
                    };
                }
            }
            catch (TaskCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Upload timeout: {ex.Message}");
                return new UploadResult
                {
                    Success = false,
                    ErrorMessage = "Upload timed out. Please check your internet connection."
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error uploading: {ex.Message}");
                return new UploadResult
                {
                    Success = false,
                    ErrorMessage = $"Upload error: {ex.Message}"
                };
            }
        }

        private object ConvertToEdgeFunctionFormatFiltered(ScreenTimeData screenTimeData, string userId, string deviceId, UploadCache cache)
        {
            var yearsDict = new Dictionary<string, object>();
            var source = "pc";
            var platform = "windows";

            foreach (var yearKvp in screenTimeData.Years)
            {
                var year = yearKvp.Key;
                var yearData = yearKvp.Value;
                var monthsDict = new Dictionary<string, object>();

                foreach (var monthKvp in yearData.Months)
                {
                    var month = monthKvp.Key;
                    var monthData = monthKvp.Value;
                    var weeksDict = new Dictionary<string, object>();

                    foreach (var weekKvp in monthData.Weeks)
                    {
                        var week = weekKvp.Key;
                        var weekData = weekKvp.Value;
                        var daysDict = new Dictionary<string, object>();

                        foreach (var dayKvp in weekData.Days)
                        {
                            var day = dayKvp.Value;
                            var dateStr = day.Date.ToString("yyyy-MM-dd");
                            var appsDict = new Dictionary<string, object>();
                            var websitesDict = new Dictionary<string, object>();

                            // Filter Apps - exclude AppLock and only include if not in cache
                            foreach (var appKvp in day.Apps)
                            {
                                var app = appKvp.Value;
                                
                                // Skip AppLock app
                                if (app.AppName.Equals("AppLock", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }
                                
                                var cacheKey = GetAppCacheKey(userId, dateStr, source, deviceId, platform, app.AppName);
                                
                                if (!cache.UploadedApps.Contains(cacheKey))
                                {
                                    appsDict[appKvp.Key] = new
                                    {
                                        AppName = app.AppName,
                                        Category = app.Category ?? "Uncategorized",
                                        ProcessPath = app.ProcessPath ?? string.Empty,
                                        TotalTime = FormatTimeSpan(app.TotalTime),
                                        SessionCount = app.SessionCount,
                                        // Time information - when the app was used
                                        FirstSeen = app.FirstSeen.ToString("O"), // ISO 8601: First time app was used today
                                        LastSeen = app.LastSeen.ToString("O"), // ISO 8601: Last time app was seen today
                                        LastActiveTime = app.LastActiveTime.ToString("O"), // ISO 8601: Last time app was actively used
                                        // Additional time context
                                        FirstSeenTime = app.FirstSeen.ToString("HH:mm:ss"), // Time of day (HH:mm:ss)
                                        LastSeenTime = app.LastSeen.ToString("HH:mm:ss"), // Time of day (HH:mm:ss)
                                        LastActiveTimeOfDay = app.LastActiveTime.ToString("HH:mm:ss") // Time of day (HH:mm:ss)
                                    };
                                }
                            }

                            // Filter Websites - only include if not in cache
                            foreach (var websiteKvp in day.Websites)
                            {
                                var website = websiteKvp.Value;
                                var cacheKey = GetWebsiteCacheKey(userId, dateStr, source, deviceId, platform, website.Domain);
                                
                                if (!cache.UploadedWebsites.Contains(cacheKey))
                                {
                                    websitesDict[websiteKvp.Key] = new
                                    {
                                        Domain = website.Domain,
                                        TotalTime = FormatTimeSpan(website.TotalTime),
                                        SessionCount = website.SessionCount,
                                        // Time information - when the website was used
                                        FirstSeen = website.FirstSeen.ToString("O"), // ISO 8601: First time website was accessed today
                                        LastSeen = website.LastSeen.ToString("O"), // ISO 8601: Last time website was seen today
                                        LastActiveTime = website.LastActiveTime.ToString("O"), // ISO 8601: Last time website was actively used
                                        // Additional time context
                                        FirstSeenTime = website.FirstSeen.ToString("HH:mm:ss"), // Time of day (HH:mm:ss)
                                        LastSeenTime = website.LastSeen.ToString("HH:mm:ss"), // Time of day (HH:mm:ss)
                                        LastActiveTimeOfDay = website.LastActiveTime.ToString("HH:mm:ss"), // Time of day (HH:mm:ss)
                                        FaviconUrl = website.FaviconUrl
                                    };
                                }
                            }

                            // Only add day if it has apps or websites to upload
                            if (appsDict.Count > 0 || websitesDict.Count > 0)
                            {
                                daysDict[dateStr] = new
                                {
                                    Date = dateStr,
                                    Apps = appsDict,
                                    Websites = websitesDict
                                };
                            }
                        }

                        if (daysDict.Count > 0)
                        {
                            weeksDict[week.ToString()] = new
                            {
                                Days = daysDict
                            };
                        }
                    }

                    if (weeksDict.Count > 0)
                    {
                        monthsDict[month.ToString()] = new
                        {
                            Weeks = weeksDict
                        };
                    }
                }

                if (monthsDict.Count > 0)
                {
                    yearsDict[year.ToString()] = new
                    {
                        Months = monthsDict
                    };
                }
            }

            // Calculate daily summaries (total_switches, total_apps per day)
            // Use the existing DayData properties which are already calculated
            // Filter out summaries that are already in cache
            var dailySummaries = new List<object>();
            foreach (var yearKvp in screenTimeData.Years)
            {
                foreach (var monthKvp in yearKvp.Value.Months.Values)
                {
                    foreach (var weekKvp in monthKvp.Weeks.Values)
                    {
                        foreach (var dayKvp in weekKvp.Days)
                        {
                            var day = dayKvp.Value;
                            var dateStr = day.Date.ToString("yyyy-MM-dd");
                            
                            // Check if this summary is already in cache
                            var summaryCacheKey = GetDailySummaryCacheKey(userId, dateStr, source, deviceId, platform);
                            if (cache.UploadedDailySummaries.Contains(summaryCacheKey))
                            {
                                continue; // Skip if already uploaded
                            }
                            
                            // Use TotalSwitches and TotalApps from DayData (already calculated)
                            // Only include if there's data for the day
                            if (day.TotalApps > 0 || day.Websites.Count > 0)
                            {
                                dailySummaries.Add(new
                                {
                                    date = dateStr,
                                    total_switches = day.TotalSwitches,
                                    total_apps = day.TotalApps
                                });
                            }
                        }
                    }
                }
            }

            return new
            {
                user_id = userId,
                device_id = deviceId,
                platform = platform,
                source = source,
                data = new
                {
                    Years = yearsDict
                },
                daily_summaries = dailySummaries
            };
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            // Format as "hh:mm:ss.fffffff" as expected by Edge Function
            // The Edge Function parses this and converts to total seconds
            var totalSeconds = (long)timeSpan.TotalSeconds;
            var hours = totalSeconds / 3600;
            var minutes = (totalSeconds % 3600) / 60;
            var seconds = totalSeconds % 60;
            
            // Get fractional seconds (7 digits for microseconds)
            // Convert remaining fractional seconds to microseconds
            var fractionalPart = timeSpan.TotalSeconds - totalSeconds;
            var microseconds = (long)(fractionalPart * 1000000); // Convert to microseconds
            
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}.{microseconds:D7}";
        }

        private void DeleteAppLockFile()
        {
            try
            {
                if (File.Exists(_appLockFilePath))
                {
                    File.Delete(_appLockFilePath);
                    System.Diagnostics.Debug.WriteLine("AppLock file deleted");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting AppLock file: {ex.Message}");
            }
        }

        private UploadCache LoadCache()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    var json = File.ReadAllText(_cacheFilePath);
                    var cache = JsonConvert.DeserializeObject<UploadCache>(json);
                    return cache ?? new UploadCache();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading cache: {ex.Message}");
            }
            return new UploadCache();
        }

        private void SaveCache(UploadCache cache)
        {
            try
            {
                var directory = Path.GetDirectoryName(_cacheFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(cache, Formatting.Indented);
                File.WriteAllText(_cacheFilePath, json);
                System.Diagnostics.Debug.WriteLine($"Cache saved to {_cacheFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving cache: {ex.Message}");
            }
        }

        private string GetAppCacheKey(string userId, string date, string source, string deviceId, string platform, string appName)
        {
            return $"{userId}|{date}|{source}|{deviceId ?? ""}|{platform}|{appName}";
        }

        private string GetWebsiteCacheKey(string userId, string date, string source, string deviceId, string platform, string domain)
        {
            return $"{userId}|{date}|{source}|{deviceId ?? ""}|{platform}|{domain}";
        }

        private string GetDailySummaryCacheKey(string userId, string date, string source, string deviceId, string platform)
        {
            return $"{userId}|{date}|{source}|{deviceId ?? ""}|{platform}|summary";
        }

        private bool HasDataToUpload(dynamic payload)
        {
            try
            {
                // Check for daily summaries
                var dailySummaries = payload.daily_summaries;
                if (dailySummaries != null && ((System.Collections.ICollection)dailySummaries).Count > 0)
                {
                    return true;
                }

                // Check for apps/websites data
                var years = payload.data?.Years;
                if (years == null) return false;

                foreach (var year in years)
                {
                    var months = year.Value?.Months;
                    if (months == null) continue;

                    foreach (var month in months)
                    {
                        var weeks = month.Value?.Weeks;
                        if (weeks == null) continue;

                        foreach (var week in weeks)
                        {
                            var days = week.Value?.Days;
                            if (days == null) continue;

                            foreach (var day in days)
                            {
                                var dayData = day.Value;
                                if (dayData == null) continue;
                                
                                var apps = dayData.Apps;
                                var websites = dayData.Websites;

                                if (apps != null && ((IDictionary<string, object>)apps).Count > 0)
                                    return true;
                                if (websites != null && ((IDictionary<string, object>)websites).Count > 0)
                                    return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking if data exists: {ex.Message}");
            }
            return false;
        }

        private void UpdateCache(dynamic payload, UploadCache cache)
        {
            try
            {
                var userId = payload.user_id?.ToString() ?? string.Empty;
                var deviceId = payload.device_id?.ToString() ?? string.Empty;
                var source = payload.source?.ToString() ?? "pc";
                var platform = payload.platform?.ToString() ?? "windows";
                var years = payload.data?.Years;
                
                // Also cache daily summaries to prevent duplicate uploads
                var dailySummaries = payload.daily_summaries;
                if (dailySummaries != null)
                {
                    foreach (var summary in dailySummaries)
                    {
                        var dateStr = summary.date?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(dateStr))
                        {
                            var summaryCacheKey = GetDailySummaryCacheKey(userId, dateStr, source, deviceId, platform);
                            if (!cache.UploadedDailySummaries.Contains(summaryCacheKey))
                            {
                                cache.UploadedDailySummaries.Add(summaryCacheKey);
                            }
                        }
                    }
                }

                if (years == null) return;

                foreach (var year in years)
                {
                    var months = year.Value?.Months;
                    if (months == null) continue;

                    foreach (var month in months)
                    {
                        var weeks = month.Value?.Weeks;
                        if (weeks == null) continue;

                        foreach (var week in weeks)
                        {
                            var days = week.Value?.Days;
                            if (days == null) continue;

                            foreach (var day in days)
                            {
                                var dayData = day.Value;
                                if (dayData == null) continue;
                                
                                var dateStr = dayData.Date?.ToString() ?? string.Empty;
                                if (string.IsNullOrEmpty(dateStr)) continue;
                                
                                var apps = dayData.Apps;
                                var websites = dayData.Websites;

                                // Add apps to cache
                                if (apps != null)
                                {
                                    foreach (var app in apps)
                                    {
                                        var appData = app.Value;
                                        if (appData == null) continue;
                                        
                                        var appName = appData.AppName?.ToString() ?? string.Empty;
                                        if (!string.IsNullOrEmpty(appName))
                                        {
                                            var cacheKey = GetAppCacheKey(userId, dateStr, source, deviceId, platform, appName);
                                            if (!cache.UploadedApps.Contains(cacheKey))
                                            {
                                                cache.UploadedApps.Add(cacheKey);
                                            }
                                        }
                                    }
                                }

                                // Add websites to cache
                                if (websites != null)
                                {
                                    foreach (var website in websites)
                                    {
                                        var websiteData = website.Value;
                                        if (websiteData == null) continue;
                                        
                                        var domain = websiteData.Domain?.ToString() ?? string.Empty;
                                        if (!string.IsNullOrEmpty(domain))
                                        {
                                            var cacheKey = GetWebsiteCacheKey(userId, dateStr, source, deviceId, platform, domain);
                                            if (!cache.UploadedWebsites.Contains(cacheKey))
                                            {
                                                cache.UploadedWebsites.Add(cacheKey);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Save updated cache
                SaveCache(cache);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating cache: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _httpClient?.Dispose();
                _isDisposed = true;
            }
        }

        public class UploadResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public int AppsInserted { get; set; }
            public int WebsitesInserted { get; set; }
            public int TotalApps { get; set; }
            public int TotalWebsites { get; set; }
        }

        private class UploadResponse
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("inserted")]
            public InsertedData? Inserted { get; set; }

            [JsonProperty("total")]
            public TotalData? Total { get; set; }
        }

        private class InsertedData
        {
            [JsonProperty("apps")]
            public int Apps { get; set; }

            [JsonProperty("websites")]
            public int Websites { get; set; }
        }

        private class TotalData
        {
            [JsonProperty("apps")]
            public int Apps { get; set; }

            [JsonProperty("websites")]
            public int Websites { get; set; }
        }

        private class UploadCache
        {
            public HashSet<string> UploadedApps { get; set; } = new HashSet<string>();
            public HashSet<string> UploadedWebsites { get; set; } = new HashSet<string>();
            public HashSet<string> UploadedDailySummaries { get; set; } = new HashSet<string>();
        }
    }
}
