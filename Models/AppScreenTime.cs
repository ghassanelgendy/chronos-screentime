namespace chronos_screentime.Models
{
    public class AppScreenTime
    {
        public string AppName { get; set; } = string.Empty;
        public TimeSpan TotalTime { get; set; }
        public string ProcessPath { get; set; } = string.Empty;
        public int SessionCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime LastActiveTime { get; set; }

        // Get today's time from the current day data
        public TimeSpan TodaysTime => TotalTime;

        // Get today's session count
        public int TodaysSessionCount => SessionCount;

        // Display today's time
        public string FormattedTotalTime => TodaysTime.ToString(@"hh\:mm\:ss");
        public string FormattedTotalTimeShort => TodaysTime.TotalHours >= 1
            ? $"{(int)TodaysTime.TotalHours}h {TodaysTime.Minutes}m"
            : $"{TodaysTime.Minutes}m {TodaysTime.Seconds}s";

        // These methods are no longer needed as we use the hierarchical data structure now
    }
}