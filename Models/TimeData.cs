using Newtonsoft.Json;

namespace chronos_screentime.Models
{
    public class YearData
    {
        public int Year { get; set; }
        public Dictionary<int, MonthData> Months { get; set; } = new();
        public TimeSpan TotalTime { get; set; }
        public int TotalSwitches { get; set; }
        public int TotalApps { get; set; }
    }

    public class MonthData
    {
        public int Month { get; set; }
        public Dictionary<int, WeekData> Weeks { get; set; } = new();
        public TimeSpan TotalTime { get; set; }
        public int TotalSwitches { get; set; }
        public int TotalApps { get; set; }
    }

    public class WeekData
    {
        public int WeekNumber { get; set; }
        public Dictionary<DateTime, DayData> Days { get; set; } = new();
        public TimeSpan TotalTime { get; set; }
        public int TotalSwitches { get; set; }
        public int TotalApps { get; set; }
    }

    public class DayData
    {
        public DateTime Date { get; set; }
        public Dictionary<string, AppDailyData> Apps { get; set; } = new();
        public Dictionary<string, WebsiteDailyData> Websites { get; set; } = new();
        public TimeSpan TotalTime { get; set; }
        public int TotalSwitches { get; set; }
        public int TotalApps { get; set; }
    }

    public class AppDailyData
    {
        public string AppName { get; set; } = string.Empty;
        public string ProcessPath { get; set; } = string.Empty;
        public TimeSpan TotalTime { get; set; }
        public int SessionCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime LastActiveTime { get; set; }
    }

    public class WebsiteDailyData
    {
        public string Domain { get; set; } = string.Empty;
        public TimeSpan TotalTime { get; set; }
        public int SessionCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime LastActiveTime { get; set; }
        public string? FaviconUrl { get; set; }
    }

    public class ScreenTimeData
    {
        public Dictionary<int, YearData> Years { get; set; } = new();
        public int CurrentYear { get; set; }
        public int CurrentMonth { get; set; }
        public int CurrentWeek { get; set; }
        public DateTime CurrentDate { get; set; }
        
        public ScreenTimeData()
        {
            CurrentDate = DateTime.Today;
            CurrentYear = CurrentDate.Year;
            CurrentMonth = CurrentDate.Month;
            CurrentWeek = GetIso8601WeekOfYear(CurrentDate);
        }

        private static int GetIso8601WeekOfYear(DateTime date)
        {
            var thursday = date.AddDays(3 - ((int)date.DayOfWeek + 6) % 7);
            return (thursday.DayOfYear - 1) / 7 + 1;
        }
    }
} 