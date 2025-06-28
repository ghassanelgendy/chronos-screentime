using System;

namespace chronos_screentime.Models
{
    public class AppScreenTime
    {
        public string AppName { get; set; } = string.Empty;
        public TimeSpan TotalTime { get; set; }
        public DateTime LastActiveTime { get; set; }
        public string ProcessPath { get; set; } = string.Empty;
        public int SessionCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }

        public string FormattedTotalTime => TotalTime.ToString(@"hh\:mm\:ss");
        public string FormattedTotalTimeShort => TotalTime.TotalHours >= 1 
            ? $"{(int)TotalTime.TotalHours}h {TotalTime.Minutes}m"
            : $"{TotalTime.Minutes}m {TotalTime.Seconds}s";
    }
} 