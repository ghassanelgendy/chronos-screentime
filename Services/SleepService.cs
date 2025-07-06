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
        private SleepData? _cachedSleepData;
        private DateTime _lastFileCheck = DateTime.MinValue;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(1); // Cache for 1 minute

        public SleepService()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _sleepDataPath = Path.Combine(userProfile, "iCloudDrive", "SleepData", "sleep.txt");
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
                if (!File.Exists(_sleepDataPath))
                {
                    Debug.WriteLine($"SleepService: Sleep data file not found at {_sleepDataPath}");
                    _cachedSleepData = new SleepData();
                    return;
                }

                var jsonContent = File.ReadAllText(_sleepDataPath);
                
                // Clean up JSON format (remove trailing comma if exists)
                jsonContent = jsonContent.TrimEnd().TrimEnd(',');
                
                // Parse JSON array
                var sleepSessions = JsonConvert.DeserializeObject<List<SleepSession>>(jsonContent) ?? new List<SleepSession>();
                
                _cachedSleepData = new SleepData
                {
                    AllSessions = sleepSessions
                };

                _lastFileCheck = DateTime.Now;
                Debug.WriteLine($"SleepService: Successfully loaded {sleepSessions.Count} sleep sessions");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SleepService: Error parsing sleep data: {ex.Message}");
                _cachedSleepData = new SleepData(); // Return empty data on parse error
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
    }
}
