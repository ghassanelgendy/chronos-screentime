using chronos_screentime.Models;
using Newtonsoft.Json;
using System.IO;
using System.Timers;

namespace chronos_screentime.Services
{
    public class ScreenTimeService : IDisposable
    {
        private readonly Win32ApiService _win32ApiService;
        private readonly System.Timers.Timer _trackingTimer;
        private readonly string _dataFilePath;

        private string _currentActiveApp = string.Empty;
        private DateTime _currentSessionStartTime;
        private bool _isTracking = false;
        private ScreenTimeData _screenTimeData;
        private DayData _currentDayData => GetOrCreateCurrentDayData();

        public event EventHandler<AppDailyData>? AppTime;
        public event EventHandler? DataChanged;

        public int TotalSwitches => _currentDayData.TotalSwitches;
        public string CurrentActiveApp => _currentActiveApp;

        public ScreenTimeService()
        {
            _screenTimeData = new ScreenTimeData();
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

        private DayData GetOrCreateCurrentDayData()
        {
            var today = DateTime.Today;
            var year = today.Year;
            var month = today.Month;
            var week = GetIso8601WeekOfYear(today);

            if (!_screenTimeData.Years.ContainsKey(year))
            {
                _screenTimeData.Years[year] = new YearData { Year = year };
            }

            var yearData = _screenTimeData.Years[year];
            if (!yearData.Months.ContainsKey(month))
            {
                yearData.Months[month] = new MonthData { Month = month };
            }

            var monthData = yearData.Months[month];
            if (!monthData.Weeks.ContainsKey(week))
            {
                monthData.Weeks[week] = new WeekData { WeekNumber = week };
            }

            var weekData = monthData.Weeks[week];
            if (!weekData.Days.ContainsKey(today))
            {
                weekData.Days[today] = new DayData { Date = today };
            }

            return weekData.Days[today];
        }

        private static int GetIso8601WeekOfYear(DateTime date)
        {
            var thursday = date.AddDays(3 - ((int)date.DayOfWeek + 6) % 7);
            return (thursday.DayOfYear - 1) / 7 + 1;
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
                    _currentDayData.TotalSwitches++; // Increment switch counter
                }

                // Start tracking new app
                _currentActiveApp = newActiveApp;
                _currentSessionStartTime = DateTime.Now;

                // Ensure app exists in dictionary
                EnsureAppExists(activeWindow);

                // Update session counts
                var app = _currentDayData.Apps[_currentActiveApp];
                app.SessionCount++;
                app.LastActiveTime = DateTime.Now;
                app.LastSeen = DateTime.Now;

                UpdateHierarchicalTotals();
            }
        }

        private void RecordTimeForCurrentApp()
        {
            if (string.IsNullOrEmpty(_currentActiveApp) || !_currentDayData.Apps.ContainsKey(_currentActiveApp))
                return;

            var sessionDuration = DateTime.Now - _currentSessionStartTime;
            if (sessionDuration.TotalSeconds < 1) return; // Ignore very short sessions

            var app = _currentDayData.Apps[_currentActiveApp];
            app.TotalTime += sessionDuration;

            // Update total time for the day
            _currentDayData.TotalTime += sessionDuration;

            UpdateHierarchicalTotals();

            AppTime?.Invoke(this, app);
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateHierarchicalTotals()
        {
            var today = DateTime.Today;
            var year = today.Year;
            var month = today.Month;
            var week = GetIso8601WeekOfYear(today);

            var yearData = _screenTimeData.Years[year];
            var monthData = yearData.Months[month];
            var weekData = monthData.Weeks[week];

            // Update totals at each level
            weekData.TotalTime = TimeSpan.FromMilliseconds(weekData.Days.Values.Sum(d => d.TotalTime.TotalMilliseconds));
            weekData.TotalSwitches = weekData.Days.Values.Sum(d => d.TotalSwitches);
            weekData.TotalApps = weekData.Days.Values.Sum(d => d.Apps.Count);

            monthData.TotalTime = TimeSpan.FromMilliseconds(monthData.Weeks.Values.Sum(w => w.TotalTime.TotalMilliseconds));
            monthData.TotalSwitches = monthData.Weeks.Values.Sum(w => w.TotalSwitches);
            monthData.TotalApps = monthData.Weeks.Values.Max(w => w.TotalApps);

            yearData.TotalTime = TimeSpan.FromMilliseconds(yearData.Months.Values.Sum(m => m.TotalTime.TotalMilliseconds));
            yearData.TotalSwitches = yearData.Months.Values.Sum(m => m.TotalSwitches);
            yearData.TotalApps = yearData.Months.Values.Max(m => m.TotalApps);
        }

        private string GetAppKey(Win32ApiService.ActiveWindowInfo windowInfo)
        {
            return windowInfo.ProcessName;
        }

        private void EnsureAppExists(Win32ApiService.ActiveWindowInfo windowInfo)
        {
            string appKey = GetAppKey(windowInfo);

            if (!_currentDayData.Apps.ContainsKey(appKey))
            {
                _currentDayData.Apps[appKey] = new AppDailyData
                {
                    AppName = windowInfo.ProcessName,
                    ProcessPath = windowInfo.ProcessPath,
                    TotalTime = TimeSpan.Zero,
                    FirstSeen = DateTime.Now,
                    LastSeen = DateTime.Now,
                    SessionCount = 0
                };
                _currentDayData.TotalApps = _currentDayData.Apps.Count;
                UpdateHierarchicalTotals();
            }
        }

        public IEnumerable<AppDailyData> GetAllAppScreenTimes()
        {
            return _currentDayData.Apps.Values.OrderByDescending(a => a.TotalTime.TotalMilliseconds);
        }

        public AppDailyData? GetAppScreenTime(string appName)
        {
            return _currentDayData.Apps.TryGetValue(appName, out var appTime) ? appTime : null;
        }

        public TimeSpan GetTotalScreenTimeToday()
        {
            return _currentDayData.TotalTime;
        }

        public TimeSpan GetTotalScreenTimeTodayIncludingCurrent()
        {
            var totalRecorded = _currentDayData.TotalTime;

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
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetAllData()
        {
            _screenTimeData = new ScreenTimeData();
            SaveData();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetAppData(string appName)
        {
            if (_currentDayData.Apps.ContainsKey(appName))
            {
                _currentDayData.Apps[appName].TotalTime = TimeSpan.Zero;
                _currentDayData.Apps[appName].SessionCount = 0;
                UpdateHierarchicalTotals();
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
                    var data = JsonConvert.DeserializeObject<ScreenTimeData>(json);

                    if (data != null)
                    {
                        _screenTimeData = data;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
                _screenTimeData = new ScreenTimeData();
            }
        }

        private void SaveData()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_dataFilePath)!);
                string json = JsonConvert.SerializeObject(_screenTimeData, Formatting.Indented);
                File.WriteAllText(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving data: {ex.Message}");
            }
        }

        public void Dispose()
        {
            StopTracking();
            _trackingTimer?.Dispose();
        }

        public ScreenTimeData GetScreenTimeData()
        {
            return _screenTimeData;
        }
    }
}