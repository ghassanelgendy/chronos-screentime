using chronos_screentime.Models;
using Newtonsoft.Json;
using System.IO;
using System.Timers;

namespace chronos_screentime.Services
{
    public class ScreenTimeService : IDisposable
    {
        private readonly Dictionary<string, AppScreenTime> _appScreenTimes;
        private readonly Win32ApiService _win32ApiService;
        private readonly System.Timers.Timer _trackingTimer;
        private readonly string _dataFilePath;

        private string _currentActiveApp = string.Empty;
        private DateTime _currentSessionStartTime;
        private bool _isTracking = false;
        private int _totalSwitches = 0;

        public event EventHandler<AppScreenTime>? AppTime;
        public event EventHandler? DataChanged;

        public int TotalSwitches => _totalSwitches;

        public ScreenTimeService()
        {
            _appScreenTimes = new Dictionary<string, AppScreenTime>();
            _win32ApiService = new Win32ApiService();
            _trackingTimer = new System.Timers.Timer(1000); // Check every second
            _trackingTimer.Elapsed += OnTrackingTimerElapsed;

            _dataFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ChronosScreenTime",
                "screentime_data.json"
            );

            LoadData();
        }

        public void StartTracking()
        {
            if (_isTracking) return;

            _isTracking = true;
            _currentSessionStartTime = DateTime.Now;
            _trackingTimer.Start();

            // Initialize with current active app
            var activeWindow = _win32ApiService.GetActiveWindow();
            if (activeWindow != null)
            {
                _currentActiveApp = GetAppKey(activeWindow);
            }
        }

        public void StopTracking()
        {
            if (!_isTracking) return;

            _isTracking = false;
            _trackingTimer.Stop();

            // Record time for current active app before stopping
            if (!string.IsNullOrEmpty(_currentActiveApp))
            {
                RecordTimeForCurrentApp();
            }

            SaveData();
        }

        private void OnTrackingTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (!_isTracking) return;

            var activeWindow = _win32ApiService.GetActiveWindow();
            if (activeWindow == null) return;

            string newActiveApp = GetAppKey(activeWindow);

            // If app changed, record time for previous app and start tracking new one
            if (newActiveApp != _currentActiveApp)
            {
                if (!string.IsNullOrEmpty(_currentActiveApp))
                {
                    RecordTimeForCurrentApp();
                    _totalSwitches++; // Increment switch counter
                }

                // Start tracking new app
                _currentActiveApp = newActiveApp;
                _currentSessionStartTime = DateTime.Now;

                // Ensure app exists in dictionary
                EnsureAppExists(activeWindow);

                // Update session counts
                _appScreenTimes[_currentActiveApp].SessionCount++;

                // Update today's session count
                var today = DateTime.Today;
                var app = _appScreenTimes[_currentActiveApp];
                if (app.DailySessions.ContainsKey(today))
                {
                    app.DailySessions[today]++;
                }
                else
                {
                    app.DailySessions[today] = 1;
                }

                _appScreenTimes[_currentActiveApp].LastActiveTime = DateTime.Now;
                _appScreenTimes[_currentActiveApp].LastSeen = DateTime.Now;
            }
        }

        private void RecordTimeForCurrentApp()
        {
            if (string.IsNullOrEmpty(_currentActiveApp) || !_appScreenTimes.ContainsKey(_currentActiveApp))
                return;

            var sessionDuration = DateTime.Now - _currentSessionStartTime;
            if (sessionDuration.TotalSeconds < 1) return; // Ignore very short sessions

            var app = _appScreenTimes[_currentActiveApp];
            var today = DateTime.Today;

            // Add to cumulative time
            app.TotalTime += sessionDuration;

            // Add to today's time
            if (app.DailyTimes.ContainsKey(today))
            {
                app.DailyTimes[today] += sessionDuration;
            }
            else
            {
                app.DailyTimes[today] = sessionDuration;
            }

            AppTime?.Invoke(this, app);
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        private string GetAppKey(Win32ApiService.ActiveWindowInfo windowInfo)
        {
            // Use process name as the key - you could also use process path for more granular tracking
            return windowInfo.ProcessName;
        }

        private void EnsureAppExists(Win32ApiService.ActiveWindowInfo windowInfo)
        {
            string appKey = GetAppKey(windowInfo);

            if (!_appScreenTimes.ContainsKey(appKey))
            {
                _appScreenTimes[appKey] = new AppScreenTime
                {
                    AppName = windowInfo.ProcessName,
                    ProcessPath = windowInfo.ProcessPath,
                    TotalTime = TimeSpan.Zero,
                    DailyTimes = new Dictionary<DateTime, TimeSpan>(),
                    DailySessions = new Dictionary<DateTime, int>(),
                    FirstSeen = DateTime.Now,
                    LastSeen = DateTime.Now,
                    SessionCount = 0
                };
            }
        }

        public IEnumerable<AppScreenTime> GetAllAppScreenTimes()
        {
            return _appScreenTimes.Values.OrderByDescending(a => a.TotalTime);
        }

        public AppScreenTime? GetAppScreenTime(string appName)
        {
            return _appScreenTimes.TryGetValue(appName, out var appTime) ? appTime : null;
        }

        public TimeSpan GetTotalScreenTimeToday()
        {
            return TimeSpan.FromMilliseconds(_appScreenTimes.Values.Sum(app => app.TodaysTime.TotalMilliseconds));
        }

        public TimeSpan GetTotalScreenTimeTodayIncludingCurrent()
        {
            var totalRecorded = TimeSpan.FromMilliseconds(_appScreenTimes.Values.Sum(app => app.TodaysTime.TotalMilliseconds));

            // Add current session time if tracking
            if (_isTracking && !string.IsNullOrEmpty(_currentActiveApp))
            {
                var currentSessionDuration = DateTime.Now - _currentSessionStartTime;
                if (currentSessionDuration.TotalSeconds >= 1)
                {
                    totalRecorded = totalRecorded.Add(currentSessionDuration);
                }
            }

            return totalRecorded;
        }

        public void RefreshCurrentSessionTime()
        {
            // This method now just triggers a data refresh event for UI updates
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetAllData()
        {
            _appScreenTimes.Clear();
            _totalSwitches = 0;
            SaveData();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetAppData(string appName)
        {
            if (_appScreenTimes.ContainsKey(appName))
            {
                _appScreenTimes[appName].TotalTime = TimeSpan.Zero;
                _appScreenTimes[appName].SessionCount = 0;
                SaveData();
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    string json = File.ReadAllText(_dataFilePath);
                    var data = JsonConvert.DeserializeObject<SavedData>(json);

                    if (data != null)
                    {
                        if (data.AppScreenTimes != null)
                        {
                            foreach (var kvp in data.AppScreenTimes)
                            {
                                _appScreenTimes[kvp.Key] = kvp.Value;
                            }
                        }
                        _totalSwitches = data.TotalSwitches;
                    }
                }
            }
            catch (Exception ex)
            {
                // Try to load old format for backwards compatibility
                try
                {
                    string json = File.ReadAllText(_dataFilePath);
                    var loadedData = JsonConvert.DeserializeObject<Dictionary<string, AppScreenTime>>(json);

                    if (loadedData != null)
                    {
                        foreach (var kvp in loadedData)
                        {
                            _appScreenTimes[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
                }
            }
        }

        private void SaveData()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_dataFilePath)!);
                var data = new SavedData
                {
                    AppScreenTimes = _appScreenTimes,
                    TotalSwitches = _totalSwitches
                };
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving data: {ex.Message}");
            }
        }

        private class SavedData
        {
            public Dictionary<string, AppScreenTime> AppScreenTimes { get; set; } = new();
            public int TotalSwitches { get; set; } = 0;
        }

        public void Dispose()
        {
            StopTracking();
            _trackingTimer?.Dispose();
        }
    }
}