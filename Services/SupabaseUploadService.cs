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
        private readonly CategoryService _categoryService;
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

            // Initialize CategoryService to load categories from categories.json
            _categoryService = new CategoryService();

            // Delete AppLock file if it exists
            DeleteAppLockFile();

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("apikey", _supabaseAnonKey);
            _httpClient.DefaultRequestHeaders.Add("authorization", $"Bearer {_supabaseAnonKey}");
            // Note: Content-Type should be set on HttpContent, not as a default header
            _httpClient.Timeout = TimeSpan.FromMinutes(5); // 5 minute timeout for large uploads
        }

        public async Task<UploadResult> UploadScreentimeDataAsync(ScreenTimeData screenTimeData, string? userId = null, string? deviceId = null, int uploadIntervalMinutes = 30)
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

                // Load cache to check for duplicates and last upload time
                var cache = LoadCache();
                System.Diagnostics.Debug.WriteLine($"Cache loaded: {cache.UploadedApps.Count} apps, {cache.UploadedWebsites.Count} websites, LastUploadTimeUtc={cache.LastUploadTimeUtc ?? "(never)"}");
                
                // Recalculate totals for all days (not just today) to ensure accuracy
                RecalculateAllDayTotals(screenTimeData);
                
                // Check if we have any data at all
                var totalAppsInData = screenTimeData.Years.Values
                    .SelectMany(y => y.Months.Values)
                    .SelectMany(m => m.Weeks.Values)
                    .SelectMany(w => w.Days.Values)
                    .SelectMany(d => d.Apps.Values)
                    .Count();
                var totalWebsitesInData = screenTimeData.Years.Values
                    .SelectMany(y => y.Months.Values)
                    .SelectMany(m => m.Weeks.Values)
                    .SelectMany(w => w.Days.Values)
                    .SelectMany(d => d.Websites.Values)
                    .Count();
                System.Diagnostics.Debug.WriteLine($"Source data: {totalAppsInData} apps, {totalWebsitesInData} websites in ScreenTimeData");
                
                // Skip if no source data
                if (totalAppsInData == 0 && totalWebsitesInData == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No data to upload - no apps or websites in ScreenTimeData");
                    return new UploadResult
                    {
                        Success = true,
                        AppsInserted = 0,
                        WebsitesInserted = 0,
                        TotalApps = 0,
                        TotalWebsites = 0
                    };
                }

                // Time-based gate: only upload if we've never uploaded or enough time has passed since last upload
                var intervalMinutes = uploadIntervalMinutes > 0 ? uploadIntervalMinutes : 30;
                if (!string.IsNullOrEmpty(cache.LastUploadTimeUtc) && DateTime.TryParse(cache.LastUploadTimeUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastUtc))
                {
                    var nextUploadAt = lastUtc.AddMinutes(intervalMinutes);
                    if (DateTime.UtcNow < nextUploadAt)
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping upload - next upload at {nextUploadAt:O} (last: {cache.LastUploadTimeUtc}, interval: {intervalMinutes} min)");
                        return new UploadResult
                        {
                            Success = true,
                            AppsInserted = 0,
                            WebsitesInserted = 0,
                            TotalApps = 0,
                            TotalWebsites = 0
                        };
                    }
                }

                // Convert ScreenTimeData to Edge Function format and filter duplicates (daily summaries always included)
                var payload = ConvertToEdgeFunctionFormatFiltered(screenTimeData, actualUserId, actualDeviceId, cache);

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
                    
                    // Update cache with uploaded data and last upload time (enables multiple uploads per day when interval has passed)
                    UpdateCache(payload, cache);
                    cache.LastUploadTimeUtc = DateTime.UtcNow.ToString("o");
                    SaveCache(cache);
                    
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

                            System.Diagnostics.Debug.WriteLine($"Processing day {dateStr}: {day.Apps.Count} apps, {day.Websites.Count} websites");

                            // Include all apps each time (exclude AppLock only). Edge Function upserts so re-uploading updates usage.
                            foreach (var appKvp in day.Apps)
                            {
                                var app = appKvp.Value;
                                if (app.AppName.Equals("AppLock", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                var category = _categoryService.GetCategoryForApp(app.AppName) ?? "Uncategorized";
                                appsDict[appKvp.Key] = new
                                {
                                    AppName = app.AppName,
                                    Category = category,
                                    ProcessPath = app.ProcessPath ?? string.Empty,
                                    TotalTime = FormatTimeSpan(app.TotalTime),
                                    SessionCount = app.SessionCount,
                                    FirstSeen = app.FirstSeen.ToString("O"),
                                    LastSeen = app.LastSeen.ToString("O"),
                                    LastActiveTime = app.LastActiveTime.ToString("O"),
                                    FirstSeenTime = app.FirstSeen.ToString("HH:mm:ss"),
                                    LastSeenTime = app.LastSeen.ToString("HH:mm:ss"),
                                    LastActiveTimeOfDay = app.LastActiveTime.ToString("HH:mm:ss")
                                };
                            }

                            // Include all websites each time. Edge Function upserts so re-uploading updates usage.
                            foreach (var websiteKvp in day.Websites)
                            {
                                var website = websiteKvp.Value;
                                var category = _categoryService.GetCategoryForWebsite(website.Domain) ?? "Uncategorized";
                                websitesDict[websiteKvp.Key] = new
                                {
                                    Domain = website.Domain,
                                    Category = category,
                                    TotalTime = FormatTimeSpan(website.TotalTime),
                                    SessionCount = website.SessionCount,
                                    FirstSeen = website.FirstSeen.ToString("O"),
                                    LastSeen = website.LastSeen.ToString("O"),
                                    LastActiveTime = website.LastActiveTime.ToString("O"),
                                    FirstSeenTime = website.FirstSeen.ToString("HH:mm:ss"),
                                    LastSeenTime = website.LastSeen.ToString("HH:mm:ss"),
                                    LastActiveTimeOfDay = website.LastActiveTime.ToString("HH:mm:ss"),
                                    FaviconUrl = website.FaviconUrl
                                };
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

            // Daily summaries (total_switches, total_apps per day). Edge Function must UPSERT into screentime_daily_summary
            // (ON CONFLICT (user_id, date, source, device_id, platform) DO UPDATE) to avoid duplicate key errors when re-uploading the same day.
            var dailySummaries = new List<object>();
            
            System.Diagnostics.Debug.WriteLine($"Calculating daily summaries from {screenTimeData.Years.Count} years");
            
            foreach (var yearKvp in screenTimeData.Years)
            {
                var year = yearKvp.Value;
                System.Diagnostics.Debug.WriteLine($"Processing year {yearKvp.Key}: {year.Months.Count} months");
                
                foreach (var monthKvp in year.Months)
                {
                    var month = monthKvp.Value;
                    System.Diagnostics.Debug.WriteLine($"Processing month {monthKvp.Key}: {month.Weeks.Count} weeks");
                    
                    foreach (var weekKvp in month.Weeks)
                    {
                        var week = weekKvp.Value;
                        System.Diagnostics.Debug.WriteLine($"Processing week {weekKvp.Key}: {week.Days.Count} days");
                        
                        foreach (var dayKvp in week.Days)
                        {
                            var day = dayKvp.Value;
                            var dateStr = day.Date.ToString("yyyy-MM-dd");
                            
                            System.Diagnostics.Debug.WriteLine($"Processing day {dateStr}: {day.Apps.Count} apps, {day.Websites.Count} websites");
                            
                            // Calculate total switches from ALL apps in the day (excluding AppLock)
                            // This should be the sum of all SessionCount values for apps
                            var appsList = day.Apps.Values
                                .Where(app => !app.AppName.Equals("AppLock", StringComparison.OrdinalIgnoreCase))
                                .ToList();
                            
                            var totalSwitches = appsList.Sum(app => app.SessionCount);
                            
                            // Calculate total apps (excluding AppLock)
                            var totalApps = appsList.Count;
                            
                            System.Diagnostics.Debug.WriteLine($"Day {dateStr} calculation: {appsList.Count} apps (excluding AppLock), total switches: {totalSwitches}, total apps: {totalApps}");
                            
                            // Debug: Show individual app session counts
                            foreach (var app in appsList)
                            {
                                System.Diagnostics.Debug.WriteLine($"  - App '{app.AppName}': {app.SessionCount} sessions");
                            }
                            
                            // Always include summary if there are apps or websites, even if counts are 0
                            // (This ensures we track days with activity)
                            if (day.Apps.Count > 0 || day.Websites.Count > 0)
                            {
                                dailySummaries.Add(new
                                {
                                    date = dateStr,
                                    total_switches = totalSwitches,
                                    total_apps = totalApps
                                });
                                
                                System.Diagnostics.Debug.WriteLine($"Added daily summary for {dateStr}: {totalSwitches} switches, {totalApps} apps");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Skipping day {dateStr} - no apps or websites");
                            }
                        }
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Total daily summaries to upload: {dailySummaries.Count}");

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
                System.Diagnostics.Debug.WriteLine($"Loading cache from: {_cacheFilePath}");
                if (File.Exists(_cacheFilePath))
                {
                    var json = File.ReadAllText(_cacheFilePath);
                    var cache = JsonConvert.DeserializeObject<UploadCache>(json);
                    var loadedCache = cache ?? new UploadCache();
                    System.Diagnostics.Debug.WriteLine($"Cache file found and loaded: {loadedCache.UploadedApps.Count} apps, {loadedCache.UploadedWebsites.Count} websites");
                    return loadedCache;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Cache file does not exist - starting with empty cache");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading cache: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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

        private void RecalculateAllDayTotals(ScreenTimeData screenTimeData)
        {
            // Recalculate TotalSwitches and TotalApps for all days to ensure accuracy
            // (UpdateHierarchicalTotals only updates today, so historical days might be stale)
            System.Diagnostics.Debug.WriteLine("Recalculating totals for all days...");
            
            foreach (var yearKvp in screenTimeData.Years)
            {
                foreach (var monthKvp in yearKvp.Value.Months)
                {
                    foreach (var weekKvp in monthKvp.Value.Weeks)
                    {
                        foreach (var dayKvp in weekKvp.Value.Days)
                        {
                            var day = dayKvp.Value;
                            
                            System.Diagnostics.Debug.WriteLine($"Recalculating {day.Date:yyyy-MM-dd}: {day.Apps.Count} apps in day.Apps");
                            
                            // Recalculate from actual app data (excluding AppLock)
                            var appsExcludingAppLock = day.Apps.Values
                                .Where(app => !app.AppName.Equals("AppLock", StringComparison.OrdinalIgnoreCase))
                                .ToList();
                            
                            var totalSwitches = appsExcludingAppLock.Sum(app => app.SessionCount);
                            var totalApps = appsExcludingAppLock.Count;
                            
                            day.TotalSwitches = totalSwitches;
                            day.TotalApps = totalApps;
                            
                            System.Diagnostics.Debug.WriteLine($"Recalculated totals for {day.Date:yyyy-MM-dd}: {totalSwitches} switches, {totalApps} apps (from {appsExcludingAppLock.Count} apps excluding AppLock)");
                            
                            // Debug: Show which apps contributed to switches
                            if (appsExcludingAppLock.Count > 0)
                            {
                                foreach (var app in appsExcludingAppLock.Take(5)) // Show first 5 apps
                                {
                                    System.Diagnostics.Debug.WriteLine($"  - {app.AppName}: {app.SessionCount} sessions");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"  WARNING: No apps found for {day.Date:yyyy-MM-dd} (excluding AppLock)");
                            }
                        }
                    }
                }
            }
        }

        private bool HasDataToUpload(dynamic payload)
        {
            try
            {
                // Check for daily summaries
                var dailySummaries = payload.daily_summaries;
                if (dailySummaries != null)
                {
                    var summariesCount = ((System.Collections.ICollection)dailySummaries).Count;
                    System.Diagnostics.Debug.WriteLine($"Daily summaries in payload: {summariesCount}");
                    if (summariesCount > 0)
                    {
                        return true;
                    }
                }

                // Check for apps/websites data
                var years = payload.data?.Years;
                if (years == null)
                {
                    System.Diagnostics.Debug.WriteLine("No Years data in payload");
                    return false;
                }
                
                System.Diagnostics.Debug.WriteLine($"Years in payload: {((System.Collections.ICollection)years).Count}");

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

                                var appsCount = apps != null ? ((IDictionary<string, object>)apps).Count : 0;
                                var websitesCount = websites != null ? ((IDictionary<string, object>)websites).Count : 0;
                                
                                if (appsCount > 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Found {appsCount} apps to upload in day {dayData.Date}");
                                    return true;
                                }
                                if (websitesCount > 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Found {websitesCount} websites to upload in day {dayData.Date}");
                                    return true;
                                }
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
            /// <summary>ISO 8601 UTC time of last successful upload. If now is after this + interval, we upload again.</summary>
            public string? LastUploadTimeUtc { get; set; }
        }
    }
}
