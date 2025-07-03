using System;
using System.Collections.Generic;
using System.Linq;

namespace chronos_screentime.Models
{
    public class AppScreenTime
    {
        public string AppName { get; set; } = string.Empty;
        public TimeSpan TotalTime { get; set; } // Cumulative time (all days)
        public Dictionary<DateTime, TimeSpan> DailyTimes { get; set; } = new(); // Historical daily data
        public Dictionary<DateTime, int> DailySessions { get; set; } = new(); // Historical daily session counts
        public DateTime LastActiveTime { get; set; }
        public string ProcessPath { get; set; } = string.Empty;
        public int SessionCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }

        // Get today's time
        public TimeSpan TodaysTime => DailyTimes.TryGetValue(DateTime.Today, out var time) ? time : TimeSpan.Zero;
        
        // Get today's session count
        public int TodaysSessionCount => DailySessions.TryGetValue(DateTime.Today, out var count) ? count : 0;

        // Get time for a specific date
        public TimeSpan GetTimeForDate(DateTime date) => DailyTimes.TryGetValue(date.Date, out var time) ? time : TimeSpan.Zero;
        
        // Get session count for a specific date
        public int GetSessionsForDate(DateTime date) => DailySessions.TryGetValue(date.Date, out var count) ? count : 0;

        // Display today's time
        public string FormattedTotalTime => TodaysTime.ToString(@"hh\:mm\:ss");
        public string FormattedTotalTimeShort => TodaysTime.TotalHours >= 1 
            ? $"{(int)TodaysTime.TotalHours}h {TodaysTime.Minutes}m"
            : $"{TodaysTime.Minutes}m {TodaysTime.Seconds}s";

        // Helper methods for getting historical data
        public TimeSpan GetWeekTotal(DateTime weekStart)
        {
            var total = TimeSpan.Zero;
            for (int i = 0; i < 7; i++)
            {
                total += GetTimeForDate(weekStart.AddDays(i));
            }
            return total;
        }

        public TimeSpan GetMonthTotal(int year, int month)
        {
            return TimeSpan.FromMilliseconds(DailyTimes
                .Where(kvp => kvp.Key.Year == year && kvp.Key.Month == month)
                .Sum(kvp => kvp.Value.TotalMilliseconds));
        }
    }
}