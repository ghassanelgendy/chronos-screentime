using chronos_screentime.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;

namespace chronos_screentime.Services
{
    public class ScreenTimeService : IDisposable
    {
        private readonly Win32ApiService _win32ApiService;
        private readonly BrowserTrackingService _browserTrackingService;
        private readonly CategoryService _categoryService;
        private readonly System.Timers.Timer _trackingTimer;
        private readonly string _dataFilePath;
        private readonly Dictionary<string, AppScreenTime> _apps;
        private readonly Dictionary<string, WebsiteScreenTime> _websites;

        private string _currentActiveApp = string.Empty;
        private string _currentActiveWebsite = string.Empty;
        private DateTime _currentSessionStartTime;
        private DateTime _currentWebsiteSessionStartTime;
        private bool _isTracking = false;
        private bool _isUserIdle = false;
        // Idle threshold is configurable from settings (0 = disabled)
        private TimeSpan _idleThreshold = TimeSpan.FromMinutes(5);
        private ScreenTimeData _screenTimeData;
        private readonly System.Timers.Timer _saveTimer;
        // Lag detection: if timer fires this late, we don't count the interval (PC was frozen/lagging)
        private DateTime _lastTickUtc;
        private static readonly TimeSpan LagThreshold = TimeSpan.FromSeconds(2.5);

        public event EventHandler? DataChanged;

        public ScreenTimeService()
        {
            _apps = new Dictionary<string, AppScreenTime>();
            _websites = new Dictionary<string, WebsiteScreenTime>();
            _screenTimeData = new ScreenTimeData();
            _win32ApiService = new Win32ApiService();
            _browserTrackingService = new BrowserTrackingService();
            _categoryService = new CategoryService();
            _trackingTimer = new System.Timers.Timer(1000); // Check every second
            _trackingTimer.Elapsed += OnTrackingTimerElapsed;
            _saveTimer = new System.Timers.Timer(300000); // 5 minutes
            _saveTimer.Elapsed += OnSaveTimerElapsed;

            _dataFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ChronosScreenTime",
                "screentime_data.json"
            );

            LoadData();
        }

        /// <summary>
        /// Update idle threshold from settings. 0 minutes disables idle timeout completely.
        /// </summary>
        public void UpdateIdleThreshold(int idleThresholdMinutes)
        {
            if (idleThresholdMinutes <= 0)
            {
                _idleThreshold = TimeSpan.Zero;
                System.Diagnostics.Debug.WriteLine("ScreenTimeService: Idle timeout disabled (threshold = 0 minutes).");
            }
            else
            {
                _idleThreshold = TimeSpan.FromMinutes(idleThresholdMinutes);
                System.Diagnostics.Debug.WriteLine($"ScreenTimeService: Idle timeout set to {_idleThreshold.TotalMinutes} minutes.");
            }
        }

        public void StartTracking()
        {
            if (_isTracking) return;

            _isTracking = true;
            _currentSessionStartTime = DateTime.Now;
            _currentWebsiteSessionStartTime = DateTime.Now;
            _lastTickUtc = DateTime.UtcNow;
            _trackingTimer.Start();
            _saveTimer.Start();

            // Initialize with current active app
            var activeWindow = _win32ApiService.GetActiveWindow();
            if (activeWindow != null)
            {
                _currentActiveApp = activeWindow.ProcessName;
                EnsureAppExists(activeWindow);

                // Initialize website tracking if it's a browser
                var browserInfo = _browserTrackingService.GetCurrentBrowserInfo(activeWindow.ProcessName);
                if (browserInfo != null && browserInfo.IsValid)
                {
                    _currentActiveWebsite = browserInfo.Domain;
                    EnsureWebsiteExists(browserInfo);
                }
            }
        }

        public void StopTracking()
        {
            if (!_isTracking) return;

            _isTracking = false;
            _trackingTimer.Stop();
            _saveTimer.Stop();

            // Record time for current active app before stopping
            if (!string.IsNullOrEmpty(_currentActiveApp))
            {
                RecordTimeForCurrentApp();
            }

            // Record time for current active website before stopping
            if (!string.IsNullOrEmpty(_currentActiveWebsite))
            {
                RecordTimeForCurrentWebsite();
            }

            SaveData();
        }

        private void OnTrackingTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                if (!_isTracking) return;

                var nowUtc = DateTime.UtcNow;
                var elapsedSinceLastTick = nowUtc - _lastTickUtc;
                if (elapsedSinceLastTick > LagThreshold)
                {
                    // PC was lagging/frozen – don't count this period; reset session start so next tick is accurate
                    _currentSessionStartTime = DateTime.Now;
                    _currentWebsiteSessionStartTime = DateTime.Now;
                    _lastTickUtc = nowUtc;
                    System.Diagnostics.Debug.WriteLine($"ScreenTimeService: Lag detected ({elapsedSinceLastTick.TotalSeconds:F1}s since last tick), skipping interval.");
                    return;
                }
                _lastTickUtc = nowUtc;

                var activeWindow = _win32ApiService.GetActiveWindow();
                if (activeWindow == null)
                {
                    // If no active window, consider it as idle for tracking purposes
                    HandleIdleState();
                    return;
                }

                // Exclude LockApp from tracking
                if (activeWindow.ProcessName.Equals("LockApp", StringComparison.OrdinalIgnoreCase) &&
                    activeWindow.ProcessPath.StartsWith(@"C:\Windows\SystemApps\Microsoft.LockApp_", StringComparison.OrdinalIgnoreCase))
                {
                    // Treat LockApp as an idle state for tracking
                    HandleIdleState();
                    return; // Skip tracking for LockApp
                }

                // Check for user idle time only if idle timeout is enabled
                if (_idleThreshold > TimeSpan.Zero)
                {
                    uint idleTimeMilliseconds = _win32ApiService.GetIdleTime();
                    TimeSpan idleTime = TimeSpan.FromMilliseconds(idleTimeMilliseconds);

                    if (idleTime > _idleThreshold)
                    {
                        // User is idle
                        HandleIdleState();
                        return; // Skip active tracking logic
                    }
                }

                // If we are here, user is considered active for tracking purposes
                HandleActiveState();

                // If we are here, it means the user is active, so proceed with normal tracking
                string newActiveApp = activeWindow.ProcessName;
                string newActiveWebsite = string.Empty;
                
                // Check if this is a browser and get website info
                var browserInfo = _browserTrackingService.GetCurrentBrowserInfo(activeWindow.ProcessName);
                if (browserInfo != null && browserInfo.IsValid)
                {
                    newActiveWebsite = browserInfo.Domain;
                }

                // Handle app changes
                bool appChanged = newActiveApp != _currentActiveApp;
                bool websiteChanged = newActiveWebsite != _currentActiveWebsite;

                if (appChanged)
                {
                    // Record time for previous app
                    if (!string.IsNullOrEmpty(_currentActiveApp))
                    {
                        RecordTimeForCurrentApp();
                    }

                    // Start tracking new app
                    _currentActiveApp = newActiveApp;
                    _currentSessionStartTime = DateTime.Now;

                    // Ensure app exists and update its session info
                    EnsureAppExists(activeWindow);
                    var app = _apps[_currentActiveApp];
                    app.SessionCount++;
                    app.LastActiveTime = DateTime.Now;
                    app.LastSeen = DateTime.Now;
                    
                    // Update daily sessions
                    if (!app.DailySessions.ContainsKey(DateTime.Today))
                    {
                        app.DailySessions[DateTime.Today] = 0;
                    }
                    app.DailySessions[DateTime.Today]++;
                }

                // Handle website changes (only for browsers)
                if (!string.IsNullOrEmpty(newActiveWebsite))
                {
                    if (websiteChanged)
                    {
                        // Record time for previous website
                        if (!string.IsNullOrEmpty(_currentActiveWebsite))
                        {
                            RecordTimeForCurrentWebsite();
                        }

                        // Start tracking new website
                        _currentActiveWebsite = newActiveWebsite;
                        _currentWebsiteSessionStartTime = DateTime.Now;

                        // Ensure website exists and update its session info
                        EnsureWebsiteExists(browserInfo!);
                        var website = _websites[_currentActiveWebsite];
                        website.SessionCount++;
                        website.LastActiveTime = DateTime.Now;
                        website.LastSeen = DateTime.Now;
                        
                        // Update daily sessions
                        if (!website.DailySessions.ContainsKey(DateTime.Today))
                        {
                            website.DailySessions[DateTime.Today] = 0;
                        }
                        website.DailySessions[DateTime.Today]++;
                    }
                    }
                else
                {
                    // Not a browser, clear current website if set
                    if (!string.IsNullOrEmpty(_currentActiveWebsite))
                    {
                        RecordTimeForCurrentWebsite();
                        _currentActiveWebsite = string.Empty;
                    }
                }

                // Time is added only on transitions (app/website change, idle, save) – not every tick.
                // Current session is included in UpdateHierarchicalData via (Now - _currentSessionStartTime).
                // This avoids timer jitter and lag affecting totals.

                // Still refresh UI when active so "live" current session shows
                UpdateHierarchicalData();
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScreenTimeService: Error in OnTrackingTimerElapsed: {ex}");
            }
        }

        private void HandleIdleState()
        {
            if (!_isUserIdle)
            {
                // Transitioning to idle
                RecordTimeForCurrentApp();
                RecordTimeForCurrentWebsite();
                SaveData(); // Save data before going idle
                _isUserIdle = true;
                _currentActiveApp = string.Empty;
                _currentActiveWebsite = string.Empty;
                System.Diagnostics.Debug.WriteLine("User is idle. Tracking paused.");
            }
        }

        private void HandleActiveState()
        {
            if (_isUserIdle)
            {
                // Transitioning from idle to active
                _isUserIdle = false;
                _currentSessionStartTime = DateTime.Now;
                _currentWebsiteSessionStartTime = DateTime.Now;
                System.Diagnostics.Debug.WriteLine("User is active. Tracking resumed.");
            }
        }

        private void OnSaveTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (!_isTracking || _isUserIdle) return;
            SaveData();
        }

        private void RecordTimeForCurrentApp()
        {
            if (string.IsNullOrEmpty(_currentActiveApp) || !_apps.ContainsKey(_currentActiveApp))
                return;

            var sessionDuration = DateTime.Now - _currentSessionStartTime;
            if (sessionDuration.TotalSeconds < 1) return; // Ignore very short sessions

            var app = _apps[_currentActiveApp];
            app.TotalTime += sessionDuration;

            // Update daily times
            if (!app.DailyTimes.ContainsKey(DateTime.Today))
            {
                app.DailyTimes[DateTime.Today] = TimeSpan.Zero;
            }
            app.DailyTimes[DateTime.Today] += sessionDuration;

            UpdateHierarchicalData();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RecordTimeForCurrentWebsite()
        {
            if (string.IsNullOrEmpty(_currentActiveWebsite) || !_websites.ContainsKey(_currentActiveWebsite))
                return;

            var sessionDuration = DateTime.Now - _currentWebsiteSessionStartTime;
            if (sessionDuration.TotalSeconds < 1) return; // Ignore very short sessions

            var website = _websites[_currentActiveWebsite];
            website.TotalTime += sessionDuration;

            // Update daily times
            if (!website.DailyTimes.ContainsKey(DateTime.Today))
            {
                website.DailyTimes[DateTime.Today] = TimeSpan.Zero;
            }
            website.DailyTimes[DateTime.Today] += sessionDuration;

            UpdateHierarchicalData();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        private void EnsureAppExists(Win32ApiService.ActiveWindowInfo windowInfo)
        {
            string appName = windowInfo.ProcessName;
            
            if (!_apps.ContainsKey(appName))
            {
                // Auto-detect category for new apps
                var detectedCategory = _categoryService.AutoDetectCategory(appName);
                _categoryService.SetCategoryForApp(appName, detectedCategory);
                
                _apps[appName] = new AppScreenTime
                {
                    AppName = appName,
                    Category = detectedCategory,
                    ProcessPath = windowInfo.ProcessPath,
                    FirstSeen = DateTime.Now,
                    LastSeen = DateTime.Now,
                    LastActiveTime = DateTime.Now
                };
            }
        }

        private void EnsureWebsiteExists(BrowserTrackingService.BrowserInfo browserInfo)
        {
            string domain = BrowserTrackingService.NormalizeDomain(browserInfo.Domain);
            
            if (!_websites.ContainsKey(domain))
            {
                // Auto-detect category for new websites
                var detectedCategory = _categoryService.AutoDetectWebsiteCategory(domain);
                _categoryService.SetCategoryForWebsite(domain, detectedCategory);
                
                _websites[domain] = new WebsiteScreenTime
                {
                    Domain = domain,
                    Category = detectedCategory,
                    FirstSeen = DateTime.Now,
                    LastSeen = DateTime.Now,
                    LastActiveTime = DateTime.Now,
                    FaviconUrl = $"https://www.google.com/s2/favicons?domain={domain}&sz=32"
                };
            }
        }

        private void UpdateHierarchicalData()
        {
            // Convert AppScreenTime data to hierarchical structure
            var today = DateTime.Today;
            var year = today.Year;
            var month = today.Month;
            var week = GetIso8601WeekOfYear(today);

            // Ensure hierarchical structure exists
            if (!_screenTimeData.Years.ContainsKey(year))
            {
                _screenTimeData.Years[year] = new YearData { Year = year };
            }
            if (!_screenTimeData.Years[year].Months.ContainsKey(month))
            {
                _screenTimeData.Years[year].Months[month] = new MonthData { Month = month };
            }
            if (!_screenTimeData.Years[year].Months[month].Weeks.ContainsKey(week))
            {
                _screenTimeData.Years[year].Months[month].Weeks[week] = new WeekData { WeekNumber = week };
            }
            if (!_screenTimeData.Years[year].Months[month].Weeks[week].Days.ContainsKey(today))
            {
                _screenTimeData.Years[year].Months[month].Weeks[week].Days[today] = new DayData { Date = today };
            }

            var dayData = _screenTimeData.Years[year].Months[month].Weeks[week].Days[today];
            dayData.Apps.Clear();
            dayData.Websites.Clear();

            // Include current (uncommitted) session in totals so display is accurate
            var currentAppSession = TimeSpan.Zero;
            var currentWebsiteSession = TimeSpan.Zero;
            if (_isTracking && !_isUserIdle)
            {
                if (!string.IsNullOrEmpty(_currentActiveApp))
                    currentAppSession = DateTime.Now - _currentSessionStartTime;
                if (!string.IsNullOrEmpty(_currentActiveWebsite))
                    currentWebsiteSession = DateTime.Now - _currentWebsiteSessionStartTime;
            }

            // Update day data from apps (committed time + current session if this is the active app)
            foreach (var app in _apps.Values)
            {
                var todayTime = app.DailyTimes.TryGetValue(today, out var t) ? t : TimeSpan.Zero;
                if (app.AppName == _currentActiveApp)
                    todayTime += currentAppSession;
                if (todayTime > TimeSpan.Zero || app.AppName == _currentActiveApp)
                {
                    dayData.Apps[app.AppName] = new AppDailyData
                    {
                        AppName = app.AppName,
                        ProcessPath = app.ProcessPath,
                        TotalTime = todayTime,
                        SessionCount = app.TodaysSessionCount,
                        FirstSeen = app.FirstSeen,
                        LastSeen = app.LastSeen,
                        LastActiveTime = app.LastActiveTime
                    };
                }
            }

            // Update day data from websites (committed + current session if active)
            foreach (var website in _websites.Values)
            {
                var todayTime = website.DailyTimes.TryGetValue(today, out var t) ? t : TimeSpan.Zero;
                if (website.Domain == _currentActiveWebsite)
                    todayTime += currentWebsiteSession;
                if (todayTime > TimeSpan.Zero || website.Domain == _currentActiveWebsite)
                {
                    dayData.Websites[website.Domain] = new WebsiteDailyData
                    {
                        Domain = website.Domain,
                        TotalTime = todayTime,
                        SessionCount = website.TodaysSessionCount,
                        FirstSeen = website.FirstSeen,
                        LastSeen = website.LastSeen,
                        LastActiveTime = website.LastActiveTime,
                        FaviconUrl = website.FaviconUrl
                    };
                }
            }

            // Update totals
            UpdateHierarchicalTotals();
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    var json = File.ReadAllText(_dataFilePath);
                    _screenTimeData = JsonConvert.DeserializeObject<ScreenTimeData>(json) ?? new ScreenTimeData();

                    // Convert hierarchical data to AppScreenTime objects
                    foreach (var yearData in _screenTimeData.Years.Values)
                    {
                        foreach (var monthData in yearData.Months.Values)
                        {
                            foreach (var weekData in monthData.Weeks.Values)
                            {
                                foreach (var dayData in weekData.Days.Values)
                                {
                                    foreach (var appData in dayData.Apps.Values)
                                    {
                                        if (!_apps.ContainsKey(appData.AppName))
                                        {
                                                                                    _apps[appData.AppName] = new AppScreenTime
                                        {
                                            AppName = appData.AppName,
                                            Category = appData.Category ?? _categoryService.GetCategoryForApp(appData.AppName),
                                            ProcessPath = appData.ProcessPath,
                                            FirstSeen = appData.FirstSeen,
                                            LastSeen = appData.LastSeen,
                                            LastActiveTime = appData.LastActiveTime
                                        };
                                        }

                                        var app = _apps[appData.AppName];
                                        app.DailyTimes[dayData.Date] = appData.TotalTime;
                                        app.DailySessions[dayData.Date] = appData.SessionCount;
                                        app.TotalTime += appData.TotalTime;
                                    }

                                    // Load website data
                                    foreach (var websiteData in dayData.Websites.Values)
                                    {
                                        if (!_websites.ContainsKey(websiteData.Domain))
                                        {
                                            _websites[websiteData.Domain] = new WebsiteScreenTime
                                            {
                                                Domain = websiteData.Domain,
                                                FirstSeen = websiteData.FirstSeen,
                                                LastSeen = websiteData.LastSeen,
                                                LastActiveTime = websiteData.LastActiveTime,
                                                FaviconUrl = websiteData.FaviconUrl
                                            };
                                        }

                                        var website = _websites[websiteData.Domain];
                                        website.DailyTimes[dayData.Date] = websiteData.TotalTime;
                                        website.DailySessions[dayData.Date] = websiteData.SessionCount;
                                        website.TotalTime += websiteData.TotalTime;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
                _screenTimeData = new ScreenTimeData();
                _apps.Clear();
            }
        }

        private void SaveData()
        {
            try
            {
                // Commit current session to stored totals before saving (transition-based timing)
                if (_isTracking && !_isUserIdle)
                {
                    RecordTimeForCurrentApp();
                    RecordTimeForCurrentWebsite();
                    _currentSessionStartTime = DateTime.Now;
                    _currentWebsiteSessionStartTime = DateTime.Now;
                }
                UpdateHierarchicalData(); // Ensure hierarchical data is up to date
                var directory = Path.GetDirectoryName(_dataFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(_screenTimeData, Formatting.Indented);
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
            _trackingTimer.Dispose();
            _saveTimer.Dispose();
        }

        public IEnumerable<AppScreenTime> GetAllApps() => _apps.Values;

        public AppScreenTime? GetApp(string appName) => 
            _apps.TryGetValue(appName, out var app) ? app : null;

        public IEnumerable<WebsiteScreenTime> GetAllWebsites() => _websites.Values;

        public WebsiteScreenTime? GetWebsite(string domain) => 
            _websites.TryGetValue(domain, out var website) ? website : null;

        private static int GetIso8601WeekOfYear(DateTime date)
        {
            var thursday = date.AddDays(3 - ((int)date.DayOfWeek + 6) % 7);
            return (thursday.DayOfYear - 1) / 7 + 1;
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
            var dayData = weekData.Days[today];

            // Update day totals
            dayData.TotalTime = TimeSpan.FromMilliseconds(dayData.Apps.Values.Sum(a => a.TotalTime.TotalMilliseconds));
            dayData.TotalSwitches = dayData.Apps.Values.Sum(a => a.SessionCount);
            dayData.TotalApps = dayData.Apps.Count;

            // Update week totals
            weekData.TotalTime = TimeSpan.FromMilliseconds(weekData.Days.Values.Sum(d => d.TotalTime.TotalMilliseconds));
            weekData.TotalSwitches = weekData.Days.Values.Sum(d => d.TotalSwitches);
            weekData.TotalApps = weekData.Days.Values.Max(d => d.TotalApps);

            // Update month totals
            monthData.TotalTime = TimeSpan.FromMilliseconds(monthData.Weeks.Values.Sum(w => w.TotalTime.TotalMilliseconds));
            monthData.TotalSwitches = monthData.Weeks.Values.Sum(w => w.TotalSwitches);
            monthData.TotalApps = monthData.Weeks.Values.Max(w => w.TotalApps);

            // Update year totals
            yearData.TotalTime = TimeSpan.FromMilliseconds(yearData.Months.Values.Sum(m => m.TotalTime.TotalMilliseconds));
            yearData.TotalSwitches = yearData.Months.Values.Sum(m => m.TotalSwitches);
            yearData.TotalApps = yearData.Months.Values.Max(m => m.TotalApps);
        }

        // Add these methods to maintain compatibility with existing code
        public ScreenTimeData GetScreenTimeData() => _screenTimeData;

        /// <summary>
        /// Reloads from screentime_data.json so upload uses the file as source of truth. Call before upload
        /// so we don't upload empty data when the file has content but in-memory state was out of sync.
        /// </summary>
        public void PrepareDataForUpload()
        {
            LoadData();
        }

        public void ResetAllData()
        {
            _apps.Clear();
            _websites.Clear();
            _screenTimeData = new ScreenTimeData();
            SaveData();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetAppData(string appName)
        {
            if (_apps.ContainsKey(appName))
            {
                _apps.Remove(appName);
                UpdateHierarchicalData();
                SaveData();
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ResetWebsiteData(string domain)
        {
            if (_websites.ContainsKey(domain))
            {
                _websites.Remove(domain);
                UpdateHierarchicalData();
                SaveData();
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ResetAllWebsiteData()
        {
            _websites.Clear();
            UpdateHierarchicalData();
            SaveData();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        // Category-related methods
        public CategoryService GetCategoryService() => _categoryService;

        public IEnumerable<AppScreenTime> GetAppsByCategory(string category)
        {
            return _apps.Values.Where(app => app.Category == category);
        }

        public void UpdateAppCategory(string appName, string category)
        {
            if (_apps.TryGetValue(appName, out var app))
            {
                app.Category = category;
                _categoryService.SetCategoryForApp(appName, category);
                UpdateHierarchicalData();
                SaveData();
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RefreshAppCategories()
        {
            foreach (var app in _apps.Values)
            {
                app.Category = _categoryService.GetCategoryForApp(app.AppName);
            }
            UpdateHierarchicalData();
            SaveData();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RefreshWebsiteCategories()
        {
            foreach (var website in _websites.Values)
            {
                website.Category = _categoryService.GetCategoryForWebsite(website.Domain);
            }
            UpdateHierarchicalData();
            SaveData();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<WebsiteScreenTime> GetWebsitesByCategory(string category)
        {
            return _websites.Values.Where(website => website.Category == category);
        }
    }
}