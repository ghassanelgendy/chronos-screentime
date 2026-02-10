using Newtonsoft.Json;

namespace chronos_screentime.Models
{
    public class AppSettings
    {
        // Break Notification Settings
        public bool EnableBreakNotifications { get; set; } = false;
        public int BreakReminderMinutes { get; set; } = 30;



        // General Settings
        public bool AlwaysOnTop { get; set; } = false;
        public bool ShowInSystemTray { get; set; } = true;
        public bool HideTitleBar { get; set; } = false;
        public string StartWithWindows { get; set; } = "No"; // "Yes", "No", "Minimized"

        // Auto Export Settings
        public bool EnableAutoExport { get; set; } = false;
        public int AutoExportHours { get; set; } = 24;
        public string AutoExportLocation { get; set; } = @"C:\Users\%USERNAME%\Documents\ChronosExports";

        // Data Management
        public bool GracefulQuitting { get; set; } = true;
        public bool MergeConsecutiveEntries { get; set; } = false;
        public int KeepDataMonths { get; set; } = 3;

        // Tracking Settings
        public bool TrackAllApplications { get; set; } = true;
        public bool TrackIdleTime { get; set; } = false;
        public bool TrackSubProcesses { get; set; } = false;
        public bool TrackAppSwitchCount { get; set; } = true;
        public bool AdvancedProcessTreeAnalysis { get; set; } = false;
        public int IdleTimeMinutes { get; set; } = 5;
        public bool AdvancedIdleDetection { get; set; } = false;
        public bool DetectFullscreenApps { get; set; } = false;
        public double UpdateFrequencySeconds { get; set; } = 1.0;
        public bool IgnoreShortSessions { get; set; } = true;

        // Notification & Sound Settings
        public bool PlaySoundForNotifications { get; set; } = true;
        public bool PlaySoundOnAppSwitch { get; set; } = false;
        public int NotificationVolume { get; set; } = 50;
        public string NotificationSoundFile { get; set; } = "sneeze.wav";

        // Auto Logout Settings
        public bool EnableAutoLogout { get; set; } = false;
        public int AutoLogoutMinutes { get; set; } = 120;

        // Motivational System
        public bool ShowMotivationalMessages { get; set; } = false;
        public string MessageFrequency { get; set; } = "Every 2 hours";

        // Display & Appearance
        public string Theme { get; set; } = "Light Theme";
        public bool ShowAnimatedCharts { get; set; } = true;
        public bool ShowLiveDashboard { get; set; } = true;
        public bool EnableChartTooltips { get; set; } = true;
        public double MainWindowRefreshSeconds { get; set; } = 1.0;
        public bool ShowAppIcons { get; set; } = true;
        public bool ShowDetailedTooltips { get; set; } = true;
        public string TimeFormat { get; set; } = "Hours and Minutes (2h 30m)";
        public bool RememberWindowPosition { get; set; } = true;
        public bool RememberWindowSize { get; set; } = true;
        public bool StartMinimized { get; set; } = false;

        // Goals & Productivity
        public bool EnableDailyGoals { get; set; } = false;
        public int ProductiveTimeGoalHours { get; set; } = 6;
        public bool EnableProductivityScoring { get; set; } = false;
        public bool EnableDistractionBlocking { get; set; } = false;

        // Update Suppression Settings
        public DateTime? LastUpdateDismissedDate { get; set; } = null;
        public DateTime? LastUpdateCancelledDate { get; set; } = null;
        public int AutoUpdateSuppressionDays { get; set; } = 7; // Default 7 days for "Later"
        public int CancelUpdateSuppressionDays { get; set; } = 30; // 30 days for "Cancel"

        // Power Scheduling Settings (Non-persistent)
        public bool EnablePowerScheduling { get; set; } = false;
        public int PowerScheduleHours { get; set; } = 0;
        public int PowerScheduleMinutes { get; set; } = 30;
        public string PowerScheduleAction { get; set; } = "Shutdown"; // "Shutdown" or "Restart"

        // Navigation Tab Visibility Settings
        public bool ShowTodayTab { get; set; } = true;
        public bool ShowYesterdayTab { get; set; } = true;
        public bool ShowThisWeekTab { get; set; } = true;
        public bool ShowLastWeekTab { get; set; } = true;
        public bool ShowThisMonthTab { get; set; } = true;
        public bool ShowSleepTab { get; set; } = true;
        public bool ShowWebBrowsingTab { get; set; } = true;
        public bool ShowDevelopmentTab { get; set; } = true;
        public bool ShowGamingTab { get; set; } = true;
        public bool ShowCommunicationTab { get; set; } = true;
        public bool ShowProductivityTab { get; set; } = true;
        public bool ShowEntertainmentTab { get; set; } = true;

        // Custom category tab visibility
        public Dictionary<string, bool> ShowCustomCategoryTabs { get; set; } = new Dictionary<string, bool>();

        // Supabase Sync Settings
        public bool EnableSupabaseSync { get; set; } = false;
        public string SupabaseUrl { get; set; } = string.Empty;
        public string SupabaseAnonKey { get; set; } = string.Empty;
        public string SupabaseUserId { get; set; } = string.Empty;
        public int SupabaseUploadIntervalMinutes { get; set; } = 30;

        // Chart Filtering Settings
        public bool ShowUncategorizedInCharts { get; set; } = true;
        public bool ShowDevelopmentInCharts { get; set; } = true;
        public bool ShowGamingInCharts { get; set; } = true;
        public bool ShowCommunicationInCharts { get; set; } = true;
        public bool ShowProductivityInCharts { get; set; } = true;
        public bool ShowEntertainmentInCharts { get; set; } = true;
        public bool ShowCustomCategoriesInCharts { get; set; } = true;
        public bool EnableChartAnimations { get; set; } = true;

        // Default constructor
        public AppSettings()
        {
        }

        // Create a deep copy of settings
        public AppSettings Clone()
        {
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
        }
    }
}