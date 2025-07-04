using System;
using System.Collections.Generic;
using System.Linq;

namespace chronos_screentime.Models
{
    public class WebsiteScreenTime
    {
        public string Domain { get; set; } = string.Empty;
        public TimeSpan TotalTime { get; set; } // Cumulative time (all days)
        public Dictionary<DateTime, TimeSpan> DailyTimes { get; set; } = new(); // Historical daily data
        public Dictionary<DateTime, int> DailySessions { get; set; } = new(); // Historical daily session counts
        public DateTime LastActiveTime { get; set; }
        public int SessionCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public string? FaviconUrl { get; set; } // Optional favicon URL for display

        // Get today's time
        public TimeSpan TodaysTime => DailyTimes.TryGetValue(DateTime.Today, out var time) ? time : TimeSpan.Zero;
        
        // Get today's session count
        public int TodaysSessionCount => DailySessions.TryGetValue(DateTime.Today, out var count) ? count : 0;

        // Get time for a specific date
        public TimeSpan GetTimeForDate(DateTime date) => DailyTimes.TryGetValue(date.Date, out var time) ? time : TimeSpan.Zero;
        
        // Get session count for a specific date
        public int GetSessionsForDate(DateTime date) => DailySessions.TryGetValue(date.Date, out var count) ? count : 0;

        // Get week total for a given week start date
        public TimeSpan GetWeekTotal(DateTime weekStart)
        {
            var total = TimeSpan.Zero;
            for (int i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);
                if (DailyTimes.TryGetValue(date, out var time))
                {
                    total = total.Add(time);
                }
            }
            return total;
        }

        // Get month total for a given year and month
        public TimeSpan GetMonthTotal(int year, int month)
        {
            var total = TimeSpan.Zero;
            var daysInMonth = DateTime.DaysInMonth(year, month);
            
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(year, month, day);
                if (DailyTimes.TryGetValue(date, out var time))
                {
                    total = total.Add(time);
                }
            }
            return total;
        }

        // Display today's time
        public string FormattedTotalTime => TodaysTime.ToString(@"hh\:mm\:ss");
        public string FormattedTotalTimeShort => TodaysTime.TotalHours >= 1 
            ? $"{(int)TodaysTime.TotalHours}h {TodaysTime.Minutes}m"
            : $"{TodaysTime.Minutes}m {TodaysTime.Seconds}s";

        // Display friendly domain name (removes www. prefix if present)
        public string DisplayName => Domain.StartsWith("www.") ? Domain[4..] : Domain;
    }
} 