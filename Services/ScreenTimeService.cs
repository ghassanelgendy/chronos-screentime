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
        private readonly System.Timers.Timer _trackingTimer;
        private readonly string _dataFilePath;
        private readonly Dictionary<string, AppScreenTime> _apps;
        private readonly Dictionary<string, WebsiteScreenTime> _websites;

        private string _currentActiveApp = string.Empty;
        private string _currentActiveWebsite = string.Empty;
        private DateTime _currentSessionStartTime;
        private DateTime _currentWebsiteSessionStartTime;
        private bool _isTracking = false;
        private ScreenTimeData _screenTimeData;

        public event EventHandler? DataChanged;

        public ScreenTimeService()
        {
            _apps = new Dictionary<string, AppScreenTime>();
            _websites = new Dictionary<string, WebsiteScreenTime>();
            _screenTimeData = new ScreenTimeData();
            _win32ApiService = new Win32ApiService();
            _browserTrackingService = new BrowserTrackingService();
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
            _currentWebsiteSessionStartTime = DateTime.Now;
            _trackingTimer.Start();

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
            if (!_isTracking) return;

            var activeWindow = _win32ApiService.GetActiveWindow();
            if (activeWindow == null) return;

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
                else if (!string.IsNullOrEmpty(_currentActiveWebsite))
                {
                    // Update time for current website
                    var website = _websites[_currentActiveWebsite];
                    var currentDuration = DateTime.Now - _currentWebsiteSessionStartTime;
                    
                    // Update daily times
                    if (!website.DailyTimes.ContainsKey(DateTime.Today))
                    {
                        website.DailyTimes[DateTime.Today] = TimeSpan.Zero;
                    }
                    website.DailyTimes[DateTime.Today] = TimeSpan.FromMilliseconds(
                        website.DailyTimes[DateTime.Today].TotalMilliseconds + currentDuration.TotalMilliseconds
                    );
                    website.TotalTime = TimeSpan.FromMilliseconds(
                        website.TotalTime.TotalMilliseconds + currentDuration.TotalMilliseconds
                    );
                    
                    website.LastActiveTime = DateTime.Now;
                    website.LastSeen = DateTime.Now;
                    
                    _currentWebsiteSessionStartTime = DateTime.Now;
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

            // Update time for current app if it hasn't changed
            if (!appChanged && !string.IsNullOrEmpty(_currentActiveApp))
            {
                var app = _apps[_currentActiveApp];
                var currentDuration = DateTime.Now - _currentSessionStartTime;
                
                // Update daily times
                if (!app.DailyTimes.ContainsKey(DateTime.Today))
                {
                    app.DailyTimes[DateTime.Today] = TimeSpan.Zero;
                }
                app.DailyTimes[DateTime.Today] = TimeSpan.FromMilliseconds(
                    app.DailyTimes[DateTime.Today].TotalMilliseconds + currentDuration.TotalMilliseconds
                );
                app.TotalTime = TimeSpan.FromMilliseconds(
                    app.TotalTime.TotalMilliseconds + currentDuration.TotalMilliseconds
                );
                
                app.LastActiveTime = DateTime.Now;
                app.LastSeen = DateTime.Now;
                
                _currentSessionStartTime = DateTime.Now;
            }

            // Update hierarchical data and notify listeners
            if (appChanged || websiteChanged)
            {
                UpdateHierarchicalData();
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
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
                _apps[appName] = new AppScreenTime
                {
                    AppName = appName,
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
                _websites[domain] = new WebsiteScreenTime
                {
                    Domain = domain,
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

            // Update day data from apps
            foreach (var app in _apps.Values)
            {
                if (app.DailyTimes.TryGetValue(today, out var todayTime))
                {
                    var appDaily = new AppDailyData
                    {
                        AppName = app.AppName,
                        ProcessPath = app.ProcessPath,
                        TotalTime = todayTime,
                        SessionCount = app.TodaysSessionCount,
                        FirstSeen = app.FirstSeen,
                        LastSeen = app.LastSeen,
                        LastActiveTime = app.LastActiveTime
                    };
                    dayData.Apps[app.AppName] = appDaily;
                }
            }

            // Update day data from websites
            foreach (var website in _websites.Values)
            {
                if (website.DailyTimes.TryGetValue(today, out var todayTime))
                {
                    var websiteDaily = new WebsiteDailyData
                    {
                        Domain = website.Domain,
                        TotalTime = todayTime,
                        SessionCount = website.TodaysSessionCount,
                        FirstSeen = website.FirstSeen,
                        LastSeen = website.LastSeen,
                        LastActiveTime = website.LastActiveTime,
                        FaviconUrl = website.FaviconUrl
                    };
                    dayData.Websites[website.Domain] = websiteDaily;
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
    }
}