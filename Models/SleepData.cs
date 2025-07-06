using System;
using System.Collections.Generic;
using System.Linq;

namespace chronos_screentime.Models
{
    public class SleepSession
    {
        public DateTime Started { get; set; }
        public DateTime Ended { get; set; }
        public int Duration { get; set; } // Duration in minutes
        
        public TimeSpan DurationTimeSpan => TimeSpan.FromMinutes(Duration);
        public string FormattedDuration => DurationTimeSpan.TotalHours >= 1 
            ? $"{(int)DurationTimeSpan.TotalHours}h {DurationTimeSpan.Minutes}m"
            : $"{DurationTimeSpan.Minutes}m";
    }

    public class DailySleepSummary
    {
        public DateTime Date { get; set; }
        public List<SleepSession> Sessions { get; set; } = new();
        public TimeSpan TotalSleepTime => TimeSpan.FromMinutes(Sessions.Sum(s => s.Duration));
        public int SessionCount => Sessions.Count;
        
        public string FormattedTotalTime => TotalSleepTime.TotalHours >= 1 
            ? $"{(int)TotalSleepTime.TotalHours}h {TotalSleepTime.Minutes}m"
            : $"{TotalSleepTime.Minutes}m";
            
        public string FormattedDetails => string.Join(" + ", Sessions.Select(s => s.FormattedDuration));
    }

    public class SleepData
    {
        public List<SleepSession> AllSessions { get; set; } = new();
        
        public List<DailySleepSummary> GetDailySummaries()
        {
            return AllSessions
                .GroupBy(s => GetLogicalDate(s.Started.ToLocalTime()))
                .Select(g => new DailySleepSummary
                {
                    Date = g.Key,
                    Sessions = g.OrderBy(s => s.Started).ToList()
                })
                .OrderByDescending(d => d.Date)
                .ToList();
        }
        
        /// <summary>
        /// Gets the logical date for sleep tracking.
        /// Evening sleep (9 PM - 4 AM) counts for the next day.
        /// Morning/afternoon sleep (4 AM - 9 PM) counts for the same day.
        /// </summary>
        private DateTime GetLogicalDate(DateTime dateTime)
        {
            // Evening sleep (9 PM - 11:59 PM) counts for the next day
            if (dateTime.Hour >= 21)
            {
                return dateTime.Date.AddDays(1);
            }
            // Early morning sleep (12 AM - 4 AM) also counts for the current day (which is the "next" day from evening)
            else if (dateTime.Hour >= 0 && dateTime.Hour < 4)
            {
                return dateTime.Date; // Keep as current date since evening sleep already moved to next day
            }
            // Morning/afternoon sleep (4 AM - 9 PM) counts for the same day
            else
            {
                return dateTime.Date;
            }
        }
        
        public DailySleepSummary? GetSummaryForDate(DateTime date)
        {
            return GetDailySummaries().FirstOrDefault(d => d.Date.Date == date.Date);
        }
        
        public TimeSpan GetTotalSleepForPeriod(DateTime startDate, DateTime endDate)
        {
            return TimeSpan.FromMinutes(AllSessions
                .Where(s => GetLogicalDate(s.Started.ToLocalTime()) >= startDate.Date && GetLogicalDate(s.Started.ToLocalTime()) <= endDate.Date)
                .Sum(s => s.Duration));
        }
    }
}
