using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace chronos_screentime.Models
{
    public class SleepNavigationNode : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;

        public string DisplayName { get; set; } = string.Empty;
        public object? Data { get; set; }
        public List<SleepNavigationNode> Children { get; set; } = new();
        public SleepNavigationNodeType NodeType { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum SleepNavigationNodeType
    {
        Year,
        Month,
        AllData
    }

    public class SleepYearData
    {
        public int Year { get; set; }
        public List<DailySleepSummary> Sessions { get; set; } = new();
        public TimeSpan TotalSleep => TimeSpan.FromMinutes(Sessions.Sum(s => s.TotalSleepTime.TotalMinutes));
        public int TotalSessions => Sessions.Sum(s => s.SessionCount);
    }

    public class SleepMonthData
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM");
        public List<DailySleepSummary> Sessions { get; set; } = new();
        public TimeSpan TotalSleep => TimeSpan.FromMinutes(Sessions.Sum(s => s.TotalSleepTime.TotalMinutes));
        public int TotalSessions => Sessions.Sum(s => s.SessionCount);
        public string FormattedTotalSleep
        {
            get
            {
                var total = TotalSleep;
                return total.TotalHours >= 1
                    ? $"{(int)total.TotalHours}h {total.Minutes}m"
                    : $"{total.Minutes}m";
            }
        }
    }
}
