using chronos_screentime.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace chronos_screentime.Services
{
    public class SleepService
    {
        private readonly string _sleepDataPath;
        private readonly string _cacheFilePath;
        private SleepData? _cachedSleepData;
        private DateTime _lastFileCheck = DateTime.MinValue;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5); // Cache for 5 minutes

        public SleepService()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _sleepDataPath = Path.Combine(userProfile, "iCloudDrive", "SleepData", "sleep.txt");
            
            // Create cache file path in the same directory as the sleep data
            var sleepDataDir = Path.GetDirectoryName(_sleepDataPath);
            _cacheFilePath = Path.Combine(sleepDataDir ?? userProfile, "sleep_cache.json");
        }

        public SleepData GetSleepData()
        {
            try
            {
                // Check if we need to refresh cache
                if (_cachedSleepData == null || 
                    DateTime.Now - _lastFileCheck > _cacheTimeout ||
                    File.GetLastWriteTime(_sleepDataPath) > _lastFileCheck)
                {
                    RefreshSleepData();
                }

                return _cachedSleepData ?? new SleepData();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SleepService: Error getting sleep data: {ex.Message}");
                return new SleepData(); // Return empty data on error
            }
        }

        private void RefreshSleepData()
        {
            try
            {
                // First, try to load from cache if it exists and is recent
                if (TryLoadFromCache())
                {
                    Debug.WriteLine("SleepService: Loaded data from cache");
                    return;
                }

                if (!File.Exists(_sleepDataPath))
                {
                    Debug.WriteLine($"SleepService: Sleep data file not found at {_sleepDataPath}");
                    _cachedSleepData = new SleepData();
                    return;
                }

                var originalContent = File.ReadAllText(_sleepDataPath);
                
                // Create a temporary file with JSON array wrapper
                var tempFilePath = Path.GetTempFileName();
                try
                {
                    // Clean up the original content and wrap it in JSON array brackets
                    var cleanedContent = originalContent.Trim();
                    
                    // Remove trailing comma if it exists
                    cleanedContent = cleanedContent.TrimEnd().TrimEnd(',');
                    
                    // Wrap in JSON array brackets if not already wrapped
                    if (!cleanedContent.StartsWith("[") || !cleanedContent.EndsWith("]"))
                    {
                        cleanedContent = $"[{cleanedContent}]";
                    }
                    
                    // Write to temporary file
                    File.WriteAllText(tempFilePath, cleanedContent);
                    
                    // Parse JSON array from temporary file
                    var sleepSessions = JsonConvert.DeserializeObject<List<SleepSession>>(cleanedContent) ?? new List<SleepSession>();
                    
                    // Remove duplicates and clean up data
                    var cleanedSessions = RemoveDuplicateSessions(sleepSessions);
                    
                    _cachedSleepData = new SleepData
                    {
                        AllSessions = cleanedSessions
                    };

                    _lastFileCheck = DateTime.Now;
                    
                    // Save to cache for faster future loading
                    SaveToCache();
                    
                    Debug.WriteLine($"SleepService: Successfully loaded {cleanedSessions.Count} sleep sessions (removed {sleepSessions.Count - cleanedSessions.Count} duplicates)");
                }
                finally
                {
                    // Clean up temporary file
                    if (File.Exists(tempFilePath))
                    {
                        try
                        {
                            File.Delete(tempFilePath);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"SleepService: Error deleting temp file: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SleepService: Error parsing sleep data: {ex.Message}");
                _cachedSleepData = new SleepData(); // Return empty data on parse error
            }
        }

        private List<SleepSession> RemoveDuplicateSessions(List<SleepSession> sessions)
        {
            try
            {
                var uniqueSessions = new List<SleepSession>();
                var seenSessions = new HashSet<string>();

                foreach (var session in sessions.OrderBy(s => s.Started))
                {
                    // Create a unique key for each session based on start time, end time, and duration
                    var sessionKey = $"{session.Started:yyyy-MM-dd HH:mm:ss}_{session.Ended:yyyy-MM-dd HH:mm:ss}_{session.Duration}";
                    
                    if (!seenSessions.Contains(sessionKey))
                    {
                        seenSessions.Add(sessionKey);
                        uniqueSessions.Add(session);
                    }
                    else
                    {
                        Debug.WriteLine($"SleepService: Removed duplicate session: {session.Started:yyyy-MM-dd HH:mm:ss} to {session.Ended:yyyy-MM-dd HH:mm:ss} ({session.Duration} minutes)");
                    }
                }

                return uniqueSessions;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SleepService: Error removing duplicate sessions: {ex.Message}");
                return sessions; // Return original list if error occurs
            }
        }

        private bool TryLoadFromCache()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                {
                    return false;
                }

                // Check if cache is still valid (not older than cache timeout)
                var cacheFileInfo = new FileInfo(_cacheFilePath);
                if (DateTime.Now - cacheFileInfo.LastWriteTime > _cacheTimeout)
                {
                    Debug.WriteLine("SleepService: Cache is expired, will reload from source");
                    return false;
                }

                // Check if source file is newer than cache
                if (File.Exists(_sleepDataPath))
                {
                    var sourceFileInfo = new FileInfo(_sleepDataPath);
                    if (sourceFileInfo.LastWriteTime > cacheFileInfo.LastWriteTime)
                    {
                        Debug.WriteLine("SleepService: Source file is newer than cache, will reload");
                        return false;
                    }
                }

                // Load from cache
                var cacheContent = File.ReadAllText(_cacheFilePath);
                var cachedData = JsonConvert.DeserializeObject<SleepData>(cacheContent);
                
                if (cachedData != null)
                {
                    _cachedSleepData = cachedData;
                    _lastFileCheck = DateTime.Now;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SleepService: Error loading from cache: {ex.Message}");
                return false;
            }
        }

        private void SaveToCache()
        {
            try
            {
                if (_cachedSleepData == null)
                {
                    return;
                }

                // Ensure the cache directory exists
                var cacheDir = Path.GetDirectoryName(_cacheFilePath);
                if (!string.IsNullOrEmpty(cacheDir) && !Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }

                // Serialize and save to cache
                var cacheContent = JsonConvert.SerializeObject(_cachedSleepData, Formatting.Indented);
                File.WriteAllText(_cacheFilePath, cacheContent);
                
                Debug.WriteLine($"SleepService: Saved {_cachedSleepData.AllSessions.Count} sessions to cache");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SleepService: Error saving to cache: {ex.Message}");
            }
        }

        public List<DailySleepSummary> GetDailySummaries()
        {
            return GetSleepData().GetDailySummaries();
        }

        public DailySleepSummary? GetSummaryForDate(DateTime date)
        {
            return GetSleepData().GetSummaryForDate(date);
        }

        public TimeSpan GetTotalSleepForPeriod(DateTime startDate, DateTime endDate)
        {
            return GetSleepData().GetTotalSleepForPeriod(startDate, endDate);
        }

        public bool IsSleepDataAvailable()
        {
            return File.Exists(_sleepDataPath);
        }

        public string GetSleepDataPath()
        {
            return _sleepDataPath;
        }

        public string GetCacheFilePath()
        {
            return _cacheFilePath;
        }

        public void ClearCache()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    File.Delete(_cacheFilePath);
                    Debug.WriteLine("SleepService: Cache cleared");
                }
                
                // Reset cache state
                _cachedSleepData = null;
                _lastFileCheck = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SleepService: Error clearing cache: {ex.Message}");
            }
        }

        public void ForceRefresh()
        {
            try
            {
                Debug.WriteLine("SleepService: Forcing refresh of sleep data");
                ClearCache();
                RefreshSleepData();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SleepService: Error forcing refresh: {ex.Message}");
            }
        }

        public int GetDuplicateCount()
        {
            try
            {
                if (!File.Exists(_sleepDataPath))
                {
                    return 0;
                }

                var originalContent = File.ReadAllText(_sleepDataPath);
                var cleanedContent = originalContent.Trim().TrimEnd().TrimEnd(',');
                
                if (!cleanedContent.StartsWith("[") || !cleanedContent.EndsWith("]"))
                {
                    cleanedContent = $"[{cleanedContent}]";
                }
                
                var sleepSessions = JsonConvert.DeserializeObject<List<SleepSession>>(cleanedContent) ?? new List<SleepSession>();
                var cleanedSessions = RemoveDuplicateSessions(sleepSessions);
                
                return sleepSessions.Count - cleanedSessions.Count;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SleepService: Error getting duplicate count: {ex.Message}");
                return 0;
            }
        }
    }
}
